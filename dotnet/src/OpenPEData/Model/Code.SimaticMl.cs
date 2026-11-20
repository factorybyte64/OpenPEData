#pragma warning disable CS8600, CS8602
using System.Xml.Linq;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>LAD/FBD networks as SimaticML (Openness export, FlgNet v5): the internal &lt;FlgNet&gt; of the plf with its
/// operands resolved from the ident table. Element names follow the mapping in docs/spec/block-code.parser.md
/// (Part Gate → Part Name, ORef → Access, LRef → Part with instruction name, CRef → Call/CallInfo, OCon → IdentCon,
/// PCon → NameCon). UIds are numbered as the export does: label declarations, operands, parts (each followed by its
/// instance), wires, all from 21.</summary>
internal sealed partial class Code
{
    public static readonly XNamespace FlgNs = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";

    static XElement X(string name, params object?[] content) => new(FlgNs + name, content.Where(c => c is not null));

    /// <summary>SimaticML &lt;FlgNet&gt; per network of a block (null for networks that are not LAD/FBD).</summary>
    public List<XElement?> SimaticMlNetworks(string block)
    {
        var units = Linked(block, "Sources", "CompileUnitData").Select(k => (Ref: p.Attr(k, "ICompileUnitData", "RefID"), K: k)).ToList();
        var idents = new Idents(this, block);
        var outl = new List<XElement?>();
        var roots = units.Select(u => (u.Ref, u.K, Root: XmlOfBlob(p.Sets(u.K).Set("ICompileUnitData").Get("Data")))).ToList();
        if (roots.FirstOrDefault(r => r.Root is not null && Et.Local(r.Root) == "GraphStructure") is { Root: not null } gs)
            return [GraphSimaticMl(block, gs.K, gs.Root, roots, idents)];
        foreach (var (rf, k) in units)
        {
            var root = XmlOfBlob(p.Sets(k).Set("ICompileUnitData").Get("Data"));
            outl.Add(root is not null && Et.Local(root) == "FlgNet" ? FlgNetSimaticMl(root, rf, idents) : null);
        }
        return outl;
    }

    public static readonly XNamespace GraphNs = "http://www.siemens.com/automation/Openness/SW/NetworkSource/Graph/v6";

    static XElement G(string name, params object?[] content) => new(GraphNs + name, content.Where(c => c is not null));

    /// <summary>The element tree moved into the GRAPH namespace (an FlgNet inside a Graph has no namespace of its own).</summary>
    static XElement InGraphNs(XElement e) =>
        new(GraphNs + e.Name.LocalName, e.Attributes(), e.Nodes().Select(n => n is XElement x ? InGraphNs(x) : n));

    static string LangName(object? l) => l switch { "LAD_CLASSIC" => "LAD", "FBD_CLASSIC" => "FBD", _ => Py.Str(l) };

