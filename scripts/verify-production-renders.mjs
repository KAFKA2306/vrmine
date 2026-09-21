import fs from 'node:fs';
import path from 'node:path';

export function verifyProductionRenders(root, names) {
  if (!root || !Array.isArray(names) || names.length === 0) throw new Error('render root and expected names are required');
  const normalizedRoot = path.resolve(root);
  for (const name of names) {
    if (path.basename(name) !== name) throw new Error(`invalid render name: ${name}`);
    const file = path.resolve(normalizedRoot, name);
    if (!file.startsWith(`${normalizedRoot}${path.sep}`)) throw new Error(`render escapes root: ${name}`);
    const stat = fs.statSync(file);
    if (!stat.isFile() || stat.size === 0) throw new Error(`empty render evidence: ${name}`);
  }
  return {status: 'PASS', root, renders: names};
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [root, ...names] = process.argv.slice(2);
  try {
    process.stdout.write(`${JSON.stringify(verifyProductionRenders(root, names))}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
