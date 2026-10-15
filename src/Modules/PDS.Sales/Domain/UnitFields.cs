using PDS.Shared;

namespace PDS.Sales.Domain;

/// <summary>The unit's physical and listing details (sale progress is separate).</summary>
internal sealed record UnitFields(
    string Identifier,
    UnitType Type,
    int? Bedrooms,
    int? Bathrooms,
    int? CarParks,
    decimal? FloorAreaSqm,
    decimal? LandAreaSqm,
    Money? ListPrice,
    string? Notes);
