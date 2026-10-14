using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Risks.Contracts;
using PDS.Risks.Domain;
using PDS.Shared.Data;

namespace PDS.Risks.Data;

internal sealed class RiskStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(RisksTable.LogicalName);

    public async Task<Risk?> GetAsync(ProjectId projectId, RiskId riskId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = RiskItemMapper.Key(projectId, riskId), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? RiskItemMapper.FromItem(item) : null;
    }

    public async Task<IReadOnlyList<Risk>> ListForProjectAsync(ProjectId projectId, CancellationToken ct)
    {
        var risks = new List<Risk>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(RiskItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(RiskItemMapper.RiskPrefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            risks.AddRange((response.Items ?? []).Select(RiskItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return risks;
    }

    public Task CreateAsync(Risk risk, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = RiskItemMapper.ToItem(risk),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Risk risk, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = RiskItemMapper.ToItem(risk),
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
