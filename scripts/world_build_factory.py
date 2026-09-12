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
    if direction.length == 0:
        fail(f"cannot aim {obj.name} at its own location")
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def hex_rgba(value):
    if not isinstance(value, str) or len(value) != 7 or not value.startswith("#"):
        fail(f"invalid #RRGGBB color {value!r}")
    try:
        rgb = tuple(int(value[i:i + 2], 16) / 255.0 for i in (1, 3, 5))
    except ValueError as exc:
        raise RuntimeError(f"World Build Factory FAIL: invalid #RRGGBB color {value!r}") from exc
    return (*rgb, 1.0)


def make_material(name, rgba, roughness=0.6, emission=None, palette_key=None, classification=None):
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
            bsdf.inputs["Emission Strength"].default_value = 2.0
    if palette_key:
        material["palette_key"] = palette_key
    if classification:
        material["material_classification"] = classification
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


def uv_sphere(name, location, radius, material, segments=20, ring_count=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=ring_count, radius=radius, location=location)
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
                created.append(cube(f"{instance_id}_Leg_{sx}_{sy}", (sx * inset_x, sy * inset_y, leg_h / 2), (leg_w, leg_w, leg_h), wood_dark))
    elif kind == "bench":
        width = float(geometry["width_m"])
        depth = float(geometry["depth_m"])
        height = float(geometry["height_m"])
        seat_y = height * 0.55
        seat_t = 0.07
        created.append(cube(f"{instance_id}_Seat", (0, 0, seat_y), (width, depth, seat_t), wood))
        back_h = height - seat_y + 0.04
        created.append(cube(f"{instance_id}_Back", (0, depth / 2 - 0.035, seat_y + back_h / 2 - 0.02), (width, 0.07, back_h), wood))
        leg_h = seat_y - seat_t / 2
        for sx in (-1, 1):
            created.append(cube(f"{instance_id}_Leg_{sx}", (sx * width * 0.36, 0, leg_h / 2), (0.08, depth * 0.72, leg_h), wood_dark))
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
        return import_glb_instance(record["asset_id"], instance_id, position, facing_target, collection, asset_root, yaw_deg)
    if kind == "procedural_blockout":
        root = procedural_instance(record["asset_id"], instance_id, record["geometry"], position, facing_target, collection, wood, wood_dark)
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


def configure_atmospheric_depth(scene, config, palette):
    if not config or not config.get("enabled"):
        return {"enabled": False}
    if config.get("mechanism") != "world_volume_haze":
        fail(f"unsupported atmospheric depth mechanism {config.get('mechanism')!r}")
    density = float(config["density"])
    if density <= 0 or density > 0.2:
        fail("atmospheric depth density must be in (0,0.2]")
    key = config["color_palette_key"]
    if key not in palette:
        fail(f"atmospheric depth references missing palette key {key}")
    world = scene.world
    world.use_nodes = True
    nodes = world.node_tree.nodes
    links = world.node_tree.links
    output = nodes.get("World Output")
    if output is None:
        fail("World Output node is unavailable")
    background = nodes.get("Background")
    if background is not None:
        bg = hex_rgba(palette.get("forest_green", "#253025"))
        background.inputs["Color"].default_value = (bg[0] * 0.10, bg[1] * 0.10, bg[2] * 0.10, 1.0)
        background.inputs["Strength"].default_value = 0.28
    volume = nodes.new("ShaderNodeVolumePrincipled")
    volume.name = "WorldBlockoutAtmosphericDepth"
    volume.inputs["Density"].default_value = density
    volume.inputs["Color"].default_value = hex_rgba(palette[key])
    links.new(volume.outputs["Volume"], output.inputs["Volume"])
    return {"enabled": True, "mechanism": "world_volume_haze", "density": density, "color_palette_key": key}


