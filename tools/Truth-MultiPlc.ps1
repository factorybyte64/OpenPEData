<#
  Openness ground truth for sample 063_multi_plc (several PLCs): Save-Truth (SimaticML + SD per DB/UDT),
  memberGet of every DB (<device>\blocks\<DB>.members.json) and the node attributes of every network
  interface of every station (network.json: device, deviceItemPath, attributes, connectedSubnetName).
  The project must be open in the TIA instance given by -Instance (process id or label).
  Usage: .\Truth-MultiPlc.ps1 -Ap <path.ap21> -Out <truth dir> -Instance <pid|label>
#>
param([Parameter(Mandatory)][string]$Ap, [Parameter(Mandatory)][string]$Out, [Parameter(Mandatory)][string]$Instance)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tia.ps1')
. (Join-Path $PSScriptRoot 'Truth.ps1')
$script:TiaInstance = $Instance
$script:TiaRaw = ${function:Tia}
function Tia([string]$Tool, [hashtable]$Arguments) {
  if (-not $Arguments.ContainsKey('instance')) { $Arguments.instance = $script:TiaInstance }
  & $script:TiaRaw $Tool $Arguments
}

Save-Truth $Ap $Out

$devs = (Get-Content -Raw "$Out\devices.json" | ConvertFrom-Json).items
$net = @()
foreach ($d in $devs) {
  $ifs = (Tia TiaNetwork @{ operation = 'interfacesDiscover'; path = $Ap; device = $d.name } | ConvertFrom-Json).items
  foreach ($i in $ifs | Where-Object { $_.hasNetworkInterface }) {
    $a = Tia TiaNetworkNode @{ operation = 'getAttributes'; path = $Ap; device = $d.name; deviceItemPath = $i.deviceItemPath } | ConvertFrom-Json
    $net += [pscustomobject]@{ device = $d.name; deviceItemPath = $i.deviceItemPath; attributes = $a.attributes; connectedSubnetName = $a.connectedSubnetName }
  }
  if ($d.softwareKind -ne 'PlcSoftware') { continue }
  $bl = (Get-Content -Raw "$Out\$($d.name)\blocks.json" | ConvertFrom-Json).items
  foreach ($b in $bl | Where-Object { $_.blockType -match 'DB' }) {
    $m = @{ operation = 'memberGet'; objectKind = 'block'; path = $Ap; device = $d.name; name = $b.name; maxDepth = '30' }
    Tia TiaPlcSoftware $m | Set-Content -Encoding UTF8 "$Out\$($d.name)\blocks\$($b.name).members.json"
  }
}
$net | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 "$Out\network.json"
"network interfaces: $($net.Count)"
