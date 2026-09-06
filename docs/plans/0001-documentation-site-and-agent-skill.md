# Plan 0001: GitHub Pages Documentation Site + Downloadable Agent Skill

Status: IMPLEMENTED (Tasks 1-9 complete locally; live Pages deployment verification pending user-authorized push/merge to `main` — see §10)
Owner: Nick Cipollina
Created: 2026-09-05

This plan is the authoritative source of truth for this work. Once implementation begins, do not fall back to conversational context, memory, or assumptions — read the relevant section of this plan before each task, implement what it specifies, and update this document if reality diverges from it (see "Plan-as-source-of-truth" governance at the end).

No ADR is created for this work per explicit instruction; design rationale lives in this plan's "Decisions" section instead.

---

## 1. Context and goals

### Current repository state (verified by direct inspection, 2026-09-05)

- `AWSSecretsManager.Provider` is a small, focused .NET library (netstandard2.0, net8.0, net9.0, net10.0, net11.0) providing an AWS Secrets Manager `IConfigurationSource`/`IConfigurationProvider` for `Microsoft.Extensions.Configuration`.
- Public surface is narrow: `SecretsManagerExtensions` (3 `AddSecretsManager` overloads), `SecretsManagerConfigurationProviderOptions` (10 properties), `SecretsManagerConfigurationProvider`, `SecretsManagerConfigurationSource` / `SecretsManagerConfigurationSourceWithLogger`, `SecretValueContext`, `MissingSecretValueException`. Full member-level detail is in §5 (API reference source material) below.
- README.md is a landing page with quickstart, logging examples, a shallow options bullet list, and a samples table. It does **not** document: every option (`AcceptedSecretArns`, `SecretFilter`, `ListSecretsFilters`, `ConfigureSecretValueRequest`/`ConfigureBatchSecretValueRequest`, `ConfigureSecretsManagerConfig`, `CreateClient`, `PollingInterval`, `UseBatchFetch`, `IgnoreMissingValues`), the JSON-flattening/key-mapping scheme, `MissingSecretValueException`, `ForceReloadAsync`, batch-fetch 20-item chunking and partial-ARN matching, binary-secrets-are-silently-skipped behavior, or the case-insensitive key comparer.
- 8 sample projects (`samples/Sample1`-`Sample7`, `SampleWeb`) demonstrate real usage patterns end-to-end (basic, region, profile-based credentials, ARN allowlist, key casing, custom client factory, advanced logging/polling/reload, ASP.NET Core hosting).
- Tests (`tests/AWSSecretsManager.Provider.Tests/`, XUnit v3 + Compono) exhaustively exercise real behavior including edge cases (binary secrets ignored, malformed-JSON-looking strings fall back to plain string, JSON `null` handling, batch partial-ARN matching, polling/`ForceReloadAsync` reload semantics) — these are ground truth for documentation content, not the README.
- CI/CD is fully templated through `LayeredCraft/devops-templates@v10.5` (`pr-build.yaml`, `publish-preview.yaml`, `publish-release.yaml`, `release-drafter.yaml`, `pr-title-check.yaml`) plus a self-contained `dependabot-auto-merge.yml`. No workflow currently touches documentation or GitHub Pages.
- **No** `docs/` folder, no `_config.yml`/`CNAME`, no docfx/mkdocs/docusaurus config, no GitHub Pages configuration exists anywhere in the repo or (verified via `gh api repos/LayeredCraft/aws-secrets-manager-provider/pages`) in the GitHub repo settings — confirmed 404 "Not Found", i.e. Pages has never been enabled for this repo.
- No `docs/plans/` convention exists yet in this repo (unlike the reference `compono` repo, which uses `NNNN-slug.md`). This plan introduces that same numbering convention (`docs/plans/NNNN-slug.md`) for consistency with the org's established practice in the reference repo.

### Problem being solved

Consumers adopting this package today must read source/tests to learn most of its real behavior (options, JSON flattening, batch semantics, error handling). There is no durable, browsable reference, and no way for an AI coding agent to reliably help a consumer use the package correctly without re-deriving all of this from source each time.

### Desired consumer experience

- A developer unfamiliar with the package can land on a GitHub Pages site, follow Getting Started, and successfully adopt every supported configuration pattern (batch fetch, polling, filtering, custom key naming, LocalStack, ASP.NET Core/worker hosting) without opening source.
- An AI coding agent (Claude Code or any Agent-Skills-compatible host) can install a small, accurate skill (`npx skills add LayeredCraft/aws-secrets-manager-provider`) and thereafter give correct, package-specific answers instead of generic/stale AWS SDK guidance.
- README stays a concise, accurate landing page; the docs site is the single deep source of truth; the skill is a distilled, agent-facing derivative of the same facts — no content is invented independently in three places.

### Scope

1. A GitHub Pages documentation site (MkDocs Material), built and deployed via GitHub Actions using the "Actions" Pages build type.
2. A hand-curated API reference page (no generated-doc toolchain).
3. A downloadable Agent Skill at `skills/aws-secrets-manager-provider/` with `SKILL.md`, `evals/evals.json`, and an eval workspace convention with correct `.gitignore` coverage.
4. A trimmed README that links to the docs site and the skill instead of duplicating their content.
5. GitHub Pages enabled on the repo via `gh api` (Actions build type).
6. CI: a docs build workflow (build-and-deploy) and a lightweight skill-structure/evals-schema validation workflow.

### Explicit non-goals

- No generated API reference toolchain (DefaultDocumentation or DocFX) — rejected per interview decision (§3).
- No multi-version docs (no `mike`, no version selector) — the site always reflects `main`, consistent with the package's own single-line versioning and the reference repo's approach.
- No `.skill` zip packaging/release automation — distribution is git-path only (`npx skills add`), matching the reference repo's actual (not aspirational) convention.
- No automated LLM-based eval grading in CI — evals are authored and schema-validated in CI; grading runs are a manual/local workflow.
- No ADR for this work.
- No git commit, push, PR, merge, NuGet publish, or GitHub release as part of this work unless the user explicitly asks separately. `gh api` calls to inspect/configure GitHub Pages are explicitly in scope and pre-authorized.
- Not modifying `/Users/ncipollina/source/repos/layered-craft/compono` (reference-only, read-only).

---

## 2. Research findings

### From the reference `compono` repository (read-only inspection)