    /// <summary>A GRAPH block (sample 074) as the single &lt;Graph&gt; network of the export. The plf keeps the sequence in
    /// several compile units: GRAPH_ADDINFOS (alarm settings), GRAPH_SEQUENCE (&lt;GraphStructure&gt;: steps and transitions
    /// with UId 100000+n and 200000+n and RefId into the ident table, connections), and per step one GRAPH_ACTIONS unit plus
    /// a LAD/FBD unit each for supervision and interlock, per transition one LAD/FBD unit. Which unit belongs to which step
    /// is in the ident entry of the step (CS/C NID = unit RefID, AK = Actions, Supervision, Interlock or Transition). Step
    /// and transition names are the static members (G7_StepPlus_V6, G7_TransitionPlus_V6); the step times are their start
    /// values T_MAX and T_WARN.</summary>
    XElement GraphSimaticMl(string block, string seqKey, XElement gs, List<(object? Ref, string K, XElement? Root)> units, Idents idents)
    {
        var byRef = new Dictionary<long, (object? Ref, string K, XElement? Root)>();
        foreach (var u in units.Where(u => u.Ref is not null))
            byRef[Py.Long(u.Ref)] = u;
        var statics = new Dictionary<string, PyDict>();
        foreach (PyDict sec in m.InterfaceSections(block))
            if (Py.Eq(sec["section"], "Static"))
                foreach (PyDict mem in (List<object?>)sec["members"]!)
                    statics[(string?)mem["name"] ?? ""] = mem;
        string? Start(string member, string sub) => statics.GetValueOrDefault(member)?.Get("members") is List<object?> ms
            ? ms.Cast<PyDict>().FirstOrDefault(x => Py.Eq(x["name"], sub))?.Get("startValue") as string : null;
        Dictionary<string, long> Roles(long rid)
        {
            var r = new Dictionary<string, long>();
            var idel = First(idents.RawByRid.GetValueOrDefault(rid), "ID");
            foreach (var c in Et.Kids(First(idel, "CS")))
                if (Et.Get(c, "AK") is { } ak && ak != "Definition" && Py.IsDigit(Et.Get(c, "NID") ?? ""))
                    r[ak] = Py.ParseInt(Et.Get(c, "NID")!);
            return r;
        }
        XElement SubNet(string kind, Dictionary<string, long> roles, string role)
        {
            (object? Ref, string K, XElement? Root) u = roles.TryGetValue(role, out var ur) && byRef.TryGetValue(ur, out var x) ? x : default;
            var e = G(kind, new XAttribute("ProgrammingLanguage", LangName(u.K is null ? null : p.Attr(u.K, "ICompileUnitData", "ProgrammingLanguage"))));
            if (u.Root is not null && Et.Local(u.Root) == "FlgNet")
                e.Add(InGraphNs(FlgNetSimaticMl(u.Root, u.Ref, idents)));
            return e;
        }
        var seq = First(gs, "Sequence");
        var steps = G("Steps");
        foreach (var st in Et.Kids(First(seq, "Steps")))
        {
            var rid = Py.ParseInt(Et.Get(st, "RefId")!);
            var name = (string?)idents.Get(rid)?.Get("name") ?? "";
            var roles = Roles(rid);
            var step = G("Step", new XAttribute("Number", Py.ParseInt(Et.Get(st, "UId")!) - 100000),
                new XAttribute("Init", Et.Get(st, "Init") == "true" ? "true" : "false"), new XAttribute("Name", name));
            if (Start(name, "T_MAX") is { } tmax)
                step.Add(new XAttribute("MaximumStepTime", tmax));
            if (Start(name, "T_WARN") is { } twarn)
                step.Add(new XAttribute("WarningTime", twarn));
            var actions = G("Actions");
            var au = roles.TryGetValue("Actions", out var ar) && byRef.TryGetValue(ar, out var a) ? a.Root : null;
            // actions (samples 074/076): Operator Operation = qualifier, OpdAccess = operand, Token = text (line end); the
            // export writes the operand and the tokens as Token Text, an empty action as <Action />
            foreach (var act in Et.Kids(au).Where(x => Et.Local(x) == "Action"))
            {
                var ae = G("Action");
                if (Et.Get(act, "Event") is { Length: > 0 } ev)
                    ae.Add(new XAttribute("Event", ev));
                if (Et.Get(act, "Interlock") == "true")
                    ae.Add(new XAttribute("Interlock", "true"));
                foreach (var o in Et.Kids(act))
                {
                    switch (Et.Local(o))
                    {
                        case "Operator":
                            if (Et.Get(o, "Operation") is { Length: > 0 } q)
                                ae.Add(new XAttribute("Qualifier", q));
                            break;
                        case "OpdAccess":
                            ae.Add(G("Token", new XAttribute("Text", StlOperand(idents, Et.Get(o, "RefId")))));
                            break;
                        case "Token":
                            ae.Add(G("Token", new XAttribute("Text", Et.Get(o, "Text") ?? "")));
                            break;
                    }
                }
                actions.Add(ae);
            }
            step.Add(actions);
            step.Add(G("Supervisions", SubNet("Supervision", roles, "Supervision")));
            step.Add(G("Interlocks", SubNet("Interlock", roles, "Interlock")));
            steps.Add(step);
        }
        var trans = G("Transitions");
        foreach (var tr in Et.Kids(First(seq, "Transitions")))
        {
            var rid = Py.ParseInt(Et.Get(tr, "RefId")!);
            var net = SubNet("Transition", Roles(rid), "Transition");
            trans.Add(G("Transition", new XAttribute("IsMissing", "false"), new XAttribute("Name", (string?)idents.Get(rid)?.Get("name") ?? ""),
                new XAttribute("Number", Py.ParseInt(Et.Get(tr, "UId")!) - 200000), net.Attribute("ProgrammingLanguage"), net.Elements()));
        }
        XElement Node(XElement r) => Et.Local(r) switch
        {
            "StepRef" => G("StepRef", new XAttribute("Number", Et.Get(r, "No") ?? "")),
            "TransitionRef" => G("TransitionRef", new XAttribute("Number", Et.Get(r, "No") ?? "")),
            // branches (sample 077): No -> Number, In/Out (index of the branch leg) as they are
            "BranchRef" => G("BranchRef", new XAttribute("Number", Et.Get(r, "No") ?? ""),
                r.Attributes().Where(x => x.Name.LocalName is "In" or "Out").Select(x => new XAttribute(x.Name.LocalName, x.Value))),
            var t => G(t, r.Attributes().Where(x => x.Name.LocalName != "LinkType")),
        };
        var conns = G("Connections");
        foreach (var c in Et.Kids(First(seq, "Structure")).Where(x => Et.Local(x) == "Connection"))
        {
            var ends = Et.Kids(c).ToList();
            if (ends.Count >= 2)
                conns.Add(G("Connection", G("NodeFrom", Node(ends[0])), G("NodeTo", Node(ends[1])), G("LinkType", Et.Get(ends[1], "LinkType") ?? "Direct")));
        }
        var title = m.Comment(seqKey);
        var ct = m.Rel1(seqKey, "CompileUnitComment");
        var comment = ct is not null ? m.CoreTextOf(p.Attr(ct, "ICoreTextRepository", "Text")) : new PyDict();
        // an empty sequence comment is exported as one empty text in the first project language
        if (comment.Count == 0 && m.ProjectLanguages(p.OfType("ProjectData").FirstOrDefault())?.Get("active") is List<object?> { Count: > 0 } langs)
            comment[langs[0]] = null;
        XElement Texts(string tag, PyDict d, bool keepEmpty) => G(tag, d.Where(kv => keepEmpty || Py.Bool(kv.Value))
            .Select(kv => G("MultiLanguageText", new XAttribute("Lang", Py.Str(kv.Key)), Py.Bool(kv.Value) ? Py.Str(kv.Value) : null)));
        var addInfo = units.FirstOrDefault(u => u.Root is not null && Et.Local(u.Root) == "AddInfos").Root;
        var alarming = First(addInfo, "Alarming");
        var alarms = G("AlarmsSettings", G("AlarmSupervisionCategories", Et.Kids(First(alarming, "CategoryProperties")).Select(cat =>
            G("AlarmSupervisionCategory", new XAttribute("Id", Et.Get(cat, "Index") ?? ""), new XAttribute("DisplayClass", Et.Text(First(cat, "DisplayClass")) ?? "0")))));
        foreach (var (tag, src) in new[] { ("AlarmInterlockCategory", "InterlockCategory"), ("AlarmSubcategory1Interlock", "InterlockSubcategory1"),
            ("AlarmSubcategory2Interlock", "InterlockSubcategory2"), ("AlarmCategorySupervision", "SupervisionCategory"),
            ("AlarmSubcategory1Supervision", "SupervisionSubcategory1"), ("AlarmSubcategory2Supervision", "SupervisionSubcategory2"),
            ("AlarmWarningCategory", "WarningCategory"), ("AlarmSubcategory1Warning", "WarningSubcategory1"), ("AlarmSubcategory2Warning", "WarningSubcategory2") })
            alarms.Add(G(tag, new XAttribute("Id", Et.Get(First(alarming, src), "Index") ?? "0")));
        // permanent operations (sample 077): LAD/FBD units that belong to no step or transition; before the GRAPH_SEQUENCE
        // unit = pre-operations, after it = post-operations
        var owned = new HashSet<long>();
        foreach (var e in idents.RawByRid.Values)
            foreach (var c in Et.Kids(First(First(e, "ID"), "CS")))
                if (Et.Get(c, "AK") is "Actions" or "Supervision" or "Interlock" or "Transition" && Py.IsDigit(Et.Get(c, "NID") ?? ""))
                    owned.Add(Py.ParseInt(Et.Get(c, "NID")!));
        var seqIdx = units.FindIndex(u => u.K == seqKey);
        XElement Perm(string tag, bool before) => G(tag, units.Select((u, i) => (u, i))
            .Where(x => x.u.Root is not null && Et.Local(x.u.Root) == "FlgNet" && (x.u.Ref is null || !owned.Contains(Py.Long(x.u.Ref))) && (x.i < seqIdx) == before)
            .Select(x => G("PermanentOperation", new XAttribute("ProgrammingLanguage", LangName(p.Attr(x.u.K, "ICompileUnitData", "ProgrammingLanguage"))),
                InGraphNs(FlgNetSimaticMl(x.u.Root!, x.u.Ref, idents)))));
        return G("Graph", Perm("PreOperations", true),
            G("Sequence", Texts("Title", title, false), Texts("Comment", comment, true), steps, trans,
                G("Branches", Et.Kids(First(seq, "Branches")).Where(b => Et.Local(b) == "Branch").Select(b => G("Branch",
                    new XAttribute("Number", Et.Get(b, "No") ?? ""), new XAttribute("Type", Et.Get(b, "Type") ?? ""),
                    new XAttribute("Cardinality", Et.Get(b, "Cardinality") ?? "")))), conns),
            Perm("PostOperations", false), alarms);
    }

