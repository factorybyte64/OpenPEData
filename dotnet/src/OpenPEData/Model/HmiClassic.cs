// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Screens and styles of classic WinCC devices (Comfort Panels, WinCC Runtime Advanced; GraphX model).
/// Screens are HmiScreenData (screens, templates, slide-in screens),
/// HmiGlobalElementsScreenData and HmiOverviewScreenData with Target = the HmiApplicationData; screen items are
/// HmiScreenItemData (Parent = screen, ICoreAttributes.Subtype = Openness type name). Only values stored in the file are
/// emitted: IHmiScreenItemAttributes (Left, Top, Width, Height, Layer) and the named expando attributes
/// IHmiScreenItemExpandoAttributes; references (IHmiScreenItemExpandoReferencesAttributes) are resolved to texts, fonts,
/// tags, style items, graphics and lists. The default values of an item type come from the style items of the device
/// (style output, DeviceStyleInstance), which the viewer merges for drawing.</summary>
internal sealed class HmiClassic
{
    readonly ProjectModel m;
    readonly Project p;

    public HmiClassic(ProjectModel model)
    {
        m = model;
        p = model.P;
    }

    string Short(string k) => Py.AfterLast(p.TypeName(k) ?? "", ".");

    List<string> Of(string app, string shortName) => Py.Sorted(m.Sources(app, "Target").Where(k => p.Objects.ContainsKey(k) && Short(k) == shortName));

    static readonly string[] ScreenTypes = ["HmiScreenData", "HmiGlobalElementsScreenData", "HmiOverviewScreenData"];

    /// <summary>A classic WinCC application: it has GraphX screens and no Unified (IL) screens.</summary>
    public bool IsClassic(string app) =>
        m.Sources(app, "Target").Any(k => p.Objects.ContainsKey(k) && Short(k) is "HmiScreenData" or "HmiGlobalElementsScreenData")
        && !m.Sources(app, "Target").Any(k => p.Objects.ContainsKey(k) && Short(k) == "ILScreenData");

    PyDict Core(string k) => p.Sets(k).Set("ICoreAttributes");

    /// <summary>Link list of a relation with the empty slots (deleted references stay as null keys and keep the
    /// positions that the expando reference indexes count; sample C03).</summary>
    List<string> RefList(string k, string relation)
    {
        var links = p.Links(k, relation);
        if (links.Count == 0 && SlotOf(relation) is { } slot)
            // an untyped slot whose targets also link back with another relation (style item Font -> style font list,
            // which is also its child, sample C03) is named after that relation: take the slot of the relation instead
            links = p.Links(k).Where(l => l.Relation is null && l.Slot == slot).ToList();
        return links.Select(l => l.Key).ToList();
    }

    readonly Dictionary<string, int?> slots = [];

    /// <summary>Slot number of a GraphX relation (ExpandoReferences, ExpandoChildren), from the first screen item that
    /// stores it as a tagged link; the slot is the same for every GraphX object type.</summary>
    int? SlotOf(string relation)
    {
        if (slots.TryGetValue(relation, out var s))
            return s;
        s = null;
        foreach (var k in p.Objects.Keys)
        {
            if (Short(k) != "HmiScreenItemData")
                continue;
            if (p.Links(k, relation).FirstOrDefault(l => l.Relation is not null && l.Slot is not null) is { } l)
            {
                s = l.Slot;
                break;
            }
        }
        return slots[relation] = s;
    }

    /// <summary>Target of an expando reference, or null when the slot is empty.</summary>
    string? RefTarget(string k, byte[] b)
    {
        if (RefIndex(b) is not { } ri)
            return null;
        var list = RefList(k, ri.Child ? "ExpandoChildren" : "ExpandoReferences");
        return ri.Index < list.Count && p.Objects.ContainsKey(list[ri.Index]) ? list[ri.Index] : null;
    }

    // --- values ---------------------------------------------------------------------------------------------------

    /// <summary>Color value of the GraphX expandos: u32 8, then B, G, R, A -> "0xAARRGGBB" (as the Unified output).</summary>
    public static string? Color(byte[] b) =>
        b.Length == 8 && Bin.U32(b, 0) == 8 ? $"0x{b[7]:X2}{b[6]:X2}{b[5]:X2}{b[4]:X2}" : null;

    /// <summary>Color as a signed ARGB integer (HmiThemeData) -> "0xAARRGGBB".</summary>
    static string ArgbInt(long v) => $"0x{unchecked((uint)v):X8}";

    /// <summary>Expando reference: u32 6, u16 index (bit 15: into ExpandoChildren, else into ExpandoReferences).</summary>
    static (int Index, bool Child)? RefIndex(byte[] b)
    {
        if (b.Length < 6 || Bin.U32(b, 0) != 6)
            return null;
        var v = Bin.U16(b, 4);
        return ((v & 0x7FFF), (v & 0x8000) != 0);
    }

