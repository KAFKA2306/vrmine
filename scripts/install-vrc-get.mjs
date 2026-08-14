import { createHash } from "node:crypto";
import { chmod, mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const policyPath = resolve(root, "config/vrchat-toolchain.json");
const policy = JSON.parse(await readFile(policyPath, "utf8"));
const assetKey = `${process.platform}-${process.arch}`;
const asset = policy.vrcGet.assets[assetKey];

if (!asset) {
  throw new Error(`Unsupported vrc-get platform: ${assetKey}`);
}

const toolsDir = resolve(root, ".tools");
const destination = resolve(toolsDir, process.platform === "win32" ? "vrc-get.exe" : "vrc-get");
const temporary = `${destination}.download`;
const expected = asset.sha256.toLowerCase();
const url = `https://github.com/vrc-get/vrc-get/releases/download/v${policy.vrcGet.version}/${asset.name}`;

const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");

async function existingMatches() {
  try {
    const bytes = await readFile(destination);
    return sha256(bytes) === expected;
  } catch (error) {
    if (error && error.code === "ENOENT") return false;
    throw error;
  }
}

await mkdir(toolsDir, { recursive: true });

if (await existingMatches()) {
  console.log(`vrc-get ${policy.vrcGet.version} already verified at ${destination}`);
  process.exit(0);
}

const response = await fetch(url, { redirect: "follow" });
if (!response.ok) {
  throw new Error(`Failed to download vrc-get: HTTP ${response.status} ${response.statusText}`);
}

const bytes = Buffer.from(await response.arrayBuffer());
const actual = sha256(bytes);
if (actual !== expected) {
  throw new Error(`vrc-get checksum mismatch: expected ${expected}, got ${actual}`);
}

await rm(temporary, { force: true });
await writeFile(temporary, bytes);
if (process.platform !== "win32") await chmod(temporary, 0o755);
await rename(temporary, destination);
console.log(`Installed vrc-get ${policy.vrcGet.version} (${actual}) -> ${destination}`);
