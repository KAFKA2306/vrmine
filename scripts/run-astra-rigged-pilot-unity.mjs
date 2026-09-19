import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(scriptDir, '..');
const outputDir = path.resolve(process.argv[2] ?? path.join(projectRoot, '.artifacts', 'astra-pilot-b'));
const manifest = JSON.parse(fs.readFileSync(path.join(outputDir, 'manifest.json'), 'utf8'));
if (manifest.pilot !== 'B') throw new Error('Pilot B manifest identity mismatch');
const glb = manifest.outputs?.glb;
if (!glb?.sha256 || glb.path !== 'pilot-b.glb') throw new Error('Pilot B manifest does not own pilot-b.glb');
const glbPath = path.join(outputDir, glb.path);
const bytes = fs.readFileSync(glbPath);
const actualSha = crypto.createHash('sha256').update(bytes).digest('hex');
if (actualSha !== glb.sha256) throw new Error('Pilot B GLB digest drift');
if (bytes.toString('ascii', 0, 4) !== 'glTF') throw new Error('Pilot B GLB header invalid');
const jsonLength = bytes.readUInt32LE(12);
const gltf = JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').replace(/\0+$/u, '').trim());
let vertices = 0;
let triangles = 0;
for (const mesh of gltf.meshes ?? []) for (const primitive of mesh.primitives ?? []) {
  const positions = gltf.accessors?.[primitive.attributes?.POSITION];
  if (!positions) throw new Error('Pilot B primitive has no POSITION');
  vertices += Number(positions.count);
  const indices = primitive.indices === undefined ? null : gltf.accessors?.[primitive.indices];
  const count = indices ? Number(indices.count) : Number(positions.count);
  if (count % 3 !== 0) throw new Error('Pilot B triangle count is not integral');
  triangles += count / 3;
}
if (vertices <= 0 || triangles <= 0) throw new Error('Pilot B GLB has no geometry');
if (!(gltf.skins?.length > 0)) throw new Error('Pilot B GLB lost skin data before Unity');
if (!(gltf.meshes ?? []).some(mesh => (mesh.primitives ?? []).some(primitive => primitive.targets?.length > 0))) {
  throw new Error('Pilot B GLB lost morph target before Unity');
}
const evidenceDir = path.join(outputDir, 'unity-evidence');
const env = {...process.env, VRMINE_GLB_PATH: glbPath, VRMINE_GLB_SHA256: actualSha, VRMINE_GLB_VERTEX_COUNT: String(vertices), VRMINE_GLB_TRIANGLE_COUNT: String(triangles), VRMINE_EVIDENCE_DIR: evidenceDir};
const run = spawnSync(process.execPath, [path.join(scriptDir, 'run-glb-consumer-unity.mjs')], {cwd: projectRoot, env, stdio: 'inherit'});
if (run.error) throw run.error;
if (run.status !== 0) throw new Error(`canonical Unity verifier exited ${run.status}`);
const evidencePath = path.join(evidenceDir, 'glb-consumer-evidence.json');
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'));
if (evidence.status !== 'PASS' || evidence.sourceSha256 !== actualSha) throw new Error('Unity evidence does not match Pilot B GLB');
console.log(JSON.stringify({status: 'PASS', pilot: 'B', sha256: actualSha, vertices, triangles, unityEvidence: evidencePath}));
