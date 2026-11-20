<#
  Complete Openness ground truth for the parser tests, taken from COPIES of the samples:
  copy sample folder to TMP\truth_<sample> -> open -> Save-Truth (tools\Truth.ps1) -> close -> delete the copy.
  Output: parser\tests\truth\<sample>\ . The samples themselves are not opened.
  Usage: .\Make-TruthSet.ps1 [-Only 000_empty,004_fb]
#>
param([string[]]$Only)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tia.ps1')
. (Join-Path $PSScriptRoot 'Truth.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$samples = Get-ChildItem "$root\samples" -Directory | Where-Object { $_.Name -match '^\d{3}_' -and (Test-Path "$($_.FullName)\System\PEData.plf") }
foreach ($s in $samples) {
  if ($Only -and $s.Name -notin $Only) { continue }
  $copy = "$root\TMP\truth_$($s.Name)"; $out = "$root\parser\tests\truth\$($s.Name)"
  if (Test-Path $copy) { Remove-Item -Recurse -Force $copy }
  if (Test-Path $out) { Remove-Item -Recurse -Force $out }
  Copy-Item $s.FullName $copy -Recurse
  $ap = (Get-ChildItem $copy -Filter *.ap21 | Select-Object -First 1).FullName
  "== $($s.Name)"
  Tia TiaProject @{ operation = 'open'; path = $ap } | Out-Null
  try { Save-Truth $ap $out } finally {
    Tia TiaProject @{ operation = 'close'; path = $ap } | Out-Null
    Remove-Item -Recurse -Force $copy
  }
}
