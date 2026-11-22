using System.Xml.Linq;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

internal sealed class MetaAttribute(long ordinal, string? name, string? type, string? dflt)
{
    public readonly long Ordinal = ordinal;
    public readonly string? Name = name, Type = type, Default = dflt;
}

internal sealed class MetaType
{
    public readonly string Kind;
    public readonly string? Name;
    public readonly string FullName;
    public readonly uint Id;
    public readonly List<string?> Bases = [];
    public readonly List<string?> Implements = [];
    public readonly List<MetaAttribute> Attributes = [];
    /// <summary>AttributeSet: name -> XML attributes of the Attribute element</summary>
    public readonly Dictionary<string, PyDict> AttrFlags = [];
    /// <summary>ObjectType: AttributeSet full name -> {attr name: XML attributes} (Implements)</summary>
    public readonly Dictionary<string, Dictionary<string, PyDict>> Overrides = [];
    public readonly List<MetaRelation> Relations = [];
    /// <summary>Enumeration value -> name</summary>
    public readonly Dictionary<string, string?> Constants = [];
    public readonly bool Abstract;
    public readonly string? BaseType, Expando, Persistent;
    public string? Package;
    public readonly List<(string? Name, string? Type)> Elements = [];
    public readonly string? ItemType;

    public MetaType(string kind, string? name, string full, uint id, XElement el)
    {
        Kind = kind;
        Name = name;
        FullName = full;
        Id = id;
        Abstract = Et.Get(el, "abstract") == "true";
        BaseType = Et.Get(el, "base");
        Expando = Et.Get(el, "expando");
        Persistent = Et.Get(el, "persistent");
        ItemType = kind == "Array" ? Et.Get(el, "type") : null;
    }
}

internal sealed class MetaRelation(string? name, uint id, string? owner, string? target, string? cardinality, string? behaviour, uint? inverseOf = null)
{
    public readonly string? Name = name;
    public readonly uint Id = id;
    public readonly string? Owner = owner, Target = target, Cardinality = cardinality, Behaviour = behaviour;
    public readonly uint? InverseOf = inverseOf;
    public string? InverseRef;
    public uint? InverseId;
}

/// <summary>Metamodel (MetaInfo XML) stored in PEData.plf record 0000:0007:1: u32 compressed length at +44, zlib stream
/// at +48 (UTF-8 XML, namespace http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta). Ids are u32 =
/// (package &lt;&lt; 16) | local id.</summary>
internal sealed class Metamodel
{
    public const string Ns = "http://www.siemens.com/Automation/2004/04/ObjectFrame/Meta";
    static readonly string[] TypeKinds = ["ObjectType", "Structure", "Enumeration", "Array", "AttributeSet", "BehaviourType"];

    public readonly byte[] Xml;
    public readonly OrderedDictionary<uint, MetaType> Types = [];
    public readonly Dictionary<string, MetaType> ByName = [];
    public readonly OrderedDictionary<uint, MetaRelation> Relations = [];
    public readonly Dictionary<uint, string?> Packages = [];
    public readonly Dictionary<string, int> DocOrder = [];
    readonly Dictionary<string, List<string>> ancestors = [];
    readonly Dictionary<string, List<string>> topo = [];
    readonly Dictionary<(uint, string), List<(long Ordinal, string? Name, string? Type, PyDict Flags)>> stored = [];
    readonly Dictionary<uint, List<string>> setsOrdered = [];
    Dictionary<string, MetaType>? shortNames;

    static uint? Id(string? s) => string.IsNullOrEmpty(s) ? null : (uint)Py.ParseHex(s);

