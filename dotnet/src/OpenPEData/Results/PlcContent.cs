using System.Text.Json.Nodes;
using static OpenPEData.JsonRead;

namespace OpenPEData;

/// <summary>The software of a PLC: blocks, tag tables and PLC data types.</summary>
public sealed class PlcSoftware : TiaContentObject
{
    internal PlcSoftware(JsonObject j, TiaDevice device) : base(j)
    {
        Device = device;
        Name = StrOrEmpty(j, "name");
        OrderNumber = Str(j, "orderNumber");
        Firmware = Str(j, "firmware");
        TypeName = Str(j, "typeName");
        Blocks = List(j, "blocks", b => new PlcBlock(b));
        TagTables = List(j, "tagTables", t => new PlcTagTable(t));
        DataTypes = List(j, "types", t => new PlcDataType(t));
    }

    /// <summary>The device the PLC belongs to.</summary>
    public TiaDevice Device { get; }

    /// <summary>PLC name.</summary>
    public string Name { get; }

    /// <summary>Article number of the CPU, e.g. "6ES7 513-1AL02-0AB0".</summary>
    public string? OrderNumber { get; }

    /// <summary>Firmware version, e.g. "V2.9".</summary>
    public string? Firmware { get; }

    /// <summary>CPU type, e.g. "CPU 1513-1 PN".</summary>
    public string? TypeName { get; }

    /// <summary>Program blocks (OB, FB, FC, DB), including instance DBs.</summary>
    public IReadOnlyList<PlcBlock> Blocks { get; }

    /// <summary>PLC tag tables.</summary>
    public IReadOnlyList<PlcTagTable> TagTables { get; }

    /// <summary>PLC data types (UDTs).</summary>
    public IReadOnlyList<PlcDataType> DataTypes { get; }

    /// <summary>All PLC tags of all tag tables.</summary>
    public IEnumerable<PlcTag> Tags => TagTables.SelectMany(t => t.Tags);

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A program block: OB, FB, FC, DB, or a system block the project holds.</summary>
public sealed class PlcBlock : TiaContentObject
{
    internal PlcBlock(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Type = StrOrEmpty(j, "type");
        Number = Long(j, "number");
        Language = Str(j, "language");
        Group = StrOrEmpty(j, "group");
        InstanceOf = Str(j, "instanceOf");
        IsKnowHowProtected = Bool(j, "knowHowProtected") ?? false;
        NetworkCount = Objects(j, "networks").Count();
    }

    /// <summary>Block name.</summary>
    public string Name { get; }

    /// <summary>Block type as stored: "OB", "FB", "FC", "GlobalDB", "InstanceDB", or for system blocks the project holds
    /// "SFC", "SFB", "SDT".</summary>
    public string Type { get; }

    /// <summary>Block number.</summary>
    public long? Number { get; }

    /// <summary>Programming language as TIA Portal shows it, e.g. "SCL", "LAD", "FBD", "STL", "GRAPH", "DB".</summary>
    public string? Language { get; }

    /// <summary>Path of the block group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>For an instance DB: the FB (or system FB) it is an instance of.</summary>
    public string? InstanceOf { get; }

    /// <summary>The block has know-how protection; its code is not readable.</summary>
    public bool IsKnowHowProtected { get; }

    /// <summary>Number of networks of a code block (0 for a DB).</summary>
    public int NetworkCount { get; }

    /// <summary>The block interface (sections with members) as JSON; null for a global DB (see "members").</summary>
    public JsonArray? Interface => Json["interface"] as JsonArray;

    /// <summary>The networks (code per network, title, comment) as JSON; null for a DB.</summary>
    public JsonArray? Networks => Json["networks"] as JsonArray;

    /// <inheritdoc/>
    public override string ToString() => Number is { } n ? $"{Name} [{Type}{n}]" : Name;
}

/// <summary>A PLC tag table.</summary>
public sealed class PlcTagTable : TiaContentObject
{
    internal PlcTagTable(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Group = StrOrEmpty(j, "group");
        Tags = List(j, "tags", t => new PlcTag(t));
    }

    /// <summary>Tag table name.</summary>
    public string Name { get; }

    /// <summary>Path of the group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>The tags of the table.</summary>
    public IReadOnlyList<PlcTag> Tags { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A PLC tag.</summary>
public sealed class PlcTag : TiaContentObject
{
    internal PlcTag(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        DataType = StrOrEmpty(j, "dataType");
        Address = Str(j, "address");
        Comment = Text(j, "comment");
        IsHmiAccessible = Bool(j, "hmiAccessible");
        IsHmiVisible = Bool(j, "hmiVisible");
        IsHmiWritable = Bool(j, "hmiWritable");
    }

    /// <summary>Tag name.</summary>
    public string Name { get; }

    /// <summary>Data type, e.g. "Bool", "Word", "\"UDT_Motor\"".</summary>
    public string DataType { get; }

    /// <summary>Address, e.g. "%MW100".</summary>
    public string? Address { get; }

    /// <summary>Tag comment.</summary>
    public MultilingualText Comment { get; }

    /// <summary>Accessible from HMI/OPC UA/Web API.</summary>
    public bool? IsHmiAccessible { get; }

    /// <summary>Visible in HMI engineering.</summary>
    public bool? IsHmiVisible { get; }

    /// <summary>Writable from HMI/OPC UA/Web API.</summary>
    public bool? IsHmiWritable { get; }

    /// <inheritdoc/>
    public override string ToString() => Address is null ? Name : $"{Name} {Address}";
}

/// <summary>A PLC data type (UDT).</summary>
public sealed class PlcDataType : TiaContentObject
{
    internal PlcDataType(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Group = StrOrEmpty(j, "group");
        Members = List(j, "members", m => new PlcMember(m));
    }

    /// <summary>Type name.</summary>
    public string Name { get; }

    /// <summary>Path of the group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>The members (first level; nested members are in each member's JSON).</summary>
    public IReadOnlyList<PlcMember> Members { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A member of a PLC data type or data block.</summary>
public sealed class PlcMember : TiaContentObject
{
    internal PlcMember(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        DataType = StrOrEmpty(j, "dataType");
        StartValue = Scalar(j, "startValue");
        Comment = Text(j, "comment");
    }

    /// <summary>Member name.</summary>
    public string Name { get; }

    /// <summary>Data type.</summary>
    public string DataType { get; }

    /// <summary>Start value as text, null if none is set.</summary>
    public string? StartValue { get; }

    /// <summary>Member comment.</summary>
    public MultilingualText Comment { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Name} : {DataType}";
}
