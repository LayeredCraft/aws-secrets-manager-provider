using AwesomeAssertions;
using AWSSecretsManager.Provider.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Xunit;

namespace AWSSecretsManager.Provider.Observability.Tests;

[Collection("DiagnosticsConfig Tests")]
public class TracerProviderBuilderExtensionsTests
{
    [Fact]
    public void AddAWSSecretsManager_ShouldAddActivitySource()
    {
        // Arrange
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSSecretsManager()
            .Build();

        // Act & Assert
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddAWSSecretsManager_ShouldReturnBuilder_ForMethodChaining()
    {
        // Arrange
        var builder = Sdk.CreateTracerProviderBuilder();

        // Act
        var result = builder.AddAWSSecretsManager();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddAWSSecretsManager_ShouldEnableActivitiesFromSource()
    {
        // Arrange
        var activities = new List<Activity>();
        
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAWSSecretsManager()
            .AddProcessor(new TestActivityProcessor(activities))
            .Build();

        // Act
        using (var activity = AWSSecretsManagerTelemetry.Source.StartActivity("test-activity"))
        {
            activity?.SetTag("test.tag", "test-value");
        }
        
        // Force flush to ensure activity is processed
        tracerProvider?.ForceFlush(1000);

        // Assert
        activities.Should().HaveCount(1);
        activities[0].DisplayName.Should().Be("test-activity");
        activities[0].Tags.Should().Contain(new KeyValuePair<string, string?>("test.tag", "test-value"));
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