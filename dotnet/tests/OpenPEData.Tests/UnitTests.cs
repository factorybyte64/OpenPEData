using System.IO.Compression;
using OpenPEData.Format;
using OpenPEData.Model;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Port of parser\tests\test_units.py (synthetic byte strings) plus unit tests of the Python
/// semantics the port relies on.</summary>
public static class UnitTests
{
    static readonly byte[] WebApp = Convert.FromHexString(
        "4e0000008a0000000100000001004a000000010000801a00000049007300440065006600610075006c0074005700650062004100" +
        "700070006c0069006300610074006900" +
        "6f006e00000001000000" + string.Concat(Enumerable.Repeat("00", 60)));

    static readonly byte[] Kind4External = Convert.FromHexString(
        "04e96f0200000000000000000000104000000003ffffffff7f00000000000000" +
        "130007000001000000000000130007000101000000000000");

    /// <summary>Kind 1 blob (zlib pages) of content, as test_units._paged.</summary>
    static byte[] Paged(byte[] content, int page = 4096)
    {
        var pages = (content.Length + page - 1) / page;
        var words = new uint[pages / 32 + 1];
        for (var i = 0; i < pages; i++)
            words[i / 32] |= 1u << (i % 32);
        var o = new List<byte> { 1 };
        o.AddRange(BitConverter.GetBytes((ulong)content.Length));
        o.AddRange(BitConverter.GetBytes(0u));
        o.AddRange(BitConverter.GetBytes((ushort)page));
        o.AddRange(BitConverter.GetBytes((uint)pages));
        o.Add((byte)words.Length);
        foreach (var w in words)
            o.AddRange(BitConverter.GetBytes(w));
        for (var i = 0; i < pages; i++)
        {
            var chunk = new byte[page];
            Array.Copy(content, i * page, chunk, 0, Math.Min(page, content.Length - i * page));
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                z.Write(chunk);
            var zb = ms.ToArray();
            var n = zb.Length;
            o.Add((byte)((n & 0x7F) | 0x80));
            o.Add((byte)(n >> 7));
            o.AddRange(zb);
        }
        return [.. o];
    }

    static byte[] StoreRecord(ulong oid, byte[] chunk)
    {
        var body = new List<byte>();
        body.AddRange(BitConverter.GetBytes((ushort)0x13));
        body.AddRange(BitConverter.GetBytes((ushort)7));
        body.AddRange(BitConverter.GetBytes(oid));
        body.AddRange(BitConverter.GetBytes(0u));
        body.AddRange(BitConverter.GetBytes(3u));
        body.AddRange(BitConverter.GetBytes(0u));
        body.AddRange(Enumerable.Repeat((byte)0x11, 16));
        body.AddRange(BitConverter.GetBytes((uint)chunk.Length));
        body.AddRange(chunk);
        body.Add(0xFF);
        return [.. BitConverter.GetBytes((uint)body.Count + 4), .. body];
    }

    [Test]
    public static void InlineBoolInSimpleExpando()
    {
        var (vals, problems) = Attrs.DecodeSimpleExpando(WebApp);
        Assert.Eq(PyDict.Of("IsDefaultWebApplication", true), vals, "values");
        Assert.Eq(0, problems.Count, "problems");
    }

    [Test]
    public static void NeutralCultureOfInterfaceComments()
    {
        Assert.Eq(PyDict.Of("en-US", "x"), Interface.DropNeutral(PyDict.Of("en-US", "x", "65535", "x")), "drop repeated neutral text");
        Assert.Eq(PyDict.Of("65535", "y"), Interface.DropNeutral(PyDict.Of("65535", "y")), "keep the only text");
    }

    [Test]
    public static void InlineExpandoCodes()
    {
        Assert.Eq(11135L, Attrs.ExpandoInline(0x12, 11135), "code 0x12");
        Assert.Eq(15.0, Attrs.ExpandoInline(7, 0x41700000), "code 7 float bits");
        Assert.Eq(true, Attrs.ExpandoInline(1, 5), "code 1 bool");
        Assert.Eq(-1L, Attrs.ExpandoInline(5, 0xFFFFFFFF), "code 5 signed");
    }

