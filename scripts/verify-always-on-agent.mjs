import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {
  acquireLock,
  answerQuestion,
  enqueueWork,
  ensureLedgers,
  loadConfig,
  runTick,
} from './always-on-agent.mjs';

const cfg = loadConfig();

function makeTempState(label) {
  return fs.mkdtempSync(path.join(os.tmpdir(), `vrmine-agent-${label}-`));
}

function cleanup(dir) {
  fs.rmSync(dir, { recursive: true, force: true });
}

const results = [];

// Case 1 (benchmark 1): Disconnect right after external write + resend same event -> 1 write, 0 duplicates
{
  const dir = makeTempState('resend');
  try {
    enqueueWork(cfg, dir, {
      work_id: 'w-resend',
      title: 'Idempotent external write',
      steps: [{ id: 'publish', kind: 'external_write', simulate_disconnect_after_write: true }],
    });
    const first = runTick({ cfg, stateDir: dir, eventId: 'ev-1', forceWindow: true, noLlm: true, skipHostProbes: true });
    assert.equal(first.status, 'OK');
    const afterFirst = ensureLedgers(cfg, dir).workLedger.items[0];
    assert.equal(afterFirst.status, 'failed');

    // Resend same event after transient disconnect; external write already landed so it reads back & resumes
    const second = runTick({ cfg, stateDir: dir, eventId: 'ev-1', forceWindow: true, noLlm: true, skipHostProbes: true });
    assert.equal(second.status, 'OK');
    const ledger = ensureLedgers(cfg, dir).workLedger;
    const item = ledger.items[0];
    assert.equal(item.status, 'verified');
    assert.equal(item.external_writes.length, 1);
    assert.equal(ledger.metrics.duplicate_writes, 0);
    const sink = JSON.parse(fs.readFileSync(path.join(dir, 'external-writes-sink.json'), 'utf8'));
    assert.equal(sink.records.length, 1);
    results.push('case1_resend_idempotency:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 2 (benchmark 2): Concurrent execution lock -> <= 1 active writer
{
  const dir = makeTempState('lock');
  try {
    const lockPath = path.join(dir, cfg.state.lock_file);
    const firstLock = acquireLock(lockPath, { waitMs: 100, metadata: { holder: 'writer-1' } });
    assert.equal(firstLock.acquired, true);

    const deferred = runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true, lockWaitMs: 150 });
    assert.equal(deferred.status, 'DEFERRED_LOCKED');

    firstLock.release();
    const afterRelease = runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true, lockWaitMs: 150 });
    assert.equal(afterRelease.status, 'OK');
    results.push('case2_single_writer_lock:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 3 (benchmark 3): Stop mid-way and resume from a separate session at next_step
{
  const dir = makeTempState('resume');
  try {
    enqueueWork(cfg, dir, {
      work_id: 'w-resume',
      title: 'Two-step resumable work',
      steps: [
        { id: 'step-1', kind: 'noop' },
        { id: 'step-2', kind: 'noop' },
      ],
    });
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true, maxStepsThisTick: 1 });
    const mid = ensureLedgers(cfg, dir).workLedger.items[0];
    assert.equal(mid.status, 'running');
    assert.deepEqual(mid.completed_steps, ['step-1']);
    assert.equal(mid.next_step, 'step-2');

    // Fresh tick resumes from step-2 without re-running step-1
    const res2 = runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    assert.equal(res2.steps_executed, 1);
    const done = ensureLedgers(cfg, dir).workLedger.items[0];
    assert.equal(done.status, 'verified');
    assert.deepEqual(done.completed_steps, ['step-1', 'step-2']);
    assert.equal(done.next_step, null);
    results.push('case3_session_resume:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 4 (benchmark 4): Failure / permission denial never marks verified and preserves error
{
  const dir = makeTempState('fail');
  try {
    enqueueWork(cfg, dir, {
      work_id: 'w-fail',
      title: 'Permission denied step',
      steps: [{ id: 'upload', kind: 'fail', permission_denied: true, error: 'human arm required' }],
    });
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    const item = ensureLedgers(cfg, dir).workLedger.items[0];
    assert.equal(item.status, 'failed');
    assert.match(item.last_error, /human arm required/);
    results.push('case4_failure_tracking:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 5 (schema question ledger 1): No-human-answer work opens ledger with 0 questions and completes
{
  const dir = makeTempState('q-zero');
  try {
    const initial = ensureLedgers(cfg, dir);
    assert.equal(initial.questionLedger.questions.length, 0);
    enqueueWork(cfg, dir, {
      work_id: 'w-auto',
      title: 'Autonomous check',
      steps: [{ id: 'probe', kind: 'probe_check' }],
    });
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    const after = ensureLedgers(cfg, dir);
    assert.equal(after.questionLedger.questions.length, 0);
    assert.equal(after.workLedger.items[0].status, 'verified');
    results.push('case5_zero_question_completion:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 6 (schema question ledger 2): Human-required step dedupes to 1 question, completes independent step, resumes on answer
{
  const dir = makeTempState('q-human');
  try {
    enqueueWork(cfg, dir, {
      work_id: 'w-mixed',
      title: 'Mixed human and autonomous work',
      steps: [
        {
          id: 'unity-license-step',
          kind: 'noop',
          requires_human: {
            subject_key: 'unity_license',
            question: 'Activate Unity Hub license for 2022.3.22f1',
            resume_condition: 'Unity.Licensing.Client.exe reports active entitlement',
          },
        },
        { id: 'independent-step', kind: 'noop' },
      ],
    });
    // Tick 1: completes independent-step, records exactly 1 open question
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    let state = ensureLedgers(cfg, dir);
    assert.equal(state.questionLedger.questions.length, 1);
    assert.equal(state.questionLedger.questions[0].status, 'open');
    assert.deepEqual(state.workLedger.items[0].completed_steps, ['independent-step']);
    assert.equal(state.workLedger.items[0].status, 'awaiting_human');

    // Tick 2 (before answer): does NOT duplicate the question
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    state = ensureLedgers(cfg, dir);
    assert.equal(state.questionLedger.questions.length, 1);

    // Record human answer and resume
    const qid = state.questionLedger.questions[0].question_id;
    answerQuestion(cfg, dir, { questionId: qid, answerRef: 'human:unity-hub-activated' });
    runTick({ cfg, stateDir: dir, forceWindow: true, noLlm: true, skipHostProbes: true });
    state = ensureLedgers(cfg, dir);
    assert.equal(state.workLedger.items[0].status, 'verified');
    assert.deepEqual(state.workLedger.items[0].completed_steps, ['independent-step', 'unity-license-step']);
    results.push('case6_human_question_dedup_and_resume:PASS');
  } finally {
    cleanup(dir);
  }
}

// Case 7 (prune): No delta / no pending LLM work -> 0 LLM calls
{
  const dir = makeTempState('zero-llm');
  try {
    const t1 = runTick({ cfg, stateDir: dir, forceWindow: true, skipHostProbes: true });
    const t2 = runTick({ cfg, stateDir: dir, forceWindow: true, skipHostProbes: true });
    assert.equal(t1.llm_calls, 0);
    assert.equal(t2.llm_calls, 0);
    results.push('case7_zero_delta_zero_llm:PASS');
  } finally {
    cleanup(dir);
  }
}

console.log(JSON.stringify({ status: 'PASS', cases: results.length, results }, null, 2));
