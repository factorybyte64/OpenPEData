using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>Integrity check of a TIA Portal V21 project without TIA and without any secret (option --verify): header hash,
/// record hashes, trailing bytes, and per $$COMMIT$ record the chain hash and the RSA PKCS#1 v1.5 / SHA-256 signature over
/// the first 128 bytes against the public key of the key record (CAPI PUBLICKEYBLOB "RSA1").</summary>
internal static class Verify
{
    public const uint Commit = 0x00070014, Close = 0x00070015, Key = 0x00070016;
    /// <summary>DER prefix of a SHA-256 DigestInfo (PKCS#1 v1.5); a span over constant data, so nobody can change it.</summary>
    public static ReadOnlySpan<byte> Sha256DigestInfo => [0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20];

    static byte[] Sha(byte[] b) => SHA256.HashData(b);

    public static (BigInteger N, long E, long Bits) PublicKey(PlfRecord rec)
    {
        var d = rec.Data;
        var i = Py.Find(d, "RSA1"u8.ToArray());
        if (i < 0)
            throw new PyValueError("subsection not found");
        var bitlen = Bin.U32(d, i + 4);
        var e = Bin.U32(d, i + 8);
        var n = new BigInteger(Py.Slice(d, i + 12, i + 12 + bitlen / 8), isUnsigned: true, isBigEndian: false);
        return (n, e, bitlen);
    }

