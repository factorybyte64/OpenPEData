using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>PLC alarms (port of plc_alarms.py): SimpleControllerAlarmData (type / instance alarms) with their texts and
/// AlarmClassData.</summary>
internal static class PlcAlarms
{
    static readonly string[] TextNames = ["infoText", "alarmText", .. Enumerable.Range(1, 9).Select(i => "additionalText" + i)];

    public static List<long> TextIndexes(object? v)
    {
        if (v is not byte[] b || b.Length < 8)
            return [];
        var n = Bin.U32(b, 4);
        return b.Length >= 8 + 2L * n ? Bin.U16s(b, 8, n).Select(x => (long)x).ToList() : [];
    }

    public static PyDict Extract(ProjectModel model)
    {
        var p = model.P;
        var classes = new List<PyDict>();
        foreach (var k in p.OfType("AlarmClassData"))
        {
            var a = p.Sets(k).Set("IAlarmClassData");
            classes.Add(PyDict.Of("name", p.Name(k), "acknowledgement", a.Get("Acknowledgement"), "readOnly", a.Get("IsReadOnly"),
                "priority", a.Get("Priority"), "globalId", a.Get("GlobalId"), "key", k));
        }
        var alarms = new List<PyDict>();
        foreach (var k in p.OfType("SimpleControllerAlarmData"))
        {
            var at = p.Sets(k);
            var ad = at.Set("IAlarmData");
            var sc = at.Set("ISimpleControllerAlarmData");
            var blk = model.Rel1(k, "PhysicalParent");
            var tgt = model.Rel1(k, "Target");
            var cls = model.Rel1(k, "AlarmClass") ?? model.Sources(k, "Alarms").FirstOrDefault(c => p.IsA(c, "AlarmClassData"));
            var typ = model.Rel1(k, "TypeAlarm");
            if (cls is null && typ is not null)
                cls = model.Rel1(typ, "AlarmClass");
            var idx = TextIndexes(ad.Get("TextIndexes"));
            var texts = new PyDict();
            var tl = p.Targets(k, "Texts");
            for (var n = 0; n < tl.Count; n++)
            {
                var i = n < idx.Count ? idx[n] : n;
                var v = new PyDict();
                foreach (var (c, s) in model.CoreTextOf(p.Attr(tl[n], "ICoreTextRepository", "Text")))
                    if (Py.Bool(s) && !Py.Eq(c, "65535"))
                        v[c] = s;
                if (v.Count > 0)
                    texts[i < TextNames.Length ? TextNames[i] : "text" + i] = v;
            }
            alarms.Add(PyDict.Of("name", p.Name(k), "kind", at.Set("ICoreAttributes").Get("Subtype"),
                "alarmType", at.Set("IAlarmDataExt").Get("AlarmType"),
                "block", blk is not null ? p.Name(blk) : null, "blockType", blk is not null ? p.TypeName(blk) : null,
                "plc", tgt is not null ? p.Name(tgt) : null, "alarmClass", cls is not null ? p.Name(cls) : null,
                "alarmClassSetHere", Py.Bool(sc.Get("AlarmClassIsSet")), "priority", ad.Get("Priority"),
                "typeAlarm", typ is not null ? Py.Str(p.Name(model.Rel1(typ, "PhysicalParent"))) + "/" + Py.Str(p.Name(typ)) : null,
                "texts", texts, "key", k));
        }
        alarms = Py.Sorted(alarms, x => Py.T(Py.Or(x["plc"], ""), Py.Or(x["block"], ""), Py.Or(x["name"], "")));
        classes = Py.Sorted(classes, x => Py.Or(x["name"], ""));
        return PyDict.Of("alarms", alarms.Cast<object?>().ToList(), "alarmClasses", classes.Cast<object?>().ToList());
    }
}

/// <summary>Watch and force tables, software units, OPC UA server interfaces, ProDiag supervisions, PLC text lists
/// (port of plc_tables.py).</summary>
internal static class PlcTables
{
    static readonly Regex NsPrefix = new(@"^\{.*\}", RegexOptions.CultureInvariant);

