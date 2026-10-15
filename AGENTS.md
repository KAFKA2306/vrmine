# VRMine Agent Contract

current GitHub `main` is the integration base. Machine-readable owners define policy, implementation defines behavior for an exact revision, and CI / production are evidence only for the exact revision they tested or deployed.

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
- Agent knowledge authority: `config/agent-knowledge.json`

## Rules

- one responsibility, one implementation, one config, one verification path.
- `DELETE > MERGE > REPLACE > ADD`.
- superseded code、docs、scripts、workflowsは残さない。
- documentationは現在の実装だけを書く。日付、進捗、Issue履歴、変更履歴、差分説明、旧仕様の注釈を正本へ残さない。
- machine-readableに表現できる状態はproseへ重複させない。
- ドキュメントと台帳は、スタースキーマ（唯一の正）から生成する。生成物は手で直さず、スキーマを直す。生成ビューに事実がなければ、スキーマに足す。
- Public Pagesはproduct surfaceとし、engineering status dashboardにしない。内部進捗、CI/release gate、Issue/PR識別子、repository構造、machine-readable stateは、ユーザー操作や安全に直接必要な場合を除き公開説明文へ重複させない。
- silent fallbackや根拠のないdefaultで失敗を隠さない。
- Unityのserialized referenceとtracked `.meta` を意図せず変更しない。

## Knowledge selection

`config/agent-knowledge.json` owns knowledge precedence and version-source selection. Resolve the current project versions from their canonical files before using version-sensitive Unity, VRChat SDK, Blender, official documentation, or specialized Skills. Project facts outrank external guidance; specialized Skills and general knowledge are advisory only. Reject or explicitly mark guidance incompatible when its declared minimum or exact version does not match the pinned project state. Do not copy pinned version values into this prose.

Use `node scripts/resolve-agent-knowledge.mjs` to read the current project state. For version-sensitive guidance, pass `--tool`, plus `--minimum-version` or `--exact-version`, before applying it. Generated behavior still requires the existing smallest-surface verifier and exact-revision evidence.

## Generated assets

Use the existing generation path and preserve the direct render evidence it produces. Merge/release behavior for generated assets is owned by `config/quality-gates.json`; do not restate that policy here.

## Verification

Use the smallest `Taskfile.yml` command for the changed surface and the verifier selected by `config/quality-gates.json`. Tie CI, main read-back, production, Unity, and VRChat claims to the exact revision that produced the evidence; never infer an unobserved runtime result.

## Outcome-first task interpretation (Altitude / Depth / Range / Speed)

- **Altitude:** Interpret a task as a verifiable user outcome, not only the named intermediate step. Infer the smallest justified acceptance criteria from the request and current repository state; do not silently expand scope or override explicit instructions.
- **Depth:** Use primary/current evidence and real domain tools and methods. Verify the actual postcondition independently of agent self-report, preferably on the exact changed revision or target runtime.
- **Range:** Combine code, data, tests, runtime/UI, and domain expertise when that materially improves the outcome, while respecting canonical owners and avoiding duplicate systems.
- **Speed:** Complete authorized read-only and reversible work through implementation, focused checks, and read-back without unnecessary approval loops. Bound retries and stop at the proven outcome.
- These principles never bypass repository-specific security, privacy, permission, publication, spending, destructive-action, or release boundaries. State verified results and any remaining `UNVERIFIED` layers.
