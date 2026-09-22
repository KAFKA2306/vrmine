import test from "node:test";
import assert from "node:assert/strict";
import { ingestText, summarize } from "./knowledge-intake.mjs";

test("deduplicates and keeps external numeric claims unverified", () => {
  const events = ingestText([
    "制作・検証手法",
    "VRChatの鏡負荷を50%削減する手法。",
    "VRChatの鏡負荷を50%削減する手法。"
  ].join("\n"), "fixture.txt");
  assert.equal(events.length, 1);
  assert.equal(events[0].evidence.status, "CLAIMED");
  assert.equal(events[0].evidence.numeric_claim_present, true);
  assert.equal(events[0].evidence.requires_local_reproduction, true);
});

test("routes one claim to multiple repositories when appropriate", () => {
  const [event] = ingestText("Gaussian SplatをVRChat Unityへ取り込み、Quest向けにLOD化する。", "fixture.txt");
  assert.deepEqual(
    event.applies_to.repositories.sort(),
    ["KAFKA2306/AutoPhotogrammetry", "KAFKA2306/vrmine"].sort()
  );
  assert.ok(event.claim.tags.includes("reconstruction"));
  assert.ok(event.claim.tags.includes("platform_budget"));
});

test("summarizes reusable tags instead of creating one issue per row", () => {
  const events = ingestText([
    "Geometry Nodesでプロシージャル生成する。",
    "NDMFで非破壊変換する。",
    "UdonSyncedの帯域を計測する。"
  ].join("\n"));
  const summary = summarize(events);
  assert.equal(summary.event_count, 3);
  assert.equal(summary.by_tag.procedural_generation, 1);
  assert.equal(summary.by_tag.non_destructive, 1);
  assert.equal(summary.by_tag.networking, 1);
});
