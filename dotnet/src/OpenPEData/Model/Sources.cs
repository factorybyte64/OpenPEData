// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Text;
using System.Xml.Linq;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>One generated source file of the source export (before it is written).</summary>
internal sealed record SourceItem(string Plc, string Folder, string Name, string Extension, string Kind, string? Language,
    string Text, IReadOnlyList<string> Notes, string? Skipped = null);

/// <summary>Block sources as TIA Portal "Generate source from blocks" writes them: SCL blocks as .scl, STL blocks as .awl,
/// data blocks as .db, PLC data types as .udt (CRLF, UTF-8 with BOM, the layout of the TIA export: header with TITLE,
/// attributes and VERSION, VAR sections indented by 3, members by 6, comments after "   // "). LAD, FBD and GRAPH have no
/// text source in TIA; they become a SimaticML block document (.xml) for "Import block" with the networks of
/// <see cref="Code.SimaticMlNetworks"/>. Checked against the TIA exports of samples 061, 062, 064, 073 and 075.</summary>
internal sealed class SourceExport
{
    const string Nl = "\r\n";
    public const string BlocksFolder = "Program blocks";
    public const string TypesFolder = "PLC data types";
    public const string TagsFolder = "PLC tags";

    static readonly Dictionary<string, string> SectionKeyword = new()
    {
        ["Input"] = "VAR_INPUT", ["Output"] = "VAR_OUTPUT", ["InOut"] = "VAR_IN_OUT", ["Static"] = "VAR", ["Temp"] = "VAR_TEMP",
        ["Constant"] = "VAR CONSTANT",
    };

    readonly ProjectModel m;
    readonly string? culture;

    SourceExport(ProjectModel model)
    {
        m = model;
        var proj = m.P.OfType("ProjectData").FirstOrDefault();
        culture = m.ProjectLanguages(proj)?.Get("editing") as string;
    }

    /// <summary>All sources of all PLCs of the project, in the order PLC, blocks (type, number, name), data types.</summary>
    public static List<SourceItem> Make(ProjectModel model, CancellationToken ct)
    {
        var x = new SourceExport(model);
        var outl = new List<SourceItem>();
        var keep = model.SourceDetails;
        model.SourceDetails = true;
        try
        {
            foreach (var plc in x.PlcKeys())
            {
                ct.ThrowIfCancellationRequested();
                var plcName = model.P.Name(plc) ?? plc;
                foreach (PyDict t in model.TagTables(plc))
                    outl.Add(x.TagTable(plcName, t));
                foreach (PyDict b in model.Blocks(plc))
                {
                    ct.ThrowIfCancellationRequested();
                    outl.Add(x.Block(plcName, b));
                }
                foreach (PyDict t in model.Types(plc))
                {
                    ct.ThrowIfCancellationRequested();
                    outl.Add(x.Type(plcName, t));
                }
            }
        }
        finally
        {
            model.SourceDetails = keep;
        }
        return outl;
    }

    IEnumerable<string> PlcKeys()
    {
        var p = m.P;
        var proj = p.OfType("ProjectData").FirstOrDefault();
        var devices = p.OfType("DeviceData").Where(d => proj is null || m.Rel1(d, "Environment") == proj)
            .OrderBy(d => m.FolderPath(d), StringComparer.Ordinal).ThenBy(d => p.Name(d) ?? "", StringComparer.Ordinal);
        foreach (var d in devices)
            foreach (var c in Py.Sorted(m.Sources(d, "LogicalContainer")))
                if (p.IsA(c, "ControllerTargetData"))
                    yield return c;
    }

    // --- blocks -----------------------------------------------------------------------------------
    SourceItem Block(string plc, PyDict b)
    {
        var notes = new List<string>();
        var name = Py.Str(b["name"]);
        var key = (string)b["key"]!;
        var type = Py.Str(b["type"]);
        var lang = b.Get("language") as string;
        string ext, text;
        if (b.Get("error") is { } err)
            notes.Add("block not fully readable: " + Py.Str(err));
        var group = Py.Str(b.Get("group") ?? "");
        // TIA generates no source for know-how protected blocks nor for the system blocks it adds itself (System
        // blocks/Program resources: R_TRIG, MOVE_BLK_VARIANT, ...)
        string? skip = IsSystem(group) ? "system block" : Py.Bool(b.Get("knowHowProtected")) ? "know-how protected" : null;
        if (skip is not null)
            return new SourceItem(plc, Path(BlocksFolder, b.Get("group")), name, "", type, lang, "", notes, skip);
        if (b.ContainsKey("members"))
        {
            ext = ".db";
            text = DataBlock(b, key, notes);
        }
        else if (lang is "SCL" or "STL")
        {
            ext = lang == "SCL" ? ".scl" : ".awl";
            text = CodeBlock(b, key, lang, notes);
        }
        else
        {
            ext = ".xml";
            text = SimaticMl(b, key, notes);
        }
        return new SourceItem(plc, Path(BlocksFolder, b.Get("group")), name, ext, type, lang, text, notes);
    }

