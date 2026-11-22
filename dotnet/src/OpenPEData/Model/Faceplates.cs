// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Text;
using System.Text.RegularExpressions;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>WinCC Unified faceplate types and instances (port of faceplates.py, step F3): per type the versions with content
/// (screen items, interface, script module), hidden device counts, local tags, mediators, and the typed / untyped
/// instances with their interface values.</summary>
internal static class Faceplates
{
    /// <summary>IVersionInfo.Version (u32 20, i32 Build, Major, Minor, Revision) -> 'Major.Minor.Build'.</summary>
    public static string? DecodeVersionText(object? raw)
    {
        if (raw is not byte[] b || b.Length < 20)
            return null;
        return $"{Bin.I32(b, 8)}.{Bin.I32(b, 12)}.{Bin.I32(b, 4)}";
    }

    const string FaceplateType = "FaceplateLibraryTypeData";
    const string ContainerType = "ILFaceplateContainerData";
    static readonly string[] VersionFields = ["version", "state", "isDefault", "finalizationTime", "author", "comment", "minimumTargetDeviceVersion",
        "versionGuid", "creationTime", "modifiedTime", "lastModifiedBy"];
    static readonly string[] TypeFields = ["name", "typeGuid", "folderPath", "isObsolete", "defaultVersion", "latestVersion", "instanceCount"];
    static readonly Regex FontListContent = new(@"^FontList_C\d+$", RegexOptions.CultureInvariant);
    static readonly Dictionary<long, (int Size, char Kind)> MapFixed = new()
    {
        [0x02] = (1, 'B'), [0x04] = (4, 'I'), [0x06] = (8, 'q'), [0x07] = (4, 'f'), [0x08] = (8, 'd'), [0x0a] = (8, 'q'), [0x14] = (8, 'q'),
    };

    static readonly Dictionary<long, string> DataTypes = new()
    {
        [1] = "Bool", [3] = "Int", [5] = "LInt", [9] = "ULInt", [10] = "Real", [11] = "LReal", [13] = "DateTime", [14] = "WString",
        [30] = "Struct", [1282] = "Color", [65542] = "Graphic", [65544] = "Multilingual text", [65555] = "Authorization",
        [65575] = "Parameter set type", [65585] = "Resource list",
    };

    static readonly HashSet<string> ContentTypes = ["HmiArrayMemberTagData", "HmiStructureMemberTagData", "CpmUdtData", "CpmHmiStructureMemberTypeData",
        "ImageInstanceData", "TextListInstanceData", "ScopedLinkMaintainerData", "HmiFunctionListEmbeddedData", "FaceplateInstanceMediatorData",
        "DynamicSVGInstanceMediatorData"];

    public const string LayersProperty = "K\x02";

    static string Short(Project p, string k) => Py.AfterLast(p.TypeName(k) ?? "", ".");

    static string Hx(long c) => "0x" + c.ToString("x");

    /// <summary>pe:MapT value -> {key: value} or null (raw: pe blob or plain map: u32 length, u32 count, u32 offset of the
    /// value area, count type codes, count keys varint(n+1) + UTF-8, values in key order).</summary>
    public static PyDict? DecodeMap(object? rawv)
    {
        if (rawv is not byte[] raw || raw.Length < 12)
            return null;
        try
        {
            var (n0, q0) = Attrs.Varint(raw, 0);
            if (24 < n0 && n0 <= raw.Length && q0 < raw.Length && raw[q0] is 0 or 1 or 4)
                raw = Py.Slice(raw, q0, n0);
            if (Py.At(raw, 0) is 0 or 1 or 4 && Bin.U32(raw, 0) != raw.Length)
            {
                var total = Bin.U64(raw, 1);
                if (total > 64UL * 1024 * 1024 || (raw[0] == 0 && total + 24 > (ulong)raw.Length))
                    return null;
                var dec = Attrs.DecodeBlob(raw);
                if (dec is null || dec.Length < 12)
                    return null;
                raw = dec;
            }
            var plen = Bin.U32(raw, 0);
            var cnt = Bin.U32(raw, 4);
            var voff = Bin.U32(raw, 8);
            if (plen != raw.Length || cnt > 10000)
                return null;
            var codes = Bin.U32s(raw, 12, cnt);
            long q = 12 + 4L * cnt;
            var keys = new List<string>();
            for (var i = 0; i < cnt; i++)
            {
                var (n, qq) = Attrs.Varint(raw, q);
                q = qq;
                keys.Add(Py.Utf8(Py.Slice(raw, q, q + n - 1)));
                q += Math.Max(n - 1, 0);
            }
            var outd = new PyDict();
            long v = voff;
            for (var i = 0; i < Math.Min(keys.Count, codes.Length); i++)
            {
                var key = keys[i];
                long c = codes[i] & 0x7fffffff;
                if (MapFixed.TryGetValue(c, out var fx))
                {
                    outd[key] = fx.Kind switch
                    {
                        'B' => (long)Bin.U8(raw, v),
                        'I' => (long)Bin.U32(raw, v),
                        'q' => Bin.I64(raw, v),
                        'f' => (double)Bin.F32(raw, v),
                        _ => (object)Bin.F64(raw, v),
                    };
                    v += fx.Size;
                }
                else if (c is 0x0e or 0x0d)
                {
                    var (n, s) = Attrs.Varint(raw, voff + (long)Bin.U32(raw, v));
                    var b = Py.Slice(raw, s, s + n - 1);
                    outd[key] = c == 0x0e ? Py.Utf8(b) : "hex:" + Py.HexStr(b);
                    v += 4;
                }
                else if (c is 0x05 or 0x13)
                {
                    outd[key] = (long)Bin.I32(raw, v);
                    v += 4;
                }
                else if (c == 0x0c)
                {
                    var g = Py.Slice(raw, v, v + 16);
                    if (g.Length != 16)
                        throw new PyValueError("bytes_le is not a 16-char string");
                    outd[key] = Attrs.Guid(g);
                    v += 16;
                }
                else if (c == 0x01)
                {
                    outd[key] = Py.At(raw, v) != 0;
                    v += 1;
                }
                else if (c == 0x0b)
                {
                    var (n, s) = Attrs.Varint(raw, voff + (long)Bin.U32(raw, v));
                    outd[key] = Py.Utf8(Py.Slice(raw, s, s + n - 1));
                    v += 4;
                }
                else if (c == 0x15)
                {
                    outd[key] = DecodeMap(Py.Slice(raw, voff + (long)Bin.U32(raw, v)));
                    v += 4;
                }
                else if (c == 0xcd4001)
                {
                    var o = voff + (long)Bin.U32(raw, v);
                    outd[key] = DecodeMapArray(Py.Slice(raw, o, o + Bin.U32(raw, o)));
                    v += 4;
                }
                else if (c > 0xffff && voff + (long)Bin.U32(raw, v) + 4 <= raw.Length && Bin.U32(raw, voff + (long)Bin.U32(raw, v)) <= raw.Length)
                {
                    var o = voff + (long)Bin.U32(raw, v);
                    var blk = Py.Slice(raw, o, o + Bin.U32(raw, o));
                    var arr = blk.Length > 8 ? DecodeMapArray(blk) : [];
                    var strs = blk.Length > 8 ? StringArray(blk) : null;
                    if (blk.Length == 8)
                        outd[key] = (long)Bin.U32(blk, 4);
                    else if (strs is not null)
                        outd[key] = strs;
                    else if (arr.Count > 0 && arr.All(x => x is PyDict))
                        outd[key] = arr;
                    else
                        outd[key] = $"type {Hx(c)} hex:{Py.HexStr(blk)}";
                    v += 4;
                }
                else
                {
                    outd[key] = Py.T("code " + Hx(c), null);
                    return outd;
                }
            }
            return outd;
        }
        catch (Exception e) when (Attrs.IsDecodeError(e) || e is PyValueError)
        {
            return null;
        }
    }

