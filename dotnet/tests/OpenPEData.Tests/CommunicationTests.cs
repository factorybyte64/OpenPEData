using System.Text.Json;
using System.Xml.Linq;

namespace OpenPEData.Tests;

/// <summary>TiaProject.GetCommunication: network interfaces, access settings, OPC UA server and interfaces, and symbols
/// against the Openness values of the samples.</summary>
public static class CommunicationTests
{
    static string Sample(string name) => Path.Combine(Program.Root, "samples", name);

    static TiaCommunicationInfo Open(string sample)
    {
        using var p = TiaProject.Open(Sample(sample + ".zap21"));
        return p.GetCommunication();
    }

    /// <summary>Sample 063: every network interface of every PLC station equals Openness (node attributes).</summary>
    [Test]
    public static void InterfacesAgainstOpenness()
    {
        var c = new Check("063 communication interfaces = Openness node attributes");
        var info = Open("063_multi_plc");
        c.Eq(4, info.Plcs.Count, "PLCs");
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Sample("063_multi_plc_truth"), "network.json")));
        var count = 0;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var dev = e.GetProperty("device").GetString()!;
            var path = e.GetProperty("deviceItemPath").GetString()!;
            var at = e.GetProperty("attributes");
            string? A(string k) => at.TryGetProperty(k, out var v) ? v.GetString() : null;
            var w = $"{dev}/{path}";
            var n = info.Plcs.FirstOrDefault(p => p.Device == dev)?.Interfaces.FirstOrDefault(i => i.Module.EndsWith(path[(path.IndexOf('/') + 1)..], StringComparison.Ordinal));
            if (n is null)
            {
                c.Fail(w + ": missing");
                continue;
            }
            count++;
            c.Eq(A("Name"), n.Name, w + " name");
            c.Eq(e.TryGetProperty("connectedSubnetName", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null, n.Subnet, w + " subnet");
            if (A("NodeType") == "Profibus")
            {
                c.Eq("Profibus", n.Kind, w + " kind");
                c.Eq(long.Parse(A("Address")!), n.ProfibusAddress, w + " PROFIBUS address");
                continue;
            }
            c.Eq("Ethernet", n.Kind, w + " kind");
            c.Eq(A("Address"), n.IpAddress, w + " IP address");
            c.Eq(A("SubnetMask"), n.SubnetMask, w + " subnet mask");
            c.Eq(A("UseRouter") == "True" ? A("RouterAddress") : null, n.Router, w + " router");
            c.Eq(A("PnDeviceName"), n.ProfinetDeviceName, w + " PROFINET device name");
            c.Eq(A("IpProtocolSelection"), n.IpProtocolSelection, w + " IP protocol selection");
        }
        c.Eq(7, count, "interfaces compared");
        c.Done();
    }

    /// <summary>Access level and PUT/GET against Openness (PlcAccessLevelProvider, ProtectionEnablePutGetCommunication):
    /// 200 (level not stored) and 204 (level changed).</summary>
    [Test]
    public static void AccessAgainstOpenness()
    {
        var c = new Check("access level and PUT/GET = Openness");
        foreach (var s in new[] { "200_hw_cycle_time", "204_hw_access_level" })
        {
            var truth = JsonDocument.Parse(File.ReadAllText(Path.Combine(Sample(s + "_truth"), "accessLevel_after.json")));
            var plc = Open(s).Plcs.Single();
            c.Eq(truth.RootElement.GetProperty("properties").GetProperty("PlcProtectionAccessLevel").GetString(), plc.Access.AccessLevel, s + " access level");
            var attrs = JsonDocument.Parse(File.ReadAllText(Path.Combine(Sample(s + "_truth"), "attributes_after.json")));
            if (attrs.RootElement.GetProperty("PLC_1").TryGetProperty("ProtectionEnablePutGetCommunication", out var pg))
                c.Eq(pg.GetProperty("value").GetBoolean(), plc.Access.PutGetEnabled, s + " PUT/GET");
        }
        c.Done();
    }

    /// <summary>OPC UA server: off by default (002), activated (230), port changed to 4841 (231); endpoints per Ethernet
    /// interface with an IP address; no OPC UA on an S7-300 (071).</summary>
    [Test]
    public static void OpcUaServer()
    {
        var c = new Check("OPC UA server settings");
        var off = Open("002_cpu").Plcs.Single().OpcUa;
        c.True(off.IsAvailable, "002 available");
        c.Eq(false, off.Enabled, "002 enabled");
        c.Eq(0, off.EndpointUrls.Count, "002 endpoints");
        var on = Open("230_hw_opcua_on").Plcs.Single().OpcUa;
        c.Eq(true, on.Enabled, "230 enabled");
        c.Eq(4840L, on.Port, "230 port");
        var port = Open("231_hw_opcua_port").Plcs.Single().OpcUa;
        c.Eq(4841L, port.Port, "231 port");
        c.Eq("opc.tcp://192.168.0.1:4841|opc.tcp://192.168.1.1:4841", string.Join("|", port.EndpointUrls), "231 endpoints");
        var s300 = Open("071_db_1200_300").Plcs.Single(p => p.Name == "PLC_300");
        c.Eq(false, s300.OpcUa.IsAvailable, "S7-300 OPC UA");
        c.Eq(null, s300.Access.AccessLevel, "S7-300 access level");
        c.True(s300.Symbols.All(x => x.OpcUaNodeId is null), "S7-300 without NodeIds");
        c.Done();
    }

    /// <summary>Sample 081: namespace URIs and nodes of the server interface against its Openness export; every mapped
    /// variable refers to a symbol of the standard interface.</summary>
    [Test]
    public static void OpcUaInterfaceAgainstExport()
    {
        var c = new Check("081 OPC UA server interface = Openness export");
        var truth = XElement.Load(Path.Combine(Sample("081_plc_opcua_nodes_truth"), "SI_Test.xml"));
        var plc = Open("081_plc_opcua_nodes").Plcs.Single();
        var si = plc.OpcUa.Interfaces.FirstOrDefault(i => i.Name == "SI_Test");
        c.True(si is not null, "interface SI_Test");
        c.Eq("server", si?.Kind, "kind");
        var uris = truth.Elements().FirstOrDefault(e => e.Name.LocalName == "NamespaceUris")?.Elements().Select(e => e.Value).ToList() ?? [];
        c.Eq(string.Join("|", uris), string.Join("|", si?.NamespaceUris ?? []), "namespace URIs");
        var ids = truth.Elements().Where(e => e.Name.LocalName is "UAObject" or "UAVariable").Select(e => (string)e.Attribute("NodeId")!).ToList();
        c.Eq(string.Join("|", ids), string.Join("|", si?.Nodes.Select(n => n.NodeId) ?? []), "node ids");
        var symbols = plc.Symbols.Select(x => x.OpcUaNodeId).ToHashSet();
        foreach (var n in si?.Nodes.Where(n => n.Mapping is not null) ?? [])
            c.True(symbols.Contains($"nsu={PlcCommunication.StandardOpcUaNamespaceUri};s={n.Mapping}"), n.NodeId + " mapping " + n.Mapping);
        c.Done();
    }

    /// <summary>Every DB member that Openness lists (memberGet, without array elements) is a symbol with the same data type,
    /// flags and one member ID per path level; every accessible DB symbol is one of them.</summary>
    [Test]
    public static void DbSymbolsAgainstOpenness()
    {
        var c = new Check("DB symbols = Openness memberGet");
        var compared = 0;
        foreach (var s in new[] { "060_db_attrs", "063_multi_plc", "066_sys_fb_instances", "068_motion_tos", "070_db_khp_units", "071_db_1200_300", "072_motion_tcpu" })
        {
            var info = Open(s);
            foreach (var plc in info.Plcs)
            {
                var dir = Path.Combine(Sample(s + "_truth"), plc.Name, "blocks");
                if (!Directory.Exists(dir))
                    continue;
                foreach (var f in Directory.GetFiles(dir, "*.members.json"))
                {
                    var db = Path.GetFileName(f)[..^".members.json".Length];
                    var w0 = $"{s} {plc.Name}/{db}";
                    var prefix = "\"" + db + "\".";
                    var got = plc.Symbols.Where(x => x.Source == "DB" && x.Container == db)
                        .ToDictionary(x => x.Path == "\"" + db + "\"" ? db : x.Path[prefix.Length..].Replace("\"", ""));
                    var doc = JsonDocument.Parse(File.ReadAllText(f));
                    if (doc.RootElement.TryGetProperty("truncated", out var tr) && tr.GetBoolean())
                        continue;
                    var seen = new HashSet<string>();
                    foreach (var e in doc.RootElement.GetProperty("members").EnumerateArray())
                    {
                        var path = e.GetProperty("path").GetString()!;
                        if (path.Contains('['))
                            continue;
                        seen.Add(path);
                        var w = w0 + "." + path;
                        if (!got.TryGetValue(path, out var g))
                        {
                            c.Fail(w + " missing");
                            continue;
                        }
                        compared++;
                        c.Eq(e.GetProperty("dataTypeName").GetString()!.Replace("\"", ""), g.DataType.Replace("\"", ""), w + " type");
                        c.Eq(e.GetProperty("externalAccessible").GetBoolean(), g.Accessible, w + " accessible");
                        c.Eq(e.GetProperty("externalVisible").GetBoolean(), g.Visible, w + " visible");
                        c.Eq(e.GetProperty("externalWritable").GetBoolean(), g.Writable, w + " writable");
                        c.True(g.Lids is { Count: > 0 } l && l.Count == path.Split('.').Length, w + " member IDs");
                    }
                    // members that Openness does not list (hidden members of system blocks) must not be accessible
                    foreach (var k in got.Keys.Where(k => !seen.Contains(k) && got[k].Accessible != false))
                        c.Fail($"{w0}.{k}: not in Openness");
                }
            }
        }
        c.True(compared > 200, $"members compared ({compared})");
        c.Done();
    }

    /// <summary>Every PLC tag of the tag table exports (SimaticML) is a symbol with the same address, data type and flags.</summary>
    [Test]
    public static void TagSymbolsAgainstExport()
    {
        var c = new Check("tag symbols = Openness tag table export");
        var compared = 0;
        foreach (var dir in Directory.GetDirectories(Path.Combine(Program.Root, "samples"), "*_truth"))
        {
            var s = Path.GetFileName(dir)[..^"_truth".Length];
            if (!File.Exists(Sample(s + ".zap21")))
                continue;
            var tables = Directory.GetDirectories(dir).Select(d => (Plc: Path.GetFileName(d), Dir: Path.Combine(d, "tagtables")))
                .Where(x => Directory.Exists(x.Dir)).ToList();
            if (tables.Count == 0 || tables.Count > 1)
                continue;
            var info = Open(s);
            foreach (var (plcName, tdir) in tables)
            {
                var plc = info.Plcs.FirstOrDefault(p => p.Name == plcName);
                if (plc is null)
                {
                    c.Fail($"{s} {plcName}: missing");
                    continue;
                }
                var got = plc.Symbols.Where(x => x.Source == "Tag").ToDictionary(x => x.Path);
                foreach (var f in Directory.GetFiles(tdir, "*.xml"))
                    foreach (var t in XElement.Load(f).Descendants().Where(e => e.Name.LocalName == "SW.Tags.PlcTag"))
                    {
                        string? A(string k) => t.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList")?.Elements().FirstOrDefault(e => e.Name.LocalName == k)?.Value;
                        var name = A("Name")!;
                        var w = $"{s} {plcName}/{name}";
                        if (!got.TryGetValue("\"" + name + "\"", out var g))
                        {
                            c.Fail(w + " missing");
                            continue;
                        }
                        compared++;
                        c.Eq(Path.GetFileNameWithoutExtension(f), g.Container, w + " table");
                        c.Eq(A("LogicalAddress"), g.Address, w + " address");
                        c.Eq(A("DataTypeName")!.Replace("\"", ""), g.DataType.Replace("\"", ""), w + " type");
                        c.Eq(A("ExternalAccessible") == "true", g.Accessible, w + " accessible");
                        c.Eq(A("ExternalWritable") == "true", g.Writable, w + " writable");
                    }
            }
        }
        c.True(compared > 10, $"tags compared ({compared})");
        c.Done();
    }

    /// <summary>Standard access (071 PLC_300): byte.bit offsets and absolute addresses of elementary members.</summary>
    [Test]
    public static void StandardAccessAddresses()
    {
        var c = new Check("071 standard access addresses");
        var plc = Open("071_db_1200_300").Plcs.Single(p => p.Name == "PLC_300");
        var s = plc.Symbols.Where(x => x.Container == "DB_300").ToDictionary(x => x.Path);
        c.Eq("%DB1.DBX0.0", s.GetValueOrDefault("\"DB_300\".x")?.Address, "Bool");
        c.Eq("%DB1.DBW2", s.GetValueOrDefault("\"DB_300\".i")?.Address, "Int");
        c.Eq("%DB1.DBD4", s.GetValueOrDefault("\"DB_300\".d")?.Address, "DInt");
        c.Eq("%DB1.DBD8", s.GetValueOrDefault("\"DB_300\".r")?.Address, "Real");
        c.Eq("24.0", s.GetValueOrDefault("\"DB_300\".s")?.Offset, "String offset");
        c.Eq(null, s.GetValueOrDefault("\"DB_300\".s")?.Address, "String address");
        c.Eq(false, s.GetValueOrDefault("\"DB_300\".x")?.Optimized, "optimized");
        c.Done();
    }

    /// <summary>The CLI with --comm prints the same JSON as the API.</summary>
    [Test]
    public static void CliMatchesApi()
    {
        var c = new Check("--comm = GetCommunication");
        var file = Sample("063_multi_plc.zap21");
        using var p = TiaProject.Open(file);
        var expected = p.GetCommunication().ToJsonString().Replace("\n", Environment.NewLine) + Environment.NewLine;
        var bin = Path.Combine(Program.Root, "dotnet", "src", "OpenPEData.Cli", "bin", "Release", "net10.0");
        var exe = Path.Combine(bin, OperatingSystem.IsWindows() ? "OpenPEData.Cli.exe" : "OpenPEData.Cli");
        if (!File.Exists(exe))
            throw new TestFailure("CLI not built: " + exe + " (dotnet build dotnet\\src\\OpenPEData.Cli -c Release)");
        var (stdout, _, code) = DifferentialTests.Exec(exe, [file, "--comm"]);
        c.Eq(0, code, "exit code");
        c.Eq(expected, System.Text.Encoding.UTF8.GetString(stdout), "output");
        c.True(ReferenceEquals(p.GetCommunication(), p.GetCommunication()), "computed once");
        c.Done();
    }
}
