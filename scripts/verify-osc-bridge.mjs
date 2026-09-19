import fs from 'node:fs';
import assert from 'node:assert/strict';

const cfg = JSON.parse(fs.readFileSync(new URL('../config/osc-bridge.json', import.meta.url)));
const fail = (m) => { throw new Error(`OSC contract: ${m}`); };

if (cfg.schema_version !== 1) fail('schema_version must be 1');
if (cfg.transport.protocol !== 'udp') fail('transport must be udp');
if (cfg.transport.external_device_required !== false) fail('external devices must remain optional');
if (!cfg.flow.bidirectional) fail('flow must be bidirectional');
if (!(cfg.flow.max_messages_per_second > 0)) fail('rate limit must be positive');
if (cfg.flow.smoothing.method !== 'ema' || !(cfg.flow.smoothing.alpha > 0 && cfg.flow.smoothing.alpha <= 1)) fail('EMA smoothing must use alpha in (0,1]');
const r = cfg.transport.reconnect;
if (!(r.initial_ms > 0 && r.max_ms >= r.initial_ms && r.multiplier > 1)) fail('invalid reconnect policy');
if (!cfg.namespace.root.startsWith('/')) fail('namespace root must be an OSC path');
const name = new RegExp(cfg.namespace.parameter_pattern);
for (const sample of ['Speed', 'FaceSmile', 'AI_GazeX']) assert.equal(name.test(sample), true);
for (const sample of ['', 'bad-name', '1startsWithDigit']) assert.equal(name.test(sample), false);

export function address(parameter) {
  if (!name.test(parameter) || cfg.namespace.reserved_prefixes.some((p) => parameter.startsWith(p))) throw new Error(`invalid parameter: ${parameter}`);
  return `${cfg.namespace.root}/${parameter}`;
}

export function reconnectDelay(attempt) {
  return Math.min(r.max_ms, r.initial_ms * r.multiplier ** Math.max(0, attempt));
}

export function smooth(previous, input) {
  const a = cfg.flow.smoothing.alpha;
  return previous + a * (input - previous);
}

export function makeHarness() {
  const outbound = [];
  const inbound = [];
  let windowStart = 0;
  let sent = 0;
  return {
    send(parameter, value, nowMs) {
      if (nowMs - windowStart >= 1000) { windowStart = nowMs; sent = 0; }
      if (sent >= cfg.flow.max_messages_per_second) return { status: 'RATE_LIMITED' };
      sent += 1;
      const packet = { address: address(parameter), value };
      outbound.push(packet);
      return { status: 'SENT', packet };
    },
    receive(packet) {
      if (!packet.address.startsWith(`${cfg.namespace.root}/`)) return { status: 'IGNORED' };
      inbound.push(packet);
      return { status: 'RECEIVED', packet };
    },
    snapshot: () => ({ outbound: [...outbound], inbound: [...inbound] })
  };
}

const h = makeHarness();
const tx = h.send('Speed', 0.75, 0);
assert.equal(tx.status, 'SENT');
assert.equal(h.receive({ address: address('FaceSmile'), value: 1 }).status, 'RECEIVED');
assert.equal(h.snapshot().outbound.length, 1);
assert.equal(h.snapshot().inbound.length, 1);
assert.equal(reconnectDelay(0), r.initial_ms);
assert.equal(reconnectDelay(99), r.max_ms);
assert.ok(Math.abs(smooth(0, 1) - cfg.flow.smoothing.alpha) < 1e-9);
for (let i = 1; i < cfg.flow.max_messages_per_second; i++) assert.equal(h.send('Speed', i, 1).status, 'SENT');
assert.equal(h.send('Speed', 999, 1).status, 'RATE_LIMITED');
for (const [key, adapter] of Object.entries(cfg.adapters)) {
  if (key !== 'vrchat_osc' && adapter.required) fail(`${key} must be optional`);
}
console.log('OSC bridge contract PASS');
