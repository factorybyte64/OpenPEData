using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using static OpenPEData.JsonRead;

namespace OpenPEData;

/// <summary>Communication data of all PLCs of a project (<see cref="TiaProject.GetCommunication"/>). Configuration data
/// from the project only.</summary>
public sealed class TiaCommunicationInfo
{
    internal TiaCommunicationInfo(TiaProjectContent content)
    {
        Project = content.Name;
        var root = content.Json;
        var opcUa = Objects(Obj(root, "project"), "plcOpcUaInterfaces").ToList();
        var list = new List<PlcCommunication>();
        foreach (var d in content.Devices)
            foreach (var p in d.Plcs)
                list.Add(new PlcCommunication(p, opcUa.Where(i => Str(i, "plc") == p.Name)));
        Plcs = new ReadOnlyCollection<PlcCommunication>(list);
    }

    /// <summary>Project name.</summary>
    public string Project { get; }

    /// <summary>One entry per PLC of the project.</summary>
    public IReadOnlyList<PlcCommunication> Plcs { get; }

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["project"] = Project,
        ["plcs"] = new JsonArray([.. Plcs.Select(p => (JsonNode)p.ToJson())]),
    };

    /// <summary>The data as indented JSON text.</summary>
    public string ToJsonString() => ToJson().ToJsonString(JsonText.Options);
}

/// <summary>Communication data of one PLC.</summary>
public sealed class PlcCommunication
{
    /// <summary>Namespace URI of the standard SIMATIC server interface of the S7-1200/S7-1500 OPC UA server.</summary>
    public const string StandardOpcUaNamespaceUri = "http://www.siemens.com/simatic-s7-opcua";

    internal PlcCommunication(PlcSoftware plc, IEnumerable<JsonObject> opcUaInterfaces)
    {
        Name = plc.Name;
        Device = plc.Device.Name;
        TypeName = plc.TypeName;
        OrderNumber = plc.OrderNumber;
        Firmware = plc.Firmware;

        var hw = HardwareItems(plc.Device.Hardware);
        var cpu = hw.FirstOrDefault(h => Str(h.Item, "objectType") == "S7ControllerTargetData" && Str(h.Item, "name") == plc.Name).Item
            ?? hw.FirstOrDefault(h => Str(h.Item, "objectType") == "S7ControllerTargetData").Item;
        var common = cpu is null ? null : Objects(cpu, "items").FirstOrDefault(i => Str(i, "subtype")?.EndsWith(".CpuCommon", StringComparison.Ordinal) == true);
        var ua = cpu is null ? null : Objects(cpu, "items").FirstOrDefault(i => Str(i, "subtype")?.EndsWith(".OPC_UA", StringComparison.Ordinal) == true);

        var ifs = new List<PlcNetworkInterface>();
        foreach (var (item, path) in hw)
            foreach (var n in Objects(item, "nodes"))
                ifs.Add(new PlcNetworkInterface(n, path));
        Interfaces = new ReadOnlyCollection<PlcNetworkInterface>(ifs);

        Access = new PlcAccess(common);
        OpcUa = new PlcOpcUaServer(ua, ifs, opcUaInterfaces);

        var symbols = new List<PlcSymbol>();
        foreach (var t in plc.Tags)
            symbols.Add(PlcSymbol.FromTag(t, OpcUa.IsAvailable));
        foreach (var b in plc.Blocks.Where(b => b.Type is "GlobalDB" or "InstanceDB" or "ArrayDB" or "TypedDB" or "TechnologyDB"))
            PlcSymbol.AddBlock(symbols, b, OpcUa.IsAvailable);
        Symbols = new ReadOnlyCollection<PlcSymbol>(symbols);
    }

    /// <summary>PLC name.</summary>
    public string Name { get; }

    /// <summary>Name of the device (station) the PLC belongs to.</summary>
    public string Device { get; }

    /// <summary>CPU type, e.g. "CPU 1513-1 PN".</summary>
    public string? TypeName { get; }

    /// <summary>Article number of the CPU.</summary>
    public string? OrderNumber { get; }

    /// <summary>Firmware version, e.g. "V2.9".</summary>
    public string? Firmware { get; }

    /// <summary>Network interfaces of the station (CPU and communication modules).</summary>
    public IReadOnlyList<PlcNetworkInterface> Interfaces { get; }

    /// <summary>Access level and PUT/GET setting.</summary>
    public PlcAccess Access { get; }

    /// <summary>OPC UA server settings and server interfaces.</summary>
    public PlcOpcUaServer OpcUa { get; }

