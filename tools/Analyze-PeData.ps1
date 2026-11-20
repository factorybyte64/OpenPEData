<#
  Exploratory analysis of System\PEData.plf / .idx of one project folder.
  Writes to -OutDir: plf_records.csv, idx_items.csv, zlib_streams.csv, zlib\<offset>.bin, summary.txt
  Usage: .\Analyze-PeData.ps1 -ProjectDir ..\samples\000_empty -OutDir ..\samples\000_empty_analysis\close
#>
param([Parameter(Mandatory)][string]$ProjectDir, [Parameter(Mandatory)][string]$OutDir)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'TiaAnalysis.cs'), (Join-Path $PSScriptRoot 'PeData.cs') -ReferencedAssemblies System.Xml
New-Item -ItemType Directory -Force $OutDir, "$OutDir\zlib" | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$plf = [IO.File]::ReadAllBytes((Resolve-Path "$ProjectDir\System\PEData.plf").Path)
$idx = [IO.File]::ReadAllBytes((Resolve-Path "$ProjectDir\System\PEData.idx").Path)

$recs = [PeData]::ReadPlf($plf)
$starts = New-Object 'System.Collections.Generic.HashSet[long]'
$keys = New-Object 'System.Collections.Generic.Dictionary[long,string]'
foreach ($r in $recs) { [void]$starts.Add($r.Offset); $keys[$r.Offset] = $r.Key }
$items = [PeData]::ReadIdx($idx, $starts, $keys)
$refCount = @{}; foreach ($i in $items) { if ($i.Kind -eq 'entry') { $refCount[[long]$i.PlfOffset] = 1 + [int]$refCount[[long]$i.PlfOffset] } }

# zlib streams, assigned to records
$zs = [TiaAnalysis]::FindZlib($plf, 16)
$zrows = foreach ($z in $zs) {
  $rec = $recs | Where-Object { $_.Offset -le $z.Offset -and $z.Offset -lt $_.Offset + $_.Length } | Select-Object -First 1
  $tr = [TiaAnalysis]::TextRatios($z.Data)
  $kind = if ($z.Data.Length -ge 3 -and $z.Data[0] -eq 0xEF -and $z.Data[1] -eq 0xBB) { 'utf8-bom' }
          elseif ($z.Data.Length -ge 2 -and $z.Data[0] -eq 0xFF -and $z.Data[1] -eq 0xFE) { 'utf16-bom' }
          elseif ($tr[1] -gt 0.8) { 'utf16le-text' } elseif ($tr[0] -gt 0.9) { 'ascii-text' } else { 'binary' }
  [IO.File]::WriteAllBytes("$OutDir\zlib\$($z.Offset).bin", $z.Data)
  $top = ([TiaAnalysis]::Strings($z.Data, 6, $false, 8) | ForEach-Object { "$($_.Key)($($_.Value))" }) -join ' | '
  $top16 = ([TiaAnalysis]::Strings($z.Data, 6, $true, 8) | ForEach-Object { "$($_.Key)($($_.Value))" }) -join ' | '
  [pscustomobject]@{
    offset = $z.Offset; compressedLength = $z.CompressedLength; decompressedLength = $z.DecompressedLength; adlerOk = $z.AdlerOk
    record = if ($rec) { $rec.Offset } else { $null }; recordKey = if ($rec) { $rec.Key } else { $null }
    offsetInRecord = if ($rec) { $z.Offset - $rec.Offset } else { $null }
    endInRecord = if ($rec -and $z.CompressedLength -gt 0) { $rec.Offset + $rec.Length - ($z.Offset + $z.CompressedLength) } else { $null }
    pre32Hex = [TiaAnalysis]::Hex($plf, $z.Offset - 32, 32); kind = $kind
    head32Hex = [TiaAnalysis]::Hex($z.Data, 0, 32); asciiStrings = $top; utf16Strings = $top16
  }
}
$zrows | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\zlib_streams.csv"

$recs | ForEach-Object {
  [pscustomobject]@{ offset = $_.Offset; length = $_.Length; hashOk = $_.HashOk; key = $_.Key; keyA = $_.KeyA; keyB = $_.KeyB; keyId = $_.KeyId
    idxRefs = [int]$refCount[[long]$_.Offset]; head48Hex = [TiaAnalysis]::Hex($plf, $_.Offset, 48)
    strings = ([PeData]::Ascii($plf, $_.Offset, [Math]::Min([int]$_.Length, 400))) }
} | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\plf_records.csv"
$items | Select-Object Offset, Kind, Key, Guid, B28, PlfOffset, I32, B37, RecLength, Text | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\idx_items.csv"

$sum = @(
  "plf length $($plf.Length), records $($recs.Count), hash mismatches $(@($recs | Where-Object { -not $_.HashOk }).Count), chain end $(if ($recs.Count) { $recs[-1].Offset + $recs[-1].Length + 32 })"
  "distinct keys $(@($recs | Select-Object -ExpandProperty Key -Unique).Count)"
  "idx length $($idx.Length), entries $(@($items | Where-Object Kind -eq 'entry').Count), records $(@($items | Where-Object Kind -eq 'record').Count), unknown $(@($items | Where-Object Kind -eq 'unknown').Count), last item end $(if ($items.Count) { $l = $items[-1]; if ($l.Kind -eq 'entry') { $l.Offset + 38 } elseif ($l.Kind -eq 'record') { $l.Offset + $l.RecLength } else { $l.Offset } })"
  "plf records referenced by idx: $($refCount.Count)"
  "zlib streams $($zs.Count), with trailer $(@($zs | Where-Object AdlerOk).Count), total decompressed $(($zs | ForEach-Object { $_.DecompressedLength } | Measure-Object -Sum).Sum)"
)
$sum | Set-Content -Encoding UTF8 "$OutDir\summary.txt"
$sum
