import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";

const fail = message => { throw new Error(message); };
const read = path => readFileSync(path, "utf8");
const contract = JSON.parse(read("config/ai-unity.json"));
const manifest = JSON.parse(read("Packages/manifest.json"));
const runner = JSON.parse(read(".uloop/project-runner-pin.json"));
const projectVersion = read("ProjectSettings/ProjectVersion.txt");
const codex = read(".codex/config.toml");

if (!projectVersion.includes(`m_EditorVersion: ${contract.unity}`)) fail("Unity version differs from AI bridge contract");
if (manifest.dependencies["io.github.hatayama.uloopmcp"] !== contract.unityCliLoop.package) fail("unity-cli-loop package is not pinned");
if (manifest.dependencies["com.tunasync.unity-mcp"] !== contract.tunaSync.unityPackage) fail("TunaSync Unity package is not pinned");
if (!codex.includes(`args = ["-y", "${contract.tunaSync.serverPackage}"]`)) fail("Codex TunaSync MCP server is not pinned");
if (!codex.includes(`tool_timeout_sec = ${contract.tunaSync.toolTimeoutSeconds}`)) fail("Codex TunaSync MCP timeout differs from contract");
if (runner.projectRunnerVersion !== "3.3.0" || runner.dispatcherReleaseTag !== "dispatcher-v3.3.1") fail("uloop project-runner pin differs from v3.5.0");

const skillsRoot = ".agents/skills";
const uloopSkills = readdirSync(skillsRoot, { withFileTypes: true })
  .filter(e => e.isDirectory() && e.name.startsWith(contract.unityCliLoop.skillPrefix) && existsSync(join(skillsRoot, e.name, "SKILL.md")))
  .map(e => e.name);
if (uloopSkills.length !== contract.unityCliLoop.skillCount) fail(`expected ${contract.unityCliLoop.skillCount} uloop skills, found ${uloopSkills.length}`);
for (const name of contract.vrcAgentSkills.installed) {
  if (!existsSync(join(skillsRoot, name, "SKILL.md"))) fail(`missing VRChat agent skill: ${name}`);
}
if (!existsSync(join(skillsRoot, "ULOOP_LICENSE.md"))) fail("vendored uloop license is missing");
console.log(`PASS AI Unity setup: Unity ${contract.unity}; uloop ${contract.unityCliLoop.version} (${uloopSkills.length} skills); VRC skills ${contract.vrcAgentSkills.version}; TunaSync ${contract.tunaSync.version} MCP/package pinned`);
