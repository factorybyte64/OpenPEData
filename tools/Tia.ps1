# Dot-source: . .\tools\Tia.ps1 ; then  Tia TiaProject @{ operation = 'list' }
# Returns the tool's text result (JSON string); throws when the tool reports isError.
$script:McpCall = Join-Path $PSScriptRoot 'McpCall.ps1'
function Tia([string]$Tool, [hashtable]$Arguments) {
  $json = $Arguments | ConvertTo-Json -Depth 20 -Compress
  $r = & $script:McpCall -Tool $Tool -Arguments $json | ConvertFrom-Json
  if ($r.error) { throw "MCP error: $($r.error.message)" }
  $t = ($r.result.content | ForEach-Object { $_.text }) -join "`n"
  if ($r.result.isError) { throw "Tool error ($Tool $($Arguments.operation)): $t" }
  $t
}
function TiaHelp([string]$Tool, [string]$Topic, [string]$Kind) {
  $a = @{ operation = 'help'; topic = $Topic }; if ($Kind) { $a.objectKind = $Kind }
  Tia $Tool $a
}
