import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const modulePath = fileURLToPath(import.meta.url);
const repoRoot = path.resolve(path.dirname(modulePath), '..');
const configPath = path.join(repoRoot, 'config', 'always-on-agent.json');

export function loadConfig(root = repoRoot) {
  const cfg = JSON.parse(fs.readFileSync(path.join(root, 'config', 'always-on-agent.json'), 'utf8'));
  if (cfg.schema_version !== 1) throw new Error('always-on-agent config schema_version must be 1');
  return cfg;
}

export function resolveStateDir(cfg, root = repoRoot, overrideDir = null) {
  const envDir = process.env[cfg.state.env_override];
  const raw = overrideDir || envDir || path.join(root, cfg.state.default_dir);
  return path.resolve(raw);
}

function nowIso() {
  return new Date().toISOString();
}

function jstHour(date = new Date()) {
  const utcHours = date.getUTCHours();
  return (utcHours + 9) % 24;
}

function isWithinActiveWindow(cfg, date = new Date()) {
  const h = jstHour(date);
  const { start_hour: start, end_hour: end } = cfg.schedule.active_window_jst;
  return h >= start && h < end;
}

function readJsonOrNull(filePath) {
  if (!fs.existsSync(filePath)) return null;
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function writeJsonAtomic(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const tmp = `${filePath}.${process.pid}.${crypto.randomBytes(4).toString('hex')}.tmp`;
  fs.writeFileSync(tmp, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
  fs.renameSync(tmp, filePath);
}

function isPidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    return err?.code === 'EPERM';
  }
}

