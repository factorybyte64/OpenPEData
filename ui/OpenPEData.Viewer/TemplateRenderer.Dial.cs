using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Xml.Linq;

namespace OpenPEData.Viewer;

/// <summary>hmi-element:arcDial and hmi-element:straightDial (scales of gauge, clock, bar, slider) expanded into plain
/// SVG: hmi-feature:scale (begin/end value, maximum-sections, type Logarithmic, extend both), range (begin/end
/// value, also begin-surcharge, end-surcharge, scale-begin, scale-end) with ribbons, division (count, nested) and
/// autoMeasure with ticks and labels, marker with ticks and poleMarkers, thresholds (markers and lines; not yet the ribbons of the segmented bar modes). Semantics derived from the templates and
/// calibrated against the runtime (164, 2026-10-31): offsets are measured from the dial line along the outward
/// normal (arc: away from the centre); a tick runs from offset to offset + length; a label sits with its centre at
/// the offset.</summary>
public static partial class TemplateRenderer
{
    private static readonly XNamespace Feature = "http://svg.siemens.com/hmi/feature/";

    private sealed class Dial
    {
        public bool Arc;
        public double Cx, Cy, R, Begin, Range;           // arc: centre, radius, begin angle, angle range (degrees)
        public double Sx, Sy, Len, Dx, Dy;               // straight: start, length, unit direction of growing values
        public double SurBegin, SurEnd;                  // surcharge in pixels before begin / after end
        public double V0, V1 = 100;                      // scale values
        public bool Log;                                 // ScalingType Logarithmic (both values > 0)

        double F(double v) => Log ? Math.Log10(Math.Max(v, 1e-12)) : v;

        /// <summary>Position along the dial (pixels from the scale begin) of a value or a keyword.</summary>
        public double At(object? v) => v switch
        {
            "begin-surcharge" => -SurBegin,
            "end-surcharge" => Length + SurEnd,
            "scale-begin" => 0,
            "scale-end" => Length,
            // a value outside the scale stops at the end of the surcharge: bar and indicator stay inside the dial
            _ => F(V1) == F(V0) ? 0 : Math.Clamp((F(TemplateExpr.Num(v)) - F(V0)) / (F(V1) - F(V0)) * Length, -SurBegin, Length + SurEnd),
        };

        /// <summary>Value at position t (inverse of At).</summary>
        public double ValueAt(double t)
        {
            var f = F(V0) + (F(V1) - F(V0)) * t / Math.Max(Length, 1e-9);
            return Log ? Math.Pow(10, f) : f;
        }

        public double Length => Arc ? Math.Abs(Range) * Math.PI / 180 * R : Len;

        /// <summary>Point at position t (pixels) and offset o along the outward normal.</summary>
        public Point Point(double t, double o)
        {
            if (Arc)
            {
                var a = (Begin + Math.Sign(Range == 0 ? 1 : Range) * t / Math.Max(R, 1e-6) * 180 / Math.PI) * Math.PI / 180;
                return new Point(Cx + (R + o) * Math.Cos(a), Cy + (R + o) * Math.Sin(a));
            }
            // normal (-Dy, Dx): right of a scale growing upwards, below a scale growing to the right (the bar
            // templates put their ribbons at offset = width / 2 from start-x/start-y)
            return new Point(Sx + Dx * t - Dy * o, Sy + Dy * t + Dx * o);
        }
    }

    /// <summary>Expands the content of a pole marker (hmi:master-inline or hmi:master-include) into a target.</summary>
    private delegate void ContentExpander(XElement holder, XElement target, TemplateScope s, double w, double h);

