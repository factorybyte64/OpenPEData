using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>Link of an object record. Relation is null for a typed link list entry (the relation is implicit by slot).</summary>
internal sealed class Link(uint? relation, uint typeId, ulong oid, int pos, int? slot = null)
{
    public readonly uint? Relation = relation;
    public readonly uint TypeId = typeId;
    public readonly ulong Oid = oid;
    public readonly int Pos = pos;
    public int? Slot = slot;

    string? key;

    public string Key => key ??= Plf.MakeKey(TypeId, Oid);
}

/// <summary>Decoded slot structure of one domain record (object records in PEData.plf).
/// Layout (little endian, offsets from record start): +0 u32 length, +4 key, +16 u32 ?, +20 u32 ? (2, 3 or 11),
/// +24 u16 0, +26 u8 slot count, +27 u8 ?, +28 16-byte object GUID, +44 u32[slot count] slot table (absolute offset of
/// the slot's block, 0 = empty), blocks, 0xFF. Slots: the S AttributeSets of the type in slot order, then link slots
/// (tagged link block: u16 size, u16 count, link16 entries; typed link list: u32 size, u32 ?, u32 count, link12).
/// Deleted object: a 49-byte tombstone with u32 @16 = 0xFFFFFFFF.</summary>
internal sealed class ObjectRecord
{
    public const int LinkSize = 16;

    public readonly PlfRecord Rec;
    public readonly byte[] Data;
    public readonly uint Flags16, Flags20;
    public readonly int SlotCount, B27;
    public readonly byte[] Guid;
    public readonly uint[] Slots;
    public readonly int TableEnd;
    public readonly Metamodel? Mm;
    public readonly List<string> SetNames;
    public readonly int NSets;
    public readonly bool LayoutOk;
    public readonly List<string> Errors = [];
    /// <summary>slot index -> (start, end)</summary>
    public readonly Dictionary<int, (int Start, int End)> Blocks = [];
    /// <summary>slot indexes of Blocks in the order they were added (offset order)</summary>
    public readonly List<int> BlockOrder = [];
    public readonly List<Link> Links = [];

    public ObjectRecord(PlfRecord rec, Metamodel? mm)
    {
        Rec = rec;
        var d = Data = rec.Data;
        Flags16 = Bin.U32(d, 16);
        Flags20 = Bin.U32(d, 20);
        SlotCount = Py.At(d, 26);
        B27 = Py.At(d, 27);
        Guid = Py.Slice(d, 28, 44);
        Slots = Bin.U32s(d, 44, SlotCount);
        TableEnd = 44 + 4 * SlotCount;
        Mm = mm;
        SetNames = mm is not null ? mm.AttributeSetsOrdered(rec.TypeId) : [];
        NSets = SetNames.Count;
        LayoutOk = Flags20 == 3 ? NSets + 2 == SlotCount : NSets + 1 < SlotCount;
        ReadBlocks();
    }

    public static bool IsTombstone(PlfRecord rec) => Bin.U32(rec.Data, 16) == 0xFFFFFFFF;

    static (ushort Size, List<Link> Links) ReadLinkBlock(byte[] d, int o)
    {
        var size = Bin.U16(d, o);
        var count = Bin.U16(d, o + 2);
        var links = new List<Link>();
        var p = o + 4;
        while (links.Count < count && p + LinkSize <= Math.Min(o + size, d.Length))
        {
            if (Bin.U32(d, p) != 0)
                links.Add(new Link(Bin.U32(d, p), ((uint)Bin.U16(d, p + 6) << 16) | Bin.U16(d, p + 4), Bin.U64(d, p + 8), p));
            p += LinkSize;
        }
        return (size, links);
    }

    static (uint Size, List<Link>? Links) ReadTypedList(byte[] d, int o, int slot)
    {
        var size = Bin.U32(d, o);
        Bin.U32(d, o + 4);
        var count = Bin.U32(d, o + 8);
        if (12 + (long)count * 12 > Math.Min(size, (long)d.Length - o))
            return (size, null);
        var links = new List<Link>();
        for (var i = 0; i < count; i++)
        {
            var p = o + 12 + i * 12;
            links.Add(new Link(null, ((uint)Bin.U16(d, p + 2) << 16) | Bin.U16(d, p), Bin.U64(d, p + 4), p, slot));
        }
        return (size, links);
    }

