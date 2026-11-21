using System.Xml.Linq;

namespace OpenPEData.Tests;

/// <summary>LAD/FBD networks as SimaticML (TiaProject.GetLadFbdSimaticMl) against the Openness exports in the truth folders.</summary>
public static class SimaticMlTests
{
    /// <summary>Every &lt;FlgNet&gt; of every block export in samples\*_truth equals the generated one element by element
    /// (names, attribute order and values, text); whitespace is ignored. OPENPEDATA_SIMATICML_DIFF=&lt;file&gt; writes the
    /// differing pairs.</summary>
    [Test]
    public static void LadFbdAgainstExport()
    {
        var c = new Check("LAD/FBD/GRAPH SimaticML = Openness export");
        var diffFile = Environment.GetEnvironmentVariable("OPENPEDATA_SIMATICML_DIFF");
        using var diff = diffFile is null ? null : new StreamWriter(diffFile);
        var samples = Path.Combine(Program.Root, "samples");
        foreach (var truth in Directory.GetDirectories(samples, "*_truth").OrderBy(x => x, StringComparer.Ordinal))
        {
            var sample = Path.GetFileName(truth)[..^"_truth".Length];
            var zap = Program.SampleArchive(sample);
            if (zap is null ||(Program.Samples is { } f && !sample.Contains(f, StringComparison.Ordinal)))
                continue;
            var exports = new Dictionary<string, List<XElement?>>();
            foreach (var x in Directory.GetFiles(truth, "*.xml", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(x);
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }
                var blk = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal));
                var name = blk?.Element("AttributeList")?.Element("Name")?.Value;
                if (blk is null || name is null || exports.ContainsKey(name))
                    continue;
                var nets = blk.Descendants("SW.Blocks.CompileUnit")
                    .Select(cu => cu.Descendants().FirstOrDefault(e => e.Name.LocalName is "Graph")
                        ?? cu.Descendants().FirstOrDefault(e => e.Name.LocalName == "FlgNet")).ToList();
                if (nets.Any(n => n is not null))
                    exports[name] = nets;
            }
            if (exports.Count == 0)
                continue;
            using var p = TiaProject.Open(zap);
            var got = p.SimaticMlNetworks(default);
            foreach (var (name, want) in exports)
            {
                if (!got.TryGetValue(name, out var mine))
                {
                    c.Fail($"{sample} {name}: no LAD/FBD networks generated");
                    continue;
                }
                for (var i = 0; i < want.Count; i++)
                {
                    if (want[i] is null)
                        continue;
                    var g = i < mine.Count ? mine[i] : null;
                    var ok = g is not null && XNode.DeepEquals(Normalize(want[i]!), Normalize(g));
                    c.True(ok, $"{sample} {name}/{i + 1} {want[i]!.Name.LocalName}");
                    if (!ok && diff is not null)
                        diff.WriteLine($"=== {sample} {name}/{i + 1}\n--- want\n{Normalize(want[i]!)}\n--- got\n{(g is null ? "(none)" : Normalize(g))}\n");
                }
            }
        }
        c.Done();
    }

    /// <summary>Copy without whitespace-only text and with trimmed text.</summary>
    static XElement Normalize(XElement e) =>
        new(e.Name, e.Attributes().Where(a => !a.IsNamespaceDeclaration),
            e.Nodes().Select(n => n switch
            {
                XElement x => (object)Normalize(x),
                XText t when !string.IsNullOrWhiteSpace(t.Value) => new XText(t.Value.Trim()),
                _ => null,
            }).Where(x => x is not null));
}
