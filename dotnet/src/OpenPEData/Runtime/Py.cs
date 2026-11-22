using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenPEData.Runtime;

/// <summary>Python semantics the port relies on: truthiness, equality and ordering of mixed values, str()/repr(), slicing,
/// text decoding with the same error handling. Values are object?: null (None), bool, long/ulong (int), double (float),
/// string, byte[] (bytes; a blob bound to its record store is a byte[] as well, see Attrs.BindStore), PyDict, List&lt;object?&gt;, PyTuple.</summary>
internal static class Py
{
    // --- truthiness, equality, ordering ----------------------------------------------------------
    public static bool Bool(object? o) => o switch
    {
        null => false,
        bool b => b,
        long l => l != 0,
        int i => i != 0,
        ulong u => u != 0,
        double d => d != 0,
        BigInteger bi => !bi.IsZero,
        string s => s.Length > 0,
        byte[] a => a.Length > 0,
        PyDict d => d.Count > 0,
        PyTuple t => t.Count > 0,
        ICollection c => c.Count > 0,
        _ => true,
    };

    /// <summary>Python "a or b" (b evaluated lazily).</summary>
    public static object? OrElse(object? a, Func<object?> b) => Bool(a) ? a : b();

    public static object? Or(object? a, object? b) => Bool(a) ? a : b;

    public static bool IsNumber(object? o) => o is bool or long or int or ulong or double or BigInteger;

    /// <summary>isinstance(o, int): bool counts as int, as in Python.</summary>
    public static bool IsInt(object? o) => o is bool or long or int or ulong or BigInteger;

    public static bool IsBytes(object? o) => o is byte[];

    /// <summary>The bytes of a bytes value, else null.</summary>
    public static byte[]? Bytes(object? o) => o as byte[];

    public static BigInteger Big(object? o) => o switch
    {
        bool b => b ? 1 : 0,
        long l => l,
        int i => i,
        ulong u => u,
        BigInteger bi => bi,
        _ => throw new PyTypeError("not an int: " + TypeName(o)),
    };

    public static long Long(object? o) => o switch
    {
        bool b => b ? 1 : 0,
        long l => l,
        int i => i,
        ulong u => (long)u,
        BigInteger bi => (long)bi,
        _ => throw new PyTypeError("not an int: " + TypeName(o)),
    };

    public static double Dbl(object? o) => o switch
    {
        double d => d,
        _ => (double)Big(o),
    };

    /// <summary>An int as the narrowest boxed type: long, else ulong / BigInteger.</summary>
    public static object Int(BigInteger v) => v >= long.MinValue && v <= long.MaxValue ? (long)v : v >= 0 && v <= ulong.MaxValue ? (ulong)v : v;

    public static object Int(ulong v) => v <= long.MaxValue ? (long)v : v;

    public static string TypeName(object? o) => o switch
    {
        null => "NoneType",
        bool => "bool",
        long or int or ulong or BigInteger => "int",
        double => "float",
        string => "str",
        byte[] => "bytes",
        PyDict => "dict",
        PyTuple => "tuple",
        IList => "list",
        _ => o.GetType().Name,
    };

    public static bool Eq(object? a, object? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (IsNumber(a) && IsNumber(b))
        {
            if (a is double || b is double)
                return Dbl(a) == Dbl(b);
            return Big(a) == Big(b);
        }
        if (a is string sa && b is string sb)
            return sa == sb;
        if (IsBytes(a) && IsBytes(b))
            return Bytes(a)!.AsSpan().SequenceEqual(Bytes(b));
        if (a is PyTuple ta && b is PyTuple tb)
            return ta.Equals(tb);
        if (a is IList la && b is IList lb && a is not PyTuple && b is not PyTuple)
        {
            if (la.Count != lb.Count)
                return false;
            for (var i = 0; i < la.Count; i++)
                if (!Eq(la[i], lb[i]))
                    return false;
            return true;
        }
        if (a is PyDict da && b is PyDict db)
        {
            if (da.Count != db.Count)
                return false;
            foreach (var kv in da)
                if (!db.TryGetValue(kv.Key, out var v) || !Eq(kv.Value, v))
                    return false;
            return true;
        }
        return a.Equals(b);
    }