def create_practical_lights(plan, palette, material_by_key):
    visual = plan.get("visual_layers") or {}
    canonical_tokens = set(visual.get("practical_lights") or [])
    bindings = visual.get("practical_light_bindings") or []
    resolved = set()
    counts = {}
    for index, binding in enumerate(bindings):
        token = binding.get("token")
        if token not in canonical_tokens:
            fail(f"practical light binding references non-canonical token {token!r}")
        position = binding.get("position_m")
        if not isinstance(position, list) or len(position) != 3:
            fail(f"practical light {token} position_m must be a 3-vector")
        bpy.ops.object.light_add(type="POINT", location=position)
        light = bpy.context.object
        light.name = f"PracticalLight_{index + 1}_{token}"
        light.data.energy = float(binding.get("energy_w", 10))
        light.data.shadow_soft_size = float(binding.get("radius_m", 0.2))
        light.data.color = hex_rgba(palette["lamp_gold"])[:3]
        light["practical_light_token"] = token
        light["render_only"] = True
        resolved.add(token)
        counts[token] = counts.get(token, 0) + 1
        globe = uv_sphere(f"PracticalGlow_{index + 1}_{token}", position, 0.025, material_by_key["lamp_gold"])
        globe["practical_light_token"] = token
    missing = sorted(canonical_tokens - resolved)
    if missing:
        fail(f"unresolved practical light token(s): {', '.join(missing)}")
    return counts


def create_life_traces(plan, material_by_key):
    visual = plan.get("visual_layers") or {}
    canonical = set(visual.get("life_trace_ids") or [])
    bindings = visual.get("life_trace_bindings") or []
    created_ids = []
    for index, binding in enumerate(bindings):
        trace_id = binding.get("id")
        if trace_id not in canonical:
            fail(f"life trace binding references non-canonical id {trace_id!r}")
        position = binding["position_m"]
        material = material_by_key[binding["palette_key"]]
        kind = binding.get("kind")
        root = bpy.data.objects.new(f"LifeTrace_{index + 1}_{trace_id}", None)
        bpy.context.scene.collection.objects.link(root)
        root.location = position
        root["life_trace_id"] = trace_id
        root["material_classification"] = binding["classification"]
        if kind == "book":
            page = cube(f"{root.name}_Pages", (0, 0, 0), (0.12, 0.085, 0.012), material)
            cover = cube(f"{root.name}_Cover", (0, 0, 0.009), (0.124, 0.089, 0.006), material)
            page.parent = root
            cover.parent = root
        elif kind == "mug":
            mug = cylinder(f"{root.name}_Cup", (0, 0, 0.03), 0.025, 0.06, material, 24)
            mug.parent = root
        elif kind == "firewood":
            for j, dx in enumerate((-0.055, 0.0, 0.055)):
                log = cylinder(f"{root.name}_Log{j + 1}", (dx, 0, 0.025), 0.022, 0.13, material, 16)
                log.rotation_euler[1] = math.radians(90)
                log.parent = root
        else:
            fail(f"unsupported life trace kind {kind!r}")
        created_ids.append(trace_id)
    if len(set(created_ids)) < 3:
        fail("at least three distinct life traces must be materialized")
    return created_ids


def create_natural_layers(plan, material_by_key):
    visual = plan.get("visual_layers") or {}
    allowed = set(visual.get("natural_layer_kinds") or [])
    layers = visual.get("natural_layers") or []
    created = []
    for index, layer in enumerate(layers):
        kind = layer.get("kind")
        if kind not in allowed:
            fail(f"natural layer kind is not allowed: {kind!r}")
        position = layer["position_m"]
        scale = layer["scale_m"]
        material = material_by_key[layer["palette_key"]]
        if kind == "moss":
            obj = cylinder(f"NaturalLayer_{index + 1}_moss", position, max(scale[0], scale[1]) / 2, scale[2], material, 32)
            obj.scale.y = min(scale[0], scale[1]) / max(scale[0], scale[1])
        elif kind == "low_vegetation":
            obj = cylinder(f"NaturalLayer_{index + 1}_low_vegetation", position, max(scale[0], scale[1]) / 2, scale[2], material, 12)
        elif kind == "leaf_scatter":
            obj = cube(f"NaturalLayer_{index + 1}_leaf_scatter", position, scale, material)
            obj.rotation_euler[2] = math.radians(18)
        else:
            fail(f"unsupported natural layer kind {kind!r}")
        obj["natural_layer_kind"] = kind
        obj["material_classification"] = layer["classification"]
        created.append(kind)
    if len(created) < 3:
        fail("at least three natural layer instances are required")
    return created


