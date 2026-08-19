import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { access, mkdir, readFile, rename, rm, stat } from 'node:fs/promises';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import path from 'node:path';

const config = JSON.parse(await readFile('config/gaussian-splats.json', 'utf8'));
const output = path.resolve('Library/VRMine/GaussianSources');

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

await mkdir(output, { recursive: true });
let reused = 0;
let materialized = 0;
for (const environment of config.environments) {
  const destination = path.join(output, `${environment.id}.ply`);
  if (await validExisting(destination, environment)) {
    reused++;
    console.log(`${environment.id}: reuse verified ${environment.source.size_bytes} bytes ${environment.source.sha256}`);
    continue;
  }

  const temporary = await downloadToTemporary(environment.source.download_url, destination);
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
    console.log(`${environment.id}: materialized ${info.size} bytes ${digest}`);
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
}

console.log(`Gaussian sources ready: count=${config.environments.length}, materialized=${materialized}, reused=${reused}, path=${path.relative(process.cwd(), output)}`);
