// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Project library view (port of library.py): folders, master copies, types / versions, typed instances.</summary>
internal sealed class Library
{
    static readonly string[] TypeBases = ["TypeObjectBase", "CoreLibraryType"];
    static readonly string[] VersionBases = ["VersionObjectBase"];
    static readonly string[] InstanceBases = ["InstanceObject"];

    static readonly Dictionary<string, string> OpennessTypeKind = new()
    {
        ["UserTypeDataByCopyTypeObject"] = "PlcTypeLibraryType", ["CodeBlockDataByCopyTypeObject"] = "CodeBlockLibraryType",
        ["FaceplateLibraryTypeData"] = "LibraryType", ["TextListLibraryTypeData"] = "LibraryType",
        ["ImageLibraryTypeData"] = "LibraryType", ["DynamicSVGLibraryTypeData"] = "LibraryType",
        ["GraphicListLibraryTypeData"] = "LibraryType", ["HmiColorPaletteLibraryTypeData"] = "LibraryType",
        ["ScriptModuleLibraryTypeData"] = "LibraryType",
        ["HmiScreenModuleTypeObjectData"] = "FaceplateLibraryType", ["HmiScreenTypeData"] = "ScreenLibraryType",
        ["HmiStyleTypeData"] = "StyleLibraryType", ["HmiStyleSheetTypeData"] = "StyleSheetLibraryType",
        ["HmiUdtTypeData"] = "HmiUdtLibraryType", ["HmiCScriptTypeData"] = "CScriptLibraryType",
        ["HmiVBScriptTypeData"] = "VBScriptLibraryType",
    };

    static readonly string[] SystemFolderOrder = ["LibraryTypeSystemFolder", "LibraryMasterCopySystemFolder", "LibrarySystemTypeSystemFolder",
        "LibrarySystemMasterCopySystemFolder", "CommonDataFolder"];

    static readonly string[] VersionElements = ["Build", "Major", "Minor", "Revision"];

    public static uint KeyTypeId(string key)
    {
        var s = key.Split(':');
        return (uint)((Py.ParseHex(s[1]) << 16) | Py.ParseHex(s[0]));
    }

    static readonly Regex XmlDecl = new(@"^\s*<\?xml[^>]*\?>", RegexOptions.CultureInvariant);

    /// <summary>Clipboard header of ICoreLibMasterCopyAttributes.SharedRawData -> dict (null if empty / unparsable).</summary>
    public static PyDict? ParseSharedRawData(object? textv)
    {
        if (!Py.Bool(textv))
            return null;
        string text;
        if (textv is byte[] tb)
        {
            try
            {
                text = Py.Utf8Exact(tb);
            }
            catch (PyUnicodeDecodeError)
            {
                try
                {
                    text = Py.Utf16Exact(tb);
                }
                catch (PyUnicodeDecodeError)
                {
                    return PyDict.Of("error", "undecodable", "raw", Py.HexStr(tb));
                }
            }
        }
        else
            text = (string)textv!;
        var body = XmlDecl.Replace(text, "");
        XElement root;
        try
        {
            root = Et.FromString(body);
        }
        catch (XmlParseError e)
        {
            return PyDict.Of("error", "xml: " + e.Message, "raw", text);
        }
        var res = PyDict.Of("formatVersion", null, "projectId", null, "sourceScopePath", new List<object?>(), "typeDescriptors", new List<object?>(),
            "selectedIds", null, "resolvers", new List<object?>());
        var hdr = Et.Find(root, "ClipboardHeader");
        if (hdr is not null)
        {
            res["formatVersion"] = Et.Get(hdr, "formatVersion");
            res["projectId"] = Et.Get(hdr, "projectId");
            foreach (var sc in Et.Iter(hdr, "ObjectScope"))
                ((List<object?>)res["sourceScopePath"]!).Add(PyDict.Of("type", Et.Get(sc, "scopeTypeName"), "id", Et.Get(sc, "scopeId")));
            foreach (var td in Et.Iter(hdr, "TypeDescriptor"))
            {
                var d = Et.Attrib(td);
                d["roles"] = (Py.Or(d.Get("roles"), "") as string ?? "").Split(',').Select(Py.Strip).Where(r => r.Length > 0).Cast<object?>().ToList();
                ((List<object?>)res["typeDescriptors"]!).Add(d);
            }
        }
        var data = Et.Find(root, "ClipboardData");
        if (data is not null)
        {
            var sel = Et.Find(data, "SelectedIds");
            res["selectedIds"] = sel is not null ? Et.Text(sel) : null;
            res["resolvers"] = Et.Iter(data, "Resolver").Select(r => (object?)Et.Get(r, "sid")).ToList();
        }
        return res;
    }

    /// <summary>IVersionInfo.Version (u32 20 + 4 x i32 Build, Major, Minor, Revision) -> (text, raw hex, parts).</summary>
    public static (string? Text, object? Raw, PyDict? Parts) DecodeVersionData(object? raw)
    {
        if (raw is null)
            return (null, null, null);
        PyDict v;
        if (raw is PyDict rd)
            v = rd;
        else if (raw is byte[] rb && rb.Length is 16 or 19 or 20)
        {
            var last = rb[^16..];
            v = new PyDict();
            for (var i = 0; i < 4; i++)
                v[VersionElements[i]] = (long)Bin.I32(last, 4 * i);
        }
        else
            return (null, raw is byte[] r2 ? Py.HexStr(r2) : Py.Str(raw), null);
        var txt = $"{Py.Str(v.Get("Major"))}.{Py.Str(v.Get("Minor"))}.{Py.Str(v.Get("Build"))}";
        if (Py.IsInt(v.Get("Revision")) && Py.Big(v["Revision"]) >= 0)
            txt += "." + Py.Big(v["Revision"]);
        return (txt, raw is byte[] r3 ? Py.HexStr(r3) : null, v);
    }

    public static PyDict? DecodeCoreObjectData(Metamodel mm, object? raw)
    {
        if (raw is null)
            return null;
        var d = Attrs.DecodeStructure(mm, "ArrayLibraryClipboardCoreObject", raw);
        if (d is not List<object?> l || !l.All(e => e is PyDict))
            return PyDict.Of("error", "not decodable", "hex", Py.HexStr(Py.Bytes(raw) ?? throw new PyTypeError("cannot convert to bytes")));
        return PyDict.Of("count", (long)l.Count, "entries", l.Cast<PyDict>().Select(e => (object?)PyDict.Of("objectRole", e.Get("ObjectRole"),
            "option", e.Get("Option"), "parentObjectId", e.Get("ParentObjectId"), "sourceObjectId", e.Get("SourceObjectId"),
            "visualPartIndex", e.Get("VisualPartIndex"))).ToList());
    }

