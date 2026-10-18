using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Actions.Contracts;
using PDS.Actions.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Actions.Data;

internal sealed class ActionStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(ActionsTable.LogicalName);

    public async Task<ActionItem?> GetAsync(ProjectId projectId, ActionId actionId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = ActionItemMapper.Key(projectId, actionId), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? ActionItemMapper.FromItem(item) : null;
    }

    public Task<IReadOnlyList<ActionItem>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        QueryAllAsync(new QueryRequest
        {
            TableName = Table,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = Attr.S(ActionItemMapper.PartitionKey(projectId)),
                [":prefix"] = Attr.S(ActionItemMapper.ActionPrefix),
            },
            ConsistentRead = true,
        }, ct);

    /// <summary>Open actions owned by <paramref name="ownerId"/> across all projects (gsi1; eventually consistent).</summary>
    public Task<IReadOnlyList<ActionItem>> ListOpenForOwnerAsync(string ownerId, CancellationToken ct) =>
        QueryAllAsync(new QueryRequest
        {
            TableName = Table,
            IndexName = ActionsTable.Gsi1,
            KeyConditionExpression = "gsi1pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = Attr.S(ActionItemMapper.OwnerPartition(ownerId)),
            },
        }, ct);

    /// <summary>
    /// Open actions due before <paramref name="today"/> across all projects (gsi2; eventually consistent).
    /// gsi2sk is "yyyy-MM-dd#id", so an action due today sorts after ":today" and is not returned.
    /// </summary>
    public Task<IReadOnlyList<ActionItem>> ListOverdueAsync(DateOnly today, CancellationToken ct) =>
        QueryAllAsync(new QueryRequest
        {
            TableName = Table,
            IndexName = ActionsTable.Gsi2,
            KeyConditionExpression = "gsi2pk = :open AND gsi2sk < :today",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":open"] = Attr.S(ActionItemMapper.OpenPartition),
                [":today"] = Attr.S(ActionItemMapper.SortableDate(today)),
            },
        }, ct);

    public Task CreateAsync(ActionItem action, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = ActionItemMapper.ToItem(action),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(ActionItem action, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = ActionItemMapper.ToItem(action),
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

    private async Task<IReadOnlyList<ActionItem>> QueryAllAsync(QueryRequest request, CancellationToken ct)
    {
        var actions = new List<ActionItem>();
        do
        {
            var response = await dynamo.QueryAsync(request, ct);
            actions.AddRange((response.Items ?? []).Select(ActionItemMapper.FromItem));
            request.ExclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (request.ExclusiveStartKey is not null);

        return actions;
    }
}
