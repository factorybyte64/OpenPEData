namespace OpenPEData.Runtime;

/// <summary>zlib stream decoder (RFC 1950/1951) with the behaviour of Python's zlib module: decompress() raises zlib.error
/// with zlib's messages on damaged or truncated data and ignores bytes behind the stream; DecompressObj() returns what a
/// zlib.decompressobj() gives for one decompress() call (partial output for truncated data, the unused data).</summary>
internal static class Zlib
{
    sealed class Truncated : Exception;

    static ZlibError Err(string msg) => new("Error -3 while decompressing data: " + msg);

    /// <summary>zlib.decompress(data[start:start+len]).</summary>
    public static byte[] Decompress(byte[] data, int start = 0, int len = -1)
    {
        if (len < 0)
            len = data.Length - start;
        start = Math.Clamp(start, 0, data.Length);
        len = Math.Clamp(len, 0, data.Length - start);
        var st = new State(data, start, start + len);
        try
        {
            st.Run();
        }
        catch (Truncated)
        {
            throw new ZlibError("Error -5 while decompressing data: incomplete or truncated stream");
        }
        return st.Output();
    }

    public static byte[] Decompress(byte[] data) => Decompress(data, 0, data.Length);

    /// <summary>One decompressobj().decompress(data[start:]) call: (output, eof, offset behind the stream).</summary>
    public static (byte[] Output, bool Eof, int End) DecompressObj(byte[] data, int start)
    {
        var st = new State(data, start, data.Length);
        try
        {
            st.Run();
            return (st.Output(), true, st.Pos);
        }
        catch (Truncated)
        {
            return (st.Output(), false, data.Length);
        }
    }

    sealed class Huffman
    {
        public readonly short[] Count = new short[16];
        public readonly short[] Symbol;
        public readonly int[] Fast = new int[1 << FastBits]; // (symbol << 4) | length, 0 = slow path
        public const int FastBits = 9;

        public Huffman(int n) => Symbol = new short[n];

        /// <summary>Builds the canonical code; returns left (0 = complete, &gt;0 incomplete, &lt;0 over-subscribed) and
        /// the longest code length.</summary>
        public (int Left, int Max) Build(byte[] lengths, int off, int n)
        {
            Array.Clear(Count);
            Array.Clear(Fast);
            for (var s = 0; s < n; s++)
                Count[lengths[off + s]]++;
            var max = 15;
            while (max > 0 && Count[max] == 0)
                max--;
            if (Count[0] == n)
                return (0, 0);
            var left = 1;
            for (var len = 1; len <= 15; len++)
            {
                left <<= 1;
                left -= Count[len];
                if (left < 0)
                    return (left, max);
            }
            var offs = new short[16];
            for (var len = 1; len < 15; len++)
                offs[len + 1] = (short)(offs[len] + Count[len]);
            for (var s = 0; s < n; s++)
                if (lengths[off + s] != 0)
                    Symbol[offs[lengths[off + s]]++] = (short)s;
            // fast table: canonical codes in order, bit reversed
            var code = 0;
            var idx = 0;
            for (var len = 1; len <= FastBits; len++)
            {
                for (var i = 0; i < Count[len]; i++)
                {
                    var sym = Symbol[idx++];
                    var rev = 0;
                    for (var b = 0; b < len; b++)
                        rev |= ((code >> b) & 1) << (len - 1 - b);
                    for (var j = rev; j < (1 << FastBits); j += 1 << len)
                        Fast[j] = (sym << 4) | len;
                    code++;
                }
                code <<= 1;
            }
            return (left, max);
        }
    }

