using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenPEData.Tests;

/// <summary>The typed results of the facade (<see cref="TiaProject.GetContent"/>, <see cref="TiaProject.GetIntegrityReport"/>):
/// on every sample they agree with the JSON structure, and on selected samples with the Openness truth.</summary>
public static class TypedResultTests
{
    static string Sample(string name) => Path.Combine(Program.Root, "samples", name);

    static int Count(JsonObject? o, string key) => (o?[key] as JsonArray)?.Count ?? 0;

    /// <summary>Every sample: the typed content has as many devices, PLCs, blocks, tag tables, tags, data types, HMI
    /// software, HMI tags, screens, screen items and libraries as the JSON structure, names equal, and the typed
    /// integrity report agrees with IsIntact.</summary>
    [Test]
    public static void AllSamplesAgreeWithJson()
    {
        var c = new Check("typed content equals the JSON structure on every sample");
        Parallel.ForEach(DifferentialTests.Inputs().Select(i => Path.Combine(Program.Root, i)), new ParallelOptions { MaxDegreeOfParallelism = 6 }, s =>
        {
            using var p = TiaProject.Open(s, new OpenOptions { AllowUnknownVersion = true });
            var content = p.GetContent();
            var json = p.GetStructure();
            var project = (JsonObject)json["project"]!;
            c.Eq((string?)project["name"], content.Name, s + " name");
            var devices = ((JsonArray)project["devices"]!).Cast<JsonObject>().ToList();
            c.Eq(devices.Count, content.Devices.Count, s + " devices");
            foreach (var (dj, d) in devices.Zip(content.Devices))
            {
                c.Eq((string?)dj["name"], d.Name, s + " device name");
                var plcs = ((JsonArray)dj["plcs"]!).Cast<JsonObject>().ToList();
                c.Eq(plcs.Count, d.Plcs.Count, s + " plcs of " + d.Name);
                foreach (var (pj, plc) in plcs.Zip(d.Plcs))
                {
                    c.Eq(Count(pj, "blocks"), plc.Blocks.Count, s + " blocks of " + plc.Name);
                    c.Eq(Count(pj, "tagTables"), plc.TagTables.Count, s + " tag tables of " + plc.Name);
                    c.Eq(((JsonArray)pj["tagTables"]!).Sum(t => Count((JsonObject)t!, "tags")), plc.Tags.Count(), s + " tags of " + plc.Name);
                    c.Eq(Count(pj, "types"), plc.DataTypes.Count, s + " data types of " + plc.Name);
                    c.True(plc.Device == d, s + " plc device");
                    foreach (var (bj, b) in ((JsonArray)pj["blocks"]!).Cast<JsonObject>().Zip(plc.Blocks))
                    {
                        c.Eq((string?)bj["name"], b.Name, s + " block name");
                        c.Eq((string?)bj["type"], b.Type, s + " block type " + b.Name);
                        c.Eq(Count(bj, "networks"), b.NetworkCount, s + " networks of " + b.Name);
                    }
                }
                var hmis = ((JsonArray)dj["hmi"]!).Cast<JsonObject>().ToList();
                c.Eq(hmis.Count, d.Hmis.Count, s + " hmi of " + d.Name);
                foreach (var (hj, h) in hmis.Zip(d.Hmis))
                {
                    c.Eq(Count(hj, "screens"), h.Screens.Count, s + " screens of " + h.Name);
                    c.Eq(((JsonArray)hj["screens"]!).Sum(x => Count((JsonObject)x!, "items")), h.Screens.Sum(x => x.Items.Count), s + " items of " + h.Name);
                    c.Eq(((JsonArray)hj["tagTables"]!).Sum(t => Count((JsonObject)t!, "tags")), h.Tags.Count(), s + " hmi tags of " + h.Name);
                    c.Eq(Count(hj, "connections"), h.Connections.Count, s + " connections of " + h.Name);
                }
            }
            c.Eq(Count((JsonObject)json["library"]!, "libraries"), content.Libraries.Count, s + " libraries");
            c.Eq((long?)json["statistics"]!["records"], content.Statistics.Records, s + " records");
            c.True(ReferenceEquals(content, p.GetContent()), s + " content computed once");
            var report = p.GetIntegrityReport();
            c.Eq(p.IsIntact(), report.IsIntact, s + " integrity");
            c.Eq(0, report.Problems.Count(), s + " no integrity problems");
            c.True(report.Files.Count > 0 && report.Files.All(f => f.Commits.Count > 0 && f.Commits.All(x => x.SignatureOk == true && x.ChainOk == true)),
                s + " commits signed and chained");
        });
        c.Done();
    }