    /// <summary>PLC tags and data block members; members of structures are separate entries, array elements are not.</summary>
    public IReadOnlyList<PlcSymbol> Symbols { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["name"] = Name,
        ["device"] = Device,
        ["typeName"] = TypeName,
        ["orderNumber"] = OrderNumber,
        ["firmware"] = Firmware,
        ["interfaces"] = new JsonArray([.. Interfaces.Select(i => (JsonNode)i.ToJson())]),
        ["access"] = Access.ToJson(),
        ["opcUa"] = OpcUa.ToJson(),
        ["symbols"] = new JsonArray([.. Symbols.Select(s => (JsonNode)s.ToJson())]),
    };

    /// <summary>All hardware items of a station with their name path, depth first.</summary>
    static List<(JsonObject Item, string Path)> HardwareItems(JsonObject? hardware)
    {
        var o = new List<(JsonObject, string)>();
        void Walk(JsonObject it, string prefix)
        {
            var path = prefix.Length == 0 ? StrOrEmpty(it, "name") : prefix + "/" + StrOrEmpty(it, "name");
            o.Add((it, path));
            foreach (var c in Objects(it, "items"))
                Walk(c, path);
        }
        foreach (var top in Objects(hardware, "items"))
            Walk(top, "");
        return o;
    }
}

/// <summary>A network interface (node) of a station.</summary>
public sealed class PlcNetworkInterface
{
    internal PlcNetworkInterface(JsonObject n, string module)
    {
        Module = module;
        Name = StrOrEmpty(n, "name");
        Kind = Long(n, "nodeType") switch { 3 => "Ethernet", 1 => "Profibus", 2 => "Mpi", _ => "Other" };
        IpAddress = Str(n, "ipAddress");
        SubnetMask = Str(n, "subnetMask");
        Router = Str(n, "router");
        ProfinetDeviceName = Str(n, "profinetDeviceName");
        ProfibusAddress = Long(n, "profibusAddress");
        IpProtocolSelection = Str(n, "ipProtocolSelection") ?? (Kind == "Ethernet" ? "Project" : null);
        Subnet = Str(n, "subnet");
    }

    /// <summary>Path of the hardware item that holds the interface, e.g. "PLC_1/PROFINET interface_1".</summary>
    public string Module { get; }

    /// <summary>Interface name as shown in TIA Portal, e.g. "X1".</summary>
    public string Name { get; }

    /// <summary>"Ethernet", "Profibus", "Mpi" or "Other".</summary>
    public string Kind { get; }

    /// <summary>IPv4 address set in the project; null if the address is not set in the project.</summary>
    public string? IpAddress { get; }

    /// <summary>Subnet mask set in the project.</summary>
    public string? SubnetMask { get; }

    /// <summary>Router (gateway) address, if a router is used.</summary>
    public string? Router { get; }

    /// <summary>PROFINET device name.</summary>
    public string? ProfinetDeviceName { get; }

    /// <summary>PROFIBUS or MPI address.</summary>
    public long? ProfibusAddress { get; }

    /// <summary>How the IP address is assigned (Openness IpProtocolSelection), e.g. "Project", "Dhcp", "OtherPath";
    /// null for interfaces without IP.</summary>
    public string? IpProtocolSelection { get; }

    /// <summary>Name of the connected subnet, null if not connected.</summary>
    public string? Subnet { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Module} {Name} {IpAddress ?? ProfibusAddress?.ToString(CultureInfo.InvariantCulture)}".TrimEnd();

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["module"] = Module,
        ["name"] = Name,
        ["kind"] = Kind,
        ["ipAddress"] = IpAddress,
        ["subnetMask"] = SubnetMask,
        ["router"] = Router,
        ["profinetDeviceName"] = ProfinetDeviceName,
        ["profibusAddress"] = ProfibusAddress,
        ["ipProtocolSelection"] = IpProtocolSelection,
        ["subnet"] = Subnet,
    };
}

/// <summary>Access settings of a PLC (protection and security).</summary>
public sealed class PlcAccess
{
    static readonly string[] Levels = ["FullAccess", "ReadAccess", "HMIAccess", "NoAccess"];

    internal PlcAccess(JsonObject? common)
    {
        if (common is null)
            return;
        var prm = Obj(common, "parameters");
        ProtectionLevel = Long(prm, "ProtectionLevel");
        AccessLevel = ProtectionLevel switch
        {
            null => "NoAccess",
            >= 1 and <= 4 => Levels[(int)ProtectionLevel - 1],
            _ => ProtectionLevel.Value.ToString(CultureInfo.InvariantCulture),
        };
        PutGetEnabled = Bool(prm, "EnablePutGetConnections") ?? false;
    }

