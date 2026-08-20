import assert from 'node:assert/strict';

import { compileProducerOrientation } from './compile-gaussian-producer-orientation.mjs';

const sha = 'd'.repeat(64);
const renderer = 'MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa';
const orientation = {
  schema_version: 2,
  status: 'accepted',
  scope: 'coordinate_basis_plus_physical_up',
  ply_sha256: sha,
  canonical_frame: { name: 'unity-basis-y-up', physical_gravity_claimed: true },
  physical_up: {
    status: 'accepted',
    authority_type: 'imu_gravity',
    authority_source: 'fixture://imu.json',
    authority_source_sha256: 'e'.repeat(64),
    evidence_sha256: 'f'.repeat(64),
    model_up_vector: [0, 0.5, 0.8660254037844386],
  },
  consumer_application: {
    consumer: 'MichaelMoroz/VRChatGaussianSplatting',
    revision: 'f96c0117cba518ff84d059d36f16909b873e23aa',
    mode: 'horizon_alignment_pre_y_reflection',
    quaternion_xyzw: [0.5, 0, 0, 0.8660254037844386],
    pivot: [0, 0, 0],
    mandatory_post_transform: 'reflect-y',
    representation_aware: ['position', 'gaussian_rotation', 'spherical_harmonics'],
  },
};

const exhibition = { renderer, import_overrides: [] };
const registry = { environments: [{ id: 'physical-fixture', source: { sha256: sha, orientation } }] };
const result = compileProducerOrientation(registry, exhibition);
assert.equal(result.compiled_count, 1);
assert.equal(result.unresolved.length, 0);
assert.equal(result.physical_up_counts.accepted, 1);
const alignment = result.exhibition.import_overrides[0].alignment;
assert.equal(alignment.scope, 'coordinate_basis_plus_physical_up');
assert.equal(alignment.physicalUpStatus, 'accepted');
assert.equal(alignment.authority, 'producer-physical-up:imu_gravity');
assert.ok(Math.abs(alignment.rotation.x - 0.5) < 1e-12);
assert.ok(Math.abs(alignment.rotation.w - 0.8660254037844386) < 1e-12);

assert.throws(
  () =>
    compileProducerOrientation(
      {
        environments: [
          {
            id: 'bad-status',
            source: {
              sha256: sha,
              orientation: {
                ...orientation,
                physical_up: { ...orientation.physical_up, status: 'review_required' },
              },
            },
          },
        ],
      },
      exhibition,
    ),
  /requires accepted physical_up/,
);

assert.throws(
  () =>
    compileProducerOrientation(
      {
        environments: [
          {
            id: 'hidden-accepted',
            source: { sha256: sha, orientation: { ...orientation, scope: 'coordinate_basis_only' } },
          },
        ],
      },
      exhibition,
    ),
  /cannot be hidden/,
);

assert.throws(
  () =>
    compileProducerOrientation(
      {
        environments: [
          {
            id: 'missing-evidence-hash',
            source: {
              sha256: sha,
              orientation: {
                ...orientation,
                physical_up: { ...orientation.physical_up, evidence_sha256: undefined },
              },
            },
          },
        ],
      },
      exhibition,
    ),
  /authority_type and evidence_sha256/,
);

console.log('Gaussian accepted physical-up consumer contract PASS');
