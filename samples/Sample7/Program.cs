using System;
using System.Linq;
using System.Threading;
using AWSSecretsManager.Provider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configBuilder = new ConfigurationBuilder();

/*
    Demonstrates logging usage with AWS Secrets Manager Provider
    Shows different log levels and how to configure logging
*/

// Create a logger factory with console logging
using var loggerFactory = LoggerFactory.Create(logBuilder => 
{
    logBuilder.AddConsole();
    // Set minimum level to see Debug and Trace logs from the provider
    logBuilder.SetMinimumLevel(LogLevel.Debug);
});

// Add secrets manager with logging
configBuilder.AddSecretsManager(
    loggerFactory: loggerFactory,
    credentials: null,
    region: null,
    configurator: options =>
    {
        options.PollingInterval = TimeSpan.FromMinutes(5);
        options.IgnoreMissingValues = true;
        options.ConfigureSecretValueRequest = (request, _) => request.VersionStage = "AWSCURRENT";
    });

var configuration = configBuilder.Build();

Console.WriteLine("Configuration loaded with logging enabled!");
Console.WriteLine($"Configuration has {configuration.AsEnumerable().Count()} keys.");
Console.WriteLine("Check the console output above for AWS Secrets Manager logs.");

// Force a manual reload to see reload logs
Console.WriteLine("\\nTriggering manual reload...");
var secretsManagerSource = configBuilder.Sources
    .OfType<AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationSourceWithLogger>()
    .FirstOrDefault();

if (secretsManagerSource != null)
{
    var provider = secretsManagerSource.Build(configBuilder) as AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationProvider;
    try
    {
        await provider?.ForceReloadAsync(CancellationToken.None)!;
        Console.WriteLine("Manual reload completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Manual reload failed (expected in demo): {ex.Message}");
    }
}
else
{
    Console.WriteLine("No SecretsManager configuration source found for manual reload demo.");
}

Console.WriteLine("\\nDemo completed. Press any key to exit...");
Console.ReadKey();