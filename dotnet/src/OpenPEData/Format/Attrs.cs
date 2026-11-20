using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>A blob whose layout is recognised but whose content cannot be rebuilt (missing or damaged store record).</summary>
internal sealed class BlobError(string message) : PyValueError(message, "BlobError");

/// <summary>Decoding of AttributeSet blocks in object records.
/// Block = u32 length (incl. this field), then the stored attributes of the set (Metamodel.StoredAttributes) in ordinal
/// order: fixed-size types inline, all other types as u32 offset relative to the block start (0 = null); the value at the
/// offset is a 7-bit varint holding the total value size incl. the varint itself (0 = null), then the bytes; strings
/// UTF-8. Structure and Array values start with a u32 total size instead (H-L2-1).</summary>
internal static class Attrs
{
    static readonly Dictionary<string, int> Fixed = new()
    {
        ["xs:boolean"] = 1, ["xs:unsignedByte"] = 1, ["xs:byte"] = 1,
        ["xs:short"] = 2, ["xs:ushort"] = 2,
        ["xs:int"] = 4, ["xs:uint"] = 4, ["xs:float"] = 4,
        ["xs:long"] = 8, ["xs:ulong"] = 8, ["xs:double"] = 8, ["xs:dateTime"] = 8,
        ["pe:GuidT"] = 16,
    };

    static readonly HashSet<string> Fmt = ["xs:boolean", "xs:unsignedByte", "xs:byte", "xs:short", "xs:ushort", "xs:int", "xs:uint",
        "xs:float", "xs:long", "xs:ulong", "xs:double"];

    /// <summary>True for errors Python reports as struct.error / IndexError. .NET range errors (IndexOutOfRange,
    /// ArgumentOutOfRange, Overflow) are not counted: they are bugs of the port, not unreadable data, and must not turn into
    /// "blob not readable" (none occurs on the samples; Py.At and Bin.* raise the Python errors).</summary>
    public static bool IsDecodeError(Exception e) => e is StructError or PyIndexError;

    public static int? TypeSize(Metamodel mm, string? typ)
    {
        if (typ is not null && Fixed.TryGetValue(typ, out var s))
            return s;
        var t = mm.Resolve(typ);
        if (t is not null && t.Kind == "Enumeration")
            return Fixed.GetValueOrDefault(t.BaseType ?? "xs:int", 4);
        return null;
    }

    public static (long Value, int End) Varint(byte[] d, long o)
    {
        long v = 0;
        var shift = 0;
        while (true)
        {
            var b = Py.At(d, o);
            o++;
            if (shift < 63)
                v |= (long)(b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0)
                return (v, (int)o);
        }
    }

    /// <summary>(bytes or null, end offset) of a length-prefixed value at block offset off.</summary>
    public static (byte[]? Value, long End) ReadVar(byte[] block, long off)
    {
        var (n, o) = Varint(block, off);
        if (n == 0)
            return (null, o);
        var end = off + n;
        return (Py.Slice(block, o, end), end);
    }

    public static bool IsStructured(Metamodel mm, string? typ)
    {
        var t = mm.Resolve(typ);
        return t is not null && t.Kind is "Structure" or "Array";
    }

    /// <summary>(bytes incl. the u32 length head, end offset) of a Structure / Array value, or null.</summary>
    public static (byte[] Value, long End)? ReadStruct(byte[] block, long off, long end)
    {
        if (off + 4 > end)
            return null;
        var n = Bin.U32(block, off);
        if (n < 4 || off + n > end)
            return null;
        return (Py.Slice(block, off, off + n), off + n);
    }

    static byte[]? StructAt(byte[] buf, long off)
    {
        var r = off != 0 ? ReadStruct(buf, off, buf.Length) : null;
        return r?.Value;
    }

