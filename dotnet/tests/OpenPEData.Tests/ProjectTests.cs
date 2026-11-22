using OpenPEData.Format;
using OpenPEData.Model;
using System.Xml.Linq;
using OpenPEData.Runtime;

namespace OpenPEData.Tests;

/// <summary>Checks on the samples themselves (port of check_verify, check_verify_tamper and check_xref of
/// parser\tests\test_samples.py): integrity, idx consistency, XRef.db references and tag addresses.</summary>
public static class ProjectTests
{
    /// <summary>A flipped byte in a record body, in the chain hash and in the signature of a commit must be detected.</summary>
    [Test]
    public static void VerifyTamper()
    {
        var buf = ProjectFiles.Member(Path.Combine(Program.Root, "samples", "005_tag_int.zap21"), "System/PEData.plf")!;
        Assert.Eq(true, Verify.VerifyPlf(buf)["ok"], "untouched file ok");
        var recs = Plf.ReadRecords(buf);
        var plain = recs.First(r => r.TypeId is not (Verify.Commit or Verify.Close or Verify.Key) && r.Length > 60);
        var commit = recs.First(r => r.TypeId == Verify.Commit);
        foreach (var (what, off) in new[] { ("record body", plain.Offset + 40), ("chain hash", commit.Offset + 100), ("signature", commit.Offset + 200) })
        {
            var b2 = (byte[])buf.Clone();
            b2[off] ^= 0x01;
            Assert.Eq(false, Verify.VerifyPlf(b2)["ok"], what + " detected");
        }
        Assert.Eq(false, Verify.VerifyPlf([.. buf, 0, 1])["ok"], "trailing bytes detected");
        Assert.Eq(false, Verify.VerifyPlf(buf[..50])["ok"], "short file detected");
    }

    static IEnumerable<string> SampleInputs() => DifferentialTests.Inputs().Select(i => Path.Combine(Program.Root, i));