- **Docs tooling**: MkDocs Material, built with `uv` (Python), `mkdocs.yml` at repo root, `pyproject.toml` pinning `mkdocs-material>=9.0.0` + `mkdocs-minify-plugin>=0.7.0`, locked via `uv.lock`. `theme.features` includes `content.code.copy`, `navigation.instant`, `search.suggest`, etc.; `validation.links.anchors: warn`; `markdown_extensions` includes admonitions, `pymdownx.superfences` (with a mermaid custom fence), tabbed content, tasklists.
- **Pages build type**: "Actions" build type — `actions/configure-pages@v6` → `actions/upload-pages-artifact@v5` → `actions/deploy-pages@v5`, **not** `peaceiris/actions-gh-pages` and not the legacy branch-based Pages mode. Two jobs (`build`, `deploy`), `deploy` gated `if: github.ref == 'refs/heads/main'`. Permissions `contents: read, pages: write, id-token: write`; `concurrency: {group: "pages", cancel-in-progress: false}`.
- **API reference generation**: DefaultDocumentation dotnet tool reflecting over compiled Release DLL + XML doc file, with a CI drift-check (`git diff --exit-code`) forcing regenerated docs to be committed. Explicitly documented (ADR-0032) as chosen over DocFX to avoid a second theme/nav/search index, and over hand-written docs to avoid drift — but this only pays for itself across Compono's many packages and much larger API surface; not adopted here (see Decisions §3).
- **IA depth**: ~10 top-level Diátaxis-adapted areas (getting-started, concepts, how-to, cookbook, migrating-from-autofixture, packages, architecture, troubleshooting, reference, roadmap) — proportionate to a multi-package framework with a large surface, not to this single narrow-surface package.
- **Skill structure**: `skills/compono/{SKILL.md, references/*.md (15 files), evals/evals.json (46 cases)}`. SKILL.md includes a Detection table (which reference file to load per installed package), a numbered default workflow, Guardrails, "When not to use", and a References index table — the `references/` split exists specifically because Compono has many optional integration packages with conditionally-relevant content, which this package does not have.
- **Distribution**: `npx skills add LayeredCraft/compono` (git-path convention, no zip/version pin, `npx skills update compono` to refresh). A generic `.skill` zip-packaging script exists in the repo's private skill-creator tooling but is **not** wired into Compono's actual distribution — confirmed not to be the real convention in use.
- **Eval schema actually used**: `{skill_name, evals: [{id, category, prompt, expected_output, files, expectations}]}` — note Compono's own field is named `expectations`, not `assertions`. This plan uses the field name `assertions` instead, matching the authoritative agentskills.io spec (see below) rather than Compono's internal deviation, since the task instructions point at the agentskills.io doc as the source of truth.
- **Eval workspace + grading**: two non-overlapping mechanisms — a generic trigger-only harness (`run_eval.py`, measures whether a skill activates at all) and a manual/agent-driven content-grading convention producing `skills/compono-workspace/{skill-snapshot/, iteration-N/{eval-<case>/{with_skill,without_skill}/{outputs/,timing.json,grading.json}, benchmark.json, feedback.json}}`. **No GitHub Actions workflow validates or runs any of this** — it is entirely manual/local.
- **`.gitignore` convention** (exact, reused verbatim in §4): raw `outputs/` directories and `skill-snapshot/` are ignored; `grading.json`/`timing.json`/`benchmark.json`/`feedback.json` are **committed** as evaluation evidence.
- **Docs↔skill cross-linking**: a dedicated `docs/getting-started/ai-agent-skill.md` page plus a README section, both pointing at the same install command and the skill's repo path.
- **Plan file naming convention**: `docs/plans/NNNN-slug.md`, sequential, no per-plan status file elsewhere — status is a field inside the plan.

### External research

