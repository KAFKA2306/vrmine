import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, cpSync, mkdirSync, rmSync, writeFileSync, readFileSync, symlinkSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { Script } from 'node:vm';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { canonicalProducts, validateEvidence, buildProductPages } from './build-product-pages.mjs';

const products = canonicalProducts();
test('actual catalog, sitemap and navigation agree after the Pages build',()=>{
  const site=mkdtempSync(join(tmpdir(),'vrmine-site-'));
  mkdirSync(join(site,'io'));
  try {
    for(const path of ['index.html','io/index.html','io/view.html'])cpSync(join('pages',path),join(site,path));
    symlinkSync(resolve('pages/io/items'),join(site,'io/items'),'junction');
    buildProductPages(site);
    execFileSync(process.execPath,['scripts/verify-product-pages.mjs',site],{stdio:'pipe'});
    for(const file of ['pages/io/index.html','pages/io/view.html']) {
      for(const script of readFileSync(file,'utf8').matchAll(/<script>([\s\S]*?)<\/script>/g))new Script(script[1],{filename:file});
    }
  } finally {rmSync(site,{recursive:true,force:true});}
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
