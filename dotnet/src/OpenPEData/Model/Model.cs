// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Structured view of a project: project -> device groups / devices -> PLC software -> tag tables -> tags, blocks
/// (port of model.py; relation names from the metamodel).</summary>
internal sealed class ProjectModel
{
    static readonly Dictionary<long, string> Lcid = new()
    {
        [1031] = "de-DE", [1033] = "en-US", [1036] = "fr-FR", [1040] = "it-IT", [2052] = "zh-CN", [3082] = "es-ES", [0xFFFF] = "",
    };

    /// <summary>Openness ProgrammingLanguage names for internal BlockLanguage values that differ.</summary>
    static readonly Dictionary<string, string> Language = new() { ["LAD_CLASSIC"] = "LAD", ["FBD_CLASSIC"] = "FBD" };

    static readonly Dictionary<string, string> DbType = new() { ["SharedDB"] = "GlobalDB", ["IDBofFB"] = "InstanceDB", ["IDBofFBT"] = "TechnologyDB", ["ArrayDB"] = "ArrayDB", ["DBOfType"] = "TypedDB", ["IDBofSDT"] = "TypedDB", ["IDBofSFB"] = "InstanceDB" };

    public readonly Project P;
    public readonly Dictionary<long, string> Cultures = [];
    /// <summary>reverse index: target key -> [(relation name, source key)]</summary>
    public readonly Dictionary<string, List<(string? Rel, string Src)>> Incoming = [];
    /// <summary>objects whose content could not be read; listed in statistics.errors (a new list per ToDict, so a model can
    /// be asked more than once)</summary>
    public List<object?> Errors = [];
    /// <summary>checked before each guarded object, so a long run can be stopped (set by TiaProject)</summary>
    public CancellationToken Cancellation;
    Dictionary<string, Dictionary<long, string?>>? types;

    public ProjectModel(string path) : this(new Project(path)) { }

    public ProjectModel(Project p)
    {
        P = p;
        foreach (var k in p.OfType("CoreCulture"))
        {
            var n = p.Name(k) ?? "";
            if (n.EndsWith(')') && n.Contains('('))
            {
                var inner = n[..^1];
                var i = inner.IndexOf('(');
                var (name, lcid) = (inner[..i], inner[(i + 1)..]);
                if (Py.IsDigit(lcid))
                    Cultures[Py.ParseInt(lcid)] = name;
            }
        }
        foreach (var (k, o) in p.Objects)
            foreach (var l in o.Links)
            {
                var rn = l.Relation is { } rid && p.Mm.Relations.TryGetValue(rid, out var r) ? r.Name : null;
                if (!Incoming.TryGetValue(l.Key, out var lst))
                    Incoming[l.Key] = lst = [];
                lst.Add((rn, k));
            }
    }

    public static string ErrorText(Exception ex) => (ex is PyError pe ? pe.PyType : ex.GetType().Name) + ": " + ex.Message;

    /// <summary>Runs fn for one object; an exception becomes entry["error"] and an entry of Errors. Cancellation and
    /// OutOfMemoryException are not data errors and pass through.</summary>
    public void Guarded(PyDict entry, string what, Action fn)
    {
        Cancellation.ThrowIfCancellationRequested();
        try
        {
            fn();
        }
        catch (Exception ex) when (PyCatch.Any(ex))
        {
            var msg = ErrorText(ex);
            entry["error"] = msg;
            Errors.Add(PyDict.Of("object", what, "key", entry.Get("key"), "error", msg));
        }
    }

