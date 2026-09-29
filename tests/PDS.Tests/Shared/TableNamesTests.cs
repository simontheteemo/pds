using Microsoft.Extensions.Configuration;
using PDS.Shared.Data;

namespace PDS.Tests.Shared;

public class TableNamesTests
{
    [Fact]
    public void Uses_configured_name_case_insensitively()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tables:Portfolio"] = "seine-dev-portfolio" })
            .Build();

        Assert.Equal("seine-dev-portfolio", new TableNames(config).For("portfolio"));
    }

    [Fact]
    public void Falls_back_to_pds_prefix() =>
        Assert.Equal("pds-portfolio", new TableNames(new ConfigurationBuilder().Build()).For("portfolio"));
}