    /// <summary>List blob of the GraphX expandos: u32 total length, u32 count (or byte length), then the elements
    /// (u16 or i32). Returns the elements or null when the blob is no such list.</summary>
    static List<object?>? IntList(byte[] b)
    {
        if (b.Length < 8 || Bin.U32(b, 0) != b.Length)
            return null;
        var n = Bin.U32(b, 4);
        var body = b.Length - 8;
        if (n == 0)
            return body == 0 ? [] : null;
        if (n == body)
            n = (uint)(body / 4);
        if (body % n != 0)
            return null;
        var size = body / n;
        var o = new List<object?>();
        for (var i = 0; i < n; i++)
            o.Add(size switch
            {
                1 => (long)b[8 + i],
                2 => (long)Bin.I16(b, 8 + 2L * i),
                4 => (long)Bin.I32(b, 8 + 4L * i),
                _ => null,
            });
        return size is 1 or 2 or 4 ? o : null;
    }

    /// <summary>"Tahoma:16:1:2:1:0" (HmiFontItemData name) -> Name, Size, Bold, raw spec.</summary>
    static PyDict? FontOf(string? spec)
    {
        if (spec is null)
            return null;
        var parts = spec.Split(':');
        var f = PyDict.Of("Name", parts[0], "spec", spec);
        if (parts.Length > 1 && long.TryParse(parts[1], out var size))
            f["Size"] = size;
        if (parts.Length > 2 && long.TryParse(parts[2], out var style))
        {
            f["Bold"] = (style & 1) != 0;
            f["Italic"] = (style & 2) != 0;
            f["Underline"] = (style & 4) != 0;
            f["Strikeout"] = (style & 8) != 0;
        }
        return f;
    }

    /// <summary>Font of a font list (HmiFontListData / HmiStyleFontListData): per culture an index into its
    /// ExpandoReferences (HmiFontItemData). Returns the font of the first culture (the project language) and all
    /// cultures under 'cultures' when there are several.</summary>
    PyDict? FontList(string fl)
    {
        var refs = RefList(fl, "ExpandoReferences");
        var per = new PyDict();
        foreach (var (c, v) in p.Sets(fl).Set("ICoreMultiCultureObjectInternalData"))
        {
            if (v is not byte[] b || RefIndex(b) is not { } ri || ri.Index >= refs.Count || !p.Objects.ContainsKey(refs[ri.Index]))
                continue;
            if (FontOf(p.Name(refs[ri.Index])) is not { } f)
                continue;
            var lc = Py.IsInt(c) ? Py.Long(c) : long.TryParse(Py.Str(c), out var x) ? x : -1;
            per[m.Cultures.TryGetValue(lc, out var cn) && cn.Length > 0 ? cn : Py.Str(c)] = f;
        }
        if (per.Count == 0)
            return refs.Where(r => p.Objects.ContainsKey(r) && Short(r) == "HmiFontItemData").Select(r => FontOf(p.Name(r))).FirstOrDefault();
        var first = new PyDict((PyDict)per.Values.First()!);
        if (per.Count > 1)
            first["cultures"] = per;
        return first;
    }

    /// <summary>Text of an HmiTextData (ICoreTextRepository.Text): {culture: text}, '' = no language.</summary>
    PyDict TextOf(string k)
    {
        var a = p.Sets(k);
        var t = m.CoreTextOf(a.Set("ICoreTextRepository").Get("Text"));
        var r = new PyDict();
        foreach (var (c, x) in t)
            r[Py.Eq(c, "65535") ? "" : c] = x;
        if (r.Count == 0 && a.Set("ICoreTextExtendedData").Get("DefaultText") is string d)
            r[""] = d;
        return r;
    }

    /// <summary>A resolved reference value: text, font, or the name (and kind) of the referenced object.</summary>
    object? Resolve(string target)
    {
        var s = Short(target);
        return s switch
        {
            "HmiTextData" => TextOf(target),
            "HmiFontListData" or "HmiStyleFontListData" => FontList(target),
            "HmiFontItemData" => FontOf(p.Name(target)),
            _ => PyDict.Of("kind", s, "name", p.Name(target)),
        };
    }

