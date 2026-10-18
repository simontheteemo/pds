using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Contractors.Contracts;
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
        var commitments = new List<Commitment>();
        var actuals = new List<ActualCost>();
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
                Dispatch(item, lines, commitments, actuals);

            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return new ProjectCostData(lines, commitments, actuals);
    }

    /// <summary>Every cost item in every project, grouped by project (full scan; for exports).</summary>
    public async Task<IReadOnlyDictionary<ProjectId, ProjectCostData>> ScanAllAsync(CancellationToken ct)
    {
        var lines = new List<BudgetLine>();
        var commitments = new List<Commitment>();
        var actuals = new List<ActualCost>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.ScanAsync(new ScanRequest
            {
                TableName = Table,
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            foreach (var item in response.Items ?? [])
                Dispatch(item, lines, commitments, actuals);

            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        var linesBy = lines.ToLookup(l => l.ProjectId);
        var commitmentsBy = commitments.ToLookup(c => c.ProjectId);
        var actualsBy = actuals.ToLookup(a => a.ProjectId);
        return linesBy.Select(g => g.Key)
            .Union(commitmentsBy.Select(g => g.Key))
            .Union(actualsBy.Select(g => g.Key))
            .ToDictionary(
                id => id,
                id => new ProjectCostData(linesBy[id].ToList(), commitmentsBy[id].ToList(), actualsBy[id].ToList()));
    }

    /// <summary>Every actual linked to the contractor, across projects (gsi1; eventually consistent).</summary>
    public async Task<IReadOnlyList<ActualCost>> ListActualsForContractorAsync(ContractorId id, CancellationToken ct)
    {
        var actuals = new List<ActualCost>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                IndexName = CostsTable.Gsi1,
                KeyConditionExpression = "gsi1pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = Attr.S(CostKeys.ContractorKey(id)) },
                ExclusiveStartKey = start,
            }, ct);
            actuals.AddRange((response.Items ?? []).Select(ActualCostItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return actuals;
    }

    /// <summary>The project's budget lines only (no commitments or actuals).</summary>
    public async Task<IReadOnlyList<BudgetLine>> ListLinesAsync(ProjectId projectId, CancellationToken ct)
    {
        var lines = new List<BudgetLine>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamo.QueryAsync(new QueryRequest
            {
                TableName = Table,
                KeyConditionExpression = "pk = :pk AND begins_with(sk, :prefix)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = Attr.S(CostKeys.Pk(projectId)),
                    [":prefix"] = Attr.S(CostKeys.LinePrefix),
                },
                ConsistentRead = true,
                ExclusiveStartKey = start,
            }, ct);
            lines.AddRange((response.Items ?? []).Select(BudgetLineItemMapper.FromItem));
            start = response.LastEvaluatedKey is { Count: > 0 } next ? next : null;
        }
        while (start is not null);

        return lines;
    }

    private static void Dispatch(
        Dictionary<string, AttributeValue> item, List<BudgetLine> lines, List<Commitment> commitments, List<ActualCost> actuals)
    {
        var sk = Attr.GetString(item, TableKeys.SortKey);
        if (sk.StartsWith(CostKeys.LinePrefix, StringComparison.Ordinal))
            lines.Add(BudgetLineItemMapper.FromItem(item));
        else if (sk.StartsWith(CostKeys.CommitmentPrefix, StringComparison.Ordinal))
            commitments.Add(CommitmentItemMapper.FromItem(item));
        else if (sk.StartsWith(CostKeys.ActualPrefix, StringComparison.Ordinal))
            actuals.Add(ActualCostItemMapper.FromItem(item));
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

    public async Task<Commitment?> GetCommitmentAsync(ProjectId projectId, CommitmentId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = Table,
            Key = CostKeys.Key(projectId, CommitmentItemMapper.Sk(id)),
            ConsistentRead = true,
        }, ct);
        return response.Item is { Count: > 0 } item ? CommitmentItemMapper.FromItem(item) : null;
    }

    public Task CreateCommitmentAsync(Commitment c, CancellationToken ct) =>
        PutNewAsync(CommitmentItemMapper.ToItem(c), ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> UpdateCommitmentAsync(Commitment c, long expectedVersion, CancellationToken ct) =>
        PutVersionedAsync(CommitmentItemMapper.ToItem(c), expectedVersion, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> DeleteCommitmentAsync(ProjectId projectId, CommitmentId id, long expectedVersion, CancellationToken ct) =>
        DeleteVersionedAsync(CostKeys.Key(projectId, CommitmentItemMapper.Sk(id)), expectedVersion, ct);

    public async Task<ActualCost?> GetActualAsync(ProjectId projectId, ActualCostId id, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = Table,
            Key = CostKeys.Key(projectId, ActualCostItemMapper.Sk(id)),
            ConsistentRead = true,
        }, ct);
        return response.Item is { Count: > 0 } item ? ActualCostItemMapper.FromItem(item) : null;
    }

    public Task CreateActualAsync(ActualCost a, CancellationToken ct) =>
        PutNewAsync(ActualCostItemMapper.ToItem(a), ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> UpdateActualAsync(ActualCost a, long expectedVersion, CancellationToken ct) =>
        PutVersionedAsync(ActualCostItemMapper.ToItem(a), expectedVersion, ct);

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public Task<bool> DeleteActualAsync(ProjectId projectId, ActualCostId id, long expectedVersion, CancellationToken ct) =>
        DeleteVersionedAsync(CostKeys.Key(projectId, ActualCostItemMapper.Sk(id)), expectedVersion, ct);

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
