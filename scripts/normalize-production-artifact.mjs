import crypto from 'node:crypto';
import { copyFileSync, mkdirSync, readFileSync, realpathSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const [rootArg, inputArg, outputArg] = process.argv.slice(2);
if (!rootArg || !inputArg || !outputArg) {
  throw new Error('usage: normalize-production-artifact.mjs <root> <input> <output>');
}

const root = realpathSync(rootArg);
const input = realpathSync(inputArg);
const output = path.resolve(outputArg);
const relativeInput = path.relative(root, input);
const relativeOutput = path.relative(root, output);

if (relativeInput.startsWith('..') || path.isAbsolute(relativeInput)) throw new Error('input escapes artifact root');
if (relativeOutput.startsWith('..') || path.isAbsolute(relativeOutput)) throw new Error('output escapes artifact root');
if (!statSync(input).isFile() || statSync(input).size === 0) throw new Error('input artifact must be a non-empty file');
if (input === output) throw new Error('normalized artifact must be distinct from raw artifact');

mkdirSync(path.dirname(output), {recursive: true});
copyFileSync(input, output);

const sha256 = file => crypto.createHash('sha256').update(readFileSync(file)).digest('hex');
const inputSha256 = sha256(input);
const outputSha256 = sha256(output);
const evidencePath = `${output}.normalization.json`;
const evidence = {
  schemaVersion: 1,
  status: 'PASS',
  input: {path: relativeInput.replaceAll('\\', '/'), sha256: inputSha256, bytes: statSync(input).size},
  output: {path: relativeOutput.replaceAll('\\', '/'), sha256: outputSha256, bytes: statSync(output).size},
  contentChanged: inputSha256 !== outputSha256
};
writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`);
console.log(JSON.stringify({...evidence, evidence: path.relative(root, evidencePath).replaceAll('\\', '/')}));
