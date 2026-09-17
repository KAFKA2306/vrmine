#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const POLICY_PATH = path.join(ROOT, "config", "astra-toolchain-policy.json");

export function assertProductionEligible(manifest, policy = JSON.parse(fs.readFileSync(POLICY_PATH, "utf8"))) {
  if (!manifest || manifest.kind !== "astra_toolchain_manifest") throw new Error("invalid Astra toolchain manifest");
  const tier = manifest.policy?.tier;
  const tierPolicy = policy.tiers?.[tier];
  if (!tierPolicy) throw new Error(`unknown production tier: ${tier ?? "missing"}`);
  const declaredExperimental = manifest.policy?.experimental;
  if (declaredExperimental !== tierPolicy.experimental) {
    throw new Error(`experimental flag disagrees with ${tier} policy`);
  }
  const toolTiers = [...new Set((manifest.tools ?? []).map((tool) => tool.tier))];
  for (const toolTier of toolTiers) {
    const toolPolicy = policy.tiers?.[toolTier];
    if (!toolPolicy) throw new Error(`unknown tool tier: ${toolTier}`);
    if (!toolPolicy.production_eligible) throw new Error(`production blocked: tool tier ${toolTier} is not production eligible`);
  }
  if (!tierPolicy.production_eligible) throw new Error(`production blocked: ${tier} is not production eligible`);
  return { status: "PASS", tier, production_eligible: true };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const manifestPath = process.argv[2];
  if (!manifestPath) {
    console.error("usage: node scripts/assert-astra-production-eligibility.mjs <toolchain-manifest.json>");
    process.exit(2);
  }
  try {
    const manifest = JSON.parse(fs.readFileSync(path.resolve(manifestPath), "utf8"));
    process.stdout.write(`${JSON.stringify(assertProductionEligible(manifest))}\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
