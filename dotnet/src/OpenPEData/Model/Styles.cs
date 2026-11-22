using System.Collections.Frozen;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>WinCC Unified styles in the PEData (port of styles.py, docs/spec/hmi-styles.md): ILStyles.StyleData per device
/// with the style values per item type (IPropertyExpandoAttributes), font references and rendering templates (SVG).</summary>
internal static class Styles
{
    const int HdrMark = 0x10;

    static readonly Dictionary<uint, (string Name, int? Size)> Types = new()
    {
        [0x80000001] = ("bool", 1), [0x80000002] = ("uint8", 1), [0x80000005] = ("int32", 4), [0x80000007] = ("float", 4),
        [0x80000008] = ("double", 8), [0x8000000B] = ("string", null), [0x8000000C] = ("guid", 16), [0x8000000D] = ("bytes", null),
        [0x80000012] = ("uint16", 2), [0x80000013] = ("uint32", 4), [0x80000015] = ("object", null), [0x0001600D] = ("color", null),
    };

    public static readonly FrozenSet<string> Meta = new string[] { "_Guid", "_TypeId", "IsDefault", "StyleItemType" }.ToFrozenSet();

    static uint U32(byte[] b, long o) => Bin.U32(b, o);

    static PyDict Body(byte[] b, long o)
    {
        var n = U32(b, o + 4);
        var doff = U32(b, o + 8);
        var codes = Enumerable.Range(0, (int)n).Select(i => U32(b, o + 12 + 4 * i)).ToList();
        var p = o + 12 + 4L * n;
        var names = new List<string>();
        for (var i = 0; i < n; i++)
        {
            var ln = Py.At(b, p);
            names.Add(Py.Utf8Exact(Py.Slice(b, p + 1, p + ln)));
            p += ln;
        }
        var d = o + doff;
        if (p != d)
            throw new PyValueError($"names end at 0x{p:x}, data at 0x{d:x}");
        var outd = new PyDict();
        var var_ = new List<(string Name, string Kind, long A)>();
        var q = d;
        for (var i = 0; i < Math.Min(codes.Count, names.Count); i++)
        {
            var (code, name) = (codes[i], names[i]);
            if (!Types.TryGetValue(code, out var ty))
                throw new PyValueError($"unknown type code 0x{code:x} ({name})");
            if (ty.Size is null)
            {
                var_.Add((name, ty.Name, d + U32(b, q)));
                q += 4;
                continue;
            }
            var raw = Py.Slice(b, q, q + ty.Size.Value);
            q += ty.Size.Value;
            outd[name] = ty.Name switch
            {
                "bool" => Py.At(raw, 0) != 0,
                "int32" => (long)Bin.I32(raw, 0) is var iv && raw.Length == 4 ? iv : throw new StructError("unpack requires a buffer of 4 bytes"),
                "float" => raw.Length == 4 ? (double)Bin.F32(raw, 0) : throw new StructError("unpack requires a buffer of 4 bytes"),
                "double" => raw.Length == 8 ? Bin.F64(raw, 0) : throw new StructError("unpack requires a buffer of 8 bytes"),
                "guid" => Py.HexStr(raw),
                _ => Py.Int(Bin.FromLe(raw, 0, raw.Length)),
            };
        }
        if (var_.Count > 0 && var_.Min(x => x.A) != q)
            throw new PyValueError("variable part does not follow the fixed part");
        foreach (var (name, kind, a) in var_)
        {
            if (kind == "string")
            {
                var ln = Py.At(b, a);
                outd[name] = ln != 0 ? Py.Utf8(Py.Slice(b, a + 1, a + ln)) : null;
            }
            else if (kind == "color")
                outd[name] = $"0x{U32(b, a + 4):X8}";
            else if (kind == "bytes")
                outd[name] = Unwrap(b, a);
            else
                outd[name] = DecodeObject(b, a);
        }
        return outd;
    }

