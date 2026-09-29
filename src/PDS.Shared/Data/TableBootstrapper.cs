using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PDS.Shared.Data;

/// <summary>Creates missing module tables. Used only when Dynamo:CreateTablesOnStartup is true (local dev, tests).</summary>
public sealed class TableBootstrapper(IAmazonDynamoDB dynamo, TableNames names, IEnumerable<TableDefinition> definitions)
{
    public async Task EnsureTablesAsync(CancellationToken ct)
    {
        var existing = await ListTableNamesAsync(ct);
        foreach (var definition in definitions)
        {
            var name = names.For(definition.LogicalName);
            if (existing.Contains(name))
                continue;

            try
            {
                await dynamo.CreateTableAsync(BuildCreateRequest(name, definition), ct);
            }
            catch (ResourceInUseException)
            {
                // Created concurrently by another host; nothing to do.
            }
        }
    }

    public static CreateTableRequest BuildCreateRequest(string tableName, TableDefinition definition)
    {
        var keyNames = new[] { TableKeys.PartitionKey, TableKeys.SortKey }
            .Concat(definition.GlobalIndexes.SelectMany(i => new[] { i.PartitionKey, i.SortKey }))
            .Distinct();

        return new CreateTableRequest
        {
            TableName = tableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions = keyNames.Select(k => new AttributeDefinition(k, ScalarAttributeType.S)).ToList(),
            KeySchema =
            [
                new KeySchemaElement(TableKeys.PartitionKey, KeyType.HASH),
                new KeySchemaElement(TableKeys.SortKey, KeyType.RANGE),
            ],
            GlobalSecondaryIndexes = definition.GlobalIndexes.Count == 0
                ? null
                : definition.GlobalIndexes.Select(i => new GlobalSecondaryIndex
                {
                    IndexName = i.Name,
                    KeySchema =
                    [
                        new KeySchemaElement(i.PartitionKey, KeyType.HASH),
                        new KeySchemaElement(i.SortKey, KeyType.RANGE),
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                }).ToList(),
        };
    }

    private async Task<HashSet<string>> ListTableNamesAsync(CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string? start = null;
        do
        {
            var response = await dynamo.ListTablesAsync(new ListTablesRequest { ExclusiveStartTableName = start }, ct);
            names.UnionWith(response.TableNames ?? []);
            start = response.LastEvaluatedTableName;
        }
        while (start is not null);

        return names;
    }
}

/// <summary>Runs the bootstrapper before the web server starts, so no request can reach a missing table.</summary>
internal sealed class TableBootstrapperHostedService(TableBootstrapper bootstrapper, IConfiguration configuration)
    : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) =>
        configuration.GetValue<bool>("Dynamo:CreateTablesOnStartup")
            ? bootstrapper.EnsureTablesAsync(cancellationToken)
            : Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
