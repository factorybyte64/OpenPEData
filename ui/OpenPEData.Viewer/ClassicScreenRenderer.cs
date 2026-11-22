using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OpenPEData.Viewer;

/// <summary>Draws a screen of a classic WinCC device (Comfort Panel, WinCC Runtime Advanced; parser screens with
/// platform "Classic") as the runtime shows it. Effective properties of an item:
/// the Openness defaults of its type (HmiClassicDefaults.json, from the WithDefaults export of sample C01), the
/// stored properties, and for an item linked to a style item (parser styleItem) the values of that style item of the
/// device (parser style.values, DeviceStyleInstance). Reference: runtime simulation screenshots of sample C01.</summary>
public static class ClassicScreenRenderer
{
    private static readonly Lazy<JsonObject> Defaults = new(() =>
    {
        using var s = typeof(ClassicScreenRenderer).Assembly.GetManifestResourceStream("HmiClassicDefaults.json");
        return s == null ? new JsonObject() : JsonNode.Parse(s) as JsonObject ?? new JsonObject();
    });

    /// <summary>Screens of the same HMI software (same style object) by name, for templates.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonObject, Dictionary<string, JsonObject>> ScreensByStyle = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonNode, JsonObject> StyleOf = new();
    [ThreadStatic] private static JsonObject? _style;
    [ThreadStatic] private static bool _focusTaken;
    [ThreadStatic] private static double _screenWidth;
    [ThreadStatic] private static double _screenHeight;
    [ThreadStatic] private static JsonObject? _firstButton;

    /// <summary>Comfort Panel (true) or PC with WinCC Runtime Advanced (false), from the parser style.deviceType.</summary>
    private static bool IsPanel => TreeBuilder.Str(_style?["deviceType"]) is not { } t
        || t.Contains("Comfort", StringComparison.OrdinalIgnoreCase) || t.Contains("Panel", StringComparison.OrdinalIgnoreCase)
        || t.StartsWith("TP", StringComparison.OrdinalIgnoreCase) || t.StartsWith("KP", StringComparison.OrdinalIgnoreCase) || t.StartsWith("KTP", StringComparison.OrdinalIgnoreCase);

    public static bool IsClassic(JsonNode? screen) => TreeBuilder.Str(screen?["platform"]) == "Classic";

    public static void Register(JsonNode screen, JsonObject? style)
    {
        if (style == null || screen is not JsonObject so) return;
        StyleOf.AddOrUpdate(screen, style);
        var d = ScreensByStyle.GetValue(style, _ => new Dictionary<string, JsonObject>());
        if (TreeBuilder.Str(so["name"]) is { } n) d[n] = so;
    }

