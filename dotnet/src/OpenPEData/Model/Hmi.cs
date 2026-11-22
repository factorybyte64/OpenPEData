// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Collections.Frozen;
using System.Net;
using System.Text.RegularExpressions;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>WinCC Unified HMI software of a device (port of hmi.py): connections, tag tables and tags, alarm classes,
/// discrete and analog alarms, screens with items, dynamizations and scripts, text and graphic lists, logs, runtime
/// settings, styles, script modules.</summary>
internal sealed class Hmi
{
    static readonly Regex TagRe = new("<[^>]+>", RegexOptions.CultureInvariant);

    /// <summary>Expando dictionary names of Unified properties (hex of the raw bytes) -> (Openness property, value kind).</summary>
    public static readonly FrozenDictionary<string, (string Name, string? Kind)> PropertyNames = new Dictionary<string, (string Name, string? Kind)>
    {
        ["05"] = ("Width", "int"), ["06"] = ("Height", "int"), ["15"] = ("CenterX", "int"), ["16"] = ("CenterY", "int"),
        ["2d"] = ("AngleRange", "int"), ["2e"] = ("StartAngle", "int"), ["63"] = ("Left", "int"), ["64"] = ("Top", "int"),
        ["66"] = ("BackColor", "color"), ["69"] = ("BorderColor", "color"), ["78"] = ("Radius", "int"),
        ["7c"] = ("RadiusY", "int"), ["7d"] = ("RadiusX", "int"), ["7f"] = ("X2", "int"), ["0202"] = ("Y2", "int"),
        ["0502"] = ("X1", "int"), ["0702"] = ("Y1", "int"),
        ["0102"] = ("EndType", "enum"), ["0602"] = ("StartType", "enum"),
        ["0403"] = ("FlashingColor", "color"), ["2302"] = ("ForeColor", "color"), ["2b21"] = ("LineColor", "color"),
        ["4302"] = ("OutputFormat", "str"), ["4902"] = ("ProcessValue", "str"),
        ["0402"] = ("LineWidth", "int"), ["0e"] = ("TabIndex", "int"), ["0d02"] = ("Points", "points"),
        ["4f02"] = ("Text", "ref"), ["3409"] = ("SelectionItems", "selection"),
        ["08"] = ("Layer", "ref"), ["1f"] = ("CaptionColor", "color"), ["7508"] = ("StyleItemClass", "str"),
        ["7402"] = ("Graphic", "ref"),
        ["050c"] = ("Screen", "ref"),
        ["07"] = ("Visible", "bool"),
        ["0409"] = ("WindowFlags", "flags"),
        ["7502"] = ("BarMode", "barmode"),
        ["0d"] = ("Opacity", "float"),
        ["7a08"] = ("HorizontalTextAlignment", "enum"), ["7908"] = ("VerticalTextAlignment", "enum"),
        ["7f08"] = ("IOFieldType", "enum"),
        ["6e"] = ("BorderWidth", "int"),
        ["0a"] = ("RotationAngle", "int"),
        ["56"] = ("ResourceList", "ref"),
    }.ToFrozenDictionary();

    static readonly Dictionary<long, string> BarModes = new() { [0] = "Segmented", [2] = "SegmentedStatic" };

    static readonly Dictionary<string, (string Name, string? Kind)> DynamicOnlyNames = new()
    {
        ["07"] = ("Visible", null), ["6e"] = ("BorderWidth", null), ["6e24"] = ("IsAlternateState", null),
        ["71"] = ("AlternateBackColor", null), ["050c"] = ("Screen", null), ["7402"] = ("Graphic", null),
    };

    // 0x14: u64 inline (Relevant/Condition bit masks of Singlebit mapping entries, sample F01), as NESTED_INLINE in hmi.py
    static readonly Dictionary<long, int> NestedInline = new() { [0x01] = 1, [0x02] = 1, [0x05] = 4, [0x0C] = 16, [0x13] = 4, [0x14] = 8 };
    static readonly Dictionary<long, string> BackupModes = new() { [0] = "NoBackup", [1] = "PrimaryPath" };
    static readonly Dictionary<long, string> LoggingModes = new() { [0] = "OnChange", [1] = "Cyclic" };
    static readonly Dictionary<long, string> ConditionTypes = new() { [0] = "None", [1] = "Range", [5] = "Singlebit", [6] = "Expression" };

    static byte[] B(string latin1) => Py.Latin1Bytes(latin1);

    /// <summary>CoreText values of HMI texts are XHTML; drop markup and empty cultures.</summary>
    public static PyDict Plain(PyDict texts)
    {
        var o = new PyDict();
        foreach (var (c, t) in texts)
        {
            var s = TagRe.Replace(t as string ?? "", "");
            if (s.Length > 0)
                o[c] = s;
        }
        return o;
    }

    /// <summary>HmiImageData k: file extension, size, the file (GenuineContent, paged blob) as base64.</summary>
    public static PyDict ImageInfo(Project p, string k)
    {
        var ii = p.Targets(k, "IHmiImageAttributes_DefaultInternalImage").FirstOrDefault();
        var a = ii is not null ? p.Sets(ii).Set("IHmiInternalImageAttributes") : new PyDict();
        var size = a.Get("ImageSize");
        object? w = null, h = null;
        if (size is byte[] sb && sb.Length >= 12)
        {
            w = (long)Bin.U32(sb, 4);
            h = (long)Bin.U32(sb, 8);
        }
        var blob = a.Get("GenuineContent");
        var data = blob is byte[] bb ? Attrs.DecodeBlob(bb) is { Length: > 0 } d ? d : null : null;
        return PyDict.Of("name", p.Name(k), "extension", a.Get("FileExtension"), "type", a.Get("ImageType"),
            "width", w, "height", h, "bytes", data is not null ? (long)data.Length : null,
            "data", data is not null ? Convert.ToBase64String(data) : null, "key", k);
    }

    /// <summary>ImageInstanceData k -IsInstanceOf-> ImageLibraryVersionData -VersionObjectToTypeObject-> type: its name.</summary>
    public static string? LibraryImageName(Project p, string k)
    {
        var v = p.Targets(k, "IsInstanceOf").FirstOrDefault();
        var t = v is not null ? p.Targets(v, "VersionObjectToTypeObject").FirstOrDefault() : null;
        return t is not null ? p.Name(t) : null;
    }

    static readonly Dictionary<long, string> Weights = new()
    {
        [100] = "Thin", [200] = "ExtraLight", [300] = "Light", [400] = "Normal", [500] = "Medium", [600] = "SemiBold", [700] = "Bold",
        [800] = "ExtraBold", [900] = "Black",
    };

    static readonly Dictionary<long, string> Orientations = new() { [0] = "Horizontal", [1] = "Vertical" };
    static readonly Dictionary<long, string> ScalingTypes = new() { [0] = "Linear", [1] = "Logarithmic" };
    static readonly Dictionary<long, string> ThresholdModes = new() { [1] = "Upper", [2] = "Lower", [3] = "Normal", [4] = "Minimum", [5] = "Maximum" };

    static readonly (string Key, string Name, (string Raw, string M)[] Members)[] ItemParts =
    [
        ("0x2338", "StraightScale", [("0x2273", "BeginValue"), ("0x2274", "EndValue"), ("0x232d", "ScalingType"), ("0x3869", "Orientation")]),
        ("0x2337", "CurvedScale", [("0x2273", "BeginValue"), ("0x2274", "EndValue"), ("0x232d", "ScalingType")]),
        ("0x094d", "Padding", [("0x0063 Left", "Left"), ("0x0064 Top", "Top"), ("0x094b", "Right"), ("0x094c", "Bottom")]),
        ("0x0002", "Content", [("0x087a HorizontalTextAlignment", "HorizontalTextAlignment")]),
        ("0x0947", "Corners", [("0x0518", "TopLeftRadius"), ("0x0519", "TopRightRadius"), ("0x051a", "BottomRightRadius"), ("0x051b", "BottomLeftRadius")]),
    ];

    static readonly Dictionary<string, Dictionary<long, string>> EnumMembers = new() { ["Orientation"] = Orientations, ["ScalingType"] = ScalingTypes };

    static object? Lookup(Dictionary<long, string> d, object? v) => v is not PyDict and not System.Collections.IList && Py.IsInt(v) && v is not bool && d.TryGetValue(Py.Long(v), out var n) ? n
        : v is bool bv && d.TryGetValue(bv ? 1 : 0, out var nb) ? nb
        : v is double dv && dv == Math.Floor(dv) && Math.Abs(dv) < 1e15 && d.TryGetValue((long)dv, out var nd) ? nd : v;

    /// <summary>Known map-valued parts of a screen item (only members that differ from the default are stored).</summary>
    static PyDict ItemPartsOf(Project p, string k)
    {
        PyDict parts;
        try
        {
            parts = Faceplates.Parts(p, k);
        }
        catch (Exception e) when (PyCatch.Any(e))
        {
            return new PyDict();
        }
        var o = new PyDict();
        foreach (var (key, name, members) in ItemParts)
        {
            if (parts.Get(key) is not PyDict part)
                continue;
            var vals = new PyDict();
            foreach (var (raw, mm) in members)
            {
                if (!part.ContainsKey(raw))
                    continue;
                vals[mm] = EnumMembers.TryGetValue(mm, out var em) ? Lookup(em, part[raw])
                    : mm.EndsWith("TextAlignment", StringComparison.Ordinal) ? Styles.EnumName(mm, part[raw]) : part[raw];
            }
            if (vals.Count > 0)
                o[name] = vals;
        }
        var fpart = parts.FirstOrDefault(kv => kv.Key is string ks && ks.StartsWith("0x021c", StringComparison.Ordinal)).Value;
        var flName = fpart is PyDict fd ? fd.Get("0x054e FontList") : null;
        if (Py.Bool(flName))
        {
            var fl = p.Targets(k, "ExpandoReferences").FirstOrDefault(t => Py.Eq(p.Name(t), flName));
            var item = fl is not null ? p.Targets(fl, "ExpandoReferences").FirstOrDefault() : null;
            if (fl is not null && item is null)
                item = p.Targets(fl).FirstOrDefault(t => (p.TypeName(t) ?? "").EndsWith("HmiFontItemData", StringComparison.Ordinal));
            if (item is not null && Py.Bool(p.Name(item)))
            {
                var font = StyleFonts(PyDict.Of("Description", "f"), PyDict.Of("f", p.Name(item)));
                if (font is PyDict fdd && fdd.ContainsKey("Name"))
                    o["Font"] = font;
            }
        }
        if (parts.Get("0x0915") is List<object?> th)
        {
            o["Thresholds"] = th.OfType<PyDict>().Select(e => (object?)PyDict.Of(
                "Name", e.Get("0x0003"), "ThresholdMode", Lookup(ThresholdModes, e.Get("0x386d")),
                "Color", Py.IsInt(e.Get("0x0871")) ? Fmt08X(e["0x0871"]) : null,
                "Enable", Py.Bool(e.Get("_#ESEnable")))).ToList();
        }
        return o;
    }

