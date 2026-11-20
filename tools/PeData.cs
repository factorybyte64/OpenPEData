// PEData.plf / PEData.idx structure parser (exploratory). Loaded via Add-Type together with TiaAnalysis.cs.
//
// Confirmed (000_empty, 000_empty2, zap21 state):
//   plf = header (65 bytes) + SHA-256(header) + 0xFF, then records from offset 98 to EOF:
//         [u32 len][len-4 bytes, last byte 0xFF][SHA-256 over the len bytes]
//   idx = same 98-byte header layout (first u32 0x80 instead of 0x40), then index entries of 38 bytes
//         [key 12][guid 16][u8][u32 plf offset][i32][u8], interleaved with unhashed commit records.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public static class PeData {
  public const int RecordsStart = 98;

  public class Record {
    public long Offset; public uint Length; public bool HashOk;
    public ushort KeyA; public ushort KeyB; public ulong KeyId;
    public string Key;
  }

  public static string KeyString(byte[] d, long o) {
    return BitConverter.ToUInt16(d, (int)o).ToString("x4") + ":" + BitConverter.ToUInt16(d, (int)o + 2).ToString("x4") + ":" +
           BitConverter.ToUInt64(d, (int)o + 4).ToString("x");
  }

  public static List<Record> ReadPlf(byte[] d) {
    var res = new List<Record>();
    var sha = SHA256.Create();
    long o = RecordsStart;
    while (o + 4 <= d.Length) {
      uint len = BitConverter.ToUInt32(d, (int)o);
      if (len < 17 || o + len + 32 > d.Length) break;
      var h = sha.ComputeHash(d, (int)o, (int)len);
      bool ok = true;
      for (int i = 0; i < 32; i++) if (h[i] != d[o + len + i]) { ok = false; break; }
      res.Add(new Record { Offset = o, Length = len, HashOk = ok,
        KeyA = BitConverter.ToUInt16(d, (int)o + 4), KeyB = BitConverter.ToUInt16(d, (int)o + 6),
        KeyId = BitConverter.ToUInt64(d, (int)o + 8), Key = KeyString(d, o + 4) });
      o += len + 32;
    }
    return res;
  }

  public class IdxItem {
    public long Offset; public string Kind; // "entry" or "record" or "unknown"
    public string Key; public string Guid; public byte B28; public uint PlfOffset; public int I32; public byte B37;
    public uint RecLength; public string Text;
  }

  // Walks the idx: an item is an entry if its u32 at +29 points to a plf record start with the same key;
  // otherwise, if the u32 at +0 is a plausible length whose last byte is 0xFF, it is an (unhashed) record.
  public static List<IdxItem> ReadIdx(byte[] idx, HashSet<long> plfStarts, Dictionary<long, string> plfKeys) {
    var res = new List<IdxItem>();
    long p = RecordsStart;
    while (p < idx.Length) {
      if (p + 38 <= idx.Length) {
        uint off = BitConverter.ToUInt32(idx, (int)p + 29);
        string key = KeyString(idx, p);
        string pk;
        if (plfKeys.TryGetValue(off, out pk) && pk == key) {
          res.Add(new IdxItem { Offset = p, Kind = "entry", Key = key, Guid = new Guid(Sub(idx, p + 12, 16)).ToString(),
            B28 = idx[p + 28], PlfOffset = off, I32 = BitConverter.ToInt32(idx, (int)p + 33), B37 = idx[p + 37] });
          p += 38; continue;
        }
      }
      if (p + 4 <= idx.Length) {
        uint len = BitConverter.ToUInt32(idx, (int)p);
        if (len >= 17 && p + len <= idx.Length && idx[p + len - 1] == 0xFF) {
          res.Add(new IdxItem { Offset = p, Kind = "record", RecLength = len, Key = KeyString(idx, p + 4),
            Text = Ascii(idx, p, (int)len) });
          p += len; continue;
        }
      }
      res.Add(new IdxItem { Offset = p, Kind = "unknown", Text = TiaAnalysis.Hex(idx, p, 64) });
      break;
    }
    return res;
  }

  // Type id of a record key as used in the metamodel XML: (u16 at +6 << 16) | u16 at +4
  public static uint TypeId(Record r) { return ((uint)r.KeyB << 16) | r.KeyA; }

  // Last (= current) version of each key in log order.
  public static Dictionary<string, Record> Latest(List<Record> recs) {
    var d = new Dictionary<string, Record>();
    foreach (var r in recs) d[r.Key] = r;
    return d;
  }

  // .NET DateTime ticks between 2000 and 2100 (Kind bits in the top 2 bits ignored).
  public static bool IsTicks(byte[] d, long o) {
    if (o + 8 > d.Length) return false;
    long v = BitConverter.ToInt64(d, (int)o) & 0x3FFFFFFFFFFFFFFFL;
    return v > 630822816000000000L && v < 662380416000000000L;
  }

  // Byte-wise diff of two records' payloads with noise masked: object GUID at +28..+43 (optional),
  // and any 8-byte window that holds DateTime ticks in both records.
  public static List<TiaAnalysis.Region> NoiseFreeDiff(byte[] a, byte[] b, bool maskGuid, int mergeGap) {
    int n = Math.Min(a.Length, b.Length);
    var mask = new bool[n];
    if (maskGuid) for (int i = 28; i < 44 && i < n; i++) mask[i] = true;
    for (int i = 0; i + 8 <= n; i++)
      if (IsTicks(a, i) && IsTicks(b, i)) for (int k = 0; k < 8; k++) mask[i + k] = true;
    var res = new List<TiaAnalysis.Region>();
    int p = 0;
    while (p < n) {
      if (mask[p] || a[p] == b[p]) { p++; continue; }
      int s = p, last = p;
      while (p < n) {
        if (!mask[p] && a[p] != b[p]) { last = p; p++; continue; }
        if (p - last > mergeGap) break;
        p++;
      }
      res.Add(new TiaAnalysis.Region { Start = s, Length = last - s + 1 });
    }
    return res;
  }

  // Metamodel record 0000:0007:1: u32 compressed length at +44, zlib stream at +48 (UTF-8 XML).
  public static byte[] MetamodelXml(byte[] plf, Record r) {
    uint clen = BitConverter.ToUInt32(plf, (int)r.Offset + 44);
    return TiaAnalysis.Inflate(plf, r.Offset + 50, clen - 2, 1L << 30);
  }

  // id -> "Namespace.Name" for every element with an id attribute (types get namespace-qualified names).
  public static Dictionary<uint, string> MetaNames(byte[] xml) {
    var res = new Dictionary<uint, string>();
    var doc = new System.Xml.XmlDocument();
    using (var ms = new System.IO.MemoryStream(xml)) doc.Load(ms);
    foreach (System.Xml.XmlElement e in doc.SelectNodes("//*[@id]")) {
      if (e.LocalName == "Package") continue;
      string ns = null, owner = null;
      for (var p = e.ParentNode as System.Xml.XmlElement; p != null; p = p.ParentNode as System.Xml.XmlElement) {
        if (ns == null && p.LocalName == "Namespace") ns = p.GetAttribute("name");
        if (owner == null && p.LocalName != "Namespace" && p.LocalName != "Package" && p.HasAttribute("id")) owner = p.GetAttribute("name");
      }
      uint id;
      if (!uint.TryParse(e.GetAttribute("id").Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out id)) continue;
      string name = owner != null ? owner + "." + e.GetAttribute("name") : (ns != null ? ns + "." : "") + e.GetAttribute("name");
      res[id] = name + " [" + e.LocalName + "]";
    }
    return res;
  }

  public static byte[] Sub(byte[] d, long o, int n) { var r = new byte[n]; Array.Copy(d, o, r, 0, n); return r; }

  public static string Ascii(byte[] d, long o, int n) {
    var sb = new System.Text.StringBuilder();
    var cur = new System.Text.StringBuilder();
    for (long i = o; i < o + n && i < d.Length; i++) {
      if (d[i] >= 32 && d[i] < 127) cur.Append((char)d[i]);
      else { if (cur.Length >= 4) { if (sb.Length > 0) sb.Append('|'); sb.Append(cur); } cur.Clear(); }
    }
    if (cur.Length >= 4) { if (sb.Length > 0) sb.Append('|'); sb.Append(cur); }
    return sb.ToString();
  }
}
