# API Reference

This page is hand-curated against the current public API in `src/AWSSecretsManager.Provider/`. If you find a mismatch with the installed package version, please open an issue.

## `AWSSecretsManager.Provider.SecretsManagerExtensions`

Static class of `IConfigurationBuilder` extension methods.

```csharp
public static IConfigurationBuilder AddSecretsManager(
    this IConfigurationBuilder configurationBuilder,
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);
```
Adds the provider with no logging.

```csharp
public static IConfigurationBuilder AddSecretsManager(
    this IConfigurationBuilder configurationBuilder,
    ILogger<SecretsManagerConfigurationProvider> logger,
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);
```
Adds the provider using the given logger for diagnostic output.

```csharp
public static IConfigurationBuilder AddSecretsManager(
    this IConfigurationBuilder configurationBuilder,
    ILoggerFactory loggerFactory,
    AWSCredentials? credentials = null,
    RegionEndpoint? region = null,
    Action<SecretsManagerConfigurationProviderOptions>? configurator = null);
```
Creates an `ILogger<SecretsManagerConfigurationProvider>` from `loggerFactory` and delegates to the overload above.

All three return the same `configurationBuilder` for chaining, and all three accept an optional `region` — if non-null, it's assigned to the resulting source's `Region` property after construction.

## `AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationProviderOptions`

See the full property table with defaults and examples in [Configuration & Secret Mapping](configuration.md#secretsmanagerconfigurationprovideroptions-reference).

| Member | Signature |
|---|---|
| `AcceptedSecretArns` | `List<string>` |
| `SecretFilter` | `Func<SecretListEntry, bool>` |
| `ListSecretsFilters` | `List<Filter>` |
| `KeyGenerator` | `Func<SecretListEntry, string, string>` |
| `ConfigureSecretValueRequest` | `Action<GetSecretValueRequest, SecretValueContext>` |
| `ConfigureBatchSecretValueRequest` | `Action<BatchGetSecretValueRequest, List<SecretValueContext>>` |
| `ConfigureSecretsManagerConfig` | `Action<AmazonSecretsManagerConfig>` |
| `CreateClient` | `Func<IAmazonSecretsManager>?` |
| `PollingInterval` | `TimeSpan?` |
| `UseBatchFetch` | `bool` |
| `IgnoreMissingValues` | `bool` |

## `AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationProvider`

```csharp
public class SecretsManagerConfigurationProvider : ConfigurationProvider, IDisposable
{
    public SecretsManagerConfigurationProviderOptions Options { get; }
    public IAmazonSecretsManager Client { get; }

    public SecretsManagerConfigurationProvider(
        IAmazonSecretsManager client,
        SecretsManagerConfigurationProviderOptions options,
        ILogger? logger = null);

    public override void Load();
    public Task ForceReloadAsync(CancellationToken cancellationToken);
    public void Dispose();
}
```

- `Load()` is called once by the `Microsoft.Extensions.Configuration` pipeline when the configuration is built; it synchronously blocks on the async fetch (required by the base `ConfigurationProvider` contract) and starts the background polling loop if `PollingInterval` is set. It runs again — starting an *additional* polling task without stopping the previous one — both if called directly and if `IConfigurationRoot.Reload()` (a standard framework API that calls `Load()` on every registered provider) is called while polling is enabled. See [Advanced Usage](advanced.md#polling-and-reload).
- `ForceReloadAsync` re-fetches and, if the resulting key set differs from what's currently loaded, fires a reload notification — usable independently of polling, and does not start an additional polling loop. Prefer this over `IConfigurationRoot.Reload()` for a configuration root that includes this provider with polling enabled.
- `Dispose()` cancels and awaits the *most recently started* polling loop, swallowing `TaskCanceledException`; safe to call whether or not polling was ever enabled. It does not track or cancel earlier polling loops left over from `Load()` running more than once. It also rethrows any other exception the polling task faulted with — notably an `ArgumentOutOfRangeException` from an invalid `PollingInterval` (negative, other than `Timeout.InfiniteTimeSpan`), which faults the polling task immediately via `Task.Delay`, outside the per-reload error handling and with no warning logged — a distinct, silent failure mode from the `OperationCanceledException` loop-termination case.
- Constructor throws `ArgumentNullException` if `client` or `options` is `null`.

## `AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationSource`

```csharp
public class SecretsManagerConfigurationSource : IConfigurationSource
{
    public SecretsManagerConfigurationSource(
        AWSCredentials? credentials = null,
        SecretsManagerConfigurationProviderOptions? options = null);

    public SecretsManagerConfigurationProviderOptions Options { get; }
    public AWSCredentials? Credentials { get; }
    public RegionEndpoint? Region { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder);
}
```

`Build` always constructs its `SecretsManagerConfigurationProvider` with `logger: null` — this source type never logs. Produced by the no-logger `AddSecretsManager` overload.

## `AWSSecretsManager.Provider.Internal.SecretsManagerConfigurationSourceWithLogger`

```csharp
public class SecretsManagerConfigurationSourceWithLogger : IConfigurationSource
{
    public SecretsManagerConfigurationSourceWithLogger(
        AWSCredentials? credentials,
        SecretsManagerConfigurationProviderOptions options,
        ILogger logger);

    public RegionEndpoint? Region { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder);
}
```

Constructor throws `ArgumentNullException` if `options` or `logger` is `null` (`credentials` may be `null`). Produced by the two logging `AddSecretsManager` overloads.

## `AWSSecretsManager.Provider.Internal.SecretValueContext`

```csharp
public class SecretValueContext
{
    public SecretValueContext(SecretListEntry secret);

    public string Name { get; }
    public Dictionary<string, List<string>> VersionsToStages { get; }
}
```

Passed into `ConfigureSecretValueRequest`/`ConfigureBatchSecretValueRequest` callbacks so you can inspect the secret's name and version-stage mapping without needing the full `SecretListEntry`. Constructor throws `ArgumentNullException` if `secret` is `null`.

## `AWSSecretsManager.Provider.Internal.MissingSecretValueException`

```csharp
public class MissingSecretValueException : Exception
{
    public MissingSecretValueException(
        string errorMessage, string secretName, string secretArn, Exception exception);

    public string SecretArn { get; }
    public string SecretName { get; }
}
```

Thrown **directly** by the single-fetch path when a secret can't be retrieved and `IgnoreMissingValues` is `false`. In batch mode (`UseBatchFetch = true`) there are two distinct paths: a per-secret missing-value failure returned inside the batch response is wrapped inside an `AggregateException` (unless `IgnoreMissingValues` is `true` **and** every error in that batch is a missing-secret error), while a request-level `ResourceNotFoundException` from the `BatchGetSecretValueAsync` call itself is rethrown as `MissingSecretValueException` **directly** — unconditionally, regardless of `IgnoreMissingValues`. A caller using `UseBatchFetch` should catch both `MissingSecretValueException` directly and `AggregateException` (inspecting `InnerExceptions` for further `MissingSecretValueException` entries) to handle *missing-secret* failures specifically. This does **not** cover every possible batch-mode failure: other AWS SDK exceptions — an authorization failure, throttling, or a service error from `ListSecretsAsync` or `BatchGetSecretValueAsync` itself — are only caught if they match `ResourceNotFoundException`; anything else propagates directly, uncaught by either of these two shapes. See [Troubleshooting & FAQ](troubleshooting.md).