    static string StripNs(XElement el) => NsPrefix.Replace(Et.Tag(el), "", 1);

    /// <summary>Text of a blob in the 24-byte header format (u32 length at 1, data from 24), else the bytes decoded.</summary>
    public static object? BlobText(object? v)
    {
        if (v is not byte[] b)
            return v;
        if (b.Length >= 24 && b[19] == 1)
        {
            var n = Bin.FromLe(b, 1, 5);
            b = Py.Slice(b, 24, 24 + (long)n);
        }
        else if (b.Length <= 24)
            return "";
        return Py.Utf8Sig(b);
    }

    /// <summary>Entries of a VarItems XML; empty trailing lines are skipped.</summary>
    public static List<object?> VarLines(object? xmlText, bool force = false)
    {
        var outl = new List<object?>();
        if (!Py.Bool(xmlText))
            return outl;
        XElement root;
        try
        {
            root = Et.FromString(xmlText as string ?? throw new PyTypeError("a bytes-like object is required"));
        }
        catch (XmlParseError)
        {
            return outl;
        }
        foreach (var vl in Et.Iter(root))
        {
            if (StripNs(vl) != "VarLine")
                continue;
            var ch = new Dictionary<string, XElement>();
            foreach (var c in Et.Kids(vl))
                ch[StripNs(c)] = c;
            if (ch.Count == 0 && !Py.Bool(Et.Get(vl, "Format")))
                continue;
            var uv = ch.GetValueOrDefault("UserValue");
            var fv = ch.GetValueOrDefault("ForceValue");
            var mv = ch.GetValueOrDefault("MonitorValue");
            // force table (sample 078): the force value is the UserValue of the line, its Checked flag = Openness ForceIntention
            if (force)
            {
                outl.Add(PyDict.Of("name", ch.TryGetValue("Name", out var fnm) ? Et.Text(fnm) : null,
                    "address", ch.TryGetValue("Address", out var fad) ? Et.Text(fad) : null,
                    "displayFormat", Et.Get(vl, "Format"), "isComment", Et.Get(vl, "IsCommentLine") == "True",
                    "comment", ch.TryGetValue("Comment", out var fcm) ? Et.Text(fcm) : null,
                    "forceValue", uv is not null ? Et.Text(uv) : fv is not null ? Et.Text(fv) : null,
                    "forceIntention", uv is not null ? Et.Get(uv, "Checked") == "True" : null,
                    "monitorTrigger", mv is not null ? Et.Get(mv, "TriggerName") : null));
                continue;
            }
            outl.Add(PyDict.Of("name", ch.TryGetValue("Name", out var nm) ? Et.Text(nm) : null,
                "address", ch.TryGetValue("Address", out var ad) ? Et.Text(ad) : null,
                "displayFormat", Et.Get(vl, "Format"), "isComment", Et.Get(vl, "IsCommentLine") == "True",
                "comment", ch.TryGetValue("Comment", out var cm) ? Et.Text(cm) : null,
                "modifyValue", uv is not null ? Et.Text(uv) : null, "modifyTrigger", uv is not null ? Et.Get(uv, "TriggerName") : null,
                "forceValue", fv is not null ? Et.Text(fv) : null,
                "monitorTrigger", mv is not null ? Et.Get(mv, "TriggerName") : null));
        }
        return outl;
    }