    public static Canvas Render(JsonObject screen, IEnumerable<JsonNode> items)
    {
        _style = StyleOf.TryGetValue(screen, out var st) ? st : null;
        _focusTaken = FocusItem != null;
        _firstButton = null;
        _screenWidth = Num(screen["width"]) ?? 800;
        _screenHeight = Num(screen["height"]) ?? 480;
        var sd = Defaults.Value["screen"] as JsonObject;
        var w = Num(screen["width"]) ?? 800;
        var h = Num(screen["height"]) ?? 480;
        var canvas = new Canvas
        {
            Width = w,
            Height = h,
            Background = Brush(TreeBuilder.Str(screen["backColor"]) ?? TreeBuilder.Str(sd?["BackColor"])) ?? Brushes.Silver,
            ClipToBounds = true,
            SnapsToDevicePixels = true,
        };
        // template items lie below the items of the screen (runtime: the template is the background layer)
        if (TreeBuilder.Str(screen["template"]) is { } tn && _style != null && ScreensByStyle.TryGetValue(_style, out var all)
            && all.TryGetValue(tn, out var tpl) && !ReferenceEquals(tpl, screen))
            AddItems(canvas, TreeBuilder.Arr(tpl["items"]));
        AddItems(canvas, items);
        // without an input field the runtime puts the focus on the first button: a light blue double frame
        if ((!_focusTaken || FocusItem != null) && _firstButton is { } fb)
        {
            var (bw, bh) = (Num(fb["Width"]) ?? 0, Num(fb["Height"]) ?? 0);
            var r = Corner(fb);
            var focus = new Grid { Width = bw, Height = bh, IsHitTestVisible = false };
            focus.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x8A, 0xAE)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(r) });
            focus.Children.Add(new Border { Margin = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x76, 0x91)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Math.Max(r - 1, 0)) });
            canvas.Children.Add(At(focus, Num(fb["Left"]) ?? 0, Num(fb["Top"]) ?? 0));
        }
        _firstButton = null;
        return canvas;
    }

    /// <summary>A slide-in screen (kind SlideinScreen, from the top) as opened over a screen of the given width
    /// (runtime TP900 / TP1200, sample C05): the slide-in screen at the top, below it the handle (gray, the middle third
    /// of the width, 58 px high on 1280 px wide screens, else 51) with three grip lines (51 px, light over dark) in its
    /// middle.</summary>
    public static Canvas RenderSlideIn(JsonObject slide, IEnumerable<JsonNode> items, double screenWidth)
    {
        var content = Render(slide, items);
        var h = Num(slide["height"]) ?? 120;
        var hh = screenWidth >= 1280 ? 58 : 51;
        var c = new Canvas { Width = screenWidth, Height = h + hh };
        c.Children.Add(content);
        var x0 = Math.Floor(screenWidth / 3);
        var x1 = Math.Ceiling(screenWidth * 2 / 3);
        c.Children.Add(At(new Rectangle { Width = x1 - x0, Height = hh, Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128)) }, x0, h));
        var gx = Math.Round(screenWidth / 2) - 25;
        var gy = h + Math.Floor((hh - 11) / 2.0);
        for (var i = 0; i < 3; i++)
        {
            c.Children.Add(At(new Rectangle { Width = 51, Height = 1, Fill = new SolidColorBrush(Color.FromRgb(223, 223, 223)) }, gx, gy + 4 * i));
            c.Children.Add(At(new Rectangle { Width = 51, Height = 1, Fill = new SolidColorBrush(Color.FromRgb(32, 32, 32)) }, gx, gy + 4 * i + 1));
        }
        return c;
    }

    private static void AddItems(Canvas canvas, IEnumerable<JsonNode> items)
    {
        foreach (var it in items.OrderBy(i => Num(i["layerIndex"]) ?? 0).ThenBy(i => Num(i["zIndex"]) ?? 0))
        {
            var p = Props(it);
            ApplyDynamics(it, p);
            if (p["Visible"] is JsonValue vv && vv.TryGetValue<bool>(out var vis) && !vis) continue;
            if (TreeBuilder.Str(it["type"]) == "FaceplateInstance")
            {
                AddItems(canvas, FaceplateItems(it, p));
                continue;
            }
            FrameworkElement? el;
            try { el = Item(TreeBuilder.Str(it["type"]) ?? "", p); }
            catch (Exception e) { el = Placeholder(p, TreeBuilder.Str(it["name"]) + ": " + e.GetType().Name); }
            if (el == null) continue;
            if (FocusItem != null ? TreeBuilder.Str(it["name"]) == FocusItem
                : _firstButton == null && TreeBuilder.Str(it["type"]) == "Button" && Bool(p["Enabled"], true)) _firstButton = p;
            Canvas.SetLeft(el, Num(p["Left"]) ?? 0);
            Canvas.SetTop(el, Num(p["Top"]) ?? 0);
            canvas.Children.Add(el);
        }
    }

    /// <summary>The items of a faceplate instance (sample C06): the items of the type moved to the instance position
    /// (after its own animations), with the interface of the instance applied. A text or a static value of a property
    /// without dynamics goes to the connected inner property. Inner animations and tag connections on an interface tag
    /// use the instance's HMI tag; a static value replaces the tag only for interface properties of a fixed type (the
    /// runtime ignores static values of properties with a dynamic data type: the I/O field shows 0, range and
    /// visibility animations keep the configured look); without a tag or such a value the animation is dropped.</summary>
    private static IEnumerable<JsonNode> FaceplateItems(JsonNode it, JsonObject p)
    {
        if (it["faceplate"] is not JsonObject fp || fp["items"] is not JsonArray inner) return [];
        var dx = (Num(p["Left"]) ?? 0) - (Num(fp["originLeft"]) ?? 0);
        var dy = (Num(p["Top"]) ?? 0) - (Num(fp["originTop"]) ?? 0);
        var iface = it["interface"] as JsonObject ?? new JsonObject();
        var defs = (fp["interface"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var byTag = new Dictionary<string, (JsonObject Def, JsonObject? Val)>();
        foreach (var def in defs)
        {
            var name = TreeBuilder.Str(def["name"]) ?? "";
            var val = iface[name] as JsonObject;
            byTag[TreeBuilder.Str(def["tag"]) ?? name] = (def, val);
        }
        var outl = new List<JsonNode>();
        foreach (var src in inner.OfType<JsonObject>())
        {
            var c = (JsonObject)src.DeepClone();
            var props = c["properties"] as JsonObject ?? new JsonObject();
            c["properties"] = props;
            var (l, t) = ((Num(c["left"]) ?? 0) + dx, (Num(c["top"]) ?? 0) + dy);
            c["left"] = l;
            c["top"] = t;
            props["Left"] = l;
            props["Top"] = t;
            if (TreeBuilder.Str(c["styleItem"]) == null && TreeBuilder.Str(props["StyleItemString_ModuleDevice"]) is { } si) c["styleItem"] = si;
            c["layerIndex"] = it["layerIndex"]?.DeepClone();
            var itemName = TreeBuilder.Str(c["name"]);
            if (itemName != null && it["itemOverrides"]?[itemName] is JsonObject ov)
                foreach (var (k, v) in ov)
                {
                    props[k] = v?.DeepClone();
                    if (k is "Width" or "Height") c[k.ToLowerInvariant()] = v?.DeepClone();
                }
            foreach (var def in defs)
            {
                if (iface[TreeBuilder.Str(def["name"]) ?? ""] is not JsonObject val) continue;
                foreach (var cn in (def["connections"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (TreeBuilder.Str(cn["item"]) != itemName || TreeBuilder.Str(cn["property"]) is not { } prop) continue;
                    if (val["text"] is JsonObject txt)
                    {
                        props[prop] = txt.DeepClone();
                        if (prop == "Text") c["text"] = txt.DeepClone();
                    }
                    else if (val["value"] is { } v && !Bool(def["animatable"]))
                        props[prop] = v.DeepClone();
                }
            }
            if (c["dynamizations"] is JsonArray dyns)
            {
                var keep = new JsonArray();
                foreach (var d in dyns.OfType<JsonObject>())
                {
                    if (TreeBuilder.Str(d["tag"]) is { } tag && byTag.TryGetValue(tag, out var b))
                    {
                        if (TreeBuilder.Str(b.Val?["tag"]) is { } real) d["tag"] = real;
                        else if (b.Val?["value"] is { } sv && TreeBuilder.Str(b.Def["type"]) != null) d["constant"] = sv.DeepClone();
                        else continue;
                    }
                    keep.Add(d.DeepClone());
                }
                c["dynamizations"] = keep;
            }
            outl.Add(c);
        }
        return outl;
    }

    /// <summary>Effective properties: type defaults, stored values, style item values (items linked to a style item).</summary>
    internal static JsonObject Props(JsonNode it)
    {
        var type = TreeBuilder.Str(it["type"]) ?? "";
        var res = (Defaults.Value["items"]?[type] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        if (it["properties"] is JsonObject stored)
            foreach (var (k, v) in stored) res[k] = v?.DeepClone();
        var useStyle = TreeBuilder.Str(it["styleItem"]) != null && !(res["UseDesignColorSchema"] is JsonValue u && u.TryGetValue<bool>(out var ub) && !ub);
        if (useStyle && _style?["values"]?[type] is JsonObject sv)
            foreach (var (k, v) in sv)
                if (k != "StyleItemName") res[k] = v?.DeepClone();
        foreach (var (k, n) in new[] { ("Left", "left"), ("Top", "top"), ("Width", "width"), ("Height", "height") })
            if (res[k] == null && it[n] is JsonValue v) res[k] = v.DeepClone();
        if (it["text"] is JsonObject t && t.Count > 0 && res["Text"] == null) res["Text"] = t.DeepClone();
        if (it["trends"] is JsonArray trends) res["__trends"] = trends.DeepClone();
        return res;
    }

    // ---------- items ----------

    private static FrameworkElement? Item(string type, JsonObject p)
    {
        var w = Num(p["Width"]) ?? 0;
        var h = Num(p["Height"]) ?? 0;
        return type switch
        {
            "Line" => Line(p),
            "Polyline" => Poly(p, false),
            "Polygon" => Poly(p, true),
            "Rectangle" => Box(p, w, h, null, Corner(p)),
            "Ellipse" or "Circle" => EllipseItem(p, w, h),
            "TextField" => Box(p, w, h, Text(p["Text"]), Corner(p)),
            "IOField" => IoField(p, w, h),
            "DateTimeField" => DateTimeField(p, w, h),
            "SymbolicIOField" => SymbolicIo(p, w, h),
            "Button" => Button(p, w, h),
            "Switch" => Switch(p, w, h),
            "GraphicView" => GraphicBox(p, w, h, false),
            "GraphicIOField" => GraphicBox(p, w, h, true),
            "Bar" => Bar(p, w, h),
            "Slider" => Slider(p, w, h),
            "Gauge" => Gauge(p, w, h),
            "Clock" => Clock(p, w, h),
            "AlarmView" => AlarmView(p, w, h),
            "TrendView" => TrendView(p, w, h),
            "UserView" => UserView(p, w, h),
            "StatusForce" => WatchTable(p, w, h),
            "RecipeView" => RecipeView(p, w, h),
            "SysDiagControl" => SysDiag(p, w, h),
            "PLCCodeViewer" or "CriteriaAnalysisView" or "S7GraphOverview" or "ProDiagOverview" or "FxTrendView" => Control(type, p, w, h),
            "HTMLBrowser" => HtmlBrowser(p, w, h),
            "SmartClientView" => SmartClient(p, w, h),
            "MediaPlayer" => IsPanel ? MediaPlayer(w, h) : null,
            "CameraControl" => Camera(w, h),
            "PDFView" => PdfView(w, h),
            "SymbolLibrary" => Frame(p, w, h),
            // a group draws nothing itself: its items are screen items with their own positions (sample C05)
            "Group" => null,
            _ => Placeholder(p, type),
        };
    }

    // ---------- tag values and dynamics ----------

    /// <summary>Tag values for the preview (tag name -> double, bool, string or DateTime). Without a value a tag is 0 /
    /// empty, as in the runtime without a PLC connection.</summary>
    [ThreadStatic] public static IReadOnlyDictionary<string, object?>? TagValues;

    /// <summary>Name of the item that has the input focus (e.g. the button pressed last); null: the runtime's start focus
    /// (first input field, else the first button).</summary>
    [ThreadStatic] public static string? FocusItem;

    private static object? TagValue(string? tag) => tag != null && TagValues != null && TagValues.TryGetValue(tag, out var v) ? v : null;

    private static double TagNum(string? tag) => TagValue(tag) switch
    {
        double d => d,
        bool b => b ? 1 : 0,
        long l => l,
        int i => i,
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => 0,
    };

    private static JsonNode? TagNode(string? tag) => TagValue(tag) switch
    {
        null => JsonValue.Create(0.0),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b ? 1.0 : 0.0),
        DateTime t => JsonValue.Create(t.ToString("o", CultureInfo.InvariantCulture)),
        var v => JsonValue.Create(Convert.ToDouble(v, CultureInfo.InvariantCulture)),
    };

    /// <summary>Property that receives the foreground color of an appearance animation: the line color of lines,
    /// the border of shapes, else the text color (runtime, sample C03).</summary>
    private static string ForeProperty(string type) => type switch
    {
        "Line" or "Polyline" => "Color",
        "Rectangle" or "Ellipse" or "Circle" or "Polygon" => "BorderColor",
        _ => "ForeColor",
    };

    /// <summary>Applies the animations and tag connections of an item with the current tag values (runtime, sample C03):
    /// appearance (range: the entry containing the value; bit: the entry of the bit's state; bits: the entry of the one
    /// set bit, several set bits change nothing), visibility, object enabling, movements (clamped to their range),
    /// direct movement (offset from the configured position, kept on the screen) and tag connections.</summary>
    private static void ApplyDynamics(JsonNode it, JsonObject p)
    {
        if (it["dynamizations"] is not JsonArray dyns || dyns.Count == 0) return;
        var type = TreeBuilder.Str(it["type"]) ?? "";
        static long F(JsonNode? n) => (long)(Num(n) ?? 0);
        static bool Cond(JsonObject dd, double x) => TreeBuilder.Str(dd["mode"]) == "Bit"
            ? (((long)x >> (int)F(dd["from"])) & 1) == 1
            : x >= F(dd["from"]) && x <= F(dd["to"]);
        foreach (var d in dyns.OfType<JsonObject>())
        {
            var tag = TreeBuilder.Str(d["tag"]);
            var constant = d["constant"];
            var v = constant != null ? Num(constant) ?? 0 : TagNum(tag);
            switch (TreeBuilder.Str(d["type"]))
            {
                case "Appearance":
                {
                    var entries = (d["entries"] as JsonArray ?? []).OfType<JsonObject>().ToList();
                    JsonObject? hit = null;
                    switch (TreeBuilder.Str(d["mode"]))
                    {
                        case "Bit":
                            var state = ((long)v >> (int)F(d["bit"])) & 1;
                            hit = entries.FirstOrDefault(e => F(e["from"]) == state);
                            break;
                        case "Bits":
                            var bits = (ulong)(long)v;
                            if (bits != 0 && (bits & (bits - 1)) == 0)
                                hit = entries.FirstOrDefault(e => F(e["from"]) == System.Numerics.BitOperations.TrailingZeroCount(bits));
                            break;
                        default:
                            hit = entries.FirstOrDefault(e => v >= F(e["from"]) && v <= F(e["to"]));
                            break;
                    }
                    // the slider ignores appearance animations; the clock colors its dial and hands (runtime, C03)
                    if (hit != null && type == "Clock")
                    {
                        p["DialColor"] = hit["backColor"]?.DeepClone();
                        p["NeedleColor"] = hit["foreColor"]?.DeepClone();
                    }
                    else if (hit != null && type != "Slider")
                    {
                        p["BackColor"] = hit["backColor"]?.DeepClone();
                        if (type == "Button") p["MiddleGradientColor"] = hit["backColor"]?.DeepClone();
                        p[ForeProperty(type)] = hit["foreColor"]?.DeepClone();
                    }
                    break;
                }
                case "Visibility":
                    p["Visible"] = Cond(d, v) == Bool(d["visible"], true);
                    break;
                case "ObjectEnable":
                    p["Enabled"] = Cond(d, v) == Bool(d["enabled"], true);
                    break;
                case "Movement":
                {
                    var (from, to) = (F(d["from"]), F(d["to"]));
                    var f = to != from ? Math.Clamp((v - from) / (to - from), 0, 1) : 0;
                    p["Left"] = (Num(p["Left"]) ?? 0) + Math.Round(F(d["dx"]) * f);
                    p["Top"] = (Num(p["Top"]) ?? 0) + Math.Round(F(d["dy"]) * f);
                    break;
                }
                case "DirectMovement":
                {
                    var w = Num(p["Width"]) ?? 0;
                    var h = Num(p["Height"]) ?? 0;
                    // the slider's thumb lies right of the item and stays on the screen too
                    if (type == "Slider" && Bool(p["ShowThumb"], true))
                        w += Math.Max(Math.Round((Num((p["Font"] as JsonObject)?["Size"]) ?? 11) * 8 / 3), 32) + 2;
                    var x = (Num(p["Left"]) ?? 0) + TagNum(TreeBuilder.Str(d["tagX"]));
                    var y = (Num(p["Top"]) ?? 0) + TagNum(TreeBuilder.Str(d["tagY"]));
                    p["Left"] = Math.Max(0, Math.Min(x, _screenWidth - w));
                    p["Top"] = Math.Max(0, Math.Min(y, _screenHeight - h));
                    break;
                }
                case "Tag":
                    if (TreeBuilder.Str(d["property"]) is { } prop) p[prop] = constant?.DeepClone() ?? TagNode(tag);
                    break;
            }
        }
    }

    // ---------- value helpers ----------

    internal static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d
        : n is JsonValue s && s.TryGetValue<string>(out var str) && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d2) ? d2 : null;

    private static bool Bool(JsonNode? n, bool dflt = false) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : dflt;

    internal static Color? Col(JsonNode? n)
    {
        var s = TreeBuilder.Str(n);
        if (s == null || !s.StartsWith("0x") || s.Length != 10 || !uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static Brush? Brush(string? argb) => ScreenRenderer.Brush(argb);

    private static SolidColorBrush SB(JsonNode? n, Color dflt) => new(Col(n) ?? dflt);

    /// <summary>Enum value of a property: the style stores integers, the Openness defaults names.</summary>
    private static string Enum(JsonNode? n, params string[] names)
    {
        if (n is JsonValue v && v.TryGetValue<double>(out var d))
        {
            var i = (int)d;
            return i >= 0 && i < names.Length ? names[i] : i.ToString(CultureInfo.InvariantCulture);
        }
        return TreeBuilder.Str(n) ?? (names.Length > 0 ? names[0] : "");
    }

    /// <summary>BackFillStyle: Solid (0), Transparent (0x10000 / "Transparent"), gradient (0x100001 and others with bit 20).</summary>
    private static string FillKind(JsonNode? n)
    {
        if (n is JsonValue v && v.TryGetValue<double>(out var d))
        {
            var i = (long)d;
            if (i == 0) return "Solid";
            if (i == 0x10000) return "Transparent";
            if ((i & 0x100000) != 0) return "Gradient";
            if (i == 1) return "Transparent";
            return "Solid";
        }
        var s = TreeBuilder.Str(n) ?? "Solid";
        return s.Contains("Transparent", StringComparison.Ordinal) ? "Transparent" : s.Contains("Gradient", StringComparison.Ordinal) ? "Gradient" : "Solid";
    }

    private static Brush? Fill(JsonObject p, string prefix = "")
    {
        var kind = FillKind(p[prefix + "BackFillStyle"]);
        if (kind == "Transparent") return null;
        var back = Col(p[prefix + "BackColor"]) ?? Colors.Gray;
        if (kind == "Gradient" && Bool(p["UseFirstGradient"], true))
        {
            var first = Col(p["FirstGradientColor"]) ?? back;
            var mid = Col(p["MiddleGradientColor"]) ?? back;
            var second = Bool(p["UseSecondGradient"], true) ? Col(p["SecondGradientColor"]) ?? back : mid;
            return new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(first, 0), new GradientStop(mid, 0.36), new GradientStop(mid, 0.55), new GradientStop(second, 1),
            }, 90);
        }
        return new SolidColorBrush(back);
    }

    private static double Corner(JsonObject p) => Num(p["CornerRadius"]) ?? Math.Max(Num(p["RoundCornerWidth"]) ?? 0, 0);

    /// <summary>EdgeStyle: 0/"Solid" single border, 6 double border (outer and inner line BorderColor, between them
    /// BorderBackColor), "None"/transparent: none.</summary>
    private static bool DoubleEdge(JsonObject p) => p["EdgeStyle"] is JsonValue v && v.TryGetValue<double>(out var d) && (int)d == 6;

    private static bool NoEdge(JsonObject p) => TreeBuilder.Str(p["EdgeStyle"]) is "None" or "Transparent"
        || (p["EdgeStyle"] is JsonValue v && v.TryGetValue<double>(out var d) && (int)d == 1);

    // ---------- shapes ----------

    /// <summary>Rectangle-like item (rectangle, text field, I/O field, date/time field): fill, border (single or
    /// double), text with margins and alignment.</summary>
    private static FrameworkElement Box(JsonObject p, double w, double h, string? text, double radius)
    {
        var grid = new Grid { Width = Math.Max(w, 0), Height = Math.Max(h, 0) };
        var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
        var fill = Fill(p);
        var border = Col(p["BorderColor"]) ?? Colors.Black;
        if (DoubleEdge(p) && bw >= 3)
        {
            var back = Col(p["BorderBackColor"]) ?? border;
            grid.Children.Add(new Border { Background = new SolidColorBrush(border), CornerRadius = new CornerRadius(radius) });
            grid.Children.Add(new Border { Margin = new Thickness(1), Background = new SolidColorBrush(back), CornerRadius = new CornerRadius(Math.Max(radius - 1, 0)) });
            grid.Children.Add(new Border { Margin = new Thickness(bw - 1), Background = new SolidColorBrush(border) });
            grid.Children.Add(new Border { Margin = new Thickness(bw), Background = fill ?? Brushes.Transparent });
        }
        else
        {
            grid.Children.Add(new Border
            {
                Background = fill,
                BorderBrush = bw > 0 ? new SolidColorBrush(border) : null,
                BorderThickness = new Thickness(bw),
                CornerRadius = new CornerRadius(radius),
            });
        }
        if (!string.IsNullOrEmpty(text))
        {
            var tb = TextBlockOf(p, text);
            var m = new Thickness(bw + (Num(p["LeftMargin"]) ?? 0), bw + (Num(p["TopMargin"]) ?? 0),
                bw + (Num(p["RightMargin"]) ?? 0), bw + (Num(p["BottomMargin"]) ?? 0));
            var aw = Math.Max(w - m.Left - m.Right, 0);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (tb.DesiredSize.Width <= aw)
            {
                tb.Margin = m;
                grid.Children.Add(tb);
            }
            else
            {
                // text wider than the field: the runtime keeps the alignment within the margins and clips only at
                // the border (nav buttons 28 px wide show "Text" completely)
                var c = new Canvas { Margin = new Thickness(bw), ClipToBounds = true };
                var x = tb.HorizontalAlignment switch
                {
                    HorizontalAlignment.Center => (aw - tb.DesiredSize.Width) / 2,
                    HorizontalAlignment.Right => aw - tb.DesiredSize.Width,
                    _ => 0,
                };
                var ah = Math.Max(h - m.Top - m.Bottom, 0);
                var y = tb.VerticalAlignment switch
                {
                    VerticalAlignment.Center => (ah - tb.DesiredSize.Height) / 2,
                    VerticalAlignment.Bottom => ah - tb.DesiredSize.Height,
                    _ => 0,
                };
                c.Children.Add(At(tb, Math.Round(x + m.Left - bw), Math.Round(y + m.Top - bw)));
                grid.Children.Add(c);
            }
            grid.Tag = tb;
        }
        return grid;
    }

    private static TextBlock TextBlockOf(JsonObject p, string text, string fontKey = "Font", string colorKey = "ForeColor")
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = SB(p[colorKey], Colors.Black),
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = Enum(p["HorizontalAlignment"], "Left", "Center", "Right") switch
            {
                "Center" => HorizontalAlignment.Center, "Right" => HorizontalAlignment.Right, _ => HorizontalAlignment.Left,
            },
            VerticalAlignment = Enum(p["VerticalAlignment"], "Top", "Middle", "Bottom") switch
            {
                "Middle" => VerticalAlignment.Center, "Bottom" => VerticalAlignment.Bottom, _ => VerticalAlignment.Top,
            },
        };
        ApplyFont(tb, p[fontKey] as JsonObject);
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
        return tb;
    }

    /// <summary>Classic font sizes are pixel heights of the em (runtime: Tahoma 15 has 11 px high digits).</summary>
    internal static void ApplyFont(TextBlock tb, JsonObject? f)
    {
        var name = TreeBuilder.Str(f?["Name"]) ?? "Tahoma";
        tb.FontFamily = new FontFamily(name + ", Tahoma, Segoe UI");
        tb.FontSize = Num(f?["Size"]) ?? 11;
        if (Bool(f?["Bold"])) tb.FontWeight = FontWeights.Bold;
        if (Bool(f?["Italic"])) tb.FontStyle = FontStyles.Italic;
        var deco = new TextDecorationCollection();
        if (Bool(f?["Underline"])) deco.Add(TextDecorations.Underline);
        if (Bool(f?["Strikeout"])) deco.Add(TextDecorations.Strikethrough);
        if (deco.Count > 0) tb.TextDecorations = deco;
    }

    private static FrameworkElement EllipseItem(JsonObject p, double w, double h)
    {
        var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
        return new Ellipse
        {
            Width = w,
            Height = h,
            Fill = Fill(p),
            Stroke = bw > 0 ? SB(p["BorderColor"], Colors.Black) : null,
            StrokeThickness = bw,
        };
    }

    private static FrameworkElement? Line(JsonObject p)
    {
        // a line is given by its start and end point in screen coordinates; both 0 = a line of length 0 (not drawn)
        var x1 = Num(p["StartLeft"]) ?? 0;
        var y1 = Num(p["StartTop"]) ?? 0;
        var x2 = Num(p["EndLeft"]) ?? 0;
        var y2 = Num(p["EndTop"]) ?? 0;
        if (x1 == x2 && y1 == y2) return null;
        var lw = Num(p["LineWidth"]) ?? 1;
        // GDI covers the pixel of a coordinate: odd widths are centred on the pixel (no 2 px anti-aliased line)
        var o = Snap(lw);
        var l = (Num(p["Left"]) ?? 0) - o;
        var t = (Num(p["Top"]) ?? 0) - o;
        return new System.Windows.Shapes.Line
        {
            X1 = x1 - l, Y1 = y1 - t, X2 = x2 - l, Y2 = y2 - t,
            Stroke = SB(p["Color"], Colors.Black),
            StrokeThickness = lw,
            StrokeDashArray = DashOf(p["Style"]),
        };
    }

    private static double Snap(double width) => (int)Math.Round(width) % 2 == 1 ? 0.5 : 0;

    private static DoubleCollection? DashOf(JsonNode? style) => Enum(style, "Solid", "Dash", "Dot", "DashDot", "DashDotDot") switch
    {
        "Dash" => [4, 2], "Dot" => [1, 2], "DashDot" => [4, 2, 1, 2], "DashDotDot" => [4, 2, 1, 2, 1, 2], _ => null,
    };

    /// <summary>Points of a polyline/polygon: parser DataPoints (x, y in screen coordinates) or the Openness
    /// default text "&lt;Item&gt;x, y&lt;/Item&gt;…".</summary>
    private static List<Point> Points(JsonObject p)
    {
        var pts = new List<Point>();
        if (p["DataPoints"] is JsonArray a)
        {
            for (var i = 0; i + 1 < a.Count; i += 2)
                pts.Add(new Point(Num(a[i]) ?? 0, Num(a[i + 1]) ?? 0));
        }
        else if (TreeBuilder.Str(p["Points"]) is { } s)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(s, @"(-?\d+),\s*(-?\d+)"))
                pts.Add(new Point(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        }
        return pts;
    }

    private static FrameworkElement? Poly(JsonObject p, bool closed)
    {
        var pts = Points(p);
        if (pts.Count < 2) return null;
        var o = Snap(closed ? NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1 : Num(p["LineWidth"]) ?? 1);
        var l = (Num(p["Left"]) ?? 0) - o;
        var t = (Num(p["Top"]) ?? 0) - o;
        var rel = new PointCollection(pts.Select(q => new Point(q.X - l, q.Y - t)));
        if (closed)
        {
            var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
            return new Polygon { Points = rel, Fill = Fill(p), Stroke = bw > 0 ? SB(p["BorderColor"], Colors.Black) : null, StrokeThickness = bw };
        }
        return new Polyline { Points = rel, Stroke = SB(p["Color"], Colors.Black), StrokeThickness = Num(p["LineWidth"]) ?? 1, StrokeDashArray = DashOf(p["Style"]) };
    }

    // ---------- fields ----------

    /// <summary>I/O field without process value: 0 in the format of FormatPattern (999.999 -> "0.000").</summary>
    /// <summary>Text of an I/O field (runtime, sample C03): DataFormat String shows the text, '#' × FieldLength when it
    /// is longer. Decimal: the format pattern ("s99999", "999.999") gives the decimal places and the number of characters
    /// without the sign (the decimal point counts); ShiftDecimalPoint moves the point of an integer pattern; rounding
    /// half away from zero; 's' shows "+" for values ≥ 0, a negative value (also −0) shows "-"; a value with more
    /// characters shows '#' × pattern length.</summary>
    internal static string IoValue(JsonObject p)
    {
        var pv = p["ProcessValue"];
        // DataFormat: the parser emits the stored integer (2 = String, sample C02), the Openness defaults the name
        if (Num(p["DataFormat"]) == 2 || TreeBuilder.Str(p["DataFormat"]) == "String")
        {
            var str = pv is JsonValue sv && sv.TryGetValue<string>(out var t) ? t : "";
            var len = (int)(Num(p["FieldLength"]) ?? 10);
            return str.Length > len ? new string('#', len) : str;
        }
        var fmt = TreeBuilder.Str(p["FormatPattern"]) ?? Num(p["FormatPattern"])?.ToString(CultureInfo.InvariantCulture) ?? "999.999";
        var signed = fmt.StartsWith('s');
        var body = signed ? fmt[1..] : fmt;
        var dot = body.IndexOf('.');
        var dec = dot >= 0 ? body.Length - dot - 1 : 0;
        var v = Num(pv) ?? 0;
        var shift = (int)(Num(p["ShiftDecimalPoint"]) ?? 0);
        if (shift > 0 && dec == 0)
        {
            v /= Math.Pow(10, shift);
            dec = shift;
        }
        var neg = v < 0 || double.IsNegative(v);
        var digits = Math.Round(Math.Abs(v), Math.Min(dec, 15), MidpointRounding.AwayFromZero).ToString("F" + dec, CultureInfo.InvariantCulture);
        if (digits.Length > body.Length || double.IsInfinity(v) || double.IsNaN(v))
            return new string('#', fmt.Length);
        return (neg ? "-" : signed ? "+" : "") + digits;
    }

    /// <summary>I/O field. The runtime puts the input focus on the first input field of the screen: its value is
    /// selected (white on navy).</summary>
    private static FrameworkElement IoField(JsonObject p, double w, double h)
    {
        var text = IoValue(p);
        var g = (Grid)Box(p, w, h, text, Corner(p));
        var input = Enum(p["Mode"], "Output", "Input", "InOutput") != "Output" && Bool(p["Enabled"], true);
        if (input && !_focusTaken && g.Tag is TextBlock tb)
        {
            _focusTaken = true;
            tb.Foreground = Brushes.White;
            tb.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0x80));
        }
        return g;
    }

    /// <summary>Date/time field: a box with the date and time; an input field (Mode Input/InOutput) ends a text that
    /// does not fit with an ellipsis (runtime, C02).</summary>
    private static FrameworkElement DateTimeField(JsonObject p, double w, double h)
    {
        var g = (Grid)Box(p, w, h, DateTimeText(p), Corner(p));
        if (Enum(p["Mode"], "Output", "Input", "InOutput") != "Output" && g.Tag is TextBlock tb && tb.Parent is Canvas)
        {
            var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
            tb.Width = Math.Max(w - 2 * bw - (Num(p["LeftMargin"]) ?? 0) - (Num(p["RightMargin"]) ?? 0), 0);
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            Canvas.SetLeft(tb, Num(p["LeftMargin"]) ?? 0);
        }
        return g;
    }

    private static string DateTimeText(JsonObject p)
    {
        // runtime (C02, en-US): short date and the time with AM/PM, the long format with weekday and month name
        var now = DateTime.Now;
        var date = Bool(p["ShowDate"], true)
            ? now.ToString(Bool(p["LongDateTimeFormat"]) ? "dddd, MMMM d, yyyy" : "M/d/yyyy", CultureInfo.InvariantCulture) : "";
        var time = Bool(p["ShowTime"], true) ? now.ToString("h:mm:ss tt", CultureInfo.InvariantCulture) : "";
        return (date + " " + time).Trim();
    }

    private static FrameworkElement SymbolicIo(JsonObject p, double w, double h)
    {
        var g = (Grid)Box(p, w, h, "", Corner(p));
        var bw = Num(p["BorderWidth"]) ?? 1;
        // drop-down arrow at the right edge (runtime: an outlined triangle)
        var size = Math.Min(16, h - 2 * bw - 6);
        if (size > 4 && Bool(p["ShowDropDownButton"], true))
        {
            var tri = new Polygon
            {
                Points = [new Point(0, 0), new Point(size, 0), new Point(size / 2, size * 0.6)],
                Stroke = SB(p["ForeColor"], Colors.Black),
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, bw + 6, 0),
            };
            g.Children.Add(tri);
        }
        // text of the text list entry, clipped before the drop-down button
        var text = SymbolicText(p);
        if (text.Length > 0)
        {
            var tb = TextBlockOf(p, text);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var right = bw + (size > 4 && Bool(p["ShowDropDownButton"], true) ? size + 8 : 0);
            var clip = new Canvas { Margin = new Thickness(bw, bw, right, bw), ClipToBounds = true };
            var ml = Num(p["LeftMargin"]) ?? 2;
            var ah = Math.Max(h - 2 * bw, 0);
            clip.Children.Add(At(tb, ml, Math.Round((ah - tb.DesiredSize.Height) / 2)));
            g.Children.Add(clip);
        }
        return g;
    }

    /// <summary>Text of a symbolic I/O field: the entry of its text list for the process value (runtime, sample C03).
    /// Value list (Decimal): the single value or range containing the value, else the default entry; bit list (Bit):
    /// the entry of 0 or 1; bit number list (Binary): the entry of the one set bit, nothing when several are set.</summary>
    internal static string SymbolicText(JsonObject p)
    {
        var hit = ListEntry(p["TextList"], p, ScreenRenderer.TextListByName);
        return hit?["text"] switch
        {
            JsonObject t => t.Select(kv => TreeBuilder.Str(kv.Value)).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "",
            var t => TreeBuilder.Str(t) ?? "",
        };
    }

    /// <summary>Entry of a text or graphic list (reference {name} or name) for the process value: value list (Decimal)
    /// the single value or range containing the value, else the default entry; bit list (Bit) the entry of 0 or 1; bit
    /// number list (Binary) the entry of the one set bit, none when no or several bits are set (runtime, C03, C05).</summary>
    private static JsonObject? ListEntry(JsonNode? list, JsonObject p, Func<string, JsonNode?> byName)
    {
        if (RefName(list) is not { } ln || byName(ln) is not { } tl)
            return null;
        var v = Num(p["ProcessValue"]) ?? 0;
        var entries = TreeBuilder.Arr(tl["entries"]).OfType<JsonObject>().ToList();
        switch (TreeBuilder.Str(tl["listRange"]))
        {
            case "Binary":
                var bits = (ulong)(long)v;
                return bits != 0 && (bits & (bits - 1)) == 0
                    ? entries.FirstOrDefault(e => Num(e["from"]) == System.Numerics.BitOperations.TrailingZeroCount(bits)) : null;
            case "Bit":
                return entries.FirstOrDefault(e => Num(e["from"]) == (v != 0 ? 1 : 0));
            default:
                return entries.FirstOrDefault(e => !Bool(e["default"]) && v >= (Num(e["from"]) ?? 0) && v <= (Num(e["to"]) ?? Num(e["from"]) ?? 0))
                    ?? entries.FirstOrDefault(e => Bool(e["default"]));
        }
    }

    /// <summary>Graphic stretched to w×h at the inset as the runtime scales it: bitmaps by nearest neighbor (pixel
    /// rows and columns are dropped or repeated, thin lines look dashed, sample C05), vector graphics smoothly.</summary>
    private static FrameworkElement StretchedImage(ImageSource img, double w, double h, double inset)
    {
        var (dw, dh) = ((int)Math.Round(w), (int)Math.Round(h));
        if (img is BitmapSource bs && dw > 0 && dh > 0 && (bs.PixelWidth != dw || bs.PixelHeight != dh))
        {
            var src = bs.Format == PixelFormats.Bgra32 ? bs : new FormatConvertedBitmap(bs, PixelFormats.Bgra32, null, 0);
            var (sw, sh) = (src.PixelWidth, src.PixelHeight);
            var sp = new byte[sw * sh * 4];
            src.CopyPixels(sp, sw * 4, 0);
            var dp = new byte[dw * dh * 4];
            for (var y = 0; y < dh; y++)
            {
                var sy = Math.Min((int)((y + 0.5) * sh / dh), sh - 1);
                for (var x = 0; x < dw; x++)
                {
                    var sx = Math.Min((int)((x + 0.5) * sw / dw), sw - 1);
                    Buffer.BlockCopy(sp, (sy * sw + sx) * 4, dp, (y * dw + x) * 4, 4);
                }
            }
            var scaled = BitmapSource.Create(dw, dh, 96, 96, PixelFormats.Bgra32, null, dp, dw * 4);
            scaled.Freeze();
            img = scaled;
        }
        var image = new Image { Source = img, Stretch = Stretch.Fill, Width = Math.Max(w, 0), Height = Math.Max(h, 0), Margin = new Thickness(inset),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        return image;
    }

    private static string? RefName(JsonNode? n) => n is JsonObject o ? TreeBuilder.Str(o["name"]) : TreeBuilder.Str(n);

    /// <summary>Project graphic as the classic runtime draws it: a gif without its transparency (the transparent pixels
    /// in their palette color), with a transparent color the pixels of that color transparent.</summary>
    private static ImageSource? ClassicGraphic(string name, Color? transparent)
    {
        if (ScreenRenderer.Graphic(name) is not { } img) return null;
        var gif = string.Equals(TreeBuilder.Str(ScreenRenderer.GraphicInfo(name)?["extension"]), ".gif", StringComparison.OrdinalIgnoreCase);
        if (!gif && transparent == null || img is not BitmapSource bs) return img;
        var src = new FormatConvertedBitmap(bs, gif ? PixelFormats.Bgr32 : PixelFormats.Bgra32, null, 0);
        var (pw, ph) = (src.PixelWidth, src.PixelHeight);
        var px = new byte[pw * ph * 4];
        src.CopyPixels(px, pw * 4, 0);
        for (var i = 0; i < px.Length; i += 4)
        {
            if (gif) px[i + 3] = 255;
            if (transparent is { } t && px[i] == t.B && px[i + 1] == t.G && px[i + 2] == t.R) px[i + 3] = 0;
        }
        var res = BitmapSource.Create(pw, ph, 96, 96, PixelFormats.Bgra32, null, px, pw * 4);
        res.Freeze();
        return res;
    }

    /// <summary>Button by mode (runtime, sample C05): Text (TextOff), TextList (entry of TextList for the process
    /// value), Graphic (PictureOff), GraphicList (entry of PictureList), GraphicAndText (PictureOff under TextOff),
    /// Invisible (nothing drawn). The graphic is stretched over the area inside the border; the button background shows
    /// through its transparent pixels. Text placement as in a field: margins, alignment, clipped at the border.</summary>
    private static FrameworkElement Button(JsonObject p, double w, double h)
    {
        var mode = Enum(p["Mode"], "Text", "Graphic", "TextList", "GraphicList", "4", "5", "Invisible", "7", "GraphicAndText");
        if (mode == "Invisible") return new Canvas { Width = w, Height = h };
        var text = mode switch
        {
            "Text" or "GraphicAndText" => Text(p["TextOff"]),
            "TextList" => SymbolicText(p),
            _ => null,
        };
        var box = (Grid)Box(p, w, h, text, Corner(p));
        var pic = mode switch
        {
            "Graphic" or "GraphicAndText" => RefName(p["PictureOff"]),
            "GraphicList" => TreeBuilder.Str(ListEntry(p["PictureList"], p, ScreenRenderer.GraphicListByName)?["graphic"]),
            _ => null,
        };
        if (pic != null && ClassicGraphic(pic, null) is { } img)
        {
            var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
            box.Children.Insert(box.Children.Count - (string.IsNullOrEmpty(text) ? 0 : 1), StretchedImage(img, w - 2 * bw, h - 2 * bw, bw));
        }
        return box;
    }

    /// <summary>Switch by mode (runtime, sample C05), state from the process value (not 0 = on).
    /// Switch (LeftToRight): caption above, a track split in two halves: off: BackColor and InnerBackColorOff with
    /// TextOff on the right; on: InnerBackColorOn with TextOn on the left and BackColor. TextSwitchButton: a field
    /// (BackColor, border) with TextOff / TextOn centered. GraphicSwitchButton: PictureOff / PictureOn stretched inside
    /// the border.</summary>
    private static FrameworkElement Switch(JsonObject p, double w, double h)
    {
        var mode = Enum(p["Mode"], "Switch", "TextSwitchButton", "GraphicSwitchButton");
        var on = (Num(p["ProcessValue"]) ?? 0) != 0;
        if (mode == "TextSwitchButton")
        {
            var q = (JsonObject)p.DeepClone();
            q["HorizontalAlignment"] = "Center";
            q["VerticalAlignment"] = "Middle";
            return Box(q, w, h, Text(p[on ? "TextOn" : "TextOff"]), Corner(p));
        }
        if (mode == "GraphicSwitchButton")
        {
            var q = (JsonObject)p.DeepClone();
            q["Picture"] = p[on ? "PictureOn" : "PictureOff"]?.DeepClone();
            return GraphicBox(q, w, h, false);
        }
        if (on) return SwitchOn(p, w, h);

        var c = new Canvas { Width = w, Height = h };
        var cap = Bool(p["ShowCaption"], true) ? Text(p["CaptionText"]) : "";
        double top = 0;
        if (!string.IsNullOrEmpty(cap))
        {
            var tb = TextBlockOf(p, cap, "CaptionFont", "CaptionColor");
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            top = Math.Ceiling(tb.DesiredSize.Height);
            Canvas.SetLeft(tb, (w - tb.DesiredSize.Width) / 2);
            Canvas.SetTop(tb, 0);
            c.Children.Add(tb);
        }
        var th = Math.Max(h - top - 1, 0);
        var half = Math.Round(w / 2) - 1;
        var bc = SB(p["BorderColor"], Colors.Black);
        // rounded corners make the outline a pill (radius at most half the height, runtime C02)
        var r = Math.Min(Corner(p), th / 2);
        var left = new Border { Width = half + 1, Height = th, Background = SB(p["BackColor"], Colors.Gray), BorderBrush = bc, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(r, 0, 0, r) };
        var right = new Border { Width = w - half, Height = th, Background = SB(p["InnerBackColorOff"], Colors.Red), BorderBrush = bc, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(0, r, r, 0) };
        if (string.IsNullOrEmpty(cap) && th >= 12)
        {
            // without a caption the thumb half shows a grip of three vertical lines in its middle (runtime C02)
            // runtime TP900: dark line with a light line right of it, pitch 8, the middle one 3 px left of the centre
            var gh = Math.Round(th * 0.4);
            var grip = new Canvas { Width = half + 1, Height = th };
            var cx = Math.Round((half + 1) / 2) - 3;
            for (var i = -1; i <= 1; i++)
            {
                grip.Children.Add(At(new Rectangle { Width = 1, Height = gh, Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)) }, cx + 8 * i - 1, Math.Round((th - gh) / 2) - 1));
                grip.Children.Add(At(new Rectangle { Width = 1, Height = gh, Fill = new SolidColorBrush(Color.FromRgb(170, 170, 170)) }, cx + 8 * i, Math.Round((th - gh) / 2) - 1));
            }
            left.Child = grip;
        }
        Canvas.SetTop(left, top);
        Canvas.SetLeft(right, half);
        Canvas.SetTop(right, top);
        if (r > 0)
        {
            // rounded: the off half is a pill of its own over the on half, which continues below its round end
            left.CornerRadius = new CornerRadius(r);
            right.Width += r;
            Canvas.SetLeft(right, half - r);
        }
        // the state text follows HorizontalAlignment inside its half (runtime C02: left 2 px, right 3 px from the edge)
        var t = TextBlockOf(p, Text(p["TextOff"]));
        t.VerticalAlignment = VerticalAlignment.Center;
        t.Margin = t.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => new Thickness(2, 0, 0, 0),
            HorizontalAlignment.Right => new Thickness(0, 0, 3, 0),
            _ => new Thickness(0),
        };
        right.Child = t;
        if (r > 0) t.Margin = new Thickness(t.Margin.Left + r, 0, t.Margin.Right, 0);
        c.Children.Add(right);
        c.Children.Add(left);
        return c;
    }

    /// <summary>Switch (mode Switch) in the on state: the left half InnerBackColorOn with TextOn, the right half
    /// BackColor (runtime, C05 S2).</summary>
    private static FrameworkElement SwitchOn(JsonObject p, double w, double h)
    {
        var q = (JsonObject)p.DeepClone();
        q["InnerBackColorOff"] = p["InnerBackColorOn"]?.DeepClone() ?? JsonValue.Create("0xFFFFFFFF");
        q["TextOff"] = p["TextOn"]?.DeepClone();
        q["Mode"] = 0;
        q["ProcessValue"] = 0;
        var c = (Canvas)Switch(q, w, h);
        // the state half lies on the left: swap the two halves
        var halves = c.Children.OfType<Border>().ToList();
        if (halves.Count == 2)
        {
            static double X(UIElement e) => double.IsNaN(Canvas.GetLeft(e)) ? 0 : Canvas.GetLeft(e);
            var (l, r) = X(halves[0]) <= X(halves[1]) ? (halves[0], halves[1]) : (halves[1], halves[0]);
            (l.Background, r.Background) = (r.Background, l.Background);
            var (lt, rt) = (l.Child, r.Child);
            (l.Child, r.Child) = (null, null);
            (l.Child, r.Child) = (rt, lt);
        }
        return c;
    }

    /// <summary>Graphic view (Picture) and graphic I/O field (entry of its graphic list PictureList for the process
    /// value), runtime sample C05: the background (BackColor, none with a transparent fill) shows through transparent
    /// pixels, the graphic is stretched over the item (graphic view: inside its border), pixels of TransparentColor are
    /// transparent with UseTransparentColor, a gif is drawn without its transparency. Without a graphic: the graphic
    /// view shows a black frame of 2 px and the runtime's "missing graphic" picture (16×16) tiled from the inner corner,
    /// the graphic I/O field its own picture without a frame.</summary>
    private static FrameworkElement GraphicBox(JsonObject p, double w, double h, bool io)
    {
        var gname = io ? TreeBuilder.Str(ListEntry(p["PictureList"], p, ScreenRenderer.GraphicListByName)?["graphic"])
            : RefName(p["Picture"]) ?? RefName(p["Graphic"]);
        if (gname != null && ClassicGraphic(gname, Bool(p["UseTransparentColor"]) ? Col(p["TransparentColor"]) : null) is { } img)
        {
            var bw = io || NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
            var box = new Grid
            {
                Width = w,
                Height = h,
                Background = FillKind(p["BackFillStyle"]) == "Transparent" ? null : SB(p["BackColor"], Color.FromRgb(128, 128, 128)),
            };
            box.Children.Add(StretchedImage(img, w - 2 * bw, h - 2 * bw, bw));
            if (bw > 0) box.Children.Add(new Border { BorderThickness = new Thickness(bw), BorderBrush = SB(p["BorderColor"], Colors.Black) });
            return box;
        }
        var g = new Grid { Width = w, Height = h, Background = Brushes.Black };
        // the graphic I/O field has its own picture, tiled from the item's corner (runtime, C01/C02)
        g.Children.Add(io ? new Border { Background = MissingIoGraphic.Value } : new Border { Margin = new Thickness(2), Background = MissingGraphic.Value });
        return g;
    }

    private static readonly string[] MissingTile =
    [
        "wcwcwcwcwccccckk", "cwcwkwcwccyycckk", "wcwkgkwccyyyyckk", "cwckgkcwcyyyyckk",
        "wckkgkwcccyycckk", "ckgkgkcwcwcccwkk", "wkgkgkkkwcwcwckk", "ckgkgkkgkwcwcwkk",
        "wckggkkgkcwcwckk", "kkkkgkgkkkkkkkkk", "yoykggkyoyoyoykk", "oyokgkyokkyoyokk",
        "yyykgkoksskyoykk", "kkkkkkkkkkkkkkkk", "ssssssssssssssss", "kkkkkkkkkkkkkkkk",
    ];

    private static readonly string[] MissingIoTile =
    [
        "aaaaaaaaaaaaaaaa", "abcbcbcbcbcdeaed", "acbcbabcbcceaaad", "abcbafabcbcdedda",
        "acbcafacbccdaaaa", "abcaafabcbcdbeba", "acafafacbccdeeea", "abafafaaabcdddda",
        "acafafaafacdddda", "abcaffaafacdbeba", "aaaaafafaaadaaaa", "aghgaffaghgdeeed",
        "ahghafaghghdaaaa", "aghgafahghgddada", "aaaaaaaaaaaaaaaa", "aeeeeeeeeeeeeeee",
    ];

    private static readonly Lazy<Brush> MissingGraphic = new(() => TileBrush(MissingTile, ch => ch switch
    {
        'w' => Colors.White, 'c' => Colors.Cyan, 'y' => Colors.Yellow, 'g' => Colors.Lime,
        'o' => Color.FromRgb(128, 128, 0), 's' => Color.FromRgb(192, 192, 192), _ => Colors.Black,
    }));

    private static readonly Lazy<Brush> MissingIoGraphic = new(() => TileBrush(MissingIoTile, ch => ch switch
    {
        'b' => Colors.White, 'c' => Colors.Cyan, 'd' => Color.FromRgb(128, 128, 128), 'e' => Color.FromRgb(192, 192, 192),
        'f' => Colors.Lime, 'g' => Colors.Yellow, 'h' => Color.FromRgb(128, 128, 0), _ => Colors.Black,
    }));

    private static Brush TileBrush(string[] tile, Func<char, Color> color)
    {
        var px = new byte[16 * 16 * 4];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var c = color(tile[y][x]);
                var o = (y * 16 + x) * 4;
                (px[o], px[o + 1], px[o + 2], px[o + 3]) = (c.B, c.G, c.R, 255);
            }
        var bmp = System.Windows.Media.Imaging.BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, px, 64);
        bmp.Freeze();
        var b = new ImageBrush(bmp) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.NearestNeighbor);
        b.Freeze();
        return b;
    }

    // ---------- bar, slider, gauge, clock ----------

    private static FrameworkElement Bar(JsonObject p, double w, double h)
    {
        var g = (Grid)Box(p, w, h, null, Corner(p));
        var bw = Num(p["BorderWidth"]) ?? 1;
        var min = Num(p["MinimumValue"]) ?? 0;
        var max = Num(p["MaximumValue"]) ?? 100;
        // runtime (samples C02, C03): the scale runs between half a label height from both ends of the track; a value
        // at the minimum or below shows no fill, above the maximum the whole track; without a tag the runtime shows 2
        var val = Num(p["ProcessValue"]) ?? 2;
        if (Enum(p["BarOrientation"], "Up", "Down", "Left", "Right") is "Left" or "Right")
        {
            // horizontal bar (runtime TP1200, C05 slide-in): the fill grows from the left (Right) or the right (Left),
            // without scale labels in the track
            var iw = Math.Max(w - 2 * bw, 0);
            var fw = val <= min || max <= min ? 0 : val > max ? iw : Math.Round((val - min) / (max - min) * iw);
            g.Children.Add(new Border
            {
                Width = Math.Max(fw, 0),
                HorizontalAlignment = Enum(p["BarOrientation"], "Up", "Down", "Left", "Right") == "Left" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(bw),
                Background = SB(p["ForeColor"], Colors.Lime),
            });
            return g;
        }
        var ih = Math.Max(h - 2 * bw, 0);
        var probe = TextBlockOf(p, "0", "Font", "ScaleColor");
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var margin = Math.Min(Math.Round(probe.DesiredSize.Height / 2), ih / 4);
        var fh = val <= min || max <= min ? 0
            : val > max ? ih
            : margin + Math.Round((val - min) / (max - min) * (ih - 2 * margin));
        var fill = new Border
        {
            Height = Math.Max(fh, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(bw),
            Background = SB(p["ForeColor"], Colors.Lime),
        };
        g.Children.Add(fill);
        if (Bool(p["ShowScale"], true) && Bool(p["ShowTickLabels"], true))
        {
            // a label wider than the track starts 3 px inside it and is clipped at its right edge (runtime, C06)
            var iw = Math.Max(w - 2 * bw, 0);
            var clip = new Canvas { Margin = new Thickness(bw), ClipToBounds = true };
            g.Children.Add(clip);
            void Label(string text, double y)
            {
                var t = TextBlockOf(p, text, "Font", "ScaleColor");
                t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                clip.Children.Add(At(t, Math.Max(Math.Round((iw - t.DesiredSize.Width) / 2), 3), y - bw));
            }
            var top = TextBlockOf(p, Fmt(max), "Font", "ScaleColor");
            top.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var topWide = top.DesiredSize.Width > iw - 6;
            top.HorizontalAlignment = HorizontalAlignment.Center;
            top.VerticalAlignment = VerticalAlignment.Top;
            top.Margin = new Thickness(0, bw + 2, 0, 0);
            var bottom = TextBlockOf(p, Fmt(min), "Font", "ScaleColor");
            bottom.HorizontalAlignment = HorizontalAlignment.Center;
            bottom.VerticalAlignment = VerticalAlignment.Bottom;
            bottom.Margin = new Thickness(0, 0, 0, bw);
            if (topWide) Label(Fmt(max), bw + 2);
            else g.Children.Add(top);
            g.Children.Add(bottom);
            // labels between the ends (runtime, samples C02 and C06): about seven steps, the step rounded up to a
            // multiple of 10 (of the span's magnitude below 10), counted from the end that is a multiple of the step's
            // unit (max first); a label that would touch an end label is left out
            var span = max - min;
            if (span > 0 && ih > 0)
            {
                var unit = span >= 10 ? 10 : Math.Pow(10, Math.Floor(Math.Log10(span)));
                var step = Math.Ceiling(span / 7 / unit) * unit;
                var lh = probe.DesiredSize.Height;
                var fromMax = Math.Abs(Math.IEEERemainder(max, unit)) < 1e-9 || Math.Abs(Math.IEEERemainder(min, unit)) >= 1e-9;
                double Y(double v) => bw + margin + (max - v) / span * (ih - 2 * margin);
                for (var k = 1; k < 100; k++)
                {
                    var v = fromMax ? max - k * step : min + k * step;
                    if (v <= min + 1e-9 || v >= max - 1e-9) break;
                    var y = Y(v);
                    if (y - lh / 2 < Y(max) + lh / 2 || y + lh / 2 > Y(min) - lh / 2) continue;
                    Label(Fmt(v), Math.Round(y - lh / 2));
                }
            }
        }
        return g;
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Slider (vertical, scale left): caption at the top (clipped), scale labels in five steps right-aligned,
    /// a dotted tick line, the position box (arrow shape, ThumbBackColor) at the value and the thumb next to the item
    /// on the right (runtime: the thumb lies outside the item's rectangle).</summary>
    private static FrameworkElement Slider(JsonObject p, double w, double h)
    {
        if (!DesignColors(p)) return SliderClassic(p, w, h);
        var c = new Canvas { Width = w, Height = h };
        // the slider keeps its solid background with every BackFillStyle (runtime, C02)
        var bp = (JsonObject)p.DeepClone();
        bp.Remove("BackFillStyle");
        var box = (Grid)Box(bp, w, h, null, Corner(p));
        c.Children.Add(box);
        var bw = NoEdge(p) ? 0 : Num(p["BorderWidth"]) ?? 1;
        var inner = new Canvas { Width = Math.Max(w - 2 * bw, 0), Height = Math.Max(h - 2 * bw, 0), ClipToBounds = true };
        c.Children.Add(At(inner, bw, bw));
        var min = Num(p["MinimumValue"]) ?? 0;
        var max = Num(p["MaximumValue"]) ?? 100;
        var cap = Text(p["Caption"] ?? p["CaptionText"]);
        if (string.IsNullOrEmpty(cap)) cap = "SIMATIC";
        var ct = TextBlockOf(p, cap, "Font", "ForeColor");
        ct.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var capH = Math.Ceiling(ct.DesiredSize.Height);
        var orient = Enum(p["BarOrientation"], "Up", "Down", "Left", "Right");
        var fs = Num((p["Font"] as JsonObject)?["Size"]) ?? 11;
        var thumb = SB(p["ThumbBackColor"], Color.FromRgb(0x31, 0x34, 0x4A));
        if (orient is "Left" or "Right")
        {
            // horizontal orientations (runtime TP1200, C02): no scale, the caption in the middle, the position box as a
            // pentagon pointing up in the bottom right corner, the thumb below the item (Left: at the left edge)
            inner.Children.Add(At(ct, (inner.Width - ct.DesiredSize.Width) / 2, Math.Round((inner.Height - ct.DesiredSize.Height) / 2)));
            var hval = Math.Clamp(Num(p["ProcessValue"]) ?? 0, Math.Min(min, max), Math.Max(min, max));
            if (Bool(p["ShowPosition"], true))
            {
                var (pw, ph, peak) = (33.0, 25.0, 7.0);
                inner.Children.Add(At(new Polygon
                {
                    Points = [new Point(0, peak), new Point(pw / 2, 0), new Point(pw, peak), new Point(pw, ph), new Point(0, ph)],
                    Fill = thumb,
                }, inner.Width - pw, inner.Height - ph));
                var pt = TextBlockOf(p, Fmt(hval), "Font", "ForeColor");
                pt.Foreground = Brushes.White;
                pt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                inner.Children.Add(At(pt, inner.Width - pw + (pw - pt.DesiredSize.Width) / 2, inner.Height - (ph - peak) / 2 - pt.DesiredSize.Height / 2));
            }
            if (Bool(p["ShowThumb"], true))
                c.Children.Add(At(new Border { Width = 38, Height = Math.Round(fs * 8 / 3), Background = thumb, CornerRadius = new CornerRadius(0, 0, 4, 4) },
                    orient == "Left" ? 0 : w - 23, h));
            return c;
        }
        inner.Children.Add(At(ct, (inner.Width - ct.DesiredSize.Width) / 2, 0));
        // scale extent measured in the runtime (TP900 font 11, TP1200 font 15)
        var top = Math.Round(capH * 1.5 + (fs - 11));
        // the scale ends at the thumb's middle: TP900 (font 11) 6 px above the inner bottom (half the value box), TP1200 (font 15) 9 px
        var bottom = inner.Height - Math.Max(6, (fs - 11) * 2.2);
        var label = SB(p["LabelColor"], Colors.Black);
        // Down: the scale runs from the top (minimum) to the bottom (runtime, C02 BarOrientation)
        var down = orient == "Down";
        double Y(double v)
        {
            var f = max > min ? (v - min) / (max - min) : 0;
            return down ? top + (bottom - top) * f : bottom - (bottom - top) * f;
        }
        var labelRight = 0.0;
        if (Bool(p["ShowScale"], true))
        {
            // as many label intervals (10, 5, 4, 2, 1) as fit without overlapping
            var steps = new[] { 10, 5, 4, 2, 1 }.First(n => n == 1 || (bottom - top) / n >= fs * 0.9);
            var labels = new List<TextBlock>();
            if (Bool(p["ShowTickLabels"], true))
                for (var i = 0; i <= steps; i++)
                {
                    var tb = TextBlockOf(p, Fmt(min + (max - min) * i / steps), "Font", "LabelColor");
                    tb.HorizontalAlignment = HorizontalAlignment.Left;
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    labelRight = Math.Max(labelRight, tb.DesiredSize.Width);
                    labels.Add(tb);
                }
            // right-aligned after 4 px (runtime TP900 and TP1200: "100" from x 4 and 5 of the inner area)
            labelRight += 4;
            for (var i = 0; i < labels.Count; i++)
                inner.Children.Add(At(labels[i], labelRight - labels[i].DesiredSize.Width, Math.Round(Y(min + (max - min) * i / steps) - labels[i].DesiredSize.Height / 2)));
            if (Bool(p["ShowTicks"], true))
            {
                // the tick line ends 14 px before the inner right edge and starts 2 px after the labels; minor ticks
                // are 6/11 of a major tick (runtime: TP900 11 and 6 px, TP1200 2 and 1 px)
                var x0 = labelRight + 2;
                var x1 = Math.Max(inner.Width - 14, x0 + 1);
                var minor = steps * 5;
                for (var i = 0; i <= minor; i++)
                {
                    var y = Math.Round(Y(min + (max - min) * i / minor));
                    var len = i % 5 == 0 ? x1 - x0 : Math.Max(Math.Round((x1 - x0) * 6 / 11), 1);
                    inner.Children.Add(At(new Rectangle { Width = len, Height = 1, Fill = label }, x1 - len, y));
                }
            }
        }
        // without a tag the value is 0 (within the range); the track between the minimum and the value is filled with
        // ForeColor over the whole width
        var lo = Math.Min(min, max);
        var hi = Math.Max(min, max);
        var val = Math.Clamp(Num(p["ProcessValue"]) ?? 0, lo, hi);
        var vy = Math.Round(Y(val));
        // the fill starts at the minimum end of the track (runtime C02/C04 MinimumValue −100: value 0 fills from the
        // bottom up to the 0 line)
        var zero = lo;
        if (val != zero)
        {
            var zy = down ? 0 : inner.Height;
            var (y0, y1) = (Math.Min(vy, zy), Math.Max(vy, zy));
            inner.Children.Insert(0, At(new Rectangle { Width = inner.Width, Height = Math.Max(y1 - y0, 0), Fill = SB(p["ForeColor"], Color.FromRgb(0, 0x7A, 0xCC)) }, 0, y0));
        }
        if (Bool(p["ShowPosition"], true))
        {
            var pt = TextBlockOf(p, Fmt(val), "Font", "ForeColor");
            pt.Foreground = Brushes.White;
            pt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var ph = Math.Round(pt.DesiredSize.Height * 0.85);
            // right of the labels when there is room for the value (TP900), else over them (TP1200)
            var px = inner.Width - (labelRight + 1) >= pt.DesiredSize.Width + 14 ? labelRight + 1 : 10;
            var pw = inner.Width - px;
            inner.Children.Add(At(new Polygon
            {
                Points = [new Point(6, 0), new Point(pw, 0), new Point(pw, ph), new Point(6, ph), new Point(0, ph / 2)],
                Fill = thumb,
            }, px, vy - Math.Floor(ph / 2)));
            inner.Children.Add(At(pt, inner.Width - 5 - pt.DesiredSize.Width, vy - pt.DesiredSize.Height / 2));
        }
        if (Bool(p["ShowThumb"], true))
        {
            // thumb size scales with the font: 40×40 at 15 px (TP1200), 30 wide and 32 high at 11 px (TP900)
            var th = Math.Round(2 * fs + 10);
            var tw = Math.Round(2.5 * fs + 2.5);
            var t = new Canvas { Width = tw, Height = th };
            t.Children.Add(new Border { Width = tw, Height = th, Background = thumb, CornerRadius = new CornerRadius(0, 4, 4, 0) });
            var (mx, my) = (tw / 2, th / 2);
            var a = th * 0.15;
            t.Children.Add(new Polygon { Points = [new Point(mx - a, my - th * 0.2), new Point(mx + a, my - th * 0.2), new Point(mx, my - th * 0.35)], Fill = Brushes.White });
            foreach (var ly in new[] { -th * 0.1, 0, th * 0.1 })
                t.Children.Add(At(new Rectangle { Width = tw * 0.55, Height = 1, Fill = Brushes.White }, mx - tw * 0.275, Math.Round(my + ly)));
            t.Children.Add(new Polygon { Points = [new Point(mx - a, my + th * 0.2), new Point(mx + a, my + th * 0.2), new Point(mx, my + th * 0.35)], Fill = Brushes.White });
            c.Children.Add(At(t, w, down ? vy + bw - 1 : vy + bw + 1 - th));
        }
        return c;
    }

    /// <summary>Gauge: dial (DialColor) with the inner dial, the closed bottom sector between AngleMax and AngleMin in
    /// the dial color, the range ring (normal, warning from RangeUpper1Start, danger from RangeUpper2Start), major ticks
    /// across the ring and labels outside it, pointer, peak value pointer, center and caption.</summary>
    private static FrameworkElement Gauge(JsonObject p, double w, double h)
    {
        if (!DesignColors(p)) return GaugeClassic(p, w, h);
        var c = new Canvas { Width = w, Height = h };
        var r = Math.Min(w, h) / 2;
        var cx = w / 2;
        var cy = h / 2;
        Point P(double deg, double rad) => new(cx + Math.Cos(deg * Math.PI / 180) * rad, cy + Math.Sin(deg * Math.PI / 180) * rad);
        var dialBrush = SB(p["DialColor"], Colors.Gray);
        var outer = (Num(p["DialSize"]) ?? 1) * r;
        // the runtime keeps a thin edge in BorderColor around the dial and measures every radial fraction (inner dial,
        // ticks, labels, needle) from a radius about 3 % smaller than half the size (C02 P_Gauge_*, 150 px: inner dial
        // 65.5, major ticks 25.8..46.3, labels 52, needle 49)
        c.Children.Add(At(new Ellipse { Width = 2 * outer, Height = 2 * outer, Fill = dialBrush, Stroke = SB(p["BorderColor"], Color.FromRgb(0x38, 0x3A, 0x48)), StrokeThickness = 1.5 }, cx - outer, cy - outer));
        r *= 0.97;
        var a0 = Num(p["AngleMin"]) ?? -225;
        var a1 = Num(p["AngleMax"]) ?? 45;
        var innerR = (Num(p["InnerDialOuterDistance"]) ?? 0.9) * r;
        if (Bool(p["ShowInnerDial"], true))
        {
            c.Children.Add(At(new Ellipse { Width = 2 * innerR, Height = 2 * innerR, Fill = SB(p["InnerDialColor"], Colors.White) }, cx - innerR, cy - innerR));
            // the part of the inner dial outside the scale (between AngleMax and AngleMin + 360) in the dial color
            if (a1 - a0 < 360)
            {
                var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
                // one pixel beyond the inner dial, so that no light seam of the inner dial's edge stays visible
                var wr = innerR + 1;
                fig.Segments.Add(new LineSegment(P(a1, wr), true));
                fig.Segments.Add(new ArcSegment(P(a0 + 360, wr), new Size(wr, wr), 0, 360 - (a1 - a0) > 180, SweepDirection.Clockwise, true));
                c.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([fig]), Fill = dialBrush });
            }
        }
        var min = Num(p["MinimumValue"]) ?? 0;
        var max = Num(p["MaximumValue"]) ?? 100;
        double A(double v) => a0 + (a1 - a0) * (max > min ? Math.Clamp((v - min) / (max - min), 0, 1) : 0);
        var tickOut = (Num(p["ScaleTickPosition"]) ?? 0.64) * r;
        var tickIn = tickOut - (Num(p["ScaleTickLength"]) ?? 0.29) * r;
        // the range band lies a bit outside the tick base and reaches 0.57 of the major tick length (150 px: 28..37.5)
        // with a thin edge in the tick color; the minor ticks cross the band
        var bandIn = tickIn + (tickOut - tickIn) * 0.12;
        var ringOut = tickIn + (tickOut - tickIn) * 0.57;
        var minorOut = ringOut;
        void Ring(double from, double to, JsonNode? color)
        {
            if (to <= from || Col(color) is not { } col) return;
            var (s, e) = (A(from), A(to));
            var fig = new PathFigure { StartPoint = P(s, ringOut), IsClosed = true };
            fig.Segments.Add(new ArcSegment(P(e, ringOut), new Size(ringOut, ringOut), 0, e - s > 180, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(P(e, bandIn), true));
            fig.Segments.Add(new ArcSegment(P(s, bandIn), new Size(bandIn, bandIn), 0, e - s > 180, SweepDirection.Counterclockwise, true));
            c.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([fig]), Fill = new SolidColorBrush(col) });
            var edge = new PathFigure { StartPoint = P(s, ringOut) };
            edge.Segments.Add(new ArcSegment(P(e, ringOut), new Size(ringOut, ringOut), 0, e - s > 180, SweepDirection.Clockwise, true));
            c.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([edge]), Stroke = SB(p["ScaleTickColor"], Colors.Black), StrokeThickness = 1 });
        }
        var warn = Num(p["RangeUpper1Start"]) ?? max;
        var danger = Num(p["RangeUpper2Start"]) ?? max;
        if (Bool(p["NormalRangeVisible"], Bool(p["RangeNormalEnabled"], true))) Ring(min, Math.Min(warn, danger), p["NormalRangeColor"] ?? p["RangeNormalColor"]);
        if (Bool(p["WarningRangeVisible"], Bool(p["RangeUpper1Enabled"], true))) Ring(warn, danger, p["WarningRangeColor"] ?? p["RangeUpper1Color"]);
        if (Bool(p["DangerRangeVisible"], Bool(p["RangeUpper2Enabled"], true))) Ring(danger, max, p["DangerRangeColor"] ?? p["RangeUpper2Color"]);
        var tickBrush = SB(p["ScaleTickColor"], Colors.Black);
        var grad = Num(p["Gradation"]) ?? 10;
        var n = grad > 0 && max > min ? (int)Math.Round((max - min) / grad) : 10;
        var labelR = (Num(p["ScaleTickLabelPosition"]) ?? 0.72) * r * 1.02;
        for (var i = 0; i <= 2 * n; i++)
        {
            var v = min + (max - min) * i / (2.0 * Math.Max(n, 1));
            var a = A(v);
            var major = i % 2 == 0;
            var (q0, q1) = (P(a, tickIn), P(a, major ? tickOut : minorOut));
            c.Children.Add(new System.Windows.Shapes.Line { X1 = q0.X, Y1 = q0.Y, X2 = q1.X, Y2 = q1.Y, Stroke = tickBrush, StrokeThickness = 1 });
            if (!major) continue;
            var tb = TextBlockOf(p, Fmt(v), "ScaleLabelFont", "ScaleLabelColor");
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var lp = P(a, labelR);
            c.Children.Add(At(tb, lp.X - tb.DesiredSize.Width / 2, lp.Y - tb.DesiredSize.Height / 2));
        }
        // without a tag the runtime shows the pointer at the middle of the range
        // without a tag the runtime shows the value 50 (C02 MaximumValue / MinimumValue variants)
        var val = Num(p["ProcessValue"]) ?? 50;
        if (Bool(p["ShowPeakValuePointer"]))
        {
            // the peak value pointer without a tag points at 0 (runtime C02 MinimumValue −100: straight up)
            var pk = P(A(Math.Clamp(0, Math.Min(min, max), Math.Max(min, max))), tickOut);
            c.Children.Add(new System.Windows.Shapes.Line { X1 = cx, Y1 = cy, X2 = pk.X, Y2 = pk.Y, Stroke = SB(p["DangerRangeColor"], Colors.Red), StrokeThickness = 1 });
        }
        var pa = A(val);
        var nl = (Num(p["NeedleHeight"]) ?? 0.66) * r * 1.03;
        var nw = Math.Max(r * 0.058, 2);
        c.Children.Add(new Polygon
        {
            Points = [P(pa + 90, nw), P(pa, nl), P(pa - 90, nw)],
            Fill = SB(p["PointerColor"], Colors.Black),
        });
        var cs = (Num(p["CenterSize"]) ?? 0.12) * r;
        c.Children.Add(At(new Ellipse { Width = 2 * cs, Height = 2 * cs, Fill = SB(p["CenterColor"], Colors.Gray), Stroke = tickBrush, StrokeThickness = 0.5 }, cx - cs, cy - cs));
        var cap = Text(p["CaptionText"] ?? p["Caption"]);
        if (string.IsNullOrEmpty(cap)) cap = "SIMATIC";
        var ctb = TextBlockOf(p, cap, "CaptionFont", "CaptionColor");
        ctb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        c.Children.Add(At(ctb, cx - ctb.DesiredSize.Width / 2, h * (Num(p["CaptionTop"]) ?? 0.88) - ctb.DesiredSize.Height));
        return c;
    }

    private static bool DesignColors(JsonObject p) => !(p["UseDesignColorSchema"] is JsonValue u && u.TryGetValue<bool>(out var b) && !b);

    /// <summary>Raised-and-sunken 3D frame of the classic look (runtime TP1200, C02 UseDesignColorSchema false): from
    /// the edge inward top/left light gray, white, gray, black and bottom/right black, gray, white, light gray, each
    /// line bw/2 wide.</summary>
    private static void Frame3D(Canvas c, double w, double h, double bw)
    {
        var lw = Math.Max(bw / 2, 1);
        var tl = new[] { Color.FromRgb(192, 192, 192), Colors.White, Color.FromRgb(128, 128, 128), Colors.Black };
        var br = new[] { Colors.Black, Color.FromRgb(128, 128, 128), Colors.White, Color.FromRgb(192, 192, 192) };
        // outer lines first, each inner one drawn over the previous
        for (var i = 0; i < 4; i++)
        {
            var o = i * lw;
            var (iw, ih) = (w - 2 * o, h - 2 * o);
            if (iw <= 0 || ih <= 0) continue;
            c.Children.Add(At(new Rectangle { Width = iw, Height = ih, Fill = new SolidColorBrush(br[i]) }, o, o));
            c.Children.Add(At(new Rectangle { Width = Math.Max(iw - lw, 0), Height = Math.Max(ih - lw, 0), Fill = new SolidColorBrush(tl[i]) }, o, o));
        }
    }

    /// <summary>3D frame of BorderOuterWidth3D (raised: bright top/left, shade bottom/right) and BorderInnerWidth3D
    /// (recessed: shade top/left, bright bottom/right); returns its width.</summary>
    private static double Frame3DStyled(Canvas c, JsonObject p, double w, double h)
    {
        var bright = Col(p["BorderBrightColor3D"]) ?? Colors.White;
        var shade = Col(p["BorderShadeColor3D"]) ?? Color.FromRgb(128, 128, 128);
        var o = 0.0;
        foreach (var (wk, sk) in new[] { ("BorderOuterWidth3D", "BorderOuterStyle3D"), ("BorderInnerWidth3D", "BorderInnerStyle3D") })
        {
            var lw = Num(p[wk]) ?? 2;
            if (lw <= 0) continue;
            var style = TreeBuilder.Str(p[sk]) ?? (wk.StartsWith("BorderOuter", StringComparison.Ordinal) ? "Raised" : "Recessed");
            if (style is "None" or "0") continue;
            var raised = !(style.StartsWith("Recess", StringComparison.Ordinal) || style == "2");
            var (tl, br) = raised ? (bright, shade) : (shade, bright);
            var (iw, ih) = (w - 2 * o, h - 2 * o);
            c.Children.Add(At(new Rectangle { Width = Math.Max(iw, 0), Height = Math.Max(ih, 0), Fill = new SolidColorBrush(br) }, o, o));
            c.Children.Add(At(new Rectangle { Width = Math.Max(iw - lw, 0), Height = Math.Max(ih - lw, 0), Fill = new SolidColorBrush(tl) }, o, o));
            o += lw;
        }
        return o;
    }

    /// <summary>Slider in the classic look (UseDesignColorSchema false; runtime TP1200, C02): BackColor in a raised and
    /// recessed 3D frame, the caption at the top, a scale of 40 px (a quarter of the height) around the middle with
    /// ticks every 2 px and the labels of minimum and maximum (LabelColor) on the left, the track (BarBackColor, filled
    /// with BarColor from the bottom to the value), the thumb (16×16, ThumbBackColor, light top/left, dark
    /// bottom/right) at the value and the value at the bottom. Without a tag the value is the middle of the range.</summary>
    private static FrameworkElement SliderClassic(JsonObject p, double w, double h)
    {
        var c = new Canvas { Width = w, Height = h, ClipToBounds = true };
        c.Children.Add(new Rectangle { Width = w, Height = h, Fill = SB(p["BackColor"], Color.FromRgb(192, 192, 192)) });
        var fw = Frame3DStyled(c, p, w, h);
        var min = Num(p["MinimumValue"]) ?? 0;
        var max = Num(p["MaximumValue"]) ?? 100;
        var val = Math.Clamp(Num(p["ProcessValue"]) ?? (min + max) / 2, Math.Min(min, max), Math.Max(min, max));
        var cap = Text(p["Caption"] ?? p["CaptionText"]);
        if (string.IsNullOrEmpty(cap)) cap = "SIMATIC";
        var ct = TextBlockOf(p, cap, "Font", "ForeColor");
        ct.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        c.Children.Add(At(ct, Math.Round((w - ct.DesiredSize.Width) / 2), fw + 1));
        var span = Math.Round(h / 4);
        var top = Math.Round(h / 2 - span / 2);
        double Y(double v) => top + span - span * (max > min ? (v - min) / (max - min) : 0);
        var tx = Math.Round(w / 2);
        if (Bool(p["ShowScale"], true))
        {
            if (Bool(p["ShowTicks"], true))
                for (var y = top; y <= top + span; y += 2)
                    c.Children.Add(At(new Rectangle { Width = 10, Height = 1, Fill = Brushes.Black }, tx, y));
            if (Bool(p["ShowTickLabels"], true))
                foreach (var v in new[] { max, min })
                {
                    var tb = TextBlockOf(p, Fmt(v), "Font", "LabelColor");
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    c.Children.Add(At(tb, tx - 3 - tb.DesiredSize.Width, Math.Round(Y(v) - tb.DesiredSize.Height / 2)));
                }
        }
        var vy = Math.Round(Y(val));
        if (Bool(p["ShowBar"], true))
        {
            c.Children.Add(At(new Rectangle { Width = 10, Height = span, Fill = SB(p["BarBackColor"], Colors.White) }, tx + 10, top));
            c.Children.Add(At(new Rectangle { Width = 10, Height = Math.Max(top + span - vy, 0), Fill = SB(p["BarColor"], Colors.Red) }, tx + 10, vy));
        }
        if (Bool(p["ShowThumb"], true))
        {
            var th = new Grid { Width = 16, Height = 16 };
            th.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)) });
            th.Children.Add(new Border { Margin = new Thickness(0, 0, 1, 1), Background = Brushes.White });
            th.Children.Add(new Border { Margin = new Thickness(1, 1, 1, 1), Background = SB(p["ThumbBackColor"], Color.FromRgb(192, 192, 192)) });
            c.Children.Add(At(th, tx + 3, vy - 8));
        }
        if (Bool(p["ShowPosition"], true))
        {
            var pt = TextBlockOf(p, Fmt(val), "Font", "ForeColor");
            pt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            c.Children.Add(At(pt, Math.Round((w - pt.DesiredSize.Width) / 2), h - fw - pt.DesiredSize.Height - 1));
        }
        return c;
    }

    /// <summary>Gauge in the classic look (UseDesignColorSchema false; runtime TP1200, C02): BackColor area in a 3D
    /// frame (4 lines of bw/2), the dial (DialSize of the inner radius, DialColor, black outline), the scale as a black
    /// arc at 0.86 of the dial radius with the range colors as a 2 px band inside it, short major ticks across the arc,
    /// labels at ScaleTickLabelPosition, a white needle with black outline, the center and the caption.</summary>
    private static FrameworkElement GaugeClassic(JsonObject p, double w, double h)
    {
        var c = new Canvas { Width = w, Height = h };
        var bw = Num(p["BorderWidth"]) ?? 2;
        if (bw > 0) Frame3D(c, w, h, bw);
        var fi = bw > 0 ? 4 * Math.Max(bw / 2, 1) : 0;
        c.Children.Add(At(new Rectangle { Width = Math.Max(w - 2 * fi, 0), Height = Math.Max(h - 2 * fi, 0), Fill = SB(p["BackColor"], Color.FromRgb(128, 128, 128)) }, fi, fi));
        var (cx, cy) = (w / 2, h / 2);
        var r = (Math.Min(w, h) - 4 * bw) / 2;
        Point P(double deg, double rad) => new(cx + Math.Cos(deg * Math.PI / 180) * rad, cy + Math.Sin(deg * Math.PI / 180) * rad);
        var dialR = (Num(p["DialSize"]) ?? 0.87) * r;
        c.Children.Add(At(new Ellipse { Width = 2 * dialR, Height = 2 * dialR, Fill = SB(p["DialColor"], Color.FromRgb(192, 192, 192)), Stroke = Brushes.Black, StrokeThickness = 1 }, cx - dialR, cy - dialR));
        var a0 = Num(p["AngleMin"]) ?? -225;
        var a1 = Num(p["AngleMax"]) ?? 45;
        var min = Num(p["MinimumValue"]) ?? 0;
        var max = Num(p["MaximumValue"]) ?? 100;
        double A(double v) => a0 + (a1 - a0) * (max > min ? Math.Clamp((v - min) / (max - min), 0, 1) : 0);
        var sr = dialR * 0.86;
        void Arc(double from, double to, double rad, Brush stroke, double th)
        {
            if (to <= from) return;
            var (s0, e0) = (A(from), A(to));
            var fig = new PathFigure { StartPoint = P(s0, rad) };
            fig.Segments.Add(new ArcSegment(P(e0, rad), new Size(rad, rad), 0, e0 - s0 > 180, SweepDirection.Clockwise, true));
            c.Children.Add(new System.Windows.Shapes.Path { Data = new PathGeometry([fig]), Stroke = stroke, StrokeThickness = th });
        }
        var warn = Num(p["RangeUpper1Start"]) ?? max;
        var danger = Num(p["RangeUpper2Start"]) ?? max;
        var band = sr - 1.5;
        if (Bool(p["RangeNormalEnabled"], true)) Arc(min, Math.Min(warn, danger), band, SB(p["RangeNormalColor"], Colors.Lime), 2);
        if (Bool(p["RangeUpper1Enabled"], true)) Arc(warn, danger, band, SB(p["RangeUpper1Color"], Colors.Yellow), 2);
        if (Bool(p["RangeUpper2Enabled"], true)) Arc(danger, max, band, SB(p["RangeUpper2Color"], Colors.Red), 2);
        var tick = SB(p["ScaleTickColor"], Colors.Black);
        Arc(min, max, sr, tick, 1);
        var grad = Num(p["Gradation"]) ?? 10;
        var n = grad > 0 && max > min ? (int)Math.Round((max - min) / grad) : 10;
        var labelR = (Num(p["ScaleTickLabelPosition"]) ?? 0.55) * r;
        for (var i = 0; i <= n; i++)
        {
            var v = min + (max - min) * i / Math.Max(n, 1);
            var a = A(v);
            var (q0, q1) = (P(a, sr - 1), P(a, sr + 3));
            c.Children.Add(new System.Windows.Shapes.Line { X1 = q0.X, Y1 = q0.Y, X2 = q1.X, Y2 = q1.Y, Stroke = tick, StrokeThickness = 1 });
            var tb = TextBlockOf(p, Fmt(v), "ScaleLabelFont", "ScaleLabelColor");
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var lp = P(a, labelR);
            c.Children.Add(At(tb, lp.X - tb.DesiredSize.Width / 2, lp.Y - tb.DesiredSize.Height / 2));
        }
        var cap = Text(p["CaptionText"] ?? p["Caption"]);
        if (string.IsNullOrEmpty(cap)) cap = "SIMATIC";
        var ctb = TextBlockOf(p, cap, "CaptionFont", "CaptionColor");
        ctb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        c.Children.Add(At(ctb, cx - ctb.DesiredSize.Width / 2, h * (Num(p["CaptionTop"]) ?? 0.85) - ctb.DesiredSize.Height));
        var pa = A(Num(p["ProcessValue"]) ?? 50);
        var nl = (Num(p["NeedleHeight"]) ?? 0.61) * r;
        var nw = Math.Max(r * 0.045, 2);
        c.Children.Add(new Polygon
        {
            Points = [P(pa + 90, nw), P(pa, nl), P(pa - 90, nw)],
            Fill = SB(p["PointerColor"], Colors.White),
            Stroke = Brushes.Black,
            StrokeThickness = 1,
        });
        var cs = (Num(p["CenterSize"]) ?? 0.08) * r;
        c.Children.Add(At(new Ellipse { Width = 2 * cs, Height = 2 * cs, Fill = SB(p["CenterColor"], Color.FromRgb(128, 128, 128)), Stroke = Brushes.Black, StrokeThickness = 1 }, cx - cs, cy - cs));
        return c;
    }

    /// <summary>Analog clock as the runtime draws it: dial (DialColor), 60 tick marks (longer every 5 minutes,
    /// widest at 12/3/6/9), small bold numbers (0.12 of the radius) at 0.69 of the radius in TicksColor, tapered filled
    /// hands (length and width in percent of the radius) and a light center.</summary>
    private static FrameworkElement Clock(JsonObject p, double w, double h)
    {
        var c = new Canvas { Width = w, Height = h };
        var r = Math.Min(w, h) / 2;
        var (cx, cy) = (w / 2, h / 2);
        Point P(double deg, double rad) => new(cx + Math.Cos((deg - 90) * Math.PI / 180) * rad, cy + Math.Sin((deg - 90) * Math.PI / 180) * rad);
        c.Children.Add(new Ellipse { Width = w, Height = h, Fill = FillKind(p["BackFillStyle"]) == "Transparent" && p["DialColor"] == null ? null : SB(p["DialColor"], Colors.Gray) });
        var ticks = SB(p["TicksColor"], Colors.White);
        // classic look (UseDesignColorSchema false, runtime TP1200 C02): radii from 0.933 of half the size, tick style
        // Circle: hour marks as filled circles in TicksColor with a white outline, minute marks as small light dots,
        // hands with a 1 px outline, the center filled with NeedleColor
        var classic = !DesignColors(p);
        if (classic) r *= 0.933;
        var td = (Num(p["TickDistance"]) ?? 90) / 100.0 * r;
        var circles = classic && TreeBuilder.Str(p["TickStyle"]) is "Circle" or "1";
        if (Bool(p["ShowTicks"], true) && circles)
            for (var i = 0; i < 60; i++)
            {
                var q = P(i * 6, td);
                var d = i % 5 == 0 ? r * 0.1 : 2;
                c.Children.Add(At(new Ellipse { Width = d, Height = d, Fill = i % 5 == 0 ? ticks : Brushes.White, Stroke = i % 5 == 0 ? Brushes.White : null, StrokeThickness = 1 }, q.X - d / 2, q.Y - d / 2));
            }
        else if (Bool(p["ShowTicks"], true))
            for (var i = 0; i < 60; i++)
            {
                var quarter = i % 15 == 0;
                var five = i % 5 == 0;
                var inner = td * (quarter ? 0.87 : five ? 0.92 : 0.955);
                var (a, b) = (P(i * 6, inner), P(i * 6, td));
                c.Children.Add(new System.Windows.Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = ticks, StrokeThickness = quarter ? 2 : five ? 1.5 : 1 });
            }
        if (Enum(p["NumberStyle"], "NoNumber", "Arabic", "Roman") != "NoNumber" || p["NumberStyle"] is null)
        {
            var font = new JsonObject { ["Name"] = (p["Font"] as JsonObject)?["Name"]?.DeepClone() ?? "Tahoma", ["Size"] = Math.Round(r * 0.12), ["Bold"] = true };
            for (var hr = 1; hr <= 12; hr++)
            {
                var tb = new TextBlock { Text = hr.ToString(CultureInfo.InvariantCulture), Foreground = ticks };
                ApplyFont(tb, font);
                TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var q = P(hr * 30, td * 0.77);
                c.Children.Add(At(tb, q.X - tb.DesiredSize.Width / 2, q.Y - tb.DesiredSize.Height / 2));
            }
        }
        var now = DateTime.Now;
        var fill = FillKind(p["NeedleFillStyle"]) == "Transparent" ? null : SB(p["NeedleColor"], Colors.White);
        var edge = SB(p["NeedleBorderColor"], Colors.Black);
        void Hand(double deg, double lenPct, double widthPct)
        {
            var len = lenPct / 100 * r;
            var half = Math.Max(widthPct / 100 * r * 0.45, 0.75);
            c.Children.Add(new Polygon
            {
                Points = [P(deg - 90, half), P(deg, len), P(deg + 90, half), P(deg + 180, half)],
                Fill = fill ?? Brushes.Transparent,
                Stroke = edge,
                StrokeThickness = classic ? 1 : 0.5,
            });
        }
        Hand((now.Hour % 12 + now.Minute / 60.0) * 30, Num(p["HourNeedleHeight"]) ?? 52, Num(p["HourNeedleWidth"]) ?? 10);
        Hand((now.Minute + now.Second / 60.0) * 6, Num(p["MinuteNeedleHeight"]) ?? 65, Num(p["MinuteNeedleWidth"]) ?? 8);
        Hand(now.Second * 6, Num(p["SecondNeedleHeight"]) ?? 90, Num(p["SecondNeedleWidth"]) ?? 2);
        var cr = r * 0.06;
        c.Children.Add(At(classic
            ? new Ellipse { Width = 2 * cr, Height = 2 * cr, Fill = SB(p["NeedleColor"], Colors.Cyan), Stroke = edge, StrokeThickness = 1 }
            : new Ellipse { Width = 2 * cr, Height = 2 * cr, Fill = edge }, cx - cr, cy - cr));
        return c;
    }

    private static T At<T>(T el, double x, double y) where T : UIElement
    {
        Canvas.SetLeft(el, x);
        Canvas.SetTop(el, y);
        return el;
    }

    // ---------- controls ----------

    /// <summary>Table controls (alarm view, trend view, user view, watch table, recipe view, diagnostics views): frame
    /// with BackColor, column header with the header colors, white table area.</summary>
    private static FrameworkElement Control(string type, JsonObject p, double w, double h)
    {
        var bw = Num(p["BorderWidth"]) ?? 1;
        var outer = new Border
        {
            Width = w,
            Height = h,
            Background = SB(p["BackColor"] ?? p["BackgroundColor"], Color.FromRgb(0xF0, 0xF0, 0xF0)),
            BorderBrush = SB(p["BorderColor"], Colors.Gray),
            BorderThickness = new Thickness(Math.Max(bw, 1)),
            CornerRadius = new CornerRadius(Num(p["CornerRadius"]) ?? 0),
        };
        var c = new Canvas();
        outer.Child = c;
        var headerH = 20.0;
        var header = new Border
        {
            Width = Math.Max(w - 2 * bw, 0),
            Height = headerH,
            Background = new LinearGradientBrush(Col(p["TableHeaderFirstGradientColor"]) ?? Color.FromRgb(0x83, 0x84, 0x8E),
                Col(p["TableHeaderSecondGradientColor"]) ?? Col(p["TableHeaderBackColor"]) ?? Color.FromRgb(0x58, 0x5A, 0x67), 90),
        };
        c.Children.Add(header);
        var table = new Border
        {
            Width = Math.Max(w - 2 * bw, 0),
            Height = Math.Max(h - 2 * bw - headerH - 60, 0),
            Background = SB(p["TableBackColor"], Colors.White),
        };
        Canvas.SetTop(table, headerH);
        c.Children.Add(table);
        return outer;
    }

    /// <summary>Integers of a list blob as the parser emits it (hex: u32 byte length, u32 count, then i32 values).</summary>
    private static int[] Ints32(JsonNode? n)
    {
        if (n is JsonArray arr) return [.. arr.Select(v => (int)(Num(v) ?? 0))];
        if (TreeBuilder.Str(n) is not { Length: >= 16 } hex || hex.Length % 2 != 0) return [];
        byte[] b;
        try { b = Convert.FromHexString(hex); } catch (FormatException) { return []; }
        var res = new int[(b.Length - 8) / 4];
        for (var i = 0; i < res.Length; i++) res[i] = BitConverter.ToInt32(b, 8 + 4 * i);
        return res;
    }

    /// <summary>u16 list blob (u32 byte length, u32 count, then u16 values), e.g. the visible columns.</summary>
    private static int[] Ints16(JsonNode? n)
    {
        if (n is JsonArray arr) return [.. arr.Select(v => (int)(Num(v) ?? 0))];
        if (TreeBuilder.Str(n) is not { Length: >= 16 } hex || hex.Length % 2 != 0) return [];
        byte[] b;
        try { b = Convert.FromHexString(hex); } catch (FormatException) { return []; }
        var count = BitConverter.ToInt32(b, 4);
        var res = new List<int>();
        for (var i = 0; i < count && 8 + 2 * i + 1 < b.Length; i++) res.Add(BitConverter.ToUInt16(b, 8 + 2 * i));
        return [.. res];
    }

    private static Brush Gradient3(JsonObject p, string prefix, Color dflt)
    {
        var mid = Col(p[prefix + "MiddleGradientColor"]) ?? Col(p[prefix + "BackColor"]) ?? dflt;
        var first = Bool(p["Use" + prefix + "FirstGradient"], true) ? Col(p[prefix + "FirstGradientColor"]) ?? mid : mid;
        var second = Col(p[prefix + "SecondGradientColor"]) ?? mid;
        if (FillKind(p[prefix + "BackFillStyle"]) != "Gradient") return new SolidColorBrush(Col(p[prefix + "BackColor"]) ?? dflt);
        return new LinearGradientBrush(new GradientStopCollection
        {
            new GradientStop(first, 0), new GradientStop(mid, 0.4), new GradientStop(second, 1),
        }, 90);
    }

    private static readonly string[] AlarmColumnNames = ["No.", "Time", "Status", "Text", "Date", "", "Acknowledge group", "", ""];

    /// <summary>Alarm view: frame, message area (MessageArea*) with the column header (Columns visible, ColumnOrder
    /// positions, ColumnWidth), the white table, a horizontal scroll bar when the columns are wider than the area, and
    /// the buttons at ButtonPositions (help, acknowledge, loop-in-alarm).</summary>
    private static FrameworkElement AlarmView(JsonObject p, double w, double h)
    {
        var bw = Num(p["BorderWidth"]) ?? 1;
        var root = new Canvas { Width = w, Height = h };
        root.Children.Add(new Border
        {
            Width = w, Height = h,
            Background = SB(p["BackColor"], Color.FromRgb(0xF1, 0xF1, 0xF2)),
            BorderBrush = SB(p["BorderColor"], Colors.Gray),
            BorderThickness = new Thickness(bw),
            CornerRadius = new CornerRadius(Num(p["CornerRadius"]) ?? 0),
        });
        var ml = Num(p["MessageAreaLeft"]) ?? bw + 1;
        var mt = Num(p["MessageAreaTop"]) ?? bw + 1;
        var mw = Num(p["MessageAreaWidth"]) ?? w - 2 * ml;
        var mh = Num(p["MessageAreaHeight"]) ?? h - 50;
        var area = new Canvas { Width = mw, Height = mh, ClipToBounds = true };
        root.Children.Add(At(area, ml, mt));
        var widths = Ints32(p["ColumnWidth"]);
        var order = Ints32(p["ColumnOrder"]);
        var visible = Ints16(p["Columns"]).Select(v => v - 1).Where(i => i >= 0 && i < widths.Length).ToList();
        if (visible.Count == 0) visible = [.. Enumerable.Range(0, Math.Min(widths.Length, 7))];
        var cols = visible.OrderBy(i => i < order.Length ? order[i] : i).ToList();
        var hdrFont = p["TableHeaderFont"] as JsonObject;
        var hh = Math.Round((Num(hdrFont?["Size"]) ?? 13) + 8);
        var x = 0.0;
        var header = new Canvas { Width = mw, Height = hh, Background = Gradient3(p, "TableHeader", Color.FromRgb(0x83, 0x84, 0x8E)) };
        area.Children.Add(header);
        var sep = SB(p["TableHeaderBorderColor"], Color.FromRgb(0x62, 0x60, 0x6E));
        foreach (var ci in cols)
        {
            var cw = widths[ci];
            var name = ci < AlarmColumnNames.Length ? AlarmColumnNames[ci] : "";
            var tb = TextBlockOf(p, name, "TableHeaderFont", "TableHeaderForeColor");
            tb.Foreground = SB(p["TableHeaderForeColor"], Colors.White);
            var cell = new Canvas { Width = cw, Height = hh, ClipToBounds = true };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            cell.Children.Add(At(tb, 2, (hh - tb.DesiredSize.Height) / 2));
            header.Children.Add(At(cell, x, 0));
            x += cw;
            header.Children.Add(At(new Rectangle { Width = 1, Height = hh, Fill = sep }, x - 1, 0));
        }
        var needScroll = x > mw;
        var sbh = needScroll ? ScrollRowHeight : 0;
        area.Children.Add(At(new Rectangle { Width = mw, Height = Math.Max(mh - hh - sbh, 0), Fill = SB(p["TableBackColor"], Colors.White) }, 0, hh));
        AlarmRows(area, p, cols, widths, hh, mw, mh - hh - sbh);
        if (needScroll) HScroll(area, p, 0, mh - sbh, mw);
        var rects = Ints32(p["ButtonPositions"]);
        var shown = new List<(int l, int t, int r, int b)>();
        for (var i = 0; i + 3 < rects.Length; i += 4)
            if (rects[i] != 0 || rects[i + 1] != 0 || rects[i + 2] != 0 || rects[i + 3] != 0)
                shown.Add((rects[i], rects[i + 1], rects[i + 2], rects[i + 3]));
        var flags = new[] { Bool(p["ShowHelpButton"], true), Bool(p["ShowAcknowledgeButton"], true), Bool(p["ShowLoopInAlarmButton"], true) };
        var buttons = shown.Where((_, i) => i >= flags.Length || flags[i]).OrderBy(e => e.l).ToList();
        for (var i = 0; i < buttons.Count; i++)
        {
            var (l, t, r, b) = buttons[i];
            root.Children.Add(At(new Border
            {
                Width = Math.Max(r - l, 0), Height = Math.Max(b - t, 0),
                Background = Gradient3(p, "Button", Color.FromRgb(0xE9, 0xE8, 0xE8)),
                BorderBrush = SB(p["ButtonBorderColor"], Colors.Gray),
                BorderThickness = new Thickness(Num(p["ButtonBorderWidth"]) ?? 1),
                CornerRadius = new CornerRadius(Num(p["ButtonCornerRadius"]) ?? 0),
            }, l, t));
            ToolIcon(root, "A" + Math.Min(i + 1, 3), l, t, r - l, b - t);
        }
        return root;
    }

    /// <summary>Rows of the alarm view for the simulated tag values (runtime C03b): every discrete alarm whose trigger
    /// bit is set and every analog alarm beyond its limit is incoming (status I), with the time of the preview; alarms
    /// of the class Errors first and marked "!", then by number (discrete before analog); rows 16 px, every second one
    /// light gray, the first one selected.</summary>
    private static void AlarmRows(Canvas area, JsonObject p, List<int> cols, int[] widths, double top, double width, double height)
    {
        if (TagValues == null || ScreenRenderer.Alarms.Count == 0) return;
        var active = new List<(JsonObject A, bool Analog)>();
        foreach (var a in ScreenRenderer.Alarms)
        {
            var v = TagNum(TreeBuilder.Str(a["triggerTag"]));
            if (a["triggerBit"] is JsonNode bit)
            {
                if ((((long)v >> (int)(Num(bit) ?? 0)) & 1) == 1) active.Add((a, false));
            }
            else if (Num(a["limit"]) is { } lim)
            {
                var upper = TreeBuilder.Str(a["condition"]) != "LowerLimit";
                if (upper ? v > lim : v < lim) active.Add((a, true));
            }
        }
        var now = DateTime.Now;
        var rows = active.OrderBy(x => TreeBuilder.Str(x.A["alarmClass"]) == "Errors" ? 0 : 1)
            .ThenBy(x => Num(x.A["id"]) ?? 0).ThenBy(x => x.Analog ? 1 : 0).ToList();
        const double rh = 16;
        for (var r = 0; r < rows.Count && top + r * rh < top + height; r++)
        {
            var (a, _) = rows[r];
            var y = top + r * rh;
            var fill = r == 0 ? Color.FromRgb(0x99, 0xB4, 0xE0) : r % 2 == 1 ? Color.FromRgb(0xE6, 0xE6, 0xE6) : Colors.White;
            area.Children.Add(At(new Rectangle { Width = width, Height = rh, Fill = new SolidColorBrush(fill) }, 0, y));
            var x = 0.0;
            foreach (var ci in cols)
            {
                var cw = widths[ci];
                var text = ci switch
                {
                    0 => Fmt(Num(a["id"]) ?? 0),
                    1 => now.ToString("h:mm:ss tt", CultureInfo.InvariantCulture),
                    2 => "I",
                    3 => Text(a["text"]),
                    4 => now.ToString("M/d/yyyy", CultureInfo.InvariantCulture),
                    5 => TreeBuilder.Str(a["alarmClass"]) == "Errors" ? "!" : "",
                    6 => "0",
                    _ => "",
                };
                var cell = new Canvas { Width = cw, Height = rh, ClipToBounds = true };
                var tb = TextBlockOf(p, text, "Font", "TableForeColor");
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                cell.Children.Add(At(tb, 2, (rh - tb.DesiredSize.Height) / 2));
                area.Children.Add(At(cell, x, y));
                x += cw;
            }
        }
    }

    /// <summary>Frame of a table control: BackColor, border, corner radius.</summary>
    private static Canvas ControlFrame(JsonObject p, double w, double h)
    {
        var root = new Canvas { Width = w, Height = h };
        root.Children.Add(new Border
        {
            Width = w, Height = h,
            Background = SB(p["BackColor"] ?? p["BackgroundColor"], Color.FromRgb(0xF1, 0xF1, 0xF2)),
            BorderBrush = SB(p["BorderColor"], Colors.Gray),
            BorderThickness = new Thickness(Num(p["BorderWidth"]) ?? 1),
            CornerRadius = new CornerRadius(Num(p["CornerRadius"]) ?? 0),
        });
        return root;
    }

    /// <summary>Column header row (table header gradient, bold header font, separators); returns its height.</summary>
    private static double HeaderRow(Canvas into, JsonObject p, double x0, double y0, IEnumerable<(double Width, string Name)> cols)
    {
        var hh = Math.Round((Num((p["TableHeaderFont"] as JsonObject)?["Size"]) ?? 13) + 8);
        var sep = SB(p["TableHeaderBorderColor"], Color.FromRgb(0x62, 0x60, 0x6E));
        var x = x0;
        foreach (var (cw, name) in cols)
        {
            var cell = new Canvas { Width = cw, Height = hh, ClipToBounds = true, Background = Gradient3(p, "TableHeader", Color.FromRgb(0x83, 0x84, 0x8E)) };
            var tb = TextBlockOf(p, name, "TableHeaderFont", "TableHeaderForeColor");
            tb.Foreground = SB(p["TableHeaderForeColor"], Colors.White);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            cell.Children.Add(At(tb, 2, (hh - tb.DesiredSize.Height) / 2));
            cell.Children.Add(At(new Rectangle { Width = 1, Height = hh, Fill = sep }, cw - 1, 0));
            into.Children.Add(At(cell, x, y0));
            x += cw;
        }
        return hh;
    }

    /// <summary>Buttons of a control at the rectangles of ButtonPositions (left, top, right, bottom; empty ones skipped),
    /// moved by dy; with icons, the n-th button from the left gets the n-th tool bar icon.</summary>
    private static void Buttons(Canvas into, JsonObject p, Func<int, bool>? show = null, string[]? icons = null, double dy = 0)
    {
        var rects = Ints32(p["ButtonPositions"]);
        var list = new List<(int n, int l, int t, int r, int b)>();
        for (var i = 0; i + 3 < rects.Length; i += 4)
        {
            var (l, t, r, b) = (rects[i], rects[i + 1], rects[i + 2], rects[i + 3]);
            if (l == 0 && t == 0 && r == 0 && b == 0) continue;
            list.Add((list.Count, l, t, r, b));
        }
        var shown = list.Where(e => show == null || show(e.n)).OrderBy(e => e.l).ToList();
        for (var k = 0; k < shown.Count; k++)
        {
            var (_, l, t, r, b) = shown[k];
            into.Children.Add(At(new Border
            {
                Width = Math.Max(r - l, 0), Height = Math.Max(b - t, 0),
                Background = Gradient3(p, "Button", Color.FromRgb(0xE9, 0xE8, 0xE8)),
                BorderBrush = SB(p["ButtonBorderColor"], Colors.Gray),
                BorderThickness = new Thickness(Num(p["ButtonBorderWidth"]) ?? 1),
                CornerRadius = new CornerRadius(Num(p["ButtonCornerRadius"]) ?? 0),
            }, l, t + dy));
            if (icons != null && k < icons.Length) ToolIcon(into, icons[k], l, t + dy, r - l, b - t);
        }
    }

    /// <summary>Tool bar icons of the classic controls, as drawn by the runtime (TP900, button 45×33): offset of the
    /// icon inside the button and the pixels ('#' solid, '+' antialias, '-' light antialias, '.' button face).</summary>
    private static readonly Dictionary<string, (int X, int Y, string[] Rows)> ToolIcons = new()
    {
        ["A1"] = (12, 9, ["################..", "################..", "##########+....-..", "#########.-####+..", "#....#...+#######.", "#....#...###+++##+", "#######+-##+.-.###", "#....#...--...-###", "#....#....+...+##+", "###########+.###+.", "#....#....#.###+..", "#....#....#.##+...", "###########.--....", "............###...", "............###...", "............###..."]),
        ["A2"] = (12, 7, ["..........-###+.....", "............+##+....", ".............###-...", ".............+###...", "......##.....+###-..", ".....+##+....-###+..", ".....####....-###+..", "....+####+.+#######+", "....##..##..+#####+.", "...+##..##+..#####..", "...###..###...###...", "..+###..###+...#....", "..####..####........", ".+##########+.......", ".#####..#####.......", "+#####..#####+......", "##############......", "##############......"]),
        ["A3"] = (12, 9, ["################....", "################....", "################....", "################....", "#....#....#....#....", "#....#....#....#....", "################....", "#....#....#....+.#+.", "#....#....#.....###+", "########..+##+.####+", "#....#..+#....####+.", "#....#.+###..####+..", "######.+########+...", "........+######+....", ".........+####+.....", "..........+##+......", "...........++......."]),
        ["R1"] = (13, 9, ["########+...........", "#######.#+..........", "#######.##+.........", "#######.###+........", "#######....#........", "############........", "###########-...#....", "##########+.#-.#.-#.", "###########.-#-#-#-.", "##########...-###-..", "##########.#########", "##########...-###-..", "###########.-#-#-#-.", "##########+.#-.#.-#.", "###########-...#...."]),
        ["R2"] = (14, 8, ["###############", "#.#.........#.#", "###.######..###", "###.........###", "###.######..###", "###.........###", "###.........###", "###############", "###############", "##..........###", "##...######.###", "##...####.#.###", "##...####.#.##+", "##...####.#.##.", "#############.."]),
        ["R3"] = (15, 8, ["....#####....", "....#-.-#....", "+###########+", "#############", ".............", ".###########.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".##.##.##.##.", ".###########."]),
        ["R4"] = (12, 9, ["################.....", "################.....", "################.....", "################.....", "#....#....#....#.....", "#....#....#....#.....", "################..#..", "#....#....#....-.###.", "#....#....#.....#.###", "#############-.###.#.", "#....#....#...#####..", "#....#....#..#####...", "###########.#####....", "...........+.###.....", "...........##.#......", "..........-##+.......", "..........#+-........"]),
        ["R5"] = (12, 7, [".......###........", ".......###........", ".......###........", "....+#######+.....", ".....+#####+......", "......####+.......", ".......###........", "........#.........", "..................", "##################", "############.#.#.#", "############.#.#.#", "############.#.#.#", "############.#.#.#", "##..########.#.#.#", "##..########.#.#.#", "############.#.#.#", "##################"]),
        ["R6"] = (12, 6, ["........#.........", ".......###........", "......+####.......", ".....+#####+......", "....+#######+.....", ".......###........", ".......###........", ".......###........", "..................", "##################", "############.#.#.#", "############.#.#.#", "############.#.#.#", "############.#.#.#", "##..########.#.#.#", "##..########.#.#.#", "############.#.#.#", "##################"]),
        ["T1"] = (15, 11, ["############", "############", "############", "############", "############", "############", "############", "############", "############", "############", "############", "############"]),
        ["T2"] = (16, 10, ["--......-", "--.....--", "--....---", "--...----", "--..-----", "--.------", "---------", "--.------", "--..-----", "--...----", "--....---", "--.....--", "--......-"]),
        ["T3"] = (13, 10, ["......+......+", ".....+#.....+#", "....+##....+##", "...####...####", "..#####..#####", ".######.######", "##############", ".######.######", "..#####..#####", "...####...####", "....+##....+##", ".....+#.....+#", "......+......+"]),
        ["T4"] = (15, 10, ["-......-......", "--.....--.....", "---....---....", "----...----...", "-----..-----..", "------.------.", "--------------", "------.------.", "-----..-----..", "----...----...", "---....---....", "--.....--.....", "-......-......"]),
        ["T5"] = (9, 8, [".......-+####+-.......", "......+########+......", ".....+###----###+.....", "....-##+......+##-....", "....+##........##+....", "....##-........-##....", "....##-........-##....", "....##-........-##....", "....##-........-##....", "....+##........##+....", "....-##+......+##-....", ".....+###----#####....", "..##..+############...", "..##...-+####+-#####..", "######..........#####.", "######...........#####", "..##..............####", "..##...............+#+"]),
        ["T6"] = (9, 8, [".......-+####+-.......", "......+########+......", ".....+###----###+.....", "....-##+......+##-....", "....+##........##+....", "....##-........-##....", "....##-........-##....", "....##-........-##....", "....##-........-##....", "....+##........##+....", "....-##+......+##-....", ".....+###----#####....", "......+############...", ".......-+####+-#####..", "................#####.", "######...........#####", "######............####", "...................+#+"]),
        ["T7"] = (19, 6, [".###.", "#####", "##-##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "#####", "-###-"]),
        ["T8"] = (11, 6, ["..........###.", ".........#####", ".........##-##", ".........##.##", ".........##.##", ".........##.##", "...+.....##.##", "..+#.....##.##", ".######..##.##", "#######..##.##", ".######..##.##", "..+#.....##.##", "...+.....##.##", ".........##.##", ".........##.##", ".........##.##", ".........##.##", ".........#####", ".........-###-"]),
        ["T9"] = (17, 6, [".###..........", "#####.........", "##-##.........", "##.##.........", "##.##.........", "##.##.........", "##.##.....+...", "##.##.....#+..", "##.##..######.", "##.##..#######", "##.##..######.", "##.##.....#+..", "##.##.....+...", "##.##.........", "##.##.........", "##.##.........", "##.##.........", "#####.........", "-###-........."]),
        ["R1L"] = (16, 8, ["###########+..............", "##########.#+.............", "##########.##+............", "##########.####...........", "##########.#####..........", "##########.######.........", "##########......#.........", "#################.........", "#################.........", "#################.........", "###############+#..#......", "##############.--..#...-..", "#############+-#-..#..-#-.", "##############--#-.#.-#-..", "###############--#-#-#-...", "############----.-###-....", "############-#############", "############----.-###-....", "###############--#-#-#-...", "##############--#-.#.-#-..", "#############+-#-..#..-#-.", "##############.--..#...-..", "###############+#..#......"]),
        ["R2L"] = (17, 9, ["#####################", "#..#.............#..#", "#..#.............#..#", "####..########...####", "####.............####", "####.............####", "####..########...####", "####.............####", "####.............####", "####.............####", "#####################", "#####################", "###..............####", "###..............####", "###....########..####", "###....#####..#..####", "###....#####..#..####", "###....#####..#..###+", "###....#####..#..##+.", "##################+..", "##################..."]),
        ["R3L"] = (18, 9, ["......########......", "......#.....-#......", ".##################.", "+##################+", "+##################+", "....................", "..################..", "..################..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..###..##..##..###..", "..################..", "..################.."]),
        ["R4L"] = (16, 11, ["######################......", "######################......", "######################......", "######################......", "######################......", "######################......", "#......#......#......#......", "#......#......#......#......", "#......#......#......#......", "######################..##..", "#......#......#......-.####.", "#......#......#.......#.####", "#......#......#......###.###", "##################-.#####.#.", "#......#......#....#######..", "#......#......#...#######...", "#......#......#..#######....", "###############.+.#####.....", "................##.###......", "...............-###.#.......", "...............+###+........", "...............#+-.........."]),
        ["R5L"] = (16, 9, [".........#####-.........", ".........#####-.........", ".........#####-.........", ".........#####-.........", "......+#########+.......", ".......+#######+........", "........+#####+.........", ".........+###+..........", "..........+#+...........", "...........-............", "########################", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "##..###########..#..#..#", "##..###########..#..#..#", "###############..#..#..#", "########################"]),
        ["R6L"] = (16, 7, ["...........-............", "..........+#+...........", ".........+###+..........", "........+#####+.........", ".......+#######+........", "......+#########+.......", "......---#####---.......", ".........#####-.........", ".........#####-.........", ".........#####-.........", "........................", "########################", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "###############..#..#..#", "##..###########..#..#..#", "##..###########..#..#..#", "###############..#..#..#", "########################"]),
        ["A1L"] = (16, 11, ["######################...", "######################...", "######################...", "######################...", "######################...", "################-........", "#......#......+.+####+-..", "#......#.......+#######+.", "#......#......+####+####-", "#############.###+...####", "#......#......###-...+###", "#......#.............####", "#......#......#.....+###-", "#################+.+###+.", "#......#......#...+###+..", "#......#......#...###-...", "#......#......#...###....", "################+........", "..................###....", "..................###....", "..................###...."]),
        ["A2L"] = (16, 9, ["............-###+.......", "..............+###-.....", "...............####.....", "...............+###+....", "...............-####-...", "........##......####+...", ".......+##+.....#####...", ".......####.....#####...", "......+####+....#####...", "......######.+#########+", ".....+######+.+#######+.", "....-###..###-.+#####+..", "....+###..###+..+###+...", "...-####..####-..+#+....", "...#####..#####...-.....", "..-#####..#####-........", "..######..######........", ".+##############+.......", ".#######..#######.......", "+#######..#######+......", "##################......", "##################......"]),
        ["A3L"] = (16, 11, ["######################......", "######################......", "######################......", "######################......", "######################......", "######################......", "#......#......#......#......", "#......#......#......#......", "#......#......#......#..++..", "####################.#.+##+.", "#......#......#.......+####+", "#......#.............+#####+", "#......#.....+-.....+#####+.", "##########+.+##-.#.+#####+..", "#......#...+####-.+#####+...", "#......#...-#####+#####+....", "#......#....-#########+.....", "############--#######+......", "..............-#####+.......", "...............-###+........", "................-#+........."]),
        ["T1L"] = (20, 12, ["################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################", "################"]),
        ["T2L"] = (22, 11, ["---.........", "---........-", "---.......--", "---......---", "---.....----", "---....-----", "---...------", "---..-------", "---.--------", "------------", "---.--------", "---..-------", "---...------", "---....-----", "---.....----", "---......---", "---.......--", "---........-", "---........."]),
        ["T3L"] = (18, 11, ["........+........+", ".......+#.......+#", "......+##......+##", ".....+###.....+###", "....+####....+####", "...-#####...-#####", "..-######..-######", ".-#######.-#######", "-########-########", "##################", "-########-########", ".-#######.-#######", "..-######..-######", "...-#####...-#####", "....+####....+####", ".....+###.....+###", "......+##......+##", ".......+#.......+#", "........+........+"]),
        ["T4L"] = (20, 12, ["-........-........", "--.......--.......", "---......---......", "----.....----.....", "-----....-----....", "------...------...", "-------..-------..", "--------.--------.", "------------------", "--------.--------.", "-------..-------..", "------...------...", "-----....-----....", "----.....----.....", "---......---......", "--.......--.......", "-........-........"]),
        ["T5L"] = (14, 9, ["..........+#####+...........", "........+#########+.........", ".......+###+++++###+........", "......+###-......+##+.......", "......###.........+##.......", ".....+##-..........##+......", ".....##+...........+##......", ".....##+...........+##......", ".....##-...........+##......", ".....##+...........+##......", ".....##+...........+##......", ".....+##-..........##+......", "......###.........+##.......", "......+###-......+###+......", ".......+###+++++######+.....", "........+##############+....", "...###....+#####+.+#####+...", "...###.............+#####+..", "...###..............+#####+.", "#########............+#####+", "#########.............+#####", "#########..............+####", "...###..................+##+", "...###......................", "...###......................"]),
        ["T6L"] = (14, 9, ["..........+#####+...........", "........+#########+.........", ".......+###+++++###+........", "......+###-......+##+.......", "......###.........+##.......", ".....+##-..........##+......", ".....##+...........+##......", ".....##+...........+##......", ".....##-...........+##......", ".....##+...........+##......", ".....##+...........+##......", ".....+##-..........##+......", "......###.........+##.......", "......+###-......+###+......", ".......+###+++++######+.....", "........+##############+....", "..........+#####+.+#####+...", "...................+#####+..", "#########...........+#####+.", "#########............+#####+", "#########.............+#####", ".......................+####", "........................+##+"]),
        ["T7L"] = (25, 7, [".+##+.", "+####+", "##++##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##--##", "##++##", "+####+", ".+##+."]),
        ["T8L"] = (15, 7, [".............+##+.", "............+####+", "............##++##", "............##--##", "............##--##", "............##--##", "............##--##", ".....+......##--##", "....+#......##--##", "...+##......##--##", "..+#######..##--##", ".+########..##--##", "-#########..##--##", ".+########..##--##", "..+#######..##--##", "...+##......##--##", "....+#......##--##", ".....+......##--##", "............##--##", "............##--##", "............##--##", "............##--##", "............##--##", "............##++##", "............+####+", ".............+##+."]),
        ["T9L"] = (23, 7, [".+##+.............", "+####+............", "##++##............", "##--##............", "##--##............", "##--##............", "##--##............", "##--##......+.....", "##--##......#+....", "##--##......##+...", "##--##..#######+..", "##--##..########+.", "##--##..#########-", "##--##..########+.", "##--##..#######+..", "##--##......##+...", "##--##......#+....", "##--##......+.....", "##--##............", "##--##............", "##--##............", "##--##............", "##--##............", "##++##............", "+####+............", ".+##+............."]),
    };

    /// <summary>Draws the tool bar icon into a button at (l, t) of size w×h; the runtime offsets hold for a 45×33 button,
    /// other sizes keep the icon at the same place relative to the button center. Disabled icons (T2, T4) are gray.</summary>
    private static void ToolIcon(Canvas into, string name, double l, double t, double w, double h)
    {
        // the TP1200 draws a larger icon set on its 56×40 buttons (offsets for 56×40)
        var large = w >= 56 && ToolIcons.ContainsKey(name + "L");
        if (!ToolIcons.TryGetValue(large ? name + "L" : name, out var ic)) return;
        var (bw0, bh0) = large ? (56.0, 40.0) : (45.0, 33.0);
        var disabled = name is "T2" or "T4";
        var (iw, ih) = (ic.Rows.Max(r => r.Length), ic.Rows.Length);
        var px = new byte[iw * ih * 4];
        for (var y = 0; y < ih; y++)
            for (var x = 0; x < ic.Rows[y].Length; x++)
            {
                var (c, a) = ic.Rows[y][x] switch
                {
                    '#' => (disabled ? Color.FromRgb(0xAC, 0xA8, 0xAB) : Color.FromRgb(0x59, 0x52, 0x57), (byte)255),
                    '+' => (disabled ? Color.FromRgb(0xAC, 0xA8, 0xAB) : Color.FromRgb(0x6A, 0x63, 0x68), (byte)(disabled ? 255 : 200)),
                    '-' => (disabled ? Color.FromRgb(0xAC, 0xA8, 0xAB) : Color.FromRgb(0x72, 0x6C, 0x71), (byte)(disabled ? 255 : 110)),
                    _ => (Colors.Transparent, (byte)0),
                };
                var o = (y * iw + x) * 4;
                px[o] = (byte)(c.B * a / 255); px[o + 1] = (byte)(c.G * a / 255); px[o + 2] = (byte)(c.R * a / 255); px[o + 3] = a;
            }
        var bmp = BitmapSource.Create(iw, ih, 96, 96, PixelFormats.Pbgra32, null, px, iw * 4);
        var img = new Image { Source = bmp, Width = iw, Height = ih, Stretch = Stretch.None };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
        into.Children.Add(At(img, l + ic.X + Math.Round((w - bw0) / 2), t + ic.Y + Math.Round((h - bh0) / 2)));
    }

    private static readonly string[] TrendIcons = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9"];

    private static readonly string[] TrendColumnNames = ["Trend", "Tag connection", "Value", "Date/time"];

    /// <summary>f(t) trend view: diagram area with left and right value axis (0–100, labels every 20), grid lines with
    /// alternating band fill, help line (ValueY1HlpLine), ruler in the middle, time axis with five labels (time, date;
    /// the right one is the current time), tool bar buttons and the value table.</summary>
    private static FrameworkElement TrendView(JsonObject p, double w, double h)
    {
        var root = ControlFrame(p, w, h);
        var dl = Num(p["DiagramAreaLeft"]) ?? 2;
        var dt = Num(p["DiagramAreaTop"]) ?? 2;
        var dw = Num(p["DiagramAreaWidth"]) ?? w - 4;
        var dh = Num(p["DiagramAreaHeight"]) ?? h - 110;
        var (x0, x1, y0, y1) = (dl + 29, dl + dw - 30, dt, dt + dh - 34);
        var scale = SB(p["ScaleColor"], Color.FromRgb(0x80, 0x80, 0x83));
        var grid = SB(p["GridlineColor"], Color.FromRgb(0x7B, 0x7D, 0x86));
        var band = SB(p["GridlineFillColor"], Color.FromRgb(0xEA, 0xEA, 0xEE));
        const int steps = 5;
        double Y(double v) => y1 - Math.Floor((y1 - y0) * v / 100 + 1e-9);
        for (var i = 0; i < steps; i += 2)
        {
            var (top, bottom) = (Y(100 - 20 * i), Y(80 - 20 * i));
            root.Children.Add(At(new Rectangle { Width = x1 - x0 - 1, Height = Math.Max(bottom - top, 0), Fill = band }, x0 + 1, top));
        }
        for (var i = 0; i <= steps; i++)
        {
            var v = 20.0 * i;
            var y = Y(v);
            if (Bool(p["GridlineEnabled"], true))
                root.Children.Add(At(new Rectangle { Width = x1 - x0, Height = 1, Fill = grid }, x0, y));
            foreach (var right in new[] { false, true })
            {
                var tb = TextBlockOf(p, Fmt(v), "Font", "ScaleColor");
                tb.Foreground = scale;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                root.Children.Add(At(tb, right ? x1 + 4 : x0 - 4 - tb.DesiredSize.Width, Math.Max(y - tb.DesiredSize.Height / 2, y0 + 5 - tb.DesiredSize.Height / 2)));
                root.Children.Add(At(new Rectangle { Width = 4, Height = 1, Fill = scale }, right ? x1 : x0 - 4, y));
            }
        }
        if (Bool(p["ShowY1HlpLine"]) && Num(p["ValueY1HlpLine"]) is { } hl)
            root.Children.Add(At(new Rectangle { Width = x1 - x0, Height = 1, Fill = scale }, x0, Y(hl)));
        root.Children.Add(At(new Rectangle { Width = 1, Height = y1 - y0 + 1, Fill = scale }, x0, y0));
        root.Children.Add(At(new Rectangle { Width = 1, Height = y1 - y0 + 1, Fill = scale }, x1, y0));
        root.Children.Add(At(new Rectangle { Width = x1 - x0 + 1, Height = 1, Fill = scale }, x0, y1 + 1));
        root.Children.Add(At(new Rectangle { Width = 1, Height = y1 - y0, Fill = SB(p["RulerColor"], Color.FromRgb(0x7B, 0x7D, 0x86)) }, Math.Round((x0 + x1) / 2), y0));
        var now = DateTime.Now;
        for (var i = 0; i <= 4; i++)
        {
            var x = Math.Round(x0 + (x1 - x0) * i / 4.0);
            root.Children.Add(At(new Rectangle { Width = 1, Height = 5, Fill = scale }, x, y1 + 1));
            var t = now.AddSeconds(25 * (i - 4));
            var tb = TextBlockOf(p, t.ToString("h:mm:ss tt", CultureInfo.InvariantCulture) + "\n" + t.ToString("M/d/yyyy", CultureInfo.InvariantCulture), "Font", "ScaleColor");
            tb.Foreground = scale;
            tb.LineHeight = 13;
            tb.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var lx = i == 0 ? x : i == 4 ? x - tb.DesiredSize.Width : x - tb.DesiredSize.Width / 2;
            root.Children.Add(At(tb, lx, y1 + 8));
        }
        if (Bool(p["ToolbarEnabled"], true)) Buttons(root, p, icons: TrendIcons, dy: -3);
        var vl = Num(p["ValueTableLeft"]) ?? 6;
        var vt = (Num(p["ValueTableTop"]) ?? h - 65) - 3;
        var vw = Num(p["ValueTableWidth"]) ?? w - 12;
        var vh = Num(p["ValueTableHeight"]) ?? 59;
        var widths = Ints32(p["ColumnWidth"]);
        var order = Ints32(p["ColumnOrder"]);
        var cols = Enumerable.Range(0, widths.Length).OrderBy(i => i < order.Length ? order[i] : i)
            .Select(i => ((double)widths[i], i < TrendColumnNames.Length ? TrendColumnNames[i] : "")).ToList();
        var table = new Canvas { Width = vw, Height = vh, ClipToBounds = true };
        root.Children.Add(At(table, vl, vt));
        var hh = HeaderRow(table, p, 0, 0, cols);
        table.Children.Add(At(new Rectangle { Width = vw, Height = Math.Max(vh - hh, 0), Fill = Brushes.White }, 0, hh));
        // the trends of the view with the simulated values (runtime C03b): a line at the current value over the diagram,
        // and per trend a table row (18 px, every second one light gray) in the trend color: color mark, name, tag,
        // value (Real with 6 decimals), date and time
        var trends = (p["__trends"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        for (var r = 0; r < trends.Count; r++)
        {
            var tr = trends[r];
            var tag = TreeBuilder.Str(tr["tag"]);
            var color = SB(tr["color"], Colors.Red);
            var val = TagValue(tag);
            if (TagValues != null)
                root.Children.Add(At(new Rectangle { Width = x1 - x0 - 1, Height = Math.Max(Num(tr["lineWidth"]) ?? 1, 1), Fill = color }, x0 + 1, Y(Math.Clamp(TagNum(tag), 0, 100))));
            // the first row is 20 px (its color mark 1 px below its top), the others 18 px
            var rh = r == 0 ? 20.0 : 18.0;
            var y = hh + (r == 0 ? 0 : 20 + (r - 1) * 18);
            if (r % 2 == 1) table.Children.Add(At(new Rectangle { Width = vw, Height = rh, Fill = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE8)) }, 0, y));
            table.Children.Add(At(new Rectangle { Width = 5, Height = 18, Fill = color }, 0, y + (r == 0 ? 1 : 0)));
            var cx = 0.0;
            foreach (var (cw, cname) in cols)
            {
                var text = cname switch
                {
                    "Trend" => TreeBuilder.Str(tr["name"]) ?? "",
                    "Tag connection" => tag ?? "",
                    "Value" => TagValues == null ? "" : val is double d && d != Math.Floor(d) ? d.ToString("F6", CultureInfo.InvariantCulture) : Fmt(TagNum(tag)),
                    "Date/time" => TagValues == null ? "" : DateTime.Now.ToString("M/d/yyyy h:mm:ss.fff tt", CultureInfo.InvariantCulture),
                    _ => "",
                };
                var cell = new Canvas { Width = cw, Height = rh, ClipToBounds = true };
                var tb = TextBlockOf(p, text, "Font", "ForeColor");
                tb.Foreground = color;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var tx = cname == "Value" ? cw - 4 - tb.DesiredSize.Width : cname == "Trend" ? 6 : 4;
                cell.Children.Add(At(tb, tx, (rh - tb.DesiredSize.Height) / 2));
                table.Children.Add(At(cell, cx, y));
                cx += cw;
            }
        }
        return root;
    }

    /// <summary>Horizontal scroll bar of a table control (arrows and a bar in TableForeColor), 34 px high.</summary>
    /// <summary>Horizontal scroll bar metrics of the table controls, measured in the runtime per device: row height,
    /// arrow tip inset, arrow width and height, arrow center, thumb top, thumb height, thumb inset left and right.</summary>
    private static (double Row, double Tip, double Aw, double Ah, double Cy, double BarTop, double BarH, double BarL, double BarR) ScrollMetrics() =>
        !IsPanel ? (25, 9, 7, 13, 12.5, 10, 5, 26, 27)
        : _screenWidth >= 1280 ? (34, 13, 9, 17, 17.5, 14, 6, 34, 34)
        : (27, 10, 7, 13, 13.5, 11, 5, 27, 29);

    private static double ScrollRowHeight => ScrollMetrics().Row;

    private static void HScroll(Canvas into, JsonObject p, double x, double y, double w)
    {
        // row (E6E6E8) under a 1 px line (9C9DA4), filled arrows at both ends, thumb at position 0
        var m = ScrollMetrics();
        var fg = SB(p["TableForeColor"], Color.FromRgb(0x18, 0x1C, 0x31));
        into.Children.Add(At(new Rectangle { Width = w, Height = m.Row, Fill = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE8)) }, x, y));
        into.Children.Add(At(new Rectangle { Width = w, Height = 1, Fill = new SolidColorBrush(Color.FromRgb(0x9C, 0x9D, 0xA4)) }, x, y - 1));
        var cy = y + m.Cy;
        var a = m.Ah / 2;
        into.Children.Add(new Polygon { Points = [new Point(x + m.Tip, cy), new Point(x + m.Tip + m.Aw, cy - a), new Point(x + m.Tip + m.Aw, cy + a)], Fill = fg });
        into.Children.Add(new Polygon { Points = [new Point(x + w - m.Tip, cy), new Point(x + w - m.Tip - m.Aw, cy - a), new Point(x + w - m.Tip - m.Aw, cy + a)], Fill = fg });
        into.Children.Add(At(new Rectangle { Width = Math.Max(w - m.BarL - m.BarR, 0), Height = m.BarH, Fill = fg }, x + m.BarL, y + m.BarTop));
    }

    private static readonly string[] UserColumnNames = ["User", "Password", "Group", "Logoff time"];

    /// <summary>User view: header from Columns (pairs of column id and width), white table, the password cell of the
    /// first row selected (runtime: input focus).</summary>
    private static FrameworkElement UserView(JsonObject p, double w, double h)
    {
        var root = ControlFrame(p, w, h);
        var bw = Num(p["BorderWidth"]) ?? 1;
        var pairs = Ints32(p["Columns"]);
        var cols = new List<(double, string)>();
        for (var i = 0; i + 1 < pairs.Length; i += 2)
            cols.Add((pairs[i + 1], pairs[i] >= 0 && pairs[i] < UserColumnNames.Length ? UserColumnNames[pairs[i]] : ""));
        var area = new Canvas { Width = Math.Max(w - 2 * (bw + 1), 0), Height = Math.Max(h - 2 * (bw + 1), 0), ClipToBounds = true };
        root.Children.Add(At(area, bw + 1, bw + 1));
        area.Children.Add(new Rectangle { Width = area.Width, Height = area.Height, Fill = Brushes.White });
        var hh = HeaderRow(area, p, 0, 0, cols);
        if (cols.Count > 1)
            area.Children.Add(At(new Rectangle { Width = cols[1].Item1, Height = 18, Fill = SB(p["SelectionBackColor"], Color.FromRgb(0x94, 0xB6, 0xE7)) }, cols[0].Item1, hh));
        return root;
    }

    private static readonly string[] WatchColumnNames = ["Connection", "Type", "DB No.", "Offset", "Bit", "Data type", "Format", "Status value", "Control value"];

    /// <summary>Watch table (status/force): list area with the header (SetOfVisibleColumns, ColumnOrder, ColumnWidth),
    /// white table, scroll bar when the columns are wider than the area, buttons at ButtonPositions.</summary>
    private static FrameworkElement WatchTable(JsonObject p, double w, double h)
    {
        var root = ControlFrame(p, w, h);
        var (ll, lt) = (Num(p["ListAreaLeft"]) ?? 2, Num(p["ListAreaTop"]) ?? 2);
        var (lw, lh) = (Num(p["ListAreaWidth"]) ?? w - 4, Num(p["ListAreaHeight"]) ?? h - 50);
        var widths = Ints32(p["ColumnWidth"]);
        var order = Ints32(p["ColumnOrder"]);
        var vis = Ints16(p["SetOfVisibleColumns"]).Select(v => v - 1).Where(i => i >= 0 && i < widths.Length).ToList();
        if (vis.Count == 0) vis = [.. Enumerable.Range(0, widths.Length)];
        var cols = vis.OrderBy(i => i < order.Length ? order[i] : i).Select(i => ((double)widths[i], i < WatchColumnNames.Length ? WatchColumnNames[i] : "")).ToList();
        var area = new Canvas { Width = lw, Height = lh, ClipToBounds = true };
        root.Children.Add(At(area, ll, lt));
        var hh = HeaderRow(area, p, 0, 0, cols);
        var scroll = cols.Sum(c => c.Item1) > lw;
        var sbh = scroll ? ScrollRowHeight : 0;
        area.Children.Add(At(new Border { Width = lw, Height = Math.Max(lh - hh - sbh, 0), Background = Brushes.White, BorderBrush = SB(p["BorderColor"], Colors.Gray), BorderThickness = new Thickness(0, 0, 0, 1) }, 0, hh));
        if (scroll) HScroll(area, p, 0, lh - sbh, lw);
        Buttons(root, p);
        return root;
    }

    /// <summary>Recipe view (extended): recipe and data record combo boxes with their numbers, the entry list with
    /// its header, six buttons under the list and the status line.</summary>
    private static FrameworkElement RecipeView(JsonObject p, double w, double h)
    {
        var root = ControlFrame(p, w, h);
        var (ll, lt) = (Num(p["ListAreaLeft"]) ?? 12, Num(p["ListAreaTop"]) ?? 124);
        var (lw, lh) = (Num(p["ListAreaWidth"]) ?? w - 24, Num(p["ListAreaHeight"]) ?? 97);
        var fore = SB(p["ForeColor"], Color.FromRgb(0x47, 0x49, 0x57));
        var tx = new JsonObject
        {
            ["BorderWidth"] = p["TextualObjectsBorderWidth"]?.DeepClone() ?? 4,
            ["EdgeStyle"] = p["TextualObjectsEdgeStyle"]?.DeepClone() ?? 6,
            ["BorderColor"] = p["TextualObjectsBorderColor"]?.DeepClone(),
            ["BorderBackColor"] = p["TextualObjectsBorderBackColor"]?.DeepClone(),
            ["BackColor"] = "0xFFFFFFFF",
            ["BackFillStyle"] = 0,
            ["Font"] = p["Font"]?.DeepClone(),
            ["ForeColor"] = p["ForeColor"]?.DeepClone(),
            ["VerticalAlignment"] = 1,
            ["LeftMargin"] = 1,
        };
        var numX = ll + lw - 73;
        void Label(string text, double x, double y)
        {
            var tb = TextBlockOf(p, text, "HeaderFont", "ForeColor");
            tb.Foreground = fore;
            root.Children.Add(At(tb, x, y));
        }
        // the recipe of the view (C03b): its first data record is selected; the recipe name is gray (fixed recipe)
        var recipe = p.Select(kv => kv.Value).OfType<JsonObject>().Where(o => TreeBuilder.Str(o["kind"]) == "HmiRecipeCoreData")
            .Select(o => TreeBuilder.Str(o["name"])).Where(n => n != null).Select(n => ScreenRenderer.RecipeByName(n!)).FirstOrDefault(r => r != null);
        var record = recipe != null ? TreeBuilder.Arr(recipe["records"]).FirstOrDefault() : null;
        foreach (var (label, y) in new[] { ("Recipe Name:", 14.0), ("Data Record Name:", 65.0) })
        {
            Label(label, ll, y);
            Label("No.:", numX, y);
            var combo = (Grid)Box(tx, numX - ll - 11, 26, null, Num(p["TextualObjectsCornerRadius"]) ?? 0);
            combo.Children.Add(new Polygon
            {
                Points = [new Point(0, 0), new Point(16, 0), new Point(8, 5)],
                Stroke = SB(p["TextualObjectsBorderColor"], Colors.Gray), StrokeThickness = 1, Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
            });
            root.Children.Add(At(combo, ll, y + 17));
            root.Children.Add(At(Box(tx, 73, 26, recipe == null ? "-----" : null, 0), numX, y + 17));
        }
        if (recipe != null)
        {
            void Value(string text, double x, double y, double cwid, Brush brush, bool bold)
            {
                var tb = TextBlockOf(p, text, "Font", "ForeColor");
                tb.Foreground = brush;
                if (bold) tb.FontWeight = FontWeights.Bold;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var clip = new Canvas { Width = Math.Max(cwid, 0), Height = 20, ClipToBounds = true };
                clip.Children.Add(At(tb, 0, (20 - tb.DesiredSize.Height) / 2));
                root.Children.Add(At(clip, x, y));
            }
            var tbw = Num(p["TextualObjectsBorderWidth"]) ?? 4;
            Value(TreeBuilder.Str(recipe["displayName"]) is { Length: > 0 } rd ? rd : TreeBuilder.Str(recipe["name"]) ?? "", ll + tbw + 2, 14 + 17 + 3, numX - ll - 11 - 2 * tbw - 30, new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)), true);
            Value(Fmt(Num(recipe["number"]) ?? 0), numX + tbw + 2, 14 + 17 + 3, 73 - 2 * tbw - 4, fore, true);
            if (record != null)
            {
                Value(TreeBuilder.Str(record["displayName"]) is { Length: > 0 } dd ? dd : TreeBuilder.Str(record["name"]) ?? "", ll + tbw + 2, 65 + 17 + 3, numX - ll - 11 - 2 * tbw - 30, fore, true);
                Value(Fmt(Num(record["number"]) ?? 0), numX + tbw + 2, 65 + 17 + 3, 73 - 2 * tbw - 4, fore, true);
            }
        }
        var cw = Ints32(p["ColumnWidth"]);
        var nameW = cw.Length > 0 ? cw[0] : Num(p["EntryNameColumnWidth"]) ?? 202;
        var valueW = cw.Length > 1 ? cw[1] : Num(p["EntryValueColumnWidth"]) ?? 132;
        var hp = new JsonObject { ["TableHeaderFont"] = p["HeaderFont"]?.DeepClone() };
        foreach (var (k, v) in p) if (k.StartsWith("TableHeader", StringComparison.Ordinal) || k == "UseTableHeaderFirstGradient") hp[k] = v?.DeepClone();
        hp["TableHeaderForeColor"] ??= "0xFFFFFFFF";
        var hh = HeaderRow(root, hp, ll, lt, [(nameW, "Entry Name"), (valueW, "Value")]);
        root.Children.Add(At(new Border { Width = lw, Height = lh + 2, Background = Brushes.White, BorderBrush = SB(p["BorderColor"], Colors.Gray), BorderThickness = new Thickness(1) }, ll, lt + hh));
        if (recipe != null)
        {
            // one row per element (19 px, every second one light gray): name left, value right-aligned in the value
            // column with the element's decimal places; the value of the first row is selected (runtime C03b)
            var rowH = 19.0;
            var rows = new Canvas { Width = lw - 2, Height = lh, ClipToBounds = true };
            var i = 0;
            foreach (var e in TreeBuilder.Arr(recipe["elements"]))
            {
                var y = i * rowH;
                if (i % 2 == 1) rows.Children.Add(At(new Rectangle { Width = nameW + valueW, Height = rowH, Fill = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)) }, 0, y));
                if (i == 0) rows.Children.Add(At(new Rectangle { Width = valueW, Height = rowH, Fill = new SolidColorBrush(Color.FromRgb(0x99, 0xB4, 0xE0)) }, nameW, y));
                var name = TextBlockOf(p, TreeBuilder.Str(e["displayName"]) is { Length: > 0 } en ? en : TreeBuilder.Str(e["name"]) ?? "", "Font", "ForeColor");
                name.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                rows.Children.Add(At(name, 2, y + (rowH - name.DesiredSize.Height) / 2));
                var raw = record?["values"]?[TreeBuilder.Str(e["index"]) ?? ""] is { } rv ? TreeBuilder.Str(rv) : TreeBuilder.Str(e["default"]);
                var dp = (int)(Num(e["decimalPlaces"]) ?? 0);
                var text = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv)
                    ? Math.Round(dv, dp, MidpointRounding.AwayFromZero).ToString("F" + dp, CultureInfo.InvariantCulture) : raw ?? "";
                var vt = TextBlockOf(p, text, "Font", "ForeColor");
                vt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                rows.Children.Add(At(vt, nameW + valueW - 3 - vt.DesiredSize.Width, y + (rowH - vt.DesiredSize.Height) / 2));
                i++;
            }
            root.Children.Add(At(rows, ll + 1, lt + hh + 1));
        }
        var by = lt + hh + lh + 2 + 6;
        var bp = new JsonObject();
        foreach (var (k, v) in p) if (k.StartsWith("Button", StringComparison.Ordinal)) bp[k] = v?.DeepClone();
        // tool bar: 56×40 buttons (pitch 61) on the TP1200, 44×32 (pitch 49) on the TP900 and the PC runtime
        var big = IsPanel && _screenWidth >= 1280;
        var (bw, bh, pitch) = big ? (56.0, 40.0, 61.0) : (44.0, 32.0, 49.0);
        var bty = big ? by - 1 : by - 12;
        double[] xs = [ll, ll + pitch, ll + 2 * pitch, ll + 3 * pitch, ll + lw - pitch - bw, ll + lw - bw];
        for (var k = 0; k < xs.Length; k++)
        {
            var x = xs[k];
            root.Children.Add(At(new Border
            {
                Width = bw, Height = bh,
                Background = Gradient3(bp, "Button", Color.FromRgb(0xE9, 0xE8, 0xE8)),
                BorderBrush = SB(p["ButtonBorderColor"], Colors.Gray),
                BorderThickness = new Thickness(Num(p["ButtonBorderWidth"]) ?? 1),
                CornerRadius = new CornerRadius(Num(p["ButtonCornerRadius"]) ?? 0),
            }, x, bty));
            ToolIcon(root, "R" + (k + 1), x, bty, bw, bh);
        }
        var st = TextBlockOf(p, "Ready", "StatuslineFont", "ForeColor");
        st.Foreground = fore;
        st.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Children.Add(At(st, ll + 3, by + 40 + (h - by - 40 - st.DesiredSize.Height) / 2));
        return root;
    }

    private static TextBlock Plain(string text, double size, Color color, string family = "Tahoma", bool bold = false)
    {
        var tb = new TextBlock { Text = text, FontFamily = new FontFamily(family), FontSize = size, Foreground = new SolidColorBrush(color), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        return tb;
    }

    private static T Centered<T>(Canvas c, T el, double w, double h, double? y = null) where T : FrameworkElement
    {
        el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return At(el, (w - el.DesiredSize.Width) / 2, y ?? (h - el.DesiredSize.Height) / 2);
    }

    /// <summary>Media player without a file: white area with a black frame and the caption "Media Player".</summary>
    private static FrameworkElement MediaPlayer(double w, double h)
    {
        var c = new Canvas { Width = w, Height = h };
        c.Children.Add(new Border { Width = w, Height = h, Background = Brushes.White, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) });
        c.Children.Add(Centered(c, Plain("Media Player", 12, Colors.Black, "Tahoma", true), w, h));
        return c;
    }

    /// <summary>Camera view without a camera: black area with a small camera symbol in the middle.</summary>
    private static FrameworkElement Camera(double w, double h)
    {
        var c = new Canvas { Width = w, Height = h, Background = Brushes.Black };
        var (cx, cy) = (w / 2, h / 2);
        c.Children.Add(At(new Border { Width = 20, Height = 15, Background = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x73)), CornerRadius = new CornerRadius(2), BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x32, 0x40)), BorderThickness = new Thickness(1) }, cx - 13, cy - 8));
        c.Children.Add(At(new Border { Width = 12, Height = 7, Background = new SolidColorBrush(Color.FromRgb(0xA8, 0xB0, 0xC8)), CornerRadius = new CornerRadius(1) }, cx - 10, cy - 4));
        c.Children.Add(new Polygon { Points = [new Point(cx + 7, cy - 2), new Point(cx + 13, cy - 6), new Point(cx + 13, cy + 6), new Point(cx + 7, cy + 2)], Fill = new SolidColorBrush(Color.FromRgb(0x65, 0x67, 0x73)) });
        return c;
    }

    /// <summary>PDF view without a file: sunken frame, dark gray area, red message at the top.</summary>
    private static FrameworkElement PdfView(double w, double h)
    {
        var c = new Canvas { Width = w, Height = h };
        c.Children.Add(new Border { Width = w, Height = h, Background = new SolidColorBrush(Color.FromRgb(0x56, 0x56, 0x56)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xA9, 0xA9, 0xA9)), BorderThickness = new Thickness(2) });
        c.Children.Add(At(new Border { Width = w - 4, Height = h - 4, BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8)), BorderThickness = new Thickness(2) }, 2, 2));
        c.Children.Add(Centered(c, Plain("File  does not exist.", 16, Colors.Red, "Times New Roman"), w, h, 8));
        return c;
    }

    /// <summary>HTML browser: gray frame, tool bar with back/forward arrows, white page area and both scroll bars (the
    /// page itself is not shown).</summary>
    private static FrameworkElement HtmlBrowser(JsonObject p, double w, double h)
    {
        var c = new Canvas { Width = w, Height = h };
        var gray = new SolidColorBrush(Color.FromRgb(0xD7, 0xD7, 0xD7));
        var bar = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        var dark = new SolidColorBrush(Color.FromRgb(0x47, 0x49, 0x57));
        var light = new SolidColorBrush(Color.FromRgb(0xAC, 0xA8, 0xAB));
        c.Children.Add(new Rectangle { Width = w, Height = h, Fill = gray });
        c.Children.Add(At(new Rectangle { Width = w - 8, Height = 49, Fill = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)) }, 4, 4));
        foreach (var (x, dir) in new[] { (22.0, -1), (86.0, 1) })
        {
            var cy = 27.0;
            c.Children.Add(new Polygon { Points = [new Point(x + (dir < 0 ? 0 : 20), cy), new Point(x + (dir < 0 ? 8 : 12), cy - 7), new Point(x + (dir < 0 ? 8 : 12), cy + 7)], Fill = light });
            c.Children.Add(At(new Rectangle { Width = 12, Height = 5, Fill = light }, x + (dir < 0 ? 8 : 0), cy - 2));
        }
        c.Children.Add(At(Plain("»", 12, Colors.Black), w - 18, 18));
        var (px, py, pw, ph) = (4.0, 56.0, w - 42, h - 94);
        c.Children.Add(At(new Rectangle { Width = pw, Height = ph, Fill = Brushes.White }, px, py));
        c.Children.Add(At(new Rectangle { Width = 34, Height = h - 60, Fill = bar }, px + pw, py - 3));
        c.Children.Add(At(new Rectangle { Width = w - 8, Height = 34, Fill = bar }, 4, py + ph));
        var vx = px + pw + 17;
        c.Children.Add(new Polygon { Points = [new Point(vx - 7, py + 20), new Point(vx + 7, py + 20), new Point(vx, py + 12)], Fill = dark });
        c.Children.Add(At(new Rectangle { Width = 5, Height = 8, Fill = dark }, vx - 2, py + 36));
        c.Children.Add(new Polygon { Points = [new Point(vx - 7, py + ph - 20), new Point(vx + 7, py + ph - 20), new Point(vx, py + ph - 12)], Fill = dark });
        var hy = py + ph + 17;
        c.Children.Add(new Polygon { Points = [new Point(px + 14, hy), new Point(px + 22, hy - 7), new Point(px + 22, hy + 7)], Fill = dark });
        c.Children.Add(At(new Rectangle { Width = 22, Height = 5, Fill = dark }, px + 34, hy - 2));
        c.Children.Add(new Polygon { Points = [new Point(px + pw - 14, hy), new Point(px + pw - 22, hy - 7), new Point(px + pw - 22, hy + 7)], Fill = dark });
        return c;
    }

    /// <summary>Sm@rtClient view while it is not connected: status text area, Connect button, address and password
    /// fields (address "MyPanel", the default).</summary>
    private static FrameworkElement SmartClient(JsonObject p, double w, double h)
    {
        var c = new Canvas { Width = w, Height = h, ClipToBounds = true };
        var size = Num((p["Font"] as JsonObject)?["Size"]) ?? 15;
        c.Children.Add(new Rectangle { Width = w, Height = h - 35, Fill = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) });
        c.Children.Add(At(new Rectangle { Width = w, Height = 35, Fill = Brushes.White }, 0, h - 35));
        var msg = Plain("Trying to connect to MyPanel\nUnable to resolve host MyPanel: No such host is known.\nDisconnected.", size, Colors.Black);
        msg.TextWrapping = TextWrapping.Wrap;
        msg.Width = w - 4;
        c.Children.Add(At(msg, 2, 2));
        var frame = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
        var btn = new Border { Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x4C)), BorderBrush = frame, BorderThickness = new Thickness(2) };
        var bt = Plain("Connect", size, Colors.White);
        bt.HorizontalAlignment = HorizontalAlignment.Center;
        bt.VerticalAlignment = VerticalAlignment.Center;
        btn.Child = bt;
        c.Children.Add(At(btn, 0, h - 33));
        foreach (var (x, text) in new[] { (85.0, "MyPanel"), (185.0, "") })
        {
            var f = new Border { Width = 95, Height = 28, Background = Brushes.White, BorderBrush = frame, BorderThickness = new Thickness(3) };
            if (text.Length > 0) f.Child = At(Plain(text, size, Colors.Black), 0, 0);
            ((FrameworkElement?)f.Child)?.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
            c.Children.Add(At(f, x, h - 33));
        }
        return c;
    }

    private static readonly string[] SysDiagColumnNames = ["Status", "Name", "Operating mode", "Slot", "Type", "Order number", "Address", "Plant designation", "Location identifier"];

    /// <summary>System diagnostics view (device view): navigation path, column header (MinimumColumnWidth), list,
    /// scroll bar and the four buttons (home, back, forward, messages). Buttons are 56×40 on wide screens (1280 px),
    /// else 44×32, as in the runtime. The list content (devices and their state) exists only at runtime.</summary>
    private static FrameworkElement SysDiag(JsonObject p, double w, double h)
    {
        var root = ControlFrame(p, w, h);
        var bw = Num(p["BorderWidth"]) ?? 1;
        var path = TextBlockOf(p, "Diagnostic overview", "NavigationPath_Font", "NavigationPath_TextColor");
        path.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Children.Add(At(path, bw + 6, bw + 1 + (26 - path.DesiredSize.Height) / 2));
        var widths = Ints32(p["MinimumColumnWidth"]);
        var cols = widths.Select((cw, i) => ((double)cw, i < SysDiagColumnNames.Length ? SysDiagColumnNames[i] : "")).ToList();
        var x0 = bw + 6;
        var lw = w - 2 * x0;
        var area = new Canvas { Width = lw, Height = h, ClipToBounds = true };
        root.Children.Add(At(area, x0, 0));
        var top = bw + 27;
        var hh = HeaderRow(area, p, 0, top, cols);
        var large = _screenWidth >= 1280;
        var (bwid, bhei, step) = large ? (56.0, 40.0, 61.0) : (44.0, 32.0, 49.0);
        var by = h - 7 - bhei;
        var sy = by - 6 - ScrollRowHeight;
        area.Children.Add(At(new Border { Width = lw, Height = Math.Max(sy - top - hh, 0), Background = SB(p["InfoArea_BackgroundColor"], Colors.White) }, 0, top + hh));
        HScroll(area, p, 0, sy, lw);
        var bp = new JsonObject();
        foreach (var (k, v) in p) if (k.StartsWith("Button", StringComparison.Ordinal)) bp[k] = v?.DeepClone();
        for (var i = 0; i < 4; i++)
            root.Children.Add(At(new Border
            {
                Width = bwid, Height = bhei,
                Background = Gradient3(bp, "Button", Color.FromRgb(0xE9, 0xE8, 0xE8)),
                BorderBrush = SB(p["ButtonBorderColor"], Colors.Gray),
                BorderThickness = new Thickness(Num(p["ButtonBorderWidth"]) ?? 1),
                CornerRadius = new CornerRadius(Num(p["ButtonCornerRadius"]) ?? 0),
            }, x0 + step * i, by));
        return root;
    }

    private static FrameworkElement Frame(JsonObject p, double w, double h) => new Border
    {
        Width = w,
        Height = h,
        Background = Brush(TreeBuilder.Str(p["BackColor"])),
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(0),
    };

    private static FrameworkElement Placeholder(JsonObject p, string name) => new Border
    {
        Width = Math.Max(Num(p["Width"]) ?? 60, 1),
        Height = Math.Max(Num(p["Height"]) ?? 24, 1),
        Background = new SolidColorBrush(Color.FromArgb(0x30, 0x4A, 0x90, 0xD9)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x5C, 0x9E)),
        BorderThickness = new Thickness(1),
        Child = new TextBlock { Text = name, FontSize = 11, Margin = new Thickness(3, 1, 3, 1) },
    };

    private static string Text(JsonNode? t) => t switch
    {
        JsonObject o when ScreenRenderer.Language != null && o.ContainsKey(ScreenRenderer.Language) => TreeBuilder.Str(o[ScreenRenderer.Language]) ?? "",
        JsonObject o => o.Select(kv => TreeBuilder.Str(kv.Value)).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "",
        JsonValue v => v.ToString(),
        _ => "",
    };
}
