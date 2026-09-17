import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "vrmine-ci-evidence-"));
const output = path.join(dir, "evidence.json");
const sha = "a".repeat(40);
const env = {
  ...process.env,
  GITHUB_SHA: sha,
  EXPECTED_HEAD_SHA: sha,
  GITHUB_REPOSITORY: "KAFKA2306/vrmine",
  GITHUB_RUN_ID: "123",
  GITHUB_RUN_ATTEMPT: "2"
};
let result = spawnSync(process.execPath, ["scripts/write-exact-head-ci-evidence.mjs", output], { env, encoding: "utf8" });
assert.equal(result.status, 0, result.stderr);
assert.deepEqual(JSON.parse(fs.readFileSync(output, "utf8")), {
  schema_version: 1,
  repository: "KAFKA2306/vrmine",
  exact_head_sha: sha,
  run_id: "123",
  run_attempt: 2,
  merge_gate: "PASS"
});
result = spawnSync(process.execPath, ["scripts/write-exact-head-ci-evidence.mjs", output], {
  env: { ...env, EXPECTED_HEAD_SHA: "b".repeat(40) }, encoding: "utf8"
});
assert.notEqual(result.status, 0);
assert.match(result.stderr, /does not match expected head/);
fs.rmSync(dir, { recursive: true, force: true });
console.log("exact-head CI evidence contract: PASS");