    public static List<object?> ExtractWatchTables(ProjectModel model)
    {
        var p = model.P;
        var res = new List<PyDict>();
        foreach (var k in Py.Sorted(p.OfType("VarTableData").Union(p.OfType("VarTableForceData"))))
        {
            var entries = new List<object?>();
            foreach (var c in p.Targets(k, "AggregatedVarItemContainer"))
                foreach (var v in p.Targets(c, "AggregatedVarItems"))
                    entries.AddRange(VarLines(BlobText(p.Attr(v, "IVarItemAttributes", "ItemData")), p.TypeName(k) == "VarTableForceData"));
            var tgt = model.Rel1(k, "Target");
            res.Add(PyDict.Of("name", p.Name(k), "kind", p.TypeName(k) == "VarTableForceData" ? "force" : "watch",
                "plc", tgt is not null ? p.Name(tgt) : null, "folder", model.FolderPath(k),
                "comment", model.Comment(k), "entries", entries, "key", k));
        }
        return Py.Sorted(res, x => Py.T(Py.Or(x["plc"], ""), x["kind"], Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    static List<object?> SortedNames(Project p, IEnumerable<string> keys)
    {
        var names = keys.Select(p.Name).ToList();
        if (names.Any(n => n is null) && names.Count > 1)
            throw new PyTypeError("'<' not supported between instances of 'NoneType' and 'str'");
        return Py.Sorted(names, n => n).Cast<object?>().ToList();
    }

    public static List<object?> ExtractUnits(ProjectModel model)
    {
        var p = model.P;
        var res = new List<PyDict>();
        foreach (var k in p.OfType("UnitData"))
        {
            var tgt = model.Rel1(k, "Target");
            res.Add(PyDict.Of("name", p.Name(k), "plc", tgt is not null ? p.Name(tgt) : null,
                "relations", SortedNames(p, p.Targets(k, "References")),
                "referencedBy", SortedNames(p, p.Targets(k, "ReferencedBy")), "key", k));
        }
        return Py.Sorted(res, x => Py.T(Py.Or(x["plc"], ""), Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    public static List<object?> ExtractOpcUaInterfaces(ProjectModel model)
    {
        var p = model.P;
        var res = new List<PyDict>();
        foreach (var k in p.OfType("OpcUaServerConfigurationData"))
        {
            var tgt = model.Rel1(k, "Target");
            var inc = p.Attr(k, "IOpcUaServerConfigurationData", "IncludeForDownload");
            var d = PyDict.Of("name", p.Name(k), "subtype", p.Attr(k, "ICoreAttributes", "Subtype"), "plc", tgt is not null ? p.Name(tgt) : null,
                "enabled", inc is not false, "author", Py.Or(p.Attr(k, "ICoreAttributes", "Author"), null),
                "comment", model.Comment(k));
            // Openness interfaceKind (sample 082): server interface or reference namespace
            d["interfaceKind"] = p.Attr(k, "ICoreAttributes", "Subtype") switch
            {
                "OpcUa.ServerConfiguration" => "server",
                "OpcUa.ReferenceNamespace" => "reference",
                _ => null,
            };
            // the nodeset of the interface (sample 081): expando blob ServerInterfaceFile (varint length, then a compressed
            // blob) = the Openness export of the interface
            if (OpcUaNodeset(p.Attr(k, "IOpcUaExpandoData", "ServerInterfaceFile") as byte[]) is { } xml)
            {
                d["nodeset"] = xml;
                var maps = OpcUaMappings(p.Attr(k, "IOpcUaExpandoData", "ServerInterfaceXmlExtensions") as byte[]);
                d["nodes"] = OpcUaNodes(xml, maps);
            }
            d["key"] = k;
            res.Add(d);
        }
        return Py.Sorted(res, x => Py.T(Py.Or(x["plc"], ""), Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }

    static byte[]? ExpandoBlob(byte[]? raw)
    {
        if (raw is null || raw.Length == 0)
            return null;
        var b = Attrs.DecodeBlob(raw);
        if (b is null)
        {
            var (_, end) = Attrs.Varint(raw, 0);
            b = end < raw.Length ? Attrs.DecodeBlob(raw[end..]) : null;
        }
        return b is { Length: > 0 } ? b : null;
    }

    static string? OpcUaNodeset(byte[]? raw)
    {
        if (ExpandoBlob(raw) is not { } b)
            return null;
        var s = System.Text.Encoding.UTF8.GetString(b);
        return s.Length > 0 && s[0] == '﻿' ? s[1..] : s;
    }

    /// <summary>Variable mappings of a server interface (expando blob ServerInterfaceXmlExtensions, sample 081): u32 1, u32
    /// count, per entry 8 bytes, length-prefixed "namespace URI;i=id", u32, length-prefixed PLC tag ("DB"."member"). The
    /// Openness export writes them as si:VariableMapping extensions of the nodes.</summary>
    static Dictionary<string, string> OpcUaMappings(byte[]? raw)
    {
        var res = new Dictionary<string, string>();
        if (ExpandoBlob(raw) is not { Length: >= 8 } b)
            return res;
        var n = Bin.U32(b, 4);
        long o = 8;
        string Str()
        {
            var (len, end) = Attrs.Varint(b, o);
            var s = System.Text.Encoding.UTF8.GetString(b, end, (int)len);
            o = end + len;
            return s;
        }
        try
        {
            for (var i = 0; i < n && o < b.Length; i++)
            {
                o += 8;
                var node = Str();
                o += 4;
                res[node] = Str();
            }
        }
        catch (ArgumentException)
        {
        }
        return res;
    }

    /// <summary>Nodes of a UANodeSet: class, NodeId, BrowseName, DisplayName, Description, DataType, AccessLevel, parent (the
    /// inverse hierarchical reference).</summary>
    static List<object?> OpcUaNodes(string xml, Dictionary<string, string> maps)
    {
        var res = new List<object?>();
        XElement root;
        try
        {
            root = XElement.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return res;
        }
        var uris = root.Elements().FirstOrDefault(e => e.Name.LocalName == "NamespaceUris")?.Elements().Select(e => e.Value).ToList() ?? [];
        // "ns=1;i=1001" -> "<namespace URI 1>;i=1001", the key of the mappings
        string? MapKey(string? id)
        {
            var m = id is null ? null : Regex.Match(id, @"^ns=(\d+);(.*)$");
            return m is { Success: true } && int.Parse(m.Groups[1].Value) is var ns && ns >= 1 && ns <= uris.Count ? uris[ns - 1] + ";" + m.Groups[2].Value : id;
        }
        foreach (var n in root.Elements().Where(e => e.Name.LocalName.StartsWith("UA", StringComparison.Ordinal) && e.Name.LocalName != "UANodeSet"))
        {
            string? Child(string name) => n.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
            var parent = n.Elements().FirstOrDefault(e => e.Name.LocalName == "References")?.Elements()
                .FirstOrDefault(r => (string?)r.Attribute("IsForward") == "false")?.Value;
            res.Add(PyDict.Of("nodeClass", n.Name.LocalName[2..], "nodeId", (string?)n.Attribute("NodeId"), "browseName", (string?)n.Attribute("BrowseName"),
                "displayName", Child("DisplayName"), "description", Child("Description"), "dataType", (string?)n.Attribute("DataType"),
                "accessLevel", (string?)n.Attribute("AccessLevel"), "parent", parent,
                "mapping", MapKey((string?)n.Attribute("NodeId")) is { } mk ? maps.GetValueOrDefault(mk) : null));
        }
        return res;
    }

    public static List<object?> ExtractSupervisions(ProjectModel model)
    {
        var p = model.P;
        var res = new List<PyDict>();
        foreach (var k in p.OfType("SupervisionContainerData"))
        {
            var fb = model.Rel1(k, "ParentSupervisionFB");
            var texts = p.Targets(k, "FormattedBlockSupervisionSpecificTexts").Select(t => model.CoreTextOf(p.Attr(t, "ICoreTextRepository", "Text"))).ToList();
            XElement? root;
            try
            {
                var raw = p.Attr(k, "ISupervisionContainerData", "Data");
                var b = Attrs.DecodeBlob(raw as byte[] ?? (Py.Bool(raw) ? throw new PyTypeError("blob") : null));
                root = Et.FromString(Py.Utf8SigExact(b ?? throw new PyAttributeError("'NoneType' object has no attribute 'decode'")));
            }
            catch (Exception e) when (PyCatch.Any(e))
            {
                root = null;
            }
            var sups = new List<object?>();
            foreach (var sv in Et.Iter(root))
            {
                var tag = StripNs(sv);
                if (!tag.EndsWith("Supervision", StringComparison.Ordinal) || tag == "GlobalTagSupervisions")
                    continue;
                var g = new Dictionary<string, string>();
                foreach (var c in Et.Kids(sv))
                    g[StripNs(c)] = Py.Strip(Et.Text(c) ?? "");
                if (!g.ContainsKey("Type"))
                    continue;
                long? ti = Py.IsDigit(g.GetValueOrDefault("SpecificTextId", "")) ? Py.ParseInt(g["SpecificTextId"]) : null;
                var txt = new PyDict();
                foreach (var (c, v) in ti is { } t && t < texts.Count ? texts[(int)t] : new PyDict())
                    if (Py.Bool(v) && !Py.Eq(c, "65535"))
                        txt[c] = v;
                sups.Add(PyDict.Of("kind", tag, "type", g.GetValueOrDefault("Type"),
                    "number", Py.IsDigit(g.GetValueOrDefault("Number", "")) ? Py.ParseInt(g["Number"]) : g.GetValueOrDefault("Number"),
                    "name", g.GetValueOrDefault("SupervisionName"), "supervisedStatus", g.GetValueOrDefault("SupervisedStatus"),
                    "category", Py.IsDigit(g.GetValueOrDefault("CategoryNumber", "")) ? Py.ParseInt(g["CategoryNumber"]) : null,
                    "subCategory1", g.GetValueOrDefault("SubCategory1Number"), "subCategory2", g.GetValueOrDefault("SubCategory2Number"),
                    "stateStruct", g.GetValueOrDefault("StateStruct"), "specificText", txt));
            }
            res.Add(PyDict.Of("proDiagFb", fb is not null ? p.Name(fb) : null, "supervisions", sups, "key", k));
        }
        return Py.Sorted(res, x => Py.Or(x["proDiagFb"], "")).Cast<object?>().ToList();
    }

    public static List<object?> ExtractTextLists(ProjectModel model)
    {
        var p = model.P;
        var res = new List<PyDict>();
        foreach (var (t, kind) in new[] { ("UserTextRangeListData", "user"), ("SystemTextRangeListData", "system") })
        {
            foreach (var k in p.OfType(t))
            {
                if (p.TypeName(k) != t)
                    continue;
                var a = p.Sets(k).Set("IObjectRangeListData");
                var tgt = model.Rel1(k, "Target");
                var ents = new List<PyDict>();
                foreach (var e in p.Targets(k, "Entries"))
                {
                    var r = p.Sets(e).Set("IRangeData");
                    var text = new PyDict();
                    foreach (var (c, v) in model.CoreTextOf(r.Get("Text")))
                        if (!Py.Eq(c, "65535") || !Py.Bool(v))
                            if (!Py.Eq(c, "65535"))
                                text[c] = v;
                    ents.Add(PyDict.Of("from", r.Get("RangeFrom"), "to", r.Get("RangeTo"), "text", text));
                }
                ents = Py.Sorted(ents, x => Py.T(x["from"] ?? 0L, x["to"] ?? 0L));
                res.Add(PyDict.Of("name", p.Name(k), "kind", kind, "plc", tgt is not null ? p.Name(tgt) : null, "listRange", a.Get("ListRange"),
                    "index", a.Get("Index"), "comment", model.Comment(k), "entries", ents.Cast<object?>().ToList(), "key", k));
            }
        }
        return Py.Sorted(res, x => Py.T(Py.Or(x["plc"], ""), x["kind"], Py.Or(x["name"], ""))).Cast<object?>().ToList();
    }
}
