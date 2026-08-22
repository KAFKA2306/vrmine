import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PINNED_RENDERER = 'MichaelMoroz/VRChatGaussianSplatting@f96c0117cba518ff84d059d36f16909b873e23aa';
const PINNED_RENDERER_REVISION = 'f96c0117cba518ff84d059d36f16909b873e23aa';
const EXPECTED_CONSUMER = 'MichaelMoroz/VRChatGaussianSplatting';
const EXPECTED_MODE = 'horizon_alignment_pre_y_reflection';
const EXPECTED_POST_TRANSFORM = 'reflect-y';
const EXPECTED_REPRESENTATION_FIELDS = new Set(['position', 'gaussian_rotation', 'spherical_harmonics']);
const ALLOWED_SCOPES = new Set(['coordinate_basis_only', 'coordinate_basis_plus_physical_up']);

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

function validateConsumerApplication(id, consumer) {
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
}

function validateBasisContract(registry, contract) {
  if (!contract) return;
  if (contract.schema_version !== 1 || contract.status !== 'accepted') {
    throw new Error('Gaussian artifact-set basis contract must be schema v1 and accepted');
  }
  if (contract.scope !== 'coordinate_basis_only') {
    throw new Error(`Gaussian artifact-set basis contract has unsupported scope ${contract.scope}`);
  }
  if (contract.producer?.repository !== registry.source_repository) {
    throw new Error('Gaussian artifact-set basis contract producer repository does not match registry');
  }
  if (contract.producer?.revision !== registry.source_commit) {
    throw new Error('Gaussian artifact-set basis contract producer revision does not match registry');
  }
  if (contract.canonical_frame?.name !== 'unity-basis-y-up') {
    throw new Error('Gaussian artifact-set basis contract canonical frame is unsupported');
  }
  if (contract.canonical_frame?.physical_gravity_claimed !== false) {
    throw new Error('Coordinate-basis-only contract must not claim physical gravity');
  }
  if (contract.physical_up?.status !== 'review_required') {
    throw new Error('Coordinate-basis-only contract must keep physical_up review_required');
  }
  validateConsumerApplication('artifact-set basis contract', contract.consumer_application);
}

function orientationFromBasisContract(source, contract) {
  if (!contract) return null;
  if (source?.provenance?.artifact_repository_commit !== contract.producer.revision) return null;
  if (!source?.sha256) return null;
  return {
    schema_version: 2,
    status: 'accepted',
    scope: contract.scope,
    ply_sha256: source.sha256,
    canonical_frame: contract.canonical_frame,
    derivation_method: 'explicit-artifact-set-coordinate-basis-contract',
    physical_up: contract.physical_up,
    producer: contract.producer,
    consumer_application: contract.consumer_application,
  };
}

function resolveOrientation(source, basisContract) {
  if (source.orientation) return { orientation: source.orientation, authority: 'producer-artifact-metadata' };
  const contracted = orientationFromBasisContract(source, basisContract);
  if (contracted) return { orientation: contracted, authority: 'artifact-set-basis-contract' };
  return { orientation: null, authority: null };
}

function validateBasisOrientation(id, source, orientation) {
  if (orientation.schema_version !== 2) {
    throw new Error(`${id}: unsupported orientation schema; v2 basis/physical-up split is required`);
  }
  if (orientation.status !== 'accepted') {
    return `orientation basis status=${orientation.status ?? 'missing'}`;
  }
  if (!ALLOWED_SCOPES.has(orientation.scope)) {
    throw new Error(`${id}: unsupported orientation scope ${orientation.scope}`);
  }
  if (orientation.ply_sha256 !== source.sha256) {
    throw new Error(`${id}: orientation PLY SHA-256 does not match source artifact`);
  }
  if (orientation.canonical_frame?.name !== 'unity-basis-y-up') {
    throw new Error(`${id}: unsupported canonical frame ${orientation.canonical_frame?.name}`);
  }
  const physicalUp = orientation.physical_up;
  const physicalUpStatus = physicalUp?.status;
  if (!['accepted', 'review_required', 'unavailable'].includes(physicalUpStatus)) {
    throw new Error(`${id}: physical_up.status is missing or unsupported`);
  }
  if (orientation.scope === 'coordinate_basis_plus_physical_up') {
    if (physicalUpStatus !== 'accepted') {
      throw new Error(`${id}: physical-up composition scope requires accepted physical_up evidence`);
    }
    if (!physicalUp?.authority_type || !physicalUp?.evidence_sha256) {
      throw new Error(`${id}: accepted physical_up requires authority_type and evidence_sha256`);
    }
    if (orientation.canonical_frame?.physical_gravity_claimed !== true) {
      throw new Error(`${id}: physical-up composition must explicitly claim validated physical gravity`);
    }
  } else if (physicalUpStatus === 'accepted') {
    throw new Error(`${id}: accepted physical_up cannot be hidden behind coordinate_basis_only scope`);
  }

  validateConsumerApplication(id, orientation.consumer_application);
  return null;
}