    public static object? DecodeStructArrayCount(Metamodel mm, string typ, object? raw)
    {
        var d = raw is not null ? Attrs.DecodeStructure(mm, typ, raw) : null;
        return d is List<object?> l ? (long)l.Count : null;
    }

    static readonly Regex ScopeId = new(@"\A\{(\d+)-(\d+)\}\z", RegexOptions.CultureInvariant);

    /// <summary>ObjectScope scopeId '{type id-object id}' -> record key 'lo:hi:oid', or null.</summary>
    public static string? ParseScopeId(object? text)
    {
        var mt = ScopeId.Match(text as string ?? "");
        if (!mt.Success)
            return null;
        var tid = ulong.Parse(mt.Groups[1].Value);
        var oid = ulong.Parse(mt.Groups[2].Value);
        return $"{tid & 0xFFFF:x4}:{tid >> 16:x4}:{oid:x}";
    }

    static readonly Regex Ascii4 = new("[\\x20-\\x7e]{4,}", RegexOptions.CultureInvariant);

    public static PyDict? ParseDependencyIdentifiers(object? rawv, IEnumerable<object?> knownGuids)
    {
        if (rawv is not byte[] raw || raw.Length < 32)
            return null;
        var body = raw[24..];
        var count = (long)Bin.FromLe(body, 4, 8);
        var guids = knownGuids.Where(g => Py.Bool(g) && Py.Contains(body, System.Guid.Parse((string)g!).ToByteArray())).ToList();
        var strings = Ascii4.Matches(Py.Latin1(body)).Select(x => (object?)x.Value).ToList();
        return PyDict.Of("count", count, "versionGuids", guids, "strings", strings);
    }

    /// <summary>Sort key of a version text 'a.b.c[.d]' (None last).</summary>
    public static PyTuple VersionKey(object? text)
    {
        if (text is not string s)
            return Py.T(1L);
        try
        {
            return new PyTuple([0L, .. s.Split('.').Select(x => (object?)Py.ParseInt(x))]);
        }
        catch (PyValueError)
        {
            return Py.T(1L);
        }
    }

    readonly Project p;
    readonly Metamodel mm;
    readonly Dictionary<string, List<(string? Rel, string Src)>> incoming = [];
    readonly Dictionary<long, string> cultures = [];
    List<object?>? versionGuids;
    Func<string, PyDict>? scriptModuleReader;

    public Library(Project project)
    {
        p = project;
        mm = p.Mm;
        foreach (var (k, o) in p.Objects)
            foreach (var l in o.Links)
            {
                var rn = l.Relation is { } rid && mm.Relations.TryGetValue(rid, out var r) ? r.Name : null;
                if (!incoming.TryGetValue(l.Key, out var lst))
                    incoming[l.Key] = lst = [];
                lst.Add((rn, k));
            }
        foreach (var k in p.OfType("CoreCulture"))
        {
            var n = p.Name(k) ?? "";
            if (n.EndsWith(')') && n.Contains('('))
            {
                var inner = n[..^1];
                var i = inner.IndexOf('(');
                if (Py.IsDigit(inner[(i + 1)..]))
                    cultures[Py.ParseInt(inner[(i + 1)..])] = inner[..i];
            }
        }
    }

    // --- helpers ----------------------------------------------------------------------------------
    List<string> Sources(string? key, string relation) =>
        key is not null && incoming.TryGetValue(key, out var l) ? l.Where(x => x.Rel == relation && p.Objects.ContainsKey(x.Src)).Select(x => x.Src).ToList() : [];

    string? Rel1(string? key, string relation)
    {
        var t = p.Targets(key, relation);
        return t.Count > 0 ? t[0] : null;
    }

    bool IsAny(string? key, string[] bases) => bases.Any(b => p.IsA(key, b));

    public PyDict ObjRef(string key, bool keys)
    {
        var tid = p.Objects[key].Rec.TypeId;
        var d = PyDict.Of("name", p.Name(key), "type", p.TypeName(key), "typeFullName", mm.TypeName(tid), "package", mm.TypePackage(tid));
        var sub = p.Attr(key, "ICoreAttributes", "Subtype");
        if (Py.Bool(sub))
            d["subtype"] = sub;
        var bt = p.Attr(key, "IGeneralBlockSourceData", "BlockType");
        if (Py.Bool(bt))
            d["blockType"] = bt;
        var cn = p.Attr(key, "IExpandoInformationData", "CustomerTypePartName");
        if (Py.Bool(cn))
            d["customerName"] = cn;
        if (keys)
            d["key"] = key;
        return d;
    }

    /// <summary>The ICoreAttributes fields spread into the library entries (**self.core(key)).</summary>
    void Core(PyDict d, string key)
    {
        var a = p.Sets(key).Set("ICoreAttributes");
        d["subtype"] = a.Get("Subtype");
        d["author"] = a.Get("Author");
        d["lastModifiedBy"] = a.Get("LastModifiedBy");
        d["creationTime"] = a.Get("CreationTime");
        d["modifiedTime"] = a.Get("ModifiedTime");
        d["comment"] = a.Get("Comment") is byte[] cb ? ProjectModel.CoreText(cb, cultures) : new PyDict();
    }

    string? EnvironmentMc(string? key)
    {
        var env = Rel1(key, "Environment");
        return env is not null && p.IsA(env, "CoreLibraryMasterCopy") ? env : null;
    }

    string? EnvironmentVersion(string? key)
    {
        var env = Rel1(key, "Environment");
        return env is not null && IsAny(env, VersionBases) ? env : null;
    }

    string? LibraryOf(string? key)
    {
        var seen = new HashSet<string>();
        while (key is not null && seen.Add(key))
        {
            if (p.IsA(key, "CoreLibrary"))
                return key;
            key = Rel1(key, "Environment") ?? Rel1(key, "AggregatingFolder") ?? Rel1(key, "FolderParent");
        }
        return null;
    }

