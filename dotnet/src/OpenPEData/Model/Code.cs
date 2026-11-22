// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Block code: the networks of FB/FC/OB (port of code.py). block --Sources--> CompileUnitData (one per network;
/// ICompileUnitData.Data = blob with &lt;SCLSource&gt; token tree or &lt;FlgNet&gt; LAD/FBD network);
/// block --CoreObject2IdentContainer--> IdentContainerData --IdentParts--> IdentPartData (operand table).</summary>
internal sealed partial class Code(ProjectModel model)
{
    public static readonly FrozenDictionary<string, string> Operators = new Dictionary<string, string>
    {
        ["OpAs"] = ":=", ["OpPlAs"] = "+=", ["OpMinAs"] = "-=", ["OpMulAs"] = "*=", ["OpDivAs"] = "/=", ["OpPa"] = "=>",
        ["OpPl"] = "+", ["OpMi"] = "-", ["OpMu"] = "*", ["OpDi"] = "/", ["OpMOD"] = "MOD", ["OpPo"] = "**",
        ["OpNOT"] = "NOT", ["OpAND"] = "AND", ["OpOR"] = "OR", ["OpXOR"] = "XOR", ["OpAAND"] = "&",
        ["OpRef"] = "REF", ["OpDeref"] = "^", ["OpE"] = "=", ["OpU"] = "<>", ["OpG"] = ">", ["OpL"] = "<", ["OpGE"] = ">=", ["OpLE"] = "<=",
    }.ToFrozenDictionary();

    public static readonly FrozenDictionary<string, string> Keywords = new Dictionary<string, string>
    {
        ["KwTHEN"] = "THEN", ["KwELSIF"] = "ELSIF", ["KwELSE"] = "ELSE", ["KwENDIF"] = "END_IF", ["KwDO"] = "DO", ["KwTO"] = "TO",
        ["KwENDFOR"] = "END_FOR", ["KwBY"] = "BY", ["KwOF"] = "OF", ["KwENDC"] = "END_CASE", ["KwENDW"] = "END_WHILE",
        ["KwENDR"] = "END_REPEAT", ["KwUNTIL"] = "UNTIL", ["KwEndRegion"] = "END_REGION",
    }.ToFrozenDictionary();

    public static readonly FrozenDictionary<string, string> Punctuation = new Dictionary<string, string>
    {
        ["FiSt"] = ";", ["Comma"] = ",", ["BracO"] = "(", ["BracC"] = ")", ["Dot"] = ".", ["BoxO"] = "[", ["BoxC"] = "]",
        ["Colon"] = ":", ["LDots"] = "..", ["BCE"] = "*)",
    }.ToFrozenDictionary();

    public static readonly FrozenSet<string> Containers = new string[] { "Statement", "Statements", "Expression", "Fold", "InstCa", "Param", "BC",
        "CaseElem", "CaseRange", "Label", "SymVaP", "RootStatements", "FctCa", "CaseSRange" }.ToFrozenSet();

    public static readonly FrozenDictionary<string, (string Pre, string Att)> AttributeText = new Dictionary<string, (string Pre, string Att)>
    {
        ["LC"] = ("//", "TE"), ["BCL"] = ("", "TE"), ["SymVa"] = ("", "ODN"), ["SymPa"] = ("", "ODN"), ["Sub"] = ("", "ODN"),
        ["SymLa"] = ("", "ODN"), ["Const"] = ("", "TE"),
    }.ToFrozenDictionary();

    readonly ProjectModel m = model;
    readonly Project p = model.P;

    /// <summary>XML root of a blob value (pages in store records resolved through the bound store).</summary>
    static XElement? XmlOfBlob(object? blobv)
    {
        if (blobv is not byte[] blob || blob.Length == 0)
            return null;
        var b = Attrs.DecodeBlob(blob);
        return b is { Length: > 0 } ? Et.FromString(Py.Utf8SigExact(b)) : null;
    }

    /// <summary>(text, unknown tokens) of an &lt;SCLSource&gt; element.</summary>
    public static (string Text, List<string> Unknown) SclText(XElement root)
    {
        var o = new StringBuilder();
        var unknown = new HashSet<string>();

        void Walk(XElement el)
        {
            var t = Et.Local(el);
            if (t == "NL")
                o.Append('\n');
            else if (t == "BL")
                o.Append(' ', (int)Math.Max(0, Py.ParseInt(Et.Get(el, "NumBLs", "1"))));
            else if (AttributeText.TryGetValue(t, out var pa))
                o.Append(pa.Pre).Append(Py.Bool(Et.Get(el, pa.Att)) ? Et.Get(el, pa.Att) : t == "Const" ? Et.Get(el, "ODN", "") : "");
            else if (t == "AdMod" && Et.Get(el, "SI") == "MoP")
                o.Append(":P");
            else if (Operators.TryGetValue(t, out var op))
                o.Append(op);
            else if (Keywords.TryGetValue(t, out var kw))
                o.Append(kw);
            else if (Punctuation.TryGetValue(t, out var pu))
                o.Append(pu);
            else if (Et.NoKids(el) && t is not ("Statements" or "Fold"))
            {
                unknown.Add(t);
                o.Append('<').Append(t).Append('>');
            }
            else
            {
                if (!Containers.Contains(t) && t is not ("Statements" or "Fold"))
                    unknown.Add(t);
                if (t == "BC")
                    o.Append("(*");
                else if (t is "Label" or "SymVaP")
                    o.Append(Et.Get(el, "ODN", ""));
                if (Py.Bool(Et.Get(el, "TE")) && t == "Statement")
                    o.Append(Et.Get(el, "TE"));
                foreach (var c in Et.Kids(el))
                    Walk(c);
            }
        }

        var rs = Et.Kids(root).FirstOrDefault(c => Et.Local(c) == "RootStatements");
        if (rs is not null)
            foreach (var c in Et.Kids(rs))
                Walk(c);
        return (o.ToString().Trim('\n'), Py.Sorted(unknown));
    }

