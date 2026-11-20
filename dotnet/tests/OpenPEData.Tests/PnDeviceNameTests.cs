using System.Text.Json;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>PROFINET device name (also with automatic generation, where the stored name is stale) against Openness
/// TiaNetworkNode getAttributes (PnDeviceName) of every hardware sample with node truth files (210–228).</summary>
public static class PnDeviceNameTests
{
    [Test]
    public static void PnDeviceNamesAgainstOpenness()
    {
        var c = new Check("PROFINET device names = Openness (samples 210-228)");
        var count = 0;
        foreach (var dir in Directory.GetDirectories(Path.Combine(Program.Root, "samples"), "2*_truth").Order())
        {
            var zap = dir[..^"_truth".Length] + ".zap21";
            var files = Directory.GetFiles(dir, "node_*.json");
            if (files.Length == 0 || !File.Exists(zap))
                continue;
            PyDict proj;
            using (var p = TiaProject.Open(zap))
                proj = (PyDict)((PyDict)p.Raw(TiaContent.Structure, default)!)["project"]!;
            foreach (var f in files)
            {
                var e = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
                var at = e.GetProperty("attributes");
                if (!at.TryGetProperty("PnDeviceName", out var want))
                    continue;
                var dev = e.GetProperty("deviceKey").GetString()!;
                var path = e.GetProperty("deviceItemPath").GetString()!;
                var got = FindNode(proj, dev, path);
                c.Eq(want.GetString(), got?["profinetDeviceName"], $"{Path.GetFileName(dir)} {dev}/{path}");
                count++;
            }
        }
        c.True(count >= 23, $"{count} interfaces compared");
        c.Done();
    }

    static PyDict? FindNode(PyDict proj, string device, string path)
    {
        foreach (var d in ((List<object?>)proj["devices"]!).Cast<PyDict>())
        {
            if ((string?)d["name"] != device)
                continue;
            PyDict? hit = null;
            void Walk(PyDict it, string prefix)
            {
                var p = prefix.Length == 0 ? (string)it["name"]! : prefix + "/" + (string)it["name"]!;
                var nodes = (List<object?>)it["nodes"]!;
                if (p == path && nodes.Count > 0)
                    hit = (PyDict)nodes[0]!;
                foreach (var ch in ((List<object?>)it["items"]!).Cast<PyDict>())
                    Walk(ch, p);
            }
            foreach (var top in ((List<object?>)((PyDict)d["hardware"]!)["items"]!).Cast<PyDict>())
                Walk(top, "");
            return hit;
        }
        return null;
    }
}
