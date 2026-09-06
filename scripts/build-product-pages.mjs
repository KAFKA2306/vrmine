import { readdirSync, readFileSync, writeFileSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { pathToFileURL } from 'node:url';

export const origin = 'https://kafka2306.github.io/vrmine/';
export const views = ['hero', 'front', 'rear', 'left', 'right', 'top'];
export const escapeHtml = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[c]));
export function canonicalProducts(root = '.') {
  const products = readdirSync(join(root, 'config/world-items')).filter(f => f.endsWith('.json')).sort().map(file => {
    const spec = JSON.parse(readFileSync(join(root, 'config/world-items', file), 'utf8'));
    if (!/^[a-z0-9-]+$/.test(spec.id) || file !== `${spec.id}.json` || !spec.display_name || !spec.family)
      throw new Error(`Invalid canonical product: ${file}`);
    return spec;
  });
  if (!products.length) throw new Error('No canonical products');
  return products;
}
export function validateEvidence(products, site) {
  for (const spec of products) {
    const dir = join(site, 'io/items', spec.id);
    const published = JSON.parse(readFileSync(join(dir, 'spec.json'), 'utf8'));
    if (!isDeepStrictEqual(spec, published)) throw new Error(`Published spec differs from canonical: ${spec.id}`);
    for (const view of views) {
      const path = join(dir, `view-${view}.png`);
      const bytes = readFileSync(path);
      if (bytes.length < 24 || bytes.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a')
        throw new Error(`Missing or invalid render: ${path}`);
    }
    for (const format of spec.formats) {
      if (!['glb', 'fbx', 'blend'].includes(format)) throw new Error(`Unsupported format: ${spec.id}/${format}`);
      const path = join(dir, `${spec.id}.${format}`);
      if (!statSync(path).isFile() || !statSync(path).size) throw new Error(`Empty model: ${path}`);
    }
  }
}
export function buildProductPages(site = '_site', root = '.') {
  const products = canonicalProducts(root);
  validateEvidence(products, site);
  const fields = ['id', 'display_name', 'family', 'description', 'dimensions_m', 'formats', 'license', 'unity_status', 'vrchat_status', 'booth_status', 'price_hypothesis'];
  writeFileSync(join(site, 'io/catalog.json'), JSON.stringify(products.map(s => Object.fromEntries(fields.filter(k => k in s).map(k => [k, s[k]]))), null, 2) + '\n');
  const fixed = [...readFileSync(join(root, 'pages/sitemap.xml'), 'utf8').matchAll(/<loc>(.*?)<\/loc>/g)].map(m => m[1]).filter(url => !url.startsWith(origin + 'io/'));
  const urls = [...new Set([...fixed, origin + 'io/', ...products.map(s => origin + 'io/view.html?item=' + s.id)])];
  writeFileSync(join(site, 'sitemap.xml'), '<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' + urls.map(url => `  <url><loc>${escapeHtml(url)}</loc></url>`).join('\n') + '\n</urlset>\n');
  const selected = [...new Map(products.map(s => [s.family, s])).values()].slice(0, 6);
  const cards = selected.map(s => `<a class="asset-listing" href="./io/view.html?item=${s.id}"><div class="asset-listing-media"><img src="./io/items/${s.id}/view-hero.png" alt="${escapeHtml(s.display_name)}" width="600" height="600" loading="lazy"></div><div class="asset-listing-body"><h3>${escapeHtml(s.display_name)}</h3><p>${escapeHtml(s.family)}</p></div></a>`).join('\n');
  const homePath = join(site, 'index.html');
  const home = readFileSync(homePath, 'utf8');
  if (!home.includes('<!-- product-previews:start -->')) throw new Error('Home product preview marker missing');
  writeFileSync(homePath, home.replace(/<!-- product-previews:start -->[\s\S]*?<!-- product-previews:end -->/, `<!-- product-previews:start -->\n${cards}\n<!-- product-previews:end -->`));
  console.log(`Product Pages: ${products.length} canonical products, catalog and sitemap generated; ${selected.length} Home previews`);
  return products;
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) buildProductPages(process.argv[2]);