    public Metamodel(byte[] xmlBytes)
    {
        Xml = xmlBytes;
        var root = Et.FromBytes(xmlBytes);
        XName pkgName = XName.Get("Package", Ns), nsName = XName.Get("Namespace", Ns);
        foreach (var pkg in root.DescendantsAndSelf(pkgName))
        {
            var pid = Id(Et.Get(pkg, "id"));
            if (pid is not null)
                Packages[pid.Value] = Et.Get(pkg, "name");
            foreach (var ns in pkg.DescendantsAndSelf(nsName))
            {
                var nsname = Et.Get(ns, "name");
                foreach (var el in ns.Elements())
                {
                    var kind = el.Name.NamespaceName == Ns ? el.Name.LocalName : Et.Tag(el);
                    if (!TypeKinds.Contains(kind) || string.IsNullOrEmpty(Et.Get(el, "id")))
                        continue;
                    var t = new MetaType(kind, Et.Get(el, "name"), nsname + "." + Et.Get(el, "name"), Id(Et.Get(el, "id"))!.Value, el)
                    {
                        Package = Et.Get(pkg, "name"),
                    };
                    Types[t.Id] = t;
                    ByName[t.FullName] = t;
                    foreach (var c in el.Elements())
                    {
                        var ck = c.Name.NamespaceName == Ns ? c.Name.LocalName : "\0";
                        if (ck == "Element" && kind == "Structure")
                            t.Elements.Add((Et.Get(c, "name"), Et.Get(c, "type")));
                        if (ck == "Base")
                            t.Bases.Add(Et.Get(c, "ref"));
                        else if (ck == "Implements")
                        {
                            t.Implements.Add(Et.Get(c, "ref"));
                            var ov = new Dictionary<string, PyDict>();
                            foreach (var a in c.Elements(XName.Get("Attribute", Ns)))
                                ov[Et.Get(a, "name") ?? "\0none"] = Et.Attrib(a);
                            t.Overrides[Et.Get(c, "ref") ?? "\0none"] = ov;
                        }
                        else if ((ck == "Attribute" || ck == "Element") && Et.Get(c, "id") is not null)
                        {
                            var dv = c.Element(XName.Get("DefaultValue", Ns));
                            t.Attributes.Add(new MetaAttribute(Py.ParseInt0(Et.Get(c, "id")!), Et.Get(c, "name"), Et.Get(c, "type"),
                                dv is not null ? Et.Text(dv) : null));
                            t.AttrFlags[Et.Get(c, "name") ?? "\0none"] = Et.Attrib(c);
                        }
                        else if (ck == "Constant")
                            t.Constants[Et.Get(c, "value") ?? "\0none"] = Et.Get(c, "name");
                        else if (ck == "Relation")
                        {
                            var tgt = c.Element(XName.Get("Target", Ns));
                            var r = new MetaRelation(Et.Get(c, "name"), Id(Et.Get(c, "id")) ?? 0, t.FullName,
                                tgt is not null ? Et.Get(tgt, "ref") : null, Et.Get(c, "cardinality"), Et.Get(c, "behaviourType"));
                            t.Relations.Add(r);
                            Relations[r.Id] = r;
                            var inv = c.Element(XName.Get("Inverse", Ns));
                            if (inv is not null && !string.IsNullOrEmpty(Et.Get(inv, "ref")))
                                r.InverseRef = Et.Get(inv, "ref");
                            if (inv is not null && !string.IsNullOrEmpty(Et.Get(inv, "id")))
                            {
                                var ir = new MetaRelation(Et.Get(inv, "name"), Id(Et.Get(inv, "id"))!.Value, r.Target, t.FullName,
                                    Et.Get(inv, "cardinality"), Et.Get(inv, "behaviourType"), r.Id);
                                Relations[ir.Id] = ir;
                                r.InverseId = ir.Id;
                                ir.InverseId = r.Id;
                            }
                        }
                    }
                }
            }
        }
        // resolve Inverse ref="Name": relation of that name declared on the target type or one of its bases
        foreach (var r in Relations.Values.ToList())
        {
            if (!string.IsNullOrEmpty(r.InverseRef) && r.InverseId is null && !string.IsNullOrEmpty(r.Target))
            {
                foreach (var a in Ancestors(r.Target))
                {
                    var at = ByName.GetValueOrDefault(a);
                    var hit = at?.Relations.FirstOrDefault(x => x.Name == r.InverseRef);
                    if (hit is not null)
                    {
                        r.InverseId = hit.Id;
                        break;
                    }
                }
            }
        }
        var i = 0;
        foreach (var t in Types.Values)
            DocOrder[t.FullName] = i++;
    }

    public static Metamodel FromRecord(byte[] recData)
    {
        var clen = Bin.U32(recData, 44);
        return new Metamodel(Zlib.Decompress(Py.Slice(recData, 48, 48 + clen)));
    }

    public string TypeName(uint tid) => Types.TryGetValue(tid, out var t) ? t.FullName : $"?0x{tid:x8}";

    public string? ShortName(uint tid) => Types.TryGetValue(tid, out var t) ? t.Name : $"?0x{tid:x8}";

    public string? TypePackage(uint tid) => Types.TryGetValue(tid, out var t) ? t.Package : null;

    /// <summary>Type and all base types (breadth first, each once).</summary>
    public List<string> Ancestors(string fullName)
    {
        if (ancestors.TryGetValue(fullName, out var c))
            return c;
        var outl = new List<string>();
        var queue = new List<string?> { fullName };
        var seen = new HashSet<string>();
        var qi = 0;
        while (qi < queue.Count)
        {
            var n = queue[qi++];
            if (n is null || !seen.Add(n))
                continue;
            outl.Add(n);
            if (ByName.TryGetValue(n, out var t))
                queue.AddRange(t.Bases);
        }
        ancestors[fullName] = outl;
        return outl;
    }

