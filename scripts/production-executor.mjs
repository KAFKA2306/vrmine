import { spawnSync } from 'node:child_process';
import { applyStageResult, nextRunnableStage, saveRun } from './production-run-state.mjs';

const stageCommands = {
  SPEC: () => ({command: process.execPath, args: ['--version']}),
  GENERATE: (stage) => stage.provider === 'blender_bpy'
    ? {command: 'task', args: ['world-build:pilot']}
    : stage.provider === 'astra_blender_rigged'
      ? {command: 'task', args: ['astra-rigged:build']}
      : null,
  VALIDATE_STATIC: (stage, state) => state.plan.capability === 'procedural_mesh'
    ? {command: 'task', args: ['world-build:pilot']}
    : state.plan.capability === 'rigged_mesh'
      ? {command: 'task', args: ['astra-rigged:build']}
      : null,
  UNITY_IMPORT: (_stage, state) => state.plan.capability === 'procedural_mesh'
    ? {command: 'task', args: ['world-build:verify-u2']}
    : state.plan.capability === 'rigged_mesh'
      ? {command: 'task', args: ['astra-rigged:verify-u2']}
      : null,
  PLATFORM_VERIFY: (_stage, state) => state.plan.capability === 'procedural_mesh'
    ? {command: 'task', args: ['world-build:verify-u2']}
    : state.plan.capability === 'rigged_mesh'
      ? {command: 'task', args: ['astra-rigged:verify-u2']}
      : null
};

export function commandForStage(state, stage = nextRunnableStage(state)) {
  if (!stage) return null;
  return stageCommands[stage.stage]?.(stage, state) ?? null;
}

export function executeNextStage(state, options = {}) {
  const stage = nextRunnableStage(state);
  if (!stage) return {state, stage: null, executed: false};
  const command = commandForStage(state, stage);
  if (!command) {
    applyStageResult(state, stage.stage, {
      status: 'BLOCKED',
      evidence: [`executor:no-command:${stage.stage}:${stage.provider ?? 'none'}`]
    });
    return {state, stage: stage.stage, executed: false};
  }

  const runner = options.runner ?? ((spec) => spawnSync(spec.command, spec.args, {encoding: 'utf8'}));
  const result = runner(command);
  const status = result.status === 0 ? 'PASS' : 'BLOCKED';
  const evidence = [`executor:${command.command} ${command.args.join(' ')}:exit=${result.status ?? 'null'}`];
  applyStageResult(state, stage.stage, {status, evidence});
  if (options.runRoot) saveRun(options.runRoot, state);
  return {state, stage: stage.stage, executed: true, command, exit_code: result.status};
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
