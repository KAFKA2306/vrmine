import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { access, mkdir, readFile, rename, rm, stat } from 'node:fs/promises';
import { spawn, spawnSync } from 'node:child_process';
import path from 'node:path';

const config = JSON.parse(await readFile('config/gaussian-splats.json', 'utf8'));
const repository = `https://github.com/${config.source_repository}.git`;
const revision = config.source_commit;
const cache = path.resolve('Library/VRMine/AutoPhotogrammetry');
const output = path.resolve('Library/VRMine/GaussianSources');

function git(args, cwd = process.cwd()) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  if (result.status !== 0) throw new Error(`git ${args.join(' ')} failed:\n${result.stderr}`);
  return result.stdout.trim();
}

async function exists(file) {
  try { await access(file); return true; } catch { return false; }
}

async function gitShowToTemporary(repo, spec, destination) {
  const temporary = `${destination}.partial`;
  await rm(temporary, { force: true });

  const child = spawn('git', ['show', spec], { cwd: repo, stdio: ['ignore', 'pipe', 'pipe'] });
  const file = createWriteStream(temporary, { flags: 'wx' });
  let stderr = '';
  child.stderr.setEncoding('utf8');
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  child.stdout.pipe(file);

  const [code] = await Promise.all([
    new Promise((resolve, reject) => {
      child.on('error', reject);
      child.on('close', resolve);
    }),
    new Promise((resolve, reject) => {
      file.on('error', reject);
      file.on('finish', resolve);
    }),
  ]);

  if (code !== 0) {
    await rm(temporary, { force: true });
    throw new Error(`git show ${spec} failed: ${stderr}`);
  }
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

await mkdir(path.dirname(cache), { recursive: true });
if (!(await exists(path.join(cache, '.git')))) {
  await rm(cache, { recursive: true, force: true });
  git(['clone', '--filter=blob:none', '--no-checkout', repository, cache]);
} else {
  git(['remote', 'set-url', 'origin', repository], cache);
}
git(['fetch', '--filter=blob:none', 'origin', revision], cache);
git(['checkout', '--detach', revision], cache);
const actual = git(['rev-parse', 'HEAD'], cache);
if (actual !== revision) throw new Error(`source revision mismatch: expected ${revision}, got ${actual}`);

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

  const temporary = await gitShowToTemporary(cache, `${revision}:${environment.source.path}`, destination);
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