    /// <summary>System folders that TIA fills itself (SystemBlocksFolder/ProgramResourcesFolder, SystemDataTypeFolder:
    /// IEC_TIMER, DTL, ...).</summary>
    static bool IsSystem(string group)
    {
        var first = group.Split('/')[0];
        return first.StartsWith("System", StringComparison.Ordinal) && first.EndsWith("Folder", StringComparison.Ordinal);
    }

    static string Path(string root, object? group) => Py.Bool(group) ? root + "/" + Py.Str(group) : root;

    SourceItem Type(string plc, PyDict t)
    {
        var notes = new List<string>();
        if (t.Get("error") is { } err)
            notes.Add("data type not fully readable: " + Py.Str(err));
        var key = (string)t["key"]!;
        if (IsSystem(Py.Str(t.Get("group") ?? "")))
            return new SourceItem(plc, Path(TypesFolder, t.Get("group")), Py.Str(t["name"]), "", "UDT", null, "", notes, "system data type");
        var sb = new StringBuilder();
        sb.Append("TYPE ").Append(Quote(Py.Str(t["name"]))).Append(Nl);
        Title(sb, key);
        Header(sb, key, false);
        sb.Append("   STRUCT").Append(Nl);
        Members(sb, (List<object?>)t["members"]!, 6, notes, inline: true);
        sb.Append("   END_STRUCT;").Append(Nl).Append(Nl);
        sb.Append("END_TYPE").Append(Nl).Append(Nl);
        return new SourceItem(plc, Path(TypesFolder, t.Get("group")), Py.Str(t["name"]), ".udt", "UDT", null, sb.ToString(), notes);
    }

    static string Quote(string name) => "\"" + name + "\"";

    string Text(PyDict t)
    {
        t = Neutral(t);
        if (t.Count == 0)
            return "";
        if (culture is not null && t.Get(culture) is string s && s.Length > 0)
            return s;
        return t.Values.OfType<string>().FirstOrDefault(x => x.Length > 0) ?? "";
    }

    /// <summary>Without the language-neutral entry (65535) when a project language is there.</summary>
    static PyDict Neutral(PyDict t)
    {
        if (!t.ContainsKey("65535") || t.Count == 1)
            return t;
        var r = new PyDict();
        foreach (var (c, x) in t)
            if (!Py.Eq(c, "65535"))
                r[c] = x;
        return r;
    }

    static string OneLine(string s) => s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

    void Title(StringBuilder sb, string key)
    {
        var t = OneLine(Text(m.Comment(key)));
        if (t.Length > 0)
            sb.Append("TITLE = ").Append(t).Append(Nl);
    }

    /// <summary>Attribute line (code blocks and data blocks), AUTHOR/FAMILY/NAME, VERSION and the block comment.</summary>
    void Header(StringBuilder sb, string key, bool access)
    {
        if (access)
        {
            var attrs = new List<string> { $"S7_Optimized_Access := '{(m.IsStandard(key) ? "FALSE" : "TRUE")}'" };
            if (PassRegisters(key))
                attrs.Add("S7_Pass_Registers := 'TRUE'");
            sb.Append("{ ").Append(string.Join(" ; ", attrs)).Append(" }").Append(Nl);
        }
        var h = m.P.Sets(key).SetOrNull("IPlcHeaderData");
        foreach (var (attr, word) in new[] { ("HeaderAuthor", "AUTHOR"), ("HeaderFamily", "FAMILY"), ("HeaderName", "NAME") })
            if (h?.Get(attr) is string v && v.Length > 0)
                sb.Append(word).Append(" : ").Append(v).Append(Nl);
        sb.Append("VERSION : ").Append(h?.Get("HeaderVersion") is string ver && ver.Length > 0 ? ver : "0.1").Append(Nl);
        var ck = m.Rel1(key, "BlockComment");
        if (ck is not null)
        {
            var c = Text(m.CoreTextOf(m.P.Attr(ck, "ICoreTextRepository", "Text")));
            foreach (var line in c.Replace("\r\n", "\n").Split('\n'))
                if (c.Length > 0)
                    sb.Append("//").Append(line).Append(Nl);
        }
    }

    /// <summary>"Parameter passing via registers" of an STL block (standard access): ImplicitRegisterPassing in the
    /// compiler settings document (sample 064, FC_StlNoPar).</summary>
    bool PassRegisters(string key)
    {
        if (m.P.Attr(key, "IExpandoInformationData", "CompilerSettings") is not byte[] blob)
            return false;
        XElement? doc;
        try
        {
            doc = Interface.XmlOf(blob, m.P.StoreData) ?? Interface.XmlOf(Format.Attrs.ReadVar(blob, 0).Value, m.P.StoreData);
        }
        catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
        {
            return false;
        }
        return doc is not null && Et.Iter(doc).Any(e => Et.Local(e) == "Attribut" && Et.Get(e, "Key") == "ImplicitRegisterPassing"
            && string.Equals(Et.Get(e, "Value"), "True", StringComparison.OrdinalIgnoreCase));
    }

