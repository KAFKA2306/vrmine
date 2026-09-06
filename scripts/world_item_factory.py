"""Deterministic Blender 4.2 generator for config/world-items/*.json.

Usage:
  blender -b --python-exit-code 1 --python scripts/world_item_factory.py -- config/world-items/<id>.json
"""
from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
VIEW_OFFSETS = {
    "hero": Vector((2.7, -3.8, 2.35)),
    "front": Vector((0.0, -4.2, 0.57)),
    "rear": Vector((0.0, 4.2, 0.57)),
    "left": Vector((-4.2, 0.0, 0.57)),
    "right": Vector((4.2, 0.0, 0.57)),
    "top": Vector((0.0, -0.01, 5.0)),
}
FRAME_FILL = 0.84


def args() -> Path:
    if "--" not in sys.argv:
        raise SystemExit("spec path is required after --")
    rest = sys.argv[sys.argv.index("--") + 1 :]
    if len(rest) != 1:
        raise SystemExit("expected exactly one spec path")
    path = (ROOT / rest[0]).resolve()
    if ROOT not in path.parents or path.suffix != ".json":
        raise SystemExit("spec must be a repository JSON file")
    return path


def load_spec(path: Path) -> dict:
    spec = json.loads(path.read_text())
    required = {
        "id", "family", "display_name", "dimensions_m", "parts", "materials",
        "variants", "price_hypothesis", "license", "formats", "unity_status",
        "vrchat_status", "booth_status",
    }
    missing = sorted(required - spec.keys())
    if missing:
        raise ValueError(f"missing spec keys: {missing}")
    if not spec["id"] or "/" in spec["id"] or ".." in spec["id"]:
        raise ValueError("invalid id")
    if set(spec["formats"]) != {"blend", "glb", "fbx"}:
        raise ValueError("formats must be exactly blend/glb/fbx")
    return spec


def make_material(name: str, data: dict):
    rgb = data["base_color"]
    if len(rgb) != 3:
        raise ValueError(f"material {name}: base_color must have 3 values")
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = float(data["roughness"])
    bsdf.inputs["Metallic"].default_value = float(data.get("metallic", 0.0))
    return mat


def finish(obj, mat, bevel=0.004):
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth = False
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def make_part(part: dict, materials: dict):
    component = part["component"]
    mat = materials[part["material"]]
    position = tuple(float(v) for v in part.get("position", [0, 0, 0]))
    if component == "box":
        bpy.ops.mesh.primitive_cube_add(size=1, location=position)
        obj = bpy.context.object
        obj.dimensions = tuple(float(v) for v in part["size"])
        rotation = [math.radians(float(v)) for v in part.get("rotation_deg", [0, 0, 0])]
        obj.rotation_euler = rotation
        finish(obj, mat, min(0.004, min(obj.dimensions) / 5))
    elif component == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=int(part.get("vertices", 48)),
            radius=float(part["radius"]),
            depth=float(part["height"]),
            location=position,
        )
        obj = finish(bpy.context.object, mat)
    else:
        raise ValueError(f"unsupported component: {component}")
    obj.name = part["name"]
    return obj


def join_parts(parts, sku):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = sku
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return obj


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    bbox_min = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
    bbox_max = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
    center = (bbox_min + bbox_max) * 0.5
    size = bbox_max - bbox_min
    return bbox_min, bbox_max, center, size, corners


