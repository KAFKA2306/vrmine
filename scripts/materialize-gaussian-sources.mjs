import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { access, copyFile, link, mkdir, readFile, rename, rm, stat } from 'node:fs/promises';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import path from 'node:path';

const configPath = process.env.VRMINE_GAUSSIAN_CONFIG ?? 'config/gaussian-splats.json';
const config = JSON.parse(await readFile(configPath, 'utf8'));
const output = path.resolve(process.env.VRMINE_GAUSSIAN_OUTPUT ?? 'Library/VRMine/GaussianSources');

async function exists(file) {
  try { await access(file); return true; } catch { return false; }
}

async function downloadToTemporary(url, destination) {
  const temporary = `${destination}.partial`;
  await rm(temporary, { force: true });
  const response = await fetch(url);
  if (!response.ok || response.body === null) throw new Error(`download failed: ${response.status} ${url}`);
  const file = createWriteStream(temporary, { flags: 'wx' });
  await pipeline(Readable.fromWeb(response.body), file);
  return temporary;
}

async function sha256(file) {
  const hash = createHash('sha256');
  await new Promise((resolve, reject) => {
    const stream = createReadStream(file);
    stream.on('data', (chunk) => hash.update(chunk));
    stream.on('end', resolve);
    stream.on('error', reject);
  });
  return hash.digest('hex');
}

async function validExisting(destination, environment) {
  if (!(await exists(destination))) return false;
  const info = await stat(destination);
  if (info.size !== environment.source.size_bytes) return false;
  return (await sha256(destination)) === environment.source.sha256;
}

function resolveArtifact(environment) {
  const rootValue = process.env.HF_CACHE_HUB_ROOT;
  if (!rootValue) throw new Error(`${environment.id}: HF_CACHE_HUB_ROOT is required for artifact source`);
  const root = path.resolve(rootValue);
  const resolver = path.join(root, 'scripts', 'artifact_cache.py');
  const manifest = environment.source.artifact_manifest
    ? path.resolve(environment.source.artifact_manifest)
    : path.join(root, 'artifacts.yaml');
  const python = process.env.HF_CACHE_HUB_PYTHON ?? process.env.PYTHON ?? process.env.PYTHON3 ?? 'python3';
  let stdout;
  try {
    stdout = execFileSync(
      python,
      [resolver, 'resolve', '--manifest', manifest, '--id', environment.source.artifact_id],
      { encoding: 'utf8', env: process.env, stdio: ['ignore', 'pipe', 'pipe'] },
    );
  } catch (error) {
    const detail = error?.stdout?.toString?.() || error?.stderr?.toString?.() || error?.message || 'unknown error';
    throw new Error(`${environment.id}: artifact resolve command failed: ${detail}`);
  }
  let payload;
  try {
    payload = JSON.parse(stdout);
  } catch {
    throw new Error(`${environment.id}: hf-cache-hub resolver returned non-JSON output: ${stdout}`);
  }
  if (payload.status !== 'READY') {
    throw new Error(`${environment.id}: artifact resolve failed: ${payload.error ?? 'unknown error'}`);
  }
  if (payload.sha256 !== environment.source.sha256) {
    throw new Error(`${environment.id}: resolver SHA-256 mismatch: expected ${environment.source.sha256}, got ${payload.sha256}`);
  }
  if (payload.size_bytes !== environment.source.size_bytes) {
    throw new Error(`${environment.id}: resolver size mismatch: expected ${environment.source.size_bytes}, got ${payload.size_bytes}`);
  }
  if (typeof payload.cache_path !== 'string' || payload.cache_path.length === 0) {
    throw new Error(`${environment.id}: resolver did not return cache_path`);
  }
  return payload;
}

async function artifactToTemporary(environment, destination) {
  const payload = resolveArtifact(environment);
  const temporary = `${destination}.partial`;
  await rm(temporary, { force: true });
  let localMaterialization = 'hardlink';
  try {
    await link(payload.cache_path, temporary);
  } catch {
    await copyFile(payload.cache_path, temporary);
    localMaterialization = 'copy';
  }
  return { temporary, payload, localMaterialization };
}

function sourceMode(environment) {
  const source = environment.source ?? {};
  const hasArtifact = typeof source.artifact_id === 'string' && source.artifact_id.length > 0;
  const hasLegacy = typeof source.download_url === 'string' && source.download_url.length > 0;
  if (hasArtifact) return 'artifact';
  if (hasLegacy) return 'legacy-url';
  throw new Error(`${environment.id}: source requires artifact_id or download_url`);
}

await mkdir(output, { recursive: true });
let reused = 0;
let materialized = 0;
let cacheHits = 0;
let transferredBytes = 0;
let hardlinks = 0;
let copies = 0;
for (const environment of config.environments) {
  const destination = path.join(output, `${environment.id}.ply`);
  if (await validExisting(destination, environment)) {
    reused++;
    console.log(`${environment.id}: reuse verified ${environment.source.size_bytes} bytes ${environment.source.sha256}`);
    continue;
  }

  const mode = sourceMode(environment);
  let temporary;
  let resolverPayload = null;
  let localMaterialization = null;
  if (mode === 'artifact') {
    const resolved = await artifactToTemporary(environment, destination);
    temporary = resolved.temporary;
    resolverPayload = resolved.payload;
    localMaterialization = resolved.localMaterialization;
  } else {
    temporary = await downloadToTemporary(environment.source.download_url, destination);
  }

  try {
    const info = await stat(temporary);
    const digest = await sha256(temporary);
    if (info.size !== environment.source.size_bytes) {
      throw new Error(`${environment.id}: size mismatch: expected ${environment.source.size_bytes}, got ${info.size}`);
    }
    if (digest !== environment.source.sha256) {
      throw new Error(`${environment.id}: SHA-256 mismatch: expected ${environment.source.sha256}, got ${digest}`);
    }

    await rename(temporary, destination);
    materialized++;
    if (resolverPayload) {
      if (resolverPayload.cache_hit === true) cacheHits++;
      transferredBytes += Number(resolverPayload.transferred_bytes ?? 0);
      if (localMaterialization === 'hardlink') hardlinks++;
      if (localMaterialization === 'copy') copies++;
      console.log(`${environment.id}: artifact materialized ${info.size} bytes ${digest} cache_hit=${resolverPayload.cache_hit === true} local=${localMaterialization}`);
    } else {
      console.log(`${environment.id}: materialized ${info.size} bytes ${digest}`);
    }
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
}

console.log(`Gaussian sources ready: count=${config.environments.length}, materialized=${materialized}, reused=${reused}, artifact_cache_hits=${cacheHits}, artifact_transferred_bytes=${transferredBytes}, artifact_hardlinks=${hardlinks}, artifact_copies=${copies}, path=${path.relative(process.cwd(), output)}`);
