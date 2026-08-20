import { execFileSync } from 'node:child_process';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const root = await mkdtemp(path.join(os.tmpdir(), 'vrmine-artifact-resolver-'));
try {
  const hfRoot = path.join(root, 'hf-cache-hub');
  const cacheFile = path.join(root, 'cache', 'fixture.ply');
  const output = path.join(root, 'Library', 'VRMine', 'GaussianSources');
  const configPath = path.join(root, 'gaussian-splats.json');
  const resolver = path.join(hfRoot, 'scripts', 'artifact_cache.py');
  await mkdir(path.dirname(resolver), { recursive: true });
  await mkdir(path.dirname(cacheFile), { recursive: true });
  await writeFile(cacheFile, 'artifact-fixture-ply\n');
  await writeFile(
    resolver,
    `import json\nprint(json.dumps({"status":"READY","cache_hit":True,"transferred_bytes":0,"cache_path":${JSON.stringify(cacheFile)},"size_bytes":21,"sha256":"c04af271a32dd3b2f3d5604aa4ab61311a7d4be15d142d1c401fa34da34587d3"}))\n`,
  );
  await writeFile(
    configPath,
    JSON.stringify({
      schema_version: 1,
      environments: [{
        id: 'fixture',
        type: 'gaussian-splat',
        format: 'ply',
        source: {
          artifact_id: 'autophotogrammetry/fixture/splat',
          size_bytes: 21,
          sha256: 'c04af271a32dd3b2f3d5604aa4ab61311a7d4be15d142d1c401fa34da34587d3',
        },
      }],
    }),
  );

  const stdout = execFileSync(
    process.execPath,
    ['scripts/materialize-gaussian-sources.mjs'],
    {
      cwd: process.cwd(),
      encoding: 'utf8',
      env: {
        ...process.env,
        HF_CACHE_HUB_ROOT: hfRoot,
        PYTHON3: process.env.PYTHON3 ?? 'python3',
        VRMINE_GAUSSIAN_CONFIG: configPath,
        VRMINE_GAUSSIAN_OUTPUT: output,
      },
    },
  );
  const materialized = await readFile(path.join(output, 'fixture.ply'), 'utf8');
  if (materialized !== 'artifact-fixture-ply\n') throw new Error('materialized bytes differ from shared cache bytes');
  if (!stdout.includes('cache_hit=true')) throw new Error(`expected cache hit evidence, got: ${stdout}`);
  if (!stdout.includes('artifact_transferred_bytes=0')) throw new Error(`expected zero transfer evidence, got: ${stdout}`);
  console.log('Gaussian artifact resolver integration verified');
} finally {
  await rm(root, { recursive: true, force: true });
}
