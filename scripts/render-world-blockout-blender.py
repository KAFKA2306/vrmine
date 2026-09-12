#!/usr/bin/env python3
"""Render WORLD_BLOCKOUT_SPEC-compatible Woodland Tabletop Village in Blender.

Run:
  blender -b --python scripts/render-world-blockout-blender.py -- \
    --spec config/world-design/generated/woodland-tabletop-village-v0.json \
    --output artifacts/world-blockout/woodland-tabletop-village-hero.png \
    --blend artifacts/world-blockout/woodland-tabletop-village.blend
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def args_after_double_dash() -> list[str]:
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--spec", required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--blend")
    p.add_argument("--resolution", type=int, default=1536)
    return p.parse_args(args_after_double_dash())


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(f"WORLD_BLOCKOUT_SPEC render verification FAIL: {message}")


def hex_rgba(value: str, alpha: float = 1.0) -> tuple[float, float, float, float]:
    value = value.lstrip("#")
    require(len(value) == 6, f"invalid hex color: {value}")
    rgb = tuple(int(value[i:i+2], 16) / 255.0 for i in (0, 2, 4))
    return (*rgb, alpha)


def material(name: str, rgba: tuple[float, float, float, float], roughness: float = 0.82):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = rgba
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def add_box(name: str, center: tuple[float, float, float], size: tuple[float, float, float], mat, bevel=0.025):
    bpy.ops.mesh.primitive_cube_add(location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("BlockoutBevel", "BEVEL")
        mod.width = min(bevel, min(size) * 0.15)
        mod.segments = 3
    obj.data.materials.append(mat)
    return obj


def add_cylinder(name: str, center: tuple[float, float, float], radius: float, depth: float, mat, vertices=96):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def add_text(label: str, location: tuple[float, float, float], mat):
    bpy.ops.object.text_add(location=location, rotation=(math.radians(72), 0.0, 0.0))
    obj = bpy.context.object
    obj.name = f"Label_{label}"
    obj.data.body = label
    obj.data.align_x = "CENTER"
    obj.data.size = 0.09
    obj.data.extrude = 0.002
    obj.data.materials.append(mat)
    return obj


def point_camera(camera, target: Vector) -> None:
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    a = parse_args()
    spec_path = Path(a.spec).resolve()
    output_path = Path(a.output).resolve()
    blend_path = Path(a.blend).resolve() if a.blend else None

    require(spec_path.is_file(), f"spec missing: {spec_path}")
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    blockout = spec.get("blockout", {})
    anchors = blockout.get("anchors", [])
    require(len(anchors) == 8, "canonical blockout must contain exactly 8 anchors")
    ids = [x.get("id") for x in anchors]
    require(len(set(ids)) == 8 and all(ids), "anchor ids must be 8 unique non-empty values")

    spatial = spec["spatial_geometry"]
    world_build = spec["world_build"]
    hero = world_build["hero_view"]
    palette = spec["atmosphere"]["palette"]
    tabletop_top = float(blockout["tabletop_top_z_m"])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except Exception:
        scene.render.engine = "BLENDER_EEVEE"

    scene.render.resolution_x = a.resolution
    scene.render.resolution_y = int(a.resolution * 9 / 16)
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.018, 0.026, 0.022, 1.0)
        bg.inputs["Strength"].default_value = 0.22

    mat_floor = material("Floor", hex_rgba("#2E2924"))
    mat_table = material("Table", hex_rgba(palette["wood_brown"]))
    mat_anchor = material("Anchor", hex_rgba(palette["warm_cream"]))
    mat_life = material("LifeTrace", hex_rgba(palette["accent_terracotta"]))
    mat_water = material("Stream", (0.18, 0.38, 0.42, 1.0), 0.45)
    mat_tree = material("GreatTree", hex_rgba(palette["forest_green"]))
    mat_text = material("Labels", (0.92, 0.90, 0.80, 1.0))

    room_w = float(spatial["overall_width_m"])
    room_d = float(spatial["overall_depth_m"])
    add_box("ObservationRoomFloor", (0, 0, -0.03), (room_w, room_d, 0.06), mat_floor, 0.0)

    table = world_build["procedural_assets"]["woodland-diorama-table-v0"]
    diameter = float(table["diameter_m"])
    thickness = float(table["top_thickness_m"])
    add_cylinder(
        "WoodlandTabletop",
        (0.0, 0.0, tabletop_top - thickness / 2),
        diameter / 2,
        thickness,
        mat_table,
    )

    for anchor in anchors:
        anchor_id = anchor["id"]
        pos = tuple(float(v) for v in anchor["position_m"])
        footprint = tuple(float(v) for v in anchor["footprint_m"])
        height = float(anchor["height_m"])
        require(footprint[0] > 0 and footprint[1] > 0 and height > 0, f"invalid dimensions: {anchor_id}")
        center = (pos[0], pos[1], pos[2] + height / 2)
        role = anchor.get("role", "")
        mat = mat_anchor
        if anchor_id == "great_tree":
            mat = mat_tree
        elif anchor_id == "stream":
            mat = mat_water
        elif role == "life_trace":
            mat = mat_life
        obj = add_box(anchor_id, center, (footprint[0], footprint[1], height), mat)
        obj.rotation_euler[2] = math.radians(float(anchor.get("yaw_deg", 0.0)))
        add_text(anchor_id, (pos[0], pos[1], pos[2] + height + 0.035), mat_text)

    spawn = world_build["spawn"]["position_m"]
    add_cylinder("SpawnMarker", (spawn[0], spawn[1], 0.02), 0.10, 0.04, mat_life, vertices=32)

    bpy.ops.object.light_add(type="AREA", location=(0.0, -1.4, 2.45))
    key = bpy.context.object
    key.name = "WarmIndirectKey"
    key.data.energy = 450
    key.data.shape = "DISK"
    key.data.size = 3.2
    key.data.color = (1.0, 0.58, 0.30)
    point_camera(key, Vector((0, 0, tabletop_top)))

    bpy.ops.object.light_add(type="AREA", location=(0.0, 1.8, 1.8))
    fill = bpy.context.object
    fill.name = "VillageFill"
    fill.data.energy = 220
    fill.data.size = 2.2
    fill.data.color = (1.0, 0.40, 0.18)
    point_camera(fill, Vector((0, 0.2, tabletop_top)))

    bpy.ops.object.camera_add(location=tuple(hero["position_m"]))
    camera = bpy.context.object
    camera.name = "HeroView"
    camera.data.lens = 34
    camera.data.sensor_width = 36
    camera.data.clip_start = max(0.01, float(spec["runtime_budget"]["camera_near_clip_m"]))
    camera.data.clip_end = max(100.0, float(spec["runtime_budget"]["background_max_distance_m"]) + 10.0)
    point_camera(camera, Vector(tuple(hero["target_m"])))
    scene.camera = camera

    output_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output_path)

    if blend_path:
        blend_path.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    bpy.ops.render.render(write_still=True)
    require(output_path.is_file() and output_path.stat().st_size > 0, "render PNG was not materialized")
    print(f"PASS: rendered canonical WORLD_BLOCKOUT_SPEC hero view: {output_path}")
    if blend_path:
        print(f"BLEND: {blend_path}")


if __name__ == "__main__":
    main()
