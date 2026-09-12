import bpy
import hashlib
import json
import math
import sys
from pathlib import Path
from mathutils import Vector


def args_after_double_dash():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def fail(message):
    raise RuntimeError(f"World Build Verification FAIL: {message}")


def close(a, b, tol=1e-4):
    return abs(float(a) - float(b)) <= tol


def angle_close_deg(actual_radians, expected_deg, tol=1e-3):
    actual = math.degrees(float(actual_radians)) % 360
    expected = float(expected_deg) % 360
    delta = abs((actual - expected + 180) % 360 - 180)
    return delta <= tol


def vec_close(a, b, tol=1e-4):
    return len(a) == len(b) and all(close(x, y, tol) for x, y in zip(a, b))


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def require_object(name):
    obj = bpy.data.objects.get(name)
    if obj is None:
        fail(f"missing scene object {name}")
    return obj


def expect_identity(obj, record, label):
    if obj.get("asset_id") != record["asset_id"]:
        fail(f"{label} asset identity mismatch")
    if obj.get("source_kind") != record.get("source_kind", "glb"):
        fail(f"{label} source kind mismatch")


def verify_hero_camera(plan):
    camera = require_object("RenderCamera_hero")
    expected_pos = plan["hero_view"]["position_m"]
    expected_target = plan["hero_view"]["target_m"]
    if not vec_close(camera.location, expected_pos):
        fail("hero camera position mismatch")
    try:
        stored_target = json.loads(camera.get("target_m_json", "null"))
        stored_source = json.loads(camera.get("source_position_m_json", "null"))
    except Exception as exc:
        fail(f"hero camera provenance is invalid: {exc}")
    if not vec_close(stored_target or [], expected_target):
        fail("hero camera target provenance mismatch")
    if not vec_close(stored_source or [], expected_pos):
        fail("hero camera position provenance mismatch")
    desired = (Vector(expected_target) - Vector(expected_pos)).normalized()
    actual = camera.matrix_world.to_quaternion() @ Vector((0, 0, -1))
    if actual.normalized().dot(desired) < 0.999:
        fail("hero camera orientation does not resolve target")


def assert_practical_token_contract(expected_tokens, actual_tokens):
    if set(actual_tokens) != set(expected_tokens):
        fail(f"practical light token mismatch: expected {sorted(set(expected_tokens))}, got {sorted(set(actual_tokens))}")


