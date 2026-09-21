import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { access, link, mkdir, rename, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const configPath = path.join(projectRoot, 'config', 'gaussian-worlds', 'minoo-river-osaka-sources.json');
const outputDirectory = path.join(projectRoot, 'Library', 'VRMine', 'GaussianSources');
const destination = path.join(outputDirectory, 'minoo-river-osaka.ply');
const sourcePath = process.env.VRMINE_MINOO_PLY;

if (!sourcePath) {
  throw new Error('VRMINE_MINOO_PLY is required. Set it to the selected AutoPhotogrammetry splat.ply before running this local-only staging command.');
}

const { readFile, rm } = await import('node:fs/promises');
const registry = JSON.parse(await readFile(configPath, 'utf8'));
const entry = registry.environments?.find((candidate) => candidate?.id === 'minoo-river-osaka');
if (!entry?.source) throw new Error('Minoo source registry is missing the minoo-river-osaka source.');

async function exists(file) {
  try { await access(file); return true; } catch { return false; }
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

const source = path.resolve(sourcePath);
const sourceInfo = await stat(source);
if (sourceInfo.size !== entry.source.size_bytes) {
  throw new Error(`Minoo PLY size mismatch: expected ${entry.source.size_bytes}, got ${sourceInfo.size}.`);
}
const sourceHash = await sha256(source);
if (sourceHash !== entry.source.sha256) {
  throw new Error(`Minoo PLY SHA-256 mismatch: expected ${entry.source.sha256}, got ${sourceHash}.`);
}

await mkdir(outputDirectory, { recursive: true });
if (await exists(destination)) {
  const existing = await stat(destination);
  if (existing.size === entry.source.size_bytes && await sha256(destination) === entry.source.sha256) {
    console.log(`Minoo source already staged and verified: ${destination}`);
    process.exit(0);
  }
  throw new Error(`Refusing to replace an existing invalid staged source: ${destination}`);
}

const temporary = `${destination}.partial`;
try {
  await link(source, temporary);
  const stagedInfo = await stat(temporary);
  const stagedHash = await sha256(temporary);
  if (stagedInfo.size !== entry.source.size_bytes || stagedHash !== entry.source.sha256) {
    throw new Error('Staged Minoo PLY failed the final size/hash check.');
  }
  await rename(temporary, destination);
  console.log(`Minoo source staged: ${destination}`);
  console.log(`size_bytes=${stagedInfo.size}`);
  console.log(`sha256=${stagedHash}`);
} catch (error) {
  try { await rm(temporary, { force: true }); } catch {}
  throw error;
}