    (object? SystemFolder, string Path) FolderInfo(string key)
    {
        var parts = new List<string?>();
        var f = Rel1(key, "AggregatingFolder") ?? Rel1(key, "FolderParent");
        var seen = new HashSet<string>();
        string Join()
        {
            var r = parts.AsEnumerable().Reverse().ToList();
            if (r.Any(x => x is null))
                throw new PyTypeError("sequence item: expected str instance, NoneType found");
            return string.Join("/", r);
        }
        while (f is not null && !seen.Contains(f) && p.IsA(f, "FolderData"))
        {
            seen.Add(f);
            var parent = Rel1(f, "FolderParent") ?? Rel1(f, "AggregatingFolder");
            if (parent is null || p.IsA(parent, "CoreLibrary"))
                return (p.Attr(f, "ICoreAttributes", "Subtype"), Join());
            parts.Add(p.Name(f));
            f = parent;
        }
        return (null, Join());
    }

    // --- folders ----------------------------------------------------------------------------------
    (List<string> Sub, List<string> Elems) Children(string f)
    {
        var sub = new List<string>();
        var elems = new List<string>();
        var cand = p.Targets(f, "Folders").Concat(Sources(f, "FolderParent")).Concat(p.Targets(f, "AggregatedElements")).Concat(Sources(f, "AggregatingFolder"));
        foreach (var c in Py.Unique(cand))
        {
            if (c == f || !p.Objects.ContainsKey(c))
                continue;
            (p.IsA(c, "FolderData") ? sub : elems).Add(c);
        }
        return (sub, elems);
    }

    PyDict Folder(string f, string path, bool keys, HashSet<string> seen)
    {
        seen.Add(f);
        var (sub, elems) = Children(f);
        var d = PyDict.Of("name", p.Name(f), "subtype", p.Attr(f, "ICoreAttributes", "Subtype"), "path", path,
            "modifiedTime", p.Attr(f, "ICoreAttributes", "ModifiedTime"),
            "elements", Py.Sorted(elems.Select(e => ObjRef(e, keys)), x => Py.T(x["type"], Py.Or(x["name"], ""))).Cast<object?>().ToList(),
            "folders", new List<object?>());
        foreach (var s in Py.Sorted(sub, k => Py.Or(p.Name(k), "")))
        {
            if (seen.Contains(s))
                continue;
            ((List<object?>)d["folders"]!).Add(Folder(s, (path.Length > 0 ? path + "/" : "") + (p.Name(s) ?? ""), keys, seen));
        }
        if (keys)
            d["key"] = f;
        return d;
    }

    // --- master copies ----------------------------------------------------------------------------
    PyDict MasterCopy(string mc, bool keys)
    {
        var (attrs, problems) = p.Attributes(mc);
        var mca = attrs.Set("ICoreLibMasterCopyAttributes");
        var lo = attrs.Set("ICoreLibObjectAttributes");
        var (sysf, path) = FolderInfo(mc);
        var copies = Sources(mc, "Environment");
        var visible = p.Targets(mc, "VisibleParts");
        var context = new List<object?>();
        foreach (var dv in p.Targets(mc, "Devices"))
        {
            var r = ObjRef(dv, keys);
            r["relation"] = "Devices";
            r["isShell"] = p.Attr(dv, "ICoreAttributes", "IsShell");
            context.Add(r);
        }
        var tgts = new List<string>();
        foreach (var v in visible)
            foreach (var t in p.Targets(v, "Target"))
                if (!tgts.Contains(t))
                    tgts.Add(t);
        foreach (var t in tgts)
        {
            var r = ObjRef(t, keys);
            r["relation"] = "Target (of visible parts)";
            context.Add(r);
        }
        object? BlobLen(object? v) => v is byte[] b ? (long)b.Length : null;
        var stubs = copies.Where(c => p.Targets(c, "TRefParent").Count > 0 && !p.IsA(c, "FolderData")).ToList();
        var frozen = copies.Where(c => p.IsA(c, "InterfaceVersionRootData") && p.Targets(c, "VersionElementToBlock").Any(stubs.Contains)).ToList();
        var byType = new PyDict();
        foreach (var c in copies)
            byType[p.TypeName(c)] = Py.Long(byType.Get(p.TypeName(c), 0L)) + 1;
        var source = ParseSharedRawData(mca.Get("SharedRawData"));
        if (source is not null && source.Count > 0 && !Py.Bool(source.Get("error")))
            foreach (PyDict sc in (List<object?>)source["sourceScopePath"]!)
            {
                var k = ParseScopeId(sc["id"]);
                if (k is not null)
                {
                    sc["sourceKey"] = k;
                    sc["sourceObject"] = p.Objects.ContainsKey(k) ? ObjRef(k, keys) : null;
                }
            }
        var rp = Rel1(mc, "RelatedPackage");
        var fold = Rel1(mc, "AggregatingFolder");
        var order = fold is not null ? p.Targets(fold, "AggregatedElements") : [];
        var d = PyDict.Of("name", p.Name(mc), "systemFolder", sysf, "folderPath", path, "folderIndex", order.Contains(mc) ? (long)order.IndexOf(mc) : null);
        Core(d, mc);
        d["state"] = lo.Get("State");
        d["isSystemElement"] = lo.Get("IsSystemElement");
        d["originalLibrary"] = lo.Get("OriginalLibrary");
        d["originalName"] = lo.Get("OriginalName");
        d["source"] = source;
        d["coreObjects"] = DecodeCoreObjectData(mm, mca.Get("CoreObjectData"));
        d["nonCoreObjects"] = DecodeStructArrayCount(mm, "ArrayLibraryClipboardNonCoreObject", mca.Get("NonCoreObjectData"));
        d["contents"] = visible.Select(v => (object?)ObjRef(v, keys)).ToList();
        d["parts"] = p.Targets(mc, "Parts").Select(v => (object?)ObjRef(v, keys)).ToList();
        d["invisibleParts"] = p.Targets(mc, "InvisibleParts").Select(v => (object?)ObjRef(v, keys)).ToList();
        d["context"] = context;
        d["referencedObjects"] = Py.Sorted(stubs, k => Py.Or(p.Name(k), "")).Select(s =>
        {
            var r = ObjRef(s, keys);
            r["stub"] = true;
            return (object?)r;
        }).ToList();
        d["frozenInterfaces"] = frozen.Select(f => (object?)ObjRef(f, keys)).ToList();
        d["copiedObjects"] = (long)copies.Count;
        var bt = new PyDict();
        foreach (var kv in Py.Sorted(byType.Items, kv => Py.T(kv.Key, kv.Value)))
            bt[kv.Key] = kv.Value;
        d["copiedObjectsByType"] = bt;
        d["relatedPackage"] = rp is not null ? p.Name(rp) : null;
        d["categories"] = p.Targets(mc, "Categories").Select(c => (object?)ObjRef(c, keys)).ToList();
        d["customAttributes"] = attrs.SetOrNull("IMasterCopyCustomAttributes") is { Count: > 0 } ca ? ca : new PyDict();
        d["raw"] = PyDict.Of("coreObjectData", Py.Bool(BlobLen(mca.Get("CoreObjectData"))) ? Py.HexStr((byte[])mca["CoreObjectData"]!) : null,
            "binaryDataBytes", BlobLen(mca.Get("BinaryData")), "nonCoreObjectDataBytes", BlobLen(mca.Get("NonCoreObjectData")));
        d["package"] = mm.TypePackage(p.Objects[mc].Rec.TypeId);
        if (problems.Count > 0)
            d["decodeProblems"] = problems;
        if (keys)
        {
            d["key"] = mc;
            d["copiedObjectKeys"] = Py.Sorted(copies).Cast<object?>().ToList();
        }
        return d;
    }