    /// <summary>Decode a Structure / Array value (bytes with u32 length head): dict (Structure) or list (Array); values
    /// that do not fit stay bytes.</summary>
    public static object? DecodeStructure(Metamodel mm, object? typ, object? raw, int depth = 0)
    {
        var t = typ is string s ? mm.Resolve(s) : typ as MetaType;
        if (t is null || raw is not byte[] b || b.Length < 4 || depth > 16)
            return raw;
        try
        {
            if (t.Kind == "Array")
            {
                var cnt = Bin.U32(b, 4);
                var isz = TypeSize(mm, t.ItemType);
                if (isz is { } sz)
                {
                    if (8 + (long)sz * cnt != b.Length)
                        return raw;
                    var l = new List<object?>();
                    for (var i = 0; i < cnt; i++)
                        l.Add(DecodeValue(mm, t.ItemType, Py.Slice(b, 8 + i * sz, 8 + (i + 1) * sz)));
                    return l;
                }
                if (8 + 4L * cnt > b.Length)
                    return raw;
                var items = new List<object?>();
                foreach (var off in Bin.U32s(b, 8, cnt))
                    items.Add(ElementValue(mm, t.ItemType, b, off, depth));
                return items;
            }
            if (t.Kind != "Structure")
                return raw;
            var ln = Bin.U32(b, 0);
            var outd = new PyDict();
            long o = 4;
            foreach (var (name, et) in t.Elements)
            {
                if (et == "pe:CharT")
                {
                    outd[name] = ((char)Bin.U16(b, o)).ToString();
                    o += 2;
                    continue;
                }
                var esz = TypeSize(mm, et);
                if (esz is null)
                {
                    if (o + 4 > ln)
                        return raw;
                    var off = Bin.U32(b, o);
                    outd[name] = off != 0 ? ElementValue(mm, et, b, off, depth) : null;
                    o += 4;
                }
                else
                {
                    if (o + esz.Value > ln)
                        return raw;
                    outd[name] = DecodeValue(mm, et, Py.Slice(b, o, o + esz.Value));
                    o += esz.Value;
                }
            }
            return outd;
        }
        catch (Exception e) when (IsDecodeError(e) || e is PyValueError)
        {
            return raw;
        }
    }

    static object? ElementValue(Metamodel mm, string? typ, byte[] buf, long off, int depth)
    {
        if (IsStructured(mm, typ))
        {
            var v = StructAt(buf, off);
            return v is not null ? DecodeStructure(mm, typ, v, depth + 1) : null;
        }
        var sz = TypeSize(mm, typ);
        if (sz is not null)
            return DecodeValue(mm, typ, Py.Slice(buf, off, off + sz.Value));
        var (val, _) = ReadVar(buf, off);
        if (typ == "xs:string" && val is not null)
            return Py.Utf8(val);
        return val;
    }

    public static string? TicksToIso(long v)
    {
        var kind = v >> 62;
        var t = v & 0x3FFFFFFFFFFFFFFF;
        var us = t / 10;
        if (us > DateTime.MaxValue.Ticks / 10)
            return null;
        var dt = new DateTime(us * 10, DateTimeKind.Unspecified);
        var s = dt.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        if (us % 1_000_000 != 0)
            s += "." + (us % 1_000_000).ToString("000000");
        return s + (kind == 1 ? "Z" : "");
    }

    public static string Guid(byte[] raw16) => new System.Guid(raw16).ToString();

    static object Unpack(string typ, byte[] raw)
    {
        var need = Fixed[typ];
        if (raw.Length != need)
            throw new StructError($"unpack requires a buffer of {need} bytes");
        return typ switch
        {
            "xs:boolean" => raw[0] != 0,
            "xs:unsignedByte" => (long)raw[0],
            "xs:byte" => (long)(sbyte)raw[0],
            "xs:short" => (long)Bin.I16(raw, 0),
            "xs:ushort" => (long)Bin.U16(raw, 0),
            "xs:int" => (long)Bin.I32(raw, 0),
            "xs:uint" => (long)Bin.U32(raw, 0),
            "xs:float" => (double)Bin.F32(raw, 0),
            "xs:long" => Bin.I64(raw, 0),
            "xs:ulong" => Py.Int(Bin.U64(raw, 0)),
            "xs:double" => Bin.F64(raw, 0),
            _ => throw new InvalidOperationException(typ),
        };
    }

    public static object? DecodeValue(Metamodel mm, string? typ, byte[] raw)
    {
        if (typ is not null && Fmt.Contains(typ))
            return Unpack(typ, raw);
        if (typ == "xs:dateTime")
            return TicksToIso((long)Unpack("xs:long", raw));
        if (typ == "pe:GuidT")
        {
            if (raw.Length != 16)
                throw new PyValueError("bytes_le is not a 16-char string");
            return Guid(raw);
        }
        var t = mm.Resolve(typ);
        if (t is not null && t.Kind == "Enumeration")
        {
            var bt = t.BaseType ?? "xs:int";
            var v = Unpack(Fmt.Contains(bt) ? bt : "xs:int", raw);
            return t.Constants.TryGetValue(Py.Str(v), out var name) ? name : v;
        }
        return raw;
    }

