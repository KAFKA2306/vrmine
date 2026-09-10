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

## Rules

- one responsibility, one implementation, one config, one verification path.
- `DELETE > MERGE > REPLACE > ADD`.
- superseded code、docs、scripts、workflowsは残さない。
- documentationは現在の実装だけを書く。日付、進捗、Issue履歴、変更履歴、差分説明、旧仕様の注釈を正本へ残さない。
- machine-readableに表現できる状態はproseへ重複させない。
- Public Pagesはproduct surfaceとし、engineering status dashboardにしない。内部進捗、CI/release gate、Issue/PR識別子、repository構造、machine-readable stateは、ユーザー操作や安全に直接必要な場合を除き公開説明文へ重複させない。
- silent fallbackや根拠のないdefaultで失敗を隠さない。
- Unityのserialized referenceとtracked `.meta` を意図せず変更しない。

## Generated assets

Use the existing generation path and preserve the direct render evidence it produces. Merge/release behavior for generated assets is owned by `config/quality-gates.json`; do not restate that policy here.

## Verification

Use the smallest `Taskfile.yml` command for the changed surface and the verifier selected by `config/quality-gates.json`. Tie CI, main read-back, production, Unity, and VRChat claims to the exact revision that produced the evidence; never infer an unobserved runtime result.