    /// <summary>Decoded expando attributes of an object: colors, int lists, references (resolved through
    /// ExpandoReferences/ExpandoChildren), the rest as stored (bytes as hex).</summary>
    PyDict Expandos(string k, string valueSet, string? refSet)
    {
        var o = new PyDict();
        var sets = p.Sets(k);
        foreach (var (n, v) in sets.Set(valueSet))
            o[n] = Value(v);
        if (refSet is null)
            return o;
        foreach (var (n, v) in sets.Set(refSet))
        {
            if (v is not byte[] b || RefIndex(b) is null)
            {
                o[n] = Value(v);
                continue;
            }
            o[n] = RefTarget(k, b) is { } t ? Resolve(t) : null;
        }
        return o;
    }

    static object? Value(object? v)
    {
        if (v is not byte[] b)
            return v;
        if (Color(b) is { } c)
            return c;
        if (IntList(b) is { } l)
            return l;
        return "hex:" + Py.HexStr(b);
    }

    // --- screens --------------------------------------------------------------------------------------------------

    public List<object?> Screens(string app)
    {
        var outl = new List<PyDict>();
        foreach (var t in ScreenTypes)
        {
            foreach (var k in Of(app, t))
            {
                var c = Core(k);
                var sa = p.Sets(k).Set("IHmiScreenAttributes");
                var ex = Expandos(k, "IHmiScreenExpandoAttributes", "IHmiScreenExpandoReferencesAttributes");
                ex.Pop("ScreenLayerCollection");
                foreach (var n in ex.Keys.Where(x => Py.Str(x).StartsWith("Layer", StringComparison.Ordinal)).ToList())
                    ex.Pop(n);
                var kind = Py.Str(Py.Or(c.Get("Subtype"), t));
                var template = m.Rel1(k, "TemplateRelation");
                var d = PyDict.Of("name", c.Get("Name"), "group", m.FolderPath(k), "kind", kind, "platform", "Classic",
                    "number", sa.Get("Number"), "width", sa.Get("Width"), "height", sa.Get("Height"),
                    "backColor", ex.Get("BackColor"), "template", template is not null ? p.Name(template) : null,
                    "properties", ex, "layers", new List<object?>(), "items", Items(k).Cast<object?>().ToList(), "key", k);
                outl.Add(d);
            }
        }
        return outl.OrderBy(x => Py.Str(x["kind"]) switch { "ScreenView" => 0, "Template" => 1, _ => 2 })
            .ThenBy(x => Py.Str(x["group"]), StringComparer.Ordinal).ThenBy(x => Py.Str(Py.Or(x["name"], "")), StringComparer.Ordinal)
            .Cast<object?>().ToList();
    }

    /// <summary>Items of a screen in drawing order: per layer from the lowest item along UpperRelation (the item above),
    /// items not on such a chain at the end in key order.</summary>
    List<string> Ordered(string screen)
    {
        var items = p.Targets(screen, "Children").Where(k => p.Objects.ContainsKey(k) && Short(k) is "HmiScreenItemData" or "HmiScreenModuleInstanceData").Distinct().ToList();
        var set = items.ToHashSet();
        var below = new HashSet<string>();
        foreach (var k in items)
            foreach (var u in p.Targets(k, "UpperRelation"))
                below.Add(u);
        var order = new List<string>();
        var seen = new HashSet<string>();
        foreach (var start in items.Where(k => !below.Contains(k)))
        {
            var cur = start;
            while (cur is not null && set.Contains(cur) && seen.Add(cur))
            {
                order.Add(cur);
                cur = p.Targets(cur, "UpperRelation").FirstOrDefault();
            }
        }
        order.AddRange(items.Where(k => !seen.Contains(k)));
        return order;
    }

    List<PyDict> Items(string screen)
    {
        var outl = new List<PyDict>();
        var order = Ordered(screen);
        for (var z = 0; z < order.Count; z++)
        {
            var k = order[z];
            if (Short(k) == "HmiScreenModuleInstanceData")
            {
                outl.Add(Faceplate(k, z));
                continue;
            }
            var c = Core(k);
            var props = new PyDict();
            foreach (var (n, v) in p.Sets(k).Set("IHmiScreenItemAttributes"))
                props[n] = Value(v);
            foreach (var (n, v) in Expandos(k, "IHmiScreenItemExpandoAttributes", "IHmiScreenItemExpandoReferencesAttributes"))
                props[n] = v;
            object? styleItem = null;
            if (props.Get("StyleItem") is PyDict si)
            {
                styleItem = si.Get("name");
                props.Pop("StyleItem");
            }
            var refs = p.Targets(k, "ExpandoReferences");
            var tags = refs.Where(t => Short(t) == "HmiTagData").Select(t => (object?)p.Name(t)).Distinct().ToList();
            var parent = m.Rel1(k, "ContainerParent") ?? p.Targets(k, "Parent").FirstOrDefault(x => p.Objects.ContainsKey(x) && Short(x) == "HmiScreenItemData");
            object text = props.Get("Text") is PyDict tx ? tx : new PyDict();
            var trends = Trends(k);
            outl.Add(PyDict.Of("name", c.Get("Name"), "type", c.Get("Subtype"), "left", props.Get("Left", 0L), "top", props.Get("Top", 0L),
                "width", props.Get("Width"), "height", props.Get("Height"), "backColor", props.Get("BackColor"),
                "text", text, "styleItem", styleItem, "properties", props, "tags", tags, "events", Events(k),
                "dynamizations", Dynamizations(k), "zIndex", (long)z,
                "parent", parent is not null ? p.Name(parent) : null,
                "layerIndex", props.Get("Layer", 0L), "key", k));
            if (trends.Count > 0)
                outl[^1]["trends"] = trends;
        }
        return outl;
    }

