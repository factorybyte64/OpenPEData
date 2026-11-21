using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenPEData.Viewer;

/// <summary>Content of the trend controls (trend areas with value and time axes, grid and legend) and the symbols of
/// the control tool bars.</summary>
public static partial class ScreenRenderer
{
    // ---------- trend content ----------

    /// <summary>Trend and function trend control: the visible trend areas stacked between caption and tool bar, each
    /// with its left/right value axes, bottom/top time axis (function trend: value axis), major grid lines and the
    /// trend legend. Areas, axes and trends come from the item (TrendAreas / FunctionTrendAreas); the parser emits only
    /// what is stored, so without stored parts the Openness defaults of HmiDefaults.json apply (sample 173). Without
    /// runtime data no curves are drawn; the time axis shows the configured range ending at 00:00:00.
    /// Layout measured in the WinCC Unified runtime (MTP700 simulation, sample 173, 2026-11-21): content 29 px from
    /// the left and 32 px from the right edge, legend at the top below the caption, the areas start 46 px below the
    /// caption with a legend and directly at the bottom edge of the content.</summary>
    private static void TrendContent(Canvas c, JsonObject p, string type, double iw, double top, double bottom)
    {
        var function = type == "HmiFunctionTrendControl";
        var areas = Composition(p, type, function ? "FunctionTrendAreas" : "TrendAreas").Where(Visible).ToList();
        if (areas.Count == 0) return;
        const double pad = 6;
        double x0 = 29, x1 = iw - 32, y0 = top + pad, y1 = bottom;
        if (x1 - x0 < 60) (x0, x1) = (pad, iw - pad);
        var legend = p["Legend"] as JsonObject;
        var trends = areas.SelectMany(a => Parts(a, function ? "FunctionTrends" : "Trends")).Where(Visible).ToList();
        if (legend is not null && Visible(legend) && trends.Count > 0 && y1 - (top + 46) > 40)
        {
            Legend(c, legend, trends, x0, top, x1 - x0);
            y0 = top + 46;
        }
        var spacing = Num(p["AreaSpacing"]) ?? 10;
        var weights = areas.Select(a => Math.Max(0.01, Num(a["SizeFactor"]) ?? 1)).ToList();
        var free = y1 - y0 - spacing * (areas.Count - 1);
        if (free < 20 || x1 - x0 < 40) return;
        var y = y0;
        for (var i = 0; i < areas.Count; i++)
        {
            var ah = free * weights[i] / weights.Sum();
            TrendArea(c, areas[i], function, x0, y, x1 - x0, ah);
            y += ah + spacing;
        }
    }

