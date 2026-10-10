# VRMine Agent Contract

This file governs work in the VRMine repository. User-level instructions provide defaults; repository and deeper-directory instructions take precedence when they are more specific.

The current GitHub `main` branch is the integration base. Machine-readable owners define policy, implementation defines behavior for an exact revision, and CI / production are evidence only for the exact revision they tested or deployed.

## Canonical paths

- Unity / VRChat: `Assets/KafkaMade/VRMine/`
- Pages: `pages/`
- 3D item specs: `config/world-items/`
- Unity packages: `Packages/`
- Unity version: `ProjectSettings/ProjectVersion.txt`
- Config: `config/`
- Automation / verification: `scripts/`
- Commands: `Taskfile.yml`
- Verification / release policy: `config/quality-gates.json`
- Verification worktree lifecycle: `config/verification-worktree-policy.json`
- Agent knowledge authority: `config/agent-knowledge.json`

## Rules

- one responsibility, one implementation, one config, one verification path.
- `DELETE > MERGE > REPLACE > ADD`.
- superseded code、docs、scripts、workflowsは残さない。
- documentationは現在の実装だけを書く。日付、進捗、Issue履歴、変更履歴、差分説明、旧仕様の注釈を正本へ残さない。
- machine-readableに表現できる状態はproseへ重複させない。
- Public Pagesはproduct surfaceとし、engineering status dashboardにしない。内部進捗、CI/release gate、Issue/PR識別子、repository構造、machine-readable stateは、ユーザー操作や安全に直接必要な場合を除き公開説明文へ重複させない。
- silent fallbackや根拠のないdefaultで失敗を隠さない。
- Unityのserialized referenceとtracked `.meta` を意図せず変更しない。
- 作業開始前に既存の変更と対象範囲を確認し、要求と無関係な変更を上書き・削除しない。
- 変更は要求されたsurfaceに限定し、既存の実装・設定・検証経路を再利用する。
- `git reset --hard`、`git clean`、worktreeの削除、再利用中の検証資源の破壊的なcleanupは通常の作業で行わない。

## Knowledge selection

`config/agent-knowledge.json` owns knowledge precedence and version-source selection. Resolve the current project versions from their canonical files before using version-sensitive Unity, VRChat SDK, Blender, official documentation, or specialized Skills. Project facts outrank external guidance; specialized Skills and general knowledge are advisory only. Reject or explicitly mark guidance incompatible when its declared minimum or exact version does not match the pinned project state. Do not copy pinned version values into this prose.

Use `node scripts/resolve-agent-knowledge.mjs` to read the current project state. For version-sensitive guidance, pass `--tool`, plus `--minimum-version` or `--exact-version`, before applying it. Generated behavior still requires the existing smallest-surface verifier and exact-revision evidence.

## Generated assets

Use the existing generation path and preserve the direct render evidence it produces. Merge/release behavior for generated assets is owned by `config/quality-gates.json`; do not restate that policy here.

## Verification

Use the smallest `Taskfile.yml` command for the changed surface and the verifier selected by `config/quality-gates.json`. Tie CI, main read-back, production, Unity, and VRChat claims to the exact revision that produced the evidence; never infer an unobserved runtime result.

Verification retry and worktree recovery must follow `config/verification-worktree-policy.json`. A normal run must not delete, reset, clean, or reuse an existing partial, failed, or stale verification worktree. Allocate a fresh detached worktree for every retry, record stale resources without mutating them, and continue. Destructive cleanup belongs to the separate maintenance path and must not block a normal run or create a human-approval dependency when a fresh isolated run can proceed.