    /// <summary>Flag (0 plain, 1 zlib chunks), u32 payload length, 9 zero bytes, 0x10, u32 chunk count n, 1 + 4n bytes,
    /// then the payload (plain) or n chunks of varint length + zlib data.</summary>
    public static byte[] Unwrap(byte[] b, long o)
    {
        var flag = Py.At(b, o);
        var ln = U32(b, o + 1);
        if (Py.Slice(b, o + 5, o + 14).Length != 9 || Py.Slice(b, o + 5, o + 14).Any(x => x != 0) || Py.At(b, o + 14) != HdrMark)
            throw new PyValueError($"unexpected object header at 0x{o:x}");
        var n = U32(b, o + 15);
        var p = o + 20 + 4L * n;
        if (flag == 0)
            return Py.Slice(b, p, p + ln);
        var data = new List<byte>();
        for (var i = 0; i < n; i++)
        {
            var (zl, z) = Attrs.Varint(b, p);
            data.AddRange(Zlib.Decompress(Py.Slice(b, z, z + zl)));
            p = z + zl;
        }
        if (data.Count < ln)
            throw new PyValueError("unpacked length too short");
        return data.Take((int)ln).ToArray();
    }

    public static PyDict DecodeObject(byte[] b, long o) => Body(Unwrap(b, o), 0);

    /// <summary>Expando value of a StyleData property: varint total length, then one object. Values that are not a blob are
    /// returned unchanged; a blob that does not decode gives {"error", "hex"}.</summary>
    public static object? DecodeStyleValue(object? rawv)
    {
        if (rawv is not byte[] raw)
            return rawv;
        try
        {
            var (total, o) = Attrs.Varint(raw, 0);
            if (total != raw.Length)
                throw new PyValueError("length prefix differs");
            return DecodeObject(raw, o);
        }
        catch (Exception e) when (e is PyValueError or PyIndexError or StructError or ZlibError || Attrs.IsDecodeError(e))
        {
            return PyDict.Of("error", e.Message, "hex", Py.HexStr(Py.Slice(raw, 0, 64)));
        }
    }

    public static object? StripMeta(object? v)
    {
        if (v is PyDict d)
        {
            var r = new PyDict();
            foreach (var (k, x) in d)
                if (k is not string ks || !Meta.Contains(ks))
                    r[k] = StripMeta(x);
            return r;
        }
        return v;
    }

    /// <summary>Reference expando of a StyleData: {4-character key: name of the linked object}.</summary>
    public static PyDict References(Project p, string k)
    {
        var refs = p.Sets(k).Set("IReferenceExpandoAttributesSimple");
        var links = p.Targets(k, "ExpandoReferences");
        var outd = new PyDict();
        foreach (var (key, v) in refs)
        {
            var i = (long)Bin.FromBe(Py.Bytes(v) ?? throw new PyTypeError("'NoneType' object is not subscriptable"), 0, 4);
            outd[key] = i < links.Count ? p.Name(links[(int)i]) : null;
        }
        return outd;
    }

    public static object? ResolveFonts(object? v, PyDict refs)
    {
        if (v is PyDict d)
        {
            var keys = d.Keys.Where(k => k is not string ks || !Meta.Contains(ks)).ToList();
            if (keys.Count == 1 && Py.Eq(keys[0], "Description") && refs.ContainsKey(d["Description"]))
            {
                var r = new PyDict(d);
                r["Description"] = refs[d["Description"]];
                return r;
            }
            var o = new PyDict();
            foreach (var (k, x) in d)
                o[k] = ResolveFonts(x, refs);
            return o;
        }
        return v;
    }

