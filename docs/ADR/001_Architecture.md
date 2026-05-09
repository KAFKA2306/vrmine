# ADR001: VRMine Architecture

## Status
Accepted

## Goal
VRChat内で、波を撃って盤面を推理し、完全一致宣言で勝利する `VRMine` を安定して遊べる形で構築する。
`GameController` を中心に、`PlayerClient`、`WaveSimulator`、`BoardState`、`LogStream`、`LogBoard` を分離し、Scene と Prefab だけで再構築できる構造を確立する。

## Strategy
1. **State first**: 盤面の真実は `BoardState` の同期値に置く。
2. **Manager authority**: 判定と進行は `GameController` に集約する。
3. **Visual split**: 表示は `LogBoard` と UI に閉じ、ロジックを持たせない。
4. **Bootstrap independent**: Scene と Prefab を直接保存しても同じ構成になるようにする。
5. **Minimal surface**: 1 つの役割に 1 つのコンポーネントを対応させる。

## Architecture Patterns

### 1. Controller-State-View
- **Controller**: `GameController` が宣言、進行、勝敗を扱う。
- **State**: `BoardState` が盤面の同期状態を持つ。
- **View**: `LogStream` と `LogBoard` がログと UI を表示する。
- **Client**: `PlayerClient` が操作の入口になる。

### 2. Networking Policy
- **Sync Mode**: `BehaviourSyncMode.Manual` を使う。
- **State Change**: `[UdonSynced]` の更新を `FieldChangeCallback` で表示へ反映する。
- **Ownership**: 盤面状態の所有権は 1 か所に寄せる。

## Implementation Details

### Current Core
- `GameController`: 宣言、勝敗、進行の入口。
- `PlayerClient`: ローカル操作の入口。
- `WaveSimulator`: 波の生成と反射。
- `BoardState`: 盤面の同期状態。
- `LogStream`: ログの流れ。
- `LogBoard`: ログ表示。

### Scene/Prefab Rule
- `MVP.unity` と `LogCanvas.prefab` は bootstrap に依存しない。
- 参照は Inspector に保存され、再読み込み後も維持される。
- ボタンの `OnClick` は永続リスナーで結線する。

## What NOT to copy (Lessons from Vowgan)
- **Transformを真実にしない**: グリッド座標と同期状態を優先する。
- **UIに判定を書かない**: 表示は表示だけにする。
- **所有権を増やしすぎない**: 頻繁な Ownership 変更を避ける。

## Success Criteria
- 2人以上で同じ盤面とログを再現できる。
- Late Joiner が正しい状態を即座に確認できる。
- 完全一致宣言の成否が `GameController` で一貫して判定される。
- Scene と Prefab を開き直しても参照が壊れない。