    void ReadBlocks()
    {
        var d = Data;
        var endAll = d.Length - 1;
        var used = Slots.Select((o, i) => (o: (long)o, i)).Where(x => x.o != 0).OrderBy(x => x.o).ThenBy(x => x.i).ToList();
        for (var k = 0; k < used.Count; k++)
        {
            var (o, i) = used[k];
            var nxt = k + 1 < used.Count ? used[k + 1].o : endAll;
            if (o < TableEnd || o > endAll)
            {
                Errors.Add($"slot {i} offset {o} outside record");
                continue;
            }
            Blocks[i] = ((int)o, (int)nxt);
            BlockOrder.Add(i);
            var oi = (int)o;
            if (LayoutOk && i >= NSets)
            {
                var size16 = Bin.U16(d, oi);
                var cnt16 = Bin.U16(d, oi + 2);
                long size;
                List<Link> links;
                long hl;
                if (i == NSets || (cnt16 > 0 && o + size16 == nxt && 4 + cnt16 * LinkSize <= size16))
                {
                    var (s, l) = ReadLinkBlock(d, oi);
                    size = s;
                    links = l;
                    hl = 4 + links.Count * LinkSize;
                    if (links.Count < cnt16)
                        Errors.Add($"link slot {i}: {links.Count} of {cnt16} links found");
                }
                else
                {
                    var (s, l) = ReadTypedList(d, oi, i);
                    if (l is null)
                    {
                        Errors.Add($"link slot {i}: neither tagged block nor typed list");
                        continue;
                    }
                    size = s;
                    links = l;
                    hl = 12 + links.Count * 12;
                }
                if (o + size != nxt)
                    Errors.Add($"link slot {i} size {size} != span {nxt - o}");
                if (hl > size)
                    Errors.Add($"link slot {i} count {links.Count} exceeds size {size}");
                foreach (var l in links)
                    l.Slot = i;
                Links.AddRange(links);
            }
            else
            {
                var ln = Bin.U32(d, oi);
                if (IsExpando(i))
                {
                    var alloc = Bin.U32(d, oi + 4);
                    if (o + alloc != nxt || ln > alloc)
                        Errors.Add($"expando slot {i} used {ln} alloc {alloc} span {nxt - o}");
                }
                else if (o + ln > nxt || ln < 4)
                    Errors.Add($"slot {i} ({SetName(i)}) length {ln} != span {nxt - o}");
            }
        }
        if (used.Count > 0 && used[0].o != TableEnd)
            Errors.Add($"first block at {used[0].o}, table ends at {TableEnd}");
    }

    public string SetName(int i)
    {
        if (!LayoutOk)
            return "?slot" + i;
        if (i >= NSets)
            return $"<links {i - NSets}>";
        return Py.AfterLast(SetNames[i], ".");
    }

    public bool IsExpando(int i)
    {
        if (!LayoutOk || i >= NSets)
            return false;
        var t = Mm?.ByName.GetValueOrDefault(SetNames[i]);
        return t is not null && !string.IsNullOrEmpty(t.Expando);
    }

    /// <summary>Bytes of a set block after its length head, or null if the slot is empty.</summary>
    public byte[]? Payload(int i)
    {
        if (!Blocks.TryGetValue(i, out var be))
            return null;
        var o = be.Start;
        return IsExpando(i) ? Py.Slice(Data, o + 8, o + Bin.U32(Data, o)) : Py.Slice(Data, o + 4, o + Bin.U32(Data, o));
    }

    public byte[]? PayloadBySet(string shortName)
    {
        for (var i = 0; i < (LayoutOk ? NSets : 0); i++)
            if (SetName(i) == shortName)
                return Payload(i);
        return null;
    }
}