    /// <summary>How the SCL renderer treats a tag: text, attribute, container, special or null.</summary>
    public static string? TokenStatus(string tag)
    {
        if (Operators.ContainsKey(tag) || Keywords.ContainsKey(tag) || Punctuation.ContainsKey(tag))
            return "text";
        if (AttributeText.ContainsKey(tag))
            return "attribute";
        if (Containers.Contains(tag))
            return "container";
        if (tag is "NL" or "BL" or "AdMod" or "Statements" or "Fold")
            return "special";
        return null;
    }

    public static readonly FrozenDictionary<string, string> IdentKinds = new Dictionary<string, string>
    {
        ["InterfaceAccess"] = "variable of the block interface (SSD@S section, @MID member id, AOS/AO path)",
        ["MultInstAccess"] = "multi-instance (static variable of an FB type)",
        ["SimpleAccess"] = "global tag (ID@N, SAD@R = Input/Output/Memory area)",
        ["GlobalAccess"] = "member of a data block (AOS/AO path: DB name, members), BAD@BIRID = interface info of the DB",
        ["LocalConstant"] = "constant of the block interface (LCD@MID, CB@V value)",
        ["GlobalConstant"] = "user constant of the tag table (CB@V value)",
        ["LiteralConstant"] = "literal (CB@T type, CB@SV literal text, CB@V value)",
        ["Label"] = "jump label (ID@N, LD@LID; C@AK Jump | Definition)",
        ["Expression"] = "equation of a Calculate box (ED@SU, ED@CV)",
        ["Instruction"] = "instruction box (ID@N = name, INSTD, TPI/TP template parameters, LPI/LP pins, IUS/IU/TV template values)",
        ["FCBlock"] = "called/used FC",
        ["FBBlock"] = "called/used FB",
        ["AufDBBlock"] = "instance DB of a call (ID@N)",
        ["DepDBBlock"] = "data block used by an access",
        ["BlockInterfaceInfo"] = "interface snapshot of a called block (BIID, BPIL/BPI = parameters N, S section, A, R, F)",
    }.ToFrozenDictionary();

    static readonly Regex Escape = new("_x([0-9A-Fa-f]{4})_", RegexOptions.CultureInvariant);

    /// <summary>XML name escapes of the ident table (_x0030_ = '0').</summary>
    public static string Unescape(string? s) => Escape.Replace(s ?? "", mo => ((char)Convert.ToInt32(mo.Groups[1].Value, 16)).ToString());

    static XElement? First(XElement? el, string tag) => el is null ? null : Et.Kids(el).FirstOrDefault(c => Et.Local(c) == tag);

    /// <summary>RID -> entry of the ident table of one block.</summary>
    public sealed class Idents
    {
        public readonly Dictionary<long, PyDict> ByRid = [];
        /// <summary>Raw ident element per RID (SimaticML output reads the instruction data from it).</summary>
        public readonly Dictionary<long, XElement> RawByRid = [];
        public readonly HashSet<string> Unknown = [];

        public Idents(Code code, string block)
        {
            var ic = code.m.Rel1(block, "CoreObject2IdentContainer") ?? code.Untagged(block, "IdentContainerData").FirstOrDefault();
            foreach (var part in ic is not null ? code.Linked(ic, "IdentParts", "IdentPartData") : [])
            {
                var root = XmlOfBlob(code.p.Attr(part, "IIdentPartData", "PayLoad"));
                foreach (var el in Et.Kids(root).ToList())
                    Add(el);
            }
        }

        static List<object?> L(IEnumerable<object?> x) => x.ToList();

