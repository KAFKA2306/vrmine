import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { canonicalProducts, origin } from './build-product-pages.mjs';

const target = process.argv[2] || '_site';
const remote = /^https?:\/\//.test(target);
const products = canonicalProducts();
async function read(path, binary = false) {
  if (!remote) return readFileSync(join(target, path), binary ? undefined : 'utf8');
  const response = await fetch(new URL(path, target.endsWith('/') ? target : target + '/'), {cache:'no-store', signal:AbortSignal.timeout(30000)});
  assert.equal(response.status, 200, `${path}: HTTP ${response.status}`);
  return binary ? Buffer.from(await response.arrayBuffer()) : response.text();
}
function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
const [home, gallery, view, catalogText, sitemap] = await Promise.all(['index.html','io/index.html','io/view.html','io/catalog.json','sitemap.xml'].map(p => read(p)));
const catalog = JSON.parse(catalogText);
const expected = products.map(s => s.id).sort();
assert.deepEqual(catalog.map(s=>s.id).sort(), expected, 'canonical SKU != catalog SKU');
const urls = [...sitemap.matchAll(/<loc>(.*?)<\/loc>/g)].map(m=>m[1].replaceAll('&amp;','&'));
assert.equal(urls.length, new Set(urls).size, 'Duplicate sitemap URL');
assert(urls.includes(origin+'io/'), 'Gallery missing from sitemap');
const productUrls = urls.filter(u=>u.startsWith(origin+'io/view.html'));
assert.deepEqual(productUrls.map(u=>new URL(u).searchParams.get('item')).sort(), expected, 'canonical SKU != sitemap SKU');
assert(!urls.some(u=>u.includes('/io/items/')), 'Non-HTML item directory in sitemap');
assert.match(home, /href="\.\/io\/"/);
assert(!/href="\.\/3d\/retro-cafe\/pendant-light\/"/.test(home), 'Legacy product is still a primary Home entry');
assert.match(home, /src="\.\/io\/items\/[^/]+\/view-hero.png"/);
for(const html of [gallery,view]) {
  assert.match(html, /href="\.\.\/">Home<\/a>/);
  assert.match(html, /href="\.\.\/#games">ゲーム一覧<\/a>/);
  assert.match(html, /href="\.\/" aria-current="page">3D素材<\/a>/);
  assert.match(html, /href="\.\.\/organizers\/">イベント主催者向け<\/a>/);
}
assert.match(view, /aria-label="パンくず"/);
assert.match(view, /id="product-name"/);
assert.match(gallery, /id="filters"/);
assert.match(gallery, /id="count" role="status"/);
// Limit concurrency while reading every actual published product, not a sample.
for(let i=0;i<products.length;i+=6) {
  await Promise.all(products.slice(i,i+6).map(async spec=>{
    const base=`io/items/${spec.id}/`;
    const [publishedText, hero, html] = await Promise.all([read(base+'spec.json'),read(base+'view-hero.png',true),remote ? read('io/view.html?item='+spec.id) : Promise.resolve(view)]);
    assert.deepEqual(JSON.parse(publishedText),spec,`${spec.id}: published spec differs`);
    assert.equal(hero.subarray(0,8).toString('hex'),'89504e470d0a1a0a',`${spec.id}: invalid hero PNG`);
    assert.match(html,/aria-label="パンくず"/,`${spec.id}: wrong product template`);
  }));
}

// World Build evidence is repository-owned production content. Use the tracked
// manifests as the only list/hash authority and read the deployed bytes back.
const worldRoot = 'pages/worlds';
const worldIds = existsSync(worldRoot)
  ? readdirSync(worldRoot, {withFileTypes:true})
      .filter(entry => entry.isDirectory() && existsSync(join(worldRoot, entry.name, 'manifest.json')))
      .map(entry => entry.name)
      .sort()
  : [];
for (const worldId of worldIds) {
  const localRoot = join(worldRoot, worldId);
  const manifest = JSON.parse(readFileSync(join(localRoot, 'manifest.json'), 'utf8'));
  const base = `worlds/${worldId}/`;
  const [publishedManifestText, publishedPlanText] = await Promise.all([
    read(base + 'manifest.json'),
    read(base + 'build-plan.json'),
  ]);
  assert.deepEqual(JSON.parse(publishedManifestText), manifest, `${worldId}: production manifest differs`);
  assert.deepEqual(JSON.parse(publishedPlanText), JSON.parse(readFileSync(join(localRoot, 'build-plan.json'), 'utf8')), `${worldId}: production build plan differs`);
  const glb = await read(base + manifest.outputs.glb.path, true);
  assert.equal(sha256(glb), manifest.outputs.glb.sha256, `${worldId}: production GLB hash differs`);
  for (const render of manifest.outputs.renders) {
    const image = await read(base + render.path, true);
    assert.equal(image.subarray(0,8).toString('hex'), '89504e470d0a1a0a', `${worldId}/${render.path}: invalid PNG`);
    assert.equal(sha256(image), render.sha256, `${worldId}/${render.path}: production render hash differs`);
  }
}
console.log(`PASS ${target}: Home → Gallery → Product navigation; ${products.length} canonical/catalog/sitemap IDs; all product pages, specs and hero PNGs; ${worldIds.length} World Build manifest/plan/GLB/render sets`);
