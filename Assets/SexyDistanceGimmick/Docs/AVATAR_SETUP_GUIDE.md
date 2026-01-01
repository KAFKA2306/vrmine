# アバター流用ガイド

SexyDistanceGimmick のゴースト（見た目）を、お手持ちのアバターに変更する手順です。

---

## 共通要件

* **Animator Controller**: 必須（同梱の `Ghost.controller` を使用）
* **Root Motion**: OFF に設定
* **Rig**: Humanoid 推奨（Generic でも可だがセットアップが複雑）
* **PhysBone / IK**: 不要（削除推奨）

---

## ケース A: Humanoid アバターの場合（推奨）

1. **Prefab の展開**
   Hierarchy 上の `SexyDistanceGimmick` を右クリック → `Unpack Prefab`

2. **モデルの差し替え**
   `GhostRoot/GhostVisual` の下にある既存モデルを削除し、
   あなたのアバター（FBX または Prefab）を配置します。

3. **Animator の設定**
   配置したアバターの `Animator` コンポーネントにて：
   * `Controller` に `Ghost.controller` をアサイン
   * `Apply Root Motion` のチェックを外す

4. **接触点の確認**
   Tポーズのアバターであれば、標準の接触点（`GhostTouch_***`）が大まかに合うはずです。
   位置がずれている場合は、Scene ビューで各 `GhostTouch_***` オブジェクトを移動させて調整してください。

---

## ケース B: 非 Humanoid / 特殊形状アバターの場合

1. **モデルの交換**
   `GhostVisual` 以下にモデルを配置（Humanoid 手順と同様）。

2. **接触点の完全手動配置**
   標準のボーン位置と合わないため、全ての検出用コライダーを手動で配置する必要があります。
   
   * `GhostTouch_Chest`: 胸部
   * `GhostTouch_Neck`: 首筋
   * `GhostTouch_Ear_L / R`: 耳元
   * `GhostTouch_Waist`: 腰回り
   * `GhostTouch_Thigh`: 太腿

   **ヒント**: 
   各 `GhostTouch` オブジェクトには Sphere Collider が付いています。
   ギズモを表示し、アバターの表面から 5cm ほど浮かせた位置に配置すると感度が良くなります。

3. **前傾アニメーションの作成**
   同梱の `Lean_***.anim` は Humanoid 用です。
   非 Humanoid の場合、前傾姿勢（Lean）のアニメーションを新規作成し、
   Animator Controller の `Blend Tree` に再登録する必要があります。

---

## 注意事項

* 本ギミックは「ゴーストが動く」のではなく「GhostRoot が動く」仕組みです。
* アバター直下の移動コンポーネントやスクリプトはすべて無効化してください。
