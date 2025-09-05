using AWSSecretsManager.Provider.Diagnostics;
using OpenTelemetry.Trace;

namespace AWSSecretsManager.Provider.Observability;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> to add AWS Secrets Manager tracing.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds AWS Secrets Manager instrumentation to the tracer provider.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to add the instrumentation to.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for call chaining.</returns>
    public static TracerProviderBuilder AddAWSSecretsManager(this TracerProviderBuilder builder)
    {
        return builder.AddSource(AWSSecretsManagerTelemetry.ActivitySourceName);
    }
}