def verify_visual_layers(plan, manifest):
    visual = plan.get("visual_layers")
    if not visual:
        return {"practical_tokens": 0, "life_traces": 0, "natural_layers": 0}

    verify_hero_camera(plan)

    expected_tokens = set(visual.get("practical_lights") or [])
    scene_lights = [obj for obj in bpy.data.objects if obj.type == "LIGHT" and obj.get("practical_light_token")]
    actual_tokens = {obj.get("practical_light_token") for obj in scene_lights}
    assert_practical_token_contract(expected_tokens, actual_tokens)
    expected_counts = {}
    for binding in visual.get("practical_light_bindings") or []:
        token = binding["token"]
        expected_counts[token] = expected_counts.get(token, 0) + 1
    actual_counts = {}
    for obj in scene_lights:
        token = obj.get("practical_light_token")
        actual_counts[token] = actual_counts.get(token, 0) + 1
    if actual_counts != expected_counts:
        fail(f"practical light binding count mismatch: expected {expected_counts}, got {actual_counts}")

    expected_palette = set((visual.get("palette") or {}).keys())
    palette_materials = {material.get("palette_key"): material for material in bpy.data.materials if material.get("palette_key")}
    if set(palette_materials) != expected_palette:
        fail(f"palette provenance mismatch: expected {sorted(expected_palette)}, got {sorted(palette_materials)}")
    avoid = set((visual.get("material_palette") or {}).get("avoid") or [])
    forbidden = [material.name for material in bpy.data.materials if material.get("material_classification") in avoid]
    if forbidden:
        fail(f"forbidden generated material classification: {forbidden}")
    manifest_palette = (manifest.get("visual_layers") or {}).get("palette_materials") or {}
    if set(manifest_palette) != expected_palette:
        fail("manifest palette provenance mismatch")

    canonical_life = set(visual.get("life_trace_ids") or [])
    expected_life = {entry["id"] for entry in visual.get("life_trace_bindings") or []}
    life_objects = [obj for obj in bpy.data.objects if obj.get("life_trace_id")]
    actual_life = {obj.get("life_trace_id") for obj in life_objects}
    if not actual_life.issubset(canonical_life):
        fail("scene contains non-canonical life trace id")
    if actual_life != expected_life or len(actual_life) < 3:
        fail(f"life trace contract mismatch: expected {sorted(expected_life)}, got {sorted(actual_life)}")
    if set((manifest.get("visual_layers") or {}).get("life_trace_ids") or []) != actual_life:
        fail("manifest life trace IDs mismatch")

    allowed_natural = set(visual.get("natural_layer_kinds") or [])
    expected_natural = [entry["kind"] for entry in visual.get("natural_layers") or []]
    natural_objects = [obj for obj in bpy.data.objects if obj.get("natural_layer_kind")]
    actual_natural = [obj.get("natural_layer_kind") for obj in natural_objects]
    if len(actual_natural) < 3:
        fail("fewer than three natural layer instances materialized")
    if any(kind not in allowed_natural for kind in actual_natural):
        fail("scene contains disallowed natural layer kind")
    if sorted(actual_natural) != sorted(expected_natural):
        fail(f"natural layer contract mismatch: expected {sorted(expected_natural)}, got {sorted(actual_natural)}")
    if sorted((manifest.get("visual_layers") or {}).get("natural_layer_kinds") or []) != sorted(actual_natural):
        fail("manifest natural layer kinds mismatch")

    depth = visual.get("atmospheric_depth") or {}
    manifest_depth = (manifest.get("visual_layers") or {}).get("atmospheric_depth") or {}
    if depth.get("enabled") is not True or manifest_depth.get("enabled") is not True:
        fail("atmospheric depth mechanism must be enabled")
    if depth.get("mechanism") != "world_volume_haze" or manifest_depth.get("mechanism") != "world_volume_haze":
        fail("atmospheric depth mechanism mismatch")
    world = bpy.context.scene.world
    if world is None or not world.use_nodes:
        fail("world nodes missing for atmospheric depth")
    volume = world.node_tree.nodes.get("WorldBlockoutAtmosphericDepth")
    if volume is None:
        fail("atmospheric depth volume node missing")
    if not close(volume.inputs["Density"].default_value, depth["density"], 1e-6):
        fail("atmospheric depth density mismatch")

    return {"practical_tokens": len(actual_tokens), "life_traces": len(actual_life), "natural_layers": len(actual_natural)}