    /// <summary>Array of strings (u32 length, u32 count, count x u32 offset, elements varint(n+1) + UTF-8) or null.</summary>
    static List<object?>? StringArray(byte[] raw)
    {
        try
        {
            var ln = Bin.U32(raw, 0);
            var cnt = Bin.U32(raw, 4);
            if (ln != raw.Length || cnt == 0 || 8 + 4L * cnt > ln)
                return null;
            var offs = Bin.U32s(raw, 8, cnt).Select(x => (long)x).Append(ln).ToList();
            var outl = new List<object?>();
            for (var i = 0; i < cnt; i++)
            {
                var (n, q) = Attrs.Varint(raw, offs[i]);
                if (n < 1 || q + n - 1 != offs[i + 1])
                    return null;
                outl.Add(Py.Utf8(Py.Slice(raw, q, q + n - 1)));
            }
            return outl;
        }
        catch (Exception e) when (Attrs.IsDecodeError(e))
        {
            return null;
        }
    }

    /// <summary>CoreMapArray: u32 length, u32 count, count x u32 element offset, elements -> [map].</summary>
    public static List<object?> DecodeMapArray(object? rawv)
    {
        if (rawv is not byte[] raw || raw.Length < 8)
            return [];
        try
        {
            var ln = Bin.U32(raw, 0);
            var cnt = Bin.U32(raw, 4);
            if (ln > raw.Length || 8 + 4L * cnt > ln)
                return [];
            var offs = Bin.U32s(raw, 8, cnt).Select(x => (long)x).Append(ln).ToList();
            return Enumerable.Range(0, (int)cnt).Select(i => (object?)DecodeMap(Py.Slice(raw, offs[i], offs[i + 1]))).ToList();
        }
        catch (StructError)
        {
            return [];
        }
    }

    /// <summary>Object referenced by an IReferenceExpandoAttributes (or IPropertyDynamicLinkExpandoAttributes) entry of k.</summary>
    static string? Ref(Project p, string k, object? entry, string setName = "IReferenceExpandoAttributes")
    {
        var v = entry is string e ? p.Sets(k).Set(setName).Get(e) : null;
        if (v is not byte[] b || b.Length < 4)
            return null;
        var refs = p.Links(k, "ExpandoReferences").Select(l => l.Key).ToList();
        var idx = (long)Bin.FromBe(b, 0, 4);
        return idx < refs.Count && p.Objects.ContainsKey(refs[(int)idx]) ? refs[(int)idx] : null;
    }

    static PyDict Interface(ProjectModel m, string c)
    {
        var p = m.P;
        var o = p.Objects[c];
        var fi = p.Sets(c).Set("IFaceplateInterface");
        var tags = new List<object?>();
        foreach (var e0 in DecodeMapArray(fi.Get("TagInterfaceProperties")))
        {
            var e = new PyDict(e0 as PyDict ?? new PyDict());
            var tag = Ref(p, c, e.Pop("Tag"));
            var dt = Ref(p, c, e.Pop("_BlDataType"));
            var d = PyDict.Of("name", e.Pop("Name"), "dataType", dt is not null ? p.Name(dt) : null, "dataTypeCode", e.Pop("DataType"),
                "tag", tag is not null ? p.Name(tag) : null,
                "acquisitionCycle", tag is not null ? p.Name(m.Rel1(tag, "IHmiTagAttributes_AcquisitionCycle")) : null,
                "guid", e.Pop("_Guid"), "typeId", e.Pop("_TypeId"));
            if (e.ContainsKey("_SelectedStructureType"))
            {
                d["structureType"] = e.Pop("_SelectedStructureType");
                e.Pop("_UDTStructure");
                var lv = Ref(p, c, e.Pop("_UDTStructureInLib"));
                var lt = lv is not null ? m.Rel1(lv, "VersionObjectToTypeObject") : null;
                d["udt"] = lt is not null ? p.Name(lt) : null;
                d["udtVersion"] = lv is not null ? DecodeVersionText(p.Attr(lv, "IVersionInfo", "Version")) : null;
            }
            var bounds = e.Pop("_BLDataTypeBounds");
            if (bounds is not null && d["dataTypeCode"] is not null && (Py.Long(d["dataTypeCode"]) & 0x40) != 0)
            {
                d["elementType"] = d["dataType"];
                d["bounds"] = bounds;
                d["dataType"] = $"Array[{((string)bounds).Replace(":", "..")}] of {Py.Str(d["elementType"])}";
            }
            if (e.Count > 0)
                d["other"] = e;
            d["key"] = tag;
            tags.Add(d);
        }
        var table = m.Rel1(c, "ILFaceplateToTagTable");
        return PyDict.Of("present", o.BlockOrder.Any(i => o.SetName(i) == "IFaceplateInterface"), "tags", tags,
            "tagTable", table is not null ? p.Name(table) : null,
            "properties", DecodeMapArray(fi.Get("InterfaceProperties")).Select(x => (object?)IfaceProperty(x)).ToList(),
            "events", DecodeMapArray(fi.Get("InterfaceEvents")).Select(x => (object?)IfaceEvent(x)).ToList());
    }

