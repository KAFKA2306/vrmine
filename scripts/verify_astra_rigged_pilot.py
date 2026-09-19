"""Independent Blender verification for Astra Pilot B."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy

from verify_rig_contract import assert_rig_contract
from verify_uv_texture_contract import assert_uv_texture_contract

OUT = Path(sys.argv[-1]).resolve()
REQUIRED_RENDERS = {
    "front_3_4.png",
    "rear_3_4.png",
    "left_side.png",
    "right_side.png",
    "top_overview.png",
    "geometry_diagnostic.png",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    manifest_path = OUT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("pilot") != "B" or manifest.get("seed") != 40102:
        raise AssertionError("Pilot B identity/seed drift")
    if manifest.get("source_asset_license") != "CC0-1.0":
        raise AssertionError("Pilot B provenance missing")
    render_paths = {output["path"] for output in manifest["outputs"]["renders"]}
    if render_paths != REQUIRED_RENDERS:
        raise AssertionError(f"Pilot B diagnostic render set drift: {sorted(render_paths)}")
    for output in (manifest["outputs"]["blend"], manifest["outputs"]["glb"], *manifest["outputs"]["renders"]):
        path = OUT / output["path"]
        if not path.is_file() or sha256(path) != output["sha256"]:
            raise AssertionError(f"artifact digest mismatch: {path}")

    bpy.ops.wm.open_mainfile(filepath=str(OUT / manifest["outputs"]["blend"]["path"]))
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    rig = assert_rig_contract(meshes, armatures)
    uv_texture = assert_uv_texture_contract(meshes, artifact_root=OUT)
    if len(meshes) != 1:
        raise AssertionError(f"expected one mesh, got {len(meshes)}")
    mesh = meshes[0]
    shape_keys = mesh.data.shape_keys
    if not shape_keys or "bend" not in shape_keys.key_blocks:
        raise AssertionError("Pilot B shape key is missing")
    bend = shape_keys.key_blocks["bend"]
    basis = shape_keys.key_blocks["Basis"]
    if not any((bend.data[i].co - basis.data[i].co).length > 1e-6 for i in range(len(bend.data))):
        raise AssertionError("Pilot B shape key has no deformation")
    print(json.dumps({"status": "PASS", "pilot": "B", "rig": rig, "uv_texture": uv_texture, "shape_key": "bend", "renders": sorted(render_paths)}))


if __name__ == "__main__":
    main()
