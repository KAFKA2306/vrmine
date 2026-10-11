const PIN = /^(?<repository>[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+)@(?<revision>[0-9a-f]{40})$/;

export function parseRendererPin(pin) {
  const match = PIN.exec(pin ?? '');
  if (!match) throw new Error(`renderers.unity_vrchat must be owner/repo@<40-hex commit>, got ${JSON.stringify(pin)}`);
  return { ...match.groups };
}
