using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>XRef\XRef.db: cross-reference database (SQLite 3, derived from the plf, rebuildable). Tables: info(id, version,
/// savepointID), parts(id, binary = b"OG" + record key), objs(id, scopeObject, primaryObject, subObject), rels(leftType,
/// left, bindingObject, leftFilter, location, rightType, right, rightFilter, relationInfo), addrs(obj, filter, type, range,
/// firstBit, lastBit).</summary>
internal static class Xref
{
    /// <summary>plf record key of a part blob, null for the empty part.</summary>
    public static string? PartKey(byte[] b)
    {
        if (b is [0])
            return null;
        if (b.Length < 2 || b[0] != (byte)'O' || b[1] != (byte)'G' || b.Length is not (14 or 15))
            throw new PyValueError("unknown XRef part " + Py.HexStr(b));
        return Plf.MakeKey(Bin.U16(b, 2), Bin.U16(b, 4), Bin.U64(b, 6));
    }

    /// <summary>XRef content of a project: {"version", "savepointId", "parts", "objs", "rels", "addrs"}; null without XRef.db.</summary>
    public static PyDict? Read(string path)
    {
        var data = ProjectFiles.Member(path, "XRef/XRef.db");
        if (data is null)
            return null;
        var db = new SqliteReader(data);
        var info = db.Select("info", "version", "savepointID").FirstOrDefault();
        var parts = new PyDict();
        foreach (var row in db.Select("parts", "id", "binary"))
            parts[row[0]] = PartKey(Py.Bytes(row[1]) ?? Py.Utf8Bytes(Py.Str(row[1])));
        var objs = new PyDict();
        foreach (var row in db.Select("objs", "id", "scopeObject", "primaryObject", "subObject"))
        {
            var sub = Py.Bytes(row[3]);
            objs[row[0]] = PyDict.Of("scope", parts[row[1]], "primary", parts[row[2]],
                "member", sub is { Length: 4 } ? (long)Bin.U32(sub, 0) : null);
        }
        var rels = new List<object?>();
        foreach (var r in db.Rows("rels"))
            rels.Add(PyDict.Of("leftType", r[0], "left", r[1], "rightType", r[5], "right", r[6],
                "location", Py.HexStr(Py.Bytes(r[4]) ?? throw new PyAttributeError("'NoneType' object has no attribute 'hex'")), "info", r[8]));
        var addrs = new List<object?>();
        foreach (var a in db.Rows("addrs"))
            addrs.Add(PyDict.Of("obj", a[0], "type", a[2], "range", a[3], "firstBit", a[4], "lastBit", a[5]));
        return PyDict.Of("version", info?[0], "savepointId", info?[1], "parts", parts, "objs", objs, "rels", rels, "addrs", addrs);
    }

    static readonly Dictionary<long, string> TypeNames = new()
    {
        [0x1000] = "OB", [0x1010] = "FC", [0x1020] = "FB", [0x1060] = "InstanceDB", [0x1065] = "Instruction",
        [0x1070] = "TechnologyObject", [0x1080] = "Tag", [0x10a0] = "Constant", [0x10b0] = "UDT", [0x1100] = "Label",
        [0x1110] = "DBMember", [0x1120] = "InterfaceMember", [0x1125] = "UDTMember",
    };

    static readonly Dictionary<long, string> RangeNames = new() { [1] = "Input", [2] = "Output", [3] = "Memory" };

    static object? TypeName(object? code) => code is long l && TypeNames.TryGetValue(l, out var n) ? n : null;

    /// <summary>XRef content with object names from the project (option --xref); null without XRef.db.</summary>
    public static PyDict? Resolved(string path, Project proj)
    {
        var d = Read(path);
        if (d is null)
            return null;
        var objs = (PyDict)d["objs"]!;

        PyDict Obj(object? i)
        {
            var o = objs.Get(i) as PyDict ?? new PyDict();
            var k = o.Get("primary") as string;
            var exists = k is not null && proj.Objects.ContainsKey(k);
            object? name = exists ? proj.Name(k) : null;
            if (exists && proj.TypeName(k) == "IECPLInstructionProxyData")
                name = Py.Or(Py.OrElse(proj.Attr(k, "ICoreAttributes", "Author"), () => proj.Attr(k, "IGenericInterfaceData", "InstanceDefaultName")), name);
            var scope = o.Get("scope") as string;
            return PyDict.Of("name", name, "objectType", exists ? proj.TypeName(k) : null,
                "scope", scope is not null && proj.Objects.ContainsKey(scope) ? proj.Name(scope) : null,
                "member", o.Get("member"), "key", k, "deleted", k is not null && !exists);
        }

        bool Live(object? i) => (objs.Get(i) as PyDict ?? new PyDict()).Get("primary") is string p && proj.Objects.ContainsKey(p);

        var groups = new PyDict();
        long stale = 0;
        foreach (var r in ((List<object?>)d["rels"]!).Cast<PyDict>())
        {
            if (!(Live(r["left"]) && Live(r["right"])))
            {
                stale++;
                continue;
            }
            var g = Py.T(r["left"], r["right"], r["info"], r["leftType"], r["rightType"]);
            groups[g] = Py.Long(groups.Get(g, 0L)) + 1;
        }
        var refs = new List<PyDict>();
        foreach (var (gk, nv) in groups)
        {
            var g = (PyTuple)gk!;
            var (left, right, info, lt, rt) = (g[0], g[1], g[2], g[3], g[4]);
            var a = Obj(left);
            var b = Obj(right);
            var inf = Py.Long(info);
            refs.Add(PyDict.Of(
                "user", a["name"], "userKind", TypeName(lt) ?? Py.OrElse(a["objectType"], () => Py.HexPrefixed(lt)), "userMember", a["member"],
                "used", b["name"], "usedKind", TypeName(rt) ?? Py.OrElse(b["objectType"], () => Py.HexPrefixed(rt)), "usedMember", b["member"],
                "usage", inf & 0xFFFF, "flags", inf >> 16, "count", nv, "scope", Py.Or(a["scope"], b["scope"]),
                "userKey", a["key"], "usedKey", b["key"]));
        }
        refs = Py.Sorted(refs, x => Py.T(Py.Or(x["scope"], ""), Py.Or(x["used"], ""), Py.Or(x["user"], "")));
        var addrs = new List<object?>();
        foreach (var a in ((List<object?>)d["addrs"]!).Cast<PyDict>())
        {
            var o = Obj(a["obj"]);
            addrs.Add(PyDict.Of("tag", o["name"], "scope", o["scope"],
                "area", a["range"] is long rg && RangeNames.TryGetValue(rg, out var an) ? an : a["range"],
                "firstBit", a["firstBit"], "lastBit", a["lastBit"], "key", o["key"]));
        }
        return PyDict.Of("version", d["version"], "savepointId", d["savepointId"], "parts", (long)((PyDict)d["parts"]!).Count,
            "objects", (long)objs.Count, "relations", (long)((List<object?>)d["rels"]!).Count, "staleRelations", stale,
            "references", refs.Cast<object?>().ToList(), "addresses", addrs);
    }
}