    /// <summary>"0x%08X" % v.</summary>
    public static string Fmt08X(object? v)
    {
        var b = Py.Big(v);
        return b < 0 ? "0x-" + Py.Hex(-b, true).PadLeft(7, '0') : "0x" + Py.Hex(b, true).PadLeft(8, '0');
    }

    /// <summary>True for a wrapped expando object whose only member is a 4-character text (the key of a dynamic link).</summary>
    static bool IsLinkObject(byte[] v)
    {
        PyDict o;
        try
        {
            o = Styles.DecodeObject(v, 0);
        }
        catch (Exception e) when (e is PyValueError or PyIndexError or StructError || Attrs.IsDecodeError(e))
        {
            return false;
        }
        return o.Count == 1 && o.Values.First() is string s && Py.CpLen(s) == 4;
    }

    /// <summary>Style font {Description: reference key} -> {Name, Size, Weight} from the font item name.</summary>
    public static object? StyleFonts(object? v, PyDict refs)
    {
        if (v is PyDict d)
        {
            if (d.Count == 1 && d.ContainsKey("Description"))
            {
                var f = (refs.Get(d["Description"]) as string ?? (Py.Bool(refs.Get(d["Description"])) ? Py.Str(refs.Get(d["Description"])) : "")).Split(';');
                if (f.Length >= 6 && Py.IsDigit(f[1]) && Py.IsDigit(f[5]))
                    return PyDict.Of("Name", f[0].Replace(" ", ""), "Size", Py.ParseInt(f[1]),
                        "Weight", Weights.TryGetValue(Py.ParseInt(f[5]), out var wn) ? wn : f[5]);
                return PyDict.Of("FontItem", string.Join(";", f));
            }
            var r = new PyDict();
            foreach (var (k, x) in d)
                r[k] = StyleFonts(x, refs);
            return r;
        }
        return v;
    }

    readonly ProjectModel m;
    readonly Project p;
    List<string>? langs;

    public Hmi(ProjectModel model)
    {
        m = model;
        p = model.P;
    }

    string Short(string k) => Py.AfterLast(p.TypeName(k) ?? "", ".");

    List<string> Of(string app, string shortName) => Py.Sorted(m.Sources(app, "Target").Where(k => Short(k) == shortName));

    static List<object?> L<T>(IEnumerable<T> x) => x.Cast<object?>().ToList();

    public List<object?> Softwares(string device)
    {
        var outl = new List<object?>();
        foreach (var a in Py.Sorted(m.Sources(device, "LogicalContainer")))
        {
            if (Short(a) != "HmiApplicationData")
                continue;
            var di = p.Sets(a).Set("IDeviceItemData");
            langs = AppLanguages(a);
            var d = PyDict.Of("name", p.Name(a), "typeName", di.Get("InvariantTypeName"), "orderNumber", di.Get("OrderNumber"), "firmware", di.Get("FwVersion"));
            d["connections"] = Connections(a);
            d["tagTables"] = TagTables(a);
            d["alarmClasses"] = AlarmClasses(a);
            d["discreteAlarms"] = Alarms(a, "HmiDiscreteAlarmData");
            d["analogAlarms"] = Alarms(a, "HmiAnalogAlarmData");
            var classic = new HmiClassic(m);
            var isClassic = classic.IsClassic(a);
            d["screens"] = isClassic ? classic.Screens(a) : Screens(a);
            d["textLists"] = TextLists(a, isClassic);
            d["cycles"] = Cycles(a);
            d["dataLogs"] = Logs(a, "HmiDataLogData");
            d["alarmLogs"] = Logs(a, "HmiAlarmLogData");
            d["auditTrails"] = Logs(a, "HmiAuditLogData");
            d["runtimeSettings"] = RuntimeSettings(a);
            d["style"] = isClassic ? classic.Style(a) : Style(a);
            // classic: the device type decides runtime details (e.g. the media player placeholder on panels only)
            if (isClassic && d["style"] is PyDict cst)
                cst["deviceType"] = d["typeName"];
            d["runtimeLanguages"] = RuntimeLanguages(a);
            d["scriptModules"] = ScriptModules(a);
            d["graphicLists"] = GraphicLists(a, isClassic);
            if (isClassic)
            {
                d["recipes"] = classic.Recipes(a);
                // classic analog alarms keep their limit in HmiAnalogAlarmLimitData (sample C03b)
                foreach (PyDict al in (List<object?>)d["analogAlarms"]!)
                    if (al.Get("condition") is null && al.Get("key") is string ak && m.Rel1(ak, "HmiAnalogAlarmLimitRelation") is { } lk)
                    {
                        var lim = p.Sets(lk).Set("IHmiAnalogAlarmLimitAttributes");
                        al["condition"] = lim.Get("LimitMode");
                        al["limit"] = lim.Get("LimitValueConstant");
                    }
            }
            d["opcUaAlarmTypes"] = OpcUaAlarmTypes(a);
            d["systemTags"] = L(Py.Sorted(Of(a, "HmiSystemTagData").Select(k => p.Name(k) ?? "")));
            d["counts"] = PyDict.Of("systemTags", (long)Of(a, "HmiSystemTagData").Count,
                "internalSystemTags", (long)Of(a, "HmiInternalSystemTagData").Count, "systemEvents", (long)Of(a, "HmiSystemEventData").Count);
            d["key"] = a;
            outl.Add(d);
        }
        return outl;
    }

    List<string> TextListEntries(string k) => p.Targets(k, "HmiTextListEntryDataChildren") is { Count: > 0 } c ? c
        : p.Links(k).Where(l => l.Relation is null && p.Objects.ContainsKey(l.Key) && Short(l.Key) == "HmiTextListEntryData").Select(l => l.Key).ToList();

