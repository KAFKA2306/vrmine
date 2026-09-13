#!/usr/bin/env python3
"""Validate VRMine verification-worktree allocation evidence.

This validator is deliberately non-destructive. It only proves that an
allocation manifest matches the canonical safe-isolation policy and, when
requested, that the allocated paths exist.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

PASS = "PASS"
FAIL = "FAIL"
UNVERIFIED = "UNVERIFIED"
EXIT_CODES = {PASS: 0, FAIL: 1, UNVERIFIED: 2}
SHA_RE = re.compile(r"^[0-9a-fA-F]{40}$")
ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return value


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _resolved_path(value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{label} must be a non-empty path string")
    return Path(value).expanduser().resolve()


def _under(path: Path, parent: Path) -> bool:
    return path == parent or parent in path.parents


def evaluate_allocation(
    manifest: dict[str, Any],
    policy: dict[str, Any],
    *,
    repo_root: Path = ROOT,
    policy_path: Path | None = None,
    check_paths: bool = True,
) -> tuple[str, list[str]]:
    repo_root = repo_root.resolve()
    policy_path = (policy_path or repo_root / "config" / "verification-worktree-policy.json").resolve()
    failures: list[str] = []
    unverified: list[str] = []

    normal_run = policy.get("normal_run")
    destructive = policy.get("destructive_recovery")
    approval = policy.get("approval_avoidance")
    if not isinstance(normal_run, dict):
        failures.append("policy.normal_run is missing")
        normal_run = {}
    if not isinstance(destructive, dict):
        failures.append("policy.destructive_recovery is missing")
        destructive = {}
    if not isinstance(approval, dict):
        failures.append("policy.approval_avoidance is missing")
        approval = {}

    if normal_run.get("delete_existing_run") is not False:
        failures.append("policy must forbid deleting an existing run")
    if normal_run.get("reuse_failed_run") is not False:
        failures.append("policy must forbid reusing failed runs")
    if normal_run.get("reuse_partial_run") is not False:
        failures.append("policy must forbid reusing partial runs")
    if normal_run.get("collision_strategy") != "allocate-new-run":
        failures.append("collision_strategy must be allocate-new-run")
    if normal_run.get("retry_strategy") != "allocate-new-run":
        failures.append("retry_strategy must be allocate-new-run")
    if destructive.get("allowed") is not False:
        failures.append("destructive recovery must remain disabled")
    if approval.get("destructive_cleanup_must_not_block_normal_run") is not True:
        failures.append("destructive cleanup must not block normal runs")

    if manifest.get("state") != "ALLOCATED":
        unverified.append(f"allocation state is not ALLOCATED: {manifest.get('state')!r}")
    if manifest.get("recovery_policy") != "allocate-new-run":
        failures.append("allocation recovery_policy must be allocate-new-run")
    if manifest.get("source_mutation_allowed") is not False:
        failures.append("source_mutation_allowed must be false")

    for field in ("source_head", "revision"):
        value = manifest.get(field)
        if not isinstance(value, str) or not SHA_RE.fullmatch(value):
            unverified.append(f"{field} is not an exact 40-hex commit SHA")

    source_status = manifest.get("source_status_porcelain")
    source_dirty = manifest.get("source_dirty")
    if not isinstance(source_status, str):
        unverified.append("source_status_porcelain is missing")
    if not isinstance(source_dirty, bool):
        unverified.append("source_dirty is not boolean")
    elif isinstance(source_status, str) and source_dirty != bool(source_status.strip()):
        failures.append("source_dirty disagrees with source_status_porcelain")

    try:
        manifest_repo = _resolved_path(manifest.get("repository"), "repository")
        state_root = _resolved_path(manifest.get("state_root"), "state_root")
        worktree = _resolved_path(manifest.get("worktree"), "worktree")
        evidence = _resolved_path(manifest.get("evidence"), "evidence")
        manifest_policy = _resolved_path(manifest.get("policy"), "policy")
    except ValueError as exc:
        unverified.append(str(exc))
    else:
        if manifest_repo != repo_root:
            failures.append("manifest repository does not match validator repo root")
        if _under(state_root, repo_root):
            failures.append("state_root must be outside the source repository")
        expected_run_root = (state_root / "runs").resolve()
        expected_evidence_root = (state_root / "evidence").resolve()
        if not _under(worktree, expected_run_root):
            failures.append("worktree must be below state_root/runs")
        if not _under(evidence, expected_evidence_root):
            failures.append("evidence must be below state_root/evidence")
        if _under(worktree, repo_root):
            failures.append("worktree must not be inside the source repository")
        if manifest_policy != policy_path:
            failures.append("manifest policy path does not match canonical policy")
        if check_paths:
            if not worktree.is_dir():
                unverified.append("allocated worktree directory does not exist")
            if not evidence.is_dir():
                unverified.append("allocation evidence directory does not exist")

    expected_policy_hash = sha256_file(policy_path) if policy_path.is_file() else None
    manifest_policy_hash = manifest.get("policy_sha256")
    if expected_policy_hash is None:
        unverified.append("canonical policy file does not exist")
    elif manifest_policy_hash != expected_policy_hash:
        failures.append("policy_sha256 does not match canonical policy bytes")

    if failures:
        return FAIL, failures + unverified
    if unverified:
        return UNVERIFIED, unverified
    return PASS, []


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate VRMine safe verification allocation evidence")
    parser.add_argument("manifest", type=Path)
    parser.add_argument(
        "--policy",
        type=Path,
        default=ROOT / "config" / "verification-worktree-policy.json",
    )
    parser.add_argument("--repo-root", type=Path, default=ROOT)
    parser.add_argument("--no-path-check", action="store_true")
    args = parser.parse_args(argv)
    try:
        manifest = load_json(args.manifest)
        policy = load_json(args.policy)
        status, reasons = evaluate_allocation(
            manifest,
            policy,
            repo_root=args.repo_root,
            policy_path=args.policy,
            check_paths=not args.no_path_check,
        )
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        status, reasons = UNVERIFIED, [str(exc)]

    print(json.dumps({"status": status, "reasons": reasons}, ensure_ascii=False, indent=2, sort_keys=True))
    return EXIT_CODES[status]


if __name__ == "__main__":
    sys.exit(main())
