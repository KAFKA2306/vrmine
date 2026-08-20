# Gaussian Splat artifact storage

VRMineはGaussian Splat PLYのconsumerです。PLY生成やStorage Bucket uploadは担当せず、`KAFKA2306/hf-cache-hub` が検証したartifactをshared local cacheから解決し、Unity用のignored pathへmaterializeします。

## Source modes

`config/gaussian-splats.json` の全20 entryは、現在は次の artifact source を正準経路にしています。

- `artifact_id`: hf-cache-hubのartifact manifestを正準sourceとして解決する方式
- `download_url`: 既存のdirect URLを使うlegacy方式（新規 entryでは使用しない）

`config/gaussian-artifacts.yaml` は、ユーザー確認済みの20件の remote readback（合計 `841,129,810` bytes）と同じサイズ/SHA-256、`k4fka/kafka-data-lake` の hash-addressed pathを記録しています。

## Shared cache setup

```bash
git clone https://github.com/KAFKA2306/hf-cache-hub.git ~/src/hf-cache-hub
export HF_CACHE_HUB_ROOT="$HOME/src/hf-cache-hub"
export HF_HOME="$HOME/hf-cache"
export HF_CACHE_HUB_PYTHON="$HOME/.venvs/hf-cache/bin/python"
```

artifact sourceを含むregistryで次を実行します。

```bash
task gaussian:prepare
```

hf-cache-hub resolverはartifactをSHA-256 content-addressed cacheへ置きます。

```text
$HF_HOME/artifacts/sha256/<sha256>/<filename>
```

VRMineはresolverが返した `READY`、`size_bytes`、SHA-256をregistryと照合し、さらにUnity consumer pathへmaterializeした後にもsize/SHA-256を検証します。

## Local materialization

Unity側のconsumer pathは次です。

```text
Library/VRMine/GaussianSources/<id>.ply
```

`Library/` はGit管理対象ではありません。consumer pathはcacheそのもののauthorityではなく、Unity import用のmaterializationです。同一SHA-256 objectのremote downloadはhf-cache-hub shared cacheで再利用できます。

artifact sourceでは、shared cache objectとconsumer pathが同一filesystem上にある場合はhard linkを優先し、PLY bytesの二重保存を避けます。hard linkを作れないfilesystem境界や環境ではcopyへfallbackします。どちらの経路でもpromotion前後のsize/SHA-256検証は維持します。CLI出力の `artifact_hardlinks` / `artifact_copies` で実際のlocal materialization方式を確認できます。

## Migration rule

1. AutoPhotogrammetry等のproducerがPLYを生成する。
2. hf-cache-hub publisherがStorage Bucket upload + readback + size/SHA-256一致を確認する。
3. artifact recordを正準manifestへ登録する。
4. VRMine registryを `artifact_id` sourceへ切り替える。
5. clean consumerで `task gaussian:prepare` と3DGS contractsを再実行する。
6. remote readbackを確認したartifactについてlegacy `download_url` を削除する。

未観測のremote objectを前提にregistryだけ先行変更しません。GitHub Actionsのfixture PASSはconsumer contractの証拠であり、実Storage Bucket上のPLYが存在する証拠とは区別します。