    /// <summary>Access level as named by Openness (PlcProtectionAccessLevel): "FullAccess", "ReadAccess", "HMIAccess",
    /// "NoAccess"; null for CPUs without these settings (e.g. S7-300/400).</summary>
    public string? AccessLevel { get; }

    /// <summary>Stored protection level value; null if not stored.</summary>
    public long? ProtectionLevel { get; }

    /// <summary>PUT/GET communication is permitted; null for CPUs without this setting.</summary>
    public bool? PutGetEnabled { get; }

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["accessLevel"] = AccessLevel,
        ["protectionLevel"] = ProtectionLevel,
        ["putGetEnabled"] = PutGetEnabled,
    };
}

/// <summary>OPC UA server of a PLC.</summary>
public sealed class PlcOpcUaServer
{
    /// <summary>Default TCP port of the OPC UA server.</summary>
    public const int DefaultPort = 4840;

    internal PlcOpcUaServer(JsonObject? item, List<PlcNetworkInterface> interfaces, IEnumerable<JsonObject> serverInterfaces)
    {
        IsAvailable = item is not null;
        var prm = Obj(item, "parameters");
        Enabled = Bool(prm, "OpcUaEnableServer") ?? false;
        Port = Long(prm, "OpcUaPortNumber") ?? DefaultPort;
        EndpointUrls = new ReadOnlyCollection<string>(Enabled
            ? [.. interfaces.Where(i => i.Kind == "Ethernet" && i.IpAddress is { } ip && ip != "0.0.0.0")
                .Select(i => $"opc.tcp://{i.IpAddress}:{Port.ToString(CultureInfo.InvariantCulture)}").Distinct()]
            : []);
        Interfaces = new ReadOnlyCollection<PlcOpcUaInterface>([.. serverInterfaces.Select(j => new PlcOpcUaInterface(j))]);
    }

    /// <summary>The CPU has an OPC UA server.</summary>
    public bool IsAvailable { get; }

    /// <summary>The OPC UA server is activated.</summary>
    public bool Enabled { get; }

    /// <summary>TCP port of the server.</summary>
    public long Port { get; }

    /// <summary>"opc.tcp://address:port" per Ethernet interface with an IP address; empty if the server is not activated.</summary>
    public IReadOnlyList<string> EndpointUrls { get; }

    /// <summary>Server interfaces, reference namespaces and companion specifications of the PLC.</summary>
    public IReadOnlyList<PlcOpcUaInterface> Interfaces { get; }

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["available"] = IsAvailable,
        ["enabled"] = Enabled,
        ["port"] = Port,
        ["endpointUrls"] = new JsonArray([.. EndpointUrls.Select(u => (JsonNode?)u)]),
        ["standardNamespaceUri"] = IsAvailable ? PlcCommunication.StandardOpcUaNamespaceUri : null,
        ["interfaces"] = new JsonArray([.. Interfaces.Select(i => (JsonNode)i.ToJson())]),
    };
}

/// <summary>An OPC UA server interface, reference namespace or companion specification of a PLC.</summary>
public sealed class PlcOpcUaInterface
{
    internal PlcOpcUaInterface(JsonObject j)
    {
        Name = StrOrEmpty(j, "name");
        Kind = Str(j, "interfaceKind");
        Enabled = Bool(j, "enabled") ?? false;
        NamespaceUris = new ReadOnlyCollection<string>(NamespaceUrisOf(Str(j, "nodeset")));
        Nodes = new ReadOnlyCollection<PlcOpcUaNode>([.. Objects(j, "nodes").Select(n => new PlcOpcUaNode(n))]);
    }

    /// <summary>Interface name.</summary>
    public string Name { get; }

    /// <summary>"server", "reference" or "companion" (as read).</summary>
    public string? Kind { get; }

    /// <summary>The interface is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Namespace URIs of the nodeset (in the order of the nodeset; ns=1 is the first).</summary>
    public IReadOnlyList<string> NamespaceUris { get; }

    /// <summary>Objects, variables and types of the interface.</summary>
    public IReadOnlyList<PlcOpcUaNode> Nodes { get; }

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["name"] = Name,
        ["kind"] = Kind,
        ["enabled"] = Enabled,
        ["namespaceUris"] = new JsonArray([.. NamespaceUris.Select(u => (JsonNode?)u)]),
        ["nodes"] = new JsonArray([.. Nodes.Select(n => (JsonNode)n.ToJson())]),
    };

    static List<string> NamespaceUrisOf(string? nodeset)
    {
        if (string.IsNullOrEmpty(nodeset))
            return [];
        try
        {
            var x = XDocument.Parse(nodeset);
            return [.. x.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "NamespaceUris")?.Elements()
                .Where(e => e.Name.LocalName == "Uri").Select(e => e.Value) ?? []];
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }
    }
}

