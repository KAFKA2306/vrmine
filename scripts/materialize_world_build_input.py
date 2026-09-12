"""Materialize one canonical world-item GLB for World Build without product renders/variants.

This is a thin consumer of the existing world_item_factory geometry functions,
not a second generator. It exists so World Build can consume canonical base-SKU
geometry without paying the product-render cost for every dependency.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from world_item_factory import join_parts, load_spec, make_material, make_part, world_bounds  # noqa: E402


def fail(message: str) -> None:
    raise RuntimeError(f"World Build Input FAIL: {message}")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def args() -> tuple[Path, Path]:
    if "--" not in sys.argv:
        raise SystemExit("usage: blender -b --python scripts/materialize_world_build_input.py -- <spec.json> <output-root>")
    rest = sys.argv[sys.argv.index("--") + 1 :]
    if len(rest) != 2:
        raise SystemExit("expected spec path and output root")
    spec_path = (ROOT / rest[0]).resolve()
    output_root = (ROOT / rest[1]).resolve()
    if ROOT not in spec_path.parents or spec_path.suffix != ".json":
        raise SystemExit("spec must be a repository JSON file")
    if ROOT not in output_root.parents and output_root != ROOT:
        raise SystemExit("output root must be inside repository")
    return spec_path, output_root


def main() -> None:
    if bpy.app.version[:2] != (4, 2):
        fail("Blender 4.2 is required")
    spec_path, output_root = args()
    spec = load_spec(spec_path)
    sku = spec["id"]
    out = output_root / sku
    out.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    materials = {name: make_material(name, data) for name, data in spec["materials"].items()}
    parts = [make_part(part, materials) for part in spec["parts"]]
    product = join_parts(parts, sku)
    bpy.ops.object.select_all(action="DESELECT")
    product.select_set(True)
    bpy.context.view_layer.objects.active = product

    glb = out / f"{sku}.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", use_selection=True)
    if not glb.is_file() or glb.stat().st_size == 0 or glb.read_bytes()[:4] != b"glTF":
        fail(f"invalid generated GLB: {glb}")

    product.data.calc_loop_triangles()
    _lo, _hi, _center, size, _points = world_bounds(product)
    actual = [float(value) for value in size]
    target = [float(value) for value in spec["dimensions_m"]]
    for value, wanted in zip(actual, target):
        if value <= 0 or value > wanted * 1.15:
            fail(f"dimension contract failed: actual={actual}, target={target}")
    triangles = len(product.data.loop_triangles)
    if triangles <= 0 or triangles > 10000:
        fail(f"triangle budget failed: {triangles}")

    evidence = {
        "schema_version": 1,
        "id": sku,
        "source_spec": spec_path.relative_to(ROOT).as_posix(),
        "source_spec_sha256": sha256(spec_path),
        "glb_sha256": sha256(glb),
        "dimensions_m_actual": actual,
        "dimensions_m_target": target,
        "triangles": triangles,
        "generator": "scripts/world_item_factory.py",
        "mode": "world_build_base_glb_only",
    }
    (out / "world-build-input.json").write_text(json.dumps(evidence, indent=2) + "\n")
    print(json.dumps({"status": "PASS", "id": sku, "triangles": triangles, "glb": str(glb)}))


if __name__ == "__main__":
    main()