    // --- CoreText ---------------------------------------------------------------------------------
    /// <summary>pe:CoreTextAttributeT: u32 total, u32 used, i32 -1, u32 count, count x u16 LCID, count x u32 offset, at each
    /// offset u32 n + n bytes UTF-8. LCID 0xFFFF = no language. Returns {culture: text}.</summary>
    public static PyDict CoreText(object? blobv, Dictionary<long, string>? cultures = null)
    {
        var res = new PyDict();
        if (blobv is not byte[] blob || blob.Length < 16)
            return res;
        var count = Bin.U32(blob, 12);
        if (16 + 6L * count > blob.Length)
            return res;
        var lcids = Bin.U16s(blob, 16, count);
        var offs = Bin.U32s(blob, 16 + 2 * count, count);
        for (var i = 0; i < count; i++)
        {
            long lcid = lcids[i];
            var off = offs[i];
            var n = Bin.U32(blob, off);
            string? culture = cultures is not null && cultures.TryGetValue(lcid, out var c) && c.Length > 0 ? c : null;
            culture ??= Lcid.TryGetValue(lcid, out var l) && l.Length > 0 ? l : lcid.ToString();
            res[culture] = Py.Utf8(Py.Slice(blob, off + 4L, off + 4L + n));
        }
        return res;
    }

    public PyDict CoreTextOf(object? blob) => CoreText(blob, Cultures);

    // --- helpers ----------------------------------------------------------------------------------
    /// <summary>CoreCulture record name 'de-DE(1031)' -> 'de-DE'.</summary>
    public string Culture(string key)
    {
        var n = P.Name(key) ?? "";
        return n.EndsWith(')') ? Py.BeforeFirst(n, "(") : n;
    }

    /// <summary>Products the project was edited with (ProductVersionInfoContainer "Products" --> ProductVersionInfoData):
    /// display name and version, product id (Name, e.g. Siemens.Simatic.Portal:14.00.00.00), type, hidden; with the names
    /// of the packages each product brings (PackageContainerData). Openness shows them as Project.UsedProducts.</summary>
    public List<object?> UsedProducts()
    {
        var outl = new List<PyDict>();
        foreach (var k in P.OfType("ProductVersionInfoData"))
        {
            var a = P.Sets(k).Set("IProductVersionInfoDataAttributes");
            var pk = P.Links(k).Where(l => P.Objects.ContainsKey(l.Key) && P.IsA(l.Key, "PackageContainerData"))
                .Select(l => (object?)P.Attr(l.Key, "IPackageContainerDataAttributes", "Name")).Distinct().ToList();
            outl.Add(PyDict.Of("name", a.Get("DisplayName"), "version", a.Get("DisplayVersion"), "id", P.Name(k),
                "type", a.Get("ProductType"), "hidden", a.Get("IsHidden"), "packages", Py.Sorted(pk.Cast<string?>().Select(x => x ?? "")).Cast<object?>().ToList(), "key", k));
        }
        return Py.Sorted(outl, x => Py.T(Py.Or(x["name"], ""), Py.Or(x["id"], ""))).Cast<object?>().ToList();
    }

    public PyDict? ProjectLanguages(string? proj)
    {
        if (proj is null)
            return null;
        var ed = Rel1(proj, "EditingCulture");
        var rf = Rel1(proj, "ReferenceCulture");
        return PyDict.Of("active", P.Targets(proj, "UsedCultures").Select(c => (object?)Culture(c)).ToList(),
            "editing", ed is not null ? Culture(ed) : null, "reference", rf is not null ? Culture(rf) : null);
    }

    public string? Rel1(string? key, string relation)
    {
        var t = P.Targets(key, relation);
        return t.Count > 0 ? t[0] : null;
    }

    public List<string> Sources(string? key, string relation) =>
        key is not null && Incoming.TryGetValue(key, out var l) ? l.Where(x => x.Rel == relation).Select(x => x.Src).ToList() : [];

    /// <summary>User folder path of an object below its system root folder (via AggregatingFolder).</summary>
    public string FolderPath(string key)
    {
        var parts = new List<string?>();
        var f = Rel1(key, "AggregatingFolder");
        while (f is not null)
        {
            var sub = P.Attr(f, "ICoreAttributes", "Subtype");
            if (Rel1(f, "AggregatingFolder") is null && !Py.Eq(sub, "DeviceGroupFolder"))
                break;
            parts.Add(P.Name(f));
            f = Rel1(f, "AggregatingFolder");
        }
        parts.Reverse();
        if (parts.Any(x => x is null))
            throw new PyTypeError("sequence item: expected str instance, NoneType found");
        return string.Join("/", parts);
    }

