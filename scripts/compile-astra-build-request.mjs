#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const [specArg, outputArg] = process.argv.slice(2);
if (!specArg) {
  console.error("usage: node scripts/compile-astra-build-request.mjs <config/world-items/*.json> [output.json]");
  process.exit(2);
}

const root = process.cwd();
const specPath = path.resolve(root, specArg);
const relativeSpec = path.relative(root, specPath).replaceAll("\\", "/");
if (!relativeSpec.startsWith("config/world-items/") || !relativeSpec.endsWith(".json")) {
  throw new Error("canonical spec must be a JSON file under config/world-items/");
}

const raw = fs.readFileSync(specPath, "utf8");
const spec = JSON.parse(raw);
for (const key of ["id", "family", "display_name", "dimensions_m", "parts", "materials", "license"]) {
  if (!(key in spec)) throw new Error(`canonical spec missing required field: ${key}`);
}
if (!Array.isArray(spec.dimensions_m) || spec.dimensions_m.length !== 3 || spec.dimensions_m.some((v) => !Number.isFinite(v) || v <= 0)) {
  throw new Error("dimensions_m must contain three positive numbers");
}
if (!Array.isArray(spec.parts) || spec.parts.length === 0) throw new Error("parts must be a non-empty array");
if (!spec.license?.provenance || !spec.license?.status) throw new Error("license provenance/status are required");

const seed = Number.parseInt(crypto.createHash("sha256").update(raw).digest("hex").slice(0, 8), 16);
const request = {
  schema_version: 1,
  kind: "astra_build_request",
  source: {
    canonical_spec: relativeSpec,
    sha256: crypto.createHash("sha256").update(raw).digest("hex")
  },
  intent: {
    id: spec.id,
    family: spec.family,
    display_name: spec.display_name,
    dimensions_m: spec.dimensions_m,
    parts: spec.parts,
    materials: spec.materials
  },
  constraints: {
    deterministic_seed: seed,
    generation_method: "procedural",
    canonical_source: ["bpy", "geometry_nodes"],
    manual_mesh_edit_as_authority: false,
    preserve_dimensions: true,
    external_asset_policy: "licensed_and_provenance_required",
    license: spec.license
  },
  required_artifacts: {
    source: ["generator", "parameters", "seed"],
    models: spec.formats ?? ["blend", "glb"],
    renders: ["front_3_4", "rear_3_4", "left", "right", "top", "geometry_diagnostic"],
    metadata: ["manifest", "toolchain_manifest", "provenance"]
  },
  verification: {
    independent_static_gate: true,
    visual_loop: { required: true, repair_on_fail: true, rerender_after_repair: true },
    unity: { version_source: "ProjectSettings/ProjectVersion.txt", status: "UNVERIFIED" },
    vrchat: { status: "UNVERIFIED" },
    platforms: { pc: "UNVERIFIED", android: "UNVERIFIED" }
  },
  toolchain_policy: {
    production_tier: "P0",
    control_priority: ["code_cli", "blender_bpy_geometry_nodes", "mcp", "unity_batchmode", "cua"],
    one_responsibility_one_authority: true
  }
};

const text = `${JSON.stringify(request, null, 2)}\n`;
if (outputArg) {
  const outputPath = path.resolve(root, outputArg);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, text);
} else {
  process.stdout.write(text);
}