    static List<PyDict> Sections(PyDict b) => b.Get("interface") is List<object?> l ? l.Cast<PyDict>().ToList() : [];

    string CodeBlock(PyDict b, string key, string lang, List<string> notes)
    {
        var sb = new StringBuilder();
        var type = Py.Str(b["type"]);
        var name = Quote(Py.Str(b["name"]));
        var secs = Sections(b);
        string end;
        switch (type)
        {
            case "FB":
                sb.Append("FUNCTION_BLOCK ").Append(name).Append(Nl);
                end = "END_FUNCTION_BLOCK";
                break;
            case "OB":
                sb.Append("ORGANIZATION_BLOCK ").Append(name).Append(Nl);
                end = "END_ORGANIZATION_BLOCK";
                break;
            default:
                var ret = secs.FirstOrDefault(s => Py.Eq(s["section"], "Return"))?["members"] is List<object?> { Count: > 0 } rm
                    ? Py.Str(((PyDict)rm[0])["dataType"]) : "Void";
                sb.Append("FUNCTION ").Append(name).Append(" : ").Append(ret).Append(Nl);
                end = "END_FUNCTION";
                break;
        }
        Title(sb, key);
        Header(sb, key, true);
        VarSections(sb, secs, !m.IsStandard(key), notes);
        sb.Append(Nl).Append("BEGIN").Append(Nl);
        var nets = b.Get("networks") is List<object?> nl ? nl.Cast<PyDict>().ToList() : [];
        if (lang == "SCL")
        {
            if (nets.Count > 1)
                notes.Add($"{nets.Count} SCL networks: written one after the other");
            foreach (var n in nets)
            {
                if (!Body(n, "scl", notes))
                    continue;
                sb.Append('\t').Append(Nl);
                foreach (var line in Py.Str(n["scl"]).Replace("\r\n", "\n").Split('\n'))
                    sb.Append('\t').Append(line).Append(Nl);
                sb.Append('\t').Append(Nl);
            }
        }
        else
        {
            foreach (var n in nets)
            {
                sb.Append("NETWORK").Append(Nl);
                sb.Append("TITLE = ").Append(OneLine(Text((PyDict)n["title"]!))).Append(Nl);
                var c = Text((PyDict)n["comment"]!);
                if (c.Length > 0)
                    foreach (var line in c.Replace("\r\n", "\n").Split('\n'))
                        sb.Append("//").Append(line).Append(Nl);
                if (!Body(n, "stl", notes))
                    continue;
                foreach (var line in Py.Str(n["stl"]).Replace("\r\n", "\n").Split('\n'))
                    sb.Append(line).Append(Nl);
            }
        }
        sb.Append(end).Append(Nl).Append(Nl);
        // absolute data block addresses (%DB6.DBX0.0) need the DB under the same number; a source has no block numbers
        var abs = System.Text.RegularExpressions.Regex.Matches(sb.ToString(), @"%DB(\d+)\.").Select(x => "DB" + x.Groups[1].Value).Distinct().ToList();
        if (abs.Count > 0)
            notes.Add($"absolute access to {string.Join(", ", abs)}: give the data block that number before this block is generated from the source (or generate this block again afterwards)");
        return sb.ToString();
    }

    /// <summary>True if the network has the text body `field`; otherwise a note says why not.</summary>
    static bool Body(PyDict n, string field, List<string> notes)
    {
        if (n.Get(field) is string)
        {
            if (n.Get("unknownTokens") is List<object?> u && u.Count > 0)
                notes.Add($"network {Py.Str(n["index"])}: {u.Count} unknown token(s) in the text");
            return true;
        }
        var why = Py.Bool(n.Get("encrypted")) ? "encrypted (know-how protection)"
            : Py.Bool(n.Get("empty")) ? null
            : n.Get("error") is { } e ? Py.Str(e)
            : $"language {Py.Str(n.Get("language"))} has no text source";
        if (why is not null)
            notes.Add($"network {Py.Str(n["index"])}: {why}, left out");
        return false;
    }

    void VarSections(StringBuilder sb, List<PyDict> secs, bool optimized, List<string> notes)
    {
        foreach (var s in secs)
        {
            var sec = Py.Str(s["section"]);
            if (!SectionKeyword.TryGetValue(sec, out var kw))
                continue;
            var members = ((List<object?>)s["members"]!).Cast<PyDict>().ToList();
            // optimized blocks: retain members of Input, Output, InOut and Static in a section of their own
            var runs = new List<(bool Retain, List<object?> Members)>();
            foreach (var mem in members)
            {
                var r = optimized && sec is "Input" or "Output" or "InOut" or "Static" && Py.Bool(mem.Get("retain"));
                if (runs.Count == 0 || runs[^1].Retain != r)
                    runs.Add((r, []));
                runs[^1].Members.Add(mem);
            }
            foreach (var (retain, ms) in runs)
            {
                sb.Append("   ").Append(kw).Append(retain ? " RETAIN" : " ").Append(Nl);
                Members(sb, ms, 6, notes, inline: true);
                sb.Append("   END_VAR").Append(Nl).Append(Nl);
            }
        }
    }

