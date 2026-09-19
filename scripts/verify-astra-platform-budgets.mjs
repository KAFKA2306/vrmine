import assert from 'node:assert/strict';
import fs from 'node:fs';

const policy = JSON.parse(fs.readFileSync(new URL('../config/astra-platform-budgets.json', import.meta.url)));
assert.equal(policy.schema_version, 1);
assert.deepEqual(Object.keys(policy.platforms).sort(), ['android', 'pc']);

for (const [platform, budget] of Object.entries(policy.platforms)) {
  assert.equal(budget.runtime_status, 'UNVERIFIED', `${platform} must not claim unexecuted runtime evidence`);
  assert.equal(budget.texture_memory.target, 'good', `${platform} must declare a texture-memory target`);
  assert.ok(Object.hasOwn(budget, 'physbone_affected_transforms'), `${platform} must declare a PhysBone budget contract`);
}

assert.equal(policy.platforms.pc.physbone_affected_transforms.hard_limit, null, 'PC must not inherit Android hard limits');
assert.equal(policy.platforms.android.physbone_affected_transforms.hard_limit, 64, 'Android PhysBone hard limit must remain explicit');
assert.notDeepEqual(policy.platforms.pc, policy.platforms.android, 'PC and Android contracts must remain independently expressible');

console.log('Astra platform budget contract: PASS');