        void Add(XElement el)
        {
            var t = Et.Local(el);
            if (!IdentKinds.ContainsKey(t))
                Unknown.Add(t);
            var idel = First(el, "ID");
            if (idel is null)
                return;
            var rid = Py.ParseInt(Et.Get(idel, "RID") ?? throw new PyTypeError("int() argument must be a string, a bytes-like object or a real number, not 'NoneType'"));
            var e = PyDict.Of("kind", t, "rid", rid, "scope", Et.Get(idel, "S"), "name", Unescape(Et.Get(idel, "N")), "uses", new PyDict());
            var uses = (PyDict)e["uses"]!;
            var cs = First(idel, "CS");
            foreach (var c in Et.Kids(cs).ToList())
                uses[Py.T(Py.ParseInt(Et.Get(c, "NID")!), Py.ParseInt(Et.Get(c, "UID")!))] = Py.Or(Et.Get(c, "AK"), "Read");
            var td = First(First(el, "OD"), "TD");
            if (td is not null && Py.Bool(Et.Get(td, "T")))
            {
                e["type"] = Py.AfterLast(Et.Get(td, "T")!, ":");
                e["typeKey"] = Et.Get(td, "T");
            }
            var ssd = First(el, "SSD");
            if (ssd is not null)
            {
                var aos = First(ssd, "AOS");
                var aosL = Et.Kids(aos).Where(a => Et.Local(a) == "AO").ToList();
                e["path"] = L(aosL.Select(a => (object?)Unescape(Et.Get(a, "N"))));
                e["indexRefs"] = L(aosL.Select(a => (object?)L((Et.Get(a, "RIDI") ?? "").Split(',').Where(x => Py.IsDigit(Py.Strip(x))).Select(x => (object?)Py.ParseInt(x)))));
                e["slice"] = Et.Get(ssd, "AM");
                e["section"] = Et.Get(ssd, "S");
                e["memberId"] = Et.Get(ssd, "MID");
                if (t is "InterfaceAccess" or "MultInstAccess" or "GlobalAccess" && aosL.Count > 0)
                    e["name"] = string.Join(".", ((List<object?>)e["path"]!).Select(x => (string)x!));
            }
            var cb = Et.Iter(el).FirstOrDefault(c => Et.Local(c) == "CB");
            if (cb is not null)
                e["constant"] = PyDict.Of("type", Et.Get(cb, "T"), "value", Et.Get(cb, "V"), "text", Unescape(Et.Get(cb, "SV")), "kind", Et.Get(cb, "ST"));
            var sad = First(el, "SAD");
            if (sad is not null)
            {
                e["area"] = Et.Get(sad, "R");
                e["periphery"] = Et.Get(sad, "SAM") == "Peripherie";
                if (ssd is not null && Py.IsDigit((Et.Get(ssd, "AO") ?? "").TrimStart('-')))
                    e["bitOffset"] = Py.ParseInt(Et.Get(ssd, "AO")!);
                var od = First(el, "OD");
                if (od is not null && Py.IsDigit(Et.Get(od, "S") ?? ""))
                    e["size"] = Py.ParseInt(Et.Get(od, "S")!);
            }
            if (t == "Instruction")
            {
                var instd = First(el, "INSTD");
                var tu = new PyDict();
                e["templateUses"] = tu;
                var ius = First(instd, "IUS");
                foreach (var iu in Et.Kids(ius).ToList())
                {
                    var vals = new PyDict();
                    foreach (var tv in Et.Kids(iu).Where(x => Et.Local(x) == "TV"))
                        vals[Et.Get(tv, "N")] = Et.Get(tv, "V");
                    tu[Py.T(Py.ParseInt(Et.Get(iu, "NId")!), Py.ParseInt(Et.Get(iu, "UId")!))] = vals;
                }
                var lpi = First(instd, "LPI");
                e["pins"] = L(Et.Kids(lpi).ToList().Select(lp => (object?)PyDict.Of("name", Et.Get(lp, "N"), "section", Et.Get(lp, "S"),
                    "type", Py.Or(Et.Get(lp, "TN"), Et.Get(lp, "T")))));
            }
            if (t == "BlockInterfaceInfo")
            {
                var bpil = First(el, "BPIL") ?? First(First(el, "BIID"), "BPIL");
                e["parameters"] = L(Et.Kids(bpil).ToList().Select(b => (object?)PyDict.Of("name", Et.Get(b, "N"), "section", Et.Get(b, "S"))));
            }
            if (t is "FCBlock" or "FBBlock" or "AufDBBlock" or "DepDBBlock")
            {
                var tod = First(el, "TOD");
                e["blockNumber"] = tod is not null ? Et.Get(tod, "N") : null;
                e["blockType"] = tod is not null ? Et.Get(tod, "BT") : null;
            }
            ByRid[rid] = e;
            RawByRid[rid] = el;
        }

        public PyDict? Get(object? rid)
        {
            if (rid is null)
                return null;
            var r = rid is string s ? Py.ParseInt(s) : Py.Long(rid);
            return ByRid.GetValueOrDefault(r);
        }

        /// <summary>Operand text: path with array indices in brackets and slice access (.%X3).</summary>
        public string? Text(PyDict? e, int depth = 0)
        {
            if (e is null)
                return "";
            if (Py.Eq(e["kind"], "LiteralConstant") && Py.Bool(e.Get("constant")))
                return (string?)((PyDict)e["constant"]!)["text"];
            if (!Py.Bool(e["name"]) && Py.Bool(e.Get("area")) && e.Get("bitOffset") is not null)
                return AddressText(e["area"], (long)e["bitOffset"]!, e.Get("size"), e.Get("periphery"));
            if (!Py.Bool(e.Get("path")) || depth > 6)
                return (string?)e["name"];
            var path = (List<object?>)e["path"]!;
            var irefs = Py.Bool(e.Get("indexRefs")) ? (List<object?>)e["indexRefs"]! : path.Select(_ => (object?)new List<object?>()).ToList();
            var parts = new List<string>();
            for (var i = 0; i < Math.Min(path.Count, irefs.Count); i++)
            {
                var refs = (List<object?>)irefs[i]!;
                var idx = string.Join(",", refs.Select(r => Text(Get(r), depth + 1)));
                parts.Add((string)path[i]! + (refs.Count > 0 ? "[" + idx + "]" : ""));
            }
            var o = string.Join(".", parts);
            if (e.Get("slice") is string sl && sl.Length > 0)
                o += ".%" + sl[..1].ToUpperInvariant() + sl[1..];
            return o;
        }