    XElement FlgNetSimaticMl(XElement root, object? refid, Idents idents)
    {
        var next = 21L;
        var uidMap = new Dictionary<long, long>();
        var openOrefs = new HashSet<long>();
        XElement? labels = null;
        var accesses = new List<XElement>();
        var parts = new List<XElement>();
        var wires = new List<XElement>();
        long Uid(XElement el) => Py.ParseInt(Et.Get(el, "UId")!);
        var sections = Et.Kids(root).ToList();
        foreach (var sec in sections.Where(s => Et.Local(s) == "Labels"))
        {
            labels = X("Labels");
            foreach (var lab in Et.Kids(sec))
            {
                uidMap[Uid(lab)] = next;
                var e = idents.Get(Et.Get(lab, "RefId"));
                labels.Add(X("LabelDeclaration", new XAttribute("UId", next++), X("Label", new XAttribute("Name", e?.Get("name") ?? ""))));
            }
        }
        var partsSec = sections.FirstOrDefault(s => Et.Local(s) == "Parts");
        var kids = partsSec is null ? [] : Et.Kids(partsSec).ToList();
        foreach (var el in kids.Where(x => Et.Local(x) == "ORef"))
        {
            if (Et.Get(el, "RefId") is null)
            {
                openOrefs.Add(Uid(el));
                continue;
            }
            uidMap[Uid(el)] = next;
            accesses.Add(Access(idents, idents.Get(Et.Get(el, "RefId")), next++));
        }
        foreach (var el in kids.Where(x => Et.Local(x) != "ORef"))
        {
            var t = Et.Local(el);
            uidMap[Uid(el)] = next;
            var uid = next++;
            if (t == "Part")
            {
                var gate = Et.Get(el, "Gate") ?? "";
                var pe = X("Part", new XAttribute("Name", ExportName.GetValueOrDefault(gate, gate)), new XAttribute("UId", uid));
                // a CALCULATE box keeps its ENO switch only in the export (FB_FbdCalc of sample 034)
                if ((Et.Get(el, "DisableENO") ?? (Et.Kids(el).Any(x => Et.Local(x) == "Equation") ? "false" : null)) is { } de)
                    pe.Add(new XAttribute("DisabledENO", de));
                foreach (var x in Et.Kids(el).Where(x => Et.Local(x) == "Equation"))
                    pe.Add(X("Equation", Py.Strip(Et.Text(x) ?? "")));
                pe.Add(TemplateValues(Et.Kids(el).Where(x => Et.Local(x) == "TemplateValue")
                    .Select(x => (Et.Get(x, "Name") ?? "", Et.Get(x, "Type") ?? "", Et.Text(x) ?? ""))));
                foreach (var x in Et.Kids(el))
                {
                    var xt = Et.Local(x);
                    if (xt is "TemplateValue" or "Equation")
                        continue;
                    else if (xt == "Negated")
                        pe.Add(X("Negated", new XAttribute("Name", Et.Get(x, "PinName") ?? "")));
                    else if (xt == "AutomaticTyped")
                        pe.Add(X("AutomaticTyped", new XAttribute("Name", Et.Get(x, "Name") ?? "")));
                }
                parts.Add(pe);
            }
            else if (t == "LRef")
            {
                var raw = idents.RawByRid.GetValueOrDefault(Py.ParseInt(Et.Get(el, "RefId")!));
                var ins = idents.Get(Et.Get(el, "RefId")) ?? new PyDict();
                var iname = (string?)ins.Get("name") ?? "";
                var pe = X("Part", new XAttribute("Name", ExportName.GetValueOrDefault(iname, iname)));
                if (InstructionVersion(raw) is { } ver)
                    pe.Add(new XAttribute("Version", ver));
                pe.Add(new XAttribute("UId", uid));
                // a few instructions print their ENO switch even when it is on (observed: MIN, MAX, LIMIT, SCATTER in
                // samples 031/032); the plf has no attribute that tells them apart from the others
                if ((Et.Get(el, "DisableENO") ?? (EnoShown.Contains(iname) ? "false" : null)) is { } de)
                    pe.Add(new XAttribute("DisabledENO", de));
                foreach (var x in Et.Kids(el).Where(x => Et.Local(x) == "Instance"))
                {
                    uidMap[Uid(x)] = next;
                    pe.Add(Instance(idents, idents.Get(Et.Get(x, "RefId")), next++));
                }
                var tv = (ins.Get("templateUses") as PyDict ?? new PyDict()).Get(Py.T(refid, Uid(el))) as PyDict;
                pe.Add(TemplateValues(TemplateParams(raw).Where(t => tv?.Get(t.Name) is not null).Select(t => (t.Name, t.Type, Py.Str(tv!.Get(t.Name))))));
                parts.Add(pe);
            }
            else if (t == "CRef")
            {
                var bii = idents.Get(Et.Get(el, "RefId")) ?? new PyDict();
                string? name = null, btype = null;
                XElement? inst = null;
                foreach (var x in Et.Kids(el))
                {
                    if (Et.Local(x) == "CodeBlock")
                    {
                        var blk = idents.Get(Et.Get(x, "RefId")) ?? new PyDict();
                        name = (string?)blk.Get("name");
                        btype = (string?)blk.Get("blockType");
                    }
                    else if (Et.Local(x) == "Instance")
                    {
                        uidMap[Uid(x)] = next;
                        inst = Instance(idents, idents.Get(Et.Get(x, "RefId")), next++);
                    }
                }
                var ci = X("CallInfo", new XAttribute("Name", name ?? ""), new XAttribute("BlockType", btype ?? ""), inst);
                var types = ParameterTypes(name);
                foreach (PyDict prm in (List<object?>)(bii.Get("parameters") ?? new List<object?>()))
                    ci.Add(X("Parameter", new XAttribute("Name", prm.Get("name") ?? ""), new XAttribute("Section", prm.Get("section") ?? ""),
                        new XAttribute("Type", types.GetValueOrDefault(((string?)prm.Get("section") ?? "") + "/" + (string?)prm.Get("name")) ?? "")));
                parts.Add(X("Call", new XAttribute("UId", uid), ci));
            }
        }
        // open connections (ORef without RefId) are numbered after the parts, before the wires
        foreach (var el in kids.Where(x => Et.Local(x) == "ORef" && openOrefs.Contains(Uid(x))))
            uidMap[Uid(el)] = next++;
        foreach (var w in sections.Where(s => Et.Local(s) == "Wires").SelectMany(Et.Kids))
        {
            var we = X("Wire", new XAttribute("UId", next++));
            foreach (var c in Et.Kids(w))
            {
                var ct = Et.Local(c);
                if (ct == "Powerrail")
                    we.Add(X("Powerrail"));
                else if (ct == "OCon")
                {
                    var u = Uid(c);
                    if (openOrefs.Contains(u))
                        we.Add(X("OpenCon", new XAttribute("UId", uidMap[u])));
                    else
                        we.Add(X("IdentCon", new XAttribute("UId", uidMap.GetValueOrDefault(u))));
                }
                else if (ct == "PCon")
                    we.Add(X("NameCon", new XAttribute("UId", uidMap.GetValueOrDefault(Uid(c))), new XAttribute("Name", Et.Get(c, "PinName") ?? "")));
            }
            wires.Add(we);
        }
        return X("FlgNet", labels, X("Parts", accesses, parts), X("Wires", wires));
    }

