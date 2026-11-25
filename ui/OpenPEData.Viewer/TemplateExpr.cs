using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace OpenPEData.Viewer;

/// <summary>Expressions of the WinCC Unified rendering templates (hmi-bind, docs\spec\hmi-styles.md): "{{ expr }}" or
/// "{ path }". Values are double, bool, string, JsonNode (objects such as Font) or null. Names: HmiProps.* (item or
/// part of the include scope), LocalProps.*, ParamProps.*, SysProps.*, Layout.ActualWidth/ActualHeight; an enum
/// literal Hmi&lt;Enum&gt;.&lt;Name&gt; is its name (item properties carry enum names). Functions: and, or, not, gt, ge,
/// lt, le, eq, ne, floor, ceil, abs, has and Converter.* (colours, text measure, min/max).</summary>
public sealed class TemplateScope
{
    public JsonObject Hmi = new();
    public Dictionary<string, object?> Local = new();
    public Dictionary<string, object?> Param = new();
    public double Width, Height;

    /// <summary>Runtime properties Openness does not list but the templates read (item is operable, no graphic). No
    /// ProcessValue: an I/O field without a tag stays empty in the runtime (164 against 165, 2026-10-31).</summary>
    private static readonly Dictionary<string, object?> RuntimeDefaults = new()
    {
        ["Operability"] = "Operable", ["Graphic"] = "", ["AlternateGraphic"] = "", ["AlternateText"] = "",
        ["GraphicWidth"] = 0.0, ["GraphicHeight"] = 0.0,
    };

    public object? Resolve(string path)
    {
        var parts = path.Split('.');
        object? cur = parts[0] switch
        {
            "HmiProps" => Hmi,
            "LocalProps" => Local,
            "ParamProps" => Param,
            "SysProps" => new Dictionary<string, object?> { ["HasFocus"] = false },
            "Layout" => new Dictionary<string, object?> { ["ActualWidth"] = Width, ["ActualHeight"] = Height },
            _ when parts[0].StartsWith("Hmi") && parts.Length == 2 => HmiEnums.Literal(parts[0], parts[1]),  // enum literal
            _ => null,
        };
        for (var i = 1; i < parts.Length && cur != null && !(parts[0].StartsWith("Hmi") && parts[0] != "HmiProps"); i++)
        {
            cur = cur switch
            {
                JsonObject o => o.TryGetPropertyValue(parts[i], out var n) ? TemplateExpr.FromJson(n)
                    : i == 1 && parts[0] == "HmiProps" && RuntimeDefaults.TryGetValue(parts[i], out var d) ? d : null,
                Dictionary<string, object?> dict => dict.TryGetValue(parts[i], out var v) ? v : null,
                _ => null,
            };
        }
        // enum properties of the item carry their names ("Lines", "Low, High"); the templates also calculate with them
        // (ThresholdIndicators % 2, ge( ..., HmiThresholdIndicator.Markers ))
        if (parts[0] == "HmiProps" && parts.Length > 1 && cur is string name) return HmiEnums.Property(parts[^1], name) ?? cur;
        return cur;
    }
}

/// <summary>Value of an enum in a template expression: the name as Openness shows it and its number. Compares with a
/// name by name, with a number by number; calculates with the number.</summary>
public sealed record EnumValue(string Name, double Value)
{
    public override string ToString() => Name;
}

/// <summary>WinCC Unified enum types (embedded table of the library: property -> enum type -> names and values).</summary>
public static class HmiEnums
{
    private static readonly Lazy<(Dictionary<string, string> Props, Dictionary<string, (bool Flags, Dictionary<string, double> Values)> Types)> Table = new(Load);

