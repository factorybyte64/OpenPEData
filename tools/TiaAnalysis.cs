// Analysis helpers for TIA project files. Loaded from PowerShell via Add-Type -Path.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

public static class TiaAnalysis {
  public class Region { public long Start; public long Length; }

  // Byte ranges where a and b differ (over the common length). Gaps <= mergeGap equal bytes are merged.
  public static List<Region> DiffRegions(byte[] a, byte[] b, int mergeGap) {
    var res = new List<Region>();
    long n = Math.Min(a.Length, b.Length);
    long i = 0;
    while (i < n) {
      if (a[i] == b[i]) { i++; continue; }
      long s = i, last = i;
      while (i < n) {
        if (a[i] != b[i]) { last = i; i++; continue; }
        if (i - last > mergeGap) break;
        i++;
      }
      res.Add(new Region { Start = s, Length = last - s + 1 });
    }
    return res;
  }

  public static long CommonPrefix(byte[] a, byte[] b) {
    long n = Math.Min(a.Length, b.Length), i = 0;
    while (i < n && a[i] == b[i]) i++;
    return i;
  }

  public static string Hex(byte[] d, long off, int len) {
    var sb = new StringBuilder();
    long end = Math.Min(d.Length, off + len);
    for (long i = Math.Max(0, off); i < end; i++) { if (sb.Length > 0) sb.Append(' '); sb.Append(d[i].ToString("x2")); }
    return sb.ToString();
  }

  public static uint Adler32(byte[] d) {
    uint a = 1, b = 0;
    foreach (byte x in d) { a = (a + x) % 65521; b = (b + a) % 65521; }
    return (b << 16) | a;
  }

  public static byte[] Inflate(byte[] d, long off, long len, long cap) {
    try {
      using (var ms = new MemoryStream(d, (int)off, (int)len))
      using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
      using (var o = new MemoryStream()) {
        var buf = new byte[65536]; int r;
        while ((r = ds.Read(buf, 0, buf.Length)) > 0) { o.Write(buf, 0, r); if (o.Length > cap) return null; }
        return o.ToArray();
      }
    } catch { return null; }
  }

  public class ZStream {
    public long Offset; public long CompressedLength = -1; public long DecompressedLength; public bool AdlerOk;
    public byte[] Data;
  }

  // Finds zlib streams: header 78 01/5E/9C/DA, inflate succeeds with >= minOut bytes.
  // Compressed length is determined by locating the big-endian Adler-32 trailer.
  public static List<ZStream> FindZlib(byte[] d, int minOut) {
    var res = new List<ZStream>();
    long i = 0;
    while (i + 2 < d.Length) {
      byte f = d[i + 1];
      if (d[i] == 0x78 && (f == 0x01 || f == 0x5E || f == 0x9C || f == 0xDA)) {
        var outp = Inflate(d, i + 2, d.Length - i - 2, 1L << 28);
        if (outp != null && outp.Length >= minOut) {
          var z = new ZStream { Offset = i, DecompressedLength = outp.Length, Data = outp };
          uint ad = Adler32(outp);
          byte[] t = { (byte)(ad >> 24), (byte)(ad >> 16), (byte)(ad >> 8), (byte)ad };
          for (long p = i + 2; p + 4 <= d.Length; p++) {
            if (d[p] == t[0] && d[p + 1] == t[1] && d[p + 2] == t[2] && d[p + 3] == t[3]) {
              var chk = Inflate(d, i + 2, p - i - 2, 1L << 28);
              if (chk != null && chk.Length == outp.Length) { z.CompressedLength = p + 4 - i; z.AdlerOk = true; break; }
            }
          }
          res.Add(z);
          if (z.CompressedLength > 0) { i += z.CompressedLength; continue; }
        }
      }
      i++;
    }
    return res;
  }

  // Printable ASCII strings (minLen) and UTF-16LE strings, with counts, top N.
  public static List<KeyValuePair<string, int>> Strings(byte[] d, int minLen, bool utf16, int top) {
    var cnt = new Dictionary<string, int>();
    var sb = new StringBuilder();
    int step = utf16 ? 2 : 1;
    for (int start = 0; start < step; start++) {
      for (long i = start; i + step - 1 < d.Length; i += step) {
        bool ok = utf16 ? (d[i + 1] == 0 && d[i] >= 32 && d[i] < 127) : (d[i] >= 32 && d[i] < 127);
        if (ok) { sb.Append((char)d[i]); continue; }
        if (sb.Length >= minLen) { var s = sb.ToString(); int c; cnt.TryGetValue(s, out c); cnt[s] = c + 1; }
        sb.Clear();
      }
      if (sb.Length >= minLen) { var s = sb.ToString(); int c; cnt.TryGetValue(s, out c); cnt[s] = c + 1; }
      sb.Clear();
      if (!utf16) break;
    }
    var l = new List<KeyValuePair<string, int>>(cnt);
    l.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : string.CompareOrdinal(x.Key, y.Key));
    if (l.Count > top) l.RemoveRange(top, l.Count - top);
    return l;
  }

  // Ratio of printable ASCII, and of UTF-16LE ASCII pairs.
  public static double[] TextRatios(byte[] d) {
    if (d.Length == 0) return new double[] { 0, 0 };
    long a = 0, u = 0;
    for (long i = 0; i < d.Length; i++) {
      byte x = d[i];
      if ((x >= 32 && x < 127) || x == 9 || x == 10 || x == 13) a++;
      if ((i & 1) == 1 && x == 0 && d[i - 1] >= 9 && d[i - 1] < 127) u++;
    }
    return new double[] { (double)a / d.Length, 2.0 * u / d.Length };
  }

  // Finds all positions where value appears as little-endian u32 or u64.
  public static List<long> FindLE(byte[] d, long value, int width) {
    var r = new List<long>();
    for (long i = 0; i + width <= d.Length; i++) {
      long v = width == 4 ? BitConverter.ToUInt32(d, (int)i) : BitConverter.ToInt64(d, (int)i);
      if (v == value) r.Add(i);
    }
    return r;
  }
}
