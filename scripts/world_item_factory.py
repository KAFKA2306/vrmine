"""Deterministic Blender 4.2 generator for config/world-items/*.json.

Usage:
  blender -b --python-exit-code 1 --python scripts/world_item_factory.py -- config/world-items/<id>.json [variant]
"""
from __future__ import annotations

import copy
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
VIEW_OFFSETS = {
    "hero": Vector((2.7, -3.8, 2.35)), "front": Vector((0.0, -4.2, 0.57)),
    "rear": Vector((0.0, 4.2, 0.57)), "left": Vector((-4.2, 0.0, 0.57)),
    "right": Vector((4.2, 0.0, 0.57)), "top": Vector((0.0, -0.01, 5.0)),
}
FRAME_FILL = 0.84
PART_OVERRIDE_FIELDS = {"radius", "height", "size", "position", "rotation_deg", "vertices", "material"}
MATERIAL_OVERRIDE_FIELDS = {"base_color", "roughness", "metallic"}


def args():
    if "--" not in sys.argv:
        raise SystemExit("spec path is required after --")
    rest = sys.argv[sys.argv.index("--") + 1:]
    if len(rest) not in (1, 2):
        raise SystemExit("expected spec path and optional variant id")
    path = (ROOT / rest[0]).resolve()
    if ROOT not in path.parents or path.suffix != ".json":
        raise SystemExit("spec must be a repository JSON file")
    return path, rest[1] if len(rest) == 2 else None


def load_spec(path: Path) -> dict:
    spec = json.loads(path.read_text())
    required = {"id", "family", "display_name", "dimensions_m", "parts", "materials", "variants", "price_hypothesis", "license", "formats", "unity_status", "vrchat_status", "booth_status"}
    missing = sorted(required - spec.keys())
    if missing:
        raise ValueError(f"missing spec keys: {missing}")
    if not spec["id"] or "/" in spec["id"] or ".." in spec["id"]:
        raise ValueError("invalid id")
    if set(spec["formats"]) != {"blend", "glb", "fbx"}:
        raise ValueError("formats must be exactly blend/glb/fbx")
    return spec


def resolve_variant(base: dict, variant_id: str | None):
    if not variant_id:
        return copy.deepcopy(base), None
    variants = {variant["id"]: variant for variant in base["variants"]}
    if variant_id not in variants:
        raise ValueError(f"unknown variant: {variant_id}")
    variant = variants[variant_id]
    unknown = set(variant) - {"id", "part_overrides", "material_overrides"}
    if unknown:
        raise ValueError(f"variant {variant_id}: unsupported keys {sorted(unknown)}")
    resolved = copy.deepcopy(base)
    parts = {part["name"]: part for part in resolved["parts"]}
    for name, override in variant.get("part_overrides", {}).items():
        if name not in parts or set(override) - PART_OVERRIDE_FIELDS:
            raise ValueError(f"variant {variant_id}: invalid part override {name}")
        parts[name].update(override)
    for name, override in variant.get("material_overrides", {}).items():
        if name not in resolved["materials"] or set(override) - MATERIAL_OVERRIDE_FIELDS:
            raise ValueError(f"variant {variant_id}: invalid material override {name}")
        resolved["materials"][name].update(override)
    resolved["id"] = f'{base["id"]}--{variant_id}'
    resolved["base_id"] = base["id"]
    resolved["variant_id"] = variant_id
    return resolved, variant


def make_material(name, data):
    rgb = data["base_color"]
    if len(rgb) != 3: raise ValueError(f"material {name}: base_color must have 3 values")
    mat = bpy.data.materials.new(name); mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = float(data["roughness"])
    bsdf.inputs["Metallic"].default_value = float(data.get("metallic", 0.0))
    return mat


def finish(obj, mat, bevel=0.004):
    obj.data.materials.append(mat); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Edge bevel", "BEVEL"); mod.width = bevel; mod.segments = 3; bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons: poly.use_smooth = False
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.smart_project(island_margin=0.02); bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def make_part(part, materials):
    mat = materials[part["material"]]; position = tuple(float(v) for v in part.get("position", [0, 0, 0]))
    if part["component"] == "box":
        bpy.ops.mesh.primitive_cube_add(size=1, location=position); obj = bpy.context.object; obj.dimensions = tuple(float(v) for v in part["size"])
        obj.rotation_euler = [math.radians(float(v)) for v in part.get("rotation_deg", [0, 0, 0])]; finish(obj, mat, min(0.004, min(obj.dimensions) / 5))
    elif part["component"] == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(vertices=int(part.get("vertices", 48)), radius=float(part["radius"]), depth=float(part["height"]), location=position); obj = finish(bpy.context.object, mat)
    else: raise ValueError(f'unsupported component: {part["component"]}')
    obj.name = part["name"]; return obj


def join_parts(parts, sku):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join(); obj = bpy.context.object; obj.name = sku
    bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type="ORIGIN_CURSOR"); return obj


def world_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    if not points: raise ValueError("product mesh has no vertices")
    lo = Vector(tuple(min(p[i] for p in points) for i in range(3))); hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return lo, hi, (lo + hi) * .5, hi - lo, points