    // --- types / versions / instances -------------------------------------------------------------
    PyDict VersionRef(string v)
    {
        var typ = Rel1(v, "VersionObjectToTypeObject");
        var txt = DecodeVersionData(p.Attr(v, "IVersionInfo", "Version")).Text;
        return PyDict.Of("typeName", typ is not null ? p.Name(typ) : null, "version", txt,
            "versionGuid", p.Attr(v, "IVersionInfo", "VersionGuid"), "state", p.Attr(v, "ICoreLibObjectAttributes", "State"));
    }

    List<object?> VersionGuids() => versionGuids ??= p.Objects.Keys.Where(k => IsAny(k, VersionBases)).Select(k => p.Attr(k, "IVersionInfo", "VersionGuid")).ToList();

    List<string> VersionInstances(string v, bool inCopies = false)
    {
        var cand = Py.Unique(p.Targets(v, "IsTypeOf").Where(p.Objects.ContainsKey).Concat(Sources(v, "IsInstanceOf")));
        return cand.Where(x => (EnvironmentMc(x) is not null || EnvironmentVersion(x) is not null) == inCopies).ToList();
    }

    List<string> MasterCopiesContaining(string v) =>
        Py.Unique(VersionInstances(v, true).Select(EnvironmentMc).Where(x => x is not null).Select(x => x!));

    List<string> InstanceVersions(string k) =>
        Py.Unique(p.Targets(k, "IsInstanceOf").Where(x => IsAny(x, VersionBases)).Concat(Sources(k, "IsTypeOf").Where(s => IsAny(s, VersionBases))));

    PyDict InstanceVersionState(string k, string v, bool keys)
    {
        var typ = Rel1(v, "VersionObjectToTypeObject");
        var own = VersionRef(v);
        var res = PyDict.Of("typeName", own["typeName"], "version", own["version"], "versionGuid", own["versionGuid"],
            "state", own["state"], "defaultVersion", null, "latestVersion", null, "usesDefaultVersion", null,
            "newerVersionExists", null, "outdated", null);
        if (typ is not null)
        {
            var allv = Py.Unique(p.Targets(typ, "TypeObjectToVersionObject").Concat(Sources(typ, "VersionObjectToTypeObject")));
            var committed = allv.Where(x => Py.Eq(p.Attr(x, "ICoreLibObjectAttributes", "State"), "Committed")).ToList();
            var dflt = Rel1(typ, "TypeToDefaultVersion") ?? Sources(typ, "DefaultVersionToType").FirstOrDefault();
            var latest = Py.Max(committed, x => VersionKey(VersionRef(x)["version"]));
            res["defaultVersion"] = dflt is not null ? VersionRef(dflt)["version"] : null;
            res["latestVersion"] = latest is not null ? VersionRef(latest)["version"] : null;
            res["usesDefaultVersion"] = dflt is not null ? v == dflt : null;
            if (Py.Eq(own["state"], "Committed") && latest is not null)
            {
                res["newerVersionExists"] = Py.Compare(VersionKey(own["version"]), VersionKey(res["latestVersion"])) < 0;
                res["outdated"] = res["newerVersionExists"];
            }
        }
        var libIf = Rel1(k, "CurrentLibraryInterface");
        var curIf = Rel1(k, "CurrentInterface");
        var curExp = curIf is not null ? p.Sets(curIf).Set("ICoreExpandoAttributes") : new PyDict();
        var utvg = curExp.Get("UserTypeVersionGUID");
        var itf = PyDict.Of("libraryInterface", libIf is not null ? p.Name(libIf) : null,
            "interfaceRoot", curIf is not null ? p.Name(curIf) : null,
            "interfaceVersionGuid", utvg,
            "interfaceVersionGuidMatches", Py.Bool(utvg) ? Py.Eq(utvg, own["versionGuid"]) : null,
            "libraryInterfaceMatches", libIf is not null ? (p.Name(libIf) ?? "").EndsWith("/" + Py.Str(Py.Or(own["versionGuid"], "?")), StringComparison.Ordinal) : null,
            "hasBlockToVersionElements", p.Targets(k, "BlockToVersionElements").Count > 0);
        res["interface"] = itf;
        var btve = Rel1(k, "BlockToVersionElements");
        if (btve is not null && btve != curIf)
        {
            var oldGuid = p.Sets(btve).Set("ICoreExpandoAttributes").Get("UserTypeVersionGUID");
            var old = Py.Bool(oldGuid) ? p.Objects.Keys.FirstOrDefault(x => IsAny(x, VersionBases) && Py.Eq(p.Attr(x, "IVersionInfo", "VersionGuid"), oldGuid)) : null;
            itf["previousInterfaceRoot"] = p.Name(btve);
            itf["previousVersionGuid"] = oldGuid;
            res["previousVersion"] = old is not null ? VersionRef(old)["version"] : null;
        }
        if (keys)
            itf["keys"] = PyDict.Of("libraryInterface", libIf, "interfaceRoot", curIf, "version", v);
        return res;
    }

