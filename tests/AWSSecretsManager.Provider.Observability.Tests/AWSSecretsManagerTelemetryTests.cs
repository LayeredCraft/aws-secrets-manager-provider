using AWSSecretsManager.Provider.Diagnostics;
using AwesomeAssertions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace AWSSecretsManager.Provider.Observability.Tests;

[Collection("DiagnosticsConfig Tests")]
public class AWSSecretsManagerTelemetryTests : IDisposable
{
    private readonly List<MetricSnapshot> _capturedMetrics = [];
    private readonly MeterProvider _meterProvider;

    public AWSSecretsManagerTelemetryTests()
    {
        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(AWSSecretsManagerTelemetry.MeterName)
            .AddInMemoryExporter(_capturedMetrics)
            .Build();
    }

    public void Dispose()
    {
        _meterProvider?.Dispose();
    }
    [Fact]
    public void ActivitySource_ShouldBeInitialized()
    {
        // Act & Assert
        AWSSecretsManagerTelemetry.Source.Should().NotBeNull();
        AWSSecretsManagerTelemetry.Source.Name.Should().Be("AWSSecretsManager.Provider");
        AWSSecretsManagerTelemetry.Source.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Meter_ShouldBeInitialized()
    {
        // Act & Assert
        AWSSecretsManagerTelemetry.Meter.Should().NotBeNull();
        AWSSecretsManagerTelemetry.Meter.Name.Should().Be("AWSSecretsManager.Provider");
        AWSSecretsManagerTelemetry.Meter.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Counters_ShouldBeInitialized()
    {
        // Act & Assert
        AWSSecretsManagerTelemetry.SecretsLoaded.Should().NotBeNull();
        AWSSecretsManagerTelemetry.ApiCalls.Should().NotBeNull();
        AWSSecretsManagerTelemetry.PollingCycles.Should().NotBeNull();
        AWSSecretsManagerTelemetry.ConfigurationErrors.Should().NotBeNull();
    }

    [Fact]
    public void Histograms_ShouldBeInitialized()
    {
        // Act & Assert
        AWSSecretsManagerTelemetry.LoadDuration.Should().NotBeNull();
        AWSSecretsManagerTelemetry.ReloadDuration.Should().NotBeNull();
        AWSSecretsManagerTelemetry.BatchSize.Should().NotBeNull();
        AWSSecretsManagerTelemetry.JsonParseDuration.Should().NotBeNull();
    }

    [Fact]
    public void TimerScope_ShouldRecordMetric_WhenDisposed()
    {
        // Act
        using (var timer = AWSSecretsManagerTelemetry.TimeLoad())
        {
            // Simulate some work
            Thread.Sleep(10);
        }
        
        _meterProvider.ForceFlush(1000);
        
        // Assert
        _capturedMetrics.Should().HaveCountGreaterThan(0);
        var loadDurationMetric = _capturedMetrics.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.LoadDuration);
        loadDurationMetric.Should().NotBeNull("Load duration metric should be recorded");
    }

    [Fact]
    public void TimerScope_WithTags_ShouldRecordMetric_WhenDisposed()
    {
        // Arrange
        var tag = new KeyValuePair<string, object?>("operation", "test");
        
        // Act
        using (var timer = new AWSSecretsManagerTelemetry.TimerScope(
            AWSSecretsManagerTelemetry.LoadDuration, tag))
        {
            // Simulate some work
            Thread.Sleep(10);
        }
        
        _meterProvider.ForceFlush(1000);
        
        // Assert
        _capturedMetrics.Should().HaveCountGreaterThan(0);
        var loadDurationMetric = _capturedMetrics.FirstOrDefault(m => m.Name == AWSSecretsManagerMetricNames.LoadDuration);
        loadDurationMetric.Should().NotBeNull("Load duration metric should be recorded with tags");
    }

    [Fact]
    public void Constants_ShouldHaveExpectedValues()
    {
        // Act & Assert
        AWSSecretsManagerTelemetry.ActivitySourceName.Should().Be("AWSSecretsManager.Provider");
        AWSSecretsManagerTelemetry.MeterName.Should().Be("AWSSecretsManager.Provider");
    }

    [Fact]
    public void TimeLoad_ShouldCreateTimerScope()
    {
        // Act
        using var timer = AWSSecretsManagerTelemetry.TimeLoad();
        
        // Assert
        timer.Should().NotBeNull();
    }

    [Fact]
    public void TimeReload_ShouldCreateTimerScope()
    {
        // Act
        using var timer = AWSSecretsManagerTelemetry.TimeReload();
        
        // Assert
        timer.Should().NotBeNull();
    }

    [Fact]
    public void TimeJsonParse_ShouldCreateTimerScope()
    {
        // Act
        using var timer = AWSSecretsManagerTelemetry.TimeJsonParse();
        
        // Assert
        timer.Should().NotBeNull();
    }
}