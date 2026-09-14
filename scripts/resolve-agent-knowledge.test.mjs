import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  loadKnowledgePolicy,
  resolveKnowledge,
  resolveKnowledgeClaim,
  resolveProjectVersions
} from './resolve-agent-knowledge.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function nextMinor(version) {
  const parts = String(version).match(/\d+/g)?.map(Number) ?? [];
  assert.ok(parts.length >= 2, `expected major.minor version, got ${version}`);
  return `${parts[0]}.${parts[1] + 1}.0`;
}

test('knowledge policy resolves canonical project versions without duplicating pinned values', () => {
  const policy = loadKnowledgePolicy(root);
  const versions = resolveProjectVersions(root, policy);

  assert.match(versions.unity, /^\d+\.\d+\.\d+[a-z]\d+$/i);
  assert.match(versions.vrchat_sdk, /^\d+\.\d+\.\d+$/);
  assert.match(versions.blender, /^\d+\.\d+$/);
  assert.deepEqual(Object.values(versions.vrchat_packages), [versions.vrchat_sdk, versions.vrchat_sdk]);

  for (const source of Object.values(policy.version_sources)) {
    assert.equal(Object.hasOwn(source, 'version'), false, `${source.path} must remain the version authority`);
  }
});

test('knowledge precedence keeps project facts above external guidance', () => {
  const resolved = resolveKnowledge(root);
  assert.deepEqual(resolved.authority_order, [
    'project_facts',
    'version_matched_official_docs',
    'specialized_skills',
    'general_knowledge',
    'runtime_evidence'
  ]);
});

test('compatible external knowledge may proceed', () => {
  const versions = resolveProjectVersions(root);
  const result = resolveKnowledgeClaim(versions, {
    tool: 'vrchat_sdk',
    exact_version: versions.vrchat_sdk
  });
  assert.equal(result.status, 'compatible');
  assert.deepEqual(result.reasons, []);
});

test('newer version-specific external knowledge is explicitly incompatible', () => {
  const versions = resolveProjectVersions(root);
  const result = resolveKnowledgeClaim(versions, {
    tool: 'vrchat_sdk',
    minimum_version: nextMinor(versions.vrchat_sdk)
  });
  assert.equal(result.status, 'incompatible');
  assert.equal(result.reasons.length, 1);
  assert.match(result.reasons[0], /project pins/);
});