    static PyDict IfaceEvent(object? e0)
    {
        var e = new PyDict(e0 as PyDict ?? new PyDict());
        var d = PyDict.Of("name", e.Pop("Name"), "guid", e.Pop("_Guid"), "typeId", e.Pop("_TypeId"));
        var pars = e.Pop("InterfaceEventParameters");
        d["parameters"] = (Py.Bool(pars) ? (List<object?>)pars! : []).Select(x0 =>
        {
            var x = x0 as PyDict ?? new PyDict();
            return (object?)PyDict.Of("name", x.Get("Name"), "dataType", x.Get("DataType", "Int"), "dataTypeStored", x.ContainsKey("DataType"), "guid", x.Get("_Guid"));
        }).ToList();
        if (e.Count > 0)
            d["other"] = e;
        return d;
    }

    static PyDict IfaceProperty(object? e0)
    {
        var e = new PyDict(e0 as PyDict ?? new PyDict());
        var code = e.Pop("DataType");
        var d = PyDict.Of("name", e.Pop("Name"), "dataType", code is not null && Py.IsInt(code) && DataTypes.TryGetValue(Py.Long(code), out var dn) ? dn : null,
            "dataTypeCode", code, "default", e.Pop("Default"), "guid", e.Pop("_Guid"), "typeId", e.Pop("_TypeId"));
        if (Py.Eq(d["dataTypeCode"], 1282L) && Py.IsInt(d["default"]))
            d["default"] = Hmi.Fmt08X(d["default"]);
        if (e.Count > 0)
            d["other"] = e;
        return d;
    }

    /// <summary>Name of a tag as Openness shows it: structure members with the path of their root tag (Tag.member).</summary>
    static string? TagName(ProjectModel m, string? k)
    {
        var p = m.P;
        var parts = new List<string?>();
        while (k is not null && Short(p, k) == "HmiStructureMemberTagData")
        {
            parts.Add(p.Name(k));
            k = m.Rel1(k, "MembersInverse");
        }
        if (k is not null)
            parts.Add(p.Name(k));
        parts.Reverse();
        var s = string.Join(".", parts.Select(x => x ?? throw new PyTypeError("sequence item: expected str instance, NoneType found")));
        return s.Length > 0 ? s : null;
    }

    static readonly Regex TagRefNum = new("'(\\d+)'", RegexOptions.CultureInvariant);

    static PyDict? Dynamization(ProjectModel m, string k, object? entry)
    {
        var p = m.P;
        var d = DecodeMap(p.Sets(k).Set("IPropertyDynamicExpandoAttributes").Get(entry));
        if (d is null)
            return null;
        string? Link(object? x)
        {
            var t = Ref(p, k, x, "IPropertyDynamicLinkExpandoAttributes");
            return t is not null && Short(p, t).Contains("Tag") ? TagName(m, t) : t is not null ? p.Name(t) : null;
        }
        var o = PyDict.Of("type", d.Get("Type"));
        if (Py.Eq(d.Get("Type"), "FaceplateInterface") && Py.Bool(d.Get("PropertyName")))
            o["interfaceProperty"] = d["PropertyName"];
        if (Py.Bool(d.Get("Tag")))
            o["tag"] = Link(d["Tag"]);
        if (Py.Bool(d.Get("ResourceList")))
            o["resourceList"] = Link(d["ResourceList"]);
        if (d.Get("FormatString") is not null)
        {
            var refs = d.Get("TagRefs");
            var names = (Py.Bool(refs) ? ((List<object?>)refs!) : []).Where(x => x is string).Select(Link).ToList();
            o["formatString"] = d["FormatString"];
            o["dynamicTagName"] = TagRefNum.Replace((string)d["FormatString"]!, mo =>
            {
                var i = Py.ParseInt(mo.Groups[1].Value);
                return "'" + (i < names.Count ? Py.Str(names[(int)i]) : mo.Groups[1].Value) + "'";
            });
        }
        if (d.ContainsKey("TwoWay"))
            o["twoWay"] = d["TwoWay"];
        if (d.Get("ValueConverter") is PyDict vc)
            o["valueConverter"] = vc;
        return o;
    }

    static object? PropertyValue(ProjectModel m, string k, object? val)
    {
        var p = m.P;
        var refs = p.Sets(k).Set("IReferenceExpandoAttributes");
        if (val is string vs && refs.ContainsKey(vs))
        {
            var rv = refs[vs];
            if (rv is byte[] rb && rb.Length == 5)
            {
                if ((rb[4] & 0x80) != 0)
                {
                    var kids = p.Links(k, "ExpandoChildren").Select(l => l.Key).ToList();
                    var i = (long)Bin.FromBe(rb, 0, 4);
                    return i < kids.Count && p.Objects.ContainsKey(kids[(int)i]) ? Hmi.LibraryImageName(p, kids[(int)i]) : null;
                }
                var t = Ref(p, k, vs);
                return t is not null ? p.Name(t) : null;
            }
            if (rv is byte[] rb2)
            {
                var r = new PyDict();
                foreach (var (c, x) in m.CoreTextOf(rb2))
                    if (!Py.Eq(c, "127"))
                        r[Py.Eq(c, "65535") ? "" : c] = x;
                return r;
            }
            return rv;
        }
        return val is byte[] vb ? Py.HexStr(vb) : val;
    }

