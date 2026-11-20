<#
  File inventory for a TIA project folder (and optional extra files, e.g. .zap21).
  Per file: relative path, size, first 32 bytes hex, Shannon entropy (whole file and per 4 KiB block min/max/mean),
  container detection (ZIP / gzip / zlib header at offset 0 incl. trial decompression) and embedded signature scan.
  Usage:
    .\Inventory.ps1 -Root ..\samples\000_empty -Extra ..\samples\000_empty.zap21 -Out ..\samples\000_empty_inventory.json
#>
param(
  [Parameter(Mandatory)][string]$Root,
  [string[]]$Extra = @(),
  [Parameter(Mandatory)][string]$Out,
  [int]$BlockSize = 4096,
  [int]$MaxOffsets = 20
)
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

public static class TiaInv {
  public static double Entropy(byte[] d, int off, int len) {
    if (len <= 0) return 0;
    var c = new int[256];
    for (int i = off; i < off + len; i++) c[d[i]]++;
    double h = 0;
    for (int i = 0; i < 256; i++) if (c[i] > 0) { double p = (double)c[i] / len; h -= p * Math.Log(p, 2); }
    return h;
  }
  // returns [min, max, mean, count]
  public static double[] BlockEntropy(byte[] d, int bs) {
    if (d.Length == 0) return new double[] { 0, 0, 0, 0 };
    double mn = double.MaxValue, mx = 0, sum = 0; int n = 0;
    for (int o = 0; o < d.Length; o += bs) {
      int l = Math.Min(bs, d.Length - o);
      double e = Entropy(d, o, l);
      if (e < mn) mn = e; if (e > mx) mx = e; sum += e; n++;
    }
    return new double[] { mn, mx, sum / n, n };
  }
  static bool IsZlibHeader(byte[] d, int i) {
    if (i + 2 > d.Length) return false;
    int cmf = d[i], flg = d[i + 1];
    return (cmf & 0x0F) == 8 && (cmf >> 4) <= 7 && ((cmf << 8) | flg) % 31 == 0 && (flg & 0x20) == 0;
  }
  public static bool ZlibAt0(byte[] d) { return IsZlibHeader(d, 0); }
  // Trial inflate of raw deflate data starting at off; returns decompressed length or -1
  public static long TryInflate(byte[] d, int off) {
    try {
      using (var ms = new MemoryStream(d, off, d.Length - off))
      using (var ds = new DeflateStream(ms, CompressionMode.Decompress)) {
        var buf = new byte[65536]; long tot = 0; int r;
        while ((r = ds.Read(buf, 0, buf.Length)) > 0) { tot += r; if (tot > (1L << 31)) break; }
        return tot;
      }
    } catch { return -1; }
  }
  public static long TryGunzip(byte[] d) {
    try {
      using (var ms = new MemoryStream(d))
      using (var gs = new GZipStream(ms, CompressionMode.Decompress)) {
        var buf = new byte[65536]; long tot = 0; int r;
        while ((r = gs.Read(buf, 0, buf.Length)) > 0) tot += r;
        return tot;
      }
    } catch { return -1; }
  }
  static bool Match(byte[] d, int i, byte[] sig) {
    if (i + sig.Length > d.Length) return false;
    for (int k = 0; k < sig.Length; k++) if (d[i + k] != sig[k]) return false;
    return true;
  }
  // Scan for embedded signatures; returns name -> offsets (count limited by max, total count in key "#name")
  public static Dictionary<string, List<long>> Scan(byte[] d, int max) {
    var sigs = new Dictionary<string, byte[]> {
      { "zip_local_PK0304", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
      { "zip_central_PK0102", new byte[] { 0x50, 0x4B, 0x01, 0x02 } },
      { "zip_eocd_PK0506", new byte[] { 0x50, 0x4B, 0x05, 0x06 } },
      { "gzip_1F8B08", new byte[] { 0x1F, 0x8B, 0x08 } },
      { "xml_decl", System.Text.Encoding.ASCII.GetBytes("<?xml") },
      { "utf16le_xml_decl", new byte[] { 0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, 0x6D, 0x00, 0x6C, 0x00 } },
      { "sqlite", System.Text.Encoding.ASCII.GetBytes("SQLite format 3") },
    };
    var res = new Dictionary<string, List<long>>();
    var cnt = new Dictionary<string, long>();
    foreach (var k in sigs.Keys) { res[k] = new List<long>(); cnt[k] = 0; }
    res["zlib_78xx_valid"] = new List<long>(); cnt["zlib_78xx_valid"] = 0;
    for (int i = 0; i < d.Length; i++) {
      foreach (var kv in sigs) {
        if (d[i] == kv.Value[0] && Match(d, i, kv.Value)) { cnt[kv.Key]++; if (res[kv.Key].Count < max) res[kv.Key].Add(i); }
      }
      // zlib: 78 01 / 78 5E / 78 9C / 78 DA that also inflate a few bytes successfully
      if (d[i] == 0x78 && i + 2 < d.Length && (d[i+1] == 0x01 || d[i+1] == 0x5E || d[i+1] == 0x9C || d[i+1] == 0xDA)) {
        if (ProbeInflate(d, i + 2)) { cnt["zlib_78xx_valid"]++; if (res["zlib_78xx_valid"].Count < max) res["zlib_78xx_valid"].Add(i); }
      }
    }
    foreach (var k in cnt.Keys) res["#" + k] = new List<long> { cnt[k] };
    return res;
  }
  static bool ProbeInflate(byte[] d, int off) {
    try {
      int len = Math.Min(d.Length - off, 1 << 20);
      using (var ms = new MemoryStream(d, off, len))
      using (var ds = new DeflateStream(ms, CompressionMode.Decompress)) {
        var buf = new byte[256];
        return ds.Read(buf, 0, buf.Length) > 16;
      }
    } catch { return false; }
  }
}
'@

$rootFull = (Resolve-Path $Root).Path.TrimEnd('\')
$files = @(Get-ChildItem -LiteralPath $rootFull -Recurse -File -Force | ForEach-Object { [pscustomobject]@{ F = $_; Rel = $_.FullName.Substring($rootFull.Length + 1) } })
foreach ($e in $Extra) { $fi = Get-Item -LiteralPath $e; $files += [pscustomobject]@{ F = $fi; Rel = '..\' + $fi.Name } }

$items = foreach ($x in ($files | Sort-Object Rel)) {
  $d = [IO.File]::ReadAllBytes($x.F.FullName)
  $n = [Math]::Min(32, $d.Length)
  $hex = if ($n) { ($d[0..($n - 1)] | ForEach-Object { $_.ToString('x2') }) -join ' ' } else { '' }
  $ascii = if ($n) { -join ($d[0..($n - 1)] | ForEach-Object { if ($_ -ge 32 -and $_ -lt 127) { [char]$_ } else { '.' } }) } else { '' }
  $be = [TiaInv]::BlockEntropy($d, $BlockSize)
  $isZip = $d.Length -ge 4 -and $d[0] -eq 0x50 -and $d[1] -eq 0x4B -and $d[2] -eq 3 -and $d[3] -eq 4
  $isGz = $d.Length -ge 3 -and $d[0] -eq 0x1F -and $d[1] -eq 0x8B -and $d[2] -eq 8
  $isZl = [TiaInv]::ZlibAt0($d)
  $zipEntries = $null
  if ($isZip) {
    try {
      Add-Type -AssemblyName System.IO.Compression
      $za = New-Object IO.Compression.ZipArchive((New-Object IO.MemoryStream(, $d)), [IO.Compression.ZipArchiveMode]::Read)
      $zipEntries = @($za.Entries | ForEach-Object { [ordered]@{ name = $_.FullName; length = $_.Length; compressed = $_.CompressedLength } })
      $za.Dispose()
    } catch { $zipEntries = "error: $($_.Exception.Message)" }
  }
  $scan = [TiaInv]::Scan($d, $MaxOffsets)
  $sigs = [ordered]@{}
  foreach ($k in ($scan.Keys | Where-Object { $_ -notlike '#*' } | Sort-Object)) {
    $c = $scan['#' + $k][0]
    if ($c -gt 0) { $sigs[$k] = [ordered]@{ count = $c; firstOffsets = @($scan[$k]) } }
  }
  [ordered]@{
    path            = $x.Rel
    size            = $d.Length
    lastWriteUtc    = $x.F.LastWriteTimeUtc.ToString('o')
    head32Hex       = $hex
    head32Ascii     = $ascii
    entropy         = [Math]::Round([TiaInv]::Entropy($d, 0, $d.Length), 4)
    blockEntropy    = [ordered]@{ blockSize = $BlockSize; blocks = [int]$be[3]; min = [Math]::Round($be[0], 4); max = [Math]::Round($be[1], 4); mean = [Math]::Round($be[2], 4) }
    container       = [ordered]@{
      zip  = $isZip; zipEntries = $zipEntries
      gzip = $isGz; gzipDecompressedSize = if ($isGz) { [TiaInv]::TryGunzip($d) } else { $null }
      zlib = $isZl; zlibDecompressedSize = if ($isZl) { [TiaInv]::TryInflate($d, 2) } else { $null }
      rawDeflateAt0Size = $(if (-not ($isZip -or $isGz -or $isZl) -and $d.Length) { $r = [TiaInv]::TryInflate($d, 0); if ($r -gt 0) { $r } else { $null } } else { $null })
    }
    embeddedSignatures = $sigs
  }
}

$result = [ordered]@{
  generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
  root         = $rootFull
  fileCount    = @($items).Count
  totalSize    = ($items | ForEach-Object { $_.size } | Measure-Object -Sum).Sum
  files        = @($items)
}
$json = $result | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText((Join-Path (Resolve-Path (Split-Path $Out -Parent)).Path (Split-Path $Out -Leaf)), $json, (New-Object Text.UTF8Encoding($false)))
"wrote $Out ($($result.fileCount) files)"
