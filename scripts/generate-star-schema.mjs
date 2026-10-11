import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const schemaPath = 'config/star-schema.json';
const checkOnly = process.argv.includes('--check');

const fail = (message) => {
  throw new Error(message);
};

const readText = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8').replace(/\r\n/g, '\n');

const assertColumns = (row, columns, label) => {
  const actual = Object.keys(row).sort().join(',');
  const expected = [...columns].sort().join(',');
  if (actual !== expected) fail(`${label}: row columns [${actual}] must equal [${expected}]`);
};

const taskKeys = () => {
  const block = readText('Taskfile.yml').split(/^tasks:\s*$/m)[1];
  if (block === undefined) fail('Taskfile.yml has no tasks: block');
  return new Set([...block.matchAll(/^ {2}([A-Za-z0-9_:.-]+):\s*$/gm)].map((match) => match[1]));
};

const verifyDimensionRow = (dimName, dim, row, tasks) => {
  const value = row[dim.verify.column];
  if (dim.verify.kind === 'path_exists') {
    if (!fs.existsSync(path.join(root, value))) fail(`${dimName}: missing canonical path ${value}`);
  } else if (dim.verify.kind === 'taskfile_task') {
    if (!tasks.has(value)) fail(`${dimName}: ${value} is not a task in Taskfile.yml`);
  } else {
    fail(`${dimName}: unknown verify kind ${dim.verify.kind}`);
  }
};

