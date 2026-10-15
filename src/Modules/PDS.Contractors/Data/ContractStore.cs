using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
using PDS.Contractors.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Contractors.Data;

internal sealed class ContractStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(ContractorsTable.LogicalName);

    public async Task<Contract?> GetAsync(ProjectId projectId, ContractId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = ContractItemMapper.Key(projectId, id), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? ContractItemMapper.FromItem(item) : null;
    }

    public Task<IReadOnlyList<Contract>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        QueryAllAsync(new QueryRequest
        {
            TableName = Table,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = Attr.S(ContractItemMapper.PartitionKey(projectId)),
                [":prefix"] = Attr.S(ContractItemMapper.ContractPrefix),
            },
            ConsistentRead = true,
        }, ct);

    public Task<IReadOnlyList<Contract>> ListForContractorAsync(ContractorId contractorId, CancellationToken ct) =>
        QueryAllAsync(new QueryRequest
        {
            TableName = Table,
            IndexName = ContractorsTable.Gsi1,
            KeyConditionExpression = "gsi1pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = Attr.S(ContractItemMapper.ContractorKey(contractorId)),
            },
        }, ct);

    /// <summary>Every contract in every project (full scan; for exports).</summary>
    public async Task<IReadOnlyList<Contract>> ScanAllAsync(CancellationToken ct)
    {
        var items = new List<Contract>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.ScanAsync(new ScanRequest
            {
                TableName = Table,
                FilterExpression = "begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":prefix"] = Attr.S(ContractItemMapper.ContractPrefix) },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            items.AddRange((response.Items ?? []).Select(ContractItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);
        return items;
    }

    public Task CreateAsync(Contract contract, CancellationToken ct) =>
        dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = Table,
            Item = ContractItemMapper.ToItem(contract),
            ConditionExpression = "attribute_not_exists(pk)",
        }, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Contract contract, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = Table,
                Item = ContractItemMapper.ToItem(contract),
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

    private async Task<IReadOnlyList<Contract>> QueryAllAsync(QueryRequest request, CancellationToken ct)
    {
        var contracts = new List<Contract>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            request.ExclusiveStartKey = start;
            var response = await dynamo.QueryAsync(request, ct);
            contracts.AddRange((response.Items ?? []).Select(ContractItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return contracts;
    }
}
