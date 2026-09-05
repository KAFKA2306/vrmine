"""Verify exported files and that all review viewpoints are real nonblank renders."""
import hashlib
import json
import math
from pathlib import Path

import bpy

OUT = Path(__file__).resolve().parents[1] / '.artifacts' / 'retro-cafe'


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    manifest = json.loads((OUT / 'manifest.json').read_text())
    require(len(manifest['models']) == 10, 'Expected ten models')
    require(len({r['name'] for r in manifest['models']}) == 10, 'Duplicate models')
    for name, digest in manifest['sha256'].items():
        require(hashlib.sha256((OUT / name).read_bytes()).hexdigest() == digest,
                f'Hash mismatch: {name}')
    for record in manifest['models']:
        for ext in ('glb', 'fbx'):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            path = str(OUT / f"{record['name']}.{ext}")
            if ext == 'glb':
                bpy.ops.import_scene.gltf(filepath=path)
            else:
                bpy.ops.import_scene.fbx(filepath=path)
            meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
            require(len(meshes) == 1, f'Expected one prop mesh in {path}')
            obj = meshes[0]
            require(all(abs(a - b) < .002 for a, b in zip(obj.dimensions, record['dimensions_m'])),
                    f'Metre-scale bounds changed: {path}: {list(obj.dimensions)}')
            require(obj.location.length < .001, f'Pivot is not at origin: {path}')
            require(len(obj.data.materials) > 0, f'Materials missing: {path}')
            require(obj.data.uv_layers.active is not None, f'UV missing: {path}')
            for item in obj.data.uv_layers.active.data:
                require(all(math.isfinite(v) and -.001 <= v <= 1.001 for v in item.uv),
                        f'Invalid UV coordinate: {path}')
            obj.data.calc_loop_triangles()
            require(0 < len(obj.data.loop_triangles) < 30000, f'Triangle budget: {path}')
            require(all(all(math.isfinite(v) for v in vertex.co) for vertex in obj.data.vertices),
                    f'Nonfinite geometry: {path}')
            require(all(triangle.area > 1e-12 for triangle in obj.data.loop_triangles),
                    f'Degenerate geometry: {path}')
    bpy.ops.wm.open_mainfile(filepath=str(OUT / 'retro-cafe.blend'))
    require(bpy.context.scene.camera is not None, 'Example scene missing camera')
    require(all(record['name'] in bpy.context.scene.objects for record in manifest['models']),
            'Example scene missing models')
    review_images = [
        'thumbnail.png',
        'view-hero.png',
        'view-front.png',
        'view-rear.png',
        'view-left.png',
        'view-right.png',
        'view-top.png',
    ]
    for filename in review_images:
        image = bpy.data.images.load(str(OUT / filename), check_existing=False)
        require(tuple(image.size) == (600, 500), f'Render dimensions: {filename}')
        pixels = list(image.pixels)[::4]
        require(max(pixels) - min(pixels) > .1, f'Render is blank: {filename}')
        bpy.data.images.remove(image)
    print('PASS: 10 GLB + 10 FBX reimports, metre bounds, origin, materials, UV, geometry, scene, 6 review viewpoints')


if __name__ == '__main__':
    main()