    /// <summary>pow(int.from_bytes(block, "big"), e, n).to_bytes(256, "big").</summary>
    public static byte[] RsaOpen(byte[] block, BigInteger n, long e)
    {
        var c = new BigInteger(block, isUnsigned: true, isBigEndian: true);
        var m = BigInteger.ModPow(c, e, n);
        var bytes = m.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length > 256)
            throw new PyError("OverflowError", "int too big to convert");
        var r = new byte[256];
        bytes.CopyTo(r, 256 - bytes.Length);
        return r;
    }

    public static PyDict VerifyPlf(byte[] buf, string name = "PEData.plf")
    {
        var res = PyDict.Of("file", name, "size", (long)buf.Length, "ok", true, "problems", new List<object?>());
        void Problem(string msg)
        {
            res["ok"] = false;
            ((List<object?>)res["problems"]!).Add(msg);
        }
        if (buf.Length < Plf.RecordsStart)
        {
            Problem("file shorter than the plf header");
            return res;
        }
        var headerOk = Sha(buf[..65]).AsSpan().SequenceEqual(buf.AsSpan(65, 32)) && buf[97] == 0xFF;
        res["headerHashOk"] = headerOk;
        if (!headerOk)
            Problem("header hash mismatch");
        var recs = Plf.ReadRecords(buf);
        res["records"] = (long)recs.Count;
        var bad = recs.Where(r => !r.HashOk).Select(r => (long)r.Offset).ToList();
        res["recordHashErrors"] = bad.Take(20).Cast<object?>().ToList();
        if (bad.Count > 0)
            Problem($"{bad.Count} record hash mismatches (first at offset {bad[0]})");
        var endsFf = recs.Count(r => r.Data[^1] != 0xFF);
        if (endsFf > 0)
            Problem($"{endsFf} records without terminating 0xFF");
        long end = recs.Count > 0 ? recs[^1].Offset + recs[^1].Length + 32 : Plf.RecordsStart;
        res["trailingBytes"] = buf.Length - end;
        if (buf.Length != end)
            Problem($"{buf.Length - end} bytes behind the last record");
        var keys = recs.Where(r => r.TypeId == Key).ToList();
        BigInteger? n = null;
        long e = 0;
        if (keys.Count > 0)
        {
            var (kn, ke, bitlen) = PublicKey(keys[^1]);
            n = kn;
            e = ke;
            var hex = Py.Hex(kn);
            res["key"] = PyDict.Of("record", keys[^1].Key, "bits", bitlen, "exponent", ke, "modulusPrefix", hex[..Math.Min(16, hex.Length)]);
        }
        var chain = buf[65..97];
        var pending = new List<byte[]>();
        var commits = new List<object?>();
        foreach (var r in recs)
        {
            if (r.TypeId == Commit)
            {
                var d = r.Data;
                var ticks = Bin.U64(d, 68);
                var prevOff = Bin.I32(d, 78);
                var field = Py.Slice(d, 96, 128);
                var chainOk = field.AsSpan().SequenceEqual(Sha(Py.Concat([chain, .. pending])));
                var c = PyDict.Of("offset", (long)r.Offset, "ticks", Py.Int(ticks), "recordsSince", (long)pending.Count,
                    "prevCommitOffset", (long)prevOff, "chainOk", chainOk);
                if (n is { } nn && !nn.IsZero)
                {
                    var m = RsaOpen(Py.Slice(d, 128, 384), nn, e);
                    var z = Py.Find(m, [0], 2);
                    c["signatureOk"] = Py.Slice(m, 0, 2) is [0, 1] && Py.Slice(m, 2, z).Distinct().SequenceEqual(new byte[] { 0xFF }) && z >= 10
                                       && Py.Slice(m, z + 1).AsSpan().SequenceEqual(Py.Concat(Sha256DigestInfo.ToArray(), Sha(Py.Slice(d, 0, 128))));
                }
                else
                    c["signatureOk"] = false;
                if (!((bool)c["chainOk"]! && (bool)c["signatureOk"]!))
                    Problem($"commit at offset {r.Offset}: chain {Py.Str(c["chainOk"])}, signature {Py.Str(c["signatureOk"])}");
                commits.Add(c);
                chain = field;
                pending = [];
            }
            else if (r.TypeId != Close)
                pending.Add(buf.AsSpan(r.Offset + r.Length, 32).ToArray());
        }
        res["commits"] = commits;
        res["uncommittedRecords"] = (long)pending.Count;
        if (commits.Count == 0)
            Problem("no commit record");
        return res;
    }

    /// <summary>Report for a project folder, .ap21 or .zap21 (all plf parts).</summary>
    public static PyDict VerifyProject(string path)
    {
        var parts = ProjectFiles.ReadPlf(path);
        var files = parts.Select(p => VerifyPlf(p.Data, p.Name)).ToList();
        var rep = PyDict.Of("path", path, "ok", files.All(f => (bool)f["ok"]!), "files", files.Cast<object?>().ToList());
        var idx = Pidx.CheckProject(path);
        if (idx is not null)
            rep["idx"] = idx;
        var stores = Pidx.ExternalStores(path);
        if (stores.Count > 0)
            rep["externalStores"] = stores;
        return rep;
    }

    public static string TextReport(PyDict rep)
    {
        var o = new List<string> { $"Verify {Py.Str(rep["path"])}: {((bool)rep["ok"]! ? "OK" : "FAILED")}" };
        foreach (var f in ((List<object?>)rep["files"]!).Cast<PyDict>())
        {
            var rhe = f.Get("recordHashErrors") as List<object?>;
            o.Add($"  {Py.Str(f["file"])}: {Py.Str(f.Get("records", 0L))} records, header hash {(Py.Bool(f.Get("headerHashOk")) ? "ok" : "BAD")}, record hashes {(!Py.Bool(rhe) ? "ok" : $"BAD ({rhe!.Count})")}, trailing bytes {Py.Str(f.Get("trailingBytes", 0L))}");
            if (f.Get("key") is PyDict k && k.Count > 0)
                o.Add($"    key {Py.Str(k["record"])} RSA-{Py.Str(k["bits"])} e={Py.Str(k["exponent"])} modulus {Py.Str(k["modulusPrefix"])}...");
            foreach (var c in ((f.Get("commits") as List<object?>) ?? []).Cast<PyDict>())
                o.Add($"    commit @{Py.Str(c["offset"])}: {Py.Str(c["recordsSince"])} records, chain {((bool)c["chainOk"]! ? "ok" : "BAD")}, signature {((bool)c["signatureOk"]! ? "ok" : "BAD")}, previous commit @{Py.Str(c["prevCommitOffset"])}");
            if (Py.Bool(f.Get("uncommittedRecords")))
                o.Add($"    {Py.Str(f["uncommittedRecords"])} records behind the last commit");
            foreach (var p in (List<object?>)f["problems"]!)
                o.Add("    PROBLEM: " + Py.Str(p));
        }
        if (rep.Get("idx") is PyDict ix && ix.Count > 0)
        {
            o.Add($"  PEData.idx: {((bool)ix["ok"]! ? "consistent with the plf" : "INCONSISTENT")} ({Py.Str(ix["entries"])} entries for {Py.Str(ix["records"])} plf records, {Py.Str(ix["commitCopies"])} commit copies)");
            foreach (var p in ((List<object?>)ix["problems"]!).Take(5))
                o.Add("    idx: " + Py.Str(p));
        }
        if (rep.Get("externalStores") is PyDict es)
            foreach (var (name, stv) in es)
            {
                var st = (PyDict)stv!;
                o.Add($"  {Py.Str(name)} savepoint: plf {Py.Str(st["plf"])}, file {Py.Str(st["file"])} ({(Py.Eq(st["plf"], st["file"]) ? "equal" : "differs")})");
            }
        return string.Join("\n", o);
    }
}

