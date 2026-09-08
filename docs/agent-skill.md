# AI Coding Agent Skill

This repository ships a downloadable [Agent Skill](https://agentskills.io/) — a small, structured instruction set that teaches an AI coding agent how to correctly use `AWSSecretsManager.Provider`, instead of relying on the agent's generic (and sometimes stale or wrong) knowledge of AWS Secrets Manager and .NET configuration providers.

## Why you'd want it

Generic AI assistance about "AWS Secrets Manager + .NET configuration" tends to get several package-specific details wrong: it may assume automatic DI-based logger resolution (this package has none — see [Getting Started](getting-started.md)), guess at a different secret-to-key flattening scheme than the one this package actually uses, or misdescribe `IgnoreMissingValues` — in single-fetch mode it suppresses a missing secret's error outright, but in batch mode it only suppresses errors AWS reports *inside* the batch response, and only when every error in that batch is a missing-secret error; a request-level failure from the batch call itself is not suppressed by this option at all, regardless of mode. The skill encodes the actual, current, tested behavior of this package instead.

## What it's for

An agent with this skill installed can correctly help you:

- Install the package and wire up the right `AddSecretsManager` overload.
- Choose and combine options (`AcceptedSecretArns`, `SecretFilter`, `ListSecretsFilters`, `KeyGenerator`, batch fetch, polling, `IgnoreMissingValues`, custom client construction).
- Understand exactly how a secret value maps to one or more configuration keys.
- Set up credentials/region correctly without inventing unsupported patterns.
- Diagnose common failures (missing secrets, batch errors, JSON-flattening surprises, binary secrets).
- Avoid recommending deprecated or non-existent APIs for this package.

## Installation

The skill lives directly in this repository at `skills/aws-secrets-manager-provider/` and is distributed via the [`skills` CLI](https://www.npmjs.com/package/skills) — no separate download or build step:

```bash
npx skills add LayeredCraft/aws-secrets-manager-provider
```

To pick up updates later:

```bash
npx skills update aws-secrets-manager-provider
```

This works with any host that implements the open [Agent Skills specification](https://agentskills.io/specification) (Claude Code among others) — it's not tied to a single AI vendor.

## Source and evaluation

- Skill source: [`skills/aws-secrets-manager-provider/`](https://github.com/LayeredCraft/aws-secrets-manager-provider/tree/main/skills/aws-secrets-manager-provider) in this repository.
- The skill is evaluated against a small suite of realistic prompts in `skills/aws-secrets-manager-provider/evals/evals.json`, following the [Agent Skills evaluation methodology](https://agentskills.io/skill-creation/evaluating-skills) — covering normal usage, an advanced multi-option scenario, edge cases (JSON flattening, binary secrets), and scenarios chosen specifically because generic knowledge tends to get them wrong.
- `evals/evals.json` is schema-validated in CI; actual with-skill/without-skill grading is a manual/local process, run when the skill is materially revised.

## Sibling skill: aws-ssm-provider

A matching skill ships for the `AWSSSM.Provider` (SSM Parameter Store) sibling package at [`skills/aws-ssm-provider/`](https://github.com/LayeredCraft/aws-secrets-manager-provider/tree/main/skills/aws-ssm-provider), with its own evals in `skills/aws-ssm-provider/evals/evals.json`. Install it the same way:

```bash
npx skills add LayeredCraft/aws-secrets-manager-provider
```

See the [SSM Parameter Store docs](ssm-parameter-store.md) for the package it describes.

For everything the skill intentionally keeps brief, it points back to this documentation site — see [Configuration & Secret Mapping](configuration.md), [Advanced Usage](advanced.md), and [Troubleshooting & FAQ](troubleshooting.md) for full depth.
