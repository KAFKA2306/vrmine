import path from 'node:path';

const PILOT_CONCEPT = 'cyber-alley-tea-nook';
const PILOT_ROOT = '.artifacts/world-builds/cyber-alley-tea-nook';
const PILOT_PLAN = `${PILOT_ROOT}/build-plan.json`;
const PILOT_GLB = `${PILOT_ROOT}/world.glb`;
const PILOT_UNITY_EVIDENCE = `${PILOT_ROOT}/unity-evidence/glb-consumer-evidence.json`;
const PILOT_RENDERS = [
  'view-hero.png',
  'view-top.png',
  'view-social-core.png',
  'view-retreat.png',
  'view-circulation.png'
];
const PILOT_B_CONCEPT = 'astra-pilot-b';
const PILOT_B_ROOT = '.artifacts/astra-pilot-b';
const PILOT_B_GLB = `${PILOT_B_ROOT}/pilot-b.glb`;

function conceptOf(state) {
  return state?.request?.concept ?? null;
}

function rawArtifact(state) {
  return state?.artifacts?.raw?.find((artifact) => /\.glb$/i.test(artifact)) ?? null;
}

export function canonicalProductionAdapters(state) {
  if (state?.request?.kind === 'rigged_asset' && conceptOf(state) === PILOT_B_CONCEPT) {
    return {
      'GENERATE': {
        command: 'blender',
        args: ['-b', '--python-exit-code', '1', '--python', 'scripts/build_astra_rigged_pilot.py', '--', PILOT_B_ROOT],
        artifacts: {raw: [PILOT_B_GLB]}
      }
    };
  }

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
    },
    'UNITY_IMPORT': ({state: current}) => {
      const glb = rawArtifact(current);
      if (glb !== PILOT_GLB) return null;
      return {
        command: process.execPath,
        args: ['scripts/run-world-build-unity.mjs', PILOT_ROOT],
        artifacts: {unity: [PILOT_UNITY_EVIDENCE]}
      };
    }
  };
}

export const canonicalPilot = Object.freeze({
  concept: PILOT_CONCEPT,
  root: PILOT_ROOT,
  plan: PILOT_PLAN,
  glb: PILOT_GLB,
  unityEvidence: PILOT_UNITY_EVIDENCE,
  renders: PILOT_RENDERS
});

export const canonicalPilotB = Object.freeze({
  concept: PILOT_B_CONCEPT,
  root: PILOT_B_ROOT,
  glb: PILOT_B_GLB
});
