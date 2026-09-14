"""Materialize one canonical world-item GLB for World Build without product renders/variants.

This is a thin consumer of the existing world_item_factory geometry functions,
not a second generator. It exists so World Build can consume canonical base-SKU
geometry without paying the product-render cost for every dependency.
"""
from __future__ import annotations

import hashlib
import json
import struct
import sys
from copy import deepcopy
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


def canonicalize_glb(path: Path) -> None:
    """Canonicalize primitive buffer layout without changing mesh geometry."""
    data = bytearray(path.read_bytes())
    if data[:4] != b"glTF" or len(data) < 28:
        fail(f"invalid GLB header: {path}")
    json_length = struct.unpack_from("<I", data, 12)[0]
    json_start = 20
    json_end = json_start + json_length
    if json_end + 8 > len(data):
        fail(f"truncated GLB JSON chunk: {path}")
    document = json.loads(bytes(data[json_start:json_end]).rstrip(b" \t\r\n\0"))
    bin_header = json_end
    bin_length, bin_type = struct.unpack_from("<II", data, bin_header)
    if bin_type != 0x004E4942 or bin_header + 8 + bin_length > len(data):
        fail(f"missing GLB binary chunk: {path}")
    bin_start = bin_header + 8
    source_bin = bytes(data[bin_start : bin_start + bin_length])
    component_widths = {5121: 1, 5123: 2, 5125: 4, 5126: 4}
    component_counts = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}
    rebuilt = deepcopy(document)
    rebuilt["bufferViews"] = []
    rebuilt["accessors"] = []
    rebuilt_bin = bytearray()

    def aligned_append(blob: bytes) -> int:
        while len(rebuilt_bin) % 4:
            rebuilt_bin.append(0)
        offset = len(rebuilt_bin)
        rebuilt_bin.extend(blob)
        return offset

    def copy_accessor(accessor_index: int, *, canonical_indices: bool) -> int:
        accessor = document["accessors"][accessor_index]
        if accessor.get("sparse") is not None:
            fail(f"sparse accessor is not supported by canonicalizer: {accessor_index}")
        component_width = component_widths.get(accessor.get("componentType"))
        component_count = component_counts.get(accessor.get("type"))
        if component_width is None or component_count is None:
            fail(f"unsupported accessor format: {accessor_index}")
        count = int(accessor["count"])
        element_width = component_width * component_count
        view = document["bufferViews"][accessor["bufferView"]]
        if view.get("byteStride") is not None:
            fail(f"interleaved accessor is not supported by canonicalizer: {accessor_index}")
        start = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
        length = count * element_width
        raw = source_bin[start : start + length]
        if len(raw) != length:
            fail(f"accessor exceeds GLB binary chunk: {accessor_index}")
        if canonical_indices:
            if accessor.get("type") != "SCALAR" or count % 3:
                fail(f"triangle index accessor is not a complete triangle list: {accessor_index}")
            fmt = {5121: "B", 5123: "H", 5125: "I"}[accessor["componentType"]]
            values = list(struct.unpack(f"<{count}{fmt}", raw))
            triplets = [tuple(values[offset : offset + 3]) for offset in range(0, count, 3)]
            triplets.sort()
            raw = struct.pack(f"<{count}{fmt}", *(value for triplet in triplets for value in triplet))
        view_copy = deepcopy(view)
        view_copy["buffer"] = 0
        view_copy["byteOffset"] = aligned_append(raw)
        view_copy["byteLength"] = len(raw)
        view_copy.pop("byteStride", None)
        view_index = len(rebuilt["bufferViews"])
        rebuilt["bufferViews"].append(view_copy)
        accessor_copy = deepcopy(accessor)
        accessor_copy["bufferView"] = view_index
        accessor_copy.pop("byteOffset", None)
        rebuilt_index = len(rebuilt["accessors"])
        rebuilt["accessors"].append(accessor_copy)
        return rebuilt_index

    for mesh in rebuilt.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            attributes = primitive.get("attributes", {})
            primitive["attributes"] = {
                name: copy_accessor(attributes[name], canonical_indices=False) for name in sorted(attributes)
            }
            if primitive.get("indices") is not None:
                primitive["indices"] = copy_accessor(primitive["indices"], canonical_indices=primitive.get("mode", 4) == 4)

    if rebuilt.get("buffers"):
        rebuilt["buffers"][0]["byteLength"] = len(rebuilt_bin)
    json_bytes = json.dumps(rebuilt, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    json_bytes += b" " * ((-len(json_bytes)) % 4)
    bin_bytes = bytes(rebuilt_bin)
    bin_bytes += b"\0" * ((-len(bin_bytes)) % 4)
    total_length = 12 + 8 + len(json_bytes) + 8 + len(bin_bytes)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, total_length)
        + struct.pack("<II", len(json_bytes), 0x4E4F534A)
        + json_bytes
        + struct.pack("<II", len(bin_bytes), 0x004E4942)
        + bin_bytes
    )


def export_canonical_glb(product, path: Path) -> None:
    last_error = None
    for _attempt in range(5):
        bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", use_selection=True)
        try:
            canonicalize_glb(path)
            return
        except RuntimeError as exc:
            last_error = exc
    raise last_error or RuntimeError(f"unable to export canonical GLB: {path}")


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
    # These base inputs contain no textures; UV islands only add exporter noise.
    while product.data.uv_layers:
        product.data.uv_layers.remove(product.data.uv_layers[0])
    bpy.ops.object.select_all(action="DESELECT")
    product.select_set(True)
    bpy.context.view_layer.objects.active = product

    glb = out / f"{sku}.glb"
    export_canonical_glb(product, glb)
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
