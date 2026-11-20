// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Program information of a PLC (TIA "Program info": resources, call structure, dependency structure, assignment
/// list), derived from the plf and XRef\XRef.db (port of proginfo.py; rules in docs\spec\program-info.md).</summary>
internal sealed class ProgInfo
{
    static readonly string[] RowTypes = ["OB", "FC", "FB", "DB"];
    static readonly long[] CallFlags = [12, 15];
    const long InstanceFlag = 16;
    static readonly Dictionary<long, string> SizeNames = new() { [1] = "bit", [8] = "B", [16] = "W", [32] = "D", [64] = "L" };

    readonly ProjectModel m;
    readonly Project p;
    readonly PyDict? xr;

    public ProgInfo(ProjectModel model, string path)
    {
        m = model;
        p = model.P;
        xr = Xref.Resolved(path, p);
    }

    (PyDict Res, PyDict Code) Loadable(string key)
    {
        foreach (var t in p.Targets(key, "LoadableBinaries"))
        {
            var a = p.Sets(t);
            return (a.SetOrNull("IGeneralBlockResultData") is { Count: > 0 } r ? r : new PyDict(),
                a.SetOrNull("ILoadablePlusCodeBlockData") is { Count: > 0 } c ? c : new PyDict());
        }
        return (new PyDict(), new PyDict());
    }

    static object? Address(object? blockType, object? number) => number is not null ? Py.Str(blockType) + Py.Str(number) : null;

    /// <summary>[(key, attributes)] of the blocks and UDTs of a PLC that count (system objects only with TargetFamily).</summary>
    public List<(string Key, AttrSets A)> Objects(string plc)
    {
        var outl = new List<(string, AttrSets)>();
        foreach (var k in m.Sources(plc, "Target"))
        {
            var a = p.Sets(k);
            var g = a.SetOrNull("IGeneralBlockSourceData");
            if (!Py.Bool(g) || g!.Get("BlockType") is null)
                continue;
            var system = Py.Eq(a.Set("ICoreAttributes").Get("Protection"), "SystemKnowHowProtection");
            if (system && !Py.Bool(a.Set("IExpandoInformationData").Get("TargetFamily")))
                continue;
            outl.Add((k, a));
        }
        return outl;
    }

    static long N(object? v) => Py.Bool(v) ? Py.Long(v) : 0;