/// <summary>System\PEData.idx: index of the record log (a derived cache, the plf alone is authoritative). 98-byte header
/// like the plf; 38-byte entries (key, record GUID, u8 0, u32 plf offset, i32 class code, u8 flags); a 421-byte commit
/// copy behind the entry of every commit record; 38-byte session marks (key 0015:0007:20).</summary>
internal static class Pidx
{
    const int Entry = 38;
    const int CommitCopy = 421;
    const uint SessionKey = 0x0007_0015;

    static PyDict ReadEntry(byte[] b, int i)
    {
        var lo = Bin.U16(b, i);
        var hi = Bin.U16(b, i + 2);
        var oid = Bin.U64(b, i + 4);
        var guid = Py.Slice(b, i + 12, i + 28);
        Bin.U8(b, i + 28);
        var off = Bin.U32(b, i + 29);
        var cls = Bin.I32(b, i + 33);
        var fl = Bin.U8(b, i + 37);
        return PyDict.Of("key", Plf.MakeKey(lo, hi, oid), "guid", Py.HexStr(guid), "offset", (long)off, "class", (long)cls,
            "flags", (long)fl, "_typeId", (long)(((uint)hi << 16) | lo), "_pos", (long)i);
    }

    /// <summary>Entries (log order), commit copies and session marks of an idx file.</summary>
    public static PyDict Parse(byte[] buf)
    {
        var res = PyDict.Of("size", (long)buf.Length,
            "headerOk", buf.Length >= 98 && SHA256.HashData(Py.Slice(buf, 0, 65)).AsSpan().SequenceEqual(Py.Slice(buf, 65, 97)) && buf[97] == 0xFF,
            "magic", (long)Bin.U32(buf, 0), "guid", Py.HexStr(Py.Slice(buf, 8, 24)), "entries", new List<object?>(),
            "commits", new List<object?>(), "sessions", new List<object?>(), "unparsed", 0L);
        var entries = (List<object?>)res["entries"]!;
        var commits = (List<object?>)res["commits"]!;
        var sessions = (List<object?>)res["sessions"]!;
        var i = Plf.RecordsStart;
        var n = buf.Length;
        while (i < n)
        {
            if (i + 4 <= n && Py.Slice(buf, i + 4, i + 6) is [0x14, 0] && Bin.U32(buf, i) == CommitCopy)
            {
                commits.Add(PyDict.Of("pos", (long)i, "key", Plf.MakeKey(Bin.U16(buf, i + 4), Bin.U16(buf, i + 6), Bin.U64(buf, i + 8))));
                i += CommitCopy;
            }
            else if (i + Entry <= n && Py.Slice(buf, i, i + 4) is [0x15, 0, 7, 0])
            {
                sessions.Add(ReadEntry(buf, i));
                i += Entry;
            }
            else if (i + Entry <= n)
            {
                entries.Add(ReadEntry(buf, i));
                i += Entry;
            }
            else
            {
                res["unparsed"] = (long)(n - i);
                break;
            }
        }
        return res;
    }

