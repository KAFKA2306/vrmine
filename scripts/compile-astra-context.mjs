#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const [specArg, outputArg] = process.argv.slice(2);
if (!specArg) {
  console.error("usage: node scripts/compile-astra-context.mjs <config/world-items/*.json> [output.json]");
  process.exit(2);
}

const root = process.cwd();
const request = JSON.parse(execFileSync(process.execPath, ["scripts/compile-astra-build-request.mjs", specArg], { cwd: root, encoding: "utf8" }));
const context = {
  schema_version: 1,
  kind: "astra_execution_context",
  objective: `Build ${request.intent.display_name} from the canonical specification without changing its intended dimensions.`,
  authority: {
    canonical_spec: request.source.canonical_spec,
    canonical_spec_sha256: request.source.sha256,
    build_request_schema: request.$schema
  },
  inputs: {
    intent: request.intent,
    deterministic_seed: request.constraints.deterministic_seed,
    license: request.constraints.license
  },
  constraints: {
    generation_method: request.constraints.generation_method,
    canonical_source: request.constraints.canonical_source,
    manual_mesh_edit_as_authority: request.constraints.manual_mesh_edit_as_authority,
    external_asset_policy: request.constraints.external_asset_policy,
    one_responsibility_one_authority: request.toolchain_policy.one_responsibility_one_authority
  },
  completion_contract: {
    required_artifacts: request.required_artifacts,
    verification: request.verification
  },
  control: {
    production_tier: request.toolchain_policy.production_tier,
    priority: request.toolchain_policy.control_priority
  }
};

const text = `${JSON.stringify(context, null, 2)}\n`;
if (outputArg) {
  const outputPath = path.resolve(root, outputArg);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, text);
} else {
  process.stdout.write(text);
}
