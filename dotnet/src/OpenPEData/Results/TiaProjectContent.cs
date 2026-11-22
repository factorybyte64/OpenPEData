using System.Text.Json.Nodes;
using static OpenPEData.JsonRead;

namespace OpenPEData;

/// <summary>Base of every typed result object: the JSON node it was read from, with all details the reader emits
/// (the same node as in <see cref="TiaProject.GetStructure"/>). The typed properties cover the common content; everything
/// else stays reachable through <see cref="Json"/>.</summary>
public abstract class TiaContentObject
{
    private protected TiaContentObject(JsonObject json) => Json = json;

    /// <summary>The JSON node of this object (do not modify; it belongs to this result).</summary>
    public JsonObject Json { get; }
}

/// <summary>Typed content of a project or global library (<see cref="TiaProject.GetContent"/>).</summary>
public sealed class TiaProjectContent : TiaContentObject
{
    internal TiaProjectContent(JsonObject root) : base(root)
    {
        var p = Obj(root, "project");
        Name = StrOrEmpty(p, "name");
        Comment = Text(p, "comment");
        DeviceGroups = List(p, "deviceGroups", g => new TiaDeviceGroup(g));
        Devices = List(p, "devices", d => new TiaDevice(d));
        Languages = new TiaProjectLanguages(Obj(p, "languages"));
        UsedProducts = List(p, "usedProducts", u => new TiaUsedProduct(u));
        Libraries = List(Obj(root, "library"), "libraries", l => new TiaLibrary(l));
        Statistics = new TiaReadStatistics(Obj(root, "statistics"));
    }

    /// <summary>Project or library name.</summary>
    public string Name { get; }

    /// <summary>Project comment.</summary>
    public MultilingualText Comment { get; }

    /// <summary>Device groups of the project tree (empty for a global library).</summary>
    public IReadOnlyList<TiaDeviceGroup> DeviceGroups { get; }

    /// <summary>Devices (stations) with their PLC and HMI software.</summary>
    public IReadOnlyList<TiaDevice> Devices { get; }

    /// <summary>Project languages.</summary>
    public TiaProjectLanguages Languages { get; }

    /// <summary>Products (option packages, support packages) the project uses.</summary>
    public IReadOnlyList<TiaUsedProduct> UsedProducts { get; }

    /// <summary>The project library, or the global library itself.</summary>
    public IReadOnlyList<TiaLibrary> Libraries { get; }

    /// <summary>Statistics of the read records.</summary>
    public TiaReadStatistics Statistics { get; }

    /// <summary>All PLC software of all devices.</summary>
    public IEnumerable<PlcSoftware> Plcs => Devices.SelectMany(d => d.Plcs);

    /// <summary>All HMI software of all devices.</summary>
    public IEnumerable<HmiSoftware> Hmis => Devices.SelectMany(d => d.Hmis);
}

/// <summary>A device group of the project tree.</summary>
public sealed class TiaDeviceGroup : TiaContentObject
{
    internal TiaDeviceGroup(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Path = StrOrEmpty(j, "path");
    }

    /// <summary>Group name.</summary>
    public string Name { get; }

    /// <summary>Path of the group in the project tree, separated by "/".</summary>
    public string Path { get; }

    /// <inheritdoc/>
    public override string ToString() => Path;
}

/// <summary>Project languages.</summary>
public sealed class TiaProjectLanguages : TiaContentObject
{
    internal TiaProjectLanguages(JsonObject j) : base(j)
    {
        Active = Strings(j, "active");
        Editing = Str(j, "editing");
        Reference = Str(j, "reference");
    }

    /// <summary>Active project languages (culture names such as "en-US").</summary>
    public IReadOnlyList<string> Active { get; }

    /// <summary>Editing language.</summary>
    public string? Editing { get; }

    /// <summary>Reference language.</summary>
    public string? Reference { get; }
}

/// <summary>A product the project uses (option package, support package, HSP).</summary>
public sealed class TiaUsedProduct : TiaContentObject
{
    internal TiaUsedProduct(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Version = Str(j, "version");
        Id = Str(j, "id");
        Type = Str(j, "type");
    }

    /// <summary>Product name.</summary>
    public string Name { get; }

    /// <summary>Product version, e.g. "V12.0 SP1".</summary>
    public string? Version { get; }

    /// <summary>Product identifier.</summary>
    public string? Id { get; }

    /// <summary>Product type, e.g. "SupportPackage".</summary>
    public string? Type { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} {Version}".Trim();
}

