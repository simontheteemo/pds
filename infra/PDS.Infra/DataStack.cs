using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;
using PDS.Shared.Data;
using DynamoAttribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace PDS.Infra;

/// <summary>One DynamoDB table per module definition, with the same keys the app creates locally.</summary>
public sealed class DataStack : Stack
{
    public DataStack(Construct scope, string id, IStackProps props, DeploymentSettings settings, IReadOnlyList<TableDefinition> definitions)
        : base(scope, id, props)
    {
        var tables = new Dictionary<string, Table>();
        foreach (var definition in definitions)
        {
            var table = new Table(this, $"{definition.LogicalName}-table", new TableProps
            {
                TableName = $"{settings.Name}-{definition.LogicalName}",
                PartitionKey = new DynamoAttribute { Name = TableKeys.PartitionKey, Type = AttributeType.STRING },
                SortKey = new DynamoAttribute { Name = TableKeys.SortKey, Type = AttributeType.STRING },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification { PointInTimeRecoveryEnabled = true },
                DeletionProtection = settings.IsProduction,
                RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            });
            foreach (var index in definition.GlobalIndexes)
            {
                table.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
                {
                    IndexName = index.Name,
                    PartitionKey = new DynamoAttribute { Name = index.PartitionKey, Type = AttributeType.STRING },
                    SortKey = new DynamoAttribute { Name = index.SortKey, Type = AttributeType.STRING },
                    ProjectionType = ProjectionType.ALL,
                });
            }

            tables[definition.LogicalName] = table;
        }

        Tables = tables;
    }

    public IReadOnlyDictionary<string, Table> Tables { get; }
}
