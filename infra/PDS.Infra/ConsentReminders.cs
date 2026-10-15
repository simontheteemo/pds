using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Scheduler;
using Amazon.CDK.AWS.Scheduler.Targets;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;
using PDS.Consents;
using LambdaFunction = Amazon.CDK.AWS.Lambda.Function;
using ScheduleExpression = Amazon.CDK.AWS.Scheduler.ScheduleExpression;

namespace PDS.Infra;

/// <summary>
/// Weekday consent digest: EventBridge Scheduler invokes the API function with an HTTP API v2-shaped event for
/// POST <see cref="ConsentsModule.ReminderSendPath"/>, which publishes to an SNS email topic. The event carries a
/// fixed system principal in the authorizer claims; only principals that can invoke the function can send one.
/// </summary>
public sealed class ConsentReminders : Construct
{
    public ConsentReminders(
        Construct scope, string id, DeploymentSettings settings, ConsentReminderSettings reminders,
        LambdaFunction api, ILogGroup apiLogs, ITopic alarms, string siteUrl)
        : base(scope, id)
    {
        var topic = new Topic(this, "topic", new TopicProps
        {
            TopicName = $"{settings.Name}-consent-reminders",
            DisplayName = Truncate(settings.Branding.ProductName, 100),
        });
        foreach (var email in reminders.Emails ?? [])
            topic.AddSubscription(new EmailSubscription(email));
        topic.GrantPublish(api);
        api.AddEnvironment("ConsentReminders__TopicArn", topic.TopicArn);
        api.AddEnvironment("ConsentReminders__AppUrl", siteUrl);

        var path = ConsentsModule.ReminderSendPath;
        var input = new Dictionary<string, object>
        {
            ["version"] = "2.0",
            ["routeKey"] = $"POST {path}",
            ["rawPath"] = path,
            ["rawQueryString"] = "",
            ["headers"] = new Dictionary<string, object>
            {
                ["content-type"] = "application/json",
                ["user-agent"] = "EventBridge Scheduler",
                ["x-correlation-id"] = ContextAttribute.ExecutionId.ToString(),
            },
            ["requestContext"] = new Dictionary<string, object>
            {
                ["routeKey"] = $"POST {path}",
                ["stage"] = "$default",
                ["requestId"] = ContextAttribute.ExecutionId.ToString(),
                ["http"] = new Dictionary<string, object>
                {
                    ["method"] = "POST",
                    ["path"] = path,
                    ["protocol"] = "HTTP/1.1",
                    ["sourceIp"] = "127.0.0.1",
                    ["userAgent"] = "EventBridge Scheduler",
                },
                ["authorizer"] = new Dictionary<string, object>
                {
                    ["jwt"] = new Dictionary<string, object>
                    {
                        ["claims"] = new Dictionary<string, object>
                        {
                            ["sub"] = "system:consent-reminders",
                            ["name"] = "Scheduled consent reminders",
                            ["cognito:groups"] = "[Admin]",
                        },
                        ["scopes"] = Array.Empty<string>(),
                    },
                },
            },
            ["isBase64Encoded"] = false,
        };

        _ = new Schedule(this, "schedule", new ScheduleProps
        {
            Description = "Weekday consent reminder digest",
            Schedule = ScheduleExpression.Expression(
                reminders.Schedule ?? ConsentReminderSettings.DefaultSchedule,
                Amazon.CDK.TimeZone.Of(reminders.TimeZone ?? settings.Locale.TimeZone)),
            Target = new LambdaInvoke(api, new ScheduleTargetBaseProps
            {
                Input = ScheduleTargetInput.FromObject(input),
                RetryAttempts = 2,
                MaxEventAge = Duration.Hours(1),
            }),
        });

        // Only the scheduler invokes this function asynchronously. Lambda's default async policy (2 retries, 6h max
        // age) would otherwise stack on top of the schedule's own retries and risk a double-send or a stale digest.
        api.ConfigureAsyncInvoke(new EventInvokeConfigOptions { RetryAttempts = 0, MaxEventAge = Duration.Hours(1) });

        var failures = new MetricFilter(this, "failures", new MetricFilterProps
        {
            LogGroup = apiLogs,
            FilterPattern = FilterPattern.AllTerms("ConsentReminderSendFailed"),
            MetricNamespace = $"PDS/{settings.Name}",
            MetricName = "ConsentReminderFailures",
            MetricValue = "1",
        });
        new Alarm(this, "failed", new AlarmProps
        {
            Metric = failures.Metric(new MetricOptions { Period = Duration.Minutes(5), Statistic = "Sum" }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING,
            AlarmDescription = "The consent reminder digest failed to send.",
        }).AddAlarmAction(new SnsAction(alarms));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
