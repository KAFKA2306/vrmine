"""Verify one generated world-item SKU with Blender 4.2."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
EXPECTED_VIEWS = ("hero", "front", "rear", "left", "right", "top")
CENTER_ERROR_LIMIT = 0.02
FILL_RATIO_MIN = 0.80
FILL_RATIO_MAX = 0.88
FRAME_LIMIT = 0.500001


def arg_path() -> Path:
    if "--" not in sys.argv:
        raise SystemExit("spec path is required after --")
    rest = sys.argv[sys.argv.index("--") + 1 :]
    if len(rest) != 1:
        raise SystemExit("expected exactly one spec path")
    return (ROOT / rest[0]).resolve()


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def assert_mesh_import(path: Path, kind: str) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if kind == "glb":
        if path.read_bytes()[:4] != b"glTF":
            raise AssertionError(f"invalid GLB header: {path}")
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif kind == "fbx":
        if not path.read_bytes().startswith(b"Kaydara FBX Binary"):
            raise AssertionError(f"invalid FBX header: {path}")
        bpy.ops.import_scene.fbx(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes or not any(len(obj.data.polygons) > 0 for obj in meshes):
        raise AssertionError(f"no mesh geometry after {kind} import")


def main() -> None:
    if bpy.app.version[:2] != (4, 2):
        raise RuntimeError("Blender 4.2 is required")
    spec_path = arg_path()
    spec = json.loads(spec_path.read_text())
    sku = spec["id"]
    out = ROOT / ".artifacts" / "world-items" / sku
    manifest_path = out / "manifest.json"
    if not manifest_path.is_file():
        raise AssertionError("manifest missing")
    manifest = json.loads(manifest_path.read_text())
    if manifest["id"] != sku or manifest["source_spec"] != spec_path.relative_to(ROOT).as_posix():
        raise AssertionError("manifest identity mismatch")
    if manifest["spec_sha256"] != digest(spec_path):
        raise AssertionError("spec hash mismatch")
    if manifest["unity_status"] != "UNVERIFIED" or manifest["vrchat_status"] != "UNVERIFIED":
        raise AssertionError("runtime status was promoted without runtime evidence")

    blend = out / f"{sku}.blend"
    glb = out / f"{sku}.glb"
    fbx = out / f"{sku}.fbx"
    for path in (blend, glb, fbx):
        if not path.is_file() or path.stat().st_size == 0:
            raise AssertionError(f"missing format: {path.name}")
    if not blend.read_bytes().startswith(b"BLENDER"):
        raise AssertionError("invalid blend header")
    assert_mesh_import(glb, "glb")
    assert_mesh_import(fbx, "fbx")

    expected_pngs = ["thumbnail.png"] + [f"view-{name}.png" for name in EXPECTED_VIEWS]
    for name in expected_pngs:
        path = out / name
        if not path.is_file() or path.stat().st_size < 1024:
            raise AssertionError(f"render missing or too small: {name}")
        if path.read_bytes()[:8] != PNG_MAGIC:
            raise AssertionError(f"not PNG: {name}")

    if manifest.get("schema_version") != 2:
        raise AssertionError(f"unexpected manifest schema: {manifest.get('schema_version')}")
    framing = manifest.get("render_framing")
    if not isinstance(framing, dict) or set(framing) != set(EXPECTED_VIEWS):
        raise AssertionError("render framing metadata missing or incomplete")
    for view in EXPECTED_VIEWS:
        data = framing[view]
        center_x = float(data["center_error_x"])
        center_y = float(data["center_error_y"])
        fill_ratio = float(data["fill_ratio"])
        bounds = [float(value) for value in data["normalized_bounds"]]
        if len(bounds) != 4:
            raise AssertionError(f"{view}: normalized bounds must have four values")
        if abs(center_x) > CENTER_ERROR_LIMIT or abs(center_y) > CENTER_ERROR_LIMIT:
            raise AssertionError(
                f"{view}: render center error exceeded: x={center_x}, y={center_y}"
            )
        if not FILL_RATIO_MIN <= fill_ratio <= FILL_RATIO_MAX:
            raise AssertionError(f"{view}: unexpected fill ratio: {fill_ratio}")
        if min(bounds) < -FRAME_LIMIT or max(bounds) > FRAME_LIMIT:
            raise AssertionError(f"{view}: projected bounds exceed frame: {bounds}")

    for name, expected in manifest["sha256"].items():
        path = out / name
        if digest(path) != expected:
            raise AssertionError(f"hash mismatch: {name}")
    dims = manifest["dimensions_m_actual"]
    target = spec["dimensions_m"]
    for actual, wanted in zip(dims, target):
        if actual <= 0 or actual > wanted * 1.15:
            raise AssertionError(f"unexpected dimensions: actual={dims}, spec={target}")
    if manifest["triangles"] <= 0 or manifest["triangles"] > 10000:
        raise AssertionError(f"triangle budget exceeded: {manifest['triangles']}")
    print(json.dumps({"id": sku, "formats": "PASS", "geometry": "PASS", "renders": "PASS", "framing": "PASS", "triangles": manifest["triangles"]}))


if __name__ == "__main__":
    main()