    static List<object?> InstanceValues(ProjectModel m, string k)
    {
        var p = m.P;
        var d = DecodeMap(p.Sets(k).Set("IPropertyExpandoAttributes").Get("Properties"));
        if (d is null)
            return [];
        var dyn0 = d.Get("_#Dynamics");
        var dyn = Py.Bool(dyn0) ? (PyDict)dyn0! : new PyDict();
        var outl = new List<object?>();
        foreach (var (namev, val) in d)
        {
            var name = (string)namev!;
            if (name.StartsWith('_'))
                continue;
            var e = PyDict.Of("name", name);
            if (val is PyDict vd && (vd.ContainsKey("Tag") || Py.Eq(vd.Get("_TypeId"), 4710L)))
            {
                var t = Ref(p, k, vd.Get("Tag"));
                e["kind"] = "tag";
                e["value"] = t is not null ? TagName(m, t) : null;
                var vdyn = vd.Get("_#Dynamics");
                foreach (var de in (Py.Bool(vdyn) ? (PyDict)vdyn! : new PyDict()).Values)
                    e["dynamization"] = Dynamization(m, k, de);
            }
            else
            {
                e["kind"] = "property";
                e["value"] = PropertyValue(m, k, val);
            }
            if (dyn.ContainsKey(name))
                e["dynamization"] = Dynamization(m, k, dyn[name]);
            outl.Add(e);
        }
        foreach (var (name, de) in dyn)
            if (!d.ContainsKey(name))
                outl.Add(PyDict.Of("name", name, "kind", "property", "value", null, "dynamization", Dynamization(m, k, de)));
        return outl;
    }

    /// <summary>Script token table -> text.</summary>
    static object? CodeText(object? v)
    {
        if (v is not byte[] b)
            return v;
        try
        {
            var cnt = Bin.U32(b, 4);
            var sb = new StringBuilder();
            foreach (var off in Bin.U32s(b, 8, cnt))
            {
                var (n, q) = Attrs.Varint(b, off + 9L);
                sb.Append(Py.Utf8(Py.Slice(b, q, q + n - 1)));
            }
            return sb.ToString();
        }
        catch (Exception e) when (Attrs.IsDecodeError(e))
        {
            return null;
        }
    }

    static PyDict? ScriptModule(ProjectModel m, string c)
    {
        var k = m.Rel1(c, "ScriptModuleContainer");
        if (k is null)
            return null;
        var a = m.P.Sets(k).Set("IScriptModuleContainerAttributes");
        return PyDict.Of("scope", a.Get("Scope"), "language", a.Get("Language"),
            "globalDefinitions", PyDict.Of("events", CodeText(a.Get("Event_GlobalSectionCode")),
                "dynamizations", CodeText(a.Get("Trigger_GlobalSectionCode")),
                "functionList", CodeText(a.Get("FunctionList_GlobalSectionCode"))));
    }

    static readonly Dictionary<long, string> PartNames = new() { [0x0051] = "Caption", [0x021c] = "Font", [0x054e] = "FontList" };

    /// <summary>Map-valued property expandos of a screen item (sub-objects such as the caption with its font):
    /// {"0x0051 Caption": {...}}; values naming an IReferenceExpandoAttributes entry are resolved.</summary>
    public static PyDict Parts(Project p, string k)
    {
        var ex = p.Sets(k).Set("IPropertyExpandoAttributes");
        var refs = p.Sets(k).Set("IReferenceExpandoAttributes");

        (string Name, string? Kind) Name(string key)
        {
            var kb = key.Select(ch => ch <= 0xFF ? (byte)ch : (byte)'?').ToArray();
            var pid = new System.Numerics.BigInteger(kb, isUnsigned: true, isBigEndian: false);
            string? n = pid <= long.MaxValue && PartNames.TryGetValue((long)pid, out var pn) ? pn : null;
            string? kind = null;
            if (n is null && Hmi.PropertyNames.TryGetValue(Py.HexStr(kb), out var pr))
                (n, kind) = (pr.Name, pr.Kind);
            var hex = pid.IsZero ? "0" : pid.ToString("x").TrimStart('0');
            var num = "0x" + hex.PadLeft(4, '0');
            return (Py.Strip(num + " " + (n ?? "")), kind);
        }

        object? Res(object? x)
        {
            if (x is PyDict d)
            {
                var o = new PyDict();
                foreach (var (kkv, vv) in d)
                {
                    var kk = (string)kkv!;
                    if (kk.StartsWith('_'))
                    {
                        o[kk] = Res(vv);
                        continue;
                    }
                    var (nm, kind) = Name(kk);
                    o[nm] = kind == "color" && Py.IsInt(vv) ? Hmi.Fmt08X(vv) : Res(vv);
                }
                return o;
            }
            if (x is List<object?> l)
                return l.Select(Res).ToList();
            if (x is string s && refs.ContainsKey(s))
            {
                var t = Ref(p, k, s);
                return t is not null ? p.Name(t) : refs[s] is string rs ? rs : null;
            }
            return x;
        }

        var parts = new PyDict();
        foreach (var (keyv, val) in ex)
        {
            var key = (string)keyv!;
            if (key.StartsWith('_') || key is "Properties" or LayersProperty || val is not byte[] vb)
                continue;
            object? d = DecodeMap(vb);
            if (d is null || (d is PyDict dd && dd.Values.Any(x => x is PyTuple)))
            {
                var arr = DecodeMapArray(vb);
                if (arr.Count > 0 && arr.All(x => x is PyDict))
                    d = arr;
            }
            if (d is PyDict or List<object?>)
                parts[Name(key).Name] = Res(d);
        }
        return parts;
    }