    /// <summary>Decode one AttributeSet block (starting at its u32 length): (values, problems).</summary>
    public static (PyDict Values, List<string> Problems) DecodeSet(Metamodel mm, uint tid, string setFull, byte[] block)
    {
        var attrs = mm.StoredAttributes(tid, setFull);
        var ln = Bin.U32(block, 0);
        var vals = new PyDict();
        var problems = new List<string>();
        var var_ = new List<(long Off, string? Name, string? Typ)>();
        long o = 4;
        foreach (var (_, name, typ, _) in attrs)
        {
            var sz = TypeSize(mm, typ);
            if (sz is null)
            {
                if (o + 4 > ln)
                {
                    problems.Add($"fixed part overruns block at {Py.Str(name)}");
                    return (vals, problems);
                }
                var_.Add((Bin.U32(block, o), name, typ));
                o += 4;
            }
            else
            {
                if (o + sz.Value > ln)
                {
                    problems.Add($"fixed part overruns block at {Py.Str(name)}");
                    return (vals, problems);
                }
                vals[name] = DecodeValue(mm, typ, Py.Slice(block, o, o + sz.Value));
                o += sz.Value;
            }
        }
        var pos = o;
        var gap = false;
        foreach (var (off, name, typ) in Py.Sorted(var_, x => Py.T(x.Off, x.Name, x.Typ)))
        {
            if (off == 0)
            {
                vals[name] = null;
                continue;
            }
            if (off < pos || off >= ln)
            {
                problems.Add($"var {Py.Str(name)} at {off}, expected {pos}");
                continue;
            }
            gap |= off > pos;
            var r = IsStructured(mm, typ) ? ReadStruct(block, off, ln) : null;
            if (r is not null)
            {
                vals[name] = r.Value.Value;
                pos = r.Value.End;
                continue;
            }
            var (raw, end) = ReadVar(block, off);
            pos = end;
            if (typ == "xs:string" && raw is not null)
            {
                try
                {
                    vals[name] = Py.Utf8Exact(raw);
                }
                catch (PyUnicodeDecodeError)
                {
                    vals[name] = raw;
                }
            }
            else
                vals[name] = raw;
        }
        if ((pos < ln || gap) && problems.Count == 0)
            problems.Add($"nested: block length {ln}, top level decoded up to {pos}");
        else if (pos > ln && problems.Count == 0)
            problems.Add($"block length {ln}, decoded up to {pos}");
        return (vals, problems);
    }

    // --- expando attributes ----------------------------------------------------------------------------
    public static readonly FrozenDictionary<long, string> ExpandoCodes = new Dictionary<long, string>
    {
        [1] = "bool", [2] = "byte", [4] = "uint", [5] = "int", [6] = "long", [7] = "float", [8] = "double", [0x12] = "int",
        [0xA] = "dateTime", [0xB] = "string", [0xC] = "guid", [0xD] = "blob", [0xE] = "xml", [0x13] = "int?", [0x14] = "long?",
        [0x1F] = "text",
    }.ToFrozenDictionary();

    static readonly Dictionary<string, int> FixedSizes = new()
    {
        ["bool"] = 1, ["byte"] = 1, ["int"] = 4, ["int?"] = 4, ["long"] = 8, ["long?"] = 8, ["double"] = 8, ["dateTime"] = 8, ["guid"] = 16,
    };

    public static readonly FrozenSet<long> InlineCodes = new long[] { 1, 2, 4, 5, 7, 0x12, 0x13 }.ToFrozenSet();

    public static object ExpandoInline(long code, uint v)
    {
        if (code == 1)
            return v != 0;
        if (code is 5 or 0x12 or 0x13)
            return (long)Bin.AsI32(v);
        if (code == 7)
            return (double)Bin.AsF32(v);
        return (long)v;
    }

