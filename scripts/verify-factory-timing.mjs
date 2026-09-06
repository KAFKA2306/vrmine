import fs from 'node:fs';
import path from 'node:path';

const timingPath = 'config/factory-timing.json';
const timing = JSON.parse(fs.readFileSync(timingPath, 'utf8'));
const fail = (message) => { throw new Error(message); };

if (timing.schema_version !== 1) fail('factory timing schema_version must be 1');
if (!Array.isArray(timing.records) || timing.records.length < 5) fail('at least 5 actual SKU timing records are required');
if (timing.target_seconds?.generation_to_production_min !== 3600 || timing.target_seconds?.generation_to_production_max !== 7200) fail('1-2 hour target must remain separate from actual observations');

const successful = [];
const previews = [];
for (const record of timing.records) {
  for (const key of ['sku', 'source_sha', 'spec_sha256', 'factory_run_id', 'pages_run_id', 'generation_started_at', 'preview_completed_at', 'generation_to_preview_seconds', 'manual_touch', 'outcome']) {
    if (record[key] === undefined) fail(`${record.sku ?? 'unknown'} missing ${key}`);
  }
  if (!/^[0-9a-f]{40}$/.test(record.source_sha)) fail(`${record.sku} source_sha is not exact`);
  if (!/^[0-9a-f]{64}$/.test(record.spec_sha256)) fail(`${record.sku} spec_sha256 is not exact`);
  if (record.manual_touch === 0) fail(`${record.sku} must not coerce unobserved manual touch to 0`);
  if (record.manual_touch !== 'UNKNOWN' && !Number.isInteger(record.manual_touch)) fail(`${record.sku} manual_touch must be UNKNOWN or an observed integer`);

  const manifestPath = path.join('pages', 'io', 'items', record.sku, 'manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  if (manifest.id !== record.sku) fail(`${record.sku} manifest id mismatch`);
  if (manifest.spec_sha256 !== record.spec_sha256) fail(`${record.sku} spec digest drift`);

  const previewSeconds = Math.round((Date.parse(record.preview_completed_at) - Date.parse(record.generation_started_at)) / 1000);
  if (previewSeconds !== record.generation_to_preview_seconds) fail(`${record.sku} preview duration mismatch`);
  previews.push(previewSeconds);

  if (record.outcome === 'success') {
    if (!record.production_readback_completed_at || !Number.isFinite(record.generation_to_production_seconds)) fail(`${record.sku} successful production record lacks read-back timing`);
    const productionSeconds = Math.round((Date.parse(record.production_readback_completed_at) - Date.parse(record.generation_started_at)) / 1000);
    if (productionSeconds !== record.generation_to_production_seconds) fail(`${record.sku} production duration mismatch`);
    successful.push(productionSeconds);
  } else if (!record.failed_stage) {
    fail(`${record.sku} failed record must name the observed failed stage`);
  }
}

const median = (values) => {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
};

if (timing.summary.sample_count !== timing.records.length) fail('summary sample_count drift');
if (timing.summary.successful_production_count !== successful.length) fail('summary successful_production_count drift');
if (timing.summary.failed_run_count !== timing.records.length - successful.length) fail('summary failed_run_count drift');
if (timing.summary.median_generation_to_preview_seconds !== median(previews)) fail('summary preview median drift');
if (timing.summary.median_generation_to_production_seconds !== median(successful)) fail('summary production median drift');
if (timing.summary.manual_touch !== 'UNKNOWN') fail('summary must retain UNKNOWN manual touch until observed');
if (timing.summary.existing_follow_up_issue !== 246) fail('largest observed cost must route to existing performance issue #246');
if (timing.summary.target_1_to_2_hours_compatible !== successful.every((seconds) => seconds <= timing.target_seconds.generation_to_production_max)) fail('target compatibility does not match actual successful runs');

console.log(JSON.stringify({
  records: timing.records.length,
  successful: successful.length,
  failed: timing.records.length - successful.length,
  median_generation_to_preview_seconds: median(previews),
  median_generation_to_production_seconds: median(successful),
  target_1_to_2_hours_compatible: timing.summary.target_1_to_2_hours_compatible,
  manual_touch: timing.summary.manual_touch,
  existing_follow_up_issue: timing.summary.existing_follow_up_issue
}, null, 2));