    private static (Dictionary<string, string>, Dictionary<string, (bool, Dictionary<string, double>)>) Load()
    {
        var props = new Dictionary<string, string>();
        var types = new Dictionary<string, (bool, Dictionary<string, double>)>();
        try
        {
            var asm = typeof(TiaProject).Assembly;
            var res = asm.GetManifestResourceNames().Where(n => n.EndsWith("unified_enums.json", StringComparison.Ordinal)).Order().LastOrDefault();
            using var st = res != null ? asm.GetManifestResourceStream(res) : null;
            if (st == null || JsonNode.Parse(st) is not JsonObject root) return (props, types);
            foreach (var (k, v) in root["properties"] as JsonObject ?? new JsonObject())
                if (v?.ToString() is { } t) props[k] = t;
            foreach (var (k, v) in root["enums"] as JsonObject ?? new JsonObject())
            {
                var vals = new Dictionary<string, double>();
                foreach (var (n, x) in v?["values"] as JsonObject ?? new JsonObject())
                    if (x is JsonValue xv && xv.TryGetValue<double>(out var d)) vals[n] = d;
                types[k] = (v?["flags"] is JsonValue f && f.TryGetValue<bool>(out var fl) && fl, vals);
            }
        }
        catch (Exception) { }
        return (props, types);
    }

    /// <summary>Hmi&lt;Enum&gt;.&lt;Name&gt;: an EnumValue when the type is known, otherwise the name.</summary>
    public static object Literal(string type, string name) =>
        Table.Value.Types.TryGetValue(type, out var t) && t.Values.TryGetValue(name, out var v) ? new EnumValue(name, v) : name;

    /// <summary>The enum value of a property holding a name ("Markers", flags "Low, High"), or null.</summary>
    public static EnumValue? Property(string property, string name)
    {
        if (!Table.Value.Props.TryGetValue(property, out var type) || !Table.Value.Types.TryGetValue(type, out var t)) return null;
        if (t.Values.TryGetValue(name, out var v)) return new EnumValue(name, v);
        if (!t.Flags) return null;
        double sum = 0;
        foreach (var part in name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (t.Values.TryGetValue(part, out var pv)) sum += pv;
            else return null;
        return new EnumValue(name, sum);
    }
}

public static class TemplateExpr
{
    /// <summary>Evaluates an attribute value: "{{ expr }}", "{ expr }" or a literal (returned as string).</summary>
    public static object? EvalAttr(string text, TemplateScope s)
    {
        var t = text.Trim();
        if (t.StartsWith("{{") && t.EndsWith("}}")) return Eval(t[2..^2], s);
        if (t.StartsWith('{') && t.EndsWith('}')) return Eval(t[1..^1], s);
        return text;
    }