    /// <summary>Python ordering (&lt;): numbers, strings by code point, bytes, tuples / lists lexicographically. Other
    /// combinations raise TypeError like Python.</summary>
    public static int Compare(object? a, object? b)
    {
        if (IsNumber(a) && IsNumber(b))
        {
            if (a is double || b is double)
                return Dbl(a).CompareTo(Dbl(b));
            return Big(a).CompareTo(Big(b));
        }
        if (a is string sa && b is string sb)
            return CompareStr(sa, sb);
        if (IsBytes(a) && IsBytes(b))
            return Bytes(a)!.AsSpan().SequenceCompareTo(Bytes(b));
        if ((a is PyTuple && b is PyTuple) || (a is IList && b is IList && a is not PyTuple && b is not PyTuple))
        {
            var la = Seq(a);
            var lb = Seq(b);
            var n = Math.Min(la.Count, lb.Count);
            for (var i = 0; i < n; i++)
                if (!Eq(la[i], lb[i]))
                    return Compare(la[i], lb[i]);
            return la.Count.CompareTo(lb.Count);
        }
        throw new PyTypeError($"'<' not supported between instances of '{TypeName(a)}' and '{TypeName(b)}'");
    }

    static IList<object?> Seq(object? o) => o is PyTuple t ? t.Items : ((IList)o!).Cast<object?>().ToList();