    public PyDict Comment(string? key) => CoreText(P.Attr(key, "ICoreAttributes", "Comment"), Cultures);

    /// <summary>{DataTypeID: name} of the DataTypeItemData of one target family scope (e.g. S71500).</summary>
    public Dictionary<long, string?> ElementaryTypes(object? scope)
    {
        if (types is null)
        {
            types = [];
            foreach (var k in P.OfType("DataTypeItemData"))
            {
                var c = P.Sets(k).Set("IDataTypeContent");
                var id = c.Get("DataTypeID");
                if (id is not null)
                {
                    var sk = ScopeKey(c.Get("Scope"));
                    if (!types.TryGetValue(sk, out var d))
                        types[sk] = d = [];
                    d.TryAdd(Py.Long(id), DisplayTypeName(P.Name(k), c.Get("DataTypeKind")));
                }
            }
        }
        var sk2 = ScopeKey(scope);
        if (!merged.TryGetValue(sk2, out var r))
        {
            // Some types exist only in another S7-1200/1500 scope with the same DataTypeID (Variant 63 only in S71200);
            // S7-300/400 blocks (TargetFamily S7300, S7400) use the scope S7300400 (sample 071)
            merged[sk2] = r = new Dictionary<long, string?>(types.GetValueOrDefault(sk2) ?? []);
            string[] others = Py.Str(scope) is "S7300" or "S7400" ? ["S7300400"] : ["S71200", "S71500"];
            foreach (var other in others)
                foreach (var (id, n) in types.GetValueOrDefault(ScopeKey(other)) ?? [])
                    r.TryAdd(id, n);
        }
        return r;
    }

    readonly Dictionary<string, Dictionary<long, string?>> merged = [];

    /// <summary>System data types (DataTypeKind 33: Conn_Ouc, Hw_Any, OB_Any, ...) are shown in capitals by TIA and
    /// Openness (CONN_OUC, HW_ANY; sample 066); the OPC UA helper types keep their name.</summary>
    static string? DisplayTypeName(string? name, object? kind) =>
        name is not null && Py.Eq(Py.Str(kind), "33") && !name.StartsWith("OPC_UA_", StringComparison.Ordinal) ? name.ToUpperInvariant() : name;

    static string ScopeKey(object? s) => s is null ? "\0None" : Py.Repr(s);

    object? Scope(string key) => Py.Or(P.Attr(key, "IExpandoInformationData", "TargetFamily"), "S71500");

    public List<object?> Members(string key) => new Interface(this, key, Scope(key)) { Standard = IsStandard(key) }.Members();

    public List<object?> InterfaceSections(string key) => new Interface(this, key, Scope(key)) { Standard = IsStandard(key) }.Sections();

    /// <summary>Standard (not optimized) block access: IGeneralBlockSourceData.OnlySymbolicAccess False (sample 059).</summary>
    public bool IsStandard(string key) => Py.Eq(P.Attr(key, "IGeneralBlockSourceData", "OnlySymbolicAccess"), false);

    /// <summary>A not optimized DB is retentive as a whole: IExpandoInformationData.NonRetain False marks all members
    /// (sample 060); optimized blocks keep Remanence per member.</summary>
    static void SetRetain(List<object?> members, bool retain)
    {
        foreach (var m in members.Cast<PyDict>())
        {
            m["retain"] = retain;
            if (m.Get("members") is List<object?> kids)
                SetRetain(kids, retain);
        }
    }

