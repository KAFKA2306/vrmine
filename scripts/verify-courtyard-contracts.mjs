import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const specRel = 'config/world-design/generated/rain-fabric-courtyard-v0.json';
const schemaRel = 'config/world-design/world-design-spec.schema.json';
const courtyardCodeDir = path.join(root, 'Assets', 'KafkaMade', 'VRMine', 'Courtyard');

const SDK310_FORBIDDEN_PATTERNS = [
  { id: 'VRCPhysBone', regex: /\bVRCPhysBone\b/ },
  { id: 'VRCContactReceiver', regex: /\bVRCContactReceiver\b/ },
  { id: 'VRCContactSender', regex: /\bVRCContactSender\b/ },
  { id: 'OnPhysBoneGrabbed', regex: /\bOnPhysBoneGrabbed\b/ },
  { id: 'OnContactEnter', regex: /\bOnContactEnter\b/ },
  { id: 'VRCTween', regex: /\bVRCTween\b/ },
  { id: 'WorldQualitySettings', regex: /\bWorldQualitySettings\b/ },
  { id: 'VRCBillboard', regex: /\bVRCBillboard\b/ },
  { id: 'VRCVelocityTracker', regex: /\bVRCVelocityTracker\b/ },
  { id: 'VRCPlacementZone', regex: /\bVRCPlacementZone\b/ },
  { id: 'UnityEngine.Cloth', regex: /\b(?:UnityEngine\.)?Cloth\b/ },
  { id: 'EventTiming.FixedUpdate', regex: /\bEventTiming\.FixedUpdate\b/ },
  { id: 'EventTiming.PostLateUpdate', regex: /\bEventTiming\.PostLateUpdate\b/ },
  { id: 'AutoHold.AutoDetect', regex: /\bAutoHold\.AutoDetect\b/ },
];

export function scanUdonSourceForSdk39Violations(sourceText, fileLabel = '<memory>') {
  const violations = [];
  for (const rule of SDK310_FORBIDDEN_PATTERNS) {
    if (rule.regex.test(sourceText)) {
      violations.push(`${fileLabel}: forbidden SDK 3.10+/Quest-incompatible API '${rule.id}'`);
    }
  }
  return violations;
}

export function validateCourtyardSpec(spec, schema) {
  const errors = [];
  if (!spec || typeof spec !== 'object') {
    return ['spec is missing or not an object'];
  }
  for (const key of schema.required || []) {
    if (!(key in spec)) errors.push(`missing required top-level key: ${key}`);
  }
  if (spec.meta?.platform_target !== 'CROSS_PLATFORM_QUEST') {
    errors.push(`platform_target must be CROSS_PLATFORM_QUEST, got ${spec.meta?.platform_target}`);
  }
  if (spec.meta?.zero_text_discovery !== true) {
    errors.push('meta.zero_text_discovery must be true');
  }
  if (typeof spec.meta?.first_touch_target_sec !== 'number' || spec.meta.first_touch_target_sec > 10) {
    errors.push('meta.first_touch_target_sec must be <= 10');
  }
  if (spec.media_governance?.text_signage_count !== 0) {
    errors.push('media_governance.text_signage_count must be 0');
  }
  if (spec.runtime_budget?.realtime_light_count !== 0) {
    errors.push('runtime_budget.realtime_light_count must be 0');
  }
  if (spec.runtime_budget?.post_processing_on_quest !== false) {
    errors.push('runtime_budget.post_processing_on_quest must be false');
  }
  if (typeof spec.runtime_budget?.max_size_quest_mb !== 'number' || spec.runtime_budget.max_size_quest_mb > 28) {
    errors.push('runtime_budget.max_size_quest_mb must be <= 28');
  }
  if (typeof spec.runtime_budget?.max_manual_sync_bytes !== 'number' || spec.runtime_budget.max_manual_sync_bytes > 40) {
    errors.push('runtime_budget.max_manual_sync_bytes must be <= 40');
  }
  if ((spec.spatial_geometry?.corridor_clearance_m ?? 0) < 1.0) {
    errors.push('DRC-01 violated: corridor_clearance_m must be >= 1.0');
  }
  if ((spec.retreat_protocol?.visual_shield_angle_deg ?? 0) < 90) {
    errors.push('DRC-02 violated: visual_shield_angle_deg must be >= 90');
  }
  if ((spec.retreat_protocol?.acoustic_attenuation_db ?? 0) > -12) {
    errors.push('DRC-02 violated: acoustic_attenuation_db must be <= -12');
  }
  if ((spec.runtime_budget?.background_max_distance_m ?? Infinity) > 150) {
    errors.push('DRC-05 violated: background_max_distance_m must be <= 150');
  }
  if (!Array.isArray(spec.tactile_toys) || spec.tactile_toys.length !== 4) {
    errors.push('tactile_toys must define exactly 4 core toys');
  } else {
    const totalManualBytes = spec.tactile_toys.reduce((sum, toy) => sum + (Number(toy.sync_bytes) || 0), 0);
    if (totalManualBytes > spec.runtime_budget.max_manual_sync_bytes) {
      errors.push(`sum of tactile_toys sync_bytes (${totalManualBytes}) exceeds budget (${spec.runtime_budget.max_manual_sync_bytes})`);
    }
  }
  if (!Array.isArray(spec.benchmarks) || spec.benchmarks.length < 5) {
    errors.push('benchmarks must define at least 5 acceptance cases (BM-01..BM-05)');
  } else {
    for (const bm of spec.benchmarks) {
      if (!bm.id || !bm.player_action || !bm.visible_state_change || !bm.metrics || !bm.required_evidence?.length) {
        errors.push(`incomplete benchmark definition: ${bm.id ?? '(unnamed)'}`);
      }
    }
  }
  return errors;
}

