import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PINNED_RENDERER = 'MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa';
const PINNED_RENDERER_REVISION = 'f96c0117cba518ff84d059d36f16909b873e23aa';
const EXPECTED_CONSUMER = 'MichaelMoroz/VRChatGaussianSplatting';
const EXPECTED_MODE = 'horizon_alignment_pre_y_reflection';
const EXPECTED_POST_TRANSFORM = 'reflect-y';
const EXPECTED_REPRESENTATION_FIELDS = new Set(['position', 'gaussian_rotation', 'spherical_harmonics']);

// This one legacy revision is auditable from repository history:
// - Dockerfile pins Nerfstudio 50e0e3c70c775e89333256213363badbf074f29d.
// - processing/huejotzingo.py invokes ns-process-data without orientation-method override.
// - pinned Nerfstudio's default orientation method is `up`, which defines model +Z as up.
// No other revision is allowed to use this fallback.
const AUDITED_LEGACY_REPOSITORY = 'KAFKA2306/AutoPhotogrammetry';
const AUDITED_LEGACY_REVISION = '1d48110c8abd891d7b0a19f9e6ce793901758742';
const AUDITED_NERFSTUDIO_REVISION = '50e0e3c70c775e89333256213363badbf074f29d';
const SQRT_HALF = Math.SQRT1_2;

function finiteNumber(value) {
  return typeof value === 'number' && Number.isFinite(value);
}

function vector3(values, label) {
  if (!Array.isArray(values) || values.length !== 3 || !values.every(finiteNumber)) {
    throw new Error(`${label} must contain exactly three finite numbers`);
  }
  return { x: values[0], y: values[1], z: values[2] };
}

function quaternion(values, label) {
  if (!Array.isArray(values) || values.length !== 4 || !values.every(finiteNumber)) {
    throw new Error(`${label} must contain exactly four finite numbers`);
  }
  const magnitudeSquared = values.reduce((sum, value) => sum + value * value, 0);
  if (Math.abs(magnitudeSquared - 1) > 1e-6) {
    throw new Error(`${label} must be normalized; magnitude^2=${magnitudeSquared}`);
  }
  return { x: values[0], y: values[1], z: values[2], w: values[3] };
}

function auditedLegacyOrientation(registry, source) {
  if (registry.source_repository !== AUDITED_LEGACY_REPOSITORY) return null;
  if (registry.source_commit !== AUDITED_LEGACY_REVISION) return null;
  if (source?.provenance?.artifact_repository_commit !== AUDITED_LEGACY_REVISION) return null;
  if (!source?.sha256) return null;

  return {
    schema_version: 1,
    status: 'accepted',
    ply_sha256: source.sha256,
    canonical_frame: { name: 'unity-semantic-y-up' },
    derivation_method: 'audited-legacy-source-revision-default-nerfstudio-up',
    audit: {
      repository: AUDITED_LEGACY_REPOSITORY,
      revision: AUDITED_LEGACY_REVISION,
      nerfstudio_revision: AUDITED_NERFSTUDIO_REVISION,
      limitation: 'establishes the Nerfstudio +Z-up model basis; does not prove zero residual physical horizon tilt',
    },
    consumer_application: {
      consumer: EXPECTED_CONSUMER,
      revision: PINNED_RENDERER_REVISION,
      mode: EXPECTED_MODE,
      quaternion_xyzw: [SQRT_HALF, 0, 0, SQRT_HALF],
      pivot: [0, 0, 0],
      mandatory_post_transform: EXPECTED_POST_TRANSFORM,
      representation_aware: ['position', 'gaussian_rotation', 'spherical_harmonics'],
    },
  };
}

function resolveOrientation(registry, source) {
  if (source.orientation) return { orientation: source.orientation, authority: 'producer-artifact-metadata' };
  const legacy = auditedLegacyOrientation(registry, source);
  if (legacy) return { orientation: legacy, authority: 'audited-legacy-revision' };
  return { orientation: null, authority: null };
}

