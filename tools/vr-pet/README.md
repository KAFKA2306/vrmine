# VRPet

公開仕様だけを参照して作る、LLMなし・ローカル学習のVRChatペット実験です。既存商品のコード、モデル、音声、名称、アセットは使用しません。

## 原理

1. 日本語入力をSudachiで解析し、名詞と読みを抽出する。
2. SQLiteへ `語 / 読み / 出現回数 / 最終観測時刻` を保存する。
3. 発話候補を次式で重み付き抽選する。

\[
w_i=(C_i+\alpha)^\beta \exp(-\lambda \Delta t_i)
\]

4. 読みを最大8文字のカナIDへ量子化する。
5. VRChatのOSC受信ポートへAvatar Parametersを送る。

同期する場合のプロトコルは `PetChar0..7` が各8 bit、`PetMood` が8 bit、`PetSpeak` が1 bitで合計73 bitです。VRChat公式のcustom parameter同期上限256 bit内に収まります。

## セットアップ

```bash
cd tools/vr-pet
python -m venv .venv
.venv/Scripts/pip install -e .
vrpet learn "猫とラーメンの話をした"
vrpet sample
```

VRChat側でOSCを有効化し、`PetChar0..7`、`PetMood`、`PetSpeak` と同名のExpression Parametersを用意します。VRChatのOSC既定受信ポートは9000です。

## マイク入力

Voskはオフライン音声認識を提供し、日本語をサポートしています。

```bash
.venv/Scripts/pip install -e ".[voice]"
vrpet listen --model C:\path\to\vosk-japanese-model
```

音声モデルはリポジトリへ同梱しません。会話DBは既定で `~/.vrmine/vrpet.sqlite3` にのみ保存されます。

## 検証

```bash
python -m unittest discover -s tests
```

CIで検証できるのは語彙DB、確率抽選、カナ量子化、OSCパケット生成までです。実際のVRChatアバター表示、Animator遷移、Modular Avatar統合はUnity/VRChat実機で別途検証が必要です。

## 一次仕様

- VRChat OSC Overview: https://docs.vrchat.com/docs/osc-overview
- VRChat Animator Parameters: https://creators.vrchat.com/avatars/animator-parameters/
- Modular Avatar: https://modular-avatar.nadena.dev/
- Vosk: https://github.com/alphacep/vosk-api

## License

MIT
