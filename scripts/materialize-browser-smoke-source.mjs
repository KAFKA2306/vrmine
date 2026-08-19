import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const id = process.argv[2] ?? 'huejotzingo';
const output = path.resolve(process.argv[3] ?? `_site/3dgs/ci/${id}.ply`);
const settingsOutput = path.resolve(process.argv[4] ?? '_site/3dgs/ci/settings.json');
const contract = JSON.parse(await readFile(new URL('../config/gaussian-splats.json', import.meta.url), 'utf8'));
const entry = contract.environments.find((candidate) => candidate.id === id);
if (!entry) throw new Error(`unknown Gaussian source id: ${id}`);
if (entry.source?.provenance?.license_status !== 'verified') {
  throw new Error(`${id}: browser smoke source must have a verified license`);
}

const sourceUrl = `https://raw.githubusercontent.com/${contract.source_repository}/${contract.source_commit}/${entry.source.path}`;
const response = await fetch(sourceUrl, { redirect: 'follow' });
if (!response.ok) throw new Error(`${id}: source PLY HTTP ${response.status}`);
const bytes = Buffer.from(await response.arrayBuffer());
const digest = createHash('sha256').update(bytes).digest('hex');
if (bytes.length !== entry.source.size_bytes) {
  throw new Error(`${id}: PLY byte-size mismatch: expected ${entry.source.size_bytes}, got ${bytes.length}`);
}
if (digest !== entry.source.sha256) {
  throw new Error(`${id}: PLY SHA-256 mismatch: expected ${entry.source.sha256}, got ${digest}`);
}

const settings = {
  version: 2,
  tonemapping: 'none',
  highPrecisionRendering: false,
  background: { color: [0, 0, 0] },
  postEffectSettings: {
    sharpness: { enabled: false, amount: 0 },
    bloom: { enabled: false, intensity: 1, blurLevel: 2 },
    grading: { enabled: false, brightness: 0, contrast: 1, saturation: 1, tint: [1, 1, 1] },
    vignette: { enabled: false, intensity: 0.5, inner: 0.3, outer: 0.75, curvature: 1 },
    fringing: { enabled: false, intensity: 0.5 }
  },
  animTracks: [],
  cameras: [{ initial: { position: [0, 1, -1], target: [0, 0, 0], fov: 60 } }],
  annotations: [],
  startMode: 'default'
};

await mkdir(path.dirname(output), { recursive: true });
await mkdir(path.dirname(settingsOutput), { recursive: true });
await writeFile(output, bytes);
await writeFile(settingsOutput, `${JSON.stringify(settings, null, 2)}\n`, 'utf8');
console.log(`Materialized browser smoke source ${id}: bytes=${bytes.length}, sha256=${digest}`);