    /// <summary>Instructions whose SimaticML name differs from the name in the ident table (sample 031, FB_Lad_125).</summary>
    static readonly Dictionary<string, string> ExportName = new() { ["Scale"] = "Scale_X" };

    /// <summary>Instructions whose export always carries DisabledENO (false when ENO is on).</summary>
    static readonly HashSet<string> EnoShown = ["MIN", "MAX", "LIMIT", "SCATTER"];

    /// <summary>TemplateValue elements in export order: Cardinality, Operation, Type (stable within a kind); operations in
    /// upper case (the plf keeps the imported spelling, Eq).</summary>
    static List<XElement> TemplateValues(IEnumerable<(string Name, string Type, string Value)> tvs) =>
        tvs.Select((t, i) => (t, i)).OrderBy(x => x.t.Type switch { "Cardinality" => 0, "Operation" => 1, _ => 2 }).ThenBy(x => x.i)
            .Select(x => X("TemplateValue", new XAttribute("Name", x.t.Name), new XAttribute("Type", x.t.Type),
                x.t.Type == "Operation" ? x.t.Value.ToUpperInvariant() : x.t.Value)).ToList();

    /// <summary>Scope of an operand ident in SimaticML.</summary>
    static string AccessScope(PyDict e)
    {
        var kind = (string?)e.Get("kind");
        if (kind == "LiteralConstant")
            return ((string?)(e.Get("constant") as PyDict)?.Get("text") ?? "").Contains('#') ? "TypedConstant" : "LiteralConstant";
        return kind switch
        {
            "InterfaceAccess" or "MultInstAccess" => "LocalVariable",
            "GlobalAccess" or "AufDBBlock" or "DepDBBlock" => "GlobalVariable",
            "SimpleAccess" => Py.Bool(e.Get("name")) ? "GlobalVariable" : "Address",
            "LocalConstant" => "LocalConstant",
            "GlobalConstant" => "GlobalConstant",
            "Label" => "Label",
            _ => kind ?? "",
        };
    }

