import assert from 'node:assert/strict';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const sha = 'a'.repeat(64);
const sqrtHalf = Math.sqrt(0.5);
const producerRevision = '1d48110c8abd891d7b0a19f9e6ce793901758742';
const artifactManifest = 'config/gaussian-artifacts.yaml';
const rendererRevision = 'f96c0117cba518ff84d059d36f16909b873e23aa';
const renderer = `MichaelMoroz/VRChatGaussianSplatting@${rendererRevision}`;
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
    revision: rendererRevision,
    mode: 'horizon_alignment_pre_y_reflection',
    quaternion_xyzw: [sqrtHalf, 0, 0, sqrtHalf],
    pivot: [0, 0, 0],
    mandatory_post_transform: 'reflect-y',
    representation_aware: ['position', 'gaussian_rotation', 'spherical_harmonics'],
  },
};

const basisContract = {
  schema_version: 1,
  status: 'accepted',
  scope: 'coordinate_basis_only',
  producer: {
    repository: 'KAFKA2306/AutoPhotogrammetry',
    revision: producerRevision,
    nerfstudio_revision: '50e0e3c70c775e89333256213363badbf074f29d',
    coordinate_frame: 'nerfstudio-model-basis',
  },
  artifact_manifest: artifactManifest,
  canonical_frame: { name: 'unity-basis-y-up', physical_gravity_claimed: false },
  physical_up: {
    status: 'review_required',
    observable_from_sfm_alone: false,
    authority: null,
  },
  consumer_application: acceptedBasisOrientation.consumer_application,
};

function registry(orientation = acceptedBasisOrientation) {
  return {
    schema_version: 1,
    environments: [{ id: 'fixture', source: { sha256: sha, orientation } }],
  };
}

function contractedRegistry({ sourceArtifactManifest = artifactManifest } = {}) {
  return {
    schema_version: 2,
    source_repository: 'KAFKA2306/AutoPhotogrammetry',
    source_commit: '6f47684aa9a1b46cf2cd75cbef3c36031f220c59',
    environments: [
      {
        id: 'contract-fixture',
        source: {
          sha256: sha,
          artifact_manifest: sourceArtifactManifest,
        },
      },
    ],
  };
}

const exhibition = {
  renderer,
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
  const contractedExhibition = { ...exhibition, import_overrides: [] };
  const result = compileProducerOrientation(contractedRegistry(), contractedExhibition, { basisContract });
  assert.equal(result.compiled_count, 1);
  assert.equal(result.unresolved.length, 0);
  assert.equal(result.authorities['contract-fixture'], 'artifact-set-basis-contract');
  assert.equal(result.physical_up_counts.review_required, 1);
  const override = result.exhibition.import_overrides[0];
  assert.equal(override.alignment.scope, 'coordinate_basis_only');
  assert.equal(override.alignment.physicalUpStatus, 'review_required');
  assert.equal(override.alignment.authority, 'artifact-set-basis-contract');
  assert.ok(Math.abs(override.alignment.rotation.x - sqrtHalf) < 1e-12);
  assert.ok(Math.abs(override.alignment.rotation.w - sqrtHalf) < 1e-12);
}

{
  const contractedExhibition = { ...exhibition, import_overrides: [] };
  const invalidRevisionContract = {
    ...basisContract,
    producer: { ...basisContract.producer, revision: 'not-a-commit' },
  };
  assert.throws(
    () => compileProducerOrientation(contractedRegistry(), contractedExhibition, { basisContract: invalidRevisionContract }),
    /immutable commit SHA/,
  );
  assert.throws(
    () => compileProducerOrientation(contractedRegistry({ sourceArtifactManifest: 'config/other-artifacts.yaml' }), contractedExhibition, { basisContract }),
    /artifact manifest/,
  );
  assert.throws(
    () => compileProducerOrientation(contractedRegistry(), contractedExhibition),
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
