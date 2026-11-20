// foreach (PyDict x in list) over the dynamic value trees of the Python port: the element type is a cast, not a claim
#pragma warning disable CS8600, CS8602
using System.Text;
using System.Text.RegularExpressions;
using OpenPEData.Format;
using OpenPEData.Runtime;

namespace OpenPEData.Model;

/// <summary>Hardware and network configuration (port of hardware.py, GOALS Z1.1, docs/spec/hw-config.md): device items
/// (tree via Parent, order by PositionNumber), address nodes, subnets, sync domains, connections, IO systems, port
/// interconnections, UMAC users and roles. Parameters are the named expandos of IPersistentExpandoData.</summary>
internal sealed class Hardware
{
    static readonly string[] Secret = ["EncryptedPassword", "RngCryptoSalt", "PkiContainerCache"];
    static readonly HashSet<string> HiddenSets = ["ICoreAttributes", "ICoreExpandoAttributes", "IConfigBaseData", "IDeviceItemData", "IDeviceData",
        "IPersistentExpandoData", "IBrowsingAttributes", "ICompileLoadConfigExpando", "INodeData", "IAddressAttributes"];
    static readonly HashSet<string> U32Params = ["Failsafe_CRC_3"];
    static readonly HashSet<string> IpParams = ["NodeIPAddress", "NodeIPSubnetMask", "NodeIPDefaultRouterAddress"];

    static readonly Dictionary<(string, string), string> OpennessNames = new()
    {
        [("S71500.CPU.CPUexecUnit", "MaxCycleConfig")] = "CycleMaximumCycleTime",
        [("S71500.CPU.CPUexecUnit", "PowerOnAction")] = "StartupActionAfterPowerOn",
        [("S71500.CPU.CpuCommon", "UseClockFlags")] = "ClockMemoryByte",
        [("S71500.CPU.CpuCommon", "ClockFlags_Local")] = "ClockMemoryByteAddress",
        [("S71500.CPU.CpuCommon", "ProtectionLevel")] = "PlcAccessLevelProvider.PlcProtectionAccessLevel",
        [("S71500.CPU", "WebServerActive")] = "WebserverActivate",
        [("S71500.CPU.Interface.IE", "EnableWebServerAccess4IE")] = "UseWebServerForInterface",
        [("S71500.CPU", "SelectedOpcUaLicense")] = "OpcUaPurchasedLicense",
        [("S71500.CPU", "Failsafe_Central_F_WD_Time")] = "Failsafe_DefaultFmonitoringTimeCentral",
        [("S71500.SM.DI.DI16", "Failsafe_F_Dest_Add")] = "Failsafe_FDestinationAddress",
        [("S71500.SM.DI.DI16", "Failsafe_F_Par_CRC")] = "Failsafe_FParameterSignatureWithAddresses",
        [("S71500.SM.DI.DI16", "Failsafe_F_Par_CRC_WithoutAddresses")] = "Failsafe_FParameterSignatureWithoutAddresses",
        [("S71500.SM.DI.DI16", "Failsafe_CRC_3")] = "Failsafe_FParameterSignatureIndividualParameters",
        [("S71500.SM.DI.DI16", "Failsafe_Short_Circuit_Test_0")] = "Failsafe_ShortCircuitTest_0",
        [("S71500.SM.AI.4AI2AO", "QualityInformation")] = "EnableValueStatus",
        [("S71500.CPU", "SuppressDeactivatingSystemDiagnosticsAlarms")] = "SuppressDeactivatingSystemDiagnosticsAlarms",
        [("S71500.CPU.CpuCommon", "EnableLockUserAccountAtRunTime")] = "PlcAccountLockingAtRuntimeFeature.EnableLockUserAccountAtRunTime",
        [("S71500.CPU.CpuCommon", "AccountLockedFor")] = "PlcAccountLockingAtRuntimeFeature.LockAccountForDuration",
        [("S71500.CPU.CpuCommon", "NoOfFailedLoginAttempts")] = "PlcAccountLockingAtRuntimeFeature.NumberOfFailedLoginAttempts",
        [("S71500.CPU.CpuCommon", "TimeBetweenFailedLoginAttempts")] = "PlcAccountLockingAtRuntimeFeature.LoginRetryInterval",
        [("S71500.CPU.CpuCommon", "LocalTimeZone")] = "TimeOfDayLocalTimeZone",
        [("S71500.CPU.CpuCommon", "EnableDaylightSavingsTime")] = "TimeOfDayActivateDaylightSavingTime",
        [("S71500.CPU.Interface.IE", "TimeSyncRole")] = "TimeSynchronizationNtpV2",
        [("S71500.CPU.Interface.IE", "TimeSyncInterval")] = "TimeSynchronizationUpdateInterval",
        [("S71500.CPU.Interface.IE", "TimeSyncNtpServer1")] = "TimeSynchronizationServer1",
        [("S71500.CPU.Interface.IE", "TimeSyncNtpServer2")] = "TimeSynchronizationServer2",
        [("S71500.CPU.Interface.IE", "TimeSyncNtpServer3")] = "TimeSynchronizationServer3",
        [("S71500.CPU.Interface.IE", "TimeSyncNtpServer4")] = "TimeSynchronizationServer4",
    };

    /// <summary>u32 as stored (3232235521) -> '192.168.0.1'.</summary>
    public static object? Ip4(object? v) => Py.IsInt(v)
        ? string.Join(".", new[] { 24, 16, 8, 0 }.Select(s => ((Py.Big(v) >> s) & 0xFF).ToString()))
        : v;

    /// <summary>u64 as stored -> 'XX-XX-XX-XX-XX-XX' (6 bytes, big endian).</summary>
    public static object? Mac(object? v) => Py.IsInt(v)
        ? string.Join("-", new[] { 40, 32, 24, 16, 8, 0 }.Select(s => ((int)((Py.Big(v) >> s) & 0xFF)).ToString("X2")))
        : v;

    static readonly Dictionary<long, string> IoTypes = new() { [2] = "Input", [4] = "Output" };
    const long SendClockBaseNs = 31250;

