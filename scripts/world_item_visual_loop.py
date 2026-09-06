#!/usr/bin/env python3
"""Evidence-bound visual iteration helpers for World Item Factory."""
from __future__ import annotations
import argparse, copy, hashlib, json
from pathlib import Path
from typing import Any, Mapping

VIEWS = ("hero", "front", "rear", "left", "right", "top")
PART_FIELDS = {"size", "radius", "height", "position", "rotation_deg", "vertices"}
MATERIAL_FIELDS = {"base_color", "roughness", "metallic"}

def sha256_file(path: str | Path) -> str:
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def stable_sha256(value: Any) -> str:
    raw = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()
    return hashlib.sha256(raw).hexdigest()

def read_object(path: str | Path) -> dict[str, Any]:
    value = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value

def write_object(path: str | Path, value: Mapping[str, Any]) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(dict(value), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def _bundle_digest(bundle: Mapping[str, Any]) -> str:
    return stable_sha256({k: v for k, v in bundle.items() if k != "bundleSha256"})

def render_protocol(manifest: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "blender": manifest.get("blender"),
        "engine": "BLENDER_EEVEE_NEXT",
        "camera": "ORTHO",
        "resolution": [640, 640],
        "frameFill": 0.84,
        "views": list(VIEWS),
    }

def build_review_bundle(spec_path: str | Path, render_root: str | Path, *, previous_bundle_path: str | Path | None = None) -> dict[str, Any]:
    spec_path, root = Path(spec_path), Path(render_root)
    spec, manifest = read_object(spec_path), read_object(root / "manifest.json")
    sku = str(spec.get("id") or "")
    if not sku or manifest.get("id") != sku:
        raise ValueError("spec/manifest product identity mismatch")
    spec_sha = sha256_file(spec_path)
    if manifest.get("spec_sha256") != spec_sha:
        raise ValueError("manifest does not match current spec SHA-256")
    framing = manifest.get("render_framing")
    if not isinstance(framing, dict) or set(framing) != set(VIEWS):
        raise ValueError("manifest render_framing is incomplete")
    renders = []
    for view in VIEWS:
        name, path = f"view-{view}.png", root / f"view-{view}.png"
        if not path.is_file():
            raise FileNotFoundError(path)
        actual = sha256_file(path)
        if (manifest.get("sha256") or {}).get(name) != actual:
            raise ValueError(f"stale or tampered render: {name}")
        renders.append({"view": view, "path": path.as_posix(), "sha256": actual, "framing": framing[view]})
    candidate_sha = (manifest.get("sha256") or {}).get(f"{sku}.glb")
    if not isinstance(candidate_sha, str) or len(candidate_sha) != 64:
        raise ValueError("candidate GLB hash is missing")
    protocol = render_protocol(manifest)
    bundle: dict[str, Any] = {
        "schemaVersion": 1,
        "productId": sku,
        "spec": {"path": spec_path.as_posix(), "sha256": spec_sha},
        "candidateArtifact": {"name": f"{sku}.glb", "sha256": candidate_sha},
        "renderProtocol": protocol,
        "renderProtocolSha256": stable_sha256(protocol),
        "renders": renders,
        "geometry": {"dimensionsM": manifest.get("dimensions_m_actual"), "triangles": manifest.get("triangles"), "partsCount": manifest.get("parts_count")},
        "previousBundle": None,
    }
    if previous_bundle_path:
        previous_path = Path(previous_bundle_path)
        previous = read_object(previous_path)
        if previous.get("productId") != sku or previous.get("renderProtocolSha256") != bundle["renderProtocolSha256"]:
            raise ValueError("previous bundle identity/protocol mismatch")
        if previous.get("bundleSha256") != _bundle_digest(previous):
            raise ValueError("previous bundle hash mismatch")
        bundle["previousBundle"] = {"path": previous_path.as_posix(), "sha256": sha256_file(previous_path), "bundleSha256": previous["bundleSha256"]}
    bundle["bundleSha256"] = _bundle_digest(bundle)
    return bundle

def _validate_patch(path: str, value: Any, spec: Mapping[str, Any]) -> None:
    parts = [p for p in path.split("/") if p]
    if len(parts) != 3:
        raise ValueError(f"patch path must target one editable leaf: {path}")
    root, key, field = parts
    if root == "parts":
        try:
            index = int(key)
        except ValueError as exc:
            raise ValueError(f"part patch index must be integer: {path}") from exc
        source = spec.get("parts")
        if not isinstance(source, list) or not 0 <= index < len(source):
            raise ValueError(f"part patch index is out of range: {path}")
        if field not in PART_FIELDS:
            raise ValueError(f"part field is not visually editable: {field}")
    elif root == "materials":
        source = spec.get("materials")
        if not isinstance(source, dict) or key not in source or field not in MATERIAL_FIELDS:
            raise ValueError(f"material patch is not allowed: {path}")
    else:
        raise ValueError(f"patch root is not allowed: {root}")
    if isinstance(value, bool) or not isinstance(value, (int, float, list)):
        raise ValueError(f"patch value must be numeric or numeric list: {path}")
    if isinstance(value, list) and (not value or any(isinstance(x, bool) or not isinstance(x, (int, float)) for x in value)):
        raise ValueError(f"patch list must contain only numbers: {path}")

def validate_review(review: Mapping[str, Any], bundle: Mapping[str, Any], spec: Mapping[str, Any]) -> None:
    expected = {
        "schemaVersion": 1,
        "productId": bundle["productId"],
        "bundleSha256": bundle["bundleSha256"],
        "candidateArtifactSha256": bundle["candidateArtifact"]["sha256"],
        "renderProtocolSha256": bundle["renderProtocolSha256"],
    }
    for key, value in expected.items():
        if review.get(key) != value:
            raise ValueError(f"review binding mismatch: {key}")
    if review.get("status") not in {"PASS", "FAIL", "NOT_ASSESSABLE"}:
        raise ValueError("review status is invalid")
    if review.get("decision") not in {"ACCEPT", "REVISE", "REJECT", "STOP"}:
        raise ValueError("review decision is invalid")
    if review.get("status") != "PASS" and review.get("decision") == "ACCEPT":
        raise ValueError("only PASS may ACCEPT a candidate")
    findings = review.get("findings", [])
    if not isinstance(findings, list):
        raise ValueError("findings must be a list")
    for i, finding in enumerate(findings):
        if not isinstance(finding, dict) or finding.get("view") not in VIEWS:
            raise ValueError(f"finding[{i}] is invalid")
        if finding.get("severity") not in {"INFO", "MINOR", "MAJOR", "BLOCKING"}:
            raise ValueError(f"finding[{i}].severity is invalid")
        confidence = finding.get("confidence")
        if isinstance(confidence, bool) or not isinstance(confidence, (int, float)) or not 0 <= confidence <= 1:
            raise ValueError(f"finding[{i}].confidence must be between 0 and 1")
        for field in ("observedDefect", "probableCause", "recommendedChange"):
            if not isinstance(finding.get(field), str) or not finding[field].strip():
                raise ValueError(f"finding[{i}].{field} is required")
    patches = review.get("patches", [])
    if not isinstance(patches, list) or (review.get("decision") == "REVISE" and not patches):
        raise ValueError("REVISE requires spec patches")
    for i, patch in enumerate(patches):
        if not isinstance(patch, dict) or not isinstance(patch.get("path"), str):
            raise ValueError(f"patch[{i}] is invalid")
        _validate_patch(patch["path"], patch.get("value"), spec)

def apply_review_patches(spec: Mapping[str, Any], review: Mapping[str, Any], bundle: Mapping[str, Any]) -> dict[str, Any]:
    validate_review(review, bundle, spec)
    if review.get("decision") != "REVISE":
        raise ValueError("only REVISE may materialize a candidate spec")
    candidate = copy.deepcopy(dict(spec))
    for patch in review["patches"]:
        root, key, field = [p for p in patch["path"].split("/") if p]
        if root == "parts":
            candidate["parts"][int(key)][field] = patch["value"]
        else:
            candidate["materials"][key][field] = patch["value"]
    return candidate

def decide_last_good(before: Mapping[str, Any], after: Mapping[str, Any], comparison: Mapping[str, Any], *, iteration: int, max_iterations: int) -> dict[str, Any]:
    if before.get("productId") != after.get("productId") or before.get("renderProtocolSha256") != after.get("renderProtocolSha256"):
        raise ValueError("before/after identity or render protocol mismatch")
    for name, bundle in (("before", before), ("after", after)):
        if bundle.get("bundleSha256") != _bundle_digest(bundle):
            raise ValueError(f"{name} bundle hash mismatch")
    if comparison.get("beforeBundleSha256") != before["bundleSha256"] or comparison.get("afterBundleSha256") != after["bundleSha256"]:
        raise ValueError("comparison is stale")
    verdict = comparison.get("verdict")
    if verdict not in {"IMPROVED", "REGRESSED", "UNCHANGED", "NOT_ASSESSABLE"}:
        raise ValueError("comparison verdict is invalid")
    if not 1 <= iteration <= max_iterations:
        raise ValueError("invalid iteration bounds")
    adopted = verdict == "IMPROVED"
    status = "ADOPTED" if adopted else ("STOPPED_LIMIT" if iteration == max_iterations else "REVERTED")
    last_good = after if adopted else before
    return {"schemaVersion": 1, "productId": before["productId"], "iteration": iteration, "maxIterations": max_iterations, "verdict": verdict, "status": status, "lastGoodBundleSha256": last_good["bundleSha256"], "candidateBundleSha256": after["bundleSha256"]}

def main() -> int:
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("bundle"); p.add_argument("--spec", required=True); p.add_argument("--render-root", required=True); p.add_argument("--output", required=True); p.add_argument("--previous")
    p = sub.add_parser("apply"); p.add_argument("--spec", required=True); p.add_argument("--bundle", required=True); p.add_argument("--review", required=True); p.add_argument("--output", required=True)
    p = sub.add_parser("decide"); p.add_argument("--before", required=True); p.add_argument("--after", required=True); p.add_argument("--comparison", required=True); p.add_argument("--iteration", type=int, required=True); p.add_argument("--max-iterations", type=int, required=True); p.add_argument("--output", required=True)
    args = parser.parse_args()
    if args.command == "bundle":
        result = build_review_bundle(args.spec, args.render_root, previous_bundle_path=args.previous)
    elif args.command == "apply":
        result = apply_review_patches(read_object(args.spec), read_object(args.review), read_object(args.bundle))
    else:
        result = decide_last_good(read_object(args.before), read_object(args.after), read_object(args.comparison), iteration=args.iteration, max_iterations=args.max_iterations)
    write_object(args.output, result)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