def setup_scene(dimensions, center):
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    backdrop = make_material("Backdrop", {"base_color": [0.70, 0.74, 0.73], "roughness": 0.9, "metallic": 0})
    bpy.ops.mesh.primitive_plane_add(size=max(dimensions) * 4.2, location=(0, 0, 0))
    floor = bpy.context.object
    floor.data.materials.append(backdrop)

    bpy.ops.object.camera_add(location=center + VIEW_OFFSETS["hero"])
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.0
    scene.camera = camera

    for location, energy, size in [((2.5, -3.5, 4.0), 650, 3.5), ((-3.0, -1.0, 2.2), 260, 2.8)]:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()

    world = bpy.data.worlds.new("World item studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.14, 0.15, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    scene.world = world
    return scene, camera


def render_views(scene, camera, out: Path, center: Vector, corners):
    aspect = scene.render.resolution_x / scene.render.resolution_y
    framing = {}
    for name, offset in VIEW_OFFSETS.items():
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        bpy.context.view_layer.update()

        inverse_camera = camera.matrix_world.inverted()
        projected = [inverse_camera @ point for point in corners]
        min_x = min(point.x for point in projected)
        max_x = max(point.x for point in projected)
        min_y = min(point.y for point in projected)
        max_y = max(point.y for point in projected)
        projected_width = max_x - min_x
        projected_height = max_y - min_y
        if projected_width <= 0 or projected_height <= 0:
            raise ValueError(f"invalid projected bounds for {name}: {projected_width} x {projected_height}")

        camera.data.ortho_scale = max(projected_height, projected_width / aspect) / FRAME_FILL
        frame_height = camera.data.ortho_scale
        frame_width = frame_height * aspect
        normalized_bounds = [
            min_x / frame_width,
            max_x / frame_width,
            min_y / frame_height,
            max_y / frame_height,
        ]
        center_error_x = (normalized_bounds[0] + normalized_bounds[1]) * 0.5
        center_error_y = (normalized_bounds[2] + normalized_bounds[3]) * 0.5
        fill_ratio = max(
            normalized_bounds[1] - normalized_bounds[0],
            normalized_bounds[3] - normalized_bounds[2],
        )
        framing[name] = {
            "center_error_x": round(float(center_error_x), 10),
            "center_error_y": round(float(center_error_y), 10),
            "fill_ratio": round(float(fill_ratio), 10),
            "normalized_bounds": [round(float(value), 10) for value in normalized_bounds],
            "ortho_scale": round(float(camera.data.ortho_scale), 10),
        }

        scene.render.filepath = str(out / f"view-{name}.png")
        bpy.ops.render.render(write_still=True)
    (out / "thumbnail.png").write_bytes((out / "view-hero.png").read_bytes())
    return framing


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    if bpy.app.version[:2] != (4, 2):
        raise RuntimeError("Blender 4.2 is required")
    spec_path = args()
    spec = load_spec(spec_path)
    sku = spec["id"]
    out = ROOT / ".artifacts" / "world-items" / sku
    out.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    materials = {name: make_material(name, data) for name, data in spec["materials"].items()}
    parts = [make_part(part, materials) for part in spec["parts"]]
    product = join_parts(parts, sku)

    bpy.ops.object.select_all(action="DESELECT")
    product.select_set(True)
    bpy.context.view_layer.objects.active = product
    bpy.ops.export_scene.gltf(filepath=str(out / f"{sku}.glb"), export_format="GLB", use_selection=True)
    bpy.ops.export_scene.fbx(
        filepath=str(out / f"{sku}.fbx"), use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y", bake_anim=False,
    )

    product.data.calc_loop_triangles()
    bbox_min, bbox_max, bounds_center, bounds_size, bounds_corners = world_bounds(product)
    dimensions = [float(value) for value in bounds_size]
    scene, camera = setup_scene(dimensions, bounds_center)
    bpy.ops.wm.save_as_mainfile(filepath=str(out / f"{sku}.blend"))
    framing = render_views(scene, camera, out, bounds_center, bounds_corners)

    expected = [f"{sku}.{ext}" for ext in ("blend", "glb", "fbx")]
    expected += ["thumbnail.png"] + [f"view-{name}.png" for name in VIEW_OFFSETS]
    manifest = {
        "schema_version": 2,
        "id": sku,
        "source_spec": spec_path.relative_to(ROOT).as_posix(),
        "spec_sha256": sha256(spec_path),
        "blender": bpy.app.version_string,
        "units": "metres",
        "dimensions_m_actual": dimensions,
        "bounds_min_m": [float(value) for value in bbox_min],
        "bounds_max_m": [float(value) for value in bbox_max],
        "bounds_center_m": [float(value) for value in bounds_center],
        "render_framing": framing,
        "triangles": len(product.data.loop_triangles),
        "parts_count": len(spec["parts"]),
        "formats": spec["formats"],
        "unity_status": spec["unity_status"],
        "vrchat_status": spec["vrchat_status"],
        "booth_status": spec["booth_status"],
        "sha256": {name: sha256(out / name) for name in expected},
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