export function compileProducerOrientation(registry, exhibition, { requireAll = true } = {}) {
  if (!registry || !Array.isArray(registry.environments)) {
    throw new Error('Gaussian registry environments are missing');
  }
  if (!exhibition || !Array.isArray(exhibition.import_overrides)) {
    throw new Error('Gaussian exhibition import_overrides are missing');
  }
  if (exhibition.renderer !== PINNED_RENDERER) {
    throw new Error(`Unexpected Gaussian renderer: ${exhibition.renderer}`);
  }

  const existing = new Map(exhibition.import_overrides.map((entry) => [entry.id, entry]));
  const compiled = [];
  const unresolved = [];
  const authorities = {};

  for (const environment of registry.environments) {
    const id = environment?.id;
    const source = environment?.source;
    if (!id || !source?.sha256) {
      throw new Error('Every Gaussian environment requires id and source.sha256');
    }
    const resolved = resolveOrientation(registry, source);
    const orientation = resolved.orientation;
    if (!orientation) {
      unresolved.push({ id, reason: 'orientation metadata missing and no audited legacy revision applies' });
      continue;
    }
    if (orientation.status !== 'accepted') {
      unresolved.push({ id, reason: `orientation status=${orientation.status ?? 'missing'}` });
      continue;
    }
    if (orientation.ply_sha256 !== source.sha256) {
      throw new Error(`${id}: orientation PLY SHA-256 does not match source artifact`);
    }
    if (orientation.canonical_frame?.name !== 'unity-semantic-y-up') {
      throw new Error(`${id}: unsupported canonical frame ${orientation.canonical_frame?.name}`);
    }

    const consumer = orientation.consumer_application;
    if (!consumer || consumer.consumer !== EXPECTED_CONSUMER) {
      throw new Error(`${id}: unsupported orientation consumer`);
    }
    if (consumer.revision !== PINNED_RENDERER_REVISION) {
      throw new Error(`${id}: orientation was derived for a different renderer revision`);
    }
    if (consumer.mode !== EXPECTED_MODE || consumer.mandatory_post_transform !== EXPECTED_POST_TRANSFORM) {
      throw new Error(`${id}: unsupported consumer orientation application mode`);
    }
    const representationAware = new Set(consumer.representation_aware ?? []);
    for (const required of EXPECTED_REPRESENTATION_FIELDS) {
      if (!representationAware.has(required)) {
        throw new Error(`${id}: producer orientation contract is missing representation-aware field ${required}`);
      }
    }

    const previous = existing.get(id) ?? { id };
    compiled.push({
      ...previous,
      id,
      alignment: {
        enabled: true,
        mode: 'horizon',
        rotation: quaternion(consumer.quaternion_xyzw, `${id} consumer quaternion`),
        pivot: vector3(consumer.pivot ?? [0, 0, 0], `${id} consumer pivot`),
      },
    });
    authorities[id] = resolved.authority;
  }

  if (requireAll && unresolved.length > 0) {
    const detail = unresolved.map((entry) => `${entry.id}: ${entry.reason}`).join('; ');
    throw new Error(`Producer orientation unresolved for ${unresolved.length} Gaussian artifacts: ${detail}`);
  }

  return {
    exhibition: { ...exhibition, import_overrides: compiled },
    compiled_count: compiled.length,
    unresolved,
    authorities,
  };
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const registryPath = process.env.VRMINE_GAUSSIAN_CONFIG ?? 'config/gaussian-splats.json';
  const exhibitionPath = process.env.VRMINE_GAUSSIAN_EXHIBITION ?? 'config/gaussian-exhibition.json';
  const registry = JSON.parse(await readFile(registryPath, 'utf8'));
  const exhibition = JSON.parse(await readFile(exhibitionPath, 'utf8'));
  const result = compileProducerOrientation(registry, exhibition, { requireAll: !args.has('--allow-missing') });
  if (args.has('--write')) {
    await writeFile(exhibitionPath, `${JSON.stringify(result.exhibition, null, 2)}\n`, 'utf8');
  }
  const legacyCount = Object.values(result.authorities).filter((value) => value === 'audited-legacy-revision').length;
  console.log(`compiled producer orientation overrides=${result.compiled_count} unresolved=${result.unresolved.length} audited_legacy=${legacyCount}`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
