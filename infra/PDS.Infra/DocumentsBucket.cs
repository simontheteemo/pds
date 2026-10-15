using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace PDS.Infra;

/// <summary>
/// Private bucket for document files. Browsers PUT to pending/ and GET from files/ through presigned URLs only;
/// unconfirmed uploads in pending/ expire after a day. The bucket name is generated (S3 names are global).
/// </summary>
public sealed class DocumentsBucket : Construct
{
    public DocumentsBucket(Construct scope, string id, DeploymentSettings settings, string siteUrl) : base(scope, id)
    {
        Bucket = new Bucket(this, "bucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            ObjectOwnership = ObjectOwnership.BUCKET_OWNER_ENFORCED,
            RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            AutoDeleteObjects = !settings.IsProduction,
            Cors =
            [
                new CorsRule
                {
                    AllowedOrigins = [siteUrl],
                    AllowedMethods = [HttpMethods.PUT, HttpMethods.GET],
                    AllowedHeaders = ["content-type"],
                    ExposedHeaders = ["etag"],
                    MaxAge = 3600,
                },
            ],
            LifecycleRules =
            [
                new LifecycleRule
                {
                    Id = "expire-pending-uploads",
                    Prefix = "pending/",
                    Expiration = Duration.Days(1),
                    AbortIncompleteMultipartUploadAfter = Duration.Days(1),
                },
            ],
        });
    }

    public Bucket Bucket { get; }

    /// <summary>
    /// Exactly what the API needs: sign PUTs, head, copy-source and delete under pending/; copy-destination and
    /// sign GETs under files/. No ListBucket, no delete under files/ (documents are soft-deleted).
    /// </summary>
    public void GrantApi(IFunction api)
    {
        api.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
            Resources = [Bucket.ArnForObjects("pending/*")],
        }));
        api.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["s3:PutObject", "s3:GetObject"],
            Resources = [Bucket.ArnForObjects("files/*")],
        }));
    }
}