    [Test]
    public static void Kind4ExternalBlob()
    {
        var keys = Attrs.ExternalRefs(Kind4External);
        Assert.Eq(Py.L("0013:0007:100", "0013:0007:101"), keys?.Cast<object?>().ToList(), "external refs");
        var total = (int)Bin.U64(Kind4External, 1);
        var content = Enumerable.Range(0, total).Select(i => (byte)((i * 7 + i / 4096) & 0xFF)).ToArray();
        const int chunk = 32 * 4096;
        var store = new Dictionary<string, byte[]>
        {
            ["0013:0007:100"] = StoreRecord(0x100, Paged(content[..chunk])),
            ["0013:0007:101"] = StoreRecord(0x101, Paged(content[chunk..])),
        };
        Assert.True(Attrs.DecodeBlob(Kind4External, k => store.GetValueOrDefault(k))!.AsSpan().SequenceEqual(content), "kind 4 external content");
        var bound = Attrs.BindStore((byte[])Kind4External.Clone(), k => store.GetValueOrDefault(k));
        Assert.True(Attrs.DecodeBlob(bound)!.AsSpan().SequenceEqual(content), "kind 4 external content through the bound store");
        Assert.Throws<BlobError>(() => Attrs.DecodeBlob((byte[])Kind4External.Clone()), "no store");
        Assert.Throws<BlobError>(() => Attrs.DecodeBlob(Kind4External, k => k.EndsWith("100") ? store[k] : null), "missing record");
        Assert.True(Attrs.DecodeBlob([4, 3, (byte)'a', (byte)'b', (byte)'c'])!.SequenceEqual("abc"u8.ToArray()), "kind 4 varint + bytes");
        Assert.True(Attrs.DecodeBlob([4, 9, (byte)'a', (byte)'b', (byte)'c']) is null, "kind 4 varint + bytes, wrong length");
        Assert.Eq(0, Attrs.DecodeBlob([])!.Length, "empty blob");
    }

    [Test]
    public static void PagedBlobKinds()
    {
        var content = Enumerable.Range(0, 10000).Select(i => (byte)(i % 251)).ToArray();
        Assert.True(Attrs.DecodeBlob(Paged(content))!.SequenceEqual(content), "kind 1 (zlib pages)");
        // kind 0: raw pages, last page shorter; page 1 missing -> zeros
        var raw = new List<byte> { 0 };
        raw.AddRange(BitConverter.GetBytes((ulong)10000));
        raw.AddRange(BitConverter.GetBytes(0u));
        raw.AddRange(BitConverter.GetBytes((ushort)4096));
        raw.AddRange(BitConverter.GetBytes(3u));
        raw.Add(1);
        raw.AddRange(BitConverter.GetBytes(0b101u));
        raw.AddRange(content[..4096]);
        raw.AddRange(content[8192..]);
        var expect = (byte[])content.Clone();
        Array.Clear(expect, 4096, 4096);
        Assert.True(Attrs.DecodeBlob([.. raw])!.SequenceEqual(expect), "kind 0 with a missing page");
        Assert.True(Attrs.DecodeBlob([7, 1, 2, 3]) is null, "unknown kind");
    }

    [Test]
    public static void VarintAndReadVar()
    {
        Assert.Eq((300L, 2), Attrs.Varint([0xAC, 0x02], 0), "varint 300");
        Assert.Eq((0L, 1), Attrs.Varint([0], 0), "varint 0");
        Assert.Throws<PyIndexError>(() => Attrs.Varint([0x80], 0), "unterminated varint");
        var (v, end) = Attrs.ReadVar([4, (byte)'a', (byte)'b', (byte)'c', 9], 0);
        Assert.True(v!.SequenceEqual("abc"u8.ToArray()) && end == 4, "read_var: total size incl. the varint");
        Assert.True(Attrs.ReadVar([0, 1], 0).Value is null, "read_var 0 = null");
    }

