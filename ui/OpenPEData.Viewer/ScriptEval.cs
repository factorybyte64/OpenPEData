using System.Globalization;
using System.Text;

namespace OpenPEData.Viewer;

/// <summary>The small part of JavaScript that WinCC Unified property scripts use for Visible:
/// let / var / const, assignment, if / else, return; literals, ! &amp;&amp; || == != === !== &lt; &gt; &lt;= &gt;= + -,
/// Tags("name").Read(), Faceplate.Properties.Name, Boolean(x), String(x), .trim() .toUpperCase() .toLowerCase().
/// Without the runtime a tag reads its default (0, false or "" as the runtime without PLC connection); the caller gives
/// the faceplate property values. Values are double, bool, string or null (undefined). Anything else (global script
/// functions, member assignments other than ignored ones) makes Evaluate return Unsupported.</summary>
public static class ScriptEval
{
    public static readonly object Unsupported = new();

    /// <summary>Result of the script or Unsupported.</summary>
    public static object? Evaluate(string code, Func<string, object?> faceplateProperty)
    {
        try
        {
            var p = new Parser(Tokenize(code), faceplateProperty);
            return p.Run();
        }
        catch (Exception)
        {
            return Unsupported;
        }
    }

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        string s => s.Length > 0,
        _ => true,
    };

    // ---------- tokens ----------

    private enum T { Num, Str, Id, Op, End }

    private readonly record struct Tok(T Kind, string Text);

    private static List<Tok> Tokenize(string s)
    {
        var res = new List<Tok>();
        var i = 0;
        string[] ops = ["===", "!==", "==", "!=", "<=", ">=", "&&", "||"];
        while (i < s.Length)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*') { var e = s.IndexOf("*/", i + 2, StringComparison.Ordinal); i = e < 0 ? s.Length : e + 2; continue; }
            if (char.IsDigit(c))
            {
                var st = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                res.Add(new Tok(T.Num, s[st..i]));
                continue;
            }
            if (c is '"' or '\'')
            {
                var sb = new StringBuilder();
                i++;
                while (i < s.Length && s[i] != c)
                {
                    if (s[i] == '\\' && i + 1 < s.Length) i++;
                    sb.Append(s[i++]);
                }
                i++;
                res.Add(new Tok(T.Str, sb.ToString()));
                continue;
            }
            if (char.IsLetter(c) || c is '_' or '$')
            {
                var st = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '$')) i++;
                res.Add(new Tok(T.Id, s[st..i]));
                continue;
            }
            var op = ops.FirstOrDefault(o => string.CompareOrdinal(s, i, o, 0, o.Length) == 0);
            if (op != null) { res.Add(new Tok(T.Op, op)); i += op.Length; continue; }
            if ("(){};=!<>+-.,".IndexOf(c) >= 0) { res.Add(new Tok(T.Op, c.ToString())); i++; continue; }
            throw new FormatException("token " + c);
        }
        res.Add(new Tok(T.End, ""));
        return res;
    }

    // ---------- statements and expressions ----------

    private sealed class ReturnSignal(object? value) : Exception
    {
        public object? Value { get; } = value;
    }

    private sealed class Parser(List<Tok> toks, Func<string, object?> prop)
    {
        private int _i;
        private readonly Dictionary<string, object?> _vars = new();

        private Tok Peek => toks[_i];
        private bool Is(string op) => Peek.Kind is T.Op or T.Id && Peek.Text == op;
        private Tok Next() => toks[_i++];
        private void Expect(string op) { if (!Is(op)) throw new FormatException("expected " + op); _i++; }
        private void Semi() { if (Is(";")) _i++; }

        public object? Run()
        {
            try
            {
                while (Peek.Kind != T.End) Statement(true);
            }
            catch (ReturnSignal r)
            {
                return r.Value;
            }
            return null;
        }

        /// <summary>One statement; exec false parses it without effect (the branch not taken).</summary>
        private void Statement(bool exec)
        {
            if (Is("{"))
            {
                _i++;
                while (!Is("}")) Statement(exec);
                _i++;
                return;
            }
            if (Is(";")) { _i++; return; }
            if (Is("let") || Is("var") || Is("const"))
            {
                _i++;
                do
                {
                    var name = Next().Text;
                    object? v = null;
                    if (Is("=")) { _i++; v = Expr(); }
                    if (exec) _vars[name] = v;
                    if (Is(",")) _i++; else break;
                } while (true);
                Semi();
                return;
            }
            if (Is("if"))
            {
                _i++;
                Expect("(");
                var cond = Truthy(Expr());
                Expect(")");
                Statement(exec && cond);
                if (Is("else")) { _i++; Statement(exec && !cond); }
                return;
            }
            if (Is("return"))
            {
                _i++;
                object? v = Is(";") || Is("}") || Peek.Kind == T.End ? null : Expr();
                Semi();
                if (exec) throw new ReturnSignal(v);
                return;
            }
            // assignment to a variable; an assignment to a member (Faceplate.Parent.Visible = ...) has no effect here
            if (Peek.Kind == T.Id && toks[_i + 1].Text == "=" )
            {
                var name = Next().Text;
                _i++;
                var v = Expr();
                if (exec) _vars[name] = v;
                Semi();
                return;
            }
            var start = _i;
            if (Peek.Kind == T.Id)
            {
                while (Peek.Kind == T.Id && toks[_i + 1].Text == ".") _i += 2;
                if (Peek.Kind == T.Id && toks[_i + 1].Text == "=")
                {
                    _i += 2;
                    Expr();
                    Semi();
                    return;
                }
                _i = start;
            }
            Expr();
            Semi();
        }

        private object? Expr() => Or();

        private object? Or()
        {
            var l = And();
            while (Is("||")) { _i++; var r = And(); l = Truthy(l) ? l : r; }
            return l;
        }

        private object? And()
        {
            var l = Equality();
            while (Is("&&")) { _i++; var r = Equality(); l = Truthy(l) ? r : l; }
            return l;
        }

        private object? Equality()
        {
            var l = Relational();
            while (Is("==") || Is("!=") || Is("===") || Is("!=="))
            {
                var op = Next().Text;
                var r = Relational();
                var eq = op.Length == 3 ? StrictEquals(l, r) : LooseEquals(l, r);
                l = op[0] == '=' ? eq : !eq;
            }
            return l;
        }

        private object? Relational()
        {
            var l = Additive();
            while (Is("<") || Is(">") || Is("<=") || Is(">="))
            {
                var op = Next().Text;
                var r = Additive();
                if (l is string ls && r is string rs)
                {
                    var c = string.CompareOrdinal(ls, rs);
                    l = op switch { "<" => c < 0, ">" => c > 0, "<=" => c <= 0, _ => c >= 0 };
                }
                else
                {
                    double a = ToNumber(l), b = ToNumber(r);
                    l = op switch { "<" => a < b, ">" => a > b, "<=" => a <= b, _ => a >= b };
                }
            }
            return l;
        }

        private object? Additive()
        {
            var l = Unary();
            while (Is("+") || Is("-"))
            {
                var op = Next().Text;
                var r = Unary();
                l = op == "+" && (l is string || r is string) ? ToStr(l) + ToStr(r)
                    : op == "+" ? ToNumber(l) + ToNumber(r) : ToNumber(l) - ToNumber(r);
            }
            return l;
        }

        private object? Unary()
        {
            if (Is("!")) { _i++; return !Truthy(Unary()); }
            if (Is("-")) { _i++; return -ToNumber(Unary()); }
            return Postfix(Primary());
        }

        private object? Postfix(object? v)
        {
            while (Is("."))
            {
                _i++;
                var m = Next().Text;
                Expect("(");
                Expect(")");
                var s = ToStr(v);
                v = m switch
                {
                    "trim" => s.Trim(),
                    "toUpperCase" => s.ToUpperInvariant(),
                    "toLowerCase" => s.ToLowerInvariant(),
                    "toString" => s,
                    _ => throw new FormatException("method " + m),
                };
            }
            return v;
        }

        private object? Primary()
        {
            var t = Next();
            switch (t.Kind)
            {
                case T.Num: return double.Parse(t.Text, CultureInfo.InvariantCulture);
                case T.Str: return t.Text;
                case T.Op when t.Text == "(":
                    var v = Expr();
                    Expect(")");
                    return v;
                case T.Id:
                    switch (t.Text)
                    {
                        case "true": return true;
                        case "false": return false;
                        case "null": case "undefined": return null;
                        case "Tags":
                            // Tags("name").Read(): no value without the runtime
                            Expect("("); Next(); Expect(")"); Expect("."); if (Next().Text != "Read") throw new FormatException("Tags");
                            Expect("("); Expect(")");
                            return 0.0;
                        case "Faceplate":
                            Expect(".");
                            if (Next().Text != "Properties") throw new FormatException("Faceplate");
                            Expect(".");
                            return prop(Next().Text);
                        case "Boolean":
                        case "String":
                        case "Number":
                            Expect("(");
                            var a = Expr();
                            Expect(")");
                            return t.Text == "Boolean" ? Truthy(a) : t.Text == "String" ? ToStr(a) : ToNumber(a);
                        default:
                            if (_vars.TryGetValue(t.Text, out var x)) return x;
                            throw new FormatException("name " + t.Text);
                    }
            }
            throw new FormatException("unexpected " + t.Text);
        }

        private static double ToNumber(object? v) => v switch
        {
            null => double.NaN,
            bool b => b ? 1 : 0,
            double d => d,
            string s => s.Trim().Length == 0 ? 0 : double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN,
            _ => double.NaN,
        };

        private static string ToStr(object? v) => v switch
        {
            null => "undefined",
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            string s => s,
            _ => v.ToString() ?? "",
        };

        private static bool StrictEquals(object? a, object? b) => (a, b) switch
        {
            (null, null) => true,
            (double x, double y) => x == y,
            (string x, string y) => x == y,
            (bool x, bool y) => x == y,
            _ => false,
        };

        /// <summary>JavaScript ==: null only equals null, otherwise both sides as numbers unless both are strings.</summary>
        private static bool LooseEquals(object? a, object? b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is string sa && b is string sb) return sa == sb;
            return ToNumber(a) == ToNumber(b);
        }
    }
}