def setup_scene(dimensions, center):
    scene = bpy.context.scene; scene.unit_settings.system = "METRIC"; scene.unit_settings.scale_length = 1.0; scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = scene.render.resolution_y = 640; scene.render.resolution_percentage = 100; scene.render.image_settings.file_format = "PNG"; scene.render.film_transparent = False
    backdrop = make_material("Backdrop", {"base_color":[.70,.74,.73],"roughness":.9,"metallic":0}); bpy.ops.mesh.primitive_plane_add(size=max(dimensions)*4.2, location=(0,0,0)); bpy.context.object.data.materials.append(backdrop)
    bpy.ops.object.camera_add(location=center + VIEW_OFFSETS["hero"]); camera=bpy.context.object; camera.data.type="ORTHO"; camera.data.ortho_scale=1.; scene.camera=camera
    for location, energy, size in [((2.5,-3.5,4.),650,3.5),((-3.,-1.,2.2),260,2.8)]:
        bpy.ops.object.light_add(type="AREA", location=location); light=bpy.context.object; light.data.energy=energy; light.data.shape="DISK"; light.data.size=size; light.rotation_euler=(center-light.location).to_track_quat("-Z","Y").to_euler()
    world=bpy.data.worlds.new("World item studio"); world.use_nodes=True; world.node_tree.nodes["Background"].inputs[0].default_value=(.12,.14,.15,1); world.node_tree.nodes["Background"].inputs[1].default_value=.55; scene.world=world
    return scene,camera


def render_views(scene,camera,out,center,points):
    aspect=scene.render.resolution_x/scene.render.resolution_y; framing={}
    for name,offset in VIEW_OFFSETS.items():
        camera.location=center+offset; camera.rotation_euler=(center-camera.location).to_track_quat("-Z","Y").to_euler(); bpy.context.view_layer.update(); inv=camera.matrix_world.inverted(); projected=[inv@p for p in points]
        min_x,max_x=min(p.x for p in projected),max(p.x for p in projected); min_y,max_y=min(p.y for p in projected),max(p.y for p in projected); basis=camera.matrix_world.to_3x3(); camera.location += basis@Vector(((min_x+max_x)*.5,(min_y+max_y)*.5,0)); bpy.context.view_layer.update(); inv=camera.matrix_world.inverted(); projected=[inv@p for p in points]
        min_x,max_x=min(p.x for p in projected),max(p.x for p in projected); min_y,max_y=min(p.y for p in projected),max(p.y for p in projected); w,h=max_x-min_x,max_y-min_y
        if w<=0 or h<=0: raise ValueError(f"invalid projected bounds for {name}: {w} x {h}")
        camera.data.ortho_scale=max(h,w/aspect)/FRAME_FILL; fh=camera.data.ortho_scale; fw=fh*aspect; bounds=[min_x/fw,max_x/fw,min_y/fh,max_y/fh]
        framing[name]={"center_error_x":round((bounds[0]+bounds[1])*.5,10),"center_error_y":round((bounds[2]+bounds[3])*.5,10),"fill_ratio":round(max(bounds[1]-bounds[0],bounds[3]-bounds[2]),10),"normalized_bounds":[round(v,10) for v in bounds],"ortho_scale":round(float(camera.data.ortho_scale),10)}
        scene.render.filepath=str(out/f"view-{name}.png"); bpy.ops.render.render(write_still=True)
    (out/"thumbnail.png").write_bytes((out/"view-hero.png").read_bytes()); return framing


def sha256(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def digest(data): return hashlib.sha256(json.dumps(data, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def main():
    if bpy.app.version[:2] != (4,2): raise RuntimeError("Blender 4.2 is required")
    spec_path, variant_id = args(); base=load_spec(spec_path); spec, variant=resolve_variant(base, variant_id); sku=spec["id"]; out=ROOT/".artifacts"/"world-items"/sku; out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.preferences.filepaths.save_version=0; materials={n:make_material(n,d) for n,d in spec["materials"].items()}; parts=[make_part(p,materials) for p in spec["parts"]]; product=join_parts(parts,sku)
    bpy.ops.object.select_all(action="DESELECT"); product.select_set(True); bpy.context.view_layer.objects.active=product; bpy.ops.export_scene.gltf(filepath=str(out/f"{sku}.glb"),export_format="GLB",use_selection=True); bpy.ops.export_scene.fbx(filepath=str(out/f"{sku}.fbx"),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",bake_anim=False)
    product.data.calc_loop_triangles(); lo,hi,center,size,points=world_bounds(product); dimensions=[float(v) for v in size]; scene,camera=setup_scene(dimensions,center); bpy.ops.wm.save_as_mainfile(filepath=str(out/f"{sku}.blend")); framing=render_views(scene,camera,out,center,points)
    expected=[f"{sku}.{ext}" for ext in ("blend","glb","fbx")]+["thumbnail.png"]+[f"view-{n}.png" for n in VIEW_OFFSETS]
    manifest={"schema_version":3,"id":sku,"base_id":base["id"],"variant_id":variant_id,"source_spec":spec_path.relative_to(ROOT).as_posix(),"spec_sha256":sha256(spec_path),"resolved_spec_sha256":digest(spec),"variant_sha256":digest(variant) if variant else None,"blender":bpy.app.version_string,"units":"metres","dimensions_m_actual":dimensions,"bounds_min_m":[float(v) for v in lo],"bounds_max_m":[float(v) for v in hi],"bounds_center_m":[float(v) for v in center],"render_framing":framing,"triangles":len(product.data.loop_triangles),"parts_count":len(spec["parts"]),"formats":spec["formats"],"unity_status":spec["unity_status"],"vrchat_status":spec["vrchat_status"],"booth_status":spec["booth_status"],"sha256":{n:sha256(out/n) for n in expected}}
    (out/"resolved-spec.json").write_text(json.dumps(spec,indent=2)+"\n"); (out/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")

if __name__ == "__main__": main()
