<#
  Creates a derived sample: copies a closed sample folder, opens it via TiaMcpXE, runs the change script block,
  saves, archives (<name>.zap21 next to the folder) and closes.
  Usage (dot-source Tia.ps1 is done here):
    .\New-Sample.ps1 -From 000_empty -Name 001_devgroup -Change { Tia TiaDevice @{ operation='groupCreate'; name='Group_1' } }
#>
param(
  [Parameter(Mandatory)][string]$From, [Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][scriptblock]$Change,
  [scriptblock]$GroundTruth,
  [string]$Samples = (Join-Path $PSScriptRoot '..\samples')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tia.ps1')
$Samples = (Resolve-Path $Samples).Path
$src = Join-Path $Samples $From; $dst = Join-Path $Samples $Name
if (Test-Path $dst) { throw "$dst exists" }
Copy-Item $src $dst -Recurse
$ap = (Get-ChildItem $dst -Filter *.ap21 | Select-Object -First 1).FullName
"open:    " + (Tia TiaProject @{ operation = 'open'; path = $ap })
try {
  & $Change | ForEach-Object { "change:  $_" }
  "save:    " + (Tia TiaProject @{ operation = 'save'; path = $ap })
  if ($GroundTruth) { & $GroundTruth $dst | ForEach-Object { "truth:   $_" } }
  "archive: " + (Tia TiaProject @{ operation = 'archive'; path = $ap; archivePath = "$dst.zap21" })
} finally {
  "close:   " + (Tia TiaProject @{ operation = 'close'; path = $ap })
}
