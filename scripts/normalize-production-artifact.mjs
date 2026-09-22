import { copyFileSync, mkdirSync, realpathSync, statSync } from 'node:fs';
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
console.log(JSON.stringify({status: 'PASS', input: relativeInput, output: relativeOutput}));
