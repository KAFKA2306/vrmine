# Local HTTP MCP Fallback

Use the project's reproducible MCP client when native MCP tools are unavailable. Start the local server through the Taskfile, initialize a Streamable HTTP session at the configured loopback endpoint, preserve `mcp-session-id`, send `notifications/initialized`, then call `tools/list` or `tools/call`.

Unity domain reload can disconnect the plugin during refresh or compilation. Wait for Unity to reconnect, create a new MCP session, and then continue. Refresh success is not compile success; require Unity readiness and clean Console evidence.
