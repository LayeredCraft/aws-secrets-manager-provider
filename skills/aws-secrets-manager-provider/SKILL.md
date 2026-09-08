---
name: aws-secrets-manager-provider
description: Helps correctly integrate and configure the AWSSecretsManager.Provider NuGet package (a Microsoft.Extensions.Configuration provider backed by AWS Secrets Manager) in .NET applications. Use when a user is adding AddSecretsManager, configuring SecretsManagerConfigurationProviderOptions, wiring AWS Secrets Manager into IConfigurationBuilder/ASP.NET Core/generic-host configuration, or diagnosing secret-to-configuration-key mapping, batch fetch, polling/reload, or credential/region issues for this specific package. Do not use for generic AWS Secrets Manager questions unrelated to .NET configuration, or for raw AWSSDK.SecretsManager usage without this package.
license: MIT
metadata:
  author: LayeredCraft
  version: 1.1.0
---

# AWSSecretsManager.Provider

`AWSSecretsManager.Provider` is a `Microsoft.Extensions.Configuration` provider that loads AWS Secrets Manager secrets as configuration key/value pairs. It is not raw `AWSSDK.SecretsManager` usage, and it is not a generic "any AWS Secrets Manager client" — its behavior (key mapping, options, error handling) is specific to this package and must not be conflated with other community Secrets Manager configuration providers or with generic AWS SDK guidance.

## Installation

```bash
dotnet add package AWSSecretsManager.Provider
```

Supported: `netstandard2.0` (→ .NET Framework 4.6.2+, .NET Core 2.0+), `net8.0`, `net9.0`, `net10.0`, `net11.0`. Native AOT is officially supported when an app targets `net8.0` or later. JSON secret flattening uses `System.Text.Json` DOM APIs and needs no source-generated serializer context.

## Core usage patterns

There are exactly three `AddSecretsManager` extension methods on `IConfigurationBuilder`, all sharing the same optional `credentials`/`region`/`configurator` parameters:

```csharp
using AWSSecretsManager.Provider;

// No logging
builder.AddSecretsManager();
builder.AddSecretsManager(credentials, region);
builder.AddSecretsManager(configurator: options => { ... });

// Explicit ILogger<SecretsManagerConfigurationProvider>
builder.AddSecretsManager(logger, credentials, region, configurator);

// ILoggerFactory (creates the logger internally, then delegates to the ILogger overload)
builder.AddSecretsManager(loggerFactory, credentials, region, configurator);
```

**Critical fact, easy to get wrong:** there is **no automatic DI-based logger resolution**. `ConfigureAppConfiguration`/`IConfigurationBuilder` setup runs before the DI container exists, so nothing is pulled from a service provider automatically. If a user wants log output (load timing, polling status, reload diffs), they must pass an `ILogger`/`ILoggerFactory` explicitly to one of the two logging overloads — never claim the plain overload logs anything, and never claim logging "just works" via DI.

Typical ASP.NET Core minimal-hosting pattern:

```csharp
using AWSSecretsManager.Provider;

var builder = WebApplication.CreateBuilder(args);
using var loggerFactory = LoggerFactory.Create(lb => lb.AddConsole());
builder.Configuration.AddSecretsManager(loggerFactory,
    configurator: options => options.PollingInterval = TimeSpan.FromSeconds(10));
```

## Options quick-reference (`SecretsManagerConfigurationProviderOptions`)

