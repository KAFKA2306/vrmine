# VRChat World 開発環境セットアップ

```bash
# 1. リポジトリをインポート
vrc-get repo import tools/vpm-repos.txt

# 2. パッケージを解決
vpm resolve project .

# 3. Unityでプロジェクトを生成
unity-editor -projectPath . -batchmode -quit
```