# Board Game Lab: Long-Term Roadmap & Directives

## Long-Term Critical Directives
*   **「動く」より「壊れても再構築可能」を優先** (Prioritize "reconstructible after breaking" over "just works").
*   **Object同期ではなくState同期** (State sync, not Object sync).
*   **Transformを真実にしない** (Transform is never the source of truth).
*   **Pieceにルールを書かない** (No game rules inside individual pieces).
*   **Human操作を減らしAgent操作を増やす** (Reduce human operation, increase agent automation).
*   **すべてをログ化** (Log everything).
*   **すべてを再現可能化** (Make everything reproducible).
*   **すべてを孤立テスト可能化** (Make everything isolatable for testing).
*   **「あとから自動生成できる構造」を先に作る** (Build structures that can be auto-generated later).
*   **特定ゲーム実装より先に汎用runtimeを育てる** (Grow universal runtime before specific game implementations).
*   **VRChat制約を前提に設計する** (Design explicitly around VRChat constraints).
*   **「Late Joinで壊れない」を最上位品質に置く** (Late Join stability is the highest quality metric).
*   **手作業Prefab運用を最終的に消す** (Eventually eliminate manual prefab workflows).
*   **Manager Authority崩壊を絶対禁止** (Absolute prohibition of Manager Authority collapse).
*   **Sync Spamを絶対禁止** (Absolute prohibition of Sync Spam).
*   **「エージェントが読める構造」を人間可読性より優先する** (Agent readability > Human readability).

---

## Phase 0: Foundation
*   Networking原則固定
*   Manager Authority固定
*   Object=Visual Only固定
*   State Reconstruction固定
*   Failure Pattern DB蓄積
*   自律修復ログ基盤作成
*   ADR運用継続
*   「なぜ壊れたか」を毎回構造化保存

## Phase 1: Primitive Systems
*   Sync Button
*   Dice
*   Card
*   Grid
*   Turn
*   Timer
*   Score
*   Ownership Queue
*   Interaction Lock
*   Event Bus
*   RPC Wrapper
*   Sync Debug Overlay
*   State Snapshot System

## Phase 2: Board Runtime
*   Generic Board Manager
*   Slot System
*   Piece Registry
*   Rule Engine
*   Action Validation
*   Replayable Action Log
*   Deterministic Turn Replay
*   Late Join Full Reconstruction
*   Master Migration Recovery
*   Undo/Redo
*   Save/Load

## Phase 3: Content Pipeline
*   Board定義JSON化
*   Card定義JSON化
*   Dice定義JSON化
*   Rule DSL化
*   自動Prefab生成
*   自動Scene生成
*   自動Material生成
*   自動Collider設定
*   Addressables整理
*   Asset Validation Bot

## Phase 4: Agent Runtime
*   AI Agent Worker
*   Scene Analyzer
*   Hierarchy理解
*   Prefab差分理解
*   自動Repair
*   Missing Reference修復
*   Collider自動修復
*   Networking Validation
*   Sync Stress Test
*   Build Pipeline Automation
*   Human Approval Gate

## Phase 5: Multiplayer Reliability
*   Lag Simulation
*   Packet Loss Simulation
*   Ownership Storm Test
*   Join/Leave Spam Test
*   Serialization Budget Monitor
*   Sync Heatmap
*   Rollback Recovery
*   Ghost Object Detection
*   Desync Detector
*   State Hash Compare

## Phase 6: Universal BoardGame Runtime
*   Chess
*   Mahjong
*   UNO
*   Poker
*   Catan
*   TRPG
*   Trading Card Game
*   Worker Placement
*   Deck Builder
*   Tile Placement
*   Real-time Hybrid

## Phase 7: Creator Platform
*   ノーコードBoard Editor
*   Rule Visual Scripting
*   In-VR Editing
*   AI Rule Generation
*   AI Board Generation
*   Auto Balancing
*   Auto Playtest
*   Replay Analytics
*   Community Package System
*   Marketplace

## Phase 8: Self-Evolving Lab
*   エージェントがIssue発見
*   エージェントが再現Scene生成
*   エージェントがPatch提案
*   エージェントがRegression Test生成
*   エージェントがArchitecture更新
*   Failure Pattern自動抽出
*   自動Knowledge Graph化
*   Runtime Telemetry学習
*   「壊れにくい設計」へ自己収束
