import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const contractPath = path.join(root, 'config', 'world-design', 'WORLD_BLOCKOUT_SPEC.json');
const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));

function resolvePointer(value, pointer) {
  return pointer.split('/').slice(1).reduce((current, raw) => {
    if (current === undefined || current === null) return undefined;
    const key = raw.replaceAll('~1', '/').replaceAll('~0', '~');
    return current[key];
  }, value);
}

if (contract?.completion?.operator !== 'all') throw new Error('WORLD_BLOCKOUT_SPEC completion.operator must be all');
if (!Array.isArray(contract?.completion?.requirements) || contract.completion.requirements.length === 0) throw new Error('WORLD_BLOCKOUT_SPEC has no requirements');

for (const requirement of contract.completion.requirements) {
  if (!Array.isArray(requirement.evidence) || requirement.evidence.length === 0) {
    throw new Error(`${requirement.id}: evidence is empty`);
  }
  for (const evidence of requirement.evidence) {
    const relative = contract.canonical_sources?.[evidence.source];
    if (!relative) throw new Error(`${requirement.id}: unknown canonical source ${evidence.source}`);
    const sourcePath = path.join(root, relative);
    if (!fs.existsSync(sourcePath)) throw new Error(`${requirement.id}: canonical source missing: ${relative}`);

    if (evidence.type === 'json_pointer') {
      const source = JSON.parse(fs.readFileSync(sourcePath, 'utf8'));
      if (resolvePointer(source, evidence.pointer) === undefined) {
        throw new Error(`${requirement.id}: unresolved JSON pointer ${evidence.pointer}`);
      }
    } else if (evidence.type === 'repository_file_contains') {
      const text = fs.readFileSync(sourcePath, 'utf8');
      if (!Array.isArray(evidence.tokens) || evidence.tokens.length === 0) throw new Error(`${requirement.id}: repository_file_contains has no tokens`);
      for (const token of evidence.tokens) {
        if (!text.includes(token)) throw new Error(`${requirement.id}: missing token ${JSON.stringify(token)} in ${relative}`);
      }
    } else {
      throw new Error(`${requirement.id}: unsupported evidence type ${evidence.type}`);
    }
  }
}

console.log(`PASS: WORLD_BLOCKOUT_SPEC ${contract.version} verified ${contract.completion.requirements.length} requirements.`);
