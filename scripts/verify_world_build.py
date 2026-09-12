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
        require_object("WorldShell_Back_Left")
        require_object("WorldShell_Back_Right")
        require_object("WorldShell_Back_Header")
        require_object("ActivityAnchor_Frame_Left")
        require_object("ActivityAnchor_Frame_Right")
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
    anchor_to_social = math.hypot(
        anchor.x - float(plan["social_core"]["center_m"][0]),
        anchor.y - float(plan["social_core"]["center_m"][1]),
    )
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
    exported_render_only = int(manifest.get("runtime", {}).get("exported_render_only_lights", -1))
    if exported_render_only != 0:
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

    expected_glb_ids = sorted(plan.get("asset_ids", []))
    if sorted(manifest.get("asset_sources", {}).keys()) != expected_glb_ids:
        fail("materialized GLB source set mismatch")

    if manifest.get("blockout_instances", []) != blockout:
        fail("manifest blockout instance contract mismatch")
    if manifest["outputs"]["blend"]["sha256"] != sha256(blend_path):
        fail("blend hash mismatch")
    if manifest["outputs"]["glb"]["sha256"] != sha256(glb_path):
        fail("glb hash mismatch")

    print(json.dumps({
        "status": "PASS",
        "room_m": [w, d, h],
        "social_seats": len(seat_positions),
        "blockout_instances": len(blockout),
        "renders": sorted(expected_renders),
    }))


if __name__ == "__main__":
    main()