    /// <summary>The elements of a composition (e.g. TrendAreas) as drawn: the Openness default elements of the type
    /// (HmiDefaults.json), the style values applied to every element (a style holds one object per composition that
    /// applies to all its elements, also nested ones such as LeftValueAxes), then the stored elements by index.
    /// <see cref="Props"/> leaves the style object or the stored array under the key.</summary>
    private static List<JsonObject> Composition(JsonObject p, string type, string key)
    {
        var defaults = (Defaults.Value["items"]?[type]?[key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var stored = (p[key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var style = p[key] as JsonObject;
        var res = new List<JsonObject>();
        for (var i = 0; i < Math.Max(defaults.Count, stored.Count); i++)
        {
            var e = i < defaults.Count ? (JsonObject)defaults[i].DeepClone() : new JsonObject();
            if (style is not null) ApplyStyle(e, style);
            if (i < stored.Count) Merge(e, stored[i]);
            res.Add(e);
        }
        return res;
    }

    private static void ApplyStyle(JsonObject element, JsonObject style)
    {
        foreach (var (k, v) in style)
            switch (v)
            {
                case JsonObject o when element[k] is JsonArray parts:
                    foreach (var part in parts.OfType<JsonObject>()) ApplyStyle(part, o);
                    break;
                case JsonObject o when element[k] is JsonObject inner:
                    ApplyStyle(inner, o);
                    break;
                case JsonObject:
                    // style of a composition the element does not have (e.g. RightValueAxes): nothing to apply it to
                    break;
                default:
                    element[k] = v?.DeepClone();
                    break;
            }
    }

    private static bool Visible(JsonObject o) => !(o["Visible"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);

    private static IEnumerable<JsonObject> Parts(JsonObject o, string key) => (o[key] as JsonArray)?.OfType<JsonObject>() ?? [];

    /// <summary>One trend area: axes outside the plot rectangle, grid inside, axis lines in AxisColor.</summary>
    private static void TrendArea(Canvas c, JsonObject area, bool function, double x, double y, double w, double h)
    {
        var left = Parts(area, "LeftValueAxes").Where(Visible).ToList();
        var right = Parts(area, "RightValueAxes").Where(Visible).ToList();
        var bottomAxes = Parts(area, function ? "BottomValueAxes" : "BottomTimeAxes").Where(Visible).ToList();
        var topAxes = Parts(area, function ? "TopValueAxes" : "TopTimeAxes").Where(Visible).ToList();
        // sizes measured in the runtime (sample 173): a value axis takes 15 + 3 × label font size (48 px at 11, 57 px
        // at 14), a horizontal axis 13 px plus its label lines (time axis: date and time); the plot starts half a label
        // height below the top so that the top label fits, and the 0 line lies 2 px above the axis line
        double FontSize(JsonObject a) => Num(a["LabelFont"]?["Size"]) ?? 11;
        double AxisWidth(JsonObject a) => Math.Max(15 + 3 * FontSize(a), Math.Ceiling(MeasureText(ValueLabels(a).OrderByDescending(s => s.Length).FirstOrDefault() ?? "100", a["LabelFont"] as JsonObject).Width) + 10);
        double AxisHeight(JsonObject a) => 13 + (function ? 1 : 2) * Math.Ceiling(MeasureText("0", a["LabelFont"] as JsonObject).Height);
        var half = (left.FirstOrDefault() ?? right.FirstOrDefault()) is { } fa ? Math.Ceiling(MeasureText("0", fa["LabelFont"] as JsonObject).Height / 2) : 0;
        double pl = x + left.Sum(AxisWidth), pr = x + w - right.Sum(AxisWidth);
        double pt = y + Math.Max(half, topAxes.Sum(AxisHeight)), pb = y + h - bottomAxes.Sum(AxisHeight) - (bottomAxes.Count > 0 ? 2 : 0);
        if (pr - pl < 10 || pb - pt < 10) return;
        var plot = new System.Windows.Shapes.Rectangle { Width = pr - pl, Height = pb - pt, Fill = Brush(TreeBuilder.Str(area["BackColor"])) ?? Brushes.White };
        Canvas.SetLeft(plot, pl);
        Canvas.SetTop(plot, pt);
        c.Children.Add(plot);
        var grid = TreeBuilder.Str(area["GridLines"]) ?? "";
        var major = Brush(TreeBuilder.Str(area["MajorGridLinesColor"])) ?? new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9));
        var minor = Brush(TreeBuilder.Str(area["MinorGridLinesColor"])) ?? major;
        // horizontal grid from the first left (else right) value axis, vertical grid from the first bottom (else top) axis
        if ((left.FirstOrDefault() ?? right.FirstOrDefault()) is { } va)
        {
            var (div, sub) = Divisions(va);
            for (var i = 0; i <= div * sub; i++)
            {
                var isMajor = i % sub == 0;
                if (!grid.Contains(isMajor ? "HorizontalMajor" : "HorizontalMinor")) continue;
                Line(c, pl, pr, pb - (pb - pt) * i / (div * sub), null, isMajor ? major : minor);
            }
        }
        if ((bottomAxes.FirstOrDefault() ?? topAxes.FirstOrDefault()) is { } ha)
        {
            var (div, sub) = function ? Divisions(ha) : (1, 1);
            var stops = function
                ? Enumerable.Range(0, div * sub + 1).Select(i => ((double)i / (div * sub), i % sub == 0)).ToList()
                : TimeTicks(ha).Select(t => (t.Pos, true)).ToList();
            foreach (var (f, isMajor) in stops)
            {
                if (!grid.Contains(isMajor ? "VerticalMajor" : "VerticalMinor")) continue;
                var gx = pl + (pr - pl) * f;
                Line(c, gx, gx, pt, pb, isMajor ? major : minor);
            }
        }
        var ax = pl;
        foreach (var a in left)
        {
            ValueAxis(c, a, ax, pt, pb, true);
            ax -= AxisWidth(a);
        }
        ax = pr;
        foreach (var a in right)
        {
            ValueAxis(c, a, ax, pt, pb, false);
            ax += AxisWidth(a);
        }
        var ay = pb + (bottomAxes.Count > 0 ? 2 : 0);
        foreach (var a in bottomAxes)
        {
            HorizontalAxis(c, a, function, pl, pr, ay, true);
            ay += AxisHeight(a);
        }
        ay = pt;
        foreach (var a in topAxes)
        {
            HorizontalAxis(c, a, function, pl, pr, ay, false);
            ay -= AxisHeight(a);
        }
    }

    private static (int Div, int Sub) Divisions(JsonObject axis) =>
        ((int)Math.Clamp(Num(axis["DivisionCount"]) ?? 10, 1, 100), (int)Math.Clamp(Num(axis["SubDivisionCount"]) ?? 1, 1, 20));

    /// <summary>Labels of a value axis from BeginValue to EndValue in DivisionCount steps, formatted by OutputFormat
    /// ({I} integer, {F<n>} n decimals, else general).</summary>
    private static List<string> ValueLabels(JsonObject axis)
    {
        double b = Num(axis["BeginValue"]) ?? 0, e = Num(axis["EndValue"]) ?? 100;
        var div = Divisions(axis).Div;
        var fmt = TreeBuilder.Str(axis["OutputFormat"]) ?? "{I}";
        string F(double v) => fmt.StartsWith("{F", StringComparison.Ordinal) && int.TryParse(fmt[2..^1], out var n)
            ? v.ToString("F" + Math.Clamp(n, 0, 10), CultureInfo.InvariantCulture)
            : fmt == "{I}" ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("G6", CultureInfo.InvariantCulture);
        return [.. Enumerable.Range(0, div + 1).Select(i => F(b + (e - b) * i / div))];
    }

    private static bool Shows(JsonObject axis, string what) => (TreeBuilder.Str(axis["ScaleMode"]) ?? "Labels, Ticks").Contains(what);

    /// <summary>Vertical value axis at x (left of the plot when <paramref name="leftSide"/>): line, ticks, labels.</summary>
    private static void ValueAxis(Canvas c, JsonObject a, double x, double top, double bottom, bool leftSide)
    {
        Line(c, x, x, top, bottom, Brush(TreeBuilder.Str(a["AxisColor"])) ?? Brushes.Black);
        var labels = ValueLabels(a);
        var tick = Brush(TreeBuilder.Str(a["TickColor"])) ?? Brushes.Black;
        var color = Brush(TreeBuilder.Str(a["LabelColor"])) ?? Brushes.DimGray;
        var lastTop = double.PositiveInfinity;
        for (var i = 0; i < labels.Count; i++)
        {
            var ty = bottom - (bottom - top) * i / (labels.Count - 1);
            if (Shows(a, "Ticks"))
                Line(c, leftSide ? x - 4 : x, leftSide ? x : x + 4, ty, null, tick);
            if (!Shows(a, "Labels")) continue;
            var sz = MeasureText(labels[i], a["LabelFont"] as JsonObject);
            // labels that would overlap the previous one are left out (dense axes in small areas)
            var ly = ty - sz.Height / 2;
            if (ly + sz.Height > lastTop) continue;
            lastTop = ly;
            var tb = Label(labels[i], a["LabelFont"] as JsonObject, color);
            Canvas.SetLeft(tb, leftSide ? x - 6 - sz.Width : x + 6);
            Canvas.SetTop(tb, ly);
            c.Children.Add(tb);
        }
    }

    /// <summary>Horizontal axis at y (below the plot when <paramref name="below"/>): a value axis of the function trend
    /// or a time axis with TimeRangeFactor × TimeRangeBase ending at 00:00:00.</summary>
    private static void HorizontalAxis(Canvas c, JsonObject a, bool valueAxis, double left, double right, double y, bool below)
    {
        Line(c, left, right, y, null, Brush(TreeBuilder.Str(a["AxisColor"])) ?? Brushes.Black);
        var values = valueAxis ? ValueLabels(a) : [];
        var stops = valueAxis
            ? [.. values.Select((s, i) => ((double)i / Math.Max(1, values.Count - 1), s))]
            : TimeTicks(a);
        var tick = Brush(TreeBuilder.Str(a["TickColor"])) ?? Brushes.Black;
        var color = Brush(TreeBuilder.Str(a["LabelColor"])) ?? Brushes.DimGray;
        var font = a["LabelFont"] as JsonObject;
        double lastRight = double.NegativeInfinity;
        foreach (var (f, text) in stops)
        {
            var tx = left + (right - left) * f;
            if (Shows(a, "Ticks"))
                Line(c, tx, tx, below ? y : y - 4, below ? y + 4 : y, tick);
            if (!Shows(a, "Labels")) continue;
            var sz = MeasureText(text, font);
            // centred under the tick. Value labels: left out if they would overlap the previous one (the last one may
            // reach into the right margin, as "100" in the runtime). Time labels (date and time on two lines) scroll
            // with the time and are clipped at the ends of the axis.
            var lx = tx - sz.Width / 2;
            if (valueAxis && lx < lastRight + 4) continue;
            lastRight = lx + sz.Width;
            var tb = Label(text, font, color);
            tb.TextAlignment = TextAlignment.Center;
            UIElement el = tb;
            if (!valueAxis)
            {
                tb.Width = sz.Width;
                var visible = Math.Min(right, lx + sz.Width) - Math.Max(left, lx);
                if (visible <= 0) continue;
                tb.Clip = new RectangleGeometry(new Rect(Math.Max(0, left - lx), 0, visible, sz.Height));
            }
            Canvas.SetLeft(el, lx);
            Canvas.SetTop(el, below ? y + (valueAxis ? 7 : 8) : y - 5 - sz.Height);
            c.Children.Add(el);
        }
    }

    /// <summary>Ticks of a time axis as (position 0..1, label): a range of TimeRangeFactor × TimeRangeBase ending at
    /// 00:00:00 today, a tick every third of the range rounded to a whole step (runtime: 20 s at 1 minute), labelled
    /// with date and time in two lines in the US format of the runtime language en-US.</summary>
    private static List<(double Pos, string Text)> TimeTicks(JsonObject a)
    {
        var factor = Num(a["TimeRangeFactor"]) ?? 1;
        var span = TreeBuilder.Str(a["TimeRangeBase"]) switch
        {
            "Millisecond" => TimeSpan.FromMilliseconds(factor), "Second" => TimeSpan.FromSeconds(factor),
            "Hour" => TimeSpan.FromHours(factor), "Day" => TimeSpan.FromDays(factor), _ => TimeSpan.FromMinutes(factor),
        };
        if (span <= TimeSpan.Zero) span = TimeSpan.FromMinutes(1);
        double[] steps = [0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 20, 30, 60, 120, 300, 600, 900, 1200, 1800, 3600, 7200, 10800, 21600, 43200, 86400];
        var step = steps.FirstOrDefault(s => s >= span.TotalSeconds / 3 - 1e-9, 86400);
        var end = DateTime.Today;
        var us = CultureInfo.GetCultureInfo("en-US");
        var fmt = step < 1 ? "h:mm:ss.fff tt" : "h:mm:ss tt";
        var res = new List<(double, string)>();
        for (var k = 0; k * step <= span.TotalSeconds + 1e-9 && k < 200; k++)
        {
            var t = end.AddSeconds(-k * step);
            res.Add((1 - k * step / span.TotalSeconds, t.ToString("M/d/yyyy", us) + "\n" + t.ToString(fmt, us)));
        }
        res.Reverse();
        return res;
    }

    /// <summary>Legend at the top of the content (runtime, sample 173): the heading "Legend:" (runtime text, not in the
    /// file; 919299, size 10 whatever the legend font) 14 px below the caption, below it per trend a 20 × 2 px
    /// line in LineColor with a dot of 10 px and the display name 8 px after the line, centred 32.5 px below the
    /// caption. Trends without name are shown as "Trend n".</summary>
    private static void Legend(Canvas c, JsonObject legend, List<JsonObject> trends, double x, double top, double w)
    {
        var fore = Brush(TreeBuilder.Str(legend["ForeColor"])) ?? Brushes.Black;
        var font = legend["Font"] as JsonObject;
        var head = font?.DeepClone() as JsonObject ?? new JsonObject();
        head["Size"] = 10;
        var title = Label("Legend:", head, new SolidColorBrush(Color.FromRgb(0x91, 0x92, 0x99)));
        var hs = MeasureText("Legend:", head);
        Canvas.SetLeft(title, x);
        Canvas.SetTop(title, top + 18 - hs.Height / 2);
        c.Children.Add(title);
        var cy = top + 33;
        var lx = x;
        var n = 0;
        foreach (var t in trends)
        {
            n++;
            var color = Brush(TreeBuilder.Str(t["LineColor"])) ?? Brushes.Green;
            var line = new System.Windows.Shapes.Rectangle { Width = 20, Height = 2, Fill = color };
            Canvas.SetLeft(line, lx);
            Canvas.SetTop(line, cy - 1);
            c.Children.Add(line);
            var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = color };
            Canvas.SetLeft(dot, lx + 5);
            Canvas.SetTop(dot, cy - 5);
            c.Children.Add(dot);
            lx += 27;
            var name = Text(t["DisplayName"]);
            if (name.Length == 0) name = TreeBuilder.Str(t["Name"]) ?? "";
            if (name.Length == 0) name = "Trend " + n.ToString(CultureInfo.InvariantCulture);
            var tb = Label(name, font, fore);
            var sz = MeasureText(name, font);
            Canvas.SetLeft(tb, lx);
            Canvas.SetTop(tb, cy - sz.Height / 2);
            c.Children.Add(tb);
            lx += sz.Width + 16;
            if (lx > x + w) break;
        }
    }

    private static void Line(Canvas c, double xa, double xb, double ya, double? yb, Brush stroke)
    {
        // 1 px lines on the pixel grid
        c.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = Math.Round(xa) + 0.5, X2 = Math.Round(xb) + 0.5, Y1 = Math.Round(ya) + 0.5, Y2 = Math.Round(yb ?? ya) + 0.5,
            Stroke = stroke, StrokeThickness = 1, SnapsToDevicePixels = true,
        });
    }

