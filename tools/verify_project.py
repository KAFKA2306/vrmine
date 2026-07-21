#!/usr/bin/env python3
"""Static integrity checks for VRMine.

These checks validate repository coherence. They do not replace Unity, UdonSharp,
or VRChat client execution in G0-G4.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def require(path: str) -> Path:
    target = ROOT / path
    if not target.exists():
        fail(f"missing required path: {path}")
    return target


def read(path: str) -> str:
    target = require(path)
    return target.read_text(encoding="utf-8-sig") if target.exists() else ""


def check_versions() -> None:
    if "m_EditorVersion: 2022.3.22f1" not in read("ProjectSettings/ProjectVersion.txt"):
        fail("Unity version must be 2022.3.22f1")

    for manifest_path in ("Packages/manifest.json", "Packages/vpm-manifest.json"):
        try:
            manifest = json.loads(read(manifest_path))
        except json.JSONDecodeError as exc:
            fail(f"invalid JSON {manifest_path}: {exc}")
            continue
        dependencies = manifest.get("dependencies", {})
        for package in ("com.vrchat.base", "com.vrchat.worlds"):
            value = dependencies.get(package)
            version = value.get("version") if isinstance(value, dict) else value
            if version != "3.10.4":
                fail(f"{manifest_path}: {package} must be 3.10.4, got {version!r}")


def iter_markdown_files() -> list[Path]:
    files = [ROOT / "README.md", ROOT / "PROJECT.md"]
    files.extend((ROOT / "docs").rglob("*.md"))
    return [path for path in files if path.exists()]


def check_markdown_links() -> None:
    link_pattern = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
    for source in iter_markdown_files():
        text = source.read_text(encoding="utf-8-sig")
        for raw_target in link_pattern.findall(text):
            target = raw_target.strip().split(maxsplit=1)[0].strip("<>")
            if not target or target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            target = unquote(target.split("#", 1)[0])
            if not target:
                continue
            resolved = (source.parent / target).resolve()
            try:
                resolved.relative_to(ROOT.resolve())
            except ValueError:
                fail(f"{source.relative_to(ROOT)} links outside repository: {raw_target}")
                continue
            if not resolved.exists():
                fail(f"broken link in {source.relative_to(ROOT)}: {raw_target}")


def expand_rule_label(label: str) -> set[int]:
    label = label.strip().replace("–", "-").replace("—", "-")
    match = re.fullmatch(r"(\d+)(?:\s*-\s*(\d+))?", label)
    if not match:
        return set()
    start = int(match.group(1))
    end = int(match.group(2) or start)
    return set(range(start, end + 1))


def check_rule_matrix() -> None:
    text = read("docs/games/trick-meister-rules.md")
    documented: set[int] = set()
    for line in text.splitlines():
        match = re.match(r"^\|\s*([^|]+?)\s*\|", line)
        if match:
            documented.update(expand_rule_label(match.group(1)))
    missing = sorted(set(range(1, 61)) - documented)
    extra = sorted(documented - set(range(1, 61)))
    if missing:
        fail(f"rule matrix is missing numbers: {missing}")
    if extra:
        fail(f"rule matrix contains unexpected numbers: {extra}")
    rule_26 = re.search(r"^\|\s*26\s*\|([^|]+)\|", text, re.MULTILINE)
    if rule_26 is None or "伏せ" not in rule_26.group(1) or "公開" not in rule_26.group(1):
        fail("rule 26 must document face-down play followed by reveal")
    if rule_26 is not None and "未実装" in rule_26.group(1):
        fail("rule 26 is implemented and must not be marked unimplemented")


def check_player_controls() -> None:
    action = read("Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs")
    scene_upgrade = read("Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs")
    orapa = read("Assets/KafkaMade/VRMine/Runtime/Game/OrapaMineGame.cs")
    lifecycle = read("Assets/KafkaMade/VRMine/Runtime/Game/TrickSeatLifecycle.cs")

    for fragment in ("action == 5", "ConfigurePlayers(value)", "OwnTrickState"):
        if fragment not in action:
            fail(f"RULEFORGE player-count action is missing: {fragment}")
    for fragment in ("action == 8", "orapaGame.ConfigurePlayers(value)"):
        if fragment not in action:
            fail(f"ECHO MINE player-count action is missing: {fragment}")

    for fragment in (
        'for (int count = 3; count <= 5; count++)',
        '"TrickPlayerCount_" + count',
        '0, 5, count, trick, null, count + "P"',
        'for (int count = 2; count <= 5; count++)',
        '"OrapaPlayerCount_" + count',
        '1, 8, count, null, orapa, count + "P"',
        'new GameObject("TrickSeatLifecycle")',
    ):
        if fragment not in scene_upgrade:
            fail(f"generated scene player-control logic is missing: {fragment}")

    for fragment in ("NextActiveSeat", "OnPlayerLeft", "attempts[seat] < 2"):
        if fragment not in orapa:
            fail(f"ECHO MINE active-seat lifecycle is missing: {fragment}")
    for fragment in ("OnPlayerLeft", "occupiedPlayerIds[seat] = 0", "local.isMaster"):
        if fragment not in lifecycle:
            fail(f"RULEFORGE seat lifecycle is missing: {fragment}")


def check_scene_upgrade_idempotence() -> None:
    scene_upgrade = read("Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs")
    for fragment in (
        "static bool upgradeInProgress",
        "IsUpgradeInProgress",
        "if (!changed) return;",
        "ReferencesMatch",
        "MinimumGeneratedActions = 152",
        "CountComponents<BoardGameAction>(scene) < MinimumGeneratedActions",
        "BoardGameShowcaseBuilder.Build();",
        "AssetDatabase.CreateAsset(material, path)",
        "if (BoardGameSceneUpgrade.IsUpgradeInProgress) return;",
    ):
        if fragment not in scene_upgrade:
            fail(f"idempotent scene upgrade is missing: {fragment}")


def check_trick_table_and_rule_26() -> None:
    controller = read("Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs")
    board_view = read("Assets/KafkaMade/VRMine/Runtime/UI/BoardView.cs")
    showcase = read("Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs")
    scene_upgrade = read("Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs")
    public_names = read("Assets/KafkaMade/VRMine/Editor/VRMinePublicNameUpgrade.cs")

    for fragment in (
        "ShouldHideTrickCard",
        "board.phase = BoardState.PhaseResolveTrick",
        "ResolvePendingTrick",
        "SendCustomEventDelayedSeconds(nameof(ResolvePendingTrick)",
        "HasRule(26) ? 2.25f : 0.55f",
        "AllSeatsOccupied()",
    ):
        if fragment not in controller:
            fail(f"RULEFORGE rule-26 or waiting-room flow is missing: {fragment}")
    if "controller.ShouldHideTrickCard(i)" not in board_view:
        fail("BoardView does not hide rule-26 trick cards")
    for fragment in ("trickTableCards", '"FACE\\nDOWN"', "OccupiedSeats"):
        if fragment not in showcase:
            fail(f"showcase trick table is missing: {fragment}")
    for fragment in ('"TrickTableCard_" + slot', "desiredTableCards[slot] = EnsureDisplay"):
        if fragment not in scene_upgrade:
            fail(f"scene upgrade does not wire Trick table display: {fragment}")
    for public_name in ('label.text = "RULEFORGE"', 'label.text = "ECHO MINE"'):
        if public_name not in public_names:
            fail(f"public game-name replacement is missing: {public_name}")


def check_g3_run_isolation() -> None:
    verification = read("Assets/KafkaMade/VRMine/Editor/BoardGameVerification.cs")
    probe = read("Assets/KafkaMade/VRMine/Runtime/Net/NetworkVerificationProbe.cs")

    for fragment in (
        "Build And Test Two Clients",
        "Finalize Two Client Logs",
        "RunToken: ",
        "StartedUtc: ",
        "expectedRunToken",
        "TryReadUtcField",
        "SECOND_CLIENT_SYNC_OBSERVED",
        "LateJoin: NOT_AUTOMATED",
        'CheckGameEvidence(report, "TRICK"',
        'CheckGameEvidence(report, "ORAPA"',
        'CheckGameEvidence(report, "CHESS"',
        "RESTORED_AFTER_TEST",
    ):
        if fragment not in verification:
            fail(f"run-isolated G3 verification is missing: {fragment}")
    if 'await builder.BuildAndTest();\n            WriteReport(VrcReportPath, "PASS' in verification:
        fail("BuildAndTest must not write PASS without parsing client evidence")

    for fragment in (
        "[UdonSynced] public int runToken",
        '"[VRMINE_G3] run=" + runToken',
        '"[VRMINE_G3_GAME] run=" + runToken',
        'LogMarker("SECOND_CLIENT_SYNC_OBSERVED")',
    ):
        if fragment not in probe:
            fail(f"network probe run isolation is missing: {fragment}")
    if "RESTORE_OR_LATE_JOIN" in verification or "RESTORE_OR_LATE_JOIN" in probe:
        fail("simultaneous two-client G3 must not claim late-join evidence")


def check_release_gate() -> None:
    release_gate = read("Assets/KafkaMade/VRMine/Editor/VRMineReleaseGate.cs")
    for report_name in ("G1Structure", "G2RuntimeRules", "G3TwoClientNetwork"):
        if report_name not in release_gate:
            fail(f"upload readiness gate is missing {report_name}")
    for requirement in (
        "TrickSeatLifecycle",
        "TrickPlayerCountControls",
        "OrapaPlayerCountControls",
        "TrickTableObjects",
        "TrickTableWiring",
        "PublicGameNames",
    ):
        if requirement not in release_gate:
            fail(f"upload readiness gate is missing {requirement}")

    g1 = read("Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt")
    g2 = read("Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt")
    g3 = read("Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt")
    g4 = read("Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt")
    if "Result: PASS" in g4 and not all("Result: PASS" in value for value in (g1, g2, g3)):
        fail("G4 says PASS while one or more prerequisite reports are not PASS")


def check_public_site_and_claims() -> None:
    site = read("site/index.html")
    readme = read("README.md")
    verification_doc = read("docs/verification.md")

    for fragment in ("<h3>RULEFORGE</h3>", "<h3>ECHO MINE</h3>", "One release-gated world"):
        if fragment not in site:
            fail(f"landing page is missing truthful public copy: {fragment}")
    for forbidden in ("Trick Meister Variant", "Orapa Mine Auto Puzzle", "late join復元", "One verified world"):
        if forbidden in site:
            fail(f"landing page contains stale or overstated copy: {forbidden}")
    if "遅延復元" in readme:
        fail("README must not claim automated late-join restoration")
    for fragment in ("RunToken", "SECOND_CLIENT_SYNC_OBSERVED", "遅延参加を自動証明しない"):
        if fragment not in verification_doc:
            fail(f"verification documentation is missing: {fragment}")


def check_project_index() -> None:
    project = read("PROJECT.md")
    for item in (
        "docs/games/trick-meister.md",
        "docs/games/orapa-mine.md",
        "docs/games/chess.md",
        "BoardGameShowcase.unity",
        "BoardGameSceneUpgrade.cs",
        "VRMinePublicNameUpgrade.cs",
        "TrickSeatLifecycle.cs",
        "LatestUploadReadiness.txt",
        "site/index.html",
        ".github/workflows/pages.yml",
    ):
        if item not in project:
            fail(f"PROJECT.md is missing explicit link: {item}")


def check_required_files() -> None:
    for path in (
        "Assets/KafkaMade/VRMine/Scenes/BoardGameShowcase.unity",
        "Assets/KafkaMade/VRMine/Runtime/Game/GameController.cs",
        "Assets/KafkaMade/VRMine/Runtime/Game/OrapaMineGame.cs",
        "Assets/KafkaMade/VRMine/Runtime/Game/ChessGame.cs",
        "Assets/KafkaMade/VRMine/Runtime/Game/TrickSeatLifecycle.cs",
        "Assets/KafkaMade/VRMine/Runtime/Net/NetworkVerificationProbe.cs",
        "Assets/KafkaMade/VRMine/Runtime/UI/BoardView.cs",
        "Assets/KafkaMade/VRMine/Runtime/UI/BoardGameShowcaseView.cs",
        "Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs",
        "Assets/KafkaMade/VRMine/Editor/VRMinePublicNameUpgrade.cs",
        "Assets/KafkaMade/VRMine/Editor/BoardGameShowcaseBuilder.cs",
        "Assets/KafkaMade/VRMine/Editor/BoardGameVerification.cs",
        "Assets/KafkaMade/VRMine/Editor/VRMineReleaseGate.cs",
        "docs/release.md",
        "site/index.html",
        ".github/workflows/project-integrity.yml",
        ".github/workflows/pages.yml",
    ):
        require(path)


def main() -> int:
    check_required_files()
    check_versions()
    check_markdown_links()
    check_rule_matrix()
    check_player_controls()
    check_scene_upgrade_idempotence()
    check_trick_table_and_rule_26()
    check_g3_run_isolation()
    check_release_gate()
    check_public_site_and_claims()
    check_project_index()

    if ERRORS:
        print("VRMine project integrity: FAIL", file=sys.stderr)
        for error in ERRORS:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("VRMine project integrity: PASS")
    print("Static checks passed. Unity/VRChat G0-G4 still require the target environment.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
