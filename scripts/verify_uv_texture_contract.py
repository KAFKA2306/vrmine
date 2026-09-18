"""Independent Blender UV/texture validator for Astra-generated assets."""
from __future__ import annotations

import math
from pathlib import Path

UV_EPSILON = 1e-6


def assert_uv_texture_contract(mesh_objects, *, artifact_root: Path | None = None) -> dict:
    """Validate UV coverage and image texture color-space/path contracts."""
    if not mesh_objects:
        raise AssertionError("UV/texture validator found no meshes")

    uv_loops = 0
    images = {}
    for obj in mesh_objects:
        uv = obj.data.uv_layers.active
        if uv is None or not uv.data:
            raise AssertionError(f"UV layer missing: {obj.name}")
        for loop in uv.data:
            u, v = loop.uv
            if not math.isfinite(u) or not math.isfinite(v):
                raise AssertionError(f"non-finite UV: {obj.name}")
            if u < -UV_EPSILON or u > 1 + UV_EPSILON or v < -UV_EPSILON or v > 1 + UV_EPSILON:
                raise AssertionError(f"UV outside 0..1 atlas: {obj.name}")
            uv_loops += 1

        if not obj.data.materials:
            raise AssertionError(f"material missing: {obj.name}")
        for material in obj.data.materials:
            if material is None or not material.use_nodes or material.node_tree is None:
                continue
            for node in material.node_tree.nodes:
                if node.type != "TEX_IMAGE" or node.image is None:
                    continue
                image = node.image
                source = Path(image.filepath_from_user()) if image.filepath else None
                if source is not None and not source.is_file():
                    raise AssertionError(f"texture path missing: {image.name}={source}")
                if artifact_root is not None and source is not None:
                    try:
                        source.resolve().relative_to(artifact_root.resolve())
                    except ValueError as exc:
                        raise AssertionError(f"texture escapes artifact root: {image.name}={source}") from exc
                color_space = image.colorspace_settings.name
                if color_space not in {"sRGB", "Non-Color", "Linear"}:
                    raise AssertionError(f"unsupported texture color space: {image.name}={color_space}")
                images[image.name] = color_space

    if uv_loops == 0:
        raise AssertionError("UV/texture validator found no UV loops")
    return {"meshes": len(mesh_objects), "uv_loops": uv_loops, "images": images}