    private static TextBlock Label(string text, JsonObject? font, Brush fore)
    {
        var tb = new TextBlock { Text = text, Foreground = fore };
        ApplyFont(tb, font);
        return tb;
    }

    private static Size MeasureText(string text, JsonObject? font)
    {
        var tb = Label(text, font, Brushes.Black);
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return tb.DesiredSize;
    }

    // ---------- tool bar symbols ----------

    /// <summary>Symbols of the default tool bar buttons per control type, in button order. The runtime draws its own
    /// icons, which are not in the project file; these are simple symbols of our own that stand for the function.</summary>
    private static readonly Dictionary<string, string[]> ToolBarSymbols = new()
    {
        ["HmiTrendControl"] = ["first", "prev", "pause", "next", "last", "zoomin", "zoomout", "zoomarea", "ruler", "original"],
        ["HmiFunctionTrendControl"] = ["zoomin", "zoomout", "zoomarea", "move", "original", "ruler", "pause"],
        ["HmiTrendCompanion"] = ["prev", "next"],
        ["HmiAlarmControl"] = ["ack", "reset", "loop", "info", "comment", "first", "prev", "next", "last", "sort", "filter", "list", "settings", "export"],
        ["HmiDetailedParameterControl"] = ["first", "prev", "next", "last", "new", "save", "delete", "import", "export", "settings"],
        ["HmiSystemDiagnosisControl"] = ["home", "up", "prev", "next"],
        ["HmiMediaControl"] = ["play", "pause", "stop", "back", "forward", "original"],
        ["HmiWebControl"] = ["back", "forward"],
        ["HmiProcessDiagnosisPlcCodeViewerControl"] = ["first", "prev", "next", "last", "zoomin", "zoomout"],
    };