    public List<object?> TextLists(string app, bool classicLists = false)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "HmiTextListData"))
        {
            var ad = p.Sets(k).Set("IAdditionalAttributes");
            var entries = new List<object?>();
            foreach (var e in TextListEntries(k))
            {
                var la = p.Sets(e).Set("IHmiListEntryAttributes");
                entries.Add(PyDict.Of("from", la.Get("From"), "to", la.Get("To"), "type", la.Get("EntryType"),
                    "default", la.Get("DefaultEntry"), "text", TextOf(e)));
            }
            var tl = PyDict.Of("name", p.Name(k), "system", ad.Get("IsSystemTextList"), "entries", entries, "key", k);
            // classic WinCC: value list (Decimal), bit list (Bit) or bit number list (Binary), for the symbolic I/O field
            if (classicLists && p.Sets(k).Set("IHmiTextListAttributes").Get("ListRange") is { } lr)
                tl["listRange"] = lr;
            outl.Add(tl);
        }
        return L(Py.Sorted(outl, x => Py.T(Py.Bool(x["system"]), Py.Or(x["name"], ""))));
    }

    public List<object?> OpcUaAlarmTypes(string app)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "ILOpcUaAlarmTypeData"))
        {
            var a = p.Sets(k).Set("IOpcUaAlarmTypeAttributeSet");
            var ac = m.Rel1(k, "ILOpcUaAlarmTypeData_HmiAlarmClassData");
            var cn = m.Rel1(k, "ILOpcUaAlarmTypeData_HmiConnectionData");
            outl.Add(PyDict.Of("name", p.Name(k), "nodeId", a.Get("NotifyNodeId"), "conditionTypeId", a.Get("ConditionTypeId"),
                "area", a.Get("Area"), "alarmClass", ac is not null ? p.Name(ac) : null, "connection", cn is not null ? p.Name(cn) : null, "key", k));
        }
        return L(Py.Sorted(outl, x => Py.Or(x["name"], "")));
    }

    public List<object?> PlantViews()
    {
        var outl = new List<object?>();
        void Walk(string k, string path, List<object?> nodes)
        {
            foreach (var c in p.Targets(k, "CpmHierarchyNodeChildren"))
            {
                var cp = (path.Length > 0 ? path + "/" : "") + (p.Name(c) ?? "");
                var node = PyDict.Of("name", p.Name(c), "path", cp, "subtype", p.Attr(c, "ICoreAttributes", "Subtype"));
                // a plant object (sample 170): LinkedCpmObject -> CpmObjectData -CpmType-> CpmTypeData, interface tag LinkedStructureTag
                if (p.Targets(c, "LinkedCpmObject").FirstOrDefault() is { } obj)
                {
                    var t = p.Targets(obj, "CpmType").FirstOrDefault();
                    var tag = p.Targets(obj, "LinkedStructureTag").FirstOrDefault();
                    node["plantObjectType"] = t is not null ? p.Name(t) : null;
                    node["interfaceTag"] = tag is not null ? p.Name(tag) : null;
                }
                node["key"] = c;
                nodes.Add(node);
                Walk(c, cp, nodes);
            }
        }
        foreach (var cpm in p.OfType("CpmData"))
            foreach (var r in p.Targets(cpm, "CpmHierarchyRootChildren"))
            {
                var ha = p.Sets(r).Set("ICpmHierarchyDataAttributes");
                var nodes = new List<object?>();
                Walk(r, "", nodes);
                outl.Add(PyDict.Of("name", p.Name(r), "assignedHmiDevice", Py.Or(ha.Get("AssignedHmiDeviceID"), ""), "nodes", nodes, "key", r));
            }
        return outl;
    }

    /// <summary>Plant object types (CpmTypeData, sample 170): name, the HMI structure type (LinkedCpmUdt -> CpmUdtData in the tag
    /// table "Plant Object Types" of the hidden HMI application PlantModel) and the names of their plant objects.</summary>
    public List<object?> PlantObjectTypes()
    {
        var outl = new List<PyDict>();
        foreach (var k in p.OfType("CpmTypeData"))
        {
            var udt = p.Targets(k, "LinkedCpmUdt").FirstOrDefault();
            outl.Add(PyDict.Of("name", p.Name(k), "structureType", udt is not null ? p.Name(udt) : null,
                "members", udt is not null ? p.Attr(udt, "IStructureItem", "MemberCount") : null,
                "objects", L(Py.Sorted(p.Targets(k, "CpmObjects").Select(o => (object?)p.Name(o)), x => Py.Or(x, ""))), "key", k));
        }
        return L(Py.Sorted(outl, x => Py.Or(x["name"], "")));
    }

    public List<object?> GraphicLists(string app, bool classicLists = false)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "HmiGraphicListData"))
        {
            // entries (sample 168): HmiGraphicListEntryData linked from the list, range in IHmiListEntryAttributes, the graphic as
            // a link to a project graphic (HmiImageData; YAML export "GraphicLibrary.<name>")
            var entries = new List<PyDict>();
            foreach (var e in p.Links(k).Where(l => p.Objects.ContainsKey(l.Key) && Short(l.Key) == "HmiGraphicListEntryData").Select(l => l.Key).Distinct())
            {
                var la = p.Sets(e).Set("IHmiListEntryAttributes");
                var img = p.Links(e).Where(l => p.Objects.ContainsKey(l.Key) && Short(l.Key) is "HmiImageData" or "HmiInternalImageData").Select(l => l.Key).FirstOrDefault();
                entries.Add(PyDict.Of("from", la.Get("From"), "to", la.Get("To"), "type", la.Get("EntryType"), "default", la.Get("DefaultEntry"),
                    "graphic", img is not null ? p.Name(img) : null, "key", e));
            }
            var gl = PyDict.Of("name", p.Name(k), "mode", p.Sets(k).Set("IHmiGraphicListAttributes").Get("Mode"),
                "entries", L(Py.Sorted(entries, x => Py.T(x["from"], x["to"]))), "key", k);
            // classic graphic lists keep the list kind (Decimal, Bit, Binary) in IHmiTextListAttributes (sample C05)
            if (classicLists && p.Sets(k).Set("IHmiTextListAttributes").Get("ListRange") is { } lr)
                gl["listRange"] = lr;
            outl.Add(gl);
        }
        return L(Py.Sorted(outl, x => Py.Or(x["name"], "")));
    }

    public List<object?> RuntimeLanguages(string app)
    {
        var ls = Of(app, "HmiLanguageAndFontSettingsData").FirstOrDefault();
        var rs = Of(app, "HmiRuntimeSettingsData").FirstOrDefault();
        var order0 = (ls is not null ? p.Sets(ls).Set("IHmiLanguageAndFontSettingsAttributes").Get("OnlineLanguageOrder") : null);
        var order = (Py.Bool(order0) ? (string)order0! : "").Split(';').Where(c => c.Length > 0).ToList();
        var blob = rs is not null ? p.Sets(rs).Set("IAdditionalAttributes").Get("LoggingLanguages") : null;
        var logging = new HashSet<long>();
        if (blob is byte[] b && b.Length >= 8)
        {
            var cnt = (long)Bin.FromLe(b, 4, 8);
            for (var i = 0; i < cnt; i++)
                if (12 + 4 * i <= b.Length)
                    logging.Add((long)Bin.FromLe(b, 8 + 4 * i, 12 + 4 * i));
        }
        var proj = p.OfType("ProjectData").FirstOrDefault();
        var outl = new List<object?>();
        foreach (var c in proj is not null ? p.Targets(proj, "UsedCultures") : [])
        {
            var name = m.Culture(c);
            var lcid = Py.AfterLast(p.Name(c) ?? "", "(").TrimEnd(')');
            outl.Add(PyDict.Of("language", name, "order", order.Contains(name) ? (long)order.IndexOf(name) : null,
                "enable", order.Contains(name), "enableForLogging", Py.IsDigit(lcid) && logging.Contains(Py.ParseInt(lcid))));
        }
        if (outl.Count == 0)
            outl = order.Select((c, i) => (object?)PyDict.Of("language", c, "order", (long)i, "enable", true, "enableForLogging", null)).ToList();
        return outl;
    }

    public PyDict? Style(string app)
    {
        foreach (var (k, _, sel, _) in Styles.Settings(p))
        {
            if (!p.Targets(k, "Target").Contains(app) || sel is null)
                continue;
            var props = p.Sets(sel).Set("IPropertyExpandoAttributes");
            var refs = new PyDict();
            var links = p.Targets(sel, "ExpandoReferences");
            foreach (var (key, v) in p.Sets(sel).Set("IReferenceExpandoAttributesSimple"))
            {
                var i = (long)Bin.FromBe(Py.Bytes(v) ?? throw new PyTypeError("'NoneType' object is not subscriptable"), 0, 4);
                var fl = i < links.Count ? links[(int)i] : null;
                var item = fl is not null ? p.Targets(fl, "ExpandoReferences").FirstOrDefault() : null;
                refs[key] = item is not null ? p.Name(item) : fl is not null ? p.Name(fl) : null;
            }
            var values = new PyDict();
            var info = new PyDict();
            foreach (var (t, v) in props)
            {
                if (Py.Eq(t, "SystemStyleName"))
                    continue;
                if (v is not byte[])
                {
                    info[t] = v;
                    continue;
                }
                values[t] = StyleFonts(Styles.EnumNames(Styles.StripMeta(Styles.DecodeStyleValue(v))), refs);
            }
            var tpl = new PyDict();
            foreach (var (n, svg) in Styles.Templates(p, sel))
                tpl[n] = Py.Utf8Sig(Py.Bool(svg) ? Py.Bytes(svg) ?? throw new PyAttributeError("'str' object has no attribute 'decode'") : []);
            var o = PyDict.Of("name", p.Name(sel), "systemStyle", props.Get("SystemStyleName"), "values", values, "templates", tpl);
            if (info.Count > 0)
                o["info"] = info;
            return o;
        }
        return null;
    }

    public PyDict RuntimeSettings(string app)
    {
        var sections = new PyDict();
        foreach (var k in Of(app, "HmiILSettingsData"))
        {
            var name = p.Name(k);
            if (name == "DomainFunctionConfigurations")
                continue;
            var ex = p.Sets(k).Set("IHmiILSettingsExpandoAttributes");
            var vals = new PyDict();
            foreach (var (n, v) in Py.Sorted(ex.Items, kv => Py.T(kv.Key, kv.Value)))
                if (!((string)n!).StartsWith('#') && v is not byte[])
                    vals[n] = v;
            if (vals.Count > 0)
                sections[name] = vals;
        }
        return PyDict.Of("gmpEnabled", p.Sets(app).Set("IHmiApplicationAttributes").Get("IsRegulatedApplication"), "sections", sections,
            "startScreen", StartScreen(app));
    }

    /// <summary>Start screen (sample 169): expando link IlStartScreen of HmiGraphXSettingsData; u16 at byte 4 of the value is
    /// the index into the expando links of the object (a link to a screen, unused entries point to 0000:0000:0).</summary>
    string? StartScreen(string app)
    {
        foreach (var k in Of(app, "HmiGraphXSettingsData"))
        {
            if (p.Attr(k, "ICoreExpandoLinkAttributes", "IlStartScreen") is not byte[] { Length: >= 6 } v)
                continue;
            var idx = v[4] | v[5] << 8;
            var links = p.Links(k).ToList();
            var rel = links.FirstOrDefault(l => p.Objects.ContainsKey(l.Key) && Short(l.Key) == "ILScreenData")?.Relation;
            if (rel is null)
                return null;
            var same = links.Where(l => l.Relation == rel).ToList();
            return idx < same.Count && p.Objects.ContainsKey(same[idx].Key) ? p.Name(same[idx].Key) : null;
        }
        return null;
    }

    /// <summary>Time period in 100 ns ticks -> {days, hours, minutes, seconds}.</summary>
    static PyDict? Duration(object? ticks)
    {
        if (!Py.IsInt(ticks))
            return null;
        var sec = Py.FloorDiv(Py.Long(ticks), 10_000_000);
        return PyDict.Of("days", Py.FloorDiv(sec, 86400), "hours", Py.FloorDiv(Py.Mod(sec, 86400), 3600),
            "minutes", Py.FloorDiv(Py.Mod(sec, 3600), 60), "seconds", Py.Mod(sec, 60));
    }

    public List<object?> Logs(string app, string shortName)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, shortName))
        {
            var la = p.Sets(k).Set("IHmiLogAttributes");
            var bs = m.Rel1(k, "IHmiLogAttributes_BackupStrategy");
            var ba = bs is not null ? p.Sets(bs).Set("IHmiBackupStrategyAttributes") : new PyDict();
            var st = la.Get("SegmentStartTime");
            var bm = ba.Get("BackupMode");
            outl.Add(PyDict.Of("name", p.Name(k), "logMaxSize", la.Get("LogMaxSize"), "logTimePeriod", Duration(la.Get("LogTimePeriod")),
                "storagePath", Py.Or(la.Get("StoragePath"), null), "storageDevice", la.Get("StorageDevice"),
                "segmentMaxSize", la.Get("SegmentMaxSize"), "segmentTimePeriod", Duration(la.Get("SegmentTimePeriod")),
                "segmentStartTime", st is string ss ? Py.Slice(ss, 0, 19) : st,
                "backupMode", Lookup(BackupModes, bm),
                "backupPrimaryPath", Py.Or(ba.Get("PrimaryPath"), null), "backupSecondaryPath", Py.Or(ba.Get("SecondaryPath"), null), "key", k));
        }
        return L(Py.Sorted(outl, x => Py.Or(x["name"], "")));
    }

    public List<object?> LoggingTags(string k)
    {
        var a = p.Sets(k);
        var ex = a.Set("IHmiLoggingTagExpandoAttributes");
        var la = a.Set("IHmiLoggingTagExpandoLinkAttributes");
        var refs = p.Links(k, "ExpandoReferences").Select(l => l.Key).ToList();
        object? Ref(object? v)
        {
            if (v is byte[] b && b.Length >= 4)
            {
                var i = (long)Bin.FromBe(b, 0, 4);
                return i < refs.Count ? p.Name(refs[(int)i]) : null;
            }
            return null;
        }
        var outl = new List<object?>();
        foreach (var (slotv, blobv) in Py.Sorted(ex.Items, kv => Py.T(kv.Key, kv.Value)))
        {
            var slot = (string)slotv!;
            if (slot.StartsWith('#') || blobv is not byte[] blob)
                continue;
            var f = new Dictionary<string, (long C, byte[] V)>();
            foreach (var (nm, c, v) in Nested(blob))
                f[Py.Utf8(nm)] = (c, v);
            object? Num(string name) => f.TryGetValue(name, out var cv) && cv.V.Length == 4 ? (long)Bin.I32(cv.V, 0) : null;
            var name = f.TryGetValue("Name", out var nv) ? nv.V : [];
            var mode = Num("LoggingMode");
            outl.Add(PyDict.Of("name", name.Length > 0 ? Py.Utf8(name[1..]) : null,
                "loggingMode", LoggingModes.TryGetValue(mode is long ml && ml != 0 ? ml : 0, out var lm) ? lm : mode,
                "cycleFactor", Num("LoggingCycleFactor"),
                "dataLog", Ref(la.Get(slot + "#LogConfiguration")), "cycle", Ref(la.Get(slot + "#LoggingCycle"))));
        }
        return outl;
    }

    public List<object?> Cycles(string app)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "HmiCycleData"))
        {
            var ca = p.Sets(k).Set("IHmiCycleAttributes");
            outl.Add(PyDict.Of("name", p.Name(k), "time", ca.Get("CycleTime"), "unit", ca.Get("CycleUnit"), "number", ca.Get("Number"), "key", k));
        }
        return L(Py.Sorted(outl, x => x["number"] ?? (object)(1L << 30)));
    }

    public List<object?> Connections(string app)
    {
        var outl = new List<object?>();
        foreach (var c in Of(app, "HmiConnectionData"))
        {
            var ca = p.Sets(c).Set("IHmiConnectionAttributes");
            outl.Add(PyDict.Of("name", p.Name(c), "kind", p.Attr(c, "ICoreAttributes", "Subtype"), "plcId", ca.Get("PlcId"), "key", c));
        }
        return outl;
    }

    /// <summary>(PLC tag name, PLC name) linked through a ScopedLinkMaintainerData (Consumer -> HMI tag).</summary>
    (string? Tag, string? Plc) PlcTag(string tag)
    {
        foreach (var slm in m.Sources(tag, "Consumer"))
        {
            var prov = m.Rel1(slm, "Provider");
            if (prov is not null)
            {
                string? plc = null;
                var table = m.Rel1(prov, "TagTable");
                var ctl = table is not null ? m.Rel1(table, "Target") : null;
                if (ctl is not null)
                    plc = p.Name(ctl);
                return (p.Name(prov), plc);
            }
        }
        return (null, null);
    }

    public PyDict Tag(string k)
    {
        var a = p.Sets(k);
        var conn = m.Rel1(k, "IHmiTagAttributes_Connection");
        var cyc = m.Rel1(k, "IHmiTagAttributes_AcquisitionCycle");
        var (plcTag, plc) = PlcTag(k);
        var d = PyDict.Of("name", p.Name(k), "dataType", a.Set("IStructureItem").Get("DisplayTypeName"),
            "connection", conn is not null ? p.Name(conn) : null, "plcName", plc, "plcTag", plcTag,
            "address", Py.Or(a.Set("ITagAddress").Get("LogicalAddress"), null),
            "acquisitionCycle", cyc is not null ? p.Name(cyc) : null,
            "persistent", a.Set("IHmiTagAttributes").Get("Persistency"));
        d["loggingTags"] = LoggingTags(k);
        d["comment"] = Plain(m.Comment(k));
        d["key"] = k;
        return d;
    }

    public List<object?> TagTables(string app)
    {
        var outl = new List<PyDict>();
        foreach (var t in Of(app, "HmiTagTableData"))
        {
            var tags = Py.Sorted(p.Targets(t, "Content").Where(k => Short(k) == "HmiTagData").Select(Tag), x => Py.Or(x["name"], ""));
            outl.Add(PyDict.Of("name", p.Name(t), "group", m.FolderPath(t), "tags", L(tags), "key", t));
        }
        return L(Py.Sorted(outl, x => Py.T(x["group"], Py.Or(x["name"], ""))));
    }

    public List<object?> AlarmClasses(string app)
    {
        var outl = new List<PyDict>();
        foreach (var c in Of(app, "HmiAlarmClassData"))
        {
            var pe = m.Rel1(c, "HmiOnlyAlarmClassPeAlarmClassRelation");
            var cid = pe is not null ? p.Attr(pe, "IAlarmClassData", "Id") : null;
            cid ??= p.Attr(c, "IHmiAlarmClassAttributes", "Id_ForInstanceFilter");
            outl.Add(PyDict.Of("name", p.Name(c), "id", cid, "key", c));
        }
        return L(Py.Sorted(outl, x => Py.T(x["id"] ?? (object)(1L << 30), Py.Or(x["name"], ""))));
    }

    PyDict TextOf(string alarm)
    {
        var res = new PyDict();
        foreach (var t in p.Targets(alarm, "ExpandoChildren"))
            if (Short(t) == "HmiTextData")
                res.Update(Plain(m.CoreTextOf(p.Attr(t, "ICoreTextRepository", "Text"))));
        return res;
    }

    public List<object?> Alarms(string app, string shortName)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, shortName))
        {
            var a = p.Sets(k);
            var cls = m.Rel1(k, "HmiAlarmClassDataRelation");
            var kind = shortName == "HmiDiscreteAlarmData" ? "IHmiDiscreteAlarmAttributes" : "IHmiAnalogAlarmAttributes";
            var trig = m.Rel1(k, kind + "_TriggerTag");
            var e = PyDict.Of("name", Core(k).Get("Name"), "id", a.Set("IAlarmData").Get("AlarmId"),
                "alarmClass", cls is not null ? p.Name(cls) : null, "triggerTag", trig is not null ? p.Name(trig) : null,
                "text", TextOf(k), "key", k);
            if (shortName == "HmiDiscreteAlarmData")
            {
                var d = a.Set("IHmiDiscreteAlarmAttributes");
                e["triggerBit"] = d.Get("TriggerBitNumber");
                e["triggerMode"] = d.Get("TriggerMode");
            }
            else
            {
                var lim = a.Set("IHmiAnalogAlarmLimitAttributes");
                e["condition"] = lim.Get("LimitMode");
                e["limit"] = lim.Get("LimitValueConstant");
            }
            outl.Add(e);
        }
        return L(Py.Sorted(outl, x => Py.T(x["id"] ?? (object)(1L << 30), Py.Or(x["name"], ""))));
    }

    public PyDict Core(string k) => p.Sets(k).Set("ICoreAttributes");

    /// <summary>Non-default properties of a screen / item from IPropertyExpandoAttributes: entries whose dictionary name is a
    /// property id (PropertyNames).</summary>
    public PyDict ScreenProperties(string k)
    {
        var o = p.Objects[k];
        var i = o.SetNames.FindIndex(s => s.EndsWith(".IPropertyExpandoAttributes", StringComparison.Ordinal));
        if (i < 0 || !o.Blocks.TryGetValue(i, out var be))
            return new PyDict();
        var b = Py.Slice(o.Data, be.Start, be.End);
        if (b.Length < 18)
            return new PyDict();
        var used = Bin.U32(b, 0);
        var cnt0 = Bin.U32(b, 8);
        var tid = Bin.U32(b, 14);
        if (18 + 8L * cnt0 > used)
            return new PyDict();
        var ids = Bin.U32s(b, 18, cnt0);
        var offs0 = Bin.U32s(b, 18 + 4 * cnt0, cnt0);
        var bas = 18 + 8L * cnt0;
        var names = p.ExpandoDicts.GetValueOrDefault(tid) ?? [];
        var outd = new PyDict();
        for (var j = 0; j < cnt0; j++)
        {
            var (eid, off) = (ids[j], offs0[j]);
            var raw = Py.HexStr(Py.Utf8Bytes(names.TryGetValue(eid, out var nc) ? nc.Name : ""));
            if (!PropertyNames.TryGetValue(raw, out var prop))
                continue;
            var p0 = bas + off;
            // a static numeric ProcessValue (bar, gauge, slider) is stored inline: type code 5, the offset word is
            // the Int32 value (Openness ProcessValue 30 -> 0x1E, 55 -> 0x37; z15 Bar_T/Gauge_T 2026-11-08)
            if (prop.Name == "ProcessValue" && nc.Code == 5)
            {
                outd[prop.Name] = (long)Bin.AsI32(off);
                continue;
            }
            try
            {
                switch (prop.Kind)
                {
                    case "int":
                        outd[prop.Name] = (long)Bin.AsI32(off);
                        break;
                    case "bool":
                        outd[prop.Name] = off != 0;
                        break;
                    case "float":
                        outd[prop.Name] = Py.Round(Bin.AsF32(off), 6);
                        break;
                    case "enum":
                    case "flags":
                        outd[prop.Name] = Styles.EnumName(prop.Name, (long)off);
                        break;
                    case "barmode":
                        outd[prop.Name] = BarModes.TryGetValue(off, out var bm) ? bm : (long)off;
                        break;
                    case "color":
                    {
                        var (n, q) = Attrs.Varint(b, p0);
                        if (n == 8)
                            outd[prop.Name] = Fmt08X((long)Bin.U32(b, q + 3));
                        break;
                    }
                    case "points":
                    {
                        Bin.U32(b, p0);
                        var cnt = Bin.U32(b, p0 + 4);
                        var offs = Bin.U32s(b, p0 + 8, cnt);
                        outd[prop.Name] = offs.Select(oo => (object?)PyDict.Of("X", (long)Bin.I32(b, p0 + oo + 4), "Y", (long)Bin.I32(b, p0 + oo + 8))).ToList();
                        break;
                    }
                    case "ref":
                    {
                        var (n, q) = Attrs.Varint(b, p0);
                        outd[prop.Name] = Py.T("ref", Py.Slice(b, q, q + n - 1));
                        break;
                    }
                    case "selection":
                    {
                        var total = Bin.U32(b, p0);
                        var cnt = Bin.U32(b, p0 + 4);
                        var offs = Bin.U32s(b, p0 + 8, cnt);
                        var refs = new List<object?>();
                        for (var jj = 0; jj < offs.Length; jj++)
                        {
                            var end = p0 + (jj + 1 < cnt ? offs[jj + 1] : total);
                            var seg = Py.Slice(b, p0 + offs[jj], end);
                            var g = Py.Find(seg, "_Guid"u8.ToArray());
                            var q = g >= 0 ? Py.Find(seg, [0x18, 0, 0, 0], g) : -1;
                            if (q >= 0 && Py.Slice(seg, q + 20, q + 21) is [0x05])
                                refs.Add(Py.T("ref", Py.Slice(seg, q + 21, q + 25)));
                        }
                        outd[prop.Name] = refs;
                        break;
                    }
                    default:
                    {
                        var (v, _) = Attrs.ReadVar(b, p0);
                        if (v is { Length: > 0 } && v[0] == 0 && prop.Name == "ProcessValue" && IsLinkObject(v))
                            continue;
                        var text = v is { Length: > 0 } ? Py.Utf8(v) : "";
                        if (prop.Name == "ProcessValue" && v is { Length: > 0 } && Py.Contains(v, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0x10, 1, 0, 0, 0]))
                            continue;
                        if (prop.Name == "ProcessValue" && text.Any(c => c < ' ' || c == '\xfffd'))
                        {
                            outd["ProcessValueRaw"] = Py.HexStr(v!);
                            continue;
                        }
                        outd[prop.Name] = text;
                        break;
                    }
                }
            }
            catch (Exception e) when (Attrs.IsDecodeError(e))
            {
            }
        }
        return outd;
    }

    /// <summary>Nested expando value: [(name bytes, code, value bytes)]; [] if it does not decode.</summary>
    public static List<(byte[] Name, long Code, byte[] Value)> Nested(byte[]? blob)
    {
        try
        {
            blob ??= [];
            long p = 0;
            if (!(blob.Length > 0 && blob[0] == 0))
                p = Attrs.Varint(blob, 0).End;
            p += 1 + 4 + 9 + 10 + 4;
            var cnt = Bin.U32(blob, p);
            Bin.U32(blob, p + 4);
            p += 8;
            var codes = Bin.U32s(blob, p, cnt);
            p += 4 * cnt;
            var names = new List<byte[]>();
            for (var i = 0; i < cnt; i++)
            {
                var (n, q) = Attrs.Varint(blob, p);
                p = q;
                names.Add(Py.Slice(blob, p, p + n - 1));
                p += n - 1;
            }
            var bas = p;
            var q2 = p;
            var fields = new List<(long C, bool Inline, byte[]? Raw, long Off)>();
            foreach (var code in codes)
            {
                long c = code & 0x7FFFFFFF;
                if (NestedInline.TryGetValue(c, out var size) && size != 0)
                {
                    fields.Add((c, true, Py.Slice(blob, q2, q2 + size), 0));
                    q2 += size;
                }
                else
                {
                    fields.Add((c, false, null, Bin.U32(blob, q2)));
                    q2 += 4;
                }
            }
            var starts = fields.Where(f => !f.Inline).Select(f => f.Off).OrderBy(x => x).ToList();
            var outl = new List<(byte[], long, byte[])>();
            for (var i = 0; i < Math.Min(names.Count, fields.Count); i++)
            {
                var f = fields[i];
                if (f.Inline)
                    outl.Add((names[i], f.C, f.Raw!));
                else
                {
                    var nxt = starts.Where(s => s > f.Off).ToList();
                    outl.Add((names[i], f.C, Py.Slice(blob, bas + f.Off, nxt.Count > 0 ? bas + nxt[0] : blob.Length)));
                }
            }
            return outl;
        }
        catch (Exception e) when (Attrs.IsDecodeError(e))
        {
            return [];
        }
    }

    static PyDict NestedDict(byte[]? blob)
    {
        var d = new PyDict();
        foreach (var (n, _, v) in Nested(blob))
            d[n] = v;
        return d;
    }

    static readonly byte[] KValueConverter = "ValueConverter"u8.ToArray(), KMappingTable = "MappingTable"u8.ToArray(),
        KConditionType = "ConditionType"u8.ToArray(), KEntries = "Entries"u8.ToArray(), KValue = "Value"u8.ToArray(),
        KCondition = "Condition"u8.ToArray(), KFrom = "From"u8.ToArray(), KTo = "To"u8.ToArray(), KRelevant = "Relevant"u8.ToArray();

    /// <summary>Mapping table of a tag dynamization: ValueConverter -> MappingTable -> Entries, ConditionType.</summary>
    public static PyDict? Mapping(byte[] entry)
    {
        var f = NestedDict(entry);
        var vc = Py.Bool(f.Get(KValueConverter)) ? NestedDict((byte[])f[KValueConverter]!) : new PyDict();
        var mt = Py.Bool(vc.Get(KMappingTable)) ? NestedDict((byte[])vc[KMappingTable]!) : new PyDict();
        if (mt.Count == 0)
            return null;
        var ct = (byte[])mt.Get(KConditionType, new byte[4])!;
        if (ct.Length != 4)
            throw new StructError("unpack requires a buffer of 4 bytes");
        var res = PyDict.Of("conditionType", ConditionTypes.GetValueOrDefault(Bin.I32(ct, 0)), "entries", new List<object?>());
        var entries = (List<object?>)res["entries"]!;
        var arr = (byte[])mt.Get(KEntries, Array.Empty<byte>())!;
        if (arr.Length >= 8)
        {
            var cnt = Bin.U32(arr, 4);
            var offs = Bin.U32s(arr, 8, cnt);
            for (var j = 0; j < offs.Length; j++)
            {
                var el = NestedDict(Py.Slice(arr, offs[j], j + 1 < cnt ? offs[j + 1] : arr.Length));
                var raw = (byte[])el.Get(KValue, Array.Empty<byte>())!;
                object? val;
                if (raw.Length == 8 && raw[0] == 8)
                    val = Fmt08X((long)Bin.U32(raw, 4));
                else if (raw.Length == 4)
                    val = (long)Bin.I32(raw, 0);
                else if (raw.Length == 1)
                    val = (long)raw[0];
                else
                    val = Py.HexStr(raw);
                var cond = (byte[])el.Get(KCondition, Array.Empty<byte>())!;
                var (cn, cp) = cond.Length > 0 ? Attrs.Varint(cond, 0) : (0, 0);
                if (cn != 0 && cn == cond.Length)
                    entries.Add(PyDict.Of("condition", Py.Latin1(cond[cp..]), "value", val));
                else if (el.ContainsKey(KFrom))
                {
                    object? Bound(byte[] v) => v.Length == 4 ? (long)Bin.I32(v, 0) : null;
                    entries.Add(PyDict.Of("from", Bound((byte[])el[KFrom]!), "to", Bound((byte[])el.Get(KTo, new byte[4])!), "value", val));
                }
                else
                {
                    // Singlebit: BitmaskType, Value, Relevant/Condition bit masks (u64, code 0x14 inline); other sizes = None
                    object? Mask(byte[] v) => v.Length == 8 ? (Bin.U64(v, 0) is var u && u <= long.MaxValue ? (object)(long)u : u) : null;
                    entries.Add(PyDict.Of("value", val, "relevant", Mask((byte[])el.Get(KRelevant, Array.Empty<byte>())!),
                        "condition", Mask(cond)));
                }
            }
        }
        return res;
    }

    static string NestedStr(byte[]? v)
    {
        var s = v is { Length: > 0 } ? Attrs.ReadVar(v, 0).Value : null;
        return s is { Length: > 0 } ? Py.Utf8(s) : "";
    }

    static readonly Regex TagRef = new(@"\$(.{4})\$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    static PyDict FieldsOf(byte[] entry)
    {
        var d = new PyDict();
        foreach (var (f, _, v) in Nested(entry))
            d[Py.Utf8(f)] = v;
        return d;
    }

    static byte[] FieldBytes(PyDict fields, string name) => fields.Get(name) as byte[] ?? [];

    /// <summary>Dynamized properties of an item: IPropertyExpandoAttributes._#Dynamics maps property ids to keys of
    /// IPropertyDynamicExpandoAttributes entries (Type Tag, Script, ResourceList, Expression, Flashing, FaceplateInterface).</summary>
    public List<object?> Dynamizations(string k, List<PyDict> scripts)
    {
        var a = p.Sets(k);
        var raw = a.Set("IPropertyExpandoAttributes").Get("_#Dynamics");
        if (raw is not byte[] rawb)
            return [];
        var dyn = a.Set("IPropertyDynamicExpandoAttributes");
        var link = a.Set("IPropertyDynamicLinkExpandoAttributes");
        var refs = p.Links(k, "ExpandoReferences").Select(l => l.Key).ToList();

        string? Target(byte[] v)
        {
            var (n, q) = v.Length > 0 ? Attrs.Varint(v, 0) : (0, 0);
            var lv = n != 0 ? link.Get(Py.Utf8(Py.Slice(v, q, q + n - 1))) : null;
            if (lv is byte[] lb && lb.Length >= 4)
            {
                var idx = (long)Bin.FromBe(lb, 0, 4);
                if (lb.Length >= 5 && (lb[4] & 0x80) != 0)
                {
                    var children = p.Links(k, "ExpandoChildren").Select(l => l.Key).ToList();
                    if (idx < children.Count && Short(children[(int)idx]) == "TextListInstanceData")
                        return children[(int)idx];
                }
                if (idx < refs.Count && p.Objects.ContainsKey(refs[(int)idx]))
                    return refs[(int)idx];
            }
            return null;
        }

        string? Name(byte[] v)
        {
            var t = Target(v);
            var parts = new List<string?>();
            if (t is not null && Short(t) == "TextListInstanceData")
            {
                var ver = m.Rel1(t, "IsInstanceOf");
                var typ = ver is not null ? m.Rel1(ver, "VersionObjectToTypeObject") : null;
                return typ is not null ? p.Name(typ) : null;
            }
            while (t is not null && Short(t) == "HmiStructureMemberTagData")
            {
                parts.Add(p.Name(t));
                t = m.Rel1(t, "MembersInverse");
            }
            if (t is not null)
                parts.Add(p.Name(t));
            parts.Reverse();
            var s = string.Join(".", parts.Select(x => x ?? throw new PyTypeError("sequence item: expected str instance, NoneType found")));
            return s.Length > 0 ? s : null;
        }

        var outl = new List<object?>();
        foreach (var (nm, _, val) in Nested(rawb))
        {
            var hex = Py.HexStr(nm);
            var prop = PropertyNames.TryGetValue(hex, out var pn) ? pn.Name : DynamicOnlyNames.TryGetValue(hex, out var dn) ? dn.Name : "#" + hex;
            var (n, q) = val.Length > 0 ? Attrs.Varint(val, 0) : (0, 0);
            var entry = n != 0 ? dyn.Get(Py.Utf8(Py.Slice(val, q, q + n - 1))) : null;
            var fields = entry is byte[] eb ? FieldsOf(eb) : new PyDict();
            var kind = NestedStr(FieldBytes(fields, "Type"));
            var d = PyDict.Of("property", prop, "type", kind.Length > 0 ? kind : null);
            if (kind == "Tag")
            {
                d["tag"] = Name(FieldBytes(fields, "Tag"));
                d["mapping"] = entry is byte[] e1 ? Mapping(e1) : null;
            }
            else if (kind == "ResourceList")
            {
                d["resourceList"] = Name(FieldBytes(fields, "ResourceList"));
                d["tag"] = Name(FieldBytes(fields, "Tag"));
            }
            else if (kind == "Expression")
            {
                var arr = FieldBytes(fields, "TagRefs");
                var tags = new List<object?>();
                if (arr.Length >= 8)
                {
                    var cnt = Bin.U32(arr, 4);
                    foreach (var o in Bin.U32s(arr, 8, cnt))
                        tags.Add(Name(Py.Slice(arr, o, o + 5)));
                }
                d["tags"] = tags;
                d["mapping"] = entry is byte[] e2 ? Mapping(e2) : null;
                string TagRefRepl(Match mo)
                {
                    var t = Name(Py.Concat([5], Py.Latin1Bytes(mo.Groups[1].Value)));
                    return Py.Bool(t) ? "'" + t + "'" : mo.Value;
                }
                foreach (PyDict e in ((d["mapping"] as PyDict)?.Get("entries") as List<object?>) ?? [])
                    if (e.ContainsKey("condition"))
                        e["condition"] = TagRef.Replace((string)e["condition"]!, TagRefRepl);
            }
            else if (kind == "Script")
            {
                var t = Target(FieldBytes(fields, "Script"));
                var code = t is not null ? Script(t).Get("code") : null;
                d["code"] = code ?? scripts.FirstOrDefault(s => Py.Eq(s.Get("name"), prop + "Trigger"))?.Get("code");
            }
            else if (kind == "Flashing")
            {
                object? Argb(byte[] v) => v.Length == 4 ? Fmt08X((long)Bin.U32(v, 0)) : null;
                d["color"] = Argb(FieldBytes(fields, "Value"));
                d["alternateColor"] = Argb(FieldBytes(fields, "AlternateValue"));
                d["flashingRate"] = Py.Or(NestedStr(FieldBytes(fields, "FlashingRate")), null);
                d["flashingCondition"] = Py.Or(NestedStr(FieldBytes(fields, "FlashingCondition")), null);
            }
            else if (kind == "FaceplateInterface")
                d["interfaceProperty"] = Py.Or(NestedStr(FieldBytes(fields, "PropertyName")), null);
            else if (kind.Length == 0)
                continue;
            foreach (PyDict e in ((d.Get("mapping") as PyDict)?.Get("entries") as List<object?>) ?? [])
            {
                var v = e.Get("value");
                if (v is string vs && vs.Length == 10 && vs[..2] == "05")
                    e["value"] = Name(FromHexPy(vs));
                else if (Py.Eq(v, ""))
                    e.Remove("value");
            }
            outl.Add(d);
        }
        return outl;
    }

    static byte[] FromHexPy(string s)
    {
        try
        {
            return Convert.FromHexString(s);
        }
        catch (FormatException)
        {
            throw new PyValueError("non-hexadecimal number found in fromhex() arg");
        }
    }

    public PyDict References(string k) => p.Sets(k).Set("IReferenceExpandoAttributes");

    static string RefName(PyTuple r) => Py.Utf8((byte[])r[1]!);

    /// <summary>Reference property whose value indexes the item's links (Graphic, Screen, ResourceList).</summary>
    public string? ResolveLink(string k, PyDict refs, PyTuple rf)
    {
        if (refs.Get(RefName(rf)) is not byte[] v || v.Length < 4)
            return null;
        var i = (long)Bin.FromBe(v, 0, 4);
        if (v.Length > 4 && (v[4] & 0x80) != 0)
        {
            var links = p.Links(k, "ExpandoChildren").Select(l => l.Key).ToList();
            return i < links.Count && p.Objects.ContainsKey(links[(int)i]) ? LibraryImageName(p, links[(int)i]) : null;
        }
        var lr = p.Links(k, "ExpandoReferences").Select(l => l.Key).ToList();
        return i < lr.Count && p.Objects.ContainsKey(lr[(int)i]) ? p.Name(lr[(int)i]) : null;
    }

    /// <summary>Project graphics: HmiImageData without ContentToVersion.</summary>
    public List<object?> Graphics() =>
        L(Py.Sorted(p.OfType("HmiImageData").Where(k => p.Targets(k, "ContentToVersion").Count == 0).Select(k => ImageInfo(p, k)), x => Py.Or(x["name"], "")));

    byte[]? FieldTextBlob(PyDict refs, PyTuple rf, string? k)
    {
        if (!(refs.Get(RefName(rf)) is byte[] v && v.Length == 5 && (v[4] & 0x80) != 0 && k is not null))
            return null;
        var links = p.Links(k, "ExpandoChildren").Select(l => l.Key).ToList();
        var i = (long)Bin.FromBe(v, 0, 4);
        var td = i < links.Count ? links[(int)i] : null;
        var blob = td is not null && Short(td) == "HmiTextData" ? p.Attr(td, "ICoreTextRepository", "Text") : null;
        return blob as byte[];
    }

    static readonly Regex ProjectText = new("<ProjectText.*?</ProjectText>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    static readonly Regex PBody = new("<p>(.*?)</p>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    static readonly Regex FieldPart = new("([^<]+)|<field ref=\"(\\d+)\"\\s*/>", RegexOptions.CultureInvariant);
    static readonly Regex FieldInfo = new("<fieldinfo name=\"(\\d+)\".*?<format ([^>]*)/>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    static readonly Regex AttrPair = new("(\\w+)=\"([^\"]*)\"", RegexOptions.CultureInvariant);

    /// <summary>Output fields of a text: {"cultures": {culture: [text | {"field": n}]}, "fields": [...]} or null.</summary>
    public PyDict? TextFields(PyDict refs, PyTuple rf, string? k)
    {
        var blob = FieldTextBlob(refs, rf, k);
        if (blob is null)
            return null;
        var plain = ProjectModel.CoreText(blob, m.Cultures);
        var raw = Py.Utf8(blob).Replace("\0", "");
        var xmls = ProjectText.Matches(raw).Select(x => x.Value).ToList();
        var fields = new List<object?>();
        var cultures = new PyDict();
        var keys = plain.Keys.ToList();
        for (var i = 0; i < Math.Min(keys.Count, xmls.Count); i++)
        {
            var (c, x) = ((string)keys[i]!, xmls[i]);
            var body = PBody.Match(x);
            if (!body.Success)
                continue;
            var parts = new List<object?>();
            var bt = body.Groups[1].Value;
            var pos = 0;
            while (pos <= bt.Length)
            {
                var fm = FieldPart.Match(bt, pos);
                if (!fm.Success)
                    break;
                parts.Add(fm.Groups[2].Success && fm.Groups[2].Length > 0 ? PyDict.Of("field", Py.ParseInt(fm.Groups[2].Value)) : WebUtility.HtmlDecode(fm.Groups[1].Value));
                pos = fm.Length > 0 ? fm.Index + fm.Length : fm.Index + 1;
            }
            cultures[c == "" ? "65535" : c] = parts;
            if (fields.Count == 0)
            {
                foreach (Match fi in FieldInfo.Matches(x))
                {
                    var at = new Dictionary<string, string>();
                    foreach (Match am in AttrPair.Matches(fi.Groups[2].Value))
                        at[am.Groups[1].Value] = am.Groups[2].Value;
                    fields.Add(PyDict.Of("field", Py.ParseInt(fi.Groups[1].Value), "displayType", at.GetValueOrDefault("displaytype"),
                        "length", Py.IsDigit(at.GetValueOrDefault("length", "")) ? Py.ParseInt(at["length"]) : null,
                        "precision", Py.IsDigit(at.GetValueOrDefault("precision", "")) ? Py.ParseInt(at["precision"]) : null));
                }
            }
        }
        cultures.Pop("127");
        var dflt = cultures.Pop("65535");
        var o = new PyDict();
        foreach (var c in Langs(cultures))
        {
            var v = cultures.Get(c, dflt);
            if (v is not null)
                o[c] = v;
        }
        return o.Count > 0 && fields.Count > 0 ? PyDict.Of("cultures", o, "fields", fields) : null;
    }

    /// <summary>sorted(set(languages()) | set(t)) or [""].</summary>
    List<string> Langs(PyDict t)
    {
        var s = new HashSet<string>(Languages());
        foreach (var k in t.Keys)
            s.Add((string)k!);
        var l = Py.Sorted(s);
        return l.Count > 0 ? l : [""];
    }

    static readonly Dictionary<string, string> FontPartMembers = new() { [" \x02"] = "Font.Size", ["u\x04"] = "Font.Weight" };

    /// <summary>Dynamizations of Font members linked to a faceplate interface property.</summary>
    public List<object?> PartDynamizations(string k)
    {
        var a = p.Sets(k);
        var dyn = a.Set("IPropertyDynamicExpandoAttributes");
        var font = a.Set("IPropertyExpandoAttributes").Get("\x1c\x02");
        if (font is not byte[] fb || dyn.Count == 0)
            return [];
        var part = Faceplates.DecodeMap(fb);
        var dyns = part is PyDict pd ? pd.Get("_#Dynamics") : null;
        var outl = new List<object?>();
        foreach (var (member, key) in dyns as PyDict ?? new PyDict())
        {
            var prop = member is string ms ? FontPartMembers.GetValueOrDefault(ms) : null;
            var entry = key is string ks ? dyn.Get(ks) : null;
            if (prop is null || entry is not byte[] eb)
                continue;
            var fields = FieldsOf(eb);
            if (NestedStr(FieldBytes(fields, "Type")) != "FaceplateInterface")
                continue;
            outl.Add(PyDict.Of("property", prop, "type", "FaceplateInterface",
                "interfaceProperty", Py.Or(NestedStr(FieldBytes(fields, "PropertyName")), null), "mapping", Mapping(eb)));
        }
        return outl;
    }

    public object ResolveText(PyDict refs, PyTuple rf, string? k = null)
    {
        var v = refs.Get(RefName(rf));
        if (v is string s)
            return s;
        if (v is not byte[] vb)
            return new PyDict();
        PyDict t;
        if (vb.Length == 5 && (vb[4] & 0x80) != 0 && k is not null)
        {
            var blob = FieldTextBlob(refs, rf, k);
            var t0 = blob is not null ? ProjectModel.CoreText(blob, m.Cultures) : new PyDict();
            t = new PyDict();
            foreach (var (c, x) in t0)
                t[Py.Eq(c, "") ? "65535" : c] = x;
        }
        else
            t = m.CoreTextOf(vb);
        t.Pop("127");
        var dflt = t.Pop("65535");
        var o = new PyDict();
        foreach (var c in Langs(t))
        {
            var x = t.Get(c, dflt);
            if (x is not null)
                o[c] = x;
        }
        return o;
    }

    /// <summary>ILScreenItems of a screen in their stored order (extended layout: the typed slot pointing back).</summary>
    List<string> ScreenItems(string screen)
    {
        var items = p.Targets(screen, "ILScreenItems");
        if (items.Count > 0)
            return items;
        var bySlot = new OrderedDictionary<int, List<string>>();
        foreach (var l in p.Links(screen))
        {
            if (l.Relation is null && p.Objects.ContainsKey(l.Key))
            {
                var s = l.Slot ?? -1;
                if (!bySlot.TryGetValue(s, out var lst))
                    bySlot[s] = lst = [];
                lst.Add(l.Key);
            }
        }
        foreach (var keys in bySlot.Values)
            if (keys.Count > 0 && keys.All(t => p.Targets(t, "ILScreenItemParent").Contains(screen)))
                return keys;
        return [];
    }

    static readonly Regex LangSplit = new("[,;]", RegexOptions.CultureInvariant);

    List<string> AppLanguages(string app)
    {
        var ls = Of(app, "HmiLanguageAndFontSettingsData").FirstOrDefault();
        var order = (ls is not null ? p.Attr(ls, "IHmiLanguageAndFontSettingsAttributes", "OnlineLanguageOrder") : null);
        return LangSplit.Split(Py.Bool(order) ? (string)order! : "").Select(Py.Strip).Where(x => x.Length > 0).ToList();
    }

    /// <summary>HMI languages (OnlineLanguageOrder of the current or the first HmiLanguageAndFontSettingsData).</summary>
    public List<string> Languages()
    {
        if (langs is null)
        {
            langs = [];
            foreach (var k in p.Objects.Keys)
            {
                if (Short(k) == "HmiLanguageAndFontSettingsData")
                {
                    var order = p.Attr(k, "IHmiLanguageAndFontSettingsAttributes", "OnlineLanguageOrder");
                    langs = LangSplit.Split(Py.Bool(order) ? (string)order! : "").Select(Py.Strip).Where(x => x.Length > 0).ToList();
                    break;
                }
            }
        }
        return langs;
    }

    public List<object?> Screens(string app)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "ILScreenData"))
        {
            var props = ScreenProperties(k);
            var layers = Layers(k);
            outl.Add(PyDict.Of("name", Core(k).Get("Name"), "group", m.FolderPath(k), "width", props.Get("Width"), "height", props.Get("Height"),
                "backColor", props.Get("BackColor"), "layers", layers, "items", L(Items(k, layers)), "key", k));
        }
        return L(Py.Sorted(outl, x => Py.T(x["group"], Py.Or(x["name"], ""))));
    }

    /// <summary>The layers of a screen (expando 'K\x02'): index, name, guid, esVisible.</summary>
    public List<object?> Layers(string screen)
    {
        var blob = p.Sets(screen).Set("IPropertyExpandoAttributes").Get("K\x02");
        var outl = new List<object?>();
        if (blob is not byte[] b || b.Length < 8)
            return outl;
        try
        {
            var cnt = Bin.U32(b, 4);
            var offs = Bin.U32s(b, 8, cnt).Select(x => (long)x).Append(b.Length).ToList();
            for (var i = 0; i < cnt; i++)
            {
                var f = new PyDict();
                foreach (var (n, _, v) in Nested(Py.Slice(b, offs[i], offs[i + 1])))
                    f[Py.Utf8(n)] = v;
                var g = f.Get("_Guid") as byte[];
                var name = f.Get("\x03") as byte[];
                var vis = f.Get("_#ESVisible") as byte[];
                outl.Add(PyDict.Of("index", (long)i, "name", name is { Length: > 0 } ? Py.Utf8(name[1..]) : null,
                    "guid", g is { Length: 16 } ? Attrs.Guid(g) : null,
                    "esVisible", vis is { Length: > 0 } ? vis[0] != 0 : null));
            }
        }
        catch (Exception e) when (Attrs.IsDecodeError(e) || e is PyValueError)
        {
        }
        return outl;
    }

    /// <summary>Screen items with properties, text, tags, events, dynamizations, layer and z order.</summary>
    public List<PyDict> Items(string screen, List<object?>? layers = null)
    {
        var outl = new List<PyDict>();
        var layerIndex = new PyDict();
        foreach (PyDict l in layers ?? [])
            if (Py.Bool(l.Get("guid")))
                layerIndex[l["guid"]] = l["index"];
        var items = ScreenItems(screen);
        for (var z = 0; z < items.Count; z++)
        {
            var k = items[z];
            var c = Core(k);
            var props = ScreenProperties(k);
            var refs = References(k);
            object text = new PyDict();
            foreach (var (pnv, pv) in props.Items.ToList())
            {
                var pn = (string)pnv!;
                if (pv is PyTuple rt && Py.Eq(rt[0], "ref") && pn is "Graphic" or "Screen" or "ResourceList")
                    props[pn] = ResolveLink(k, refs, rt);
                else if (pv is PyTuple rt2 && Py.Eq(rt2[0], "ref"))
                {
                    props[pn] = ResolveText(refs, rt2, k);
                    if (pn == "Text")
                    {
                        text = props[pn]!;
                        var fields = TextFields(refs, rt2, k);
                        if (fields is not null)
                            props["TextFields"] = fields;
                    }
                }
                else if (pv is List<object?> pl && pl.Count > 0 && pl[0] is PyTuple)
                    props[pn] = pl.Select(r => (object?)PyDict.Of("Text", ResolveText(refs, (PyTuple)r!, k))).ToList();
            }
            var tags = p.Targets(k, "ExpandoReferences").Where(t => Short(t) == "HmiTagData").Select(t => (object?)p.Name(t)).ToList();
            var widget = p.Targets(k, "ExpandoReferences").Where(t => Short(t) == "HmiDynamicScreenItemResourceData").Select(p.Name).FirstOrDefault();
            if (Py.Bool(widget))
                props["Widget"] = widget;
            foreach (var (pname, pval) in ItemPartsOf(p, k))
                props[pname] = pval;
            var scripts = p.Targets(k, "EmbeddedScriptChildren").Select(e =>
            {
                var s = new PyDict(Script(e));
                s["name"] = Core(e).Get("Name");
                return s;
            }).ToList();
            var dyn = Dynamizations(k, scripts);
            dyn.AddRange(PartDynamizations(k));
            if (dyn.Any(x => Py.Eq(((PyDict)x!).Get("property"), "ProcessValue")))
            {
                props.Pop("ProcessValue");
                props.Pop("ProcessValueRaw");
            }
            var events = scripts.Where(s => !Py.Eq(s.Get("kind"), "Screen.Trigger")).Cast<object?>().ToList();
            var parent = m.Rel1(k, "ContainerParent");
            outl.Add(PyDict.Of("name", c.Get("Name"), "type", c.Get("Subtype"), "left", props.Get("Left", 0L),
                "width", props.Get("Width"), "height", props.Get("Height"), "backColor", props.Get("BackColor"),
                "text", text, "properties", props, "tags", tags, "events", events,
                "dynamizations", dyn, "zIndex", (long)z,
                "parent", parent is not null ? p.Name(parent) : null,
                "layerIndex", layerIndex.Get(props.Get("Layer")), "key", k));
        }
        return Py.Sorted(outl, x => Py.Or(x["name"], ""));
    }

    /// <summary>Text of an item: CoreText value in IReferenceExpandoAttributes; default under ''.</summary>
    public PyDict ItemText(string k)
    {
        foreach (var v in p.Sets(k).Set("IReferenceExpandoAttributes").Values)
        {
            if (v is byte[] b && b.Length > 16)
            {
                var t = m.CoreTextOf(b);
                t.Pop("127");
                if (t.Count > 0)
                {
                    var r = new PyDict();
                    foreach (var (c, x) in t)
                        r[Py.Eq(c, "65535") ? "" : c] = x;
                    return r;
                }
            }
        }
        return new PyDict();
    }

    /// <summary>IScriptSignatureAttributes.ParameterList -> parameter names.</summary>
    static List<object?> Params(object? v)
    {
        var outl = new List<object?>();
        if (v is not byte[] b || b.Length < 8)
            return outl;
        try
        {
            var cnt = Bin.U32(b, 4);
            foreach (var off in Bin.U32s(b, 8, cnt))
            {
                var f3 = Bin.U32(b, off + 12L);
                var (t, _) = Attrs.ReadVar(b, (long)off + f3);
                outl.Add(t is { Length: > 0 } ? Py.Utf8(t) : "");
            }
        }
        catch (Exception e) when (Attrs.IsDecodeError(e))
        {
        }
        return outl;
    }

    public List<object?> ScriptModules(string app)
    {
        var outl = new List<PyDict>();
        foreach (var k in Of(app, "HmiScriptGlobalModuleData"))
            outl.Add(ScriptModule(k));
        return L(Py.Sorted(outl, x => Py.Or(x["name"], "")));
    }

    /// <summary>A global script module (HmiScriptGlobalModuleData): global definition area, functions and the whole code.</summary>
    public PyDict ScriptModule(string k)
    {
        var g = m.Rel1(k, "GlobalSection");
        var glob = g is not null ? Py.Str(Py.Or(Script(g)["code"], "")) : "";
        var funcs = new List<PyDict>();
        foreach (var f in p.Targets(k, "SimpleScriptChildren"))
        {
            var sig = p.Sets(f).Set("IScriptSignatureAttributes");
            funcs.Add(PyDict.Of("name", p.Name(f), "parameters", Params(sig.Get("ParameterList")),
                "exported", sig.Get("IsExported"), "async", sig.Get("IsAsync"),
                "hasReturnValue", sig.Get("HasReturnValue"), "body", Py.Or(Script(f)["code"], "")));
        }
        var code = glob + "\r\n" + string.Concat(funcs.Select(fn =>
            $"{(Py.Bool(fn["exported"]) ? "export " : "")}{(Py.Bool(fn["async"]) ? "async " : "")}function {Py.Str(fn["name"])}({string.Join(", ", ((List<object?>)fn["parameters"]!).Select(Py.Str))}) {{{Py.Str(fn["body"])}\r\n}}\r\n"));
        var ma = p.Sets(k).Set("IScriptModuleAttributes");
        return PyDict.Of("name", p.Name(k), "language", ma.Get("Language"), "functions", L(funcs), "code", code);
    }

    /// <summary>HmiScriptEmbeddedData: language, scope (kind) and the code (token table; references resolved).</summary>
    public PyDict Script(string k)
    {
        var sa = p.Sets(k).Set("IScriptAttributes");
        var b = sa.Get("Code");
        var refsOut = new PyDict();
        var res = PyDict.Of("language", sa.Get("Language"), "kind", sa.Get("Scope"), "code", null, "references", refsOut);
        var refs = p.Links(k, "ExpandoReferences").Select(l => l.Key).ToList();
        foreach (var (name, v) in p.Sets(k).Set("IScriptLinkExpandoAttributes"))
        {
            if (v is byte[] vb && vb.Length >= 4)
            {
                var idx = (long)Bin.FromBe(vb, 0, 4);
                if (idx < refs.Count && p.Objects.ContainsKey(refs[(int)idx]))
                    refsOut[name] = p.Name(refs[(int)idx]);
            }
        }
        if (b is not byte[] bb)
            return res;
        try
        {
            var cnt = Bin.U32(bb, 4);
            var offs = Bin.U32s(bb, 8, cnt);
            var toks = new List<string>();
            foreach (var off in offs)
            {
                var (t, _) = Attrs.ReadVar(bb, off + 9L);
                toks.Add(t is { Length: > 0 } ? Py.Utf8(t) : "");
            }
            res["code"] = string.Concat(toks.Select(t => refsOut.TryGetValue(t, out var r) ? Py.Str(r) : t));
        }
        catch (Exception e) when (Attrs.IsDecodeError(e))
        {
        }
        return res;
    }

    // --- text output ----------------------------------------------------------------------------------
    static string KV(object? d) => string.Join("; ", ((PyDict)d!).Select(kv => Py.Str(kv.Key) + ": " + Py.Str(kv.Value)));

    public static List<string> TextLines(PyDict sw, int indent)
    {
        var pad = new string(' ', indent);
        var o = new List<string> { $"{pad}HMI {Py.Str(sw["name"])}  {Py.Str(sw["typeName"])} {Py.Str(sw["orderNumber"])} {Py.Str(sw["firmware"])}" };
        foreach (PyDict c in (List<object?>)sw["connections"]!)
            o.Add($"{pad}  Connection {Py.Str(c["name"])} ({Py.Str(c["kind"])}, {Py.Str(c["plcId"])})");
        foreach (PyDict t in (List<object?>)sw["tagTables"]!)
        {
            o.Add($"{pad}  HMI tag table {(Py.Bool(t["group"]) ? Py.Str(t["group"]) + "/" : "")}{Py.Str(t["name"])}");
            foreach (PyDict g in (List<object?>)t["tags"]!)
            {
                var src = Py.Bool(g["connection"]) ? $"{Py.Str(g["connection"])} {Py.Str(Py.Or(g["plcTag"], ""))}" : "<internal>";
                o.Add($"{pad}    {Py.Ljust(g["name"], 24)} {Py.Ljust(g["dataType"], 8)} {Py.Ljust(Py.Strip(src), 30)} {Py.Ljust(Py.Or(g["acquisitionCycle"], ""), 8)} {KV(g["comment"])}");
            }
        }
        var cn = (PyDict)sw["counts"]!;
        o.Add($"{pad}  System tags {Py.Long(cn["systemTags"])}, internal system tags {Py.Long(cn["internalSystemTags"])}, system events {Py.Long(cn["systemEvents"])}");
        var cycles = (List<object?>)sw["cycles"]!;
        if (cycles.Count > 0)
            o.Add($"{pad}  Cycles: {string.Join(", ", cycles.Cast<PyDict>().Select(x => $"{Py.Str(x["name"])}({Py.Str(x["time"])} {Py.Str(x["unit"])})"))}");
        var tls = ((List<object?>)sw["textLists"]!).Cast<PyDict>().ToList();
        var user = tls.Where(t => !Py.Bool(t["system"])).ToList();
        o.Add($"{pad}  Text lists: {user.Count} user, {tls.Count - user.Count} system");
        foreach (var t in user)
        {
            o.Add($"{pad}    {Py.Str(t["name"])}");
            foreach (PyDict e in (List<object?>)t["entries"]!)
                o.Add($"{pad}      {Py.Str(e["from"])}..{Py.Str(e["to"])} {Py.Str(e["type"])}{(Py.Bool(e["default"]) ? " default" : "")}  {KV(e["text"])}");
        }
        var acs = (List<object?>)sw["alarmClasses"]!;
        if (acs.Count > 0)
            o.Add($"{pad}  Alarm classes: {string.Join(", ", acs.Cast<PyDict>().Select(c => $"{Py.Str(c["name"])}({Py.Str(c["id"])})"))}");
        foreach (PyDict a in (List<object?>)sw["discreteAlarms"]!)
            o.Add($"{pad}  Discrete alarm {Py.Str(a["name"])} id={Py.Str(a["id"])} class={Py.Str(a["alarmClass"])} trigger={Py.Str(a["triggerTag"])}.{Py.Str(a["triggerBit"])} {Py.Str(a["triggerMode"])}  {KV(a["text"])}");
        foreach (PyDict a in (List<object?>)sw["analogAlarms"]!)
            o.Add($"{pad}  Analog alarm {Py.Str(a["name"])} id={Py.Str(a["id"])} class={Py.Str(a["alarmClass"])} trigger={Py.Str(a["triggerTag"])} {Py.Str(a["condition"])} {Py.Str(a["limit"])}  {KV(a["text"])}");
        foreach (PyDict s in (List<object?>)sw["screens"]!)
        {
            o.Add($"{pad}  Screen {(Py.Bool(s["group"]) ? Py.Str(s["group"]) + "/" : "")}{Py.Str(s["name"])}  {Py.Str(s["width"])}x{Py.Str(s["height"])}{(Py.Bool(s["backColor"]) ? "  BackColor " + Py.Str(s["backColor"]) : "")}");
            foreach (PyDict it in (List<object?>)s["items"]!)
            {
                var props = Py.Sorted(((PyDict)it["properties"]!).Items, kv => Py.T(kv.Key, kv.Value));
                o.Add($"{pad}    {Py.Ljust(it["name"], 16)} {Py.Ljust(it["type"], 14)} {string.Join(" ", props.Select(kv => Py.Str(kv.Key) + "=" + Py.Str(kv.Value)))}" +
                      (Py.Bool(it["tags"]) ? "  tags " + string.Join(",", ((List<object?>)it["tags"]!).Select(Py.Str)) : "") +
                      (Py.Bool(it["text"]) ? "  text " + KV(it["text"]) : ""));
                foreach (PyDict ev in (List<object?>)it["events"]!)
                    o.Add($"{pad}      {Py.Str(ev["name"])} [{Py.Str(ev.Get("language"))}]: {Py.Str(ev.Get("code"))}");
                foreach (PyDict d in (List<object?>)it.Get("dynamizations", new List<object?>())!)
                {
                    var mp = d.Get("mapping") as PyDict;
                    var mt = "";
                    if (mp is not null && mp.Count > 0 && !Py.Eq(mp.Get("conditionType"), "None"))
                        mt = $" {Py.Str(mp.Get("conditionType"))}: " + string.Join(", ", ((List<object?>)mp.Get("entries", new List<object?>())!).Cast<PyDict>().Select(e =>
                            e.ContainsKey("from") ? $"{Py.Str(e["from"])}..{Py.Str(e["to"])}->{Py.Str(e.Get("value", ""))}"
                            : e.ContainsKey("condition") ? $"[{Py.Str(e["condition"])}]->{Py.Str(e["value"])}" : Py.Str(e["value"])));
                    string? what;
                    if (Py.Eq(d["type"], "Tag"))
                        what = Py.Str(Py.Or(d.Get("tag"), "")) + mt;
                    else if (Py.Eq(d["type"], "ResourceList"))
                        what = $"{Py.Str(d.Get("resourceList"))} index {Py.Str(d.Get("tag"))}";
                    else if (Py.Eq(d["type"], "Expression"))
                        what = $"tags {string.Join(",", ((List<object?>)d.Get("tags", new List<object?>())!).Select(Py.Str))}{mt}";
                    else if (Py.Eq(d["type"], "Flashing"))
                        what = $"{Py.Str(d.Get("color"))}/{Py.Str(d.Get("alternateColor"))} {Py.Str(d.Get("flashingRate"))} {Py.Str(d.Get("flashingCondition"))}";
                    else
                        what = Py.Str(d.Get("code"));
                    o.Add($"{pad}      dyn {Py.Str(d["property"])} <- {Py.Str(d["type"])} {what}");
                }
            }
        }
        return o;
    }
}