        /// <summary>Readable operand of an ident entry used by part uid of network net.</summary>
        public PyDict? Operand(PyDict? e, object? net, long uid)
        {
            if (e is null)
                return null;
            var o = PyDict.Of("identKind", e["kind"], "name", Text(e), "scope", e["scope"],
                "access", ((PyDict)e["uses"]!).Get(Py.T(net, uid), "Read"));
            foreach (var k in new[] { "type", "section", "memberId", "area", "constant" })
                if (e.Get(k) is not null)
                    o[k] = e[k];
            if (Py.Bool(e.Get("area")) && e.Get("bitOffset") is not null)
                o["address"] = AddressText(e["area"], (long)e["bitOffset"]!, e.Get("size"), e.Get("periphery"));
            return o;
        }
    }

    static readonly Dictionary<string, string> AreaLetter = new() { ["Memory"] = "M", ["Input"] = "I", ["Output"] = "Q", ["Data"] = "DB", ["XData"] = "DI" };

    /// <summary>Absolute address as TIA prints it: %M10.3, %MW2, %IW0:P, T1, C5.</summary>
    public static string AddressText(object? area, long bitOffset, object? size, object? periphery = null)
    {
        if (Py.Eq(area, "Timer"))
            return "T" + bitOffset;
        if (Py.Eq(area, "Counter"))
            return "C" + bitOffset;
        var letter = area is string a && AreaLetter.TryGetValue(a, out var l) ? l : Py.Str(area);
        string txt;
        if (size is null || Py.Eq(size, 1L))
            txt = $"%{letter}{(letter is "DB" or "DI" ? "X" : "")}{Py.FloorDiv(bitOffset, 8)}.{Py.Mod(bitOffset, 8)}";
        else
        {
            var sz = Py.Long(size) switch { 8 => "B", 16 => "W", 32 => "D", 64 => "L", _ => "?" };
            txt = $"%{letter}{sz}{Py.FloorDiv(bitOffset, 8)}";
        }
        return txt + (Py.Bool(periphery) ? ":P" : "");
    }

    /// <summary>Short operand text of a network part: variable path, tag, DB path, 'const:literal' for constants.</summary>
    public static string? OperandLabel(PyDict x)
    {
        if (Py.Eq(x.Get("identKind"), "LiteralConstant") && Py.Bool(x.Get("constant")))
            return "const:" + (string?)((PyDict)x["constant"]!)["text"];
        if (x.Get("identKind") is "LocalConstant" or "GlobalConstant")
            return "const:" + (string?)x["name"];
        if (Py.Eq(x.Get("identKind"), "Label"))
            return "label:" + (string?)x["name"];
        return (string?)Py.OrElse(x.Get("name"), () => "refId " + Py.Str(x.Get("refId")));
    }

    /// <summary>Targets of the untagged links (typed link list, relation implicit by slot) of key that are of the given type.
    /// Large blocks (here FB_StlMath of sample 064 with 69 networks) keep Sources and the ident container this way.</summary>
    List<string> Untagged(string key, string type) =>
        p.Links(key).Where(l => l.Relation is null && p.Objects.ContainsKey(l.Key) && p.IsA(l.Key, type)).Select(l => l.Key).ToList();

    /// <summary>Targets of relation, or the untagged links to objects of type when the relation has none.</summary>
    List<string> Linked(string key, string relation, string type) => p.Targets(key, relation) is { Count: > 0 } t ? t : Untagged(key, type);

    public List<object?> Networks(string block)
    {
        var units = Linked(block, "Sources", "CompileUnitData").Select(k => (Ref: p.Attr(k, "ICompileUnitData", "RefID"), K: k)).ToList();
        var idents = new Idents(this, block);
        var graphRoles = GraphRoles(idents);
        var seenSequence = false;
        var outl = new List<object?>();
        for (var i = 0; i < units.Count; i++)
        {
            var (rf, k) = units[i];
            var cu = p.Sets(k).Set("ICompileUnitData");
            var lang = cu.Get("ProgrammingLanguage");
            var title = new PyDict();
            foreach (var (c, t) in m.Comment(k))
                if (Py.Bool(t))
                    title[c] = t;
            var ct = m.Rel1(k, "CompileUnitComment");
            var comment0 = ct is not null ? m.CoreTextOf(p.Attr(ct, "ICoreTextRepository", "Text")) : new PyDict();
            var comment = new PyDict();
            foreach (var (c, t) in comment0)
                if (Py.Bool(t))
                    comment[c] = t;
            var n = PyDict.Of("index", (long)(i + 1),
                "language", lang is "LAD_CLASSIC" ? "LAD" : lang is "FBD_CLASSIC" ? "FBD" : lang,
                "title", title, "comment", comment, "key", k);
            var root = XmlOfBlob(cu.Get("Data"));
            if (root is null)
            {
                var data = cu.Get("Data");
                if (data is byte[] db && !db.Contains((byte)'<'))
                    n["empty"] = true;
                else
                    n["error"] = "no network source";
            }
            else if (Et.Local(root) == "SCLSource")
            {
                var (scl, unknown) = SclText(root);
                n["scl"] = scl;
                if (unknown.Count > 0)
                    n["unknownTokens"] = unknown.Cast<object?>().ToList();
            }
            else if (Et.Local(root) == "FlgNet")
                n.Update(FlgNet(root, rf, idents));
            else if (Et.Local(root) == "Statements")
            {
                var (stl, unknown) = StlText(root, idents, rf);
                n["stl"] = stl;
                if (unknown.Count > 0)
                    n["unknownTokens"] = unknown.Cast<object?>().ToList();
            }
            else if (Et.Local(root) == "EncryptedData")
                n["encrypted"] = true;
            else if (Et.Local(root) == "GraphStructure")
                n["sequence"] = GraphSequence(root, idents);
            else if (Et.Local(root) == "AddInfos")
                n["alarmSettings"] = GraphAlarmSettings(root);
            else if (Et.Local(root) == "Actions")
                n["actions"] = Et.Kids(root).Where(a => Et.Local(a) == "Action").Select(a => (object?)PyDict.Of("event", Et.Get(a, "Event"),
                    "interlock", Et.Get(a, "Interlock") == "true",
                    "qualifier", Et.Kids(a).Where(o => Et.Local(o) == "Operator").Select(o => Et.Get(o, "Operation")).FirstOrDefault(),
                    "operands", Et.Kids(a).Where(o => Et.Local(o) == "OpdAccess").Select(o => (object?)StlOperand(idents, Et.Get(o, "RefId"))).ToList())).ToList();
            else
                n["error"] = "unknown network source <" + Et.Local(root) + ">";
            if (rf is not null && graphRoles.TryGetValue(Py.Long(rf), out var gr))
            {
                n["graphRole"] = gr.Role;
                n["graphElement"] = gr.Owner;
            }
            // permanent operations of a GRAPH block (sample 077): LAD/FBD units of no step or transition, before or after the sequence
            else if (graphRoles.Count > 0 && n.ContainsKey("parts"))
                n["graphRole"] = seenSequence ? "PostOperation" : "PreOperation";
            if (root is not null && Et.Local(root) == "GraphStructure")
                seenSequence = true;
            outl.Add(n);
        }
        return outl;
    }