    /// <summary>IAddressAttributes.AM_x_AddrData_y (34 bytes): u32 length, u32 IO type, u16 length in bits at 10, u32 process
    /// image at 18, u32 start address in bits at 22.</summary>
    public static PyDict AddressData(object? v)
    {
        if (v is not byte[] b || b.Length < 26 || (long)Bin.FromLe(b, 0, 4) != b.Length)
            return PyDict.Of("raw", v is byte[] bb ? Py.HexStr(bb) : v);
        var code = (long)Bin.FromLe(b, 4, 8);
        var start = (long)Bin.FromLe(b, 22, 26);
        return PyDict.Of("ioType", IoTypes.TryGetValue(code, out var io) ? io : code, "startAddress", start / 8, "startBit", start % 8,
            "lengthBits", (long)Bin.FromLe(b, 10, 12), "processImage", (long)Bin.FromLe(b, 18, 22), "raw", Py.HexStr(b));
    }

    /// <summary>AddressManagerV2Data.AddressMap: [(kind, index, hwid)] or null.</summary>
    public static List<(long Kind, long Idx, long Hwid)>? AddressMap(object? v)
    {
        if (v is not byte[] b || b.Length < 36)
            return null;
        var off = 32 + (long)Bin.FromLe(b, 30, 32);
        var n = (long)Bin.FromLe(b, off, off + 4);
        if (off + 4 + 16 * n != b.Length)
            return null;
        var outl = new List<(long, long, long)>();
        for (var i = 0; i < n; i++)
        {
            var e = Py.Slice(b, off + 4 + 16 * i, off + 20 + 16 * i);
            outl.Add(((long)Bin.FromLe(e, 0, 2), (long)Bin.FromLe(e, 4, 8), (long)Bin.FromLe(e, 8, 12)));
        }
        return outl;
    }

    static readonly (string Pre, string Suf, string Kind)[] GsdKinds = [("Comp_", "_Rack", "R"), ("HM_", "", "DAP"), ("Module_", "", "M"),
        ("Submodule_", "", "SM"), ("Interface_", "", "Interface"), ("Port_", "", "Port")];

    /// <summary>Catalog entry of a GSD object 'GSD\&lt;gsdml file&gt;\&lt;kind&gt;_&lt;id&gt;' -> file, kind, id, Openness TypeIdentifier.</summary>
    public static PyDict? GsdType(object? catalog)
    {
        var parts = (catalog as string ?? "").Split('\\');
        if (parts.Length != 3 || parts[0] != "GSD")
            return null;
        var (fn, ent) = (parts[1], parts[2]);
        if (ent == "DEVICE")
            return PyDict.Of("file", fn, "kind", "DEVICE", "id", null, "typeIdentifier", $"GSD:{fn.ToUpperInvariant()}/D");
        foreach (var (pre, suf, kind) in GsdKinds)
        {
            if (ent.StartsWith(pre, StringComparison.Ordinal) && ent.EndsWith(suf, StringComparison.Ordinal))
            {
                var ident = Py.Slice(ent, pre.Length, ent.Length - suf.Length).Replace("_SPA_", " ").Replace("_DOT_", ".");
                var tid = kind is "R" or "DAP" or "M" or "SM" ? $"GSD:{fn.ToUpperInvariant()}/{kind}/{ident}" : null;
                return PyDict.Of("file", fn, "kind", kind, "id", ident, "typeIdentifier", tid);
            }
        }
        return PyDict.Of("file", fn, "kind", null, "id", ent, "typeIdentifier", null);
    }

    // --- DER (certificates) -------------------------------------------------------------------------
    static (int Tag, long Cs, long Ce) Der(byte[] b, long o)
    {
        int tag = Py.At(b, o), n0 = Py.At(b, o + 1);
        o += 2;
        long n = n0;
        if ((n0 & 0x80) != 0)
        {
            var k = n0 & 0x7F;
            n = (long)Bin.FromBe(b, o, o + k);
            o += k;
        }
        return (tag, o, o + n);
    }

    static List<(int Tag, long Cs, long Ce)> DerChildren(byte[] b, long o, long e)
    {
        var outl = new List<(int, long, long)>();
        while (o < e)
        {
            var x = Der(b, o);
            outl.Add(x);
            o = x.Ce;
        }
        return outl;
    }

    static string DerTime(byte[] b, int t, long cs, long ce)
    {
        var v = Py.Ascii(Py.Slice(b, cs, ce));
        if (t == 0x17)
            v = (Py.ParseInt(Py.Slice(v, 0, 2)) >= 50 ? "19" : "20") + v;
        return $"{Py.Slice(v, 0, 4)}-{Py.Slice(v, 4, 6)}-{Py.Slice(v, 6, 8)}T{Py.Slice(v, 8, 10)}:{Py.Slice(v, 10, 12)}:{Py.Slice(v, 12, 14)}Z";
    }

    static readonly Dictionary<string, string> Oids = new() { ["550403"] = "CN", ["55040a"] = "O", ["55040b"] = "OU", ["550406"] = "C" };

    static string DerName(byte[] b, long cs, long ce)
    {
        var parts = new List<string>();
        foreach (var (_, ss, se) in DerChildren(b, cs, ce))
            foreach (var (_, a0, ae) in DerChildren(b, ss, se))
            {
                var ch = DerChildren(b, a0, ae);
                if (ch.Count < 2)
                    throw new PyValueError("not enough values to unpack");
                var (_, os, oe) = ch[0];
                var (_, vs, ve) = ch[1];
                var oid = Py.HexStr(Py.Slice(b, os, oe));
                parts.Add((Oids.TryGetValue(oid, out var nm) ? nm : oid) + "=" + Py.Utf8(Py.Slice(b, vs, ve)));
            }
        return string.Join(", ", parts);
    }

