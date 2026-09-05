"""Generate ten metre-scale café props, GLB/FBX, a staged .blend and multi-angle renders.

Run with Blender 4.2: blender -b --python-exit-code 1 --python scripts/build-retro-cafe.py
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parents[1] / '.artifacts' / 'retro-cafe'
NAMES = ('pendant-light', 'table-lamp', 'wall-light', 'round-table', 'stool',
         'side-table', 'cup', 'saucer', 'tray', 'vase')


def material(name, color, roughness, metallic=0, emission=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Metallic'].default_value = metallic
    node.inputs['Emission Color'].default_value = (*color, 1)
    node.inputs['Emission Strength'].default_value = emission
    return mat


def finish(obj, mat, bevel=0.002):
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft edges', 'BEVEL')
        mod.width, mod.segments = bevel, 3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    mod = obj.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def cylinder(radius, height, z, mat, x=0, y=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=height,
                                      location=(x, y, z))
    return finish(bpy.context.object, mat, min(0.003, height / 5, radius / 5))


def box(size, position, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, mat)


def lathe(profile, mat):
    """Closed radial profile; the inner wall makes vessels/shades solid meshes."""
    vertices, faces = [], []
    segments = 48
    for radius, z in profile:
        for i in range(segments):
            angle = 2 * math.pi * i / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), z))
    for row in range(len(profile)):
        next_row = (row + 1) % len(profile)
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((row * segments + i, row * segments + j,
                          next_row * segments + j, next_row * segments + i))
    mesh = bpy.data.meshes.new('Turned profile')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('Turned profile', mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # Profile direction differs between shade and vessel; orient closed shells outward.
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return finish(obj, mat, 0.001)


def shade(z, radius, mat):
    return lathe([(radius, z), (radius * .4, z + radius * .65),
                  (radius * .4 - .004, z + radius * .65),
                  (radius - .004, z)], mat)


def build(name, mats):
    wood, metal, brass, cream, glow = mats
    before = set(bpy.data.objects)
    if name == 'pendant-light':
        cylinder(.07, .025, .9375, metal)
        cylinder(.006, .69, .58, metal)
        shade(.025, .30, brass)
        cylinder(.045, .025, .025, glow)
    elif name == 'table-lamp':
        cylinder(.10, .025, .0125, metal)
        cylinder(.014, .28, .16, brass)
        shade(.25, .16, cream)
        cylinder(.035, .02, .26, glow)
    elif name == 'wall-light':
        box((.10, .025, .20), (0, -.12, .10), metal)
        box((.018, .15, .018), (0, -.035, .12), brass)
        shade(.12, .10, cream)
        cylinder(.025, .02, .125, glow)
    elif name in ('round-table', 'side-table', 'stool'):
        radius, height = {'round-table': (.38, .72), 'side-table': (.23, .48),
                          'stool': (.17, .45)}[name]
        cylinder(radius, .035, height - .0175, wood)
        if name == 'stool':
            for x in (-.10, .10):
                for y in (-.10, .10):
                    cylinder(.017, height - .035, (height - .035) / 2, metal, x, y)
        else:
            cylinder(.03, height - .035, (height - .035) / 2, metal)
            cylinder(radius * .65, .025, .0125, metal)
    elif name == 'cup':
        cylinder(.033, .006, .003, cream)
        lathe([(.004, 0), (.035, 0), (.043, .085), (.038, .085),
               (.030, .006), (.004, .006)], cream)
        # A half torus attaches at the two ends, with no handle crossing the cavity.
        curve = bpy.data.curves.new('Cup handle', 'CURVE')
        curve.dimensions, curve.bevel_depth, curve.bevel_resolution = '3D', .005, 3
        spline = curve.splines.new('POLY')
        spline.points.add(24)
        for i, point in enumerate(spline.points):
            a = -math.pi / 2 + math.pi * i / 24
            point.co = (.039 + .025 * math.cos(a), 0, .045 + .028 * math.sin(a), 1)
        obj = bpy.data.objects.new('Handle', curve)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target='MESH')
        finish(bpy.context.object, cream, 0)
    elif name == 'saucer':
        cylinder(.075, .008, .004, cream)
        lathe([(.050, .004), (.075, .004), (.075, .015), (.050, .008)], cream)
    elif name == 'tray':
        box((.30, .20, .010), (0, 0, .005), wood)
        for x in (-.145, .145):
            box((.010, .20, .022), (x, 0, .015), wood)
        for y in (-.095, .095):
            box((.28, .010, .022), (0, y, .015), wood)
    elif name == 'vase':
        cylinder(.038, .006, .003, cream)
        lathe([(.038, .003), (.045, .05), (.020, .13), (.018, .19),
               (.014, .19), (.016, .13), (.040, .05), (.033, .006)], cream)
    else:
        raise ValueError(name)
    objects = sorted(set(bpy.data.objects) - before, key=lambda obj: obj.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return obj


def main():
    if bpy.app.version[:2] != (4, 2):
        raise RuntimeError('Use Blender / bpy 4.2 for reproducible exports')
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    mats = [material('Walnut', (.18, .075, .032), .55),
            material('Charcoal', (.028, .032, .030), .42, .65),
            material('Satin brass', (.46, .28, .09), .38, .75),
            material('Ivory ceramic', (.80, .72, .55), .38),
            material('Warm diffuser', (1, .68, .35), .45, emission=2)]
    props, records = {}, []
    for name in NAMES:
        obj = build(name, mats)
        props[name] = obj
        bpy.ops.export_scene.gltf(filepath=str(OUT / f'{name}.glb'),
                                  export_format='GLB', use_selection=True)
        bpy.ops.export_scene.fbx(filepath=str(OUT / f'{name}.fbx'),
                                 use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', bake_anim=False)
        obj.data.calc_loop_triangles()
        records.append({'name': name, 'dimensions_m': list(obj.dimensions),
                        'triangles': len(obj.data.loop_triangles)})
        obj.hide_set(True)
    placements = {
        'round-table': (0, 0, 0), 'stool': (.64, -.20, 0),
        'side-table': (-.70, .18, 0), 'table-lamp': (-.70, .18, .48),
        'pendant-light': (0, 0, 1.30), 'wall-light': (-.73, .6175, 1.22),
        'tray': (.04, 0, .72), 'saucer': (-.025, 0, .746),
        'cup': (-.025, 0, .754), 'vase': (.18, .15, .72)}
    for name, obj in props.items():
        obj.hide_set(False)
        obj.location = placements[name]
    props['wall-light'].rotation_euler.z = math.pi
    floor = material('Backdrop', (.32, .38, .34), .85)
    box((2.8, 2.4, .06), (0, 0, -.035), floor)
    box((2.8, .06, 2.6), (0, .78, 1.27), floor)
    bpy.ops.object.camera_add(location=(3.2, -4.8, 3.1))
    camera = bpy.context.object
    camera.data.type = 'ORTHO'
    scene.camera = camera
    for location, energy, size in [((1, -3, 4), 450, 4), ((-2, -1, 2), 180, 3)]:
        bpy.ops.object.light_add(type='AREA', location=location)
        light = bpy.context.object
        light.data.energy, light.data.shape, light.data.size = energy, 'DISK', size
        light.rotation_euler = (Vector((0, 0, .7)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.world = bpy.data.worlds.new('Cafe world')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.12, .12, .12, 1)
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.cycles.seed = 191
    scene.render.resolution_x, scene.render.resolution_y = 600, 500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'

    views = {
        'thumbnail.png': ((3.2, -4.8, 3.1), (0, 0, 1.0), 3.3),
        'view-hero.png': ((3.2, -4.8, 3.1), (0, 0, 1.0), 3.3),
        'view-front.png': ((0, -5.2, 1.7), (0, 0, 1.0), 3.0),
        'view-rear.png': ((0, 5.2, 1.7), (0, 0, 1.0), 3.0),
        'view-left.png': ((-5.2, 0, 1.7), (0, 0, 1.0), 3.0),
        'view-right.png': ((5.2, 0, 1.7), (0, 0, 1.0), 3.0),
        'view-top.png': ((0, -0.05, 6.5), (0, 0, .72), 3.1),
    }
    hero_location, hero_target, hero_scale = views['view-hero.png']
    camera.location = hero_location
    camera.rotation_euler = (Vector(hero_target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = hero_scale
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'retro-cafe.blend'))

    for filename, (location, target, ortho_scale) in views.items():
        camera.location = location
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.ortho_scale = ortho_scale
        scene.render.filepath = str(OUT / filename)
        bpy.ops.render.render(write_still=True)

    files = [f'{name}.{ext}' for name in NAMES for ext in ('glb', 'fbx')]
    files += ['retro-cafe.blend', *views.keys()]
    manifest = {'blender': bpy.app.version_string, 'units': 'metres', 'models': records,
                'unity_import': 'UNVERIFIED', 'vrchat_runtime': 'UNVERIFIED',
                'sha256': {name: hashlib.sha256((OUT / name).read_bytes()).hexdigest() for name in files}}
    (OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    main()
