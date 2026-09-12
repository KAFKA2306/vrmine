# VRMine

VRChat向けワールド素材、ワールド設計、関連インタラクティブコンテンツを、仕様・生成・検証・配布まで一つのrepositoryで扱います。

- Public site: https://kafka2306.github.io/vrmine/
- 3D item catalog: https://kafka2306.github.io/vrmine/io/

## Start here

- 開発・agentルールとcanonical path: [`AGENTS.md`](AGENTS.md)
- 実行可能なcommand: [`Taskfile.yml`](Taskfile.yml)
- merge / release gate: [`config/quality-gates.json`](config/quality-gates.json)
- 3D item仕様: [`config/world-items/`](config/world-items/)
- World Design仕様・生成spec: [`config/world-design/`](config/world-design/)

## Verification

repository全体の高速な再現可能チェックは次で実行します。

```bash
task check
```

変更面ごとの最小commandと検証条件は `Taskfile.yml` と `config/quality-gates.json` を正本とし、このREADMEには重複して列挙しません。
