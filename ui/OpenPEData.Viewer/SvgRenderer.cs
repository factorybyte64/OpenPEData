using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace OpenPEData.Viewer;

/// <summary>Minimal SVG reader for the project graphics of WinCC Unified (Z1.5 R3), without external libraries: svg
/// (viewBox), g, path, rect, circle, ellipse, line, polyline, polygon; fill, stroke, stroke-width, stroke-linecap/-join,
/// fill-rule, opacity, fill-/stroke-opacity, style attribute, transform (matrix, translate, scale, rotate, skewX/Y).
/// Linear and radial gradients (stops, units, gradientTransform, href), clip-path, use (also of a symbol), embedded
/// images (data URI), single-line text, nested svg viewports (x/y/width/height). The result is a
/// vector image whose natural size is the viewBox.</summary>
public static class SvgRenderer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static DrawingImage? Load(string svgText)
    {
        var doc = XDocument.Parse(svgText);
        var root = doc.Root;
        if (root == null || root.Name.LocalName != "svg") return null;
        var ids = root.Descendants().Where(e => e.Attribute("id") != null)
            .GroupBy(e => e.Attribute("id")!.Value).ToDictionary(g => g.Key, g => g.First());
        var gradients = ids.Where(kv => kv.Value.Name.LocalName is "linearGradient" or "radialGradient")
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var vb = Numbers(root.Attribute("viewBox")?.Value);
        Rect box = vb.Count == 4 ? new Rect(vb[0], vb[1], vb[2], vb[3])
            : new Rect(0, 0, Length(root.Attribute("width")?.Value) ?? 100, Length(root.Attribute("height")?.Value) ?? 100);
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(box) };
        // transparent frame so the image keeps the viewBox as its size (Stretch then scales like the runtime)
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(box)));
        var style = new Style(gradients, ids).With(root);  // presentation attributes of the svg element itself (fill="none")
        foreach (var e in root.Elements()) Element(e, group, style);
        group.Freeze();
        var img = new DrawingImage(group);
        img.Freeze();
        return img;
    }

    /// <summary>Inherited presentation attributes.</summary>
    private sealed record Style(Dictionary<string, XElement> Gradients, Dictionary<string, XElement> Ids)
    {
        public string Fill = "black", Stroke = "none", FillRule = "nonzero", LineCap = "butt", LineJoin = "miter";
        public string? ClipPath;  // not inherited: set per element
        public double StrokeWidth = 1, Opacity = 1, FillOpacity = 1, StrokeOpacity = 1;

        public Style With(XElement e)
        {
            var s = this with { ClipPath = null };
            var attrs = e.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
            if (attrs.TryGetValue("style", out var css))
                foreach (var decl in css.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = decl.Split(':', 2);
                    if (kv.Length == 2) attrs[kv[0].Trim()] = kv[1].Trim();
                }
            if (attrs.TryGetValue("fill", out var v)) s.Fill = v;
            if (attrs.TryGetValue("stroke", out v)) s.Stroke = v;
            if (attrs.TryGetValue("fill-rule", out v)) s.FillRule = v;
            if (attrs.TryGetValue("stroke-linecap", out v)) s.LineCap = v;
            if (attrs.TryGetValue("stroke-linejoin", out v)) s.LineJoin = v;
            if (attrs.TryGetValue("stroke-width", out v) && Length(v) is { } sw) s.StrokeWidth = sw;
            if (attrs.TryGetValue("fill-opacity", out v) && Number(v) is { } fo) s.FillOpacity = fo;
            if (attrs.TryGetValue("stroke-opacity", out v) && Number(v) is { } so) s.StrokeOpacity = so;
            if (attrs.TryGetValue("clip-path", out v)) s.ClipPath = v;
            return s;
        }
    }

    private static void Element(XElement e, DrawingGroup parent, Style inherited, int depth = 0)
    {
        var name = e.Name.LocalName;
        if (name is "defs" or "title" or "desc" or "metadata" or "clipPath" or "linearGradient" or "radialGradient"
            or "style" or "namedview" or "symbol" || depth > 16) return;
        if (e.Attribute("display")?.Value == "none" || e.Attribute("visibility")?.Value is "hidden" or "collapse") return;
        var st = inherited.With(e);
        var group = new DrawingGroup();
        if (Number(e.Attribute("opacity")?.Value) is { } op) group.Opacity = op;
        if (e.Attribute("transform")?.Value is { } tr) group.Transform = Transform(tr);
        if (st.ClipPath != null && Regex.Match(st.ClipPath, @"url\(#([^)]+)\)") is { Success: true } cm
            && st.Ids.TryGetValue(cm.Groups[1].Value, out var clip) && Clip(clip, st.Ids) is { } cg)
            group.ClipGeometry = cg;
        if (name == "svg" && depth > 0)
        {
            // nested svg (rendering templates): a viewport at x/y, clipped to width/height
            double sx = Length(e.Attribute("x")?.Value) ?? 0, sy = Length(e.Attribute("y")?.Value) ?? 0;
            var tg = new TransformGroup();
            if (group.Transform != null) tg.Children.Add(group.Transform);
            tg.Children.Insert(0, new TranslateTransform(sx, sy));
            group.Transform = tg;
            if (e.Attribute("overflow")?.Value != "visible"
                && Length(e.Attribute("width")?.Value) is { } sw && Length(e.Attribute("height")?.Value) is { } sh)
                group.ClipGeometry = new RectangleGeometry(new Rect(0, 0, Math.Max(0, sw), Math.Max(0, sh)));
        }
        if (name == "text")
        {
            if (TextGeometry(e, st) is { } tgeo && Paint(st.Fill, st.FillOpacity, st) is { } tfill)
                group.Children.Add(new GeometryDrawing(tfill, null, tgeo));
        }
        else if (name is "g" or "svg" or "a" or "switch")
        {
            foreach (var c in e.Elements()) Element(c, group, st, depth + 1);
        }
        else if (name == "use")
        {
            // the referenced element (a symbol like a group) at x/y, styled by the use element
            if (Href(e) is { } id && st.Ids.TryGetValue(id, out var target))
            {
                var inner = new DrawingGroup();
                double ux = Length(e.Attribute("x")?.Value) ?? 0, uy = Length(e.Attribute("y")?.Value) ?? 0;
                if (ux != 0 || uy != 0) inner.Transform = new TranslateTransform(ux, uy);
                if (target.Name.LocalName == "symbol")
                    foreach (var c in target.Elements()) Element(c, inner, st, depth + 1);
                else
                    Element(target, inner, st, depth + 1);
                if (inner.Children.Count > 0) group.Children.Add(inner);
            }
        }
        else if (name == "image")
        {
            // embedded raster images only (data URI); external files are not part of the graphic
            var href = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
            var m = href == null ? null : Regex.Match(href, @"^data:image/[\w+.-]+;base64,(.*)$", RegexOptions.Singleline);
            if (m is { Success: true })
                try
                {
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.StreamSource = new System.IO.MemoryStream(Convert.FromBase64String(Regex.Replace(m.Groups[1].Value, @"\s", "")));
                    bi.EndInit();
                    bi.Freeze();
                    group.Children.Add(new ImageDrawing(bi, new Rect(Length(e.Attribute("x")?.Value) ?? 0, Length(e.Attribute("y")?.Value) ?? 0,
                        Length(e.Attribute("width")?.Value) ?? bi.Width, Length(e.Attribute("height")?.Value) ?? bi.Height)));
                }
                catch (Exception) { /* unreadable image data: left out */ }
        }
        else if (Shape(e, name) is { } geo)
        {
            if (geo.IsFrozen) geo = geo.Clone();  // Geometry.Parse returns a frozen StreamGeometry
            if (geo is PathGeometry pg) pg.FillRule = st.FillRule == "evenodd" ? FillRule.EvenOdd : FillRule.Nonzero;
            else if (geo is StreamGeometry sg) sg.FillRule = st.FillRule == "evenodd" ? FillRule.EvenOdd : FillRule.Nonzero;
            var fill = name is "line" or "polyline" && st.Fill == "black" && e.Attribute("fill") == null ? null : Paint(st.Fill, st.FillOpacity, st);
            Pen? pen = null;
            if (Paint(st.Stroke, st.StrokeOpacity, st) is { } sb && st.StrokeWidth > 0)
                pen = new Pen(sb, st.StrokeWidth)
                {
                    StartLineCap = Cap(st.LineCap), EndLineCap = Cap(st.LineCap),
                    LineJoin = st.LineJoin switch { "round" => PenLineJoin.Round, "bevel" => PenLineJoin.Bevel, _ => PenLineJoin.Miter },
                };
            group.Children.Add(new GeometryDrawing(fill, pen, geo));
        }
        if (group.Children.Count > 0) parent.Children.Add(group);
    }

    /// <summary>clipPath (clipPathUnits userSpaceOnUse): union of its shapes, each with its own transform.</summary>
    private static Geometry? Clip(XElement clip, Dictionary<string, XElement> ids)
    {
        var gg = new GeometryGroup { FillRule = FillRule.Nonzero };
        void Add(XElement x, System.Windows.Media.Transform? outer, int depth)
        {
            if (depth > 8) return;
            var tg = new TransformGroup();
            if (x.Attribute("transform")?.Value is { } tr) tg.Children.Add(Transform(tr));
            if (outer != null) tg.Children.Add(outer);
            var name = x.Name.LocalName;
            if (name == "g")
            {
                foreach (var c in x.Elements()) Add(c, tg, depth + 1);
                return;
            }
            if (name == "use" && Href(x) is { } id && ids.TryGetValue(id, out var target))
            {
                Add(target, tg, depth + 1);
                return;
            }
            if (Shape(x, name) is not { } geo) return;
            if (geo.IsFrozen) geo = geo.Clone();
            geo.Transform = tg;
            gg.Children.Add(geo);
        }
        foreach (var c in clip.Elements()) Add(c, clip.Attribute("transform")?.Value is { } ct ? Transform(ct) : null, 0);
        return gg.Children.Count > 0 ? gg : null;
    }

    private static Geometry? Shape(XElement e, string name)
    {
        double A(string a) => Length(e.Attribute(a)?.Value) ?? 0;
        try
        {
            switch (name)
            {
                case "path":
                    return e.Attribute("d")?.Value is { } d ? Geometry.Parse(NormalizePath(d)) : null;
                case "rect":
                    var rx = Length(e.Attribute("rx")?.Value) ?? Length(e.Attribute("ry")?.Value) ?? 0;
                    var ry = Length(e.Attribute("ry")?.Value) ?? rx;
                    return new RectangleGeometry(new Rect(A("x"), A("y"), A("width"), A("height")), rx, ry);
                case "circle":
                    return new EllipseGeometry(new Point(A("cx"), A("cy")), A("r"), A("r"));
                case "ellipse":
                    return new EllipseGeometry(new Point(A("cx"), A("cy")), A("rx"), A("ry"));
                case "line":
                    return new LineGeometry(new Point(A("x1"), A("y1")), new Point(A("x2"), A("y2")));
                case "polyline":
                case "polygon":
                    var n = Numbers(e.Attribute("points")?.Value);
                    if (n.Count < 4) return null;
                    var fig = new PathFigure { StartPoint = new Point(n[0], n[1]), IsClosed = name == "polygon" };
                    for (var i = 2; i + 1 < n.Count; i += 2) fig.Segments.Add(new LineSegment(new Point(n[i], n[i + 1]), true));
                    return new PathGeometry([fig]);
            }
        }
        catch (Exception) { /* unsupported path syntax: leave the element out */ }
        return null;
    }

    /// <summary>WPF path markup is close to SVG; it needs blanks between numbers like "1-2" or ".5.5" and knows no "e" exponent issues.</summary>
    private static string NormalizePath(string d)
    {
        d = Regex.Replace(d, @"(?<=\d)-", " -");
        d = Regex.Replace(d, @"(\.\d+)(?=\.)", "$1 ");
        return d.Replace(",", " ");
    }

    private static Brush? Paint(string v, double opacity, Style st)
    {
        v = v.Trim();
        if (v is "none" or "transparent" or "") return null;
        var m = Regex.Match(v, @"^url\(#([^)]+)\)");
        if (m.Success)
            return st.Gradients.TryGetValue(m.Groups[1].Value, out var grad) ? Gradient(grad, opacity, st.Gradients) : null;
        var c = ParseColor(v);
        if (c == null) return null;
        var b = new SolidColorBrush(c.Value) { Opacity = opacity };
        b.Freeze();
        return b;
    }

    /// <summary>linearGradient (x1, y1, x2, y2) and radialGradient (cx, cy, r, fx, fy) with stops (offset, stop-color,
    /// stop-opacity), gradientUnits (objectBoundingBox = relative, userSpaceOnUse = absolute), gradientTransform,
    /// spreadMethod; stops and attributes missing on the element come from the gradient named in xlink:href.</summary>
    private static Brush? Gradient(XElement g, double opacity, Dictionary<string, XElement> all)
    {
        var chain = new List<XElement> { g };
        for (var x = g; chain.Count < 8 && Href(x) is { } id && all.TryGetValue(id, out var next) && !chain.Contains(next); x = next)
            chain.Add(next);
        string? Attr(string n) => chain.Select(x => x.Attribute(n)?.Value).FirstOrDefault(a => a != null);
        var stopsOf = chain.FirstOrDefault(x => x.Elements().Any(s => s.Name.LocalName == "stop"));
        var stops = new GradientStopCollection();
        foreach (var s in stopsOf?.Elements().Where(s => s.Name.LocalName == "stop") ?? [])
        {
            var sc = s.Attribute("stop-color")?.Value ?? StyleValue(s.Attribute("style")?.Value, "stop-color") ?? "black";
            var so = Number(s.Attribute("stop-opacity")?.Value ?? StyleValue(s.Attribute("style")?.Value, "stop-opacity")) ?? 1;
            var off = s.Attribute("offset")?.Value?.Trim() ?? "0";
            var o = (Number(off) ?? 0) / (off.EndsWith('%') ? 100 : 1);
            if (ParseColor(sc) is not { } col) continue;
            col.A = (byte)Math.Round(col.A * Math.Clamp(so, 0, 1));
            stops.Add(new GradientStop(col, Math.Clamp(o, 0, 1)));
        }
        if (stops.Count == 0) return null;
        var user = Attr("gradientUnits") == "userSpaceOnUse";
        double P(string n, double def)
        {
            var a = Attr(n);
            if (a == null || Number(a) is not { } num) return def;
            return a.Trim().EndsWith('%') ? num / 100 : num;
        }
        GradientBrush brush;
        if (g.Name.LocalName == "radialGradient")
        {
            double cx = P("cx", 0.5), cy = P("cy", 0.5), r = P("r", 0.5);
            brush = new RadialGradientBrush(stops)
            {
                Center = new Point(cx, cy), GradientOrigin = new Point(P("fx", cx), P("fy", cy)), RadiusX = r, RadiusY = r,
            };
        }
        else
            brush = new LinearGradientBrush(stops, new Point(P("x1", 0), P("y1", 0)), new Point(P("x2", 1), P("y2", 0)));
        brush.MappingMode = user ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox;
        brush.SpreadMethod = Attr("spreadMethod") switch { "reflect" => GradientSpreadMethod.Reflect, "repeat" => GradientSpreadMethod.Repeat, _ => GradientSpreadMethod.Pad };
        if (Attr("gradientTransform") is { } gt)
        {
            if (user) brush.Transform = Transform(gt);
            else brush.RelativeTransform = Transform(gt);
        }
        brush.Opacity = opacity;
        brush.Freeze();
        return brush;
    }

    private static string? Href(XElement e)
    {
        var h = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
        return h != null && h.StartsWith('#') ? h[1..] : null;
    }

    private static string? StyleValue(string? css, string key) =>
        css?.Split(';').Select(x => x.Split(':', 2)).FirstOrDefault(kv => kv.Length == 2 && kv[0].Trim() == key)?[1].Trim();

    private static Color? ParseColor(string v)
    {
        var ma = Regex.Match(v, @"^rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*([\d.]+)\s*\)$");
        if (ma.Success)
            return Color.FromArgb((byte)Math.Round(255 * Math.Clamp(double.Parse(ma.Groups[4].Value, CultureInfo.InvariantCulture), 0, 1)),
                byte.Parse(ma.Groups[1].Value), byte.Parse(ma.Groups[2].Value), byte.Parse(ma.Groups[3].Value));
        var m = Regex.Match(v, @"^rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$");
        if (m.Success)
            return Color.FromRgb(byte.Parse(m.Groups[1].Value), byte.Parse(m.Groups[2].Value), byte.Parse(m.Groups[3].Value));
        if (Regex.IsMatch(v, "^#[0-9a-fA-F]{3}$")) v = "#" + string.Concat(v.Skip(1).Select(ch => $"{ch}{ch}"));
        try { return (Color)ColorConverter.ConvertFromString(v); }
        catch (Exception) { return null; }
    }

    private static Transform Transform(string t)
    {
        var res = new TransformGroup();
        foreach (Match m in Regex.Matches(t, @"(\w+)\s*\(([^)]*)\)"))
        {
            var n = Numbers(m.Groups[2].Value);
            System.Windows.Media.Transform? x = m.Groups[1].Value switch
            {
                "matrix" when n.Count == 6 => new MatrixTransform(n[0], n[1], n[2], n[3], n[4], n[5]),
                "translate" when n.Count >= 1 => new TranslateTransform(n[0], n.Count > 1 ? n[1] : 0),
                "scale" when n.Count >= 1 => new ScaleTransform(n[0], n.Count > 1 ? n[1] : n[0]),
                "rotate" when n.Count >= 1 => new RotateTransform(n[0], n.Count > 2 ? n[1] : 0, n.Count > 2 ? n[2] : 0),
                "skewX" when n.Count == 1 => new SkewTransform(n[0], 0),
                "skewY" when n.Count == 1 => new SkewTransform(0, n[0]),
                _ => null,
            };
            // SVG applies the list from right to left: the last transform acts first on the element
            if (x != null) res.Children.Insert(0, x);
        }
        return res;
    }

    /// <summary>text (single line): x/y = start of the baseline, text-anchor start/middle/end, font-family, font-size,
    /// font-weight (number or name), font-style, text-decoration.</summary>
    private static Geometry? TextGeometry(XElement e, Style st)
    {
        var text = string.Concat(e.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
        if (text.Length == 0) return null;
        string? A(string n) => e.Attribute(n)?.Value;
        var size = Length(A("font-size")) ?? 14;
        var weight = A("font-weight") switch
        {
            null or "normal" or "Normal" or "400" => FontWeights.Normal,
            "bold" or "Bold" or "700" => FontWeights.Bold,
            { } w when int.TryParse(w, out var wn) => FontWeight.FromOpenTypeWeight(Math.Clamp(wn, 1, 999)),
            { } w => new FontWeightConverter().ConvertFromInvariantString(w) is FontWeight fw ? fw : FontWeights.Normal,
        };
        var style = A("font-style") is "italic" or "Italic" ? FontStyles.Italic : FontStyles.Normal;
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(TemplateExpr.FontFamilyOf(A("font-family") ?? "SiemensSans"), style, weight, FontStretches.Normal),
            size, Brushes.Black, 1.0);
        var deco = A("text-decoration");
        if (deco == "underline") ft.SetTextDecorations(TextDecorations.Underline);
        else if (deco == "line-through") ft.SetTextDecorations(TextDecorations.Strikethrough);
        double x = Length(A("x")) ?? 0, y = Length(A("y")) ?? 0;
        x -= A("text-anchor") switch { "middle" => ft.WidthIncludingTrailingWhitespace / 2, "end" => ft.WidthIncludingTrailingWhitespace, _ => 0 };
        return ft.BuildGeometry(new Point(x, y - ft.Baseline));
    }

    private static PenLineCap Cap(string v) => v switch { "round" => PenLineCap.Round, "square" => PenLineCap.Square, _ => PenLineCap.Flat };

    private static List<double> Numbers(string? s) =>
        s == null ? [] : Regex.Matches(s, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")
            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();

    private static double? Number(string? s) => Numbers(s) is { Count: > 0 } n ? n[0] : null;

    /// <summary>Length with unit; px/pt/mm only scale relative to each other, the viewBox decides the drawing.</summary>
    private static double? Length(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var n = Number(s);
        if (n == null) return null;
        return s.Trim().EndsWith('%') ? null : n;
    }
}