/// <summary>A node of an OPC UA server interface.</summary>
public sealed class PlcOpcUaNode
{
    internal PlcOpcUaNode(JsonObject n)
    {
        NodeClass = StrOrEmpty(n, "nodeClass");
        NodeId = StrOrEmpty(n, "nodeId");
        BrowseName = Str(n, "browseName");
        DisplayName = Str(n, "displayName");
        DataType = Str(n, "dataType");
        AccessLevel = Str(n, "accessLevel");
        Parent = Str(n, "parent");
        Mapping = Str(n, "mapping");
    }

    /// <summary>"Object", "Variable", "ObjectType", ...</summary>
    public string NodeClass { get; }

    /// <summary>NodeId in the nodeset, e.g. "ns=1;i=1001" (ns relative to the interface's <see cref="PlcOpcUaInterface.NamespaceUris"/>).</summary>
    public string NodeId { get; }

    /// <summary>BrowseName.</summary>
    public string? BrowseName { get; }

    /// <summary>DisplayName.</summary>
    public string? DisplayName { get; }

    /// <summary>DataType of a variable.</summary>
    public string? DataType { get; }

    /// <summary>AccessLevel of a variable (1 read, 3 read/write).</summary>
    public string? AccessLevel { get; }

    /// <summary>NodeId of the parent node.</summary>
    public string? Parent { get; }

    /// <summary>The PLC tag a variable is mapped to, e.g. "\"DB\".\"member\"".</summary>
    public string? Mapping { get; }

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["nodeClass"] = NodeClass,
        ["nodeId"] = NodeId,
        ["browseName"] = BrowseName,
        ["displayName"] = DisplayName,
        ["dataType"] = DataType,
        ["accessLevel"] = AccessLevel,
        ["parent"] = Parent,
        ["mapping"] = Mapping,
    };
}

/// <summary>A symbol of a PLC: a PLC tag or a data block member.</summary>
public sealed class PlcSymbol
{
    PlcSymbol(string path, string source, string dataType)
    {
        Path = path;
        Source = source;
        DataType = dataType;
    }

    /// <summary>Symbolic name as in TIA Portal, e.g. "\"Tag_1\"" or "\"DB_Motor\".motor.Speed".</summary>
    public string Path { get; }

    /// <summary>"Tag" for a PLC tag, "DB" for a data block member.</summary>
    public string Source { get; }

    /// <summary>Tag table (for a tag) or data block (for a member).</summary>
    public string? Container { get; private init; }

    /// <summary>Data block number (for a member).</summary>
    public long? BlockNumber { get; private init; }

    /// <summary>Data type, e.g. "Int", "Array[0..3] of Int", "\"UDT_Motor\"".</summary>
    public string DataType { get; }

    /// <summary>The member has members (struct, PLC data type, multi-instance).</summary>
    public bool IsStructured { get; private init; }

    /// <summary>Absolute address: of a tag (e.g. "%MW100"), or of an elementary member of a data block with standard
    /// access (e.g. "%DB1.DBW2"); null otherwise.</summary>
    public string? Address { get; private init; }

    /// <summary>Byte.bit offset in a data block with standard access, e.g. "2.0".</summary>
    public string? Offset { get; private init; }

    /// <summary>The data block has optimized access (null for tags).</summary>
    public bool? Optimized { get; private init; }

    /// <summary>Accessible from HMI/OPC UA/Web API.</summary>
    public bool? Accessible { get; private init; }

    /// <summary>Visible in HMI engineering.</summary>
    public bool? Visible { get; private init; }

    /// <summary>Writable from HMI/OPC UA/Web API.</summary>
    public bool? Writable { get; private init; }

    /// <summary>Member IDs (LID) stored in the block interface, from the first level down to this member; null for tags.</summary>
    public IReadOnlyList<long>? Lids { get; private init; }

    /// <summary>NodeId in the standard SIMATIC server interface with namespace URI, e.g. "nsu=…;s=\"DB\".\"member\"";
    /// null if the CPU has no OPC UA server or the symbol is not accessible.</summary>
    public string? OpcUaNodeId { get; private init; }

    /// <inheritdoc/>
    public override string ToString() => $"{Path} : {DataType}";

