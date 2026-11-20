using OpenPEData.Runtime;

namespace OpenPEData;

/// <summary>Options of <see cref="TiaProject.Open"/>.</summary>
public sealed record OpenOptions
{
    /// <summary>Read a project of a TIA version that is not in <see cref="TiaVersion.Supported"/>, or whose version cannot be
    /// told, with the reader of the newest supported version. The deviation is listed in <see cref="TiaProject.Warnings"/>;
    /// results may be incomplete or wrong. Default false: such a project throws UnsupportedVersionException or
    /// UnsupportedFormatException.</summary>
    public bool AllowUnknownVersion { get; init; }

    /// <summary>Largest archive entry read into memory (default 1 GiB); a larger one makes the project unreadable
    /// (CorruptProjectException).</summary>
    public long MaxEntryBytes { get; init; } = ReadLimits.Default.MaxEntryBytes;

    /// <summary>Largest decompressed blob (default 256 MiB); a larger one makes that object unreadable (an issue), the
    /// metamodel the whole project (CorruptProjectException).</summary>
    public long MaxDecompressedBytes { get; init; } = ReadLimits.Default.MaxDecompressedBytes;

    internal ReadLimits Limits => new(MaxEntryBytes, MaxDecompressedBytes);
}

/// <summary>What <see cref="TiaProject.ToJson"/> and <see cref="TiaProject.ToText"/> write; the same as the command line options.</summary>
public enum TiaContent
{
    /// <summary>Project structure (no option, --json).</summary>
    Structure,

    /// <summary>Project structure with record keys typeLo:typeHi:oid (--keys).</summary>
    StructureWithKeys,

    /// <summary>Integrity check (--verify).</summary>
    Integrity,

    /// <summary>Cross references from XRef\XRef.db (--xref), always JSON; "null" without XRef.db.</summary>
    CrossReferences,

    /// <summary>Program information per PLC (--proginfo), always JSON.</summary>
    ProgramInfo,
}

/// <summary>An object whose content could not be read; the rest of the project is still read.</summary>
/// <param name="Object">What it is, e.g. "block Main".</param>
/// <param name="Key">Record key of the object (typeLo:typeHi:oid), if known.</param>
/// <param name="Message">The error, e.g. "error: unpack requires a buffer of 4 bytes".</param>
public sealed record ParseIssue(string Object, string? Key, string Message);
