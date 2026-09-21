import { spawnSync } from 'node:child_process';
import { applyStageResult, nextRunnableStage, saveRun } from './production-run-state.mjs';

const VERDICTS = new Set(['PASS', 'REPAIRABLE', 'BLOCKED']);

export function commandForStage(state, stage = nextRunnableStage(state), adapters = {}) {
  if (!stage) return null;
  if (stage.stage === 'SPEC') return {command: process.execPath, args: ['--version']};
  const key = stage.provider ? `${stage.stage}:${stage.provider}` : stage.stage;
  const adapter = adapters[key] ?? adapters[stage.stage];
  return typeof adapter === 'function' ? adapter({state, stage}) : adapter ?? null;
}

export function classifyStageResult(result) {
  if (result?.verdict !== undefined) {
    if (!VERDICTS.has(result.verdict)) throw new Error(`invalid verifier verdict: ${result.verdict}`);
    if (result.status !== 0 && result.verdict === 'PASS') {
      throw new Error('verifier cannot report PASS for a nonzero process exit');
    }
    return result.verdict;
  }
  return result?.status === 0 ? 'PASS' : 'BLOCKED';
}

export function executeNextStage(state, options = {}) {
  const stage = nextRunnableStage(state);
  if (!stage) return {state, stage: null, executed: false};
  const command = commandForStage(state, stage, options.adapters);
  if (!command) {
    applyStageResult(state, stage.stage, {
      status: 'BLOCKED',
      evidence: [`executor:no-adapter:${stage.stage}:${stage.provider ?? 'none'}`]
    });
    if (options.runRoot) saveRun(options.runRoot, state);
    return {state, stage: stage.stage, executed: false};
  }

  const runner = options.runner ?? ((spec) => spawnSync(spec.command, spec.args, {encoding: 'utf8'}));
  const result = runner(command);
  const status = classifyStageResult(result);
  const evidence = [
    `executor:${command.command} ${command.args.join(' ')}:exit=${result.status ?? 'null'}`,
    ...(result.evidence ?? [])
  ];
  applyStageResult(state, stage.stage, {status, evidence, artifacts: result.artifacts});
  if (options.runRoot) saveRun(options.runRoot, state);
  return {state, stage: stage.stage, executed: true, command, exit_code: result.status, verdict: status};
}

export function executeUntilBlocked(state, options = {}) {
  const executed = [];
  while (true) {
    const stage = nextRunnableStage(state);
    if (!stage) return {state, executed};
    const result = executeNextStage(state, options);
    executed.push({stage: result.stage, executed: result.executed, exit_code: result.exit_code ?? null});
    if (state.evidence === 'BLOCKED' || state.evidence === 'REPAIRABLE') return {state, executed};
  }
}