    private static void ExpandDial(XElement e, XElement target, TemplateScope s, ContentExpander content)
    {
        _content = content;
        object? B(XElement x, string n) => x.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s) : x.Attribute(n)?.Value;
        double N(XElement x, string n) => TemplateExpr.Num(B(x, n));
        var d = new Dial { Arc = e.Name.LocalName == "arcDial" };
        if (d.Arc)
        {
            (d.Cx, d.Cy, d.R) = (N(e, "center-x"), N(e, "center-y"), N(e, "radius"));
            (d.Begin, d.Range) = (N(e, "begin-angle"), N(e, "angle-range"));
            if (d.R <= 0) return;
        }
        else
        {
            (d.Sx, d.Sy, d.Len) = (N(e, "start-x"), N(e, "start-y"), N(e, "length"));
            // StraightScale.Orientation: Vertical grows from the bottom upwards, Horizontal from left to right
            if (TemplateExpr.Str(B(e, "orientation")) == "Vertical")
            {
                (d.Dx, d.Dy) = (0, -1);
                d.Sy += d.Len;
            }
            else
                (d.Dx, d.Dy) = (1, 0);
        }
        (d.SurBegin, d.SurEnd) = (N(e, "begin-surcharge"), N(e, "end-surcharge"));
        var g = new XElement(Svg + "g");
        foreach (var scale in e.Elements(Feature + "scale"))
        {
            if (!Cond(scale, s)) continue;
            (d.V0, d.V1) = (N(scale, "begin-value"), N(scale, "end-value"));
            d.Log = TemplateExpr.Str(B(scale, "type")) is "Logarithmic" or "logarithmic" && d.V0 > 0 && d.V1 > 0;
            var sections = Math.Max(1, (int)N(scale, "maximum-sections"));
            // extend both (AutoScaling): the scale ends move outwards to the next step of the automatic division
            if (!d.Log && TemplateExpr.Str(B(scale, "extend")) == "both" && d.V1 != d.V0)
            {
                var step = NiceStep(d.V0, d.V1, sections);
                var (lo, hi) = (Math.Min(d.V0, d.V1), Math.Max(d.V0, d.V1));
                (lo, hi) = (Math.Floor(lo / step + 1e-9) * step, Math.Ceiling(hi / step - 1e-9) * step);
                (d.V0, d.V1) = d.V0 <= d.V1 ? (lo, hi) : (hi, lo);
            }
            foreach (var f in scale.Elements()) Feat(f, d, g, s, 0, d.Length, sections);
        }
        target.Add(g);
    }

    [ThreadStatic] private static ContentExpander? _content;

    private static bool Cond(XElement x, TemplateScope s) =>
        x.Attribute(Bind + "condition") is not { } c || TemplateExpr.Truthy(TemplateExpr.EvalAttr(c.Value, s));

    /// <summary>One feature in the interval [t0, t1] (pixels along the dial) of its parent.</summary>
    private static void Feat(XElement f, Dial d, XElement g, TemplateScope s, double t0, double t1, int sections)
    {
        if (!Cond(f, s)) return;
        object? B(string n) => f.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s) : f.Attribute(n)?.Value;
        double N(string n) => TemplateExpr.Num(B(n));
        switch (f.Name.LocalName)
        {
            case "range":
            {
                // a bound value without a runtime value (ProcessValue) counts as 0, as the runtime shows it
                bool Has(string n) => f.Attribute(Bind + n) != null || f.Attribute(n) != null;
                var a = Has("begin-value") ? d.At(B("begin-value") ?? 0.0) : -d.SurBegin;
                var b = Has("end-value") ? d.At(B("end-value") ?? 0.0) : d.Length + d.SurEnd;
                foreach (var r in f.Elements(Feature + "ribbons"))
                    if (Cond(r, s)) Ribbon(r, d, g, s, a, b);
                // threshold markers inside a range (bar, gauge, slider templates)
                foreach (var th in f.Elements(Feature + "thresholds"))
                    Feat(th, d, g, s, t0, t1, sections);
                break;
            }
            case "marker":
            {
                // a value marker: ticks and pole markers at one position; no runtime value counts as 0
                var t = d.At(B("value") ?? 0.0);
                foreach (var c in f.Elements())
                {
                    if (!Cond(c, s)) continue;
                    if (c.Name == Feature + "ticks") Marks(c, d, g, s, [t]);
                    else if (c.Name == Feature + "poleMarkers") Pole(c, d, g, s, t);
                }
                break;
            }
            case "thresholds":
            {
                // hmi-feature:thresholds config = HmiProps.Thresholds: per enabled threshold its ticks (lines) and pole
                // markers at its value; inside the marker content HmiProps is the threshold part (Color). Normal has no
                // marker. Without a stored Value the runtime puts them on 0 (2026-11-08, runtime reference sample 171;
                // the 90/80/20/10 % positions of the TIA editor are only an editor preview)
                if (B("config") is not JsonArray list) break;
                foreach (var th in list.OfType<JsonObject>())
                {
                    if (!TemplateExpr.Truthy(TemplateExpr.FromJson(th["Enable"]))) continue;
                    if (th["ThresholdMode"]?.ToString() is not ("Maximum" or "Upper" or "Lower" or "Minimum")) continue;
                    var value = th["Value"] is { } tv ? TemplateExpr.Num(TemplateExpr.FromJson(tv)) : 0;
                    var t = d.At(value);
                    var ts = new TemplateScope { Hmi = th, Local = s.Local, Param = s.Param, Width = s.Width, Height = s.Height };
                    foreach (var c in f.Elements())
                    {
                        if (!Cond(c, s)) continue;
                        if (c.Name == Feature + "ticks") Marks(c, d, g, ts, [t]);
                        else if (c.Name == Feature + "poleMarkers") Pole(c, d, g, ts, t, threshold: true);
                    }
                }
                break;
            }
            case "division":
            case "autoMeasure":
            {
                var count = f.Name.LocalName == "division" ? Math.Max(1, (int)N("count")) : NiceSections(d.V0, d.V1, sections);
                var pts = Enumerable.Range(0, count + 1).Select(i => t0 + (t1 - t0) * i / count).ToList();
                List<double>? values = null;
                // logarithmic scale, automatic division: the runtime labels linear steps (0, 200, ..., 1000 for 1..1000)
                // at their logarithmic positions; values below the scale begin sit at the begin (164 Screen_R8)
                if (d.Log && f.Name.LocalName == "autoMeasure" && t0 == 0 && Math.Abs(t1 - d.Length) < 1e-6)
                {
                    var step = NiceStep(d.V0, d.V1, sections);
                    values = [];
                    for (var v = Math.Floor(Math.Min(d.V0, d.V1) / step) * step; v <= Math.Max(d.V0, d.V1) + step * 1e-9; v += step)
                        values.Add(v);
                    pts = values.Select(v => Math.Clamp(d.At(v), 0, d.Length)).ToList();
                    count = pts.Count - 1;
                }
                foreach (var c in f.Elements())
                {
                    if (c.Name == Feature + "division" || c.Name == Feature + "autoMeasure")
                    {
                        for (var i = 0; i < count; i++) Feat(c, d, g, s, pts[i], pts[i + 1], sections);
                        continue;
                    }
                    Marks(c, d, g, s, pts, values);
                }
                break;
            }
        }
    }

    /// <summary>Ticks or labels at the division points, filtered by position-filter: begin, end, inner, regular (all).</summary>
    private static void Marks(XElement c, Dial d, XElement g, TemplateScope s, List<double> pts, List<double>? values = null)
    {
        if (!Cond(c, s)) return;
        object? B(string n) => c.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s) : c.Attribute(n)?.Value;
        double N(string n) => TemplateExpr.Num(B(n));
        var filter = TemplateExpr.Str(B("position-filter"));
        if (filter == "") filter = "regular";
        bool Keep(int i) => filter.Contains("regular") || (i == 0 && filter.Contains("begin"))
            || (i == pts.Count - 1 && filter.Contains("end")) || (i > 0 && i < pts.Count - 1 && filter.Contains("inner"));
        var full = d.Arc && Math.Abs(Math.Abs(d.Range) - 360) < 1e-6;
        for (var i = 0; i < pts.Count; i++)
        {
            if (!Keep(i)) continue;
            var t = pts[i];
            if (c.Name.LocalName == "ticks")
            {
                var o = N("offset");
                var (p, q) = (d.Point(t, o), d.Point(t, o + N("length")));
                var w = N("stroke-width");
                g.Add(new XElement(Svg + "line", new XAttribute("x1", Fmt(p.X)), new XAttribute("y1", Fmt(p.Y)),
                    new XAttribute("x2", Fmt(q.X)), new XAttribute("y2", Fmt(q.Y)),
                    new XAttribute("stroke", Color(B("stroke"))), new XAttribute("stroke-width", Fmt(w > 0 ? w : 1))));
            }
            else if (c.Name.LocalName == "labels")
            {
                var value = values != null && i < values.Count ? values[i] : d.ValueAt(t);
                // on a full circle the scale end coincides with the begin: label the end value there (only the end of
                // the whole scale, not the ends of nested divisions: Flat clock 12, 3, 6, 9)
                if (full && i == pts.Count - 1 && Math.Abs(t - d.Length) < 1e-6) value = d.V1;
                var text = Format(value, TemplateExpr.Str(B("output-format")));
                var font = c.Elements(Feature + "font").FirstOrDefault()?.Attribute(Bind + "scope") is { } fs
                    ? TemplateExpr.EvalAttr(fs.Value, s) as JsonObject : null;
                var size = TemplateExpr.Num(TemplateExpr.FromJson(font?["Size"]));
                if (size <= 0) size = 14;
                var m = TemplateExpr.Measure(text, font);
                var p = d.Point(t, N("offset"));
                g.Add(new XElement(Svg + "text", new XAttribute("x", Fmt(p.X)), new XAttribute("y", Fmt(p.Y - m.Height / 2 + m.Height * 0.8)),
                    new XAttribute("fill", Color(B("fill"))), new XAttribute("font-size", Fmt(size)),
                    new XAttribute("font-family", font?["Name"]?.ToString() ?? "SiemensSans"),
                    new XAttribute("font-weight", font?["Weight"]?.ToString() ?? "Normal"),
                    new XAttribute("text-anchor", "middle"), text));
            }
        }
    }

    /// <summary>Pole marker: its content (content-width × content-height) placed at position t and offset, aligned
    /// by alignment-horizontal/-vertical, rotated by angle around the point.</summary>
    private static void Pole(XElement c, Dial d, XElement g, TemplateScope s, double t, bool threshold = false)
    {
        if (_content == null) return;
        object? B(string n) => c.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s) : c.Attribute(n)?.Value;
        double N(string n) => TemplateExpr.Num(B(n));
        double w = N("content-width"), h = N("content-height");
        if (w <= 0 || h <= 0) return;
        var p = d.Point(t, N("offset"));
        // alignment -1: the content starts at the point (to the right / below), 0: centred, 1: ends at the point
        double x = p.X - w / 2 * (1 + N("alignment-horizontal")), y = p.Y - h / 2 * (1 + N("alignment-vertical"));
        var holder = c.Elements().FirstOrDefault(el => el.Name == Hmi + "master-inline" || el.Name == Hmi + "master-include");
        if (holder == null) return;
        var box = new XElement(Svg + "svg", new XAttribute("x", Fmt(x)), new XAttribute("y", Fmt(y)),
            new XAttribute("width", Fmt(w)), new XAttribute("height", Fmt(h)), new XAttribute("overflow", "visible"));
        _content(holder, box, s, w, h);
        var angle = N("angle");
        // threshold markers: the marker shape has its tip at the content origin. On a straight scale the rotation
        // centre is the corner of the content box (TIA editor, 2026-11-08). On an arc dial the content origin sits on
        // the point and the marker turns with the dial (angle relative to the normal at 12 o'clock), so the tip lies on
        // the threshold value (WinCC Unified runtime, 2026-11-08, sample 171)
        var (rx, ry) = (p.X, p.Y);
        if (threshold)
        {
            if (d.Arc)
            {
                angle += Math.Atan2(p.Y - d.Cy, p.X - d.Cx) * 180 / Math.PI + 90;
                box.SetAttributeValue("x", Fmt(p.X));
                box.SetAttributeValue("y", Fmt(p.Y));
            }
            else
                (rx, ry) = (x, y);
        }
        g.Add(angle != 0
            ? new XElement(Svg + "g", new XAttribute("transform", $"rotate({Fmt(angle)} {Fmt(rx)} {Fmt(ry)})"), box)
            : box);
    }

    private static void Ribbon(XElement r, Dial d, XElement g, TemplateScope s, double a, double b)
    {
        object? B(string n) => r.Attribute(Bind + n) is { } bb ? TemplateExpr.EvalAttr(bb.Value, s) : r.Attribute(n)?.Value;
        var o = TemplateExpr.Num(B("offset"));
        var w = TemplateExpr.Num(B("stroke-width"));
        if (w <= 0 || Math.Abs(b - a) < 1e-6) return;
        string dpath;
        var (p, q) = (d.Point(a, o), d.Point(b, o));
        if (d.Arc)
        {
            var rr = Math.Max(0, d.R + o);
            var sweepDeg = Math.Abs(b - a) / Math.Max(d.R, 1e-6) * 180 / Math.PI;
            var cw = (b > a) == (d.Range >= 0);
            dpath = $"M {Fmt(p.X)},{Fmt(p.Y)} A {Fmt(rr)},{Fmt(rr)} 0 {(sweepDeg > 180 ? 1 : 0)} {(cw ? 1 : 0)} {Fmt(q.X)},{Fmt(q.Y)}";
        }
        else
            dpath = $"M {Fmt(p.X)},{Fmt(p.Y)} L {Fmt(q.X)},{Fmt(q.Y)}";
        g.Add(new XElement(Svg + "path", new XAttribute("d", dpath), new XAttribute("fill", "none"),
            new XAttribute("stroke", Color(B("stroke"))), new XAttribute("stroke-width", Fmt(w))));
    }

    /// <summary>Number of sections with a 1/2/5 × 10^n step that covers the scale with at most max sections.</summary>
    private static int NiceSections(double v0, double v1, int max)
    {
        var span = Math.Abs(v1 - v0);
        return span <= 0 || max <= 0 ? 1 : Math.Max(1, (int)Math.Round(span / NiceStep(v0, v1, max)));
    }

    /// <summary>Smallest 1/2/2.5/5 × 10^n step that divides the scale into at most max sections.</summary>
    private static double NiceStep(double v0, double v1, int max)
    {
        var span = Math.Abs(v1 - v0);
        if (span <= 0 || max <= 0) return 1;
        var raw = span / max;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        foreach (var k in new[] { 1.0, 2, 2.5, 5, 10 })
            if (k * mag >= raw - 1e-9) return k * mag;
        return raw;
    }

    /// <summary>Label text: {I} integer, {F<n>} fixed point; otherwise the shortest form.</summary>
    private static string Format(double v, string fmt)
    {
        if (fmt.Contains("{I}")) return Math.Round(v).ToString(CultureInfo.InvariantCulture);
        var m = System.Text.RegularExpressions.Regex.Match(fmt, @"\{F(\d+)\}");
        if (m.Success) return v.ToString("F" + m.Groups[1].Value, CultureInfo.InvariantCulture);
        return Math.Round(v, 6).ToString(CultureInfo.InvariantCulture);
    }

    private static string Color(object? v) => v switch
    {
        double c => string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3:0.###})",
            ((uint)c >> 16) & 255, ((uint)c >> 8) & 255, (uint)c & 255, (((uint)c >> 24) & 255) / 255.0),
        _ => TemplateExpr.Str(v) is { Length: > 0 } str ? str : "black",
    };
}
