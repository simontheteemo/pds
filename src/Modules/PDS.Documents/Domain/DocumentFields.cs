namespace PDS.Documents.Domain;

internal sealed record DocumentFields(string Title, string Category, DocumentLink? Link, IReadOnlyList<string> Tags, string? Notes);