    /// <summary>--verify on every sample: header, record hashes, chain hash and RSA signature of every commit intact.</summary>
    [Test]
    public static void SamplesVerify()
    {
        var c = new Check("verify ok on every sample");
        Parallel.ForEach(SampleInputs(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, s =>
        {
            var rep = Verify.VerifyProject(s);
            c.Eq(true, rep["ok"], s + " verify ok");
            foreach (var f in ((List<object?>)rep["files"]!).Cast<PyDict>())
                c.Eq(0, ((List<object?>)f["problems"]!).Count, s + " " + f["file"] + " problems");
        });
        c.Done();
    }

    /// <summary>idx consistency and XRef.db references (check_xref of test_samples.py).</summary>
    [Test]
    public static void SamplesXrefAndIdx()
    {
        var c = new Check("idx / XRef.db consistency");
        Parallel.ForEach(SampleInputs(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, source =>
        {
            var ix = Pidx.CheckProject(source);
            var stale = ix is not null && Py.Bool(ix.Get("stale"));
            if (ix is not null && !stale)
            {
                c.Eq(0, ((List<object?>)ix["problems"]!).Count, source + " idx consistent with plf");
                var sg = (PyDict)ix["commitCopySignatures"]!;
                c.Eq(sg["total"], sg["padded"], source + " idx commit copies carry an RSA block of the project key");
            }
            var x = Format.Xref.Read(source);
            if (x is null)
                return;
            var p = new Project(source);
            var parts = (PyDict)x["parts"]!;
            var objs = (PyDict)x["objs"]!;
            var missing = parts.Values.Where(k => k is string ks && !p.Latest.ContainsKey(ks)).ToList();
            if (!source.EndsWith(".zap21", StringComparison.OrdinalIgnoreCase) && missing.Count * 2 < parts.Count)
                c.Eq(0, missing.Count, source + " xref parts resolve to plf records");
            c.Eq(0, objs.Values.Cast<PyDict>().Count(o => o["scope"] is null || o["primary"] is null), source + " xref objs use known parts");
            c.Eq(0, ((List<object?>)x["rels"]!).Cast<PyDict>().Count(r => !objs.ContainsKey(r["left"]) || !objs.ContainsKey(r["right"])), source + " xref rels use known objs");
            var st = Pidx.ExternalStores(source);
            if (st.Get("xref") is PyDict xs && Py.Bool(xs.Get("file")))
                c.Eq(xs["plf"], xs["file"], source + " xref savepoint equals plf expectation");
            var area = new Dictionary<long, string> { [1] = "I", [2] = "Q", [3] = "M" };
            foreach (var a in ((List<object?>)x["addrs"]!).Cast<PyDict>())
            {
                var k = ((PyDict)objs[a["obj"]]!)["primary"] as string;
                if (k is null || !p.Objects.ContainsKey(k))
                    continue;
                var fb = Py.Long(a["firstBit"]);
                var width = Py.Long(a["lastBit"]) - fb + 1;
                var range = Py.Long(a["range"]);
                // ranges 4/5 = S5 timer/counter (sample 064): firstBit = number * 8, one bit wide
                var text = range is 4 or 5 ? $"%{(range == 4 ? "T" : "C")}{fb / 8}"
                    : width == 1 ? $"%{area[Py.Long(a["range"])]}{fb / 8}.{fb % 8}"
                    : $"%{area[Py.Long(a["range"])]}{width switch { 8 => "B", 16 => "W", 32 => "D", _ => "?" }}{fb / 8}";
                c.Eq(text, p.Attr(k, "ITagAddress", "LogicalAddress"), source + " xref address of tag " + p.Name(k));
            }
        });
        c.Done();
    }

    /// <summary>Every archive has an intact record log and a complete metamodel (packages with names).</summary>
    [Test]
    public static void SamplesRecordLayout()
    {
        var c = new Check("record layout");
        Parallel.ForEach(SampleInputs().Where(s => s.EndsWith(".zap21", StringComparison.OrdinalIgnoreCase)), new ParallelOptions { MaxDegreeOfParallelism = 6 }, s =>
        {
            var p = new Project(s);
            c.True(p.Mm.Types.Count > 0 && p.Mm.Types.Values.All(t => p.Mm.ByName.ContainsKey(t.FullName)), s + " metamodel types");
            c.True(p.Mm.Packages.Values.All(n => !string.IsNullOrEmpty(n)), s + " package names");
            c.Eq(0, p.Records.Count(r => !r.HashOk), s + " record hashes");
        });
        c.Done();
    }

    /// <summary>Samples 059_db_symbols and 060_db_attrs (all elementary types, Struct/UDT nesting, multi-dimensional
    /// arrays, element start values below array elements, non-optimized DB, ARRAY DBs, DB of a UDT, instance DBs with
    /// multi-instances on two levels) and 066_sys_fb_instances (64 system FBs as multi-instances, single instance DBs
    /// of 31 system FBs (TSEND and TRCV in 067_tsend_trcv_single), the motion technology DBs of 068_motion_tos (speed,
    /// positioning and synchronous axis, external encoder on a PD_TEL81 data block), Main_Safety_RTG1_DB of the
    /// compiled safety program 069_safety_compiled, know-how protected DBs and a DB in a software unit
    /// (070_db_khp_units, truth exported before the protection), S7-1200 and S7-300 DBs (071_db_1200_300), technology DBs of PID_Compact, PID_3Step, PID_Temp, CONT_C, CONT_S, TCONT_CP, TCONT_S): every
    /// member of the SimaticML export of each DB and UDT appears with the same data
    /// type, start value and array element start values. The parser may show more (elements of DTL).</summary>
    [Test]
    public static void DbSymbolsAgainstSimaticML()
    {
        // (V21 samples; the samples of other TIA versions are checked in OlderVersionSamples)
        var c = new Check("059/060/066-071 members = SimaticML");
        foreach (var sample in new[] { "059_db_symbols", "060_db_attrs", "066_sys_fb_instances", "067_tsend_trcv_single", "068_motion_tos", "069_safety_compiled", "070_db_khp_units", "071_db_1200_300", "072_motion_tcpu" })
            DbSymbols(c, sample);
        c.Done();
    }

    /// <summary>Test projects of TIA Portal V19 and V20 (samples\v19_*, v20_*): each opens as a supported version
    /// without warnings, its integrity check passes, and where a truth folder holds Openness block exports, every block
    /// is parsed with the members, data types and start values of its SimaticML interface. Without such test projects
    /// in samples\ the test checks nothing.</summary>
    [Test]
    public static void OlderVersionSamples()
    {
        var c = new Check("V19/V20 samples: version, verify, members = SimaticML");
        var dir = Path.Combine(Program.Root, "samples");
        var archives = !Directory.Exists(dir) ? [] : Directory.GetFiles(dir, "v??_*.zap??").Concat(Directory.GetFiles(dir, "v??_*.zal??"))
            .Order(StringComparer.Ordinal).ToList();
        foreach (var a in archives)
        {
            var sample = Path.GetFileNameWithoutExtension(a);
            var want = new TiaVersion(int.Parse(sample[1..3], System.Globalization.CultureInfo.InvariantCulture));
            if (Program.Samples is { } f && !sample.Contains(f, StringComparison.Ordinal))
                continue;
            using (var p = TiaProject.Open(a))
            {
                c.Eq(want, p.Version, sample + " version");
                c.Eq(true, p.Version.IsSupported, sample + " supported");
                c.Eq(0, p.Warnings.Count, sample + " no warnings");
                c.Eq(true, p.IsIntact(), sample + " verify ok");
                // 005: watch table and software units; 006: alarm text, info text and text list written via Openness
                var json = p.ToJson();
                // 105: integrated HMI connection HMI_Connection_1 to PLC_1 and an HMI text list (TL_Hmi in V20, TLHmi in V19)
                if (sample.Contains("_105_"))
                    foreach (var s in new[] { "\"HMI_Connection_1\"", sample.StartsWith("v19", StringComparison.Ordinal) ? "\"TLHmi\"" : "\"TL_Hmi\"", "\"Stopped\"", "\"Running\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 203 (V19): library type UDT_Motor 0.0.1 created from the PLC data type via the TIA UI
                if (sample.Contains("_203_"))
                    foreach (var s in new[] { "PlcTypeLibraryType", "\"UDT_Motor\"", "\"0.0.1\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 104: faceplate type FPBasic 0.0.1 (released) and its instance Faceplate_1 on Screen_1
                if (sample.Contains("_104_"))
                    foreach (var s in new[] { "\"FPBasic\"", "\"Faceplate_1\"", "V0.0.1\\\\FPBasic" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 009: ProDiag operand supervision of "Tag_Bool_1" with its text (as 057)
                if (sample.Contains("_009_"))
                    foreach (var s in new[] { "\"supervisions\"", "\"Test supervision\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 103: selected style Dark style (style name with the version: FlatStyle_DarkV1902 / FlatStyle_DarkV2000)
                if (sample.Contains("_103_"))
                    c.True(json.Contains("\"FlatStyle_DarkV" + (sample.StartsWith("v19", StringComparison.Ordinal) ? "1902" : "2000") + "\"", StringComparison.Ordinal), sample + " dark style selected");
                // 301: ET 200SP IO device on subnet PN/IE_1 in the PROFINET IO system of PLC_1
                if (sample.Contains("_301_"))
                    foreach (var s in new[] { "\"IO_1\"", "\"PN/IE_1\"", "\"PROFINET IO-System\"", "\"192.168.0.2\"", "6ES7 155-6AU02-0BN0" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 202: global library (.zalNN) with the master copy UDT_Motor; in V20 also the type FC_Iface 1.0.0
                if (sample.Contains("_202_"))
                {
                    c.Eq(TiaProjectKind.GlobalLibrary, p.Info.Kind, sample + " kind");
                    foreach (var s in sample.StartsWith("v20", StringComparison.Ordinal)
                        ? new[] { "\"UDT_Motor\"", "\"FC_Iface\"", "\"1.0.0\"" }
                        : new[] { "\"UDT_Motor\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                }
                // 201: project library with master copies (FB, UDT, tag table); in V20 also a released type FC_Iface 1.0.0
                if (sample.Contains("_201_"))
                    foreach (var s in sample.StartsWith("v20", StringComparison.Ordinal)
                        ? new[] { "\"FB_Iface\"", "\"UDT_Motor\"", "\"TagTable_1\"", "LibraryMasterCopyData", "CodeBlockLibraryType", "\"1.0.0\"" }
                        : new[] { "\"FB_Iface\"", "\"UDT_Motor\"", "\"TagTable_1\"", "LibraryMasterCopyData" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 102: alarm control, gauge, trend control, data log with logging tag, global script module
                if (sample.Contains("_102_"))
                    foreach (var s in new[] { "\"AlarmControl_1\"", "\"Gauge_1\"", "\"Trend_1\"", "\"DataLog_1\"", "\"HmiTag_Int_Log\"", "\"Module_1\"", "return a + b" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 101: WinCC Unified panel with screen items, HMI tag bound to the I/O field, discrete alarm
                if (sample.Contains("_101_"))
                    foreach (var s in new[] { "\"Screen_1\"", "\"Button_1\"", "\"IOField_1\"", "\"Rect_1\"", "\"Text_1\"", "\"Bar_1\"", "\"HmiTag_Int\"", "\"Alarm_Disc_1\"", "\"Tank level high\"", "\"0xFF3366CC\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 008: force table entry %Q0.0 Bool TRUE (as 078)
                if (sample.Contains("_008_"))
                    foreach (var s in new[] { "\"%Q0.0:P\"", "\"Force table\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                // 007: technology object with DynamicDefaults.Velocity 750, OPC UA server interface, ProDiag FB
                if (sample.Contains("_007_"))
                    foreach (var s in new[] { "\"TO_Speed\"", "\"startValue\": \"750.0\"", "\"SI_Test\"", "\"ProDiag_FB\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                if (sample.Contains("_005_"))
                    foreach (var s in new[] { "\"WT_Test\"", "\"Unit_A\"", "\"Unit_B\"", "\"FB_Alarm_DB\"", "\"FB_Iface_DB\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
                if (sample.Contains("_006_"))
                    foreach (var s in new[] { "\"Pump overload\"", "\"Check the motor protection\"", "\"TL_Test\"", "\"Off\"", "\"On\"" })
                        c.True(json.Contains(s, StringComparison.Ordinal), sample + " contains " + s);
            }
            if (Directory.Exists(Path.Combine(dir, sample + "_truth")))
                DbSymbols(c, sample);
        }
        c.Done();
    }

    /// <summary>Samples 060_db_attrs and 066_sys_fb_instances: retain, external accessible/visible/writable and setpoint of every DB member (without
    /// array elements) equal the values Openness reports (*.members.json), also the setpoint
    /// Openness derives for members of a UDT, FB or system type (true in a DB or Static section, false for DTL and
    /// anonymous Structs). memberGet leaves out some members (e.g. multi-instances of PID_Compact), so
    /// only the members it reports are compared. Offsets of the not optimized DB_NonOpt follow the standard S7 layout.</summary>
    [Test]
    public static void DbMemberAttributesAgainstOpenness()
    {
        var c = new Check("060/066-068/070/071 member attributes = Openness memberGet");
        string[] keys = ["retain", "externalAccessible", "externalVisible", "externalWritable", "setpoint"];
        foreach (var sample in new[] { "060_db_attrs", "066_sys_fb_instances", "067_tsend_trcv_single", "068_motion_tos", "070_db_khp_units", "071_db_1200_300" })
        {
        var objs = ParsedBlocks(sample);
        foreach (var f in TruthFiles(sample, "*.members.json"))
        {
            var name = Path.GetFileName(f)[..^".members.json".Length];
            var b = objs[name];
            var got = new Dictionary<string, PyDict>();
            if (b.Get("interface") is List<object?> { Count: > 0 })
                foreach (var sec in ((List<object?>)b["interface"]!).Cast<PyDict>())
                    Flat((List<object?>)sec["members"]!, "", got);
            else
                Flat((List<object?>)b["members"]!, "", got);
            var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
            foreach (var e in doc.RootElement.GetProperty("members").EnumerateArray())
            {
                var path = e.GetProperty("path").GetString()!;
                if (path.Contains('['))
                    continue;
                if (!got.TryGetValue(path, out var g))
                {
                    c.Fail($"{name}.{path} missing");
                    continue;
                }
                foreach (var k in keys)
                    if (e.TryGetProperty(k, out var v) && v.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                        c.Eq(v.GetBoolean(), g.Get(k), $"{sample} {name}.{path} {k}");
            }
        }
        }
        var std = new Dictionary<string, PyDict>();
        var o060 = ParsedBlocks("060_db_attrs");
        Flat((List<object?>)o060["DB_NonOpt"]["members"]!, "", std);
        c.Eq(false, o060["DB_NonOpt"]["optimized"], "DB_NonOpt optimized");
        var offsets = "b0=0.0 b1=0.1 by=1.0 i=2.0 d=4.0 r=8.0 t=12.0 dt=16.0 s=24.0 arr=38.0 m=46.0 st=58.0 st.x=58.0 st.y=60.0 u=64.0 "
            + "u.Temp=64.0 u.Input=68.0 u.Static=70.0 u.Return=72.0 u.Arr=76.0";
        foreach (var kv in offsets.Split(' ').Select(x => x.Split('=')))
            c.Eq(kv[1], std[kv[0]].Get("offset"), $"DB_NonOpt.{kv[0]} offset");
        c.Done();
    }

    /// <summary>Samples 061_fc_stl and 062_fb_stl: every STL network (statements, operands, jump labels, line comments,
    /// empty lines), its title and comment equal the network bodies of the STL source export (Openness
    /// sourceExport, *.awl). No unknown STL element may remain.</summary>
    [Test]
    public static void StlAgainstSource()
    {
        var c = new Check("061/062/064/073/075 STL = .awl source export");
        foreach (var sample in new[] { "061_fc_stl", "062_fb_stl", "064_stl_all", "073_code_stress", "075_stl_calls" })
        {
            var objs = ParsedBlocks(sample);
            foreach (var f in Directory.GetFiles(Path.Combine(Program.Root, "samples", sample + "_truth", "PLC_1", "blocks"), "*.awl"))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var nets = ((List<object?>)objs[name]["networks"]!).Cast<PyDict>().ToList();
                var want = AwlNetworks(File.ReadAllText(f));
                c.Eq(want.Count, nets.Count, $"{sample} {name} network count");
                for (var i = 0; i < Math.Min(want.Count, nets.Count); i++)
                {
                    var (title, comment, body) = want[i];
                    var n = nets[i];
                    c.Eq("STL", n["language"], $"{sample} {name}/{i + 1} language");
                    c.Eq(title, (n["title"] as PyDict)?.Get("en-US") ?? "", $"{sample} {name}/{i + 1} title");
                    c.Eq(comment, (n["comment"] as PyDict)?.Get("en-US") ?? "", $"{sample} {name}/{i + 1} comment");
                    c.Eq(body, n.Get("stl"), $"{sample} {name}/{i + 1} code");
                    c.True(n.Get("unknownTokens") is null, $"{sample} {name}/{i + 1} no unknown STL elements");
                }
            }
        }
        c.Done();
    }

    /// <summary>(title, comment, body) of each NETWORK of an .awl source: TITLE line, leading // lines = network comment,
    /// the rest up to the next NETWORK or END_ line (trailing empty lines dropped).</summary>
    static List<(string Title, string Comment, string Body)> AwlNetworks(string src)
    {
        var lines = src.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
        var o = new List<(string, string, string)>();
        var i = Array.FindIndex(lines, l => l.Trim() == "BEGIN") + 1;
        while (i < lines.Length && lines[i].Trim() == "NETWORK")
        {
            i++;
            var title = lines[i].StartsWith("TITLE =") ? lines[i++]["TITLE =".Length..].Trim() : "";
            var comment = new List<string>();
            while (lines[i].StartsWith("//"))
                comment.Add(lines[i++][2..]);
            var body = new List<string>();
            while (lines[i].Trim() != "NETWORK" && !lines[i].StartsWith("END_"))
                body.Add(lines[i++]);
            while (body.Count > 0 && body[^1] == "")
                body.RemoveAt(body.Count - 1);
            o.Add((title, string.Join("\n", comment), string.Join("\n", body)));
        }
        return o;
    }

    /// <summary>Sample 065_db_iec_timer: single instance DB of an IEC timer (created in the TIA editor, Openness cannot
    /// create or list it; truth = editor view in 065_db_iec_timer_truth): DB of the system data type IEC_TIMER in Program
    /// resources with PT, ET, IN, Q and the start value of PT; the hidden ST and RU are not shown.</summary>
    [Test]
    public static void DbIecTimerSingleInstance()
    {
        var c = new Check("065 DBTon = TIA editor");
        var b = ParsedBlocks("065_db_iec_timer")["DBTon"];
        c.Eq("TypedDB", b["type"], "type");
        c.Eq("IEC_TIMER", b["instanceOf"], "instanceOf");
        c.Eq(13L, b["number"], "number");
        var ms = ((List<object?>)b["members"]!).Cast<PyDict>().ToList();
        c.Eq("PT:Time:T#2S ET:Time: IN:Bool: Q:Bool:", string.Join(" ", ms.Select(m => $"{m["name"]}:{m["dataType"]}:{m["startValue"]}")), "members");
        c.Eq("True False True False", string.Join(" ", ms.Select(m => Py.Str(m["externalWritable"]))), "externalWritable");
        c.Done();
    }

    /// <summary>Every data block of every sample is recognized and read: a known DB type (no "DB:..."), every
    /// instance, typed and technology DB has members (except instances of empty FBs, DBs whose interface is empty in
    /// the file, e.g. F_SystemInfo_DB, and the safety compiler DBs whose members are all hidden), and no DB member has an unresolved data type ("RID ...").</summary>
    [Test]
    public static void AllSampleDbsRead()
    {
        var c = new Check("all sample DBs recognized and read");
        Parallel.ForEach(SampleInputs(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, s =>
        {
            using var p = TiaProject.Open(s);
            void Walk(object? o)
            {
                if (o is PyDict d)
                {
                    if (d.Get("blocks") is List<object?> bl)
                        foreach (var b in bl.Cast<PyDict>().Where(x => Py.Str(x.Get("type")).EndsWith("DB", StringComparison.Ordinal)
                                     || Py.Str(x.Get("type")).StartsWith("DB:", StringComparison.Ordinal)))
                            CheckDb(c, s, b);
                    foreach (var v in d.Values)
                        Walk(v);
                }
                else if (o is List<object?> l)
                    foreach (var v in l)
                        Walk(v);
            }
            Walk(p.Raw(TiaContent.Structure, default));
        });
        c.Done();
    }

    static void CheckDb(Check c, string sample, PyDict b)
    {
        var what = $"{Path.GetFileName(sample)} {b["name"]}";
        var type = Py.Str(b["type"]);
        c.Eq(false, type.StartsWith("DB:", StringComparison.Ordinal), what + " known DB type " + type);
        var all = new Dictionary<string, PyDict>();
        Flat((List<object?>)b["members"]!, "", all);
        if (b.Get("interface") is List<object?> secs)
            foreach (var sec in secs.Cast<PyDict>())
                Flat((List<object?>)sec["members"]!, sec["section"] + ":", all);
        if (type is "InstanceDB" or "TypedDB" or "TechnologyDB" && !Py.Eq(b.Get("instanceOf"), "FB_Empty") && !Py.Eq(b.Get("instanceOf"), "TO_InterpreterMapping")
            && !Py.Eq(b.Get("instanceOf"), "TO_InterpreterProgram") && !Py.Str(b["name"]).StartsWith("Main_Safety", StringComparison.Ordinal)
            && !Py.Str(b.Get("group")).Contains("SafetyCompilerBlocksFolder", StringComparison.Ordinal))
            c.Eq(true, all.Count > 0, what + " has members");
        foreach (var (path, m) in all)
            if (Py.Str(m.Get("dataType")).StartsWith("RID ", StringComparison.Ordinal))
                c.Fail($"{what}.{path} unresolved type {m["dataType"]}");
    }

    /// <summary>Sample 070_db_khp_units: know-how protection (DB, FB and the FB's instance DB, like Openness) and the
    /// software unit of a block are part of the block output.</summary>
    [Test]
    public static void KnowHowProtectionAndUnits()
    {
        var c = new Check("070 know-how protection and software unit");
        var objs = ParsedBlocks("070_db_khp_units");
        foreach (var n in new[] { "DB_Khp", "FB_Khp", "FB_Khp_DB" })
            c.Eq(true, objs[n].Get("knowHowProtected"), n + " knowHowProtected");
        c.Eq(null, objs["DB_Global"].Get("knowHowProtected"), "DB_Global not protected");
        c.Eq("Unit_A", objs["DB_Global_1"].Get("unit"), "DB_Global_1 unit");
        c.Eq(null, objs["DB_Global"].Get("unit"), "DB_Global without unit");
        c.Done();
    }

    /// <summary>Sample 069_safety_compiled: the safety system DBs Openness can neither list nor export (MCP-109),
    /// F_SystemInfo_DB, RTG1SysInfo (F_CTRL_1 with F_SYSINFO) and the F-I/O DB F00000_FDI_1, equal the TIA editor view
    /// (editor/system_dbs.json with the screenshots): every member with section, data type and start value (the DB's
    /// own value or the default of the F-block), and nothing more. Sample 072_motion_tcpu: mTO_LeadingAxisProxy, which
    /// TIA cannot compile without a leading CPU and therefore not export, equals the DB editor with the external
    /// accessible/writable/visible and setpoint flags (4th column).</summary>
    [Test]
    public static void SafetySystemDbsAgainstEditor()
    {
        var c = new Check("069/072 DBs without Openness export = TIA editor");
        foreach (var sample in new[] { "069_safety_compiled", "072_motion_tcpu" })
        {
        var objs = ParsedBlocks(sample);
        var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Program.Root, "samples", sample + "_truth", "editor", "system_dbs.json")));
        foreach (var db in doc.RootElement.EnumerateObject().Where(x => x.Name != "source"))
        {
            var b = objs[db.Name];
            var got = new Dictionary<string, PyDict>();
            if (b.Get("interface") is List<object?> secs && secs.Count > 0)
                foreach (var sec in secs.Cast<PyDict>())
                    Flat((List<object?>)sec["members"]!, sec["section"] + ":", got);
            else
                Flat((List<object?>)b["members"]!, "Static:", got);
            var expected = new HashSet<string>();
            foreach (var sec in db.Value.EnumerateObject())
                foreach (var m in sec.Value.EnumerateArray())
                {
                    var path = sec.Name + ":" + m[0].GetString();
                    expected.Add(path);
                    if (!got.TryGetValue(path, out var g))
                    {
                        c.Fail($"{db.Name}.{path} missing");
                        continue;
                    }
                    c.Eq(m[1].GetString(), g.Get("dataType"), $"{db.Name}.{path} dataType");
                    c.Eq(m[2].ValueKind == System.Text.Json.JsonValueKind.Null ? null : m[2].GetString(),
                        g.Get("startValue") ?? g.Get("defaultValue"), $"{db.Name}.{path} start value");
                    if (m.GetArrayLength() > 3)
                    {
                        var f = m[3].GetString()!;
                        c.Eq(f.Contains('A'), g.Get("externalAccessible"), $"{db.Name}.{path} accessible");
                        c.Eq(f.Contains('W'), g.Get("externalWritable"), $"{db.Name}.{path} writable");
                        c.Eq(f.Contains('V'), g.Get("externalVisible"), $"{db.Name}.{path} visible");
                        c.Eq(f.Contains('S'), g.Get("setpoint"), $"{db.Name}.{path} setpoint");
                    }
                }
            foreach (var path in got.Keys.Where(k => !expected.Contains(k) && !k.Contains("_DAT.", StringComparison.Ordinal)))
                c.Fail($"{db.Name}.{path} not in the editor");
        }
        }
        c.Done();
    }

    static Dictionary<string, PyDict> ParsedBlocks(string sample)
    {
        using var p = TiaProject.Open(Program.SampleArchive(sample) ?? throw new TestFailure("no archive of sample " + sample));
        var objs = new Dictionary<string, PyDict>();
        void Walk(object? o)
        {
            if (o is PyDict d)
            {
                foreach (var key in new[] { "blocks", "types" })
                    if (d.Get(key) is List<object?> l)
                        foreach (var b in l.Cast<PyDict>())
                            objs[(string)b["name"]!] = b;
                foreach (var v in d.Values)
                    Walk(v);
            }
            else if (o is List<object?> l)
                foreach (var v in l)
                    Walk(v);
        }
        Walk(p.Raw(TiaContent.Structure, default));
        return objs;
    }

    /// <summary>Truth files of all PLCs of a sample: &lt;sample&gt;_truth/&lt;device&gt;/blocks.</summary>
    static string[] TruthFiles(string sample, string pattern) =>
        Directory.GetDirectories(Path.Combine(Program.Root, "samples", sample + "_truth"))
            .Select(d => Path.Combine(d, "blocks")).Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, pattern)).ToArray();

    static void DbSymbols(Check c, string sample)
    {
        var objs = ParsedBlocks(sample);
        var files = TruthFiles(sample, "*.xml");
        c.Eq(true, files.Length > 0, sample + " SimaticML exports");
        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            if (!objs.TryGetValue(name, out var b))
            {
                c.Fail(sample + " " + name + " not parsed");
                continue;
            }
            var got = new Dictionary<string, PyDict>();
            if (b.Get("interface") is List<object?> { Count: > 0 })
                foreach (var sec in ((List<object?>)b["interface"]!).Cast<PyDict>())
                    Flat((List<object?>)sec["members"]!, "", got);
            else if (b.Get("members") is List<object?> members)
                Flat(members, "", got);
            var exp = new Dictionary<string, (string? Type, string? Start, Dictionary<string, string?> Elems)>();
            var itf = XDocument.Load(f).Descendants().First(e => e.Name.LocalName == "Interface");
            // a technology object type nests the sections of its base type in Section "Base" (072: TO_SynchronousAxis
            // : TO_PositioningAxis : TO_SpeedAxis : TO_Axis); Openness shows their members flat in the DB
            void Secs(XElement s)
            {
                foreach (var sec in s.Elements().Where(e => e.Name.LocalName == "Sections").Elements())
                {
                    XmlFlat(sec, "", exp);
                    Secs(sec);
                }
            }
            Secs(itf);
            foreach (var (path, e) in exp)
            {
                if (!got.TryGetValue(path, out var g))
                {
                    c.Fail($"{name}.{path} missing");
                    continue;
                }
                c.Eq(e.Type, g.Get("dataType"), $"{name}.{path} dataType");
                if (e.Start is not null)
                    c.Eq(e.Start, g.Get("startValue"), $"{name}.{path} startValue");
                var ge = g.Get("elementStartValues") as PyDict ?? new PyDict();
                c.Eq(string.Join(";", e.Elems.Select(x => x.Key + "=" + x.Value).Order(StringComparer.Ordinal)),
                    string.Join(";", ge.Select(x => x.Key + "=" + x.Value).Order(StringComparer.Ordinal)),
                    $"{name}.{path} elementStartValues");
            }
            foreach (var path in got.Keys.Where(k => !exp.ContainsKey(k)))
                if (!exp.TryGetValue(path[..Math.Max(0, path.LastIndexOf('.'))], out var parent) || parent.Type != "DTL")
                    c.Fail($"{name}.{path} not in SimaticML");
        }
    }

    static void Flat(List<object?> members, string prefix, Dictionary<string, PyDict> o)
    {
        foreach (var m in members.Cast<PyDict>())
        {
            var path = prefix + (string)m["name"]!;
            o[path] = m;
            if (m.Get("members") is List<object?> kids)
                Flat(kids, path + ".", o);
        }
    }

    static void XmlFlat(XElement parent, string prefix, Dictionary<string, (string?, string?, Dictionary<string, string?>)> o)
    {
        foreach (var m in parent.Elements().Where(e => e.Name.LocalName == "Member"))
        {
            var path = prefix + (string?)m.Attribute("Name");
            var sv = m.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue")?.Value;
            var elems = m.Elements().Where(e => e.Name.LocalName == "Subelement")
                .ToDictionary(e => (string)e.Attribute("Path")!, e => e.Elements().FirstOrDefault(x => x.Name.LocalName == "StartValue")?.Value);
            o[path] = ((string?)m.Attribute("Datatype"), sv, elems);
            XmlFlat(m, path + ".", o);
            foreach (var sec in m.Elements().Where(e => e.Name.LocalName == "Sections").Elements())
                XmlFlat(sec, path + ".", o);
        }
    }
}
