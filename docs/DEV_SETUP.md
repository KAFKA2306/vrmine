# VRChat World 開発環境セットアップ

## 前提条件
- Unity 2022.3.22f1 が `~/Unity/Hub/Editor/2022.3.22f1/Editor/Unity` にインストール済み
- `~/.local/bin` が PATH に含まれている
- .NET 8 SDK がインストール済み

## 初回セットアップ
1. Unity エディタのシンボリックリンクを作成:
   ```bash
   ln -sf ~/Unity/Hub/Editor/2022.3.22f1/Editor/Unity ~/.local/bin/unity-editor
   ```

2. vrc-get をインストール:
   ```bash
   curl -L https://github.com/vrc-get/vrc-get/releases/download/v1.9.1/x86_64-unknown-linux-musl-vrc-get -o ~/.local/bin/vrc-get
   chmod +x ~/.local/bin/vrc-get
   vrc-get --version
   ```

3. vpm CLI をインストール:
   ```bash
   dotnet tool install --global vrchat.vpm.cli
   vpm --version
   ```

4. VPM リポジトリを登録:
   ```bash
   vrc-get repo add https://vpm.nadena.dev/vpm.json nadena
   vrc-get repo add https://vcc.vrcfury.com VRCFury
   vrc-get repo add https://lilxyzw.github.io/vpm-repos/vpm.json lilxyzw
   mkdir -p tools
   vrc-get repo export > tools/vpm-repos.txt
   ```

## プロジェクト同期
- Unity プロジェクトを配置し、`Packages/manifest.json` と `Packages/vpm-manifest.json` が存在する状態にする
- `vpm resolve project .` でパッケージをインストール
- 別マシンでは `vrc-get repo import tools/vpm-repos.txt` を実行してからリポジトリを解決する

## 動作確認
`unity-editor -projectPath .` で Unity を起動
