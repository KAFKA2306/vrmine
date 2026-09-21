import path from 'node:path';

const PILOT_CONCEPT = 'cyber-alley-tea-nook';
const PILOT_ROOT = '.artifacts/world-builds/cyber-alley-tea-nook';
const PILOT_PLAN = `${PILOT_ROOT}/build-plan.json`;

function conceptOf(state) {
  return state?.request?.concept ?? null;
}

function rawArtifact(state) {
  return state?.artifacts?.raw?.find((artifact) => /\.glb$/i.test(artifact)) ?? null;
}

export function canonicalProductionAdapters(state) {
  if (state?.request?.kind !== 'world_prop' || conceptOf(state) !== PILOT_CONCEPT) return {};

  return {
    'VALIDATE_STATIC': ({state: current}) => {
      const glb = rawArtifact(current);
      if (!glb) return null;
      const normalized = path.normalize(glb).replaceAll('\\', '/');
      if (!normalized.startsWith(`${PILOT_ROOT}/`)) return null;
      return {
        command: 'blender',
        args: ['-b', '--python-exit-code', '1', '--python', 'scripts/verify_world_build.py', '--', PILOT_PLAN, PILOT_ROOT]
      };
    }
  };
}

export const canonicalPilot = Object.freeze({concept: PILOT_CONCEPT, root: PILOT_ROOT, plan: PILOT_PLAN});