    PyDict Version(string v, bool keys)
    {
        var attrs = p.Sets(v);
        var vi = attrs.Set("IVersionInfo");
        var (txt, raw, parts) = DecodeVersionData(vi.Get("Version"));
        var typ = Rel1(v, "VersionObjectToTypeObject");
        var depRaw = attrs.Set("IDependencyInfo").Get("DependencyIdentifiers");
        var copies = Sources(v, "Environment");
        var d = PyDict.Of("name", p.Name(v), "type", p.TypeName(v), "package", mm.TypePackage(p.Objects[v].Rec.TypeId),
            "version", txt, "versionParts", parts, "versionRaw", raw, "versionGuid", vi.Get("VersionGuid"),
            "finalizationTime", vi.Get("FinalizationTime"),
            "minimumTargetDeviceVersion", vi.Get("MinimumTargetDeviceVersion"),
            "state", attrs.Set("ICoreLibObjectAttributes").Get("State"),
            "originalLibrary", attrs.Set("ICoreLibObjectAttributes").Get("OriginalLibrary"),
            "manufacturer", attrs.Set("IVersionManufacturingInfo").Get("Manufacturer"),
            "systemVersionOrderNumber", attrs.Set("ISystemVersionOrderInfo").Get("SystemVersionOrderNumber"),
            "isDefault", p.Targets(v, "DefaultVersionToType").Count > 0 || (typ is not null && p.Targets(typ, "TypeToDefaultVersion").Contains(v)),
            "ofType", typ is not null ? p.Name(typ) : null);
        d["previous"] = p.Targets(v, "Previous").Select(x => (object?)VersionRef(x)).ToList();
        d["next"] = p.Targets(v, "Next").Select(x => (object?)VersionRef(x)).ToList();
        d["dependsOn"] = p.Targets(v, "DependsOn").Select(x => (object?)(IsAny(x, VersionBases) ? VersionRef(x) : ObjRef(x, keys))).ToList();
        d["usedBy"] = Py.Unique(p.Targets(v, "DependsOnInverse").Concat(Sources(v, "DependsOn"))).Where(x => IsAny(x, VersionBases))
            .Select(x => (object?)VersionRef(x)).ToList();
        d["dependencyIdentifiers"] = ParseDependencyIdentifiers(depRaw, VersionGuids());
        d["dependencyIdentifiersRaw"] = depRaw is byte[] db ? Py.HexStr(db) : depRaw;
        d["instances"] = VersionInstances(v).Select(x =>
        {
            var r = ObjRef(x, keys);
            var t = Rel1(x, "Target");
            r["device"] = t is not null ? p.Name(t) : null;
            return (object?)r;
        }).ToList();
        d["masterCopiesContainingInstances"] = MasterCopiesContaining(v).Select(mc => (object?)p.Name(mc)).ToList();
        d["contents"] = p.Targets(v, "VisibleParts").Select(x => (object?)ObjRef(x, keys)).ToList();
        d["context"] = p.Targets(v, "Devices").Select(x =>
        {
            var r = ObjRef(x, keys);
            r["isShell"] = p.Attr(x, "ICoreAttributes", "IsShell");
            return (object?)r;
        }).ToList();
        d["copiedObjects"] = (long)copies.Count;
        Core(d, v);
        var img = Sources(v, "ContentToVersion").FirstOrDefault();
        if (img is not null && p.IsA(img, "HmiImageData"))
            d["image"] = Hmi.ImageInfo(p, img);
        var tl = p.Targets(v, "VersionToContent").FirstOrDefault();
        if (tl is not null && p.IsA(tl, "HmiTextListData"))
        {
            var entries = new List<object?>();
            var children = p.Targets(tl, "HmiTextListEntryDataChildren") is { Count: > 0 } ch ? ch
                : p.Links(tl).Where(l => l.Relation is null && p.Objects.ContainsKey(l.Key) && p.IsA(l.Key, "HmiTextListEntryData")).Select(l => l.Key).ToList();
            foreach (var e in children)
            {
                var la = p.Sets(e).Set("IHmiListEntryAttributes");
                var text = new PyDict();
                foreach (var t in p.Targets(e, "ExpandoChildren"))
                    if (p.IsA(t, "HmiTextData"))
                        text.Update(Hmi.Plain(ProjectModel.CoreText(p.Attr(t, "ICoreTextRepository", "Text"))));
                entries.Add(PyDict.Of("from", la.Get("From"), "to", la.Get("To"), "type", la.Get("EntryType"), "default", la.Get("DefaultEntry"), "text", text));
            }
            d["textList"] = PyDict.Of("name", p.Name(tl), "entries", entries);
        }
        // graphic list type (sample L40): HmiGraphicListData with HmiGraphicListEntryData; the graphic of an entry is an image
        // type of the library (ExpandoChildren -> ImageInstanceData -IsInstanceOf-> ImageLibraryVersionData -> type)
        if (tl is not null && p.IsA(tl, "HmiGraphicListData"))
        {
            var entries = new List<object?>();
            foreach (var e in p.Links(tl).Where(l => p.Objects.ContainsKey(l.Key) && p.IsA(l.Key, "HmiGraphicListEntryData")).Select(l => l.Key).Distinct())
            {
                var la = p.Sets(e).Set("IHmiListEntryAttributes");
                var inst = p.Targets(e, "ExpandoChildren").FirstOrDefault(x => p.IsA(x, "ImageInstanceData"));
                entries.Add(PyDict.Of("from", la.Get("From"), "to", la.Get("To"), "type", la.Get("EntryType"), "default", la.Get("DefaultEntry"),
                    "graphicType", inst is not null ? Hmi.LibraryImageName(p, inst) : null));
            }
            d["graphicList"] = PyDict.Of("name", p.Name(tl), "entries", entries);
        }
        // content of HMI type versions (sample L37): a script module type holds a global script module, a dynamic SVG type a
        // resource whose file (CoreFile, compressed) is the .svghmi file
        if (tl is not null && p.IsA(tl, "HmiScriptGlobalModuleData") && scriptModuleReader is not null)
            d["scriptModule"] = scriptModuleReader(tl);
        if (tl is not null && p.IsA(tl, "HmiDynamicScreenItemResourceData")
            && p.Targets(tl, "IHmiDynamicScreenItemResourceAttributes_ResourceFile").FirstOrDefault() is { } cf)
        {
            var data = p.Attr(cf, "ICoreFileData", "Content") is byte[] cfRaw ? p.DecodeBlob(cfRaw) : null;
            d["dynamicSvg"] = PyDict.Of("file", p.Name(cf), "bytes", data is not null ? (long)data.Length : null,
                "sha256", data is not null ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant() : null,
                "text", data is not null ? Py.Utf8SigExact(data) : null);
        }
        if (keys)
        {
            d["key"] = v;
            d["copiedObjectKeys"] = Py.Sorted(copies).Cast<object?>().ToList();
        }
        return d;
    }