    static readonly short[] LBase = [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    static readonly short[] LExt = [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];
    static readonly short[] DBase = [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];
    static readonly short[] DExt = [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];
    static readonly byte[] Order = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    sealed class State(byte[] data, int start, int end)
    {
        public int Pos = start;
        ulong bitbuf;
        int bitcnt;
        byte[] outBuf = new byte[Math.Max(64, (end - start) * 4)];
        int outLen;
        readonly long maxOut = ReadLimits.Current.MaxDecompressedBytes;

        /// <summary>Grows the output buffer to hold n more bytes; beyond ReadLimits.MaxDecompressedBytes a zlib.error
        /// (a few bytes of input can expand to gigabytes).</summary>
        void Grow(int n)
        {
            if (outLen + (long)n > maxOut)
                throw new ZlibError($"decompressed data exceeds the limit of {maxOut} bytes");
            Array.Resize(ref outBuf, (int)Math.Min(Math.Max((long)outBuf.Length * 2, outLen + (long)n), Math.Min(maxOut, Array.MaxLength)));
        }
        readonly Huffman lit = new(288), dist = new(30), lens = new(19);

        public byte[] Output() => outBuf.AsSpan(0, outLen).ToArray();

        void Need(int n)
        {
            while (bitcnt < n)
            {
                if (Pos >= end)
                    throw new Truncated();
                bitbuf |= (ulong)data[Pos++] << bitcnt;
                bitcnt += 8;
            }
        }

        int Bits(int n)
        {
            if (n == 0)
                return 0;
            Need(n);
            var v = (int)(bitbuf & ((1UL << n) - 1));
            bitbuf >>= n;
            bitcnt -= n;
            return v;
        }

        void Put(byte b)
        {
            if (outLen == outBuf.Length)
                Grow(1);
            outBuf[outLen++] = b;
        }

        int Decode(Huffman h)
        {
            // fill up to FastBits when the input allows it
            while (bitcnt < Huffman.FastBits && Pos < end)
            {
                bitbuf |= (ulong)data[Pos++] << bitcnt;
                bitcnt += 8;
            }
            if (bitcnt >= Huffman.FastBits)
            {
                var e = h.Fast[(int)(bitbuf & ((1 << Huffman.FastBits) - 1))];
                if (e != 0)
                {
                    var l = e & 15;
                    bitbuf >>= l;
                    bitcnt -= l;
                    return e >> 4;
                }
            }
            // slow canonical decode (puff)
            int code = 0, first = 0, index = 0;
            for (var len = 1; len <= 15; len++)
            {
                code |= Bits(1);
                var count = h.Count[len];
                if (code - count < first)
                    return h.Symbol[index + (code - first)];
                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
            }
            return -1; // ran out of codes
        }

        public void Run()
        {
            // zlib header
            var cmf = Bits(8);
            var flg = Bits(8);
            if ((cmf * 256 + flg) % 31 != 0)
                throw Err("incorrect header check");
            if ((cmf & 15) != 8)
                throw Err("unknown compression method");
            if ((cmf >> 4) + 8 > 15)
                throw Err("invalid window size");
            if ((flg & 0x20) != 0)
                throw new ZlibError("Error 2 while decompressing data");
            int last;
            do
            {
                last = Bits(1);
                var type = Bits(2);
                if (type == 0)
                    Stored();
                else if (type == 1)
                    Fixed();
                else if (type == 2)
                    Dynamic();
                else
                    throw Err("invalid block type");
            } while (last == 0);
            // adler32, byte aligned
            bitbuf >>= bitcnt & 7;
            bitcnt -= bitcnt & 7;
            uint want = 0;
            for (var i = 0; i < 4; i++)
                want = (want << 8) | (uint)Bits(8);
            // give back whole bytes still in the bit buffer
            Pos -= bitcnt / 8;
            bitcnt = 0;
            if (want != Adler32(outBuf, outLen))
                throw Err("incorrect data check");
        }

        void Stored()
        {
            bitbuf >>= bitcnt & 7;
            bitcnt -= bitcnt & 7;
            var len = Bits(16);
            var nlen = Bits(16);
            if (len != (~nlen & 0xFFFF))
                throw Err("invalid stored block lengths");
            for (var i = 0; i < len; i++)
                Put((byte)Bits(8));
        }

        static readonly byte[] FixedLengths = MakeFixed();

        static byte[] MakeFixed()
        {
            var l = new byte[288 + 30];
            for (var s = 0; s < 144; s++) l[s] = 8;
            for (var s = 144; s < 256; s++) l[s] = 9;
            for (var s = 256; s < 280; s++) l[s] = 7;
            for (var s = 280; s < 288; s++) l[s] = 8;
            for (var s = 0; s < 30; s++) l[288 + s] = 5;
            return l;
        }

        void Fixed()
        {
            lit.Build(FixedLengths, 0, 288);
            dist.Build(FixedLengths, 288, 30);
            Codes();
        }

        void Dynamic()
        {
            var nlen = Bits(5) + 257;
            var ndist = Bits(5) + 1;
            var ncode = Bits(4) + 4;
            if (nlen > 286 || ndist > 30)
                throw Err("too many length or distance symbols");
            var lengths = new byte[320];
            for (var i = 0; i < ncode; i++)
                lengths[Order[i]] = (byte)Bits(3);
            var (left, _) = lens.Build(lengths, 0, 19);
            if (left != 0)
                throw Err("invalid code lengths set");
            Array.Clear(lengths);
            var idx = 0;
            while (idx < nlen + ndist)
            {
                var sym = Decode(lens);
                if (sym < 0)
                    throw Err("invalid code lengths set");
                if (sym < 16)
                    lengths[idx++] = (byte)sym;
                else
                {
                    byte len = 0;
                    int rep;
                    if (sym == 16)
                    {
                        if (idx == 0)
                            throw Err("invalid bit length repeat");
                        len = lengths[idx - 1];
                        rep = 3 + Bits(2);
                    }
                    else if (sym == 17)
                        rep = 3 + Bits(3);
                    else
                        rep = 11 + Bits(7);
                    if (idx + rep > nlen + ndist)
                        throw Err("invalid bit length repeat");
                    while (rep-- > 0)
                        lengths[idx++] = len;
                }
            }
            if (lengths[256] == 0)
                throw Err("invalid code -- missing end-of-block");
            var (l1, m1) = lit.Build(lengths, 0, nlen);
            if (l1 < 0 || (l1 > 0 && m1 != 1))
                throw Err("invalid literal/lengths set");
            var dl = new byte[30];
            Array.Copy(lengths, nlen, dl, 0, ndist);
            var (l2, m2) = dist.Build(dl, 0, ndist);
            if (l2 < 0 || (l2 > 0 && m2 != 1))
                throw Err("invalid distances set");
            Codes();
        }

        void Codes()
        {
            while (true)
            {
                var sym = Decode(lit);
                if (sym < 0)
                    throw Err("invalid literal/length code");
                if (sym < 256)
                {
                    Put((byte)sym);
                    continue;
                }
                if (sym == 256)
                    return;
                sym -= 257;
                if (sym >= 29)
                    throw Err("invalid literal/length code");
                var len = LBase[sym] + Bits(LExt[sym]);
                var ds = Decode(dist);
                if (ds < 0 || ds >= 30)
                    throw Err("invalid distance code");
                var d = DBase[ds] + Bits(DExt[ds]);
                if (d > outLen)
                    throw Err("invalid distance too far back");
                if (outLen + len > outBuf.Length)
                    Grow(len);
                for (var i = 0; i < len; i++)
                {
                    outBuf[outLen] = outBuf[outLen - d];
                    outLen++;
                }
            }
        }
    }

    public static uint Adler32(byte[] b, int n)
    {
        uint a = 1, s = 0;
        var i = 0;
        while (i < n)
        {
            var k = Math.Min(n - i, 5552);
            for (var j = 0; j < k; j++)
            {
                a += b[i + j];
                s += a;
            }
            a %= 65521;
            s %= 65521;
            i += k;
        }
        return (s << 16) | a;
    }
}
