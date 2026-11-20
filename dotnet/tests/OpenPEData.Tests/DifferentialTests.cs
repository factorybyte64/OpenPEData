using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using OpenPEData.Format;
using OpenPEData.Model;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Regression baseline: for every sample input and every output mode the library (in process) and the CLI (as a
/// process) must produce exactly the bytes recorded in baseline.json (SHA-256 of stdout and stderr, exit code per input and
/// mode). The baseline was taken from the Python parser (commit 49e3508), which the port matched byte for byte before
/// Python was removed. With --ref DIR (stored CLI outputs: mode/input.out|.err|.rc) the outputs are compared with those
/// files instead, with the first differing line in the report.</summary>
public static class DifferentialTests
{
    public static readonly string[] Modes = ["json_keys", "json", "text", "verify_json", "verify_text", "xref", "proginfo"];

    /// <summary>Inputs of the reference run: file names with "/" written as "__".</summary>
    public static List<string> Inputs()
    {
        var all = Program.RefDir is not null
            ? Directory.EnumerateFiles(Path.Combine(Program.RefDir, "json"), "*.rc").Select(f => Path.GetFileNameWithoutExtension(f).Replace("__", "/"))
            : Baseline.Keys.Select(k => (string)k!);
        return all.Where(i => Program.Samples is null || i.Contains(Program.Samples, StringComparison.OrdinalIgnoreCase))
            .Where(i => Program.RefDir is not null || File.Exists(Path.Combine(Program.Root, i)) || Directory.Exists(Path.Combine(Program.Root, i)))
            .Order(StringComparer.Ordinal).ToList() is { Count: > 0 } l ? l : throw new TestFailure("no inputs (samples missing or filter too narrow)");
    }

    static PyDict? baseline;

    /// <summary>input -> mode -> [sha256 stdout, sha256 stderr, exit code]</summary>
    static PyDict Baseline => baseline ??= (PyDict)((PyDict)PyJson.Loads(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "baseline.json")))!)["inputs"]!;

    public static string Sha(byte[] b) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(b));

    public static string Sha(string s) => Sha(new UTF8Encoding(false).GetBytes(s));

    /// <summary>Expected (sha256 stdout, sha256 stderr, exit code) of one input and mode.</summary>
    static (string Out, string Err, string Rc) Expected(string mode, string input)
    {
        if (Program.RefDir is not null)
        {
            var b = Path.Combine(Program.RefDir, mode, input.Replace("/", "__"));
            return (Sha(File.ReadAllBytes(b + ".out")), Sha(File.ReadAllBytes(b + ".err")), File.ReadAllText(b + ".rc").Trim());
        }
        var e = (List<object?>)((PyDict)Baseline[input]!)[mode]!;
        return ((string)e[0]!, (string)e[1]!, Py.Str(e[2]));
    }

    /// <summary>Writes baseline.json from a directory of stored CLI outputs.</summary>
    public static void WriteBaseline(string refDir, string file)
    {
        var inputs = new PyDict();
        foreach (var rc in Directory.EnumerateFiles(Path.Combine(refDir, "json"), "*.rc").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(rc);
            var per = new PyDict();
            foreach (var m in Modes)
            {
                var b = Path.Combine(refDir, m, name);
                per[m] = Py.L(Sha(File.ReadAllBytes(b + ".out")), Sha(File.ReadAllBytes(b + ".err")), Py.ParseInt(File.ReadAllText(b + ".rc")));
            }
            inputs[name.Replace("__", "/")] = per;
        }
        File.WriteAllText(file, PyJson.Dumps(PyDict.Of("source", "OpenPEData.Cli (.NET) output stored by run_cli.py; regression baseline, see dotnet/README.md", "inputs", inputs), 1, true));
    }

    static string Crlf(string s) => (s + "\n").Replace("\n", "\r\n");

    static readonly Dictionary<string, (TiaContent Content, bool Json)> ModeContent = new()
    {
        ["json_keys"] = (TiaContent.StructureWithKeys, true), ["json"] = (TiaContent.Structure, true), ["text"] = (TiaContent.Structure, false),
        ["verify_json"] = (TiaContent.Integrity, true), ["verify_text"] = (TiaContent.Integrity, false),
        ["xref"] = (TiaContent.CrossReferences, true), ["proginfo"] = (TiaContent.ProgramInfo, true),
    };

    /// <summary>(stdout, stderr, exit code) the CLI would write for one mode, computed with the library API (a new TiaProject
    /// per mode, like one CLI run).</summary>
    public static (string Out, string Err, int Rc) Run(string mode, string path)
    {
        using var p = TiaProject.Open(path);
        if (mode is "json_keys" or "json" or "text")
            CheckTypes(p.Raw(ModeContent[mode].Content, default), "$");
        return Run(mode, p);
    }

    /// <summary>The CLI output of one mode from an open project (same steps as OpenPEData.Cli).</summary>
    static (string Out, string Err, int Rc) Run(string mode, TiaProject p)
    {
        var (content, json) = ModeContent[mode];
        var o = Crlf(json ? p.ToJson(content) : p.ToText(content));
        if (content == TiaContent.Integrity)
            return (o, "", p.IsIntact() ? 0 : 1);
        var err = new StringBuilder();
        if (content is TiaContent.Structure or TiaContent.StructureWithKeys)
            foreach (var e in p.GetIssues())
                err.Append(Crlf($"not readable: {e.Object}: {e.Message}"));
        return (o, err.ToString(), 0);
    }

    /// <summary>The result tree uses only the value types of the Python model (dict, list, tuple, scalars, bytes).</summary>
    public static void CheckTypes(object? o, string path)
    {
        switch (o)
        {
            case null or string or bool or long or ulong or BigInteger or double or byte[]:
                return;
            case PyDict d:
                foreach (var (k, v) in d)
                    CheckTypes(v, path + "." + Py.Str(k));
                return;
            case List<object?> l:
                for (var i = 0; i < l.Count; i++)
                    CheckTypes(l[i], $"{path}[{i}]");
                return;
            case PyTuple t:
                foreach (var x in t)
                    CheckTypes(x, path + "()");
                return;
            default:
                throw new TestFailure($"unexpected value type {o.GetType().Name} at {path}");
        }
    }

    static string Read(string mode, string input, string ext) =>
        File.ReadAllText(Path.Combine(Program.RefDir!, mode, input.Replace("/", "__") + ext), new UTF8Encoding(false));

    static void RunMode(string mode)
    {
        var inputs = Inputs();
        var c = new Check($"{mode}: {inputs.Count} inputs");
        Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, input =>
        {
            try
            {
                var (o, e, rc) = Run(mode, input);
                var (xo, xe, xrc) = Expected(mode, input);
                if (Sha(o) != xo && Program.RefDir is null)
                    c.Fail($"{input} stdout differs from the baseline");
                else if (Sha(o) != xo)
                {
                    var eo = Read(mode, input, ".out");
                    var lo = o.Split('\n');
                    var le = eo.Split('\n');
                    var i = 0;
                    while (i < Math.Min(lo.Length, le.Length) && lo[i] == le[i])
                        i++;
                    c.Fail($"{input} stdout differs at line {i + 1}: python {(i < le.Length ? le[i] : "<end>")} / .NET {(i < lo.Length ? lo[i] : "<end>")}");
                }
                else
                    c.True(true, input);
                c.Eq(xe, Sha(e), input + " stderr");
                c.Eq(xrc, rc.ToString(), input + " exit code");
            }
            catch (Exception ex)
            {
                c.Fail($"{input}: {ex.GetType().Name}: {ex.Message}");
            }
        });
        c.Done();
    }

    [Test] public static void JsonWithKeys() => WithRootCwd(() => RunMode("json_keys"));

    [Test] public static void Json() => WithRootCwd(() => RunMode("json"));

    [Test] public static void Text() => WithRootCwd(() => RunMode("text"));

    [Test] public static void VerifyJson() => WithRootCwd(() => RunMode("verify_json"));

    [Test] public static void VerifyText() => WithRootCwd(() => RunMode("verify_text"));

    [Test] public static void Xref() => WithRootCwd(() => RunMode("xref"));

    [Test] public static void ProgramInfo() => WithRootCwd(() => RunMode("proginfo"));

    /// <summary>The paths in the outputs are the relative input paths the reference run used (cwd = repository root).</summary>
    static void WithRootCwd(Action a)
    {
        var old = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Program.Root);
        try
        {
            a();
        }
        finally
        {
            Directory.SetCurrentDirectory(old);
        }
    }

    /// <summary>The CLI as a process: arguments, CRLF output, exit codes, usage, on a sample of inputs per mode.</summary>
    [Test]
    public static void CliProcess()
    {
        var exe = Path.Combine(Program.Root, "dotnet", "src", "OpenPEData.Cli", "bin", "Release", "net10.0", "OpenPEData.Cli.exe");
        if (!File.Exists(exe))
            throw new TestFailure("CLI not built: " + exe + " (dotnet build dotnet\\src\\OpenPEData.Cli -c Release)");
        var args = new Dictionary<string, string[]>
        {
            ["json_keys"] = ["--json", "--keys"], ["json"] = ["--json"], ["text"] = [], ["verify_json"] = ["--verify", "--json"],
            ["verify_text"] = ["--verify"], ["xref"] = ["--xref"], ["proginfo"] = ["--proginfo"],
        };
        var inputs = Inputs();
        var pick = inputs.Where((_, i) => i % Math.Max(1, inputs.Count / 25) == 0).ToList();
        var c = new Check($"CLI on {pick.Count} inputs x {Modes.Length} modes");
        Parallel.ForEach(pick.SelectMany(i => Modes.Select(m => (i, m))), new ParallelOptions { MaxDegreeOfParallelism = 6 }, x =>
        {
            var (o, e, rc) = Exec(exe, [x.i, .. args[x.m]]);
            var (xo, xe, xrc) = Expected(x.m, x.i);
            c.Eq(xo, Sha(o), $"{x.m} {x.i} stdout bytes");
            c.Eq(xe, Sha(e), $"{x.m} {x.i} stderr");
            c.Eq(xrc, rc.ToString(), $"{x.m} {x.i} exit code");
        });
        var (_, ue, urc) = Exec(exe, []);
        c.Eq(2, urc, "usage: exit code 2");
        c.True(Encoding.UTF8.GetString(ue).Contains("Usage: OpenPEData"), "usage text on stderr");
        c.Eq(2, Exec(exe, ["a", "b"]).Rc, "two paths: exit code 2");
        c.Done();
    }

    static (byte[] Out, byte[] Err, int Rc) Exec(string exe, string[] args)
    {
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = Program.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var os = new MemoryStream();
        var es = new MemoryStream();
        var t1 = p.StandardOutput.BaseStream.CopyToAsync(os);
        var t2 = p.StandardError.BaseStream.CopyToAsync(es);
        p.WaitForExit();
        Task.WaitAll(t1, t2);
        return (os.ToArray(), es.ToArray(), p.ExitCode);
    }

    /// <summary>One TiaProject per input for all seven modes, in reverse order (program information first, as the viewer may
    /// ask): reusing the loaded model gives the same bytes as a fresh run per mode.</summary>
    [Test]
    public static void SessionReuse()
    {
        WithRootCwd(() =>
        {
            var inputs = Inputs();
            var c = new Check($"one TiaProject for all modes: {inputs.Count} inputs");
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2) }, input =>
            {
                try
                {
                    using var p = TiaProject.Open(input);
                    foreach (var mode in Modes.Reverse())
                    {
                        var (o, e, rc) = Run(mode, p);
                        var (xo, xe, xrc) = Expected(mode, input);
                        c.Eq(xo, Sha(o), $"{mode} {input} stdout");
                        c.Eq(xe, Sha(e), $"{mode} {input} stderr");
                        c.Eq(xrc, rc.ToString(), $"{mode} {input} exit code");
                    }
                }
                catch (Exception ex)
                {
                    c.Fail($"{input}: {ex.GetType().Name}: {ex.Message}");
                }
            });
            c.Done();
        });
    }

    static T? Throws<T>(Check c, Action a, string what) where T : Exception
    {
        try
        {
            a();
        }
        catch (T e)
        {
            c.True(true, what);
            return e;
        }
        catch (Exception e)
        {
            c.Fail($"{what}: {typeof(T).Name} expected, got {e.GetType().Name}: {e.Message}");
            return null;
        }
        c.Fail($"{what}: {typeof(T).Name} expected, nothing thrown");
        return null;
    }

    /// <summary>Public API: JsonObject results agree with the CLI JSON, version and kind detection, own exceptions,
    /// cancellation and Dispose.</summary>
    [Test]
    public static void PublicApi()
    {
        WithRootCwd(() =>
        {
            var c = new Check("TiaProject API");
            foreach (var input in Inputs().Where((_, i) => i % 40 == 0))
            {
                using var p = TiaProject.Open(input);
                c.Eq(TiaVersion.V21, p.Version, input + " version");
                c.Eq(TiaProject.Probe(input), p.Info, input + " Probe = Info");
                c.Eq(input.EndsWith(".zap21") || input.EndsWith(".zal21"), p.Info.IsArchive, input + " archive");
                var node = p.GetStructure();
                c.True(node["statistics"] is JsonObject, input + " JsonObject has statistics");
                c.True(node.ToJsonString().Length > 2, input + " JsonObject serializes");
                c.Eq(Expected("json", input).Out, Sha(Crlf(p.ToJson())), input + " ToJson");
                c.Eq(Expected("text", input).Out, Sha(Crlf(p.ToText())), input + " ToText");
                c.Eq(p.IsIntact(), p.Verify()["ok"]!.GetValue<bool>(), input + " Verify ok");
                c.True(p.GetProgramInfo()["plcs"] is JsonArray, input + " GetProgramInfo");
                c.True(!ReferenceEquals(p.GetStructure(), node), input + " each call a new JsonObject");
            }
            foreach (var lib in Inputs().Where(i => i.EndsWith(".zal21")).Take(1))
                c.Eq(TiaProjectKind.GlobalLibrary, TiaProject.Probe(lib)?.Kind, lib + " kind");
            var tmp = Directory.CreateTempSubdirectory("openpedata-");
            try
            {
                c.True(TiaProject.Probe(tmp.FullName) is null, "Probe: empty folder is no project");
                Throws<NotATiaProjectException>(c, () => TiaProject.Open(tmp.FullName), "Open: empty folder");
                Throws<NotATiaProjectException>(c, () => TiaProject.Open(Path.Combine(tmp.FullName, "missing.zap21")), "Open: missing file");
                var junk = Path.Combine(tmp.FullName, "junk.zap21");
                File.WriteAllText(junk, "no zip");
                Throws<NotATiaProjectException>(c, () => TiaProject.Open(junk), "Open: no zip");
                var src = Inputs().First(i => i.EndsWith(".zap21"));
                var v22 = Path.Combine(tmp.FullName, "copy.zap22");
                File.Copy(src, v22);
                c.Eq(new TiaVersion(22), TiaProject.Probe(v22)?.Version, "Probe: .zap22 is V22");
                var ex = Throws<UnsupportedVersionException>(c, () => TiaProject.Open(v22), "Open: V22 not supported");
                c.Eq(new TiaVersion(22), ex?.Version, "UnsupportedVersionException.Version");
                using (var p22 = TiaProject.Open(v22, new OpenOptions { AllowUnknownVersion = true }))
                {
                    c.Eq(TiaVersion.V21, p22.Version, "AllowUnknownVersion: read as V21");
                    c.Eq(1, p22.Warnings.Count, "AllowUnknownVersion: one warning");
                    c.Eq(Expected("json", src).Out, Sha(Crlf(p22.ToJson())), "AllowUnknownVersion: same structure as the V21 copy");
                }
                var plfOnly = Directory.CreateDirectory(Path.Combine(tmp.FullName, "plfonly", "System"));
                File.WriteAllBytes(Path.Combine(plfOnly.FullName, "PEData.plf"), []);
                var plfDir = plfOnly.Parent!.FullName;
                c.Eq(null, TiaProject.Probe(plfDir)?.Version, "Probe: folder without .apNN has no version");
                Throws<UnsupportedFormatException>(c, () => TiaProject.Open(plfDir), "Open: version unknown");
                Throws<OpenPEDataException>(c, () => TiaProject.Open(plfDir, new OpenOptions { AllowUnknownVersion = true }),
                    "Open: empty plf is an OpenPEDataException");
                Throws<CorruptProjectException>(c, () => TiaProject.Open(src, new OpenOptions { MaxEntryBytes = 1000 }),
                    "Open: archive entry above MaxEntryBytes");
                var ze = Throws<CorruptProjectException>(c, () => TiaProject.Open(src, new OpenOptions { MaxDecompressedBytes = 64 }),
                    "Open: metamodel above MaxDecompressedBytes");
                c.True(ze?.InnerException?.Message.Contains("exceeds the limit") == true, "limit named in the inner exception");
                using var cts = new CancellationTokenSource();
                cts.Cancel();
                Throws<OperationCanceledException>(c, () => TiaProject.Open(src, null, cts.Token), "Open: canceled");
                var pc = TiaProject.Open(src);
                Throws<OperationCanceledException>(c, () => pc.GetStructure(false, cts.Token), "GetStructure: canceled");
                pc.Dispose();
                Throws<ObjectDisposedException>(c, () => pc.GetStructure(), "GetStructure after Dispose");
            }
            finally
            {
                tmp.Delete(true);
            }
            c.Done();
        });
    }
}