    static bool IsStruct(string dt) => dt == "Struct" || dt.EndsWith(" of Struct", StringComparison.Ordinal);

    /// <summary>Element type of an array type ("Array[0..2] of TON_TIME" -> "TON_TIME"), else the type.</summary>
    static string ElementType(string dt)
    {
        while (dt.StartsWith("Array[", StringComparison.Ordinal) && dt.IndexOf("] of ", StringComparison.Ordinal) is var i and > 0)
            dt = dt[(i + 5)..];
        return dt;
    }

    /// <summary>Words a local name cannot be written as without quotes (TIA: Syntax error "counter" is invalid).</summary>
    static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "XOR", "NOT", "MOD", "IF", "THEN", "ELSE", "ELSIF", "END_IF", "CASE", "OF", "END_CASE", "FOR", "TO", "BY",
        "DO", "END_FOR", "WHILE", "END_WHILE", "REPEAT", "UNTIL", "END_REPEAT", "EXIT", "CONTINUE", "RETURN", "GOTO",
        "REGION", "END_REGION", "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_TEMP", "VAR_STAT", "END_VAR", "CONSTANT",
        "RETAIN", "NON_RETAIN", "BEGIN", "FUNCTION", "END_FUNCTION", "FUNCTION_BLOCK", "END_FUNCTION_BLOCK",
        "ORGANIZATION_BLOCK", "END_ORGANIZATION_BLOCK", "DATA_BLOCK", "END_DATA_BLOCK", "TYPE", "END_TYPE", "STRUCT",
        "END_STRUCT", "ARRAY", "TRUE", "FALSE", "NULL", "REF_TO", "REF", "AT", "BOOL", "BYTE", "WORD", "DWORD", "LWORD",
        "SINT", "INT", "DINT", "LINT", "USINT", "UINT", "UDINT", "ULINT", "REAL", "LREAL", "TIME", "LTIME", "S5TIME", "DATE",
        "TOD", "LTOD", "TIME_OF_DAY", "LTIME_OF_DAY", "DT", "LDT", "DTL", "DATE_AND_TIME", "CHAR", "WCHAR", "STRING",
        "WSTRING", "VARIANT", "VOID", "COUNTER", "TIMER", "POINTER", "ANY", "BLOCK_DB", "BLOCK_FB", "BLOCK_FC", "BLOCK_SDB",
        "BLOCK_SFB", "BLOCK_SFC", "DB_ANY", "DB_WWW", "DB_DYN", "EVENT_ANY", "EVENT_ATT", "EVENT_HWINT", "AOM_IDENT",
        "HW_ANY", "HW_IO", "HW_DEVICE", "HW_SUBMODULE", "OB_ANY", "OB_ATT", "OB_CYCLIC", "OB_DELAY", "OB_TOD", "OB_PCYCLE",
        "OB_HWINT", "OB_DIAG", "OB_TIMEERROR", "OB_STARTUP", "PORT", "RTM", "PIP", "CONN_ANY", "CONN_PRG", "CONN_OUC",
        "CONN_R_ID", "REMOTE", "ERRORSTRUCT", "IEC_TIMER", "IEC_COUNTER",
    };

    /// <summary>A local name as the source writes it: in quotes when it is a reserved word or no plain identifier.</summary>
    static string Ident(string name)
    {
        var plain = name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
        return plain && !Reserved.Contains(name) ? name : "\"" + name.Replace("\"", "$\"") + "\"";
    }

    /// <summary>Attributes in braces after the member name: system instruction instance, HMI/OPC UA access, setpoint.</summary>
    static string Attributes(PyDict mem)
    {
        var a = new List<string>();
        if (mem.Get("_lib") is string lib)
        {
            a.Add($"InstructionName := '{ElementType(Py.Str(mem["dataType"]))}'");
            a.Add($"LibVersion := '{lib}'");
        }
        var f = mem.Get("_mflags") is { } mf ? Py.Long(mf) : 15L;
        var acc = (f & 1) != 0;
        if (!acc)
            a.Add("ExternalAccessible := 'False'");
        if (!acc || (f & 2) == 0)
            a.Add("ExternalVisible := 'False'");
        if (!acc || (f & 64) != 0)
            a.Add("ExternalWritable := 'False'");
        if ((f & 512) != 0)
            a.Add("S7_SetPoint := 'True'");
        if (a.Count == 0)
            return "";
        return mem.ContainsKey("_lib") ? " {" + string.Join("; ", a) + "}" : " { " + string.Join("; ", a) + "}";
    }

    /// <summary>Declaration lines. inline: start values of elementary members go after ":=" (code blocks, data types and
    /// global DBs); array element start values cannot be written there and are reported.</summary>
    void Members(StringBuilder sb, List<object?> members, int indent, List<string> notes, bool inline, string path = "")
    {
        var pad = new string(' ', indent);
        foreach (PyDict mem in members)
        {
            var name = Py.Str(mem["name"]);
            var dt = Py.Str(mem["dataType"]);
            var comment = OneLine(Text(mem.Get("comment") as PyDict ?? new PyDict()));
            var cmt = comment.Length > 0 ? "   // " + comment : "";
            sb.Append(pad).Append(Ident(name)).Append(Attributes(mem)).Append(" : ");
            if (IsStruct(dt) && mem.Get("members") is List<object?> kids)
            {
                sb.Append(dt).Append(cmt).Append(Nl);
                Members(sb, kids, indent + 3, notes, inline, path + name + ".");
                sb.Append(pad).Append("END_STRUCT;").Append(Nl);
                continue;
            }
            sb.Append(dt);
            if (inline && mem.Get("startValue") is { } sv)
                sb.Append(" := ").Append(Py.Str(sv));
            sb.Append(';').Append(cmt).Append(Nl);
            if (inline && mem.Get("elementStartValues") is PyDict ev && ev.Count > 0)
                notes.Add($"{path}{name}: start values of array elements are not part of the declaration");
        }
    }

    // --- data blocks ------------------------------------------------------------------------------
    string DataBlock(PyDict b, string key, List<string> notes)
    {
        var sb = new StringBuilder();
        var type = Py.Str(b["type"]);
        sb.Append("DATA_BLOCK ").Append(Quote(Py.Str(b["name"]))).Append(Nl);
        Title(sb, key);
        Header(sb, key, true);
        var optimized = !m.IsStandard(key);
        var nonRetain = Py.Eq(m.P.Attr(key, "IExpandoInformationData", "NonRetain"), true);
        var members = ((List<object?>)b["members"]!).Cast<PyDict>().ToList();
        var anyRetain = optimized && members.Any(x => Py.Bool(x.Get("retain")));
        var assign = new List<string>();
        if (type is "InstanceDB" or "TypedDB" or "TechnologyDB")
        {
            sb.Append(optimized ? (anyRetain ? "" : "NON_RETAIN" + Nl) : nonRetain ? "NON_RETAIN" + Nl : "");
            var of = Py.Str(b.Get("instanceOf"));
            var system = type == "TechnologyDB" || !m.P.OfType("CodeBlockData").Concat(m.P.OfType("DataTypeData"))
                .Any(k => m.P.Name(k) == of);
            sb.Append(system ? of : Quote(of)).Append(Nl);
            var vals = type == "InstanceDB" ? Sections(b).Where(s => Py.Eq(s["section"], "Input") || Py.Eq(s["section"], "Output")
                || Py.Eq(s["section"], "InOut") || Py.Eq(s["section"], "Static")).SelectMany(s => ((List<object?>)s["members"]!).Cast<PyDict>())
                : type == "TypedDB" ? members : [];
            foreach (var mem in vals)
                Assignments(assign, mem, "", own: true);
        }
        else
        {
            if (!optimized)
                sb.Append(nonRetain ? "NON_RETAIN" + Nl : "");
            else if (!anyRetain)
                sb.Append("NON_RETAIN" + Nl);
            if (type == "ArrayDB" && members.Count == 1)
            {
                sb.Append(Py.Str(members[0]["dataType"])).Append(Nl).Append(Nl);
                if (members[0].Get("elementStartValues") is PyDict ev)
                    foreach (var (ix, v) in ev)
                        assign.Add($"THIS[{Py.Str(ix)}] := {Py.Str(v)};");
            }
            else if (anyRetain)
            {
                var runs = new List<(bool Retain, List<object?> Members)>();
                foreach (var mem in members)
                {
                    var r = Py.Bool(mem.Get("retain"));
                    if (runs.Count == 0 || runs[^1].Retain != r)
                        runs.Add((r, []));
                    runs[^1].Members.Add(mem);
                }
                foreach (var (retain, ms) in runs)
                {
                    sb.Append("   VAR").Append(retain ? " RETAIN" : " ").Append(Nl);
                    Members(sb, ms, 6, [], inline: true);
                    sb.Append("   END_VAR").Append(Nl);
                }
                sb.Append(Nl);
            }
            else
            {
                sb.Append("   STRUCT ").Append(Nl);
                Members(sb, members.Cast<object?>().ToList(), 6, [], inline: true);
                sb.Append("   END_STRUCT;").Append(Nl).Append(Nl);
            }
            foreach (var mem in members)
                Assignments(assign, mem, "", own: false);
        }
        sb.Append(Nl).Append("BEGIN").Append(Nl);
        foreach (var a in assign)
            sb.Append("   ").Append(a).Append(Nl);
        sb.Append(Nl).Append("END_DATA_BLOCK").Append(Nl).Append(Nl);
        return sb.ToString();
    }

    /// <summary>Value assignments of the BEGIN part. own (instance and typed DBs): every start value of the DB that is not
    /// the default of the FB or type. Otherwise (global DB): what the declaration cannot hold, i.e. array element values and
    /// values below members of a named type (UDT, FB, system type).</summary>
    static void Assignments(List<string> outl, PyDict mem, string prefix, bool own, bool inNamed = false)
    {
        var name = prefix + Ident(Py.Str(mem["name"]));
        var dt = Py.Str(mem["dataType"]);
        var kids = mem.Get("members") as List<object?>;
        if (own || inNamed)
        {
            if (mem.Get("startValue") is { } sv && kids is null && !Py.Eq(sv, mem.Get("defaultValue")))
                outl.Add($"{name} := {Py.Str(sv)};");
        }
        if (mem.Get("elementStartValues") is PyDict ev)
        {
            var def = mem.Get("defaultElementStartValues") as PyDict;
            foreach (var (ix, v) in ev)
                if (!own || def is null || !Py.Eq(def.Get(ix), v))
                    outl.Add($"{name}[{Py.Str(ix)}] := {Py.Str(v)};");
        }
        if (kids is null)
            return;
        var named = !IsStruct(dt) && !dt.StartsWith("Array[", StringComparison.Ordinal);
        foreach (PyDict k in kids)
            Assignments(outl, k, name + ".", own, inNamed || named);
    }

    // --- PLC tag tables as SimaticML ---------------------------------------------------------------
    /// <summary>A PLC tag table as the Openness export writes it (SW.Tags.PlcTagTable, checked against the truth of samples
    /// 003-014); the blocks refer to the tags, so the tables are imported first.</summary>
    SourceItem TagTable(string plc, PyDict t)
    {
        var id = 0;
        string Id() => (id++).ToString("X");
        var table = new XElement("SW.Tags.PlcTagTable", new XAttribute("ID", Id()), new XElement("AttributeList", new XElement("Name", Py.Str(t["name"]))));
        var objs = new XElement("ObjectList");
        foreach (PyDict tag in (List<object?>)t["tags"]!)
        {
            var e = new XElement("SW.Tags.PlcTag", new XAttribute("ID", Id()), new XAttribute("CompositionName", "Tags"),
                new XElement("AttributeList",
                    new XElement("DataTypeName", Py.Str(tag["dataType"])),
                    new XElement("ExternalAccessible", tag.Get("hmiAccessible") is false ? "false" : "true"),
                    new XElement("ExternalVisible", tag.Get("hmiVisible") is false ? "false" : "true"),
                    new XElement("ExternalWritable", tag.Get("hmiWritable") is false ? "false" : "true"),
                    new XElement("LogicalAddress", Py.Str(tag["address"])),
                    new XElement("Name", Py.Str(tag["name"]))));
            var c = Neutral(tag.Get("comment") as PyDict ?? new PyDict());
            if (c.Count == 0 || c.ContainsKey("65535"))
                c = PyDict.Of(culture ?? "en-US", c.Values.FirstOrDefault() ?? "");
            {
                var mt = new XElement("MultilingualText", new XAttribute("ID", Id()), new XAttribute("CompositionName", "Comment"));
                mt.Add(new XElement("ObjectList", c.Select(kv => new XElement("MultilingualTextItem", new XAttribute("ID", Id()),
                    new XAttribute("CompositionName", "Items"), new XElement("AttributeList", new XElement("Culture", Py.Str(kv.Key)),
                        new XElement("Text", Py.Str(kv.Value)))))));
                e.Add(new XElement("ObjectList", mt));
            }
            objs.Add(e);
        }
        // user constants (SimaticConstantTagData with IsConstant false; the system constants have true): not part of the
        // structure output, but blocks refer to them (FC_SclMore of sample 030)
        if (t.Get("key") is string tk)
            foreach (var k in m.Sources(tk, "TagTable").Where(k => m.P.IsA(k, "SimaticConstantTagData")
                         && Py.Eq(m.P.Attr(k, "ICoreAttributes", "IsConstant"), false)).OrderBy(k => m.P.Name(k) ?? "", StringComparer.Ordinal))
            {
                var e = new XElement("SW.Tags.PlcUserConstant", new XAttribute("ID", Id()), new XAttribute("CompositionName", "UserConstants"),
                    new XElement("AttributeList",
                        new XElement("DataTypeName", Py.Str(m.P.Attr(k, "IStructureItem", "DisplayTypeName"))),
                        new XElement("Name", m.P.Name(k) ?? ""),
                        new XElement("Value", Py.Str(m.P.Attr(k, "IDefaultStrategyData", "DefaultValue")))));
                var c = Neutral(m.Comment(k));
                if (c.Count == 0 || c.ContainsKey("65535"))
                    c = PyDict.Of(culture ?? "en-US", c.Values.FirstOrDefault() ?? "");
                var mt = new XElement("MultilingualText", new XAttribute("ID", Id()), new XAttribute("CompositionName", "Comment"));
                mt.Add(new XElement("ObjectList", c.Select(kv => new XElement("MultilingualTextItem", new XAttribute("ID", Id()),
                    new XAttribute("CompositionName", "Items"), new XElement("AttributeList", new XElement("Culture", Py.Str(kv.Key)),
                        new XElement("Text", Py.Str(kv.Value)))))));
                e.Add(new XElement("ObjectList", mt));
                objs.Add(e);
            }
        if (objs.HasElements)
            table.Add(objs);
        return new SourceItem(plc, Path(TagsFolder, t.Get("group")), Py.Str(t["name"]), ".xml", "TagTable", null, XmlDoc(table), []);
    }

    static string XmlDoc(XElement body)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("Document", new XElement("Engineering", new XAttribute("version", "V21")), body));
        var sw = new Utf8Writer();
        doc.Save(sw);
        return sw.ToString().Replace("\r\n", "\n").Replace("\n", Nl) + Nl;
    }

    // --- LAD / FBD / GRAPH as SimaticML -----------------------------------------------------------
    static readonly XNamespace IfNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    string SimaticMl(PyDict b, string key, List<string> notes)
    {
        var type = Py.Str(b["type"]);
        var lang = Py.Str(b.Get("language"));
        var h = m.P.Sets(key).SetOrNull("IPlcHeaderData");
        var nets = b.Get("networks") is List<object?> nl ? nl.Cast<PyDict>().ToList() : [];
        List<XElement?> flg;
        try
        {
            flg = new Code(m).SimaticMlNetworks(key);
        }
        catch (Exception e) when (e is not (OperationCanceledException or OutOfMemoryException))
        {
            notes.Add("networks cannot be made: " + ProjectModel.ErrorText(e));
            flg = [];
        }
        var id = 0;
        string Id() => (id++).ToString("X");
        XElement Mlt(string comp, PyDict? texts)
        {
            texts = texts is null ? null : Neutral(texts);
            var items = (texts is { Count: > 0 } && !texts.ContainsKey("65535") ? texts : PyDict.Of(culture ?? "en-US", texts?.Values.FirstOrDefault() ?? "")).Select(kv =>
                new XElement("MultilingualTextItem", new XAttribute("ID", Id()), new XAttribute("CompositionName", "Items"),
                    new XElement("AttributeList", new XElement("Culture", Py.Str(kv.Key)), new XElement("Text", Py.Str(kv.Value)))));
            var e = new XElement("MultilingualText", new XAttribute("ID", Id()), new XAttribute("CompositionName", comp));
            e.Add(new XElement("ObjectList", items));
            return e;
        }
        var sections = new XElement(IfNs + "Sections");
        var secs = Sections(b);
        var order = type switch
        {
            "FB" => new[] { "Input", "Output", "InOut", "Static", "Temp", "Constant" },
            "FC" => ["Input", "Output", "InOut", "Temp", "Constant", "Return"],
            _ => ["Input", "Temp", "Constant"],
        };
        foreach (var s in order)
        {
            var sec = new XElement(IfNs + "Section", new XAttribute("Name", s));
            if (secs.FirstOrDefault(x => Py.Eq(x["section"], s))?["members"] is List<object?> ms)
                foreach (PyDict mem in ms)
                    sec.Add(XmlMember(mem, s is not ("Temp" or "Constant"), s == "Static", type == "OB" && s == "Input",
                        type == "FB" && m.IsStandard(key) && s is "Input" or "Output" or "InOut" or "Static"));
            else if (s == "Return")
                sec.Add(new XElement(IfNs + "Member", new XAttribute("Name", "Ret_Val"), new XAttribute("Datatype", "Void")));
            sections.Add(sec);
        }
        var attrs = new XElement("AttributeList");
        attrs.Add(new XElement("AutoNumber", "false"));
        foreach (var a in new[] { "HeaderAuthor", "HeaderFamily", "HeaderName", "HeaderVersion" })
            attrs.Add(new XElement(a, h?.Get(a) as string ?? (a == "HeaderVersion" ? "0.1" : "")));
        attrs.Add(new XElement("Interface", sections));
        attrs.Add(new XElement("MemoryLayout", m.IsStandard(key) ? "Standard" : "Optimized"));
        attrs.Add(new XElement("Name", Py.Str(b["name"])));
        attrs.Add(new XElement("Namespace"));
        attrs.Add(new XElement("Number", Py.Str(b.Get("number"))));
        attrs.Add(new XElement("ProgrammingLanguage", lang.StartsWith("GRAPH", StringComparison.Ordinal) ? "GRAPH" : lang));
        // OB event class (ICoreAttributes.Subtype "OB.ProgramCycle" -> ProgramCycle); the import needs it
        if (type == "OB")
            attrs.Add(new XElement("SecondaryType", m.P.Attr(key, "ICoreAttributes", "Subtype") is string st && st.StartsWith("OB.", StringComparison.Ordinal)
                ? st[3..] : "ProgramCycle"));
        var block = new XElement("SW.Blocks." + type, new XAttribute("ID", Id()), attrs);
        var objs = new XElement("ObjectList");
        var ck = m.Rel1(key, "BlockComment");
        objs.Add(Mlt("Comment", ck is not null ? m.CoreTextOf(m.P.Attr(ck, "ICoreTextRepository", "Text")) : null));
        var graph = flg.Count == 1 && flg[0] is { } g0 && g0.Name.LocalName == "Graph";
        if (graph)
            notes.Add("GRAPH: the Static section holds the internal sequence members as stored (the TIA export shows RT_DATA instead); not checked by a reimport");
        for (var i = 0; i < (graph ? 1 : nets.Count); i++)
        {
            var n = graph ? null : nets[i];
            var x = i < flg.Count ? flg[i] : null;
            if (x is null && Py.Bool(n?.Get("empty")))
                x = new XElement("Dummy");
            if (x is null)
            {
                notes.Add($"network {Py.Str(n?["index"])}: language {Py.Str(n?.Get("language"))} is not exported as SimaticML, left out");
                continue;
            }
            if (x.Name.LocalName != "Dummy")
                x = ForImport(x);
            var cu = new XElement("SW.Blocks.CompileUnit", new XAttribute("ID", Id()), new XAttribute("CompositionName", "CompileUnits"),
                new XElement("AttributeList", new XElement("NetworkSource", x.Name.LocalName == "Dummy" ? null : x),
                    new XElement("ProgrammingLanguage", graph ? "GRAPH" : Py.Str(n!.Get("language")))));
            cu.Add(new XElement("ObjectList", Mlt("Comment", n?["comment"] as PyDict), Mlt("Title", n?["title"] as PyDict)));
            objs.Add(cu);
        }
        objs.Add(Mlt("Title", m.Comment(key)));
        block.Add(objs);
        return XmlDoc(block);
    }

    /// <summary>The network for "Import block": the export lists template values the import rejects ("Card" of Sub, Div and
    /// Mod, "SrcType" of Move; found by a reimport into TIA V21).</summary>
    static XElement ForImport(XElement net)
    {
        var x = new XElement(net);
        foreach (var tv in x.Descendants().Where(e => e.Name.LocalName == "TemplateValue").ToList())
        {
            var part = tv.Parent?.Attribute("Name")?.Value;
            var n = tv.Attribute("Name")?.Value;
            if ((n == "Card" && part is "Sub" or "Div" or "Mod") || (n == "SrcType" && part == "Move"))
                tv.Remove();
        }
        return x;
    }

    sealed class Utf8Writer : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }

    static XElement XmlMember(PyDict mem, bool attributes, bool setpoint, bool informative, bool setInIdb = false)
    {
        var e = new XElement(IfNs + "Member", new XAttribute("Name", Py.Str(mem["name"])), new XAttribute("Datatype", Py.Str(mem["dataType"])));
        if (informative)
            e.Add(new XAttribute("Informative", "true")); // OB start information (Initial_Call, Remanence, ...)
        if (mem.Get("_remanence") is string rem && rem != "NonRetain")
            e.Add(new XAttribute("Remanence", rem));
        else if (setInIdb && !(mem.ContainsKey("members") && !IsStruct(Py.Str(mem["dataType"])) && !Py.Str(mem["dataType"]).StartsWith('"')))
            e.Add(new XAttribute("Remanence", "SetInIDB")); // standard FB: retentivity is set in the instance DB
        else if (Py.Bool(mem.Get("retain")))
            e.Add(new XAttribute("Remanence", "Retain"));
        // the attributes as stored (MFlags), like the SimaticML export; Temp and Constant members have none
        var al = new XElement(IfNs + "AttributeList");
        void B(string n, bool v) => al.Add(new XElement(IfNs + "BooleanAttribute", new XAttribute("Name", n), new XAttribute("SystemDefined", "true"), v ? "true" : "false"));
        var f = mem.Get("_mflags") is { } mf ? Py.Long(mf) : 15L;
        if (attributes)
        {
            if ((f & 1) == 0)
                B("ExternalAccessible", false);
            if ((f & 1) == 0 || (f & 2) == 0)
                B("ExternalVisible", false);
            if ((f & 1) == 0 || (f & 64) != 0)
                B("ExternalWritable", false);
            if (setpoint && (f & 512) != 0)
                B("SetPoint", true);
        }
        if (al.HasElements)
            e.Add(al);
        if (mem.Get("comment") is PyDict { Count: > 0 } c)
            e.Add(new XElement(IfNs + "Comment", c.Select(kv => new XElement(IfNs + "MultiLanguageText", new XAttribute("Lang", Py.Str(kv.Key)), Py.Str(kv.Value)))));
        if (IsStruct(Py.Str(mem["dataType"])) && mem.Get("members") is List<object?> kids)
            foreach (PyDict k in kids)
                e.Add(XmlMember(k, attributes, setpoint, false));
        if (mem.Get("elementStartValues") is PyDict ev)
            foreach (var (ix, v) in ev)
                e.Add(new XElement(IfNs + "Subelement", new XAttribute("Path", Py.Str(ix)), new XElement(IfNs + "StartValue", Py.Str(v))));
        if (mem.Get("startValue") is { } sv)
            e.Add(new XElement(IfNs + "StartValue", Py.Str(sv)));
        return e;
    }
}
