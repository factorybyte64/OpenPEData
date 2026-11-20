using System.Collections.Immutable;

namespace OpenPEData.Format;

/// <summary>What differs between TIA Portal versions, as data: the metamodel record and the embedded data files. The
/// extensions .apNN/.zapNN/.alNN/.zalNN carry the version number and are recognized for any version (TiaProjectInfo). One
/// profile per supported version, added only together with test projects of that version (a "V22 = V21" profile without
/// evidence is worse than UnsupportedVersionException). The decoding tables (Attrs.ExpandoCodes, Hmi.PropertyNames,
/// Code.Operators ...) are V21 findings; they move into the profile as soon as a version is shown to differ.</summary>
internal sealed record VersionProfile(
    TiaVersion Version,
    string MetamodelKey,
    uint MetamodelTypeId,
    string EnumsResource)
{
    public static readonly VersionProfile V21 = new(new TiaVersion(21), "0000:0007:1", 0x00070000, "OpenPEData.Data.V21.unified_enums.json");

    /// <summary>All profiles, oldest first; TiaVersion.Supported is derived from it.</summary>
    public static readonly ImmutableArray<VersionProfile> All = [V21];

    public static VersionProfile? For(TiaVersion v) => All.FirstOrDefault(p => p.Version == v);

    /// <summary>Profile of the newest supported version (used for an unknown version with AllowUnknownVersion).</summary>
    public static VersionProfile Newest => All[^1];
}
