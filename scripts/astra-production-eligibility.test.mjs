import assert from "node:assert/strict";
import { assertProductionEligible } from "./assert-astra-production-eligibility.mjs";

const policy = {
  tiers: {
    P0: { production_eligible: true, experimental: false },
    P1: { production_eligible: true, experimental: false },
    P2: { production_eligible: false, experimental: true },
    P3: { production_eligible: false, experimental: true },
  },
};
const manifest = (tier, toolTiers = [tier], experimental = policy.tiers[tier]?.experimental) => ({
  kind: "astra_toolchain_manifest",
  policy: { tier, experimental },
  tools: toolTiers.map((toolTier) => ({ name: `tool-${toolTier}`, tier: toolTier })),
});

assert.deepEqual(assertProductionEligible(manifest("P0"), policy), { status: "PASS", tier: "P0", production_eligible: true });
assert.equal(assertProductionEligible(manifest("P1", ["P0", "P1"]), policy).status, "PASS");
assert.throws(() => assertProductionEligible(manifest("P2"), policy), /P2 is not production eligible/);
assert.throws(() => assertProductionEligible(manifest("P3"), policy), /P3 is not production eligible/);
assert.throws(() => assertProductionEligible(manifest("P0", ["P0", "P2"]), policy), /tool tier P2 is not production eligible/);
assert.throws(() => assertProductionEligible(manifest("P2", ["P0"], false), policy), /experimental flag disagrees/);
assert.throws(() => assertProductionEligible(manifest("P9", [], false), policy), /unknown production tier/);

console.log("astra production eligibility contract: PASS");