    /// <summary>Trends of a trend view (AssignedCurves -> HmiCurveData, sample C03b): name, color, the tag (TrendTag) or
    /// data log, and the curve settings (side, line width, trend type, samples).</summary>
    List<object?> Trends(string k)
    {
        var o = new List<object?>();
        foreach (var c in p.Targets(k, "AssignedCurves").Distinct())
        {
            var ex = Expandos(c, "IHmiCurveExpandoAttributes", "IHmiCurveExpandoReferencesAttributes");
            string? RefName(string n) => ex.Get(n) is PyDict r ? Py.Str(r.Get("name")) : null;
            o.Add(PyDict.Of("name", p.Name(c), "color", Value(p.Sets(c).Set("IHmiCurveAttributes").Get("Color")),
                "tag", RefName("TrendTag"), "log", RefName("Archive"), "side", ex.Get("Side"), "lineWidth", ex.Get("TrendLineWidth"),
                "trendType", ex.Get("TrendType"), "samples", ex.Get("Samples"), "displayMode", ex.Get("CurveDisplayMode")));
        }
        return o;
    }

    readonly Dictionary<string, PyDict> faceplateTypes = [];

    /// <summary>A faceplate instance (HmiScreenModuleInstanceData, sample C06): position and size, the type version
    /// (IsInstanceOf -> HmiScreenModuleVersionData), the interface values of the instance (tag connections
    /// IDynamicLinkAttributes Dyn.&lt;id&gt;.Tag#, static values and texts as expandos named by the property id), its own
    /// animations, and the type content (the screen of the type's module device with its items, the interface and
    /// the connections of the interface properties to inner object properties).</summary>
    PyDict Faceplate(string k, int z)
    {
        var c = Core(k);
        var props = new PyDict();
        foreach (var (n, v) in p.Sets(k).Set("IHmiScreenItemAttributes"))
            props[n] = Value(v);
        foreach (var (n, v) in Expandos(k, "IHmiScreenItemExpandoAttributes", "IHmiScreenItemExpandoReferencesAttributes"))
            props[n] = v;
        props.Pop("ScreenModuleType");
        props.Pop("Resizing");
        var version = p.Targets(k, "IsInstanceOf").FirstOrDefault();
        var type = version is not null ? FaceplateType(version) : null;
        var values = new PyDict();
        var dyns = new List<object?>();
        foreach (var d in Dynamizations(k))
        {
            if (d is PyDict dd && Py.Str(dd.Get("property")).StartsWith("Guid_", StringComparison.Ordinal))
                values[Py.Str(dd.Get("property"))] = PyDict.Of("tag", dd.Get("tag"));
            else
                dyns.Add(d);
        }
        foreach (var n in props.Keys.Where(x => Py.Str(x).StartsWith("Guid_", StringComparison.Ordinal)).ToList())
        {
            var v = props.Pop(n);
            if (!values.ContainsKey(n))
                values[n] = v is PyDict t && !t.ContainsKey("kind") ? PyDict.Of("text", t) : PyDict.Of("value", v);
        }
        // interface values by property name (ids without a known property stay under their id)
        var iface = new PyDict();
        var byId = new Dictionary<string, string>();
        if (type?.Get("interface") is List<object?> defs)
            foreach (PyDict def in defs)
                byId[Py.Str(def.Get("id"))] = Py.Str(def.Get("name"));
        foreach (var (id, v) in values)
            iface[byId.TryGetValue(Py.Str(id), out var nm) ? nm : id] = v;
        // instance values of inner objects (IAdditionalAttributes "__IS|V01|<index>|<kind>|<property id>", index = rank
        // of the inner object's name): the size of auto-sized objects (Width 167, Height 160), e.g. the title text
        // field, which the editor sizes per instance and device font (sample C06)
        var overrides = new PyDict();
        if (type?.Get("items") is List<object?> innerItems)
        {
            var names = innerItems.OfType<PyDict>().Select(x => Py.Str(x.Get("name"))).OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var (n, v) in p.Sets(k).Set("IAdditionalAttributes"))
            {
                var parts = Py.Str(n).Split('|');
                if (parts.Length != 5 || parts[0] != "__IS" || !int.TryParse(parts[2], out var idx) || idx < 0 || idx >= names.Count || !Py.IsInt(v))
                    continue;
                var prop = parts[4] switch { "167" => "Width", "160" => "Height", _ => null };
                if (prop is null)
                    continue;
                if (overrides.Get(names[idx]) is not PyDict o)
                    overrides[names[idx]] = o = new PyDict();
                o[prop] = v;
            }
        }
        var parent = m.Rel1(k, "ContainerParent");
        return PyDict.Of("name", c.Get("Name"), "type", "FaceplateInstance", "left", props.Get("Left", 0L), "top", props.Get("Top", 0L),
            "width", props.Get("Width"), "height", props.Get("Height"), "backColor", null, "text", new PyDict(), "styleItem", null,
            "properties", props, "tags", dyns.OfType<PyDict>().Select(d => d.Get("tag")).Concat(iface.Values.OfType<PyDict>().Select(v => v.Get("tag")))
                .Where(t => t is not null).Distinct().ToList(),
            "events", Events(k), "dynamizations", dyns, "zIndex", (long)z,
            "parent", parent is not null ? p.Name(parent) : null, "layerIndex", props.Get("Layer", 0L),
            "faceplate", type, "interface", iface, "itemOverrides", overrides, "key", k);
    }

    /// <summary>Content of a classic faceplate type version: name and version, the interface (categories with their
    /// properties: name, id, .NET type or dynamic data type, animatable, the inner object properties it is connected
    /// to) and the items of the type's screen (in the coordinates of the faceplate editor, origin = top left of the
    /// items' bounding box).</summary>
    PyDict? FaceplateType(string version)
    {
        if (faceplateTypes.TryGetValue(version, out var known))
            return known;
        var typeObj = p.Targets(version, "VersionObjectToTypeObject").FirstOrDefault();
        var iface = new List<object?>();
        foreach (var pi in p.Targets(version, "IHmiScreenModuleTypeAttributes_PropertyInterface2").Take(1))
            foreach (var cat in p.Targets(pi, "InterfaceCategories"))
                foreach (var ip in p.Targets(cat, "InterfaceProperties"))
                {
                    var a = p.Sets(ip);
                    var b = a.Set("IHmiScreenModulePropertyInterfacePropertyBaseAttributes");
                    var t = Py.Str(Py.Or(a.Set("IHmiScreenModulePropertyInterfacePropertyAttributes").Get("Type"), ""));
                    var dt = p.Targets(ip, "IHmiScreenModulePropertyInterfacePropertyBaseAttributes_DynamicDataType").FirstOrDefault();
                    var conns = new List<object?>();
                    foreach (var pc in p.Targets(ip, "PropertyConnections"))
                    {
                        var inner = p.Targets(pc, "IHmiScreenModulePropertyInterfacePropertyConnectionAttributes_InnerObject").FirstOrDefault();
                        conns.Add(PyDict.Of("item", inner is not null ? p.Name(inner) : null,
                            "property", p.Attr(pc, "IHmiScreenModulePropertyInterfacePropertyConnectionAttributes", "InnerObjectPropertyName")));
                    }
                    var related = p.Targets(ip, "IHmiScreenModulePropertyInterfacePropertyBaseAttributes_RelatedItem").FirstOrDefault();
                    iface.Add(PyDict.Of("name", p.Name(ip), "category", p.Name(cat), "id", b.Get("Id"),
                        "type", t.Length > 0 ? t.Split(',')[0] : null, "dynamicType", dt is not null ? p.Name(dt) : null,
                        "animatable", b.Get("Animatable"), "direction", a.Set("IHmiScreenModulePropertyInterfacePropertyAttributes").Get("PropertyDirection"),
                        "tag", related is not null ? p.Name(related) : null, "connections", conns));
                }
        var items = new List<object?>();
        double? ox = null, oy = null;
        // the module device of the version (Parts -> DeviceData -> DeviceItems) holds the application with the screen
        var apps = p.Targets(version, "Parts").Where(x => Short(x) == "DeviceData").SelectMany(x => p.Targets(x, "DeviceItems"))
            .Concat(m.Sources(version, "Environment")).Where(a => Short(a) == "HmiApplicationData").Distinct();
        var screen = apps.SelectMany(a => m.Sources(a, "Target")).FirstOrDefault(s => p.Objects.ContainsKey(s) && Short(s) == "HmiScreenData");
        if (screen is not null)
        {
            foreach (var it in Items(screen))
            {
                items.Add(it);
                if (Py.IsInt(it.Get("left")) && Py.IsInt(it.Get("top")))
                {
                    ox = Math.Min(ox ?? double.MaxValue, Py.Long(it.Get("left")));
                    oy = Math.Min(oy ?? double.MaxValue, Py.Long(it.Get("top")));
                }
            }
        }
        var d = PyDict.Of("name", typeObj is not null ? p.Name(typeObj) : null,
            "version", Faceplates.DecodeVersionText(p.Attr(version, "IVersionInfo", "Version")),
            "interface", iface, "originLeft", ox is { } x ? (long)x : 0L, "originTop", oy is { } y ? (long)y : 0L,
            "items", items, "key", version);
        return faceplateTypes[version] = d;
    }

    const string DynPrefix = "Siemens.Simatic.Hmi.Utah.Dynamics.";

    /// <summary>Class name of a dynamic attribute blob (length-prefixed .NET type name) and the offset after it.</summary>
    static (string Name, int End)? DynClass(byte[] b)
    {
        var pre = System.Text.Encoding.ASCII.GetBytes(DynPrefix);
        var i = b.AsSpan().IndexOf(pre);
        if (i < 0)
            return null;
        var e = i + pre.Length;
        while (e < b.Length && (char.IsAsciiLetterOrDigit((char)b[e]) || b[e] == '.'))
            e++;
        return (System.Text.Encoding.ASCII.GetString(b, i + pre.Length, e - i - pre.Length), e);
    }

    /// <summary>Events of an item: IDynamicAttributes of class EventScript (Dyn.Click …) with the function list they
    /// call.</summary>
    List<object?> Events(string k)
    {
        var o = new List<object?>();
        foreach (var (n, v) in p.Sets(k).Set("IDynamicAttributes"))
        {
            if (v is byte[] b && DynClass(b) is { } dc && dc.Name != "EventScript")
                continue;
            var name = Py.Str(n);
            var ev = name.StartsWith("Dyn.", StringComparison.Ordinal) ? name[4..] : name;
            o.Add(PyDict.Of("name", ev, "kind", ev, "language", "FunctionList", "code", null));
        }
        return o;
    }

    /// <summary>BGRA bytes -> "0xAARRGGBB".</summary>
    static string Bgra(byte[] b, int o) => $"0x{b[o + 3]:X2}{b[o + 2]:X2}{b[o + 1]:X2}{b[o]:X2}";

    static readonly string[] FlashRates = ["None", "Slow", "Medium", "Fast"];

    /// <summary>Animations and tag connections of an item (IDynamicAttributes Dyn.* with the tags of
    /// IDynamicLinkAttributes Dyn.*.Tag#, sample C03):
    /// Appearance (mode Range / Bits / Bit, entries lo..hi with back and fore color and flashing),
    /// Visibility and ObjectEnable (Range lo..hi or Bit), Horizontal/Vertical/DiagonalMovement (lo..hi -> dx, dy),
    /// DirectMovement (tagX, tagY) and PropertyTag (property = tag).</summary>
    List<object?> Dynamizations(string k)
    {
        var o = new List<object?>();
        var links = p.Sets(k).Set("IDynamicLinkAttributes");
        string? Tag(string n) => links.Get(n) is byte[] lb && RefTarget(k, lb) is { } t ? p.Name(t) : null;
        foreach (var (n, v) in p.Sets(k).Set("IDynamicAttributes"))
        {
            if (v is not byte[] b || DynClass(b) is not { } dc)
                continue;
            var name = Py.Str(n);
            var prop = name.StartsWith("Dyn.", StringComparison.Ordinal) ? name[4..] : name;
            var cls = dc.Name.StartsWith("PropertyViews.", StringComparison.Ordinal) ? dc.Name["PropertyViews.".Length..] : dc.Name;
            try
            {
                switch (cls)
                {
                    case "PropertyTag":
                        o.Add(PyDict.Of("type", "Tag", "property", prop, "tag", Tag(name + ".Tag#")));
                        break;
                    case "Appearance":
                    {
                        var mode = Bin.U16(b, 10) switch { 1 => "Bits", 2 => "Bit", _ => "Range" };
                        var e = dc.End;
                        // after the class name: u8 1, u32 length, u32 count, count offsets, the entries (u32 size,
                        // back BGRA, u16 flashing, fore BGRA, i64 from, i64 to)
                        var count = (int)Bin.U32(b, e + 5);
                        var pos = e + 9 + 4 * count;
                        var entries = new List<object?>();
                        for (var i = 0; i < count && pos + 30 <= b.Length; i++)
                        {
                            var size = (int)Bin.U32(b, pos);
                            var fl = Bin.U16(b, pos + 8);
                            entries.Add(PyDict.Of("backColor", Bgra(b, pos + 4), "foreColor", Bgra(b, pos + 10),
                                "flashing", fl < FlashRates.Length ? FlashRates[fl] : (object)(long)fl,
                                "from", Bin.I64(b, pos + 14), "to", Bin.I64(b, pos + 22)));
                            pos += Math.Max(size, 30);
                        }
                        o.Add(PyDict.Of("type", "Appearance", "property", prop, "tag", Tag(name + ".Tag#"), "mode", mode,
                            "bit", (long)Bin.U16(b, 8), "entries", entries));
                        break;
                    }
                    case "Visibility":
                        o.Add(PyDict.Of("type", "Visibility", "property", prop, "tag", Tag(name + ".Tag#"),
                            "mode", Bin.U16(b, 10) == 1 ? "Bit" : "Range", "from", Bin.I64(b, 12), "to", Bin.I64(b, 20),
                            "visible", b[28] != 0));
                        break;
                    case "ObjectEnable":
                        o.Add(PyDict.Of("type", "ObjectEnable", "property", prop, "tag", Tag(name + ".Tag#"),
                            "mode", Bin.U16(b, 12) == 1 ? "Bit" : "Range", "from", Bin.I64(b, 14), "to", Bin.I64(b, 22),
                            "enabled", Bin.U16(b, 10) == 0));
                        break;
                    case "HorizontalMovement" or "VerticalMovement" or "DiagonalMovement":
                        o.Add(PyDict.Of("type", "Movement", "property", prop, "tag", Tag(name + ".Tag#"),
                            "from", Bin.I64(b, 8), "to", Bin.I64(b, 16), "dx", Bin.I64(b, 24), "dy", Bin.I64(b, 32)));
                        break;
                    case "DirectMovement":
                        o.Add(PyDict.Of("type", "DirectMovement", "property", prop, "tagX", Tag(name + ".TagX#"), "tagY", Tag(name + ".TagY#")));
                        break;
                    case "EventScript":
                        break;
                    default:
                        o.Add(PyDict.Of("type", cls, "property", prop, "tag", Tag(name + ".Tag#")));
                        break;
                }
            }
            catch (Exception ex) when (ex is StructError or ArgumentException or IndexOutOfRangeException)
            {
                o.Add(PyDict.Of("type", cls, "property", prop, "tag", Tag(name + ".Tag#")));
            }
        }
        return o;
    }

    // --- recipes --------------------------------------------------------------------------------------------------

    /// <summary>Recipes of a classic device (HmiRecipeCoreData, sample C03b): name, number, display name, storage, the
    /// elements (HmiRecipeEntryData, by RecipeIndex: name, display name, tag, data type, decimal places, limits,
    /// default) and the data records (HmiRecipeDataRecordData, by number: name, display name and the values by element
    /// index, stored as IHmiRecipeDataExpandoAttributes X&lt;index&gt;Y0001).</summary>
    public List<object?> Recipes(string app)
    {
        string? Text(string k, string set, string name) =>
            p.Sets(k).Set(set).Get(name) is byte[] b && RefTarget(k, b) is { } t && TextOf(t) is { Count: > 0 } tx ? Py.Str(tx.Values.First()) : null;
        string? DisplayName(string k, string set, string relation) =>
            p.Targets(k, relation).FirstOrDefault() is { } t && TextOf(t) is { Count: > 0 } tx ? Py.Str(tx.Values.First()) : null;
        var outl = new List<object?>();
        foreach (var r in Of(app, "HmiRecipeCoreData"))
        {
            var ra = p.Sets(r).Set("IHmiRecipeAttributes");
            var elements = new List<PyDict>();
            foreach (var e in p.Targets(r, "HmiRecipeEntryDataChildren").Distinct())
            {
                var ea = p.Sets(e).Set("IHmiRecipeEntryAttributes");
                var tag = p.Targets(e, "IHmiRecipeEntryAttributes_Tag").FirstOrDefault();
                elements.Add(PyDict.Of("name", p.Name(e), "displayName", Text(e, "ITextAttributes", "DisplayName"),
                    "index", ea.Get("RecipeIndex"), "tag", tag is not null ? p.Name(tag) : null, "dataType", ea.Get("HmiDataType"),
                    "decimalPlaces", ea.Get("DecimalPlaces"), "min", ea.Get("MinValue"), "max", ea.Get("MaxValue"),
                    "default", p.Sets(e).Set("ICoreExpandoAttributes").Get("DefaultValue")));
            }
            var records = new List<PyDict>();
            foreach (var d in p.Targets(r, "HmiRecipeDataRecordDataChildren").Distinct())
            {
                var values = new PyDict();
                foreach (var (n, v) in p.Sets(d).Set("IHmiRecipeDataExpandoAttributes"))
                {
                    var key = Py.Str(n);
                    if (key.Length == 10 && key[0] == 'X' && key[5] == 'Y' && int.TryParse(key[1..5], out var x))
                        values[x.ToString(System.Globalization.CultureInfo.InvariantCulture)] = v;
                }
                records.Add(PyDict.Of("name", p.Name(d), "number", p.Sets(d).Set("IHmiRecipeDataAttributes").Get("Number"),
                    "displayName", DisplayName(d, "IHmiRecipeDataAttributes", "IHmiRecipeDataAttributes_DisplayName"), "values", values));
            }
            outl.Add(PyDict.Of("name", p.Name(r), "number", ra.Get("Number"),
                "displayName", DisplayName(r, "IHmiRecipeAttributes", "IHmiRecipeAttributes_DisplayName"),
                "path", ra.Get("PathName"), "communication", ra.Get("CommunicationType"), "maxRecords", ra.Get("MaxRecordCount"),
                "elements", elements.OrderBy(x => Py.IsInt(x.Get("index")) ? Py.Long(x.Get("index")) : long.MaxValue).Cast<object?>().ToList(),
                "records", records.OrderBy(x => Py.IsInt(x.Get("number")) ? Py.Long(x.Get("number")) : long.MaxValue).Cast<object?>().ToList(),
                "key", r));
        }
        return outl;
    }

    // --- style ----------------------------------------------------------------------------------------------------

    /// <summary>Style of the device: the selected panel style (HmiStyleAndThemeSettingsData
    /// IHmiStyleSettingsAttributes_SelectedPanelStyle -> HmiStyleVersionData) and the style items of the device's
    /// DeviceStyleInstance (HmiStyleData with Target = app) by their Subtype, which is the screen item type.</summary>
    public PyDict? Style(string app)
    {
        var settings = Of(app, "HmiStyleAndThemeSettingsData").FirstOrDefault();
        var sa = settings is not null ? p.Sets(settings).Set("IHmiStyleSettingsAttributes") : new PyDict();
        var selected = settings is not null ? p.Targets(settings, "IHmiStyleSettingsAttributes_SelectedPanelStyle").FirstOrDefault() : null;
        var inst = Of(app, "HmiStyleData").FirstOrDefault(k => p.Name(k) == "DeviceStyleInstance") ?? Of(app, "HmiStyleData").FirstOrDefault();
        if (inst is null && selected is null)
            return null;
        var values = new PyDict();
        var items = inst is not null ? Of(app, "HmiStyleItemData_V13SP1").Where(k => p.Targets(k, "StyleItem2Style").Contains(inst)) : [];
        foreach (var k in items)
        {
            var v = Expandos(k, "IHmiStyleItemExpandoAttributes", "IHmiStyleItemExpandoReferencesAttributes");
            v.Pop("Guid");
            v["StyleItemName"] = p.Name(k);
            values[Py.Str(Py.Or(Core(k).Get("Subtype"), p.Name(k)))] = v;
        }
        // the styles of the project (library style versions, e.g. WinCC Dark / Fresh / Light / Wireframe, sample C04):
        // their style items by Subtype; the device instance above is a copy of the selected one with device fonts
        var library = new PyDict();
        var styleItems = p.Objects.Keys.Where(k => Short(k) == "HmiStyleItemData_V13SP1").ToList();
        foreach (var ver in Py.Sorted(p.Objects.Keys.Where(k => Short(k) == "HmiStyleVersionData")))
        {
            var sd = p.Targets(ver, "Parts").FirstOrDefault(x => Short(x) == "HmiStyleData");
            if (sd is null)
                continue;
            var lv = new PyDict();
            foreach (var k in styleItems.Where(k => p.Targets(k, "StyleItem2Style").Contains(sd)))
            {
                var v = Expandos(k, "IHmiStyleItemExpandoAttributes", "IHmiStyleItemExpandoReferencesAttributes");
                v.Pop("Guid");
                v["StyleItemName"] = p.Name(k);
                lv[Py.Str(Py.Or(Core(k).Get("Subtype"), p.Name(k)))] = v;
            }
            if (lv.Count > 0)
                library[p.Name(ver) ?? ver] = lv;
        }
        var o = PyDict.Of("name", selected is not null ? p.Name(selected) : null, "platform", "Classic",
            "adjustFontsToDeviceSize", sa.Get("AdjustStyleFontsToDeviceSize"), "useDefaultPanelStyle", sa.Get("UseDefaultPanelStyle"),
            "values", values, "templates", new PyDict(), "library", library);
        return o;
    }
}
