"""Publish self-contained World Build evidence from a CI artifact directory.

The factory consumes materialized inputs from a transient directory. Published
evidence must retain those exact inputs so a clean checkout can re-run the
verifier without depending on the CI workspace.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
OUTPUT_FILES = (
    "world.blend",
    "world.glb",
    "build-plan.json",
    "view-hero.png",
    "view-top.png",
    "view-social-core.png",
    "view-retreat.png",
    "view-circulation.png",
)
RENDER_FILES = OUTPUT_FILES[3:]


def fail(message: str) -> None:
    raise SystemExit(f"World Build Evidence Publish FAIL: {message}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def inside_root(path: Path, label: str) -> Path:
    resolved = path.resolve()
    if resolved != ROOT and ROOT not in resolved.parents:
        fail(f"{label} must be inside the repository: {resolved}")
    return resolved


def arguments() -> tuple[Path, Path, Path]:
    if "--" not in sys.argv:
        fail("usage: python scripts/publish_world_build_evidence.py -- <artifact-root> <destination-root> <source-root>")
    values = sys.argv[sys.argv.index("--") + 1 :]
    if len(values) != 3:
        fail("expected artifact-root, destination-root, and source-root")
    return tuple(inside_root(Path(value), label) for value, label in zip(values, ("artifact root", "destination root", "source root")))


def main() -> None:
    artifact_root, destination_root, source_root = arguments()
    manifest_path = artifact_root / "manifest.json"
    plan_path = artifact_root / "build-plan.json"
    if not manifest_path.is_file() or not plan_path.is_file():
        fail("artifact manifest or build plan is missing")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    asset_ids = sorted(manifest.get("asset_sources", {}))
    if not asset_ids:
        fail("artifact manifest contains no materialized asset sources")

    destination_root.mkdir(parents=True, exist_ok=True)
    for name in OUTPUT_FILES:
        source = artifact_root / name
        if not source.is_file() or source.stat().st_size == 0:
            fail(f"artifact output is missing or empty: {name}")
        shutil.copy2(source, destination_root / name)

    source_destination = destination_root / "source-assets"
    source_destination.mkdir(parents=True, exist_ok=True)
    published_sources = {}
    destination_prefix = destination_root.relative_to(ROOT).as_posix()
    for asset_id in asset_ids:
        source = source_root / asset_id / f"{asset_id}.glb"
        if not source.is_file() or source.stat().st_size == 0:
            fail(f"materialized source is missing or empty: {asset_id}")
        destination = source_destination / f"{asset_id}.glb"
        shutil.copy2(source, destination)
        published_sources[asset_id] = {
            "path": f"{destination_prefix}/source-assets/{asset_id}.glb",
            "sha256": sha256(destination),
        }

    manifest["build_plan"] = f"{destination_prefix}/build-plan.json"
    manifest["build_plan_sha256"] = sha256(destination_root / "build-plan.json")
    manifest["asset_sources"] = published_sources
    manifest["outputs"]["blend"]["sha256"] = sha256(destination_root / "world.blend")
    manifest["outputs"]["glb"]["sha256"] = sha256(destination_root / "world.glb")
    manifest["outputs"]["renders"] = [
        {"path": name, "sha256": sha256(destination_root / name)} for name in RENDER_FILES
    ]
    (destination_root / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (destination_root / "render.sha256").write_text(
        "".join(f"{sha256(destination_root / name)}  {name}\n" for name in RENDER_FILES),
        encoding="utf-8",
    )
    print(json.dumps({"status": "PASS", "destination": str(destination_root), "source_assets": len(asset_ids)}))


if __name__ == "__main__":
    main()
