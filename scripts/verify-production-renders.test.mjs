import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { verifyProductionRenders } from './verify-production-renders.mjs';

test('render verifier requires every expected render to exist and be non-empty', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'vrmine-renders-'));
  try {
    fs.writeFileSync(path.join(root, 'hero.png'), 'evidence');
    assert.equal(verifyProductionRenders(root, ['hero.png']).status, 'PASS');
    assert.throws(() => verifyProductionRenders(root, ['missing.png']), /ENOENT/);
    fs.writeFileSync(path.join(root, 'empty.png'), '');
    assert.throws(() => verifyProductionRenders(root, ['empty.png']), /empty render evidence/);
    assert.throws(() => verifyProductionRenders(root, ['../escape.png']), /invalid render name/);
  } finally {
    fs.rmSync(root, {recursive: true, force: true});
  }
});
