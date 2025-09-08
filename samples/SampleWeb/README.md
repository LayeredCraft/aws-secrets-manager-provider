# SampleWeb - ASP.NET Core Integration

This sample demonstrates how to integrate the AWS Secrets Manager Configuration Provider with ASP.NET Core applications.

## What it demonstrates

- Integration with `WebApplicationBuilder` and ASP.NET Core configuration system
- Logger factory integration using the web application's logging infrastructure
- Background polling in web applications
- Exposing configuration endpoints for debugging (with security considerations)

## Key features

```csharp
// Integration with ASP.NET Core
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
builder.Configuration.AddSecretsManager(
    loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromSeconds(10));
```

## Web Application Features

### Configuration Endpoints
- `GET /` - Basic health check endpoint
- `GET /config` - Returns configuration keys (⚠️ **Development only** - never expose secrets in production)

### Logging Integration
- Uses ASP.NET Core's built-in logging system
- Console logging for development visibility
- Structured logging with performance metrics

### Background Polling
- 10-second polling interval (faster than typical for demonstration)
- Automatic secret updates without application restart
- Web application continues serving requests during updates

## Alternative Configuration Approaches

The sample shows two approaches:

### Approach 1: Logger Factory (Recommended)
```csharp
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
builder.Configuration.AddSecretsManager(loggerFactory, configurator: options => ...);
```

### Approach 2: Explicit Logger
```csharp
var logger = loggerFactory.CreateLogger<SecretsManagerConfigurationProvider>();
builder.Configuration.AddSecretsManager(logger, configurator: options => ...);
```

## Security Considerations

⚠️ **Important**: The `/config` endpoint in this sample exposes configuration keys for demonstration purposes. **Never expose secret values or keys in production applications**.

For production:
- Remove or secure the `/config` endpoint
- Use proper authentication and authorization
- Consider using structured logging instead of exposing configuration

## Use cases

- ASP.NET Core web applications needing dynamic secret updates
- Microservices that need to rotate credentials without restart
- Web APIs requiring database connection strings from Secrets Manager
- Development environments where secret changes are frequent

## Running the sample

1. Ensure AWS credentials are configured
2. Run: `dotnet run`
3. Navigate to `http://localhost:5000` for basic endpoint
4. Navigate to `http://localhost:5000/config` to see configuration keys
5. Watch console output for secret loading and polling logs

## Requirements

- AWS credentials configured
- Access to AWS Secrets Manager
- .NET 9.0 or compatible ASP.NET Core version