    /// <summary>Compares the idx with the plf: {"ok", "problems", counts}.</summary>
    public static PyDict Check(byte[] idxBuf, byte[] plfBuf)
    {
        var res = PyDict.Of("ok", true, "problems", new List<object?>());
        var problems = (List<object?>)res["problems"]!;
        void Problem(string msg)
        {
            res["ok"] = false;
            if (problems.Count < 20)
                problems.Add(msg);
        }
        var ix = Parse(idxBuf);
        var entries = ((List<object?>)ix["entries"]!).Cast<PyDict>().ToList();
        var ixCommits = (List<object?>)ix["commits"]!;
        var recs = Plf.ReadRecords(plfBuf, false).Where(r => r.TypeId != SessionKey).ToList();
        res["entries"] = (long)entries.Count;
        res["records"] = (long)recs.Count;
        res["commitCopies"] = (long)ixCommits.Count;
        res["sessionMarks"] = (long)((List<object?>)ix["sessions"]!).Count;
        if (!(bool)ix["headerOk"]!)
            Problem("idx header hash mismatch");
        if (Py.Bool(ix["unparsed"]))
            Problem($"{Py.Str(ix["unparsed"])} unparsed bytes at the end");
        if (entries.Count > recs.Count)
            Problem($"{entries.Count} entries for {recs.Count} plf records");
        var live = entries.Where(e => ((long)e["flags"]! & 4) == 0).ToList();
        var dead = new Dictionary<string, long>();
        foreach (var e in entries.Where(e => ((long)e["flags"]! & 4) != 0))
            dead[(string)e["key"]!] = dead.GetValueOrDefault((string)e["key"]!) + 1;
        var byOff = new Dictionary<long, PyDict>();
        foreach (var e in live)
            byOff[(long)e["offset"]!] = e;
        if (byOff.Count != live.Count)
            Problem("duplicate offsets in the entries");
        foreach (var r in recs)
        {
            if (ObjectRecord.IsTombstone(r))
            {
                if (dead.GetValueOrDefault(r.Key) == 0)
                    Problem($"no deletion entry for tombstone at {r.Offset} ({r.Key})");
                else
                    dead[r.Key] -= 1;
                continue;
            }
            long cls = Bin.I32(r.Data, 16);
            if (r.IsStore && cls == 0 && r.TypeId != 0x00070014)
                cls = -1;
            if (!byOff.TryGetValue(r.Offset, out var e))
            {
                if (r.IsStore)
                    res["unindexedStore"] = Py.Long(res.Get("unindexedStore", 0L)) + 1;
                else
                    Problem($"no entry for plf record at {r.Offset} ({r.Key})");
            }
            else if ((string)e["key"]! != r.Key || (string)e["guid"]! != Py.HexStr(Py.Slice(r.Data, 28, 44)) ||
                     ((long)e["class"]! != cls && (long)e["class"]! != Bin.I32(r.Data, 16)))
                Problem($"entry at {Py.Str(e["_pos"])} differs from plf record at {r.Offset} ({r.Key})");
        }
        var deadSum = dead.Values.Sum();
        if (deadSum != 0)
            Problem($"{deadSum} deletion entries without tombstone");
        var matched = recs.Count(r => !ObjectRecord.IsTombstone(r) && byOff.ContainsKey(r.Offset));
        var nlive = recs.Count(r => !ObjectRecord.IsTombstone(r));
        res["stale"] = nlive != 0 && matched < nlive / 2;
        var ncommit = recs.Count(r => r.TypeId == 0x00070014);
        if (ixCommits.Count != ncommit)
            Problem($"{ixCommits.Count} commit copies for {ncommit} commit records");
        res["commitCopySignatures"] = CopySignatures(idxBuf, ixCommits, recs);
        return res;
    }

    static PyDict CopySignatures(byte[] idxBuf, List<object?> commits, List<PlfRecord> recs)
    {
        var keys = recs.Where(r => r.TypeId == Verify.Key).ToList();
        var outd = PyDict.Of("total", (long)commits.Count, "padded", 0L);
        if (keys.Count == 0 || commits.Count == 0)
            return outd;
        var (n, e, _) = Verify.PublicKey(keys[^1]);
        var di = Verify.Sha256DigestInfo;
        foreach (var c in commits.Cast<PyDict>())
        {
            var pos = (long)c["pos"]!;
            var blk = Py.Slice(idxBuf, pos + 160, pos + 416);
            if (blk.Length < 256)
                continue;
            var m = Verify.RsaOpen(blk, n, e);
            var z = Py.Find(m, [0], 2);
            if (Py.Slice(m, 0, 2) is [0, 1] && z >= 10 && Py.Slice(m, 2, z).Distinct().SequenceEqual(new byte[] { 0xFF }) &&
                Py.Slice(m, z + 1, z + 1 + di.Length).AsSpan().SequenceEqual(di) && m.Length - z - 1 == di.Length + 32)
                outd["padded"] = (long)outd["padded"]! + 1;
        }
        return outd;
    }