    /// <summary>{style key: (name, system style name, {type: values})}.</summary>
    public static OrderedDictionary<string, (string? Name, object? SystemName, PyDict Values)> All(Project p, bool fonts = true)
    {
        var res = new OrderedDictionary<string, (string?, object?, PyDict)>();
        foreach (var k in p.Objects.Keys)
        {
            if (p.TypeName(k) != "StyleData")
                continue;
            var props = p.Sets(k).Set("IPropertyExpandoAttributes");
            var sysname = props.Get("SystemStyleName");
            var refs = fonts ? References(p, k) : new PyDict();
            var values = new PyDict();
            foreach (var (t, v) in props)
            {
                if (Py.Eq(t, "SystemStyleName"))
                    continue;
                values[t] = DecodeStyleValue(v);
                if (fonts)
                    values[t] = ResolveFonts(values[t], refs);
            }
            res[k] = (p.Name(k), sysname, values);
        }
        return res;
    }

    /// <summary>Rendering templates of a StyleData: {template name: SVG bytes}.</summary>
    public static PyDict Templates(Project p, string k)
    {
        var outd = new PyDict();
        foreach (var (name, v) in p.Sets(k).Set("IRenderingTemplateExpandoContainer"))
        {
            var o = DecodeStyleValue(v);
            outd[name] = o is PyDict od ? od.Get("Content") : throw new PyAttributeError($"'{Py.TypeName(o)}' object has no attribute 'get'");
        }
        return outd;
    }

    /// <summary>[(settings key, device key, selected style key, [system style keys])].</summary>
    public static List<(string Key, string? Device, string? Selected, List<string> System)> Settings(Project p)
    {
        var outl = new List<(string, string?, string?, List<string>)>();
        foreach (var k in p.Objects.Keys)
        {
            if (p.TypeName(k) != "StyleSettingsData")
                continue;
            var dev = p.Targets(k, "StyleSettingsParent");
            var sel = p.Targets(k, "IStyleSettingsAttributes_SelectedStyle");
            var sys = p.Targets(k, "IStyleSettingsAttributes_SystemStyles");
            outl.Add((k, dev.Count > 0 ? dev[0] : null, sel.Count > 0 ? sel[0] : null, sys));
        }
        return outl;
    }

    /// <summary>Openness enum names, loaded once (thread-safe). One version so far; with a second one the profile comes from
    /// the project.</summary>
    static readonly Lazy<PyDict> enums = new(() =>
    {
        using var s = typeof(Styles).Assembly.GetManifestResourceStream(VersionProfile.V21.EnumsResource)!;
        using var r = new StreamReader(s, System.Text.Encoding.UTF8);
        return (PyDict)PyJson.Loads(r.ReadToEnd())!;
    });

    static PyDict Enums() => enums.Value;

    /// <summary>Openness name of an enum style value; flags as "A, B" in value order; the number itself when the property is
    /// no known enum.</summary>
    public static object? EnumName(object? prop, object? value)
    {
        var e = Enums();
        var t = ((PyDict)e["properties"]!).Get(prop);
        if (t is null || value is bool || !Py.IsInt(value))
            return value;
        var en = (PyDict)((PyDict)e["enums"]!)[t]!;
        var vals = (PyDict)en["values"]!;
        var names = new PyDict();
        foreach (var (n, v) in vals)
            names[v] = n;
        if (names.ContainsKey(value))
            return names[value];
        if (Py.Bool(en["flags"]))
        {
            var val = Py.Big(value);
            var parts = Py.Sorted(vals.Items, x => x.Value).Where(x => Py.Bool(x.Value) && (val & Py.Big(x.Value)) == Py.Big(x.Value)).Select(x => (string)x.Key!).ToList();
            if (parts.Aggregate(System.Numerics.BigInteger.Zero, (s, n) => s + Py.Big(vals[n])) == val)
                return string.Join(", ", parts);
        }
        return value;
    }

    public static object? EnumNames(object? v)
    {
        if (v is PyDict d)
        {
            var r = new PyDict();
            foreach (var (k, x) in d)
                r[k] = x is PyDict ? EnumNames(x) : EnumName(k, x);
            return r;
        }
        return v;
    }
}