    /// <summary>{id: (name, code)} from a 0x00070011 store record, plus the object type id.</summary>
    public static (uint Tid, Dictionary<uint, (string Name, long Code)> Names) ReadDictionary(byte[] data)
    {
        var tid = Bin.U32(data, 52);
        var cnt = Bin.U32(data, 56);
        Bin.U32(data, 60);
        long o = 64;
        var res = new Dictionary<uint, (string, long)>();
        for (var i = 0; i < cnt; i++)
        {
            var eid = Bin.U32(data, o);
            var (n, p) = Varint(data, o + 4);
            o = p;
            var name = Py.Utf8(Py.Slice(data, o, o + n));
            o += n;
            var code = Bin.U32(data, o);
            o += 4;
            res[eid] = (name, code & 0x7FFFFFFF);
        }
        return (tid, res);
    }

    /// <summary>Decode an expando block (starting at its u32 used length): (values, problems).</summary>
    public static (PyDict, List<string>) DecodeExpando(byte[] block, Dictionary<uint, Dictionary<uint, (string Name, long Code)>> dictionaries)
    {
        if (block.Length < 8)
            return (new PyDict(), []);
        var used = Bin.U32(block, 0);
        Bin.U32(block, 4);
        if (used <= 8)
            return (new PyDict(), []);
        var cnt = Bin.U32(block, 8);
        var tid = Bin.U32(block, 14);
        const int o = 18;
        if (o + 8L * cnt > used)
            return (new PyDict(), [$"expando count {cnt} does not fit used {used}"]);
        var ids = Bin.U32s(block, o, cnt);
        var offs = Bin.U32s(block, o + 4 * cnt, cnt);
        var bas = o + 8L * cnt;
        var names = dictionaries.GetValueOrDefault(tid) ?? [];
        var vals = new PyDict();
        var problems = new List<string>();
        var outl = new List<(uint Off, int I)>();
        (string, long?) Name(int i) => names.TryGetValue(ids[i], out var nc) ? (nc.Name, nc.Code) : ("#" + ids[i], null);
        for (var i = 0; i < cnt; i++)
        {
            var (name, code) = Name(i);
            if (code is { } c && InlineCodes.Contains(c))
                vals[name] = ExpandoInline(c, offs[i]);
            else
                outl.Add((offs[i], i));
        }
        outl = outl.OrderBy(x => x.Off).ThenBy(x => x.I).ToList();
        for (var j = 0; j < outl.Count; j++)
        {
            var (off, i) = outl[j];
            var a = bas + off;
            var e = j + 1 < outl.Count ? bas + outl[j + 1].Off : used;
            var (name, code) = Name(i);
            if (!(bas <= a && a <= e && e <= used))
            {
                problems.Add($"expando value {name} out of range");
                continue;
            }
            var raw = Py.Slice(block, a, e);
            if (code is { } c && ExpandoCodes.TryGetValue(c, out var kind) && FixedSizes.TryGetValue(kind, out var size) && raw.Length > size)
                raw = raw[..size];
            vals[name] = ExpandoValue(code, raw);
        }
        return (vals, problems);
    }

    /// <summary>Expando set with expando="simple" (member comments of block interfaces): text values stay raw bytes.</summary>
    public static (PyDict, List<string>) DecodeSimpleExpando(byte[] block)
    {
        var used = Bin.U32(block, 0);
        Bin.U32(block, 4);
        var cnt = Bin.U32(block, 8);
        if (used <= 12 || cnt == 0)
            return (new PyDict(), []);
        var vtab = Bin.U32(block, 14);
        if (18 + 8L * cnt > used || vtab + 4L * cnt > used)
            return (new PyDict(), [$"simple expando count {cnt} does not fit used {used}"]);
        var codes = Bin.U32s(block, 18, cnt);
        var noffs = Bin.U32s(block, 18 + 4 * cnt, cnt);
        var voffs = Bin.U32s(block, vtab, cnt);
        var bas = vtab + 4L * cnt;
        var vals = new PyDict();
        var problems = new List<string>();
        for (var k = 0; k < cnt; k++)
        {
            long code = codes[k];
            long no = noffs[k], vo = voffs[k];
            var e = no;
            while (e + 1 < used && !(Py.Slice(block, e, e + 2) is [0, 0]))
                e += 2;
            var name = Py.Utf16Le(Py.Slice(block, no, e));
            code &= 0x7FFFFFFF;
            var inRange = bas <= bas + vo && bas + vo < used;
            if (InlineCodes.Contains(code) && !inRange)
            {
                vals[name] = ExpandoInline(code, (uint)vo);
                continue;
            }
            if (!inRange)
            {
                problems.Add($"simple expando value {name} out of range");
                continue;
            }
            var (raw, _) = ReadVar(block, bas + vo);
            if (ExpandoCodes.GetValueOrDefault(code) == "string")
                vals[name] = raw is null ? null : Py.Utf8(raw);
            else
                vals[name] = code == 0x1F ? raw : ExpandoValue(code, raw ?? []);
        }
        return (vals, problems);
    }