    /// <summary>031_lad_all: one S7-1500 with blocks, the default tag table and UDT_Motor.</summary>
    [Test]
    public static void PlcContent()
    {
        using var p = TiaProject.Open(Sample("031_lad_all.zap21"));
        var c = p.GetContent();
        Assert.Eq(1, c.Devices.Count, "devices");
        var d = c.Devices[0];
        Assert.Eq("PLC_1", d.Name, "device name");
        Assert.Eq("S7-1500 station", d.TypeName, "device type");
        Assert.True(d.Hardware is not null, "hardware json");
        var plc = c.Plcs.Single();
        Assert.Eq("CPU 1513-1 PN", plc.TypeName, "cpu type");
        Assert.Eq("6ES7 513-1AL02-0AB0", plc.OrderNumber, "order number");
        Assert.Eq("V2.9", plc.Firmware, "firmware");
        var fb = plc.Blocks.First(b => b.Name == "FB_Empty");
        Assert.Eq("FB", fb.Type, "FB_Empty type");
        Assert.Eq(1L, fb.Number, "FB_Empty number");
        Assert.Eq("SCL", fb.Language, "FB_Empty language");
        Assert.Eq("FB_Empty [FB1]", fb.ToString(), "block text");
        Assert.True(fb.Interface is not null && fb.Networks is not null, "interface and networks json");
        Assert.Eq(5, plc.Blocks.Count(b => b.Type == "GlobalDB"), "global DBs");
        Assert.True(plc.Blocks.Where(b => b.Type == "InstanceDB").All(b => b.InstanceOf is not null), "instance DBs name their FB");
        var tag = plc.Tags.First(t => t.Name == "Tag_1");
        Assert.Eq("Word", tag.DataType, "Tag_1 type");
        Assert.Eq("%MW100", tag.Address, "Tag_1 address");
        Assert.Eq(true, tag.IsHmiAccessible, "Tag_1 hmi accessible");
        var udt = plc.DataTypes.Single(t => t.Name == "UDT_Motor");
        var running = udt.Members.First(m => m.Name == "Running");
        Assert.Eq("Bool", running.DataType, "Running type");
        Assert.True(running.Comment.Get("en-US") is { Length: > 0 }, "Running comment");
        Assert.Eq("en-US", string.Join(",", c.Languages.Active), "active languages");
        Assert.Eq("en-US", c.Languages.Editing, "editing language");
        Assert.Eq("Project library", c.Libraries.Single().Name, "project library");
        Assert.Eq(77, c.Libraries[0].Types.Count, "library types");
        Assert.Eq(3585L, c.Statistics.Records, "records");
    }

