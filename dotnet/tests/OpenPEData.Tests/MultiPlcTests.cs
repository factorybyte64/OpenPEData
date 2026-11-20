using System.Text.Json;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Sample 063_multi_plc: four PLCs (S7-1516-3 PN/DP with X1, X2 and DP, S7-1214C, S7-1513 with CP 1543-1,
/// ET 200SP 1512SP with "IP address set directly at the device"), three Ethernet subnets and one PROFIBUS subnet, DBs
/// with the same name in different PLCs. Truth: samples\063_multi_plc_truth (tools\Truth-MultiPlc.ps1).</summary>
public static class MultiPlcTests
{
    static readonly string Truth = Path.Combine(Program.Root, "samples", "063_multi_plc_truth");

    static PyDict Project()
    {
        using var p = TiaProject.Open(Path.Combine(Program.Root, "samples", "063_multi_plc.zap21"));
        return (PyDict)((PyDict)p.Raw(TiaContent.Structure, default)!)["project"]!;
    }

    static IEnumerable<PyDict> Devices(PyDict project) => ((List<object?>)project["devices"]!).Cast<PyDict>();

    /// <summary>All hardware items of a device keyed by their name path below the station (e.g. PLC_A/PROFINET interface_1).</summary>
    static Dictionary<string, PyDict> Items(PyDict device)
    {
        var o = new Dictionary<string, PyDict>();
        void Walk(PyDict it, string prefix)
        {
            var path = prefix.Length == 0 ? (string)it["name"]! : prefix + "/" + (string)it["name"]!;
            o.TryAdd(path, it);
            foreach (var c in ((List<object?>)it["items"]!).Cast<PyDict>())
                Walk(c, path);
        }
        // top items are the rack and the modules plugged into it, like the first level of an Openness path
        foreach (var top in ((List<object?>)((PyDict)device["hardware"]!)["items"]!).Cast<PyDict>())
            Walk(top, "");
        return o;
    }

    /// <summary>Every network interface of every station: IP address, subnet mask, router, subnet name, IP protocol
    /// selection and PROFIBUS address equal Openness (TiaNetworkNode getAttributes).</summary>
    [Test]
    public static void InterfacesAgainstOpenness()
    {
        var c = new Check("063 interfaces = Openness node attributes");
        var proj = Project();
        var devs = Devices(proj).ToDictionary(d => (string)d["name"]!);
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Truth, "network.json")));
        var count = 0;
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var dev = e.GetProperty("device").GetString()!;
            var path = e.GetProperty("deviceItemPath").GetString()!;
            var at = e.GetProperty("attributes");
            string? A(string k) => at.TryGetProperty(k, out var v) ? v.GetString() : null;
            if (!Items(devs[dev]).TryGetValue(path, out var it) || ((List<object?>)it["nodes"]!).Count == 0)
            {
                c.Fail($"{dev}/{path}: no node");
                continue;
            }
            count++;
            var n = (PyDict)((List<object?>)it["nodes"]!)[0]!;
            var w = $"{dev}/{path}";
            c.Eq(e.TryGetProperty("connectedSubnetName", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null, n["subnet"], w + " subnet");
            if (A("NodeType") == "Profibus")
            {
                c.Eq(long.Parse(A("Address")!), n["profibusAddress"], w + " PROFIBUS address");
                continue;
            }
            c.Eq(A("Address"), n["ipAddress"], w + " IP address");
            c.Eq(A("SubnetMask"), n["subnetMask"], w + " subnet mask");
            c.Eq(A("UseRouter") == "True" ? A("RouterAddress") : null, n["router"], w + " router");
            c.Eq(A("PnDeviceName"), n["profinetDeviceName"], w + " PROFINET device name");
            var sel = A("IpProtocolSelection");
            c.Eq(sel == "Project" ? null : sel, n.Get("ipProtocolSelection"), w + " IP protocol selection");
        }
        c.Eq(7, count, "interfaces compared");
        c.Done();
    }

    /// <summary>Every member of every DB in every PLC (same DB name in three PLCs): data type, start value (also start
    /// values of array elements), retain and external accessible/visible/writable equal Openness memberGet.</summary>
    [Test]
    public static void DbSymbolsPerPlcAgainstOpenness()
    {
        var c = new Check("063 DB members per PLC = Openness memberGet");
        var compared = 0;
        foreach (var dev in Devices(Project()))
            foreach (var plc in ((List<object?>)dev["plcs"]!).Cast<PyDict>())
            {
                var blocks = ((List<object?>)plc["blocks"]!).Cast<PyDict>().ToDictionary(b => (string)b["name"]!);
                foreach (var f in Directory.GetFiles(Path.Combine(Truth, (string)dev["name"]!, "blocks"), "*.members.json"))
                {
                    var name = Path.GetFileName(f)[..^".members.json".Length];
                    var w0 = $"{dev["name"]}/{name}";
                    if (!blocks.TryGetValue(name, out var b))
                    {
                        c.Fail(w0 + " missing");
                        continue;
                    }
                    var got = new Dictionary<string, PyDict>();
                    if (Py.Eq(b.Get("type"), "InstanceDB"))
                        foreach (var sec in ((List<object?>)b["interface"]!).Cast<PyDict>())
                            Flat((List<object?>)sec["members"]!, "", got);
                    else
                        Flat((List<object?>)b["members"]!, "", got);
                    var doc = JsonDocument.Parse(File.ReadAllText(f));
                    foreach (var e in doc.RootElement.GetProperty("members").EnumerateArray())
                    {
                        var path = e.GetProperty("path").GetString()!;
                        string? S(string k) => e.TryGetProperty(k, out var v) ? v.GetString() : null;
                        var sv = S("startValue");
                        var changed = sv is not null && sv != S("defaultValue");
                        var w = w0 + "." + path;
                        if (path.Contains('['))
                        {
                            // element start value: kept at the innermost member as elementStartValues {index: value}
                            if (!changed)
                                continue;
                            var open = path.IndexOf('[');
                            var close = path.IndexOf(']', open);
                            var key = path[..open] + path[(close + 1)..];
                            var idx = path[(open + 1)..close].Replace(" ", "");
                            var ev = got.TryGetValue(key, out var gm) ? gm.Get("elementStartValues") as PyDict : null;
                            c.Eq(sv, ev?.Get(idx), w + " element start value");
                            compared++;
                            continue;
                        }
                        if (!got.TryGetValue(path, out var g))
                        {
                            c.Fail(w + " missing");
                            continue;
                        }
                        compared++;
                        c.Eq(e.GetProperty("dataTypeName").GetString()!.Replace("\"", ""), ((string)g["dataType"]!).Replace("\"", ""), w + " type");
                        if (changed)
                            c.Eq(sv, g.Get("startValue"), w + " start value");
                        foreach (var k in new[] { "retain", "externalAccessible", "externalVisible", "externalWritable" })
                            c.Eq(e.GetProperty(k).GetBoolean(), g.Get(k), w + " " + k);
                    }
                }
            }
        c.Eq(32, compared, "members compared (31 members and one array element start value)");
        c.Done();
    }

    static void Flat(List<object?> members, string prefix, Dictionary<string, PyDict> o)
    {
        foreach (var m in members.Cast<PyDict>())
        {
            var path = prefix + (string)m["name"]!;
            o[path] = m;
            if (m.Get("members") is List<object?> kids)
                Flat(kids, path + ".", o);
        }
    }
}