    /// <summary>GRAPH (sample 074): compile unit RefID -> (role, step or transition name) from the ident entries of the
    /// steps and transitions (CS/C NID = unit RefID, AK = Actions | Supervision | Interlock | Transition).</summary>
    static Dictionary<long, (string Role, string? Owner)> GraphRoles(Idents idents)
    {
        var r = new Dictionary<long, (string, string?)>();
        foreach (var (rid, el) in idents.RawByRid)
            foreach (var c in Et.Kids(First(First(el, "ID"), "CS")))
                if (Et.Get(c, "AK") is "Actions" or "Supervision" or "Interlock" or "Transition" && Py.IsDigit(Et.Get(c, "NID") ?? ""))
                    r[Py.ParseInt(Et.Get(c, "NID")!)] = (Et.Get(c, "AK")!, (string?)idents.ByRid[rid].Get("name"));
        return r;
    }

    /// <summary>Steps, transitions and connections of a GRAPH sequence (&lt;GraphStructure&gt;); numbers are UId - 100000
    /// (steps) and UId - 200000 (transitions), names the static members of the step and transition structures.</summary>
    static PyDict GraphSequence(XElement root, Idents idents)
    {
        var seq = First(root, "Sequence");
        string? Name(XElement e) => (string?)idents.Get(Et.Get(e, "RefId"))?.Get("name");
        var steps = Et.Kids(First(seq, "Steps")).Select(st => (object?)PyDict.Of("number", Py.ParseInt(Et.Get(st, "UId")!) - 100000,
            "name", Name(st), "init", Et.Get(st, "Init") == "true")).ToList();
        var trans = Et.Kids(First(seq, "Transitions")).Select(t => (object?)PyDict.Of("number", Py.ParseInt(Et.Get(t, "UId")!) - 200000,
            "name", Name(t))).ToList();
        static string Node(XElement e) => Et.Local(e) switch
        {
            "StepRef" => "S" + Et.Get(e, "No"),
            "TransitionRef" => "T" + Et.Get(e, "No"),
            var t => t + string.Concat(e.Attributes().Where(a => a.Name.LocalName != "LinkType").Select(a => " " + a.Name.LocalName + "=" + a.Value)),
        };
        var conns = Et.Kids(First(seq, "Structure")).Where(c => Et.Local(c) == "Connection").Select(c => Et.Kids(c).ToList()).Where(e => e.Count >= 2)
            .Select(e => (object?)PyDict.Of("from", Node(e[0]), "to", Node(e[1]), "linkType", Et.Get(e[1], "LinkType") ?? "Direct")).ToList();
        var unknown = Et.Kids(seq).Select(Et.Local).Where(t => t is not ("Steps" or "Transitions" or "Branches" or "Structure")).ToList();
        var res = PyDict.Of("steps", steps, "transitions", trans, "branches", Et.Kids(First(seq, "Branches")).Select(b => (object?)Et.Attrib(b)).ToList(),
            "connections", conns);
        if (unknown.Count > 0)
            res["unknownElements"] = unknown.Cast<object?>().ToList();
        return res;
    }

    /// <summary>Alarm settings of a GRAPH block (&lt;AddInfos&gt;/Alarming): display class per supervision category and the
    /// categories of interlock, supervision and warning alarms.</summary>
    static PyDict GraphAlarmSettings(XElement root)
    {
        var al = First(root, "Alarming");
        var res = PyDict.Of("categories", Et.Kids(First(al, "CategoryProperties")).Select(c => (object?)PyDict.Of("index", Et.Get(c, "Index"),
            "displayClass", Et.Text(First(c, "DisplayClass")))).ToList());
        foreach (var x in Et.Kids(al).Where(x => Et.Local(x) != "CategoryProperties"))
            res[Et.Local(x)] = Et.Get(x, "Index");
        return res;
    }

    /// <summary>Parameter names the .awl export puts in quotes because they are STL keywords.</summary>
    static readonly FrozenSet<string> StlKeywordParams = new[] { "TIMER", "COUNTER", "db" }.ToFrozenSet();

