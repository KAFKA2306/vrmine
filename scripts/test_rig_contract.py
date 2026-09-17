"""Focused contract tests for the rig validator; runs in Blender Python."""
from __future__ import annotations

import bpy

from verify_rig_contract import assert_rig_contract


def build_fixture(*, zero_length=False, unweighted=False, influence_count=1):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.object.armature_add()
    armature = bpy.context.object
    armature.name = "Rig"
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")
    root = armature.data.edit_bones[0]
    root.name = "hips"
    root.head = (0, 0, 0)
    root.tail = (0, 0, 0 if zero_length else 1)
    for index in range(1, influence_count):
        bone = armature.data.edit_bones.new(f"bone{index}")
        bone.head = (0, 0, 0)
        bone.tail = (0, 0, 1)
        bone.parent = root
    bpy.ops.object.mode_set(mode="OBJECT")

    mesh = bpy.data.meshes.new("Mesh")
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (0, 1, 0)], [], [(0, 1, 2)])
    obj = bpy.data.objects.new("Mesh", mesh)
    bpy.context.collection.objects.link(obj)
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature
    if not unweighted:
        weight = 1.0 / influence_count
        for bone in armature.data.bones:
            group = obj.vertex_groups.new(name=bone.name)
            group.add([0, 1, 2], weight, "REPLACE")
    return obj, armature


def expect_failure(**kwargs):
    obj, armature = build_fixture(**kwargs)
    try:
        assert_rig_contract([obj], [armature])
    except AssertionError:
        return
    raise AssertionError(f"invalid rig fixture was accepted: {kwargs}")


obj, armature = build_fixture()
result = assert_rig_contract([obj], [armature])
assert result == {"bones": 1, "weighted_vertices": 3, "humanoid": False}
expect_failure(zero_length=True)
expect_failure(unweighted=True)
expect_failure(influence_count=5)
print("Astra rig validator contract: PASS")
