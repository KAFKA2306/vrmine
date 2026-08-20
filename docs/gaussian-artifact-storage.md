# Gaussian Splat artifact storage

VRMineはGaussian Splat PLYのconsumerです。PLY生成やStorage Bucket uploadは担当せず、`KAFKA2306/hf-cache-hub` が検証したartifactをshared local cacheから解決し、Unity用のignored pathへmaterializeします。

## Source modes

`config/gaussian-splats.json` の各entryは、移行期間中は次のいずれかをsourceにできます。

- `artifact_id`: hf-cache-hubのartifact manifestを正準sourceとして解決する方式
- `download_url`: 既存のdirect URLを使うlegacy方式

`artifact_id` がある場合はartifact方式を優先します。remote publish/readbackが実証される前にlegacy sourceを削除しません。

## Shared cache setup

```bash
git clone https://github.com/KAFKA2306/hf-cache-hub.git ~/src/hf-cache-hub
export HF_CACHE_HUB_ROOT="$HOME/src/hf-cache-hub"
export HF_HOME="$HOME/hf-cache"
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

## Migration rule

1. AutoPhotogrammetry等のproducerがPLYを生成する。
2. hf-cache-hub publisherがStorage Bucket upload + readback + size/SHA-256一致を確認する。
3. artifact recordを正準manifestへ登録する。
4. VRMine registryを `artifact_id` sourceへ切り替える。
5. clean consumerで `task gaussian:prepare` と3DGS contractsを再実行する。
6. そのartifactについてのみlegacy `download_url` を削除する。

未観測のremote objectを前提にregistryだけ先行変更しません。GitHub Actionsのfixture PASSはconsumer contractの証拠であり、実Storage Bucket上のPLYが存在する証拠とは区別します。