def main():
    argv = args_after_double_dash()
    if len(argv) < 2:
        fail("usage: blender -b --python scripts/verify_world_build.py -- <build-plan.json> <output-dir>")
    plan_path = Path(argv[0])
    out_dir = Path(argv[1])
    plan = json.loads(plan_path.read_text())
    if plan.get("schema_version") != 4:
        fail("build plan schema_version must be 4")

    manifest_path = out_dir / "manifest.json"
    blend_path = out_dir / "world.blend"
    glb_path = out_dir / "world.glb"
    for path in [manifest_path, blend_path, glb_path]:
        if not path.is_file() or path.stat().st_size == 0:
            fail(f"missing output {path}")
    manifest = json.loads(manifest_path.read_text())
    if manifest.get("build_plan_sha256") != sha256(plan_path):
        fail("manifest build plan digest mismatch")
    if manifest.get("source_spec_sha256") != plan.get("source_spec_sha256"):
        fail("manifest source spec digest mismatch")
    if glb_path.read_bytes()[:4] != b"glTF":
        fail("world.glb has invalid header")
    if blend_path.read_bytes()[:7] != b"BLENDER":
        fail("world.blend has invalid header")

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    floor = require_object("WorldShell_Floor")
    ceiling = require_object("WorldShell_Ceiling")
    left = require_object("WorldShell_Left")
    right = require_object("WorldShell_Right")
    front = require_object("WorldShell_Front")
    w = float(plan["footprint"]["width"])
    d = float(plan["footprint"]["depth"])
    h = float(plan["footprint"]["height"])
    if not close(floor.dimensions.x, w) or not close(floor.dimensions.y, d):
        fail("floor dimensions do not match plan footprint")
    if not close(ceiling.location.z, h + 0.04):
        fail("ceiling height does not match plan")
    if not close(abs(left.location.x), w / 2 + 0.04) or not close(abs(right.location.x), w / 2 + 0.04):
        fail("side walls do not match plan width")
    if not close(abs(front.location.y), d / 2 + 0.04):
        fail("front wall does not match plan depth")

    opening = plan.get("view_opening")
    if opening:
        for name in ["WorldShell_Back_Left", "WorldShell_Back_Right", "WorldShell_Back_Header", "ActivityAnchor_Frame_Left", "ActivityAnchor_Frame_Right"]:
            require_object(name)
    else:
        require_object("WorldShell_Back")

    table_record = plan["social_core"]["table"]
    table = require_object("SocialTable")
    expect_identity(table, table_record, "social table")
    if not vec_close(table.location, table_record["position_m"]):
        fail("social table position mismatch")

    seat_positions = []
    for seat in plan["social_core"]["seats"]:
        obj = require_object(seat["id"])
        expect_identity(obj, seat, seat["id"])
        if not vec_close(obj.location, seat["position_m"]):
            fail(f"{seat['id']} position mismatch")
        seat_positions.append(Vector(obj.location))
    max_face_distance = float(plan["social_core"]["max_face_distance"])
    for i, a in enumerate(seat_positions):
        for b in seat_positions[i + 1:]:
            if (a - b).length > max_face_distance + 1e-4:
                fail("social seat pair exceeds max face distance")

    retreat = plan["retreat"]
    for i in range(int(retreat["seat_count"])):
        obj = require_object(f"retreat-seat-{i + 1}")
        expect_identity(obj, retreat, "retreat seat")
        if not vec_close(obj.location, retreat["center_m"]):
            fail("retreat seat position mismatch")

    blockout = plan.get("blockout_instances", [])
    for item in blockout:
        obj = require_object(item["instance_id"])
        expect_identity(obj, item, item["instance_id"])
        if not vec_close(obj.location, item["position_m"]):
            fail(f"{item['instance_id']} position mismatch")
        if not angle_close_deg(obj.rotation_euler.z, item.get("yaw_deg", 0)):
            fail(f"{item['instance_id']} yaw mismatch")
        if obj.get("blockout_role", "") != (item.get("role") or ""):
            fail(f"{item['instance_id']} role mismatch")

    waypoints = [Vector(p) for p in plan["circulation_contract"]["waypoints_m"]]
    half_w, half_d = w / 2, d / 2
    clearance = float(plan["circulation_contract"]["minimum_clearance"])
    for i, p in enumerate(waypoints):
        if half_w - abs(p.x) < clearance / 2 - 1e-4 or half_d - abs(p.y) < clearance / 2 - 1e-4:
            fail(f"circulation waypoint {i} violates wall clearance")
    anchor = Vector(plan["activity_anchor"]["position_m"])
    anchor_to_social = math.hypot(anchor.x - float(plan["social_core"]["center_m"][0]), anchor.y - float(plan["social_core"]["center_m"][1]))
    social_radius = float(plan["social_core"]["diameter"]) / 2
    path_distance = min(math.hypot(p.x - anchor.x, p.y - anchor.y) for p in waypoints)
    approach_distance = max(0.0, path_distance - social_radius) if anchor_to_social <= social_radius + 1e-4 else path_distance
    if approach_distance > float(plan["activity_anchor"]["approach_clearance_m"]) + 1e-4:
        fail("activity anchor is not reachable from circulation path")
    retreat_center = Vector(plan["retreat"]["center_m"])
    if min((p - retreat_center).length for p in waypoints) > clearance + 1e-4:
        fail("retreat is not reachable from circulation path")

    if int(plan["runtime"]["realtime_lights"]) != 0:
        fail("world runtime contract requires zero realtime lights")
    if int(manifest.get("runtime", {}).get("exported_render_only_lights", -1)) != 0:
        fail("render-only lights leaked into runtime export")

    expected_renders = {"view-hero.png", "view-top.png", "view-social-core.png", "view-retreat.png", "view-circulation.png"}
    actual_renders = {entry["path"] for entry in manifest.get("outputs", {}).get("renders", [])}
    if actual_renders != expected_renders:
        fail(f"render set mismatch: {sorted(actual_renders)}")
    for name in expected_renders:
        path = out_dir / name
        if not path.is_file() or path.stat().st_size < 1024:
            fail(f"missing or empty render {name}")
        try:
            image = bpy.data.images.load(str(path), check_existing=False)
            if tuple(int(v) for v in image.size) != (960, 540):
                fail(f"render {name} has wrong dimensions {tuple(image.size)}")
            bpy.data.images.remove(image)
        except Exception as exc:
            fail(f"render {name} cannot be decoded: {exc}")

    for asset_id, record in manifest.get("asset_sources", {}).items():
        source = Path(record["path"])
        if not source.is_file():
            fail(f"source asset disappeared: {asset_id}")
        if sha256(source) != record["sha256"]:
            fail(f"source asset hash mismatch: {asset_id}")

    expected_procedural = {}
    procedural_records = [table_record, *plan["social_core"]["seats"], retreat]
    for record in procedural_records:
        if record.get("source_kind") == "procedural_blockout":
            expected_procedural[record["asset_id"]] = record["geometry"]
    if manifest.get("procedural_sources", {}) != expected_procedural:
        fail("procedural source contract mismatch")
    if sorted(manifest.get("asset_sources", {}).keys()) != sorted(plan.get("asset_ids", [])):
        fail("materialized GLB source set mismatch")
    if manifest.get("blockout_instances", []) != blockout:
        fail("manifest blockout instance contract mismatch")

    visual_summary = verify_visual_layers(plan, manifest)
    negative_fixture = None
    if plan.get("visual_layers"):
        actual_tokens = {obj.get("practical_light_token") for obj in bpy.data.objects if obj.type == "LIGHT" and obj.get("practical_light_token")}
        broken_expected = set(plan["visual_layers"].get("practical_lights") or []) | {"__broken_practical_light_token__"}
        try:
            assert_practical_token_contract(broken_expected, actual_tokens)
            fail("broken practical-light negative fixture unexpectedly passed")
        except RuntimeError as exc:
            if "practical light token mismatch" not in str(exc):
                raise
        negative_fixture = "practical-light-token"

    if manifest["outputs"]["blend"]["sha256"] != sha256(blend_path):
        fail("blend hash mismatch")
    if manifest["outputs"]["glb"]["sha256"] != sha256(glb_path):
        fail("glb hash mismatch")

    print(json.dumps({
        "status": "PASS",
        "room_m": [w, d, h],
        "social_seats": len(seat_positions),
        "blockout_instances": len(blockout),
        "practical_light_tokens": visual_summary["practical_tokens"],
        "life_traces": visual_summary["life_traces"],
        "natural_layers": visual_summary["natural_layers"],
        "negative_fixture": negative_fixture,
        "renders": sorted(expected_renders),
    }))


if __name__ == "__main__":
    main()