function runScorerSelfCheck(validSpec, schema) {
  // 1. Known-good case must pass
  assert.deepEqual(validateCourtyardSpec(validSpec, schema), [], 'self-check: known-good spec must pass');
  assert.deepEqual(
    scanUdonSourceForSdk39Violations('public void _OnLocalTouch() { RequestSerialization(); }', 'good.cs'),
    [],
    'self-check: clean SDK 3.9.0 Udon source must pass',
  );

  // 2. Representative defect cases must fail
  const badRealtime = structuredClone(validSpec);
  badRealtime.runtime_budget.realtime_light_count = 1;
  assert.ok(validateCourtyardSpec(badRealtime, schema).length > 0, 'self-check: realtime light defect must fail');

  const badSdkSource = 'public class Bad : UdonSharpBehaviour { VRCPhysBone bone; void X() { VRCTween.Move(); } }';
  const sdkHits = scanUdonSourceForSdk39Violations(badSdkSource, 'defect.cs');
  assert.equal(sdkHits.length, 2, 'self-check: SDK 3.10 defect must flag both VRCPhysBone and VRCTween');

  // 3. Missing evidence case must fail
  const missingEvidence = structuredClone(validSpec);
  missingEvidence.benchmarks[0].required_evidence = [];
  assert.ok(validateCourtyardSpec(missingEvidence, schema).length > 0, 'self-check: missing evidence must fail');
}

function collectCsFiles(dir) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...collectCsFiles(full));
    else if (entry.name.endsWith('.cs')) out.push(full);
  }
  return out;
}

function main() {
  const spec = JSON.parse(fs.readFileSync(path.join(root, specRel), 'utf8'));
  const schema = JSON.parse(fs.readFileSync(path.join(root, schemaRel), 'utf8'));

  runScorerSelfCheck(spec, schema);

  const specErrors = validateCourtyardSpec(spec, schema);
  if (specErrors.length > 0) {
    throw new Error(`Courtyard spec FAIL:\n- ${specErrors.join('\n- ')}`);
  }

  const csFiles = collectCsFiles(courtyardCodeDir);
  const codeViolations = [];
  for (const file of csFiles) {
    const rel = path.relative(root, file).replaceAll('\\', '/');
    codeViolations.push(...scanUdonSourceForSdk39Violations(fs.readFileSync(file, 'utf8'), rel));
  }
  if (codeViolations.length > 0) {
    throw new Error(`Courtyard SDK 3.9.0 static ban FAIL:\n- ${codeViolations.join('\n- ')}`);
  }

  console.log(JSON.stringify({
    status: 'PASS',
    spec: specRel,
    world_id: spec.meta.world_id,
    theme_name: spec.meta.theme_name,
    platform_target: spec.meta.platform_target,
    tactile_toys: spec.tactile_toys.length,
    benchmarks: spec.benchmarks.length,
    scanned_cs_files: csFiles.length,
    sdk310_forbidden_hits: 0,
    scorer_self_check: 'PASS',
  }, null, 2));
}

try {
  main();
} catch (err) {
  console.error(err.message);
  process.exitCode = 1;
}