    /// <summary>Type and all base types, every type after all of its own bases (base ... derived).</summary>
    public List<string> AncestorsTopological(string fullName)
    {
        if (topo.TryGetValue(fullName, out var c))
            return c;
        var outl = new List<string>();
        var seen = new HashSet<string>();
        var stack = new Stack<(string?, bool)>();
        stack.Push((fullName, false));
        while (stack.Count > 0)
        {
            var (n, done) = stack.Pop();
            if (done)
            {
                outl.Add(n!);
                continue;
            }
            if (n is null || !seen.Add(n))
                continue;
            stack.Push((n, true));
            var t = ByName.GetValueOrDefault(n);
            var bases = t?.Bases ?? [];
            for (var k = bases.Count - 1; k >= 0; k--)
                if (bases[k] is null || !seen.Contains(bases[k]!))
                    stack.Push((bases[k], false));
        }
        topo[fullName] = outl;
        return outl;
    }

    /// <summary>Metamodel type for an attribute type reference (full or namespace-relative name).</summary>
    public MetaType? Resolve(string? typeRef, string? contextNs = null)
    {
        if (typeRef is null)
            return null;
        var t = ByName.GetValueOrDefault(typeRef);
        if (t is null && !string.IsNullOrEmpty(contextNs))
            t = ByName.GetValueOrDefault(contextNs + "." + typeRef);
        if (t is null)
        {
            if (shortNames is null)
            {
                shortNames = [];
                foreach (var x in Types.Values)
                    if (x.Name is not null)
                        shortNames.TryAdd(x.Name, x);
            }
            t = shortNames.GetValueOrDefault(typeRef);
        }
        return t;
    }

    /// <summary>Attributes of an AttributeSet that are stored in a record of type tid, in ordinal order. An attribute is
    /// stored unless it is intrinsic, mapped to a relation, or constant (flags merged from the set definition and the
    /// Implements overrides of all ancestors, more derived types overriding per flag).</summary>
    public List<(long Ordinal, string? Name, string? Type, PyDict Flags)> StoredAttributes(uint tid, string setFull)
    {
        var key = (tid, setFull);
        if (stored.TryGetValue(key, out var c))
            return c;
        var st = ByName.GetValueOrDefault(setFull);
        var t = Types.GetValueOrDefault(tid);
        var res = new List<(long, string?, string?, PyDict)>();
        if (st is not null && t is not null)
        {
            var anc = AncestorsTopological(t.FullName);
            var attrs = Py.Sorted(st.Attributes, a => Py.T(a.Ordinal, a.Name, a.Type, a.Default));
            foreach (var a in attrs)
            {
                var fl = new PyDict(st.AttrFlags.GetValueOrDefault(a.Name ?? "\0none") ?? new PyDict());
                foreach (var an in anc)
                {
                    var at = ByName.GetValueOrDefault(an);
                    if (at is not null && at.Overrides.TryGetValue(setFull, out var ovs) && ovs.TryGetValue(a.Name ?? "\0none", out var ov))
                        fl.Update(ov);
                }
                if ((string?)fl.Get("intrinsic") == "true" || Py.Bool(fl.Get("relation")) || (string?)fl.Get("constant") == "true")
                    continue;
                res.Add((a.Ordinal, a.Name, a.Type, fl));
            }
        }
        stored[key] = res;
        return res;
    }

    /// <summary>Implemented AttributeSets in record slot order: code point order of the qualified set name (H-020).</summary>
    public List<string> AttributeSetsOrdered(uint tid)
    {
        if (!setsOrdered.TryGetValue(tid, out var c))
            setsOrdered[tid] = c = Py.Sorted(AttributeSets(tid));
        return c;
    }

    public bool IsA(uint tid, string fullName) => Types.TryGetValue(tid, out var t) && Ancestors(t.FullName).Contains(fullName);

    /// <summary>AttributeSets implemented by a type including inherited ones (full names).</summary>
    public List<string> AttributeSets(uint tid)
    {
        if (!Types.TryGetValue(tid, out var t))
            return [];
        var res = new List<string>();
        foreach (var a in Ancestors(t.FullName))
            if (ByName.TryGetValue(a, out var at))
                foreach (var s in at.Implements)
                    if (s is not null && !res.Contains(s))
                        res.Add(s);
        return res;
    }
}
