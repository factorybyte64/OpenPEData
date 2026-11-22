using System.Buffers.Binary;

namespace OpenPEData.Runtime;

/// <summary>struct.unpack_from for little endian fields; raises StructError when the buffer is too short.</summary>
internal static class Bin
{
    static int Off(byte[] b, long o, int size)
    {
        if (o < 0)
            o += b.Length;
        if (o < 0 || o + size > b.Length)
            throw new StructError($"unpack_from requires a buffer of at least {Math.Max(0, o) + size} bytes for unpacking {size} bytes at offset {o} (actual buffer size is {b.Length})");
        return (int)o;
    }

    public static byte U8(byte[] b, long o) => b[Off(b, o, 1)];

    public static sbyte I8(byte[] b, long o) => (sbyte)b[Off(b, o, 1)];

    public static ushort U16(byte[] b, long o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(Off(b, o, 2)));

    public static short I16(byte[] b, long o) => BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(Off(b, o, 2)));

    public static uint U32(byte[] b, long o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(Off(b, o, 4)));

    public static int I32(byte[] b, long o) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(Off(b, o, 4)));

    public static ulong U64(byte[] b, long o) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(Off(b, o, 8)));

    public static long I64(byte[] b, long o) => BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(Off(b, o, 8)));

    public static float F32(byte[] b, long o) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(Off(b, o, 4)));

    public static double F64(byte[] b, long o) => BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(Off(b, o, 8)));

    /// <summary>"&lt;nI" at o.</summary>
    public static uint[] U32s(byte[] b, long o, long n)
    {
        if (n < 0)
            throw new StructError("count < 0");
        var start = Off(b, o, (int)Math.Min(n * 4, int.MaxValue));
        var r = new uint[n];
        for (var i = 0; i < n; i++)
            r[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(start + 4 * i));
        return r;
    }

    public static ushort[] U16s(byte[] b, long o, long n)
    {
        var start = Off(b, o, (int)Math.Min(n * 2, int.MaxValue));
        var r = new ushort[n];
        for (var i = 0; i < n; i++)
            r[i] = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(start + 2 * i));
        return r;
    }

    /// <summary>int.from_bytes(b[a:e], "little") (slice semantics: never raises).</summary>
    public static ulong FromLe(byte[] b, long a, long e)
    {
        var s = Py.Slice(b, a, e);
        ulong v = 0;
        for (var i = s.Length - 1; i >= 0; i--)
            v = (v << 8) | s[i];
        return v;
    }

    /// <summary>int.from_bytes(b[a:e], "big").</summary>
    public static ulong FromBe(byte[] b, long a, long e)
    {
        var s = Py.Slice(b, a, e);
        ulong v = 0;
        foreach (var x in s)
            v = (v << 8) | x;
        return v;
    }

    /// <summary>struct.unpack("&lt;i", struct.pack("&lt;I", v)).</summary>
    public static int AsI32(uint v) => unchecked((int)v);

    public static float AsF32(uint v) => BitConverter.Int32BitsToSingle(unchecked((int)v));
}