    /// <summary>Elements of an STL &lt;Statements&gt; network the renderer knows (anything else is reported as unknown).</summary>
    public static readonly FrozenSet<string> StlElements = new string[] { "Statement", "Token", "OpdAccess", "UserToken", "Label", "LC",
        "CallInfo", "ParaExpression" }.ToFrozenSet();

    /// <summary>STL operand as the STL source export writes it: #local, "global" (also tags TIA created for absolute
    /// addresses), literal, jump label, %DB100 for a data block without symbol.</summary>
    public static string StlOperand(Idents idents, string? refId)
    {
        var e = idents.Get(refId);
        if (e is null)
            return "<refId " + refId + ">";
        var kind = (string?)e["kind"];
        var text = idents.Text(e) ?? "";
        return kind switch
        {
            "InterfaceAccess" or "MultInstAccess" => "#" + text,
            "SimpleAccess" when Py.Bool(e["name"]) => "\"" + text + "\"",
            "GlobalAccess" => string.Join(".", text.Split('.').Select((s, i) => i == 0 ? "\"" + s + "\"" : s)),
            "LocalConstant" => "#" + text,
            "FCBlock" or "FBBlock" or "AufDBBlock" or "DepDBBlock" when !Py.Bool(e["name"]) =>
                "%" + Py.Str(e.Get("blockType")) + Py.Str(e.Get("blockNumber")),
            "GlobalConstant" or "FCBlock" or "FBBlock" or "AufDBBlock" or "DepDBBlock" => "\"" + text + "\"",
            _ => text,
        };
    }

    /// <summary>(text, unknown elements) of an STL &lt;Statements&gt; network in the layout of the STL source export
    /// (.awl): one statement per line, 6 blanks indent, "label:" in front, tokens and operands separated by blanks
    /// (a "," token sticks to the operand before it), ";", "//comment". A CALL with parameters (CallInfo /
    /// ParaExpression FPNum = parameter of the called block's interface) is followed by the parameter list. The editor
    /// spacing (NumBLs) is not used.</summary>
    public static (string Text, List<string> Unknown) StlText(XElement root, Idents idents, object? net = null)
    {
        var lines = new List<string>();
        var unknown = new HashSet<string>();
        foreach (var st in Et.Kids(root))
        {
            if (Et.Local(st) != "Statement")
            {
                unknown.Add(Et.Local(st));
                continue;
            }
            string? label = null, comment = null;
            var body = new StringBuilder();
            List<string>? call = null;
            string? templates = null;
            void Word(string w) => body.Append(body.Length > 0 ? " " : "").Append(w);
            foreach (var c in Et.Kids(st))
            {
                switch (Et.Local(c))
                {
                    case "Label":
                        var lo = Et.Kids(c).FirstOrDefault(x => Et.Local(x) == "OpdAccess");
                        label = lo is not null ? StlOperand(idents, Et.Get(lo, "RefId")) : "";
                        foreach (var x in Et.Kids(c).Where(x => Et.Local(x) is not ("OpdAccess" or "Token")))
                            unknown.Add("Label/" + Et.Local(x));
                        break;
                    case "Token" when Et.Get(c, "DispName") is null && Et.Get(c, "Kw") is "," or "[" or "]":
                        body.Append(Et.Get(c, "Kw"));
                        break;
                    case "Token":
                        Word(Et.Get(c, "DispName") ?? "<Kw " + Et.Get(c, "Kw") + ">");
                        break;
                    case "UserToken":
                        Word(Et.Get(c, "DispName") ?? "");
                        break;
                    case "OpdAccess":
                        // a built-in instruction (MOVE_BLK, SCALE_X, sample 075) has RefId 0 and its name in DispName
                        Word(Et.Get(c, "RefId") == "0" && Et.Get(c, "DispName") is { } dn ? dn : StlOperand(idents, Et.Get(c, "RefId")));
                        break;
                    case "LC":
                        comment = Et.Get(c, "DispName") ?? "";
                        break;
                    case "CallInfo":
                        var callee = idents.Get(Et.Get(c, "RefId"));
                        // a system instruction (sample 075): its pins, and the type templates of this call as a pragma line
                        var pars = (callee?.Get("parameters") as List<object?> ?? callee?.Get("pins") as List<object?> ?? []).Cast<PyDict>().ToList();
                        if (Py.Eq(callee?.Get("kind"), "Instruction"))
                            templates = StlTemplates(idents, callee!, net, st);
                        call = [];
                        var inline = new List<string>();
                        foreach (var pe in Et.Kids(c))
                        {
                            // templates of a built-in instruction stand inline: TemplateParameter Type Name Value, separated by
                            // Token Type="LinkingWord"
                            if (Et.Local(pe) == "TemplateParameter")
                            {
                                // Type and Operation templates (CompType relation := 'EQ', sample 075)
                                if (Et.Get(pe, "Type") is "Type" or "Operation")
                                    inline.Add($"{Et.Get(pe, "Name")} := '{Et.Get(pe, "Value")}'");
                                continue;
                            }
                            if (Et.Local(pe) == "Token" && Et.Get(pe, "Type") == "LinkingWord")
                                continue;
                            if (Et.Local(pe) != "ParaExpression")
                            {
                                unknown.Add("CallInfo/" + Et.Local(pe));
                                continue;
                            }
                            var no = (int)Py.ParseInt(Et.Get(pe, "FPNum") ?? "0");
                            var pname = Et.Get(pe, "DispName") ?? (no >= 1 && no <= pars.Count ? (string?)pars[no - 1]["name"] ?? "" : "<FPNum " + no + ">");
                            var vals = Et.Kids(pe).Select(v => Et.Local(v) switch
                            {
                                "OpdAccess" => StlOperand(idents, Et.Get(v, "RefId")),
                                "UserToken" => Et.Get(v, "DispName") ?? "",
                                _ => "<" + Et.Local(v) + ">",
                            });
                            foreach (var v in Et.Kids(pe).Where(v => Et.Local(v) is not ("OpdAccess" or "UserToken")))
                                unknown.Add("ParaExpression/" + Et.Local(v));
                            // an unassigned pin of an FB call (DRUM EVENT3..16, sample 075) is not written in the source
                            if (!Et.Kids(pe).Any())
                                continue;
                            // a parameter named like an STL keyword is quoted in the source (RESET_TIMER "TIMER", ReadFromArrayDB "db", sample 075)
                            if (StlKeywordParams.Contains(pname))
                                pname = "\"" + pname + "\"";
                            call.Add(pname.PadRight(28) + ":= " + string.Join(" ", vals));
                        }
                        if (inline.Count > 0)
                            templates = "{" + string.Join(", ", inline) + "}";
                        break;
                    default:
                        unknown.Add(Et.Local(c));
                        break;
                }
            }
            if (body.Length == 0 && label is null && comment is null)
            {
                lines.Add("");
                continue;
            }
            var o = new StringBuilder(label is not null ? label + ":" : "").Append("      ");
            if (body.Length > 0)
                o.Append(body).Append(call is { Count: > 0 } ? "" : comment is not null ? " ;" : ";");
            if (comment is not null)
                o.Append("//").Append(comment);
            lines.Add(o.ToString());
            if (templates is not null)
                lines.Add("      " + templates);
            if (call is { Count: > 0 })
            {
                for (var i = 0; i < call.Count; i++)
                    lines.Add((i == 0 ? "      (  " : "         ") + call[i] + (i < call.Count - 1 ? " , " : ""));
                lines.Add("      );");
            }
        }
        return (string.Join("\n", lines), Py.Sorted(unknown));
    }

