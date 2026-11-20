<#
  Steps 5a/5b: derived samples, each from a closed predecessor sample via New-Sample.ps1 (copy -> open ->
  change -> save -> ground truth (Save-Truth into <sample>_truth) -> archive -> close).
  Recipes below; the MANIFEST lists the same recipes.
  Usage: .\Make-Samples.ps1 -Only 010_dev_in_group
#>
param([Parameter(Mandatory)][string[]]$Only)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Tia.ps1')
. (Join-Path $PSScriptRoot 'Truth.ps1')
$tt = @{ objectKind = 'tagTable' }
$recipes = [ordered]@{
  # --- 5a: gaps --- (non-ASCII only as [char] escapes: PowerShell 5.1 reads BOM-less scripts as ANSI)
  '010_dev_in_group'    = @{ From = '001_devgroup'; Change = { Tia TiaDevice @{ operation = 'create'; name = 'PLC_G'; deviceType = 'OrderNumber:6ES7 513-1AL02-0AB0/V2.9'; parentGroup = 'Group_1' } } }
  '011_tagtable_folder' = @{ From = '003_tagtable'; Change = {
      Tia TiaPlcSoftware @{ operation = 'groupCreate'; objectKind = 'tagTable'; name = 'TagGroup_1' }
      Tia TiaPlcSoftware @{ operation = 'create'; objectKind = 'tagTable'; name = 'TagTable_2'; groupPath = 'TagGroup_1' } } }
  '012_tags_multi'      = @{ From = '003_tagtable'; Change = {
      $tags = @(
        @{ tagName = 'In_Bool'; dataType = 'Bool'; logicalAddress = '%I0.1' },
        @{ tagName = 'Out_Bool'; dataType = 'Bool'; logicalAddress = '%Q2.0' },
        @{ tagName = 'M_Int'; dataType = 'Int'; logicalAddress = '%MW20' },
        @{ tagName = 'M_Real'; dataType = 'Real'; logicalAddress = '%MD24' },
        @{ tagName = 'In_Word'; dataType = 'Word'; logicalAddress = '%IW4' },
        @{ tagName = 'Out_Byte'; dataType = 'Byte'; logicalAddress = '%QB3' },
        @{ tagName = 'M_DInt'; dataType = 'DInt'; logicalAddress = '%MD28'; comment = 'DInt Kommentar' },
        @{ tagName = 'M_Time'; dataType = 'Time'; logicalAddress = '%MD32' })
      Tia TiaPlcSoftware @{ operation = 'tagCreate'; objectKind = 'tagTable'; name = 'TagTable_1'; tags = ($tags | ConvertTo-Json -Compress) } } }
  '013_tag_umlaut'      = @{ From = '003_tagtable'; Change = { Tia TiaPlcSoftware @{ operation = 'tagSet'; objectKind = 'tagTable'; name = 'TagTable_1'; tagName = 'Tag_Bool_1'; comment = "Pr$([char]0xFC)fung $([char]0xE4)$([char]0xF6)$([char]0xFC)$([char]0xDF)" } } }
  '014_tag_delete'      = @{ From = '003_tagtable'; Change = { Tia TiaPlcSoftware @{ operation = 'tagDelete'; objectKind = 'tagTable'; name = 'TagTable_1'; tagName = 'Tag_Bool_1' } } }
  '015_cpu2'            = @{ From = '002_cpu'; Change = { Tia TiaDevice @{ operation = 'create'; name = 'PLC_2'; cloneTypeFromDevice = 'PLC_1' } } }
  '016_tag_accessible'  = @{ From = '003_tagtable'; Change = { Tia TiaPlcSoftware @{ operation = 'tagSet'; objectKind = 'tagTable'; name = 'TagTable_1'; tagName = 'Tag_Bool_1'; externalAccessible = $false } } }
  '021_mastercopy'      = @{ From = '004_fb'; Change = { Tia TiaProjectLibrary @{ operation = 'masterCopyCreate'; device = 'PLC_1'; blockName = 'FB_Empty' }; Tia TiaProjectLibrary @{ operation = 'masterCopiesList' } } }  # slot-order check: LibraryMasterCopyData
  # --- 5b: data blocks and PLC data types (sources in tools\sources\) ---
  '017_udt'             = @{ From = '004_fb'; Change = { Tia TiaPlcSoftware @{ operation = 'documentWrite'; objectKind = 'type'; sourceFile = (Join-Path $PSScriptRoot 'sources\UDT_Motor.s7dcl') } } }
  '018_db_global'       = @{ From = '017_udt'; Change = { Tia TiaPlcSoftware @{ operation = 'documentWrite'; objectKind = 'block'; sourceFile = (Join-Path $PSScriptRoot 'sources\DB_Global.s7dcl') } } }
  '019_db_instance'     = @{ From = '018_db_global'; Change = { Tia TiaPlcSoftware @{ operation = 'create'; objectKind = 'block'; name = 'FB_Empty_DB'; blockType = 'InstanceDB'; instanceOf = 'FB_Empty' } } }
  '020_block_group'     = @{ From = '019_db_instance'; Change = {
      Tia TiaPlcSoftware @{ operation = 'groupCreate'; objectKind = 'block'; name = 'BlockGroup_1' }
      Tia TiaPlcSoftware @{ operation = 'documentWrite'; objectKind = 'block'; groupPath = 'BlockGroup_1'; sourceFile = (Join-Path $PSScriptRoot 'sources\DB_InGroup.s7dcl') } } }
  '022_db_udt'          = @{ From = '020_block_group'; Change = { Tia TiaPlcSoftware @{ operation = 'documentWrite'; objectKind = 'block'; sourceFile = (Join-Path $PSScriptRoot 'sources\DB_Motor.s7dcl') } } }
}
foreach ($name in $Only) {
  $r = $recipes[$name]; if (-not $r) { throw "no recipe $name" }
  "== $name from $($r.From)"
  & (Join-Path $PSScriptRoot 'New-Sample.ps1') -From $r.From -Name $name -Change $r.Change -GroundTruth {
    param($dst)
    $ap = (Get-ChildItem $dst -Filter *.ap21 | Select-Object -First 1).FullName
    Save-Truth $ap "$dst`_truth"
  }
}
