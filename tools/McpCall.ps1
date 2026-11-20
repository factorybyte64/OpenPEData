<#
  Minimal MCP Streamable-HTTP client for TiaMcpXE (http://127.0.0.1:47124/mcp).
  Usage:
    .\McpCall.ps1 -List
    .\McpCall.ps1 -Tool TiaProject -Arguments '{"operation":"help"}'
#>
param(
  [string]$Tool,
  [string]$Arguments = '{}',
  [switch]$List,
  [string]$Resource,
  [string]$Url = 'http://127.0.0.1:47124/mcp',
  [int]$TimeoutSec = 600
)
$ErrorActionPreference = 'Stop'

function Invoke-Rpc([hashtable]$Body, [string]$SessionId) {
  $headers = @{ Accept = 'application/json, text/event-stream' }
  if ($SessionId) { $headers['Mcp-Session-Id'] = $SessionId }
  $json = $Body | ConvertTo-Json -Depth 50 -Compress
  $resp = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -ContentType 'application/json' `
          -Body ([Text.Encoding]::UTF8.GetBytes($json)) -UseBasicParsing -TimeoutSec $TimeoutSec
  $text = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray())
  if ($resp.Headers['Content-Type'] -like 'text/event-stream*') {
    $data = ($text -split "`n" | Where-Object { $_ -like 'data:*' } | ForEach-Object { $_.Substring(5).Trim() }) -join ''
    $text = $data
  }
  [pscustomobject]@{ Session = $resp.Headers['Mcp-Session-Id']; Text = $text }
}

$init = Invoke-Rpc @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{
  protocolVersion = '2025-03-26'; capabilities = @{}; clientInfo = @{ name = 'OpenPEData-McpCall'; version = '0.1' } } } $null
$sid = $init.Session
try { Invoke-Rpc @{ jsonrpc = '2.0'; method = 'notifications/initialized' } $sid | Out-Null } catch {}

if ($List) {
  $r = Invoke-Rpc @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} } $sid
} elseif ($Resource) {
  $r = Invoke-Rpc @{ jsonrpc = '2.0'; id = 2; method = 'resources/read'; params = @{ uri = $Resource } } $sid
} else {
  $argsObj = $Arguments | ConvertFrom-Json
  $ht = @{}; $argsObj.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value }
  $r = Invoke-Rpc @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = $Tool; arguments = $ht } } $sid
}
$r.Text
