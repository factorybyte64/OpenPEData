using System.Collections.Immutable;
using System.Xml.Linq;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Block interfaces: members of data blocks and PLC data types (UDTs) with data type, start value and comment
/// (port of interface.py). block/UDT --CurrentInterface--> InterfaceVersionRootData (IInterfacePartData.Payload = XML
/// &lt;Root&gt;), --InterfaceComments--> InterfaceCommentsPartData; root --UsedParts--> parts indexed by SubPartIndex.</summary>
internal sealed class Interface
{
    /// <summary>presentation order of Openness / the TIA editor</summary>
    public static readonly ImmutableArray<string> SectionOrder = ["Input", "Output", "InOut", "Static", "Temp", "Constant", "Return"];

    readonly ProjectModel m;
    readonly Project p;
    readonly Dictionary<long, string?> types;
    public readonly string? Root;
    public readonly List<string?> Parts;
    public readonly PyDict Values = new();
    public bool KeepPath;
    /// <summary>Standard (not optimized) block access: members get their absolute offset "byte.bit".</summary>
    public bool Standard;
    readonly PyDict comments = new();
    Dictionary<string, PyDict>? elemIndex;
    int elemN = -1;

    /// <summary>Parsed XML element of a pe:XmlT/BlobT value, or null. A blob whose store records cannot be read raises
    /// BlobError.</summary>
    public static XElement? XmlOf(object? blobv, Func<string, byte[]?>? store = null)
    {
        if (blobv is not byte[] blob || blob.Length == 0)
            return null;
        byte[]? b;
        try
        {
            b = Attrs.DecodeBlob(blob, store);
        }
        catch (BlobError)
        {
            throw;
        }
        catch (Exception e) when (PyCatch.Any(e))
        {
            return null;
        }
        if (b is null || b.Length == 0)
            return null;
        try
        {
            return Et.FromString(Py.Utf8SigExact(b));
        }
        catch (Exception e) when (e is XmlParseError or PyUnicodeDecodeError)
        {
            return null;
        }
    }

    public static PyDict ValuesMap(XElement? el)
    {
        var d = new PyDict();
        if (el is null)
            return d;
        foreach (var v in Et.Iter(el))
            if (Et.Local(v) == "V")
                d[Et.Get(v, "p")] = Et.Get(v, "v");
        return d;
    }

    /// <summary>The language-neutral entry (65535) repeats the text of a project language in system templates; the export
    /// shows only the project languages.</summary>
    public static PyDict DropNeutral(PyDict t)
    {
        if (t.ContainsKey("65535") && t.Any(kv => !Py.Eq(kv.Key, "65535") && Py.Eq(kv.Value, t["65535"])))
        {
            var r = new PyDict();
            foreach (var (c, x) in t)
                if (!Py.Eq(c, "65535"))
                    r[c] = x;
            return r;
        }
        return t;
    }

    public Interface(ProjectModel model, string owner, object? scope, string? root = null)
    {
        m = model;
        p = model.P;
        types = model.ElementaryTypes(scope);
        // system and library blocks (PID_Compact, High_Speed_Counter, F-blocks, ...) link their interface root by
        // CurrentLibraryInterface instead of CurrentInterface (sample 036)
        Root = root ?? model.Rel1(owner, "CurrentInterface") ?? model.Rel1(owner, "CurrentLibraryInterface");
        Parts = Root is not null ? p.Links(Root, "UsedParts").Select(l => p.Objects.ContainsKey(l.Key) ? l.Key : null).ToList() : [];
        if (Root is not null && Py.Eq(p.Attr(Root, "IInterfacePartData", "Kind"), "Values"))
            Values.Update(ValuesMap(XmlOf(p.Attr(Root, "IInterfacePartData", "Payload"))));
        foreach (var k in Parts)
            if (k is not null && Py.Eq(p.Attr(k, "IInterfacePartData", "Kind"), "Values"))
                Values.Update(ValuesMap(XmlOf(p.Attr(k, "IInterfacePartData", "Payload"))));
        if (Root is not null)
        {
            var ex = p.Sets(Root).Set("ICoreExpandoAttributes");
            if (ex.Get("Values") is byte[] vb)
                Values.Update(ValuesMap(XmlOf(Attrs.ReadVar(vb, 0).Value, p.StoreData)));
        }
        var cm = model.Rel1(owner, "InterfaceComments");
        var raw = cm is not null ? p.Sets(cm).Set("ICommentsExpandoAttributeSet") : new PyDict();
        foreach (var (path, v) in raw)
            comments[path] = DropNeutral(model.CoreTextOf(v));
    }

