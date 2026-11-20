using System.Security.Cryptography;
using System.Text;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Python semantics against golden.json (dotnet\tools\make_golden.py, generated with the Python of the parser):
/// float repr, round, repr, json.dumps, text decoding, zlib, SQLite reading, datetime ticks.</summary>
public static class GoldenTests
{
    static PyDict? golden;

    static PyDict G => golden ??= (PyDict)PyJson.Loads(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden.json")))!;

    static List<PyDict> Cases(string name) => ((List<object?>)G[name]!).Cast<PyDict>().ToList();

    static double D(object? bits) => BitConverter.Int64BitsToDouble(Py.Long(bits));

    [Test]
    public static void FloatRepr()
    {
        var c = new Check("float repr / json");
        foreach (var e in Cases("floats"))
        {
            var d = D(e["bits"]);
            c.Eq(e["repr"], Py.FloatRepr(d), $"repr {e["repr"]}");
            c.Eq(e["json"], PyJson.Dumps(d), $"json {e["json"]}");
        }
        c.Done();
    }

    [Test]
    public static void Round6()
    {
        var c = new Check("round(x, 6)");
        foreach (var e in Cases("rounds"))
            c.Eq(e["result"], Py.FloatRepr(Py.Round(D(e["bits"]), (int)Py.Long(e["n"]))), $"round {Py.FloatRepr(D(e["bits"]))}");
        c.Done();
    }

    [Test]
    public static void Repr()
    {
        var c = new Check("repr");
        foreach (var e in Cases("reprs"))
        {
            if (e.ContainsKey("s"))
                c.Eq(e["repr"], Py.ReprStr((string)e["s"]!), "repr " + e["repr"]);
            else
                c.Eq(e["repr"], Py.ReprBytes(Convert.FromBase64String((string)e["b"]!)), "repr " + e["repr"]);
        }
        c.Done();
    }

    [Test]
    public static void JsonDumps()
    {
        var c = new Check("json.dumps");
        foreach (var e in Cases("jsons"))
        {
            if (e.ContainsKey("float"))
            {
                c.Eq(e["flat"], PyJson.Dumps(Py.L(double.NaN, double.PositiveInfinity, double.NegativeInfinity)), "non-finite floats");
                continue;
            }
            var v = e["value"];
            c.Eq(e["indent2"], PyJson.Dumps(v, 2, false), "indent=2 " + e["flat"]);
            c.Eq(e["indent2ascii"], PyJson.Dumps(v, 2, true), "indent=2 ascii " + e["flat"]);
            c.Eq(e["flat"], PyJson.Dumps(v, null, false), "flat " + e["flat"]);
        }
        c.Eq("\"hex:00ff\"", PyJson.Dumps(new byte[] { 0, 255 }, null, false, PyJson.Fallback.Hex), "bytes as hex (OpenPEData.py default)");
        c.Eq("[\"b'\\\\x00'\"]", PyJson.Dumps(Py.L(new byte[] { 0 }), null, false, PyJson.Fallback.Str), "default=str");
        c.Eq("{\"1\": 4, \"null\": 3, \"false\": 5}", PyJson.Dumps(PyDict.Of(1L, 2L, null, 3L, true, 4L, false, 5L), null, false), "key conversion (True == 1)");
        c.Eq("[1, \"x\"]", PyJson.Dumps(Py.T(1L, "x"), null, false), "tuple as array");
        c.Done();
    }

    [Test]
    public static void TextDecoding()
    {
        var c = new Check("bytes.decode");
        foreach (var e in Cases("decodes"))
        {
            var b = Convert.FromBase64String((string)e["b"]!);
            var hex = Py.HexStr(b);
            c.Eq(e["utf8"], Py.Utf8(b), "utf-8 replace " + hex);
            string? strict;
            try
            {
                strict = Py.Utf8Exact(b);
            }
            catch (PyUnicodeDecodeError)
            {
                strict = null;
            }
            c.Eq(e["utf8strict"], strict, "utf-8 strict " + hex);
            c.Eq(e["utf8sig"], Py.Utf8Sig(b), "utf-8-sig " + hex);
            c.Eq(e["utf16le"], Py.Utf16Le(b), "utf-16-le replace " + hex);
            c.Eq(e["ascii"], Py.Ascii(b), "ascii replace " + hex);
        }
        c.Done();
    }

    [Test]
    public static void ZlibDecompress()
    {
        var c = new Check("zlib.decompress");
        foreach (var e in Cases("zlib"))
        {
            var z = Convert.FromBase64String((string)e["z"]!);
            string? sha = null, err = null;
            try
            {
                sha = Convert.ToHexStringLower(SHA256.HashData(Zlib.Decompress(z)));
            }
            catch (ZlibError ex)
            {
                err = ex.Message;
            }
            c.Eq(e["sha256"], sha, "content " + Py.HexStr(z[..Math.Min(12, z.Length)]));
            c.Eq(e["error"], err, "error " + Py.HexStr(z[..Math.Min(12, z.Length)]));
        }
        c.Done();
    }

    [Test]
    public static void DecompressObjPartial()
    {
        var c = new Check("zlib.decompressobj");
        var data = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("abcdefgh", 500)));
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
            z.Write(data);
        var zb = ms.ToArray();
        var (o1, eof1, end1) = Zlib.DecompressObj([.. zb, 1, 2, 3], 0);
        c.True(o1.SequenceEqual(data) && eof1 && end1 == zb.Length, "complete stream with unused data behind it");
        var (_, eof2, end2) = Zlib.DecompressObj(zb[..^4], 0);
        c.True(!eof2 && end2 == zb.Length - 4, "truncated stream: no eof, all input used");
        c.Done();
    }

    [Test]
    public static void SqliteTables()
    {
        var c = new Check("SQLite reader");
        foreach (var e in Cases("sqlite"))
        {
            var db = new SqliteReader(ProjectFiles.Member(Path.Combine(Program.Root, (string)e["db"]!), "XRef/XRef.db")!);
            var rows = db.Rows((string)e["table"]!).Select(r => (object?)r.Select(v => v is byte[] b ? "hex:" + Py.HexStr(b) : v).ToList()).ToList();
            c.Eq(e["rows"], (long)rows.Count, $"{e["db"]} {e["table"]} row count");
            c.Eq(e["sha256"], Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(PyJson.Dumps(rows)))), $"{e["db"]} {e["table"]} content");
        }
        c.Done();
    }

    [Test]
    public static void DateTimeTicks()
    {
        var c = new Check("ticks_to_iso");
        foreach (var e in Cases("ticks"))
            c.Eq(e["iso"], Attrs.TicksToIso(Py.Long(e["v"])), "ticks " + e["v"]);
        c.Done();
    }
}
