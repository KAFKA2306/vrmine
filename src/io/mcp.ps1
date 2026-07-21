param(
    [string]$Tool,
    [string]$Arguments
)

$mcpHeaders = @{ Accept = 'application/json, text/event-stream'; 'Content-Type' = 'application/json' }
$initializeBody = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{ protocolVersion = '2025-03-26'; capabilities = @{}; clientInfo = @{ name = 'vrmine-task'; version = '1.0' } } } | ConvertTo-Json -Depth 8 -Compress
$initializeResponse = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8080/mcp' -Method Post -Headers $mcpHeaders -Body $initializeBody
$mcpHeaders['mcp-session-id'] = $initializeResponse.Headers['mcp-session-id']
$initializedBody = @{ jsonrpc = '2.0'; method = 'notifications/initialized'; params = @{} } | ConvertTo-Json -Depth 4 -Compress
Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8080/mcp' -Method Post -Headers $mcpHeaders -Body $initializedBody | Out-Null
$callBody = @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = $Tool; arguments = $Arguments | ConvertFrom-Json } } | ConvertTo-Json -Depth 12 -Compress
$callResponse = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8080/mcp' -Method Post -Headers $mcpHeaders -Body $callBody
$callResponse.Content
