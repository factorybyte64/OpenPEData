using System.Text;
using System.Text.Json.Nodes;

namespace OpenPEData.Viewer;

/// <summary>
/// Runs the parser (the library OpenPEData.dll, dotnet\src\OpenPEData) in process: the project is opened once
/// (TiaProject.Open) and every later run (--verify, --xref, --proginfo) reads from the same loaded project. Results are the
/// JSON structure the CLI writes for the same flags.
/// </summary>
public static class ParserRunner
{
    public sealed record Result(JsonNode? Json, string StdErr, int ExitCode);

    /// <summary>Parser in use, for the info dialog.</summary>
    public static string BackendDescription() =>
        $"OpenPEData.dll {typeof(TiaProject).Assembly.GetName().Version} (TIA Portal {string.Join(", ", TiaVersion.Supported)})";

    /// <summary>Opens the project and reads its structure (CLI --json). Project is null if it could not be opened; the error
    /// is then in Result.StdErr.</summary>
    public static Task<(TiaProject? Project, Result Result)> OpenAsync(string projectPath, CancellationToken ct) =>
        Task.Run(() =>
        {
            TiaProject? p = null;
            try
            {
                p = TiaProject.Open(projectPath, new OpenOptions { AllowUnknownVersion = true }, ct);
                var res = Run(p, ["--json"], ct);
                if (p.Warnings.Count > 0)
                    res = res with { StdErr = string.Concat(p.Warnings.Select(w => "warning: " + w + Environment.NewLine)) + res.StdErr };
                return (res.Json is null ? null : p, res);
            }
            catch (OperationCanceledException)
            {
                p?.Dispose();
                throw;
            }
            catch (OpenPEDataException ex)
            {
                return ((TiaProject?)null, new Result(null, ex.Message + Environment.NewLine + ex, 1));
            }
        }, ct);

    /// <summary>Runs the parser on an open project with extra flags (e.g. "--verify").</summary>
    public static Task<Result> RunAsync(TiaProject project, CancellationToken ct, params string[] flags) =>
        Task.Run(() => Run(project, flags, ct), ct);

    /// <summary>LAD/FBD networks as SimaticML (--simaticml) under "simaticml" of each block with such networks; only for the
    /// viewer's SimaticML tab, the structure output itself has no such key.</summary>
    static void AddSimaticMl(JsonNode? node, IReadOnlyDictionary<string, IReadOnlyList<string?>> xml)
    {
        if (node is JsonObject o)
        {
            if (o["networks"] is JsonArray && o["name"] is JsonValue v && v.TryGetValue<string>(out var name) && xml.TryGetValue(name, out var nets))
                o["simaticml"] = new JsonArray(nets.Select(n => (JsonNode?)n).ToArray());
            foreach (var kv in o.ToList())
                AddSimaticMl(kv.Value, xml);
        }
        else if (node is JsonArray a)
            foreach (var x in a.ToList())
                AddSimaticMl(x, xml);
    }

    /// <summary>Same results as the CLI flags: stderr lines for unreadable objects, exit code 1 for a failed --verify,
    /// an exception becomes stderr text with exit code 1.</summary>
    static Result Run(TiaProject project, string[] flags, CancellationToken ct)
    {
        try
        {
            JsonNode? json;
            var err = new StringBuilder();
            var rc = 0;
            if (flags.Contains("--verify"))
            {
                json = project.Verify(ct);
                rc = project.IsIntact(ct) ? 0 : 1;
            }
            else if (flags.Contains("--xref"))
                json = project.GetCrossReferences(ct);
            else if (flags.Contains("--proginfo"))
                json = project.GetProgramInfo(ct);
            else
            {
                json = project.GetStructure(flags.Contains("--keys"), ct);
                foreach (var e in project.GetIssues(ct))
                    err.AppendLine($"not readable: {e.Object}: {e.Message}");
                try
                {
                    AddSimaticMl(json, project.GetLadFbdSimaticMl(ct));
                }
                catch (OpenPEDataException e)
                {
                    err.AppendLine("SimaticML: " + e.Message);
                }
            }
            ct.ThrowIfCancellationRequested();
            return new Result(json, err.ToString(), rc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Result(null, ex.ToString(), 1);
        }
    }
}