    PyDict LibType(string t, bool keys)
    {
        var attrs = p.Sets(t);
        var lt = attrs.Set("ICoreLibTypeAttributes");
        var ti = attrs.Set("ITypeInfo");
        var tb = attrs.Set("ITypeBaseAttributesInfo");
        var ct = attrs.Set("ICoreTypeObjectAttributes");
        var lo = attrs.Set("ICoreLibObjectAttributes");
        var (sysf, path) = FolderInfo(t);
        var major = lt.Get("MajorVersion");
        var ver = major is null ? null : $"{Py.Str(major)}.{Py.Str(lt.Get("MinorVersion"))}.{Py.Str(lt.Get("SubminorVersion"))}";
        var vkeys = Py.Unique(p.Targets(t, "TypeObjectToVersionObject").Concat(Sources(t, "VersionObjectToTypeObject")));
        var versions = Py.Sorted(vkeys.Select(v => Version(v, keys)), x => VersionKey(x["version"]));
        var dflt = Rel1(t, "TypeToDefaultVersion") ?? Sources(t, "DefaultVersionToType").FirstOrDefault();
        var states = versions.Select(v => v["state"]).ToList();
        var committed = versions.Where(v => Py.Eq(v["state"], "Committed")).Select(v => v["version"]).ToList();
        var latest = committed.Count > 0 ? committed[^1] : null;
        var nInst = versions.Sum(v => (long)((List<object?>)v["instances"]!).Count);
        var nOld = versions.Where(v => Py.Eq(v["state"], "Committed") && Py.Bool(latest) && Py.Compare(VersionKey(v["version"]), VersionKey(latest)) < 0)
            .Sum(v => (long)((List<object?>)v["instances"]!).Count);
        var tn = p.TypeName(t);
        var d = PyDict.Of("name", p.Name(t), "type", tn, "opennessKind", tn is not null && OpennessTypeKind.TryGetValue(tn, out var ok) ? ok : null,
            "category", p.IsA(t, "TypeObjectBase") ? "user" : "system",
            "package", mm.TypePackage(p.Objects[t].Rec.TypeId),
            "systemFolder", sysf, "folderPath", path,
            "coreLibVersion", ver, "coreLibState", lt.Get("State"),
            "typeObjectId", ct.Get("TypeObjectId"), "versionId", ct.Get("VersionId"),
            "originalLibrary", lo.Get("OriginalLibrary"), "libObjectState", lo.Get("State"),
            "state", states.Any(s => Py.Eq(s, "InWork")) ? "InWork" : states.Count > 0 ? "Committed" : null,
            "typeGuid", ti.Get("TypeGuid"), "originalTypeGuid", tb.Get("OriginalTypeGuid"),
            "typeNamespace", tb.Get("TypeNamespace"), "isObsolete", tb.Get("IsObsolete"),
            "isSetAsNonUpdateable", tb.Get("IsSetAsNonUpdateable"),
            "defaultVersion", dflt is not null ? DecodeVersionData(p.Attr(dflt, "IVersionInfo", "Version")).Text : null,
            "latestVersion", latest, "instanceCount", nInst, "outdatedInstances", nOld);
        d["dependsOn"] = p.Targets(t, "DependsOn").Select(x => (object?)ObjRef(x, keys)).ToList();
        d["contents"] = p.Targets(t, "VisibleParts").Select(x => (object?)ObjRef(x, keys)).ToList();
        d["versions"] = versions.Cast<object?>().ToList();
        Core(d, t);
        if (keys)
            d["key"] = t;
        return d;
    }

    (List<(PyDict T, string K)> Types, List<PyDict> Orphan, List<PyDict> Inst, PyDict Stats) TypesAndVersions(bool keys)
    {
        var types = new List<string>();
        var versions = new List<string>();
        long inMc = 0, inVer = 0;
        foreach (var k in p.Objects.Keys)
        {
            if (EnvironmentVersion(k) is not null)
                inVer++;
            if (IsAny(k, TypeBases))
            {
                if (EnvironmentMc(k) is null)
                    types.Add(k);
                else
                    inMc++;
            }
            else if (IsAny(k, VersionBases))
            {
                if (EnvironmentMc(k) is null)
                    versions.Add(k);
                else
                    inMc++;
            }
        }
        long nInstCapable = 0;
        var inst = new List<PyDict>();
        foreach (var k in p.Objects.Keys)
        {
            if (!IsAny(k, InstanceBases))
                continue;
            nInstCapable++;
            if (EnvironmentVersion(k) is not null || EnvironmentMc(k) is not null)
                continue;
            var vers = InstanceVersions(k);
            if (vers.Count == 0)
                continue;
            var d = ObjRef(k, keys);
            d["instanceOf"] = vers.Select(v => (object?)VersionRef(v)).ToList();
            var tgt = Rel1(k, "Target");
            d["device"] = tgt is not null ? p.Name(tgt) : null;
            d["creationTime"] = p.Attr(k, "ICoreAttributes", "CreationTime");
            d["versionState"] = InstanceVersionState(k, vers[0], keys);
            inst.Add(d);
        }
        var typed = types.Select(t => LibType(t, keys)).ToList();
        var owned = types.SelectMany(t => p.Targets(t, "TypeObjectToVersionObject").Concat(Sources(t, "VersionObjectToTypeObject"))).ToHashSet();
        var orphan = versions.Where(v => !owned.Contains(v)).Select(v => Version(v, keys)).ToList();
        var pairs = typed.Zip(types).Select(x => (x.First, x.Second)).ToList();
        return (Py.Sorted(pairs, x => Py.T(Py.Or(x.First.Get("systemFolder"), ""), Py.Or(x.First.Get("folderPath"), ""), Py.Or(x.First["name"], ""))),
            Py.Sorted(orphan, x => Py.Or(x["name"], "")),
            Py.Sorted(inst, x => Py.T(x["type"], Py.Or(x["name"], ""))),
            PyDict.Of("instanceCapableObjects", nInstCapable, "libraryObjectsInsideMasterCopies", inMc, "objectsInsideVersions", inVer));
    }

