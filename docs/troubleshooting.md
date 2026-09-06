# Troubleshooting & FAQ

## "A secret I expect isn't showing up in configuration at all"

Check, in order:

1. **Is it a binary secret?** Secrets stored with only `SecretBinary` (no `SecretString`) are silently skipped — no key is created, no error is raised. This library doesn't decode binary secret values. If you need a binary payload, store it as a base64 string inside a `SecretString` instead.
2. **Is `SecretFilter` excluding it?** A custom `SecretFilter` predicate that returns `false` for this secret will silently exclude it (again, no error).
3. **Is it outside `AcceptedSecretArns`?** If `AcceptedSecretArns` is non-empty, only those exact entries are fetched — `ListSecrets`/`ListSecretsFilters` are not consulted at all in that mode.
4. **Does your IAM policy actually grant access to it?** `IgnoreMissingValues` only suppresses a *missing-secret* failure (`ResourceNotFoundException`) — it does **not** swallow an IAM/authorization failure (e.g. `AccessDeniedException`), which is always raised as an exception regardless of `IgnoreMissingValues`. If a secret is silently absent from configuration, a permissions problem on it is not the explanation — check the other candidates in this list first, and confirm access separately (e.g. via the AWS CLI) rather than by toggling `IgnoreMissingValues`.

## `MissingSecretValueException`

Thrown when a specific secret can't be retrieved (typically `ResourceNotFoundException` from AWS) and `IgnoreMissingValues` is `false`. It carries `SecretName` and `SecretArn` so you can identify which secret failed. Fixes: correct the ARN/name, verify the secret exists in the target account/region, verify IAM permissions, or set `IgnoreMissingValues = true` if a missing secret should be tolerated rather than fail startup.

With `UseBatchFetch = true`, this exception is not thrown directly — it's wrapped inside the `AggregateException` described above. Catch `AggregateException` and check `ex.InnerExceptions.OfType<MissingSecretValueException>()` rather than `catch (MissingSecretValueException)`, which will never trigger in batch mode.

## `AggregateException` from batch fetch

In batch mode (`UseBatchFetch = true`), any AWS-reported error for any secret in a chunk is collected and, unless every error in that chunk is a missing-secret error **and** `IgnoreMissingValues` is `true`, raised together as an `AggregateException`. This is a common point of confusion: **`IgnoreMissingValues` does not suppress every kind of batch error** — only missing-secret errors. A `DecryptionFailureException` or `InvalidParameterException` mixed into the same batch still throws, even with `IgnoreMissingValues = true`. Inspect the `AggregateException.InnerExceptions` to see exactly which secrets failed and why.

## "My JSON secret isn't being flattened into nested keys"

The provider only attempts JSON parsing if the secret value's first non-whitespace character is `{` or `[`, and only treats it as JSON if it then actually parses. If your value is meant to be JSON but doesn't start with one of those characters (e.g. it has a byte-order mark, or leading text), it will be stored as one plain-string key instead. Conversely, a string that merely *starts* with `{` or `[` but isn't valid JSON (e.g. `"{not real json}"`) safely falls back to plain-string storage rather than throwing — this is intentional, not a bug to route around.

## "Polling is enabled but changes aren't being picked up"

- Confirm `PollingInterval` is actually set (it's `null`/disabled by default).
- The provider only fires a reload notification when the fetched key/value set actually differs from what it currently holds — if the secret's value is unchanged between polls, that's correctly treated as a no-op, not a missed reload.
- Poll failures are logged as warnings and do not stop the polling loop — check logs (you need a logging overload enabled; see [Getting Started](getting-started.md)) for recurring warnings if reload seems to have stopped.

## Credential / region resolution failures

These come from the AWS SDK itself, not from this library — this library only passes through whatever `AWSCredentials`/`RegionEndpoint` you give it (or lets the SDK's own default chain resolve them). See [Authentication & Security](authentication-and-security.md) and the [AWS SDK credential configuration guide](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-creds.html).

## FAQ

**Why doesn't the provider pick up a logger automatically from DI?**
Because `IConfigurationBuilder`/`ConfigureAppConfiguration` runs before the DI container is built, there's no service provider available yet to resolve a logger from. You must pass an `ILogger<SecretsManagerConfigurationProvider>` or `ILoggerFactory` explicitly to one of the two logging overloads.

**Why does the package target `netstandard2.0` at all?**
To support consumers still on .NET Framework 4.6.2+ or older .NET Core versions, alongside modern .NET. See [Platform Support](platform-support.md).

**What's the difference between `AcceptedSecretArns`, `ListSecretsFilters`, and `SecretFilter`?**
See the "Filtering: three different mechanisms" section of [Configuration & Secret Mapping](configuration.md).

**Does this package support secret rotation?**
There's nothing rotation-specific here — rotation is an AWS Secrets Manager concept independent of this client library. If you rotate secrets and want the new value picked up without an app restart, use `PollingInterval` (or `ForceReloadAsync` triggered by your own rotation-completion event) — see [Advanced Usage](advanced.md).
