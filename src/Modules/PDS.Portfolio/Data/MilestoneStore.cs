using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Portfolio.Domain;
using PDS.Shared.Data;

namespace PDS.Portfolio.Data;

internal sealed class MilestoneStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(PortfolioTable.LogicalName);

    public async Task<IReadOnlyList<Milestone>> ListForProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var milestones = new List<Milestone>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(MilestoneItemMapper.Pk(projectId)),
                    [":prefix"] = Attr.S(MilestoneItemMapper.SkPrefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            milestones.AddRange((response.Items ?? []).Select(MilestoneItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return milestones;
    }

    /// <summary>Every milestone in every project through the list index (for the cross-project programme).</summary>
    public async Task<IReadOnlyList<Milestone>> ListAllAsync(CancellationToken ct)
    {
        var milestones = new List<Milestone>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                IndexName = PortfolioTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :type",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":type"] = Attr.S(MilestoneItemMapper.MilestoneType) },
                ExclusiveStartKey = start,
            }, ct);
            milestones.AddRange((response.Items ?? []).Select(MilestoneItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return milestones;
    }

    public async Task<Milestone?> GetAsync(ProjectId projectId, MilestoneId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = Table,
            Key = MilestoneItemMapper.Key(projectId, id),
            ConsistentRead = true,
        }, ct);
        return response.Item is { Count: > 0 } item ? MilestoneItemMapper.FromItem(item) : null;
    }

    public Task CreateAsync(Milestone milestone, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = MilestoneItemMapper.ToItem(milestone),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Milestone milestone, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = MilestoneItemMapper.ToItem(milestone),
                ConditionExpression = "#version = :expected",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = Attr.N(expectedVersion) },
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> DeleteAsync(ProjectId projectId, MilestoneId id, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = Table,
                Key = MilestoneItemMapper.Key(projectId, id),
                ConditionExpression = "#version = :expected",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":expected"] = Attr.N(expectedVersion) },
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
