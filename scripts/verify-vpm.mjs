import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const policy = JSON.parse(await readFile(resolve(root, "config/vrchat-toolchain.json"), "utf8"));
const projectVersionPath = resolve(root, "ProjectSettings/ProjectVersion.txt");
const manifestPath = resolve(root, "Packages/manifest.json");
const vpmManifestPath = resolve(root, "Packages/vpm-manifest.json");
const vrcGetPath = resolve(root, ".tools", process.platform === "win32" ? "vrc-get.exe" : "vrc-get");
const evidenceDir = resolve(root, ".artifacts");
const evidencePath = resolve(evidenceDir, "vpm-u1.json");

const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");
const readBytes = (path) => readFile(path);
const packageIds = ["com.vrchat.base", "com.vrchat.worlds"];

function fail(message) {
  throw new Error(message);
}

function dependencyVersion(value) {
  if (typeof value === "string") return value;
  if (value && typeof value.version === "string") return value.version;
  return null;
}

function runVrcGet(args) {
  const result = spawnSync(vrcGetPath, args, {
    cwd: root,
    encoding: "utf8",
    env: process.env,
    maxBuffer: 16 * 1024 * 1024,
  });

  return {
    args,
    status: result.status,
    stdout: result.stdout ?? "",
    stderr: result.stderr ?? "",
    error: result.error ? String(result.error) : null,
  };
}

const projectVersionText = await readFile(projectVersionPath, "utf8");
const unityMatch = projectVersionText.match(/^m_EditorVersion:\s*(\S+)/m);
if (!unityMatch) fail("ProjectSettings/ProjectVersion.txt does not contain m_EditorVersion");
const unityVersion = unityMatch[1];
if (unityVersion !== policy.unityVersion) {
  fail(`Unity version drift: policy=${policy.unityVersion}, project=${unityVersion}`);
}

const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
const vpmManifest = JSON.parse(await readFile(vpmManifestPath, "utf8"));
const sdkVersions = {};

for (const id of packageIds) {
  const manifestVersion = dependencyVersion(manifest.dependencies?.[id]);
  const declaredVersion = dependencyVersion(vpmManifest.dependencies?.[id]);
  const lockedVersion = dependencyVersion(vpmManifest.locked?.[id]);
  sdkVersions[id] = { manifestVersion, declaredVersion, lockedVersion };

  for (const [source, version] of [
    ["Packages/manifest.json", manifestVersion],
    ["Packages/vpm-manifest.json dependencies", declaredVersion],
    ["Packages/vpm-manifest.json locked", lockedVersion],
  ]) {
    if (version !== policy.vrchatSdkVersion) {
      fail(`${id} drift in ${source}: expected ${policy.vrchatSdkVersion}, got ${version ?? "missing"}`);
    }
  }
}

const before = {
  manifest: sha256(await readBytes(manifestPath)),
  vpmManifest: sha256(await readBytes(vpmManifestPath)),
};

const resolveResult = runVrcGet(["resolve"]);
const after = {
  manifest: sha256(await readBytes(manifestPath)),
  vpmManifest: sha256(await readBytes(vpmManifestPath)),
};
const outdatedResult = runVrcGet(["outdated"]);

await mkdir(evidenceDir, { recursive: true });
const evidence = {
  schemaVersion: 1,
  evidenceLevel: "U1",
  unityVersion,
  vrchatSdkTarget: policy.vrchatSdkVersion,
  vrcGetVersion: policy.vrcGet.version,
  sdkVersions,
  canonicalHashes: { before, after },
  resolve: resolveResult,
  outdated: outdatedResult,
};
await writeFile(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`);

if (resolveResult.error || resolveResult.status !== 0) {
  fail(`vrc-get resolve failed with status ${resolveResult.status}: ${resolveResult.stderr}`);
}
if (before.manifest !== after.manifest || before.vpmManifest !== after.vpmManifest) {
  fail("vrc-get resolve mutated canonical VPM manifests; commit the intended lock state before merging");
}
if (outdatedResult.error || outdatedResult.status !== 0) {
  fail(`vrc-get outdated failed with status ${outdatedResult.status}: ${outdatedResult.stderr}`);
}

console.log(`U1 VPM verification passed: Unity ${unityVersion}, VRChat SDK ${policy.vrchatSdkVersion}`);
console.log(`Evidence: ${evidencePath}`);
if (outdatedResult.stdout.trim()) console.log(outdatedResult.stdout.trim());