    /// <summary>Code point order (UTF-16 ordinal order differs for supplementary characters).</summary>
    public static int CompareStr(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var x = a[i];
            var y = b[i];
            if (x == y)
                continue;
            if (char.IsSurrogate(x) || char.IsSurrogate(y))
                return Rune.GetRuneAt(a, i).Value.CompareTo(Rune.GetRuneAt(b, i).Value);
            return x.CompareTo(y);
        }
        return a.Length.CompareTo(b.Length);
    }

    public sealed class Comparer : IComparer<object?>
    {
        public static readonly Comparer Instance = new();

        public int Compare(object? x, object? y) => Py.Compare(x, y);
    }

    public sealed class StrComparer : IComparer<string>
    {
        public static readonly StrComparer Instance = new();

        public int Compare(string? x, string? y) => CompareStr(x!, y!);
    }

    /// <summary>sorted(src, key=key, reverse=reverse): stable.</summary>
    public static List<T> Sorted<T>(IEnumerable<T> src, Func<T, object?> key, bool reverse = false)
    {
        var items = src.Select((x, i) => (x, k: key(x), i)).ToList();
        items.Sort((p, q) =>
        {
            var c = reverse ? Compare(q.k, p.k) : Compare(p.k, q.k);
            return c != 0 ? c : p.i.CompareTo(q.i);
        });
        return items.Select(p => p.x).ToList();
    }

    public static List<string> Sorted(IEnumerable<string> src)
    {
        var l = src.ToList();
        var idx = l.Select((s, i) => (s, i)).ToList();
        idx.Sort((p, q) => { var c = CompareStr(p.s, q.s); return c != 0 ? c : p.i.CompareTo(q.i); });
        return idx.Select(p => p.s).ToList();
    }

    /// <summary>max(src, key=key, default=dflt): the first of the largest.</summary>
    public static T? Max<T>(IEnumerable<T> src, Func<T, object?> key, T? dflt = default)
    {
        var found = false;
        T? best = dflt;
        object? bk = null;
        foreach (var x in src)
        {
            var k = key(x);
            if (!found || Compare(k, bk) > 0)
            {
                best = x;
                bk = k;
                found = true;
            }
        }
        return best;
    }

    public static PyTuple T(params object?[] items) => new(items);

    /// <summary>list(dict.fromkeys(src)): first occurrence order without duplicates.</summary>
    public static List<T> Unique<T>(IEnumerable<T> src)
    {
        var seen = new HashSet<T>();
        var r = new List<T>();
        foreach (var x in src)
            if (seen.Add(x))
                r.Add(x);
        return r;
    }

    // --- arithmetic ------------------------------------------------------------------------------------
    public static long FloorDiv(long a, long b)
    {
        var q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0)))
            q--;
        return q;
    }

    public static long Mod(long a, long b)
    {
        var m = a % b;
        if (m != 0 && ((m < 0) != (b < 0)))
            m += b;
        return m;
    }

    /// <summary>round(x, n) as Python: correctly rounded from the exact binary value, ties to even.</summary>
    public static double Round(double x, int n)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0)
            return x;
        // exact value of x as m * 2^e
        var bits = BitConverter.DoubleToInt64Bits(x);
        var neg = bits < 0;
        var exp = (int)((bits >> 52) & 0x7FF);
        var man = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0)
            exp = 1;
        else
            man |= 1L << 52;
        exp -= 1075;
        // scaled = man * 2^exp * 10^n, round half even
        BigInteger num = man, den = 1;
        if (exp > 0)
            num <<= exp;
        else
            den <<= -exp;
        if (n >= 0)
            num *= BigInteger.Pow(10, n);
        else
            den *= BigInteger.Pow(10, -n);
        var q = BigInteger.DivRem(num, den, out var rem);
        var twice = rem * 2;
        if (twice > den || (twice == den && !q.IsEven))
            q += 1;
        var s = q.ToString(CultureInfo.InvariantCulture);
        string txt;
        if (n > 0)
        {
            s = s.PadLeft(n + 1, '0');
            txt = s[..^n] + "." + s[^n..];
        }
        else
            txt = s + new string('0', -n);
        var r = double.Parse(txt, CultureInfo.InvariantCulture);
        return neg ? -r : r;
    }

    // --- str() / repr() ----------------------------------------------------------------------------------
    public static string Str(object? o) => o switch
    {
        null => "None",
        string s => s,
        bool b => b ? "True" : "False",
        long or int or ulong or BigInteger => Convert.ToString(o, CultureInfo.InvariantCulture)!,
        double d => FloatRepr(d),
        _ => Repr(o),
    };

    public static string Repr(object? o) => o switch
    {
        null => "None",
        string s => ReprStr(s),
        bool or long or int or ulong or BigInteger or double => Str(o),
        byte[] a => ReprBytes(a),
        PyDict d => "{" + string.Join(", ", d.Select(kv => Repr(kv.Key) + ": " + Repr(kv.Value))) + "}",
        PyTuple t => t.Count == 1 ? "(" + Repr(t[0]) + ",)" : "(" + string.Join(", ", t.Select(Repr)) + ")",
        IDictionary id => "{" + string.Join(", ", id.Keys.Cast<object>().Select(k => Repr(k) + ": " + Repr(id[k]))) + "}",
        IList l => "[" + string.Join(", ", l.Cast<object?>().Select(Repr)) + "]",
        ISet<object> set => set.Count == 0 ? "set()" : "{" + string.Join(", ", set.Select(Repr)) + "}",
        _ => o.ToString() ?? "",
    };

    public static string FloatRepr(double d)
    {
        if (double.IsNaN(d))
            return "nan";
        if (double.IsPositiveInfinity(d))
            return "inf";
        if (double.IsNegativeInfinity(d))
            return "-inf";
        if (d == 0)
            return BitConverter.DoubleToInt64Bits(d) < 0 ? "-0.0" : "0.0";
        var r = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
        string digits;
        int decpt; // value = 0.digits * 10^decpt
        var ei = r.IndexOfAny(['E', 'e']);
        var mant = ei >= 0 ? r[..ei] : r;
        var e10 = ei >= 0 ? int.Parse(r[(ei + 1)..], CultureInfo.InvariantCulture) : 0;
        var dot = mant.IndexOf('.');
        var intPart = dot >= 0 ? mant[..dot] : mant;
        var frac = dot >= 0 ? mant[(dot + 1)..] : "";
        digits = intPart + frac;
        decpt = intPart.Length + e10;
        var lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0')
            lead++;
        digits = digits[lead..];
        decpt -= lead;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0)
            digits = "0";
        var x = decpt - 1;
        string s;
        if (x < -4 || x >= 16)
        {
            s = digits[..1] + (digits.Length > 1 ? "." + digits[1..] : "") + "e" + (x < 0 ? "-" : "+") +
                Math.Abs(x).ToString("00", CultureInfo.InvariantCulture);
        }
        else if (decpt <= 0)
            s = "0." + new string('0', -decpt) + digits;
        else if (decpt >= digits.Length)
            s = digits + new string('0', decpt - digits.Length) + ".0";
        else
            s = digits[..decpt] + "." + digits[decpt..];
        return d < 0 ? "-" + s : s;
    }

    public static bool IsPrintable(int cp)
    {
        if (cp == ' ')
            return true;
        var cat = Rune.IsValid(cp) ? Rune.GetUnicodeCategory(new Rune(cp)) : UnicodeCategory.Surrogate;
        return cat is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator);
    }

    public static string ReprStr(string s)
    {
        var q = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
        var sb = new StringBuilder();
        sb.Append(q);
        for (var i = 0; i < s.Length; i++)
        {
            int cp = s[i];
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                cp = char.ConvertToUtf32(s[i], s[i + 1]);
                i++;
            }
            if (cp == q || cp == '\\')
                sb.Append('\\').Append((char)cp);
            else if (cp == '\t')
                sb.Append("\\t");
            else if (cp == '\n')
                sb.Append("\\n");
            else if (cp == '\r')
                sb.Append("\\r");
            else if (cp < ' ' || cp == 0x7F)
                sb.Append("\\x").Append(cp.ToString("x2"));
            else if (cp < 0x7F)
                sb.Append((char)cp);
            else if (IsPrintable(cp))
                sb.Append(char.ConvertFromUtf32(cp));
            else if (cp <= 0xFF)
                sb.Append("\\x").Append(cp.ToString("x2"));
            else if (cp <= 0xFFFF)
                sb.Append("\\u").Append(cp.ToString("x4"));
            else
                sb.Append("\\U").Append(cp.ToString("x8"));
        }
        sb.Append(q);
        return sb.ToString();
    }

    public static string ReprBytes(byte[] a)
    {
        var q = a.Contains((byte)'\'') && !a.Contains((byte)'"') ? '"' : '\'';
        var sb = new StringBuilder("b");
        sb.Append(q);
        foreach (var c in a)
        {
            if (c == q || c == '\\')
                sb.Append('\\').Append((char)c);
            else if (c == '\t')
                sb.Append("\\t");
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c < ' ' || c >= 0x7F)
                sb.Append("\\x").Append(c.ToString("x2"));
            else
                sb.Append((char)c);
        }
        sb.Append(q);
        return sb.ToString();
    }

    /// <summary>"%-Ns" % o.</summary>
    public static string Ljust(object? o, int width) => PadRightCp(Str(o), width);

    /// <summary>"%Ns" % o.</summary>
    public static string Rjust(object? o, int width)
    {
        var s = Str(o);
        var n = CpLen(s);
        return n >= width ? s : new string(' ', width - n) + s;
    }

    static string PadRightCp(string s, int width)
    {
        var n = CpLen(s);
        return n >= width ? s : s + new string(' ', width - n);
    }

    /// <summary>len() of a str (code points).</summary>
    public static int CpLen(string s)
    {
        var n = s.Length;
        for (var i = 0; i + 1 < s.Length; i++)
            if (char.IsHighSurrogate(s[i]) && char.IsLowSurrogate(s[i + 1]))
            {
                n--;
                i++;
            }
        return n;
    }

    /// <summary>s[:n] by code points.</summary>
    public static string HeadCp(string s, int n)
    {
        if (n >= s.Length)
            return s;
        var i = 0;
        var k = 0;
        while (i < s.Length && k < n)
        {
            i += char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;
            k++;
        }
        return s[..i];
    }

    /// <summary>"%x" / "%X" of an int.</summary>
    public static string Hex(object? v, bool upper = false)
    {
        var b = Big(v);
        var neg = b < 0;
        if (neg)
            b = -b;
        var s = b.IsZero ? "0" : b.ToString(upper ? "X" : "x", CultureInfo.InvariantCulture).TrimStart('0');
        if (s.Length == 0)
            s = "0";
        return neg ? "-" + s : s;
    }

    /// <summary>Python hex(v): 0x prefix.</summary>
    public static string HexPrefixed(object? v)
    {
        var b = Big(v);
        return b < 0 ? "-0x" + Hex(-b) : "0x" + Hex(b);
    }

    // --- bytes -------------------------------------------------------------------------------------------
    static (int, int) Bounds(int len, long start, long end)
    {
        if (start < 0)
            start = Math.Max(0, start + len);
        if (end < 0)
            end = Math.Max(0, end + len);
        start = Math.Min(start, len);
        end = Math.Min(end, len);
        return ((int)start, (int)Math.Max(start, end));
    }

    /// <summary>b[start:end] with Python semantics (negative indexes count from the end, clamped, never raises).</summary>
    public static byte[] Slice(byte[] b, long start, long end)
    {
        var (s, e) = Bounds(b.Length, start, end);
        return b.AsSpan(s, e - s).ToArray();
    }

    public static byte[] Slice(byte[] b, long start) => Slice(b, start, b.Length);

    /// <summary>s[start:end] (UTF-16 units; the inputs here are BMP text).</summary>
    public static string Slice(string s, long start, long end)
    {
        var (a, e) = Bounds(s.Length, start, end);
        return s[a..e];
    }

    public static string Slice(string s, long start) => Slice(s, start, s.Length);

    /// <summary>b[i] (IndexError when outside).</summary>
    public static byte At(byte[] b, long i)
    {
        if (i < 0)
            i += b.Length;
        if (i < 0 || i >= b.Length)
            throw new PyIndexError("index out of range");
        return b[i];
    }

    public static string HexStr(byte[] b) => Convert.ToHexStringLower(b);

    public static int Find(byte[] b, byte[] sub, int start = 0)
    {
        if (start < 0)
            start = Math.Max(0, start + b.Length);
        if (start > b.Length)
            return -1;
        var i = b.AsSpan(start).IndexOf(sub);
        return i < 0 ? -1 : i + start;
    }

    public static int Find(byte[] b, byte[] sub, int start, int end)
    {
        var (s, e) = Bounds(b.Length, start, end);
        if (sub.Length == 0)
            return s <= e ? s : -1;
        var i = b.AsSpan(s, e - s).IndexOf(sub);
        return i < 0 ? -1 : i + s;
    }

    public static bool Contains(byte[] b, byte[] sub) => b.AsSpan().IndexOf(sub) >= 0;

    public static byte[] Concat(params byte[][] parts)
    {
        var r = new byte[parts.Sum(p => p.Length)];
        var o = 0;
        foreach (var p in parts)
        {
            p.CopyTo(r, o);
            o += p.Length;
        }
        return r;
    }

    public static byte[] FromHex(string s) => Convert.FromHexString(s);

    // --- text decoding -------------------------------------------------------------------------------------
    static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);
    static readonly Encoding Utf8Replace = new UTF8Encoding(false, false);
    static readonly Encoding Utf16LeStrict = new UnicodeEncoding(false, false, true);
    static readonly Encoding Utf16LeReplace = new UnicodeEncoding(false, false, false);
    static readonly Encoding Utf16BeStrict = new UnicodeEncoding(true, false, true);

    /// <summary>bytes.decode("utf-8", errors="replace").</summary>
    public static string Utf8(byte[] b) => Utf8Replace.GetString(b);

    public static string Utf8(ReadOnlySpan<byte> b) => Utf8Replace.GetString(b);

    /// <summary>bytes.decode("utf-8") (UnicodeDecodeError on invalid data).</summary>
    public static string Utf8Exact(byte[] b)
    {
        try
        {
            return Utf8Strict.GetString(b);
        }
        catch (DecoderFallbackException e)
        {
            throw new PyUnicodeDecodeError("'utf-8' codec can't decode bytes: " + e.Message);
        }
    }

    static byte[] NoBom(byte[] b) => b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? b[3..] : b;

    /// <summary>bytes.decode("utf-8-sig") strict.</summary>
    public static string Utf8SigExact(byte[] b) => Utf8Exact(NoBom(b));

    /// <summary>bytes.decode("utf-8-sig", "replace").</summary>
    public static string Utf8Sig(byte[] b) => Utf8(NoBom(b));

    /// <summary>bytes.decode("utf-16-le", "replace"): an odd trailing byte, together with a high surrogate right before it,
    /// is one replacement character (unexpected end of data).</summary>
    public static string Utf16Le(byte[] b)
    {
        if (b.Length % 2 == 0)
            return Utf16LeReplace.GetString(b);
        var cut = b.Length - 1;
        if (cut >= 2 && b[cut - 1] is >= 0xD8 and <= 0xDB)
            cut -= 2;
        return Utf16LeReplace.GetString(b, 0, cut) + "\xfffd";
    }

    /// <summary>bytes.decode("utf-16") strict: BOM decides the byte order, little endian without BOM.</summary>
    public static string Utf16Exact(byte[] b)
    {
        try
        {
            if (b.Length % 2 == 1)
                throw new PyUnicodeDecodeError("'utf-16' codec can't decode byte: truncated data");
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
                return Utf16BeStrict.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
                return Utf16LeStrict.GetString(b, 2, b.Length - 2);
            return Utf16LeStrict.GetString(b);
        }
        catch (DecoderFallbackException e)
        {
            throw new PyUnicodeDecodeError("'utf-16' codec can't decode bytes: " + e.Message);
        }
    }

    public static string Latin1(byte[] b) => Encoding.Latin1.GetString(b);

    public static byte[] Latin1Bytes(string s)
    {
        var r = new byte[s.Length];
        for (var i = 0; i < s.Length; i++)
            r[i] = s[i] <= 0xFF ? (byte)s[i] : (byte)'?';
        return r;
    }

    /// <summary>bytes.decode("ascii", errors="replace").</summary>
    public static string Ascii(byte[] b)
    {
        var c = new char[b.Length];
        for (var i = 0; i < b.Length; i++)
            c[i] = b[i] < 0x80 ? (char)b[i] : '\xfffd';
        return new string(c);
    }

    /// <summary>bytes.decode("ascii") strict.</summary>
    public static string AsciiExact(byte[] b)
    {
        if (b.Any(x => x >= 0x80))
            throw new PyUnicodeDecodeError("'ascii' codec can't decode byte");
        return Encoding.ASCII.GetString(b);
    }

    public static byte[] Utf8Bytes(string s) => Encoding.UTF8.GetBytes(s);

    // --- str ---------------------------------------------------------------------------------------------
    public static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r' or '\x1c' or '\x1d' or '\x1e'
        or '\x1f' or '\x85' or '\xa0' or '\x1680' or (>= '\x2000' and <= '\x200a') or '\x2028' or '\x2029' or '\x202f'
        or '\x205f' or '\x3000';

    /// <summary>str.strip() with Python's whitespace set.</summary>
    public static string Strip(string s)
    {
        int a = 0, e = s.Length;
        while (a < e && IsSpace(s[a]))
            a++;
        while (e > a && IsSpace(s[e - 1]))
            e--;
        return s[a..e];
    }

    public static string Strip(string s, string chars) => s.Trim(chars.ToCharArray());

    public static string LStrip(string s, string chars) => s.TrimStart(chars.ToCharArray());

    public static string RStrip(string s, string chars) => s.TrimEnd(chars.ToCharArray());

    /// <summary>str.split() without separator: runs of whitespace, no empty parts.</summary>
    public static List<string> SplitWs(string s)
    {
        var r = new List<string>();
        var i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && IsSpace(s[i]))
                i++;
            if (i >= s.Length)
                break;
            var a = i;
            while (i < s.Length && !IsSpace(s[i]))
                i++;
            r.Add(s[a..i]);
        }
        return r;
    }

    /// <summary>str.splitlines().</summary>
    public static List<string> SplitLines(string s)
    {
        var r = new List<string>();
        var a = 0;
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c is '\n' or '\r' or '\v' or '\f' or '\x1c' or '\x1d' or '\x1e' or '\x85' or '\x2028' or '\x2029')
            {
                r.Add(s[a..i]);
                i += c == '\r' && i + 1 < s.Length && s[i + 1] == '\n' ? 2 : 1;
                a = i;
            }
            else
                i++;
        }
        if (a < s.Length)
            r.Add(s[a..]);
        return r;
    }

    /// <summary>str.isdigit().</summary>
    public static bool IsDigit(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        foreach (var c in s)
            if (!(char.IsDigit(c) || c is '\x00b2' or '\x00b3' or '\x00b9' or (>= '\x2070' and <= '\x2079') or (>= '\x2080' and <= '\x2089')))
                return false;
        return true;
    }

    /// <summary>int(s) for a decimal text (surrounding whitespace allowed); ValueError otherwise.</summary>
    public static long ParseInt(string s)
    {
        var t = Strip(s).Replace("_", "");
        if (t.Length > 0 && t.All(c => char.IsDigit(c) || c is '+' or '-'))
        {
            var sb = new StringBuilder();
            foreach (var c in t)
                sb.Append(c is '+' or '-' ? c : (char)('0' + (int)char.GetNumericValue(c)));
            if (long.TryParse(sb.ToString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        throw new PyValueError($"invalid literal for int() with base 10: {ReprStr(s)}");
    }

    /// <summary>int(s, 16).</summary>
    public static long ParseHex(string s)
    {
        var t = Strip(s);
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            t = t[2..];
        if (t.Length > 0 && long.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var v) && v >= 0)
            return v;
        if (t.Length > 0 && ulong.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var u))
            return (long)u;
        throw new PyValueError($"invalid literal for int() with base 16: {ReprStr(s)}");
    }

    /// <summary>int(s, 0): decimal, 0x hex, 0o octal, 0b binary.</summary>
    public static long ParseInt0(string s)
    {
        var t = Strip(s);
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ParseHex(t);
        if (t.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(t[2..], 8);
        if (t.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(t[2..], 2);
        return ParseInt(t);
    }

    /// <summary>s.rsplit(sep, 1)[-1].</summary>
    public static string AfterLast(string s, string sep)
    {
        var i = s.LastIndexOf(sep, StringComparison.Ordinal);
        return i < 0 ? s : s[(i + sep.Length)..];
    }

    /// <summary>s.split(sep, 1)[0].</summary>
    public static string BeforeFirst(string s, string sep)
    {
        var i = s.IndexOf(sep, StringComparison.Ordinal);
        return i < 0 ? s : s[..i];
    }

    public static List<object?> L(params object?[] items) => [.. items];
}
