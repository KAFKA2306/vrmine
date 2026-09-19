#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const POLICY = path.join(ROOT, 'config', 'astra-toolchain-policy.json');

export function selectControl(available, policy = JSON.parse(fs.readFileSync(POLICY, 'utf8'))) {
  if (!Array.isArray(available) || available.length === 0) {
    throw new Error('available controls must be a non-empty array');
  }
  const known = new Set(policy.control_priority);
  const unknown = available.filter((control) => !known.has(control));
  if (unknown.length) throw new Error(`unknown controls: ${unknown.join(', ')}`);
  const selected = policy.control_priority.find((control) => available.includes(control));
  if (!selected) throw new Error('no selectable control');
  return {
    selected,
    available: [...new Set(available)],
    priority: policy.control_priority,
    rejected_parallel_authorities: [...new Set(available)].filter((control) => control !== selected),
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const available = process.argv.slice(2);
  try {
    process.stdout.write(`${JSON.stringify(selectControl(available), null, 2)}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
