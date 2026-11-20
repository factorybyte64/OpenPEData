using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace OpenPEData.Viewer;

/// <summary>What a tree node stands for; selects icon and detail views.</summary>
public enum NodeKind
{
    Project, Folder, Device, Plc, Hmi, BlocksFolder, Block, TagsFolder, AllTags, TagTable, TypesFolder, PlcType,
    ScreensFolder, Screen, Connections, Alarms, TextLists, Cycles, Libraries, Library, LibType, LibVersion,
    MasterCopy, Instances, Faceplate, Statistics, Verify, CrossRefs, ProgramInfo, Info,
    Hardware, HwItem, Safety, Network, Umac, PlcSection, WatchTable,
}

public sealed class TreeNode : INotifyPropertyChanged
{
    public TreeNode(string name, NodeKind kind, JsonNode? data = null, string? suffix = null)
    {
        Name = name;
        Kind = kind;
        Data = data;
        Suffix = suffix;
    }

    public string Name { get; }
    private string? _suffix;
    public string? Suffix
    {
        get => _suffix;
        set
        {
            Set(ref _suffix, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        }
    }
    public string Display => string.IsNullOrEmpty(Suffix) ? Name : $"{Name} [{Suffix}]";
    public NodeKind Kind { get; }
    /// <summary>The JSON object shown in the detail pane (set later for nodes filled by a background parser run).</summary>
    public JsonNode? Data { get; set; }
    /// <summary>List properties of <see cref="Data"/> that are already children in the tree (no extra table tab).</summary>
    public HashSet<string> ListsInTree { get; } = [];
    public ObservableCollection<TreeNode> Children { get; } = [];
    public TreeNode? Parent { get; private set; }

    public string Glyph => Kind switch
    {
        NodeKind.Project => "",
        NodeKind.Folder or NodeKind.BlocksFolder or NodeKind.TagsFolder or NodeKind.TypesFolder
            or NodeKind.ScreensFolder or NodeKind.Libraries => "",
        NodeKind.Device or NodeKind.Hardware or NodeKind.HwItem => "",
        NodeKind.Plc => "",
        NodeKind.Hmi => "",
        NodeKind.Block => "",
        NodeKind.AllTags or NodeKind.TagTable or NodeKind.WatchTable => "",
        NodeKind.PlcType => "",
        NodeKind.Screen => "",
        NodeKind.Connections or NodeKind.Network => "",
        NodeKind.Alarms or NodeKind.PlcSection or NodeKind.Safety or NodeKind.Umac => "",
        NodeKind.TextLists => "",
        NodeKind.Cycles => "",
        NodeKind.Library => "",
        NodeKind.LibType or NodeKind.Faceplate => "",
        NodeKind.LibVersion => "",
        NodeKind.MasterCopy => "",
        NodeKind.Instances => "",
        NodeKind.Statistics => "",
        NodeKind.Verify => "",
        NodeKind.CrossRefs or NodeKind.ProgramInfo => "",
        _ => "",
    };

    public TreeNode Add(TreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private bool _isVisible = true;
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    /// <summary>Shows nodes whose name contains the text, plus their ancestors; returns true if this subtree matches.</summary>
    public bool ApplyFilter(string text)
    {
        var self = text.Length == 0 || Display.Contains(text, StringComparison.OrdinalIgnoreCase);
        var any = false;
        foreach (var c in Children)
            any |= c.ApplyFilter(self && text.Length > 0 ? "" : text);
        IsVisible = self || any;
        if (text.Length > 0 && any) IsExpanded = true;
        return IsVisible;
    }

    public string PathText
    {
        get
        {
            var parts = new List<string>();
            for (var n = this; n != null; n = n.Parent) parts.Add(n.Display);
            parts.Reverse();
            return string.Join("  ›  ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Builds a TIA-Portal-like project tree from the parser JSON.</summary>
public static class TreeBuilder
{
    public static TreeNode Build(JsonNode root, string fileName)
    {
        ScreenRenderer.SetGraphics(root["project"]?["graphics"], root["library"]);
        ScreenRenderer.SetLibraryTextLists(root["library"]);
        ScreenRenderer.SetFaceplates(root["faceplates"]);
        var project = root["project"]!;
        var top = new TreeNode(Str(project["name"]) ?? fileName, NodeKind.Project, project);
        top.ListsInTree.UnionWith(["devices", "deviceGroups", "network", "umac", "plcAlarms", "plcWatchTables", "plcUnits",
            "plcOpcUaInterfaces", "plcSupervisions", "plcTextLists", "plantViews"]);
        top.IsExpanded = true;

        // device groups as folders
        var groups = new Folders(top, NodeKind.Folder);
        foreach (var g in Arr(project["deviceGroups"]))
            groups.Get(Str(g["path"]), g);
        foreach (var dev in Arr(project["devices"]))
            AddDevice(groups.Get(Str(dev["group"])), dev, project);

        AddNetwork(top, project);
        if (project["umac"] is JsonObject um && (Arr(um["users"]).Any() || Arr(um["roles"]).Any()))
        {
            // role assignments of a user as text ("Role (device)") so that the user table shows them
            var data = (JsonObject)um.DeepClone();
            foreach (var u in Arr(data["users"]))
                u["roles"] = new JsonArray(Arr(u["roles"]).Select(r => (JsonNode?)JsonValue.Create(
                    Str(r["role"]) + (Str(r["device"]) is { } d ? $" ({d})" : ""))).ToArray());
            top.Add(new TreeNode("User management (UMAC)", NodeKind.Umac, data,
                $"{Arr(um["users"]).Count()} users, {Arr(um["roles"]).Count()} roles"));
        }

        // plant view (one per project): its nodes as one table with their paths
        foreach (var pv in Arr(project["plantViews"]))
            top.Add(new TreeNode("Plant view " + (Str(pv["name"]) ?? ""), NodeKind.PlcSection, pv,
                Arr(pv["nodes"]).Count() + " nodes"));

        AddLibrary(top, root);

        if (root["statistics"] is JsonObject st)
            top.Add(new TreeNode("Statistics", NodeKind.Statistics, st));
        SystemFoldersLast(top);
        return top;
    }

    private static void SystemFoldersLast(TreeNode n)
    {
        var sys = n.Children.Where(c => c.Data == null && SystemFolderNames.ContainsValue(c.Name)).ToList();
        foreach (var s in sys) n.Children.Move(n.Children.IndexOf(s), n.Children.Count - 1);
        foreach (var c in n.Children) SystemFoldersLast(c);
    }

    private static void AddDevice(TreeNode parent, JsonNode dev, JsonNode project)
    {
        var plcs = Arr(dev["plcs"]).ToList();
        var hmis = Arr(dev["hmi"]).ToList();
        var name = Str(dev["name"]) ?? "?";
        // TIA shows a station with a single CPU/HMI as one node "PLC_1 [CPU 1513-1 PN]".
        if (plcs.Count + hmis.Count == 1)
        {
            var n = plcs.Count == 1 ? AddPlc(parent, plcs[0], name, project) : AddHmi(parent, hmis[0], name);
            AddHardware(n, dev);
            return;
        }
        var d = parent.Add(new TreeNode(name, NodeKind.Device, dev, Str(dev["typeName"])));
        d.ListsInTree.UnionWith(["plcs", "hmi", "hardware"]);
        foreach (var p in plcs) AddPlc(d, p, null, project);
        foreach (var h in hmis) AddHmi(d, h, null);
        AddHardware(d, dev);
    }

    /// <summary>Device configuration of a station: the device item tree with parameters, nodes, addresses and
    /// certificates (parser "hardware"), and the safety administration of an F-CPU.</summary>
    private static void AddHardware(TreeNode station, JsonNode dev)
    {
        if (dev["hardware"] is not JsonObject hw || !Arr(hw["items"]).Any()) return;
        var hn = station.Add(new TreeNode("Device configuration", NodeKind.Hardware, hw, Str(hw["catalog"])?.Split('\\').LastOrDefault()));
        hn.ListsInTree.UnionWith(["items", "safety"]);
        void Items(TreeNode parent, JsonNode? items)
        {
            foreach (var it in Arr(items))
            {
                var pos = it["positionNumber"]?.ToString();
                var info = Str(it["orderNumber"]) ?? Str(it["typeName"]) ?? Str(it["subtype"]);
                var n = parent.Add(new TreeNode(Str(it["name"]) ?? "?", NodeKind.HwItem, it,
                    string.IsNullOrEmpty(pos) ? info : $"{pos}: {info}"));
                n.ListsInTree.Add("items");
                Items(n, it["items"]);
            }
        }
        Items(hn, hw["items"]);
        if (hw["safety"] is JsonObject sf)
            station.Add(new TreeNode("Safety administration", NodeKind.Safety, sf,
                Str(sf["programSettings"]?["SafetySystemVersion"])));
    }

    /// <summary>Network view of the project: subnets, IO systems, domains, connections and port interconnections.</summary>
    private static void AddNetwork(TreeNode top, JsonNode project)
    {
        if (project["network"] is not JsonObject net) return;
        var data = (JsonObject)net.DeepClone();
        var links = new JsonArray();
        foreach (var l in Arr(net["portLinks"]))
            if (l is JsonArray pair && pair.Count == 2)
                links.Add(new JsonObject { ["port"] = Str(pair[0]), ["partnerPort"] = Str(pair[1]) });
        data["portLinks"] = links;
        var lists = new[] { ("subnets", "Subnets"), ("ioSystems", "IO systems"), ("syncDomains", "Sync and MRP domains"),
            ("connections", "Connections"), ("portLinks", "Port interconnections") };
        if (!lists.Any(l => Arr(data[l.Item1]).Any())) return;
        var n = top.Add(new TreeNode("Network", NodeKind.Network, data));
        foreach (var (key, label) in lists)
        {
            n.ListsInTree.Add(key);
            if (Arr(data[key]).Any()) n.Add(Sub(label, NodeKind.Network, data, key));
        }
    }

    /// <summary>PLC objects the parser outputs at project level (keyed by the PLC name): alarms, text lists, watch and
    /// force tables, software units, OPC UA server interfaces, ProDiag supervisions.</summary>
    private static void AddPlcSections(TreeNode n, JsonNode project, string? plcName)
    {
        bool MinePlc(JsonNode x) => plcName == null || Str(x["plc"]) == plcName;
        JsonArray Clone(IEnumerable<JsonNode> xs) => new(xs.Select(x => x.DeepClone()).ToArray());

        var pa = project["plcAlarms"];
        var al = Arr(pa?["alarms"]).Where(MinePlc).ToList();
        if (al.Count > 0)
            n.Add(new TreeNode("PLC alarms", NodeKind.PlcSection,
                new JsonObject { ["alarms"] = Clone(al), ["alarmClasses"] = pa?["alarmClasses"]?.DeepClone() }, al.Count.ToString()));
        var tl = Arr(project["plcTextLists"]).Where(x => MinePlc(x) && Str(x["kind"]) == "user").ToList();
        if (tl.Count > 0)
        {
            var f = n.Add(new TreeNode("PLC alarm text lists", NodeKind.TagsFolder, null, tl.Count.ToString()));
            foreach (var t in tl.OrderBy(x => Str(x["name"]) ?? "", TiaOrder.Instance))
                f.Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.TextLists, t, Str(t["listRange"])));
        }
        var wt = Arr(project["plcWatchTables"]).Where(MinePlc).ToList();
        if (wt.Count > 0)
        {
            var f = n.Add(new TreeNode("Watch and force tables", NodeKind.TagsFolder, null, wt.Count.ToString()));
            foreach (var t in wt.OrderBy(x => Str(x["kind"]) == "force").ThenBy(x => Str(x["name"]) ?? "", TiaOrder.Instance))
                f.Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.WatchTable, t,
                    (Str(t["kind"]) == "force" ? "Force table, " : "") + Arr(t["entries"]).Count()));
        }
        foreach (var (key, label, list) in new[] { ("plcUnits", "Software units", "units"),
                     ("plcOpcUaInterfaces", "OPC UA server interfaces", "interfaces") })
        {
            var xs = Arr(project[key]).Where(MinePlc).ToList();
            if (xs.Count > 0)
                n.Add(new TreeNode(label, NodeKind.PlcSection, new JsonObject { [list] = Clone(xs) }, xs.Count.ToString()));
        }
        var sv = new JsonArray();
        foreach (var c in Arr(project["plcSupervisions"]))
            foreach (var s in Arr(c["supervisions"]))
            {
                var o = new JsonObject { ["proDiagFb"] = Str(c["proDiagFb"]) };
                foreach (var kv in s.AsObject()) o[kv.Key] = kv.Value?.DeepClone();
                sv.Add(o);
            }
        if (sv.Count > 0)
            n.Add(new TreeNode("ProDiag supervisions", NodeKind.PlcSection, new JsonObject { ["supervisions"] = sv }, sv.Count.ToString()));
    }

    private static TreeNode AddPlc(TreeNode parent, JsonNode plc, string? stationName, JsonNode project)
    {
        var n = parent.Add(new TreeNode(stationName ?? Str(plc["name"]) ?? "PLC", NodeKind.Plc, plc, Str(plc["typeName"])));
        n.ListsInTree.UnionWith(["blocks", "tagTables", "types"]);

        var blocks = n.Add(new TreeNode("Program blocks", NodeKind.BlocksFolder, plc));
        var bf = new Folders(blocks, NodeKind.BlocksFolder);
        var yf0 = new List<JsonNode>();
        foreach (var b in ByName(plc["blocks"]).OrderBy(BlockRank))
        {
            // system data types (TIA: PLC data types > System data types) are listed with the blocks
            if ((Str(b["group"]) ?? "").StartsWith("SystemDataTypeFolder", StringComparison.Ordinal))
            {
                yf0.Add(b);
                continue;
            }
            var type = Str(b["type"]) ?? "";
            var num = b["number"]?.ToString();
            var label = type switch { "GlobalDB" or "InstanceDB" => "DB", "ArrayDB" => "DB", _ => type };
            var bn = bf.Get(Str(b["group"])).Add(new TreeNode(Str(b["name"]) ?? "?", NodeKind.Block, b,
                string.IsNullOrEmpty(num) ? label : label + num));
            bn.ListsInTree.Add("networks");
        }
        bf.Finish();

        var tags = n.Add(new TreeNode("PLC tags", NodeKind.TagsFolder, plc));
        tags.Add(new TreeNode("Show all tags", NodeKind.AllTags, AllTags(Arr(plc["tagTables"]))));
        var tf = new Folders(tags, NodeKind.TagsFolder);
        foreach (var t in ByName(plc["tagTables"]))
            tf.Get(Str(t["group"])).Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.TagTable, t, Arr(t["tags"]).Count().ToString()));

        var types = n.Add(new TreeNode("PLC data types", NodeKind.TypesFolder, plc));
        var yf = new Folders(types, NodeKind.TypesFolder);
        foreach (var t in ByName(plc["types"]))
            yf.Get(Str(t["group"])).Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.PlcType, t));
        foreach (var t in yf0)
            yf.Get(Str(t["group"])).Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.PlcType, t, Str(t["type"])));
        yf.Finish();

        AddPlcSections(n, project, Str(plc["name"]));
        return n;
    }

    private static TreeNode AddHmi(TreeNode parent, JsonNode hmi, string? stationName)
    {
        var n = parent.Add(new TreeNode(stationName ?? Str(hmi["name"]) ?? "HMI", NodeKind.Hmi, hmi, Str(hmi["typeName"])));
        n.ListsInTree.UnionWith(["tagTables", "screens", "style"]);

        var tags = n.Add(new TreeNode("HMI tags", NodeKind.TagsFolder, hmi));
        tags.Add(new TreeNode("Show all tags", NodeKind.AllTags, AllTags(Arr(hmi["tagTables"]))));
        var tf = new Folders(tags, NodeKind.TagsFolder);
        foreach (var t in ByName(hmi["tagTables"]))
            tf.Get(Str(t["group"])).Add(new TreeNode(Str(t["name"]) ?? "?", NodeKind.TagTable, t, Arr(t["tags"]).Count().ToString()));

        ScreenRenderer.SetTextLists(hmi["textLists"]);
        var screens = n.Add(new TreeNode("Screens", NodeKind.ScreensFolder, hmi));
        var sf = new Folders(screens, NodeKind.ScreensFolder);
        foreach (var s in ByName(hmi["screens"]))
        {
            ScreenRenderer.SetStyle(s, hmi["style"] as JsonObject);
            sf.Get(Str(s["group"])).Add(new TreeNode(Str(s["name"]) ?? "?", NodeKind.Screen, s));
        }
        sf.Finish();

        n.Add(Sub("Connections", NodeKind.Connections, hmi, "connections"));
        n.Add(Sub("HMI alarms", NodeKind.Alarms, hmi, "discreteAlarms", "analogAlarms", "alarmClasses"));
        n.Add(Sub("Text and graphic lists", NodeKind.TextLists, hmi, "textLists", "graphicLists"));
        n.Add(Sub("Cycles", NodeKind.Cycles, hmi, "cycles"));
        // logging: data logs, alarm logs, audit trails, and the logging tags of all HMI tags as one list
        var lt = new JsonArray();
        foreach (var t in Arr(hmi["tagTables"]))
            foreach (var tag in Arr(t["tags"]))
                foreach (var l in Arr(tag["loggingTags"]))
                {
                    var o = new JsonObject { ["tag"] = Str(tag["name"]), ["tagTable"] = Str(t["name"]) };
                    foreach (var kv in l.AsObject()) o[kv.Key] = kv.Value?.DeepClone();
                    lt.Add(o);
                }
        var logging = Sub("Logging", NodeKind.PlcSection, hmi, "dataLogs", "alarmLogs", "auditTrails");
        if (lt.Count > 0) ((JsonObject)logging.Data!)["loggingTags"] = lt;
        if (logging.Data is JsonObject lo && lo.Count > 0) n.Add(logging);
        // global script modules: one node per module, its code as its own tab
        var sm = Arr(hmi["scriptModules"]).ToList();
        if (sm.Count > 0)
        {
            var f = n.Add(new TreeNode("Script modules", NodeKind.TagsFolder, null, sm.Count.ToString()));
            foreach (var m in sm)
                f.Add(new TreeNode(Str(m["name"]) ?? "?", NodeKind.PlcSection, m, Arr(m["functions"]).Count() + " functions"));
        }
        // runtime settings: only the values stored in the project (absent = default), one row per setting
        if (hmi["runtimeSettings"] is JsonObject rs)
        {
            var rows = new JsonArray { new JsonObject { ["section"] = "Application", ["setting"] = "GMPEnabled",
                ["value"] = rs["gmpEnabled"]?.DeepClone() } };
            foreach (var sec in (rs["sections"] as JsonObject ?? new JsonObject()))
                foreach (var kv in (sec.Value as JsonObject ?? new JsonObject()))
                    rows.Add(new JsonObject { ["section"] = sec.Key, ["setting"] = kv.Key, ["value"] = kv.Value?.DeepClone() });
            var data = new JsonObject { ["runtimeSettings"] = rows };
            if (hmi["runtimeLanguages"] is JsonArray rl && rl.Count > 0) data["runtimeLanguages"] = rl.DeepClone();
            n.Add(new TreeNode("Runtime settings", NodeKind.PlcSection, data, (rows.Count - 1).ToString()));
        }
        n.ListsInTree.UnionWith(["connections", "discreteAlarms", "analogAlarms", "alarmClasses", "textLists", "cycles",
            "dataLogs", "alarmLogs", "auditTrails", "scriptModules", "runtimeLanguages", "graphicLists"]);
        return n;
    }

    private static void AddLibrary(TreeNode top, JsonNode root)
    {
        var lib = root["library"];
        var faceplates = Arr(root["faceplates"]).ToList();
        if (lib == null && faceplates.Count == 0) return;
        var libs = top.Add(new TreeNode("Libraries", NodeKind.Libraries, lib));
        libs.ListsInTree.UnionWith(["libraries", "instances"]);
        var fpByName = faceplates.Where(f => Str(f["name"]) != null).ToDictionary(f => Str(f["name"])!, f => f);

        foreach (var L in Arr(lib?["libraries"]))
        {
            var ln = libs.Add(new TreeNode(Str(L["name"]) ?? "Library", NodeKind.Library, L));
            ln.ListsInTree.UnionWith(["types", "masterCopies", "versionsWithoutType", "systemFolders"]);
            ln.IsExpanded = true;

            var types = ln.Add(new TreeNode("Types", NodeKind.TypesFolder, L));
            var masters = ln.Add(new TreeNode("Master copies", NodeKind.TypesFolder, L));
            var tf = new Folders(types, NodeKind.TypesFolder);
            var mf = new Folders(masters, NodeKind.TypesFolder);
            // empty user folders exist only in systemFolders
            foreach (var sys in Arr(L["systemFolders"]))
            {
                var target = Str(sys["name"]) switch
                {
                    "LibraryTypeSystemFolder" => tf,
                    "LibraryMasterCopySystemFolder" => mf,
                    _ => null,
                };
                if (target != null) AddFolders(target, sys);
            }

            TreeNode? systemTypes = null;
            foreach (var t in ByName(L["types"]))
            {
                var system = Str(t["category"]) == "system";
                TreeNode parent;
                if (system)
                    parent = systemTypes ??= new TreeNode("System types (hidden in TIA)", NodeKind.TypesFolder, null);
                else
                    parent = tf.Get(Str(t["folderPath"]));
                var name = Str(t["name"]) ?? "?";
                var isFp = Str(t["type"]) == "FaceplateLibraryTypeData" && fpByName.ContainsKey(name);
                var data = isFp ? fpByName[name] : t;
                var tn = parent.Add(new TreeNode(name, isFp ? NodeKind.Faceplate : NodeKind.LibType, data,
                    isFp ? "Faceplate" : Str(t["opennessKind"]) ?? Str(t["subtype"])));
                tn.ListsInTree.Add("versions");
                if (isFp) fpByName.Remove(name);
                foreach (var v in Arr(data["versions"]))
                    tn.Add(new TreeNode("V" + (Str(v["version"]) ?? "?"), NodeKind.LibVersion, v, Str(v["state"])));
            }
            if (systemTypes != null) types.Add(systemTypes);

            foreach (var m in ByName(L["masterCopies"]))
                mf.Get(Str(m["folderPath"])).Add(new TreeNode(Str(m["name"]) ?? "?", NodeKind.MasterCopy, m, Str(m["subtype"])));

            if (Arr(L["versionsWithoutType"]).Any())
                ln.Add(Sub("Versions without type", NodeKind.Info, L, "versionsWithoutType"));
        }

        // faceplate types the library list did not contain, and containers without a type
        foreach (var f in fpByName.Values)
        {
            var tn = libs.Add(new TreeNode(Str(f["name"])!, NodeKind.Faceplate, f, "Faceplate"));
            tn.ListsInTree.Add("versions");
            foreach (var v in Arr(f["versions"]))
                tn.Add(new TreeNode("V" + (Str(v["version"]) ?? "?"), NodeKind.LibVersion, v, Str(v["state"])));
        }
        foreach (var f in faceplates.Where(f => Str(f["name"]) == null && Arr(f["instances"]).Any()))
            libs.Add(new TreeNode("Faceplate instances without type", NodeKind.Instances, f));

        if (lib?["instances"] is JsonArray inst && inst.Count > 0)
            libs.Add(new TreeNode("Type instances", NodeKind.Instances, new JsonObject { ["instances"] = inst.DeepClone() }, inst.Count.ToString()));
    }

    private static void AddFolders(Folders f, JsonNode folder)
    {
        foreach (var sub in Arr(folder["folders"]))
        {
            f.Get(Str(sub["path"]), sub);
            AddFolders(f, sub);
        }
    }

    /// <summary>Node whose data holds only the named lists of the object.</summary>
    private static TreeNode Sub(string name, NodeKind kind, JsonNode obj, params string[] lists)
    {
        var o = new JsonObject();
        var count = 0;
        foreach (var l in lists)
            if (obj[l] is JsonArray a)
            {
                o[l] = a.DeepClone();
                count += a.Count;
            }
        return new TreeNode(name, kind, o, count.ToString());
    }

    private static JsonObject AllTags(IEnumerable<JsonNode> tables)
    {
        var all = new JsonArray();
        foreach (var t in tables)
            foreach (var tag in Arr(t["tags"]))
            {
                var o = new JsonObject { ["tagTable"] = Str(t["name"]) };
                foreach (var kv in tag.AsObject()) o[kv.Key] = kv.Value?.DeepClone();
                all.Add(o);
            }
        return new JsonObject { ["tags"] = all };
    }

    private static IEnumerable<JsonNode> ByName(JsonNode? n) =>
        Arr(n).OrderBy(x => Str(x["name"]) ?? "", TiaOrder.Instance);

    /// <summary>Name order of the TIA project tree (TIA V21): case ignored, digit runs as
    /// numbers (FB8_C before FB50_C, DB75_C before DB302_C), '_' before letters (F_PS_… before F_PSV2…) – the Windows
    /// "logical" order of StrCmpLogicalW.</summary>
    internal sealed class TiaOrder : IComparer<string>
    {
        public static readonly TiaOrder Instance = new();
        [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string a, string b);
        public int Compare(string? a, string? b) => StrCmpLogicalW(a ?? "", b ?? "");
    }

    /// <summary>Blocks in a folder as TIA lists them: OB, FC, FB, DB, then by name.</summary>
    private static int BlockRank(JsonNode b) => (Str(b["type"]) ?? "") switch
    {
        "OB" => 0, "FC" => 1, "FB" => 2, "GlobalDB" or "InstanceDB" or "ArrayDB" or "DB" => 3, _ => 4,
    };

    /// <summary>Display names of TIA system folders, in the order TIA lists them; they come after user folders and
    /// objects (TIA V21 project tree: Program resources, STEP 7 Safety; in it the blocks, then F-I/O data blocks,
    /// Flexible F-Link, OPC UA Safety, Compiler blocks).</summary>
    private static readonly Dictionary<string, string> SystemFolderNames = new()
    {
        ["SystemBlocksFolder"] = "System blocks",
        ["ProgramResourcesFolder"] = "Program resources",
        ["SafetyResourcesFolder"] = "STEP 7 Safety",
        ["SafetyIODataBlocksFolder"] = "F-I/O data blocks",
        ["SafetyCompilerBlocksFolder"] = "Compiler blocks",
        ["SystemDataTypeFolder"] = "System data types",
    };

    private static int SystemFolderRank(string display)
    {
        var i = SystemFolderNames.Values.ToList().IndexOf(display);
        return i < 0 ? int.MaxValue : i;
    }

    internal static IEnumerable<JsonNode> Arr(JsonNode? n) =>
        n is JsonArray a ? a.Where(x => x != null).Select(x => x!) : [];

    internal static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n?.ToString();

    /// <summary>Creates folder nodes for slash-separated group paths below a root node.</summary>
    private sealed class Folders(TreeNode root, NodeKind kind)
    {
        private readonly Dictionary<string, TreeNode> _map = new(StringComparer.Ordinal) { [""] = root };

        public TreeNode Get(string? path, JsonNode? data = null)
        {
            path = (path ?? "").Trim('/');
            if (_map.TryGetValue(path, out var n)) return n;
            var cut = path.LastIndexOf('/');
            var parent = Get(cut < 0 ? "" : path[..cut]);
            var seg = cut < 0 ? path : path[(cut + 1)..];
            var system = SystemFolderNames.TryGetValue(seg, out var display);
            n = new TreeNode(display ?? seg, kind, data);
            // user folders before objects, sorted by name like the TIA project tree; system folders last
            var idx = parent.Children.Count;
            if (!system)
            {
                var first = parent.Children.TakeWhile(c => c.Kind == NodeKind.AllTags).Count();
                idx = first;
                while (idx < parent.Children.Count && IsFolder(parent.Children[idx]) && !SystemFolderNames.ContainsValue(parent.Children[idx].Name)
                       && TiaOrder.Instance.Compare(parent.Children[idx].Name, n.Name) <= 0)
                    idx++;
            }
            InsertAt(parent, n, idx);
            _map[path] = n;
            return n;
        }

        private bool IsFolder(TreeNode c) => c.Kind == kind && _map.ContainsValue(c);

        /// <summary>After all objects are added: system folders to the end of each folder, in TIA order.</summary>
        public void Finish()
        {
            foreach (var parent in _map.Values.Distinct())
            {
                var sys = parent.Children.Where(c => IsFolder(c) && SystemFolderNames.ContainsValue(c.Name))
                    .OrderBy(c => SystemFolderRank(c.Name)).ToList();
                foreach (var c in sys)
                    parent.Children.Move(parent.Children.IndexOf(c), parent.Children.Count - 1);
            }
        }

        private static void InsertAt(TreeNode parent, TreeNode child, int idx)
        {
            parent.Add(child);
            var from = parent.Children.Count - 1;
            if (idx < from) parent.Children.Move(from, idx);
        }
    }
}
