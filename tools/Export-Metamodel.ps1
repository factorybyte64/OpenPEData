<#
  Flattens a TIA MetaInfo XML (decompressed from PEData.plf record 0000:0007:1) into CSV tables.
  Output in -OutDir: packages.csv, types.csv (ObjectType/Structure/Enumeration/Array/AttributeSet/BehaviourType),
  attributes.csv, relations.csv, all_ids.csv (every element with an id attribute)
  Usage: .\Export-Metamodel.ps1 -Xml ..\samples\000_empty_analysis\close\zlib\166083.bin -OutDir ..\samples\000_empty_analysis\metamodel
#>
param([Parameter(Mandatory)][string]$Xml, [Parameter(Mandatory)][string]$OutDir)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$doc = New-Object Xml.XmlDocument; $doc.Load((Resolve-Path $Xml).Path)
function A($n, $a) { $v = $n.GetAttribute($a); if ($v) { $v } else { $null } }
function Ctx($n) {
  $pkg = $null; $ns = $null; $owner = $null; $p = $n.ParentNode
  while ($p -and $p.NodeType -eq 'Element') {
    switch ($p.LocalName) {
      'Package' { if (-not $pkg) { $pkg = $p.GetAttribute('name') } }
      'Namespace' { if (-not $ns) { $ns = $p.GetAttribute('name') } }
      default { if (-not $owner -and $p.GetAttribute('id')) { $owner = $p } }
    }
    $p = $p.ParentNode
  }
  [pscustomobject]@{ Package = $pkg; Namespace = $ns; Owner = $owner }
}
function IdParts($id) {
  if (-not $id) { return @($null, $null) }
  $v = [Convert]::ToUInt32($id, 16); @(('0x{0:x4}' -f ($v -shr 16)), ('0x{0:x4}' -f ($v -band 0xFFFF)))
}

$doc.SelectNodes("//*[local-name()='Package']") | ForEach-Object {
  [pscustomobject]@{ id = A $_ 'id'; name = A $_ 'name'; version = A $_ 'version'; container = A $_ 'packageContainer'
    interfaceAssembly = A $_ 'interfaceAssembly'; required = (($_.ChildNodes | Where-Object LocalName -eq 'RequiredPackage' | ForEach-Object { $_.GetAttribute('package') }) -join ';') }
} | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\packages.csv"

$rows = foreach ($n in $doc.SelectNodes('//*[@id]')) {
  if ($n.LocalName -eq 'Package') { continue }
  $c = Ctx $n; $hi, $lo = IdParts $n.GetAttribute('id')
  [pscustomobject]@{
    id = $n.GetAttribute('id'); idHi = $hi; idLo = $lo; kind = $n.LocalName; name = A $n 'name'
    fullName = if ($c.Namespace -and $n.LocalName -ne 'Attribute' -and $n.LocalName -ne 'Relation' -and $n.LocalName -ne 'Inverse') { "$($c.Namespace).$($n.GetAttribute('name'))" } else { $null }
    owner = if ($c.Owner) { $c.Owner.GetAttribute('name') } else { $null }; ownerId = if ($c.Owner) { $c.Owner.GetAttribute('id') } else { $null }
    package = $c.Package; namespace = $c.Namespace
    type = A $n 'type'; cardinality = A $n 'cardinality'; behaviourType = A $n 'behaviourType'; access = A $n 'access'
    abstract = A $n 'abstract'; private = A $n 'private'; persistent = A $n 'persistent'
    base = (($n.ChildNodes | Where-Object LocalName -eq 'Base' | ForEach-Object { $_.GetAttribute('ref') }) -join ';')
    target = (($n.ChildNodes | Where-Object LocalName -eq 'Target' | ForEach-Object { $_.GetAttribute('ref') }) -join ';')
  }
}
$rows | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\all_ids.csv"
$rows | Where-Object { $_.kind -in 'ObjectType', 'Structure', 'Enumeration', 'Array', 'AttributeSet', 'BehaviourType' } | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\types.csv"
$rows | Where-Object kind -eq 'Attribute' | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\attributes.csv"
$rows | Where-Object { $_.kind -in 'Relation', 'Inverse' } | Export-Csv -NoTypeInformation -Encoding UTF8 "$OutDir\relations.csv"
$rows | Group-Object kind | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }
