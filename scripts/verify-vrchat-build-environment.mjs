import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const readJson = async (path) => JSON.parse(await readFile(path, 'utf8'));
const contract = await readJson('config/vrchat-build-environment.json');
const toolchain = await readJson(contract.production.toolchain_policy);
const manifest = await readJson(contract.production.package_manifest);
const vpm = await readJson(contract.production.vpm_lock);
const projectVersion = await readFile(contract.production.unity_version_source, 'utf8');

assert.equal(contract.schema_version, 1);
assert.equal(contract.future_rnd.unity_6_allowed_in_production, false);
assert.equal(contract.future_rnd.production_version_must_match_toolchain_policy, true);
assert.match(projectVersion, new RegExp(`^m_EditorVersion:\\s*${toolchain.unityVersion.replaceAll('.', '\\.')}$`, 'm'));
assert.equal(contract.production.dependency_resolution, 'node scripts/verify-vpm.mjs');
assert.equal(contract.production.compile_and_sdk_validation, 'node scripts/run-perspective-cage-unity.mjs');
assert.equal(contract.evidence.vpm, '.artifacts/vpm-u1.json');
assert.equal(contract.evidence.ci_artifact, 'vrchat-build-environment-u1');

for (const id of ['com.vrchat.base', 'com.vrchat.worlds']) {
  const declared = typeof vpm.dependencies?.[id] === 'string' ? vpm.dependencies[id] : vpm.dependencies?.[id]?.version;
  const locked = typeof vpm.locked?.[id] === 'string' ? vpm.locked[id] : vpm.locked?.[id]?.version;
  const installed = typeof manifest.dependencies?.[id] === 'string' ? manifest.dependencies[id] : manifest.dependencies?.[id]?.version;
  assert.equal(declared, toolchain.vrchatSdkVersion, `${id} declared version drift`);
  assert.equal(locked, toolchain.vrchatSdkVersion, `${id} lock version drift`);
  assert.equal(installed, toolchain.vrchatSdkVersion, `${id} manifest version drift`);
}

console.log(`VRChat build environment contract: PASS (Unity ${toolchain.unityVersion}, SDK ${toolchain.vrchatSdkVersion})`);
