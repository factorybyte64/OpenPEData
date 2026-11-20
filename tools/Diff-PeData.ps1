<#
  Record-level diff of two project states (PEData.plf), current version of each key, with known noise hidden.
  Noise hidden by default: store records xxxx:0007 of types 0x14 (COMMIT), 0x15 (CLOSE), 0x16 (project key),
  0x0d/0x0c (store bookkeeping); DateTime ticks present in both versions; object GUID at +28 (only with -MaskGuid,
  needed when comparing independently created projects).
  Usage: .\Diff-PeData.ps1 -A ..\samples\000_empty -B ..\samples\001_comment [-Out diff.txt]
#>
param(
  [Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B,
  [switch]$MaskGuid, [switch]$ShowNoise, [int]$HexBytes = 64, [string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TiaAnalysis.cs'), (Join-Path $PSScriptRoot 'PeData.cs') -ReferencedAssemblies System.Xml
$noiseTypes = 0x00070014, 0x00070015, 0x00070016, 0x0007000d, 0x0007000c

$da = [IO.File]::ReadAllBytes((Resolve-Path "$A\System\PEData.plf").Path); $db = [IO.File]::ReadAllBytes((Resolve-Path "$B\System\PEData.plf").Path)
$ra = [PeData]::ReadPlf($da); $rb = [PeData]::ReadPlf($db)
$la = [PeData]::Latest($ra); $lb = [PeData]::Latest($rb)
# class names from the metamodel stored in B (record 0000:0007:1, current version)
$names = [PeData]::MetaNames([PeData]::MetamodelXml($db, $lb['0000:0007:1']))

function TypeName($r) { $t = [PeData]::TypeId($r); $n = $null; if ($names.TryGetValue([uint32]$t, [ref]$n)) { $n } else { 'store:0x{0:x8}' -f $t } }
function Strs($d, $o, $len) {
  $p = [PeData]::Sub($d, $o, [int]$len)
  $u = [TiaAnalysis]::Strings($p, 3, $true, 12) | ForEach-Object { $_.Key }
  $s = [TiaAnalysis]::Strings($p, 4, $false, 12) | ForEach-Object { $_.Key }
  "ascii[" + ($s -join '|') + "] utf16[" + ($u -join '|') + "]"
}
$lines = New-Object Collections.Generic.List[string]
function W($s) { $lines.Add($s) }
W "A: $A  ($($ra.Count) records, $($la.Count) keys)"
W "B: $B  ($($rb.Count) records, $($lb.Count) keys)"
$stats = @{ new = 0; removed = 0; changed = 0; same = 0; noise = 0 }
$keys = @($la.Keys) + @($lb.Keys) | Select-Object -Unique
foreach ($k in $keys) {
  $x = if ($la.ContainsKey($k)) { $la[$k] } else { $null }; $y = if ($lb.ContainsKey($k)) { $lb[$k] } else { $null }
  $r = if ($y) { $y } else { $x }
  $isNoise = $noiseTypes -contains [int][PeData]::TypeId($r)
  if (-not $x) {
    $stats.new++; W ""; W "NEW     $k  $(TypeName $y)  @B$($y.Offset) len $($y.Length)  versionsInB=$(@($rb | Where-Object Key -eq $k).Count)"
    W "        $(Strs $db $y.Offset $y.Length)"
    W "        hex: $([TiaAnalysis]::Hex($db, $y.Offset, [Math]::Min([int]$y.Length, $HexBytes * 2)))"
    continue
  }
  if (-not $y) { $stats.removed++; W ""; W "REMOVED $k  $(TypeName $x)  @A$($x.Offset) len $($x.Length)"; continue }
  $pa = [PeData]::Sub($da, $x.Offset, [int]$x.Length); $pb = [PeData]::Sub($db, $y.Offset, [int]$y.Length)
  if ($x.Length -eq $y.Length) {
    $regs = [PeData]::NoiseFreeDiff($pa, $pb, [bool]$MaskGuid, 4)
    # length field is identical here; ignore nothing else
    if ($regs.Count -eq 0) { $stats.same++; continue }
    if ($isNoise -and -not $ShowNoise) { $stats.noise++; continue }
    $stats.changed++; W ""; W "CHANGED $k  $(TypeName $y)  @A$($x.Offset) @B$($y.Offset) len $($x.Length)  versionsInB=$(@($rb | Where-Object Key -eq $k).Count)"
    foreach ($g in ($regs | Select-Object -First 12)) {
      $s = [Math]::Max(0, $g.Start - 4); $n = [Math]::Min($HexBytes, [int]$g.Length + 8)
      W ("        +{0} len {1}:  A: {2}" -f $g.Start, $g.Length, [TiaAnalysis]::Hex($pa, $s, $n))
      W ("        {0}           B: {1}" -f (' ' * "$($g.Start)$($g.Length)".Length), [TiaAnalysis]::Hex($pb, $s, $n))
    }
  } else {
    if ($isNoise -and -not $ShowNoise) { $stats.noise++; continue }
    $pre = 4 + [TiaAnalysis]::CommonPrefix([PeData]::Sub($pa, 4, $pa.Length - 4), [PeData]::Sub($pb, 4, $pb.Length - 4))
    $suf = 0; while ($suf -lt [Math]::Min($pa.Length, $pb.Length) - $pre -and $pa[$pa.Length - 1 - $suf] -eq $pb[$pb.Length - 1 - $suf]) { $suf++ }
    $stats.changed++; W ""; W "CHANGED $k  $(TypeName $y)  @A$($x.Offset) @B$($y.Offset) len $($x.Length) -> $($y.Length)  versionsInB=$(@($rb | Where-Object Key -eq $k).Count)"
    W "        length field differs; common prefix $pre, common suffix $suf"
    $s = [Math]::Max(4, $pre - 4)
    W ("        A[+{0}..+{1}): {2}" -f $s, ($pa.Length - $suf), [TiaAnalysis]::Hex($pa, $s, [Math]::Min($HexBytes * 2, $pa.Length - $suf - $s + 4)))
    W ("        B[+{0}..+{1}): {2}" -f $s, ($pb.Length - $suf), [TiaAnalysis]::Hex($pb, $s, [Math]::Min($HexBytes * 2, $pb.Length - $suf - $s + 4)))
    W "        A $(Strs $da $x.Offset $x.Length)"
    W "        B $(Strs $db $y.Offset $y.Length)"
  }
}
W ""; W ("summary: new {0}, removed {1}, changed {2}, same {3}, noise-only {4}" -f $stats.new, $stats.removed, $stats.changed, $stats.same, $stats.noise)
if ($Out) { $lines | Set-Content -Encoding UTF8 $Out }
$lines
