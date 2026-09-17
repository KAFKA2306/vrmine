#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {validateVisualVerification} from './validate-astra-visual-verification.mjs';

function fail(message) { throw new Error(`visual repair loop: ${message}`); }
function readEvidence(file) {
  const absolute = path.resolve(file);
  const data = JSON.parse(fs.readFileSync(absolute, 'utf8'));
  return {absolute, data, result: validateVisualVerification(data, path.dirname(absolute))};
}
function run(command, args, label) {
  const result = spawnSync(command, args, {stdio: 'inherit'});
  if (result.error) fail(`${label} failed to start: ${result.error.message}`);
  if (result.status !== 0) fail(`${label} exited ${result.status}`);
}

export function runVisualRepairLoop({initialEvidence, repairCommand, repairArgs = [], rerenderCommand, rerenderArgs = [], repairedEvidence}) {
  const initial = readEvidence(initialEvidence);
  if (initial.result.verdict === 'PASS') return {verdict: 'PASS', repaired: false, attempts: 0};
  if (!initial.data.repair_required || initial.data.repair_targets.length === 0) fail('FAIL evidence has no repair contract');
  if (!repairCommand || !rerenderCommand || !repairedEvidence) fail('repair, rerender and repaired evidence are required after FAIL');

  run(repairCommand, [...repairArgs, ...initial.data.repair_targets], 'repair');
  run(rerenderCommand, rerenderArgs, 'rerender');
  const repaired = readEvidence(repairedEvidence);
  if (repaired.data.artifact_sha256 === initial.data.artifact_sha256) fail('repair did not produce a new artifact revision');
  if (repaired.result.verdict !== 'PASS') fail(`repaired evidence remains ${repaired.result.verdict}`);
  return {verdict: 'PASS', repaired: true, attempts: 1, repaired_artifact_sha256: repaired.data.artifact_sha256};
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(new URL(import.meta.url).pathname)) {
  const [initialEvidence, repairCommand, rerenderCommand, repairedEvidence] = process.argv.slice(2);
  if (!initialEvidence) fail('usage: run-astra-visual-repair-loop.mjs <initial.json> <repair-command> <rerender-command> <repaired.json>');
  const result = runVisualRepairLoop({initialEvidence, repairCommand, rerenderCommand, repairedEvidence});
  process.stdout.write(`${JSON.stringify(result)}\n`);
}
