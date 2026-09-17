#!/usr/bin/env node
import assert from 'node:assert/strict';
import { selectControl } from './select-astra-control.mjs';

const policy = {
  control_priority: ['code_cli', 'blender_bpy_geometry_nodes', 'mcp', 'unity_batchmode', 'cua'],
};

assert.equal(selectControl(['cua', 'mcp', 'code_cli'], policy).selected, 'code_cli');
assert.equal(selectControl(['cua', 'unity_batchmode'], policy).selected, 'unity_batchmode');
assert.equal(selectControl(['cua'], policy).selected, 'cua');
assert.deepEqual(
  selectControl(['mcp', 'mcp', 'cua'], policy).rejected_parallel_authorities,
  ['cua'],
);
assert.throws(() => selectControl([], policy), /non-empty/);
assert.throws(() => selectControl(['manual'], policy), /unknown controls/);

console.log('astra control selector contract: PASS');
