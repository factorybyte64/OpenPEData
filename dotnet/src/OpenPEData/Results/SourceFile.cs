using System.Text;

namespace OpenPEData;

/// <summary>One block or PLC data type as an external source, as TIA Portal "Generate source from blocks" writes it:
/// .scl (SCL block), .awl (STL block), .db (data block), .udt (PLC data type). LAD, FBD and GRAPH blocks have no text
/// source in TIA; they come as a SimaticML block document (.xml) for "Import block". PLC tag tables come as SimaticML
/// tag table (.xml, "PLC tags") so that blocks using global tags compile after the import.</summary>
public sealed class TiaSourceFile
{
    internal TiaSourceFile(string plc, string folder, string name, string extension, string kind, string? language, string content,
        IReadOnlyList<string> notes)
    {
        Plc = plc;
        Folder = folder;
        Name = name;
        Extension = extension;
        Kind = kind;
        Language = language;
        Content = content;
        Notes = notes;
    }

    /// <summary>Name of the PLC the block belongs to.</summary>
    public string Plc { get; }

    /// <summary>Folder below the PLC: "Program blocks", "PLC data types" or "PLC tags", followed by the user group path
    /// ("Program blocks/Motors/Conveyors"); separator "/".</summary>
    public string Folder { get; }

    /// <summary>Name of the block or data type.</summary>
    public string Name { get; }

    /// <summary>File extension with the dot: ".scl", ".awl", ".db", ".udt" or ".xml".</summary>
    public string Extension { get; }

    /// <summary>Block type (FB, FC, OB, GlobalDB, InstanceDB, ArrayDB, TypedDB, TechnologyDB), "UDT" or "TagTable".</summary>
    public string Kind { get; }

    /// <summary>Programming language of the block (SCL, STL, LAD, FBD, GRAPH, DB); null for a data type.</summary>
    public string? Language { get; }

    /// <summary>The source text (CRLF line ends). Written as UTF-8 with byte order mark, like TIA does.</summary>
    public string Content { get; }

    /// <summary>What the source does not contain, e.g. encrypted networks or array element start values of an FB interface.
    /// Empty if the source is complete.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>Path relative to the export folder: PLC / Folder / Name + Extension, with characters that are not allowed
    /// in file names replaced by "_" (platform separator).</summary>
    public string RelativePath =>
        System.IO.Path.Combine([Safe(Plc), .. Folder.Split('/').Select(Safe), Safe(Name) + Extension]);

    static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    static string Safe(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c < 32 || Invalid.Contains(c) ? '_' : c);
        var r = sb.ToString().TrimEnd('.', ' ');
        return r.Length == 0 || r is "." or ".." ? "_" : r;
    }

    /// <inheritdoc/>
    public override string ToString() => RelativePath;
}

/// <summary>A block for which TIA Portal generates no source either; it is not exported.</summary>
/// <param name="Plc">Name of the PLC.</param>
/// <param name="Folder">"Program blocks" or "PLC data types" and the group path, separator "/".</param>
/// <param name="Name">Name of the block.</param>
/// <param name="Reason">"know-how protected", "system block" or "system data type" (added by TIA itself).</param>
public sealed record SkippedSource(string Plc, string Folder, string Name, string Reason);

/// <summary>Result of <see cref="TiaProject.GetSources"/>: the sources and the blocks without a source.</summary>
public sealed class TiaSourceSet
{
    internal TiaSourceSet(IReadOnlyList<TiaSourceFile> files, IReadOnlyList<SkippedSource> skipped)
    {
        Files = files;
        Skipped = skipped;
    }

    /// <summary>All sources, in the order PLC, blocks (type, number, name), data types.</summary>
    public IReadOnlyList<TiaSourceFile> Files { get; }

    /// <summary>Know-how protected blocks and system blocks; TIA generates no source for them.</summary>
    public IReadOnlyList<SkippedSource> Skipped { get; }
}

/// <summary>Result of <see cref="TiaProject.ExportSources"/>: the written files.</summary>
public sealed class SourceExportResult
{
    internal SourceExportResult(string folder, IReadOnlyList<TiaSourceFile> sources, IReadOnlyList<string> paths, IReadOnlyList<SkippedSource> skipped)
    {
        Folder = folder;
        Sources = sources;
        Paths = paths;
        Skipped = skipped;
    }

    /// <summary>Know-how protected blocks and system blocks; TIA generates no source for them.</summary>
    public IReadOnlyList<SkippedSource> Skipped { get; }

    /// <summary>The export folder (full path).</summary>
    public string Folder { get; }

    /// <summary>All sources, in the order PLC, blocks (type, number, name), data types.</summary>
    public IReadOnlyList<TiaSourceFile> Sources { get; }

    /// <summary>Full path of each written file, in the order of <see cref="Sources"/>.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>Sources with notes (something could not be exported completely).</summary>
    public IEnumerable<TiaSourceFile> Incomplete => Sources.Where(s => s.Notes.Count > 0);
}
