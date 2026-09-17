#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";

const [outputArg = ".artifacts/ci/exact-head.json"] = process.argv.slice(2);
const required = ["EXACT_HEAD_SHA", "GITHUB_SHA", "GITHUB_REPOSITORY", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT"];
for (const name of required) {
  if (!process.env[name]) throw new Error(`missing CI identity: ${name}`);
}
const sha = process.env.EXACT_HEAD_SHA;
if (!/^[0-9a-f]{40}$/.test(sha)) throw new Error("EXACT_HEAD_SHA must be an exact 40-character commit SHA");

const evidence = {
  schema_version: 1,
  repository: process.env.GITHUB_REPOSITORY,
  exact_head_sha: sha,
  event_sha: process.env.GITHUB_SHA,
  run_id: process.env.GITHUB_RUN_ID,
  run_attempt: Number(process.env.GITHUB_RUN_ATTEMPT),
  merge_gate: "PASS"
};
const output = path.resolve(outputArg);
fs.mkdirSync(path.dirname(output), { recursive: true });
fs.writeFileSync(output, `${JSON.stringify(evidence, null, 2)}\n`);
console.log(`exact-head CI evidence: PASS ${sha}`);
