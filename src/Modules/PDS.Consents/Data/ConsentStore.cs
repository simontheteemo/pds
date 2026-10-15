using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Consents.Contracts;
using PDS.Consents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

using Condition = PDS.Consents.Domain.Condition;

namespace PDS.Consents.Data;

internal sealed record ConsentWithConditions(Consent Consent, IReadOnlyList<Condition> Conditions);

internal sealed class ConsentStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(ConsentsTable.LogicalName);

    public async Task<IReadOnlyList<ConsentWithConditions>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, ConsentItemMapper.ConsentPrefix, ct));

    /// <returns>The consent with its conditions, or null when the consent item is absent.</returns>
    public async Task<ConsentWithConditions?> GetAsync(ProjectId projectId, ConsentId consentId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, ConsentItemMapper.SortKey(consentId), ct)).FirstOrDefault();

    /// <summary>Scans the whole table, grouped per consent. For the cross-project due list.</summary>
    public async Task<IReadOnlyList<ConsentWithConditions>> ScanAllAsync(CancellationToken ct)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.ScanAsync(new ScanRequest
            {
                TableName = Table,
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            items.AddRange(response.Items ?? []);
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return Group(items);
    }

    public Task CreateAsync(Consent consent, CancellationToken ct) =>
        PutNewAsync(ConsentItemMapper.ToItem(consent), ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> UpdateAsync(Consent consent, long expectedVersion, CancellationToken ct) =>
        PutVersionedAsync(ConsentItemMapper.ToItem(consent), expectedVersion, ct);

    public async Task<Condition?> GetConditionAsync(
        ProjectId projectId, ConsentId consentId, ConditionId conditionId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = Table,
            Key = ConditionItemMapper.Key(projectId, consentId, conditionId),
            ConsistentRead = true,
        }, ct);
        return response.Item is { Count: > 0 } item ? ConditionItemMapper.FromItem(item) : null;
    }

    public Task CreateConditionAsync(Condition condition, CancellationToken ct) =>
        PutNewAsync(ConditionItemMapper.ToItem(condition), ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> UpdateConditionAsync(Condition condition, long expectedVersion, CancellationToken ct) =>
        PutVersionedAsync(ConditionItemMapper.ToItem(condition), expectedVersion, ct);

    private async Task<List<Dictionary<string, AttributeValue>>> QueryAsync(
        ProjectId projectId, string sortKeyPrefix, CancellationToken ct)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(ConsentItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(sortKeyPrefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            items.AddRange(response.Items ?? []);
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return items;
    }

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

    /// <summary>
    /// Splits items by the itemType attribute. Conditions are attached to their consent by consentId;
    /// conditions whose consent item is missing are dropped (never throw).
    /// </summary>
    private static IReadOnlyList<ConsentWithConditions> Group(IEnumerable<Dictionary<string, AttributeValue>> items)
    {
        var consents = new List<Consent>();
        var conditions = new List<Condition>();
        foreach (var item in items)
        {
            switch (Attr.GetStringOrNull(item, ConsentItemMapper.ItemTypeAttr))
            {
                case ConsentItemMapper.ItemType:
                    consents.Add(ConsentItemMapper.FromItem(item));
                    break;
                case ConditionItemMapper.ItemType:
                    conditions.Add(ConditionItemMapper.FromItem(item));
                    break;
            }
        }

        var byConsent = conditions.ToLookup(c => c.ConsentId);
        return consents.Select(c => new ConsentWithConditions(c, byConsent[c.Id].ToList())).ToList();
    }
}
