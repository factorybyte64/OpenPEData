using System.Text.Json.Nodes;
using static OpenPEData.JsonRead;

namespace OpenPEData;

/// <summary>The software of a WinCC Unified device: screens, tags, connections, alarms.</summary>
public sealed class HmiSoftware : TiaContentObject
{
    internal HmiSoftware(JsonObject j, TiaDevice device) : base(j)
    {
        Device = device;
        Name = StrOrEmpty(j, "name");
        TypeName = Str(j, "typeName");
        OrderNumber = Str(j, "orderNumber");
        Firmware = Str(j, "firmware");
        Connections = List(j, "connections", c => new HmiConnection(c));
        TagTables = List(j, "tagTables", t => new HmiTagTable(t));
        Screens = List(j, "screens", s => new HmiScreen(s));
        SystemTags = Strings(j, "systemTags");
    }

    /// <summary>The device the HMI software belongs to.</summary>
    public TiaDevice Device { get; }

    /// <summary>Name of the HMI software.</summary>
    public string Name { get; }

    /// <summary>Device type, e.g. "MTP700 Unified Comfort".</summary>
    public string? TypeName { get; }

    /// <summary>Article number.</summary>
    public string? OrderNumber { get; }

    /// <summary>Firmware version.</summary>
    public string? Firmware { get; }

    /// <summary>HMI connections.</summary>
    public IReadOnlyList<HmiConnection> Connections { get; }

    /// <summary>HMI tag tables.</summary>
    public IReadOnlyList<HmiTagTable> TagTables { get; }

    /// <summary>Screens with their items.</summary>
    public IReadOnlyList<HmiScreen> Screens { get; }

    /// <summary>Names of the system tags.</summary>
    public IReadOnlyList<string> SystemTags { get; }

    /// <summary>All HMI tags of all tag tables.</summary>
    public IEnumerable<HmiTag> Tags => TagTables.SelectMany(t => t.Tags);

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>An HMI connection.</summary>
public sealed class HmiConnection : TiaContentObject
{
    internal HmiConnection(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Kind = Str(j, "kind");
    }

    /// <summary>Connection name.</summary>
    public string Name { get; }

