#!/usr/bin/env python3
import hashlib
import importlib.util
import math
import random
import struct
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("gaussian_orientation", ROOT / "gaussian_orientation.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def assert_close(actual, expected, tol, label):
    if abs(actual - expected) > tol:
        raise AssertionError(f"{label}: expected {expected} ± {tol}, got {actual}")


def synthetic_plane(normal, seed=7, plane_points=3000, outliers=300):
    rng = random.Random(seed)
    e1 = (0.0, 1.0, 0.0)
    e2 = mod.vunit(mod.vcross(normal, e1))
    if e2 is None:
        e1 = (1.0, 0.0, 0.0)
        e2 = mod.vunit(mod.vcross(normal, e1))
    points = []
    for _ in range(plane_points):
        a = rng.uniform(-5.0, 5.0)
        b = rng.uniform(-5.0, 5.0)
        point = (
            e1[0] * a + e2[0] * b + normal[0] * rng.gauss(0.0, 0.01),
            e1[1] * a + e2[1] * b + normal[1] * rng.gauss(0.0, 0.01),
            e1[2] * a + e2[2] * b + normal[2] * rng.gauss(0.0, 0.01),
        )
        points.append(point)
    for _ in range(outliers):
        points.append((rng.uniform(-5, 5), rng.uniform(-5, 5), rng.uniform(-5, 5)))
    return points


def write_binary_ply(path, points):
    with path.open("wb") as handle:
        handle.write(
            b"ply\n"
            b"format binary_little_endian 1.0\n"
            + f"element vertex {len(points)}\n".encode()
            + b"property float x\n"
            b"property float y\n"
            b"property float z\n"
            b"end_header\n"
        )
        for xyz in points:
            handle.write(struct.pack("<fff", *xyz))


def fixture_registry(source_id, path, *, physical_up_status="review_required", scope="coordinate_basis_only"):
    payload = path.read_bytes() if path and path.is_file() else b"missing"
    size = path.stat().st_size if path and path.is_file() else 123
    digest = hashlib.sha256(payload).hexdigest() if path and path.is_file() else "a" * 64
    registry = {
        "environments": [
            {
                "id": source_id,
                "source": {
                    "sha256": digest,
                    "size_bytes": size,
                    "provenance": {"title": "title must not affect geometry semantics"},
                },
            }
        ]
    }
    exhibition = {
        "import_overrides": [
            {
                "id": source_id,
                "alignment": {
                    "enabled": True,
                    "scope": scope,
                    "physicalUpStatus": physical_up_status,
                    "authority": "producer-artifact-metadata",
                },
            }
        ]
    }
    return registry, exhibition


theta = math.radians(12.0)
ground_like_normal = (math.sin(theta), 0.0, math.cos(theta))
ground_diagnostic = mod.analyze_geometry(
    synthetic_plane(ground_like_normal), "synthetic-ground-like"
)
assert ground_diagnostic["status"] == "measured"
assert ground_diagnostic["semantic_authority"] is False
assert "action" not in ground_diagnostic
assert "alignment" not in ground_diagnostic
assert_close(
    ground_diagnostic["dominant_plane"]["nerfstudio_z_up_angle_deg"],
    12.0,
    0.2,
    "raw model +Z diagnostic",
)

wall_theta = math.radians(8.0)
wall_normal = (math.cos(wall_theta), math.sin(wall_theta), 0.0)
wall_diagnostic = mod.analyze_geometry(
    synthetic_plane(wall_normal, seed=11), "synthetic-wall-like"
)
assert wall_diagnostic["dominant_plane"]["nearest_axis"] == "x"
assert_close(
    wall_diagnostic["dominant_plane"]["nearest_axis_angle_deg"],
    8.0,
    0.2,
    "wall-like nearest-axis diagnostic",
)

with tempfile.TemporaryDirectory() as td:
    root = Path(td)
    ply = root / "fixture.ply"
    points = synthetic_plane((0.0, 0.0, 1.0), plane_points=500, outliers=20)
    write_binary_ply(ply, points)
    sampled, count = mod.sample_ply_points(ply, 100)
    assert count == len(points)
    assert len(sampled) <= 100

    source_dir = root / "sources"
    source_dir.mkdir()
    materialized = source_dir / "fixture.ply"
    materialized.write_bytes(ply.read_bytes())
    registry, exhibition = fixture_registry("fixture", materialized)
    report = mod.build_report(registry, exhibition, source_dir, max_points=500)
    assert report["hard_failures"] == []
    assert report["summary"]["registered"] == 1
    assert report["summary"]["basis_accepted"] == 1
    assert report["summary"]["physical_up"]["review_required"] == 1
    assert report["summary"]["orientation_decisions"]["review_required"] == 1
    assert report["summary"]["artifact_verification"]["verified"] == 1
    result = report["results"][0]
    assert result["artifact"]["actual_sha256"] == registry["environments"][0]["source"]["sha256"]
    assert result["geometry_diagnostic"]["status"] == "measured"
    assert result["geometry_diagnostic"]["semantic_authority"] is False

with tempfile.TemporaryDirectory() as td:
    source_dir = Path(td)
    registry, exhibition = fixture_registry("missing", None)
    report = mod.build_report(registry, exhibition, source_dir, allow_missing=True)
    assert report["hard_failures"] == []
    assert report["summary"]["artifact_verification"]["unavailable"] == 1
    assert report["results"][0]["orientation_decision"] == "review_required"
    assert report["results"][0]["geometry_diagnostic"]["status"] == "unavailable"

    strict_report = mod.build_report(registry, exhibition, source_dir, allow_missing=False)
    assert strict_report["hard_failures"] == ["missing: materialized PLY missing"]

    accepted_registry, accepted_exhibition = fixture_registry(
        "accepted",
        None,
        physical_up_status="accepted",
        scope="coordinate_basis_plus_physical_up",
    )
    accepted_report = mod.build_report(
        accepted_registry, accepted_exhibition, source_dir, allow_missing=True
    )
    assert accepted_report["results"][0]["orientation_decision"] == "accepted"
    assert accepted_report["results"][0]["artifact"]["verification_status"] == "unavailable"

    broken_exhibition = {"import_overrides": []}
    broken_report = mod.build_report(registry, broken_exhibition, source_dir, allow_missing=True)
    assert broken_report["results"][0]["orientation_decision"] == "fail"

print("Gaussian orientation diagnostic/report verification PASS")
