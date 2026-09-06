# Getting Started

## Installation

```bash
dotnet add package AWSSecretsManager.Provider
```

Supported target frameworks: `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`, `net11.0`. See [Platform Support](platform-support.md) for details on what `netstandard2.0` gets you (including .NET Framework 4.6.2+).

## Minimal console app

```csharp
using AWSSecretsManager.Provider;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder();
builder.AddSecretsManager();

var configuration = builder.Build();
Console.WriteLine("Secret: " + configuration["MySecret"]);
```

With no arguments, `AddSecretsManager()`:

- Resolves AWS credentials using the default AWS SDK credential chain (see [Authentication & Security](authentication-and-security.md)).
- Resolves the AWS region the same way (environment/profile/instance metadata — whatever the AWS SDK client would otherwise use).
- Lists **every** secret the resolved credentials can see (`secretsmanager:ListSecrets`) and fetches each one.

For anything beyond a quick trial, you'll usually want to scope this down — see [Configuration & Secret Mapping](configuration.md) for filtering options.

## Generic Host / Worker Service

```csharp
using AWSSecretsManager.Provider;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddSecretsManager();
    })
    .Build();

host.Run();
```

## ASP.NET Core (minimal hosting)

```csharp
var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
builder.Configuration.AddSecretsManager(
    loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromSeconds(10));

var app = builder.Build();
app.MapGet("/", () => "Hello World!");
app.Run();
```

This is the pattern used by the `SampleWeb` sample in the repository. Note the deliberate choice to pass a logger factory explicitly — see the next section for why.

## Choosing an `AddSecretsManager` overload

There are three overloads, all with the same `credentials`/`region`/`configurator` optional parameters:

```csharp
IConfigurationBuilder AddSecretsManager(
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);

IConfigurationBuilder AddSecretsManager(
    ILogger<SecretsManagerConfigurationProvider> logger,
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);

IConfigurationBuilder AddSecretsManager(
    ILoggerFactory loggerFactory,
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);
```

!!! warning "There is no automatic logger resolution"
    Unlike some `IConfigurationSource` integrations, this provider does **not** pull an `ILogger` from dependency injection automatically, even inside `ConfigureAppConfiguration` in a generic host. If you want log output (load timing, polling status, reload results), you must explicitly pass a logger or logger factory to one of the two logging overloads. The no-argument overload is always silent.

Use the `loggerFactory` overload when you already have one in scope (e.g. built once at startup) — it's the pattern used throughout the samples. Use the explicit `ILogger<SecretsManagerConfigurationProvider>` overload if you're constructing a logger some other way. Use the plain overload when you don't need diagnostics.

## Next steps

- [Configuration & Secret Mapping](configuration.md) — the full options reference and how secret values map to configuration keys.
- [Advanced Usage](advanced.md) — batch fetching, polling, and manual reload.
- [Authentication & Security](authentication-and-security.md) — credentials, region, and IAM permissions.