    [Test]
    public static void DecodeValueTypes()
    {
        var mm = new Metamodel("""<Meta xmlns="http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta"><Package id="0x10000" name="P"><Namespace name="N"><Enumeration name="E" id="0x15001" base="xs:short"><Constant value="2" name="Two"/></Enumeration></Namespace></Package></Meta>"""u8.ToArray());
        Assert.Eq(true, Attrs.DecodeValue(mm, "xs:boolean", [1]), "bool");
        Assert.Eq(-2L, Attrs.DecodeValue(mm, "xs:short", [0xFE, 0xFF]), "short");
        Assert.Eq(0xFFFFFFFFL, Attrs.DecodeValue(mm, "xs:uint", [0xFF, 0xFF, 0xFF, 0xFF]), "uint");
        Assert.Eq(ulong.MaxValue, Attrs.DecodeValue(mm, "xs:ulong", [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]), "ulong above long");
        Assert.Eq(1.5, Attrs.DecodeValue(mm, "xs:float", BitConverter.GetBytes(1.5f)), "float");
        Assert.Eq("Two", Attrs.DecodeValue(mm, "E", [2, 0]), "enumeration constant");
        Assert.Eq(3L, Attrs.DecodeValue(mm, "N.E", [3, 0]), "enumeration without constant");
        Assert.Eq("00000000-0000-0000-0000-000000000000", Attrs.DecodeValue(mm, "pe:GuidT", new byte[16]), "guid");
        Assert.Throws<StructError>(() => Attrs.DecodeValue(mm, "xs:int", [1, 2]), "short buffer");
        Assert.Eq(2, Attrs.TypeSize(mm, "E"), "enum size from base type");
    }

    [Test]
    public static void PyDictSemantics()
    {
        var d = PyDict.Of("a", 1L, "b", 2L);
        d["a"] = 3L;
        Assert.Eq(Py.L("a", "b"), d.Keys.ToList(), "replacing keeps the position");
        d.Pop("a");
        d["a"] = 4L;
        Assert.Eq(Py.L("b", "a"), d.Keys.ToList(), "pop + insert appends");
        d[null] = 1L;
        d[1L] = "one";
        Assert.Eq("one", d.Get(true), "True == 1 as a key");
        Assert.Eq("one", d.Get(1.0), "1.0 == 1 as a key");
        Assert.Eq(1L, d.Get(null), "None key");
        d[Py.T(1L, "x")] = "t";
        Assert.Eq("t", d.Get(Py.T(1L, "x")), "tuple key");
        d[new byte[] { 1, 2 }] = "b";
        Assert.Eq("b", d.Get(new byte[] { 1, 2 }), "bytes key by value");
        Assert.Throws<PyTypeError>(() => d[new PyDict()] = 1, "dict is unhashable");
        Assert.Throws<PyKeyError>(() => _ = d["missing"], "KeyError");
    }

    [Test]
    public static void OrderingAndSorting()
    {
        Assert.True(Py.Compare("\xffff", "\xd83d\xde00") < 0, "code point order (U+FFFF < U+1F600)");
        Assert.True(Py.Compare(Py.T(null, 1L), Py.T(null, 2L)) < 0, "tuple with equal None");
        Assert.Throws<PyTypeError>(() => Py.Compare(null, "a"), "None vs str");
        Assert.True(Py.Compare(1L, 1.5) < 0 && Py.Compare(true, 0L) > 0 && Py.Compare(ulong.MaxValue, -1L) > 0, "mixed numbers");
        var items = new[] { ("b", 1), ("a", 2), ("b", 0), ("a", 1) };
        Assert.Eq(Py.L("a2", "a1", "b1", "b0"), Py.Sorted(items, x => x.Item1).Select(x => (object?)(x.Item1 + x.Item2)).ToList(), "stable sort");
        Assert.Eq(Py.L("b1", "b0", "a2", "a1"), Py.Sorted(items, x => x.Item1, true).Select(x => (object?)(x.Item1 + x.Item2)).ToList(), "stable reverse sort");
        Assert.Eq(("b", 1), Py.Max(items, x => x.Item1), "max keeps the first of the largest");
        Assert.Eq(Py.L(3L, 1L, 2L), Py.Unique(new object?[] { 3L, 1L, 3L, 2L, 1L }).ToList(), "dict.fromkeys order");
    }

