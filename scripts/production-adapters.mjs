import path from 'node:path';

const PILOT_CONCEPT = 'cyber-alley-tea-nook';
const PILOT_ROOT = '.artifacts/world-builds/cyber-alley-tea-nook';
const PILOT_PLAN = `${PILOT_ROOT}/build-plan.json`;
const PILOT_GLB = `${PILOT_ROOT}/world.glb`;
const PILOT_RENDERS = [
  'view-hero.png',
  'view-top.png',
  'view-social-core.png',
  'view-retreat.png',
  'view-circulation.png'
];

function conceptOf(state) {
  return state?.request?.concept ?? null;
}

function rawArtifact(state) {
  return state?.artifacts?.raw?.find((artifact) => /\.glb$/i.test(artifact)) ?? null;
}

export function canonicalProductionAdapters(state) {
  if (state?.request?.kind !== 'world_prop' || conceptOf(state) !== PILOT_CONCEPT) return {};

  return {
    'GENERATE': {
      command: 'blender',
      args: ['-b', '--python-exit-code', '1', '--python', 'scripts/world_build_factory.py', '--', PILOT_PLAN, PILOT_ROOT],
      artifacts: {raw: [PILOT_GLB]}
    },
    'VALIDATE_STATIC': ({state: current}) => {
      const glb = rawArtifact(current);
      if (!glb) return null;
      const normalized = path.normalize(glb).replaceAll('\\', '/');
      if (!normalized.startsWith(`${PILOT_ROOT}/`)) return null;
      return {
        command: 'blender',
        args: ['-b', '--python-exit-code', '1', '--python', 'scripts/verify_world_build.py', '--', PILOT_PLAN, PILOT_ROOT]
      };
    },
    'RENDER': {
      command: process.execPath,
      args: ['scripts/verify-production-renders.mjs', PILOT_ROOT, ...PILOT_RENDERS]
    }
  };
}

export const canonicalPilot = Object.freeze({
  concept: PILOT_CONCEPT,
  root: PILOT_ROOT,
  plan: PILOT_PLAN,
  glb: PILOT_GLB,
  renders: PILOT_RENDERS
});
