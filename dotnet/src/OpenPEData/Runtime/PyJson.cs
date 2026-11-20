using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenPEData.Runtime;

/// <summary>json.dumps with the output of Python's encoder (indent, separators, ensure_ascii, float repr, key conversion)
/// and json.loads into PyDict / List / long / double / string values.</summary>
internal static class PyJson
{
    public enum Fallback
    {
        /// <summary>bytes as "hex:..." (OpenPEData.py _json_default), anything else TypeError.</summary>
        Hex,
        /// <summary>default=str.</summary>
        Str,
        /// <summary>no default: TypeError.</summary>
        None,
    }

    public static string Dumps(object? o, int? indent = null, bool ensureAscii = true, Fallback fallback = Fallback.None)
    {
        var sb = new StringBuilder();
        Write(sb, o, indent, ensureAscii, fallback, false, 0);
        return sb.ToString();
    }

    /// <summary>Standard JSON (RFC 8259): non-finite floats as null, otherwise like Dumps.</summary>
    public static string DumpsStrict(object? o, Fallback fallback = Fallback.Hex)
    {
        var sb = new StringBuilder();
        Write(sb, o, null, false, fallback, true, 0);
        return sb.ToString();
    }

    static void Newline(StringBuilder sb, int? indent, int level)
    {
        sb.Append('\n');
        sb.Append(' ', indent!.Value * level);
    }

    static void Write(StringBuilder sb, object? o, int? indent, bool ascii, Fallback fb, bool strict, int level)
    {
        switch (o)
        {
            case null:
                sb.Append("null");
                return;
            case string s:
                Str(sb, s, ascii);
                return;
            case bool b:
                sb.Append(b ? "true" : "false");
                return;
            case long or int or ulong or BigInteger:
                sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
                return;
            case double d:
                sb.Append(strict && !double.IsFinite(d) ? "null" : Float(d));
                return;
            case PyDict d:
                if (d.Count == 0)
                {
                    sb.Append("{}");
                    return;
                }
                sb.Append('{');
                var first = true;
                foreach (var kv in d)
                {
                    if (!first)
                        sb.Append(indent is null ? ", " : ",");
                    first = false;
                    if (indent is not null)
                        Newline(sb, indent, level + 1);
                    Str(sb, Key(kv.Key), ascii);
                    sb.Append(": ");
                    Write(sb, kv.Value, indent, ascii, fb, strict, level + 1);
                }
                if (indent is not null)
                    Newline(sb, indent, level);
                sb.Append('}');
                return;
            case IList or PyTuple when o is not byte[]:
                var items = o is PyTuple t ? (IList<object?>)t.Items : ((IList)o).Cast<object?>().ToList();
                if (items.Count == 0)
                {
                    sb.Append("[]");
                    return;
                }
                sb.Append('[');
                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                        sb.Append(indent is null ? ", " : ",");
                    if (indent is not null)
                        Newline(sb, indent, level + 1);
                    Write(sb, items[i], indent, ascii, fb, strict, level + 1);
                }
                if (indent is not null)
                    Newline(sb, indent, level);
                sb.Append(']');
                return;
        }
        if (fb == Fallback.Hex && Py.Bytes(o) is { } bytes)
        {
            Str(sb, "hex:" + Py.HexStr(bytes), ascii);
            return;
        }
        if (fb == Fallback.Str)
        {
            Str(sb, Py.Str(o), ascii);
            return;
        }
        throw new PyTypeError($"Object of type {Py.TypeName(o)} is not JSON serializable");
    }

    static string Key(object? k) => k switch
    {
        string s => s,
        null => "null",
        bool b => b ? "true" : "false",
        long or int or ulong or BigInteger => Convert.ToString(k, CultureInfo.InvariantCulture)!,
        double d => Float(d),
        _ => throw new PyTypeError($"keys must be str, int, float, bool or None, not {Py.TypeName(k)}"),
    };

    public static string Float(double d) => double.IsNaN(d) ? "NaN" : double.IsPositiveInfinity(d) ? "Infinity" :
        double.IsNegativeInfinity(d) ? "-Infinity" : Py.FloatRepr(d);

    static void Str(StringBuilder sb, string s, bool ascii)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (c < 0x20 || (ascii && c > 0x7E))
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }

    // --- loads --------------------------------------------------------------------------------------------
    public static object? Loads(string s)
    {
        var i = 0;
        var v = Value(s, ref i);
        Ws(s, ref i);
        if (i != s.Length)
            throw new PyValueError("Extra data");
        return v;
    }

    static void Ws(string s, ref int i)
    {
        while (i < s.Length && s[i] is ' ' or '\t' or '\n' or '\r' or '\xfeff')
            i++;
    }

    static object? Value(string s, ref int i)
    {
        Ws(s, ref i);
        if (i >= s.Length)
            throw new PyValueError("Expecting value");
        var c = s[i];
        if (c == '{')
        {
            i++;
            var d = new PyDict();
            Ws(s, ref i);
            if (s[i] == '}')
            {
                i++;
                return d;
            }
            while (true)
            {
                Ws(s, ref i);
                var k = (string)Value(s, ref i)!;
                Ws(s, ref i);
                i++; // ':'
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i++] == '}')
                    return d;
            }
        }
        if (c == '[')
        {
            i++;
            var l = new List<object?>();
            Ws(s, ref i);
            if (s[i] == ']')
            {
                i++;
                return l;
            }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i++] == ']')
                    return l;
            }
        }
        if (c == '"')
        {
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    var e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                    sb.Append(s[i++]);
            }
            i++;
            return sb.ToString();
        }
        if (s.AsSpan(i).StartsWith("true"))
        {
            i += 4;
            return true;
        }
        if (s.AsSpan(i).StartsWith("false"))
        {
            i += 5;
            return false;
        }
        if (s.AsSpan(i).StartsWith("null"))
        {
            i += 4;
            return null;
        }
        foreach (var (word, val) in new[] { ("NaN", double.NaN), ("Infinity", double.PositiveInfinity), ("-Infinity", double.NegativeInfinity) })
        {
            if (s.AsSpan(i).StartsWith(word))
            {
                i += word.Length;
                return val;
            }
        }
        var a = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '-' or '+' or '.' or 'e' or 'E'))
            i++;
        var num = s[a..i];
        if (num.IndexOfAny(['.', 'e', 'E']) >= 0)
            return double.Parse(num, CultureInfo.InvariantCulture);
        return Py.Int(BigInteger.Parse(num, CultureInfo.InvariantCulture));
    }
}
