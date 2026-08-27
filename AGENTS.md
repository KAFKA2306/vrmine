# VRMine 作業ルール

このファイルを、VRMineで作業するエージェント向けルールの正準とする。
同じ内容を別の `SKILL.md` や別の指示ファイルへ複製しない。

## 1. まず確認するもの

作業を始める前に、次を確認する。

- 現在の `main`
- 関係するIssueとPull Request
- 現在のCI結果
- 実際に使われている設定ファイル、scene、script
- すでに同じ目的を持つ実装や作業ブランチがないか

READMEやIssue本文だけを見て、現在の実装状態を決めつけない。
同じ目的のIssueや実装がすでにある場合は、それを続ける。

## 2. ディレクトリの役割

- `Assets/`: Unity / VRChatのassetとcode
- `Assets/KafkaMade/VRMine/`: VRMineが管理するUnity code
- `pages/`: GitHub Pagesで公開するブラウザ画面
- `Packages/`: Unity / VPM package設定
- `ProjectSettings/ProjectVersion.txt`: Unity versionの正準
- `config/`: 機械が読む正準設定
- `scripts/`: 検証や自動処理
- `Taskfile.yml`: 人とCIが使う操作入口

`Library/`、downloadしたpackage、生成した一時report、実行時のartifactをGitへ追加しない。
検証結果はGitHub Actionsのsummaryやartifactへ残す。

## 3. 検証は5段階に分ける

ブラウザで動いたことと、UnityやVRChatで動いたことを混同しない。

- **U1**: package、設定、静的な契約の検証
- **U2**: 指定UnityでのcompileとEditMode検証
- **U3**: PlayModeとClientSimで確認できるローカル挙動
- **U4**: 実際のVRChat clientでのBuild & Test、複数client確認
- **U5**: uploadしたprivate/public worldでの最終確認

低い段階の成功を、高い段階の成功として報告しない。

特にClientSimだけでは、次を証明できない。

- 実client間の同期
- ownership移行
- late join
- owner離脱後の挙動
- PCとAndroidの同等性
- upload後のworld挙動

実行していない検証は `PASS` にしない。

## 4. 目的と完了条件を先に決める

大きな作業では、編集前に最低限次を決める。

- 何を完成させるか
- 何を変更してよいか
- 何を変更してはいけないか
- どの検証段階まで必要か
- 何を満たせば完了か

作業中に別の目的を見つけても、現在の目的に不要なら同じ変更へ混ぜない。

## 5. 実装は1責務1経路にする

同じ責務の実装、設定、状態管理、検証経路を並立させない。

- 既存の正準実装を先に再利用する
- 新しいframeworkやdependencyは、既存経路へ入れられない理由がある場合だけ追加する
- 新経路へ置き換えたら、不要になった旧経路を同じ作業で削除する
- 履歴保存のためにdead codeを残さない。履歴はGitにある
- 不正な状態をもっともらしいdefault値へ変換して成功扱いしない
- retryやrecoveryは、できるだけworkflowや実行側へ置く

repository固有の作業ルールはこの `AGENTS.md` に置く。
現在、repo内Skillを必須の実行経路にはしない。

## 6. 操作入口はTaskfileへ集約する

通常の入口は次とする。

```text
task setup
task check
```

主な個別入口:

```text
task vpm:check
task release:perspective-cage:u2
task gaussian:open
task gaussian:verify-u2
task gaussian:verify-sdk
task pages:test
```

同じ目的のshell、PowerShell、npm、独自wrapperを増やさない。
Unity自動実行は `ProjectSettings/ProjectVersion.txt` の指定versionを使う。

## 7. CI/CDは必須

変更を手元で確認しただけでは完了にしない。

### Pull Request

- 変更はPull Requestで確認する
- 関係するGitHub Actionsを実行する
- `task check` 相当のrepository検証を通す
- 変更箇所専用の検証がある場合はそれも通す
- CIが失敗した状態ではmergeしない

### mainへ反映した後

- exact `main` commitに対するCI結果を確認する
- Pagesを変更した場合はPagesのdeploy成功を確認する
- Pagesを変更した場合は公開URLへ実際にアクセスして確認する

CI成功だけでUnity / VRChat実機成功とはみなさない。
Unity / VRChat挙動を変更した場合は、その主張に必要なU2〜U5の証拠も別に確認する。

## 8. Pagesの扱い

公開Pagesの入口はREADMEへURLそのものが見える形で書く。
公開ページを追加・削除した場合はREADMEも同じ変更で直す。

ブラウザの検証結果をUnity / VRChatの検証結果として使わない。

## 9. Unity / VRChat変更の注意

- C# / UdonSharpはUTF-8、4-space indentを基本とする
- serialized referenceとGUIDを壊さない
- ownership、serialization、synced state、late joinを暗黙にしない
- SDK更新はversion番号だけ変更して完了にしない
- SDK更新後に必要なUnity / SDK / client検証を行う
- third-party Unity MCPを必須CIやrelease依存にしない
- public repositoryのPR codeへcredential付きWindows実行を直接つながない

## 10. 証拠の扱い

重要な主張は次のどれかとして扱う。

- **確認済み**: 必要な検証を実際に実行して確認した
- **観察**: 人が実際に見た結果
- **推定**: 証拠から推測したもの
- **未確認**: まだ確認していない

未確認を確認済みとして書かない。
screenshotだけでnetwork同期を証明したことにしない。

## 11. 作業を途中で止める場合

IssueまたはPull Requestへ次を残す。

- 最後に確認したcommit
- 完成させる目的
- どこまで確認できたか
- 何が失敗または不足しているか
- 次に実行する具体的な操作

エージェント用の第二の状態databaseは作らない。
Git、Issue、Pull Request、CI artifactを正準とする。

## 12. 完了条件

次をすべて満たしたときに完了とする。

- 求められた結果が実装されている
- 必要な段階の検証を実行している
- Pull RequestのCIを確認している
- merge後のexact `main` CIを確認している
- Pages変更ではdeployと公開URLを確認している
- 不要になった旧実装、一時file、重複経路を残していない
- Issue / Pull Requestの状態が実際の結果と一致している

完了後に別の改善案が出ても、現在の目的に不要ならそこで止める。