    public static object? ExpandoValue(long? code, byte[] raw)
    {
        var kind = code is { } c ? ExpandoCodes.GetValueOrDefault(c) : null;
        try
        {
            if (kind == "bool" && raw.Length == 1)
                return raw[0] != 0;
            if (kind == "byte" && raw.Length == 1)
                return (long)raw[0];
            if (kind is "int" or "int?" && raw.Length == 4)
                return (long)Bin.I32(raw, 0);
            if (kind is "long" or "long?" && raw.Length == 8)
                return Bin.I64(raw, 0);
            if (kind == "double" && raw.Length == 8)
                return Bin.F64(raw, 0);
            if (kind == "dateTime" && raw.Length == 8)
                return TicksToIso(Bin.I64(raw, 0));
            if (kind == "guid" && raw.Length == 16)
                return Guid(raw);
            if (kind == "string")
            {
                var (v, _) = ReadVar(raw, 0);
                return v is null ? null : Py.Utf8(v);
            }
        }
        catch (Exception e) when (IsDecodeError(e))
        {
        }
        return raw;
    }

    // --- blobs (pe:BlobT, pe:XmlT, expando blobs) --------------------------------------------------------------
    // Content after the value's varint: u8 kind. kind 0/1: paged blob: u64 total size, u32 ?, u16 page size, u32 page count,
    // varint n + n x u32 bitmap of present pages; per present page: kind 0 raw page bytes, kind 1 varint compressed length
    // + zlib stream. kind 4: paged blob whose pages live in store records (BLOB_STORE_TYPE, 32 pages each), one link12 per
    // non-zero bitmap word; kind 4 without that layout: varint n + n bytes.
    public const uint BlobStoreType = 0x00070013;
    const int ChunkPages = 32;

    static readonly ConditionalWeakTable<byte[], Func<string, byte[]?>> Stores = new();

    /// <summary>Binds a blob value to the record store of its project (Python StoredBlob), so DecodeBlob can resolve pages
    /// that live in store records.</summary>
    public static byte[] BindStore(byte[] raw, Func<string, byte[]?> store)
    {
        Stores.AddOrUpdate(raw, store);
        return raw;
    }

    public static Func<string, byte[]?>? StoreOf(byte[] raw) => Stores.TryGetValue(raw, out var s) ? s : null;

    static (byte Kind, ulong Total, ushort Page, uint Pages, uint[] Bitmap, long End)? PagedHead(byte[] raw)
    {
        if (raw.Length < 20 || raw[0] is not (0 or 1 or 4))
            return null;
        ulong total;
        ushort page;
        uint pages;
        uint[] bitmap;
        int o;
        try
        {
            total = Bin.U64(raw, 1);
            Bin.U32(raw, 9);
            page = Bin.U16(raw, 13);
            pages = Bin.U32(raw, 15);
            long nwords;
            (nwords, o) = Varint(raw, 19);
            if (page == 0 || nwords > 1 << 16 || o + 4 * nwords > raw.Length)
                return null;
            bitmap = Bin.U32s(raw, o, nwords);
            return (raw[0], total, page, pages, bitmap, o + 4 * nwords);
        }
        catch (Exception e) when (IsDecodeError(e))
        {
            return null;
        }
    }

    /// <summary>Record keys of the store records of a kind 4 blob, in page order, or null.</summary>
    public static List<string>? ExternalRefs(object? value)
    {
        if (value is not byte[] raw || raw.Length == 0 || raw[0] != 4)
            return null;
        var h = PagedHead(raw);
        if (h is null)
            return null;
        var (_, total, page, pages, bitmap, o) = h.Value;
        var words = bitmap.Where(w => w != 0).ToList();
        if ((ulong)page * pages < total || pages % ChunkPages != 0 || o + 12L * words.Count != raw.Length)
            return null;
        var keys = new List<string>();
        for (var i = 0; i < words.Count; i++)
        {
            var p = o + 12 * i;
            var lo = Bin.U16(raw, p);
            var hi = Bin.U16(raw, p + 2);
            var oid = Bin.U64(raw, p + 4);
            if ((((uint)hi << 16) | lo) != BlobStoreType)
                return null;
            keys.Add(Plf.MakeKey(lo, hi, oid));
        }
        return keys;
    }