def render_view(name, position, target, out_dir, clip_start, clip_end, resolution=(960, 540)):
    bpy.ops.object.camera_add(location=position)
    camera = bpy.context.object
    camera.name = f"RenderCamera_{name}"
    look_at(camera, target)
    camera.data.lens = 34
    camera.data.clip_start = float(clip_start)
    camera.data.clip_end = float(clip_end)
    camera["target_m_json"] = json.dumps(target)
    camera["source_position_m_json"] = json.dumps(position)
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

    visual = plan.get("visual_layers")
    palette = (visual or {}).get("palette") or {}
    avoid = set(((visual or {}).get("material_palette") or {}).get("avoid") or [])

    build_collection = bpy.data.collections.new("WorldBuildInstances")
    scene.collection.children.link(build_collection)
    shell_material = make_material("WorldShell", (0.18, 0.16, 0.14, 1), 0.82, classification="wood")
    accent_material = make_material("ActivityAnchor", (0.08, 0.2, 0.28, 1), 0.35, (0.08, 0.28, 0.42, 1), classification="aged_brass")
    if visual:
        material_by_key = {
            "wood_brown": make_material("Palette_wood_brown", hex_rgba(palette["wood_brown"]), 0.78, palette_key="wood_brown", classification="wood"),
            "forest_green": make_material("Palette_forest_green", hex_rgba(palette["forest_green"]), 0.90, palette_key="forest_green", classification="moss"),
            "warm_cream": make_material("Palette_warm_cream", hex_rgba(palette["warm_cream"]), 0.84, palette_key="warm_cream", classification="paper"),
            "accent_terracotta": make_material("Palette_accent_terracotta", hex_rgba(palette["accent_terracotta"]), 0.88, palette_key="accent_terracotta", classification="paper"),
            "lamp_gold": make_material("Palette_lamp_gold", hex_rgba(palette["lamp_gold"]), 0.42, emission=hex_rgba(palette["lamp_gold"]), palette_key="lamp_gold", classification="aged_brass"),
        }
        wood = material_by_key["wood_brown"]
        wood_dark = make_material("ProceduralWoodDark", (0.16, 0.085, 0.045, 1), 0.82, classification="wood")
    else:
        material_by_key = {}
        wood = make_material("ProceduralWood", (0.34, 0.20, 0.10, 1), 0.72, classification="wood")
        wood_dark = make_material("ProceduralWoodDark", (0.16, 0.085, 0.045, 1), 0.82, classification="wood")
    if any(material.get("material_classification") in avoid for material in bpy.data.materials):
        fail("generated material uses a forbidden material classification")

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
    root, source = instantiate(table, "SocialTable", table["position_m"], plan["social_core"]["center_m"], build_collection, asset_root, wood, wood_dark)
    record_source(root, source)
    for seat in plan["social_core"]["seats"]:
        root, source = instantiate(seat, seat["id"], seat["position_m"], seat["face_target_m"], build_collection, asset_root, wood, wood_dark)
        record_source(root, source)
    retreat = plan["retreat"]
    for i in range(int(retreat["seat_count"])):
        root, source = instantiate(retreat, f"retreat-seat-{i + 1}", retreat["center_m"], plan["social_core"]["center_m"], build_collection, asset_root, wood, wood_dark)
        record_source(root, source)
    for item in plan.get("blockout_instances", []):
        root, source = instantiate(item, item["instance_id"], item["position_m"], None, build_collection, asset_root, wood, wood_dark, item.get("yaw_deg", 0))
        root["blockout_role"] = item.get("role") or ""
        record_source(root, source)
    if not plan.get("blockout_instances"):
        anchor = plan["activity_anchor"]["position_m"]
        cube("ActivityAnchor_Platform", (anchor[0], anchor[1], 0.025), (0.7, 0.45, 0.05), accent_material)

    life_trace_ids = []
    natural_layer_kinds = []
    practical_counts = {}
    atmospheric_depth = {"enabled": False}
    if visual:
        life_trace_ids = create_life_traces(plan, material_by_key)
        natural_layer_kinds = create_natural_layers(plan, material_by_key)
        practical_counts = create_practical_lights(plan, palette, material_by_key)
        atmospheric_depth = configure_atmospheric_depth(scene, visual["atmospheric_depth"], palette)

    bpy.ops.object.light_add(type="AREA", location=(0, -0.8, 2.05))
    key = bpy.context.object
    key.name = "RenderOnly_Key"
    key.data.energy = 520
    key.data.shape = "DISK"
    key.data.size = 3.5
    key["render_only"] = True
    look_at(key, (0, 0, 0.5))
    bpy.ops.object.light_add(type="AREA", location=(2.7, 1.8, 1.8))
    fill = bpy.context.object
    fill.name = "RenderOnly_Fill"
    fill.data.energy = 220
    fill.data.size = 2.0
    fill["render_only"] = True
    look_at(fill, plan["retreat"]["center_m"])

    geometry = [obj for obj in scene.objects if obj.type in {"MESH", "EMPTY"} and not obj.name.startswith("RenderCamera_")]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in geometry:
        obj.select_set(True)
    glb_path = out_dir / "world.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True, export_yup=True)

    depth = (visual or {}).get("atmospheric_depth") or {}
    clip_start = depth.get("camera_clip_start_m", 0.01)
    clip_end = depth.get("camera_clip_end_m", 100)
    views = {
        "hero": (plan["hero_view"]["position_m"], plan["hero_view"]["target_m"]),
        "top": ([0, 0, max(plan["footprint"]["width"], plan["footprint"]["depth"]) * 1.05], [0, 0, 0]),
        "social-core": ([2.8, -2.8, 2.0], [*plan["social_core"]["center_m"][:2], 0.75]),
        "retreat": ([3.15, -0.4, 1.65], [*plan["retreat"]["center_m"][:2], 0.75]),
        "circulation": ([0, -3.35, 1.55], [0, 2.2, 0.75]),
    }
    render_paths = [render_view(name, pos, target, out_dir, clip_start, clip_end) for name, (pos, target) in views.items()]
    blend_path = out_dir / "world.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

    palette_materials = {}
    for material in bpy.data.materials:
        key_name = material.get("palette_key")
        if key_name:
            palette_materials[key_name] = {"material": material.name, "classification": material.get("material_classification", "")}
    manifest = {
        "schema_version": 3 if visual else 2,
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
        "hero_view": plan["hero_view"],
        "circulation_contract": plan["circulation_contract"],
        "blockout_instances": plan.get("blockout_instances", []),
        "asset_sources": asset_sources,
        "procedural_sources": procedural_sources,
        "visual_layers": {
            "practical_light_counts": practical_counts,
            "palette_materials": palette_materials,
            "life_trace_ids": life_trace_ids,
            "natural_layer_kinds": natural_layer_kinds,
            "atmospheric_depth": atmospheric_depth,
        } if visual else {},
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
        "life_traces": len(set(life_trace_ids)),
        "natural_layers": len(natural_layer_kinds),
        "practical_light_tokens": sorted(practical_counts),
        "renders": [p.name for p in render_paths],
    }))


if __name__ == "__main__":
    main()