    /// <summary>Path data of the symbols in a 20 × 20 box; symbols listed in <see cref="FilledSymbols"/> are filled.</summary>
    private static readonly Dictionary<string, string> SymbolPaths = new()
    {
        ["first"] = "M4,4 V16 M15,4 L6,10 L15,16 Z",
        ["prev"] = "M14,4 L5,10 L14,16 Z",
        ["next"] = "M6,4 L15,10 L6,16 Z",
        ["last"] = "M16,4 V16 M5,4 L14,10 L5,16 Z",
        ["play"] = "M6,4 L16,10 L6,16 Z",
        ["pause"] = "M6.5,4 V16 M13.5,4 V16",
        ["stop"] = "M5,5 H15 V15 H5 Z",
        ["zoomin"] = "M8.5,3 A5.5,5.5 0 1 1 8.49,3 M12.5,12.5 L17,17 M6,8.5 H11 M8.5,6 V11",
        ["zoomout"] = "M8.5,3 A5.5,5.5 0 1 1 8.49,3 M12.5,12.5 L17,17 M6,8.5 H11",
        ["zoomarea"] = "M3,3 H17 V17 H3 Z M7,10 H13 M10,7 V13",
        ["ruler"] = "M10,2 V18 M6,5 H14 M6,15 H14",
        ["original"] = "M3,8 V3 H8 M12,3 H17 V8 M17,12 V17 H12 M8,17 H3 V12",
        ["move"] = "M10,2 V18 M2,10 H18 M7,5 L10,2 L13,5 M7,15 L10,18 L13,15 M5,7 L2,10 L5,13 M15,7 L18,10 L15,13",
        ["ack"] = "M3,10 L8,15 L17,5",
        ["reset"] = "M15.5,6.5 A6,6 0 1 0 16,12 M16,2.5 V6.5 H12",
        ["loop"] = "M3,10 H13 M9,6 L13,10 L9,14 M16,3 V17",
        ["info"] = "M10,8 V16 M10,4 V5.5 M3,10 A7,7 0 1 1 3,10.01",
        ["comment"] = "M3,4 H17 V13 H8 L4,17 V13 H3 Z",
        ["sort"] = "M6,3 V17 M3,14 L6,17 L9,14 M14,17 V3 M11,6 L14,3 L17,6",
        ["filter"] = "M3,4 H17 L11,11 V17 L9,15 V11 Z",
        ["list"] = "M3,5 H17 M3,10 H17 M3,15 H17",
        ["settings"] = "M10,6 A4,4 0 1 1 9.99,6 M10,2 V5 M10,15 V18 M2,10 H5 M15,10 H18 M4.3,4.3 L6.5,6.5 M13.5,13.5 L15.7,15.7 M15.7,4.3 L13.5,6.5 M6.5,13.5 L4.3,15.7",
        ["export"] = "M10,3 V13 M6,9 L10,13 L14,9 M4,17 H16",
        ["import"] = "M10,13 V3 M6,7 L10,3 L14,7 M4,17 H16",
        ["back"] = "M16,10 H4 M9,5 L4,10 L9,15",
        ["forward"] = "M4,10 H16 M11,5 L16,10 L11,15",
        ["home"] = "M3,10 L10,3 L17,10 M5,8 V17 H15 V8",
        ["up"] = "M10,17 V3 M5,8 L10,3 L15,8",
        ["new"] = "M5,3 H12 L15,6 V17 H5 Z M12,3 V6 H15",
        ["save"] = "M4,4 H14 L16,6 V16 H4 Z M7,4 V8 H13 V4",
        ["delete"] = "M5,5 L15,15 M15,5 L5,15",
    };

    private static readonly HashSet<string> FilledSymbols = ["first", "prev", "next", "last", "play"];

    /// <summary>The symbol of button <paramref name="index"/> of the default tool bar of a control type; null if the
    /// type has no known symbols.</summary>
    internal static UIElement? ToolBarIcon(string type, int index, Brush? fore, double size)
    {
        if (!ToolBarSymbols.TryGetValue(type, out var symbols) || index >= symbols.Length
            || !SymbolPaths.TryGetValue(symbols[index], out var data)) return null;
        var color = fore ?? new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3B));
        var filled = FilledSymbols.Contains(symbols[index]);
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = color,
            StrokeThickness = symbols[index] == "pause" ? 2.5 : 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = filled ? color : null,
        };
        var box = new Canvas { Width = 20, Height = 20 };
        box.Children.Add(path);
        var s = Math.Max(8, Math.Round(size * 0.5));
        return new Viewbox { Width = s, Height = s, Child = box, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }
}
