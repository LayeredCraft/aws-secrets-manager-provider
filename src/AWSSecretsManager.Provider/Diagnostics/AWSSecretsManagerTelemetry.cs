using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AWSSecretsManager.Provider.Diagnostics;

public static class AWSSecretsManagerTelemetry
{
    private const string Version = "1.0.0";
    
    public const string ActivitySourceName = "AWSSecretsManager.Provider";
    public const string MeterName = "AWSSecretsManager.Provider";
    
    public static readonly ActivitySource Source = new(ActivitySourceName, Version);
    public static readonly Meter Meter = new(MeterName, Version);

    public static readonly Counter<long> SecretsLoaded = 
        Meter.CreateCounter<long>(AWSSecretsManagerMetricNames.SecretsLoaded);
    public static readonly Counter<long> ApiCalls = 
        Meter.CreateCounter<long>(AWSSecretsManagerMetricNames.ApiCalls);
    public static readonly Counter<long> PollingCycles = 
        Meter.CreateCounter<long>(AWSSecretsManagerMetricNames.PollingCycles);
    public static readonly Counter<long> ConfigurationErrors = 
        Meter.CreateCounter<long>(AWSSecretsManagerMetricNames.ConfigurationErrors);

    public static readonly Histogram<double> LoadDuration = 
        Meter.CreateHistogram<double>(AWSSecretsManagerMetricNames.LoadDuration, unit: "ms");
    public static readonly Histogram<double> ReloadDuration = 
        Meter.CreateHistogram<double>(AWSSecretsManagerMetricNames.ReloadDuration, "ms");
    public static readonly Histogram<long> BatchSize = 
        Meter.CreateHistogram<long>(AWSSecretsManagerMetricNames.BatchSize, unit: "{secret}");
    public static readonly Histogram<double> JsonParseDuration = 
        Meter.CreateHistogram<double>(AWSSecretsManagerMetricNames.JsonParseDuration, "ms");

    public static TimerScope TimeLoad() => new(LoadDuration);
    public static TimerScope TimeReload() => new(ReloadDuration);
    public static TimerScope TimeJsonParse() => new(JsonParseDuration);

    public readonly struct TimerScope : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly long _start;
        private readonly KeyValuePair<string, object?>[]? _tags;

        public TimerScope(Histogram<double> histogram)
        {
            _histogram = histogram;
            _start = Stopwatch.GetTimestamp();
            _tags = null;
        }

        public TimerScope(Histogram<double> histogram, params KeyValuePair<string, object?>[] tags)
        {
            _histogram = histogram;
            _start = Stopwatch.GetTimestamp();
            _tags = tags;
        }

        public void Dispose()
        {
            var ms = (Stopwatch.GetTimestamp() - _start) * 1000.0 / Stopwatch.Frequency;
            if (_tags != null && _tags.Length > 0)
            {
                _histogram.Record(ms, _tags);
            }
            else
            {
                _histogram.Record(ms);
            }
        }
    }

    internal static void ResetForTesting()
    {
        // Reset any static state if needed for testing
    }
}