const main = () => {
  const schema = JSON.parse(readText(schemaPath));
  if (schema.schema_version !== 1) fail(`${schemaPath}: schema_version must be 1`);

  const dims = new Map();
  for (const [name, dim] of Object.entries(schema.dimensions ?? {})) {
    if (!name.startsWith('dim_')) fail(`dimension name must start with dim_: ${name}`);
    if (!dim.columns?.includes(dim.key)) fail(`${name}: key ${dim.key} must be one of its columns`);
    if (!Array.isArray(dim.rows) || dim.rows.length === 0) fail(`${name}: rows are required`);
    const byKey = new Map();
    dim.rows.forEach((row, index) => {
      assertColumns(row, dim.columns, `${name}[${index}]`);
      if (byKey.has(row[dim.key])) fail(`${name}: duplicate ${dim.key} ${row[dim.key]}`);
      byKey.set(row[dim.key], row);
    });
    dims.set(name, { ...dim, byKey });
  }

  const tasks = taskKeys();
  for (const [name, dim] of dims) {
    if (!dim.verify) continue;
    for (const row of dim.rows) verifyDimensionRow(name, dim, row, tasks);
  }

  const facts = new Map();
  for (const [name, fact] of Object.entries(schema.facts ?? {})) {
    if (!name.startsWith('fct_')) fail(`fact name must start with fct_: ${name}`);
    if (!Array.isArray(fact.columns) || !fact.line) fail(`${name}: columns and line are required`);
    for (const [column, target] of Object.entries(fact.foreign_keys ?? {})) {
      if (!fact.columns.includes(column)) fail(`${name}: foreign key ${column} is not a column`);
      const dim = dims.get(target) ?? fail(`${name}: ${column} references unknown dimension ${target}`);
      if (dim.key !== column) fail(`${name}: ${column} must match the key ${dim.key} of ${target}`);
    }
    const seen = new Set();
    fact.rows.forEach((row, index) => {
      const label = `${name}[${index}]`;
      assertColumns(row, fact.columns, label);
      for (const [column, target] of Object.entries(fact.foreign_keys ?? {})) {
        if (!dims.get(target).byKey.has(row[column])) fail(`${label}: ${column} ${row[column]} has no row in ${target}`);
      }
      const naturalKey = (fact.natural_key ?? []).map((column) => row[column]).join('\u0000');
      if (seen.has(naturalKey)) fail(`${label}: duplicate natural key ${fact.natural_key.join(',')}`);
      seen.add(naturalKey);
    });
    facts.set(name, { ...fact, name });
  }

  const referenced = new Map();
  for (const fact of facts.values()) {
    for (const [column, target] of Object.entries(fact.foreign_keys ?? {})) {
      if (!referenced.has(target)) referenced.set(target, new Set());
      for (const row of fact.rows) referenced.get(target).add(row[column]);
    }
  }
  for (const [name, dim] of dims) {
    for (const key of dim.byKey.keys()) {
      if (!referenced.get(name)?.has(key)) fail(`${name}: ${key} is not referenced by any fact`);
    }
  }

  const readCache = new Map();
  const readSource = (sourcePath, context) => {
    if (readCache.has(sourcePath)) return readCache.get(sourcePath);
    const absolute = path.join(root, sourcePath);
    if (!fs.existsSync(absolute)) fail(`${context}: missing source ${sourcePath}`);
    if (!fs.statSync(absolute).isFile()) fail(`${context}: source is not a file ${sourcePath}`);
    const text = readText(sourcePath);
    readCache.set(sourcePath, text);
    return text;
  };

  const resolveRead = (read, sourcePath, context) => {
    if (!read.json && !read.pattern) fail(`${context}: read needs json or pattern`);
    const text = readSource(sourcePath, context);
    let value = text;
    if (read.json) {
      let data;
      try {
        data = JSON.parse(text);
      } catch {
        fail(`${context}: ${sourcePath} is not valid JSON`);
      }
      value = read.json.reduce((node, key) => node?.[key], data);
      if (value === undefined) fail(`${context}: ${sourcePath} has no ${read.json.join('.')}`);
    }
    if (read.pattern) {
      if (typeof value !== 'string') fail(`${context}: pattern needs a string in ${sourcePath}`);
      const match = new RegExp(read.pattern, 'm').exec(value);
      if (!match) fail(`${context}: pattern ${read.pattern} did not match ${sourcePath}`);
      value = match[read.group ?? 1];
    }
    if (typeof value !== 'string' && typeof value !== 'number') fail(`${context}: ${sourcePath} value is not a scalar`);
    const resolved = String(value);
    if (resolved === '') fail(`${context}: ${sourcePath} value is empty`);
    return resolved;
  };

  for (const fact of facts.values()) {
    if (!fact.columns.includes('read')) continue;
    fact.rows = fact.rows.map((row, index) => {
      const sourcePath = dims.get('dim_source').byKey.get(row.source_key).path;
      return { ...row, value: resolveRead(row.read, sourcePath, `${fact.name}[${index}]`) };
    });
  }

  const consumed = new Set();
  const renderLine = (fact, row) => fact.line.replace(/\{([a-z_]+(?:\.[a-z_]+)?)\}/g, (_, token) => {
    if (!token.includes('.')) {
      if (!(token in row)) fail(`${fact.name}: line references unknown column ${token}`);
      return String(row[token]);
    }
    const [alias, column] = token.split('.');
    const foreignKey = `${alias}_key`;
    const target = fact.foreign_keys?.[foreignKey] ?? fail(`${fact.name}: line references unlinked ${token}`);
    const dimRow = dims.get(target).byKey.get(row[foreignKey]);
    if (!(column in dimRow)) fail(`${fact.name}: ${target} has no column ${column}`);
    return String(dimRow[column]);
  });

  const renderView = (view) => {
    if (!view.output || !view.title || !Array.isArray(view.sections) || !Array.isArray(view.fact_tables)) {
      fail(`view ${view.output ?? '(unnamed)'}: output, title, sections and fact_tables are required`);
    }
    if (view.output.startsWith('pages/')) fail(`view ${view.output}: pages/ is public surface and takes no engineering ledger`);
    const out = [
      `<!-- Generated from ${schemaPath} by scripts/generate-star-schema.mjs. Do not edit; change the schema and regenerate. -->`,
      '',
      `# ${view.title}`,
    ];
    for (const sectionKey of view.sections) {
      const section = dims.get('dim_section').byKey.get(sectionKey) ?? fail(`view ${view.output}: unknown section ${sectionKey}`);
      const lines = [];
      for (const factName of view.fact_tables) {
        const fact = facts.get(factName) ?? fail(`view ${view.output}: unknown fact table ${factName}`);
        fact.rows.forEach((row, index) => {
          if (row.section_key !== sectionKey) return;
          consumed.add(`${factName}[${index}]`);
          lines.push(renderLine(fact, row));
        });
      }
      if (lines.length === 0) fail(`view ${view.output}: section ${sectionKey} has no rows`);
      out.push('', `## ${section.heading}`, '', ...lines);
    }
    return `${out.join('\n')}\n`;
  };

  const outputs = new Map();
  for (const view of schema.views ?? []) {
    if (outputs.has(view.output)) fail(`duplicate view output ${view.output}`);
    outputs.set(view.output, renderView(view));
  }

  for (const fact of facts.values()) {
    fact.rows.forEach((_, index) => {
      if (!consumed.has(`${fact.name}[${index}]`)) fail(`${fact.name}[${index}] is not rendered by any view`);
    });
  }

  for (const [output, content] of outputs) {
    const absolute = path.join(root, output);
    if (checkOnly) {
      const current = fs.existsSync(absolute) ? readText(output) : null;
      if (current !== content) fail(`${output} is stale; run node scripts/generate-star-schema.mjs`);
    } else {
      fs.mkdirSync(path.dirname(absolute), { recursive: true });
      fs.writeFileSync(absolute, content);
    }
  }

  console.log(JSON.stringify({
    status: checkOnly ? 'PASS' : 'WRITTEN',
    schema: schemaPath,
    dimensions: Object.fromEntries([...dims].map(([name, dim]) => [name, dim.rows.length])),
    facts: Object.fromEntries([...facts].map(([name, fact]) => [name, fact.rows.length])),
    outputs: [...outputs.keys()],
  }, null, 2));
};

try {
  main();
} catch (error) {
  console.error(`Star schema FAIL: ${error.message}`);
  process.exitCode = 1;
}
