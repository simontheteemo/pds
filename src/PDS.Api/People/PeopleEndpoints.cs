using Microsoft.AspNetCore.Http.HttpResults;
using PDS.Shared.People;
using PDS.Shared.Security;

namespace PDS.Api.People;

public static class PeopleEndpoints
{
    public static IEndpointRouteBuilder MapPeopleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/people",
                async Task<Ok<IReadOnlyList<Person>>> (IPeopleDirectory people, CancellationToken ct) =>
                    TypedResults.Ok(await people.ListAsync(ct)))
            .RequireAuthorization(Policies.CanRead)
            .WithName("ListPeople")
            .WithTags("People");
        return app;
    }
}
