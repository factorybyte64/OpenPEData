using System.Text;

namespace OpenPEData.Tests;

/// <summary>Block sources (TiaProject.GetSources / ExportSources) against the sources TIA Portal generated for the samples
/// ("Generate source from blocks": the .awl, .scl and .db files in samples\*_truth).</summary>
public static class SourceExportTests
{
    /// <summary>Every TIA source file in a truth folder equals the generated source byte for byte (UTF-8 with BOM, CRLF).</summary>
    [Test]
    public static void SourcesAgainstTia()
    {
        var c = new Check("block sources = TIA generated sources");
        var samples = Path.Combine(Program.Root, "samples");
        if (!Directory.Exists(samples))
        {
            c.Done(); // the test projects are local only
            return;
        }
        var enc = new UTF8Encoding(true);
        foreach (var truth in Directory.GetDirectories(samples, "*_truth").OrderBy(x => x, StringComparer.Ordinal))
        {
            var sample = Path.GetFileName(truth)[..^"_truth".Length];
            var zap = Path.Combine(samples, sample + ".zap21");
            var files = Directory.GetFiles(truth, "*.*", SearchOption.AllDirectories)
                .Where(f => Path.GetExtension(f) is ".awl" or ".scl" or ".db" or ".udt").OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (files.Count == 0 || !File.Exists(zap) || (Program.Samples is { } flt && !sample.Contains(flt, StringComparison.Ordinal)))
                continue;
            using var p = TiaProject.Open(zap);
            var set = p.GetSources();
            foreach (var f in files)
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var src = set.Files.FirstOrDefault(s => s.Name == name && s.Extension == Path.GetExtension(f));
                if (src is null)
                {
                    c.Fail($"{sample}: no source for {Path.GetFileName(f)}");
                    continue;
                }
                var want = File.ReadAllBytes(f);
                var got = enc.GetPreamble().Concat(enc.GetBytes(src.Content)).ToArray();
                c.True(want.AsSpan().SequenceEqual(got), $"{sample}: {Path.GetFileName(f)}");
            }
        }
        if (Program.Samples is null && File.Exists(Path.Combine(samples, "064_stl_all.zap21")))
            c.True(c.Count > 90, "at least 90 TIA sources compared");
        c.Done();
    }

    /// <summary>ExportSources writes one file per source below PLC\Program blocks and PLC\PLC data types; system and
    /// know-how protected blocks are skipped.</summary>
    [Test]
    public static void ExportWritesFiles()
    {
        var c = new Check("ExportSources writes the files");
        var zap = Path.Combine(Program.Root, "samples", "064_stl_all.zap21");
        if (!File.Exists(zap))
        {
            c.Done(); // the test projects are local only
            return;
        }
        var dir = Path.Combine(Path.GetTempPath(), "openpedata-sources-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var p = TiaProject.Open(zap);
            var r = p.ExportSources(dir);
            c.Eq(r.Sources.Count, r.Paths.Count, "one path per source");
            c.True(r.Paths.All(File.Exists), "all files exist");
            c.True(r.Paths.Any(x => x.EndsWith(Path.Combine("PLC_1", "Program blocks", "FB_StlCall.awl"), StringComparison.Ordinal)), "FB_StlCall.awl");
            c.True(r.Paths.Any(x => x.EndsWith(Path.Combine("PLC_1", "Program blocks", "BlockGroup_1", "DB_InGroup.db"), StringComparison.Ordinal)), "group folder");
            c.True(r.Paths.Any(x => x.EndsWith(Path.Combine("PLC_1", "PLC data types", "UDT_Motor.udt"), StringComparison.Ordinal)), "UDT_Motor.udt");
            c.True(r.Paths.Any(x => x.EndsWith("Main.xml", StringComparison.Ordinal)), "LAD OB as SimaticML");
            var tags = r.Paths.FirstOrDefault(x => x.EndsWith(Path.Combine("PLC_1", "PLC tags", "Default tag table.xml"), StringComparison.Ordinal));
            c.True(tags is not null && File.ReadAllText(tags).Contains("<SW.Tags.PlcTagTable"), "tag table as SimaticML");
            var main = File.ReadAllText(r.Paths.First(x => x.EndsWith("Main.xml", StringComparison.Ordinal)));
            c.True(main.Contains("<Namespace />") && main.Contains("<SecondaryType>ProgramCycle</SecondaryType>") && main.Contains("Informative=\"true\""), "OB import attributes");
            var udt = File.ReadAllText(r.Paths.First(x => x.EndsWith("UDT_Motor.udt", StringComparison.Ordinal)));
            c.True(udt.StartsWith("TYPE \"UDT_Motor\"\r\n", StringComparison.Ordinal) && udt.Contains("Speed : Real := 1500.0;   // Speed setpoint"), "UDT text");
            var db = File.ReadAllText(r.Paths.First(x => x.EndsWith("DB_Global.db", StringComparison.Ordinal)));
            c.True(db.Contains("xBool : Bool := TRUE;   // Bool comment") && db.Contains("aInt[1] := 11;"), "global DB text");
            c.True(r.Skipped.All(s => s.Reason is "know-how protected" or "system block" or "system data type"), "skip reasons");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        c.Done();
    }
}
