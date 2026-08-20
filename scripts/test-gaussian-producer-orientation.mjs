import assert from 'node:assert/strict';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const sha = 'a'.repeat(64);
const sqrtHalf = Math.sqrt(0.5);
const legacyRevision = '1d48110c8abd891d7b0a19f9e6ce793901758742';
const acceptedBasisOrientation = {
  schema_version: 2,
  status: 'accepted',
  scope: 'coordinate_basis_only',
  ply_sha256: sha,
  canonical_frame: { name: 'unity-basis-y-up', physical_gravity_claimed: false },
  physical_up: {
    status: 'review_required',
    observable_from_sfm_alone: false,
    authority: null,
  },
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

function registry(orientation = acceptedBasisOrientation) {
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
    environments: [
      {
        id: 'legacy-fixture',
        source: {
          sha256: sha,
          provenance: { artifact_repository_commit: artifactRevision },
        },
      },
    ],
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
  assert.equal(result.physical_up_counts.review_required, 1);
  const override = result.exhibition.import_overrides[0];
  assert.equal(override.id, 'fixture');
  assert.deepEqual(override.crop, { enabled: false });
  assert.equal(override.alignment.enabled, true);
  assert.equal(override.alignment.mode, 'horizon');
  assert.equal(override.alignment.scope, 'coordinate_basis_only');
  assert.equal(override.alignment.physicalUpStatus, 'review_required');
  assert.equal(override.alignment.authority, 'producer-artifact-metadata');
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
  assert.equal(result.authorities['legacy-fixture'], 'audited-legacy-basis');
  assert.equal(result.physical_up_counts.review_required, 1);
  const override = result.exhibition.import_overrides[0];
  assert.equal(override.alignment.scope, 'coordinate_basis_only');
  assert.equal(override.alignment.physicalUpStatus, 'review_required');
  assert.equal(override.alignment.authority, 'audited-legacy-basis');
  assert.ok(Math.abs(override.alignment.rotation.x - sqrtHalf) < 1e-12);
  assert.ok(Math.abs(override.alignment.rotation.w - sqrtHalf) < 1e-12);
}

{
  const legacyExhibition = { ...exhibition, import_overrides: [] };
  assert.throws(
    () => compileProducerOrientation(legacyRegistry({ revision: '0'.repeat(40) }), legacyExhibition),
    /basis unresolved/,
  );
  assert.throws(
    () => compileProducerOrientation(legacyRegistry({ artifactRevision: '0'.repeat(40) }), legacyExhibition),
    /basis unresolved/,
  );
}

{
  const oldSemanticV1 = {
    ...acceptedBasisOrientation,
    schema_version: 1,
    scope: undefined,
    canonical_frame: { name: 'unity-semantic-y-up' },
  };
  assert.throws(() => compileProducerOrientation(registry(oldSemanticV1), exhibition), /v2 basis/);
}

{
  const reviewRequired = { ...acceptedBasisOrientation, status: 'review_required' };
  assert.throws(
    () => compileProducerOrientation(registry(reviewRequired), exhibition),
    /basis unresolved/,
  );
  const allowed = compileProducerOrientation(registry(reviewRequired), exhibition, { requireAll: false });
  assert.equal(allowed.compiled_count, 0);
  assert.equal(allowed.unresolved[0].reason, 'orientation basis status=review_required');
}

{
  const wrongHash = { ...acceptedBasisOrientation, ply_sha256: 'b'.repeat(64) };
  assert.throws(() => compileProducerOrientation(registry(wrongHash), exhibition), /PLY SHA-256/);
}

{
  const wrongRenderer = {
    ...acceptedBasisOrientation,
    consumer_application: { ...acceptedBasisOrientation.consumer_application, revision: '0'.repeat(40) },
  };
  assert.throws(
    () => compileProducerOrientation(registry(wrongRenderer), exhibition),
    /different renderer revision/,
  );
}

{
  const missingSh = {
    ...acceptedBasisOrientation,
    consumer_application: {
      ...acceptedBasisOrientation.consumer_application,
      representation_aware: ['position', 'gaussian_rotation'],
    },
  };
  assert.throws(
    () => compileProducerOrientation(registry(missingSh), exhibition),
    /spherical_harmonics/,
  );
}

console.log('Gaussian producer orientation basis consumer contract PASS');
