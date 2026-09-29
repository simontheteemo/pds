using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

internal sealed class ProjectStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private const string ConditionalCheckFailed = "ConditionalCheckFailed";

    private string Table => names.For(PortfolioTable.LogicalName);

    public async Task<Project?> GetAsync(ProjectId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = ProjectItemMapper.Key(id), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? ProjectItemMapper.FromItem(item) : null;
    }

    /// <summary>Reads every project through the list index. Fine up to a few thousand projects (ADR-0002).</summary>
    public async Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct)
    {
        var projects = new List<Project>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                IndexName = PortfolioTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = Attr.S(ProjectItemMapper.ProjectType) },
                ExclusiveStartKey = start,
            }, ct);
            projects.AddRange((response.Items ?? []).Select(ProjectItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return projects;
    }

    /// <summary>BatchGetItem in chunks of 100 (the DynamoDB limit), retrying unprocessed keys. Duplicate ids are removed
    /// first because BatchGetItem rejects duplicate keys.</summary>
    public async Task<IReadOnlyList<Project>> GetManyAsync(IReadOnlyCollection<ProjectId> ids, CancellationToken ct)
    {
        var projects = new List<Project>();
        foreach (var chunk in ids.Distinct().Chunk(100))
        {
            var pending = new Dictionary<string, KeysAndAttributes>
            {
                [Table] = new KeysAndAttributes { Keys = chunk.Select(ProjectItemMapper.Key).ToList(), ConsistentRead = true },
            };
            while (pending.Count > 0)
            {
                var response = await dynamo.BatchGetItemAsync(new BatchGetItemRequest { RequestItems = pending }, ct);
                if (response.Responses?.TryGetValue(Table, out var items) == true)
                    projects.AddRange(items.Select(ProjectItemMapper.FromItem));
                pending = response.UnprocessedKeys is { Count: > 0 } unprocessed ? unprocessed : [];
            }
        }

        return projects;
    }

    public Task<SaveResult> CreateAsync(Project project, CancellationToken ct) =>
        TransactAsync(
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.ToItem(project), ConditionExpression = "attribute_not_exists(pk)" } },
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.CodeGuard(project), ConditionExpression = "attribute_not_exists(pk)" } },
            ],
            codeGuardIndex: 1,
            ct);

    /// <summary>Writes the project only if the stored version still equals <paramref name="expectedVersion"/>.
    /// When the code changed, the old code guard is released and the new one claimed in the same transaction.</summary>
    public async Task<SaveResult> UpdateAsync(Project project, long expectedVersion, string previousCode, CancellationToken ct)
    {
        var attributeNames = new Dictionary<string, string> { ["#version"] = "version" };
        var attributeValues = new Dictionary<string, AttributeValue>
        {
            [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
        };
        const string versionMatches = "#version = :expected";

        if (previousCode == project.Fields.Code)
        {
            try
            {
                await dynamo.PutItemAsync(new PutItemRequest
                {
                    TableName = Table,
                    Item = ProjectItemMapper.ToItem(project),
                    ConditionExpression = versionMatches,
                    ExpressionAttributeNames = attributeNames,
                    ExpressionAttributeValues = attributeValues,
                }, ct);
                return SaveResult.Saved;
            }
            catch (ConditionalCheckFailedException)
            {
                return SaveResult.VersionConflict;
            }
        }

        return await TransactAsync(
            [
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.ToItem(project), ConditionExpression = versionMatches, ExpressionAttributeNames = attributeNames, ExpressionAttributeValues = attributeValues } },
                new TransactWriteItem { Delete = new Delete { TableName = Table, Key = ProjectItemMapper.CodeGuardKey(previousCode) } },
                new TransactWriteItem { Put = new Put { TableName = Table, Item = ProjectItemMapper.CodeGuard(project), ConditionExpression = "attribute_not_exists(pk)" } },
            ],
            codeGuardIndex: 2,
            ct);
    }

    private async Task<SaveResult> TransactAsync(List<TransactWriteItem> items, int codeGuardIndex, CancellationToken ct)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = items }, ct);
            return SaveResult.Saved;
        }
        catch (TransactionCanceledException ex)
        {
            var reasons = ex.CancellationReasons ?? [];
            if (reasons.Count > 0 && reasons[0].Code == ConditionalCheckFailed)
                return SaveResult.VersionConflict;
            if (reasons.Count > codeGuardIndex && reasons[codeGuardIndex].Code == ConditionalCheckFailed)
                return SaveResult.CodeTaken;
            throw;
        }
    }
}
