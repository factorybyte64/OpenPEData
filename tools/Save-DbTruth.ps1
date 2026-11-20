# Dot-source after Tia.ps1. Save-DbTruth writes SimaticML exports and memberGet results of all data blocks and
# technology objects of the open project (all PLCs) to $Out\<device>\blocks\<name>.xml / <name>.members.json.
# System blocks (Program resources, F-I/O DBs) are included as far as Openness lists them (includeSystemBlocks).
function Save-DbTruth([string]$Ap, [string]$Out, [string]$Instance) {
  $dev = Tia TiaDevice @{ operation = 'list'; path = $Ap; instance = $Instance } | ConvertFrom-Json
  foreach ($d in $dev.items) {
    if ($d.softwareKind -ne 'PlcSoftware') { continue }
    $dd = Join-Path $Out "$($d.name)\blocks"; New-Item -ItemType Directory -Force $dd | Out-Null
    $bl = Tia TiaPlcSoftware @{ operation = 'list'; objectKind = 'block'; path = $Ap; device = $d.name; includeSystemBlocks = 'true'; instance = $Instance } | ConvertFrom-Json
    $dbs = @($bl.items) + @($bl.systemBlocks) | Where-Object { $_ -and $_.blockType -match 'DB' }
    $tos = (Tia TiaPlcSoftware @{ operation = 'list'; objectKind = 'technologyObject'; path = $Ap; device = $d.name; instance = $Instance } | ConvertFrom-Json).items
    foreach ($b in $dbs) {
      $a = @{ operation = 'export'; objectKind = 'block'; path = $Ap; device = $d.name; name = $b.name; targetPath = "$dd\$($b.name).xml"; instance = $Instance }
      if ($b.groupPath) { $a.groupPath = $b.groupPath }
      try { Tia TiaPlcSoftware $a | Out-Null } catch { "export $($b.name) failed: $(($_ | Out-String).Split("`n")[0])" }
      try { $r = Tia TiaPlcSoftware @{ operation = 'memberGet'; objectKind = 'block'; path = $Ap; device = $d.name; name = $b.name; maxDepth = '30'; instance = $Instance }
            $j = $r | ConvertFrom-Json; if ($j.fullResultPath) { Copy-Item $j.fullResultPath "$dd\$($b.name).members.json" } else { $r | Set-Content -Encoding UTF8 "$dd\$($b.name).members.json" } }
      catch { "memberGet $($b.name) failed" }
    }
    foreach ($t in $tos) {
      try { Tia TiaPlcSoftware @{ operation = 'export'; objectKind = 'technologyObject'; path = $Ap; device = $d.name; name = $t.name; targetPath = "$dd\$($t.name).xml"; instance = $Instance } | Out-Null } catch { "export TO $($t.name) failed" }
    }
    "$($d.name): $(@($dbs).Count) DBs ($((@($dbs) | ForEach-Object { $_.name }) -join ',')), $(@($tos).Count) TOs ($((@($tos) | ForEach-Object { $_.name }) -join ','))"
  }
}
