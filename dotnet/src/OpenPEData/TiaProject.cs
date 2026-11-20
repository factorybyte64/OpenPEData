using System.Text.Json;
using System.Text.Json.Nodes;
using OpenPEData.Format;
using OpenPEData.Model;
using OpenPEData.Runtime;

namespace OpenPEData;

/// <summary>A TIA Portal project or global library, opened once: project folder, .ap21 file, .zap21 archive, library folder
/// or .zal21 archive. The records are read and hashed once in <see cref="Open"/>; each result is computed on first request
/// and kept. Results are System.Text.Json trees with the structure of the command line JSON output.
/// <para>Calls on one instance are serialized (a second thread waits); different instances run in parallel.</para></summary>
/// <example><code>
/// using var p = TiaProject.Open(@"C:\Projects\Plant.zap21");
/// JsonObject structure = p.GetStructure();
/// bool intact = p.IsIntact();
/// </code></example>
public sealed class TiaProject : IDisposable
{
    readonly Lock gate = new();
    readonly Dictionary<TiaContent, object?> results = [];
    readonly string corePath;
    readonly ReadLimits limits;
    ProjectModel? model;
    bool disposed;

    TiaProject(TiaProjectInfo info, TiaVersion version, ProjectModel model, string corePath, IReadOnlyList<string> warnings, ReadLimits limits)
    {
        this.limits = limits;
        Info = info;
        Version = version;
        this.model = model;
        this.corePath = corePath;
        Warnings = warnings;
    }

    /// <summary>The path as given to Open.</summary>
    public string Path => Info.Path;

    /// <summary>What the path is (version as detected, kind, archive or folder).</summary>
    public TiaProjectInfo Info { get; }

    /// <summary>TIA version the project is read as; differs from Info.Version only with OpenOptions.AllowUnknownVersion.</summary>
    public TiaVersion Version { get; }

    /// <summary>Project or global library.</summary>
    public TiaProjectKind Kind => Info.Kind;

    /// <summary>Notes about the opening itself, e.g. a version read with AllowUnknownVersion. Objects that cannot be read are
    /// in <see cref="GetIssues"/>.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Recognizes a project or global library from its file names without loading it; null if the path is neither.</summary>
    public static TiaProjectInfo? Probe(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return TiaProjectInfo.Of(path);
    }

