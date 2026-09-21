"""Generate the #449 Geometry Nodes pilot categories from the canonical contract.

Run with Blender:
  blender --background --python scripts/generate-geometry-nodes-pilots.py -- --output <dir>

The script intentionally owns generation only. Unity verification remains the existing
`glb:verify-u2` authority declared by config/geometry-nodes-generation.json.
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
POLICY = ROOT / "config" / "geometry-nodes-generation.json"


def cli() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / ".artifacts" / "geometry-nodes-pilots")
    return parser.parse_args(argv)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = color
    return mat


def cube(name: str, size: tuple[float, float, float], location=(0.0, 0.0, 0.0)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = tuple(v / 2 for v in size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def build_architecture() -> bpy.types.Object:
    root = cube("architecture", (4.0, 3.0, 2.6), (0, 0, 1.3))
    root.data.materials.append(material("GN_Shared_Wood", (0.30, 0.16, 0.07, 1)))
    return root


def build_furniture() -> bpy.types.Object:
    parts = [cube("table_top", (1.2, 0.7, 0.08), (0, 0, 0.72))]
    for x in (-0.5, 0.5):
        for y in (-0.25, 0.25):
            parts.append(cube("table_leg", (0.08, 0.08, 0.72), (x, y, 0.36)))
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    parts[0].name = "furniture"
    parts[0].data.materials.append(material("GN_Shared_Wood", (0.30, 0.16, 0.07, 1)))
    return parts[0]


def build_vegetation() -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.12, depth=1.6, location=(0, 0, 0.8))
    trunk = bpy.context.object
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.8, location=(0, 0, 1.8))
    crown = bpy.context.object
    trunk.select_set(True)
    crown.select_set(True)
    bpy.context.view_layer.objects.active = trunk
    bpy.ops.object.join()
    trunk.name = "vegetation"
    trunk.data.materials.append(material("GN_Shared_Foliage", (0.10, 0.34, 0.08, 1)))
    return trunk


def attach_geometry_nodes(obj: bpy.types.Object, category: str, seed: int) -> None:
    modifier = obj.modifiers.new(name="VRMine_GeometryNodes", type="NODES")
    group = bpy.data.node_groups.new(f"VRMine_{category}_{seed}", "GeometryNodeTree")
    modifier.node_group = group
    group.interface.new_socket(name="Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    group.interface.new_socket(name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    input_node = group.nodes.new("NodeGroupInput")
    output_node = group.nodes.new("NodeGroupOutput")
    group.links.new(input_node.outputs["Geometry"], output_node.inputs["Geometry"])
    obj["vrmine_category"] = category
    obj["vrmine_seed"] = seed


def add_collision(render: bpy.types.Object) -> bpy.types.Object:
    collision = cube(f"{render.name}_collision", tuple(render.dimensions), tuple(render.location))
    collision.display_type = "WIRE"
    collision.hide_render = True
    collision["vrmine_collision_proxy"] = True
    return collision


def export_category(category: str, seed: int, out: Path) -> dict:
    reset_scene()
    builders = {"architecture": build_architecture, "furniture": build_furniture, "vegetation": build_vegetation}
    render = builders[category]()
    attach_geometry_nodes(render, category, seed)
    collision = add_collision(render)
    out.mkdir(parents=True, exist_ok=True)
    blend = out / f"{category}.blend"
    glb = out / f"{category}.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.object.select_all(action="DESELECT")
    render.select_set(True)
    collision.select_set(True)
    bpy.context.view_layer.objects.active = render
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format="GLB", use_selection=True)
    manifest = {
        "category": category,
        "seed": seed,
        "generator": "geometry_nodes",
        "procedural_source": "scripts/generate-geometry-nodes-pilots.py",
        "blend": blend.name,
        "glb": glb.name,
        "collision_proxy": collision.name,
        "runtime": "UNVERIFIED",
    }
    (out / f"{category}.manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    return manifest


def main() -> None:
    args = cli()
    policy = json.loads(POLICY.read_text(encoding="utf-8"))
    manifests = []
    for category, spec in policy["categories"].items():
        if category not in {"architecture", "furniture", "vegetation"}:
            continue
        manifests.append(export_category(category, int(spec["seed"]), args.output / category))
    if len(manifests) < 3:
        raise RuntimeError("canonical policy must produce at least three pilot categories")
    print(json.dumps({"status": "generated", "categories": [m["category"] for m in manifests]}))


if __name__ == "__main__":
    main()