    /// <summary>Instance DB: the interface comes from the FB; only Input, Output, InOut and Static exist. startValue is the
    /// DB's own start value, defaultValue the start value declared in the FB.</summary>
    public List<object?> InstanceSections(string idb, string fb)
    {
        var scope = Scope(fb);
        var own = new Interface(this, idb, scope).Values;
        var fbi = new Interface(this, fb, scope) { KeepPath = true, Standard = IsStandard(fb) };
        var outl = new List<object?>();
        foreach (PyDict sec in fbi.Sections())
            if ((string?)sec["section"] is "Input" or "Output" or "InOut" or "Static")
            {
                var ms = ((List<object?>)sec["members"]!).Select(m => (object?)IdbMember((PyDict)m!, own)).ToList();
                if (!Py.Eq(sec["section"], "Static"))
                    Interface.ClearSetpoint(ms); // Openness: setpoints only in the Static section (UDT parameter, sample 060)
                outl.Add(PyDict.Of("section", sec["section"], "members", ms));
            }
        return outl;
    }

    /// <summary>Technology object DB (Type IDBofFBT): the own interface root holds the changed start values, its UsedParts
    /// point to the TO type interface.</summary>
    public List<object?> ToSections(string db)
    {
        var scope = Scope(db);
        var ownIf = new Interface(this, db, scope);
        var troot = ownIf.Parts.FirstOrDefault(k => k is not null && P.IsA(k, "InterfaceVersionRootData"));
        if (troot is null)
            return [];
        var ti = new Interface(this, db, scope, troot) { KeepPath = true };
        var outl = new List<object?>();
        foreach (PyDict sec in ti.Sections())
            if ((string?)sec["section"] is "Input" or "Output" or "InOut" or "Static")
                outl.Add(PyDict.Of("section", sec["section"], "members", ((List<object?>)sec["members"]!).Select(m => (object?)IdbMember((PyDict)m!, ownIf.Values)).ToList()));
        return outl;
    }

    /// <summary>DB of a PLC data type (Type DBOfType, sample 059): like an instance DB, the own interface root holds only
    /// the changed start values and GeneratedFromMaster points to the UDT, whose elements are the members. Same for a DB
    /// of a system data type (Type IDBofSDT, e.g. IEC_TIMER in Program resources, sample 065): the system type has no
    /// CurrentInterface, its interface root is the UsedParts entry of the DB's own root.</summary>
    public List<object?> TypedMembers(string db, string udt)
    {
        var scope = Scope(db);
        var ownIf = new Interface(this, db, scope);
        var own = ownIf.Values;
        var root = Rel1(udt, "CurrentInterface") ?? ownIf.Parts.FirstOrDefault(k => k is not null && P.IsA(k, "InterfaceVersionRootData"));
        if (root is null)
            return [];
        var ti = new Interface(this, udt, scope, root) { KeepPath = true, Standard = IsStandard(db) };
        return ti.Members().Select(m => (object?)IdbMember((PyDict)m!, own)).ToList();
    }

    /// <summary>ARRAY DB (Type ArrayDB, sample 059): the root holds the hidden system member Header (ArrayDBHeader) and
    /// the array THIS; Openness shows only the array, named like the DB.</summary>
    static List<object?> ArrayDbMembers(List<object?> members, object? name) =>
        members.Cast<PyDict>().Where(m => !Py.Eq(m["dataType"], "ArrayDBHeader"))
            .Select(m => { if (Py.Eq(m["name"], "THIS")) m["name"] = name; return (object?)m; }).ToList();

    static object? And(object? a, object? b) => Py.Bool(a) ? b : a;

    // --- content ----------------------------------------------------------------------------------
    public PyDict Tag(string k)
    {
        var si = P.Sets(k).Set("IStructureItem");
        var ta = P.Sets(k).Set("ITagAddress");
        var acc = si.Get("HmiStructureTypeRelevant");
        var vis = acc is null || si.Get("HmiVisible") is null ? null : And(acc, si["HmiVisible"]);
        return PyDict.Of(
            "name", P.Name(k),
            "dataType", si.Get("DisplayTypeName"),
            "address", ta.Get("LogicalAddress"),
            "comment", Comment(k),
            "hmiAccessible", acc,
            "hmiVisible", vis,
            "hmiWritable", vis is null || si.Get("HmiReadOnly") is null ? null : And(vis, !Py.Bool(si["HmiReadOnly"])),
            "key", k);
    }

