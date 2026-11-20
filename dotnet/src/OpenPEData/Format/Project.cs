using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>Attribute sets of one object: set short name -> values (insertion ordered).</summary>
internal sealed class AttrSets : OrderedDictionary<string, PyDict>
{
    /// <summary>attributes.get(name, {}): an empty dict when the set is absent.</summary>
    public PyDict Set(string name) => TryGetValue(name, out var v) ? v : new PyDict();

    /// <summary>attributes.get(name) (null when absent).</summary>
    public PyDict? SetOrNull(string name) => TryGetValue(name, out var v) ? v : null;
}

/// <summary>Files of a project folder, .ap21 or .zap21 / .zal21 archive.</summary>
internal static class ProjectFiles
{
    static ProjectFiles() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Entry names as Python's zipfile decodes them (cp437 unless the UTF-8 flag is set). If the file is no
    /// zip archive, the file stream is closed before the exception leaves (ZipArchive does not close it then).</summary>
    public static ZipArchive OpenZip(string path)
    {
        var stream = File.OpenRead(path);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.GetEncoding(437));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>The bytes of an entry; more than ReadLimits.MaxEntryBytes (stated or actual size) is InvalidDataException.</summary>
    public static byte[] ReadEntry(ZipArchiveEntry e)
    {
        var max = ReadLimits.Current.MaxEntryBytes;
        if (e.Length > max)
            throw new InvalidDataException($"archive entry {e.FullName} is larger than the limit of {max} bytes");
        using var s = e.Open();
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        int n;
        while ((n = s.Read(buf, 0, buf.Length)) > 0)
        {
            if (ms.Length + n > max)
                throw new InvalidDataException($"archive entry {e.FullName} is larger than the limit of {max} bytes");
            ms.Write(buf, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>zipfile.is_zipfile.</summary>
    public static bool IsZip(string path)
    {
        try
        {
            using var z = OpenZip(path);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>os.path.dirname(os.path.abspath(path)).</summary>
    public static string DirOf(string path) => Path.GetDirectoryName(Path.GetFullPath(path)) ?? path;

    static readonly Regex ProjectFileRe = new(@"\.ap[0-9]{2}\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    static readonly Regex ProjectArchiveRe = new(@"\.zap[0-9]{2}\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Project file of a project folder (.ap21; .apNN of any TIA version, see TiaVersion).</summary>
    public static bool IsProjectFile(string path) => ProjectFileRe.IsMatch(path);

    /// <summary>Project archive (.zap21; .zapNN of any TIA version).</summary>
    public static bool IsProjectArchive(string path) => ProjectArchiveRe.IsMatch(path);

    static readonly Regex PartRe = new(@"\Asystem/pedata-(\d+)\.plf\z", RegexOptions.CultureInvariant);

    /// <summary>PEData.plf plus continuation files PEData-01.plf, ... in reading order.</summary>
    public static List<string> PlfParts(IEnumerable<string> names)
    {
        static string Norm(string n) => n.Replace("\\", "/").ToLowerInvariant();
        var l = names.ToList();
        var bas = l.Where(n => Norm(n) == "system/pedata.plf").ToList();
        var more = Py.Sorted(l.Where(n => PartRe.IsMatch(Norm(n))), n => long.Parse(PartRe.Match(Norm(n)).Groups[1].Value));
        return [.. bas, .. more];
    }

    /// <summary>[(name, bytes)] of the plf file(s) of a project folder, .ap21 or .zap21.</summary>
    public static List<(string Name, byte[] Data)> ReadPlf(string path)
    {
        if (IsProjectFile(path))
            path = DirOf(path);
        if (Directory.Exists(path))
        {
            var sysdir = Path.Combine(path, "System");
            var names = PlfParts(Directory.EnumerateFileSystemEntries(sysdir).Select(f => "System\\" + Path.GetFileName(f)));
            return names.Select(n => (n, File.ReadAllBytes(Path.Combine(path, n)))).ToList();
        }
        using var z = OpenZip(path);
        var byName = new Dictionary<string, ZipArchiveEntry>();
        foreach (var e in z.Entries)
            byName[e.FullName] = e;
        return PlfParts(z.Entries.Select(e => e.FullName)).Select(n => (n, ReadEntry(byName[n]))).ToList();
    }

    /// <summary>Bytes of a member (e.g. "System/PEData.idx") in a project folder, next to an .ap21 or in a .zap21; null if
    /// absent.</summary>
    public static byte[]? Member(string path, string name)
    {
        if (IsProjectArchive(path))
        {
            using var z = OpenZip(path);
            var e = z.Entries.LastOrDefault(x => x.FullName == name);
            return e is null ? null : ReadEntry(e);
        }
        if (IsProjectFile(path))
            path = DirOf(path);
        var p = Path.Combine([path, .. name.Split('/')]);
        return File.Exists(p) ? File.ReadAllBytes(p) : null;
    }
}

/// <summary>Loads a TIA Portal V21 project (project folder, .ap21 file or .zap21 archive) into an object store view.</summary>
internal sealed class Project
{
    public readonly string Path;
    public readonly List<string> PlfFiles;
    public readonly byte[] PlfBytes;
    public readonly List<PlfRecord> Records = [];
    public readonly OrderedDictionary<string, PlfRecord> Latest;
    public readonly Metamodel Mm;
    /// <summary>key -> ObjectRecord (domain objects, current version)</summary>
    public readonly OrderedDictionary<string, ObjectRecord> Objects = [];
    /// <summary>keys whose current version is a tombstone</summary>
    public readonly List<string> Deleted = [];
    /// <summary>object type id -> {expando id: (name, type code)}</summary>
    public readonly Dictionary<uint, Dictionary<uint, (string Name, long Code)>> ExpandoDicts = [];
    readonly Dictionary<string, (AttrSets, PyDict)> attrs = [];
    readonly Dictionary<(uint, string), bool> isA = [];

    /// <summary>What differs between TIA versions (metamodel record, ...); V21 unless TiaProject chose another.</summary>
    public readonly VersionProfile Profile;

    public Project(string path, bool verify = true, VersionProfile? profile = null)
    {
        Path = path;
        Profile = profile ?? VersionProfile.V21;
        var parts = ProjectFiles.ReadPlf(path);
        PlfFiles = parts.Select(p => p.Name).ToList();
        if (parts.Count == 0)
            throw new PyIndexError("list index out of range");
        PlfBytes = parts[0].Data;
        foreach (var (_, b) in parts)
            Records.AddRange(Plf.ReadRecords(b, verify));
        Latest = Plf.Latest(Records);
        var mmRec = Latest.GetValueOrDefault(Profile.MetamodelKey) ?? Latest.Values.FirstOrDefault(r => r.TypeId == Profile.MetamodelTypeId)
            ?? throw new PyStopIteration();
        Mm = Metamodel.FromRecord(mmRec.Data);
        foreach (var (k, r) in Latest)
        {
            if (r.IsStore || !Mm.Types.ContainsKey(r.TypeId))
                continue;
            if (ObjectRecord.IsTombstone(r))
                Deleted.Add(k);
            else
                Objects[k] = new ObjectRecord(r, Mm);
        }
        foreach (var r in Latest.Values)
        {
            if (r.TypeId == 0x00070011)
            {
                var (tid, d) = Attrs.ReadDictionary(r.Data);
                ExpandoDicts[tid] = d;
            }
        }
    }

    ObjectRecord Obj(string? key) => key is not null && Objects.TryGetValue(key, out var o) ? o : throw new PyKeyError(Py.Repr(key));

    // --- attributes -------------------------------------------------------------------------------
    /// <summary>{set short name: {attr: value}} for all decodable attribute sets of an object, plus the problems per set.</summary>
    public (AttrSets Sets, PyDict Problems) Attributes(string? key)
    {
        if (key is not null && attrs.TryGetValue(key, out var c))
            return c;
        var o = Obj(key);
        var res = new AttrSets();
        var problems = new PyDict();
        for (var i = 0; i < (o.LayoutOk ? o.NSets : 0); i++)
        {
            if (!o.Blocks.TryGetValue(i, out var be))
                continue;
            var (a, e) = be;
            var block = Py.Slice(o.Data, a, e);
            PyDict vals;
            List<string> p;
            if (o.IsExpando(i) && Mm.ByName[o.SetNames[i]].Expando == "simple")
                (vals, p) = Attrs.DecodeSimpleExpando(block);
            else if (o.IsExpando(i))
                (vals, p) = Attrs.DecodeExpando(block, ExpandoDicts);
            else
                (vals, p) = Attrs.DecodeSet(Mm, o.Rec.TypeId, o.SetNames[i], block);
            foreach (var (n, v) in vals.Items.ToList())
                if (v is byte[] b && b.Length > 0 && b[0] == 4 && Attrs.ExternalRefs(b) is not null)
                    vals[n] = Attrs.BindStore(b, StoreData);
            res[o.SetName(i)] = vals;
            if (p.Count > 0)
                problems[o.SetName(i)] = p.Cast<object?>().ToList();
        }
        attrs[key!] = (res, problems);
        return (res, problems);
    }

    public AttrSets Sets(string? key) => Attributes(key).Sets;

    /// <summary>Bytes of the current version of a store record (e.g. the pages of a large blob), or null.</summary>
    public byte[]? StoreData(string key) => Latest.TryGetValue(key, out var r) && r.IsStore ? r.Data : null;

    public byte[]? DecodeBlob(byte[]? raw) => Attrs.DecodeBlob(raw, StoreData);

    public object? Attr(string? key, string setName, string name, object? dflt = null) =>
        Sets(key).TryGetValue(setName, out var s) ? s.Get(name, dflt) : dflt;

    public string? Name(string? key) => Attr(key, "ICoreAttributes", "Name") as string;

    // --- types and links --------------------------------------------------------------------------
    public string? TypeName(string? key) => Mm.ShortName(Obj(key).Rec.TypeId);

    /// <summary>type_name: short or full metamodel type name.</summary>
    public bool IsA(string? key, string typeName)
    {
        if (key is null || !Objects.TryGetValue(key, out var o))
            return false;
        var ck = (o.Rec.TypeId, typeName);
        if (!isA.TryGetValue(ck, out var r))
        {
            var anc = Mm.Ancestors(Mm.Types[o.Rec.TypeId].FullName);
            isA[ck] = r = anc.Any(a => a == typeName || a.EndsWith("." + typeName, StringComparison.Ordinal));
        }
        return r;
    }

    /// <summary>Outgoing links (tagged ones carry the relation id). relation: relation name filter.</summary>
    public List<Link> Links(string? key, string? relation = null)
    {
        if (key is null || !Objects.TryGetValue(key, out var o))
            return [];
        if (relation is null)
            return [.. o.Links];
        var outl = new List<Link>();
        foreach (var l in o.Links)
        {
            var name = l.Relation is null ? SlotRelation(key, o, l.Slot)
                : Mm.Relations.TryGetValue(l.Relation.Value, out var r) ? r.Name : null;
            if (name == relation)
                outl.Add(l);
        }
        return outl;
    }

    readonly Dictionary<(string, int?), string?> slotRelation = [];

    /// <summary>Relation of a typed link list (long lists, e.g. UsedParts of an FB interface with many multi-instances,
    /// sample 066): the slot carries no relation id, so it is taken from the target side, which links back with the
    /// inverse relation as a tagged link.</summary>
    string? SlotRelation(string key, ObjectRecord o, int? slot)
    {
        if (slotRelation.TryGetValue((key, slot), out var name))
            return name;
        name = null;
        foreach (var l in o.Links)
        {
            if (l.Relation is not null || l.Slot != slot || !Objects.TryGetValue(l.Key, out var t))
                continue;
            foreach (var b in t.Links)
                if (b.Relation is { } br && b.Key == key && Mm.Relations.TryGetValue(br, out var r))
                {
                    name = r.InverseId is { } ii && Mm.Relations.TryGetValue(ii, out var inv) ? inv.Name : r.InverseRef;
                    if (name is not null)
                        break;
                }
            if (name is not null)
                break;
        }
        return slotRelation[(key, slot)] = name;
    }

    public List<string> Targets(string? key, string? relation = null) =>
        Links(key, relation).Select(l => l.Key).Where(Objects.ContainsKey).ToList();

    public List<string> OfType(string typeName) => Objects.Keys.Where(k => IsA(k, typeName)).ToList();
}
