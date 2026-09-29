using Microsoft.Extensions.Configuration;

namespace PDS.Shared.Data;

public sealed class TableNames(IConfiguration configuration)
{
    public string For(string logicalName) =>
        configuration[$"Tables:{logicalName}"] is { Length: > 0 } name ? name : $"pds-{logicalName}";
}