    static PyDict Content(ProjectModel m, Hmi h, string c)
    {
        var p = m.P;
        var props = h.ScreenProperties(c);
        var items = new List<object?>();
        foreach (var it in h.Items(c, h.Layers(c)))
        {
            var ip = (PyDict)it["properties"]!;
            var key = (string)it["key"]!;
            var inner = Py.Eq(it["type"], "HmiFaceplateContainer") ? m.Rel1(key, "IsInstanceOf") : null;
            var d = PyDict.Of("name", it["name"], "type", it["type"],
                "containedType", inner is not null ? $"V{Py.Str(DecodeVersionText(p.Attr(inner, "IVersionInfo", "Version")))}\\{Py.Str(p.Name(m.Rel1(inner, "VersionObjectToTypeObject")))}" : null,
                "left", ip.Get("Left", ip.Get("CenterX")), "top", ip.Get("Top", ip.Get("CenterY")),
                "width", it["width"], "height", it["height"], "text", it["text"], "tags", it["tags"],
                "properties", ip, "parts", Parts(p, key), "dynamizations", it["dynamizations"],
                "events", ((List<object?>)it["events"]!).Where(e => !Py.Str(((PyDict)e!).Get("kind")).EndsWith(".Trigger", StringComparison.Ordinal)).ToList(),
                "layerIndex", it["layerIndex"], "zIndex", it["zIndex"], "parent", it["parent"]);
            if (inner is not null)
                d["interface"] = InstanceValues(m, key);
            d["key"] = key;
            items.Add(d);
        }
        var ex = p.Sets(c).Set("IPropertyExpandoAttributes");
        var refs = p.Sets(c).Set("IReferenceExpandoAttributes");
        var layers = DecodeMapArray(ex.Get(LayersProperty)).Select(x0 =>
        {
            var x = x0 as PyDict ?? new PyDict();
            return (Name: x.Get("\x03"), Guid: x.Get("_Guid"), Visible: x.Get("\x07", true), EsVisible: x.Get("_#ESVisible"));
        }).ToList();
        var active = refs.Get(ex.Get("_#ActiveLayer"));
        return PyDict.Of("width", props.Get("Width"), "height", props.Get("Height"), "items", items,
            "suspendable", ex.Get("c\x02"), "backColor", props.Get("BackColor"),
            "layers", layers.Select(x => x.Name).ToList(),
            "hiddenLayers", layers.Where(x => x.Visible is false).Select(x => x.Name).ToList(),
            "esHiddenLayers", layers.Where(x => x.EsVisible is false).Select(x => x.Name).ToList(),
            "activeLayer", layers.Any(x => Py.Eq(x.Guid, active)) ? layers.First(x => Py.Eq(x.Guid, active)).Name : active,
            "properties", DecodeMap(ex.Get("Properties")),
            "scriptModule", ScriptModule(m, c), "interface", Interface(m, c), "key", c);
    }

    static PyDict LocalTag(ProjectModel m, string k)
    {
        var p = m.P;
        var a = p.Sets(k).Set("IHmiTagStructureAttributes");
        var limits = new PyDict();
        foreach (var n in new[] { "Low", "LowHigh", "HighLow", "High" })
            if (a.Get($"Limit{n}Type") is not (null or "NoLimit"))
                limits[n] = a.Get($"Limit{n}Constant");
        var dt = m.Rel1(k, "DataTypeRef");
        return PyDict.Of("name", p.Name(k), "dataType", dt is not null ? p.Name(dt) : null,
            "table", p.Name(m.Rel1(k, "TagTable")), "acquisitionCycle", p.Name(m.Rel1(k, "IHmiTagAttributes_AcquisitionCycle")),
            "limits", limits, "startValue", Py.Or(a.Get("StartValue"), null), "key", k);
    }

    static PyDict Version(ProjectModel m, Hmi h, PyDict v)
    {
        var p = m.P;
        var vk = (string)v["key"]!;
        var e = new PyDict();
        foreach (var f in VersionFields)
            e[f] = v.Get(f);
        if ((e["finalizationTime"] as string ?? "").StartsWith("0001-", StringComparison.Ordinal))
            e["finalizationTime"] = null;
        var cok = v.Get("copiedObjectKeys");
        var mem = (Py.Bool(cok) ? (List<object?>)cok! : []).Select(x => (string)x!).Where(p.Objects.ContainsKey).ToList();
        var c = m.Rel1(vk, "VersionToContent");
        e["content"] = c is not null ? Content(m, h, c) : null;
        var shells = mem.Where(k => Short(p, k) == "HmiTagData" && m.Rel1(k, "TagTable") is null).ToList();
        e["unresolvedTags"] = Py.Sorted(shells.Select(p.Name), x => x).Cast<object?>().ToList();
        var scripts = mem.Where(k => Short(p, k) is "HmiScriptEmbeddedData" or "HmiScriptModuleContainerData").ToList();
        e["scripts"] = (long)scripts.Count(k => Short(p, k) == "HmiScriptEmbeddedData");
        e["environmentObjects"] = (long)mem.Count;
        var var_ = new HashSet<string>();
        if (e["content"] is PyDict cd)
            foreach (PyDict it in (List<object?>)cd["items"]!)
                var_.Add((string)it["key"]!);
        var_.UnionWith(shells);
        var_.UnionWith(scripts);
        var_.UnionWith(mem.Where(k => Short(p, k) == "HmiFontListData" && FontListContent.IsMatch(p.Name(k) ?? "")));
        var_.UnionWith(mem.Where(k => ContentTypes.Contains(Short(p, k))));
        var table = c is not null ? m.Rel1(c, "ILFaceplateToTagTable") : null;
        if (table is not null)
        {
            var_.Add(table);
            var_.UnionWith(p.Targets(table, "Content"));
        }
        var local = mem.Where(k => Short(p, k) == "HmiTagData" && m.Rel1(k, "TagTable") is { } tt && tt != table).ToList();
        var_.UnionWith(local);
        e["localTags"] = Py.Sorted(local, x => Py.Or(p.Name(x), "")).Select(k => (object?)LocalTag(m, k)).ToList();
        e["standardObjects"] = (long)mem.Count(k => !var_.Contains(k));
        e["instances"] = Py.Bool(v.Get("instances")) ? v["instances"] : new List<object?>();
        object? Cult(string k)
        {
            var s = Py.BeforeFirst(p.Name(k) ?? "", "(");
            return s.Length > 0 ? s : null;
        }
        var rc = m.Rel1(vk, "ReferenceCulture");
        var ec = m.Rel1(vk, "EditingCulture");
        e["cultures"] = PyDict.Of("reference", rc is not null ? Cult(rc) : null, "editing", ec is not null ? Cult(ec) : null,
            "used", Py.Sorted(p.Targets(vk, "UsedCultures").Select(Cult), x => x).ToList());
        e["dependsOn"] = (Py.Bool(v.Get("dependsOn")) ? (List<object?>)v["dependsOn"]! : []).Select(x => (object?)$"{Py.Str(((PyDict)x!).Get("typeName"))} {Py.Str(((PyDict)x).Get("version"))}").ToList();
        e["usedBy"] = (Py.Bool(v.Get("usedBy")) ? (List<object?>)v["usedBy"]! : []).Select(x => (object?)$"{Py.Str(((PyDict)x!).Get("typeName"))} {Py.Str(((PyDict)x).Get("version"))}").ToList();
        e["key"] = vk;
        return e;
    }