    /// <summary>"{name := 'value', ...}" of the type templates (TPI/TP T=type) of an instruction call in an STL statement, as the
    /// .awl export writes it; the values are the template uses (IUS/IU) of the call's elements in this network.</summary>
    static string? StlTemplates(Idents idents, PyDict ins, object? net, XElement st)
    {
        var raw = idents.RawByRid.GetValueOrDefault(Py.Long(ins["rid"]));
        var tpi = First(First(raw, "INSTD"), "TPI");
        // type templates and operation templates (CompType relation := 'EQ', sample 075), in declaration order
        var types = Et.Kids(tpi).Where(tp => Et.Get(tp, "T") is "type" or "operation").Select(tp => Et.Get(tp, "N") ?? "").ToList();
        var uses = ins.Get("templateUses") as PyDict ?? new PyDict();
        // the template values can be spread over several uses of the statement (CompType: src_type and relation, sample 075)
        PyDict? tv = null;
        foreach (var e in Et.Iter(st))
            if (Et.Get(e, "UId") is { } u && Py.IsDigit(u) && uses.Get(Py.T(net, Py.ParseInt(u))) is PyDict d)
            {
                tv ??= new PyDict();
                foreach (var (k, v) in d)
                    if (tv.Get(k) is null)
                        tv[k] = v;
            }
        var items = types.Where(n => tv?.Get(n) is not null).Select(n => $"{n} := '{Py.Str(tv!.Get(n))}'").ToList();
        return items.Count > 0 ? "{" + string.Join(", ", items) + "}" : null;
    }

    /// <summary>Parts and wires of a LAD/FBD network.</summary>
    public PyDict FlgNet(XElement root, object? refid, Idents idents)
    {
        var parts = new List<object?>();
        var wires = new List<object?>();
        var unhandled = new List<string>();
        var labels = new List<object?>();
        foreach (var sec in Et.Kids(root))
        {
            var t = Et.Local(sec);
            if (t == "Parts")
            {
                foreach (var el in Et.Kids(sec))
                    parts.Add(FlgPart(el, refid, idents, unhandled));
            }
            else if (t == "Labels")
            {
                foreach (var lab in Et.Kids(sec))
                {
                    var o = idents.Operand(idents.Get(Et.Get(lab, "RefId")), refid, Py.ParseInt(Et.Get(lab, "UId")!));
                    labels.Add(PyDict.Of("uid", Py.ParseInt(Et.Get(lab, "UId")!), "name", o?["name"]));
                }
            }
            else if (t == "Wires")
            {
                foreach (var w in Et.Kids(sec))
                {
                    var ends = new List<object?>();
                    foreach (var c in Et.Kids(w))
                    {
                        var ctag = Et.Local(c);
                        if (ctag == "Powerrail")
                            ends.Add(PyDict.Of("powerrail", true));
                        else if (ctag == "OCon")
                            ends.Add(PyDict.Of("operand", Py.ParseInt(Et.Get(c, "UId")!)));
                        else if (ctag == "PCon")
                            ends.Add(PyDict.Of("part", Py.ParseInt(Et.Get(c, "UId")!), "pin", Et.Get(c, "PinName")));
                        else
                        {
                            unhandled.Add("Wire/" + ctag);
                            ends.Add(PyDict.Of(ctag, Et.Attrib(c)));
                        }
                    }
                    wires.Add(PyDict.Of("uid", Py.ParseInt(Et.Get(w, "UId")!), "ends", ends));
                }
            }
            else
                unhandled.Add(t);
        }
        var byuid = new PyDict();
        foreach (PyDict x in parts)
            byuid[x["uid"]] = x;
        foreach (PyDict w in wires)
        {
            var ends = (List<object?>)w["ends"]!;
            for (var i = 0; i < ends.Count; i++)
            {
                var e = (PyDict)ends[i]!;
                if (e.ContainsKey("operand"))
                {
                    var x = byuid.Get(e["operand"]) as PyDict ?? new PyDict();
                    ends[i] = Py.Eq(x.Get("kind"), "open") ? PyDict.Of("open", true) : PyDict.Of("operand", OperandLabel(x));
                }
                else if (e.ContainsKey("part"))
                    e["name"] = (byuid.Get(e["part"]) as PyDict ?? new PyDict()).Get("name");
            }
        }
        var res = PyDict.Of("parts", parts, "wires", wires);
        if (labels.Count > 0)
            res["labels"] = labels;
        if (unhandled.Count > 0)
            res["unhandled"] = Py.Sorted(unhandled.Distinct()).Cast<object?>().ToList();
        if (idents.Unknown.Count > 0)
            res["unknownIdentKinds"] = Py.Sorted(idents.Unknown).Cast<object?>().ToList();
        return res;
    }

