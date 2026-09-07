# Plan 0002: SSM Parameter Store Configuration Provider

Status: draft
Branch: `feat/ssm-parameter-store`
Goal: Add `AWSSSM.Provider` — a Microsoft.Extensions.Configuration provider backed by SSM Parameter Store — reusing existing patterns (architecture, testing, docs, skill), with Floci-backed integration tests.

---

## Key decisions (up front)

| Decision | Choice | Notes |
|---|---|---|
| Package name | `AWSSSM.Provider` | Mirrors `AWSSecretsManager.Provider` branding. Short, unambiguous. |
| Namespace / root class | `AWSSSM.Provider` / `SsmExtensions.AddSsmParameters(...)` | Three overloads (plain / `ILogger` / `ILoggerFactory`), same shape as Secrets Manager extensions. |
| TFMs | `netstandard2.0; net8.0; net9.0` | Match main lib. No net10/net11 initially. |
| AWS SDK | `AWSSDK.SimpleSystemsManagement` | Centrally managed in `Directory.Packages.props`. |
| Slicing strategy | `GetParametersByPath` (single paginated call) | No separate "list" step — one API does list+fetch. Batch mode not needed. |
| Key mapping | `/MyApp/Db/Host` → `MyApp:Db:Host` | Strip path prefix, convert `/` → `:`. JSON values flatten with existing logic. |
| Integration test emulator | Floci via official `Testcontainers.Floci` module | SSM fully emulated (String, StringList, SecureString + decryption, versions). |
| SecureString | `WithDecryption = true` default | Floci in-process KMS handles default key decryption. |

**Known constraint — `Testcontainers.Floci` feed:** ~~The official .NET module is published to the floci-io GitHub Packages feed~~ **RESOLVED during Phase 0**: the official `Testcontainers.Floci` module ships on **nuget.org** (4.15.0, maintained in the `testcontainers/testcontainers-dotnet` repo itself by the Testcontainers team — `FlociBuilder`/`FlociContainer`, health-check wait strategy built in). No nuget.config or PAT needed. The floci-io GitHub Packages module remains an alternative (typed per-service config) but was not required. Image pinned to `floci/floci:2.0.1`.

---

## Phase 0 — Scaffolding

1. `git checkout -b feat/ssm-parameter-store` (done)
2. Add projects to `AWSSecretsManager.slnx`:
   - `src/AWSSSM.Provider/AWSSSM.Provider.csproj`
   - `tests/AWSSSM.Provider.Tests/AWSSSM.Provider.Tests.csproj`
3. `Directory.Packages.props`:
   - AWS SDK group: `AWSSDK.SimpleSystemsManagement`
   - Testing group: `Testcontainers.Floci` (feed-pinned version)
4. Root `nuget.config` with `packageSources` (nuget.org + github-floci-io) and `packageSourceMapping` routing **only** `Testcontainers.Floci` to the floci feed.
5. New test csproj copies shape of existing test project (OutputType Exe, MTP runner, `ComponoGeneratedTestDoubles`, TFM `net8.0;net9.0`) + `Testcontainers.Floci` reference.
6. CI: `pr-build.yaml` already `secrets: inherit` — add `GITHUB_PACKAGES_PAT` secret to repo; confirm reusable `pr-build` workflow runs `dotnet test` with Docker available, else integration tests must self-skip (Phase 3 handles this).

## Phase 1 — Extract shared JSON flattener (no behavior change)

1. Create `src/AWSSecretsManager.Provider/Internal/JsonFlattener.cs` — move `TryParseJson` + `ExtractValues` (currently private static in `SecretsManagerConfigurationProvider.cs:174-265`) into internal static class.
2. Secrets Manager provider calls it. No public API change.
3. Existing test suite must stay green (this is the regression gate).

## Phase 2 — SSM provider (minimum code)

New files in `src/AWSSSM.Provider/`:

```
Internal/
  SsmConfigurationProviderOptions.cs   // options only
  SsmConfigurationProvider.cs          // provider
  SsmConfigurationSource.cs            // IConfigurationSource
SsmExtensions.cs                       // AddSsmParameters overloads
```

### Options (complete set — keep it minimal)

| Option | Default | Purpose |
|---|---|---|
| `Path` | `"/"` | Parameter hierarchy to fetch (`GetParametersByPath.Path`). |
| `Recursive` | `true` | Fetch nested paths. |
| `WithDecryption` | `true` | Decrypt SecureString values. |
| `ParameterFilter` | `_ => true` | Client-side predicate over each returned `Parameter`. |
| `KeyGenerator` | strip-prefix-and-`/`→`:` | Rewrite configuration keys. |
| `ConfigureSsmConfig` | no-op | Customize `AmazonSimpleSystemsManagementConfig`. |
| `CreateClient` | `null` | Full client override (test seam). |
| `PollingInterval` | `null` | Optional background reload loop. |

