using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Portfolio.Contracts;
using PDS.Sales.Contracts;
using PDS.Sales.Domain;
using PDS.Shared.Data;

namespace PDS.Sales.Data;

internal sealed class UnitStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(SalesTable.LogicalName);

    public async Task<Unit?> GetAsync(ProjectId projectId, UnitId unitId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = UnitItemMapper.Key(projectId, unitId), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? UnitItemMapper.FromItem(item) : null;
    }

    public Task<IReadOnlyList<Unit>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        QueryAllAsync(
            new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(UnitItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(UnitItemMapper.UnitPrefix),
                },
                ConsistentRead = true,
            },
            ct);

    /// <summary>Every unit in every project, via gsi1 (eventually consistent: GSIs cannot be read consistently).</summary>
    public Task<IReadOnlyList<Unit>> ListAllAsync(CancellationToken ct) =>
        QueryAllAsync(
            new QueryRequest
            {
                TableName = Table,
                IndexName = SalesTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :all",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":all"] = Attr.S(UnitItemMapper.AllUnits),
                },
            },
            ct);

    public Task CreateAsync(Unit unit, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = UnitItemMapper.ToItem(unit),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Unit unit, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = UnitItemMapper.ToItem(unit),
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

    private async Task<IReadOnlyList<Unit>> QueryAllAsync(QueryRequest request, CancellationToken ct)
    {
        var units = new List<Unit>();
        do
        {
            var response = await dynamo.QueryAsync(request, ct);
            units.AddRange((response.Items ?? []).Select(UnitItemMapper.FromItem));
            request.ExclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (request.ExclusiveStartKey is not null);

        return units;
    }
}