    XElement? Payload(string key) => XmlOf(p.Attr(key, "IInterfacePartData", "Payload"));

    /// <summary>Top-level members (data block: the Static section; UDT: its elements).</summary>
    public List<object?> Members()
    {
        if (Root is null)
            return [];
        var root = Payload(Root);
        if (root is null)
            return [];
        return MembersOf(root, "", true);
    }

    string? TypeOf(XElement el)
    {
        if (!string.IsNullOrEmpty(Et.Get(el, "Type")))
            return Et.Get(el, "Type");
        var rid = Py.ParseHex(Et.Get(el, "RID", "0"));
        if (rid >> 24 == 0x02)
        {
            if ((rid & 0xFFFF) == 0)
                return "Void";
            return types.TryGetValue(rid & 0xFFFF, out var n) ? n : $"RID 0x{rid:X8}";
        }
        if (rid >> 24 is 0x91 or 0x92 or 0x93 or 0x94)
            return "Struct"; // anonymous Struct: 0x92 in a DB, 0x91 in a UDT, 0x93 in an FB (sample 059), 0x94 in an FC (sample 073)
        return $"RID 0x{rid:X8}";
    }

    List<string?> PartsOf(string rootKey) => p.Links(rootKey, "UsedParts").Select(l => p.Objects.ContainsKey(l.Key) ? l.Key : null).ToList();

    static string? PartAt(List<string?> parts, string? spi)
    {
        if (spi is null)
            return null;
        var i = Py.ParseInt(spi);
        if (i >= parts.Count)
            return null;
        if (i < 0)
            i += parts.Count;
        if (i < 0)
            throw new PyIndexError("list index out of range");
        return parts[(int)i];
    }

    /// <summary>Section member of a code block root (fixed IDs 2..8); user members start at ID 51, so a UDT element named
    /// e.g. "Temp" is not a section.</summary>
    static bool IsSection(XElement el) =>
        Et.Local(el) == "Member" && SectionOrder.Contains(Et.Get(el, "Name")!) && Py.ParseInt(Et.Get(el, "ID", "0")!) is >= 2 and <= 8;

    /// <summary>Members shown below a member whose type is the interface root `part`: elements of a UDT, or for an FB
    /// (multi-instance) the members of its Input, Output, InOut and Static sections.</summary>
    List<object?> SubMembers(string part, string path, long baseOff, bool retain)
    {
        var sub = Payload(part);
        if (sub is null)
            return [];
        var subparts = PartsOf(part);
        var sections = Et.Kids(sub).Where(IsSection).ToList();
        if (sections.Count == 0)
            return MembersOf(sub, path + ":", false, subparts, baseOff, retain);
        var outl = new List<object?>();
        foreach (var name in new[] { "Input", "Output", "InOut", "Static" })
        {
            foreach (var el in sections)
            {
                if (Et.Get(el, "Name") != name)
                    continue;
                var sp = PartAt(subparts, Et.Get(el, "SubPartIndex"));
                var wrap = sp is not null ? Payload(sp) : el;
                var ms = wrap is not null ? MembersOf(wrap, path + ":", false, subparts, baseOff, retain) : [];
                if (name != "Static")
                    ClearSetpoint(ms);
                outl.AddRange(ms);
            }
        }
        return outl;
    }

    /// <summary>Start values of the array elements of a member: {"1": v} for the value path "path[1]".</summary>
    PyDict ElementValues(string path)
    {
        if (elemN != Values.Count)
            elemIndex = IndexElements(Values, out elemN);
        return elemIndex!.TryGetValue(path, out var r) ? new PyDict(r) : new PyDict();
    }

