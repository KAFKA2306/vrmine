"""Independent Blender rig validator for Astra-generated rigged assets."""
from __future__ import annotations

import math

MAX_INFLUENCES = 4
WEIGHT_EPSILON = 1e-6
WEIGHT_SUM_TOLERANCE = 1e-3
ZERO_LENGTH_EPSILON = 1e-6
REQUIRED_HUMANOID_BONES = {
    "hips",
    "spine",
    "chest",
    "neck",
    "head",
    "leftupperarm",
    "leftlowerarm",
    "lefthand",
    "rightupperarm",
    "rightlowerarm",
    "righthand",
    "leftupperleg",
    "leftlowerleg",
    "leftfoot",
    "rightupperleg",
    "rightlowerleg",
    "rightfoot",
}


def _normalized(name: str) -> str:
    return "".join(ch for ch in name.lower() if ch.isalnum())


def assert_rig_contract(mesh_objects, armature_objects, *, humanoid: bool = False) -> dict:
    """Validate hierarchy and skin weights without trusting generator metadata."""
    if len(armature_objects) != 1:
        raise AssertionError(f"rig validator expected one armature, got {len(armature_objects)}")
    armature = armature_objects[0]
    bones = list(armature.data.bones)
    if not bones:
        raise AssertionError("rig validator found no bones")

    zero_length = [bone.name for bone in bones if not math.isfinite(bone.length) or bone.length <= ZERO_LENGTH_EPSILON]
    if zero_length:
        raise AssertionError(f"zero-length bones: {zero_length[:10]}")
    for bone in bones:
        seen = set()
        current = bone
        while current is not None:
            if current.name in seen:
                raise AssertionError(f"bone hierarchy cycle: {bone.name}")
            seen.add(current.name)
            current = current.parent

    if humanoid:
        present = {_normalized(bone.name) for bone in bones}
        missing = sorted(REQUIRED_HUMANOID_BONES - present)
        if missing:
            raise AssertionError(f"required humanoid bones missing: {missing}")

    weighted_vertices = 0
    for obj in mesh_objects:
        armature_modifiers = [modifier for modifier in obj.modifiers if modifier.type == "ARMATURE"]
        if len(armature_modifiers) != 1 or armature_modifiers[0].object != armature:
            raise AssertionError(f"mesh must have exactly one modifier bound to validated armature: {obj.name}")
        group_names = {group.index: group.name for group in obj.vertex_groups}
        bone_names = {bone.name for bone in bones}
        for vertex in obj.data.vertices:
            influences = [group for group in vertex.groups if group.weight > WEIGHT_EPSILON]
            if not influences:
                raise AssertionError(f"unweighted vertex: {obj.name}[{vertex.index}]")
            if len(influences) > MAX_INFLUENCES:
                raise AssertionError(f"too many bone influences: {obj.name}[{vertex.index}]={len(influences)}")
            total = sum(group.weight for group in influences)
            if not math.isfinite(total) or abs(total - 1.0) > WEIGHT_SUM_TOLERANCE:
                raise AssertionError(f"extreme/non-normalized weights: {obj.name}[{vertex.index}]={total}")
            unknown = [group_names.get(group.group, "") for group in influences if group_names.get(group.group) not in bone_names]
            if unknown:
                raise AssertionError(f"weights reference non-bones: {obj.name}[{vertex.index}]={unknown}")
            weighted_vertices += 1

    if not mesh_objects:
        raise AssertionError("rig validator found no skinned meshes")
    return {"bones": len(bones), "weighted_vertices": weighted_vertices, "humanoid": humanoid}