- **Agent Skills evaluation methodology** — [agentskills.io/skill-creation/evaluating-skills](https://agentskills.io/skill-creation/evaluating-skills) (fetched in full 2026-09-05). Authoritative points adopted directly into this plan:
  - `evals/evals.json` schema: `{skill_name, evals: [{id, prompt, expected_output, files, assertions}]}` — `assertions` (not `expectations`) is the correct field name per this spec; `files` is an array of paths under `evals/files/`.
  - "Start with 2-3 test cases... expand later" — but the task's explicit coverage requirement (normal, advanced, edge case, skill-value-add) drives this plan to 6 cases (§ Agent Skill design), still well short of "unnecessarily huge."
  - Workspace tree: `<skill>-workspace/iteration-N/eval-<case>/{with_skill,without_skill}/{outputs/, timing.json, grading.json}` plus `iteration-N/benchmark.json`. `timing.json = {total_tokens, duration_ms}`. `grading.json = {assertion_results: [{text, passed, evidence}], summary: {passed, failed, total, pass_rate}}`. `benchmark.json = {run_summary: {with_skill: {...}, without_skill: {...}, delta: {...}}}`.
  - Baseline-for-improving-an-existing-skill guidance: snapshot the previous skill (`cp -r <skill> <workspace>/skill-snapshot/`), point the baseline run at the snapshot, save to `old_skill/outputs/` instead of `without_skill/outputs/`. This repo's skill is new (no prior version), so `skill-snapshot/` support is set up structurally (folder + `.gitignore` rule) but not populated at this time.
  - Grading principles: require concrete evidence for PASS; prefer scripts over LLM judgment for mechanically checkable assertions; blind comparison for holistic quality; iterate by feeding failed assertions + human feedback + transcripts back to an LLM to propose `SKILL.md` changes.
- **Agent Skills format spec** — [github.com/anthropics/skills/blob/main/spec/agent-skills-spec.md](https://github.com/anthropics/skills/blob/main/spec/agent-skills-spec.md) and [agentskills.io/specification](https://agentskills.io/specification): `SKILL.md` frontmatter requires `name` (lowercase/digits/hyphens, ≤64 chars, must match parent folder name) and `description` (≤1024 chars, must state what + when); progressive disclosure stages (discovery → activation → execution) confirm the "single lean SKILL.md, no references/" choice is spec-compliant, not a shortcut.
- **GitHub Pages "Actions" build type** — confirmed via `gh api repos/LayeredCraft/aws-secrets-manager-provider/pages` returning 404 (Pages never configured for this repo). Enabling via `gh api -X POST .../pages -f "build_type=workflow"` is the documented way to set the Actions-based build type without ever touching a `gh-pages` branch.

---

## 3. Decisions (interview outcomes, 2026-09-05)

All decisions below were confirmed via `AskUserQuestion` with the user; each states the rejected alternative and why.

1. **Docs tooling: MkDocs Material** (not Docusaurus, not plain Jekyll). Rationale: proven in the reference repo, Python/`uv` toolchain is build-time-only and doesn't touch the .NET runtime story, strong nav/search/dark-mode defaults for minimal config. Docusaurus rejected as unnecessary Node toolchain/config surface for a single small package; plain Jekyll rejected for weaker nav/search/admonition support.
2. **API reference: hand-curated, not generated.** Rationale: public surface is 6 types / ~20 members total — small enough that a single well-maintained Markdown reference page is more valuable (can narrate batch chunking, JSON flattening, case-insensitivity — none of which XML doc comments alone convey) than standing up DefaultDocumentation + a CI drift-check for so little surface. This is a deliberate divergence from the reference repo's generated-docs convention, justified by the differing scale (documented here per the "not an ADR, but preserve rationale" instruction).
3. **Skill structure: single `SKILL.md`, no `references/` split.** Rationale: the reference repo's 15-file `references/` split exists because it has many optional, conditionally-relevant integration packages; this package has one options class and one provider — everything fits legibly in one lean file, consistent with the Agent Skills spec's progressive-disclosure model (skip a disclosure layer that has no content to disclose).
4. **Skill distribution: `npx skills add LayeredCraft/aws-secrets-manager-provider`.** Rationale: matches the reference repo's actual (not aspirational) convention, requires no packaging/release automation, and is vendor-neutral (works with any Agent-Skills-compliant host, not just Claude Code).
5. **Eval execution: schema-validate `evals/evals.json` in CI; grade manually/locally.** Rationale: schema validation is cheap/deterministic and prevents authoring mistakes from silently breaking the eval suite; LLM-based grading is nondeterministic and costs tokens per run — the reference repo runs zero automated grading in CI, and the task instructions explicitly warn against adding this by default.
6. **Docs IA: slim, package-scoped (~9 pages), not a Diátaxis-style multi-section IA.** Rationale: the reference repo's ~10-area split serves a multi-package framework with a much larger surface; mirroring that structure here would produce mostly-empty sections. See §6 for the exact page list.
7. **README: trimmed to a concise landing page.** Rationale: single source of truth for depth (the docs site) with README linking out, matching the reference repo's own README/docs split; avoids permanent duplication risk between README and docs.
8. **Platform/edge-case scope: netstandard2.0/.NET Framework 4.6.2+ consumers and LocalStack local-dev are explicitly covered** (not footnoted). Rationale: both are real, currently-shipped, currently-tested-adjacent scenarios (samples/README already reference .NET Framework 4.6.2+ support; `ConfigureSecretsManagerConfig`/`ServiceUrl` already supports LocalStack) — omitting them would under-document real supported behavior.

---

## 4. Target repository structure

```
aws-secrets-manager-provider/
├── mkdocs.yml
├── pyproject.toml
├── uv.lock                              # generated by `uv lock` during implementation
├── README.md                            # trimmed, links to docs site + skill
├── docs/
│   ├── plans/
│   │   └── 0001-documentation-site-and-agent-skill.md   (this file)
│   ├── assets/
│   │   └── icon.png                     # copy of repo-root icon.png for Material logo/favicon
│   ├── index.md
│   ├── getting-started.md
│   ├── configuration.md
│   ├── advanced.md
│   ├── authentication-and-security.md
│   ├── platform-support.md
│   ├── troubleshooting.md
│   ├── api-reference.md
│   └── agent-skill.md
├── skills/
│   └── aws-secrets-manager-provider/
│       ├── SKILL.md
│       └── evals/
│           ├── evals.json
│           └── files/                   # empty at launch; present so the schema's `files` array has somewhere to point later
├── skills/aws-secrets-manager-provider-workspace/    # NOT committed except the few files noted in .gitignore rules below
│   └── (created on demand when evals are actually run; not created by this implementation)
├── scripts/
│   └── validate_skill.py                # stdlib-only: validates SKILL.md frontmatter + evals/evals.json schema
└── .github/
    └── workflows/
        ├── docs.yml                      # NEW: build + deploy Pages
        └── skill-validate.yml            # NEW: schema/structure validation for the skill
```

Nothing under `src/`, `tests/`, `samples/`, or the existing `.github/workflows/*.yaml` (templated ones) is modified except README.md.

---

## 5. Documentation information architecture

Nav is hand-maintained in `mkdocs.yml` (no auto-nav plugin), mirroring the reference repo's convention. Every page must contain real, package-specific content — no page exists just to fill out nav.

| Page | Purpose | Must cover |
|---|---|---|
| `index.md` | Home/overview | What the package is/isn't, one-paragraph elevator pitch, link map to every other page, install one-liner, "why a fork of Kralizek's extension" provenance note (from NOTICE), badges (mirrored from README). |
| `getting-started.md` | Installation + first working config | `dotnet add package AWSSecretsManager.Provider`; minimal `Host.CreateDefaultBuilder` example; minimal plain-console example; ASP.NET Core `WebApplication.CreateBuilder` example (from SampleWeb, including the "don't expose secrets via an endpoint" caution comment); a short "what you get" note on the 3 `AddSecretsManager` overloads and when to use the logger vs logger-factory vs no-logger variant (including the explicit fact there is **no automatic DI logger resolution** — must use an explicit overload). |
| `configuration.md` | Full options reference + secret naming/key-mapping | A table of all 10 `SecretsManagerConfigurationProviderOptions` properties (type, default, purpose, example) sourced from §"API Surface" in the repo discovery report — `AcceptedSecretArns`, `SecretFilter`, `ListSecretsFilters`, `KeyGenerator`, `ConfigureSecretValueRequest`, `ConfigureBatchSecretValueRequest`, `ConfigureSecretsManagerConfig`, `CreateClient`, `PollingInterval`, `UseBatchFetch`, `IgnoreMissingValues`. A dedicated subsection on secret-value-to-config-key mapping: JSON object/array flattening using `:`-delimited `ConfigurationPath` keys and numeric array indices, case-insensitive key comparer, plain-string secrets become a single key via `KeyGenerator`, binary secrets (`SecretBinary`-only, no `SecretString`) are silently skipped, malformed-JSON-looking strings that don't actually parse fall back to plain-string storage, JSON `null` produces a config entry with a null value (not an error). |
| `advanced.md` | Batch fetch, polling/reload, custom client | `UseBatchFetch` semantics: `BatchGetSecretValue` chunks of ≤20 secrets, partial-ARN/short-name/full-ARN matching against AWS's randomized-suffix full ARNs, IAM permission required (`secretsmanager:BatchGetSecretValue`); polling (`PollingInterval`, background loop, poll failures logged as warnings and do not kill the loop, reload only fires `IChangeToken`/`OnReload` when the resulting key set actually changed) and `ForceReloadAsync` for manual reload (with the `IConfigurationRoot.GetReloadToken().RegisterChangeCallback` pattern from tests/Sample7); `CreateClient` full override (Sample6 pattern) and `ConfigureSecretsManagerConfig` partial override (LocalStack `ServiceUrl` pattern — cross-link to platform-support.md); `Dispose()` semantics (cancels polling `CancellationTokenSource`, safe to call even if polling was never enabled). |
| `authentication-and-security.md` | Credentials, region, IAM, security guidance | AWS SDK default credential-resolution chain (env vars → shared credentials file → IAM role) used when no explicit `AWSCredentials` is passed; `CredentialProfileStoreChain`/named-profile pattern (Sample3); region resolution (`RegionEndpoint` parameter vs `Region` property on the source, no implicit env-based region resolution in this library beyond what the AWS SDK client itself does); minimum IAM policy example covering `secretsmanager:GetSecretValue` + `secretsmanager:ListSecrets` (and `secretsmanager:BatchGetSecretValue` if `UseBatchFetch=true`); security guidance — never expose the configuration provider's contents via a public endpoint (the exact caution already present in SampleWeb), least-privilege ARN scoping via `AcceptedSecretArns`/`ListSecretsFilters`/IAM resource conditions instead of blanket `ListSecrets` access. |
| `platform-support.md` | Target framework / compatibility matrix | Table: netstandard2.0 (→ .NET Framework 4.6.2+, .NET Core 2.0+), net8.0, net9.0, net10.0, net11.0; note on `Microsoft.Extensions.Configuration` version pinning per-TFM (why: parallel preview/stable lines) — informational only, not something a consumer configures; a short LocalStack section: point `ConfigureSecretsManagerConfig` at a local `ServiceUrl`, use `AnonymousAWSCredentials` or dummy creds, note this is a supported pattern via existing extensibility hooks, not a first-class LocalStack integration. |
| `troubleshooting.md` | Diagnostics + FAQ (merged into one page) | Common problems and root causes: `MissingSecretValueException` (what it means, `SecretName`/`SecretArn` properties, fix = set `IgnoreMissingValues` or verify IAM/ARN); an `AggregateException` from batch mode (means multiple distinct AWS error types occurred; `IgnoreMissingValues` only suppresses when *every* error is a missing-secret error); "my secret value is missing/empty" → check if it's a binary secret (silently skipped); "my JSON secret isn't being flattened" → check it actually starts with `{`/`[` and is valid JSON; "polling isn't picking up changes" → verify `PollingInterval` is set, and that the key set actually changed (identical re-fetches don't trigger reload); credential/region resolution failures and how to diagnose them; a short FAQ (why no auto logger-from-DI resolution, why netstandard2.0 target, difference between `AcceptedSecretArns` and `SecretFilter` and `ListSecretsFilters`). |
| `api-reference.md` | Hand-curated API reference | One section per public type: `SecretsManagerExtensions` (all 3 overload signatures + params), `SecretsManagerConfigurationProviderOptions` (full property table, reused from configuration.md but as authoritative signatures/defaults), `SecretsManagerConfigurationProvider` (public members: `Options`, `Client`, `Load()`, `ForceReloadAsync`, `Dispose()`), `SecretsManagerConfigurationSource` / `SecretsManagerConfigurationSourceWithLogger` (ctors, `Options`/`Credentials`/`Region`, `Build`), `SecretValueContext` (`Name`, `VersionsToStages`), `MissingSecretValueException` (`SecretArn`, `SecretName`). Sourced directly from XML doc comments + signatures in `src/AWSSecretsManager.Provider/`, not invented. |
| `agent-skill.md` | Skill install/discovery page | What the skill is, why a consumer would want it, exact install command (`npx skills add LayeredCraft/aws-secrets-manager-provider`), what hosts/agents it works with (any Agent-Skills-compliant host — Claude Code, etc., vendor-neutral), where its source lives (`skills/aws-secrets-manager-provider/` in this repo), how it's evaluated (`evals/evals.json`, link to agentskills.io evaluation methodology), link back to the rest of the docs site for depth the skill intentionally keeps out of `SKILL.md`. |

**Example strategy**: every code example must be traceable to an actual sample project or test in this repo (cite the source sample/test in an HTML comment during authoring, not published) — no invented API usage. Prefer linking to the relevant `samples/SampleN` project for full runnable context rather than duplicating entire `Program.cs` files into the docs.

---

## 6. Agent Skill design

### Purpose & expected activation

Helps an AI coding agent correctly assist a consumer integrating `AWSSecretsManager.Provider` into a .NET application: installing the package, wiring `AddSecretsManager`, choosing options, understanding secret-to-config-key mapping, diagnosing failures — while explicitly distinguishing this package's behavior from generic AWS Secrets Manager/SDK knowledge (e.g., the agent's own general AWS knowledge might assume DI-based logger auto-resolution or a different key-flattening scheme; the skill corrects both).