    /// <summary>Splits a value path with array indices into the member path and the indices of all array levels, like the
    /// Subelement Path of Openness: "56[2]:51" -> ("56:51", "2"), "55:57[1]:51[2]" -> ("55:57:51", "1,2") (sample 059).
    /// Null without an index.</summary>
    public static (string Path, string Index)? SplitIndexed(string key)
    {
        if (key.IndexOf('[') <= 0)
            return null;
        var path = new System.Text.StringBuilder();
        var idx = new List<string>();
        for (var i = 0; i < key.Length; i++)
        {
            if (key[i] != '[')
            {
                path.Append(key[i]);
                continue;
            }
            var j = key.IndexOf(']', i);
            if (j < 0)
                return null;
            idx.Add(key[(i + 1)..j]);
            i = j;
        }
        return (path.ToString(), string.Join(",", idx));
    }

    /// <summary>{member path: {indices: value}} of all indexed value paths.</summary>
    public static Dictionary<string, PyDict> IndexElements(PyDict values, out int count)
    {
        var index = new Dictionary<string, PyDict>();
        count = values.Count;
        foreach (var (pk, v) in values)
            if (pk is string ps && SplitIndexed(ps) is var (path, ix))
            {
                if (!index.TryGetValue(path, out var d))
                    index[path] = d = new PyDict();
                d[ix] = v;
            }
        return index;
    }

    /// <summary>MFlags of a member (default 15 without the attribute): bit 0 accessible from HMI/OPC UA, bit 1 visible,
    /// bit 6 read-only from outside, bit 9 setpoint; bits 0..2 clear = hidden system member (sample 060). Accessible gates
    /// visible and writable like in Openness. A Variant (reference) is never accessible (sample 066).</summary>
    static void AddAttributes(PyDict mem, XElement el, bool retain)
    {
        var f = Et.Get(el, "MFlags") is { } mf ? Py.ParseInt(mf) : 15;
        var acc = (f & 1) != 0 && !Py.Eq(mem["dataType"], "Variant");
        mem["retain"] = retain;
        mem["externalAccessible"] = acc;
        mem["externalVisible"] = acc && (f & 2) != 0;
        mem["externalWritable"] = acc && (f & 64) == 0;
        mem["setpoint"] = (f & 512) != 0;
    }

    /// <summary>Openness reports setpoints only in the Static section of an FB: members below Input, Output and InOut
    /// (also of a multi-instance) are no setpoints (sample 060).</summary>
    public static void ClearSetpoint(List<object?> members)
    {
        foreach (var m in members.Cast<PyDict>())
        {
            m["setpoint"] = false;
            if (m.Get("members") is List<object?> kids)
                ClearSetpoint(kids);
        }
    }

    /// <summary>Members below a member that is not accessible are not accessible either (e.g. the private timers
    /// rcv_timer, wd_timer of MB_CLIENT; sample 066).</summary>
    static void Inaccessible(List<object?> members)
    {
        foreach (var m in members.Cast<PyDict>())
        {
            m["externalAccessible"] = m["externalVisible"] = m["externalWritable"] = false;
            if (m.Get("members") is List<object?> kids)
                Inaccessible(kids);
        }
    }