Deliberately omitted: `IgnoreMissingValues` (GetParametersByPath never throws for absent paths — empty result is legal), batch knobs (single API call already batched).

### Provider behavior

1. Paginated `GetParametersByPathAsync` (NextToken loop) — all values in one sweep.
2. Apply `ParameterFilter`.
3. Key mapping: remove `Path` prefix, replace `/` with `:`; empty segments trimmed.
4. Value handling: JSON first-char sniff (`{`/`[`) → `JsonFlattener`; else plain string. Reuses Phase 1 flattener.
5. Polling loop: same diff-then-`OnReload()` pattern as Secrets Manager provider (copy, ~60 lines, no logging-mandatory variant — accept optional `ILogger` like the SM provider).
6. Reuse `LayeredCraft.StructuredLogging` timing extensions in `Load()`.

### Unit tests (`tests/AWSSSM.Provider.Tests/`)

Same strategy as Secrets Manager tests:

- `ComponoTestProfile`-style profile registering `IAmazonSSM` generated test double (`ComponoGeneratedTestDoubles` already on in csproj), `SsmConfigurationProviderOptions`, sample `Parameter` objects.
- Extension methods on the double: `SetGetParametersByPathResponse(...)`, `QueuePagedResponses(...)`, `ThrowOnGetParametersByPath(...)`.
- Cover: path prefix stripping, `/`→`:` mapping, recursive vs non-recursive, StringList → comma? **No** — StringList arrives as single string; store as-is (document), SecureString with/without decryption, JSON flattening (reuse existing Root/Mid/Leaf test shapes), pagination, polling diff-reload, case-insensitive keys.

## Phase 3 — Floci integration tests

1. `tests/AWSSSM.Provider.Tests/Integration/FlociFixture.cs`:
   ```csharp
   var _floci = new FlociBuilder("floci/floci:1.5.22")
       .WithRegion("us-east-1")
       .Build();
   // client: new AmazonSSMClient(_floci.AccessKey, _floci.SecretKey,
   //     new AmazonSimpleSystemsManagementConfig { ServiceURL = _floci.GetEndpoint(), ... })
   ```
   `IAsyncLifetime`, one container per test class collection (startup cost dominates).
2. Integration test coverage (real wire protocol):
   - Seed hierarchy via `PutParameter` → build config via `AddSsmParameters` → assert keys/values incl. nested JSON flattening.
   - SecureString round-trip with `WithDecryption=true`.
   - Pagination with >10 params (SSM page size 10).
   - Polling: change a param, wait one interval, assert `IConfiguration` reload token fires.
3. Skip guard: detect Docker daemon at runtime; if absent, `Assert.Skip` (xunit.v3 supports dynamic skip). Keeps non-Docker local runs and CI-without-docker green.
4. CI: add `GITHUB_PACKAGES_PAT` repo secret; if reusable workflow can't pass it to `dotnet restore`, activate fallback (LocalStackBuilder + floci image) — one-file change.

## Phase 4 — Docs + skill (repo conventions)

1. **Docs site** (`docs/`, mkdocs):
   - New page `docs/ssm-parameter-store.md`: install, `AddSsmParameters` usage, key mapping rules table, options table, SecureString note, polling, Floci/local testing snippet.
   - `mkdocs.yml` nav: insert after "Advanced Usage".
   - Update `docs/index.md`, `getting-started.md` (mention second provider), `api-reference.md` (new types), `platform-support.md` (Floci section).
2. **README.md**: add "Sibling package" section — one paragraph + install snippet.
3. **Skill** — new `skills/aws-ssm-provider/`:
   - `SKILL.md` mirroring existing skill structure: description with trigger phrases (`AddSsmParameters`, `AWSSSM.Provider`, SSM key mapping), options table, key mapping rules ("highest-value section"), polling gotchas, common mistakes.
   - `evals/evals.json` — follow existing eval format (`scripts/validate_skill.py` validates skills + evals; run it in verification).
   - Update `docs/agent-skill.md` (lists available skills).

## Phase 5 — Verification (definition of done)

```bash
dotnet build
cd tests/AWSSecretsManager.Provider.Tests && dotnet run --framework net8.0   # SM suite green
cd tests/AWSSSM.Provider.Tests && dotnet run --framework net8.0              # SSM suite green (unit + integration)
dotnet pack src/AWSSSM.Provider/
uv run scripts/validate_skill.py                                             # skill + evals valid
```

- Docker running for integration tests; without Docker, integration tests skip and unit suites still pass.
- Coverage threshold 70% maintained (CI).

## Explicitly out of scope

- AppConfig provider (separate plan — different session-based API).
- Batch/parallel fetch knobs, net10/net11 TFMs, new sample app (Sample7-style sample only if time permits).
- Any change to Secrets Manager provider public API.