    XElement Access(Idents idents, PyDict? e, long? uid, int depth = 0)
    {
        var a = X("Access");
        if (e is null)
            return a;
        var scope = AccessScope(e);
        a.Add(new XAttribute("Scope", scope));
        if (uid is not null)
            a.Add(new XAttribute("UId", uid));
        switch (scope)
        {
            case "LiteralConstant":
            case "TypedConstant":
                {
                    var c = (PyDict)e["constant"]!;
                    a.Add(X("Constant", scope == "LiteralConstant" && Py.Bool(c.Get("type")) ? X("ConstantType", c.Get("type")) : null, X("ConstantValue", c.Get("text") ?? "")));
                    break;
                }
            case "LocalConstant":
            case "GlobalConstant":
                a.Add(X("Constant", new XAttribute("Name", e.Get("name") ?? "")));
                break;
            case "Label":
                a.Add(X("Label", new XAttribute("Name", e.Get("name") ?? "")));
                break;
            case "Address":
                a.Add(X("Address", new XAttribute("Area", e.Get("area") ?? ""), new XAttribute("Type", e.Get("type") ?? ""),
                    new XAttribute("BitOffset", e.Get("bitOffset") ?? 0L)));
                break;
            default:
                a.Add(X("Symbol", Components(idents, e, depth)));
                break;
        }
        return a;
    }

