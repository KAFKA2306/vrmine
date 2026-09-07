"""Verify one generated world-item SKU with Blender 4.2."""
from __future__ import annotations

import copy
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


def args() -> tuple[Path, str | None]:
    if "--" not in sys.argv:
        raise SystemExit("spec path is required after --")
    rest = sys.argv[sys.argv.index("--") + 1 :]
    if len(rest) not in (1, 2):
        raise SystemExit("expected spec path and optional variant id")
    return (ROOT / rest[0]).resolve(), rest[1] if len(rest) == 2 else None


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def data_digest(data: object) -> str:
    return hashlib.sha256(
        json.dumps(data, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()


def expected_variant_spec(base: dict, variant_id: str) -> tuple[dict, dict]:
    matches = [variant for variant in base["variants"] if variant.get("id") == variant_id]
    if len(matches) != 1:
        raise AssertionError(f"variant must resolve exactly once: {variant_id}")
    variant = matches[0]
    expected = copy.deepcopy(base)
    parts = {part["name"]: part for part in expected["parts"]}
    for name, override in variant.get("part_overrides", {}).items():
        if name not in parts:
            raise AssertionError(f"variant references missing part: {name}")
        parts[name].update(override)
    for name, override in variant.get("material_overrides", {}).items():
        if name not in expected["materials"]:
            raise AssertionError(f"variant references missing material: {name}")
        expected["materials"][name].update(override)
    expected["id"] = f'{base["id"]}--{variant_id}'
    expected["base_id"] = base["id"]
    expected["variant_id"] = variant_id
    return expected, variant


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
    spec_path, variant_id = args()
    base = json.loads(spec_path.read_text())
    expected_spec = base
    expected_variant = None
    sku = base["id"]
    if variant_id:
        expected_spec, expected_variant = expected_variant_spec(base, variant_id)
        sku = expected_spec["id"]

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

    if variant_id:
        if manifest.get("schema_version") != 3:
            raise AssertionError(f"unexpected variant manifest schema: {manifest.get('schema_version')}")
        if manifest.get("base_id") != base["id"] or manifest.get("variant_id") != variant_id:
            raise AssertionError("variant lineage mismatch")
        resolved_path = out / "resolved-spec.json"
        if not resolved_path.is_file():
            raise AssertionError("resolved variant spec missing")
        resolved = json.loads(resolved_path.read_text())
        if resolved != expected_spec:
            raise AssertionError("resolved variant spec differs from declared overrides")
        if manifest.get("resolved_spec_sha256") != data_digest(expected_spec):
            raise AssertionError("resolved spec digest mismatch")
        if manifest.get("variant_sha256") != data_digest(expected_variant):
            raise AssertionError("variant digest mismatch")
    elif manifest.get("schema_version") != 2:
        raise AssertionError(f"unexpected base manifest schema: {manifest.get('schema_version')}")

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
    target = expected_spec["dimensions_m"]
    for actual, wanted in zip(dims, target):
        if actual <= 0 or actual > wanted * 1.15:
            raise AssertionError(f"unexpected dimensions: actual={dims}, spec={target}")
    if manifest["triangles"] <= 0 or manifest["triangles"] > 10000:
        raise AssertionError(f"triangle budget exceeded: {manifest['triangles']}")
    print(json.dumps({"id": sku, "variant": variant_id, "formats": "PASS", "geometry": "PASS", "renders": "PASS", "framing": "PASS", "triangles": manifest["triangles"]}))


if __name__ == "__main__":
    main()
