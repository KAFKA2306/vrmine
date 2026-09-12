"""Blender regression checks for primitive transforms and GLB round trips."""
import sys
import tempfile
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from world_item_factory import make_material, make_part, world_bounds


def main():
    for component, data, expected in [
        ("cylinder", {"radius": 0.02, "height": 0.30, "vertices": 8}, (0.30, 0.04, 0.04)),
        ("cone", {"size": [0.04, 0.02, 0.30], "vertices": 8}, (0.30, 0.02, 0.04)),
        ("sphere", {"size": [0.08, 0.06, 0.02], "vertices": 12}, (0.02, 0.06, 0.08)),
    ]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        material = make_material("test", {"base_color": [0.2, 0.3, 0.1], "roughness": 0.9})
        obj = make_part({"name": component, "component": component, "material": "test",
                         "position": [0.1, 0.2, 0.3], "rotation_deg": [0, 90, 0], **data}, {"test": material})
        for stage in ("source", "glb"):
            if stage == "glb":
                with tempfile.TemporaryDirectory() as directory:
                    path = str(Path(directory) / "primitive.glb")
                    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True)
                    bpy.ops.wm.read_factory_settings(use_empty=True)
                    bpy.ops.import_scene.gltf(filepath=path)
                    obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
            bpy.context.view_layer.update()
            _, _, center, size, _ = world_bounds(obj)
            assert all(abs(a-b) < (0.003 if component == "cylinder" else 0.001) for a,b in zip(size, expected)), (component, stage, tuple(size), expected)
            assert all(abs(a-b) < 0.001 for a,b in zip(center, (0.1, 0.2, 0.3))), (component, stage, tuple(center))
    print("PASS primitive rotation, anisotropic dimensions, and GLB round trips")


if __name__ == "__main__":
    main()
