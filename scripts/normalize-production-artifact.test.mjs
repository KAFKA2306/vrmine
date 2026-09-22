import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const script = path.resolve('scripts/normalize-production-artifact.mjs');

test('normalization preserves raw input and records exact before/after evidence', () => {
  const root = mkdtempSync(path.join(os.tmpdir(), 'vrmine-normalize-'));
  const raw = path.join(root, 'raw', 'asset.glb');
  const normalized = path.join(root, 'normalized', 'asset.glb');
  mkdirSync(path.dirname(raw), {recursive: true});
  writeFileSync(raw, Buffer.from('glTF deterministic fixture'));

  const run = spawnSync(process.execPath, [script, root, raw, normalized], {encoding: 'utf8'});
  assert.equal(run.status, 0, run.stderr);
  assert.equal(readFileSync(raw, 'utf8'), 'glTF deterministic fixture');
  assert.equal(readFileSync(normalized, 'utf8'), 'glTF deterministic fixture');

  const evidence = JSON.parse(readFileSync(`${normalized}.normalization.json`, 'utf8'));
  assert.equal(evidence.status, 'PASS');
  assert.equal(evidence.input.path, 'raw/asset.glb');
  assert.equal(evidence.output.path, 'normalized/asset.glb');
  assert.equal(evidence.input.sha256, evidence.output.sha256);
  assert.equal(evidence.input.bytes, evidence.output.bytes);
  assert.equal(evidence.contentChanged, false);
});
