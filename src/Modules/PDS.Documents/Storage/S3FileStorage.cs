using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PDS.Documents.Storage;

internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<DocumentsOptions> options, ILogger<S3FileStorage> logger)
    : IFileStorage
{
    private string Bucket => options.Value.BucketName is { Length: > 0 } bucket
        ? bucket
        : throw new InvalidOperationException("Documents:BucketName is not configured.");

    public Task<string> CreateUploadUrlAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken ct) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt.UtcDateTime,
        });

    public Task<string> CreateDownloadUrlAsync(
        string key, string fileName, string contentType, DateTimeOffset expiresAt, CancellationToken ct) =>
        s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = ContentDispositionHeader.Attachment(fileName),
                ContentType = contentType,
            },
        });

    public async Task<StoredObject?> HeadAsync(string key, CancellationToken ct)
    {
        try
        {
            var head = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = Bucket, Key = key }, ct);
            return new StoredObject(head.ContentLength, head.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            // Without s3:ListBucket, S3 reports a missing key as 403. Log so a real permission fault is visible.
            if (ex.StatusCode == HttpStatusCode.Forbidden)
                logger.LogInformation("HeadObject on {Key} returned 403; treating it as missing.", key);
            return null;
        }
    }

    public Task CopyAsync(string sourceKey, string destinationKey, CancellationToken ct) =>
        s3.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = Bucket,
            SourceKey = sourceKey,
            DestinationBucket = Bucket,
            DestinationKey = destinationKey,
            MetadataDirective = S3MetadataDirective.COPY,
        }, ct);

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket, Key = key }, ct);
}