    /// <summary>063_multi_plc: the four PLC devices equal the Openness device list (devices.json of the truth).</summary>
    [Test]
    public static void MultiPlcDevicesAgainstOpenness()
    {
        using var p = TiaProject.Open(Sample("063_multi_plc.zap21"));
        using var truth = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.Root, "samples", "063_multi_plc_truth", "devices.json")));
        var expected = truth.RootElement.GetProperty("items").EnumerateArray()
            .Where(i => i.TryGetProperty("softwareKind", out var k) && k.GetString() == "PlcSoftware")
            .Select(i => i.GetProperty("softwareName").GetString()).Order(StringComparer.Ordinal).ToArray();
        Assert.Eq(4, expected.Length, "truth PLCs");
        Assert.Eq(string.Join(",", expected), string.Join(",", p.GetContent().Plcs.Select(x => x.Name).Order(StringComparer.Ordinal)), "PLC names");
    }

    /// <summary>100_hmi_device, 103_hmi_tag_ext, 122_hmi_item_button: HMI device, external tag and a button.</summary>
    [Test]
    public static void HmiContent()
    {
        using (var p = TiaProject.Open(Sample("100_hmi_device.zap21")))
        {
            var h = p.GetContent().Hmis.Single();
            Assert.Eq("HMI_1", h.Name, "hmi name");
            Assert.Eq("MTP700 Unified Comfort", h.TypeName, "hmi type");
            Assert.Eq("6AV2 128-3GB06-0AXx", h.OrderNumber, "hmi order number");
            Assert.Eq("21.0.1.0", h.Firmware, "hmi firmware");
            Assert.Eq("HMI_Connection_1", h.Connections.Single().Name, "connection");
            Assert.Eq(29, h.SystemTags.Count, "system tags");
            Assert.True(h.Device.Name == "HMI_1", "hmi device");
        }
        using (var p = TiaProject.Open(Sample("103_hmi_tag_ext.zap21")))
        {
            var t = p.GetContent().Hmis.Single().Tags.Single(x => x.Name == "HmiTag_Ext1");
            Assert.Eq("Bool", t.DataType, "tag type");
            Assert.Eq("HMI_Connection_1", t.Connection, "tag connection");
            Assert.Eq("PLC_1", t.PlcName, "tag plc");
            Assert.Eq("Tag_Bool_1", t.PlcTag, "tag plc tag");
            Assert.Eq("%M0.0", t.Address, "tag address");
            Assert.Eq("T1s", t.AcquisitionCycle, "tag cycle");
            Assert.Eq(false, t.IsInternal, "external tag");
        }
        using (var p = TiaProject.Open(Sample("122_hmi_item_button.zap21")))
        {
            var screen = p.GetContent().Hmis.Single().Screens.Single();
            Assert.Eq("Screen_1", screen.Name, "screen name");
            Assert.Eq(800L, screen.Width, "screen width");
            Assert.Eq(480L, screen.Height, "screen height");
            Assert.Eq(32, screen.Layers.Count, "layers");
            var b = screen.Items.Single(i => i.Name == "Button_1");
            Assert.Eq("HmiButton", b.Type, "button type");
            Assert.Eq("0,0,160,40", $"{b.Left},{b.Top},{b.Width},{b.Height}", "button bounds");
            Assert.Eq("Text", b.Text.Get("en-US"), "button text");
            Assert.Eq(0L, b.LayerIndex, "button layer");
            Assert.True(b.Properties is not null && b.Dynamizations is not null && b.Events is not null, "button json");
        }
    }

    /// <summary>A global library: kind, name and the library itself as the only library.</summary>
    [Test]
    public static void GlobalLibraryContent()
    {
        using var p = TiaProject.Open(Sample("L31_glib_type.zal21"));
        var c = p.GetContent();
        Assert.Eq(TiaProjectKind.GlobalLibrary, p.Kind, "kind");
        Assert.Eq(0, c.Devices.Count, "no devices");
        Assert.Eq(c.Name, c.Libraries.Single().Name, "library name");
    }

    /// <summary>Integrity report of an intact sample: one plf, signed commits, index check if present.</summary>
    [Test]
    public static void IntegrityReportContent()
    {
        using var p = TiaProject.Open(Sample("000_empty2"));
        var r = p.GetIntegrityReport();
        Assert.True(r.IsIntact, "intact");
        var f = r.Files.Single();
        Assert.Eq("System/PEData.plf", f.File.Replace('\\', '/'), "file");
        Assert.True(f.HeaderHashOk == true && f.RecordHashErrorCount == 0 && f.TrailingBytes == 0, "file checks");
        Assert.True(f.Commits.All(x => x.Time is { Year: >= 2020 }), "commit times");
        Assert.True(r.Index is { IsIntact: true, Entries: > 0 }, "index check");
        Assert.True(ReferenceEquals(r, p.GetIntegrityReport()), "report computed once");
    }

    /// <summary>Typed results after Dispose throw ObjectDisposedException; MultilingualText falls back to the first language.</summary>
    [Test]
    public static void DisposeAndTexts()
    {
        var p = TiaProject.Open(Sample("005_tag_int.zap21"));
        _ = p.GetContent();
        p.Dispose();
        Assert.Throws<ObjectDisposedException>(() => p.GetContent(), "content after Dispose");
        Assert.Throws<ObjectDisposedException>(() => p.GetIntegrityReport(), "integrity after Dispose");
        var t = MultilingualText.From(JsonNode.Parse("""{"en-GB":"Remark","en-US":"Comment"}"""));
        Assert.Eq("Comment", t.Get("en-US"), "exact culture");
        Assert.Eq("Remark", t.Get("fr-FR"), "fallback to the first language");
        Assert.Eq(2, t.Count, "count");
        Assert.Eq(null, MultilingualText.Empty.Get(), "empty");
        Assert.Eq(0, MultilingualText.From(JsonNode.Parse("[1]")).Count, "not a text");
    }
}
