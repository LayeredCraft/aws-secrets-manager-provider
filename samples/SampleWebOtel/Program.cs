using AWSSecretsManager.Provider;
using AWSSecretsManager.Provider.Observability;
using AWSSecretsManager.Provider.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry 
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("SampleWebOtel", "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = "development",
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAWSSecretsManager()  // Add AWS Secrets Manager tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAWSSecretsManager()  // Add AWS Secrets Manager metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddConsoleExporter());

// Configure logging to see OpenTelemetry data
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);

// Add AWS Secrets Manager with logging support
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
builder.Configuration.AddSecretsManager(
    loggerFactory,
    configurator: options =>
    {
        // Enable polling to generate more telemetry data
        options.PollingInterval = TimeSpan.FromSeconds(30);
        options.IgnoreMissingValues = true;
        options.UseBatchFetch = true; // Use batch fetching for better performance
    });

var app = builder.Build();

// Configure endpoints
app.MapGet("/", () => "AWS Secrets Manager + OpenTelemetry Demo");

app.MapGet("/config", (IConfiguration config) =>
{
    // Display configuration keys (be careful not to expose secrets in production!)
    var keys = config.AsEnumerable()
        .Where(kvp => !string.IsNullOrEmpty(kvp.Key))
        .Select(kvp => kvp.Key)
        .OrderBy(k => k)
        .ToArray();
    
    return new 
    { 
        Message = "Configuration keys loaded from AWS Secrets Manager",
        ConfigurationKeys = keys, 
        Count = keys.Length,
        Timestamp = DateTime.UtcNow
    };
});

app.MapGet("/health", () =>
{
    return new 
    {
        Status = "Healthy",
        Service = "SampleWebOtel",
        Timestamp = DateTime.UtcNow,
        OpenTelemetry = new
        {
            TracingEnabled = true,
            MetricsEnabled = true,
            AWSSecretsManagerInstrumentation = true
        }
    };
});

app.MapGet("/telemetry-info", () =>
{
    return new
    {
        Message = "AWS Secrets Manager OpenTelemetry Integration",
        Features = new[]
        {
            "Distributed tracing for secret loading operations",
            "Performance metrics for load times and success rates", 
            "AWS region tracking in spans",
            "API call monitoring (ListSecrets, GetSecretValue, BatchGetSecretValue)",
            "JSON parsing telemetry",
            "Polling cycle monitoring",
            "Comprehensive error tracking"
        },
        MetricNames = new[]
        {
            "aws_secrets.loaded - Number of secrets loaded",
            "aws_secrets.load.duration - Time to load secrets",
            "aws_secrets.reload.duration - Time to reload secrets",
            "aws_secrets.api_calls - AWS API call counts",
            "aws_secrets.polling_cycles - Polling operation counts",
            "aws_secrets.configuration_errors - Configuration error counts",
            "aws_secrets.batch_size - Batch operation sizes",
            "aws_secrets.json_parse.duration - JSON parsing times"
        },
        SpanNames = new[]
        {
            "aws_secrets.load - Main secret loading operation",
            "aws_secrets.reload - Secret reload operation", 
            "aws_secrets.fetch_configuration - Individual secret fetching",
            "aws_secrets.fetch_configuration_batch - Batch secret fetching",
            "aws_secrets.aws_api_call - Individual AWS API calls",
            "aws_secrets.json_parse - JSON parsing operations"
        },
        ConsoleOutput = "Check the console output for OpenTelemetry traces and metrics"
    };
});

Console.WriteLine("🚀 AWS Secrets Manager + OpenTelemetry Demo Starting...");
Console.WriteLine("📊 Telemetry data will be output to the console");
Console.WriteLine("🔗 Available endpoints:");
Console.WriteLine("   GET / - Welcome message");
Console.WriteLine("   GET /config - Show loaded configuration keys");
Console.WriteLine("   GET /health - Health check");
Console.WriteLine("   GET /telemetry-info - OpenTelemetry integration details");
Console.WriteLine();

app.Run();