    /// <summary>idx check of a project folder or .ap21; null for a .zap21 and without idx/plf.</summary>
    public static PyDict? CheckProject(string path)
    {
        if (ProjectFiles.IsProjectArchive(path))
            return null;
        var idx = ProjectFiles.Member(path, "System/PEData.idx");
        var plf = ProjectFiles.Member(path, "System/PEData.plf");
        if (idx is null || plf is null)
            return null;
        return Check(idx, plf);
    }

    static readonly Regex Printable4 = new("[ -~]{4,}", RegexOptions.CultureInvariant);
    static readonly Regex SegName = new(@"\Asegments_\w+\z", RegexOptions.CultureInvariant);
    static readonly Regex SegZipName = new(@"\AIM/SearchIndex/segments_\w+\z", RegexOptions.CultureInvariant);

    /// <summary>Savepoints the plf (project history record 1006:0001:*) expects for the stores outside the plf versus the ones
    /// found: {"xref": {"plf", "file"}, "searchIndex": {"plf", "file"}}.</summary>
    public static PyDict ExternalStores(string path)
    {
        var plfBuf = ProjectFiles.Member(path, "System/PEData.plf");
        var outd = new PyDict();
        if (plfBuf is null)
            return outd;
        var recs = Plf.ReadRecords(plfBuf, false).Where(r => r.Key.StartsWith("1006:0001:", StringComparison.Ordinal) &&
            (Py.Contains(r.Data, "XRef%"u8.ToArray()) || Py.Contains(r.Data, "SaveCommit_"u8.ToArray()))).ToList();
        var txt = recs.Count > 0 ? string.Join(" ", Printable4.Matches(Py.Latin1(recs[^1].Data)).Select(x => x.Value)) : "";
        var m = Regex.Match(txt, "XRef%([0-9a-f-]{36})");
        var s = Regex.Match(txt, "SaveCommit_([0-9a-f-]{36})");
        var db = ProjectFiles.Member(path, "XRef/XRef.db");
        object? found = null;
        if (db is not null && db.Length > 0)
        {
            var row = new SqliteReader(db).Select("info", "savepointID").FirstOrDefault();
            found = row?[0];
        }
        outd["xref"] = PyDict.Of("plf", m.Success ? m.Groups[1].Value : null, "file", found);
        byte[]? seg = null;
        if (ProjectFiles.IsProjectArchive(path))
        {
            using var z = ProjectFiles.OpenZip(path);
            var names = Py.Sorted(z.Entries.Select(e => e.FullName).Where(x => SegZipName.IsMatch(x)));
            seg = names.Count > 0 ? ProjectFiles.ReadEntry(z.Entries.Last(e => e.FullName == names[^1])) : null;
        }
        else
        {
            var bas = ProjectFiles.IsProjectFile(path) ? ProjectFiles.DirOf(path) : path;
            var d = Path.Combine(bas, "IM", "SearchIndex");
            var names = Py.Sorted((Directory.Exists(d) ? Directory.EnumerateFileSystemEntries(d).Select(x => Path.GetFileName(x)) : [])
                .Where(x => SegName.IsMatch(x)));
            seg = names.Count > 0 ? File.ReadAllBytes(Path.Combine(d, names[^1])) : null;
        }
        var sm = seg is not null && seg.Length > 0 ? Regex.Match(Py.Latin1(seg), "SaveCommit_([0-9a-f-]{36})") : null;
        outd["searchIndex"] = PyDict.Of("plf", s.Success ? s.Groups[1].Value : null,
            "file", sm is { Success: true } ? Encoding.UTF8.GetString(Py.Latin1Bytes(sm.Groups[1].Value)) : null);
        return outd;
    }
}