    List<object?> MembersOf(XElement parent, string prefix, bool own, List<string?>? parts = null, long baseOff = 0, bool retain = false)
    {
        parts ??= Parts;
        var outl = new List<object?>();
        foreach (var el in Et.Kids(parent))
        {
            if (Et.Local(el) != "Member")
                continue;
            // hidden system member (MFlags bits 0..2 clear): ST, RU of IEC_TIMER, internal statics of PID_Compact (also in
            // its instance DB, sample 066); user settings never clear all three bits
            if (Et.Get(el, "MFlags") is { } mf && (Py.ParseInt(mf) & 7) == 0)
                continue;
            if (Et.Get(el, "AtMemberID") is not null)
                continue; // AT view over another member (e.g. FC1_4_Req of MB_CLIENT): not shown by Openness (sample 066)
            var path = prefix + (Et.Get(el, "ID") ?? throw new PyTypeError("can only concatenate str (not \"NoneType\") to str"));
            var mem = PyDict.Of("name", Et.Get(el, "Name"), "dataType", TypeOf(el), "startValue", Values.Get(path),
                "comment", own ? comments.Get(path, new PyDict()) : new PyDict());
            var elems = ElementValues(path);
            if (elems.Count > 0)
                mem["elementStartValues"] = elems;
            // Remanence="Retain" on the member (optimized) holds for all its elements; StdO = standard offset in bits,
            // relative to the enclosing struct/UDT
            var ret = retain || Et.Get(el, "Remanence") == "Retain";
            var off = baseOff + Py.ParseInt(Et.Get(el, "StdO", "0")!);
            AddAttributes(mem, el, ret);
            if (Et.Get(el, "Accessibility") == "Private")
                mem["accessibility"] = "Private"; // internal member of a system/library FB (InternalDiagnostic of PID_Compact)
            if (Standard)
                mem["offset"] = $"{off / 8}.{off % 8}";
            if (KeepPath)
                mem["_path"] = path;
            var kids = MembersOf(el, path + ":", own, parts, off, ret);
            var part = kids.Count == 0 ? PartAt(parts, Et.Get(el, "SubPartIndex")) : null;
            if (part is not null)
            {
                if (p.IsA(part, "InterfaceVersionRootData"))
                    kids = SubMembers(part, path, off, ret);
                else
                {
                    var sub = Payload(part);
                    if (sub is not null)
                        kids = MembersOf(sub, path + ":", own, parts, off, ret);
                }
            }
            if (kids.Count > 0)
            {
                // Openness reports a member of a named structured type (UDT, FB, system type, array of them) as
                // setpoint; anonymous Structs and DTL are not (226 values in samples 060, 066-068). Below Input, Output
                // and InOut ClearSetpoint resets it.
                var dt = Py.Str(mem["dataType"]);
                if (dt != "Struct" && !dt.EndsWith(" of Struct", StringComparison.Ordinal) && dt != "DTL" && !dt.EndsWith(" of DTL", StringComparison.Ordinal))
                    mem["setpoint"] = true;
                if (!Py.Bool(mem["externalAccessible"]))
                    Inaccessible(kids);
                mem["members"] = kids;
            }
            outl.Add(mem);
        }
        return outl;
    }

    /// <summary>Code block interface: [{"section", "members"}] in section order; empty sections are omitted.</summary>
    public List<object?> Sections() => Root is null ? [] : SectionsOf(Root, Parts, "");

    /// <summary>Sections of the interface root `rootKey`. A technology object type derives from a base type
    /// (TO_SynchronousAxis : TO_PositioningAxis : TO_SpeedAxis : TO_Axis : TO_Object, sample 072): the root member
    /// "Base" (ID 49, outside the sections) points to the base type's root. Its members come first in each section,
    /// with the value path prefix "49:" per level, as in the SimaticML export (nested Section "Base").</summary>
    List<object?> SectionsOf(string rootKey, List<string?> parts, string prefix)
    {
        var root = Payload(rootKey);
        if (root is null)
            return [];
        var outl = new List<PyDict>();
        List<object?> bases = [];
        foreach (var el in Et.Kids(root))
        {
            if (Et.Local(el) != "Member")
                continue;
            var sp = PartAt(parts, Et.Get(el, "SubPartIndex"));
            var name = Et.Get(el, "Name");
            if (name == "Base" && !SectionOrder.Contains(name) && sp is not null && p.IsA(sp, "InterfaceVersionRootData"))
            {
                bases = SectionsOf(sp, PartsOf(sp), prefix + Et.Get(el, "ID") + ":");
                continue;
            }
            List<object?> members;
            if (sp is not null)
            {
                var sub = Payload(sp);
                members = sub is not null ? MembersOf(sub, prefix, true, parts) : [];
            }
            else
                members = MembersOf(el, prefix, true, parts);
            if (members.Count > 0)
                outl.Add(PyDict.Of("section", name, "members", members));
        }
        foreach (var b in bases.Cast<PyDict>())
        {
            var own = outl.FirstOrDefault(s => (string?)s["section"] == (string?)b["section"]);
            if (own is null)
                outl.Add(PyDict.Of("section", b["section"], "members", new List<object?>((List<object?>)b["members"]!)));
            else
                own["members"] = ((List<object?>)b["members"]!).Concat((List<object?>)own["members"]!).ToList();
        }
        return Py.Sorted(outl, s => (long)(SectionOrder.IndexOf((s["section"] as string)!) is var i and >= 0 ? i : SectionOrder.Length))
            .Cast<object?>().ToList();
    }
}