    public PyDict Resources(string plc, List<(string Key, AttrSets A)> objs)
    {
        var rows = RowTypes.ToDictionary(t => t, _ => new Dictionary<string, long>
        {
            ["loadMemory"] = 0, ["codeWorkMemory"] = 0, ["dataWorkMemory"] = 0, ["retainMemory"] = 0, ["blocks"] = 0, ["withoutValue"] = 0,
        });
        var blocks = new List<PyDict>();
        long udtLoad = 0, udtCount = 0, udtWithout = 0;
        foreach (var (k, a) in objs)
        {
            var g = a["IGeneralBlockSourceData"];
            var e = a.Set("IExpandoInformationData");
            var bt = g.Get("BlockType");
            var system = Py.Eq(a.Set("ICoreAttributes").Get("Protection"), "SystemKnowHowProtection");
            if (Py.Eq(bt, "UDT"))
            {
                udtCount++;
                if (e.Get("LoadMemorySize") is null)
                    udtWithout++;
                udtLoad += N(e.Get("LoadMemorySize"));
                continue;
            }
            if (bt is not string bts || !rows.TryGetValue(bts, out var row))
                continue;
            var (res, code) = Loadable(k);
            row["blocks"]++;
            var counted = !Py.Eq(g.Get("ResourceHandling"), "NotCounted");
            var b = PyDict.Of("name", p.Name(k), "type", bt, "number", g.Get("Number"), "system", system,
                "resourceHandling", g.Get("ResourceHandling"), "key", k);
            object? lm;
            if (bts == "DB")
            {
                lm = e.Get("LoadMemorySize");
                b["loadMemory"] = lm;
                b["dataWorkMemory"] = lm is not null ? res.Get("WorkMemoryRequired") : null;
                b["retainMemory"] = e.Get("RetentiveMemoryLength");
                row["dataWorkMemory"] += N(b["dataWorkMemory"]);
                row["retainMemory"] += N(b["retainMemory"]);
            }
            else
            {
                lm = Py.OrElse(res.Get("LoadMemoryRequired"), () => e.Get("LoadMemorySize"));
                var wm = Py.Bool(lm) ? res.Get("WorkMemoryRequired") : null;
                b["loadMemory"] = lm;
                b["codeWorkMemory"] = wm;
                b["codeWorkMemoryCounted"] = counted;
                b["localData"] = code.Get("LocalNenaDataLength");
                if (counted)
                    row["codeWorkMemory"] += N(wm);
            }
            if (!Py.Bool(lm))
                row["withoutValue"]++;
            row["loadMemory"] += N(lm);
            blocks.Add(b);
        }
        var tags = new PyDict();
        foreach (var d in p.Sets(plc).Values)
            foreach (var (n, v) in d)
                if (n is string ns && ns.StartsWith("TagLoadMemory_", StringComparison.Ordinal))
                    tags[ns["TagLoadMemory_".Length..]] = v;
        var outRows = new List<PyDict>();
        foreach (var t in RowTypes)
        {
            var r = rows[t];
            outRows.Add(PyDict.Of("objects", t, "loadMemory", r["loadMemory"], "codeWorkMemory", t != "DB" ? r["codeWorkMemory"] : null,
                "dataWorkMemory", t == "DB" ? r["dataWorkMemory"] : null,
                "retainMemory", t == "DB" ? r["retainMemory"] : null,
                "blocks", r["blocks"], "incomplete", r["withoutValue"] > 0,
                "noValue", r["blocks"] > 0 && r["withoutValue"] == r["blocks"]));
        }
        outRows.Add(PyDict.Of("objects", "DataTypes", "loadMemory", udtLoad, "blocks", udtCount, "incomplete", udtWithout > 0, "noValue", false));
        object? tagSum = null;
        if (tags.Count > 0)
        {
            var any = tags.Values.Any(v => v is double);
            tagSum = any ? tags.Values.Sum(Py.Dbl) : (object)tags.Values.Aggregate(System.Numerics.BigInteger.Zero, (s, v) => s + Py.Big(v)) is var bs ? Py.Int((System.Numerics.BigInteger)bs) : null;
        }
        outRows.Add(PyDict.Of("objects", "PlcTags", "loadMemory", tagSum, "detail", tags,
            "retainMemory", 0L, "incomplete", tags.Count == 0, "noValue", tags.Count == 0));
        var used = PyDict.Of("loadMemory", outRows.Sum(r => N(r["loadMemory"])),
            "codeWorkMemory", outRows.Sum(r => N(r.Get("codeWorkMemory"))),
            "dataWorkMemory", rows["DB"]["dataWorkMemory"], "retainMemory", rows["DB"]["retainMemory"]);
        blocks = Py.Sorted(blocks, x => Py.T((long)Array.IndexOf(RowTypes, (string)x["type"]!), Py.Or(x["number"], 0L), Py.Or(x["name"], "")));
        return PyDict.Of("rows", outRows.Cast<object?>().ToList(), "used", used, "blocks", blocks.Cast<object?>().ToList());
    }

    List<PyDict> Refs(string? plcName) =>
        (xr?.Get("references") as List<object?> ?? []).Cast<PyDict>().Where(r => Py.Eq(r["scope"], plcName)).ToList();