export function compileProducerOrientation(
  registry,
  exhibition,
  { requireAll = true, basisContract = null } = {},
) {
  if (!registry || !Array.isArray(registry.environments)) {
    throw new Error('Gaussian registry environments are missing');
  }
  if (!exhibition || !Array.isArray(exhibition.import_overrides)) {
    throw new Error('Gaussian exhibition import_overrides are missing');
  }
  if (exhibition.renderer !== PINNED_RENDERER) {
    throw new Error(`Unexpected Gaussian renderer: ${exhibition.renderer}`);
  }
  validateBasisContract(registry, basisContract);

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
    const resolved = resolveOrientation(source, basisContract);
    const orientation = resolved.orientation;
    if (!orientation) {
      unresolved.push({ id, reason: 'orientation metadata missing and no explicit artifact-set basis contract applies' });
      continue;
    }
    const unresolvedReason = validateBasisOrientation(id, source, orientation);
    if (unresolvedReason) {
      unresolved.push({ id, reason: unresolvedReason });
      continue;
    }

    const consumer = orientation.consumer_application;
    const previous = existing.get(id) ?? { id };
    const physicalAccepted = orientation.physical_up.status === 'accepted';
    const authority = physicalAccepted
      ? `producer-physical-up:${orientation.physical_up.authority_type}`
      : resolved.authority;
    compiled.push({
      ...previous,
      id,
      alignment: {
        enabled: true,
        mode: 'horizon',
        scope: orientation.scope,
        physicalUpStatus: orientation.physical_up.status,
        authority,
        rotation: quaternion(consumer.quaternion_xyzw, `${id} consumer quaternion`),
        pivot: vector3(consumer.pivot ?? [0, 0, 0], `${id} consumer pivot`),
      },
    });
    authorities[id] = authority;
  }

  if (requireAll && unresolved.length > 0) {
    const detail = unresolved.map((entry) => `${entry.id}: ${entry.reason}`).join('; ');
    throw new Error(`Producer orientation basis unresolved for ${unresolved.length} Gaussian artifacts: ${detail}`);
  }

  return {
    exhibition: { ...exhibition, import_overrides: compiled },
    compiled_count: compiled.length,
    unresolved,
    authorities,
    physical_up_counts: compiled.reduce((counts, entry) => {
      const status = entry.alignment.physicalUpStatus;
      counts[status] = (counts[status] ?? 0) + 1;
      return counts;
    }, {}),
  };
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const registryPath = process.env.VRMINE_GAUSSIAN_CONFIG ?? 'config/gaussian-splats.json';
  const exhibitionPath = process.env.VRMINE_GAUSSIAN_EXHIBITION ?? 'config/gaussian-exhibition.json';
  const registry = JSON.parse(await readFile(registryPath, 'utf8'));
  const exhibition = JSON.parse(await readFile(exhibitionPath, 'utf8'));
  if (!exhibition.basis_contract) {
    throw new Error('Gaussian exhibition basis_contract is required');
  }
  const basisContractPath = path.resolve(path.dirname(exhibitionPath), path.basename(exhibition.basis_contract));
  const basisContract = JSON.parse(await readFile(basisContractPath, 'utf8'));
  const result = compileProducerOrientation(registry, exhibition, {
    requireAll: !args.has('--allow-missing'),
    basisContract,
  });
  if (args.has('--write')) {
    await writeFile(exhibitionPath, `${JSON.stringify(result.exhibition, null, 2)}\n`, 'utf8');
  }
  const contractCount = Object.values(result.authorities).filter((value) => value === 'artifact-set-basis-contract').length;
  console.log(
    `compiled producer basis overrides=${result.compiled_count} unresolved=${result.unresolved.length} artifact_set_contract=${contractCount} physical_up=${JSON.stringify(result.physical_up_counts)}`,
  );
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main();
}
