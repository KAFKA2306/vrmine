#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import struct
from pathlib import Path

PLY_TYPES = {
    "char": ("b", 1),
    "int8": ("b", 1),
    "uchar": ("B", 1),
    "uint8": ("B", 1),
    "short": ("h", 2),
    "int16": ("h", 2),
    "ushort": ("H", 2),
    "uint16": ("H", 2),
    "int": ("i", 4),
    "int32": ("i", 4),
    "uint": ("I", 4),
    "uint32": ("I", 4),
    "float": ("f", 4),
    "float32": ("f", 4),
    "double": ("d", 8),
    "float64": ("d", 8),
}
AXES = {
    "x": (1.0, 0.0, 0.0),
    "y": (0.0, 1.0, 0.0),
    "z": (0.0, 0.0, 1.0),
}


def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vdot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def vcross(a, b):
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def vnorm(a):
    return math.sqrt(max(0.0, vdot(a, a)))


def vunit(a):
    norm = vnorm(a)
    if norm < 1e-12:
        return None
    return (a[0] / norm, a[1] / norm, a[2] / norm)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_header(handle):
    if handle.readline().strip() != b"ply":
        raise ValueError("not a PLY file")
    fmt = None
    vertex_count = None
    vertex_props = []
    in_vertex = False
    while True:
        line = handle.readline()
        if not line:
            raise ValueError("truncated PLY header")
        text = line.decode("ascii", "strict").strip()
        if text == "end_header":
            break
        parts = text.split()
        if not parts or parts[0] in {"comment", "obj_info"}:
            continue
        if parts[0] == "format":
            fmt = parts[1]
        elif parts[0] == "element":
            in_vertex = len(parts) >= 3 and parts[1] == "vertex"
            if in_vertex:
                vertex_count = int(parts[2])
        elif parts[0] == "property" and in_vertex:
            if len(parts) >= 2 and parts[1] == "list":
                raise ValueError("list property inside vertex element is unsupported")
            typ, name = parts[1], parts[2]
            if typ not in PLY_TYPES:
                raise ValueError(f"unsupported PLY type {typ}")
            vertex_props.append((name, typ))
    if fmt not in {"binary_little_endian", "ascii"}:
        raise ValueError(f"unsupported PLY format {fmt}")
    if not vertex_count or vertex_count < 3:
        raise ValueError("vertex count < 3")
    names = {name for name, _ in vertex_props}
    if not {"x", "y", "z"} <= names:
        raise ValueError("PLY vertex requires x/y/z")
    return fmt, vertex_count, vertex_props, handle.tell()


def sample_ply_points(path, max_points=12000):
    path = Path(path)
    with path.open("rb") as handle:
        fmt, count, props, data_offset = _read_header(handle)
        stride = max(1, math.ceil(count / max_points))
        indices = range(0, count, stride)
        points = []
        if fmt == "binary_little_endian":
            offsets = {}
            row_size = 0
            for name, typ in props:
                offsets[name] = (row_size, PLY_TYPES[typ][0])
                row_size += PLY_TYPES[typ][1]
            for idx in indices:
                handle.seek(data_offset + idx * row_size)
                row = handle.read(row_size)
                if len(row) != row_size:
                    raise ValueError("truncated binary PLY data")
                xyz = []
                for name in ("x", "y", "z"):
                    offset, code = offsets[name]
                    xyz.append(struct.unpack_from("<" + code, row, offset)[0])
                if all(math.isfinite(value) for value in xyz):
                    points.append(tuple(float(value) for value in xyz))
        else:
            prop_index = {name: i for i, (name, _) in enumerate(props)}
            wanted = set(indices)
            last = max(wanted)
            for idx in range(last + 1):
                row = handle.readline()
                if not row:
                    raise ValueError("truncated ascii PLY data")
                if idx in wanted:
                    values = row.decode("ascii", "strict").split()
                    xyz = tuple(float(values[prop_index[name]]) for name in ("x", "y", "z"))
                    if all(math.isfinite(value) for value in xyz):
                        points.append(xyz)
    if len(points) < 3:
        raise ValueError("not enough finite sampled points")
    return points, count


