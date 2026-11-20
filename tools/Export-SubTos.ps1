param([int]$TiaPid, [string]$Out)
$api = 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48'
$bin = 'C:\Program Files\Siemens\Automation\Portal V21\Bin\PublicAPI'
Add-Type -TypeDefinition @"
using System; using System.IO; using System.Reflection;
public static class TiaResolve {
  static string[] dirs;
  public static void Install(string[] d) { dirs = d; AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
    var n = new AssemblyName(e.Name).Name + ".dll";
    foreach (var x in dirs) { var f = Path.Combine(x, n); if (File.Exists(f)) return Assembly.LoadFrom(f); }
    return null; }; }
}
"@
[TiaResolve]::Install(@($api, $bin, "$bin\V21\net48", "$bin\Client"))
Add-Type -Path "$api\Siemens.Engineering.Base.dll"
Add-Type -Path "$api\Siemens.Engineering.Step7.dll"
function Svc($o, [type]$t) { $m = [Siemens.Engineering.IEngineeringServiceProvider].GetMethod('GetService').MakeGenericMethod($t); return $m.Invoke($o, $null) }
$proc = [Siemens.Engineering.TiaPortal]::GetProcesses() | Where-Object { $_.Id -eq $TiaPid }
$tia = $proc.Attach()
$prj = $tia.Projects[0]
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($dev in $prj.Devices) { foreach ($di in $dev.DeviceItems) {
  $sc = Svc $di ([Siemens.Engineering.HW.Features.SoftwareContainer])
  if (-not $sc) { continue }
  $sw = $sc.Software
  if ($sw -isnot [Siemens.Engineering.SW.PlcSoftware]) { continue }
  foreach ($to in $sw.TechnologicalObjectGroup.TechnologicalObjects) {
    if ($to.Name -eq 'mTO_LeadingAxisProxy') { $f = Join-Path $Out ($to.Name + '.xml'); try { $to.Export((New-Object IO.FileInfo $f), [Siemens.Engineering.ExportOptions]::WithDefaults); "exported proxy" } catch { "proxy: $($_.Exception.InnerException.Message) $($_.Exception.Message)" } }
    $c = Svc $to ([Siemens.Engineering.SW.TechnologicalObjects.Motion.OutputCamMeasuringInputContainer])
    if (-not $c) { continue }
    foreach ($x in @($c.OutputCams) + @($c.MeasuringInputs)) {
      if (-not $x) { continue }
      $f = Join-Path $Out ($x.Name + '.xml')
      if (Test-Path $f) { Remove-Item $f }
      $x.Export((New-Object IO.FileInfo $f), [Siemens.Engineering.ExportOptions]::WithDefaults)
      "exported $($to.Name)/$($x.Name) consistent=$($x.IsConsistent)"
    }
  }
}}
$tia.Dispose()
