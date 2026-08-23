import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFile, mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import path from 'node:path';

const id = process.argv[2] ?? 'huejotzingo';
const output = path.resolve(process.argv[3] ?? `_site/3dgs/ci/${id}.ply`);
const settingsOutput = path.resolve(process.argv[4] ?? '_site/3dgs/ci/settings.json');
const contract = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));
const basisContract = JSON.parse(await readFile(new URL('../config/gaussian-basis-contract.json', import.meta.url), 'utf8'));
const entry = contract.environments.find((candidate) => candidate.id === id);
if (!entry) throw new Error(`unknown Gaussian source id: ${id}`);
if (basisContract.producer?.repository !== contract.source_repository) {
  throw new Error(`${id}: artifact producer repository does not match source registry`);
}
if (typeof basisContract.producer?.revision !== 'string' || !/^[0-9a-f]{40}$/.test(basisContract.producer.revision)) {
  throw new Error(`${id}: artifact producer revision must be an immutable commit SHA`);
}
if (entry.source?.artifact_manifest !== basisContract.artifact_manifest) {
  throw new Error(`${id}: source artifact manifest does not match artifact-set basis contract`);
}
if (typeof entry.source?.artifact_id !== 'string' || entry.source.artifact_id.length === 0) {
  throw new Error(`${id}: source.artifact_id is required`);
}

const resolverRoot = process.env.HF_CACHE_HUB_ROOT;
if (!resolverRoot) throw new Error(`${id}: HF_CACHE_HUB_ROOT is required for artifact materialization`);
const resolver = path.join(path.resolve(resolverRoot), 'scripts', 'artifact_cache.py');
const manifest = path.resolve(entry.source.artifact_manifest);
const python = process.env.HF_CACHE_HUB_PYTHON ?? process.env.PYTHON ?? process.env.PYTHON3 ?? 'python3';
let stdout;
try {
  stdout = execFileSync(
    python,
    [resolver, 'resolve', '--manifest', manifest, '--id', entry.source.artifact_id],
    { encoding: 'utf8', env: process.env, stdio: ['ignore', 'pipe', 'pipe'] },
  );
} catch (error) {
  const detail = error?.stdout?.toString?.() || error?.stderr?.toString?.() || error?.message || 'unknown error';
  throw new Error(`${id}: artifact resolve command failed: ${detail}`);
}

let resolved;
try {
  resolved = JSON.parse(stdout);
} catch {
  throw new Error(`${id}: hf-cache-hub resolver returned non-JSON output: ${stdout}`);
}
if (resolved.status !== 'READY') {
  throw new Error(`${id}: artifact resolve failed: ${resolved.error ?? 'unknown error'}`);
}
if (resolved.sha256 !== entry.source.sha256) {
  throw new Error(`${id}: resolver SHA-256 mismatch: expected ${entry.source.sha256}, got ${resolved.sha256}`);
}
if (resolved.size_bytes !== entry.source.size_bytes) {
  throw new Error(`${id}: resolver size mismatch: expected ${entry.source.size_bytes}, got ${resolved.size_bytes}`);
}
if (typeof resolved.cache_path !== 'string' || resolved.cache_path.length === 0) {
  throw new Error(`${id}: resolver did not return cache_path`);
}

const info = await stat(resolved.cache_path);
const bytes = await readFile(resolved.cache_path);
const digest = createHash('sha256').update(bytes).digest('hex');
if (info.size !== entry.source.size_bytes) {
  throw new Error(`${id}: PLY byte-size mismatch: expected ${entry.source.size_bytes}, got ${info.size}`);
}
if (digest !== entry.source.sha256) {
  throw new Error(`${id}: PLY SHA-256 mismatch: expected ${entry.source.sha256}, got ${digest}`);
}

const settings = {
  version: 2,
  tonemapping: 'none',
  highPrecisionRendering: false,
  background: { color: [0, 0, 0] },
  postEffectSettings: {
    sharpness: { enabled: false, amount: 0 },
    bloom: { enabled: false, intensity: 1, blurLevel: 2 },
    grading: { enabled: false, brightness: 0, contrast: 1, saturation: 1, tint: [1, 1, 1] },
    vignette: { enabled: false, intensity: 0.5, inner: 0.3, outer: 0.75, curvature: 1 },
    fringing: { enabled: false, intensity: 0.5 }
  },
  animTracks: [],
  cameras: [{ initial: { position: [0, 1, -1], target: [0, 0, 0], fov: 60 } }],
  annotations: [],
  startMode: 'default'
};

await mkdir(path.dirname(output), { recursive: true });
await mkdir(path.dirname(settingsOutput), { recursive: true });
await copyFile(resolved.cache_path, output);
await writeFile(settingsOutput, `${JSON.stringify(settings, null, 2)}\n`, 'utf8');
console.log(`Materialized browser smoke source ${id}: artifact_id=${entry.source.artifact_id}, producer_revision=${basisContract.producer.revision}, bytes=${info.size}, sha256=${digest}, cache_hit=${resolved.cache_hit === true}`);