    List<XElement> Components(Idents idents, PyDict e, int depth)
    {
        var path = e.Get("path") as List<object?> is { Count: > 0 } pl ? pl : [e.Get("name")];
        var irefs = e.Get("indexRefs") as List<object?>;
        var outl = new List<XElement>();
        for (var i = 0; i < path.Count; i++)
        {
            var ce = X("Component", new XAttribute("Name", Py.Str(path[i])));
            var refs = irefs is not null && i < irefs.Count ? (List<object?>)irefs[i]! : [];
            if (refs.Count > 0 && depth < 6)
            {
                ce.Add(new XAttribute("AccessModifier", "Array"));
                foreach (var r in refs)
                    ce.Add(Access(idents, idents.Get(r), null, depth + 1));
            }
            if (i == path.Count - 1 && e.Get("slice") is string sl && sl.Length > 0)
                ce.Add(new XAttribute("SliceAccessModifier", sl));
            if (i == path.Count - 1 && Py.Eq(e.Get("kind"), "SimpleAccess") && e.Get("periphery") is true)
                ce.Add(new XAttribute("SimpleAccessModifier", "Periphery"));
            outl.Add(ce);
        }
        return outl;
    }

    XElement Instance(Idents idents, PyDict? e, long uid)
    {
        var ie = X("Instance", new XAttribute("Scope", e is null ? "" : Py.Eq(e.Get("kind"), "AufDBBlock") || Py.Eq(e.Get("kind"), "DepDBBlock") ? "GlobalVariable" : AccessScope(e)),
            new XAttribute("UId", uid));
        if (e is not null)
            ie.Add(Components(idents, e, 0));
        return ie;
    }

