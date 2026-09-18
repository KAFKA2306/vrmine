#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const CHECKS = ["scale_dimensions", "floating_parts", "mesh_intersection", "topology", "material_assignment", "texture_uv", "rig_joints", "colliders", "camera_clipping", "layout_obstruction"];
const SHA256 = /^[0-9a-f]{64}$/;

function fail(message) { throw new Error(`astra visual repair loop: ${message}`); }
function readVerdict(file) {
  const data = JSON.parse(fs.readFileSync(path.resolve(file), "utf8"));
  if (data.kind !== "astra_visual_verdict" || data.schema_version !== 1) fail("invalid verdict identity");
  if (!SHA256.test(data.artifact_sha256 ?? "")) fail("invalid artifact sha256");
  if (!data.render_sha256 || !Object.keys(data.render_sha256).length || Object.values(data.render_sha256).some((v) => !SHA256.test(v))) fail("invalid render sha256 map");
  const failed = CHECKS.filter((name) => data.checks?.[name]?.status === "FAIL");
  if (CHECKS.some((name) => !["PASS", "FAIL", "NOT_APPLICABLE"].includes(data.checks?.[name]?.status))) fail("missing or invalid visual check");
  const expected = failed.length ? "FAIL" : "PASS";
  if (data.verdict !== expected) fail(`verdict contradicts checks: expected ${expected}`);
  if (expected === "FAIL" && (!data.repair?.required || !data.repair.instructions?.length)) fail("FAIL verdict requires repair instructions");
  if (expected === "PASS" && (data.repair?.required || data.repair?.instructions?.length)) fail("PASS verdict cannot request repair");
  return data;
}
function defaultExecutor(command) {
  if (!Array.isArray(command) || !command.length || command.some((v) => typeof v !== "string" || !v)) fail("command must be a non-empty JSON string array");
  const result = spawnSync(command[0], command.slice(1), { stdio: "inherit" });
  if (result.error) fail(`command failed to start: ${result.error.message}`);
  if (result.status !== 0) fail(`command exited ${result.status}`);
}

export function runVisualRepairLoop({ initialEvidence, repairedEvidence, repairCommand, rerenderCommand, executor = defaultExecutor }) {
  const initial = readVerdict(initialEvidence);
  if (initial.verdict === "PASS") return { verdict: "PASS", repaired: false, attempts: 0, artifact_sha256: initial.artifact_sha256 };
  if (!repairedEvidence || !repairCommand || !rerenderCommand) fail("FAIL requires repair command, rerender command, and repaired evidence path");
  executor([...repairCommand, ...initial.repair.instructions]);
  executor(rerenderCommand);
  const repaired = readVerdict(repairedEvidence);
  if (repaired.artifact_sha256 === initial.artifact_sha256) fail("repair did not produce a new artifact revision");
  if (JSON.stringify(repaired.render_sha256) === JSON.stringify(initial.render_sha256)) fail("rerender did not produce new render evidence");
  if (repaired.verdict !== "PASS") fail("one repair attempt completed but visual verdict is still FAIL");
  return { verdict: "PASS", repaired: true, attempts: 1, artifact_sha256: repaired.artifact_sha256 };
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname)) {
  const args = Object.fromEntries(process.argv.slice(2).map((arg) => { const i = arg.indexOf("="); return i < 0 ? [arg, true] : [arg.slice(0, i), arg.slice(i + 1)]; }));
  if (!args["--initial"]) fail("usage: --initial=<json> [--repaired=<json> --repair-json='[...]' --rerender-json='[...]']");
  const result = runVisualRepairLoop({ initialEvidence: args["--initial"], repairedEvidence: args["--repaired"], repairCommand: args["--repair-json"] ? JSON.parse(args["--repair-json"]) : null, rerenderCommand: args["--rerender-json"] ? JSON.parse(args["--rerender-json"]) : null });
  process.stdout.write(`${JSON.stringify(result)}\n`);
}