    [Test]
    public static void SlicingAndText()
    {
        byte[] b = [0, 1, 2, 3, 4];
        Assert.True(Py.Slice(b, 1, -1).SequenceEqual(new byte[] { 1, 2, 3 }), "negative end");
        Assert.True(Py.Slice(b, 3, 1).Length == 0 && Py.Slice(b, 10, 20).Length == 0 && Py.Slice(b, -2).SequenceEqual(new byte[] { 3, 4 }), "clamping");
        Assert.Eq(4L, (long)Py.At(b, -1), "negative index");
        Assert.Throws<PyIndexError>(() => Py.At(b, 5), "index out of range");
        Assert.Eq(Py.L("a", "b", "", "c", "d"), Py.SplitLines("a\r\nb\n\nc" + (char)0x1c + "d").Cast<object?>().ToList(), "splitlines");
        Assert.Eq("x", Py.Strip("\x1c\t x \x85\x3000"), "strip with Python whitespace");
        Assert.True(Py.IsDigit("012") && !Py.IsDigit("") && !Py.IsDigit("-1") && !Py.IsDigit("1a"), "isdigit");
        Assert.Eq("0x000000FF", Hmi.Fmt08X(255L), "0x%08X");
        Assert.Eq("-ff", Py.Hex(-255L), "%x negative");
        Assert.Eq("'a'", Py.Repr("a"), "repr str");
        Assert.Eq("[1, 'x', None, True, (2,)]", Py.Repr(Py.L(1L, "x", null, true, Py.T(2L))), "repr list");
        Assert.Eq("{'k': b'\\x00'}", Py.Repr(PyDict.Of("k", new byte[] { 0 })), "repr dict with bytes");
    }

    [Test]
    public static void ElementTreeSemantics()
    {
        var el = Et.FromString("<a xmlns=\"urn:x\" k=\"v\" xmlns:p=\"urn:p\" p:q=\"w\">t1<b>t2</b>tail<!--c--><c/></a>");
        Assert.Eq("{urn:x}a", Et.Tag(el), "namespaced tag");
        Assert.Eq("t1", Et.Text(el), "text before the first child");
        Assert.Eq(PyDict.Of("k", "v", "{urn:p}q", "w"), Et.Attrib(el), "attributes without namespace declarations");
        Assert.True(Et.Text(Et.Kids(el).Last()) is null, "empty element: text None");
        Assert.Eq(3, Et.Iter(el).Count(), "iter: element and descendants");
        Assert.Throws<XmlParseError>(() => Et.FromString("<a>"), "parse error");
        Assert.Eq(" ", Et.Text(Et.FromString("<a> </a>")), "whitespace text kept");
    }

    [Test]
    public static void CoreTextLayout()
    {
        // u32 total, u32 used, i32 -1, u32 count, count x u16 LCID, count x u32 offset, at each offset u32 n + UTF-8
        var t1 = "Hallo"u8.ToArray();
        var t2 = "x\xe4"u8.ToArray();
        var head = 16 + 2 * 2 + 4 * 2;
        var o1 = head;
        var o2 = o1 + 4 + t1.Length;
        var b = new List<byte>();
        b.AddRange(BitConverter.GetBytes(0u));
        b.AddRange(BitConverter.GetBytes(0u));
        b.AddRange(BitConverter.GetBytes(-1));
        b.AddRange(BitConverter.GetBytes(2u));
        b.AddRange(BitConverter.GetBytes((ushort)1031));
        b.AddRange(BitConverter.GetBytes((ushort)0xFFFF));
        b.AddRange(BitConverter.GetBytes((uint)o1));
        b.AddRange(BitConverter.GetBytes((uint)o2));
        b.AddRange(BitConverter.GetBytes((uint)t1.Length));
        b.AddRange(t1);
        b.AddRange(BitConverter.GetBytes((uint)t2.Length));
        b.AddRange(t2);
        Assert.Eq(PyDict.Of("de-DE", "Hallo", "65535", "x\xe4"), ProjectModel.CoreText(b.ToArray()), "cultures by LCID, 0xFFFF as 65535");
        Assert.Eq(PyDict.Of("Deutsch", "Hallo", "65535", "x\xe4"), ProjectModel.CoreText(b.ToArray(), new Dictionary<long, string> { [1031] = "Deutsch" }), "project cultures first");
        Assert.Eq(0, ProjectModel.CoreText(new byte[10]).Count, "short blob");
    }

    [Test]
    public static void AddressTexts()
    {
        Assert.Eq("%M10.3", Code.AddressText("Memory", 83, null), "bit");
        Assert.Eq("%MW2", Code.AddressText("Memory", 16, 16L), "word");
        Assert.Eq("%IW0:P", Code.AddressText("Input", 0, 16L, true), "periphery");
        Assert.Eq("%QD4", Code.AddressText("Output", 32, 32L), "double word");
        Assert.Eq("T1", Code.AddressText("Timer", 1, null), "timer");
        Assert.Eq("C5", Code.AddressText("Counter", 5, null), "counter");
        Assert.Eq("%M-1.7", Code.AddressText("Memory", -1, null), "negative offset floors like Python");
    }

