import assert from 'node:assert/strict';
import test from 'node:test';
import { assertPolicyShape, estimateTextureBytes, validateTextureSet } from './verify-material-texture-policy.mjs';

test('canonical policy is complete', () => assert.doesNotThrow(assertPolicyShape));

test('normal maps use platform compression and valid color spaces pass', () => {
  const manifest = {
    shader: 'lilToon', atlas_padding_px: 8, atlas_dilation_px: 8,
    textures: [
      { name: 'albedo', role: 'base_color', color_space: 'sRGB', width: 1024, height: 1024 },
      { name: 'normal', role: 'normal', color_space: 'Linear', width: 1024, height: 1024 }
    ]
  };
  assert.equal(validateTextureSet(manifest, 'pc').status, 'PASS');
  assert.equal(validateTextureSet(manifest, 'android').status, 'PASS');
});

test('wrong data-map color space and missing shader fallback fail closed', () => {
  const result = validateTextureSet({
    shader: 'Unknown/Shader', atlas_padding_px: 2, atlas_dilation_px: 2,
    textures: [{ name: 'rough', role: 'roughness', color_space: 'sRGB', width: 512, height: 512 }]
  }, 'android');
  assert.equal(result.status, 'FAIL');
  assert.ok(result.errors.some((x) => x.includes('roughness must be Linear')));
  assert.ok(result.errors.some((x) => x.includes('no android fallback')));
  assert.ok(result.errors.some((x) => x.includes('atlas padding')));
});

test('dimension and aggregate memory budgets are machine checked', () => {
  const textures = Array.from({ length: 20 }, (_, i) => ({ name: `t${i}`, role: 'base_color', color_space: 'sRGB', width: 4096, height: 4096 }));
  const result = validateTextureSet({ shader: 'lilToon', atlas_padding_px: 8, atlas_dilation_px: 8, textures }, 'android');
  assert.equal(result.status, 'FAIL');
  assert.ok(result.errors.some((x) => x.includes('max dimension')));
  assert.ok(result.errors.some((x) => x.includes('texture memory')));
  assert.equal(estimateTextureBytes({ width: 36, height: 36, compression: 'ASTC_6x6' }), 768);
});
