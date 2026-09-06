https://kafka2306.github.io/vrmine/
https://kafka2306.github.io/vrmine/io/

# VRMine

VRChat向けコンテンツ、3Dワールド素材、ブラウザゲームを同じrepositoryで管理します。

## Repository

- `pages/`: GitHub Pages
- `pages/io/`: 3D素材カタログ
- `config/world-items/`: 3D素材仕様
- `Assets/KafkaMade/VRMine/`: Unity / VRChat実装
- `Packages/`: Unity package設定
- `ProjectSettings/ProjectVersion.txt`: Unity version
- `config/`: machine-readable設定
- `scripts/`: 生成・検証
- `Taskfile.yml`: 実行入口

## Commands

```bash
task setup
task check
task pages:test
```

個別の実行入口は `Taskfile.yml` を参照してください。

## Verification

変更はPull Requestで検証してからmergeします。Pages変更は公開後のURLを直接確認します。Unity / VRChatの状態は、実際に確認した実行環境の結果だけを扱います。release条件は `config/quality-gates.json` を正本とします。
