#!/usr/bin/env python3
import argparse
import json
import re
import subprocess
from pathlib import Path

SHA40 = re.compile(r"^[0-9a-f]{40}$")
REQUIRED_FORBIDDEN = {
    "git worktree remove",
    "git clean",
    "git reset --hard",
    "Remove-Item -Recurse",
    "rm -rf",
}


def inside(child: Path, parent: Path) -> bool:
    try:
        child.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def git_head(path: Path) -> str:
    return subprocess.check_output(
        ["git", "-C", str(path), "rev-parse", "HEAD"], text=True
    ).strip().lower()


def validate(manifest_path: Path, policy_path: Path) -> dict:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    policy = json.loads(policy_path.read_text(encoding="utf-8-sig"))
    errors = []

    if manifest.get("state") != "ALLOCATED":
        errors.append("manifest state must be ALLOCATED")

    revision = str(manifest.get("revision", "")).lower()
    source_head = str(manifest.get("source_head", "")).lower()
    if not SHA40.fullmatch(revision):
        errors.append("revision must be an exact 40-character SHA")
    if not SHA40.fullmatch(source_head):
        errors.append("source_head must be an exact 40-character SHA")

    if not isinstance(manifest.get("source_dirty"), bool):
        errors.append("source_dirty must be boolean")
    status = manifest.get("source_status_porcelain")
    if not isinstance(status, list) or not all(isinstance(item, str) for item in status):
        errors.append("source_status_porcelain must be a string array")
    elif manifest.get("source_dirty") != bool(status):
        errors.append("source_dirty must match source_status_porcelain")

    state_root = Path(str(manifest.get("state_root", "")))
    worktree = Path(str(manifest.get("worktree", "")))
    if (
        not state_root.is_absolute()
        or not worktree.is_absolute()
        or not inside(worktree, state_root / "runs")
    ):
        errors.append("worktree must be inside state_root/runs")

    if revision and worktree.is_dir():
        try:
            if git_head(worktree) != revision:
                errors.append("worktree HEAD does not match revision")
        except Exception as exc:
            errors.append(f"cannot verify worktree HEAD: {exc}")
    else:
        errors.append("allocated worktree does not exist")

    normal = policy.get("normal_run", {})
    destructive = policy.get("destructive_recovery", {})
    if manifest.get("recovery_policy") != "allocate-new-run":
        errors.append("manifest recovery_policy must be allocate-new-run")
    if (
        normal.get("collision_strategy") != "allocate-new-run"
        or normal.get("retry_strategy") != "allocate-new-run"
    ):
        errors.append("policy must allocate a new run on collision and retry")
    if (
        normal.get("delete_existing_run") is not False
        or normal.get("reuse_failed_run") is not False
        or normal.get("reuse_partial_run") is not False
    ):
        errors.append("policy must forbid deletion/reuse during normal runs")

    forbidden = set(destructive.get("forbidden_commands", []))
    if destructive.get("allowed") is not False or not REQUIRED_FORBIDDEN.issubset(forbidden):
        errors.append("destructive recovery policy is not fail-closed")

    if errors:
        raise SystemExit("FAIL safe verification isolation: " + "; ".join(errors))

    result = {
        "status": "PASS",
        "run_id": manifest.get("run_id"),
        "source_head": source_head,
        "source_dirty": manifest.get("source_dirty"),
        "revision": revision,
        "worktree": str(worktree),
    }
    print(json.dumps(result, separators=(",", ":")))
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument(
        "--policy",
        type=Path,
        default=Path("config/verification-worktree-policy.json"),
    )
    args = parser.parse_args()
    validate(args.manifest, args.policy)


if __name__ == "__main__":
    main()