    /// <summary>Subject, issuer, serial and validity of a DER certificate.</summary>
    public static PyDict X509Summary(byte[] der)
    {
        var (_, cs, _) = Der(der, 0);
        var (_, tcs, tce) = Der(der, cs);
        var tbs = DerChildren(der, tcs, tce);
        if (tbs.Count == 0)
            throw new PyIndexError("list index out of range");
        var i = tbs[0].Tag == 0xA0 ? 1 : 0;
        if (tbs.Count <= i + 4)
            throw new PyIndexError("list index out of range");
        var (serial, issuer, validity, subject) = (tbs[i], tbs[i + 2], tbs[i + 3], tbs[i + 4]);
        var nbna = DerChildren(der, validity.Cs, validity.Ce);
        if (nbna.Count != 2)
            throw new PyValueError("unpack");
        return PyDict.Of("subject", DerName(der, subject.Cs, subject.Ce), "issuer", DerName(der, issuer.Cs, issuer.Ce),
            "serialNumber", Py.HexStr(Py.Slice(der, serial.Cs, serial.Ce)),
            "notBefore", DerTime(der, nbna[0].Tag, nbna[0].Cs, nbna[0].Ce), "notAfter", DerTime(der, nbna[1].Tag, nbna[1].Cs, nbna[1].Ce));
    }

    /// <summary>base64.b64decode (non-alphabet characters discarded; bad padding raises ValueError).</summary>
    static byte[] B64(string s)
    {
        var f = new string(s.Where(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '=').ToArray());
        try
        {
            return Convert.FromBase64String(f);
        }
        catch (FormatException)
        {
            throw new PyValueError("Incorrect padding");
        }
    }