    // --- all --------------------------------------------------------------------------------------
    public PyDict ToDict(bool keys = false)
    {
        var libs = new List<PyDict>();
        var libKeys = Py.Sorted(p.OfType("CoreLibrary")).Where(k => EnvironmentMc(k) is null).ToList();
        foreach (var lk in libKeys)
        {
            var la = p.Sets(lk);
            var seen = new HashSet<string>();
            var sysfolders = Py.Sorted(p.Targets(lk, "Folders").Concat(Sources(lk, "FolderParent").Where(s => p.IsA(s, "FolderData"))),
                k => Py.T((long)(p.Attr(k, "ICoreAttributes", "Subtype") is string st && Array.IndexOf(SystemFolderOrder, st) is var i and >= 0 ? i : 99), Py.Or(p.Name(k), "")));
            var folders = Py.Unique(sysfolders).Select(f => (object?)Folder(f, "", keys, seen)).ToList();
            var d = PyDict.Of("name", p.Name(lk), "libraryType", la.Set("ICoreLibAttributes").Get("LibraryType"),
                "isWriteProtected", la.Set("ICoreLibAttributes").Get("IsWriteProtected"),
                "isContentWriteProtected", la.Set("ILibraryProtectionInfo").Get("IsLibraryContentWriteProtected"));
            Core(d, lk);
            d["systemFolders"] = folders;
            d["masterCopies"] = new List<object?>();
            d["types"] = new List<object?>();
            d["versionsWithoutType"] = new List<object?>();
            if (keys)
                d["key"] = lk;
            libs.Add(d);
        }
        var byKey = new Dictionary<string, PyDict>();
        for (var i = 0; i < libKeys.Count; i++)
            byKey[libKeys[i]] = libs[i];
        var other = PyDict.Of("masterCopies", new List<object?>(), "types", new List<object?>(), "versionsWithoutType", new List<object?>());
        PyDict Target(string? lib) => lib is not null && byKey.TryGetValue(lib, out var x) ? x : other;
        var mcs = p.OfType("CoreLibraryMasterCopy").Where(mc => EnvironmentMc(mc) is null).ToList();
        foreach (var mc in mcs)
            ((List<object?>)Target(LibraryOf(mc))["masterCopies"]!).Add(MasterCopy(mc, keys));
        var (types, orphan, inst, stats) = TypesAndVersions(keys);
        foreach (var (t, tk) in types)
            ((List<object?>)Target(LibraryOf(tk))["types"]!).Add(t);
        ((List<object?>)other["versionsWithoutType"]!).AddRange(orphan);
        foreach (var l in libs)
            l["masterCopies"] = Py.Sorted(((List<object?>)l["masterCopies"]!).Cast<PyDict>(), x => Py.T(Py.Or(x["systemFolder"], ""), x["folderPath"],
                x["folderIndex"] ?? (object)(1L << 30), Py.Or(x["name"], ""))).Cast<object?>().ToList();
        string[] libBases = [.. TypeBases, .. VersionBases, "CoreLibraryMasterCopy", "CoreLibrary"];
        var deleted = new PyDict();
        foreach (var k in p.Deleted)
        {
            if (mm.Types.TryGetValue(KeyTypeId(k), out var t) && mm.Ancestors(t.FullName).Any(a => libBases.Any(b => a.EndsWith("." + b, StringComparison.Ordinal))))
                deleted[t.Name] = Py.Long(deleted.Get(t.Name, 0L)) + 1;
        }
        var st2 = new PyDict(stats);
        st2["masterCopies"] = (long)mcs.Count;
        st2["types"] = (long)types.Count;
        st2["versions"] = types.Sum(t => (long)((List<object?>)t.T["versions"]!).Count) + orphan.Count;
        st2["deletedLibraryObjectsHidden"] = deleted;
        var res = PyDict.Of("libraries", libs.Cast<object?>().ToList(), "instances", inst.Cast<object?>().ToList(), "statistics", st2);
        if (other.Values.Any(Py.Bool))
            res["unassigned"] = other;
        return res;
    }

    /// <summary>Library view of a project (Model.ToDict puts it under "library").</summary>
    /// <summary>scriptModule: reader of a global script module (Hmi.ScriptModule) for the content of script module type versions.</summary>
    public static PyDict Extract(Project p, bool keys = false, Func<string, PyDict>? scriptModule = null) =>
        new Library(p) { scriptModuleReader = scriptModule }.ToDict(keys);

    // --- text output ----------------------------------------------------------------------------------
    static string FmtObj(PyDict o)
    {
        var bt = o.Get("blockType");
        var head = Py.Bool(bt) ? $"{Py.Str(o["type"])}/{Py.Str(bt)}" : Py.Str(o["type"]);
        return head + " " + Py.Repr(o["name"]);
    }

    static List<PyDict> Dicts(object? l) => ((List<object?>)l!).Cast<PyDict>().ToList();

