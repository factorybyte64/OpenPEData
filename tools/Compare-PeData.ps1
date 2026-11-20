<#
  Record-level comparison of two PEData.plf files (records matched by key and occurrence number).
  Usage: .\Compare-PeData.ps1 -A ..\samples\000_empty -B ..\samples\000_empty2 [-Out diff.csv]
#>
param([Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B, [string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TiaAnalysis.cs'), (Join-Path $PSScriptRoot 'PeData.cs') -ReferencedAssemblies System.Xml
$da = [IO.File]::ReadAllBytes((Resolve-Path "$A\System\PEData.plf").Path); $db = [IO.File]::ReadAllBytes((Resolve-Path "$B\System\PEData.plf").Path)
$ra = [PeData]::ReadPlf($da); $rb = [PeData]::ReadPlf($db)
function Index($recs) { $h = [ordered]@{}; $occ = @{}; foreach ($r in $recs) { $n = [int]$occ[$r.Key]; $occ[$r.Key] = $n + 1; $h["$($r.Key)#$n"] = $r }; $h }
$ia = Index $ra; $ib = Index $rb
$rows = foreach ($k in (@($ia.Keys) + @($ib.Keys) | Select-Object -Unique)) {
  $x = $ia[$k]; $y = $ib[$k]
  if (-not $x -or -not $y) { [pscustomobject]@{ key = $k; status = if ($x) { 'onlyA' } else { 'onlyB' }; offA = $x.Offset; offB = $y.Offset; lenA = $x.Length; lenB = $y.Length }; continue }
  $pa = [PeData]::Sub($da, $x.Offset, [int]$x.Length); $pb = [PeData]::Sub($db, $y.Offset, [int]$y.Length)
  $regs = [TiaAnalysis]::DiffRegions($pa, $pb, 4)
  $status = if ($x.Length -ne $y.Length) { 'lenDiff' } elseif ($regs.Count) { 'contentDiff' } else { 'same' }
  [pscustomobject]@{ key = $k; status = $status; offA = $x.Offset; offB = $y.Offset; lenA = $x.Length; lenB = $y.Length
    diffRegions = ($regs | Select-Object -First 6 | ForEach-Object { "$($_.Start)+$($_.Length)" }) -join ' '
    firstDiffA = if ($regs.Count) { [TiaAnalysis]::Hex($pa, [Math]::Max(0, $regs[0].Start - 4), 24) } else { '' }
    firstDiffB = if ($regs.Count) { [TiaAnalysis]::Hex($pb, [Math]::Max(0, $regs[0].Start - 4), 24) } else { '' } }
}
if ($Out) { $rows | Export-Csv -NoTypeInformation -Encoding UTF8 $Out }
"records A=$($ra.Count) B=$($rb.Count)"
$rows | Group-Object status | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }
$rows
