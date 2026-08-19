import { access, cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const REPOSITORY = 'https://github.com/MichaelMoroz/VRChatGaussianSplatting.git';
const REVISION = 'f96c0117cba518ff84d059d36f16909b873e23aa';
const CACHE = path.resolve('Library/VRMine/VRChatGaussianSplatting');
const DESTINATION = path.resolve('Assets/VRChatGaussianSplatting');
const PATHS = ['Editor', 'RTPool', 'RadixSort', 'Resources', 'Scripts', 'Shaders'];
const ROOT_FILES = [
  'Editor.meta', 'RTPool.meta', 'RadixSort.meta', 'Resources.meta', 'Scripts.meta', 'Shaders.meta',
  'LICENSE', 'LICENSE.meta', 'README.md', 'README.md.meta',
];
const REVISION_FILE = path.join(DESTINATION, 'VRMINE_UPSTREAM_REVISION.txt');

function git(args, cwd = process.cwd()) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  if (result.status !== 0) throw new Error(`git ${args.join(' ')} failed:\n${result.stderr}`);
  return result.stdout.trim();
}

async function exists(file) {
  try { await access(file); return true; } catch { return false; }
}

async function destinationIsCurrent() {
  if (!(await exists(REVISION_FILE))) return false;
  const marker = await readFile(REVISION_FILE, 'utf8');
  if (!marker.includes(`repository=${REPOSITORY}`) || !marker.includes(`revision=${REVISION}`)) return false;
  for (const file of [
    path.join(DESTINATION, 'Scripts', 'GaussianSplatRenderer.cs'),
    path.join(DESTINATION, 'Scripts', 'Importer', 'GaussianSplatImporter.cs'),
    path.join(DESTINATION, 'LICENSE'),
  ]) {
    if (!(await exists(file))) return false;
  }
  return true;
}

if (await destinationIsCurrent()) {
  console.log(`Reusing VRChatGaussianSplatting ${REVISION} -> ${path.relative(process.cwd(), DESTINATION)}`);
  process.exit(0);
}

await mkdir(path.dirname(CACHE), { recursive: true });
if (!(await exists(path.join(CACHE, '.git')))) {
  await rm(CACHE, { recursive: true, force: true });
  git(['clone', '--filter=blob:none', '--no-checkout', REPOSITORY, CACHE]);
} else {
  git(['remote', 'set-url', 'origin', REPOSITORY], CACHE);
}
git(['sparse-checkout', 'init', '--no-cone'], CACHE);
const patterns = [...PATHS.map((entry) => `/${entry}/`), ...ROOT_FILES.map((entry) => `/${entry}`)].join('\n') + '\n';
await writeFile(path.join(CACHE, '.git', 'info', 'sparse-checkout'), patterns);
git(['fetch', '--filter=blob:none', 'origin', REVISION], CACHE);
git(['checkout', '--detach', REVISION], CACHE);
const actual = git(['rev-parse', 'HEAD'], CACHE);
if (actual !== REVISION) throw new Error(`renderer revision mismatch: expected ${REVISION}, got ${actual}`);

const temporary = `${DESTINATION}.partial`;
await rm(temporary, { recursive: true, force: true });
await mkdir(temporary, { recursive: true });
for (const entry of PATHS) await cp(path.join(CACHE, entry), path.join(temporary, entry), { recursive: true });
for (const entry of ROOT_FILES) await cp(path.join(CACHE, entry), path.join(temporary, entry));
await writeFile(path.join(temporary, 'VRMINE_UPSTREAM_REVISION.txt'), `repository=${REPOSITORY}\nrevision=${REVISION}\n`);
await rm(DESTINATION, { recursive: true, force: true });
const { rename } = await import('node:fs/promises');
await rename(temporary, DESTINATION);

console.log(`Materialized VRChatGaussianSplatting ${REVISION} -> ${path.relative(process.cwd(), DESTINATION)}`);
