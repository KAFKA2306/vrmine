"""Build the reproducible Astra Pilot B rigged asset in Blender."""
from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(sys.argv[-1]).resolve() if "--" in sys.argv else ROOT / ".artifacts" / "astra-pilot-b"
OUT.mkdir(parents=True, exist_ok=True)
SEED = 40102
RENDER_VIEWS = {
    "front_3_4": (4, -6, 3),
    "rear_3_4": (-4, 6, 3),
    "left_side": (-6, 0, 2),
    "right_side": (6, 0, 2),
    "top_overview": (0, 0, 8),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def point_camera(camera: bpy.types.Object, target: tuple[float, float, float]) -> None:
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_view(scene: bpy.types.Scene, camera: bpy.types.Object, name: str, location: tuple[float, float, float]) -> None:
    camera.location = location
    point_camera(camera, (0, 0, 1))
    scene.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)

    mesh = bpy.data.meshes.new("PilotBMesh")
    vertices = [(-.5, 0, 0), (.5, 0, 0), (-.5, 0, 1), (.5, 0, 1), (-.5, 0, 2), (.5, 0, 2)]
    faces = [(0, 1, 3, 2), (2, 3, 5, 4)]
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("PilotB", mesh)
    bpy.context.collection.objects.link(obj)

    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = ((co.x + .5), co.z / 2)

    mat = bpy.data.materials.new("PilotBMaterial")
    mat.diffuse_color = (0.25, 0.55, 0.8, 1)
    obj.data.materials.append(mat)

    arm_data = bpy.data.armatures.new("PilotBArmature")
    arm = bpy.data.objects.new("PilotBArmature", arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    lower = arm.data.edit_bones.new("lower")
    lower.head, lower.tail = (0, 0, 0), (0, 0, 1)
    upper = arm.data.edit_bones.new("upper")
    upper.head, upper.tail, upper.parent, upper.use_connect = (0, 0, 1), (0, 0, 2), lower, True
    bpy.ops.object.mode_set(mode="OBJECT")

    for name, indices in (("lower", (0, 1, 2, 3)), ("upper", (4, 5))):
        group = obj.vertex_groups.new(name=name)
        group.add(indices, 1.0, "REPLACE")
    modifier = obj.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = arm
    obj.parent = arm

    basis = obj.shape_key_add(name="Basis")
    smile = obj.shape_key_add(name="bend")
    smile.data[4].co.x -= .12
    smile.data[5].co.x += .12
    assert basis and smile

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = 384
    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    for name, location in RENDER_VIEWS.items():
        render_view(scene, camera, name, location)

    obj.show_wire = True
    obj.show_all_edges = True
    render_view(scene, camera, "geometry_diagnostic", (4, -6, 3))
    obj.show_wire = False
    obj.show_all_edges = False

    blend_path = OUT / "pilot-b.blend"
    glb_path = OUT / "pilot-b.glb"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_skins=True,
        export_morph=True,
        export_morph_normal=False,
        export_morph_tangent=False,
    )

    render_names = [*RENDER_VIEWS, "geometry_diagnostic"]
    manifest = {
        "schema_version": 1,
        "pilot": "B",
        "seed": SEED,
        "generated_with": "gpt-6-astra",
        "generation_method": "procedural",
        "human_reviewed": False,
        "source_asset_license": "CC0-1.0",
        "source_asset_urls": [],
        "features": {"uv": True, "material": True, "rig": True, "weights": True, "shape_key": "bend"},
        "outputs": {
            "blend": {"path": "pilot-b.blend", "sha256": sha256(blend_path)},
            "glb": {"path": "pilot-b.glb", "sha256": sha256(glb_path)},
            "renders": [
                {"path": f"{name}.png", "sha256": sha256(OUT / f"{name}.png")} for name in render_names
            ],
        },
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS", "pilot": "B", "output": str(OUT)}))


if __name__ == "__main__":
    main()