    /// <summary>Opens a project folder, .ap21 file, .zap21 archive, library folder or .zal21 archive and reads its records.</summary>
    /// <exception cref="NotATiaProjectException">The path is no TIA project or library.</exception>
    /// <exception cref="UnsupportedVersionException">TIA version not supported (see OpenOptions.AllowUnknownVersion).</exception>
    /// <exception cref="UnsupportedFormatException">TIA version cannot be told (see OpenOptions.AllowUnknownVersion).</exception>
    /// <exception cref="CorruptProjectException">The records cannot be read.</exception>
    public static TiaProject Open(string path, OpenOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        options ??= new OpenOptions();
        cancellationToken.ThrowIfCancellationRequested();
        var info = TiaProjectInfo.Of(path)
            ?? throw new NotATiaProjectException($"No TIA Portal project or library (no folder or archive with System\\PEData.plf): {path}", path);
        var warnings = new List<string>();
        var profile = VersionProfile.Newest;
        if (info.Version is not { } v)
        {
            if (!options.AllowUnknownVersion)
                throw new UnsupportedFormatException($"The TIA Portal version cannot be told (no .apNN or .alNN file): {path}", path);
            warnings.Add($"TIA Portal version unknown, read as {profile.Version}.");
        }
        else if (!v.IsSupported)
        {
            if (!options.AllowUnknownVersion)
                throw new UnsupportedVersionException(v, path);
            warnings.Add($"TIA Portal {v} is not supported, read as {profile.Version}; results may be incomplete or wrong.");
        }
        else
            profile = VersionProfile.For(v)!;
        // the reader takes a folder, an .apNN file or an archive; a library file .alNN stands for its folder
        var corePath = !info.IsArchive && File.Exists(path) && !ProjectFiles.IsProjectFile(path) ? ProjectFiles.DirOf(path) : path;
        ProjectModel model;
        var limits = options.Limits;
        try
        {
            using var scope = limits.Use();
            model = new ProjectModel(new Project(corePath, true, profile));
        }
        catch (PyStopIteration e)
        {
            throw new NotATiaProjectException($"No metamodel record in System\\PEData.plf: {path}", path, e);
        }
        catch (Exception e) when (IsReaderError(e))
        {
            throw new CorruptProjectException($"The project cannot be read: {Message(e)}", path, e);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new TiaProject(info, profile.Version, model, corePath, warnings, limits);
    }

    /// <summary>Project structure: devices, PLCs with blocks, tags and types, HMI, library (command line --json).</summary>
    /// <param name="includeKeys">Also the record key of each object (--keys).</param>
    /// <param name="cancellationToken">Checked before each block and data type.</param>
    public JsonObject GetStructure(bool includeKeys = false, CancellationToken cancellationToken = default) =>
        (JsonObject)ToNode(Raw(includeKeys ? TiaContent.StructureWithKeys : TiaContent.Structure, cancellationToken))!;

    /// <summary>Objects whose content could not be read (statistics.errors of the structure); reads the structure if needed.</summary>
    public IReadOnlyList<ParseIssue> GetIssues(CancellationToken cancellationToken = default)
    {
        var d = (PyDict)Raw(TiaContent.Structure, cancellationToken)!;
        return (((PyDict)d["statistics"]!).Get("errors") as List<object?> ?? []).Cast<PyDict>()
            .Select(e => new ParseIssue(Py.Str(e["object"]), e.Get("key") as string, Py.Str(e["error"]))).ToList();
    }

    /// <summary>Integrity check without TIA Portal (--verify): header, record hashes, hash chain and RSA signatures of the
    /// commits, PEData.idx. Key "ok" is the overall result.</summary>
    public JsonObject Verify(CancellationToken cancellationToken = default) => (JsonObject)ToNode(Raw(TiaContent.Integrity, cancellationToken))!;

    /// <summary>True if the integrity check finds no problem (exit code 0 of --verify).</summary>
    public bool IsIntact(CancellationToken cancellationToken = default) => ((PyDict)Raw(TiaContent.Integrity, cancellationToken)!)["ok"] is true;

    /// <summary>Cross references from XRef\XRef.db with object names (--xref); null if the project has no XRef.db.</summary>
    public JsonObject? GetCrossReferences(CancellationToken cancellationToken = default) =>
        (JsonObject?)ToNode(Raw(TiaContent.CrossReferences, cancellationToken));

    /// <summary>Program information per PLC (--proginfo): resources, call and dependency structure, assignment list.</summary>
    public JsonObject GetProgramInfo(CancellationToken cancellationToken = default) => (JsonObject)ToNode(Raw(TiaContent.ProgramInfo, cancellationToken))!;

    /// <summary>JSON text exactly as the command line writes it with --json (indent 2; bytes as "hex:...").</summary>
    public string ToJson(TiaContent content = TiaContent.Structure, CancellationToken cancellationToken = default)
    {
        var v = Raw(content, cancellationToken);
        return content switch
        {
            TiaContent.Structure or TiaContent.StructureWithKeys => PyJson.Dumps(v, 2, ensureAscii: false, PyJson.Fallback.Hex),
            TiaContent.Integrity => PyJson.Dumps(v, 2, ensureAscii: true),
            _ => PyJson.Dumps(v, 2, ensureAscii: false),
        };
    }

    /// <summary>Text exactly as the command line writes it without --json: indented tree for the structure, report for the
    /// integrity check, JSON for cross references and program information.</summary>
    public string ToText(TiaContent content = TiaContent.Structure, CancellationToken cancellationToken = default) => content switch
    {
        TiaContent.Structure or TiaContent.StructureWithKeys => TextTree.Of((PyDict)Raw(content, cancellationToken)!),
        TiaContent.Integrity => Format.Verify.TextReport((PyDict)Raw(content, cancellationToken)!),
        _ => ToJson(content, cancellationToken),
    };

    /// <summary>LAD/FBD networks of every code block in the SimaticML form of the Openness export (&lt;FlgNet&gt; of
    /// SW/NetworkSource/FlgNet/v5): block name → one XML text per network, null for networks in other languages.
    /// Blocks of the same name in several PLCs: the first one.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string?>> GetLadFbdSimaticMl(CancellationToken cancellationToken = default) =>
        SimaticMlNetworks(cancellationToken).ToDictionary(kv => kv.Key,
            kv => (IReadOnlyList<string?>)kv.Value.Select(x => x?.ToString().ReplaceLineEndings("\n")).ToList());

    internal Dictionary<string, List<System.Xml.Linq.XElement?>> SimaticMlNetworks(CancellationToken ct)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var m = model!;
            using var scope = limits.Use();
            var code = new Code(m);
            var res = new Dictionary<string, List<System.Xml.Linq.XElement?>>();
            foreach (var b in m.P.OfType("CodeBlockData"))
            {
                ct.ThrowIfCancellationRequested();
                if (m.P.Name(b) is not { } name || res.ContainsKey(name))
                    continue;
                try
                {
                    var nets = code.SimaticMlNetworks(b);
                    if (nets.Any(x => x is not null))
                        res[name] = nets;
                }
                catch (Exception e) when (IsReaderError(e))
                {
                    throw new CorruptProjectException($"SimaticML of {name} cannot be made: {Message(e)}", Path, e);
                }
            }
            return res;
        }
    }

    /// <summary>Releases the loaded records. A call still running finishes first.</summary>
    public void Dispose()
    {
        disposed = true;
        if (gate.TryEnter())
        {
            try
            {
                Release();
            }
            finally
            {
                gate.Exit();
            }
        }
    }

    void Release()
    {
        model = null;
        results.Clear();
    }

    /// <summary>The value tree of the reader for one content, computed once; reader errors become CorruptProjectException.</summary>
    internal object? Raw(TiaContent content, CancellationToken ct)
    {
        if (!Enum.IsDefined(content))
            throw new ArgumentOutOfRangeException(nameof(content));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ct.ThrowIfCancellationRequested();
            try
            {
                if (results.TryGetValue(content, out var r))
                    return r;
                var m = model!;
                m.Cancellation = ct;
                using var scope = limits.Use();
                r = content switch
                {
                    TiaContent.Structure => m.ToDict(false),
                    TiaContent.StructureWithKeys => m.ToDict(true),
                    TiaContent.Integrity => Format.Verify.VerifyProject(corePath),
                    TiaContent.CrossReferences => Xref.Resolved(corePath, m.P),
                    _ => ProgInfo.Extract(m, corePath),
                };
                results[content] = r;
                return r;
            }
            catch (Exception e) when (IsReaderError(e))
            {
                throw new CorruptProjectException($"{content} cannot be read: {Message(e)}", Path, e);
            }
            finally
            {
                if (model is { } m)
                    m.Cancellation = default;
                if (disposed)
                    Release();
            }
        }
    }

    static JsonNode? ToNode(object? value) =>
        value is null ? null : JsonNode.Parse(PyJson.DumpsStrict(value), null, new JsonDocumentOptions { MaxDepth = 1024 });

    static bool IsReaderError(Exception e) => e is not (OpenPEDataException or OperationCanceledException or OutOfMemoryException
        or ObjectDisposedException or IOException or UnauthorizedAccessException);

    static string Message(Exception e) => ProjectModel.ErrorText(e);
}
