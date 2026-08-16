# 001 Local Unity MCP

ローカルUnity操作用。CI/U1–U5の正準経路にはしない。

## 起動

```powershell
uvx --from "git+https://github.com/CoplayDev/unity-mcp@v10.1.2#subdirectory=Server" mcp-for-unity --transport http --http-host 127.0.0.1 --http-port 8080
```

Unityを開いた状態で確認:

```powershell
curl.exe http://127.0.0.1:8080/health
```

ChatGPT接続時はOpenAI `tunnel-client` のMCP先を `http://127.0.0.1:8080/mcp` にし、`doctor --explain` → `run`。

完了条件: `/health` 成功 + ChatGPTからUnityのread-only tool/resourceを1回実行できること。資格情報はrepoへ保存しない。

Sources:
- https://github.com/CoplayDev/unity-mcp/blob/v10.1.2/Server/README.md
- https://github.com/openai/tunnel-client/blob/master/docs/configuration.md