### `SKILL.md` responsibilities (single file, no `references/`)

Frontmatter: `name: aws-secrets-manager-provider` (matches folder name, kebab-case, ≤64 chars), `description` stating what it does and when to trigger (mentions `AddSecretsManager`, `SecretsManagerConfigurationProviderOptions`, `AWSSecretsManager.Provider`, "AWS Secrets Manager" + "Microsoft.Extensions.Configuration" together, so it doesn't over-trigger on generic AWS Secrets Manager questions unrelated to .NET config).

Body sections:
1. **What this package is / is not** — one paragraph distinguishing it from raw `AWSSDK.SecretsManager` usage and from other community Secrets Manager config providers.
2. **Installation** — `dotnet add package AWSSecretsManager.Provider`, minimum supported TFMs.
3. **Core usage patterns** — the 3 `AddSecretsManager` overloads with guidance on which to pick (no logger / explicit `ILogger<T>` / `ILoggerFactory`), explicitly noting there is no automatic DI-based logger resolution.
4. **Options quick-reference** — condensed table of all 10 options (name, one-line purpose, when to set it) — full depth intentionally lives in the docs site, not duplicated here.
5. **Secret naming / key mapping rules** — the JSON-flattening scheme, case-insensitivity, binary secrets silently skipped, malformed-JSON fallback — this is the highest-value, most-likely-to-be-gotten-wrong section, kept in full here rather than deferred.
6. **Credentials & region** — default AWS credential chain vs explicit `AWSCredentials`/named profiles; region via constructor param or `Region` property; never hardcode credentials in examples.
7. **Batch fetch & polling** — condensed guidance (20-item chunks, `IgnoreMissingValues` semantics, `PollingInterval`/`ForceReloadAsync`), pointing to `advanced.md` on the docs site for full depth.
8. **Common mistakes to avoid** — explicit list: don't expose provider contents via an endpoint; don't assume `IgnoreMissingValues` suppresses all batch errors (only missing-secret errors); don't expect binary secrets to work; don't recommend deprecated/non-existent APIs (e.g., no JSON.NET dependency — this package is System.Text.Json only).
9. **Where to point the user for more depth** — link to the docs site pages by name.

### `evals/evals.json`

Schema follows the agentskills.io spec exactly: `{"skill_name": "aws-secrets-manager-provider", "evals": [...]}`, each eval `{id, prompt, expected_output, files, assertions}` (field name `assertions`, not Compono's internal `expectations`). 6 cases — sized per task instruction ("start appropriately small, cover normal/advanced/edge/value-add", not 46-case scale):

1. **id 1 — normal usage.** Prompt: consumer asks to wire up `AddSecretsManager` in an ASP.NET Core app reading a named secret. Assertions: uses a real overload signature; does not fabricate a DI-auto-resolved-logger claim; uses `Host.CreateDefaultBuilder`/`WebApplication.CreateBuilder` correctly.
2. **id 2 — advanced (batch + polling + custom key mapping combined).** Prompt: consumer wants to batch-fetch several secrets, poll every 5 minutes, and upper-case all keys. Assertions: sets `UseBatchFetch=true`, sets `PollingInterval`, sets `KeyGenerator`, mentions the 20-secret chunk limit and required `secretsmanager:BatchGetSecretValue` IAM permission.
3. **id 3 — edge case (JSON flattening + case-insensitivity).** Prompt: consumer has a JSON secret with nested objects and asks why a lookup with different casing didn't work / how nested keys map. Assertions: correctly explains `:`-delimited flattening and array indices; correctly explains keys are case-insensitive; does not claim JSON.NET or a different delimiter.
4. **id 4 — commonly misunderstood (`IgnoreMissingValues` + batch errors).** Prompt: consumer sets `IgnoreMissingValues=true` with `UseBatchFetch=true` and is confused why a non-missing-secret AWS error still threw. Assertions: correctly explains `IgnoreMissingValues` only suppresses errors that are *all* missing-secret errors, not all error types; references `AggregateException`.
5. **id 5 — value-add over generic knowledge (LocalStack / no DI-auto-logger).** Prompt: consumer asks to point the provider at LocalStack for local dev and also wants log output without wiring a logger explicitly. Assertions: correctly uses `ConfigureSecretsManagerConfig` + `ServiceUrl` for LocalStack; correctly states there is **no** automatic DI-based logger resolution and an explicit `ILogger`/`ILoggerFactory` overload is required — a fact a generic-knowledge answer would likely get wrong by assuming ambient DI logger injection.
6. **id 6 — edge case (binary secret / malformed JSON diagnostics).** Prompt: consumer reports a secret "isn't showing up" in configuration at all. Assertions: asks about/explains binary secrets being silently skipped as one candidate cause; explains malformed-JSON-looking strings fall back to plain-string storage rather than throwing, as another candidate cause; does not claim the provider throws on either case.

`files`: empty arrays for all 6 (no input fixture files needed — these are pure API-usage/reasoning prompts, not data-processing tasks). `evals/files/` directory is created empty with a `.gitkeep` so the schema's `files` field has a real location to point at once/if a future case needs one.

### Scripts/assets

None — the skill needs no bundled scripts or template assets given its narrow, advisory (not data-processing) nature.

### Evaluation workspace & strategy

Location: `skills/aws-secrets-manager-provider-workspace/` (sibling to `skills/aws-secrets-manager-provider/`, matching the reference repo's `<skill>-workspace/` convention).

```
skills/aws-secrets-manager-provider-workspace/
├── skill-snapshot/            # populated only when improving an existing skill version (not at launch)
└── iteration-1/
    ├── eval-<case-slug>/
    │   ├── with_skill/{outputs/, timing.json, grading.json}
    │   └── without_skill/{outputs/, timing.json, grading.json}
    ├── benchmark.json
    └── feedback.json
```

- **Baseline strategy**: `without_skill` runs for a first eval pass (no prior skill version exists to snapshot). When this skill is later revised, snapshot the pre-revision `skills/aws-secrets-manager-provider/` into `skill-snapshot/` and run the baseline against that snapshot into `old_skill/` instead of `without_skill/`, per agentskills.io guidance.
- **Clean-context execution**: each with/without run must be a fresh subagent/session (e.g. the `Agent` tool with a fresh `general-purpose` subagent, or a new Claude Code session) given only the skill path (or none/snapshot), the eval's prompt, and a distinct output directory — never a continuation of skill-development conversation.
- **Grading**: manual/agent-driven against the case's `assertions`, following agentskills.io's grading principles (concrete evidence required for PASS; scripts preferred over LLM judgment for mechanically-checkable assertions — none of this suite's 6 cases have mechanically-checkable assertions, all require reasoning-content grading).
- **Timing**: `timing.json = {total_tokens, duration_ms}` captured from the harness's task-completion notification immediately after each run (per agentskills.io — "these aren't persisted anywhere else").
- **Benchmark aggregation**: `benchmark.json` per iteration, `{run_summary: {with_skill, without_skill, delta}}` — `pass_rate`/`time_seconds`/`tokens` means (stddev not meaningful until multiple runs/case exist).
- **Human review**: `feedback.json` keyed by eval-case slug, free-text, empty string = no complaint.
- **CI vs local**: CI only schema-validates `evals/evals.json` and `SKILL.md` frontmatter (§8) — it never runs an actual with/without grading pass. Running the eval loop itself (spawning agents, grading, aggregating) is a manual/local activity performed on demand (e.g., when materially revising the skill), not part of routine CI.
- **`.gitignore` rules — revised, diverges from the reference repo's convention**:
  ```gitignore
  # Agent Skill eval workspace: entirely generated/regenerated by running the evals
  # locally (see skills/aws-secrets-manager-provider/evals/evals.json). None of it is
  # committed to the repo.
  skills/aws-secrets-manager-provider-workspace/
  ```
  **Decision update (2026-09-06):** the original plan copied the reference repo's convention verbatim — committing `grading.json`/`timing.json`/`benchmark.json`/`feedback.json` as evaluation evidence while ignoring only `outputs/` and `skill-snapshot/`. An iteration-1 eval run was performed and those 26 evidence files were committed and pushed to PR #121 accordingly. The user then determined the entire eval workspace should never be checked into this repo (unlike the reference repo, which treats committed eval evidence as a deliberate historical record across many packages) — for this single, small package, the eval workspace is purely a local/on-demand artifact. The 26 previously-committed files were removed from git tracking (`git rm --cached`, kept on disk) and the `.gitignore` rule now excludes the whole `skills/aws-secrets-manager-provider-workspace/` directory rather than just `outputs/`/`skill-snapshot/` within it. Running the evals still produces the same on-disk structure (§ above) for local inspection — none of it is committed.
  This must NOT match `skills/aws-secrets-manager-provider/evals/evals.json` or `skills/aws-secrets-manager-provider/evals/files/` (authored, always committed, and outside the `-workspace/` directory entirely) — verify with `git check-ignore` during validation (§9).

### Downloadable packaging/distribution

`npx skills add LayeredCraft/aws-secrets-manager-provider` — resolves to the `skills/` directory at the repo root by the tool's own convention (matching the reference repo, which has exactly one skill directly under `skills/`). No zip packaging, no release automation, no version pin — `npx skills update aws-secrets-manager-provider` pulls whatever is on `main`. Documented on `docs/agent-skill.md` and in a new README section (§7).

### How documentation links to it

- `docs/agent-skill.md` (full page, §5).
- `docs/index.md` links to it in the page link-map.
- README gets a short `## AI Coding Agent Skill` section (mirroring the reference repo's placement, right after Quick Start) with the install command and links to `docs/agent-skill.md` and the `skills/aws-secrets-manager-provider/` path.

---

## 7. GitHub Pages design

### Technology

MkDocs Material, Python toolchain managed by `uv` (per Decision §3.1).

`pyproject.toml`:
```toml
[project]
name = "aws-secrets-manager-provider-docs"
version = "0.0.0"
description = "Documentation build dependencies for AWSSecretsManager.Provider."
requires-python = ">=3.12"
dependencies = [
  "mkdocs-material>=9.0.0",
  "mkdocs-minify-plugin>=0.7.0",
]
```

`mkdocs.yml` — adapted from the reference repo's, trimmed to a single-package nav (no `edit_uri` behavior change, same theme features/palette/markdown_extensions, `site_url: https://layeredcraft.github.io/aws-secrets-manager-provider/`, `repo_name: LayeredCraft/aws-secrets-manager-provider`, `repo_url: https://github.com/LayeredCraft/aws-secrets-manager-provider`, `logo`/`favicon: docs/assets/icon.png`). `nav:` lists exactly the 9 pages from §5 in reading order (index, getting-started, configuration, advanced, authentication-and-security, platform-support, troubleshooting, api-reference, agent-skill). `validation.links.anchors: warn` retained so broken in-page anchors fail `mkdocs build --strict`.

### Build process

No code needs to be compiled to build the docs (hand-curated API reference, Decision §3.2) — the build is pure Markdown→HTML: `uv sync --locked` then `uv run mkdocs build --clean --strict`.

### Generated API documentation process

None (Decision §3.2) — `docs/api-reference.md` is authored Markdown, versioned like any other doc page, kept in sync manually against `src/AWSSecretsManager.Provider/` when the public API changes (call this out in `CONTRIBUTING`-equivalent guidance if one is ever added; for now it's a note in `docs/api-reference.md`'s own front matter/comment).

### Local preview

Documented in a short "Building docs locally" subsection added to README (or a `docs/index.md` callout): `uv sync` then `uv run mkdocs serve` (live-reload) or `uv run mkdocs build --strict` (one-shot, matches CI).

### GitHub Actions workflow (`docs.yml`)

Adapted from the reference repo's `docs.yml`, simplified (no dotnet build step, no API-reference generation/drift-check step, since there's nothing generated):

- Triggers: `push`/`pull_request` on `main`, path-filtered to `docs/**`, `mkdocs.yml`, `pyproject.toml`, `uv.lock`, `.github/workflows/docs.yml`.
- Permissions: `contents: read, pages: write, id-token: write`. `concurrency: {group: "pages", cancel-in-progress: false}`.
- `build` job (runs on every push/PR matching the filter): checkout → install `uv` (`astral-sh/setup-uv@v10.0.1`) → `uv sync --locked` → `actions/configure-pages@v6` → `uv run mkdocs build --clean --strict` → `actions/upload-pages-artifact@v5` with `path: site`.
- `deploy` job: `needs: build`, `if: github.ref == 'refs/heads/main'`, `environment: {name: github-pages, url: ${{ steps.deployment.outputs.page_url }}}`, single step `actions/deploy-pages@v5` with `id: deployment`.

### Pages deployment

GitHub "Actions" build type (not legacy branch-based) — set via `gh api` (§ below), consumed by `actions/deploy-pages@v5` in the workflow above.

### `gh api` configuration required

Performed as an implementation task (pre-authorized by the user's original instructions to configure Pages via `gh api` rather than the web UI):

```bash
# 1. Inspect current state (already done during planning — confirmed 404, Pages not configured)
gh api repos/LayeredCraft/aws-secrets-manager-provider/pages

# 2. Enable Pages with the Actions build type
gh api -X POST repos/LayeredCraft/aws-secrets-manager-provider/pages \
  -f "build_type=workflow"

# 3. Verify
gh api repos/LayeredCraft/aws-secrets-manager-provider/pages
```

No other repository settings are touched (branch protection, visibility, etc. are out of scope).

### Verification

Full "site is live" verification requires `docs.yml` to actually run on `main` (the `deploy` job is gated on `main`), which requires the workflow file to be merged to `main` — this cannot happen without a commit/push/PR, which this plan's non-goals explicitly withhold unless the user asks. This is called out explicitly as a **known gap** in §10 (Completion criteria) rather than silently glossed over: enabling Pages via `gh api` can and will be done as part of this implementation; the first real deployment and live-site verification is blocked on the user separately authorizing a push/PR/merge to `main`.

---

## 8. CI/CD

Two new workflows; no existing templated workflow is modified.

### `.github/workflows/docs.yml`

As specified in §7. Validates: docs build succeeds (`mkdocs build --strict` fails the job on any broken internal link/anchor per `validation.links.anchors: warn` + `--strict`), and deploys on `main`.

### `.github/workflows/skill-validate.yml`

- Triggers: `push`/`pull_request` touching `skills/aws-secrets-manager-provider/**` (explicitly excluding the sibling `-workspace/` directory via path filtering) or the validation script itself.
- Single job: checkout → setup Python (stdlib only, no extra deps needed) → `python3 scripts/validate_skill.py`.
- `scripts/validate_skill.py` (stdlib-only, no PyYAML dependency — hand-parses the simple `key: value` frontmatter block since the schema is flat) checks:
  1. `skills/aws-secrets-manager-provider/SKILL.md` exists, has a well-formed `---`-delimited frontmatter block.
  2. Frontmatter has `name` and `description`; `name == "aws-secrets-manager-provider"` and matches the parent folder name; `name` matches `^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$`; `description` non-empty and ≤1024 chars.
  3. `skills/aws-secrets-manager-provider/evals/evals.json` exists, is valid JSON, has top-level `skill_name` (matches the SKILL.md `name`) and `evals` (non-empty array).
  4. Every eval object has non-empty `id` (unique across the array), `prompt`, `expected_output`, `assertions` (non-empty array of strings), and `files` (array, may be empty) — matching the agentskills.io schema field names exactly.
  5. Exits non-zero with a clear message on any failure (fails the CI job).

Explicitly not added to CI (per Decision §3.5 / task guidance against expensive nondeterministic checks by default): docs prose linting/spellcheck, code-example compilation checks (mitigated instead by the authoring rule in §5 that every example must trace to a real sample/test), automated LLM eval grading, packaging/download artifact checks (no packaging exists to check).

---

## 9. Implementation tasks

Ordered; each depends only on prior tasks unless noted. This section is what a future session should execute directly.

### Task 1 — Scaffold docs tooling

- Create `pyproject.toml`, run `uv lock` to generate `uv.lock`.
- Create `mkdocs.yml` per §7.
- Create `docs/assets/icon.png` (copy of repo-root `icon.png`).
- Validation: `uv sync --locked` succeeds; `uv run mkdocs build --strict` succeeds even with placeholder/stub page content (create empty stub `.md` files for all 9 pages from §5 first, filled in Task 2).
- Depends on: nothing.

### Task 2 — Author the 9 documentation pages

- Write full content for each page listed in §5, sourcing every fact from `src/AWSSecretsManager.Provider/` (read the actual source files directly — do not rely on this plan's summaries as the final word on signatures/defaults; re-verify against current source at implementation time) and from `samples/`/`tests/` for example fidelity.
- Validation: `uv run mkdocs build --clean --strict` passes (no broken links/anchors); spot-check that every option in `configuration.md`'s table matches the actual current property list/defaults in `SecretsManagerConfigurationProviderOptions.cs`; every code example matches a real sample or test.
- Depends on: Task 1.

### Task 3 — `docs.yml` workflow

- Create `.github/workflows/docs.yml` per §7/§8.
- Validation: workflow YAML is syntactically valid (`actionlint` if available, else careful manual review); cannot fully validate the `deploy` job without pushing to `main` (see §7 Verification / §10 known gap) — validate the `build` job logic by running the same commands locally (`uv sync --locked && uv run mkdocs build --clean --strict`).
- Depends on: Task 2.

### Task 4 — Enable GitHub Pages via `gh api`

- Run the 3 commands in §7 (`gh api ... pages` GET to reconfirm current state, POST with `build_type=workflow`, GET to verify).
- Validation: the verification GET returns `build_type: "workflow"` (or equivalent field name — confirm actual response shape at execution time) instead of 404.
- Depends on: nothing strictly, but do this after Task 3 exists so the eventual first `main` push has a working workflow to deploy with.

### Task 5 — Trim README, add Agent Skill + docs-link sections

- Remove/condense: the detailed options bullet list, the full logging-examples code blocks, the aspirational log-levels table (correct or remove the "Trace" inaccuracy noted in the repo discovery report) — replace with a short paragraph + link to `docs/configuration.md`/`docs/getting-started.md`.
- Add: `## Documentation` section linking to the Pages site; `## AI Coding Agent Skill` section (install command + links) per §6.
- Keep: badges, provenance/acknowledgments statement, samples table, installation one-liner, building-locally instructions, license, contributors table.
- Validation: README renders correctly on GitHub (manual visual check of Markdown); no broken relative links; no information is lost that isn't now available at the linked docs page (diff old vs new README against §5's page coverage table).
- Depends on: Task 2 (so link targets exist).

### Task 6 — Scaffold the Agent Skill

- Create `skills/aws-secrets-manager-provider/SKILL.md` per §6.
- Create `skills/aws-secrets-manager-provider/evals/evals.json` with the 6 cases per §6 (field names: `skill_name`, `evals[].{id,prompt,expected_output,files,assertions}`).
- Create empty `skills/aws-secrets-manager-provider/evals/files/.gitkeep`.
- Validation: run `scripts/validate_skill.py` (Task 7 must exist first, or run an inline equivalent check) against this content; manually re-read `SKILL.md` against the Agent Skills spec's frontmatter rules (name matches folder, ≤64 chars, kebab-case; description states what+when, ≤1024 chars).
- Depends on: Task 2 (so the "where to point the user for more depth" links resolve).

### Task 7 — `scripts/validate_skill.py` + `skill-validate.yml`

- Write the stdlib-only validation script per §8.
- Create `.github/workflows/skill-validate.yml` per §8.
- Validation: run `python3 scripts/validate_skill.py` locally against Task 6's output — must pass; then deliberately break one field (e.g. rename `assertions` to `expectations`, or duplicate an `id`) and confirm the script fails with a clear message, then revert the deliberate break.
- Depends on: Task 6.

### Task 8 — `.gitignore` update for the eval workspace

- Add the two-line rule block from §6 to the root `.gitignore`.
- Validation: `git check-ignore -v skills/aws-secrets-manager-provider-workspace/iteration-1/eval-x/with_skill/outputs/foo.txt` matches; `git check-ignore -v skills/aws-secrets-manager-provider/evals/evals.json` and `.../evals/files/anything` do **NOT** match (must exit non-zero / print nothing, confirming they are NOT ignored).
- Depends on: nothing (can run any time; grouped last because it's independent and quick to verify against Task 6's real paths).

### Task 9 — Full validation pass

See §10 — run the complete validation matrix and record results (in this plan or in a follow-up note) before declaring the work complete.

---

## 10. Validation

Explicit final validation matrix — every row must be checked before declaring completion:

| Area | Check | Status |
|---|---|---|
| Repository build/tests | `dotnet build` passes, 0 errors (pre-existing warnings only, unrelated to this work; no `src`/`tests` code touched) | ✅ done |
| Docs local build | `uv sync --locked && uv run mkdocs build --clean --strict` exits 0 | ✅ done |
| GitHub Pages workflow | `docs.yml` YAML valid (parsed via PyYAML); `build` job steps verified locally by running the same commands | ✅ done |
| Deployed Pages site | **Blocked** on a `main`-branch push/merge the user must separately authorize (see §7 known gap) — cannot be marked done by this implementation alone | blocked (needs user go-ahead) |
| Links/navigation | `mkdocs build --strict` passed with zero broken-link/anchor warnings across all 9 pages | ✅ done |
| Code examples | Every example traced to a real sample (Sample1/3/4/5/6/7, SampleWeb) or documented behavior from source/tests | ✅ done |
| Skill download | `npx skills add ...` cannot be tested against a not-yet-pushed `main` branch — validated structurally instead (skill exists at `skills/aws-secrets-manager-provider/`, matches the convention) | pending (structural only, same push-gating as above) |
| Skill structure | `scripts/validate_skill.py` passes; deliberate-break test (duplicate eval id) confirmed to fail correctly, then reverted | ✅ done |
| `evals/evals.json` | Schema-valid per agentskills.io field names (`skill_name`, `evals[].{id,prompt,expected_output,files,assertions}`); 6 cases covering normal/advanced/edge(×2)/misunderstood/value-add | ✅ done |
| Evaluation workspace behavior | `skills/aws-secrets-manager-provider-workspace/` structure manually created and torn down to verify `.gitignore` coverage; no run data committed | ✅ done |
| `.gitignore` | Revised (2026-09-06) to ignore the entire `skills/aws-secrets-manager-provider-workspace/` directory rather than just `outputs/`/`skill-snapshot/` within it, after 26 evidence files were mistakenly committed in the iteration-1 eval run; removed from git tracking via `git rm --cached` (kept on disk). Verified via `git check-ignore -v`: the whole workspace dir IS ignored; `evals/evals.json` and `evals/files/.gitkeep` are NOT ignored; `.venv/` and `site/` (docs tooling) also ignored | ✅ done |
| GitHub Pages repo configuration | `gh api repos/LayeredCraft/aws-secrets-manager-provider/pages` confirmed `build_type: "workflow"`, `html_url: "https://layeredcraft.github.io/aws-secrets-manager-provider/"` | ✅ done |
| Package documentation links | README, `docs/index.md`, `docs/agent-skill.md`, and `SKILL.md` all consistently reference the docs site URL and `npx skills add LayeredCraft/aws-secrets-manager-provider` | ✅ done |

---

## 11. Completion criteria

The work is complete only when:

- [ ] Full GitHub Pages documentation exists (9 pages per §5, all real content, no stub pages left).
- [ ] The site builds successfully locally (`mkdocs build --strict`).
- [ ] Pages deployment workflow (`docs.yml`) exists and is correct (validated per Task 3, acknowledging the `deploy` job itself can't run until pushed to `main`).
- [ ] Pages is enabled/configured through `gh api` (Task 4 executed, `build_type=workflow` verified).
- [ ] The deployed site has been verified — **explicitly flagged as blocked** on user-authorized push/merge to `main`; not achievable within this plan's non-goals (no unauthorized push/PR/merge). This must be surfaced to the user as an outstanding follow-up, not silently marked done.
- [ ] The Agent Skill exists at `skills/aws-secrets-manager-provider/`.
- [ ] The skill is downloadable (structurally correct for `npx skills add`; live end-to-end fetch is gated on the same `main`-push dependency above).
- [ ] The skill is documented (`docs/agent-skill.md` + README section).
- [ ] `evals/evals.json` exists and follows the agentskills.io schema (6 cases, correct field names).
- [ ] The documented evaluation workspace structure is supported (directories/conventions in place; no populated run data required at launch).
- [ ] Generated evaluation workspaces are ignored by Git (Task 8 verified both directions).
- [ ] Repository documentation and README are synchronized (README links match actual docs page names/paths; no stale content duplicated).
- [ ] All relevant existing tests/builds/checks still pass (unaffected by this work, but re-run to confirm no accidental interference, e.g. via `.gitignore` changes).
- [ ] Implementation matches this plan, or this plan has been updated to reflect any material deviation discovered during implementation (see governance below).

---

## Plan-as-source-of-truth governance

- Before implementing each task in §9, re-read the relevant section of this plan (not this conversation's transcript).
- If implementation reveals this plan is wrong or incomplete: investigate → determine the correct design → if it's a meaningful product/design tradeoff, use `AskUserQuestion` → update this plan → continue from the updated plan.
- Do not silently diverge. A Todo list may track in-session progress but is not authoritative — this file is.
- Update the `Status` field at the top of this document as work progresses (e.g. `IN PROGRESS`, `COMPLETE (Pages deploy pending user push authorization)`).
