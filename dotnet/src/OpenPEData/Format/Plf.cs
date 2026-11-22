using System.Security.Cryptography;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>One record of System\PEData.plf (append-only record log).
/// File = 64-byte header + 1 byte, SHA-256 over those 65 bytes, 0xFF (98 bytes), then records to EOF:
/// [u32 len][len-4 bytes, last byte 0xFF][SHA-256 over the len bytes]. Every record starts with a 12-byte key:
/// u16 typeLo, u16 typeHi, u64 object id. The current state of an object is the last record with its key (log order).</summary>
internal sealed class PlfRecord
{
    public readonly int Offset;
    public readonly int Length;
    public readonly bool HashOk;
    public readonly ushort TypeLo, TypeHi;
    public readonly ulong Oid;
    public readonly byte[] Data;
    public readonly string Key;

    public PlfRecord(int offset, int length, bool hashOk, byte[] data)
    {
        Offset = offset;
        Length = length;
        HashOk = hashOk;
        Data = data;
        TypeLo = Bin.U16(data, 4);
        TypeHi = Bin.U16(data, 6);
        Oid = Bin.U64(data, 8);
        Key = Plf.MakeKey(TypeLo, TypeHi, Oid);
    }

    /// <summary>Metamodel id of the record's class: (typeHi &lt;&lt; 16) | typeLo.</summary>
    public uint TypeId => ((uint)TypeHi << 16) | TypeLo;

    /// <summary>Store bookkeeping records (type hi 0x0007: metamodel, commit, close, ...).</summary>
    public bool IsStore => TypeHi == 0x0007;
}

internal static class Plf
{
    public const int RecordsStart = 98;

    public static string MakeKey(uint lo, uint hi, ulong oid) => $"{lo:x4}:{hi:x4}:{oid:x}";

    public static string MakeKey(uint typeId, ulong oid) => MakeKey(typeId & 0xFFFF, typeId >> 16, oid);

    public static List<PlfRecord> ReadRecords(byte[] buf, bool verify = true)
    {
        var recs = new List<PlfRecord>();
        var o = RecordsStart;
        var n = buf.Length;
        while (o + 4 <= n)
        {
            var ln = Bin.U32(buf, o);
            if (ln < 17 || (long)o + ln + 32 > n)
                break;
            var data = buf.AsSpan(o, (int)ln).ToArray();
            var ok = !verify || SHA256.HashData(data).AsSpan().SequenceEqual(buf.AsSpan(o + (int)ln, 32));
            recs.Add(new PlfRecord(o, (int)ln, ok, data));
            o += (int)ln + 32;
        }
        return recs;
    }

    /// <summary>key -> last record with that key, in log order of first appearance.</summary>
    public static OrderedDictionary<string, PlfRecord> Latest(IEnumerable<PlfRecord> recs)
    {
        var d = new OrderedDictionary<string, PlfRecord>();
        foreach (var r in recs)
            d[r.Key] = r;
        return d;
    }
}
