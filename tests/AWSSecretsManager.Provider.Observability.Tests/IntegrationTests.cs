using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using AwesomeAssertions;
using AWSSecretsManager.Provider.Diagnostics;
using AWSSecretsManager.Provider.Internal;
using NSubstitute;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Xunit;

namespace AWSSecretsManager.Provider.Observability.Tests;

[Collection("DiagnosticsConfig Tests")]
public class IntegrationTests
{
    [Fact]
    public void SecretsManagerConfigurationProvider_Load_ShouldGenerateActivities()
    {
        // Arrange
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSSecretsManager()
            .AddProcessor(new TestActivityProcessor(activities))
            .Build();

        var client = Substitute.For<IAmazonSecretsManager>();
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListSecretsResponse
            {
                SecretList = new List<SecretListEntry>
                {
                    new() { ARN = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret", Name = "test-secret" }
                }
            });

        client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                ARN = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret",
                Name = "test-secret",
                SecretString = "test-value"
            });

        var options = new SecretsManagerConfigurationProviderOptions();
        var provider = new SecretsManagerConfigurationProvider(client, options);

        // Act
        provider.Load();

        // Assert - We now have comprehensive telemetry with nested activities
        activities.Should().HaveCountGreaterThan(1);
        
        // Verify we have the main Load activity
        var loadActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.Load);
        loadActivity.Should().NotBeNull();
        loadActivity.Tags.Should().Contain(new KeyValuePair<string, string?>(
            AWSSecretsManagerSemanticAttributes.OperationType, 
            AWSSecretsManagerSemanticValues.OperationTypeLoad));

        // Verify we have FetchConfiguration activity
        var fetchActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.FetchConfiguration);
        fetchActivity.Should().NotBeNull();

        // Verify we have AWS API call activity
        var apiCallActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.AwsApiCall);
        apiCallActivity.Should().NotBeNull();

        // Verify we have JSON parse activity
        var jsonParseActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.JsonParse);
        jsonParseActivity.Should().NotBeNull();
    }

    [Fact]
    public void SecretsManagerConfigurationProvider_Load_ShouldGenerateMetrics()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAWSSecretsManager()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var client = Substitute.For<IAmazonSecretsManager>();
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListSecretsResponse
            {
                SecretList = new List<SecretListEntry>
                {
                    new() { ARN = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret", Name = "test-secret" }
                }
            });

        client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                ARN = "arn:aws:secretsmanager:us-east-1:123456789012:secret:test-secret",
                Name = "test-secret",
                SecretString = "test-value"
            });

        var options = new SecretsManagerConfigurationProviderOptions();
        var provider = new SecretsManagerConfigurationProvider(client, options);

        // Act
        provider.Load();
        meterProvider.ForceFlush(1000);

        // Assert
        exportedItems.Should().HaveCountGreaterThan(0);
        
        // Verify we have the expected metrics
        var loadDurationMetric = exportedItems.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.LoadDuration);
        var secretsLoadedMetric = exportedItems.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.SecretsLoaded);
        
        loadDurationMetric.Should().NotBeNull("Load duration metric should be recorded");
        secretsLoadedMetric.Should().NotBeNull("Secrets loaded metric should be recorded");
    }

    [Fact]
    public void SecretsManagerConfigurationProvider_LoadWithError_ShouldGenerateErrorMetrics()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAWSSecretsManager()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var client = Substitute.For<IAmazonSecretsManager>();
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ListSecretsResponse>(new ResourceNotFoundException("Secret not found")));

        var options = new SecretsManagerConfigurationProviderOptions();
        var provider = new SecretsManagerConfigurationProvider(client, options);

        // Act & Assert
        Assert.Throws<ResourceNotFoundException>(() => provider.Load());
        
        meterProvider.ForceFlush(1000);

        // Assert
        var errorMetric = exportedItems.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.ConfigurationErrors);
        errorMetric.Should().NotBeNull("Configuration error metric should be recorded");
    }

    [Fact]
    public void SecretsManagerConfigurationProvider_LoadWithBatchFetch_ShouldSetBatchAttributes()
    {
        // Arrange
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSSecretsManager()
            .AddProcessor(new TestActivityProcessor(activities))
            .Build();

        var client = Substitute.For<IAmazonSecretsManager>();
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListSecretsResponse { SecretList = new List<SecretListEntry>() });

        var options = new SecretsManagerConfigurationProviderOptions { UseBatchFetch = true };
        var provider = new SecretsManagerConfigurationProvider(client, options);

        // Act
        provider.Load();

        // Assert - With comprehensive telemetry, we have both Load and FetchConfigurationBatch activities
        activities.Should().HaveCountGreaterThanOrEqualTo(2);
        
        // Verify the Load activity has batch fetch attribute
        var loadActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.Load);
        loadActivity.Should().NotBeNull();
        loadActivity.Tags.Should().Contain(new KeyValuePair<string, string?>(
            AWSSecretsManagerSemanticAttributes.UseBatchFetch, 
            AWSSecretsManagerSemanticValues.True));

        // Verify the FetchConfigurationBatch activity exists
        var batchFetchActivity = activities.FirstOrDefault(a => a.DisplayName == AWSSecretsManagerSpanNames.FetchConfigurationBatch);
        batchFetchActivity.Should().NotBeNull();
        batchFetchActivity.Tags.Should().Contain(new KeyValuePair<string, string?>(
            AWSSecretsManagerSemanticAttributes.OperationType, 
            AWSSecretsManagerSemanticValues.OperationTypeBatchFetch));
    }

    private class TestActivityProcessor : BaseProcessor<Activity>
    {
        private readonly List<Activity> _activities;

        public TestActivityProcessor(List<Activity> activities)
        {
            _activities = activities;
        }

        public override void OnEnd(Activity activity)
        {
            _activities.Add(activity);
        }
    }
}