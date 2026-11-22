using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenPEData;

/// <summary>Version of TIA Portal that wrote a project or library, e.g. V21 = (21, 0). Detected from the file name
/// extension of the project file or archive (.ap21, .zap21, .al21, .zal21), which TIA Portal numbers by version.</summary>
public readonly record struct TiaVersion(int Major, int Minor = 0) : IComparable<TiaVersion>
{
    /// <summary>TIA Portal V21.</summary>
    public static TiaVersion V21 { get; } = new(21);

    /// <summary>Versions this library reads; each one is backed by test projects of that version.</summary>
    public static ImmutableArray<TiaVersion> Supported { get; } = [.. Format.VersionProfile.All.Select(p => p.Version)];

    /// <summary>True if this library has been tested with the version.</summary>
    public bool IsSupported => Supported.Contains(this);

    public int CompareTo(TiaVersion other) => Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    /// <summary>"V21", or "V21.1" for an update with its own format.</summary>
    public override string ToString() => Minor == 0 ? $"V{Major}" : $"V{Major}.{Minor}";
}

/// <summary>What a path contains.</summary>
public enum TiaProjectKind
{
    /// <summary>A project (folder with .apNN file, or .zapNN archive).</summary>
    Project,

    /// <summary>A global library (folder with .alNN file, or .zalNN archive).</summary>
    GlobalLibrary,
}

/// <summary>Result of <see cref="TiaProject.Probe"/>: what the path is, without loading it.</summary>
/// <param name="Path">The path as given.</param>
/// <param name="Version">TIA version from the extension; null if the folder has System\PEData.plf but no .apNN/.alNN file.</param>
/// <param name="Kind">Project or global library.</param>
/// <param name="IsArchive">True for a .zapNN/.zalNN archive, false for a folder or a .apNN/.alNN file in one.</param>
public sealed record TiaProjectInfo(string Path, TiaVersion? Version, TiaProjectKind Kind, bool IsArchive)
{
    static readonly Regex Extension = new(@"\.(z?)a([pl])([0-9]{2})\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    const string Plf = "System/PEData.plf";

    /// <summary>Recognizes the path from file names only (extension, System\PEData.plf); reads no records.</summary>
    internal static TiaProjectInfo? Of(string path)
    {
        if (Directory.Exists(path))
            return OfFolder(path, path);
        if (!File.Exists(path))
            return null;
        var m = Extension.Match(path);
        if (m.Success && m.Groups[1].Length == 0)
            return File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!, "System", "PEData.plf"))
                ? new TiaProjectInfo(path, VersionOf(m), KindOf(m), false) : null;
        try
        {
            using var z = Format.ProjectFiles.OpenZip(path);
            if (!z.Entries.Any(e => string.Equals(e.FullName.Replace('\\', '/'), Plf, StringComparison.OrdinalIgnoreCase)))
                return null;
            if (m.Success)
                return new TiaProjectInfo(path, VersionOf(m), KindOf(m), true);
            // archive with another name: the project or library file at its root tells the version
            var inner = z.Entries.Where(e => e.FullName.IndexOfAny(['/', '\\']) < 0).Select(e => Extension.Match(e.FullName))
                .FirstOrDefault(x => x.Success && x.Groups[1].Length == 0);
            return new TiaProjectInfo(path, inner is null ? null : VersionOf(inner), inner is null ? TiaProjectKind.Project : KindOf(inner), true);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static TiaProjectInfo? OfFolder(string path, string folder)
    {
        if (!File.Exists(System.IO.Path.Combine(folder, "System", "PEData.plf")))
            return null;
        var m = Directory.EnumerateFiles(folder).Select(f => Extension.Match(System.IO.Path.GetFileName(f)))
            .Where(x => x.Success && x.Groups[1].Length == 0).OrderBy(x => x.Value, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        return m is null ? new TiaProjectInfo(path, null, TiaProjectKind.Project, false) : new TiaProjectInfo(path, VersionOf(m), KindOf(m), false);
    }

    static TiaVersion VersionOf(Match m) => new(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));

    static TiaProjectKind KindOf(Match m) => char.ToLowerInvariant(m.Groups[2].Value[0]) == 'l' ? TiaProjectKind.GlobalLibrary : TiaProjectKind.Project;
}