export function acquireLock(lockPath, { waitMs = 3000, pollMs = 50, metadata = {} } = {}) {
  fs.mkdirSync(path.dirname(lockPath), { recursive: true });
  const deadline = Date.now() + waitMs;
  while (true) {
    try {
      const fd = fs.openSync(lockPath, 'wx');
      const payload = {
        pid: process.pid,
        acquired_at: nowIso(),
        ...metadata,
      };
      fs.writeFileSync(fd, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');
      fs.closeSync(fd);
      return {
        acquired: true,
        release() {
          try {
            const current = readJsonOrNull(lockPath);
            if (!current || current.pid === process.pid) {
              fs.rmSync(lockPath, { force: true });
            }
          } catch {}
        },
      };
    } catch (err) {
      if (err?.code !== 'EEXIST') throw err;
      const existing = readJsonOrNull(lockPath);
      if (existing && !isPidAlive(existing.pid)) {
        fs.rmSync(lockPath, { force: true });
        continue;
      }
      if (Date.now() >= deadline) {
        return { acquired: false, holder: existing };
      }
      const until = Date.now() + pollMs;
      while (Date.now() < until) {
        // bounded short spin for synchronous lock wait
      }
    }
  }
}

export function ensureLedgers(cfg, stateDir) {
  fs.mkdirSync(stateDir, { recursive: true });
  fs.mkdirSync(path.join(stateDir, cfg.state.runs_dir), { recursive: true });

  const workPath = path.join(stateDir, cfg.state.work_ledger);
  const questionPath = path.join(stateDir, cfg.state.question_ledger);

  let workLedger = readJsonOrNull(workPath);
  if (!workLedger) {
    workLedger = {
      schema_version: 1,
      agent_id: cfg.agent_id,
      schedule: cfg.schedule.task_name,
      active_window_jst: cfg.schedule.active_window_jst,
      last_observed_revision: null,
      last_tick_at: null,
      metrics: {
        tick_count: 0,
        llm_calls: 0,
        duplicate_writes: 0,
        concurrent_defers: 0,
      },
      items: [],
    };
    writeJsonAtomic(workPath, workLedger);
  }

  let questionLedger = readJsonOrNull(questionPath);
  if (!questionLedger) {
    questionLedger = {
      schema_version: 1,
      agent_id: cfg.agent_id,
      questions: [],
    };
    writeJsonAtomic(questionPath, questionLedger);
  }

  return { workPath, questionPath, workLedger, questionLedger };
}

export function upsertQuestion(questionLedger, entry) {
  const existing = questionLedger.questions.find(
    (q) => q.work_id === entry.work_id && q.subject_key === entry.subject_key,
  );
  const timestamp = nowIso();
  if (existing) {
    if (existing.status === 'open') {
      existing.question = entry.question;
      existing.rationale = entry.rationale;
      existing.checked_alternatives = entry.checked_alternatives;
      existing.blocked_step = entry.blocked_step;
      existing.resume_condition = entry.resume_condition;
      existing.updated_at = timestamp;
    }
    return existing;
  }
  const created = {
    question_id: `q-${crypto.createHash('sha1').update(`${entry.work_id}:${entry.subject_key}`).digest('hex').slice(0, 8)}`,
    work_id: entry.work_id,
    subject_key: entry.subject_key,
    question: entry.question,
    responder: entry.responder || 'human:owner',
    rationale: entry.rationale,
    checked_alternatives: entry.checked_alternatives || [],
    blocked_step: entry.blocked_step,
    resume_condition: entry.resume_condition,
    status: 'open',
    answer_ref: null,
    created_at: timestamp,
    updated_at: timestamp,
  };
  questionLedger.questions.push(created);
  return created;
}

export function answerQuestion(cfg, stateDir, { questionId, answerRef, status = 'answered' }) {
  const { workPath, questionPath, workLedger, questionLedger } = ensureLedgers(cfg, stateDir);
  const q = questionLedger.questions.find((item) => item.question_id === questionId);
  if (!q) throw new Error(`Question not found: ${questionId}`);
  q.status = status;
  q.answer_ref = answerRef;
  q.updated_at = nowIso();
  writeJsonAtomic(questionPath, questionLedger);

  const work = workLedger.items.find((item) => item.work_id === q.work_id);
  if (work && work.status === 'awaiting_human') {
    work.status = 'queued';
    work.updated_at = nowIso();
    writeJsonAtomic(workPath, workLedger);
  }
  return q;
}

export function enqueueWork(cfg, stateDir, spec) {
  const { workPath, workLedger } = ensureLedgers(cfg, stateDir);
  const timestamp = nowIso();
  let item = workLedger.items.find((x) => x.work_id === spec.work_id);
  if (!item) {
    item = {
      work_id: spec.work_id,
      title: spec.title || spec.work_id,
      target_revision: spec.target_revision || null,
      idempotency_key: spec.idempotency_key || spec.work_id,
      status: 'queued',
      steps: spec.steps || [],
      completed_steps: [],
      next_step: (spec.steps && spec.steps[0]?.id) || null,
      last_verified_artifact: null,
      external_writes: [],
      question_ids: [],
      last_event_id: null,
      last_run_id: null,
      last_error: null,
      created_at: timestamp,
      updated_at: timestamp,
    };
    workLedger.items.push(item);
  } else {
    if (spec.title) item.title = spec.title;
    if (spec.steps?.length) item.steps = spec.steps;
    if (spec.target_revision) item.target_revision = spec.target_revision;
    item.updated_at = timestamp;
  }
  writeJsonAtomic(workPath, workLedger);
  return item;
}

function runCmd(command, args, { cwd = repoRoot, timeoutMs = 15000 } = {}) {
  const res = spawnSync(command, args, {
    cwd,
    encoding: 'utf8',
    timeout: timeoutMs,
    windowsHide: true,
  });
  return {
    status: res.status ?? 1,
    stdout: (res.stdout || '').trim(),
    stderr: (res.stderr || '').trim(),
    error: res.error ? String(res.error) : null,
  };
}

function runHostProbes(root = repoRoot) {
  const head = runCmd('git', ['rev-parse', 'HEAD'], { cwd: root });
  const branch = runCmd('git', ['rev-parse', '--abbrev-ref', 'HEAD'], { cwd: root });
  const porcelain = runCmd('git', ['status', '--porcelain'], { cwd: root });
  const ledgerCheck = runCmd('node', ['scripts/generate-star-schema.mjs', '--check'], { cwd: root });
  const gatesCheck = runCmd('node', ['scripts/verify-quality-gates.mjs'], { cwd: root });

  const unityLicExe = 'C:\\Program Files\\Unity\\Hub\\Editor\\2022.3.22f1\\Editor\\Data\\Resources\\Licensing\\Client\\Unity.Licensing.Client.exe';
  let unityLicenseActive = false;
  let unityLicenseNote = 'licensing client not found';
  if (fs.existsSync(unityLicExe)) {
    const lic = runCmd(unityLicExe, ['--showEntitlements'], { cwd: root });
    unityLicenseActive = lic.status === 0 && !/No licenses were found/i.test(`${lic.stdout}\n${lic.stderr}`);
    unityLicenseNote = unityLicenseActive ? 'active' : 'No licenses were found.';
  }

  return {
    observed_at: nowIso(),
    git_head: head.status === 0 ? head.stdout : null,
    git_branch: branch.status === 0 ? branch.stdout : null,
    git_dirty: porcelain.stdout.length > 0,
    star_schema_pass: ledgerCheck.status === 0,
    quality_gates_pass: gatesCheck.status === 0,
    unity_license_active: unityLicenseActive,
    unity_license_note: unityLicenseNote,
  };
}

function defaultStepExecutor({ step, item, stateDir, probes }) {
  const extSinkPath = path.join(stateDir, 'external-writes-sink.json');
  const readSink = () => readJsonOrNull(extSinkPath) || { records: [] };
  const writeSink = (sink) => writeJsonAtomic(extSinkPath, sink);

  if (step.kind === 'external_write') {
    const writeKey = `${item.idempotency_key}:${step.id}`;
    const sink = readSink();
    const alreadyWritten = sink.records.find((r) => r.key === writeKey);
    if (alreadyWritten) {
      return {
        ok: true,
        artifact: `${extSinkPath}#${writeKey}`,
        externalWrite: { key: writeKey, target: extSinkPath, verified_at: alreadyWritten.written_at, reused: true },
      };
    }
    const record = { key: writeKey, work_id: item.work_id, step_id: step.id, written_at: nowIso() };
    sink.records.push(record);
    writeSink(sink);
    if (step.simulate_disconnect_after_write) {
      return { ok: false, disconnectedAfterWrite: true, error: 'simulated response disconnect after external write' };
    }
    return {
      ok: true,
      artifact: `${extSinkPath}#${writeKey}`,
      externalWrite: { key: writeKey, target: extSinkPath, verified_at: record.written_at, reused: false },
    };
  }

  if (step.kind === 'fail') {
    return { ok: false, error: step.error || 'step execution failed', permissionDenied: Boolean(step.permission_denied) };
  }

  if (step.kind === 'probe_check') {
    const passed = Boolean(probes?.star_schema_pass && probes?.quality_gates_pass);
    return passed
      ? { ok: true, artifact: `probe:${probes.git_head}` }
      : { ok: false, error: 'repository probe check failed' };
  }

  return { ok: true, artifact: `step-ok:${item.work_id}:${step.id}` };
}

export function runTick({
  cfg = loadConfig(),
  root = repoRoot,
  stateDir = resolveStateDir(cfg, root),
  eventId = null,
  forceWindow = false,
  noLlm = false,
  skipHostProbes = false,
  stepExecutor = defaultStepExecutor,
  llmExecutor = null,
  maxStepsThisTick = Infinity,
  lockWaitMs = 3000,
} = {}) {
  const { workPath, questionPath } = ensureLedgers(cfg, stateDir);
  const lockPath = path.join(stateDir, cfg.state.lock_file);
  const runId = `run-${Date.now()}-${crypto.randomBytes(3).toString('hex')}`;
  const effectiveEventId = eventId || `tick-${new Date().toISOString().slice(0, 16)}`;

  const lock = acquireLock(lockPath, { waitMs: lockWaitMs, metadata: { run_id: runId, event_id: effectiveEventId } });
  if (!lock.acquired) {
    return {
      status: 'DEFERRED_LOCKED',
      run_id: runId,
      event_id: effectiveEventId,
      holder: lock.holder,
    };
  }

  try {
    const { workLedger, questionLedger } = ensureLedgers(cfg, stateDir);
    workLedger.metrics.tick_count += 1;

    if (!forceWindow && !isWithinActiveWindow(cfg)) {
      workLedger.last_tick_at = nowIso();
      writeJsonAtomic(workPath, workLedger);
      return {
        status: 'SKIPPED_OUTSIDE_WINDOW',
        run_id: runId,
        event_id: effectiveEventId,
        jst_hour: jstHour(),
        llm_calls: 0,
      };
    }

    const probes = skipHostProbes
      ? { observed_at: nowIso(), git_head: 'test-rev', git_branch: 'main', git_dirty: false, star_schema_pass: true, quality_gates_pass: true, unity_license_active: true }
      : runHostProbes(root);

    // Reconcile open unity_license questions automatically if host license became active
    if (probes.unity_license_active) {
      for (const q of questionLedger.questions) {
        if (q.subject_key === 'unity_license' && q.status === 'open') {
          q.status = 'resolved';
          q.answer_ref = 'probe:unity-licensing-client-active';
          q.updated_at = nowIso();
        }
      }
    }

    const revisionDelta = workLedger.last_observed_revision !== probes.git_head;
    let stepsExecuted = 0;
    let llmCallsThisTick = 0;
    const stepResults = [];

    for (const item of workLedger.items) {
      if (item.status === 'verified') continue;
      item.last_event_id = effectiveEventId;
      item.last_run_id = runId;
      item.target_revision = item.target_revision || probes.git_head;

      let blockedByQuestion = false;
      let failed = false;

      for (const step of item.steps) {
        if (item.completed_steps.includes(step.id)) continue;
        if (stepsExecuted >= maxStepsThisTick) {
          item.next_step = step.id;
          item.status = 'running';
          break;
        }

        if (step.requires_human) {
          const existingQ = questionLedger.questions.find(
            (q) => q.work_id === item.work_id && q.subject_key === step.requires_human.subject_key,
          );
          if (!existingQ || existingQ.status === 'open') {
            const q = upsertQuestion(questionLedger, {
              work_id: item.work_id,
              subject_key: step.requires_human.subject_key,
              question: step.requires_human.question,
              responder: step.requires_human.responder || 'human:owner',
              rationale: step.requires_human.rationale || 'Human authorization or credential is required.',
              checked_alternatives: step.requires_human.checked_alternatives || ['Automated non-interactive check'],
              blocked_step: step.id,
              resume_condition: step.requires_human.resume_condition || 'Record answer in question ledger and rerun tick.',
            });
            if (!item.question_ids.includes(q.question_id)) item.question_ids.push(q.question_id);
            blockedByQuestion = true;
            item.next_step = step.id;
            // Continue inspecting independent steps that do NOT require human input
            continue;
          }
        }

        if (step.kind === 'llm') {
          if (noLlm || llmCallsThisTick >= cfg.schedule.max_llm_runs_per_tick) {
            item.next_step = step.id;
            continue;
          }
          const res = llmExecutor
            ? llmExecutor({ cfg, step, item, probes })
            : invokeHeadlessAgy(cfg, step.prompt || `VRMine work ${item.work_id}: ${step.id}`, root);
          llmCallsThisTick += 1;
          workLedger.metrics.llm_calls += 1;
          stepResults.push({ work_id: item.work_id, step_id: step.id, ...res });
          if (!res.ok) {
            failed = true;
            item.last_error = res.error;
            item.next_step = step.id;
            break;
          }
          item.completed_steps.push(step.id);
          item.last_verified_artifact = res.artifact;
          stepsExecuted += 1;
          continue;
        }

        const res = stepExecutor({ step, item, stateDir, probes });
        stepResults.push({ work_id: item.work_id, step_id: step.id, ...res });
        if (!res.ok) {
          failed = true;
          item.last_error = res.error;
          item.next_step = step.id;
          break;
        }
        if (res.externalWrite) {
          const alreadyLogged = item.external_writes.some((w) => w.key === res.externalWrite.key);
          if (!alreadyLogged) {
            item.external_writes.push(res.externalWrite);
          } else if (!res.externalWrite.reused) {
            workLedger.metrics.duplicate_writes += 1;
          }
        }
        item.completed_steps.push(step.id);
        item.last_verified_artifact = res.artifact || item.last_verified_artifact;
        item.last_error = null;
        stepsExecuted += 1;
      }

      const remaining = item.steps.filter((s) => !item.completed_steps.includes(s.id));
      if (failed) {
        item.status = 'failed';
      } else if (remaining.length === 0) {
        item.status = 'verified';
        item.next_step = null;
      } else if (blockedByQuestion) {
        item.status = 'awaiting_human';
        item.next_step = remaining[0].id;
      } else {
        item.status = 'running';
        item.next_step = remaining[0].id;
      }
      item.updated_at = nowIso();
    }

    workLedger.last_observed_revision = probes.git_head;
    workLedger.last_tick_at = nowIso();

    writeJsonAtomic(workPath, workLedger);
    writeJsonAtomic(questionPath, questionLedger);

    const runEvidence = {
      schema_version: 1,
      run_id: runId,
      event_id: effectiveEventId,
      executed_at: nowIso(),
      probes,
      revision_delta: revisionDelta,
      steps_executed: stepsExecuted,
      llm_calls: llmCallsThisTick,
      open_questions: questionLedger.questions.filter((q) => q.status === 'open').length,
      step_results: stepResults,
    };
    const runPath = path.join(stateDir, cfg.state.runs_dir, `${runId}.json`);
    writeJsonAtomic(runPath, runEvidence);

    return {
      status: 'OK',
      run_id: runId,
      event_id: effectiveEventId,
      run_evidence: runPath,
      revision: probes.git_head,
      revision_delta: revisionDelta,
      steps_executed: stepsExecuted,
      llm_calls: llmCallsThisTick,
      open_questions: runEvidence.open_questions,
      work_items: workLedger.items.length,
    };
  } finally {
    lock.release();
  }
}

function invokeHeadlessAgy(cfg, prompt, root = repoRoot) {
  const args = [
    '-p', prompt,
    '--model', cfg.llm_cli.model,
    '--output-format', cfg.llm_cli.output_format,
    '--print-timeout', cfg.llm_cli.print_timeout,
  ];
  const res = runCmd(cfg.llm_cli.command, args, { cwd: root, timeoutMs: 360000 });
  if (res.status !== 0) {
    return { ok: false, error: `agy exited ${res.status}: ${res.stderr || res.stdout}` };
  }
  try {
    const parsed = JSON.parse(res.stdout);
    if (parsed.status !== 'SUCCESS') {
      return { ok: false, error: `agy status=${parsed.status}` };
    }
    return { ok: true, artifact: `agy:${parsed.conversation_id}`, response: parsed.response };
  } catch (err) {
    return { ok: false, error: `agy returned non-JSON output: ${err.message}` };
  }
}

function installWindowsSchedule(cfg, root = repoRoot) {
  const taskName = cfg.schedule.task_name;
  const interval = cfg.schedule.interval_minutes;
  const scriptPath = path.join(root, 'scripts', 'always-on-agent.mjs');
  const nodeExe = process.execPath;
  const taskCmd = `"${nodeExe}" "${scriptPath}" tick`;
  const create = runCmd('schtasks', [
    '/Create', '/F',
    '/TN', taskName,
    '/SC', 'MINUTE',
    '/MO', String(interval),
    '/TR', taskCmd,
  ], { cwd: root });
  if (create.status !== 0) {
    throw new Error(`schtasks /Create failed (${create.status}): ${create.stderr || create.stdout}`);
  }
  const verify = runCmd('schtasks', ['/Query', '/TN', taskName, '/FO', 'LIST'], { cwd: root });
  if (verify.status !== 0) {
    throw new Error(`schtasks /Query readback failed (${verify.status}): ${verify.stderr || verify.stdout}`);
  }
  return { status: 'INSTALLED', task_name: taskName, interval_minutes: interval, command: taskCmd, readback: verify.stdout };
}

function uninstallWindowsSchedule(cfg, root = repoRoot) {
  const taskName = cfg.schedule.task_name;
  const del = runCmd('schtasks', ['/Delete', '/F', '/TN', taskName], { cwd: root });
  if (del.status !== 0) {
    throw new Error(`schtasks /Delete failed (${del.status}): ${del.stderr || del.stdout}`);
  }
  return { status: 'UNINSTALLED', task_name: taskName };
}

function parseCliArgs(argv) {
  const cmd = argv[0] || 'status';
  const flags = {};
  for (let i = 1; i < argv.length; i += 1) {
    const a = argv[i];
    if (!a.startsWith('--')) continue;
    const key = a.slice(2);
    const next = argv[i + 1];
    if (!next || next.startsWith('--')) {
      flags[key] = true;
    } else {
      flags[key] = next;
      i += 1;
    }
  }
  return { cmd, flags };
}

const isCli = process.argv[1] && path.resolve(process.argv[1]) === modulePath;
if (isCli) {
  try {
    const cfg = loadConfig();
    const { cmd, flags } = parseCliArgs(process.argv.slice(2));
    const stateDir = resolveStateDir(cfg, repoRoot, flags['state-dir']);

    if (cmd === 'init') {
      const { workPath, questionPath, workLedger, questionLedger } = ensureLedgers(cfg, stateDir);
      console.log(JSON.stringify({
        status: 'INITIALIZED',
        state_dir: stateDir,
        work_ledger: workPath,
        question_ledger: questionPath,
        work_items: workLedger.items.length,
        open_questions: questionLedger.questions.filter((q) => q.status === 'open').length,
      }, null, 2));
    } else if (cmd === 'tick') {
      const result = runTick({
        cfg,
        stateDir,
        eventId: flags['event-id'] || null,
        forceWindow: Boolean(flags['force-window']),
        noLlm: Boolean(flags['no-llm']),
      });
      console.log(JSON.stringify(result, null, 2));
    } else if (cmd === 'enqueue') {
      if (!flags['work-id']) throw new Error('--work-id is required');
      const steps = String(flags.steps || 'probe')
        .split(',')
        .filter(Boolean)
        .map((id) => ({ id, kind: id === 'probe' ? 'probe_check' : 'noop' }));
      if (flags['blocked-step'] && flags['question-key']) {
        steps.push({
          id: flags['blocked-step'],
          kind: 'noop',
          requires_human: {
            subject_key: flags['question-key'],
            question: flags.question || `Human input required for ${flags['question-key']}`,
            resume_condition: flags.resume || 'Provide answer and rerun tick',
          },
        });
      }
      const item = enqueueWork(cfg, stateDir, {
        work_id: flags['work-id'],
        title: flags.title || flags['work-id'],
        steps,
      });
      console.log(JSON.stringify(item, null, 2));
    } else if (cmd === 'answer') {
      if (!flags['question-id'] || !flags.ref) throw new Error('--question-id and --ref are required');
      const q = answerQuestion(cfg, stateDir, {
        questionId: flags['question-id'],
        answerRef: flags.ref,
      });
      console.log(JSON.stringify(q, null, 2));
    } else if (cmd === 'status') {
      const { workPath, questionPath, workLedger, questionLedger } = ensureLedgers(cfg, stateDir);
      console.log(JSON.stringify({
        agent_id: cfg.agent_id,
        branch: cfg.branch,
        schedule: cfg.schedule,
        state_dir: stateDir,
        work_ledger: workPath,
        question_ledger: questionPath,
        last_observed_revision: workLedger.last_observed_revision,
        last_tick_at: workLedger.last_tick_at,
        metrics: workLedger.metrics,
        work_items: workLedger.items,
        open_questions: questionLedger.questions.filter((q) => q.status === 'open'),
      }, null, 2));
    } else if (cmd === 'install-schedule') {
      console.log(JSON.stringify(installWindowsSchedule(cfg), null, 2));
    } else if (cmd === 'uninstall-schedule') {
      console.log(JSON.stringify(uninstallWindowsSchedule(cfg), null, 2));
    } else {
      throw new Error(`Unknown command: ${cmd}`);
    }
  } catch (err) {
    console.error(`Always-on agent FAIL: ${err.message}`);
    process.exitCode = 1;
  }
}
