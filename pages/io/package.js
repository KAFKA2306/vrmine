(() => {
  const encoder = new TextEncoder();
  const blockSize = 512;

  function writeAscii(target, offset, length, value) {
    const bytes = encoder.encode(value);
    if (bytes.length > length) throw new Error(`tar field too long: ${value}`);
    target.set(bytes, offset);
  }

  function writeOctal(target, offset, length, value) {
    const text = Math.trunc(value).toString(8).padStart(length - 1, "0") + "\0";
    writeAscii(target, offset, length, text);
  }

  function tarHeader(name, size) {
    const header = new Uint8Array(blockSize);
    writeAscii(header, 0, 100, name);
    writeOctal(header, 100, 8, 0o644);
    writeOctal(header, 108, 8, 0);
    writeOctal(header, 116, 8, 0);
    writeOctal(header, 124, 12, size);
    writeOctal(header, 136, 12, 0);
    header.fill(0x20, 148, 156);
    header[156] = "0".charCodeAt(0);
    writeAscii(header, 257, 6, "ustar\0");
    writeAscii(header, 263, 2, "00");
    const checksum = header.reduce((sum, byte) => sum + byte, 0);
    writeAscii(header, 148, 8, checksum.toString(8).padStart(6, "0") + "\0 ");
    return header;
  }

  function buildTar(entries) {
    const chunks = [];
    let total = 1024;
    for (const entry of entries) {
      const data = entry.data instanceof Uint8Array ? entry.data : new Uint8Array(entry.data);
      const padding = (blockSize - (data.length % blockSize)) % blockSize;
      chunks.push(tarHeader(entry.name, data.length), data, new Uint8Array(padding));
      total += blockSize + data.length + padding;
    }
    chunks.push(new Uint8Array(1024));
    const output = new Uint8Array(total);
    let offset = 0;
    for (const chunk of chunks) {
      output.set(chunk, offset);
      offset += chunk.length;
    }
    return output;
  }

  async function sha256Hex(bytes) {
    const digest = await crypto.subtle.digest("SHA-256", bytes);
    return [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, "0")).join("");
  }

  async function fetchBytes(url) {
    const response = await fetch(url, {cache: "no-store"});
    if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
    return new Uint8Array(await response.arrayBuffer());
  }

  async function fetchJson(url) {
    const response = await fetch(url, {cache: "no-store"});
    if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
    return {text: await response.text(), response};
  }

  function packageReadme(spec, manifest) {
    const dimensions = Array.isArray(spec.dimensions_m) ? spec.dimensions_m.join(" x ") : "UNVERIFIED";
    const formats = Array.isArray(spec.formats) ? spec.formats.join(", ") : "UNVERIFIED";
    return [
      `${spec.display_name || spec.id}`,
      "",
      `SKU: ${spec.id}`,
      `Dimensions (m): ${dimensions}`,
      `Formats: ${formats}`,
      `License: ${spec.license?.name || "UNVERIFIED"} / ${spec.license?.status || "UNVERIFIED"}`,
      `Unity: ${spec.unity_status || "UNVERIFIED"}`,
      `VRChat: ${spec.vrchat_status || "UNVERIFIED"}`,
      `BOOTH: ${spec.booth_status || "UNVERIFIED"}`,
      `Generator: ${manifest.blender ? `Blender ${manifest.blender}` : "UNVERIFIED"}`,
      `Spec SHA-256: ${manifest.spec_sha256 || "UNVERIFIED"}`,
      "",
      "manifest.json is the generated-asset identity and hash record.",
      "spec.json is the canonical product declaration included with this package.",
      "Runtime or marketplace states marked UNVERIFIED are not claimed as tested or published.",
      ""
    ].join("\n");
  }

  async function downloadPackage(button, status, base, id) {
    button.disabled = true;
    status.textContent = "manifestと公開assetを照合しています…";
    try {
      const manifestResult = await fetchJson(base + "manifest.json");
      const specResult = await fetchJson(base + "spec.json");
      const manifest = JSON.parse(manifestResult.text);
      const spec = JSON.parse(specResult.text);
      if (!manifest.id || manifest.id !== id || manifest.id !== spec.id) throw new Error("manifest/spec SKU mismatch");

      const licenseStatus = String(spec.license?.status || "UNVERIFIED");
      if (!/^(VERIFIED|PUBLISHED)$/i.test(licenseStatus)) throw new Error(`distribution blocked by license status: ${licenseStatus}`);

      const specBytes = encoder.encode(specResult.text);
      if (!manifest.spec_sha256 || await sha256Hex(specBytes) !== manifest.spec_sha256) throw new Error("spec.json: SHA-256 mismatch");

      const assetNames = Object.keys(manifest.sha256 || {}).filter(name => /\.(blend|glb|fbx)$/i.test(name));
      const declaredFormats = new Set((spec.formats || []).map(format => String(format).toLowerCase()));
      const actualFormats = new Set(assetNames.map(name => name.split(".").pop().toLowerCase()));
      if (assetNames.length === 0) throw new Error("manifest has no distributable 3D formats");
      if (declaredFormats.size !== actualFormats.size || [...declaredFormats].some(format => !actualFormats.has(format))) throw new Error("spec/manifest format mismatch");

      const entries = [];
      for (const name of assetNames) {
        status.textContent = `${name} をSHA-256で照合しています…`;
        const bytes = await fetchBytes(base + name);
        if (await sha256Hex(bytes) !== manifest.sha256[name]) throw new Error(`${name}: SHA-256 mismatch`);
        entries.push({name, data: bytes});
      }
      entries.push({name: "manifest.json", data: encoder.encode(manifestResult.text)});
      entries.push({name: "spec.json", data: specBytes});
      entries.push({name: "README.txt", data: encoder.encode(packageReadme(spec, manifest))});

      const blob = new Blob([buildTar(entries)], {type: "application/x-tar"});
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${manifest.id}-package.tar`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      status.textContent = `取得完了: ${assetNames.length}形式をmanifestと照合済み。利用条件: ${licenseStatus}`;
    } catch (error) {
      console.error(error);
      status.textContent = `取得失敗: ${error.message}`;
    } finally {
      button.disabled = false;
    }
  }

  function waitForSelector(selector) {
    const existing = document.querySelector(selector);
    if (existing) return Promise.resolve(existing);
    return new Promise(resolve => {
      const observer = new MutationObserver(() => {
        const found = document.querySelector(selector);
        if (!found) return;
        observer.disconnect();
        resolve(found);
      });
      observer.observe(document.documentElement, {childList: true, subtree: true});
    });
  }

  async function install() {
    const id = new URLSearchParams(location.search).get("item");
    if (!/^[a-z0-9-]+$/.test(id || "")) return;
    const base = `items/${id}/`;
    const specResult = await fetchJson(base + "spec.json");
    const spec = JSON.parse(specResult.text);
    if (spec.id !== id || !/^(VERIFIED|PUBLISHED)$/i.test(String(spec.license?.status || ""))) return;

    const actions = await waitForSelector(".actions");
    const note = await waitForSelector(".distribution-note");
    actions.replaceChildren();
    const button = document.createElement("button");
    button.type = "button";
    button.className = "btn primary";
    button.dataset.packageDownload = "";
    button.textContent = "検証済み配布パッケージを取得";
    actions.appendChild(button);
    note.dataset.packageStatus = "";
    note.textContent = "BLEND / GLB / FBX をmanifestのSHA-256と照合して1つのTARにまとめます。";
    button.addEventListener("click", () => downloadPackage(button, note, base, id));
  }

  install().catch(error => console.error("package installer failed", error));
})();
