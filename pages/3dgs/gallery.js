const status = document.getElementById('viewer-status');
const iframe = document.getElementById('viewer');
const select = document.getElementById('fixture-select');
const previous = document.getElementById('fixture-previous');
const next = document.getElementById('fixture-next');
const count = document.getElementById('fixture-count');
const sourceCommit = document.getElementById('source-commit');
const fixtureId = document.getElementById('fixture-id');
const sourceSha = document.getElementById('source-sha');
const sourcePage = document.getElementById('source-page');
const sourceAuthor = document.getElementById('source-author');
const sourceLicense = document.getElementById('source-license');
const rendererLink = document.getElementById('renderer-link');

let contract;
let entries = [];
let selectedIndex = -1;
let requestToken = 0;

const viewerSettings = {
  version: 2,
  tonemapping: 'none',
  highPrecisionRendering: false,
  background: { color: [0, 0, 0] },
  postEffectSettings: {
    sharpness: { enabled: false, amount: 0 },
    bloom: { enabled: false, intensity: 1, blurLevel: 2 },
    grading: { enabled: false, brightness: 0, contrast: 1, saturation: 1, tint: [1, 1, 1] },
    vignette: { enabled: false, intensity: 0.5, inner: 0.3, outer: 0.75, curvature: 1 },
    fringing: { enabled: false, intensity: 0.5 }
  },
  animTracks: [],
  cameras: [{ initial: { position: [0, 1, -1], target: [0, 0, 0], fov: 60 } }],
  annotations: [],
  startMode: 'default'
};
const settingsUrl = `data:application/json;charset=utf-8,${encodeURIComponent(JSON.stringify(viewerSettings))}`;

const setEvidence = (parts) => {
  document.documentElement.dataset.splatRender = parts.join('-');
};

const fail = (message) => {
  status.textContent = message;
  status.dataset.state = 'error';
  setEvidence(['render', 'error']);
};

const updateNavigation = () => {
  previous.disabled = selectedIndex <= 0;
  next.disabled = selectedIndex < 0 || selectedIndex >= entries.length - 1;
  select.value = selectedIndex >= 0 ? entries[selectedIndex].id : '';
  count.textContent = `${entries.length} upstream entries available`;
  document.documentElement.dataset.splatEntries = String(entries.length);
};

const updateMetadata = (entry) => {
  sourceCommit.href = `https://github.com/${contract.source_repository}/commit/${contract.source_commit}`;
  sourceCommit.textContent = `${contract.source_repository} ${contract.source_commit.slice(0, 8)}`;
  fixtureId.textContent = entry.id;
  sourceSha.textContent = entry.source.sha256;
  sourcePage.href = entry.source.provenance.source_page;
  sourcePage.textContent = entry.source.provenance.title;
  sourceAuthor.textContent = entry.source.provenance.author || 'author未確認';
  if (entry.source.provenance.license_status === 'verified') {
    sourceLicense.href = entry.source.provenance.license_url;
    sourceLicense.textContent = entry.source.provenance.license;
    sourceLicense.removeAttribute('aria-disabled');
  } else {
    sourceLicense.removeAttribute('href');
    sourceLicense.textContent = 'license review required';
    sourceLicense.setAttribute('aria-disabled', 'true');
  }
};

const selectEntry = (index, { updateHash = true } = {}) => {
  if (index < 0 || index >= entries.length) return;
  const entry = entries[index];
  selectedIndex = index;
  updateNavigation();
  updateMetadata(entry);
  if (updateHash) history.replaceState(null, '', `#${encodeURIComponent(entry.id)}`);

  if (entry.source.provenance.license_status !== 'verified') {
    iframe.removeAttribute('src');
    fail(`${entry.id}: exact source license is unverified; browser rendering is blocked.`);
    return;
  }

  requestToken += 1;
  const token = requestToken;
  status.textContent = `${entry.id}: 実PLYを読み込み、first frameを待っています…`;
  status.dataset.state = 'loading';
  delete document.documentElement.dataset.splatRender;
  const source = `https://raw.githubusercontent.com/${contract.source_repository}/${contract.source_commit}/${entry.source.path}`;
  const params = new URLSearchParams({
    content: source,
    settings: settingsUrl,
    vrmine_id: entry.id,
    vrmine_token: String(token)
  });
  params.set('webgl', '');
  iframe.src = `./viewer/?${params}`;
};

window.addEventListener('message', (event) => {
  if (event.origin !== location.origin || event.source !== iframe.contentWindow) return;
  if (event.data?.token !== requestToken || event.data?.id !== entries[selectedIndex]?.id) return;
  if (event.data.type === 'vrmine:3dgs:first-frame') {
    status.textContent = `${event.data.id}: 実PLYのfirst frameを描画しました — ${contract.renderers.browser}`;
    status.dataset.state = 'ready';
    setEvidence(['first', 'frame', 'pass']);
  } else if (event.data.type === 'vrmine:3dgs:error') {
    fail(`${event.data.id}: viewer error: ${event.data.message}`);
  }
});

select.addEventListener('change', () => {
  const index = entries.findIndex((entry) => entry.id === select.value);
  selectEntry(index);
});
previous.addEventListener('click', () => selectEntry(selectedIndex - 1));
next.addEventListener('click', () => selectEntry(selectedIndex + 1));
window.addEventListener('hashchange', () => {
  const requested = decodeURIComponent(location.hash.slice(1));
  const index = entries.findIndex((entry) => entry.id === requested);
  if (index >= 0 && index !== selectedIndex) selectEntry(index, { updateHash: false });
});

try {
  const response = await fetch('./gaussian-splats.json', { cache: 'no-store' });
  if (!response.ok) throw new Error(`fixture contract HTTP ${response.status}`);
  contract = await response.json();
  entries = Array.isArray(contract.environments) ? contract.environments : [];
  if (!entries.length) throw new Error('no Gaussian Splat entries are available');

  const browserRenderer = contract.renderers?.browser;
  if (browserRenderer !== '@playcanvas/supersplat-viewer@1.28.0') {
    throw new Error(`unsupported browser renderer pin: ${browserRenderer ?? 'missing'}`);
  }
  rendererLink.textContent = browserRenderer;

  const canvas = document.createElement('canvas');
  if (!navigator.gpu && !canvas.getContext('webgl2')) throw new Error('WebGPU and WebGL 2 are unavailable');

  for (const [index, entry] of entries.entries()) {
    const option = document.createElement('option');
    option.value = entry.id;
    const order = String(index + 1).padStart(2, '0');
    const licenseSuffix = entry.source.provenance.license_status === 'verified' ? '' : ' — license review required';
    option.textContent = `${order}. ${entry.source.provenance.title}${licenseSuffix}`;
    select.append(option);
  }

  const requested = decodeURIComponent(location.hash.slice(1));
  const requestedIndex = entries.findIndex((entry) => entry.id === requested);
  const firstVerifiedIndex = entries.findIndex((entry) => entry.source.provenance.license_status === 'verified');
  selectEntry(requestedIndex >= 0 ? requestedIndex : Math.max(firstVerifiedIndex, 0), { updateHash: requestedIndex < 0 });
} catch (error) {
  fail(`galleryの読み込みに失敗しました: ${error.message}`);
  console.error(error);
}
