namespace PDS.Portfolio.Domain;

internal sealed record Site(
    string AddressLine,
    string? Suburb,
    string City,
    string? Region,
    string? Postcode,
    string? LegalDescription,
    string? TitleReference,
    decimal? LandAreaSqm);
