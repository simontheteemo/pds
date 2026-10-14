using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Costs.Contracts;
using PDS.Costs.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Costs.Data;

internal sealed class CostStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(CostsTable.LogicalName);

    public async Task<ProjectCostData> LoadProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var lines = new List<BudgetLine>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = Attr.S(CostKeys.Pk(projectId)) },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            foreach (var item in response.Items ?? [])
            {
                var sk = Attr.GetString(item, TableKeys.SortKey);
                if (sk.StartsWith(CostKeys.LinePrefix, StringComparison.Ordinal))
                    lines.Add(BudgetLineItemMapper.FromItem(item));
            }

            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return new ProjectCostData(lines);
    }

    public async Task<BudgetLine?> GetLineAsync(ProjectId projectId, BudgetLineId lineId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = Table,
            Key = CostKeys.Key(projectId, BudgetLineItemMapper.Sk(lineId)),
            ConsistentRead = true,
        }, ct);
        return response.Item is { Count: > 0 } item ? BudgetLineItemMapper.FromItem(item) : null;
    }

    public Task CreateLineAsync(BudgetLine line, CancellationToken ct) =>
        PutNewAsync(BudgetLineItemMapper.ToItem(line), ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> UpdateLineAsync(BudgetLine line, long expectedVersion, CancellationToken ct) =>
        PutVersionedAsync(BudgetLineItemMapper.ToItem(line), expectedVersion, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> DeleteLineAsync(ProjectId projectId, BudgetLineId lineId, long expectedVersion, CancellationToken ct) =>
        DeleteVersionedAsync(CostKeys.Key(projectId, BudgetLineItemMapper.Sk(lineId)), expectedVersion, ct);

    private Task PutNewAsync(Dictionary<string, AttributeValue> item, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = item,
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    private async Task<bool> PutVersionedAsync(Dictionary<string, AttributeValue> item, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = item,
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

    private async Task<bool> DeleteVersionedAsync(Dictionary<string, AttributeValue> key, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = Table,
                Key = key,
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
