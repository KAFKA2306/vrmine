import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const modulePath = fileURLToPath(import.meta.url);
const defaultRoot = path.resolve(path.dirname(modulePath), '..');

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function readRequired(root, relativePath) {
  const absolutePath = path.join(root, relativePath);
  if (!fs.existsSync(absolutePath)) {
    throw new Error(`missing canonical source: ${relativePath}`);
  }
  return fs.readFileSync(absolutePath, 'utf8');
}

function parseUnityVersion(text) {
  const match = text.match(/^m_EditorVersion:\s*(\S+)\s*$/m);
  if (!match) throw new Error('ProjectVersion.txt does not contain m_EditorVersion');
  return match[1];
}

function parseVrchatVersion(text, packages) {
  const manifest = JSON.parse(text);
  const versions = Object.fromEntries(packages.map((name) => [name, manifest.dependencies?.[name]]));
  for (const [name, version] of Object.entries(versions)) {
    if (typeof version !== 'string' || !version.trim()) {
      throw new Error(`Packages/manifest.json missing pinned package ${name}`);
    }
  }
  const unique = [...new Set(Object.values(versions))];
  if (unique.length !== 1) {
    throw new Error(`VRChat package versions diverge: ${JSON.stringify(versions)}`);
  }
  return { version: unique[0], packages: versions };
}

function parseBlenderVersion(text) {
  const versions = [...text.matchAll(/\bBlender\s+(\d+\.\d+)\b/g)].map((match) => match[1]);
  const unique = [...new Set(versions)];
  if (unique.length !== 1) {
    throw new Error(`Taskfile.yml must expose exactly one Blender major.minor, found: ${unique.join(', ') || 'none'}`);
  }
  return unique[0];
}

function numericVersionParts(version) {
  const parts = String(version).match(/\d+/g);
  if (!parts?.length) throw new Error(`invalid version: ${version}`);
  return parts.map(Number);
}

export function compareVersions(left, right) {
  const a = numericVersionParts(left);
  const b = numericVersionParts(right);
  const length = Math.max(a.length, b.length);
  for (let index = 0; index < length; index += 1) {
    const delta = (a[index] ?? 0) - (b[index] ?? 0);
    if (delta !== 0) return Math.sign(delta);
  }
  return 0;
}

export function loadKnowledgePolicy(root = defaultRoot) {
  const policyPath = path.join(root, 'config', 'agent-knowledge.json');
  const policy = readJson(policyPath);
  if (policy.schema_version !== 1) throw new Error('agent-knowledge schema_version must be 1');
  if (!Array.isArray(policy.authority_order) || policy.authority_order.length < 2) {
    throw new Error('agent-knowledge authority_order is missing');
  }
  return policy;
}

export function resolveProjectVersions(root = defaultRoot, policy = loadKnowledgePolicy(root)) {
  const sources = policy.version_sources ?? {};
  const unityText = readRequired(root, sources.unity?.path);
  const vrchatText = readRequired(root, sources.vrchat_sdk?.path);
  const blenderText = readRequired(root, sources.blender?.path);

  const vrchat = parseVrchatVersion(vrchatText, sources.vrchat_sdk?.packages ?? []);
  return {
    unity: parseUnityVersion(unityText),
    vrchat_sdk: vrchat.version,
    vrchat_packages: vrchat.packages,
    blender: parseBlenderVersion(blenderText)
  };
}

export function resolveKnowledgeClaim(projectVersions, claim) {
  const supportedTools = new Set(['unity', 'vrchat_sdk', 'blender']);
  if (!supportedTools.has(claim?.tool)) {
    throw new Error(`unsupported tool claim: ${claim?.tool ?? 'missing'}`);
  }
  const pinned = projectVersions[claim.tool];
  const reasons = [];

  if (!claim.minimum_version && !claim.exact_version) {
    throw new Error('claim must provide minimum_version or exact_version');
  }
  if (claim.minimum_version && compareVersions(pinned, claim.minimum_version) < 0) {
    reasons.push(`requires ${claim.tool} >= ${claim.minimum_version}, project pins ${pinned}`);
  }
  if (claim.exact_version && compareVersions(pinned, claim.exact_version) !== 0) {
    reasons.push(`requires ${claim.tool} == ${claim.exact_version}, project pins ${pinned}`);
  }

  return {
    status: reasons.length ? 'incompatible' : 'compatible',
    tool: claim.tool,
    pinned_version: pinned,
    claim: {
      ...(claim.minimum_version ? { minimum_version: claim.minimum_version } : {}),
      ...(claim.exact_version ? { exact_version: claim.exact_version } : {})
    },
    reasons
  };
}

function parseArgs(args) {
  const result = {};
  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    if (!arg.startsWith('--')) throw new Error(`unexpected argument: ${arg}`);
    const key = arg.slice(2).replaceAll('-', '_');
    const value = args[index + 1];
    if (!value || value.startsWith('--')) throw new Error(`missing value for ${arg}`);
    result[key] = value;
    index += 1;
  }
  return result;
}

export function resolveKnowledge(root = defaultRoot, claim = null) {
  const policy = loadKnowledgePolicy(root);
  const project_versions = resolveProjectVersions(root, policy);
  return {
    schema_version: policy.schema_version,
    authority_order: policy.authority_order.map(({ id }) => id),
    project_versions,
    ...(claim ? { compatibility: resolveKnowledgeClaim(project_versions, claim) } : {})
  };
}

const isCli = process.argv[1] && path.resolve(process.argv[1]) === modulePath;
if (isCli) {
  try {
    const args = parseArgs(process.argv.slice(2));
    const claim = args.tool
      ? {
          tool: args.tool,
          ...(args.minimum_version ? { minimum_version: args.minimum_version } : {}),
          ...(args.exact_version ? { exact_version: args.exact_version } : {})
        }
      : null;
    const result = resolveKnowledge(defaultRoot, claim);
    console.log(JSON.stringify(result, null, 2));
    if (result.compatibility?.status === 'incompatible') process.exitCode = 2;
  } catch (error) {
    console.error(`Agent knowledge resolver FAIL: ${error.message}`);
    process.exitCode = 1;
  }
}
