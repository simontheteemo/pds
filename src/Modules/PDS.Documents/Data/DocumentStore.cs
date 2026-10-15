using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using PDS.Documents.Contracts;
using PDS.Documents.Domain;
using PDS.Portfolio.Contracts;
using PDS.Shared.Data;

namespace PDS.Documents.Data;

/// <summary>A document with all its versions, newest first.</summary>
internal sealed record DocumentRecord(Document Document, IReadOnlyList<DocumentVersion> Versions);

internal sealed class DocumentStore(IAmazonDynamoDB dynamo, TableNames names)
{
    private string Table => names.For(DocumentsTable.LogicalName);

    /// <summary>Includes deleted documents; callers filter.</summary>
    public async Task<IReadOnlyList<DocumentRecord>> ListForProjectAsync(ProjectId projectId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, DocumentItemMapper.DocumentPrefix, ct));

    public async Task<DocumentRecord?> GetAsync(ProjectId projectId, DocumentId documentId, CancellationToken ct) =>
        Group(await QueryAsync(projectId, DocumentItemMapper.SortKey(documentId), ct)).FirstOrDefault();

    /// <summary>Puts the document, its version 1 and the upload claim atomically.</summary>
    /// <returns>
    /// False when the upload claim was not won: another document already claimed <paramref name="uploadId"/>
    /// (ConditionalCheckFailed) or a concurrent transaction on the claim conflicted (TransactionConflict).
    /// </returns>
    public async Task<bool> CreateAsync(Document document, Guid uploadId, CancellationToken ct)
    {
        const int claimIndex = 2;
        try
        {
            await dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem { Put = new Put { TableName = Table, Item = DocumentItemMapper.ToItem(document), ConditionExpression = "attribute_not_exists(pk)" } },
                    new TransactWriteItem { Put = NewVersionPut(document) },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = Table,
                            Item = UploadClaimItemMapper.ToItem(document.ProjectId, uploadId, document.Id),
                            ConditionExpression = "attribute_not_exists(pk)",
                        },
                    },
                ],
            }, ct);
            return true;
        }
        catch (TransactionCanceledException ex)
            when (ex.CancellationReasons is { Count: > claimIndex } reasons
                && reasons[claimIndex].Code is "ConditionalCheckFailed" or "TransactionConflict")
        {
            return false;
        }
    }

    /// <returns>The document created from <paramref name="uploadId"/>, or null when it is unclaimed.</returns>
    public async Task<DocumentId?> FindByUploadAsync(ProjectId projectId, Guid uploadId, CancellationToken ct)
    {
        var response = await dynamo.GetItemAsync(
            new GetItemRequest { TableName = Table, Key = UploadClaimItemMapper.Key(projectId, uploadId), ConsistentRead = true }, ct);
        return response.Item is { Count: > 0 } item ? UploadClaimItemMapper.DocumentIdOf(item) : null;
    }

    /// <returns>False when the document's version changed or the version number is already taken.</returns>
    public async Task<bool> AddVersionAsync(Document document, long expectedVersion, CancellationToken ct)
    {
        try
        {
            await dynamo.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem { Put = VersionedPut(DocumentItemMapper.ToItem(document), expectedVersion) },
                    new TransactWriteItem { Put = NewVersionPut(document) },
                ],
            }, ct);
            return true;
        }
        catch (TransactionCanceledException)
        {
            return false;
        }
    }

    /// <returns>False when the stored version no longer equals <paramref name="expectedVersion"/>.</returns>
    public async Task<bool> UpdateAsync(Document document, long expectedVersion, CancellationToken ct)
    {
        var put = VersionedPut(DocumentItemMapper.ToItem(document), expectedVersion);
        try
        {
            await dynamo.PutItemAsync(new PutItemRequest
            {
                TableName = put.TableName,
                Item = put.Item,
                ConditionExpression = put.ConditionExpression,
                ExpressionAttributeNames = put.ExpressionAttributeNames,
                ExpressionAttributeValues = put.ExpressionAttributeValues,
            }, ct);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private Put NewVersionPut(Document document) => new()
    {
        TableName = Table,
        Item = VersionItemMapper.ToItem(document.ProjectId, document.Id, document.CurrentVersion),
        ConditionExpression = "attribute_not_exists(pk)",
    };

    private Put VersionedPut(Dictionary<string, AttributeValue> item, long expectedVersion) => new()
    {
        TableName = Table,
        Item = item,
        ConditionExpression = "#version = :expected",
        ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = "version" },
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":expected"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
        },
    };

    private async Task<List<Dictionary<string, AttributeValue>>> QueryAsync(ProjectId projectId, string prefix, CancellationToken ct)
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
                    [":pk"] = Attr.S(DocumentItemMapper.PartitionKey(projectId)),
                    [":prefix"] = Attr.S(prefix),
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

    /// <summary>Splits by itemType; versions whose document item is missing are dropped (never throw).</summary>
    private static IReadOnlyList<DocumentRecord> Group(IEnumerable<Dictionary<string, AttributeValue>> items)
    {
        var documents = new List<Document>();
        var versions = new List<(DocumentId DocumentId, DocumentVersion Version)>();
        foreach (var item in items)
        {
            switch (Attr.GetStringOrNull(item, DocumentItemMapper.ItemTypeAttr))
            {
                case DocumentItemMapper.ItemType:
                    documents.Add(DocumentItemMapper.FromItem(item));
                    break;
                case VersionItemMapper.ItemType:
                    versions.Add((VersionItemMapper.DocumentIdOf(item), VersionItemMapper.ReadVersion(item)));
                    break;
            }
        }

        var byDocument = versions.ToLookup(v => v.DocumentId, v => v.Version);
        return documents
            .Select(d => new DocumentRecord(d, byDocument[d.Id].OrderByDescending(v => v.Number).ToList()))
            .ToList();
    }
}
