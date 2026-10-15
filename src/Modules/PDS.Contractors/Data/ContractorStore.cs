using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Shared.Data;

namespace PDS.Contractors.Data;

internal sealed class ContractorStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(ContractorsTable.LogicalName);

    public async Task<Contractor?> GetAsync(ContractorId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = ContractorItemMapper.Key(id), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? ContractorItemMapper.FromItem(item) : null;
    }

    /// <summary>All contractors via gsi1pk = CONTRACTOR (all pages).</summary>
    public async Task<IReadOnlyList<Contractor>> ListAllAsync(CancellationToken ct)
    {
        var contractors = new List<Contractor>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                IndexName = ContractorsTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":type"] = Attr.S(ContractorItemMapper.ContractorType),
                },
                ExclusiveStartKey = start,
            }, ct);
            contractors.AddRange((response.Items ?? []).Select(ContractorItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return contractors;
    }

    /// <summary>BatchGetItem in chunks of 100, retrying unprocessed keys. Duplicate ids are removed first because
    /// BatchGetItem rejects duplicate keys.</summary>
    public async Task<IReadOnlyList<Contractor>> GetManyAsync(IReadOnlyCollection<ContractorId> ids, CancellationToken ct)
    {
        var contractors = new List<Contractor>();
        foreach (var chunk in ids.Distinct().Chunk(100))
        {
            var pending = new Dictionary<string, KeysAndAttributes>
            {
                [Table] = new KeysAndAttributes { Keys = chunk.Select(ContractorItemMapper.Key).ToList(), ConsistentRead = true },
            };
            while (pending.Count > 0)
            {
                var response = await dynamo.BatchGetItemAsync(new BatchGetItemRequest { RequestItems = pending }, ct);
                if (response.Responses?.TryGetValue(Table, out var items) == true)
                    contractors.AddRange(items.Select(ContractorItemMapper.FromItem));
                pending = response.UnprocessedKeys is { Count: > 0 } unprocessed ? unprocessed : [];
            }
        }

        return contractors;
    }

    public Task CreateAsync(Contractor contractor, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = ContractorItemMapper.ToItem(contractor),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Contractor contractor, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = ContractorItemMapper.ToItem(contractor),
                ConditionExpression = "#version = :expected",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
                },
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