    static PyDict Geometry(Hmi h, ProjectModel m, string k)
    {
        var ip = h.ScreenProperties(k);
        var d = PyDict.Of("left", ip.Get("Left"), "top", ip.Get("Top"), "width", ip.Get("Width"), "height", ip.Get("Height"));
        d["interface"] = InstanceValues(m, k);
        d["usedTags"] = m.P.Targets(k, "ILFaceplateContainer_UsedTag").Select(t => (object?)TagName(m, t)).ToList();
        var events = new List<object?>();
        d["events"] = events;
        foreach (var e in m.P.Targets(k, "EmbeddedScriptChildren"))
        {
            var ev = new PyDict(h.Script(e));
            ev["name"] = m.P.Name(e);
            ev["async"] = m.P.Attr(e, "IScriptSignatureAttributes", "IsAsync");
            if (Short(m.P, e) == "HmiFunctionListEmbeddedData")
            {
                ev["handler"] = "functionList";
                ev["functions"] = FunctionCalls(ev.Get("code") as string);
            }
            else
                ev["handler"] = "script";
            events.Add(ev);
        }
        return d;
    }

    static readonly Regex SysFctCall = new(@"HMIRuntime\.(\w+)\.SysFct\.(\w+)\((.*)\);\s*$", RegexOptions.CultureInvariant);

    /// <summary>System function calls of a function list: [{"function", "area", "arguments"}].</summary>
    static List<object?> FunctionCalls(string? code)
    {
        var outl = new List<object?>();
        foreach (var line in Py.SplitLines(code ?? ""))
        {
            var mt = SysFctCall.Match(Py.Strip(line));
            if (!mt.Success)
                continue;
            var args = new List<object?>();
            var cur = "";
            char? quote = null;
            foreach (var ch in mt.Groups[3].Value)
            {
                if (quote is { } qc)
                {
                    cur += ch;
                    quote = ch == qc && !cur.EndsWith("\\" + ch, StringComparison.Ordinal) ? null : quote;
                }
                else if (ch is '"' or '\'')
                {
                    cur += ch;
                    quote = ch;
                }
                else if (ch == ',')
                {
                    args.Add(Py.Strip(cur));
                    cur = "";
                }
                else
                    cur += ch;
            }
            if (Py.Strip(cur).Length > 0)
                args.Add(Py.Strip(cur));
            outl.Add(PyDict.Of("function", mt.Groups[2].Value, "area", mt.Groups[1].Value, "arguments", args));
        }
        return outl;
    }

    static PyDict Mediators(ProjectModel m)
    {
        var p = m.P;
        var outd = new PyDict();
        foreach (var k in p.Objects.Keys)
        {
            if (Short(p, k) != "FaceplateInstanceMediatorData")
                continue;
            var target = m.Rel1(k, "IsInstanceOf");
            var env = m.Rel1(k, "Environment");
            var inVersion = env is not null && Short(p, env) == "FaceplateLibraryVersionData";
            var owner = inVersion ? m.Rel1(env, "VersionObjectToTypeObject") : null;
            var scr = m.Rel1(k, "ExpandoParent");
            var item = scr is not null ? m.Rel1(scr, "EmbeddedScriptParent") : null;
            var screen = item is not null && !inVersion ? m.Rel1(item, "ILScreenItemParent") : null;
            object? placeholder = null;
            if (scr is not null)
            {
                var children = p.Links(scr, "ExpandoChildren").Select(l => l.Key).ToList();
                foreach (var (name, v) in p.Sets(scr).Set("IScriptLinkExpandoAttributes"))
                {
                    if (v is byte[] vb && vb.Length > 4 && (vb[4] & 0x80) != 0)
                    {
                        var idx = (long)Bin.FromBe(vb, 0, 4);
                        if (idx < children.Count && children[(int)idx] == k)
                            placeholder = name;
                    }
                }
            }
            var ttype = target is not null ? m.Rel1(target, "VersionObjectToTypeObject") : null;
            var tver = target is not null ? DecodeVersionText(p.Attr(target, "IVersionInfo", "Version")) : null;
            var lst = (List<object?>)outd.SetDefault(target, new List<object?>())!;
            lst.Add(PyDict.Of("type", owner is not null ? p.Name(owner) : null,
                "version", inVersion ? DecodeVersionText(p.Attr(env, "IVersionInfo", "Version")) : null,
                "screen", screen is not null ? p.Name(screen) : null,
                "script", scr is not null ? p.Name(scr) : null, "item", item is not null ? p.Name(item) : null,
                "placeholder", placeholder,
                "opennessName", ttype is not null && Py.Bool(tver) ? $"{Py.Str(p.Name(ttype))}_V_{tver!.Replace(".", "_")}" : null,
                "key", k));
        }
        return outd;
    }