    [Test]
    public static void HardwareValueFormats()
    {
        Assert.Eq("192.168.0.1", Hardware.Ip4(3232235521L), "ip4");
        Assert.Eq("0.0.0.1", Hardware.Ip4(true), "ip4 of bool (int in Python)");
        Assert.Eq("text", Hardware.Ip4("text"), "ip4 of a non-int");
        Assert.Eq("00-1B-1B-12-34-56", Hardware.Mac(0x001B1B123456L), "mac");
        Assert.Eq(PyDict.Of("file", "GSDML-V2.3-X.xml", "kind", "DAP", "id", "DAP 1.0", "typeIdentifier", "GSD:GSDML-V2.3-X.XML/DAP/DAP 1.0"),
            Hardware.GsdType("GSD\\GSDML-V2.3-X.xml\\HM_DAP_SPA_1_DOT_0"), "gsd DAP with escapes");
        Assert.Eq(PyDict.Of("file", "f.xml", "kind", "R", "id", "x", "typeIdentifier", "GSD:F.XML/R/x"), Hardware.GsdType("GSD\\f.xml\\Comp_x_Rack"), "gsd rack");
        Assert.True(Hardware.GsdType("S71500.CPU") is null, "no GSD catalog");
    }

    [Test]
    public static void LibraryHelpers()
    {
        Assert.Eq("0002:0040:1f", Library.ParseScopeId("{4194306-31}"), "scope id -> record key");
        Assert.True(Library.ParseScopeId("Persistence") is null, "other scope id forms");
        Assert.Eq(Py.T(0L, 1L, 2L, 3L), Library.VersionKey("1.2.3"), "version key");
        Assert.Eq(Py.T(1L), Library.VersionKey(null), "None last");
        Assert.Eq(Py.T(1L), Library.VersionKey("1.x"), "unparsable last");
        var raw = new byte[20];
        BitConverter.GetBytes(20u).CopyTo(raw, 0);
        BitConverter.GetBytes(3).CopyTo(raw, 4);
        BitConverter.GetBytes(1).CopyTo(raw, 8);
        BitConverter.GetBytes(2).CopyTo(raw, 12);
        BitConverter.GetBytes(-1).CopyTo(raw, 16);
        var (txt, hex, parts) = Library.DecodeVersionData(raw);
        Assert.Eq("1.2.3", txt, "version text Major.Minor.Build");
        Assert.Eq(Py.HexStr(raw), hex, "raw hex");
        Assert.Eq(PyDict.Of("Build", 3L, "Major", 1L, "Minor", 2L, "Revision", -1L), parts, "parts in declaration order");
        Assert.Eq("1.2.3", Faceplates.DecodeVersionText(raw), "faceplates version text");
        Assert.True(Faceplates.DecodeVersionText(new byte[3]) is null, "short version");
    }

    [Test]
    public static void MapDecoding()
    {
        // u32 length, u32 count, u32 value offset, codes, keys varint(n+1), values: 0x05 i32 "A" = -1, 0x01 bool "B"
        var keys = new List<byte> { 2, (byte)'A', 2, (byte)'B' };
        // total length 148: the first byte 0x94 reads as a varint 20 (<= 24), so it is not taken for a length prefix
        var head = 12 + 8 + keys.Count;
        var v = new List<byte>();
        v.AddRange(BitConverter.GetBytes(148u));
        v.AddRange(BitConverter.GetBytes(2u));
        v.AddRange(BitConverter.GetBytes((uint)head));
        v.AddRange(BitConverter.GetBytes(0x80000005u));
        v.AddRange(BitConverter.GetBytes(0x80000001u));
        v.AddRange(keys);
        v.AddRange(BitConverter.GetBytes(-1));
        v.Add(1);
        v.AddRange(new byte[148 - v.Count]);
        Assert.Eq(PyDict.Of("A", -1L, "B", true), Faceplates.DecodeMap(v.ToArray()), "plain map");
        Assert.True(Faceplates.DecodeMap(new byte[5]) is null, "too short");
        var bad = v.ToArray();
        BitConverter.GetBytes(0x80000072u).CopyTo(bad, 16);
        Assert.Eq(Py.T("code 0x72", null), Faceplates.DecodeMap(bad)!["B"], "unknown code kept as (text, None)");
    }
}
