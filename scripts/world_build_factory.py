import bpy
import hashlib
import json
import math
import os
import subprocess
import sys
from pathlib import Path
from mathutils import Vector


def args_after_double_dash():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def fail(message):
    raise RuntimeError(f"World Build Factory FAIL: {message}")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def source_commit():
    env = os.environ.get("SOURCE_COMMIT") or os.environ.get("GITHUB_SHA")
    if env:
        return env
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    except Exception:
        return "UNVERIFIED"


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_material(name, rgba, roughness=0.6, emission=None):
    material = bpy.data.materials.new(name)
    material.diffuse_color = rgba
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is None:
        fail("Principled BSDF node is unavailable")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission
        elif "Emission" in bsdf.inputs:
            bsdf.inputs["Emission"].default_value = emission
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 1.5
    return material


def cube(name, location, scale, material):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material:
        obj.data.materials.append(material)
    return obj


def cylinder(name, location, radius, depth, material, vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    if material:
        obj.data.materials.append(material)
    return obj


def set_instance_rotation(root, position, facing_target=None, yaw_deg=None):
    if facing_target is not None:
        delta = Vector(facing_target) - Vector(position)
        root.rotation_euler[2] = math.atan2(delta.y, delta.x) - math.pi / 2
    elif yaw_deg is not None:
        root.rotation_euler[2] = math.radians(float(yaw_deg))


def import_glb_instance(asset_id, instance_id, position, facing_target, collection, asset_root, yaw_deg=None):
    source = asset_root / asset_id / f"{asset_id}.glb"
    if not source.is_file():
        fail(f"missing world-build source asset {source}")
    before_names = set(bpy.data.objects.keys())
    bpy.ops.import_scene.gltf(filepath=str(source))
    imported = [obj for obj in bpy.data.objects if obj.name not in before_names]
    if not imported:
        fail(f"asset {asset_id} imported no objects")
    root = bpy.data.objects.new(instance_id, None)
    collection.objects.link(root)
    for obj in imported:
        if obj.parent is None:
            obj.parent = root
    root.location = position
    set_instance_rotation(root, position, facing_target, yaw_deg)
    root["asset_id"] = asset_id
    root["source_kind"] = "glb"
    root["source_glb"] = str(source)
    root["source_sha256"] = sha256(source)
    return root, source


def procedural_instance(asset_id, instance_id, geometry, position, facing_target, collection, wood, wood_dark):
    kind = geometry.get("kind")
    root = bpy.data.objects.new(instance_id, None)
    collection.objects.link(root)

    created = []
    if kind == "round_table":
        diameter = float(geometry["diameter_m"])
        height = float(geometry["height_m"])
        top_t = float(geometry["top_thickness_m"])
        created.append(cylinder(f"{instance_id}_Top", (0, 0, height - top_t / 2), diameter / 2, top_t, wood, 64))
        created.append(cylinder(f"{instance_id}_Pedestal", (0, 0, (height - top_t) / 2), max(0.12, diameter * 0.075), height - top_t, wood_dark, 32))
        created.append(cylinder(f"{instance_id}_Foot", (0, 0, 0.035), diameter * 0.24, 0.07, wood_dark, 48))
    elif kind == "stool":
        width = float(geometry["width_m"])
        depth = float(geometry["depth_m"])
        height = float(geometry["height_m"])
        seat_t = min(0.06, height * 0.12)
        created.append(cylinder(f"{instance_id}_Seat", (0, 0, height - seat_t / 2), min(width, depth) / 2, seat_t, wood, 48))
        inset_x = width * 0.30
        inset_y = depth * 0.30
        leg_h = height - seat_t
        leg_w = min(width, depth) * 0.11
        for sx in (-1, 1):
            for sy in (-1, 1):
                created.append(cube(
                    f"{instance_id}_Leg_{sx}_{sy}",
                    (sx * inset_x, sy * inset_y, leg_h / 2),
                    (leg_w, leg_w, leg_h),
                    wood_dark,
                ))
    elif kind == "bench":
        width = float(geometry["width_m"])
        depth = float(geometry["depth_m"])
        height = float(geometry["height_m"])
        seat_y = height * 0.55
        seat_t = 0.07
        created.append(cube(f"{instance_id}_Seat", (0, 0, seat_y), (width, depth, seat_t), wood))
        back_h = height - seat_y + 0.04
        created.append(cube(
            f"{instance_id}_Back",
            (0, depth / 2 - 0.035, seat_y + back_h / 2 - 0.02),
            (width, 0.07, back_h),
            wood,
        ))
        leg_h = seat_y - seat_t / 2
        for sx in (-1, 1):
            created.append(cube(
                f"{instance_id}_Leg_{sx}",
                (sx * width * 0.36, 0, leg_h / 2),
                (0.08, depth * 0.72, leg_h),
                wood_dark,
            ))
    else:
        fail(f"unsupported procedural_blockout kind {kind!r} for {asset_id}")

    for obj in created:
        obj.parent = root
    root.location = position
    set_instance_rotation(root, position, facing_target, None)
    root["asset_id"] = asset_id
    root["source_kind"] = "procedural_blockout"
    root["geometry_json"] = json.dumps(geometry, sort_keys=True)
    return root


def instantiate(record, instance_id, position, facing_target, collection, asset_root, wood, wood_dark, yaw_deg=None):
    kind = record.get("source_kind", "glb")
    if kind == "glb":
        return import_glb_instance(
            record["asset_id"], instance_id, position, facing_target, collection, asset_root, yaw_deg
        )
    if kind == "procedural_blockout":
        root = procedural_instance(
            record["asset_id"], instance_id, record["geometry"], position, facing_target, collection, wood, wood_dark
        )
        if yaw_deg is not None:
            set_instance_rotation(root, position, None, yaw_deg)
        return root, None
    fail(f"unsupported source_kind {kind!r} for {record.get('asset_id')}")


def create_shell(plan, shell_material, accent_material):
    w = float(plan["footprint"]["width"])
    d = float(plan["footprint"]["depth"])
    h = float(plan["footprint"]["height"])
    t = 0.08
    floor = cube("WorldShell_Floor", (0, 0, -t / 2), (w, d, t), shell_material)
    ceiling = cube("WorldShell_Ceiling", (0, 0, h + t / 2), (w, d, t), shell_material)
    left = cube("WorldShell_Left", (-w / 2 - t / 2, 0, h / 2), (t, d, h), shell_material)
    right = cube("WorldShell_Right", (w / 2 + t / 2, 0, h / 2), (t, d, h), shell_material)
    front = cube("WorldShell_Front", (0, -d / 2 - t / 2, h / 2), (w, t, h), shell_material)

    opening = plan.get("view_opening")
    if not opening:
        back = cube("WorldShell_Back", (0, d / 2 + t / 2, h / 2), (w, t, h), shell_material)
        return [floor, ceiling, left, right, front, back]

    ow = float(opening["width"])
    sill = float(opening["sill"])
    oh = float(opening["height"])
    if ow <= 0 or oh <= 0 or ow >= w or sill < 0 or sill + oh >= h:
        fail("view_opening is outside the room wall contract")
    side_w = (w - ow) / 2
    back_y = d / 2 + t / 2
    parts = [floor, ceiling, left, right, front]
    parts.append(cube("WorldShell_Back_Left", (-(ow / 2 + side_w / 2), back_y, h / 2), (side_w, t, h), shell_material))
    parts.append(cube("WorldShell_Back_Right", ((ow / 2 + side_w / 2), back_y, h / 2), (side_w, t, h), shell_material))
    if sill > 0:
        parts.append(cube("WorldShell_Back_Sill", (0, back_y, sill / 2), (ow, t, sill), shell_material))
    top_h = h - sill - oh
    parts.append(cube("WorldShell_Back_Header", (0, back_y, sill + oh + top_h / 2), (ow, t, top_h), shell_material))
    frame_t = 0.05
    parts.append(cube("ActivityAnchor_Frame_Left", (-ow / 2, d / 2 - frame_t, sill + oh / 2), (frame_t, frame_t, oh), accent_material))
    parts.append(cube("ActivityAnchor_Frame_Right", (ow / 2, d / 2 - frame_t, sill + oh / 2), (frame_t, frame_t, oh), accent_material))
    return parts


def render_view(name, position, target, out_dir, resolution=(960, 540)):
    bpy.ops.object.camera_add(location=position)
    camera = bpy.context.object
    camera.name = f"RenderCamera_{name}"
    look_at(camera, target)
    camera.data.lens = 34
    camera.data.clip_start = 0.01
    camera.data.clip_end = 100
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(out_dir / f"view-{name}.png")
    bpy.ops.render.render(write_still=True)
    return out_dir / f"view-{name}.png"


def main():
    argv = args_after_double_dash()
    if len(argv) not in (2, 3):
        fail("usage: blender -b --python scripts/world_build_factory.py -- <build-plan.json> <output-dir> [asset-root]")
    plan_path = Path(argv[0])
    out_dir = Path(argv[1])
    asset_root = Path(argv[2]) if len(argv) == 3 else Path("pages/io/items")
    if not plan_path.is_file():
        fail(f"missing build plan {plan_path}")
    out_dir.mkdir(parents=True, exist_ok=True)
    plan = json.loads(plan_path.read_text())
    if plan.get("schema_version") != 4:
        fail("build plan schema_version must be 4")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    if scene.world is None:
        scene.world = bpy.data.worlds.new("WorldBuildWorld")
    scene.world.color = (0.018, 0.022, 0.028)

    build_collection = bpy.data.collections.new("WorldBuildInstances")
    scene.collection.children.link(build_collection)
    shell_material = make_material("WorldShell", (0.18, 0.16, 0.14, 1), 0.82)
    accent_material = make_material("ActivityAnchor", (0.08, 0.2, 0.28, 1), 0.35, (0.08, 0.28, 0.42, 1))
    wood = make_material("ProceduralWood", (0.34, 0.20, 0.10, 1), 0.72)
    wood_dark = make_material("ProceduralWoodDark", (0.16, 0.085, 0.045, 1), 0.82)
    create_shell(plan, shell_material, accent_material)

    asset_sources = {}
    procedural_sources = {}

    def record_source(root, source):
        asset_id = root["asset_id"]
        if root["source_kind"] == "glb":
            asset_sources[asset_id] = {"path": str(source), "sha256": root["source_sha256"]}
        else:
            procedural_sources[asset_id] = json.loads(root["geometry_json"])

    table = plan["social_core"]["table"]
    root, source = instantiate(
        table, "SocialTable", table["position_m"], plan["social_core"]["center_m"],
        build_collection, asset_root, wood, wood_dark
    )
    record_source(root, source)

    for seat in plan["social_core"]["seats"]:
        root, source = instantiate(
            seat, seat["id"], seat["position_m"], seat["facing_target_m"],
            build_collection, asset_root, wood, wood_dark
        )
        record_source(root, source)

    retreat = plan["retreat"]
    for i in range(int(retreat["seat_count"])):
        root, source = instantiate(
            retreat, f"retreat-seat-{i + 1}", retreat["center_m"], plan["social_core"]["center_m"],
            build_collection, asset_root, wood, wood_dark
        )
        record_source(root, source)

    for item in plan.get("blockout_instances", []):
        root, source = instantiate(
            item, item["instance_id"], item["position_m"], None,
            build_collection, asset_root, wood, wood_dark, item.get("yaw_deg", 0)
        )
        root["blockout_role"] = item.get("role") or ""
        record_source(root, source)

    if not plan.get("blockout_instances"):
        anchor = plan["activity_anchor"]["position_m"]
        cube("ActivityAnchor_Platform", (anchor[0], anchor[1], 0.025), (0.7, 0.45, 0.05), accent_material)

    bpy.ops.object.light_add(type="AREA", location=(0, -0.8, 2.05))
    key = bpy.context.object
    key.name = "RenderOnly_Key"
    key.data.energy = 700
    key.data.shape = "DISK"
    key.data.size = 3.5
    key["render_only"] = True
    look_at(key, (0, 0, 0.5))
    bpy.ops.object.light_add(type="AREA", location=(2.7, 1.8, 1.8))
    fill = bpy.context.object
    fill.name = "RenderOnly_Fill"
    fill.data.energy = 350
    fill.data.size = 2.0
    fill["render_only"] = True
    look_at(fill, plan["retreat"]["center_m"])

    geometry = [obj for obj in scene.objects if obj.type in {"MESH", "EMPTY"} and not obj.name.startswith("RenderCamera_")]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in geometry:
        obj.select_set(True)
    glb_path = out_dir / "world.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True, export_yup=True)

    views = {
        "hero": (plan["hero_view"]["position_m"], plan["hero_view"]["target_m"]),
        "top": ([0, 0, max(plan["footprint"]["width"], plan["footprint"]["depth"]) * 1.05], [0, 0, 0]),
        "social-core": ([2.8, -2.8, 2.0], [*plan["social_core"]["center_m"][:2], 0.75]),
        "retreat": ([3.15, -0.4, 1.65], [*plan["retreat"]["center_m"][:2], 0.75]),
        "circulation": ([0, -3.35, 1.55], [0, 2.2, 0.75]),
    }
    render_paths = [render_view(name, pos, target, out_dir) for name, (pos, target) in views.items()]

    blend_path = out_dir / "world.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    manifest = {
        "schema_version": 2,
        "source_commit": source_commit(),
        "build_plan": str(plan_path),
        "build_plan_sha256": sha256(plan_path),
        "source_spec": plan.get("source_spec"),
        "source_spec_sha256": plan.get("source_spec_sha256"),
        "blender_version": bpy.app.version_string,
        "footprint": plan["footprint"],
        "social_core": plan["social_core"],
        "retreat": plan["retreat"],
        "activity_anchor": plan["activity_anchor"],
        "circulation_contract": plan["circulation_contract"],
        "blockout_instances": plan.get("blockout_instances", []),
        "asset_sources": asset_sources,
        "procedural_sources": procedural_sources,
        "runtime": {"realtime_lights": plan["runtime"]["realtime_lights"], "exported_render_only_lights": 0},
        "outputs": {
            "blend": {"path": blend_path.name, "sha256": sha256(blend_path)},
            "glb": {"path": glb_path.name, "sha256": sha256(glb_path)},
            "renders": [{"path": p.name, "sha256": sha256(p)} for p in render_paths],
        },
    }
    (out_dir / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({
        "status": "PASS",
        "output": str(out_dir),
        "blockout_instances": len(plan.get("blockout_instances", [])),
        "renders": [p.name for p in render_paths],
    }))


if __name__ == "__main__":
    main()