    static (PyDict Typed, List<object?> Untyped) Instances(ProjectModel m, Hmi h, PyDict lib)
    {
        var p = m.P;
        var typed = new PyDict();
        var seen = new HashSet<string>();
        var untyped = new List<object?>();
        var vers = new PyDict();
        foreach (PyDict L in (List<object?>)lib["libraries"]!)
            foreach (PyDict t in (List<object?>)L["types"]!)
                foreach (PyDict v in (List<object?>)t["versions"]!)
                    vers[v.Get("versionGuid")] = t["key"];
        foreach (PyDict i in (List<object?>)lib.Get("instances", new List<object?>())!)
        {
            var k = i.Get("key") as string;
            if (k is null || !p.Objects.ContainsKey(k) || Short(p, k) != ContainerType)
                continue;
            seen.Add(k);
            var scr = m.Rel1(k, "ILScreenItemParent");
            var io = i.Get("instanceOf");
            var iol = Py.Bool(io) ? ((List<object?>)io!).Cast<PyDict>().ToList() : [];
            var ct = iol.Select(x => (object?)$"V{Py.Str(x.Get("version"))}\\{Py.Str(x.Get("typeName"))}").ToList();
            var inst = PyDict.Of("name", i["name"], "screen", scr is not null ? p.Name(scr) : null, "device", i.Get("device"),
                "containedType", ct.Count == 1 ? ct[0] : ct.Count > 0 ? ct : null,
                "instanceOf", i.Get("instanceOf"), "versionState", i.Get("versionState"));
            inst.Update(Geometry(h, m, k));
            inst["key"] = k;
            var tk = Py.Unique(iol.Select(x => vers.Get(x.Get("versionGuid"))).Where(x => x is not null));
            foreach (var t in tk.Count > 0 ? tk : [null])
            {
                if (Py.Bool(t))
                    ((List<object?>)typed.SetDefault(t, new List<object?>())!).Add(inst);
                else
                    untyped.Add(inst);
            }
        }
        foreach (var k in Py.Sorted(p.Objects.Keys))
        {
            if (seen.Contains(k) || Short(p, k) != ContainerType)
                continue;
            var env = m.Rel1(k, "Environment");
            if (env is not null && Short(p, env) == "FaceplateLibraryVersionData")
                continue;
            var scr = m.Rel1(k, "ILScreenItemParent");
            var inst = PyDict.Of("name", p.Name(k), "screen", scr is not null ? p.Name(scr) : null, "device", null,
                "containedType", null, "instanceOf", null, "versionState", null);
            inst.Update(Geometry(h, m, k));
            inst["key"] = k;
            untyped.Add(inst);
        }
        return (typed, untyped);
    }

    /// <summary>Faceplate types (versions, content, hidden device counts, instances) of a project. libraryWithKeys:
    /// Library.Extract(p, true) if the caller has it already.</summary>
    public static List<object?> Extract(ProjectModel m, bool keys = false, PyDict? libraryWithKeys = null)
    {
        var p = m.P;
        if (!p.Objects.Keys.Any(k => Short(p, k) is FaceplateType or ContainerType))
            return [];
        var h = new Hmi(m);
        var lib = libraryWithKeys ?? Library.Extract(p, true);
        var (typed, untyped) = Instances(m, h, lib);
        var med = Mediators(m);
        var outl = new List<object?>();
        foreach (PyDict L in (List<object?>)lib["libraries"]!)
        {
            foreach (PyDict t in (List<object?>)L["types"]!)
            {
                if (!Py.Eq(t["type"], FaceplateType))
                    continue;
                var d = new PyDict();
                foreach (var f in TypeFields)
                    d[f] = t.Get(f);
                d["library"] = L["name"];
                d["alwaysDownload"] = p.Attr((string)t["key"]!, "IHmiByRefTypeObjectBaseAttributes", "AlwaysDownload");
                var versions = ((List<object?>)t["versions"]!).Select(v => Version(m, h, (PyDict)v!)).ToList();
                d["versions"] = versions.Cast<object?>().ToList();
                foreach (var e in versions)
                    e["referencedBy"] = med.Get(e["key"], new List<object?>());
                d["instances"] = typed.Get(t["key"], new List<object?>());
                var codes = new PyDict();
                foreach (var v in versions)
                    if (Py.Bool(v["content"]))
                        foreach (PyDict x in (List<object?>)((PyDict)((PyDict)v["content"]!)["interface"]!)["properties"]!)
                            codes[x["name"]] = x["dataTypeCode"];
                foreach (PyDict i in (List<object?>)d["instances"]!)
                    foreach (PyDict e in (List<object?>)i["interface"]!)
                        if (Py.Eq(codes.Get(e["name"]), 1282L) && Py.IsInt(e.Get("value")))
                            e["value"] = Hmi.Fmt08X(e["value"]);
                d["key"] = t["key"];
                outl.Add(d);
            }
        }
        if (untyped.Count > 0)
            outl.Add(PyDict.Of("name", null, "versions", new List<object?>(), "instances", untyped));
        if (!keys)
            ProjectModel.Strip(outl);
        return outl;
    }

    // --- text output ----------------------------------------------------------------------------------
    static List<PyDict> Dicts(object? l) => ((List<object?>?)l ?? []).Cast<PyDict>().ToList();

