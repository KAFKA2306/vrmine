import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
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
console.log(`PASS ${target}: Home → Gallery → Product navigation; ${products.length} canonical/catalog/sitemap IDs; all product pages, specs and hero PNGs`);
