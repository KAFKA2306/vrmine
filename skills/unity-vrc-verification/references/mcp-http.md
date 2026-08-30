# Local HTTP MCP Fallback

Use this fallback when native Unity MCP tools are unavailable. The project’s verified local endpoint is `http://127.0.0.1:8080/mcp`.

## Connect

Start the server from the repository root:

```bash
uvx --from mcpforunityserver mcp-for-unity \
  --transport http \
  --http-url http://127.0.0.1:8080 \
  --project-scoped-tools
```

Open the project with the exact Unity version in `ProjectSettings/ProjectVersion.txt`. Then prove the connection using the CLI client:

```bash
uvx --from mcpforunityserver unity-mcp \
  --host 127.0.0.1 --port 8080 --format json status
uvx --from mcpforunityserver unity-mcp \
  --host 127.0.0.1 --port 8080 --format json instances
uvx --from mcpforunityserver unity-mcp \
  --host 127.0.0.1 --port 8080 --format json scene active
```

The instance must identify the intended project and Unity version. Use read-only scene or hierarchy queries first; only then issue editor commands. Read Console state with:

```bash
uvx --from mcpforunityserver unity-mcp \
  --host 127.0.0.1 --port 8080 --format json \
  editor console --type all --count 30 --stacktrace
```

## Reconnect

Unity domain reload can disconnect the plugin during package refresh or compilation. Wait for Unity to finish reloading, create a new Streamable HTTP session, preserve its `mcp-session-id` for the session requests, send `notifications/initialized`, and repeat `status`, `instances`, and the read-only scene proof. A healthy HTTP server, a successful MCP call, or a generated scene is not compile, gameplay, networking, build, or upload evidence.