    static readonly Regex PkiEntry = new(
        "(?<len>[\\x01-\\x7f])(?<name>[\\x20-\\x7e]{1,127})(?<id>[\\x00-\\xff]{4})\\x00\\x00\\x00\\x00\\x01\\x00\\x00\\x00" +
        "\\x02\\x00\\x00\\x00\\x00\\x00\\x00\\x00\\x14\\x00(?<thumb>[\\x00-\\xff]{20})", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    static readonly Regex Pem = new("-----BEGIN ([A-Z ]+)-----\\r?\\n(.*?)-----END \\1-----", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    static readonly Regex San = new("([\\x01-\\x7f])((?:IP|DNS|URI|EMAIL):[\\x20-\\x7e]+)$", RegexOptions.CultureInvariant);

    /// <summary>Certificates of S7ControllerTargetData.PkiContainerCache: zlib streams with PEM blocks, each certificate
    /// preceded by its subject alternative names, name, ID and SHA-1 thumbprint.</summary>
    public static List<object?>? PkiCertificates(object? v)
    {
        if (v is not byte[] blob)
            return null;
        var data = new List<byte>();
        var pos = 0;
        while (true)
        {
            var i = Py.Find(blob, [0x78, 0x5e], pos);
            if (i < 0)
                break;
            try
            {
                var (o, _, end) = Zlib.DecompressObj(blob, i);
                data.AddRange(o);
                pos = end;
            }
            catch (ZlibError)
            {
                pos = i + 1;
            }
        }
        var text = Py.Latin1([.. data]);
        var pem = Pem.Matches(text).ToList();
        var outl = new List<object?>();
        for (var j = 0; j < pem.Count; j++)
        {
            var m = pem[j];
            if (m.Groups[1].Value != "CERTIFICATE")
                continue;
            var prevEnd = j > 0 ? pem[j - 1].Index + pem[j - 1].Length : 0;
            Match? meta = null;
            var seg0 = text[prevEnd..m.Index];
            foreach (Match x in PkiEntry.Matches(seg0))
                meta = x;
            if (meta is not null && meta.Groups["len"].Value[0] != meta.Groups["name"].Value.Length)
                meta = null;
            var cert = PyDict.Of("id", meta is not null ? (long)Bin.FromLe(Py.Latin1Bytes(meta.Groups["id"].Value), 0, 4) : null,
                "name", meta?.Groups["name"].Value,
                "thumbprintSha1", meta is not null ? Py.HexStr(Py.Latin1Bytes(meta.Groups["thumb"].Value)) : null,
                "hasPrivateKey", j + 1 < pem.Count && pem[j + 1].Groups[1].Value == "ENCRYPTED PRIVATE KEY");
            if (meta is not null)
            {
                var seg = seg0[..meta.Index];
                var san = San.Match(seg);
                cert["subjectAlternativeNames"] = san.Success && san.Groups[1].Value[0] == san.Groups[2].Value.Length
                    ? san.Groups[2].Value.Split(',').Cast<object?>().ToList() : new List<object?>();
            }
            try
            {
                cert.Update(X509Summary(B64(m.Groups[2].Value)));
            }
            catch (Exception e) when (e is PyValueError or PyIndexError || Attrs.IsDecodeError(e))
            {
            }
            outl.Add(cert);
        }
        return outl;
    }

    /// <summary>UMAC `...Encrypted` attribute: with bytes 19 and 20 = 1 the data (n bytes at 24) is stored plain.</summary>
    public static byte[]? UmacValue(object? v)
    {
        if (v is not byte[] b || b.Length < 24 || b[19] != 1 || b[20] != 1)
            return null;
        var n = (long)Bin.FromLe(b, 1, 5);
        return b.Length == 24 + n ? Py.Slice(b, 24, 24 + n) : null;
    }

    public static string? UmacText(object? b) => UmacValue(b) is { } v ? Py.Utf16Le(v) : null;

    /// <summary>{(gsdml file name lower, DAP id): {subslot: submodule ID}} from AdditionalFiles/GSD.</summary>
    public static Dictionary<string, Dictionary<long, string>> GsdmlSubmodules(string projectPath)
    {
        var files = new OrderedDictionary<string, byte[]>();
        try
        {
            if (Directory.Exists(projectPath))
            {
                var d = Path.Combine(projectPath, "AdditionalFiles", "GSD");
                foreach (var f in Directory.Exists(d) ? Directory.EnumerateFileSystemEntries(d) : [])
                {
                    var n = Path.GetFileName(f);
                    if (n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        files[n] = File.ReadAllBytes(f);
                }
            }
            else if (ProjectFiles.IsZip(projectPath))
            {
                using var z = ProjectFiles.OpenZip(projectPath);
                foreach (var e in z.Entries)
                    if (e.FullName.StartsWith("AdditionalFiles/GSD/", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        files[Py.AfterLast(e.FullName, "/")] = ProjectFiles.ReadEntry(e);
            }
        }
        catch (IOException)
        {
            return [];
        }
        var res = new Dictionary<string, Dictionary<long, string>>();
        foreach (var (n, data) in files)
        {
            System.Xml.Linq.XElement root;
            try
            {
                root = Et.FromBytes(data);
            }
            catch (XmlParseError)
            {
                continue;
            }
            foreach (var dap in Et.Iter(root))
            {
                if (!Et.Tag(dap).EndsWith("DeviceAccessPointItem", StringComparison.Ordinal))
                    continue;
                var subs = new Dictionary<long, string>();
                foreach (var e in Et.Iter(dap))
                    if (Et.Tag(e).EndsWith("InterfaceSubmoduleItem", StringComparison.Ordinal) || Et.Tag(e).EndsWith("PortSubmoduleItem", StringComparison.Ordinal))
                        if (Py.Bool(Et.Get(e, "SubslotNumber")) && Py.Bool(Et.Get(e, "ID")))
                            subs[Py.ParseInt(Et.Get(e, "SubslotNumber")!)] = Et.Get(e, "ID")!;
                res[n.ToLowerInvariant() + "\0" + Et.Get(dap, "ID")] = subs;
            }
        }
        return res;
    }

    static object? Value(string name, object? v)
    {
        if (Secret.Any(s => name.StartsWith(s, StringComparison.Ordinal)))
            return Py.Bool(v) ? $"(set, {(v is string s ? Py.CpLen(s) : v is byte[] b ? b.Length : throw new PyTypeError("object has no len()"))} bytes)" : null;
        return v is byte[] arr && SparseArray(arr) is { } sa ? sa : v;
    }

    /// <summary>Per-channel parameters stored as a sparse array (samples 254/255, DI module: EnableRisingEdgeArray,
    /// EnableFallingEdgeArray, ProcessAlarm): u32 total size, u32 20 (header size), u32 element count, u32 0, u32 offset of
    /// the value section; at 20 the index section (u32 size = 8 + 4n, u32 n, n u32 indices), at the offset the value section
    /// (u32 size, u32 n, n values of (size - 8) / n bytes, little endian). Only elements that differ from the default are
    /// stored. Result: {"arrayLength": count, "entries": {"index": value}}; null if the bytes have another layout.</summary>
    public static PyDict? SparseArray(byte[] b)
    {
        if (b.Length < 36)
            return null;
        long U(long o) => o + 4 <= b.Length ? (long)Bin.FromLe(b, o, o + 4) : -1;
        var total = U(0);
        var count = U(8);
        var vo = U(16);
        if (total < 36 || total > b.Length || U(4) != 20 || U(12) != 0 || count <= 0 || count > 1 << 20)
            return null;
        var n = U(24);
        if (n < 0 || n > count || U(20) != 8 + 4 * n || vo != 20 + 8 + 4 * n || vo + 8 > total || U(vo + 4) != n)
            return null;
        var vsize = U(vo) - 8;
        if (n == 0 ? vsize != 0 : vsize <= 0 || vsize % n != 0 || vo + 8 + vsize > total)
            return null;
        var w = n == 0 ? 0 : vsize / n;
        if (w is not (0 or 1 or 2 or 4 or 8))
            return null;
        var entries = new PyDict();
        for (var i = 0; i < n; i++)
        {
            var idx = U(28 + 4 * i);
            if (idx < 0 || idx >= count)
                return null;
            entries[idx.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Py.Int(Bin.FromLe(b, vo + 8 + i * w, vo + 8 + (i + 1) * w));
        }
        return PyDict.Of("arrayLength", count, "entries", entries);
    }

    readonly ProjectModel m;
    readonly Project p;
    Dictionary<string, long>? hw;
    Dictionary<string, Dictionary<long, string>>? gsdml;

    public Hardware(ProjectModel model)
    {
        m = model;
        p = model.P;
    }

    AttrSets AttrsOf(string k) => p.Sets(k);

    PyDict Params(string k)
    {
        var ex = AttrsOf(k).Set("IPersistentExpandoData");
        var r = new PyDict();
        foreach (var (n, v) in Py.Sorted(ex.Items, kv => Py.T(kv.Key, kv.Value)))
        {
            var ns = (string)n!;
            r[ns] = U32Params.Contains(ns) && Py.IsInt(v) ? Py.Int(Py.Big(v) & 0xFFFFFFFF) : Value(ns, v);
        }
        return r;
    }

    PyDict OtherSets(string k)
    {
        var r = new PyDict();
        foreach (var (s, v) in AttrsOf(k))
            if (!HiddenSets.Contains(s) && v.Count > 0)
                r[s] = v;
        return r;
    }

    (object? Name, object? Version) Catalog(string k)
    {
        var cb = AttrsOf(k).Set("IConfigBaseData");
        return (cb.Get("ConfigObjectTypeName"), cb.Get("ConfigObjectTypeVersion"));
    }

    Dictionary<string, long> HwMap()
    {
        if (hw is null)
        {
            hw = [];
            foreach (var k in p.OfType("AddressManagerV2Data"))
            {
                var regs = p.Targets(k, "RegisteredDeviceItems");
                foreach (var (kind, idx, hwid) in AddressMap(p.Attr(k, "IPersistentExpandoData", "AddressMap")) ?? [])
                {
                    if (kind == 8)
                    {
                        var par = m.Rel1(k, "Parent");
                        if (par is not null)
                            hw.TryAdd(par, hwid);
                        else
                            hw.TryAdd("\0None", hwid);
                    }
                    else if (kind == 2 && idx < regs.Count)
                        hw.TryAdd(regs[(int)idx], hwid);
                }
            }
        }
        return hw;
    }

    PyDict? HwId(string k)
    {
        object? v = HwMap().TryGetValue(k, out var hv) ? hv : null;
        var consts = p.Targets(k, "ConstantTags");
        if (v is null)
        {
            foreach (var c in consts)
            {
                var d = p.Attr(c, "IDefaultStrategyData", "DefaultValue");
                if (d is not null && Py.IsDigit(Py.Str(d)))
                {
                    v = d is string ds ? Py.ParseInt(ds) : Py.Int(Py.Big(d));
                    break;
                }
            }
        }
        var cn = consts.Count > 0 ? p.Name(consts[0]) : null;
        return v is not null ? PyDict.Of("value", v, "constant", cn) : null;
    }

    PyDict? SystemHwId(string? master, string? name)
    {
        foreach (var c in master is not null ? p.Targets(master, "ConstantTags") : [])
        {
            var v = p.Attr(c, "IDefaultStrategyData", "DefaultValue");
            if ((p.Name(c) ?? "").EndsWith("~" + (name ?? "").Replace(" ", "_"), StringComparison.Ordinal) && Py.IsDigit(Py.Str(v)))
                return PyDict.Of("value", v is string vs ? Py.ParseInt(vs) : Py.Int(Py.Big(v)), "constant", p.Name(c));
        }
        return null;
    }

    PyDict? Gsd(object? cat)
    {
        var g = GsdType(cat);
        if (g is not null && g["kind"] is "Interface" or "Port" && Py.Bool(g["id"]) && ((string)g["id"]!).Contains('_'))
        {
            gsdml ??= GsdmlSubmodules(p.Path);
            var id = (string)g["id"]!;
            var cut = id.LastIndexOf('_');
            var (dap, sub) = (id[..cut], id[(cut + 1)..]);
            string? sid = null;
            if (Py.IsDigit(sub) && gsdml.TryGetValue(((string)g["file"]!).ToLowerInvariant() + "\0" + dap, out var subs))
                sid = subs.GetValueOrDefault(Py.ParseInt(sub));
            if (Py.Bool(sid))
            {
                g = new PyDict(g);
                g["gsdmlId"] = sid;
                g["typeIdentifier"] = $"GSD:{((string)g["file"]!).ToUpperInvariant()}/SM/{sid}";
            }
        }
        return g;
    }

    bool IsItem(string k) => AttrsOf(k).ContainsKey("IDeviceItemData");

    /// <summary>Station/.../item names, for references from subnets and connections.</summary>
    string PathOf(string? k)
    {
        var parts = new List<string>();
        while (k is not null && p.Objects.ContainsKey(k))
        {
            parts.Add(p.Name(k) ?? "");
            if (p.IsA(k, "DeviceData"))
                break;
            k = m.Rel1(k, "Parent") ?? m.Rel1(k, "DeviceItem");
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    List<string> ByPosition(IEnumerable<string> keys) =>
        Py.Sorted(keys, c => Py.T(Py.Or(p.Attr(c, "IDeviceItemData", "PositionNumber"), 0L), Py.Or(p.Name(c), "")));

    // --- device items -----------------------------------------------------------------------------
    public PyDict Node(string n)
    {
        var a = AttrsOf(n);
        var nd = a.Set("INodeData");
        var par = Params(n);
        var sub = m.Rel1(n, "Subnet");
        var prm = new PyDict();
        foreach (var (k, v) in par)
            if (!IpParams.Contains((string)k!))
                prm[k] = v;
        // NodeIPConfiguration = Openness IpProtocolSelection. Only "Project" (0) has a configured address; otherwise
        // the stored address is a leftover that Openness does not show (sample 063: 1 = Dhcp, 3 = OtherPath).
        var ipSel = par.Get("NodeIPConfiguration") is long sel ? sel : 0L;
        var inProject = ipSel == 0;
        var node = PyDict.Of("name", p.Name(n), "nodeType", nd.Get("NodeType"), "nodeId", nd.Get("NodeID"),
            "subtype", a.Set("ICoreAttributes").Get("Subtype"), "catalog", Catalog(n).Name,
            "subnet", sub is not null ? p.Name(sub) : null,
            "ipAddress", inProject ? Ip4(par.Get("NodeIPAddress")) : null, "subnetMask", inProject ? Ip4(par.Get("NodeIPSubnetMask")) : null,
            "router", inProject && Py.Bool(par.Get("NodeIPDefaultRouterAddressUsed")) ? Ip4(par.Get("NodeIPDefaultRouterAddress")) : null,
            "macAddress", Mac(par.Get("NodeMacAddress")), "profinetDeviceName", par.Get("PnNameOfStation"),
            "profibusAddress", par.Get("NodePBMpiAddress"), "parameters", prm, "key", n);
        if (!inProject)
            node["ipProtocolSelection"] = ipSel is >= 0 and < 6 ? IpProtocolSelections[ipSel] : ipSel.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return node;
    }

    /// <summary>Openness IpProtocolSelection by NodeIPConfiguration value (0, 1, 3 confirmed with sample 063; 2, 4, 5 by enum order).</summary>
    static readonly string[] IpProtocolSelections = ["Project", "Dhcp", "UserProgram", "OtherPath", "ViaIoController", "None"];

    static readonly Regex AddrName = new(@"^AM_\d+_AddrData_\d+$", RegexOptions.CultureInvariant);

    public PyDict Item(string k)
    {
        var a = AttrsOf(k);
        var core = a.Set("ICoreAttributes");
        var di = a.Set("IDeviceItemData");
        var (cat, catver) = Catalog(k);
        var children = ByPosition(m.Sources(k, "Parent").Where(IsItem));
        var nodes = Py.Sorted(m.Sources(k, "DeviceItem"), x => Py.Or(p.Name(x), ""));
        var par = Params(k);
        var sub = core.Get("Subtype");
        var onames = new PyDict();
        foreach (var e in par.Keys)
            if (sub is string ss && OpennessNames.TryGetValue((ss, (string)e!), out var on))
                onames[e] = on;
        var addrs = new List<object?>();
        foreach (var (n, v) in Py.Sorted(a.Set("IAddressAttributes").Items, kv => Py.T(kv.Key, kv.Value)))
        {
            if (!AddrName.IsMatch((string)n!))
                continue;
            var ad = new PyDict(AddressData(v));
            ad["name"] = n;
            addrs.Add(ad);
        }
        var d = PyDict.Of("name", p.Name(k), "objectType", p.TypeName(k), "subtype", sub,
            "positionNumber", di.Get("PositionNumber"), "orderNumber", Py.Or(di.Get("OrderNumber"), null),
            "firmware", Py.Or(di.Get("FwVersion"), null), "typeName", Py.Or(di.Get("InvariantTypeName"), null),
            "deviceItemType", di.Get("DeviceItemType"), "catalog", cat, "catalogVersion", catver);
        d["gsd"] = Gsd(cat);
        d["hwIdentifier"] = HwId(k);
        d["addresses"] = addrs;
        d["partnerPorts"] = Py.Sorted(p.Targets(k, "PortToPorts").Select(PathOf)).Cast<object?>().ToList();
        d["certificates"] = par.ContainsKey("PkiContainerCache") ? PkiCertificates(p.Attr(k, "IPersistentExpandoData", "PkiContainerCache")) : null;
        d["sendClockNs"] = Py.IsInt(par.Get("PnIoSendClockFactor")) ? Py.Int(Py.Big(par["PnIoSendClockFactor"]) * SendClockBaseNs) : null;
        d["comment"] = m.Comment(k);
        d["parameters"] = par;
        d["opennessNames"] = onames;
        d["attributes"] = OtherSets(k);
        d["nodes"] = nodes.Where(n => p.IsA(n, "NodeData")).Select(n => (object?)Node(n)).ToList();
        d["items"] = children.Select(c => (object?)Item(c)).ToList();
        d["key"] = k;
        return d;
    }

    /// <summary>Safety administration of the F-CPUs of station d; null for stations without F-CPU.</summary>
    public PyDict? Safety(string d)
    {
        var tgts = m.Sources(d, "Parent").Where(t => p.IsA(t, "S7ControllerTargetData")).ToList();
        tgts.AddRange(m.Sources(d, "Parent").SelectMany(x => m.Sources(x, "Parent")).Where(t => p.IsA(t, "S7ControllerTargetData")));
        PyDict? res = null;
        foreach (var t in tgts)
        {
            foreach (var k in m.Sources(t, "Target"))
            {
                var tn = p.TypeName(k);
                if (tn is not ("FailsafeProgramSettings" or "RuntimegroupInfo" or "FModuleInfo"))
                    continue;
                res ??= PyDict.Of("programSettings", null, "runtimeGroups", new List<object?>(), "fModules", new List<object?>());
                var a = AttrsOf(k);
                if (tn == "FailsafeProgramSettings")
                {
                    var ps = new PyDict(a.Set("IFailsafeProgramSettings"));
                    var pw = ps.Pop("FProgramPasswordPlus");
                    ps.Update(a.Set("IFailsafeProgramSettingsExpando"));
                    ps["passwordSet"] = Py.Bool(pw);
                    res["programSettings"] = ps;
                }
                else if (tn == "RuntimegroupInfo")
                {
                    var r = a.Set("IRuntimegroupInfo");
                    ((List<object?>)res["runtimeGroups"]!).Add(PyDict.Of("name", p.Name(k), "maxCycleTimeUs", r.Get("MaxCycleTime"),
                        "warnCycleTimeUs", r.Get("WarningCycletime"), "infoDb", Py.Or(r.Get("Control1IDBSymbol"), null),
                        "preProcessing", Py.Or(r.Get("PreProcessorFCName"), null), "postProcessing", Py.Or(r.Get("PostProcessorFCName"), null), "key", k));
                }
                else
                {
                    var mi = a.Set("IFModuleInfo");
                    var di = m.Rel1(k, "FModuleInfoToDeviceItemData");
                    ((List<object?>)res["fModules"]!).Add(PyDict.Of("deviceItem", di is not null ? PathOf(di) : null, "fIoDb", mi.Get("StoredNameStorage"),
                        "fIoDbNumber", mi.Get("IDBNr"), "monitoringTime", mi.Get("MonitoringTime"), "parameterCrc", mi.Get("ModuleCRC"), "key", k));
                }
            }
        }
        if (res is not null)
        {
            res["runtimeGroups"] = Py.Sorted(((List<object?>)res["runtimeGroups"]!).Cast<PyDict>(), x => Py.Or(x["name"], "")).Cast<object?>().ToList();
            res["fModules"] = Py.Sorted(((List<object?>)res["fModules"]!).Cast<PyDict>(), x => Py.Or(x["deviceItem"], "")).Cast<object?>().ToList();
        }
        return res;
    }

    /// <summary>Hardware of one station: catalog entry, parameters and the device item tree.</summary>
    public PyDict Device(string d)
    {
        var (cat, catver) = Catalog(d);
        var top = ByPosition(m.Sources(d, "Parent").Where(IsItem));
        var r = PyDict.Of("isCentral", p.Attr(d, "IDeviceData", "IsCentral"), "catalog", cat, "catalogVersion", catver, "gsd", GsdType(cat));
        r["hwIdentifier"] = HwId(d);
        r["parameters"] = Params(d);
        var items = top.Select(c => (object?)Item(c)).ToList();
        AutoPnNames(items);
        r["items"] = items;
        r["safety"] = Safety(d);
        return r;
    }

    /// <summary>PROFINET device name with automatic generation (<c>PnPnNoSAutoGenerate</c> True): the stored
    /// <c>PnNameOfStation</c> is stale, Openness derives the name from the current names. Rule (samples 063, 210–228,
    /// GSD device): base = CPU or head module name; an interface of the base module gives the base name alone if the
    /// module has one Ethernet interface, else "base.interface"; an interface of another module (CP) gives
    /// "base.module". Lower case. Stations without CPU or head module (HMI) keep the stored name.</summary>
    static void AutoPnNames(List<object?> top)
    {
        var baseItem = top.OfType<PyDict>().FirstOrDefault(i => i["objectType"] is "S7ControllerTargetData" or "HeadDeviceItemData");
        if (baseItem is null)
            return;
        var baseName = (string?)baseItem["name"] ?? "";
        foreach (var mod in top.OfType<PyDict>())
        {
            var eth = new List<(PyDict Item, PyDict Node)>();
            void Walk(PyDict it)
            {
                foreach (PyDict n in (List<object?>)it["nodes"]!)
                    if (n["nodeType"] is 3L)
                        eth.Add((it, n));
                foreach (var c in ((List<object?>)it["items"]!).Cast<PyDict>())
                    Walk(c);
            }
            Walk(mod);
            foreach (var (it, n) in eth)
            {
                if (((PyDict)n["parameters"]!).Get("PnPnNoSAutoGenerate") is not true)
                    continue;
                string? name = ReferenceEquals(mod, baseItem)
                    ? eth.Count == 1 ? baseName : $"{baseName}.{it["name"]}"
                    : eth.Count == 1 ? $"{baseName}.{mod["name"]}" : null;
                if (name is null)
                    continue;
                n["profinetDeviceNameStored"] = n["profinetDeviceName"];
                n["profinetDeviceName"] = name.ToLowerInvariant();
            }
        }
    }

    // --- project level ----------------------------------------------------------------------------
    public PyDict Network(HashSet<string> devices)
    {
        bool Mine(string? k)
        {
            while (k is not null && p.Objects.ContainsKey(k) && !p.IsA(k, "DeviceData"))
                k = m.Rel1(k, "Parent") ?? m.Rel1(k, "DeviceItem") ?? m.Rel1(k, "Target");
            return k is not null && devices.Contains(k);
        }
        List<object?> SortedPaths(IEnumerable<string> keys) => Py.Sorted(keys.Select(PathOf)).Cast<object?>().ToList();

        var subnets = new List<PyDict>();
        foreach (var s in p.OfType("SubnetData"))
        {
            var nodes = m.Sources(s, "Subnet").Where(n => p.IsA(n, "NodeData") && Mine(n)).ToList();
            if (nodes.Count == 0)
                continue;
            subnets.Add(PyDict.Of("name", p.Name(s), "subtype", p.Attr(s, "ICoreAttributes", "Subtype"),
                "catalog", Catalog(s).Name, "parameters", Params(s), "nodes", SortedPaths(nodes), "key", s));
        }
        var domains = new List<PyDict>();
        foreach (var k in p.OfType("DomainData"))
        {
            var sub = m.Rel1(k, "Subnet");
            var links = p.Links(k).Where(l => l.Relation is { } r && p.Mm.Relations.ContainsKey(r))
                .Select(l => p.Mm.Relations[l.Relation!.Value].Name).Distinct().ToList();
            domains.Add(PyDict.Of("name", p.Name(k), "subtype", p.Attr(k, "ICoreAttributes", "Subtype"),
                "catalog", Catalog(k).Name, "subnet", sub is not null ? p.Name(sub) : null,
                "participants", SortedPaths(p.Targets(k, "InterfaceSubmodules")), "parameters", Params(k),
                "links", Py.Sorted(links, x => x).Cast<object?>().ToList(), "key", k));
        }
        var conns = new List<PyDict>();
        foreach (var k in p.OfType("HmiConnectionPointData"))
        {
            var hc = m.Rel1(k, "HmiConnection");
            var nodes = p.Targets(k, "Conn2Nodes");
            var remote = m.Sources(k, "Node2RemoteConns").Where(p.Objects.ContainsKey).ToList();
            if (nodes.Count > 0 && !nodes.Concat(remote).Any(Mine))
                continue;
            conns.Add(PyDict.Of("name", p.Name(k), "kind", "HmiConnection", "hmiConnection", hc is not null ? p.Name(hc) : null,
                "nodes", SortedPaths(nodes), "partnerNodes", SortedPaths(remote), "parameters", Params(k), "key", k));
        }
        foreach (var k in p.OfType("BlockConnectionPointData"))
        {
            var nodes = p.Targets(k, "Conn2Nodes");
            var tgt = m.Rel1(k, "Parent") ?? m.Rel1(k, "Target");
            if (!(tgt is not null && Mine(tgt)))
                continue;
            var par = Params(k);
            var cid = par.Get("ConnId");
            object? connId = Py.IsInt(cid) ? Py.Hex(cid, true) : cid is byte[] cb && cb.Length >= 8 ? Py.Hex(Py.Int(Bin.FromLe(cb, 0, 8)), true) : null;
            conns.Add(PyDict.Of("name", p.Name(k), "kind", p.Attr(k, "ICoreAttributes", "Subtype"), "target", p.Name(tgt),
                "nodes", SortedPaths(nodes), "partnerNodes", new List<object?>(), "connectionId", connId, "parameters", par, "key", k));
        }
        var iosys = new List<PyDict>();
        foreach (var k in p.OfType("MastersystemData"))
        {
            var dev = m.Rel1(k, "Device") ?? m.Rel1(k, "Target");
            if (!(dev is not null && Mine(dev)))
                continue;
            var sub = m.Rel1(k, "Subnet");
            var master = m.Rel1(k, "Master");
            var par = Params(k);
            iosys.Add(PyDict.Of("name", p.Name(k), "subtype", p.Attr(k, "ICoreAttributes", "Subtype"),
                "catalog", Catalog(k).Name, "number", par.Get("PositionNumber"),
                "subnet", sub is not null ? p.Name(sub) : null, "master", master is not null ? PathOf(master) : null,
                "devices", SortedPaths(p.Targets(k, "HeadModules")), "hwIdentifier", SystemHwId(master, p.Name(k)), "parameters", par, "key", k));
        }
        var links2 = new HashSet<(string, string)>();
        foreach (var k in p.OfType("DeviceItemData"))
        {
            if (!Mine(k))
                continue;
            foreach (var t in p.Targets(k, "PortToPorts"))
            {
                var a = PathOf(k);
                var b = PathOf(t);
                links2.Add(Py.CompareStr(a, b) <= 0 ? (a, b) : (b, a));
            }
        }
        List<object?> ByName(List<PyDict> l) => Py.Sorted(l, x => Py.Or(x["name"], "")).Cast<object?>().ToList();
        return PyDict.Of("subnets", ByName(subnets), "syncDomains", ByName(domains), "ioSystems", ByName(iosys), "connections", ByName(conns),
            "portLinks", Py.Sorted(links2, x => Py.T(x.Item1, x.Item2)).Select(x => (object?)new List<object?> { x.Item1, x.Item2 }).ToList());
    }

    // --- project level: UMAC users and roles --------------------------------------------------------
    public PyDict Umac()
    {
        string? Name(string k, string s) => UmacText(p.Attr(k, s, "NameEncrypted"));

        object? Timeout(string k)
        {
            var v = UmacValue(Py.OrElse(p.Attr(k, "ICommonEditableRoleAttributes", "SessionTimeoutEncrypted"),
                () => p.Attr(k, "IProjectUserAttributes", "SessionTimeoutEncrypted")));
            return v is { Length: 4 } ? (long)Bin.FromLe(v, 0, 4) : null;
        }
        var roles = new List<PyDict>();
        var users = new List<PyDict>();
        foreach (var root in p.OfType("UmacRootData"))
        {
            foreach (var r in p.Targets(root, "AllRoles"))
            {
                roles.Add(PyDict.Of("name", Py.OrElse(Name(r, "IEditableRoleAttributes"), () => p.Attr(r, "ISystemRoleProxyAttributes", "ExternalId")),
                    "objectType", p.TypeName(r),
                    "comment", Py.Or(UmacText(p.Attr(r, "ICommonEditableRoleAttributes", "CommentEncrypted")), null),
                    "sessionTimeout", Timeout(r),
                    "devices", Py.Sorted(p.Targets(r, "RoleToDevice").Select(d => p.Name(d) ?? "")).Cast<object?>().ToList(),
                    "deviceRights", Py.Sorted(p.Targets(r, "AssignedDeviceFunctionRight").Select(x => Py.AfterLast(p.Name(x) ?? "", "."))).Cast<object?>().ToList(),
                    "key", r));
            }
            foreach (var u in p.Targets(root, "AllUsers"))
            {
                var ur = new List<object?>();
                foreach (var a in p.Targets(u, "UserRoles"))
                {
                    var ro = m.Rel1(a, "AssociationToRole");
                    var dev = m.Rel1(a, "RoleAndUserAssociationToDevice");
                    ur.Add(PyDict.Of("role", ro is not null ? Name(ro, "IEditableRoleAttributes") : null, "device", dev is not null ? p.Name(dev) : null));
                }
                var ua = AttrsOf(u).Set("IUserExtendedAttributes");
                users.Add(PyDict.Of("name", Name(u, "IUserAttributes"), "objectType", p.TypeName(u),
                    "alias", Py.Or(ua.Get("Alias"), null), "sessionTimeout", Timeout(u), "roles", ur, "key", u));
            }
        }
        return PyDict.Of("users", Py.Sorted(users, x => Py.Or(x["name"], "")).Cast<object?>().ToList(),
            "roles", Py.Sorted(roles, x => Py.Or(x["name"], "")).Cast<object?>().ToList());
    }

    // --- text output ----------------------------------------------------------------------------------
    /// <summary>Text tree of one station's hardware (only the main fields).</summary>
    public static List<string> TextLines(PyDict hw, int indent)
    {
        var o = new List<string>();
        var pad = new string(' ', indent);
        void Walk(PyDict it, int depth)
        {
            var pp = pad + new string(' ', 2 * depth);
            var head = $"{pp}[{Py.Str(it["positionNumber"])}] {Py.Str(it["name"])}";
            var extra = string.Join(" ", new[] { it["typeName"], it["orderNumber"], it["firmware"] }.Where(Py.Bool).Select(Py.Str));
            o.Add(head + (extra.Length > 0 ? "  " + extra : ""));
            foreach (PyDict ad in (List<object?>)it["addresses"]!)
                if (ad.ContainsKey("ioType"))
                    o.Add($"{pp}  {Py.Str(ad["ioType"])} {Py.Long(ad["startAddress"])}.{Py.Long(ad["startBit"])}, {Py.Long(ad["lengthBits"])} bit, process image {Py.Long(ad["processImage"])}");
            foreach (var x in (List<object?>)it["partnerPorts"]!)
                o.Add($"{pp}  connected to port {Py.Str(x)}");
            foreach (PyDict n in (List<object?>)it["nodes"]!)
                o.Add($"{pp}  node {Py.Str(n["name"])} {Py.Str(Py.Or(n["ipAddress"], ""))} {Py.Str(Py.Or(n["subnetMask"], ""))} {Py.Str(Py.Or(n["profinetDeviceName"], ""))}{(Py.Bool(n["subnet"]) ? "  subnet " + Py.Str(n["subnet"]) : "")}{(n.ContainsKey("ipProtocolSelection") ? "  ip " + Py.Str(n["ipProtocolSelection"]) : "")}");
            foreach (var c in ((List<object?>)it["items"]!).Cast<PyDict>())
                Walk(c, depth + 1);
        }
        foreach (var it in ((List<object?>)hw["items"]!).Cast<PyDict>())
            Walk(it, 0);
        return o;
    }

    public static List<string> NetworkText(PyDict net, int indent)
    {
        var pad = new string(' ', indent);
        var o = new List<string>();
        static string J(object? l) => string.Join(", ", ((List<object?>)l!).Select(Py.Str));
        foreach (PyDict s in (List<object?>)net["subnets"]!)
            o.Add($"{pad}Subnet {Py.Str(s["name"])} ({Py.Str(s["catalog"])}): {J(s["nodes"])}");
        foreach (PyDict d in (List<object?>)net["syncDomains"]!)
            o.Add($"{pad}Domain {Py.Str(d["name"])} ({Py.Str(d["catalog"])}): {J(d["participants"])}");
        foreach (List<object?> ab in (List<object?>)net["portLinks"]!)
            o.Add($"{pad}Port link {Py.Str(ab[0])} <-> {Py.Str(ab[1])}");
        foreach (PyDict c in (List<object?>)net["connections"]!)
            o.Add($"{pad}Connection {Py.Str(c["name"])} ({Py.Str(c["kind"])}): {J(c["nodes"])} -> {J(c["partnerNodes"])}");
        return o;
    }
}
