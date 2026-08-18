import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, readFile, rm, stat } from 'node:fs/promises';
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

async function gitShowToFile(repo, spec, destination) {
  await new Promise((resolve, reject) => {
    const child = spawn('git', ['show', spec], { cwd: repo, stdio: ['ignore', 'pipe', 'pipe'] });
    const file = createWriteStream(destination);
    let stderr = '';
    child.stderr.setEncoding('utf8');
    child.stderr.on('data', (chunk) => { stderr += chunk; });
    child.stdout.pipe(file);
    child.on('error', reject);
    file.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`git show ${spec} failed: ${stderr}`));
    });
  });
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

await rm(cache, { recursive: true, force: true });
await mkdir(path.dirname(cache), { recursive: true });
git(['clone', '--filter=blob:none', '--no-checkout', repository, cache]);
git(['checkout', '--detach', revision], cache);
const actual = git(['rev-parse', 'HEAD'], cache);
if (actual !== revision) throw new Error(`source revision mismatch: expected ${revision}, got ${actual}`);

await mkdir(output, { recursive: true });
for (const environment of config.environments) {
  const destination = path.join(output, `${environment.id}.ply`);
  await rm(destination, { force: true });
  await gitShowToFile(cache, `${revision}:${environment.source.path}`, destination);
  const info = await stat(destination);
  const digest = await sha256(destination);
  if (info.size !== environment.source.size_bytes) {
    throw new Error(`${environment.id}: size mismatch: expected ${environment.source.size_bytes}, got ${info.size}`);
  }
  if (digest !== environment.source.sha256) {
    throw new Error(`${environment.id}: SHA-256 mismatch: expected ${environment.source.sha256}, got ${digest}`);
  }
  console.log(`${environment.id}: verified ${info.size} bytes ${digest}`);
}

console.log(`Materialized ${config.environments.length}/20 registered Gaussian PLY sources -> ${path.relative(process.cwd(), output)}`);
if (config.environments.length !== 20) {
  console.log(`BLOCKED: ${20 - config.environments.length} final exhibition source(s) are still missing upstream.`);
}
