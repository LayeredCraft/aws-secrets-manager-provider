using AWSSecretsManager.Provider.Diagnostics;
using OpenTelemetry.Metrics;

namespace AWSSecretsManager.Provider.Observability;

/// <summary>
/// Extension methods for <see cref="MeterProviderBuilder"/> to add AWS Secrets Manager metrics.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds AWS Secrets Manager instrumentation to the meter provider.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> to add the instrumentation to.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for call chaining.</returns>
    public static MeterProviderBuilder AddAWSSecretsManager(this MeterProviderBuilder builder)
    {
        return builder.AddMeter(AWSSecretsManagerTelemetry.MeterName);
    }
}