using Amazon.DynamoDBv2;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class TableBootstrapperTests
{
    [Fact]
    public void Builds_on_demand_table_with_pk_sk_and_gsi()
    {
        var definition = new TableDefinition("portfolio", [new GlobalIndex("gsi1", "gsi1pk", "gsi1sk")]);

        var request = TableBootstrapper.BuildCreateRequest("pds-portfolio", definition);

        Assert.Equal("pds-portfolio", request.TableName);
        Assert.Equal(BillingMode.PAY_PER_REQUEST, request.BillingMode);
        Assert.Equal(new[] { "pk", "sk", "gsi1pk", "gsi1sk" }, request.AttributeDefinitions.Select(a => a.AttributeName));
        Assert.All(request.AttributeDefinitions, a => Assert.Equal(ScalarAttributeType.S, a.AttributeType));
        var index = Assert.Single(request.GlobalSecondaryIndexes);
        Assert.Equal("gsi1", index.IndexName);
        Assert.Equal(ProjectionType.ALL, index.Projection.ProjectionType);
    }

    [Fact]
    public void Omits_index_list_when_table_has_no_indexes()
    {
        var request = TableBootstrapper.BuildCreateRequest("t", new TableDefinition("t", []));

        Assert.Null(request.GlobalSecondaryIndexes);
    }
}
