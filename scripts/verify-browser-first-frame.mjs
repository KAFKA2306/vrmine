const targetUrl = process.argv[2];
const timeoutMs = Number(process.argv[3] ?? 120000);
const debuggerBase = process.argv[4] ?? 'http://127.0.0.1:9222';

if (!targetUrl) throw new Error('usage: node scripts/verify-browser-first-frame.mjs <url> [timeoutMs] [debuggerBase]');
if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) throw new Error(`invalid timeout: ${timeoutMs}`);
if (typeof WebSocket !== 'function') throw new Error('Node WebSocket client is unavailable');

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const deadline = Date.now() + timeoutMs;
let target;
while (Date.now() < deadline) {
  try {
    const response = await fetch(`${debuggerBase}/json/list`, { cache: 'no-store' });
    if (response.ok) {
      const pages = await response.json();
      target = pages.find((page) => page.type === 'page' && page.url?.startsWith(targetUrl));
      if (!target) target = pages.find((page) => page.type === 'page');
      if (target?.webSocketDebuggerUrl) break;
    }
  } catch {
    // Chrome may still be starting. Retry until the shared deadline.
  }
  await sleep(250);
}
if (!target?.webSocketDebuggerUrl) throw new Error('Chrome DevTools page target was not available before timeout');

const socket = new WebSocket(target.webSocketDebuggerUrl);
const pending = new Map();
let sequence = 0;
let lastState = null;
let runtimeError = null;

const call = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++sequence;
  const timer = setTimeout(() => {
    pending.delete(id);
    reject(new Error(`CDP ${method} timed out`));
  }, 10000);
  pending.set(id, { resolve, reject, timer });
  socket.send(JSON.stringify({ id, method, params }));
});

socket.addEventListener('message', (event) => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    const current = pending.get(message.id);
    clearTimeout(current.timer);
    pending.delete(message.id);
    if (message.error) current.reject(new Error(`CDP error: ${JSON.stringify(message.error)}`));
    else current.resolve(message.result);
    return;
  }

  if (message.method === 'Runtime.exceptionThrown') {
    runtimeError = message.params?.exceptionDetails?.exception?.description
      ?? message.params?.exceptionDetails?.text
      ?? 'runtime exception';
    console.error(`browser exception: ${runtimeError}`);
  }
  if (message.method === 'Runtime.consoleAPICalled') {
    const values = (message.params?.args ?? []).map((arg) => arg.value ?? arg.description ?? '').join(' ');
    if (message.params?.type === 'error' || message.params?.type === 'warning') {
      console.error(`browser console ${message.params.type}: ${values}`);
    }
  }
});

await new Promise((resolve, reject) => {
  socket.addEventListener('open', resolve, { once: true });
  socket.addEventListener('error', () => reject(new Error('failed to connect to Chrome DevTools WebSocket')), { once: true });
});
await call('Runtime.enable');

while (Date.now() < deadline) {
  const evaluated = await call('Runtime.evaluate', {
    expression: `(() => {
      const root = document.documentElement;
      return {
        href: location.href,
        readyState: document.readyState,
        bridge: root?.dataset?.vrmineBridge || null,
        dom: root?.dataset?.vrmineDom || null,
        firstFrame: root?.dataset?.vrmineFirstFrame || null,
        sourceId: root?.dataset?.vrmineSourceId || null,
        error: root?.dataset?.vrmineError || null,
        body: document.body?.innerText?.slice(0, 800) || ''
      };
    })()`,
    returnByValue: true,
    awaitPromise: true,
  });
  lastState = evaluated?.result?.value ?? null;

  if (lastState?.error) {
    throw new Error(`viewer reported runtime error: ${lastState.error}`);
  }
  if (runtimeError) throw new Error(`viewer runtime exception: ${runtimeError}`);
  if (lastState?.firstFrame === 'pass') {
    if (lastState.sourceId !== 'huejotzingo') {
      throw new Error(`unexpected first-frame source id: ${lastState.sourceId}`);
    }
    console.log(`SuperSplat first frame PASS: source=${lastState.sourceId}, readyState=${lastState.readyState}`);
    socket.close();
    process.exit(0);
  }
  await sleep(500);
}

socket.close();
throw new Error(`SuperSplat first frame timeout after ${timeoutMs} ms; lastState=${JSON.stringify(lastState)}`);