    public List<object?> CallStructure(string plc, List<(string Key, AttrSets A)> objs)
    {
        var info = new OrderedDictionary<string, PyDict>();
        foreach (var (k, a) in objs)
        {
            var g = a["IGeneralBlockSourceData"];
            if (g.Get("BlockType") is not string bt || !RowTypes.Contains(bt))
                continue;
            var (_, code) = Loadable(k);
            info[k] = PyDict.Of("name", p.Name(k), "type", bt, "number", g.Get("Number"), "localData", code.Get("LocalNenaDataLength"));
        }
        var byName = new PyDict();
        foreach (var (k, v) in info)
            byName.SetDefault(v["name"], k);
        var calls = new Dictionary<string, List<string>>();
        var dbs = new Dictionary<string, HashSet<string>>();
        void AddCall(string u, string w)
        {
            if (!calls.TryGetValue(u, out var l))
                calls[u] = l = [];
            if (!l.Contains(w))
                l.Add(w);
        }
        void AddDb(string u, string w)
        {
            if (!dbs.TryGetValue(u, out var s))
                dbs[u] = s = [];
            s.Add(w);
        }
        foreach (var r in Refs(p.Name(plc)))
        {
            var u = r["userKey"] as string;
            var w = r["usedKey"] as string;
            if (u is null || !info.ContainsKey(u) || !Py.Eq(r["usage"], 1L))
                continue;
            var flags = Py.Long(r["flags"]);
            if (r["usedKind"] is "FB" or "FC" && CallFlags.Contains(flags) && w is not null && info.ContainsKey(w))
                AddCall(u, w);
            else if (Py.Eq(r["usedKind"], "Instruction") && CallFlags.Contains(flags) && byName.ContainsKey(r["used"]))
                AddCall(u, (string)byName[r["used"]]!);
            else if (Py.Eq(r["usedKind"], "InstanceDB") && flags == InstanceFlag && w is not null && info.ContainsKey(w))
                AddDb(u, w);
            else if (r["usedKind"] is "DBMember" or "InstanceDB" && w is not null && info.ContainsKey(w))
                AddDb(u, w);
        }
        var reach = new HashSet<string>();
        var pathKeys = new HashSet<string>();

        PyDict Node(string k, long path)
        {
            var n = new PyDict(info[k]);
            n["address"] = Address(info[k]["type"], info[k]["number"]);
            reach.Add(k);
            var local = N(n["localData"]);
            n["localDataInPath"] = path + local;
            var kids = new List<object?>();
            if (!pathKeys.Contains(k))
            {
                pathKeys.Add(k);
                foreach (var w in Py.Sorted(calls.GetValueOrDefault(k) ?? [], x => Py.T(info[x]["type"], Py.Or(info[x]["number"], 0L))))
                    kids.Add(Node(w, path + local));
                pathKeys.Remove(k);
            }
            else
                n["recursive"] = true;
            foreach (var d in Py.Sorted(dbs.GetValueOrDefault(k)?.AsEnumerable() ?? [], x => Py.Or(info[x]["number"], 0L)))
            {
                var dn = new PyDict(info[d]);
                dn["address"] = Address("DB", info[d]["number"]);
                dn["localData"] = null;
                dn["localDataInPath"] = null;
                dn["children"] = new List<object?>();
                kids.Add(dn);
            }
            n["children"] = kids;
            return n;
        }

        string[] ord = ["OB", "DB", "FC", "FB"];
        object Order(string k) => Py.T((long)(Array.IndexOf(ord, info[k]["type"] as string) is var i and >= 0 ? i : 4),
            Py.Or(info[k]["number"], 0L), Py.Or(info[k]["name"], ""));
        var roots = new List<object?>();
        foreach (var k in Py.Sorted(info.Keys.Where(k => Py.Eq(info[k]["type"], "OB")), Order))
            roots.Add(Node(k, 0));
        var fromOb = new HashSet<string>(reach);
        foreach (var k in Py.Sorted(info.Keys, Order))
            if (!Py.Eq(info[k]["type"], "OB") && !fromOb.Contains(k))
                roots.Add(Node(k, 0));
        return roots;
    }