    public List<object?> TagTables(string plc)
    {
        var outl = new List<PyDict>();
        foreach (var t in Py.Sorted(Sources(plc, "Target")))
        {
            if (!P.IsA(t, "TagTableData"))
                continue;
            var tags = Sources(t, "TagTable").Where(x => P.IsA(x, "EAMTZTagData")).Select(Tag).ToList();
            tags = Py.Sorted(tags, x => Py.Or(x["name"], ""));
            outl.Add(PyDict.Of("name", P.Name(t), "group", FolderPath(t), "tags", tags.Cast<object?>().ToList(), "key", t));
        }
        return Py.Sorted(outl, x => Py.T(x["group"], Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    public List<object?> Blocks(string plc)
    {
        var outl = new List<PyDict>();
        foreach (var b in Sources(plc, "Target"))
        {
            var gb = P.Sets(b).SetOrNull("IGeneralBlockSourceData");
            if (!Py.Bool(gb) || gb!.Get("BlockType") is null)
                continue;
            if (Py.Eq(gb.Get("BlockType"), "UDT"))
                continue;
            var lang = gb.Get("BlockLanguage");
            var blk = PyDict.Of("name", P.Name(b), "type", gb.Get("BlockType"), "number", gb.Get("Number"),
                "language", lang is string ls && Language.TryGetValue(ls, out var ln) ? ln : lang, "languageInternal", lang,
                "group", FolderPath(b), "key", b);
            // know-how protection and software unit (sample 070): the interface stays readable in the file
            if (Py.Bool(P.Attr(b, "ICoreAttributes", "IsKnowHowProtected")))
                blk["knowHowProtected"] = true;
            if (Rel1(b, "SWObjectData_IUnitElementAttributes_BaseUnitData") is { } unit && P.IsA(unit, "UnitData"))
                blk["unit"] = P.Name(unit);
            if (Py.Eq(gb.Get("BlockType"), "DB"))
            {
                var dt = P.Attr(b, "IGeneralDataBlockSourceData", "Type");
                blk["type"] = dt is string ds && DbType.TryGetValue(ds, out var dbt) ? dbt : "DB:" + Py.Str(dt);
            }
            var isDb = Py.Eq(gb.Get("BlockType"), "DB");
            Guarded(blk, "block " + Py.Str(blk["name"]), () => BlockContent(b, blk, isDb));
            outl.Add(blk);
        }
        return Py.Sorted(outl, x => Py.T(Py.Or(x["type"], ""), Py.Or(x["number"], 0L), Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    void BlockContent(string b, PyDict blk, bool isDb)
    {
        if (isDb)
        {
            var fb = Rel1(b, "IsGeneratedFrom");
            blk["members"] = Members(b);
            if (Py.Eq(blk["type"], "ArrayDB"))
                blk["members"] = ArrayDbMembers((List<object?>)blk["members"]!, blk["name"]);
            if (Py.Eq(blk["type"], "TypedDB") && Rel1(b, "GeneratedFromMaster") is { } udt)
            {
                blk["instanceOf"] = P.Name(udt);
                blk["members"] = TypedMembers(b, udt);
            }
            if (Py.Eq(blk["type"], "TechnologyDB"))
            {
                blk["instanceOf"] = P.Attr(b, "IGeneralDataBlockSourceData", "OfName");
                blk["interface"] = ToSections(b);
                if (new Code(this).ToHardwareConnections(b) is { Count: > 0 } hc)
                    blk["hardwareConnections"] = hc;
            }
            if (fb is not null)
            {
                blk["instanceOf"] = P.Name(fb);
                var itf = InstanceSections(b, fb);
                blk["interface"] = itf;
                blk["members"] = itf.Cast<PyDict>().FirstOrDefault(x => Py.Eq(x["section"], "Static"))?["members"] ?? new List<object?>();
            }
            blk["optimized"] = !IsStandard(b);
            if (IsStandard(b))
            {
                var retain = Py.Eq(P.Attr(b, "IExpandoInformationData", "NonRetain"), false);
                SetRetain((List<object?>)blk["members"]!, retain);
                if (blk.Get("interface") is List<object?> secs)
                    foreach (var sec in secs.Cast<PyDict>())
                        SetRetain((List<object?>)sec["members"]!, retain);
            }
        }
        else
        {
            blk["interface"] = InterfaceSections(b);
            blk["networks"] = new Code(this).Networks(b);
        }
    }

    public List<object?> Types(string plc)
    {
        var outl = new List<PyDict>();
        foreach (var t in Sources(plc, "Target"))
        {
            if (!Py.Eq(P.Attr(t, "IGeneralBlockSourceData", "BlockType"), "UDT"))
                continue;
            var udt = PyDict.Of("name", P.Name(t), "group", FolderPath(t), "members", new List<object?>(), "key", t);
            Guarded(udt, "PLC data type " + Py.Str(udt["name"]), () => udt["members"] = Members(t));
            outl.Add(udt);
        }
        return Py.Sorted(outl, x => Py.T(x["group"], Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    public List<object?> Plcs(string device)
    {
        var outl = new List<object?>();
        foreach (var c in Py.Sorted(Sources(device, "LogicalContainer")))
        {
            if (!P.IsA(c, "ControllerTargetData"))
                continue;
            var di = P.Sets(c).Set("IDeviceItemData");
            var d = PyDict.Of("name", P.Name(c), "orderNumber", di.Get("OrderNumber"), "firmware", di.Get("FwVersion"),
                "typeName", di.Get("InvariantTypeName"));
            d["tagTables"] = TagTables(c);
            d["blocks"] = Blocks(c);
            d["types"] = Types(c);
            d["key"] = c;
            outl.Add(d);
        }
        return outl;
    }

    public PyDict ToDict(bool keys = false)
    {
        Errors = [];
        var p = P;
        var proj = p.OfType("ProjectData").FirstOrDefault();
        // L6: a user global library has no ProjectData; its root object is the LibraryData with LibraryType GlobalLib
        var glib = proj is not null ? null : p.OfType("LibraryData").FirstOrDefault(k => Py.Eq(p.Attr(k, "ICoreLibAttributes", "LibraryType"), "GlobalLib"));
        var root = proj ?? glib;
        var groups = new List<PyDict>();
        foreach (var f in p.OfType("FolderData"))
        {
            if (Py.Eq(p.Attr(f, "ICoreAttributes", "Subtype"), "DeviceGroupFolder"))
            {
                var parent = FolderPath(f);
                groups.Add(PyDict.Of("name", p.Name(f), "path", (parent.Length > 0 ? parent + "/" : "") + Need(p.Name(f)), "key", f));
            }
        }
        groups = Py.Sorted(groups, g => g["path"]);
        var hmi = new Hmi(this);
        var hw = new Hardware(this);
        var devices = new List<PyDict>();
        foreach (var d in p.OfType("DeviceData"))
        {
            if (root is not null && Rel1(d, "Environment") != root)
                continue;
            var dd = p.Sets(d).Set("IDeviceData");
            var dev = PyDict.Of("name", p.Name(d), "typeName", dd.Get("InvariantTypeName"), "group", FolderPath(d), "comment", Comment(d));
            dev["hardware"] = hw.Device(d);
            dev["plcs"] = Plcs(d);
            dev["hmi"] = hmi.Softwares(d);
            // the hidden HMI module device "PlantModel" (Subtype HMI.ModuleDeviceILCpm) that TIA creates with the first plant
            // object type (sample 170); Openness does not list it as a device
            if (p.Attr(d, "ICoreAttributes", "Subtype") is string st && st.StartsWith("HMI.ModuleDevice", StringComparison.Ordinal))
                dev["hiddenModuleDevice"] = true;
            dev["key"] = d;
            devices.Add(dev);
        }
        devices = Py.Sorted(devices, x => Py.T(x["group"], Py.Or(x["name"], "")));
        var pr = PyDict.Of("name", root is not null ? p.Name(root) : null, "kind", glib is not null ? "GlobalLibrary" : "Project",
            "comment", proj is not null ? Comment(proj) : new PyDict(), "deviceGroups", groups.Cast<object?>().ToList(),
            "devices", devices.Cast<object?>().ToList());
        pr["languages"] = ProjectLanguages(proj);
        pr["usedProducts"] = UsedProducts();
        pr["plantViews"] = hmi.PlantViews();
        if (hmi.PlantObjectTypes() is { Count: > 0 } pots)
            pr["plantObjectTypes"] = pots;
        pr["graphics"] = hmi.Graphics();
        pr["network"] = hw.Network(devices.Select(x => (string)x["key"]!).ToHashSet());
        pr["umac"] = hw.Umac();
        pr["plcAlarms"] = PlcAlarms.Extract(this);
        pr["plcWatchTables"] = PlcTables.ExtractWatchTables(this);
        pr["plcUnits"] = PlcTables.ExtractUnits(this);
        pr["plcOpcUaInterfaces"] = PlcTables.ExtractOpcUaInterfaces(this);
        pr["plcSupervisions"] = PlcTables.ExtractSupervisions(this);
        pr["plcTextLists"] = PlcTables.ExtractTextLists(this);
        var res = PyDict.Of("project", pr);
        // with --keys the faceplate types read the same library view (only within this run, it is not kept)
        var lib = Library.Extract(p, keys, k => hmi.ScriptModule(k));
        res["library"] = lib;
        res["faceplates"] = Faceplates.Extract(this, keys, keys ? lib : null);
        res["statistics"] = PyDict.Of("plfFiles", p.PlfFiles.Cast<object?>().ToList(), "records", (long)p.Records.Count,
            "objects", (long)p.Objects.Count, "deletedObjects", (long)p.Deleted.Count, "hashErrors", (long)p.Records.Count(r => !r.HashOk));
        if (Errors.Count > 0)
            ((PyDict)res["statistics"]!)["errors"] = Errors;
        if (!keys)
            Strip(res);
        return res;
    }

    static string Need(string? s) => s ?? throw new PyTypeError("can only concatenate str (not \"NoneType\") to str");

    /// <summary>Indexed start values per own value map (built once per data block, not once per member).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PyDict, Dictionary<string, PyDict>> ElemIndex = new();

    static PyDict IdbMember(PyDict m, PyDict own)
    {
        var path = (string)m["_path"]!;
        var e = PyDict.Of("name", m["name"], "dataType", m["dataType"], "startValue", own.Get(path), "comment", new PyDict());
        foreach (var k in new[] { "retain", "externalAccessible", "externalVisible", "externalWritable", "setpoint", "accessibility", "offset" })
            if (m.ContainsKey(k))
                e[k] = m[k];
        if (m.Get("startValue") is not null)
            e["defaultValue"] = m["startValue"];
        var index = ElemIndex.GetValue(own, o => Interface.IndexElements(o, out _));
        var elems = index.TryGetValue(path, out var ev) ? new PyDict(ev) : new PyDict();
        if (elems.Count > 0)
            e["elementStartValues"] = elems;
        if (Py.Bool(m.Get("elementStartValues")))
            e["defaultElementStartValues"] = m["elementStartValues"];
        if (Py.Bool(m.Get("members")))
            e["members"] = ((List<object?>)m["members"]!).Select(x => (object?)IdbMember((PyDict)x!, own)).ToList();
        return e;
    }

    /// <summary>Removes "key" from every dict (recursively), like model._strip.</summary>
    public static void Strip(object? o)
    {
        if (o is PyDict d)
        {
            d.Remove("key");
            foreach (var v in d.Values)
                Strip(v);
        }
        else if (o is System.Collections.IList l and not byte[])
        {
            foreach (var v in l)
                Strip(v);
        }
    }
}
