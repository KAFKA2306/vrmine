import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const outputDir = path.resolve(process.argv[2] ?? path.join(projectRoot, '.artifacts', 'world-builds', 'cyber-alley-tea-nook'));
const manifestPath = path.join(outputDir, 'manifest.json');
const glbPath = path.join(outputDir, 'world.glb');

function fail(message) {
  throw new Error(`World build Unity verification FAIL: ${message}`);
}
function sha256(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}
function countGlbGeometry(file) {
  const bytes = fs.readFileSync(file);
  if (bytes.toString('ascii', 0, 4) !== 'glTF') fail('world.glb has invalid header');
  const jsonLength = bytes.readUInt32LE(12);
  if (bytes.toString('ascii', 16, 20) !== 'JSON') fail('world.glb has no JSON chunk');
  const gltf = JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').replace(/\0+$/u, '').trim());
  let vertices = 0;
  let triangles = 0;
  for (const mesh of gltf.meshes ?? []) {
    for (const primitive of mesh.primitives ?? []) {
      const positionAccessor = gltf.accessors?.[primitive.attributes?.POSITION];
      if (!positionAccessor) fail('mesh primitive is missing POSITION accessor');
      vertices += Number(positionAccessor.count);
      if (primitive.mode !== undefined && primitive.mode !== 4) fail(`unsupported primitive mode ${primitive.mode}; expected TRIANGLES`);
      if (primitive.indices === undefined) {
        if (positionAccessor.count % 3 !== 0) fail('non-indexed triangle primitive has non-multiple-of-three vertex count');
        triangles += positionAccessor.count / 3;
      } else {
        const indexAccessor = gltf.accessors?.[primitive.indices];
        if (!indexAccessor || indexAccessor.count % 3 !== 0) fail('indexed triangle primitive has invalid index accessor');
        triangles += indexAccessor.count / 3;
      }
    }
  }
  if (vertices <= 0 || triangles <= 0) fail('world.glb contains no triangle geometry');
  return { vertices, triangles };
}

if (!fs.existsSync(manifestPath)) fail(`missing manifest ${manifestPath}`);
if (!fs.existsSync(glbPath)) fail(`missing GLB ${glbPath}`);
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const expectedGlb = manifest.outputs?.glb;
if (!expectedGlb?.sha256 || expectedGlb.path !== 'world.glb') fail('manifest does not own world.glb with SHA-256');
const actualSha = sha256(glbPath);
if (actualSha !== expectedGlb.sha256) fail(`manifest GLB digest drift: expected=${expectedGlb.sha256}, actual=${actualSha}`);
const geometry = countGlbGeometry(glbPath);

const env = {
  ...process.env,
  VRMINE_GLB_PATH: glbPath,
  VRMINE_GLB_SHA256: actualSha,
  VRMINE_GLB_VERTEX_COUNT: String(geometry.vertices),
  VRMINE_GLB_TRIANGLE_COUNT: String(geometry.triangles),
  VRMINE_EVIDENCE_DIR: path.join(outputDir, 'unity-evidence'),
};
const run = spawnSync(process.execPath, [path.join(scriptDir, 'run-glb-consumer-unity.mjs')], {
  cwd: projectRoot,
  env,
  stdio: 'inherit',
});
if (run.error) throw run.error;
if (run.status !== 0) fail(`canonical GLB consumer verifier exited ${run.status}`);
const evidencePath = path.join(env.VRMINE_EVIDENCE_DIR, 'glb-consumer-evidence.json');
if (!fs.existsSync(evidencePath)) fail(`canonical Unity evidence missing: ${evidencePath}`);
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'));
if (evidence.status !== 'PASS' || evidence.sourceSha256 !== actualSha) fail('canonical Unity evidence does not match Pilot A GLB');
console.log(JSON.stringify({ status: 'PASS', pilot: 'A', source: glbPath, sha256: actualSha, vertices: geometry.vertices, triangles: geometry.triangles, unityEvidence: evidencePath }));
