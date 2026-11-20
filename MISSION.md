---
type: "mission"
description: "VRChatのPCとQuest双方の実クライアントで体験できる公開ワールドを、再現可能な設計・制作・検証の仕組みで継続的に届ける。"
tags:
  - vrmine/mission
  - vrmine/vrchat-world
  - vrmine/policy
aliases:
  - "ミッション"
  - "VRMineのミッション"
generated:
  by: "antigravity/gemini-4-neon"
  at: "2026-11-20T00:22:00Z"
sources:
  - id: agent-contract
    resource: /AGENTS.md
    title: VRMine Agent Contract
  - id: quality-gates
    resource: /config/quality-gates.json
    title: VRMine quality gates policy
  - id: star-schema
    resource: /config/star-schema.json
    title: VRMine star schema SSOT
  - id: taskfile
    resource: /Taskfile.yml
    title: VRMine Taskfile
---

# ミッション

VRMineは、プレイヤーがVRChatのPCとQuest（Android）双方の実クライアントで入室し、最初から最後まで体験できる公開ワールドを、再現可能な設計・制作・検証の仕組みで継続的に届ける。[^agent-contract]

## 主成果物

VRMineの主成果物は、実クライアントで確認済みの**VRChat公開ワールド**である。次の4系統の体験を扱う。

| 系統 | 届ける体験 | 代表シーン・構成 |
| --- | --- | --- |
| 実写3DGS風景・展示 | 現実の風景や空間を3D Gaussian Splattingで歩いて鑑賞する | `GaussianSplatExhibition`、`MinooRiverOsaka` |
| 居場所・ジオラマ・チル空間 | 滞在・会話・鑑賞を目的とした空間と卓上ジオラマで過ごす | `WoodlandTabletopVillage`、`config/world-design/` |
| 遊べるギミック・パズル・ボードゲーム | UdonSharp同期を備えたパズルや対戦を複数人で遊ぶ | `PerspectiveCage`、`BoardGameShowcase` |
| 表現特化・インタラクティブ演出 | 雨滴や布など、プレイヤーの操作に応じて変化する表現を触って確かめる | インタラクティブ表現評価契約（[AGENTS.md](AGENTS.md)） |

## 手段と付随物

主成果物と手段を混同しない。

- Blender、ComfyUI、3Dアイテム仕様（`config/world-items/`）、ワールド設計仕様（`config/world-design/`）、およびVFX試作は、公開ワールドの品質と再現性を高めるための制作・検証手段であり、単独では最終成果物にしない。
- GitHub Pages（`pages/`、3Dカタログ、ブラウザゲーム）は公開導線および付随コンテンツであり、VRChat公開ワールドの完了を代替しない。

## 公開完了の条件

ワールド1本を完成と認めるには、対象のexact commit SHAに紐づく実測証拠で次をすべて満たす。[^quality-gates]

1. **リポジトリ契約と正本の整合**: [Taskfile.yml](Taskfile.yml) の必須検証とスタースキーマ正本（[config/star-schema.json](config/star-schema.json)）の照合を通過すること。[^taskfile][^star-schema]
2. **Unity・VRChat SDK検証の通過**: 固定ツールチェーンでのコンパイルエラー0件、Missing Script / Reference 0件、VRChat SDK World Builderのブロッキング検証0件を満たすこと（`release_candidate_gate`）。[^quality-gates]
3. **PC実クライアント確認**: VRChat公開ワールドへPC実クライアントで入室し、初期位置からの主要動線、操作、誤入力からの復旧、リセット再実行を確認すること。マルチプレイ要素があるワールドは、2クライアント公開状態同期、Late Join復元、Owner切替復旧まで確認すること（`product_release_gate`）。[^quality-gates]
4. **Quest（Android）実クライアント確認**: 同じ公開ワールドをQuest（Android）実機クライアントでも入室し、描画・操作・動作負荷を含めて体験できることを確認すること。

静的チェック、ローカルの`.vrcw`ビルド、スクリーンショット単体、またはブラウザ検証だけでは公開完了と認めない。PC実クライアントのみ確認済みでQuest実機確認が未完了のワールド（シェーダーや計算制約でQuest適合が未確定の3DGSワールドを含む）は、完成とはせず**Quest `UNVERIFIED`**として記録する。[^quality-gates]

## 正本と検証経路

- 作業規約とcanonical path: [AGENTS.md](AGENTS.md)[^agent-contract]
- ゲート定義: [config/quality-gates.json](config/quality-gates.json)[^quality-gates]
- スタースキーマ正本と生成台帳: [config/star-schema.json](config/star-schema.json)・[Docs/AI/UnityProjectContext.md](Docs/AI/UnityProjectContext.md)[^star-schema]
- 実行コマンド: [Taskfile.yml](Taskfile.yml)[^taskfile]

[^agent-contract]: [AGENTS.md](AGENTS.md)
[^quality-gates]: [config/quality-gates.json](config/quality-gates.json)
[^star-schema]: [config/star-schema.json](config/star-schema.json)
[^taskfile]: [Taskfile.yml](Taskfile.yml)
