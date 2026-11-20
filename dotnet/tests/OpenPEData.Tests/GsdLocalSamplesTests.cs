using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>GSD devices of any vendor against Openness. The samples contain third-party GSDML and are therefore only local:
/// samples\local_gsd\&lt;name&gt;.zap21 with &lt;name&gt;_truth\truth.json (device, Openness type identifier, items with name,
/// position, type identifier and HW identifier from TiaDevice getDetails / TiaDeviceItem hwIdentifierList, addresses from
/// addressList, PROFINET node from TiaNetworkNode getAttributes). The test passes without checks when the folder is empty.</summary>
public static class GsdLocalSamplesTests
{
    [Test]
    public static void LocalGsdDevicesAgainstOpenness()
    {
        var c = new Check("local GSD samples = Openness");
        var dir = Path.Combine(Program.Root, "samples", "local_gsd");
        var zaps = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.zap21").Order(StringComparer.Ordinal).ToList() : [];
        foreach (var zap in zaps)
        {
            var name = Path.GetFileNameWithoutExtension(zap);
            var tf = Path.Combine(dir, name + "_truth", "truth.json");
            if (!File.Exists(tf))
                continue;
            var t = JsonNode.Parse(File.ReadAllText(tf))!;
            using var p = TiaProject.Open(zap);
            var dev = p.GetStructure()["project"]!["devices"]!.AsArray().FirstOrDefault(d => d!["name"]!.GetValue<string>() == t["device"]!.GetValue<string>());
            c.True(dev is not null, name + " device");
            if (dev is null)
                continue;
            c.Eq(t["typeIdentifier"]!.GetValue<string>(), dev["hardware"]?["gsd"]?["typeIdentifier"]?.GetValue<string>(), name + " station type");
            if (t["stationHwid"] is { } sh)
                c.Eq(sh.GetValue<long>(), dev["hardware"]?["hwIdentifier"]?["value"]?.GetValue<long>(), name + " station HW identifier");
            var flat = new Dictionary<string, JsonNode>();
            void Walk(JsonArray? items)
            {
                foreach (var it in items ?? [])
                {
                    flat.TryAdd(it!["name"]!.GetValue<string>(), it);
                    Walk(it["items"]?.AsArray());
                }
            }
            Walk(dev["hardware"]?["items"]?.AsArray());
            foreach (var e in t["items"]!.AsArray())
            {
                var n = e!["name"]!.GetValue<string>();
                if (!flat.TryGetValue(n, out var it))
                {
                    c.Fail($"{name} {n}: missing");
                    continue;
                }
                c.Eq(e["type"]!.GetValue<string>(), it["gsd"]?["typeIdentifier"]?.GetValue<string>(), $"{name} {n} type");
                c.Eq(e["pos"]!.GetValue<long>(), it["positionNumber"]?.GetValue<long>(), $"{name} {n} position");
                if (e["hwid"] is { } hw)
                    c.Eq(hw.GetValue<long>(), it["hwIdentifier"]?["value"]?.GetValue<long>(), $"{name} {n} HW identifier");
            }
            foreach (var a in t["addresses"]!.AsArray())
            {
                var ad = flat.GetValueOrDefault(a!["item"]!.GetValue<string>())?["addresses"]?.AsArray().FirstOrDefault();
                c.Eq(a["io"]!.GetValue<string>(), ad?["ioType"]?.GetValue<string>(), $"{name} {a["item"]} io");
                c.Eq(a["start"]!.GetValue<long>(), ad?["startAddress"]?.GetValue<long>(), $"{name} {a["item"]} start");
                c.Eq(a["len"]!.GetValue<long>() * 8, ad?["lengthBits"]?.GetValue<long>(), $"{name} {a["item"]} length");
            }
            if (t["pn"] is { } pn)
            {
                var node = flat.Values.SelectMany(i => i["nodes"]?.AsArray() ?? []).FirstOrDefault();
                c.Eq(pn["ip"]!.GetValue<string>(), node?["ipAddress"]?.GetValue<string>(), name + " IP");
                c.Eq(pn["pnName"]!.GetValue<string>(), node?["profinetDeviceName"]?.GetValue<string>(), name + " PROFINET name");
                c.Eq(pn["subnet"]!.GetValue<string>(), node?["subnet"]?.GetValue<string>(), name + " subnet");
            }
        }
        c.True(true, $"{zaps.Count} local GSD samples");
        c.Done();
    }
}
