import { cp, mkdir, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const REPOSITORY = 'https://github.com/MichaelMoroz/VRChatGaussianSplatting.git';
const REVISION = 'f96c0117cba518ff84d059d36f16909b873e23aa';
const CACHE = path.resolve('Library/VRMine/VRChatGaussianSplatting');
// Upstream Editor code loads resources through exact AssetDatabase paths under
// Assets/VRChatGaussianSplatting. Preserve that root instead of nesting the
// checkout under VRMine-specific folders.
const DESTINATION = path.resolve('Assets/VRChatGaussianSplatting');
const PATHS = ['Editor', 'RTPool', 'RadixSort', 'Resources', 'Scripts', 'Shaders'];
const ROOT_FILES = [
  'Editor.meta', 'RTPool.meta', 'RadixSort.meta', 'Resources.meta', 'Scripts.meta', 'Shaders.meta',
  'LICENSE', 'LICENSE.meta', 'README.md', 'README.md.meta',
];

function git(args, cwd = process.cwd()) {
  const result = spawnSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  if (result.status !== 0) throw new Error(`git ${args.join(' ')} failed:\n${result.stderr}`);
  return result.stdout.trim();
}

await mkdir(path.dirname(CACHE), { recursive: true });
await rm(CACHE, { recursive: true, force: true });
git(['clone', '--filter=blob:none', '--no-checkout', REPOSITORY, CACHE]);
git(['sparse-checkout', 'init', '--no-cone'], CACHE);
const patterns = [...PATHS.map((entry) => `/${entry}/`), ...ROOT_FILES.map((entry) => `/${entry}`)].join('\n') + '\n';
await writeFile(path.join(CACHE, '.git', 'info', 'sparse-checkout'), patterns);
git(['checkout', '--detach', REVISION], CACHE);
const actual = git(['rev-parse', 'HEAD'], CACHE);
if (actual !== REVISION) throw new Error(`renderer revision mismatch: expected ${REVISION}, got ${actual}`);

await rm(DESTINATION, { recursive: true, force: true });
await mkdir(DESTINATION, { recursive: true });
for (const entry of PATHS) await cp(path.join(CACHE, entry), path.join(DESTINATION, entry), { recursive: true });
for (const entry of ROOT_FILES) await cp(path.join(CACHE, entry), path.join(DESTINATION, entry));
await writeFile(
  path.join(DESTINATION, 'VRMINE_UPSTREAM_REVISION.txt'),
  `repository=${REPOSITORY}\nrevision=${REVISION}\n`,
);

console.log(`Materialized VRChatGaussianSplatting ${REVISION} -> ${path.relative(process.cwd(), DESTINATION)}`);
