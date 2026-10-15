using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PDS.Documents.Data;
using PDS.Documents.Features;
using PDS.Documents.Storage;
using PDS.Shared.Data;
using PDS.Shared.Security;

namespace PDS.Documents;

public static class DocumentsModule
{
    /// <summary>Tables this module owns. The host creates them locally; CDK creates them in AWS.</summary>
    public static IReadOnlyList<TableDefinition> Tables { get; } = [DocumentsTable.Definition];

    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddSingleton(DocumentsTable.Definition);
        services.AddOptions<DocumentsOptions>()
            .BindConfiguration(DocumentsOptions.Section)
            .Validate<IHostEnvironment>(
                (options, env) => options.Storage != StorageMode.Local || env.IsDevelopment(),
                "Documents:Storage=Local is only allowed when ASPNETCORE_ENVIRONMENT is Development.")
            .Validate(options => options.MaxFileSizeBytes > 0, "Documents:MaxFileSizeBytes must be positive.")
            .ValidateOnStart();
        services.AddHttpContextAccessor();

        // Created lazily: Local mode and build-time OpenAPI generation never construct the S3 client.
        services.AddSingleton<IAmazonS3>(_ =>
        {
            try
            {
                return new AmazonS3Client();
            }
            catch (AmazonClientException)
            {
                return new AmazonS3Client(RegionEndpoint.APSoutheast2);
            }
        });
        services.AddSingleton<DocumentStore>();
        services.AddSingleton<S3FileStorage>();
        services.AddSingleton<LocalFileStorage>();
        services.AddSingleton<IFileStorage>(sp =>
            sp.GetRequiredService<IOptions<DocumentsOptions>>().Value.Storage == StorageMode.Local
                ? sp.GetRequiredService<LocalFileStorage>()
                : sp.GetRequiredService<S3FileStorage>());

        services.AddValidatorsFromAssemblyContaining<RequestUploadValidator>(includeInternalTypes: true);
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization(Policies.CanRead);
        GetSettings.Map(root);

        var group = app.MapGroup("/api/documents/projects/{projectId:guid}/documents")
            .WithTags("Documents")
            .RequireAuthorization(Policies.CanRead);
        RequestUpload.Map(group);
        ListDocuments.Map(group);
        GetDocument.Map(group);
        CreateDocument.Map(group);
        AddVersion.Map(group);
        UpdateDocument.Map(group);
        DeleteDocument.Map(group);
        DownloadVersion.Map(group);

        if (app.ServiceProvider.GetRequiredService<IOptions<DocumentsOptions>>().Value.Storage == StorageMode.Local)
            LocalFileEndpoints.Map(app);
        return app;
    }
}
