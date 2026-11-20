<#
  Step 4b: derived samples 005-009 from 003_tagtable, exactly one tag change each (TiaPlcSoftware tagSet on
  TagTable_1/Tag_Bool_1). Ground truth: SimaticML export of TagTable_1 and Default tag table, tag table get (JSON).
  Usage: .\Make-TagSamples.ps1 [-Only 005_tag_int]
#>
param([string[]]$Only)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tia.ps1')
$recipes = [ordered]@{
  '005_tag_int'     = @{ dataType = 'Int' }
  '006_tag_addr'    = @{ logicalAddress = '%M10.3' }
  '007_tag_comment' = @{ comment = 'Test' }
  '008_tag_flags'   = @{ externalWritable = $false }
  '009_tag_rename'  = @{ newName = 'Tag_Renamed' }
}
$samples = (Resolve-Path (Join-Path $PSScriptRoot '..\samples')).Path
foreach ($name in $recipes.Keys) {
  if ($Only -and $name -notin $Only) { continue }
  $change = $recipes[$name]
  $tagArgs = @{ operation = 'tagSet'; objectKind = 'tagTable'; name = 'TagTable_1'; tagName = 'Tag_Bool_1' } + $change
  "== $name  " + ($change | ConvertTo-Json -Compress)
  & (Join-Path $PSScriptRoot 'New-Sample.ps1') -From 003_tagtable -Name $name -Change { Tia TiaPlcSoftware $tagArgs } -GroundTruth {
    param($dst)
    $t = "$dst`_truth"; New-Item -ItemType Directory -Force $t | Out-Null
    foreach ($tt in 'TagTable_1', 'Default tag table') {
      Tia TiaPlcSoftware @{ operation = 'export'; objectKind = 'tagTable'; name = $tt; targetPath = "$t\$tt.xml" }
    }
    Tia TiaPlcSoftware @{ operation = 'get'; objectKind = 'tagTable'; name = 'TagTable_1' } | Set-Content -Encoding UTF8 "$t\TagTable_1.get.json"
  }
}
