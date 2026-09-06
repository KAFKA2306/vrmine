import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, cpSync, mkdirSync, rmSync, writeFileSync, readFileSync, symlinkSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { Script } from 'node:vm';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { canonicalProducts, validateEvidence, distributionLedger, buildProductPages } from './build-product-pages.mjs';

const products = canonicalProducts();
test('actual catalog, distribution ledger, sitemap and navigation agree after the Pages build',()=>{
  const site=mkdtempSync(join(tmpdir(),'vrmine-site-'));
  mkdirSync(join(site,'io'));
  try {
    for(const path of ['index.html','io/index.html','io/view.html'])cpSync(join('pages',path),join(site,path));
    symlinkSync(resolve('pages/io/items'),join(site,'io/items'),'junction');
    buildProductPages(site);
    execFileSync(process.execPath,['scripts/verify-product-pages.mjs',site],{stdio:'pipe'});
    const ledger=JSON.parse(readFileSync(join(site,'io/distributions.json'),'utf8'));
    assert.equal(ledger.length,products.length);
    assert.deepEqual(ledger.map(x=>x.id),products.map(x=>x.id));
    for(const entry of ledger){
      assert.match(entry.status,/^(READY|BLOCKED_LICENSE)$/);
      assert.ok(entry.spec_sha256);
      assert.ok(entry.models.length>0);
      assert.equal(entry.renders.length,6);
      for(const file of [...entry.models,...entry.renders])assert.match(file.sha256,/^[0-9a-f]{64}$/);
    }
    for(const file of ['pages/io/index.html','pages/io/view.html']) {
      for(const script of readFileSync(file,'utf8').matchAll(/<script>([\s\S]*?)<\/script>/g))new Script(script[1],{filename:file});
    }
  } finally {rmSync(site,{recursive:true,force:true});}
});
test('product view gates model downloads on verified or published license status',()=>{
  const view=readFileSync('pages/io/view.html','utf8');
  assert.match(view,/const licenseReady=\/\^\(VERIFIED\|PUBLISHED\)\$\/i\.test\(licenseStatus\)/);
  assert.match(view,/const formatButtons=licenseReady/);
  assert.match(view,/ライセンスが確定するまでモデルファイルの取得リンクは公開しません/);
  assert.match(view,/\['License',licenseStatus\]/);
});
test('canonical package route is fail-closed and bound to manifest, spec and declared formats',()=>{
  const view=readFileSync('pages/io/view.html','utf8');
  const source=readFileSync('pages/io/package.js','utf8');
  assert.match(view,/<script src="\.\/package\.js"><\/script>/);
  assert.match(source,/\^\(VERIFIED\|PUBLISHED\)\$/);
  assert.match(source,/manifest\.spec_sha256/);
  assert.match(source,/spec\.json: SHA-256 mismatch/);
  assert.match(source,/spec\/manifest format mismatch/);
  assert.match(source,/items\/\$\{id\}\//);
  assert.match(source,/manifest\/spec SKU mismatch/);
});
test('distribution ledger binds current model and render files to the generation manifest',()=>{
  const ledger=distributionLedger(products,'pages');
  for(const entry of ledger){
    const spec=products.find(x=>x.id===entry.id);
    const expectedReady=/^(VERIFIED|PUBLISHED)$/i.test(spec.license?.status||'');
    assert.equal(entry.status,expectedReady?'READY':'BLOCKED_LICENSE');
    assert.equal(entry.models.length,spec.formats.length);
    assert.equal(entry.renders.length,6);
  }
});
test('all canonical products have matching real publication evidence',()=>validateEvidence(products,'pages'));
for (const failure of ['missing hero','corrupt side image','stale spec','missing model']) {
  test(`publication rejects ${failure}`,()=>{
    const site=mkdtempSync(join(tmpdir(),'vrmine-product-'));
    const spec=products[0];
    const dest=join(site,'io/items',spec.id);
    mkdirSync(dest,{recursive:true});
    cpSync(join('pages/io/items',spec.id),dest,{recursive:true});
    try {
      if(failure==='missing hero')rmSync(join(dest,'view-hero.png'));
      if(failure==='corrupt side image')writeFileSync(join(dest,'view-left.png'),'not a PNG');
      if(failure==='stale spec')writeFileSync(join(dest,'spec.json'),JSON.stringify({...spec,display_name:'stale'}));
      if(failure==='missing model')rmSync(join(dest,`${spec.id}.${spec.formats[0]}`));
      assert.throws(()=>validateEvidence([spec],site));
    } finally {rmSync(site,{recursive:true,force:true});}
  });
}
