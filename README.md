# VRMine: Stich-Meister

「ルールを奪い合う、手仕事感あふれるトリックテイキング・ボードゲーム」

## プロジェクト概要
本作は、ボードゲーム「Stich-Meister（スティッヒマイスター）」を VRChat 上で再現した、物理コンポーネント主体の VR ボードゲーム空間です。
プレイヤーは毎ラウンド、カードを出すことで「切り札」「トリックルール」「得点ルール」を物理的に書き換え、目まぐるしく変化するゲーム展開を読み合います。

## 技術スタック
- **VRChat SDK3**: UdonSharp 1.1.x
- **UdonSharp**: 物理演算と同期ロジック
- **Handcrafted AI Assets**: 手描き風の質感を持つ SF 静寂（Quiet UI）デザイン

## アーキテクチャ
本プロジェクトは、ビジュアルとロジックを分離・統合する独自のパイプラインを採用しています。
1. **VisualBuilder.cs**: 盤面、カード、宝石、ポスター等の物理環境を動的に構築。
2. **VRMineBridge.cs**: 生成された物理オブジェクトを UdonSharp コンポーネントへ自動配線。
3. **BoardState / GameController**: ビットパックされたデータによる高効率なネットワーク同期。

## 開発フロー
Unity メニューの以下を実行することで、即座に最新の環境が整います。
- **VRMine > build_visuals**: 物理アセットの再生成。
- **VRMine > wire_scene**: アセット生成とロジック配線の実行。

## ドキュメント
- [Rulebook](docs/Rulebook.md): ゲームルールの詳細。
- [STATE](docs/STATE.md): データ同期構造の定義。
- [ARCHITECTURE_RULES](docs/ARCHITECTURE_RULES.md): 設計ガイドライン。