def robust_core(points, quantile=0.985):
    xs = sorted(point[0] for point in points)
    ys = sorted(point[1] for point in points)
    zs = sorted(point[2] for point in points)
    center = (xs[len(xs) // 2], ys[len(ys) // 2], zs[len(zs) // 2])
    distances = sorted((vnorm(vsub(point, center)), i) for i, point in enumerate(points))
    keep = max(3, int(len(points) * quantile))
    return [points[i] for _, i in distances[:keep]]


def bbox_diag(points):
    mins = [min(point[i] for point in points) for i in range(3)]
    maxs = [max(point[i] for point in points) for i in range(3)]
    return vnorm(tuple(maxs[i] - mins[i] for i in range(3)))


def plane_from3(a, b, c):
    normal = vunit(vcross(vsub(b, a), vsub(c, a)))
    if normal is None:
        return None
    return normal, -vdot(normal, a)


def jacobi_smallest_eigenvector(cov, sweeps=32):
    matrix = [list(row) for row in cov]
    vectors = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]
    for _ in range(sweeps):
        p, q = max(((0, 1), (0, 2), (1, 2)), key=lambda ij: abs(matrix[ij[0]][ij[1]]))
        if abs(matrix[p][q]) < 1e-15:
            break
        phi = 0.5 * math.atan2(2 * matrix[p][q], matrix[q][q] - matrix[p][p])
        cosine = math.cos(phi)
        sine = math.sin(phi)
        app, aqq, apq = matrix[p][p], matrix[q][q], matrix[p][q]
        matrix[p][p] = cosine * cosine * app - 2 * sine * cosine * apq + sine * sine * aqq
        matrix[q][q] = sine * sine * app + 2 * sine * cosine * apq + cosine * cosine * aqq
        matrix[p][q] = matrix[q][p] = 0.0
        for row in range(3):
            if row in (p, q):
                continue
            arp, arq = matrix[row][p], matrix[row][q]
            matrix[row][p] = matrix[p][row] = cosine * arp - sine * arq
            matrix[row][q] = matrix[q][row] = sine * arp + cosine * arq
        for row in range(3):
            vrp, vrq = vectors[row][p], vectors[row][q]
            vectors[row][p] = cosine * vrp - sine * vrq
            vectors[row][q] = sine * vrp + cosine * vrq
    index = min(range(3), key=lambda i: matrix[i][i])
    return vunit((vectors[0][index], vectors[1][index], vectors[2][index]))


def refine_plane(points, inlier_indices):
    selected = [points[i] for i in inlier_indices]
    inv = 1.0 / len(selected)
    center = tuple(sum(point[i] for point in selected) * inv for i in range(3))
    xx = xy = xz = yy = yz = zz = 0.0
    for point in selected:
        x, y, z = vsub(point, center)
        xx += x * x
        xy += x * y
        xz += x * z
        yy += y * y
        yz += y * z
        zz += z * z
    normal = jacobi_smallest_eigenvector(((xx, xy, xz), (xy, yy, yz), (xz, yz, zz)))
    if normal is None:
        raise ValueError("plane refinement failed")
    return normal, center


def fit_dominant_plane(points, seed, iterations=500, distance_ratio=0.006):
    core = robust_core(points)
    diagonal = bbox_diag(core)
    if not math.isfinite(diagonal) or diagonal <= 1e-9:
        raise ValueError("degenerate point cloud bounds")
    threshold = max(diagonal * distance_ratio, 1e-7)
    rng = random.Random(seed)
    best = None
    for _ in range(iterations):
        i, j, k = rng.sample(range(len(core)), 3)
        model = plane_from3(core[i], core[j], core[k])
        if model is None:
            continue
        normal, offset = model
        inliers = [idx for idx, point in enumerate(core) if abs(vdot(normal, point) + offset) <= threshold]
        if best is None or len(inliers) > len(best):
            best = inliers
    if not best or len(best) < 3:
        raise ValueError("RANSAC found no plane")
    normal, center = refine_plane(core, best)
    residuals = [abs(vdot(normal, vsub(core[i], center))) for i in best]
    rms = math.sqrt(sum(value * value for value in residuals) / len(residuals))
    return {
        "normal": normal,
        "center": center,
        "inliers": len(best),
        "sampled": len(core),
        "inlier_ratio": len(best) / len(core),
        "distance_threshold": threshold,
        "rms_residual": rms,
        "bbox_diag": diagonal,
    }


def acute_axis_angle_deg(normal, axis):
    unit = vunit(normal)
    if unit is None:
        raise ValueError("zero normal")
    cosine = max(-1.0, min(1.0, abs(vdot(unit, axis))))
    return math.degrees(math.acos(cosine))


def analyze_geometry(points, source_id):
    seed = int.from_bytes(hashlib.sha256(source_id.encode()).digest()[:8], "big")
    plane = fit_dominant_plane(points, seed)
    angles = {name: acute_axis_angle_deg(plane["normal"], axis) for name, axis in AXES.items()}
    nearest_axis = min(angles, key=angles.get)
    return {
        "status": "measured",
        "semantic_authority": False,
        "authority_note": "dominant geometry is diagnostic only; it does not identify physical gravity or ground",
        "dominant_plane": {
            "normal": list(plane["normal"]),
            "center": list(plane["center"]),
            "axis_angles_deg": angles,
            "nearest_axis": nearest_axis,
            "nearest_axis_angle_deg": angles[nearest_axis],
            "nerfstudio_z_up_angle_deg": angles["z"],
            "inliers": plane["inliers"],
            "sampled": plane["sampled"],
            "inlier_ratio": plane["inlier_ratio"],
            "distance_threshold": plane["distance_threshold"],
            "rms_residual": plane["rms_residual"],
            "bbox_diag": plane["bbox_diag"],
        },
    }


def _alignment_map(exhibition):
    return {
        entry["id"]: entry.get("alignment") or {}
        for entry in exhibition.get("import_overrides", [])
        if isinstance(entry, dict) and entry.get("id")
    }


def build_report(registry, exhibition, source_dir, max_points=12000, allow_missing=False):
    source_dir = Path(source_dir)
    alignments = _alignment_map(exhibition)
    results = []
    hard_failures = []

    for environment in registry.get("environments", []):
        source_id = environment.get("id")
        source = environment.get("source") or {}
        if not source_id or not source.get("sha256") or not source.get("size_bytes"):
            hard_failures.append(f"invalid registry entry: {source_id!r}")
            continue

        alignment = alignments.get(source_id) or {}
        scope = alignment.get("scope")
        basis_accepted = bool(alignment.get("enabled")) and scope in {
            "coordinate_basis_only",
            "coordinate_basis_plus_physical_up",
        }
        physical_up_status = alignment.get("physicalUpStatus") or "unavailable"
        if not basis_accepted:
            decision = "fail"
        elif physical_up_status == "accepted":
            decision = "accepted"
        else:
            decision = "review_required"

        result = {
            "id": source_id,
            "orientation_decision": decision,
            "basis": {
                "status": "accepted" if basis_accepted else "fail",
                "scope": scope,
                "authority": alignment.get("authority"),
            },
            "physical_up": {"status": physical_up_status},
            "artifact": {
                "expected_sha256": source["sha256"],
                "expected_size_bytes": source["size_bytes"],
            },
        }

        file_path = source_dir / f"{source_id}.ply"
        if not file_path.is_file():
            result["artifact"]["verification_status"] = "unavailable"
            result["artifact"]["reason"] = "artifact bytes are not materialized"
            result["geometry_diagnostic"] = {
                "status": "unavailable",
                "semantic_authority": False,
                "reason": "artifact bytes are not materialized",
            }
            if not allow_missing:
                hard_failures.append(f"{source_id}: materialized PLY missing")
            results.append(result)
            continue

        try:
            actual_size = file_path.stat().st_size
            actual_sha256 = sha256_file(file_path)
            if actual_size != source["size_bytes"]:
                raise ValueError(f"size mismatch expected={source['size_bytes']} actual={actual_size}")
            if actual_sha256 != source["sha256"]:
                raise ValueError(f"SHA-256 mismatch expected={source['sha256']} actual={actual_sha256}")
            points, vertex_count = sample_ply_points(file_path, max_points)
            result["artifact"].update(
                {
                    "verification_status": "verified",
                    "actual_sha256": actual_sha256,
                    "actual_size_bytes": actual_size,
                    "vertex_count": vertex_count,
                    "sample_count": len(points),
                }
            )
            result["geometry_diagnostic"] = analyze_geometry(points, source_id)
        except Exception as exc:
            result["artifact"]["verification_status"] = "fail"
            result["artifact"]["reason"] = str(exc)
            result["geometry_diagnostic"] = {
                "status": "fail",
                "semantic_authority": False,
                "reason": str(exc),
            }
            result["orientation_decision"] = "fail"
            hard_failures.append(f"{source_id}: {exc}")
        results.append(result)

    physical_counts = {}
    decision_counts = {}
    artifact_counts = {}
    for result in results:
        physical = result["physical_up"]["status"]
        physical_counts[physical] = physical_counts.get(physical, 0) + 1
        decision = result["orientation_decision"]
        decision_counts[decision] = decision_counts.get(decision, 0) + 1
        artifact_status = result["artifact"].get("verification_status", "unavailable")
        artifact_counts[artifact_status] = artifact_counts.get(artifact_status, 0) + 1

    report = {
        "schema_version": 2,
        "method": "exact-artifact-identity + deterministic dominant-plane diagnostic",
        "semantic_policy": {
            "geometry_is_authority": False,
            "title_heuristics_are_authority": False,
            "physical_up_acceptance_requires_external_authority": True,
            "raw_ply_frame": "pinned Nerfstudio model/world basis; model +Z is a basis convention, not independently observed gravity",
        },
        "summary": {
            "registered": len(registry.get("environments", [])),
            "reported": len(results),
            "basis_accepted": sum(1 for result in results if result["basis"]["status"] == "accepted"),
            "physical_up": physical_counts,
            "orientation_decisions": decision_counts,
            "artifact_verification": artifact_counts,
        },
        "results": results,
        "hard_failures": hard_failures,
    }
    if len(results) != len(registry.get("environments", [])):
        hard_failures.append("orientation report count mismatch")
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--registry", default="config/gaussian-splats.json")
    parser.add_argument("--exhibition", default="config/gaussian-exhibition.json")
    parser.add_argument("--source-dir", default="Library/VRMine/GaussianSources")
    parser.add_argument("--output", default="Library/VRMine/gaussian-orientation-evidence.json")
    parser.add_argument("--max-points", type=int, default=12000)
    parser.add_argument("--allow-missing", action="store_true")
    args = parser.parse_args()

    registry = json.loads(Path(args.registry).read_text(encoding="utf-8"))
    exhibition = json.loads(Path(args.exhibition).read_text(encoding="utf-8"))
    report = build_report(
        registry,
        exhibition,
        args.source_dir,
        max_points=args.max_points,
        allow_missing=args.allow_missing,
    )
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    summary = report["summary"]
    print(
        "Gaussian orientation report: "
        f"registered={summary['registered']} "
        f"basis_accepted={summary['basis_accepted']} "
        f"physical_up={json.dumps(summary['physical_up'], sort_keys=True)} "
        f"decisions={json.dumps(summary['orientation_decisions'], sort_keys=True)} "
        f"artifacts={json.dumps(summary['artifact_verification'], sort_keys=True)}"
    )
    if report["hard_failures"]:
        raise SystemExit("; ".join(report["hard_failures"]))


if __name__ == "__main__":
    main()
