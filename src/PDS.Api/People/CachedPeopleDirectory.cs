using PDS.Shared.People;

namespace PDS.Api.People;

/// <summary>Caches the inner directory's list for five minutes. A failed reload keeps the old cache and propagates the error.</summary>
public sealed class CachedPeopleDirectory(IPeopleDirectory inner, TimeProvider clock) : IPeopleDirectory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim gate = new(1, 1);
    private (IReadOnlyList<Person> People, DateTimeOffset LoadedAt)? cache;

    public Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct) => LoadAsync(ct);

    public async Task<Person?> FindAsync(string id, CancellationToken ct) =>
        (await LoadAsync(ct)).FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    private async Task<IReadOnlyList<Person>> LoadAsync(CancellationToken ct)
    {
        if (cache is { } fresh && clock.GetUtcNow() - fresh.LoadedAt < Ttl)
            return fresh.People;
        await gate.WaitAsync(ct);
        try
        {
            if (cache is { } again && clock.GetUtcNow() - again.LoadedAt < Ttl)
                return again.People;
            var people = await inner.ListAsync(ct);
            cache = (people, clock.GetUtcNow());
            return people;
        }
        finally
        {
            gate.Release();
        }
    }
}