    PyDict FlgPart(XElement el, object? refid, Idents idents, List<string> unhandled)
    {
        var t = Et.Local(el);
        var uid = Py.ParseInt(Et.Get(el, "UId") ?? throw new PyTypeError("int() argument must be a string, not 'NoneType'"));
        var rf = Et.Get(el, "RefId");
        if (t == "Part")
        {
            var e = PyDict.Of("uid", uid, "kind", "part", "name", Et.Get(el, "Gate"));
            foreach (var x in Et.Kids(el))
            {
                var xt = Et.Local(x);
                if (xt == "TemplateValue")
                    ((PyDict)e.SetDefault("templateValues", new PyDict())!)[Et.Get(x, "Name")] = Et.Text(x);
                else if (xt == "Negated")
                    ((List<object?>)e.SetDefault("negated", new List<object?>())!).Add(Et.Get(x, "PinName"));
                else if (xt == "AutomaticTyped")
                    ((List<object?>)e.SetDefault("automaticTyped", new List<object?>())!).Add(Et.Get(x, "Name"));
                else if (xt == "Equation")
                {
                    e["equation"] = Py.Strip(Et.Text(x) ?? "");
                    e["equationValid"] = Et.Get(x, "Valid") == "true";
                }
                else
                    unhandled.Add("Part/" + xt);
            }
            if (Py.Bool(Et.Get(el, "DisableENO")))
                e["disableEno"] = Et.Get(el, "DisableENO") == "true";
            return e;
        }
        if (t == "ORef")
        {
            if (rf is null)
                return PyDict.Of("uid", uid, "kind", "open");
            var o = idents.Operand(idents.Get(rf), refid, uid);
            var e = PyDict.Of("uid", uid, "kind", "operand", "refId", Py.ParseInt(rf));
            if (o is not null)
                e.Update(o);
            return e;
        }
        if (t == "LRef")
        {
            var ins = idents.Get(rf) ?? new PyDict();
            var e = PyDict.Of("uid", uid, "kind", "instruction", "name", ins.Get("name"), "refId", Py.ParseInt(rf ?? throw new PyTypeError("int() argument must be a string, not 'NoneType'")),
                "en", Et.Get(el, "EN") == "true", "eno", Et.Get(el, "ENO") == "true");
            if (Et.Get(el, "DisableENO") == "true")
                e["disableEno"] = true;
            var tv = (ins.Get("templateUses") as PyDict ?? new PyDict()).Get(Py.T(refid, uid));
            if (Py.Bool(tv))
                e["templateValues"] = tv;
            foreach (var x in Et.Kids(el))
            {
                if (Et.Local(x) == "Instance")
                    e["instance"] = idents.Operand(idents.Get(Et.Get(x, "RefId")), refid, Py.ParseInt(Et.Get(x, "UId")!));
                else
                    unhandled.Add("LRef/" + Et.Local(x));
            }
            return e;
        }
        if (t == "CRef")
        {
            var bii = idents.Get(rf) ?? new PyDict();
            var e = PyDict.Of("uid", uid, "kind", "call", "refId", Py.ParseInt(rf ?? throw new PyTypeError("int() argument must be a string, not 'NoneType'")),
                "parameters", bii.Get("parameters", new List<object?>()));
            foreach (var x in Et.Kids(el))
            {
                var xt = Et.Local(x);
                if (xt == "CodeBlock")
                {
                    var blk = idents.Get(Et.Get(x, "RefId")) ?? new PyDict();
                    e["name"] = blk.Get("name");
                    e["blockType"] = blk.Get("blockType");
                }
                else if (xt == "Instance")
                    e["instance"] = idents.Operand(idents.Get(Et.Get(x, "RefId")), refid, Py.ParseInt(Et.Get(x, "UId")!));
                else
                    unhandled.Add("CRef/" + xt);
            }
            return e;
        }
        unhandled.Add(t);
        return PyDict.Of("uid", uid, "kind", t, "attributes", Et.Attrib(el));
    }
}