    /// <summary>Instruction version as SimaticML prints it (Part Version = Major.Minor of the IECPLInstructionData whose
    /// VersionId is INSTD@V of the ident entry); null when unknown.</summary>
    string? InstructionVersion(XElement? raw)
    {
        if (First(raw, "INSTD") is not { } d || Et.Get(d, "V") is not { } v)
            return null;
        if (instrVersions is null)
        {
            instrVersions = [];
            foreach (var k in p.OfType("IECPLInstructionData"))
                if (p.Attr(k, "ICoreTypeObjectAttributes", "VersionId") is string id)
                    instrVersions[id.ToLowerInvariant()] = $"{Py.Str(p.Attr(k, "ICoreLibTypeAttributes", "MajorVersion"))}.{Py.Str(p.Attr(k, "ICoreLibTypeAttributes", "MinorVersion"))}";
        }
        return instrVersions.GetValueOrDefault(v.ToLowerInvariant());
    }

    Dictionary<string, string>? instrVersions;

    /// <summary>(name, SimaticML type) of the template parameters of an instruction (INSTD/TPI/TP).</summary>
    static List<(string Name, string Type)> TemplateParams(XElement? raw)
    {
        var tpi = First(First(raw, "INSTD"), "TPI");
        return tpi is null ? [] : Et.Kids(tpi).Select(tp => (Et.Get(tp, "N") ?? "", Et.Get(tp, "T") switch
        {
            "type" => "Type",
            "card" or "cardinality" => "Cardinality",
            "operation" => "Operation",
            var x => x ?? "",
        })).ToList();
    }

    readonly Dictionary<string, Dictionary<string, string>> paramTypes = [];

    /// <summary>"section/name" → data type of the parameters of the called block name.</summary>
    Dictionary<string, string> ParameterTypes(string? name)
    {
        name ??= "";
        if (paramTypes.TryGetValue(name, out var r))
            return r;
        r = [];
        var key = p.OfType("CodeBlockData").FirstOrDefault(k => p.Name(k) == name);
        if (key is not null)
            foreach (PyDict sec in m.InterfaceSections(key))
                foreach (PyDict mem in (List<object?>)sec["members"]!)
                    r[(string?)sec["section"] + "/" + (string?)mem["name"]] = (string?)mem.Get("dataType") ?? "";
        paramTypes[name] = r;
        return r;
    }
}
