# Quality Gates

VRMineでは **PR merge条件** と **製品release条件** を別の判定として扱います。

## 1. PR Merge Gate

目的は「この差分を `main` に統合してよいか」を判定することです。

必須:

- 実装scopeがPR内で完結している
- machine-readable contract / static testがPASS
- Repository U1がPASS
- 変更対象に対応する高速testがPASS
- PRがmergeable

PR mergeに **不要**:

- visual reviewのPASS
- manual approval
- Draft解除待ち
- unresolved reviewの解消待ち
- Unity Editor実行
- SDK Builder実行
- actual VRChat client実行
- 2-client/late-join実測
- release時性能測定

これらを理由にMerge GateをPASSしたPRを保留しません。特に生成物は、見た目をユーザーへ提示することを優先し、visual reviewをmerge authorityにしません。生成開始後は多面レンダリングをPR・Issue・コメントへ掲載し、そのまま機械的にmergeまで進めます。

ただし、変更内容そのものに既知のcompile error、破壊的schema drift、明確なruntime defectがある場合は「release evidence不足」ではなく「変更品質の既知不良」なのでmerge blockerです。

## 2. Release Candidate Gate

対象はPRではなく **exact `main` commit** です。

必須:

- exact main commit SHAを固定
- exact Unity / VRChat SDK toolchainを固定
- Unity compile PASS
- canonical scene integrity PASS
- SDK Builder blocking validation = 0

ここまで通ったcommitだけをactual-client検証へ進めます。

## 3. Product Release Gate

「完成World」「release可能」「actual VRChatで検証済み」と表現する条件です。

必須:

- Release Candidate Gate PASS
- actual clientで1人通しclear
- wrong-input recovery
- reset → replay
- 2-client public state sync
- late join reconstruction
- owner transition recovery
- actual playthrough duration記録
- evidenceがexact commit / toolchain / sceneへ紐付く

Quest/Androidや未測定性能は、別の実測証拠がない限りUNVERIFIEDのままです。

## Issue lifecycle

### Implementation issue

実装IssueはMerge Gateで完了できます。

例:

- builder実装
- UdonSharp runtime実装
- static validator実装
- verification entrypoint実装

実機release証拠をimplementation issueのclose条件へ混ぜません。

### Release verification issue

Unity / SDK / actual-client証拠は専用release issueへ集約します。

Perspective Cageでは `#145` がこの責務を持ちます。Product Release Gateを満たすまでcloseしません。

### Product epic

製品Epicは実装PRが全部mergeされても自動closeしません。Product Release Gateを満たした時点でのみ「完成」と判定します。

## Evidence promotion rule

```text
U1 static / repository checks
    ↓
merge可能性を証明

U2 Unity / canonical scene
    ↓
release candidate可能性を証明

SDK Builder + U4 actual VRChat client
    ↓
product release可能性を証明
```

下位gateのPASSを上位gateのPASSへ読み替えません。

## Canonical authority

machine-readable authorityは `config/quality-gates.json` です。

```bash
node scripts/verify-quality-gates.mjs
```

このvalidatorは、actual-client等のrelease-only証拠がPR merge必須条件へ逆流していないことと、release条件が弱体化していないことを検査します。