    /// <summary>"Integrated" for a connection to a PLC of the project, else the kind as stored.</summary>
    public string? Kind { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>An HMI tag table.</summary>
public sealed class HmiTagTable : TiaContentObject
{
    internal HmiTagTable(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Group = StrOrEmpty(j, "group");
        Tags = List(j, "tags", t => new HmiTag(t));
    }

    /// <summary>Tag table name.</summary>
    public string Name { get; }

    /// <summary>Path of the group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>The tags of the table.</summary>
    public IReadOnlyList<HmiTag> Tags { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>An HMI tag, internal or connected to a PLC tag.</summary>
public sealed class HmiTag : TiaContentObject
{
    internal HmiTag(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        DataType = StrOrEmpty(j, "dataType");
        Connection = Str(j, "connection");
        PlcName = Str(j, "plcName");
        PlcTag = Str(j, "plcTag");
        Address = Str(j, "address");
        AcquisitionCycle = Str(j, "acquisitionCycle");
        Comment = Text(j, "comment");
    }

    /// <summary>Tag name.</summary>
    public string Name { get; }

    /// <summary>HMI data type.</summary>
    public string DataType { get; }

    /// <summary>Connection for an external tag; null for an internal tag.</summary>
    public string? Connection { get; }

    /// <summary>Name of the connected PLC.</summary>
    public string? PlcName { get; }

    /// <summary>Connected PLC tag.</summary>
    public string? PlcTag { get; }

    /// <summary>Address of the connected PLC tag.</summary>
    public string? Address { get; }

    /// <summary>Acquisition cycle, e.g. "T1s".</summary>
    public string? AcquisitionCycle { get; }

    /// <summary>Tag comment.</summary>
    public MultilingualText Comment { get; }

    /// <summary>True for a tag without connection.</summary>
    public bool IsInternal => Connection is null;

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>An HMI screen with its layers and items.</summary>
public sealed class HmiScreen : TiaContentObject
{
    internal HmiScreen(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Group = StrOrEmpty(j, "group");
        Width = Long(j, "width");
        Height = Long(j, "height");
        BackColor = Scalar(j, "backColor");
        Layers = List(j, "layers", l => new HmiLayer(l));
        Items = List(j, "items", i => new HmiScreenItem(i));
    }

    /// <summary>Screen name.</summary>
    public string Name { get; }

    /// <summary>Path of the screen group, "" at the top level.</summary>
    public string Group { get; }

    /// <summary>Width in pixels.</summary>
    public long? Width { get; }

    /// <summary>Height in pixels.</summary>
    public long? Height { get; }

    /// <summary>Background color as stored, null if the style decides.</summary>
    public string? BackColor { get; }

    /// <summary>The layers of the screen.</summary>
    public IReadOnlyList<HmiLayer> Layers { get; }

    /// <summary>All items of the screen, also those inside groups (see <see cref="HmiScreenItem.Parent"/>), ordered by name.</summary>
    public IReadOnlyList<HmiScreenItem> Items { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>A layer of an HMI screen.</summary>
public sealed class HmiLayer : TiaContentObject
{
    internal HmiLayer(JsonObject j) : base(j)
    {
        Index = Long(j, "index") ?? 0;
        Name = StrOrEmpty(j, "name");
        IsVisibleInEngineering = Bool(j, "esVisible");
    }

    /// <summary>Layer index (0 to 31).</summary>
    public long Index { get; }

    /// <summary>Layer name.</summary>
    public string Name { get; }

    /// <summary>Visible in the engineering system; null if not stored (default).</summary>
    public bool? IsVisibleInEngineering { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>An item on an HMI screen (shape, I/O field, button, control, group …).</summary>
public sealed class HmiScreenItem : TiaContentObject
{
    internal HmiScreenItem(JsonObject j) : base(j)
    {
        Name = StrOrEmpty(j, "name");
        Type = StrOrEmpty(j, "type");
        var props = ObjOrNull(j, "properties");
        Left = Long(j, "left") ?? 0;
        Top = Long(props, "Top") ?? 0;
        Width = Long(j, "width");
        Height = Long(j, "height");
        Text = JsonRead.Text(j, "text");
        ZIndex = Long(j, "zIndex") ?? 0;
        LayerIndex = Long(j, "layerIndex");
        Parent = Str(j, "parent");
    }

    /// <summary>Item name.</summary>
    public string Name { get; }

    /// <summary>Item type, e.g. "HmiButton", "HmiIOField", "HmiRectangle".</summary>
    public string Type { get; }

    /// <summary>Left edge in pixels.</summary>
    public long Left { get; }

    /// <summary>Top edge in pixels.</summary>
    public long Top { get; }

    /// <summary>Width in pixels.</summary>
    public long? Width { get; }

    /// <summary>Height in pixels.</summary>
    public long? Height { get; }

    /// <summary>Text of the item, if it has one.</summary>
    public MultilingualText Text { get; }

    /// <summary>Order within the layer (higher is in front).</summary>
    public long ZIndex { get; }

    /// <summary>Index of the layer, null if not known.</summary>
    public long? LayerIndex { get; }

    /// <summary>Name of the containing group, null at the top level.</summary>
    public string? Parent { get; }

    /// <summary>All stored properties as JSON (Openness names).</summary>
    public JsonObject? Properties => ObjOrNull(Json, "properties");

    /// <summary>Dynamizations (tag, script, resource list) as JSON.</summary>
    public JsonArray? Dynamizations => Json["dynamizations"] as JsonArray;

    /// <summary>Events with their scripts as JSON.</summary>
    public JsonArray? Events => Json["events"] as JsonArray;

    /// <inheritdoc/>
    public override string ToString() => $"{Name} ({Type})";
}
