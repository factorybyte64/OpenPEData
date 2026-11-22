using System.Text.Json.Nodes;
using static OpenPEData.JsonRead;

namespace OpenPEData;

/// <summary>Typed result of the integrity check (<see cref="TiaProject.GetIntegrityReport"/>, command line --verify).</summary>
public sealed class IntegrityReport : TiaContentObject
{
    internal IntegrityReport(JsonObject j) : base(j)
    {
        IsIntact = Bool(j, "ok") ?? false;
        Files = List(j, "files", f => new PlfFileCheck(f));
        Index = ObjOrNull(j, "idx") is { } ix ? new IndexCheck(ix) : null;
    }

    /// <summary>True if every PEData file passed (exit code 0 of --verify).</summary>
    public bool IsIntact { get; }

    /// <summary>One check per PEData file (System\PEData.plf and further parts).</summary>
    public IReadOnlyList<PlfFileCheck> Files { get; }

    /// <summary>Check of System\PEData.idx against the plf; null if the project has no index.</summary>
    public IndexCheck? Index { get; }

    /// <summary>All problems of all files and of the index, prefixed with the file name.</summary>
    public IEnumerable<string> Problems =>
        Files.SelectMany(f => f.Problems.Select(p => $"{f.File}: {p}"))
            .Concat(Index?.Problems.Select(p => "PEData.idx: " + p) ?? []);
}

/// <summary>Integrity check of one PEData file.</summary>
public sealed class PlfFileCheck : TiaContentObject
{
    internal PlfFileCheck(JsonObject j) : base(j)
    {
        File = StrOrEmpty(j, "file");
        Size = Long(j, "size") ?? 0;
        IsIntact = Bool(j, "ok") ?? false;
        Problems = Strings(j, "problems");
        HeaderHashOk = Bool(j, "headerHashOk");
        Records = Long(j, "records") ?? 0;
        RecordHashErrorCount = (j["recordHashErrors"] as JsonArray)?.Count ?? 0;
        TrailingBytes = Long(j, "trailingBytes") ?? 0;
        Commits = List(j, "commits", c => new CommitCheck(c));
        UncommittedRecords = Long(j, "uncommittedRecords") ?? 0;
    }

    /// <summary>Path of the file inside the project, e.g. "System/PEData.plf".</summary>
    public string File { get; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; }

    /// <summary>True if the file has no problem.</summary>
    public bool IsIntact { get; }

    /// <summary>The problems found (at most 20).</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Hash of the file header matches; null if not checked.</summary>
    public bool? HeaderHashOk { get; }

    /// <summary>Number of records.</summary>
    public long Records { get; }

    /// <summary>Number of records with a wrong hash that were listed (at most 20).</summary>
    public int RecordHashErrorCount { get; }

    /// <summary>Bytes after the last record.</summary>
    public long TrailingBytes { get; }

    /// <summary>The commit records with hash chain and signature result.</summary>
    public IReadOnlyList<CommitCheck> Commits { get; }

    /// <summary>Records after the last commit.</summary>
    public long UncommittedRecords { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{File}: {(IsIntact ? "ok" : "damaged")}";
}

/// <summary>A commit record of a PEData file: hash chain and RSA signature.</summary>
public sealed class CommitCheck : TiaContentObject
{
    internal CommitCheck(JsonObject j) : base(j)
    {
        Offset = Long(j, "offset") ?? 0;
        Ticks = Long(j, "ticks");
        RecordsSince = Long(j, "recordsSince") ?? 0;
        ChainOk = Bool(j, "chainOk");
        SignatureOk = Bool(j, "signatureOk");
    }

    /// <summary>File offset of the commit record.</summary>
    public long Offset { get; }

    /// <summary>Time stamp of the commit in .NET ticks (UTC), if stored.</summary>
    public long? Ticks { get; }

    /// <summary>Time stamp of the commit, if stored.</summary>
    public DateTime? Time => Ticks is { } t && t >= DateTime.MinValue.Ticks && t <= DateTime.MaxValue.Ticks
        ? new DateTime(t, DateTimeKind.Utc) : null;

    /// <summary>Records since the previous commit.</summary>
    public long RecordsSince { get; }

    /// <summary>Hash chain to the previous commit is intact.</summary>
    public bool? ChainOk { get; }

    /// <summary>RSA signature is valid.</summary>
    public bool? SignatureOk { get; }
}

/// <summary>Check of System\PEData.idx against the records of the plf.</summary>
public sealed class IndexCheck : TiaContentObject
{
    internal IndexCheck(JsonObject j) : base(j)
    {
        IsIntact = Bool(j, "ok") ?? false;
        Problems = Strings(j, "problems");
        Entries = Long(j, "entries") ?? 0;
        Records = Long(j, "records") ?? 0;
        IsStale = Bool(j, "stale") ?? false;
    }

    /// <summary>True if the index matches the plf.</summary>
    public bool IsIntact { get; }

    /// <summary>The problems found (at most 20).</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Number of index entries.</summary>
    public long Entries { get; }

    /// <summary>Number of plf records compared.</summary>
    public long Records { get; }

    /// <summary>The index describes an older state of the plf (TIA rebuilds it on opening).</summary>
    public bool IsStale { get; }
}
