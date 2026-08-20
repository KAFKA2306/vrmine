import assert from 'node:assert/strict';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const sha = 'a'.repeat(64);
const sqrtHalf = Math.sqrt(0.5);
const legacyRevision = '1d48110c8abd891d7b0a19f9e6ce793901758742';
const acceptedOrientation = {
  schema_version: 1,
  status: 'accepted',
  ply_sha256: sha,
  canonical_frame: { name: 'unity-semantic-y-up' },
  consumer_application: {
    consumer: 'MichaelMoroz/VRChatGaussianSplatting',
    revision: 'f96c0117cba518ff84d059d36f16909b873e23aa',
    mode: 'horizon_alignment_pre_y_reflection',
    quaternion_xyzw: [sqrtHalf, 0, 0, sqrtHalf],
    pivot: [0, 0, 0],
    mandatory_post_transform: 'reflect-y',
    representation_aware: ['position', 'gaussian_rotation', 'spherical_harmonics'],
  },
};

function registry(orientation = acceptedOrientation) {
  return {
    schema_version: 1,
    environments: [{ id: 'fixture', source: { sha256: sha, orientation } }],
  };
}

function legacyRegistry({ revision = legacyRevision, artifactRevision = legacyRevision } = {}) {
  return {
    schema_version: 1,
    source_repository: 'KAFKA2306/AutoPhotogrammetry',
    source_commit: revision,
    environments: [{
      id: 'legacy-fixture',
      source: {
        sha256: sha,
        provenance: { artifact_repository_commit: artifactRevision },
      },
    }],
  };
}

const exhibition = {
  renderer: 'MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa',
  import_overrides: [{ id: 'fixture', crop: { enabled: false } }],
};

{
  const result = compileProducerOrientation(registry(), exhibition);
  assert.equal(result.compiled_count, 1);
  assert.equal(result.unresolved.length, 0);
  assert.equal(result.authorities.fixture, 'producer-artifact-metadata');
  const override = result.exhibition.import_overrides[0];
  assert.equal(override.id, 'fixture');
  assert.deepEqual(override.crop, { enabled: false });
  assert.equal(override.alignment.enabled, true);
  assert.equal(override.alignment.mode, 'horizon');
  assert.ok(Math.abs(override.alignment.rotation.x - sqrtHalf) < 1e-12);
  assert.equal(override.alignment.rotation.y, 0);
  assert.equal(override.alignment.rotation.z, 0);
  assert.ok(Math.abs(override.alignment.rotation.w - sqrtHalf) < 1e-12);
  assert.deepEqual(override.alignment.pivot, { x: 0, y: 0, z: 0 });
}

{
  const legacyExhibition = { ...exhibition, import_overrides: [] };
  const result = compileProducerOrientation(legacyRegistry(), legacyExhibition);
  assert.equal(result.compiled_count, 1);
  assert.equal(result.unresolved.length, 0);
  assert.equal(result.authorities['legacy-fixture'], 'audited-legacy-revision');
  const override = result.exhibition.import_overrides[0];
  assert.equal(override.id, 'legacy-fixture');
  assert.ok(Math.abs(override.alignment.rotation.x - sqrtHalf) < 1e-12);
  assert.ok(Math.abs(override.alignment.rotation.w - sqrtHalf) < 1e-12);
}

{
  const legacyExhibition = { ...exhibition, import_overrides: [] };
  assert.throws(
    () => compileProducerOrientation(legacyRegistry({ revision: '0'.repeat(40) }), legacyExhibition),
    /orientation unresolved/,
  );
  assert.throws(
    () => compileProducerOrientation(legacyRegistry({ artifactRevision: '0'.repeat(40) }), legacyExhibition),
    /orientation unresolved/,
  );
}

{
  const reviewRequired = { ...acceptedOrientation, status: 'review_required' };
  assert.throws(
    () => compileProducerOrientation(registry(reviewRequired), exhibition),
    /Producer orientation unresolved/,
  );
  const allowed = compileProducerOrientation(registry(reviewRequired), exhibition, { requireAll: false });
  assert.equal(allowed.compiled_count, 0);
  assert.equal(allowed.unresolved[0].reason, 'orientation status=review_required');
}

{
  const wrongHash = { ...acceptedOrientation, ply_sha256: 'b'.repeat(64) };
  assert.throws(() => compileProducerOrientation(registry(wrongHash), exhibition), /PLY SHA-256/);
}

{
  const wrongRenderer = {
    ...acceptedOrientation,
    consumer_application: { ...acceptedOrientation.consumer_application, revision: '0'.repeat(40) },
  };
  assert.throws(
    () => compileProducerOrientation(registry(wrongRenderer), exhibition),
    /different renderer revision/,
  );
}

{
  const missingSh = {
    ...acceptedOrientation,
    consumer_application: {
      ...acceptedOrientation.consumer_application,
      representation_aware: ['position', 'gaussian_rotation'],
    },
  };
  assert.throws(
    () => compileProducerOrientation(registry(missingSh), exhibition),
    /spherical_harmonics/,
  );
}

{
  const missing = registry(undefined);
  missing.environments[0].source = { sha256: sha };
  assert.throws(() => compileProducerOrientation(missing, exhibition), /orientation unresolved/);
}

console.log('Gaussian producer orientation consumer contract PASS');
