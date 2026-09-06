# VRMine Agent Contract

current GitHub `main`、このファイル、実装、machine-readable config、current CI、productionを正本とする。

## Canonical paths

- Unity / VRChat: `Assets/KafkaMade/VRMine/`
- Pages: `pages/`
- 3D item specs: `config/world-items/`
- Unity packages: `Packages/`
- Unity version: `ProjectSettings/ProjectVersion.txt`
- Config: `config/`
- Automation / verification: `scripts/`
- Commands: `Taskfile.yml`
- Release policy: `config/quality-gates.json`

## Rules

- one responsibility, one implementation, one config, one verification path.
- `DELETE > MERGE > REPLACE > ADD`.
- superseded code、docs、scripts、workflowsは残さない。
- documentationは現在の実装だけを書く。日付、進捗、Issue履歴、変更履歴、差分説明、旧仕様の注釈を正本へ残さない。
- machine-readableに表現できる状態はproseへ重複させない。
- silent fallbackや根拠のないdefaultで失敗を隠さない。
- Unityのserialized referenceとtracked `.meta` を意図せず変更しない。

## Generated assets

生成開始後は、生成、実ファイル検証、多面render、PR/Issue掲載、merge、main read-back、Pages公開確認まで進める。見た目の評価はmerge blockerにしない。生成物が構造的に不正な場合はfail loudlyとする。

## Verification

変更surfaceに対応するexact-head checkを実行する。repository全体の入口は `task check`、Pagesは `task pages:test` とする。merge後はexact `main` を再取得する。Pages変更はproduction URLを直接確認する。未実行のUnity / VRChat runtimeは `UNVERIFIED` とする。
