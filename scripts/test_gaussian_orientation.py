#!/usr/bin/env python3
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
    e1 = (0.0, 0.0, 1.0)
    e2 = mod.vunit(mod.vcross(normal, e1))
    if e2 is None:
        e1 = (1.0, 0.0, 0.0)
        e2 = mod.vunit(mod.vcross(normal, e1))
    points = []
    for _ in range(plane_points):
        a = rng.uniform(-5.0, 5.0)
        b = rng.uniform(-5.0, 5.0)
        p = mod.vadd(mod.vscale(e1, a), mod.vscale(e2, b))
        p = mod.vadd(p, mod.vscale(normal, rng.gauss(0.0, 0.01)))
        points.append(p)
    for _ in range(outliers):
        points.append((rng.uniform(-5, 5), rng.uniform(-5, 5), rng.uniform(-5, 5)))
    return points

theta = math.radians(12.0)
ground_normal = (math.sin(theta), math.cos(theta), 0.0)
ground = mod.analyze(synthetic_plane(ground_normal), "synthetic-ground", "horizon")
assert ground["action"] == "apply"
assert_close(ground["tilt_deg"], 12.0, 0.15, "ground tilt")
assert ground["plane"]["inlier_ratio"] > 0.85
assert ground["post_alignment_residual_deg"] < 1e-4

wall_theta = math.radians(8.0)
wall_normal = (math.cos(wall_theta), math.sin(wall_theta), 0.0)
wall = mod.analyze(synthetic_plane(wall_normal, seed=11), "synthetic-wall", "wall")
assert wall["action"] == "apply"
assert_close(wall["tilt_deg"], 8.0, 0.15, "wall normal tilt")
assert wall["post_alignment_residual_deg"] < 1e-4

with tempfile.TemporaryDirectory() as td:
    p = Path(td) / "sample.ply"
    with p.open("wb") as f:
        f.write(
            b"ply\n"
            b"format binary_little_endian 1.0\n"
            b"element vertex 4\n"
            b"property float x\n"
            b"property float y\n"
            b"property float z\n"
            b"end_header\n"
        )
        for xyz in [(1,2,3),(2,2,3),(3,2,3),(4,2,3)]:
            f.write(struct.pack("<fff", *xyz))
    sampled, count = mod.sample_ply_points(p, 10)
    assert count == 4
    assert sampled[0] == (1.0, 2.0, 3.0)
    assert sampled[-1] == (4.0, 2.0, 3.0)

assert mod.infer_mode("Fachada del Templo") == "wall"
assert mod.infer_mode("Aerial video of a castle") == "horizon"

print("Gaussian orientation analyzer synthetic verification PASS")
