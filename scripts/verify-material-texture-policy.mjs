import fs from 'node:fs';

const policyPath = new URL('../config/material-texture-policy.json', import.meta.url);
export const policy = JSON.parse(fs.readFileSync(policyPath, 'utf8'));

export function estimateTextureBytes({ width, height, compression }) {
  if (!Number.isInteger(width) || !Number.isInteger(height) || width <= 0 || height <= 0) throw new Error('texture dimensions must be positive integers');
  const bpp = { BC5: 8, ASTC_6x6: 128 / 36, RGBA32: 32 }[compression];
  if (!bpp) throw new Error(`unsupported compression: ${compression}`);
  return Math.ceil(width * height * bpp / 8 * 4 / 3);
}

export function validateTextureSet(manifest, platform) {
  const target = policy.platforms[platform];
  if (!target) throw new Error(`unknown platform: ${platform}`);
  const errors = [];
  let bytes = 0;
  for (const texture of manifest.textures ?? []) {
    const expected = policy.color_space[texture.role];
    if (!expected) errors.push(`${texture.name}: unknown role ${texture.role}`);
    else if (texture.color_space !== expected) errors.push(`${texture.name}: ${texture.role} must be ${expected}`);
    if (texture.width > target.max_texture_dimension || texture.height > target.max_texture_dimension) errors.push(`${texture.name}: exceeds ${platform} max dimension ${target.max_texture_dimension}`);
    const compression = texture.role === 'normal' ? target.normal_compression : (texture.compression ?? 'RGBA32');
    bytes += estimateTextureBytes({ width: texture.width, height: texture.height, compression });
  }
  if ((manifest.atlas_padding_px ?? 0) < policy.atlas.minimum_padding_px) errors.push(`atlas padding must be >= ${policy.atlas.minimum_padding_px}px`);
  if ((manifest.atlas_dilation_px ?? 0) < policy.atlas.minimum_dilation_px) errors.push(`atlas dilation must be >= ${policy.atlas.minimum_dilation_px}px`);
  const fallback = policy.shader_fallback[manifest.shader]?.[platform];
  if (!fallback) errors.push(`shader ${manifest.shader} has no ${platform} fallback`);
  const budgetBytes = target.memory_budget_mib * 1024 * 1024;
  if (bytes > budgetBytes) errors.push(`texture memory ${bytes} exceeds ${platform} budget ${budgetBytes}`);
  return { platform, bytes, budget_bytes: budgetBytes, shader: fallback ?? null, errors, status: errors.length ? 'FAIL' : 'PASS' };
}

export function assertPolicyShape() {
  for (const role of ['base_color', 'normal', 'roughness', 'metallic', 'occlusion']) if (!policy.color_space[role]) throw new Error(`missing color-space role: ${role}`);
  for (const platform of ['pc', 'android']) {
    const p = policy.platforms[platform];
    if (!p?.normal_compression || !p.max_texture_dimension || !p.memory_budget_mib) throw new Error(`incomplete platform policy: ${platform}`);
  }
  if (policy.atlas.minimum_padding_px <= 0 || policy.atlas.minimum_dilation_px <= 0) throw new Error('atlas padding/dilation must be positive');
}

if (process.argv[1] && import.meta.url === new URL(`file://${process.argv[1]}`).href) {
  assertPolicyShape();
  console.log('material-texture-policy: PASS');
}
