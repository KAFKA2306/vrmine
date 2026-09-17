#!/usr/bin/env node
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const [specArg, outputArg] = process.argv.slice(2);
if (!specArg) {
  console.error("usage: node scripts/compile-astra-toolchain-manifest.mjs <config/world-items/*.json> [output.json]");
  process.exit(2);
}

const root = process.cwd();
const request = JSON.parse(execFileSync(process.execPath, ["scripts/compile-astra-build-request.mjs", specArg], { cwd: root, encoding: "utf8" }));
const projectVersion = fs.readFileSync(path.join(root, "ProjectSettings/ProjectVersion.txt"), "utf8").match(/^m_EditorVersion: (.+)$/m)?.[1];
const packages = JSON.parse(fs.readFileSync(path.join(root, "Packages/manifest.json"), "utf8")).dependencies;
const repositoryCommit = execFileSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).trim();
if (!projectVersion) throw new Error("Unity version is missing from ProjectSettings/ProjectVersion.txt");
if (!/^[0-9a-f]{40}$/.test(repositoryCommit)) throw new Error("repository HEAD is not an exact Git commit");

const manifest = {
  $schema: "config/astra-toolchain-manifest.schema.json",
  schema_version: 1,
  kind: "astra_toolchain_manifest",
  source: {
    canonical_spec: request.source.canonical_spec,
    canonical_spec_sha256: request.source.sha256,
    repository_commit: repositoryCommit
  },
  policy: {
    tier: request.toolchain_policy.production_tier,
    control_priority: request.toolchain_policy.control_priority,
    one_responsibility_one_authority: request.toolchain_policy.one_responsibility_one_authority,
    experimental: request.toolchain_policy.production_tier === "P2" || request.toolchain_policy.production_tier === "P3"
  },
  tools: [
    {
      name: "gpt-6-astra",
      version: "gpt-6-astra",
      role: "authoring_agent",
      control_mode: "code",
      tier: request.toolchain_policy.production_tier,
      source: null,
      license: null,
      exact_revision: null
    },
    {
      name: "Unity",
      version: projectVersion,
      role: "production_runtime_verification",
      control_mode: "code",
      tier: "P0",
      source: "ProjectSettings/ProjectVersion.txt",
      license: null,
      exact_revision: null
    },
    {
      name: "VRChat SDK Worlds",
      version: packages["com.vrchat.worlds"] ?? "UNVERIFIED",
      role: "vrchat_world_validation",
      control_mode: "code",
      tier: "P0",
      source: "Packages/manifest.json",
      license: null,
      exact_revision: null
    }
  ],
  runtime: {
    blender: { status: "UNVERIFIED", evidence: null },
    unity: { status: "UNVERIFIED", evidence: null },
    vrchat: { status: "UNVERIFIED", evidence: null },
    pc: { status: "UNVERIFIED", evidence: null },
    android: { status: "UNVERIFIED", evidence: null }
  }
};

const text = `${JSON.stringify(manifest, null, 2)}\n`;
if (outputArg) {
  const outputPath = path.resolve(root, outputArg);
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, text);
} else {
  process.stdout.write(text);
}
