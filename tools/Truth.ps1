# Dot-source after Tia.ps1. Save-Truth writes the Openness ground truth of an OPEN project to $Out:
#   project.json, devices.json, groups.json, per PLC device: <device>\tagTables.json, <device>\tagtables\<table>.xml
#   (SimaticML), <device>\blocks.json, <device>\types.json, <device>\blocks\<name>.xml (SimaticML export of
#   data blocks and PLC data types; skipped with reason when TIA refuses, e.g. inconsistent objects)
function Save-Truth([string]$Ap, [string]$Out) {
  New-Item -ItemType Directory -Force $Out | Out-Null
  Tia TiaProject @{ operation = 'list' } | Set-Content -Encoding UTF8 "$Out\project.json"
  $dev = Tia TiaDevice @{ operation = 'list'; path = $Ap }; $dev | Set-Content -Encoding UTF8 "$Out\devices.json"
  Tia TiaDevice @{ operation = 'groupList'; path = $Ap } | Set-Content -Encoding UTF8 "$Out\groups.json"
  foreach ($d in ($dev | ConvertFrom-Json).items) {
    if ($d.softwareKind -ne 'PlcSoftware') { continue }
    $dn = $d.name
    $dd = Join-Path $Out $d.name; New-Item -ItemType Directory -Force "$dd\tagtables", "$dd\blocks" | Out-Null
    $tt = Tia TiaPlcSoftware @{ operation = 'list'; objectKind = 'tagTable'; path = $Ap; device = $dn }
    $tt | Set-Content -Encoding UTF8 "$dd\tagTables.json"
    foreach ($t in ($tt | ConvertFrom-Json).items) {
      $a = @{ operation = 'export'; objectKind = 'tagTable'; path = $Ap; device = $dn; name = $t.name; targetPath = "$dd\tagtables\$($t.name).xml" }
      if ($t.groupPath) { $a.groupPath = $t.groupPath }
      Tia TiaPlcSoftware $a | Out-Null
    }
    $bl = Tia TiaPlcSoftware @{ operation = 'list'; objectKind = 'block'; path = $Ap; device = $dn }
    $bl | Set-Content -Encoding UTF8 "$dd\blocks.json"
    $ty = Tia TiaPlcSoftware @{ operation = 'list'; objectKind = 'type'; path = $Ap; device = $dn }
    $ty | Set-Content -Encoding UTF8 "$dd\types.json"
    $exp = @()
    foreach ($b in ($bl | ConvertFrom-Json).items) { if ($b.blockType -match 'DB') { $exp += , @('block', $b) } }
    foreach ($t in ($ty | ConvertFrom-Json).items) { $exp += , @('type', $t) }
    foreach ($e in $exp) {
      $a = @{ operation = 'export'; objectKind = $e[0]; path = $Ap; device = $dn; name = $e[1].name; targetPath = "$dd\blocks\$($e[1].name).xml" }
      if ($e[1].groupPath) { $a.groupPath = $e[1].groupPath }
      try { Tia TiaPlcSoftware $a | Out-Null } catch { "export $($e[1].name) failed: $_" | Add-Content -Encoding UTF8 "$dd\blocks\_export_errors.txt" }
      $g = @{ operation = 'documentGet'; objectKind = $e[0]; path = $Ap; device = $dn; name = $e[1].name }
      try { Tia TiaPlcSoftware $g | Set-Content -Encoding UTF8 "$dd\blocks\$($e[1].name).sd.json" } catch { }
    }
    "   $($d.name): $((($tt | ConvertFrom-Json).items).Count) tag tables, $((($bl | ConvertFrom-Json).items).Count) blocks, $((($ty | ConvertFrom-Json).items).Count) types"
  }
}
