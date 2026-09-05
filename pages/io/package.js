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

  async function downloadPackage(button, status) {
    button.disabled = true;
    status.textContent = "manifestと公開assetを照合しています…";
    try {
      const manifestResult = await fetchJson("manifest.json");
      const specResult = await fetchJson("spec.json");
      const manifest = JSON.parse(manifestResult.text);
      const spec = JSON.parse(specResult.text);
      if (!manifest.id || manifest.id !== spec.id) throw new Error("manifest/spec SKU mismatch");

      const assetNames = Object.keys(manifest.sha256 || {}).filter(name => /\.(blend|glb|fbx)$/i.test(name));
      if (assetNames.length === 0) throw new Error("manifest has no distributable 3D formats");

      const entries = [];
      for (const name of assetNames) {
        status.textContent = `${name} をSHA-256で照合しています…`;
        const bytes = await fetchBytes(name);
        const actual = await sha256Hex(bytes);
        const expected = manifest.sha256[name];
        if (actual !== expected) throw new Error(`${name}: SHA-256 mismatch`);
        entries.push({name, data: bytes});
      }
      entries.push({name: "manifest.json", data: encoder.encode(manifestResult.text)});
      entries.push({name: "spec.json", data: encoder.encode(specResult.text)});

      const archive = buildTar(entries);
      const blob = new Blob([archive], {type: "application/x-tar"});
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `${manifest.id}-package.tar`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      const licenseStatus = spec.license?.status || "UNVERIFIED";
      status.textContent = `取得完了: ${assetNames.length}形式をmanifestと照合済み。利用条件: ${licenseStatus}`;
    } catch (error) {
      console.error(error);
      status.textContent = `取得失敗: ${error.message}`;
    } finally {
      button.disabled = false;
    }
  }

  document.addEventListener("DOMContentLoaded", () => {
    const button = document.querySelector("[data-package-download]");
    const status = document.querySelector("[data-package-status]");
    if (!button || !status) return;
    button.addEventListener("click", () => downloadPackage(button, status));
  });
})();