| Option                                                   | Purpose                                                                                                                                                    |
| -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AcceptedSecretArns` (`List<string>`)                    | Explicit allowlist (full/partial ARN or name). Non-empty → `ListSecrets` is skipped entirely; only these are fetched. Prefer this for least-privilege IAM. |
| `SecretFilter` (`Func<SecretListEntry, bool>`)           | Client-side predicate over each listed/accepted secret before fetching its value.                                                                          |
| `ListSecretsFilters` (`List<Filter>`)                    | Passed straight through as `ListSecretsRequest.Filters` — AWS-side narrowing.                                                                              |
| `KeyGenerator` (`Func<SecretListEntry, string, string>`) | Rewrites every flattened configuration key, default identity.                                                                                              |
| `ConfigureSecretValueRequest`                            | Customizes `GetSecretValueRequest`. Only used when `UseBatchFetch=false`.                                                                                  |
| `ConfigureBatchSecretValueRequest`                       | Customizes `BatchGetSecretValueRequest`. Only used when `UseBatchFetch=true`.                                                                              |
| `ConfigureSecretsManagerConfig`                          | Customizes `AmazonSecretsManagerConfig` before client creation (timeouts, LocalStack `ServiceURL`). Ignored if `CreateClient` is set.                      |
| `CreateClient` (`Func<IAmazonSecretsManager>?`)          | Full override of client construction; bypasses region/credentials/`ConfigureSecretsManagerConfig` entirely.                                                |
| `PollingInterval` (`TimeSpan?`)                          | `null` (default) = load once, no polling. Set to enable background reload.                                                                                 |
| `UseBatchFetch` (`bool`)                                 | `false` (default). `true` uses `BatchGetSecretValue`, chunks of ≤20, requires `secretsmanager:BatchGetSecretValue` IAM permission.                         |
| `IgnoreMissingValues` (`bool`)                           | `false` (default). Suppresses missing-secret errors only — see batch-error nuance below.                                                                   |

Don't recommend `AcceptedSecretArns` and `ListSecretsFilters` together for the same result set — if `AcceptedSecretArns` is non-empty, no `ListSecrets` call happens at all, so `ListSecretsFilters` has nothing to apply to.

## Secret naming / key mapping rules (highest-value section — read fully)

- **Plain-string secret** → one configuration key (the secret's name, through `KeyGenerator`), whole string as the value.
- **JSON object/array secret** → recursively flattened using `:`-delimited `ConfigurationPath` keys and numeric array indices, e.g. `MyDb:ConnectionStrings:Primary`, `MyDb:Replicas:0`. This is the same delimiter/shape `appsettings.json` nesting produces — do not invent a different delimiter or claim JSON.NET/Newtonsoft is used (it isn't; `System.Text.Json` only).
- **JSON detection is conservative**: only attempted if the value's first non-whitespace character is `{` or `[`; if it then fails to parse, it falls back to plain-string storage rather than throwing. A string like `"{not real json}"` is stored as-is, not an error.
- **JSON `null`** produces a configuration entry that exists with a `null` value — not omitted, not an error.
- **Binary secrets** (`SecretBinary` set, no `SecretString`) are **silently skipped** — no key, no error. This is the single most common "why isn't my secret showing up" cause.
- **Keys are case-insensitive** — `config["MySecret"]` and `config["mysecret"]` are the same key.

## Credentials & region

Default (no `credentials`/`region` passed): resolved via the standard AWS SDK for .NET credential chain and the client's own region-resolution fallback — this package adds no logic on top of that. For explicit control, pass `AWSCredentials`/`RegionEndpoint` directly, e.g. via `CredentialProfileStoreChain` for named profiles. Never invent an env-var name or resolution order this package doesn't actually implement — it's a pure pass-through to the AWS SDK client.

## Batch fetch & polling (condensed — full depth in the docs site)

- `UseBatchFetch=true`: chunks of ≤20 secrets per `BatchGetSecretValue` call; AWS returns full randomized-suffix ARNs even for short-name/partial-ARN requests, but the provider matches responses back correctly on its own — no special handling needed by the caller.
- **`IgnoreMissingValues` in batch mode only suppresses errors when EVERY error in that batch is a missing-secret error.** A single non-missing error (e.g. decryption failure) alongside missing-secret errors still throws an `AggregateException`. Do not tell a user `IgnoreMissingValues=true` makes batch fetch fully fault-tolerant — and note this whole mechanism only covers errors AWS reports _inside_ the batch response; a request-level failure (auth error, throttling, service error) from `ListSecretsAsync`/`BatchGetSecretValueAsync` itself propagates directly and is caught by neither `MissingSecretValueException` nor `AggregateException` handling.
- `PollingInterval` set → background loop; reload (`IChangeToken`/`OnReload`) only fires when the fetched key set actually differs from before; most poll failures are logged as warnings and do not stop the loop — **except** `OperationCanceledException` (always breaks the loop silently, even when not from actual shutdown) and an invalid `PollingInterval` (negative, other than `Timeout.InfiniteTimeSpan` — faults the polling task immediately, also silently). Don't claim polling survives every failure type.
- `ForceReloadAsync(CancellationToken)` on `SecretsManagerConfigurationProvider` triggers the same fetch-diff-reload logic on demand, and does **not** start an additional polling loop. Calling `Load()` again while polling is enabled stops the existing polling loop before starting a new one (no leak, but the interval clock resets); `Dispose()` cancels the tracked loop. Prefer `ForceReloadAsync` for manual reload — it refetches without touching the polling loop at all.

## Common mistakes to avoid

- Don't claim logging works without an explicit `ILogger`/`ILoggerFactory` overload.
- Don't claim binary secrets are decoded — they're skipped.
- Don't claim `IgnoreMissingValues` suppresses all batch-mode errors — only all-missing-secret batches.
- Don't recommend exposing configuration _values_ (not just key names) through a diagnostic/health endpoint.
- Don't invent options that don't exist on `SecretsManagerConfigurationProviderOptions` — the 11 listed above are the complete set.
- Don't assume this package handles secret rotation itself — rotation is an AWS Secrets Manager concept; this package only re-reads via polling/`ForceReloadAsync`.

## Where to point the user for more depth

Full documentation site: `Getting Started`, `Configuration & Secret Mapping`, `Advanced Usage`, `Authentication & Security`, `Platform Support` (LocalStack), `Troubleshooting & FAQ`, `API Reference` — https://layeredcraft.github.io/aws-secrets-manager-provider/