/// <summary>Statistics of the read records.</summary>
public sealed class TiaReadStatistics : TiaContentObject
{
    internal TiaReadStatistics(JsonObject j) : base(j)
    {
        PlfFiles = Strings(j, "plfFiles");
        Records = Long(j, "records") ?? 0;
        Objects = Long(j, "objects") ?? 0;
        DeletedObjects = Long(j, "deletedObjects") ?? 0;
        HashErrors = Long(j, "hashErrors") ?? 0;
    }

    /// <summary>The PEData files that were read.</summary>
    public IReadOnlyList<string> PlfFiles { get; }

    /// <summary>Number of records.</summary>
    public long Records { get; }

    /// <summary>Number of live objects.</summary>
    public long Objects { get; }

    /// <summary>Number of deleted objects.</summary>
    public long DeletedObjects { get; }

    /// <summary>Number of records whose hash does not match.</summary>
    public long HashErrors { get; }
}

/// <summary>A device (station) with its PLC and HMI software.</summary>
public sealed class TiaDevice : TiaContentObject
{
    internal TiaDevice(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        TypeName = Str(j, "typeName");
        Group = StrOrEmpty(j, "group");
        Comment = Text(j, "comment");
        Plcs = List(j, "plcs", p => new PlcSoftware(p, this));
        Hmis = List(j, "hmi", h => new HmiSoftware(h, this));
    }

    /// <summary>Device name.</summary>
    public string Name { get; }

    /// <summary>Device type, e.g. "S7-1500 station".</summary>
    public string? TypeName { get; }

    /// <summary>Path of the device group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>Device comment.</summary>
    public MultilingualText Comment { get; }

    /// <summary>Hardware configuration (modules, interfaces, parameters) as JSON; see the "hardware" node of the structure.</summary>
    public JsonObject? Hardware => ObjOrNull(Json, "hardware");

    /// <summary>PLC software on this device.</summary>
    public IReadOnlyList<PlcSoftware> Plcs { get; }

    /// <summary>HMI software on this device.</summary>
    public IReadOnlyList<HmiSoftware> Hmis { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A library: the project library or a global library.</summary>
public sealed class TiaLibrary : TiaContentObject
{
    internal TiaLibrary(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        LibraryType = Str(j, "libraryType");
        Author = Str(j, "author");
        LastModifiedBy = Str(j, "lastModifiedBy");
        CreationTime = Str(j, "creationTime");
        ModifiedTime = Str(j, "modifiedTime");
        Comment = Text(j, "comment");
        Types = List(j, "types", t => new TiaLibraryType(t));
        MasterCopyCount = Objects(j, "masterCopies").Count();
    }

    /// <summary>Library name.</summary>
    public string Name { get; }

    /// <summary>"ProjectLib" or the kind of global library.</summary>
    public string? LibraryType { get; }

    /// <summary>Author.</summary>
    public string? Author { get; }

    /// <summary>Last modified by.</summary>
    public string? LastModifiedBy { get; }

    /// <summary>Creation time (ISO 8601, UTC).</summary>
    public string? CreationTime { get; }

    /// <summary>Modification time (ISO 8601, UTC).</summary>
    public string? ModifiedTime { get; }

    /// <summary>Library comment.</summary>
    public MultilingualText Comment { get; }

    /// <summary>Types, including the system types the library holds.</summary>
    public IReadOnlyList<TiaLibraryType> Types { get; }

    /// <summary>Number of master copies; the master copies themselves are in <see cref="TiaContentObject.Json"/>.</summary>
    public int MasterCopyCount { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A library type.</summary>
public sealed class TiaLibraryType : TiaContentObject
{
    internal TiaLibraryType(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Type = Str(j, "type");
        Category = Str(j, "category");
        DefaultVersion = Scalar(j, "defaultVersion");
        LatestVersion = Scalar(j, "latestVersion");
        InstanceCount = Long(j, "instanceCount") ?? 0;
        OutdatedInstances = Long(j, "outdatedInstances") ?? 0;
    }

    /// <summary>Type name.</summary>
    public string Name { get; }

    /// <summary>Kind of the type as stored, e.g. "IECPLInstructionData".</summary>
    public string? Type { get; }

    /// <summary>"system" for types that TIA Portal brings along, else the user category.</summary>
    public string? Category { get; }

    /// <summary>Default version, if set.</summary>
    public string? DefaultVersion { get; }

    /// <summary>Latest version, if any.</summary>
    public string? LatestVersion { get; }

    /// <summary>Number of instances in the project.</summary>
    public long InstanceCount { get; }

    /// <summary>Number of instances that use an outdated version.</summary>
    public long OutdatedInstances { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
