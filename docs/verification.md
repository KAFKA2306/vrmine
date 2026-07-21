# VRMine Verification System

アップロード前にG0からG4を順番に通す。MCP呼び出し成功、生成シーン、スクリーンショットだけではゲーム動作の証明にしない。

## ゲート

### G0: 環境

- VCCで対象プロジェクトを開く。
- Unity 2022.3.22f1、Worlds SDK、UdonSharp、MCP接続を記録する。
- Consoleのコンパイルエラーを0にする。

### G1: Editor構造

`task verify:games`を実行する。

- `BoardGameShowcase.unity`
- `VRCSceneDescriptor`、spawn、reference camera
- 3ゲームのmanager
- 130以上の操作対象
- 全UdonSharp behaviourのprogram asset
- 同期配列容量

証跡は`Assets/KafkaMade/VRMine/Verification/LatestBoardGamesVerification.txt`へ出力する。

### G2: デスクトップ規則

`task verify:games:runtime`を実行する。

- トリックマイスターの配札・切り札・合法手・得点表明
- オラパ・マインの反射・色・吸収表明
- チェスの合法手・特殊手・メイト・ステイルメイト表明
- Edit Modeへ自動復帰
- Consoleエラー0

証跡は`Assets/KafkaMade/VRMine/Verification/LatestBoardGamesRuntimeVerification.txt`へ出力する。自己テストに含まれないルールの完全性は証明しない。

### G3: VRChat 2クライアント

`task verify:vrc:two-client`を実行する。

1. SDKとVRChatクライアント実行ファイルをpreflightする。
2. 2クライアントを起動する。
3. 各ゲームでclient Aの操作をclient Bが同じ値として観測する。
4. 2番目を遅延起動してlate join復元を確認する。
5. ownerを終了し、owner handoff後の更新を確認する。
6. `.vrcw`、client log、player ID、owner、同期値を報告へ保存する。

ClientSimはG3の代替にならない。

### G4: アップロード準備

- G0–G3がすべてPASS。
- SDK validation、Windows build target、descriptor、spawn、layers、collision matrixがPASS。
- 顧客公開する名称・ルール文・アートの権利が確認済み。
- 公開アップロードは別途明示的な許可を得る。

## 2026-07-20の実測

- G0: PASS。Unity 2022.3.22f1、MCP for Unity 10.1.0、Console clean。
- G1: PASS。3 manager、140 interactions、Udon program 145/145。
- G2: PASS。3規則ゲートのfailures 0。
- G3: FAIL。Worlds SDK 3.9.0が`C:\Program Files (x86)\Steam\steamapps\common\VRChat\VRChat.exe`を要求するが、このPCの2026年版VRChatは`D:\SteamLibrary\steamapps\common\VRChat\launch.exe`構成。`.vrcw`生成後の起動で`Win32Exception: アプリケーションが見つかりません`を再現した。SDK更新はVCC resolveが必要。
- G4: BLOCKED。G3とルール完全性・権利確認が未完了。
