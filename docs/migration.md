# Migration & Compatibility Notes

This page records observable behavior changes between package versions so existing consumers can assess compatibility impact before upgrading. It is updated alongside the packages, one section per breaking or externally visible change.

## Duplicate configuration keys (AWSSecretsManager.Provider 1.3.0)

When two or more secrets (or flattened JSON properties) map to the same configuration key — after key generation and case normalization — the provider's contract changed in 1.3.0 of `AWSSecretsManager.Provider`:

| Scenario (case-insensitive key comparison) | Old behavior                                              | New behavior                                                                                     |
| ------------------------------------------ | --------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Same key, same value                       | `ArgumentException` from `ToDictionary` when `Load()` ran | Warning logged, extra entry ignored, `Load()` succeeds                                           |
| Same key, different values                 | `ArgumentException` from `ToDictionary` when `Load()` ran | `InvalidOperationException` with a descriptive message and guidance naming the options to adjust |

What this means for compatibility:

- **Exception type changed**: if you catch `ArgumentException` around configuration building (`IConfigurationBuilder.Build()`, `AddSecretsManager()`, or a provider's `Load()`), you must update your `catch` clauses to `InvalidOperationException` (or the base `Exception`). This affects both `Load()` and polling-triggered reloads.
- **Identical duplicates no longer fail**: the previous version _always_ threw when a duplicate key was generated, even when the conflicting key/value pairs were identical. The new version treats a case-insensitively identical key/value pair as redundant: it logs a warning (`Duplicate configuration key '{ConfigurationKey}' was generated more than once with an identical value; the extra entry was ignored`) and continues. If you relied on the throw as a failsafe against misconfigured filters or key generators, that failsafe now only fires for _conflicting_ values.
- **Keys are case-insensitive**: configuration keys are stored case-insensitively (matching `IConfiguration` semantics), so a secret generating `Foo` and another generating `foo` are the same key. Identical values (byte-for-byte) warn and skip; different values throw.

If you hit the `InvalidOperationException` in production, the message names the key and points at the options that likely caused the collision — typically `KeyGenerator`, `SecretFilter`/`ParameterFilter`, or overly broad `Path` (SSM).

The SSM provider (`AWSSSM.Provider`) ships with this contract from its first release; no migration applies to it.