    public static object? Eval(string expr, TemplateScope s)
    {
        try
        {
            var p = new Parser(expr, s);
            var v = p.Ternary();
            return v;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static object? FromJson(JsonNode? n)
    {
        switch (n)
        {
            case null: return null;
            case JsonObject { Count: 0 }: return "";  // empty multilingual text
            case JsonObject o when o.All(kv => kv.Key.Length == 5 && kv.Key[2] == '-'):
                // multilingual text {"en-US": "..."}: the runtime language (--lang), else the first language
                if (ScreenRenderer.Language is { } lang && o.ContainsKey(lang)) return o[lang]?.ToString() ?? "";
                return o.Select(kv => kv.Value?.ToString()).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
            case JsonObject or JsonArray: return n;
            case JsonValue v:
                if (v.TryGetValue<bool>(out var b)) return b;
                if (v.TryGetValue<double>(out var d)) return d;
                if (v.TryGetValue<string>(out var str))
                    return str.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && str.Length == 10
                           && uint.TryParse(str[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u)
                        ? (double)u : str;
                return v.ToString();
        }
        return null;
    }

    public static double Num(object? v) => v switch
    {
        double d => d,
        bool b => b ? 1 : 0,
        EnumValue e => e.Value,
        string str when str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        && ulong.TryParse(str[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u) => u,
        string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => 0,
    };

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        EnumValue e => e.Value != 0,
        string str => str.Length > 0 && str != "false",
        _ => true,
    };

    public static string Str(object? v) => v switch
    {
        null => "",
        double d => d.ToString("0.######", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        // stored texts can hold control characters (F40: NUL), which XML does not allow
        _ => XmlSafe(v.ToString() ?? ""),
    };

    private static string XmlSafe(string s) =>
        s.Any(c => c < 0x20 && c is not ('\t' or '\n' or '\r')) ? new string(s.Where(c => c >= 0x20 || c is '\t' or '\n' or '\r').ToArray()) : s;

    private static bool Equal(object? a, object? b)
    {
        if (a is EnumValue || b is EnumValue)
        {
            if (a is null || b is null) return false;
            var other = a is EnumValue ? b : a;
            return other is double or bool or EnumValue && !(a is EnumValue ea && b is EnumValue eb && ea.Name != eb.Name)
                ? Math.Abs(Num(a) - Num(b)) < 1e-9
                : Str(a) == Str(b);
        }
        if (a is string sa && b is string sb) return sa == sb;
        if (a == null || b == null) return a == null && b == null || Str(a ?? b) == "";
        if (a is string || b is string) return Str(a) == Str(b);
        return Math.Abs(Num(a) - Num(b)) < 1e-9;
    }

    // ---------- converters ----------

    private static uint Argb(object? c) => (uint)(ulong)Math.Max(0, Num(c));

    private static object? Call(string name, List<object?> a)
    {
        object? A(int i) => i < a.Count ? a[i] : null;
        switch (name)
        {
            case "and": return a.All(Truthy);
            case "or": return a.Any(Truthy);
            case "not": return !Truthy(A(0));
            case "gt": return Num(A(0)) > Num(A(1));
            case "ge": return Num(A(0)) >= Num(A(1));
            case "lt": return Num(A(0)) < Num(A(1));
            case "le": return Num(A(0)) <= Num(A(1));
            case "eq": return Equal(A(0), A(1));
            case "ne": return !Equal(A(0), A(1));
            case "floor": return Math.Floor(Num(A(0)));
            case "ceil": return Math.Ceiling(Num(A(0)));
            case "abs": return Math.Abs(Num(A(0)));
            // has(flags, flag): flags enum value as names ("Ticks, Labels", "None") contains flag; a numeric value counts
            // as set when not 0 (PeakIndicators "None" draws no peak marker, runtime 2026-11-08)
            case "has":
            {
                var f = A(0);
                if (f == null || Str(f) == "") return false;
                if (A(1) is EnumValue fe && f is EnumValue or double)
                    return fe.Value == 0 ? Num(f) == 0 : ((long)Num(f) & (long)fe.Value) == (long)fe.Value;
                if (a.Count < 2 || A(1) is not (string or EnumValue)) return true;
                var flag = Str(A(1));
                if (f is double d) return d != 0;
                return Str(f).Split(',').Any(x => x.Trim() == flag);
            }
            case "Converter.RGBA":
            {
                var c = Argb(A(0));
                return string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3:0.###})",
                    (c >> 16) & 255, (c >> 8) & 255, c & 255, ((c >> 24) & 255) / 255.0);
            }
            case "Converter.RGB":
            {
                var c = Argb(A(0));
                return $"rgb({(c >> 16) & 255},{(c >> 8) & 255},{c & 255})";
            }
            case "Converter.Alpha": return ((Argb(A(0)) >> 24) & 255) / 255.0;
            case "Converter.Darker":
            case "Converter.Lighter":
            {
                // each channel moved by the factor towards black/white, rounded; the gradient end colours of button
                // and toggle switch match the runtime exactly (164, 2026-10-31), rounding against truncating undecided
                var c = Argb(A(0));
                var f = Math.Clamp(Num(A(1)), 0, 1);
                uint Ch(int sh)
                {
                    var v = (c >> sh) & 255;
                    return (uint)Math.Round(name.EndsWith("Darker") ? v * (1 - f) : v + (255 - v) * f);
                }
                // the alpha is kept, in the runtime and in the TIA screen editor alike: a button with BackColor
                // 0x00F2F4FF is fully transparent (both gradient stops), one with 0x80F2F4FF half transparent
                // (TIA V21 screen editor, 2026-11-20, docs\spec\hmi-rendering.md)
                return (double)((c & 0xFF000000u) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0));
            }
            case "Converter.Max": return a.Count == 0 ? 0.0 : a.Max(Num);
            case "Converter.Min": return a.Count == 0 ? 0.0 : a.Min(Num);
            case "Converter.Bounds": return Math.Clamp(Num(A(0)), Math.Min(Num(A(1)), Num(A(2))), Math.Max(Num(A(1)), Num(A(2))));
            case "Converter.IsMultiline": return Str(A(0)).Contains('\n');
            case "Converter.IsNumber": return A(0) is double || double.TryParse(Str(A(0)), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            case "Converter.CountItems":
            case "Converter.CountVisibleItems": return A(0) is JsonArray arr ? (double)arr.Count : 0.0;
            case "Converter.TextWidth": return Measure(Str(A(0)), A(1) as JsonObject).Width;
            case "Converter.TextHeight": return Measure(Str(A(0)), A(1) as JsonObject).Height;
            case "Converter.TextEllipsis": return Ellipsis(Str(A(0)), Num(A(1)), A(2) as JsonObject);
            case "Converter.TextDecoration":
            {
                var f = A(0) as JsonObject;
                bool B(string k) => f?[k] is JsonValue v && v.TryGetValue<bool>(out var x) && x;
                return B("Underline") ? "underline" : B("StrikeOut") ? "line-through" : "none";
            }
            case "Converter.FormatPattern": return FormatPattern(A(0), Str(A(1)));
            case "Converter.GetTextByID": return "";
            case "Converter.ThresholdColorForValue": return ThresholdColor(Num(A(0)), A(1) as JsonArray, A(2));
            default: return null;  // GetDefaultValue, ...: not needed for the resting state
        }
    }

    /// <summary>Colour of the range a value lies in. Only enabled thresholds in ascending order count: going
    /// Minimum, Lower, Normal, Upper, Maximum, a threshold whose value is not above the last one kept is ignored.
    /// Then above Maximum/Upper their colour, below Minimum/Lower theirs, Normal if kept and the value lies strictly
    /// inside the limits, else the fallback. Runtime 2026-11-08 (all thresholds on 0): Maximum+Upper enabled, value 30
    /// shows Upper; all enabled, values 0, 55, 100 show the fallback. TIA editor: value 0 with Minimum at 10 shows the
    /// Minimum colour, with only Maximum/Upper enabled the fallback.</summary>
    private static object? ThresholdColor(double v, JsonArray? list, object? fallback)
    {
        if (list == null) return fallback;
        double V(JsonObject t) => Num(FromJson(t["Value"]));
        var kept = new Dictionary<string, JsonObject>();
        double? last = null;
        foreach (var mode in new[] { "Minimum", "Lower", "Normal", "Upper", "Maximum" })
            if (list.OfType<JsonObject>().FirstOrDefault(t => t["ThresholdMode"]?.ToString() == mode
                    && Truthy(FromJson(t["Enable"])) && t["Value"] != null) is { } th && (last is null || V(th) > last))
                (kept[mode], last) = (th, V(th));
        JsonObject? Get(string mode) => kept.GetValueOrDefault(mode);
        foreach (var (mode, above) in new[] { ("Maximum", true), ("Upper", true), ("Minimum", false), ("Lower", false) })
            if (Get(mode) is { } t && (above ? v > V(t) : v < V(t)))
                return FromJson(t["Color"]);
        // Normal only strictly inside the enabled limits: all thresholds on 0 and value 0 show the fallback (runtime)
        var inside = new[] { "Maximum", "Upper" }.All(m => Get(m) is not { } t || v < V(t))
            && new[] { "Minimum", "Lower" }.All(m => Get(m) is not { } t || v > V(t));
        return inside && Get("Normal") is { } n ? FromJson(n["Color"]) : fallback;
    }

    private static readonly System.Text.RegularExpressions.Regex FormatToken = new(@"\{([^{}]*)\}");

    /// <summary>Output format of WinCC Unified ("{I} %", "{F2} s", "{H4}", "{#,##0} Fl/h", "{S}"): the first {…} is
    /// replaced by the value, the rest is kept. I integer, F&lt;n&gt; fixed point (without n 2 decimals), H&lt;n&gt;
    /// hexadecimal with n digits, S text, otherwise a .NET custom number pattern. A value that is no number stays text.</summary>
    public static string FormatPattern(object? value, string? pattern)
    {
        var v = value is JsonNode jn ? FromJson(jn) : value;
        if (string.IsNullOrEmpty(pattern)) return Str(v);
        var done = false;
        return FormatToken.Replace(pattern, m =>
        {
            var spec = m.Groups[1].Value;
            // "{D} {T}": date and time of the same value
            if (done && !(spec.Length > 0 && spec[0] is 'D' or 'T')) return m.Value;
            done = true;
            if (v is not double d) return Str(v);
            // numbers in the format of the runtime language ('0,00 s' for {F} s in de-DE: TIA ES and
            // runtime); {F} without digits has 2 decimals (.NET de-DE would give 3)
            var ci = CultureInfo.GetCultureInfo(ScreenRenderer.Language ?? "de-DE");
            if (spec.Length == 0 || spec == "S") return Str(v);
            // date / time: {D}, {T}, {D,@dd.MM.yyyy}, {T,@HH:mm:ss}; the value counts milliseconds from 1970-01-01
            // (TIA screen editor: 0 -> '01.01.1970', '00:00:00'); pattern letters as in ICU (EEEE = day name)
            if (spec[0] is 'D' or 'T' && (spec.Length == 1 || spec[1] == ','))
            {
                var culture = CultureInfo.GetCultureInfo(ScreenRenderer.Language ?? "de-DE");
                var at = DateTime.UnixEpoch.AddMilliseconds(d);
                var dp = spec.IndexOf('@') is var ai and >= 0 ? spec[(ai + 1)..].Replace("EEEE", "dddd").Replace("EEE", "ddd") : null;
                try { return at.ToString(dp ?? (spec[0] == 'D' ? "d" : "T"), culture); }
                catch (FormatException) { return Str(v); }
            }
            switch (char.ToUpperInvariant(spec[0]))
            {
                case 'I' when spec.Length == 1 || spec[1..].All(char.IsDigit):
                    return ((long)Math.Round(d)).ToString(spec.Length > 1 ? "D" + spec[1..] : "D", ci);
                case 'F' when spec.Length == 1 || spec[1..].All(char.IsDigit):
                    return d.ToString("F" + (spec.Length > 1 ? spec[1..] : "2"), ci);
                case 'H' when spec.Length == 1 || spec[1..].All(char.IsDigit):
                    return ((long)Math.Round(d)).ToString("X" + spec[1..], ci);
                default:
                    try { return d.ToString(spec, ci); } catch (FormatException) { return Str(v); }
            }
        });
    }

    public static Size Measure(string text, JsonObject? font)
    {
        var size = font?["Size"] is JsonValue sv && sv.TryGetValue<double>(out var fs) ? fs : 14;
        var family = font?["Name"]?.ToString() ?? "SiemensSans";
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamilyOf(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Brushes.Black, 1.0);
        return new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
    }

    public static FontFamily FontFamilyOf(string name) =>
        new(name.Replace(" ", "").Equals("SiemensSans", StringComparison.OrdinalIgnoreCase)
            ? "Siemens Sans, Segoe UI" : name + ", Segoe UI");

    private static string Ellipsis(string text, double width, JsonObject? font)
    {
        if (width <= 0 || Measure(text, font).Width <= width) return text;
        for (var n = text.Length - 1; n > 0; n--)
            if (Measure(text[..n] + "…", font).Width <= width) return text[..n] + "…";
        return "";
    }

    // ---------- parser (recursive descent) ----------

    private sealed class Parser(string src, TemplateScope scope)
    {
        private int _i;

        private void Ws()
        {
            while (_i < src.Length && char.IsWhiteSpace(src[_i])) _i++;
        }

        private bool Take(string tok)
        {
            Ws();
            if (string.CompareOrdinal(src, _i, tok, 0, tok.Length) != 0) return false;
            _i += tok.Length;
            return true;
        }

        public object? Ternary()
        {
            var c = Or();
            if (!Take("?")) return c;
            var a = Ternary();
            if (!Take(":")) throw new FormatException("':' expected");
            var b = Ternary();
            return Truthy(c) ? a : b;
        }

        private object? Or()
        {
            var l = And();
            while (Take("||")) { var r = And(); l = Truthy(l) || Truthy(r); }
            return l;
        }

        private object? And()
        {
            var l = Cmp();
            while (Take("&&")) { var r = Cmp(); l = Truthy(l) && Truthy(r); }
            return l;
        }

        private object? Cmp()
        {
            var l = Add();
            while (true)
            {
                if (Take("===") || Take("==")) l = Equal(l, Add());
                else if (Take("!==") || Take("!=")) l = !Equal(l, Add());
                else if (Take("<=")) l = Num(l) <= Num(Add());
                else if (Take(">=")) l = Num(l) >= Num(Add());
                else if (Take("<")) l = Num(l) < Num(Add());
                else if (Take(">")) l = Num(l) > Num(Add());
                else return l;
            }
        }

        private object? Add()
        {
            var l = Mul();
            while (true)
            {
                if (Take("+"))
                {
                    var r = Mul();
                    l = l is string || r is string ? Str(l) + Str(r) : Num(l) + Num(r);
                }
                else if (Take("-")) l = Num(l) - Num(Mul());
                else return l;
            }
        }

        private object? Mul()
        {
            var l = Unary();
            while (true)
            {
                if (Take("*")) l = Num(l) * Num(Unary());
                else if (Take("/")) { var r = Num(Unary()); l = r == 0 ? 0.0 : Num(l) / r; }
                else if (Take("%")) { var r = Num(Unary()); l = r == 0 ? 0.0 : Num(l) % r; }
                else return l;
            }
        }

        private object? Unary()
        {
            if (Take("!")) return !Truthy(Unary());
            if (Take("-")) return -Num(Unary());
            return Primary();
        }

        private object? Primary()
        {
            Ws();
            if (_i >= src.Length) throw new FormatException("unexpected end");
            var ch = src[_i];
            if (ch == '(')
            {
                _i++;
                var v = Ternary();
                if (!Take(")")) throw new FormatException("')' expected");
                return v;
            }
            if (ch is '\'' or '"')
            {
                var end = src.IndexOf(ch, _i + 1);
                if (end < 0) throw new FormatException("unterminated string");
                var str = src[(_i + 1)..end];
                _i = end + 1;
                return str;
            }
            if (char.IsDigit(ch) || ch == '.' && _i + 1 < src.Length && char.IsDigit(src[_i + 1]))
            {
                var start = _i;
                if (src.Length > _i + 1 && ch == '0' && (src[_i + 1] is 'x' or 'X'))
                {
                    _i += 2;
                    while (_i < src.Length && Uri.IsHexDigit(src[_i])) _i++;
                    return (double)ulong.Parse(src[(start + 2).._i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                }
                while (_i < src.Length && (char.IsDigit(src[_i]) || src[_i] == '.')) _i++;
                return double.Parse(src[start.._i], CultureInfo.InvariantCulture);
            }
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = _i;
                while (_i < src.Length && (char.IsLetterOrDigit(src[_i]) || src[_i] is '_' or '.')) _i++;
                var name = src[start.._i];
                if (name == "true") return true;
                if (name == "false") return false;
                if (name == "null") return null;
                if (Take("("))
                {
                    var args = new List<object?>();
                    if (!Take(")"))
                    {
                        do args.Add(Ternary()); while (Take(","));
                        if (!Take(")")) throw new FormatException("')' expected");
                    }
                    return Call(name, args);
                }
                return scope.Resolve(name);
            }
            throw new FormatException($"unexpected '{ch}'");
        }
    }

    internal static string Describe(object? v) => v switch
    {
        JsonNode n => n.ToJsonString(),
        _ => Str(v),
    };
}
