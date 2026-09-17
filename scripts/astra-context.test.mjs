import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const spec = "config/world-items/cafe-bar-stool-01.json";
const compile = (script) => JSON.parse(execFileSync(process.execPath, [script, spec], { cwd: root, encoding: "utf8" }));
const request = compile("scripts/compile-astra-build-request.mjs");
const first = compile("scripts/compile-astra-context.mjs");
const second = compile("scripts/compile-astra-context.mjs");

assert.deepEqual(first, second, "execution context must be deterministic");
assert.equal(first.kind, "astra_execution_context");
assert.equal(first.authority.canonical_spec, request.source.canonical_spec);
assert.equal(first.authority.canonical_spec_sha256, request.source.sha256);
assert.deepEqual(first.inputs.intent, request.intent);
assert.equal(first.inputs.deterministic_seed, request.constraints.deterministic_seed);
assert.deepEqual(first.inputs.license, request.constraints.license);
assert.deepEqual(first.completion_contract.required_artifacts, request.required_artifacts);
assert.deepEqual(first.completion_contract.verification, request.verification);
assert.deepEqual(first.control.priority, ["code_cli", "blender_bpy_geometry_nodes", "mcp", "unity_batchmode", "cua"]);
assert.equal(first.constraints.manual_mesh_edit_as_authority, false);
assert.equal(first.constraints.one_responsibility_one_authority, true);
assert.equal(first.completion_contract.verification.platforms.android, "UNVERIFIED");
console.log("astra execution context contract: PASS");