    public static string ToText(PyDict d)
    {
        var o = new List<string>();
        foreach (var L in Dicts(d["libraries"]))
        {
            o.Add($"Library {Py.Repr(L["name"])} ({Py.Str(L["libraryType"])}){(Py.Bool(L["isWriteProtected"]) ? " write-protected" : "")}");
            void Walk(PyDict f, int ind)
            {
                o.Add($"{new string(' ', 2 * ind)}[{Py.Str(f["name"])}]{(Py.Eq(f["name"], f["subtype"]) ? "" : $" ({Py.Str(f["subtype"])})")}");
                foreach (var e in Dicts(f["elements"]))
                    o.Add($"{new string(' ', 2 * (ind + 1))}- {FmtObj(e)}");
                foreach (var s in Dicts(f["folders"]))
                    Walk(s, ind + 1);
            }
            foreach (var f in Dicts(L["systemFolders"]))
                Walk(f, 1);
            foreach (var m in Dicts(L["masterCopies"]))
            {
                o.Add($"  Master copy {Py.Repr(m["name"])} in {Py.Str(m["systemFolder"])}{(Py.Bool(m["folderPath"]) ? "/" + Py.Str(m["folderPath"]) : "")}");
                o.Add($"    subtype {Py.Str(m["subtype"])}, state {Py.Str(m["state"])}, author {Py.Str(m["author"])}, created {Py.Str(m["creationTime"])}, modified {Py.Str(m["modifiedTime"])}");
                var src = m["source"] as PyDict ?? new PyDict();
                foreach (PyDict td in src.Get("typeDescriptors", new List<object?>()) as List<object?> ?? [])
                    o.Add($"    source type {Py.Str(td.Get("typeName"))} roles {string.Join(",", ((List<object?>)td.Get("roles", new List<object?>())!).Select(Py.Str))}");
                if (Py.Bool(src.Get("sourceScopePath")))
                    o.Add("    source scope " + string.Join(" / ", Dicts(src["sourceScopePath"]).Select(s => $"{Py.Str(s["type"])}={Py.Str(s["id"])}")));
                foreach (var c in Dicts(m["contents"]))
                    o.Add($"    contains {FmtObj(c)}");
                foreach (var c in Dicts(m["context"]))
                    o.Add($"    context  {FmtObj(c)} via {Py.Str(c["relation"])}");
                foreach (var c in Dicts(m.Get("referencedObjects", new List<object?>())))
                    o.Add($"    references {FmtObj(c)} (name stub)");
                foreach (var c in Dicts(m.Get("frozenInterfaces", new List<object?>())))
                    o.Add($"    frozen interface {Py.Repr(c["name"])}");
                var co = m.Get("coreObjects") as PyDict ?? new PyDict();
                foreach (var e in Dicts(co.Get("entries", new List<object?>())))
                    o.Add($"    clipboard object {Py.Str(e["objectRole"])} sourceId {Py.Str(e["sourceObjectId"])} parent {Py.Str(e["parentObjectId"])} visualPartIndex {Py.Str(e["visualPartIndex"])}{(Py.Bool(e["option"]) ? " option " + Py.Repr(e["option"]) : "")}");
                if (Py.Bool(co.Get("error")))
                    o.Add($"    CoreObjectData not decoded: {Py.Str(co["error"])}");
                foreach (var sc in Dicts(src.Get("sourceScopePath", new List<object?>())))
                {
                    if (Py.Bool(sc.Get("sourceKey")))
                    {
                        var so = sc.Get("sourceObject") as PyDict;
                        o.Add($"    scope {Py.Str(sc["type"])} = {Py.Str(sc["sourceKey"])} {(so is { Count: > 0 } ? FmtObj(so) : "(not in project)")}");
                    }
                }
                o.Add($"    copied objects: {Py.Long(m["copiedObjects"])}");
            }
            foreach (var t in Dicts(L["types"]))
            {
                if (Py.Eq(t["category"], "system"))
                {
                    o.Add($"  System type {Py.Repr(t["name"])} ({Py.Str(t["type"])}) in {Py.Str(t["systemFolder"])}{(Py.Bool(t["folderPath"]) ? "/" + Py.Str(t["folderPath"]) : "")} version {Py.Str(t["coreLibVersion"])} ({Py.Str(t["coreLibState"])}) typeObjectId {Py.Str(t["typeObjectId"])} versionId {Py.Str(t["versionId"])} from {Py.Str(t["originalLibrary"])}");
                    foreach (var c in Dicts(t["contents"]))
                        o.Add($"    contains {FmtObj(c)}");
                    if (Py.Bool(t["dependsOn"]))
                        o.Add($"    depends on {string.Join(", ", Dicts(t["dependsOn"]).Select(x => Py.Str(x["name"])))}");
                    continue;
                }
                o.Add($"  Type {Py.Repr(t["name"])} ({Py.Str(t["type"])} / {Py.Str(t["opennessKind"])}) in {Py.Str(t["systemFolder"])}{(Py.Bool(t["folderPath"]) ? "/" + Py.Str(t["folderPath"]) : "")} guid {Py.Str(t["typeGuid"])} state {Py.Str(t["state"])} default {Py.Str(t["defaultVersion"])} latest {Py.Str(t.Get("latestVersion"))} instances {Py.Long(t.Get("instanceCount", 0L))} (outdated {Py.Long(t.Get("outdatedInstances", 0L))})");
                foreach (var v in Dicts(t["versions"]))
                {
                    o.Add($"    version {Py.Str(v["version"])} {Py.Str(v["state"])}{(Py.Bool(v["isDefault"]) ? " (default)" : "")} finalized {Py.Str(v["finalizationTime"])} guid {Py.Str(v["versionGuid"])} author {Py.Str(v["author"])}");
                    if (Py.Bool(v["comment"]))
                        o.Add($"      comment {Py.Str(v["comment"])}");
                    foreach (var c in Dicts(v["contents"]))
                        o.Add($"      content copy {FmtObj(c)}{(Py.Bool(c.Get("customerName")) ? " = " + Py.Repr(c["customerName"]) : "")}");
                    foreach (var x in Dicts(v["dependsOn"]))
                        o.Add($"      depends on {Py.Str(x.Get("typeName"))} {Py.Str(x.Get("version"))} ({Py.Str(x.Get("versionGuid"))})");
                    foreach (var x in Dicts(v["usedBy"]))
                        o.Add($"      used by {Py.Str(x.Get("typeName"))} {Py.Str(x.Get("version"))}");
                    foreach (var x in Dicts(v["instances"]))
                        o.Add($"      instance {FmtObj(x)} on {Py.Str(x.Get("device"))}");
                    foreach (var x in (List<object?>)v.Get("masterCopiesContainingInstances", new List<object?>())!)
                        o.Add($"      typed copy inside master copy {Py.Repr(x)}");
                }
            }
        }
        if (Py.Bool(d.Get("unassigned")))
            o.Add("Unassigned: " + Py.HeadCp(PyJson.Dumps(d["unassigned"], null, false, PyJson.Fallback.Str), 2000));
        foreach (var i in Dicts(d["instances"]))
        {
            var vs = i.Get("versionState") as PyDict ?? new PyDict();
            var flags = new List<string>();
            if (Py.Bool(vs.Get("outdated")))
                flags.Add($"OUTDATED, latest {Py.Str(vs.Get("latestVersion"))}");
            if (vs.Get("usesDefaultVersion") is false)
                flags.Add($"not default {Py.Str(vs.Get("defaultVersion"))}");
            if (Py.Bool((vs.Get("interface") as PyDict ?? new PyDict()).Get("previousInterfaceRoot")))
                flags.Add($"updated from {Py.Str(Py.OrElse(vs.Get("previousVersion"), () => ((PyDict)vs["interface"]!).Get("previousVersionGuid")))}");
            var itf = vs.Get("interface") as PyDict ?? new PyDict();
            if (itf.Get("interfaceVersionGuidMatches") is false || itf.Get("libraryInterfaceMatches") is false)
                flags.Add("INTERFACE VERSION MISMATCH");
            o.Add($"Instance {FmtObj(i)} on {Py.Str(i["device"])} -> {string.Join(", ", Dicts(i["instanceOf"]).Select(x => $"{Py.Str(x["typeName"])} {Py.Str(x["version"])} {Py.Str(x["state"])}"))}{(flags.Count > 0 ? $" ({string.Join("; ", flags)})" : "")}");
        }
        o.Add("Statistics: " + PyJson.Dumps(d["statistics"], null, false));
        return string.Join("\n", o);
    }
}
