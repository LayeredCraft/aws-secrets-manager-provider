# Advanced Usage

## Batch fetching

By default the provider calls `GetSecretValue` once per secret. Setting `UseBatchFetch = true` switches to `BatchGetSecretValue`, which retrieves up to **20 secrets per request**:

```csharp
builder.AddSecretsManager(configurator: options =>
{
    options.UseBatchFetch = true;
    options.ConfigureBatchSecretValueRequest = (request, contexts) =>
    {
        // customize the BatchGetSecretValueRequest here
    };
});
```

Things to know:

- The secret list (from `ListSecrets`, `AcceptedSecretArns`, or filtered by `SecretFilter`) is chunked into groups of 20 before each `BatchGetSecretValue` call.
- Requires the `secretsmanager:BatchGetSecretValue` IAM permission in addition to whatever permission fetches the secret list.
- When `UseBatchFetch` is `true`, `ConfigureSecretValueRequest` is **not** used — only `ConfigureBatchSecretValueRequest` is called.
- AWS returns full ARNs (with a random suffix, e.g. `-AbCdEf`) in the batch response even if you requested by short name or partial ARN via `AcceptedSecretArns`. The provider matches responses back to requests using exact ARN, ARN-suffix, or name matching — you don't need to do anything special to make partial ARNs or names work with batch fetch.
- If AWS reports per-secret errors inside a batch response, the provider raises an `AggregateException` wrapping one exception per failed secret — **unless** `IgnoreMissingValues` is `true` **and** every failure in the batch is a missing-secret error. A single non-missing error (e.g. a decryption failure) in an otherwise-ignorable batch still throws. This only covers errors AWS reports *inside* the batch response — a request-level failure (an authorization error, throttling, or a service error from `ListSecretsAsync` or `BatchGetSecretValueAsync` itself) propagates directly instead, uncaught by this handling. See [Troubleshooting & FAQ](troubleshooting.md).

## Polling and reload

Set `PollingInterval` to have the provider periodically re-fetch and diff its secrets:

```csharp
builder.AddSecretsManager(loggerFactory, configurator: options =>
{
    options.PollingInterval = TimeSpan.FromMinutes(5);
});
```

Behavior:

- The provider compares the newly-fetched key/value set against what it currently holds. It only calls `IConfigurationProvider.OnReload()` (firing `IChangeToken` callbacks registered via `configuration.GetReloadToken().RegisterChangeCallback(...)`) when something actually changed — an identical re-fetch is a silent no-op, not a spurious reload notification.
- If a poll fails with most exception types (network blip, throttling, transient AWS error), the failure is logged as a warning and the polling loop keeps running on the same interval. **This does not hold for `OperationCanceledException`**: any exception of that type unconditionally breaks the polling loop *silently* (no warning logged) — not only when it's caused by genuine provider shutdown, but also if it originates elsewhere (a custom `IAmazonSecretsManager` implementation, an operation-level timeout, a callback that throws it). If polling appears to have stopped with nothing in the logs, this is the likely cause.
- `PollingInterval` must be a valid non-negative `TimeSpan` (or `Timeout.InfiniteTimeSpan`). An invalid value (e.g. a negative interval other than `Timeout.InfiniteTimeSpan`) throws `ArgumentOutOfRangeException` from the delay itself, *before* the per-reload try/catch — this faults the polling task immediately with **no warning logged at all**, and is a distinct failure mode from the `OperationCanceledException` case above.
- Polling only starts if `PollingInterval` has a value; it's opt-in.
- Disposing the `SecretsManagerConfigurationProvider` cancels the *current* polling loop cleanly, and swallows the resulting `TaskCanceledException` — but if `Load()` was called more than once (each call starts a new polling task, overwriting the tracked cancellation source without stopping the previous one), only the most recently started loop is canceled; earlier loops are leaked and keep running after `Dispose()`. Don't call `Load()` directly more than once — the configuration pipeline calls it exactly once; use [`ForceReloadAsync`](#forcing-a-reload-manually) for manual reloads instead, which does not start an additional polling loop. Also note: if the polling task faulted from an invalid `PollingInterval` (see above), `Dispose()` only swallows `TaskCanceledException` and will rethrow that fault.

### Forcing a reload manually

`SecretsManagerConfigurationProvider.ForceReloadAsync(CancellationToken)` triggers the same fetch-and-diff-and-reload logic on demand, outside the polling interval. Get the provider that's actually attached to your built configuration — via `IConfigurationRoot.Providers` — rather than calling `source.Build(...)` again, which constructs a brand-new, detached provider instance whose reload has no effect on your application's `IConfiguration` or its registered change-token callbacks:

```csharp
var configuration = configBuilder.Build();

var provider = ((IConfigurationRoot)configuration).Providers
    .OfType<SecretsManagerConfigurationProvider>()
    .First();

await provider.ForceReloadAsync(CancellationToken.None);
```

This is useful for tests, admin-triggered refresh endpoints, or event-driven reload (e.g. in response to an EventBridge notification that a secret changed) instead of waiting out a polling interval.

## Custom client construction

If you need full control over how the `IAmazonSecretsManager` client is built — a custom `HttpClient`, a non-standard credential provider, request signing customization — bypass the provider's own client construction entirely:

```csharp
builder.AddSecretsManager(configurator: options =>
{
    options.CreateClient = () => new AmazonSecretsManagerClient(RegionEndpoint.EUWest1);
});
```

When `CreateClient` is set, `Region`, `Credentials`, and `ConfigureSecretsManagerConfig` are all ignored — your factory is the only thing that runs.

For lighter customization (timeouts, `ServiceUrl` for LocalStack) without giving up the provider's own credential/region wiring, use `ConfigureSecretsManagerConfig` instead — see [Platform Support](platform-support.md).

## Disposal

`SecretsManagerConfigurationProvider` implements `IDisposable`. Disposing it cancels any in-flight polling loop and waits for it to stop. It's always safe to dispose even if polling was never enabled (`PollingInterval` was `null`).