    /// <summary>The data as JSON.</summary>
    public JsonObject ToJson() => new()
    {
        ["path"] = Path,
        ["source"] = Source,
        ["container"] = Container,
        ["blockNumber"] = BlockNumber,
        ["dataType"] = DataType,
        ["structured"] = IsStructured,
        ["address"] = Address,
        ["offset"] = Offset,
        ["lids"] = Lids is null ? null : new JsonArray([.. Lids.Select(l => (JsonNode?)l)]),
        ["optimized"] = Optimized,
        ["accessible"] = Accessible,
        ["visible"] = Visible,
        ["writable"] = Writable,
        ["opcUaNodeId"] = OpcUaNodeId,
    };

    internal static PlcSymbol FromTag(PlcTag t, bool opcUa)
    {
        var table = t.Json.Parent?.Parent as JsonObject;
        return new PlcSymbol(Quote(t.Name), "Tag", t.DataType)
        {
            Container = table is null ? null : Str(table, "name"),
            Address = t.Address,
            Accessible = t.IsHmiAccessible,
            Visible = t.IsHmiVisible,
            Writable = t.IsHmiWritable,
            OpcUaNodeId = opcUa && t.IsHmiAccessible != false ? NodeId(Quote(t.Name)) : null,
        };
    }

    static readonly HashSet<string> DataSections = ["Input", "Output", "InOut", "Static"];

    internal static void AddBlock(List<PlcSymbol> o, PlcBlock b, bool opcUa)
    {
        var optimized = Bool(b.Json, "optimized");
        IEnumerable<JsonObject> members = b.Interface is { Count: > 0 } iface
            ? iface.OfType<JsonObject>().Where(s => DataSections.Contains(StrOrEmpty(s, "section"))).SelectMany(s => Objects(s, "members"))
            : Objects(b.Json, "members");
        var db = Quote(b.Name);
        void Add(JsonObject m, string? path, string? ua, bool accessible, IReadOnlyList<long> parentLids)
        {
            IReadOnlyList<long>? lids = Long(m, "lid") is { } lid ? [.. parentLids, lid] : null;
            var name = StrOrEmpty(m, "name");
            var p = path is null ? db : path + "." + Member(name);
            var id = ua is null ? db : ua + ".\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            var type = StrOrEmpty(m, "dataType");
            var acc = Bool(m, "externalAccessible");
            var reach = accessible && acc != false;
            var children = Objects(m, "members").ToList();
            var isArray = type.StartsWith("Array", StringComparison.OrdinalIgnoreCase);
            var offset = Str(m, "offset");
            o.Add(new PlcSymbol(p, "DB", type)
            {
                Container = b.Name,
                BlockNumber = b.Number,
                IsStructured = children.Count > 0 && !isArray,
                Offset = offset,
                Lids = lids,
                Address = optimized == false && offset is not null && children.Count == 0 && !isArray ? DbAddress(b.Number, type, offset) : null,
                Optimized = optimized,
                Accessible = acc,
                Visible = Bool(m, "externalVisible"),
                Writable = Bool(m, "externalWritable"),
                OpcUaNodeId = opcUa && reach ? NodeId(id) : null,
            });
            if (!isArray)
                foreach (var c in children)
                    Add(c, p, id, reach, lids ?? []);
        }
        var arrayDb = b.Type == "ArrayDB";
        foreach (var m in members)
            Add(m, arrayDb ? null : db, arrayDb ? null : db, true, []);
    }

    static string NodeId(string identifier) => $"nsu={PlcCommunication.StandardOpcUaNamespaceUri};s={identifier}";

    static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>A member name as TIA Portal writes it: plain if it is an identifier, else quoted.</summary>
    static string Member(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_') ? name : Quote(name);

    /// <summary>Absolute address of an elementary member of a DB with standard access.</summary>
    static string? DbAddress(long? db, string type, string offset)
    {
        if (db is null)
            return null;
        var dot = offset.IndexOf('.');
        var bytePart = dot < 0 ? offset : offset[..dot];
        var bitPart = dot < 0 ? "0" : offset[(dot + 1)..];
        var size = type.ToUpperInvariant() switch
        {
            "BOOL" => "X",
            "BYTE" or "CHAR" or "SINT" or "USINT" => "B",
            "WORD" or "INT" or "UINT" or "WCHAR" or "DATE" or "S5TIME" => "W",
            "DWORD" or "DINT" or "UDINT" or "REAL" or "TIME" or "TIME_OF_DAY" or "TOD" => "D",
            _ => null,
        };
        var n = db.Value.ToString(CultureInfo.InvariantCulture);
        return size switch
        {
            null => null,
            "X" => $"%DB{n}.DBX{bytePart}.{bitPart}",
            _ => $"%DB{n}.DB{size}{bytePart}",
        };
    }
}

static class JsonText
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
