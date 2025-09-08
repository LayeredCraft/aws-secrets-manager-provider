# Sample7 - Advanced Logging and Configuration

This sample demonstrates advanced logging integration with the AWS Secrets Manager provider, including polling and detailed logging configuration.

## What it demonstrates

- Comprehensive logging configuration with different log levels
- Background polling for secret updates
- Manual reload functionality
- Error handling and logging best practices
- Integration with `Microsoft.Extensions.Logging`

## Key features

```csharp
// Create logger factory with console logging
using var loggerFactory = LoggerFactory.Create(logBuilder => 
{
    logBuilder.AddConsole();
    logBuilder.SetMinimumLevel(LogLevel.Debug);
});

// Add secrets manager with comprehensive logging
configBuilder.AddSecretsManager(
    loggerFactory: loggerFactory,
    configurator: options =>
    {
        options.PollingInterval = TimeSpan.FromMinutes(5);
        options.IgnoreMissingValues = true;
        options.ConfigureSecretValueRequest = (request, _) => 
            request.VersionStage = "AWSCURRENT";
    });
```

## Advanced Features Demonstrated

### Logging Configuration
- **Debug Level**: Shows detailed provider operations
- **Console Output**: Real-time logging to console
- **Structured Logging**: Uses LayeredCraft.StructuredLogging for performance metrics

### Polling Configuration
- **Background Updates**: Automatically checks for secret updates every 5 minutes
- **Manual Reload**: Demonstrates triggering manual reload operations
- **Error Handling**: Shows how polling errors are handled gracefully

### AWS Configuration
- **Version Stage**: Specifies which version of secrets to retrieve
- **Error Tolerance**: `IgnoreMissingValues` prevents failures on missing secrets

## Log Output Examples

The sample will show logs like:
```
[Information] AWS Secrets Manager provider loading secrets...
[Debug] Loading secrets from region us-east-1
[Information] Loaded 5 secrets successfully in 1.2s
[Debug] Starting background polling every 00:05:00
```

## Use cases

- Development environments where you need visibility into secret loading
- Production monitoring and troubleshooting
- Applications requiring real-time secret updates
- Integration with centralized logging systems
- Performance monitoring of secret operations

## Running the sample

1. Ensure AWS credentials are configured
2. Run: `dotnet run`
3. Watch the console output for detailed logging
4. Press any key to exit after the demo completes

## Requirements

- AWS credentials configured
- Access to AWS Secrets Manager
- Secrets in your AWS account to see polling and reload operations