    public static string ToText(List<object?> types)
    {
        var o = new List<string>();
        foreach (PyDict t in types)
        {
            if (t["name"] is null)
            {
                foreach (var i in Dicts(t["instances"]))
                    o.Add($"Faceplate instance {Py.Str(i["name"])} on {Py.Str(i["screen"])} (no type)");
                continue;
            }
            o.Add($"Faceplate type {Py.Str(t["name"])}  guid {Py.Str(t["typeGuid"])}{(Py.Bool(t["isObsolete"]) ? "  [do not use]" : "")}{(Py.Bool(t["alwaysDownload"]) ? "  [always download]" : "")}  default {Py.Str(t["defaultVersion"])}");
            foreach (var v in Dicts(t["versions"]))
            {
                o.Add($"  V {Py.Str(v["version"])} {Py.Str(v["state"])}{(Py.Bool(v["isDefault"]) ? " default" : "")}  author {Py.Str(v["author"])}{(Py.Bool(v["finalizationTime"]) ? "  released " + Py.Str(v["finalizationTime"]) : "")}  env {Py.Str(v["environmentObjects"])} objects ({Py.Str(v["standardObjects"])} standard)");
                if (v["content"] is PyDict c && c.Count > 0)
                {
                    var itf0 = (PyDict)c["interface"]!;
                    o.Add($"    content {Py.Str(c["width"])}x{Py.Str(c["height"])}, interface {(Py.Bool(itf0["present"]) ? "present" : "empty")}, {Py.Long(v["scripts"])} scripts" +
                          (Py.Bool(c.Get("backColor")) ? ", background " + Py.Str(c["backColor"]) : "") +
                          (c.Get("suspendable") is not null ? ", suspendable " + Py.Str(c["suspendable"]) : "") +
                          (Py.Bool(c.Get("hiddenLayers")) ? ", hidden in runtime: " + string.Join(", ", ((List<object?>)c["hiddenLayers"]!).Select(Py.Str)) : ""));
                    var itf = itf0;
                    foreach (var x in Dicts(itf["tags"]))
                        o.Add($"      interface tag {Py.Ljust(x["name"], 16)} {Py.Ljust(x["dataType"], 10)} -> tag {Py.Str(x["tag"])} in table {Py.Str(itf["tagTable"])}, cycle {Py.Str(x["acquisitionCycle"])}" +
                              (Py.Bool(x.Get("structureType")) ? $", {Py.Str(x["structureType"])} {Py.Str(x.Get("udt"))} {Py.Str(x.Get("udtVersion"))}" : ""));
                    foreach (var x in Dicts(itf["properties"]))
                        o.Add($"      interface property {Py.Ljust(x["name"], 16)} {Py.Str(x["dataType"])} ({Py.Str(x["dataTypeCode"])}){(x["default"] is not null ? ", default " + Py.Repr(x["default"]) : "")}");
                    foreach (var x in Dicts(itf["events"]))
                        o.Add($"      interface event {Py.Str(x["name"])}({string.Join(", ", Dicts(x["parameters"]).Select(q => $"{Py.Str(q["name"])}: {Py.Str(q["dataType"])}"))})");
                    foreach (var it in Dicts(c["items"]))
                    {
                        var evs = Dicts(it["events"]);
                        o.Add($"      {Py.Ljust(it["name"], 16)} {Py.Ljust(it["type"], 28)} {Py.Str(it["left"])},{Py.Str(it["top"])}" +
                              (it["width"] is not null ? $" {Py.Str(it["width"])}x{Py.Str(it["height"])}" : "") +
                              (Py.Bool(it.Get("containedType")) ? " -> " + Py.Str(it["containedType"]) : "") +
                              (evs.Count > 0 ? "  events " + string.Join(",", evs.Select(e => Py.Str(Py.Or(e.Get("name"), "?")))) : ""));
                        foreach (var dz in Dicts(it["dynamizations"]))
                        {
                            if (!Py.Bool(dz.Get("type")))
                                continue;
                            o.Add($"        dynamization {Py.Str(dz.Get("property"))}: {Py.Str(dz.Get("type"))}{(Py.Bool(dz.Get("tag")) ? " " + Py.Str(dz["tag"]) : "")}");
                            if (Py.Bool(dz.Get("code")))
                                o.AddRange(Py.SplitLines((string)dz["code"]!).Select(line => "          | " + line));
                        }
                        foreach (var e in evs)
                        {
                            o.Add($"        event {Py.Str(e.Get("name"))} ({Py.Str(e.Get("kind"))})");
                            o.AddRange(Py.SplitLines(Py.Or(e.Get("code"), "") as string ?? "").Select(line => "          | " + line));
                        }
                    }
                    var sm = c.Get("scriptModule") as PyDict ?? new PyDict();
                    foreach (var (area, code) in sm.Get("globalDefinitions") as PyDict ?? new PyDict())
                    {
                        if (code is string cs && cs.Length > 0 && !cs.Contains("Insert definitions") && Py.Strip(cs) != "// [FunctionListModule]")
                        {
                            o.Add($"      global definitions ({Py.Str(area)}):");
                            o.AddRange(Py.SplitLines(cs).Select(line => "        | " + line));
                        }
                    }
                }
                if (Py.Bool(v["unresolvedTags"]))
                    o.Add($"    unresolved tags (shells): {string.Join(", ", ((List<object?>)v["unresolvedTags"]!).Select(Py.Str))}");
                foreach (var x in Dicts(v.Get("localTags")))
                    o.Add($"    local tag {Py.Ljust(x["name"], 16)} {Py.Ljust(x["dataType"], 10)} in {Py.Str(x["table"])}, cycle {Py.Str(x["acquisitionCycle"])}{(Py.Bool(x["limits"]) ? ", limits " + Py.Str(x["limits"]) : "")}");
                if (Py.Bool(v.Get("dependsOn")))
                    o.Add($"    depends on {string.Join(", ", ((List<object?>)v["dependsOn"]!).Select(Py.Str))}");
                if (Py.Bool(v.Get("usedBy")))
                    o.Add($"    used by {string.Join(", ", ((List<object?>)v["usedBy"]!).Select(Py.Str))}");
                foreach (var r in Dicts(v.Get("referencedBy")))
                {
                    var where = Py.Bool(r.Get("screen")) ? $"screen {Py.Str(r["screen"])}" : $"{Py.Str(r["type"])} {Py.Str(r["version"])}";
                    o.Add($"    referenced by {where}, script {Py.Str(r["script"])} of {Py.Str(r["item"])} as {Py.Str(r.Get("opennessName"))} (mediator)");
                }
            }
            foreach (var i in Dicts(t["instances"]))
            {
                o.Add($"  instance {Py.Str(i["name"])} on {Py.Str(i["screen"])} (ContainedType {Py.Str(i["containedType"])})");
                foreach (var e in Dicts(i.Get("events")))
                {
                    o.Add($"    event {Py.Str(e.Get("name"))} ({Py.Str(e.Get("kind"))}{(Py.Eq(e.Get("handler"), "functionList") ? ", function list" : "")})");
                    o.AddRange(Py.SplitLines(Py.Or(e.Get("code"), "") as string ?? "").Select(line => "      | " + line));
                }
                foreach (var e in Dicts(i.Get("interface")))
                {
                    var dz = e.Get("dynamization") as PyDict ?? new PyDict();
                    var how = "";
                    if (dz.Count > 0)
                        how = $"  <- {Py.Str(dz.Get("type"))} {Py.Str(Py.OrElse(dz.Get("dynamicTagName"), () => string.Join(" ", new[] { "tag", "resourceList" }.Where(f => Py.Bool(dz.Get(f))).Select(f => Py.Str(dz[f])))))}";
                    o.Add($"    {Py.Ljust(e["name"], 24)} {Py.Ljust(e["kind"], 8)} {PyJson.Dumps(e.Get("value"), null, false)}{how}");
                }
            }
        }
        return string.Join("\n", o);
    }
}