    /// <summary>Python iterates a set of record keys in hash order; the order of the set used here is not defined, so the
    /// output sorts it (usedBy is sorted by name afterwards).</summary>
    public List<object?> DependencyStructure(string plc, List<(string Key, AttrSets A)> objs)
    {
        var names = new OrderedDictionary<string, (object? Name, object? Type, object? Number)>();
        foreach (var (k, a) in objs)
        {
            var g = a["IGeneralBlockSourceData"];
            if (g.Get("BlockType") is string bt && (RowTypes.Contains(bt) || bt == "UDT"))
                names[k] = (p.Name(k), bt, g.Get("Number"));
        }
        var users = new Dictionary<string, List<string>>();
        foreach (var r in Refs(p.Name(plc)))
        {
            var u = r["userKey"] as string;
            var w = r["usedKey"] as string;
            if (u == w || u is null || w is null || !names.ContainsKey(u) || !names.ContainsKey(w))
                continue;
            if (!users.TryGetValue(w, out var l))
                users[w] = l = [];
            if (!l.Contains(u))
                l.Add(u);
        }
        string[] rank = ["UDT", "DB", "FC", "FB", "OB"];
        var outl = new List<object?>();
        foreach (var (k, (n, t, num)) in Py.Sorted(names.Select(kv => (kv.Key, kv.Value)),
                     x => Py.T((long)(Array.IndexOf(rank, x.Value.Type as string) is var i and >= 0 ? i : rank.Length), Py.Or(x.Value.Number, 0L), Py.Or(x.Value.Name, ""))))
        {
            outl.Add(PyDict.Of("name", n, "type", t, "address", !Py.Eq(t, "UDT") ? Address(t, num) : null,
                "usedBy", Py.Sorted(users.GetValueOrDefault(k) ?? [], x => Py.Or(names[x].Name, ""))
                    .Select(u => (object?)PyDict.Of("name", names[u].Name, "type", names[u].Type, "address", Address(names[u].Type, names[u].Number))).ToList()));
        }
        return outl;
    }

    public List<object?> AssignmentList(string plc)
    {
        var refs = Refs(p.Name(plc));
        var used = refs.Where(r => Py.Eq(r["usedKind"], "Tag")).Select(r => r["usedKey"] as string).ToHashSet();
        var outl = new List<PyDict>();
        foreach (PyDict a in xr?.Get("addresses") as List<object?> ?? [])
        {
            if (!Py.Eq(a["scope"], p.Name(plc)) || !used.Contains(a["key"] as string))
                continue;
            var fb = Py.Long(a["firstBit"]);
            var n = Py.Long(a["lastBit"]) - fb + 1;
            outl.Add(PyDict.Of("tag", a["tag"], "area", a["area"], "byte", Py.FloorDiv(fb, 8),
                "bit", n == 1 ? Py.Mod(fb, 8) : null, "size", SizeNames.TryGetValue(n, out var sn) ? sn : n + " bits",
                "firstBit", a["firstBit"], "lastBit", a["lastBit"]));
        }
        return Py.Sorted(outl, x => Py.T(Py.Str(x["area"]), x["firstBit"])).Cast<object?>().ToList();
    }

    public List<object?> Plcs()
    {
        var proj = p.OfType("ProjectData").FirstOrDefault();
        var outl = new List<PyDict>();
        foreach (var d in p.OfType("DeviceData"))
        {
            if (proj is not null && m.Rel1(d, "Environment") != proj)
                continue;
            foreach (var c in Py.Sorted(m.Sources(d, "LogicalContainer")))
            {
                if (!p.IsA(c, "ControllerTargetData"))
                    continue;
                var di = p.Sets(c).Set("IDeviceItemData");
                var objs = Objects(c);
                var e = PyDict.Of("device", p.Name(d), "name", p.Name(c), "orderNumber", di.Get("OrderNumber"),
                    "typeName", di.Get("InvariantTypeName"), "firmware", di.Get("FwVersion"));
                e["resources"] = Resources(c, objs);
                e["callStructure"] = CallStructure(c, objs);
                e["dependencyStructure"] = DependencyStructure(c, objs);
                e["assignmentList"] = AssignmentList(c);
                e["xref"] = xr is not null;
                e["key"] = c;
                outl.Add(e);
            }
        }
        return Py.Sorted(outl, x => Py.T(Py.Or(x["device"], ""), Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    /// <summary>Program information of all PLCs of a project (option --proginfo).</summary>
    public static PyDict Extract(ProjectModel model, string path) => PyDict.Of("plcs", new ProgInfo(model, path).Plcs());
}
