# Configuration & Secret Mapping

## `SecretsManagerConfigurationProviderOptions` reference

Pass a `configurator` delegate to any `AddSecretsManager` overload to set these:

```csharp
builder.AddSecretsManager(configurator: options =>
{
    options.PollingInterval = TimeSpan.FromMinutes(5);
});
```

| Option | Type | Default | Purpose |
|---|---|---|---|
| `AcceptedSecretArns` | `List<string>` | empty | Explicit allowlist of secret ARNs (full or partial) or names. When non-empty, `ListSecrets` is **skipped entirely** — only these are fetched directly. This is the recommended way to scope access down instead of granting `secretsmanager:ListSecrets` broadly. |
| `SecretFilter` | `Func<SecretListEntry, bool>` | `_ => true` | Predicate applied to each listed secret (or, in batch mode, to the accepted set before chunking) to decide whether to fetch it. |
| `ListSecretsFilters` | `List<Filter>` | empty | Passed straight through as `ListSecretsRequest.Filters`, so filtering happens AWS-side rather than after listing everything. |
| `KeyGenerator` | `Func<SecretListEntry, string, string>` | `(secret, key) => key` | Rewrites the final configuration key from `(secretEntry, rawKey)`. Runs on every flattened key, not just the secret's own name — e.g. `options.KeyGenerator = (entry, key) => key.ToUpper();`. |
| `ConfigureSecretValueRequest` | `Action<GetSecretValueRequest, SecretValueContext>` | no-op | Customizes each `GetSecretValueRequest` before it's sent. **Only used when `UseBatchFetch` is `false`.** |
| `ConfigureBatchSecretValueRequest` | `Action<BatchGetSecretValueRequest, List<SecretValueContext>>` | no-op | Customizes the `BatchGetSecretValueRequest` before it's sent. **Only used when `UseBatchFetch` is `true`.** |
| `ConfigureSecretsManagerConfig` | `Action<AmazonSecretsManagerConfig>` | no-op | Customizes the `AmazonSecretsManagerConfig` before the client is built (timeouts, `ServiceUrl` for LocalStack — see [Platform Support](platform-support.md)). Ignored if `CreateClient` is set. |
| `CreateClient` | `Func<IAmazonSecretsManager>?` | `null` | Full override for client construction. When set, this bypasses region, credentials, and `ConfigureSecretsManagerConfig` entirely — you own the client completely. |
| `PollingInterval` | `TimeSpan?` | `null` | Enables a background reload loop on this interval. `null` (the default) means the provider loads once and never polls. See [Advanced Usage](advanced.md). |
| `UseBatchFetch` | `bool` | `false` | Use `BatchGetSecretValue` (up to 20 secrets per request) instead of one `GetSecretValue` call per secret. Requires the `secretsmanager:BatchGetSecretValue` IAM permission. See [Advanced Usage](advanced.md). |
| `IgnoreMissingValues` | `bool` | `false` | Suppress errors for secrets that don't exist instead of throwing `MissingSecretValueException`. See [Advanced Usage](advanced.md) for the batch-mode nuance. |

## How a secret value becomes configuration keys

This is the part that's easy to get wrong, so it's worth reading in full.

### Plain-string secrets

If a secret's value isn't recognized as JSON (see below), it becomes exactly **one** configuration key: the secret's name (run through `KeyGenerator`), with the whole string as the value.

```
Secret name: MyConnectionString
Secret value: "Server=...;Database=...;"
→ configuration["MyConnectionString"] == "Server=...;Database=...;"
```

### JSON object/array secrets

If a secret's value parses as a JSON object or array, it is **recursively flattened** into multiple configuration keys, using `:` as the path delimiter (the same delimiter `Microsoft.Extensions.Configuration` itself uses for `appsettings.json` nesting) and numeric indices for array elements:

```json
{
  "ConnectionStrings": {
    "Primary": "Server=...",
    "Replicas": ["Server=replica1", "Server=replica2"]
  }
}
```

Stored under a secret named `MyDb`, this produces:

```
MyDb:ConnectionStrings:Primary        = "Server=..."
MyDb:ConnectionStrings:Replicas:0     = "Server=replica1"
MyDb:ConnectionStrings:Replicas:1     = "Server=replica2"
```

A JSON `null` value produces a configuration entry that **exists with a `null` value** (`configuration.GetSection(...).Exists()` returns `true`), rather than being omitted or throwing.

### Detecting JSON vs. plain string

Detection is deliberately conservative: the provider only *attempts* JSON parsing if the value's first non-whitespace character is `{` or `[`. If parsing then fails (e.g. the value is the literal string `"{THIS IS NOT AN OBJECT}"`), the provider falls back to treating it as a plain string rather than throwing — it does not require you to escape or otherwise mark plain strings that happen to start with a brace or bracket.

### Binary secrets are silently skipped

If a secret has only `SecretBinary` set (no `SecretString`), the provider **skips it entirely** — no configuration key is created and no error is raised. This library does not decode binary secret values. If a secret you expect to see is missing from configuration, check whether it's a binary secret first (see [Troubleshooting & FAQ](troubleshooting.md)).

### Key lookups are case-insensitive

All configuration keys are stored with a case-insensitive comparer, matching the rest of `Microsoft.Extensions.Configuration`'s conventions: `configuration["MySecret"]` and `configuration["mysecret"]` return the same value.

## Filtering: three different mechanisms, three different purposes

It's easy to reach for the wrong one:

- **`AcceptedSecretArns`** — use this when you know exactly which secrets you want by ARN or name, and you want to avoid calling `ListSecrets` at all (least privilege, fewest API calls).
- **`ListSecretsFilters`** — use this when you want AWS itself to narrow the `ListSecrets` result (e.g. by tag or name prefix) before anything is fetched.
- **`SecretFilter`** — use this when the decision needs client-side logic that `ListSecretsFilters` can't express (it runs against every listed/accepted `SecretListEntry` before fetching its value).

`AcceptedSecretArns` and `ListSecretsFilters`/`SecretFilter` are not meant to be combined for the same secret set — if `AcceptedSecretArns` is non-empty, `ListSecretsFilters` is never sent (there's no `ListSecrets` call to send it on), though `SecretFilter` still applies to the accepted set.
