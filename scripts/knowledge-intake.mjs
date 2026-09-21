import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { basename, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const TYPE_RULES = [
  ["failure", /fail|failure|error|破綻|失敗|不具合|バグ|問題|クラッシュ|ジッター|貫通|めり込み/i],
  ["repair", /repair|fix|修復|解消|防止|回避|置換|補正|最適化/i],
  ["constraint", /limit|上限|制限|constraint|規約|禁止|必須|budget|閾値|しきい値/i],
  ["metric", /fps|vram|runtime|latency|ms\b|kb\/s|mb\b|gb\b|%|倍|削減|短縮|向上/i],
  ["distribution", /booth|market|公開|publish|販売|配布|community labs/i],
  ["pattern", /設計|pattern|workflow|パイプライン|責務分離|非破壊|自律|再生成/i],
  ["technique", /geometry nodes|bpy|python|mcp|cua|shader|physbone|udon|colmap|splat/i],
  ["capability", /.*/i]
];

const TAG_RULES = {
  procedural_generation: /procedural|プロシージャル|geometry nodes|bpy|パラメトリック/i,
  self_repair: /self[- ]?repair|自己修復|再試行|再インポート|修正.*再/i,
  non_destructive: /non[- ]?destructive|非破壊|ndmf|modular avatar/i,
  control_plane: /mcp|cua|cli|api|操作形態|責務分離/i,
  platform_budget: /quest|android|mobile|ios|pc版|platform|プラットフォーム/i,
  performance: /fps|vram|cpu|gpu|gc alloc|draw call|ドローコール|帯域|kb\/s|レイテンシ|ms\b/i,
  networking: /network|sync|同期|udonsynced|serialization|rpc|playerdata|playerobject/i,
  rigging_physics: /rig|bone|physbone|collider|blendshape|animator|weight|リグ|ボーン|衣装/i,
  rendering: /shader|material|texture|lighting|lightmap|reflection|シェーダ|マテリアル|テクスチャ|照明/i,
  reconstruction: /photogrammetry|gaussian|splat|colmap|nerfstudio|reconstruction|写真|映像.*3d/i,
  provenance: /provenance|license|ライセンス|著作権|ai生成.*明記|開示/i
};

const ROUTES = {
  "KAFKA2306/vrmine": /vrchat|unity|blender|geometry|udon|world|ワールド|shader|lighting|network|splat/i,
  "KAFKA2306/AutoPhotogrammetry": /photogrammetry|gaussian|splat|colmap|nerfstudio|reconstruction|再構成|camera pose|写真.*3d|映像.*3d/i,
  "KAFKA2306/avatars2509": /avatar|アバター|outfit|衣装|physbone|blendshape|animator|ndmf|modular avatar|mochifitter|vrcfury|bone|ボーン/i
};

const NUMERIC_CLAIM = /(\d+(?:\.\d+)?)\s*(?:%|ms|s\b|秒|分|時間|kb\/s|mb\b|gb\b|fps|倍|個|本|ポリゴン|transforms?)/i;

function normalize(text) {
  return text.normalize("NFKC").replace(/\s+/g, " ").trim().toLowerCase();
}

function classifyType(statement) {
  for (const [type, pattern] of TYPE_RULES) if (pattern.test(statement)) return type;
  return "capability";
}

function tags(statement) {
  return Object.entries(TAG_RULES).filter(([, p]) => p.test(statement)).map(([name]) => name);
}

function repositories(statement) {
  const matched = Object.entries(ROUTES).filter(([, p]) => p.test(statement)).map(([name]) => name);
  return matched.length ? matched : ["KAFKA2306/vrmine"];
}

function eventFor(statement, sourceRef, line) {
  const canonical = normalize(statement);
  const id = "ke_" + createHash("sha256").update(canonical).digest("hex").slice(0, 16);
  const numeric = NUMERIC_CLAIM.test(statement);
  return {
    schema_version: 1,
    id,
    source: { kind: "text", ref: sourceRef, line },
    claim: { type: classifyType(statement), statement, tags: tags(statement) },
    applies_to: { repositories: repositories(statement) },
    evidence: {
      status: "CLAIMED",
      numeric_claim_present: numeric,
      requires_local_reproduction: true
    },
    promotion: { target: "candidate" }
  };
}

export function ingestText(text, sourceRef = "stdin") {
  const seen = new Set();
  const events = [];
  const lines = text.split(/\r?\n/);
  lines.forEach((raw, index) => {
    const statement = raw.trim().replace(/^[-*|]\s*/, "").replace(/\|$/, "").trim();
    if (!statement || /^制作[・\s]?検証手法$/.test(statement)) return;
    const key = normalize(statement);
    if (seen.has(key)) return;
    seen.add(key);
    events.push(eventFor(statement, sourceRef, index + 1));
  });
  return events;
}

export function summarize(events) {
  const byRepository = {};
  const byTag = {};
  let numericClaims = 0;
  for (const event of events) {
    if (event.evidence.numeric_claim_present) numericClaims++;
    for (const repo of event.applies_to.repositories) byRepository[repo] = (byRepository[repo] ?? 0) + 1;
    for (const tag of event.claim.tags) byTag[tag] = (byTag[tag] ?? 0) + 1;
  }
  return { event_count: events.length, numeric_claim_count: numericClaims, by_repository: byRepository, by_tag: byTag };
}

async function main() {
  const args = process.argv.slice(2);
  const value = flag => {
    const i = args.indexOf(flag);
    return i >= 0 ? args[i + 1] : null;
  };
  const input = value("--input");
  if (!input) throw new Error("usage: node scripts/knowledge-intake.mjs --input <text-file> [--output <dir>]");
  const output = resolve(value("--output") ?? ".artifacts/knowledge-intake");
  const text = await readFile(input, "utf8");
  const events = ingestText(text, basename(input));
  await mkdir(output, { recursive: true });
  await writeFile(resolve(output, "events.jsonl"), events.map(x => JSON.stringify(x)).join("\n") + "\n");
  await writeFile(resolve(output, "summary.json"), JSON.stringify(summarize(events), null, 2) + "\n");
  process.stdout.write(JSON.stringify({ output, ...summarize(events) }, null, 2) + "\n");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}