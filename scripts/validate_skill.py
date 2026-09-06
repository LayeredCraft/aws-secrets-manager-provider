#!/usr/bin/env python3
"""Validate an Agent Skill's SKILL.md frontmatter and evals/evals.json schema.

Stdlib-only (no PyYAML dependency) — the frontmatter block here is a flat
`key: value` mapping, so it's hand-parsed rather than pulling in a YAML
library for a handful of scalar fields.

Usage: python3 scripts/validate_skill.py [skill_dir]
Defaults to skills/aws-secrets-manager-provider.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

NAME_PATTERN = re.compile(r"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$")
MAX_DESCRIPTION_LENGTH = 1024
REQUIRED_EVAL_FIELDS = ("id", "prompt", "expected_output", "files", "assertions")


class ValidationError(Exception):
    pass


def fail(message: str) -> None:
    raise ValidationError(message)


def parse_frontmatter(skill_md_path: Path) -> dict[str, str]:
    text = skill_md_path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        fail(f"{skill_md_path}: must start with a '---' frontmatter delimiter")

    end = text.find("\n---", 4)
    if end == -1:
        fail(f"{skill_md_path}: frontmatter block is not closed with '---'")

    block = text[4:end]
    frontmatter: dict[str, str] = {}
    for line in block.splitlines():
        if not line.strip() or line.startswith("  ") or line.startswith("\t"):
            continue  # skip blank lines and nested (indented) keys like metadata.*
        if ":" not in line:
            continue
        key, _, value = line.partition(":")
        frontmatter[key.strip()] = value.strip()
    return frontmatter


def validate_skill_md(skill_dir: Path) -> str:
    skill_md_path = skill_dir / "SKILL.md"
    if not skill_md_path.is_file():
        fail(f"{skill_md_path}: does not exist")

    frontmatter = parse_frontmatter(skill_md_path)

    name = frontmatter.get("name")
    if not name:
        fail(f"{skill_md_path}: frontmatter missing required 'name' field")
    if len(name) > 64:
        fail(f"{skill_md_path}: name '{name}' exceeds 64 characters")
    if not NAME_PATTERN.match(name):
        fail(f"{skill_md_path}: name '{name}' must be lowercase letters/digits/hyphens, "
             "not starting or ending with a hyphen")
    if name != skill_dir.name:
        fail(f"{skill_md_path}: name '{name}' must match parent folder name '{skill_dir.name}'")

    description = frontmatter.get("description")
    if not description:
        fail(f"{skill_md_path}: frontmatter missing required 'description' field")
    if len(description) > MAX_DESCRIPTION_LENGTH:
        fail(f"{skill_md_path}: description exceeds {MAX_DESCRIPTION_LENGTH} characters")

    return name


def validate_evals(skill_dir: Path, expected_skill_name: str) -> None:
    evals_path = skill_dir / "evals" / "evals.json"
    if not evals_path.is_file():
        fail(f"{evals_path}: does not exist")

    try:
        data = json.loads(evals_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        fail(f"{evals_path}: invalid JSON ({e})")
        return

    if not isinstance(data, dict):
        fail(f"{evals_path}: top level must be a JSON object")

    skill_name = data.get("skill_name")
    if skill_name != expected_skill_name:
        fail(f"{evals_path}: skill_name '{skill_name}' must match SKILL.md name "
             f"'{expected_skill_name}'")

    evals = data.get("evals")
    if not isinstance(evals, list) or len(evals) == 0:
        fail(f"{evals_path}: 'evals' must be a non-empty array")

    seen_ids: set = set()
    for i, case in enumerate(evals):
        if not isinstance(case, dict):
            fail(f"{evals_path}: evals[{i}] must be an object")

        for field in REQUIRED_EVAL_FIELDS:
            if field not in case:
                fail(f"{evals_path}: evals[{i}] missing required field '{field}'")

        case_id = case.get("id")
        if case_id in seen_ids:
            fail(f"{evals_path}: duplicate eval id {case_id!r}")
        seen_ids.add(case_id)

        if not isinstance(case.get("prompt"), str) or not case["prompt"].strip():
            fail(f"{evals_path}: evals[{i}].prompt must be a non-empty string")
        if not isinstance(case.get("expected_output"), str) or not case["expected_output"].strip():
            fail(f"{evals_path}: evals[{i}].expected_output must be a non-empty string")
        if not isinstance(case.get("files"), list):
            fail(f"{evals_path}: evals[{i}].files must be an array")
        assertions = case.get("assertions")
        if not isinstance(assertions, list) or len(assertions) == 0:
            fail(f"{evals_path}: evals[{i}].assertions must be a non-empty array")
        for j, assertion in enumerate(assertions):
            if not isinstance(assertion, str) or not assertion.strip():
                fail(f"{evals_path}: evals[{i}].assertions[{j}] must be a non-empty string")


def main() -> int:
    skill_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("skills/aws-secrets-manager-provider")

    try:
        skill_name = validate_skill_md(skill_dir)
        validate_evals(skill_dir, skill_name)
    except ValidationError as e:
        print(f"FAIL: {e}", file=sys.stderr)
        return 1

    print(f"OK: '{skill_dir}' skill structure and evals schema are valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
