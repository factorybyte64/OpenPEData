<#
  Builds parser\openpedata\unified_enums.json: the enum types of WinCC Unified screen item properties and their
  values, read by reflection (metadata only) from the Openness API Siemens.Engineering.WinCCUnified.dll of TIA V21.
  Style values store enums as numbers (docs\spec\hmi-styles.md); the numbers equal the Openness enum values (checked
  against samples 165-167). Properties whose name maps to different enum types (e.g. EventType) are left out.
  Usage: powershell -File tools\unified_enums.ps1 [-ApiDir <PublicAPI\V21\net48>]
#>
param([string]$ApiDir = "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48")
$ErrorActionPreference = 'Stop'
$out = Join-Path (Split-Path $PSScriptRoot -Parent) "parser\openpedata\unified_enums.json"
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve({
    param($s, $e)
    $p = Join-Path $ApiDir ((New-Object Reflection.AssemblyName $e.Name).Name + ".dll")
    if (Test-Path $p) { [Reflection.Assembly]::ReflectionOnlyLoadFrom($p) } else { [Reflection.Assembly]::ReflectionOnlyLoad($e.Name) }
})
$a = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $ApiDir "Siemens.Engineering.WinCCUnified.dll"))
try { $types = $a.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ } }
$ui = $types | Where-Object { $_.Namespace -like "*HmiUnified.UI*" }
$props = @{}
foreach ($t in $ui | Where-Object { -not $_.IsEnum }) {
    try { $ps = $t.GetProperties() } catch { continue }
    foreach ($p in $ps) {
        try { $pt = $p.PropertyType } catch { continue }
        if (-not $pt.IsEnum) { continue }
        if (-not $props.ContainsKey($p.Name)) { $props[$p.Name] = @{} }
        $props[$p.Name][$pt.Name] = $pt
    }
}
$propMap = [ordered]@{}
$enums = [ordered]@{}
foreach ($k in $props.Keys | Sort-Object) {
    if ($props[$k].Count -ne 1) { continue }
    $pt = @($props[$k].Values)[0]
    $propMap[$k] = $pt.Name
    if (-not $enums.Contains($pt.Name)) {
        $flags = @($pt.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -eq "FlagsAttribute" }).Count -gt 0
        $vals = [ordered]@{}
        foreach ($f in $pt.GetFields() | Where-Object { $_.IsLiteral }) { $vals[$f.Name] = [long]$f.GetRawConstantValue() }
        $enums[$pt.Name] = [ordered]@{ flags = $flags; values = $vals }
    }
}
$doc = [ordered]@{
    source = "Siemens.Engineering.WinCCUnified.dll (TIA Portal V21 Openness), enum metadata by reflection; tools\unified_enums.ps1"
    properties = $propMap
    enums = $enums
}
$json = $doc | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($out, $json + "`n", (New-Object Text.UTF8Encoding $false))
"{0} properties, {1} enum types -> {2}" -f $propMap.Count, $enums.Count, $out
