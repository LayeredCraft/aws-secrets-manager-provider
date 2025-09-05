using AwesomeAssertions;
using AWSSecretsManager.Provider.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace AWSSecretsManager.Provider.Observability.Tests;

[Collection("DiagnosticsConfig Tests")]
public class MeterProviderBuilderExtensionsTests
{
    [Fact]
    public void AddAWSSecretsManager_ShouldAddMeter()
    {
        // Arrange
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAWSSecretsManager()
            .Build();

        // Act & Assert
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddAWSSecretsManager_ShouldReturnBuilder_ForMethodChaining()
    {
        // Arrange
        var builder = Sdk.CreateMeterProviderBuilder();

        // Act
        var result = builder.AddAWSSecretsManager();

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddAWSSecretsManager_ShouldEnableMetricsFromMeter()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAWSSecretsManager()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        AWSSecretsManagerTelemetry.SecretsLoaded.Add(5);
        meterProvider?.ForceFlush(1000);

        // Assert
        exportedItems.Should().HaveCountGreaterThan(0);
        var secretsLoadedMetric = exportedItems.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.SecretsLoaded);
        secretsLoadedMetric.Should().NotBeNull();
    }

    [Fact]
    public void AddAWSSecretsManager_ShouldCaptureMultipleMetricTypes()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAWSSecretsManager()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        AWSSecretsManagerTelemetry.SecretsLoaded.Add(3);
        AWSSecretsManagerTelemetry.ApiCalls.Add(1);
        AWSSecretsManagerTelemetry.LoadDuration.Record(123.45);
        AWSSecretsManagerTelemetry.BatchSize.Record(5);
        
        meterProvider?.ForceFlush(1000);

        // Assert
        exportedItems.Should().HaveCountGreaterThan(0);
        
        // Verify we have different metric types
        var counterMetrics = exportedItems.Where(m => m.MetricType == MetricType.LongSum).ToList();
        var histogramMetrics = exportedItems.Where(m => m.MetricType == MetricType.Histogram).ToList();
        
        counterMetrics.Should().HaveCountGreaterThan(0);
        histogramMetrics.Should().HaveCountGreaterThan(0);
    }
}