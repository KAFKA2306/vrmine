#!/usr/bin/env python3
"""Static integrity checks for VRMine.

This intentionally does not claim Unity or VRChat runtime verification. It checks
that the repository is internally coherent before the platform-specific gates run.
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
    project_version = read("ProjectSettings/ProjectVersion.txt")
    if "m_EditorVersion: 2022.3.22f1" not in project_version:
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
    if "26 | **未実装" not in text:
        fail("rule 26 must remain explicitly marked unimplemented until code and tests exist")


def check_player_controls() -> None:
    action = read("Assets/KafkaMade/VRMine/Runtime/UI/BoardGameAction.cs")
    scene_upgrade = read("Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs")
    orapa = read("Assets/KafkaMade/VRMine/Runtime/Game/OrapaMineGame.cs")
    lifecycle = read("Assets/KafkaMade/VRMine/Runtime/Game/TrickSeatLifecycle.cs")

    for fragment in ("action == 5", "ConfigurePlayers(value)", "OwnTrickState"):
        if fragment not in action:
            fail(f"Trick player-count action is missing: {fragment}")
    for fragment in ("action == 8", "orapaGame.ConfigurePlayers(value)"):
        if fragment not in action:
            fail(f"Orapa player-count action is missing: {fragment}")
    for control in (
        "TrickPlayerCount_3",
        "TrickPlayerCount_4",
        "TrickPlayerCount_5",
        "OrapaPlayerCount_2",
        "OrapaPlayerCount_3",
        "OrapaPlayerCount_4",
        "OrapaPlayerCount_5",
    ):
        if control not in scene_upgrade:
            fail(f"generated scene upgrade is missing control: {control}")
    for fragment in ("NextActiveSeat", "OnPlayerLeft", "attempts[seat] < 2"):
        if fragment not in orapa:
            fail(f"Orapa active-seat lifecycle is missing: {fragment}")
    for fragment in ("OnPlayerLeft", "occupiedPlayerIds[seat] = 0", "local.isMaster"):
        if fragment not in lifecycle:
            fail(f"Trick seat lifecycle is missing: {fragment}")


def check_verification_fail_closed() -> None:
    verification = read("Assets/KafkaMade/VRMine/Editor/BoardGameVerification.cs")
    required_fragments = (
        "Build And Test Two Clients",
        "Finalize Two Client Logs",
        "DistinctClients",
        'CheckGameEvidence(report, "TRICK"',
        'CheckGameEvidence(report, "ORAPA"',
        'CheckGameEvidence(report, "CHESS"',
        "RESTORED_AFTER_TEST",
    )
    for fragment in required_fragments:
        if fragment not in verification:
            fail(f"two-client verification is missing: {fragment}")
    if 'await builder.BuildAndTest();\n            WriteReport(VrcReportPath, "PASS' in verification:
        fail("BuildAndTest must not write PASS without parsing client evidence")

    release_gate = read("Assets/KafkaMade/VRMine/Editor/VRMineReleaseGate.cs")
    for report_name in ("G1Structure", "G2RuntimeRules", "G3TwoClientNetwork"):
        if report_name not in release_gate:
            fail(f"upload readiness gate is missing {report_name}")
    for release_requirement in ("TrickSeatLifecycle", "TrickPlayerCountControls", "OrapaPlayerCountControls"):
        if release_requirement not in release_gate:
            fail(f"upload readiness gate is missing {release_requirement}")

    g1 = read("Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt")
    g2 = read("Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt")
    g3 = read("Assets/KafkaMade/VRMine/Verification/LatestVRChatBuildAndTest.txt")
    g4 = read("Assets/KafkaMade/VRMine/Verification/LatestUploadReadiness.txt")
    if "Result: PASS" in g4 and not all("Result: PASS" in value for value in (g1, g2, g3)):
        fail("G4 says PASS while one or more prerequisite reports are not PASS")


def check_project_index() -> None:
    project = read("PROJECT.md")
    required = (
        "docs/games/trick-meister.md",
        "docs/games/orapa-mine.md",
        "docs/games/chess.md",
        "BoardGameShowcase.unity",
        "BoardGameSceneUpgrade.cs",
        "TrickSeatLifecycle.cs",
        "LatestUploadReadiness.txt",
        "site/index.html",
        ".github/workflows/pages.yml",
    )
    for item in required:
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
        "Assets/KafkaMade/VRMine/Editor/BoardGameSceneUpgrade.cs",
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
    check_verification_fail_closed()
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
