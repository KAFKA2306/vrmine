---
type: "mission"
description: "説明文なしで入室10秒で触って分かり、Quest単体でも手触りと空気感に驚く「応答する物理おもちゃ箱」VRChat公開ワールドをPCとQuest双方の実機へ届ける。"
tags:
  - vrmine/mission
  - vrmine/vrchat-world
  - vrmine/tactile-playbox
  - vrmine/quest-first
aliases:
  - "ミッション"
  - "VRMineのミッション"
generated:
  by: "antigravity/gemini-4-neon"
  at: "2026-11-20T00:32:00Z"
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

VRMineは、**「触れてほどける、応答する物理おもちゃ箱」**をコンセプトに、PCとQuest（Android）双方のVRChat実クライアントで体験できる公開ワールドを継続的に届ける。[^agent-contract]

## コンセプトと3つの設計原則

既存の「鑑賞だけ」「ゲームだけ」という枠を外し、**感覚の驚き・発見と知的好奇心・会話と盛り上がり・チルと居場所**の4つを、手で直接触れられる空間体験として1つの器に統合する。

1. **説明文ゼロ・入室10秒の発見**: 説明ボードを読ませず、入室して10秒以内に手が伸びて「つまむ・引く・弾く・透かす・叩く」と仕組みがほどける。言語の壁を超えて初対面でもそのまま一緒に遊べる。
2. **Quest-First の手触り品質**: 最初からQuest（Android）単体で滑らかに動き、質感・音・光の手触りに驚く軽量表現として設計し、PC版で演出を上乗せする。
3. **応答する空間**: プレイヤーの操作、滞在人数、滞在時間に応じて、音・光・天候・仕掛けの連鎖がリアルタイムに応答し続ける。1人なら手遊びしながら落ち着ける居場所になり、2人以上なら同時操作の共鳴やハプニングから自然な会話が生まれる。

## 主成果物とフラッグシップ

VRMineの主成果物は、PCとQuest双方の実クライアントで確認済みの**VRChat公開ワールド**である。

- **フラッグシップ第1作「軒下の雨と布と光の庭」**: 軒下で雨音を聴きながら、濡れた布をつまんで引く、水滴を弾く、光を透かすといった手触りの仕掛けを配置する。1人でも静かに過ごせ、複数人で同時に触れると水音と光が連鎖共鳴するQuest-firstの公開ワールドとして設計・実装・検証する。

## 手段・付随物と整理方針

主成果物と手段を混同せず、新コンセプトに合致する主力ラインへ資源を集中する。

- Blender、ComfyUI、3Dアイテム仕様（`config/world-items/`）、ワールド設計仕様（`config/world-design/`）、およびインタラクティブ表現の自動評価は、公開ワールドの手触りと再現性を高めるための制作・検証手段であり、単独では最終成果物にしない。
- GitHub Pages（`pages/`）は公開導線と付随コンテンツであり、VRChat公開ワールドの完了を代替しない。
- 新コンセプトの体験と直接つながらない旧試作、重複設定、孤立スクリプトは、利用経路と正本を確認のうえ順次削除・整理する。[^agent-contract]

## 公開完了の条件

ワールド1本を完成と認めるには、対象のexact commit SHAに紐づく実測証拠で次をすべて満たす。[^quality-gates]

1. **リポジトリ契約と正本の整合**: [Taskfile.yml](Taskfile.yml) の必須検証とスタースキーマ正本（[config/star-schema.json](config/star-schema.json)）の照合を通過すること。[^taskfile][^star-schema]
2. **Unity・VRChat SDK検証の通過**: 固定ツールチェーンでのコンパイルエラー0件、Missing Script / Reference 0件、VRChat SDK World Builderのブロッキング検証0件を満たすこと（`release_candidate_gate`）。[^quality-gates]
3. **PC実クライアント確認**: 公開ワールドへPC実クライアントで入室し、説明なしの初見操作、誤入力復旧、リセット再実行、および2クライアント公開状態同期・Late Join復元・Owner切替復旧を確認すること（`product_release_gate`）。[^quality-gates]
4. **Quest（Android）実クライアント確認**: 同じ公開ワールドをQuest（Android）実機クライアントでも入室し、描画・操作・手触り・動作負荷を実測確認すること（`product_release_gate`）。[^quality-gates]

静的チェック、ローカルの`.vrcw`ビルド、スクリーンショット単体、またはブラウザ検証だけでは公開完了と認めない。PC実クライアントのみ確認済みでQuest実機確認が未完了のワールドは、完成とはせず**Quest `UNVERIFIED`**として記録する。[^quality-gates]

## 正本と検証経路

- 作業規約とcanonical path: [AGENTS.md](AGENTS.md)[^agent-contract]
- ゲート定義: [config/quality-gates.json](config/quality-gates.json)[^quality-gates]
- スタースキーマ正本と生成台帳: [config/star-schema.json](config/star-schema.json)・[Docs/AI/UnityProjectContext.md](Docs/AI/UnityProjectContext.md)[^star-schema]
- 実行コマンド: [Taskfile.yml](Taskfile.yml)[^taskfile]

[^agent-contract]: [AGENTS.md](AGENTS.md)
[^quality-gates]: [config/quality-gates.json](config/quality-gates.json)
[^star-schema]: [config/star-schema.json](config/star-schema.json)
[^taskfile]: [Taskfile.yml](Taskfile.yml)