    /// <summary>The chunk blob of a BLOB_STORE_TYPE store record: 44-byte head, u32 n, n bytes, trailing 0xFF.</summary>
    public static byte[]? BlobStoreContent(byte[]? data)
    {
        if (data is null || data.Length < 49)
            return null;
        var n = Bin.U32(data, 44);
        return 48 + (long)n <= data.Length ? Py.Slice(data, 48, 48 + n) : null;
    }

    /// <summary>out[a:b] = data with Python bytearray slice assignment semantics.</summary>
    static void SliceAssign(ref byte[] outb, long a, long b, byte[] data)
    {
        var len = outb.Length;
        if (a < 0) a = Math.Max(0, a + len);
        if (b < 0) b = Math.Max(0, b + len);
        a = Math.Min(a, len);
        b = Math.Min(Math.Max(a, b), len);
        if (b - a == data.Length)
        {
            data.CopyTo(outb, a);
            return;
        }
        var r = new byte[len - (b - a) + data.Length];
        Array.Copy(outb, 0, r, 0, a);
        data.CopyTo(r, a);
        Array.Copy(outb, b, r, a + data.Length, len - b);
        outb = r;
    }

    static byte[]? DecodePaged(byte[] raw)
    {
        var h = PagedHead(raw);
        if (h is null || h.Value.Kind is not (0 or 1))
            return null;
        var (kind, total, page, pages, bitmap, ob) = h.Value;
        if (total > (ulong)page * Math.Max(pages, 1) || total > 1UL << 30)
            return null;
        var o = ob;
        var outb = new byte[total];
        for (long i = 0; i < pages; i++)
        {
            if (i / 32 >= bitmap.Length || ((bitmap[i / 32] >> (int)(i % 32)) & 1) == 0)
                continue;
            var size = Math.Min(page, (long)total - i * page);
            byte[] data;
            if (kind == 1)
            {
                var (clen, p) = Varint(raw, o);
                o = p;
                data = Zlib.Decompress(Py.Slice(raw, o, o + clen));
                o += clen;
            }
            else
            {
                data = Py.Slice(raw, o, o + size);
                o += size;
            }
            data = Py.Slice(data, 0, size);
            SliceAssign(ref outb, i * page, i * page + data.Length, data);
        }
        return outb;
    }

    static byte[] DecodeExternal(byte[] raw, List<string> keys, Func<string, byte[]?>? store)
    {
        var (_, total, page, _, bitmap, _) = PagedHead(raw)!.Value;
        if (store is null)
            throw new BlobError($"blob with {total / page + 1} pages in store records, but no record store");
        var outb = new byte[total];
        long chunk = (long)page * ChunkPages;
        var words = bitmap.Select((w, i) => (w, i)).Where(x => x.w != 0).Select(x => x.i).ToList();
        for (var k = 0; k < Math.Min(keys.Count, words.Count); k++)
        {
            var key = keys[k];
            var word = words[k];
            var inner = BlobStoreContent(store(key)) ?? throw new BlobError($"blob store record {key} missing");
            var data = DecodePaged(inner);
            var expect = Math.Min(chunk, (long)total - word * chunk);
            if (data is null || data.Length != expect)
                throw new BlobError($"blob store record {key}: {(data is null ? "None" : data.Length.ToString())} bytes, expected {expect}");
            SliceAssign(ref outb, word * chunk, word * chunk + expect, data);
        }
        return outb;
    }

    /// <summary>Plain content of a blob value, or null if the layout is not recognised. Pages in store records (kind 4) are
    /// read through store, by default the one bound to the value (BindStore); raises BlobError if they cannot be read.</summary>
    public static byte[]? DecodeBlob(byte[]? raw, Func<string, byte[]?>? store = null)
    {
        if (raw is null || raw.Length == 0)
            return [];
        if (raw[0] == 4)
        {
            var keys = ExternalRefs(raw);
            if (keys is not null)
                return DecodeExternal(raw, keys, store ?? StoreOf(raw));
            var (n, o) = Varint(raw, 1);
            return o + n == raw.Length ? Py.Slice(raw, o, o + n) : null;
        }
        return DecodePaged(raw);
    }
}
