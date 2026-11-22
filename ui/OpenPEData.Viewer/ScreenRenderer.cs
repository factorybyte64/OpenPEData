using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenPEData.Viewer;

/// <summary>Draws a WinCC Unified screen with its screen items (Z1.5). <see cref="Render"/> gives the screen at 1:1 in
/// screen pixels; the preview tab wraps it in a scalable frame, <see cref="SavePng"/> and <see cref="Compare"/> serve
/// the comparison with runtime / editor screenshots (options --screen-png, --compare).</summary>
public static partial class ScreenRenderer
{
    /// <summary>Openness defaults per screen item type (HmiDefaults.json, generated from the Openness export of test
    /// project 165). Only for drawing: the parser output holds just what the file stores.</summary>
    private static readonly Lazy<JsonObject> Defaults = new(() =>
    {
        using var s = typeof(ScreenRenderer).Assembly.GetManifestResourceStream("HmiDefaults.json");
        return s == null ? new JsonObject() : JsonNode.Parse(s) as JsonObject ?? new JsonObject();
    });

    /// <summary>Graphics by name: project graphics (parser project.graphics: extension, data = the file as base64), then
    /// the images of the library image types (parser library type versions: image, the default version first) under
    /// the type name, as the Graphic property of screen and faceplate items names them.</summary>
    private static Dictionary<string, JsonNode> _graphics = new();
    private static readonly Dictionary<string, ImageSource?> ImageCache = new();

    /// <summary>Selected style of the HMI software a screen belongs to (parser hmi style: values per item type,
    /// rendering templates), registered while the tree is built.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonNode, JsonObject> Styles = new();
    [ThreadStatic] private static JsonObject? _style;
    /// <summary>The selected style is a Flat style (FlatStyle_Dark/Bright: templates include.style.flat.*).</summary>
    private static bool IsFlatStyle => (_style?["templates"] as JsonObject)?.ContainsKey("include.style.flat.controlbarbutton.default") == true;

    public static void SetStyle(JsonNode screen, JsonObject? style)
    {
        if (style != null) Styles.AddOrUpdate(screen, style);
        if (screen is JsonObject cs && ClassicScreenRenderer.IsClassic(cs)) ClassicScreenRenderer.Register(cs, style);
        // screens by name for screen windows (parser properties.Screen)
        if (screen is JsonObject so && TreeBuilder.Str(so["name"]) is { } n) Screens.TryAdd(n, so);
    }

    private static readonly Dictionary<string, JsonObject> Screens = new();

    /// <summary>Screen window with a static Screen: the referenced screen at 1:1 (Adaption None, scroll position 0)
    /// in the window area, clipped; nested screen windows up to 4 levels.</summary>
    private static FrameworkElement? ScreenWindowContent(JsonObject p, double w, double h)
    {
        if (TreeBuilder.Str(p["Screen"]) is not { } sn || !Screens.TryGetValue(sn, out var screen) || _faceplateDepth > 3) return null;
        var saved = (_screenName, _style);
        _faceplateDepth++;
        _screenName = sn;
        try
        {
            // a screen shown in a screen window has no background of its own (WebRH DOM: the screen's rect fill="none",
            // also for a screen with BackColor white; TIA screen editor: no style grey)
            var shown = screen.DeepClone().AsObject();
            shown["backColor"] = "0x00000000";
            var c = Render(shown, TreeBuilder.Arr(screen["items"]));
            return new Canvas { Width = w, Height = h, ClipToBounds = true, Children = { c } };
        }
        finally
        {
            _faceplateDepth--;
            (_screenName, _style) = saved;
        }
    }

    public static void SetGraphics(JsonNode? graphics, JsonNode? library = null)
    {
        _graphics = TreeBuilder.Arr(graphics).Where(g => TreeBuilder.Str(g["name"]) != null)
            .GroupBy(g => TreeBuilder.Str(g["name"])!).ToDictionary(g => g.Key, g => g.First());
        foreach (var t in TreeBuilder.Arr(library?["libraries"]).SelectMany(l => TreeBuilder.Arr(l["types"])))
            if (TreeBuilder.Str(t["name"]) is { } name && TypeImage(t) is { } img)
                _graphics.TryAdd(name, img);
        ImageCache.Clear();
    }

    /// <summary>Faceplate type version content (parser "faceplates": versions[].content with width, height, items) by
    /// (screen name, container name) of the typed instances; registered while the tree is built.</summary>
    private static Dictionary<(string, string), (JsonObject Content, JsonNode? Interface)> _faceplates = new();
    [ThreadStatic] private static string? _screenName;
    [ThreadStatic] private static int _faceplateDepth;

    /// <summary>Type version content by ContainedType text "V&lt;version&gt;\&lt;type&gt;" (nested containers in a faceplate type).</summary>
    private static Dictionary<string, JsonObject> _faceplateTypes = new(StringComparer.Ordinal);
    /// <summary>Text lists of the HMI software by name (symbolic I/O fields in the editor view).</summary>
    private static Dictionary<string, JsonNode> _textLists = new(StringComparer.Ordinal);

    /// <summary>A text list set with SetTextLists / SetLibraryTextLists, by name.</summary>
    internal static JsonNode? TextListByName(string name) => _textLists.TryGetValue(name, out var t) ? t : null;

    /// <summary>Graphic lists of the HMI software by name (graphic I/O fields, buttons and switches of classic WinCC).</summary>
    private static Dictionary<string, JsonNode> _graphicLists = new(StringComparer.Ordinal);

    internal static JsonNode? GraphicListByName(string name) => _graphicLists.TryGetValue(name, out var t) ? t : null;

    public static void SetGraphicLists(JsonNode? lists)
    {
        foreach (var t in TreeBuilder.Arr(lists))
            if (TreeBuilder.Str(t["name"]) is { } n) _graphicLists[n] = t;
    }

    /// <summary>Discrete and analog alarms of the HMI software (classic alarm view with simulated tag values).</summary>
    internal static List<JsonObject> Alarms { get; private set; } = [];

    public static void SetAlarms(JsonNode? hmi)
    {
        Alarms = TreeBuilder.Arr(hmi?["discreteAlarms"]).Concat(TreeBuilder.Arr(hmi?["analogAlarms"])).OfType<JsonObject>().ToList();
    }

    /// <summary>Recipes of the HMI software by name (classic recipe view).</summary>
    private static Dictionary<string, JsonNode> _recipes = new(StringComparer.Ordinal);

    internal static JsonNode? RecipeByName(string name) => _recipes.TryGetValue(name, out var t) ? t : null;

    public static void SetRecipes(JsonNode? recipes)
    {
        _recipes = new(StringComparer.Ordinal);
        foreach (var t in TreeBuilder.Arr(recipes))
            if (TreeBuilder.Str(t["name"]) is { } n) _recipes[n] = t;
    }

    /// <summary>Parser graphic object (extension, data) by name.</summary>
    internal static JsonNode? GraphicInfo(string? name) => name != null && _graphics.TryGetValue(name, out var g) ? g : null;

    public static void SetTextLists(JsonNode? lists)
    {
        foreach (var t in TreeBuilder.Arr(lists))
            if (TreeBuilder.Str(t["name"]) is { } n) _textLists[n] = t;
    }

    /// <summary>Library text list types (parser library versions[].textList) by type name, for faceplate texts that
    /// use them; the default version, else the last one. HMI text lists of
    /// the same name keep precedence.</summary>
    public static void SetLibraryTextLists(JsonNode? library)
    {
        foreach (var t in TreeBuilder.Arr(library?["libraries"]).SelectMany(l => TreeBuilder.Arr(l["types"])))
        {
            var versions = TreeBuilder.Arr(t["versions"]).Where(v => v["textList"] is JsonObject).ToList();
            var v = versions.FirstOrDefault(x => x["isDefault"]?.GetValueKind() == System.Text.Json.JsonValueKind.True) ?? versions.LastOrDefault();
            if (v != null && TreeBuilder.Str(t["name"]) is { } n) _textLists.TryAdd(n, v["textList"]!);
        }
    }

    /// <summary>Interface values of the faceplate being drawn (for nested containers bound to them).</summary>
    [ThreadStatic] private static Dictionary<string, JsonNode>? _outerValues;
    /// <summary>Default values of the interface properties of the faceplate type being drawn.</summary>
    [ThreadStatic] private static Dictionary<string, JsonNode>? _outerDefaults;

    public static void SetFaceplates(JsonNode? faceplates)
    {
        _faceplates = new();
        _faceplateTypes = new(StringComparer.Ordinal);
        foreach (var t in TreeBuilder.Arr(faceplates))
        {
            foreach (var v in TreeBuilder.Arr(t["versions"]).Where(v => v["content"] is JsonObject))
                _faceplateTypes.TryAdd($"V{TreeBuilder.Str(v["version"])}\\{TreeBuilder.Str(t["name"])}", (JsonObject)v["content"]!);
            var byGuid = TreeBuilder.Arr(t["versions"]).Where(v => v["content"] is JsonObject)
                .GroupBy(v => TreeBuilder.Str(v["versionGuid"]) ?? "").ToDictionary(g => g.Key, g => (JsonObject)g.First()["content"]!);
            foreach (var i in TreeBuilder.Arr(t["instances"]))
            {
                var guid = TreeBuilder.Str(TreeBuilder.Arr(i["instanceOf"]).FirstOrDefault()?["versionGuid"]);
                if (TreeBuilder.Str(i["screen"]) is { } sn && TreeBuilder.Str(i["name"]) is { } cn && guid != null
                    && byGuid.TryGetValue(guid, out var content))
                    _faceplates.TryAdd((sn, cn), (content, i["interface"]));
            }
        }
    }

    /// <summary>Faceplate container: the content of its type version (items at their positions in a canvas of the
    /// content size) scaled to the container size. Without a type in the file (untyped container) a placeholder.</summary>
    private static FrameworkElement? Faceplate(JsonNode it, double w, double h)
    {
        if (_screenName == null || TreeBuilder.Str(it["name"]) is not { } name || _faceplateDepth > 4) return null;
        // a container in a faceplate type carries its ContainedType; only screen containers are looked up by
        // (screen, name), else a nested Faceplate-Container_13 took the type of the screen's container of that name
        var nested = _faceplateDepth > 0 && it["containedType"] != null;
        if (nested || !_faceplates.TryGetValue((_screenName, name), out var fp))
        {
            // nested container in a faceplate type: its type by ContainedType, its interface values from the item;
            // values bound to the enclosing faceplate (FaceplateInterface) take that faceplate's value
            if (_faceplateDepth == 0 || TreeBuilder.Str(it["containedType"]) is not { } ct || !_faceplateTypes.TryGetValue(ct, out var tc))
                return null;
            var iface = new JsonArray();
            foreach (var e in TreeBuilder.Arr(it["interface"]))
            {
                var c = (JsonObject)e.DeepClone();
                // a dynamized entry keeps no value (InterfaceValues), so in the editor view it stays empty
                if (RuntimeVisibility && TreeBuilder.Str(e["dynamization"]?["type"]) == "FaceplateInterface"
                    && TreeBuilder.Str(e["dynamization"]?["interfaceProperty"]) is { } ip && _outerValues?.TryGetValue(ip, out var ov) == true)
                {
                    c["value"] = ov.DeepClone();
                    c.Remove("dynamization");
                }
                iface.Add(c);
            }
            fp = (tc, iface);
        }
        var content = fp.Content;
        var screen = new JsonObject
        {
            ["name"] = name, ["width"] = Num(content["width"]) ?? w, ["height"] = Num(content["height"]) ?? h,
            // the background of the type content fills the whole container (below), not the content size
            ["backColor"] = "0x00000000",
            ["layers"] = content["layers"]?.DeepClone(), ["esHiddenLayers"] = content["esHiddenLayers"]?.DeepClone(),
        };
        var saved = (_screenName, _style, _outerValues, _outerDefaults);
        _faceplateDepth++;
        try
        {
            _outerValues = InterfaceValues(fp.Interface);
            _outerDefaults = TreeBuilder.Arr(content["interface"]?["properties"])
                .Where(x => TreeBuilder.Str(x["name"]) != null && x["default"] != null)
                .GroupBy(x => TreeBuilder.Str(x["name"])!).ToDictionary(g => g.Key, g => g.First()["default"]!);
            var canvas = Render(screen, WithInterfaceValues(TreeBuilder.Arr(content["items"]), fp.Interface));
            // the runtime shows the content 1:1 from the top left, cut to the container (WebRH DOM: overflow hidden,
            // translate(0,0); a 1600 px wide type in a 1280 px container keeps its positions), no scaling; the type's
            // backColor fills the container (svg of container size, rect 100 %; a 1000 px type in 1280 px). The TIA
            // screen editor shrinks content larger than the container in both directions, keeping the aspect ratio
            // (e.g. a 200 px type in 146 x 147); content larger in one direction only is cut as in the runtime
            // (e.g. 1600 x 80 in 1280 x 80), smaller content keeps its size
            if (!RuntimeVisibility && canvas.Width > w && canvas.Height > h
                && Math.Min(w / canvas.Width, h / canvas.Height) is var f)
                canvas.LayoutTransform = new ScaleTransform(f, f);
            return new Canvas { Width = w, Height = h, ClipToBounds = true, Children = { canvas },
                Background = Brush(TreeBuilder.Str(content["backColor"])) };
        }
        finally
        {
            _faceplateDepth--;
            (_screenName, _style, _outerValues, _outerDefaults) = saved;
        }
    }

    /// <summary>Static interface property values of an instance (not null, not an empty text, not dynamized).</summary>
    private static Dictionary<string, JsonNode> InterfaceValues(JsonNode? iface)
    {
        var values = new Dictionary<string, JsonNode>();
        foreach (var e in TreeBuilder.Arr(iface))
            if (TreeBuilder.Str(e["kind"]) == "property" && TreeBuilder.Str(e["name"]) is { } n && e["value"] is { } v
                && e["dynamization"] == null && !(v is JsonObject o && o.All(kv => string.IsNullOrEmpty(TreeBuilder.Str(kv.Value))))
                && !(v is JsonValue sv && string.IsNullOrEmpty(TreeBuilder.Str(sv))))
                values[n] = v;
        return values;
    }

    /// <summary>Faceplate content items with the static interface property values of the instance: a property bound
    /// to an interface property (dynamization FaceplateInterface, parser interfaceProperty) takes the instance value
    /// when it has one (not null, not an empty text); a dynamized instance value (e.g. ResourceList) has no value
    /// without the runtime, there the value of the type stays.</summary>
    private static IEnumerable<JsonNode> WithInterfaceValues(IEnumerable<JsonNode> items, JsonNode? iface)
    {
        var values = InterfaceValues(iface);
        foreach (var it in items)
        {
            var binds = TreeBuilder.Arr(it["dynamizations"])
                .Where(d => TreeBuilder.Str(d["type"]) == "FaceplateInterface" && TreeBuilder.Str(d["interfaceProperty"]) is { } ip && values.ContainsKey(ip))
                .ToList();
            if (binds.Count == 0)
            {
                yield return it;
                continue;
            }
            var copy = (JsonObject)it.DeepClone();
            var props = copy["properties"] as JsonObject ?? new JsonObject();
            copy["properties"] = props;
            foreach (var d in binds)
            {
                var prop = TreeBuilder.Str(d["property"])!;
                var v = values[TreeBuilder.Str(d["interfaceProperty"])!];
                // a Font member fed by an interface property (parser part dynamization Font.Size / Font.Weight, real
                // project ButtonB FontSize 16, FontBold false/true -> 400/700 through the mapping)
                if (prop.StartsWith("Font.", StringComparison.Ordinal))
                {
                    var member = prop[5..];
                    object? fv = TemplateExpr.FromJson(v);
                    if (fv is bool fb) fv = fb ? 1.0 : 0.0;
                    foreach (var e in TreeBuilder.Arr(d["mapping"]?["entries"]))
                        if (fv is double x && Num(e["from"]) <= x && x <= Num(e["to"])) { fv = TemplateExpr.FromJson(e["value"]); break; }
                    var font = (props["Font"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                    font[member] = member == "Weight" && fv is double w ? (w >= 600 ? "Bold" : "Normal")
                        : fv is double n ? JsonValue.Create(n) : JsonValue.Create(fv?.ToString());
                    props["Font"] = font;
                    continue;
                }
                // multilingual text: the runtime language as for stored texts (templates read plain text)
                props[prop] = v is JsonObject ? JsonValue.Create(Text(v)) : v.DeepClone();
                if (prop == "Text") copy["text"] = props[prop]!.DeepClone();
            }
            yield return copy;
        }
    }

    /// <summary>Image of a library image type: that of its default version, else of its newest version with one.</summary>
    internal static JsonNode? TypeImage(JsonNode? type)
    {
        var versions = TreeBuilder.Arr(type?["versions"]).Where(v => v["image"] is JsonObject).ToList();
        var v = versions.FirstOrDefault(x => x["isDefault"]?.GetValueKind() == System.Text.Json.JsonValueKind.True)
            ?? versions.LastOrDefault();
        return v?["image"];
    }

    /// <summary>Project or library graphic as image source: bitmaps (png, jpg, bmp, gif, tif) directly, svg via
    /// SvgRenderer, emf and wmf through GDI (MetafileImage).</summary>
    internal static ImageSource? Graphic(string? name)
    {
        if (name == null || !_graphics.TryGetValue(name, out var g)) return null;
        if (ImageCache.TryGetValue(name, out var cached)) return cached;
        var img = Image(g);
        ImageCache[name] = img;
        return img;
    }

    /// <summary>Image of a parser graphic object (extension, data = the file as base64).</summary>
    internal static ImageSource? Image(JsonNode g)
    {
        var ext = (TreeBuilder.Str(g["extension"]) ?? "").ToLowerInvariant();
        if (TreeBuilder.Str(g["data"]) is { } b64 && ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff")
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = new MemoryStream(Convert.FromBase64String(b64));
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception) { return null; }
        }
        if (TreeBuilder.Str(g["data"]) is { } mf64 && ext is ".emf" or ".wmf")
        {
            try { return MetafileImage.Load(Convert.FromBase64String(mf64)); }
            catch (Exception) { return null; }
        }
        if (TreeBuilder.Str(g["data"]) is { } svg64 && ext == ".svg")
        {
            try { return SvgRenderer.Load(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(svg64)).TrimStart('﻿')); }
            catch (Exception) { return null; }
        }
        return null;
    }

    /// <summary>Preview tab of a graphic (project graphic, library image type or version): the image at its size,
    /// scaled down to the available space; a note when it cannot be drawn (unknown format or unreadable file).</summary>
    public static UIElement GraphicPreview(JsonNode g)
    {
        var img = Image(g);
        if (img == null)
            return new TextBlock
            {
                Text = $"Graphic {TreeBuilder.Str(g["extension"]) ?? "without file type"} cannot be drawn.",
                Margin = new Thickness(12),
            };
        var image = new Image { Source = img, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = image,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Indexes of the layers hidden in the TIA screen editor ("visible in ES" off): on a screen the parser layer
    /// objects with esVisible false, in faceplate content the names in esHiddenLayers (index = position in layers).</summary>
    private static HashSet<int> EsHiddenLayers(JsonObject screen)
    {
        var hidden = new HashSet<int>();
        var names = TreeBuilder.Arr(screen["esHiddenLayers"]).Select(TreeBuilder.Str).ToHashSet();
        var i = 0;
        foreach (var l in TreeBuilder.Arr(screen["layers"]))
        {
            if (l is JsonObject o ? o["esVisible"]?.GetValueKind() == System.Text.Json.JsonValueKind.False
                    : names.Contains(TreeBuilder.Str(l)))
                hidden.Add(l is JsonObject lo ? (int)(Num(lo["index"]) ?? i) : i);
            i++;
        }
        return hidden;
    }

    /// <summary>The screen as a canvas of exactly its size (objects outside the screen are clipped as in the runtime).</summary>
    public static Canvas Render(JsonObject screen, IEnumerable<JsonNode> items)
    {
        if (ClassicScreenRenderer.IsClassic(screen)) return ClassicScreenRenderer.Render(screen, items);
        // faceplate content keeps the style of the screen it is drawn on
        if (Styles.TryGetValue(screen, out var st)) _style = st;
        else if (_faceplateDepth == 0) _style = null;
        if (_faceplateDepth == 0) _screenName = TreeBuilder.Str(screen["name"]);
        var sd = Defaults.Value["screen"]?.DeepClone() as JsonObject ?? new JsonObject();
        if (StyleValues("HmiScreen") is { } ss) Merge(sd, ss);
        var w = Num(screen["width"]) ?? Num(sd?["Width"]) ?? 800;
        var h = Num(screen["height"]) ?? Num(sd?["Height"]) ?? 480;
        var canvas = new Canvas
        {
            Width = w,
            Height = h,
            Background = Brush(TreeBuilder.Str(screen["backColor"]) ?? TreeBuilder.Str(sd?["BackColor"])) ?? Brushes.White,
            ClipToBounds = true,
            SnapsToDevicePixels = true,
        };
        // group members (parser parent) have Left/Top relative to their group, groups can be nested
        var list = items.ToList();
        var byName = list.Where(i => TreeBuilder.Str(i["name"]) != null).GroupBy(i => TreeBuilder.Str(i["name"])!)
            .ToDictionary(g => g.Key, g => g.First());
        (double, double) Offset(JsonNode it, int depth = 0)
        {
            if (depth > 16 || TreeBuilder.Str(it["parent"]) is not { } pn || !byName.TryGetValue(pn, out var g)) return (0, 0);
            var gp = Props(g);
            var (ox, oy) = Offset(g, depth + 1);
            return (ox + (Num(gp["Left"]) ?? 0), oy + (Num(gp["Top"]) ?? 0));
        }
        var esHidden = RuntimeVisibility ? new HashSet<int>() : EsHiddenLayers(screen);
        // drawing order: layer (0 at the bottom), then the order of the items in the screen (parser layerIndex/zIndex)
        foreach (var it in list.OrderBy(i => Num(i["layerIndex"]) ?? 0).ThenBy(i => Num(i["zIndex"]) ?? 0))
        {
            if (esHidden.Contains((int)(Num(it["layerIndex"]) ?? 0))) continue;
            FrameworkElement? el;
            try { el = Item(it); }
            catch (Exception e)
            {
                // one item that cannot be drawn must not end the screen: placeholder with the error
                var p = Props(it);
                el = Placeholder(p, Num(p["Width"]) ?? 60, Num(p["Height"]) ?? 24, $"{TreeBuilder.Str(it["name"])}: {e.GetType().Name}");
                el.ToolTip = e.Message;
                Canvas.SetLeft(el, Num(p["Left"]) ?? 0);
                Canvas.SetTop(el, Num(p["Top"]) ?? 0);
            }
            if (el != null && TreeBuilder.Str(it["parent"]) != null)
            {
                var (ox, oy) = Offset(it);
                Canvas.SetLeft(el, Canvas.GetLeft(el) + ox);
                Canvas.SetTop(el, Canvas.GetTop(el) + oy);
            }
            if (el != null) canvas.Children.Add(el);
        }
        return canvas;
    }

    /// <summary>Preview tab: the screen in a frame, scaled to the available space, with a button that saves the screen
    /// at 1:1 as PNG (the same image as the command line option --screen-png).</summary>
    public static UIElement Preview(JsonObject screen, IEnumerable<JsonNode> items)
    {
        var list = items.ToList();
        var frame = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = Render(screen, list),
            Margin = new Thickness(12),
        };
        // display mode of this view: like the TIA screen editor (hidden objects drawn) or like the runtime
        var runtime = new CheckBox
        {
            Content = "Like runtime (respect Visible)",
            IsChecked = RuntimeVisibility,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            ToolTip = "Off: like the TIA screen editor, objects with Visible=false are drawn. On: like the runtime, they are missing.",
        };
        runtime.Click += (_, _) =>
        {
            RuntimeVisibility = runtime.IsChecked == true;
            RuntimeVisibilityChanged?.Invoke();
        };
        var export = new Button
        {
            Content = "Export PNG…",
            ToolTip = "Save the screen at its original size (1 screen pixel = 1 image pixel) as PNG",
            Padding = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
        };
        export.Click += (_, _) => ExportPng(screen, list);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 8, 12, 0) };
        tools.Children.Add(runtime);
        tools.Children.Add(export);
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(tools, Dock.Top);
        panel.Children.Add(tools);
        panel.Children.Add(new Viewbox { Child = frame, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both });
        return panel;
    }

    /// <summary>Asks for a file name and saves the screen at 1:1 as PNG.</summary>
    private static void ExportPng(JsonObject screen, IReadOnlyList<JsonNode> items)
    {
        var name = TreeBuilder.Str(screen["name"]) ?? "screen";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export screen as PNG",
            FileName = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)) + ".png",
            DefaultExt = ".png",
            Filter = "PNG image (*.png)|*.png",
            AddExtension = true,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            SavePng(ToBitmap(Render(screen, items)), dlg.FileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"The PNG could not be saved: {e.Message}", "Export PNG", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Style values of an item type (StyleItemClass) in the selected style of the screen being drawn.</summary>
    private static JsonObject? StyleValues(string? cls) =>
        cls == null ? null : _style?["values"]?[cls] as JsonObject;

    /// <summary>Effective properties of an item like Openness shows them: the object defaults of its type (without
    /// style), overwritten by the values of the selected style for its StyleItemClass, overwritten by the stored
    /// properties (nested objects such as Font, Padding, Corners merged member by member; docs\spec\hmi-styles.md).</summary>
    internal static JsonObject Props(JsonNode it)
    {
        var type = TreeBuilder.Str(it["type"]) ?? "";
        var res = (Defaults.Value["items"]?[type] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        var cls = TreeBuilder.Str(it["properties"]?["StyleItemClass"]) ?? TreeBuilder.Str(res["StyleItemClass"]) ?? type;
        if (StyleValues(cls) is { } sv) Merge(res, sv);
        if (it["properties"] is JsonObject stored) Merge(res, stored);
        foreach (var (k, n) in new[] { ("Left", "left"), ("Top", "top"), ("Width", "width"), ("Height", "height") })
            if (res[k] == null && it[n] is JsonValue v) res[k] = v.DeepClone();
        res["Left"] ??= 0;
        res["Top"] ??= 0;
        return res;
    }

    private static void Merge(JsonObject into, JsonObject from)
    {
        foreach (var (k, v) in from)
            if (v is JsonObject o && into[k] is JsonObject io && k != "Text") Merge(io, o);
            else into[k] = v?.DeepClone();
    }

    /// <summary>Visible of an item with a script dynamization, evaluated as the runtime without PLC connection does it:
    /// tags read their default, Faceplate.Properties the instance value or else the type default (ScriptEval); null
    /// without such a script or when the script uses more than ScriptEval knows (e.g. a faceplate type that hides itself
    /// with Tags("Visible").Read()).</summary>
    private static bool? ScriptVisible(JsonNode it)
    {
        var code = TreeBuilder.Arr(it["dynamizations"])
            .FirstOrDefault(d => TreeBuilder.Str(d["property"]) == "Visible" && TreeBuilder.Str(d["type"]) == "Script")?["code"];
        if (TreeBuilder.Str(code) is not { Length: > 0 } src) return null;
        object? Prop(string name)
        {
            JsonNode? v = null;
            if (_outerValues?.TryGetValue(name, out var iv) == true) v = iv;
            else if (_outerDefaults?.TryGetValue(name, out var dv) == true) v = dv;
            return v is JsonObject ? Text(v) : TemplateExpr.FromJson(v);
        }
        var r = ScriptEval.Evaluate(src, Prop);
        return ReferenceEquals(r, ScriptEval.Unsupported) ? null : ScriptEval.Truthy(r);
    }

    /// <summary>Visible of an item with a tag dynamization, the tag at its default 0 (runtime without PLC connection):
    /// a direct link gives false, a Range mapping the value of the range holding 0 (none: null, the static value
    /// stays); Singlebit and other mappings null.</summary>
    private static bool? TagVisible(JsonNode it)
    {
        var d = TreeBuilder.Arr(it["dynamizations"])
            .FirstOrDefault(x => TreeBuilder.Str(x["property"]) == "Visible" && TreeBuilder.Str(x["type"]) == "Tag");
        if (d == null) return null;
        var kind = TreeBuilder.Str(d["mapping"]?["conditionType"]);
        if (kind is null or "None") return false;
        if (kind != "Range") return null;
        // entries without from/to carry bit masks (relevant/condition) and
        // are not evaluated
        foreach (var e in TreeBuilder.Arr(d["mapping"]?["entries"]))
            if (Num(e["from"]) is { } from && Num(e["to"]) is { } to && from <= 0 && 0 <= to)
                return ScriptEval.Truthy(TemplateExpr.FromJson(e["value"]));
        return null;
    }

    private static FrameworkElement? Item(JsonNode it)
    {
        var p = Props(it);
        var type = TreeBuilder.Str(it["type"]) ?? "";
        // Visible=false: the TIA screen editor draws the item as usual (164 Screen_R8 ScrWin_V, 2026-11-01), the runtime
        // not (RuntimeVisibility, used for the runtime comparison). A touch area or an alarm indicator without alarms
        // shows nothing in the runtime (164 against 165, 2026-10-31)
        if (RuntimeVisibility && (ScriptVisible(it) ?? TagVisible(it)) is { } sv)
        {
            if (!sv) return null;
        }
        else if (RuntimeVisibility && p["Visible"] is JsonValue vis && vis.TryGetValue<bool>(out var visible) && !visible) return null;
        if (type is "HmiTouchArea" or "HmiAlarmIndicator") return null;
        // a Text from a text list selected by a tag: the TIA screen editor shows the first entry (a default entry first),
        // as for symbolic I/O fields
        if (!RuntimeVisibility && TreeBuilder.Arr(it["dynamizations"]).FirstOrDefault(d => TreeBuilder.Str(d["property"]) == "Text"
                && TreeBuilder.Str(d["type"]) == "ResourceList") is { } rld
            && TreeBuilder.Str(rld["resourceList"]) is { } rln && _textLists.TryGetValue(rln, out var rlist))
        {
            var entries = TreeBuilder.Arr(rlist["entries"]).ToList();
            var e = entries.FirstOrDefault(x => x["default"]?.GetValueKind() == System.Text.Json.JsonValueKind.True) ?? entries.FirstOrDefault();
            if (e?["text"] is JsonNode et) p["Text"] = et.DeepClone();
        }
        // a text with output fields: the TIA screen editor shows each field as '0' repeated to its length ('00: 000…'
        // for Decimal length 2 and Text length 50); the runtime view keeps the fixed part
        if (!RuntimeVisibility && p["TextFields"] is JsonObject tf)
        {
            var lengths = TreeBuilder.Arr(tf["fields"]).ToDictionary(f => (int)(Num(f["field"]) ?? 0), f => (int)(Num(f["length"]) ?? 1));
            var composed = new JsonObject();
            foreach (var (culture, parts) in tf["cultures"] as JsonObject ?? new JsonObject())
                composed[culture] = string.Concat(TreeBuilder.Arr(parts).Select(x => x is JsonObject fo
                    ? new string('0', lengths.GetValueOrDefault((int)(Num(fo["field"]) ?? 0), 1)) : TreeBuilder.Str(x)));
            p["Text"] = composed;
        }
        // a ProcessValue bound to a tag or script has no value without the runtime (the parser leaves out the stored
        // link object; a static text the item may still carry is not shown either, as in the runtime)
        if (TreeBuilder.Arr(it["dynamizations"]).Any(d => TreeBuilder.Str(d["property"]) == "ProcessValue"))
        {
            p.Remove("ProcessValue");
            // the TIA screen editor shows a numeric field bound to a tag with 0 in its format (e.g. "0 %");
            // the runtime view keeps it empty
            if (!RuntimeVisibility && type == "HmiIOField" && !(TreeBuilder.Str(p["OutputFormat"]) ?? "{S}").Contains("{S}"))
                p["ProcessValue"] = 0;
            // a symbolic I/O field bound to a tag shows in the TIA screen editor the first entry of its text list;
            // a default entry would come first
            if (!RuntimeVisibility && type == "HmiSymbolicIOField" && TreeBuilder.Str(p["ResourceList"]) is { } rl
                && _textLists.TryGetValue(rl, out var list))
            {
                var entries = TreeBuilder.Arr(list["entries"]).ToList();
                var e = entries.FirstOrDefault(x => x["default"]?.GetValueKind() == System.Text.Json.JsonValueKind.True) ?? entries.FirstOrDefault();
                if (e?["text"] is JsonNode t) p["Text"] = t.DeepClone();
            }
        }
        // the alarm line draws its empty line in AlarmLineView.BackColor (Flat styles), white when the style has none
        // (Extended), with a frame, whatever BackColor says (runtime 164/166/167, 2026-10-31)
        if (type == "HmiAlarmLineControl") p["BackColor"] = TreeBuilder.Str((p["AlarmLineView"] as JsonObject)?["BackColor"]) ?? "0xFFFFFFFF";
        double left = Num(p["Left"]) ?? 0, top = Num(p["Top"]) ?? 0;
        double w = Math.Max(0, Num(p["Width"]) ?? 0), h = Math.Max(0, Num(p["Height"]) ?? 0);
        // circles, ellipses, arcs and segments have no Left/Top/Width/Height: CenterX/CenterY (screen coordinates) and
        // Radius or RadiusX/RadiusY span the bounding box
        if (Num(p["CenterX"]) is { } ccx && Num(p["CenterY"]) is { } ccy)
        {
            var rx = Num(p["RadiusX"]) ?? Num(p["Radius"]) ?? 0;
            var ry = Num(p["RadiusY"]) ?? Num(p["Radius"]) ?? 0;
            (left, top, w, h) = (ccx - rx, ccy - ry, 2 * rx, 2 * ry);
        }
        // operating objects with a rendering template of the selected style are drawn from it (prototype)
        var tel = TemplateRenderer.Types.Contains(type) ? TemplateRenderer.Render(p, _style, w, h) : null;
        FrameworkElement el = tel ?? type switch
        {
            "HmiRectangle" => Rectangle(p, w, h),
            "HmiText" => TextItem(p, w, h, null),
            "HmiTextBox" => TextItem(p, w, h, p),
            "HmiLine" => Line(p, w, h),
            "HmiCircle" or "HmiEllipse" => Ellipse(p, w, h),
            "HmiCircularArc" or "HmiEllipticalArc" => Arc(p, w, h, false),
            "HmiCircleSegment" or "HmiEllipseSegment" => Arc(p, w, h, true),
            "HmiPolyline" => Poly(p, w, h, false),
            "HmiPolygon" => Poly(p, w, h, true),
            "HmiIOField" => TextItem(WithText(p, ValueText(p)), w, h, p),
            "HmiButton" => TextItem(FromContent(p), w, h, p),
            "HmiSymbolicIOField" => DropDown(FromContent(p), w, h),
            "HmiToggleSwitch" => ToggleSwitch(p, w, h),
            "HmiGraphicView" => GraphicView(p, w, h, TreeBuilder.Str(it["name"]) ?? "",
                TreeBuilder.Arr(it["dynamizations"]).Any(d => TreeBuilder.Str(d["property"]) == "Graphic")),
            "HmiListBox" => SelectionList(p, w, h, null),
            "HmiRadioButtonGroup" => SelectionList(p, w, h, true),
            "HmiCheckBoxGroup" => SelectionList(p, w, h, false),
            // a group draws nothing itself; its members follow it in the item list
            "HmiGroup" => new Canvas { Width = w, Height = h },
            "HmiFaceplateContainer" when Faceplate(it, w, h) is { } fp => fp,
            _ when p["WindowFlags"] != null => ControlWindow(p, w, h, type),
            // alarm line: an empty line in the runtime (white, frame), no name label
            "HmiAlarmLineControl" => new Border
            {
                Width = w, Height = h, Background = Brush(TreeBuilder.Str(p["BackColor"])) ?? Brushes.White,
                BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])) ?? Brushes.Black,
                BorderThickness = new Thickness(Math.Max(1, Num(p["BorderWidth"]) ?? 1)),
            },
            // custom widget: only the widget package the file names (its content is not decoded)
            "HmiCustomWidgetContainer" => Placeholder(p, w, h, TreeBuilder.Str(p["Widget"]) ?? TreeBuilder.Str(it["name"]) ?? ""),
            _ => Placeholder(p, w, h, TreeBuilder.Str(it["name"]) ?? ""),
        };
        el.ToolTip = $"{TreeBuilder.Str(it["name"])}\n{type}\nLeft {left}, Top {top}, {w} × {h}";
        if (Num(p["Opacity"]) is { } op && op < 1) el.Opacity = op;
        if (Num(p["RotationAngle"]) is { } angle && angle != 0)
        {
            // RotationCenterPlacement (help ObjectModelWCCU): AbsoluteFromCenter = item centre + RotationCenterX/Y in
            // pixels, NormedFromCenter = centre + X/Y × half the size (±1 = frame), AbsoluteToContainer = screen coordinates
            double rcx = Num(p["RotationCenterX"]) ?? 0, rcy = Num(p["RotationCenterY"]) ?? 0;
            var (cx, cy) = TreeBuilder.Str(p["RotationCenterPlacement"]) switch
            {
                "NormedFromCenter" => (w / 2 * (1 + rcx), h / 2 * (1 + rcy)),
                "AbsoluteToContainer" => (rcx - left, rcy - top),
                _ => (w / 2 + rcx, h / 2 + rcy),
            };
            el.RenderTransform = new RotateTransform(angle, cx, cy);
        }
        Canvas.SetLeft(el, left);
        Canvas.SetTop(el, top);
        return el;
    }

    // ---------- item types (R1) ----------

    private static FrameworkElement Rectangle(JsonObject p, double w, double h)
    {
        var c = p["Corners"] as JsonObject;
        double tl = Num(c?["TopLeftRadius"]) ?? 0, tr = Num(c?["TopRightRadius"]) ?? 0,
            br = Num(c?["BottomRightRadius"]) ?? 0, bl = Num(c?["BottomLeftRadius"]) ?? 0;
        // like the runtime (164 against 165, 2026-10-31): the frame lies centred on the outline, anti-aliased
        if (tl == tr && tr == br && br == bl)
            return CenteredStroke(new RectangleGeometry(new Rect(0, 0, w, h), tl, tl), p, w, h);
        return new Border
        {
            Width = w,
            Height = h,
            Background = Brush(TreeBuilder.Str(p["BackColor"])),
            BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])),
            BorderThickness = new Thickness(Num(p["BorderWidth"]) ?? 0),
            CornerRadius = new CornerRadius(Num(c?["TopLeftRadius"]) ?? 0, Num(c?["TopRightRadius"]) ?? 0,
                Num(c?["BottomRightRadius"]) ?? 0, Num(c?["BottomLeftRadius"]) ?? 0),
        };
    }

    /// <summary>HmiText (no frame) and HmiTextBox (frame, background, padding, trimming, wrapping).</summary>
    private static FrameworkElement TextItem(JsonObject p, double w, double h, JsonObject? box)
    {
        var tb = new TextBlock
        {
            Text = Text(p["Text"]),
            Foreground = Brush(TreeBuilder.Str(p["ForeColor"])) ?? Brushes.Black,
            HorizontalAlignment = TreeBuilder.Str(p["HorizontalTextAlignment"]) switch
            {
                "Left" => HorizontalAlignment.Left, "Right" => HorizontalAlignment.Right, _ => HorizontalAlignment.Center,
            },
            VerticalAlignment = TreeBuilder.Str(p["VerticalTextAlignment"]) switch
            {
                "Top" => VerticalAlignment.Top, "Bottom" => VerticalAlignment.Bottom, _ => VerticalAlignment.Center,
            },
            TextAlignment = TreeBuilder.Str(p["HorizontalTextAlignment"]) switch
            {
                "Left" => TextAlignment.Left, "Right" => TextAlignment.Right, _ => TextAlignment.Center,
            },
            TextWrapping = TreeBuilder.Str(p["TextWrapping"]) is "Wrap" or "WordWrap" ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = TreeBuilder.Str(p["TextTrimming"]) switch
            {
                "CharacterEllipsis" => TextTrimming.CharacterEllipsis, "WordEllipsis" => TextTrimming.WordEllipsis,
                _ => TextTrimming.None,
            },
        };
        ApplyFont(tb, p["Font"] as JsonObject);
        var pad = p["Padding"] as JsonObject;
        return new Border
        {
            Width = w,
            Height = h,
            ClipToBounds = true,
            Background = box != null ? Brush(TreeBuilder.Str(p["BackColor"])) : null,
            BorderBrush = box != null ? Brush(TreeBuilder.Str(p["BorderColor"])) : null,
            BorderThickness = new Thickness(box != null ? Num(p["BorderWidth"]) ?? 0 : 0),
            Padding = new Thickness(Num(pad?["Left"]) ?? 0, Num(pad?["Top"]) ?? 0, Num(pad?["Right"]) ?? 0, Num(pad?["Bottom"]) ?? 0),
            Child = tb,
        };
    }

    /// <summary>HmiLine: X1/Y1/X2/Y2 relative to Left/Top of the item.</summary>
    private static FrameworkElement Line(JsonObject p, double w, double h)
    {
        var cap = TreeBuilder.Str(p["CapType"]) switch
        {
            "Flat" => PenLineCap.Flat, "Square" => PenLineCap.Square, _ => PenLineCap.Round,
        };
        var line = new System.Windows.Shapes.Line
        {
            X1 = Num(p["X1"]) ?? 0,
            Y1 = Num(p["Y1"]) ?? 0,
            X2 = Num(p["X2"]) ?? w,
            Y2 = Num(p["Y2"]) ?? h,
            Stroke = Brush(TreeBuilder.Str(p["LineColor"])) ?? Brushes.Black,
            StrokeThickness = Num(p["LineWidth"]) ?? 1,
            StrokeStartLineCap = cap,
            StrokeEndLineCap = cap,
            StrokeDashArray = Dash(TreeBuilder.Str(p["DashType"])),
        };
        var c = new Canvas { Width = w, Height = h, Children = { line } };
        if (LineEnd(TreeBuilder.Str(p["StartType"]), new Point(line.X1, line.Y1), new Point(line.X2, line.Y2), line) is { } s) c.Children.Add(s);
        if (LineEnd(TreeBuilder.Str(p["EndType"]), new Point(line.X2, line.Y2), new Point(line.X1, line.Y1), line) is { } e) c.Children.Add(e);
        return c;
    }

    /// <summary>Line end (HmiLineEndType) at <paramref name="at"/>, the line coming from <paramref name="from"/>.
    /// Arrow as in the TIA screen editor: filled, tip on the end point, 2 × LineWidth long, 3 × LineWidth wide (real
    /// project, LineWidth 4, 2026-11-01); the other kinds in the same size, not yet compared.</summary>
    private static System.Windows.Shapes.Shape? LineEnd(string? kind, Point at, Point from, System.Windows.Shapes.Line line)
    {
        if (kind is null or "Line") return null;
        var d = at - from;
        if (d.Length < 0.001) return null;
        d.Normalize();
        var n = new Vector(-d.Y, d.X);
        double lw = Math.Max(1, line.StrokeThickness), len = 2 * lw, half = 1.5 * lw;
        bool filled = kind is "Arrow" or "ReversedArrow" or "Circle";
        if (kind is "Circle" or "EmptyCircle")
        {
            var el = new System.Windows.Shapes.Ellipse { Width = 2 * half, Height = 2 * half, Stroke = line.Stroke, StrokeThickness = filled ? 0 : 1, Fill = filled ? line.Stroke : Brushes.White };
            Canvas.SetLeft(el, at.X - half);
            Canvas.SetTop(el, at.Y - half);
            return el;
        }
        var (tip, baseC) = kind == "ReversedArrow" ? (at - d * len, at) : (at, at - d * len);
        return new System.Windows.Shapes.Polygon
        {
            Points = [tip, baseC + n * half, baseC - n * half],
            Fill = filled ? line.Stroke : Brushes.White,
            Stroke = filled ? null : line.Stroke,
            StrokeThickness = filled ? 0 : 1,
        };
    }

    // ---------- input / output (R2) ----------

    /// <summary>I/O field: the static ProcessValue as stored (editor view); without one "0" as the editor shows it.</summary>
    private static string ValueText(JsonObject p) => p["ProcessValue"] switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v => v.ToJsonString(),
        _ => "0",
    };

    private static JsonObject WithText(JsonObject p, string text)
    {
        var q = (JsonObject)p.DeepClone();
        q["Text"] = text;
        return q;
    }

    /// <summary>Button, symbolic I/O field, toggle switch keep text alignment and trimming in Content.</summary>
    private static JsonObject FromContent(JsonObject p)
    {
        var q = (JsonObject)p.DeepClone();
        if (q["Content"] is JsonObject c)
            foreach (var k in new[] { "HorizontalTextAlignment", "VerticalTextAlignment", "TextTrimming" })
                if (c[k] != null) q[k] = c[k]!.DeepClone();
        return q;
    }

    /// <summary>Symbolic I/O field: text field with the drop-down button at the right edge. The style template draws
    /// it with a runtime combo box element the template interpreter does not know; the button follows the runtime
    /// (164, 2026-10-31): 30 px wide inside the frame, upper half a gradient from BackColor darker by 0.1 to darker by
    /// 1/3, lower half BackColor darker by 0.4, black arrow in the middle.</summary>
    private static FrameworkElement DropDown(JsonObject p, double w, double h)
    {
        var box = TextItem(p, w, h, p);
        // output field (IOFieldType Output): no drop-down button (templates: TIFID_ComboboxButton only for InputOutput)
        if (TreeBuilder.Str(p["IOFieldType"]) == "Output") return box;
        var bw = Num(p["BorderWidth"]) ?? 1;
        var back = Brush(TreeBuilder.Str(p["BackColor"])) is SolidColorBrush sb ? sb.Color : Colors.White;
        Color Darker(double f) => Color.FromRgb((byte)Math.Round(back.R * (1 - f)), (byte)Math.Round(back.G * (1 - f)), (byte)Math.Round(back.B * (1 - f)));
        var bh = Math.Max(0, h - 2 * bw);
        var button = new Grid
        {
            Width = Math.Min(30, Math.Max(0, w - 2 * bw)),
            Height = bh,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, bw, bw, 0),
            Background = new LinearGradientBrush(new GradientStopCollection
            {
                new(Darker(0.1), 0), new(Darker(1.0 / 3), 0.5), new(Darker(0.4), 0.5), new(Darker(0.4), 1),
            }, 90),
        };
        button.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 0,0 L 8,0 L 4,4 Z"),
            Fill = Brush(TreeBuilder.Str(p["ForeColor"])) ?? Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return new Grid { Width = w, Height = h, Children = { box, button } };
    }

    /// <summary>Toggle switch in its basic state (IsAlternateState false): rounded track, knob on the left.</summary>
    private static FrameworkElement ToggleSwitch(JsonObject p, double w, double h)
    {
        var bw = Num(p["BorderWidth"]) ?? 1;
        var on = p["IsAlternateState"] is JsonValue a && a.TryGetValue<bool>(out var alt) && alt;
        var track = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(h / 2),
            Background = Brush(TreeBuilder.Str(on ? p["AlternateBackColor"] : p["BackColor"])),
            BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])),
            BorderThickness = new Thickness(bw),
        };
        var d = Math.Max(0, h - 4 * bw - 4);
        var knob = new System.Windows.Shapes.Ellipse
        {
            Width = d,
            Height = d,
            Fill = Brush(TreeBuilder.Str(p["ForeColor"])) ?? Brushes.Black,
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2 * bw + 2),
        };
        var g = new Grid { Width = w, Height = h, Children = { track, knob } };
        var text = Text(p["Text"]);
        if (text.Length > 0) g.Children.Add(TextItem(FromContent(p), w, h, null));
        return g;
    }

    /// <summary>List box (radio null), option group (true) or check box group (false) with its SelectionItems, nothing
    /// selected. Layout from the runtime (164, 2026-10-31, defaults): list rows 2 × font size high, every second row in
    /// AlternateBackColor, text without indent; group rows 3 × font size high inside Padding, the selector (circle 22,
    /// box 28) centred in a square cell of row height, text after the cell; selector with the vertical gradient
    /// BackColor → BackColor darker by 0.4 and a frame in BorderColor; group frame with rounded corners.</summary>
    private static FrameworkElement SelectionList(JsonObject p, double w, double h, bool? radio)
    {
        var bw = Num(p["BorderWidth"]) ?? 1;
        var pad = p["Padding"] as JsonObject;
        double pl = Num(pad?["Left"]) ?? 0, pt = Num(pad?["Top"]) ?? 0;
        var font = Num((p["Font"] as JsonObject)?["Size"]) ?? 14;
        var rowH = Num(p["SelectionItemHeight"]) is { } ih && ih > 0 ? ih : (radio == null ? 2 : 3) * font;
        var back = Brush(TreeBuilder.Str(p["BackColor"])) is SolidColorBrush sb ? sb.Color : Colors.White;
        var dark = Color.FromRgb((byte)Math.Round(back.R * 0.6), (byte)Math.Round(back.G * 0.6), (byte)Math.Round(back.B * 0.6));
        var flat = IsFlatStyle;
        var rows = new Canvas { ClipToBounds = true };
        var items = TreeBuilder.Arr(p["SelectionItems"]).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var y = pt + i * rowH;
            if (radio == null && i % 2 == 1 && Brush(TreeBuilder.Str(p["AlternateBackColor"])) is { } alt)
            {
                var band = new System.Windows.Shapes.Rectangle { Width = Math.Max(0, w - 2 * bw), Height = rowH, Fill = alt };
                Canvas.SetTop(band, y);
                rows.Children.Add(band);
            }
            var textX = pl;
            if (radio != null)
            {
                // Flat styles (runtime 166/167): plain selector, BackColor without gradient, square corners, frame 2 px
                var size = radio.Value ? 22.0 : flat ? 24.0 : 28.0;
                var sel = new System.Windows.Shapes.Path
                {
                    Data = radio.Value
                        ? new EllipseGeometry(new Point(pl + rowH / 2, y + rowH / 2), size / 2, size / 2)
                        : new RectangleGeometry(new Rect(pl + (rowH - size) / 2, y + (rowH - size) / 2, size, size), flat ? 0 : 3, flat ? 0 : 3),
                    Fill = flat ? new SolidColorBrush(back) : new LinearGradientBrush(back, dark, 90),
                    Stroke = Brush(TreeBuilder.Str(p["BorderColor"])),
                    StrokeThickness = flat ? 2 : 1,
                    SnapsToDevicePixels = false,
                };
                rows.Children.Add(sel);
                textX = pl + rowH;
            }
            var tb = new TextBlock
            {
                Text = Text(items[i]["Text"]),
                Foreground = Brush(TreeBuilder.Str(p["ForeColor"])) ?? Brushes.Black,
                Height = rowH,
            };
            ApplyFont(tb, p["Font"] as JsonObject);
            // vertical centre of the row (TextBlock has no vertical content alignment of its own)
            var cell = new Border { Height = rowH, Child = tb };
            tb.VerticalAlignment = VerticalAlignment.Center;
            tb.Height = double.NaN;
            Canvas.SetLeft(cell, textX);
            Canvas.SetTop(cell, y);
            rows.Children.Add(cell);
        }
        return new Border
        {
            Width = w,
            Height = h,
            Background = Brush(TreeBuilder.Str(p["BackColor"])),
            BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])),
            BorderThickness = new Thickness(bw),
            CornerRadius = new CornerRadius(radio == null ? 0 : 4),
            ClipToBounds = true,
            Child = rows,
        };
    }

    // ---------- controls with a window frame ----------

    /// <summary>Controls with WindowFlags (alarm, trend, web, media, diagnosis controls, screen window): window frame
    /// in CaptionColor, caption bar with maximise/close symbols, ToolBar and StatusBar band, BackColor in between; a
    /// table view (style values with RowHeight and HeaderSettings) as header row and alternating rows. Colours come
    /// from the properties and the style; the sizes are measured in the runtime (164, 2026-10-31): frame 2 px,
    /// caption 32 px including the frame, tool bar 70 px, status bar 38 px (upper half flat, lower half a gradient
    /// to darker by 0.2), table header 30 px. Media and web control show no status bar although StatusBar.Visible is
    /// true; only alarm control and trend companion show their table in the default state. The content of the
    /// controls (curves, buttons, columns) is not drawn.</summary>
    private static FrameworkElement ControlWindow(JsonObject p, double w, double h, string type)
    {
        var flags = TreeBuilder.Str(p["WindowFlags"]) ?? "";
        // the parser names the HmiWindowFlag bits; a number left over (unknown bit) keeps the default frame
        if (flags.Length > 0 && char.IsDigit(flags[0])) flags = "ShowCaption, ShowBorder, CanSize, CanMove, CanMaximize, CanClose";
        var frame = flags.Contains("ShowBorder") ? 2.0 : 0;
        var caption = flags.Contains("ShowCaption") ? 32.0 - frame : 0;
        var capColor = Brush(TreeBuilder.Str(p["CaptionColor"])) ?? Brushes.Gray;
        bool Shown(string part) => p[part] is JsonObject o && !(o["Visible"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b);
        double iw = Math.Max(0, w - 2 * frame), ih = Math.Max(0, h - 2 * frame);
        var c = new Canvas { Width = iw, Height = ih, ClipToBounds = true };
        void Band(double y, double bh, Brush? fill)
        {
            var r = new System.Windows.Shapes.Rectangle { Width = iw, Height = Math.Max(0, bh), Fill = fill };
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }
        if (caption > 0)
        {
            Band(0, caption, capColor);
            var sym = Brush(TreeBuilder.Str((p["Caption"] as JsonObject)?["ForeColor"])) ?? Brushes.Black;
            if (flags.Contains("CanMaximize"))
            {
                var max = new System.Windows.Shapes.Rectangle { Width = 11, Height = 8, Stroke = sym, StrokeThickness = 1 };
                Canvas.SetLeft(max, iw - 54.5);
                Canvas.SetTop(max, 11.5);
                c.Children.Add(max);
            }
            if (flags.Contains("CanClose"))
                c.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse($"M {Fmt(iw - 30)},10 L {Fmt(iw - 20)},20 M {Fmt(iw - 20)},10 L {Fmt(iw - 30)},20"),
                    Stroke = sym,
                    StrokeThickness = 1.3,
                });
        }
        var bottom = ih;
        if (type == "HmiProcessDiagnosisOverviewControl")
        {
            Band(caption, ih - caption, Brush(TreeBuilder.Str(p["BackColor"])) ?? Brushes.White);
            DiagnosisGrid(c, p, iw, caption);
            return new Border { Width = w, Height = h, BorderBrush = capColor, BorderThickness = new Thickness(frame), Child = c };
        }
        // a bar that does not fit below the caption is left out (small controls in F01); controls the runtime shows
        // without status bar (media, web, criteria analysis) or without tool bar (criteria analysis, graph overview;
        // 164 Screen_R8, 2026-10-31) although the bars are Visible
        if (Shown("StatusBar") && type is not ("HmiMediaControl" or "HmiWebControl" or "HmiProcessDiagnosisCriteriaAnalysisControl")
            && bottom - 38 >= caption)
        {
            var sc = Brush(TreeBuilder.Str((p["StatusBar"] as JsonObject)?["BackColor"])) is SolidColorBrush s ? s.Color : Colors.Gainsboro;
            var dark = Color.FromRgb((byte)Math.Round(sc.R * 0.8), (byte)Math.Round(sc.G * 0.8), (byte)Math.Round(sc.B * 0.8));
            bottom -= 38;
            Band(bottom, 38, new LinearGradientBrush(new GradientStopCollection { new(sc, 0), new(sc, 0.5), new(dark, 1) }, 90));
        }
        // web control in the Flat styles: tool bar at the top below the caption (runtime 166/167, 2026-10-31)
        if (Shown("ToolBar") && type == "HmiWebControl" && IsFlatStyle && bottom - 70 >= caption)
        {
            Band(caption, 70, Brush(TreeBuilder.Str((p["ToolBar"] as JsonObject)?["BackColor"])));
            ToolBarButtons(c, p, type, iw, caption);
            caption += 70;
        }
        else if (Shown("ToolBar") && bottom - 70 >= caption
            && type is not ("HmiProcessDiagnosisCriteriaAnalysisControl" or "HmiProcessDiagnosisGraphOverviewControl"))
        {
            bottom -= 70;
            Band(bottom, 70, Brush(TreeBuilder.Str((p["ToolBar"] as JsonObject)?["BackColor"])));
            ToolBarButtons(c, p, type, iw, bottom);
        }
        // PLC code viewer: symbol line (50 px, SymbolLineBackColor, 1 px frame darker by 0.1) above the tool bar
        if (type == "HmiProcessDiagnosisPlcCodeViewerControl" && p["ShowSymbolLine"]?.GetValueKind() != System.Text.Json.JsonValueKind.False
            && bottom - 50 >= caption)
        {
            bottom -= 50;
            var sl = Brush(TreeBuilder.Str(p["SymbolLineBackColor"])) is SolidColorBrush slb ? slb.Color : Colors.LightGray;
            var edge = new SolidColorBrush(Color.FromRgb((byte)Math.Round(sl.R * 0.9), (byte)Math.Round(sl.G * 0.9), (byte)Math.Round(sl.B * 0.9)));
            Band(bottom, 50, edge);
            Band(bottom + 1, 48, new SolidColorBrush(sl));
        }
        Band(caption, bottom - caption, Brush(TreeBuilder.Str(p["BackColor"])) ?? Brushes.White);
        // PLC code viewer: divider between overview and detail at OverviewDetailRatio (10 px, 7C7D85 measured)
        if (type == "HmiProcessDiagnosisPlcCodeViewerControl")
        {
            var div = new System.Windows.Shapes.Rectangle
            {
                Width = 10, Height = Math.Max(0, bottom - caption),
                Fill = new SolidColorBrush(Color.FromRgb(0x7C, 0x7D, 0x85)),
            };
            Canvas.SetLeft(div, iw * (Num(p["OverviewDetailRatio"]) ?? 0.5) - 5);
            Canvas.SetTop(div, caption);
            c.Children.Add(div);
        }
        if (type is "HmiTrendControl" or "HmiFunctionTrendControl")
            TrendContent(c, p, type, iw, caption, bottom);
        if (type == "HmiScreenWindow" && ScreenWindowContent(p, iw, Math.Max(0, bottom - caption)) is { } content)
        {
            Canvas.SetTop(content, caption);
            c.Children.Add(content);
        }
        if (type is "HmiAlarmControl" or "HmiTrendCompanion" or "HmiProcessDiagnosisCriteriaAnalysisControl" && TableView(p) is { } tv)
        {
            var rowH = Num(tv["RowHeight"]) ?? 28;
            var hs = tv["HeaderSettings"] as JsonObject;
            var line = Brush(TreeBuilder.Str(hs?["HeaderGridLineColor"]));
            // header lines as wide as GridLineWidth (alarm control 2, criteria analysis 1; runtime 164)
            var lw = Math.Max(1, Num(tv["GridLineWidth"]) ?? 2);
            Band(caption, lw, line);
            Band(caption + lw, rowH - lw, Brush(TreeBuilder.Str(hs?["HeaderBackColor"])));
            Band(caption + rowH, lw, line);
            var alt = Brush(TreeBuilder.Str(tv["AlternateBackColor"]));
            // rows alternate between the view's BackColor (white in Extended, dark in FlatStyle_Dark) and AlternateBackColor
            var rowBack = Brush(TreeBuilder.Str(tv["BackColor"])) ?? Brushes.White;
            var i = 0;
            for (var y = caption + rowH + lw; y < bottom - lw; y += rowH, i++)
                Band(y, Math.Min(rowH, bottom - lw - y), i % 2 == 0 ? rowBack : alt);
            // RowHeaderType Index: a 52 px column (runtime) in HeaderBackColor with the row numbers
            if (TreeBuilder.Str(hs?["RowHeaderType"]) == "Index")
            {
                // small controls (F01): tool and status bar can leave no room for the table
                var head = new Canvas { Width = 54, Height = Math.Max(0, bottom - 2 - caption), ClipToBounds = true };
                head.Children.Add(new System.Windows.Shapes.Rectangle { Width = 52, Height = head.Height, Fill = Brush(TreeBuilder.Str(hs?["HeaderBackColor"])) });
                var sep = new System.Windows.Shapes.Rectangle { Width = 2, Height = head.Height, Fill = line };
                Canvas.SetLeft(sep, 52);
                head.Children.Add(sep);
                var n = 1;
                for (var y = rowH + 2; y < head.Height; y += rowH, n++)
                {
                    var tb = new TextBlock
                    {
                        Text = n.ToString(CultureInfo.InvariantCulture), Width = 52, TextAlignment = TextAlignment.Center,
                        Foreground = Brush(TreeBuilder.Str(hs?["HeaderForeColor"])) ?? Brushes.Black,
                    };
                    ApplyFont(tb, tv["Font"] as JsonObject);
                    var cell = new Border { Width = 52, Height = rowH, Child = tb };
                    tb.VerticalAlignment = VerticalAlignment.Center;
                    Canvas.SetTop(cell, y);
                    head.Children.Add(cell);
                }
                Canvas.SetTop(head, caption);
                c.Children.Add(head);
                Band(caption, 2, line);
                Band(caption + rowH, 2, line);
            }
            Band(bottom - 2, 2, line);
        }
        return new Border
        {
            Width = w,
            Height = h,
            BorderBrush = capColor,
            BorderThickness = new Thickness(frame),
            Child = c,
        };
    }

    /// <summary>Process diagnosis overview: no tool bar or status bar in the runtime although both are Visible. Two
    /// rows, labelled with PDiagCategories.Label and PDiagSupervisionTypes.Label, each with six symbol fields (the
    /// runtime's fixed categories E W I C4 C5 C6 and supervision types O I R A P M, not in Openness). Measured in
    /// the runtime (164, 2026-10-31): fields 43 × 40 at x 80 + 50.6·i, rows 14 and 62 px below the caption, label
    /// 4 px from the left, field colour E6E6EB, a 2 px LineColor line 63 px from the right edge.</summary>
    private static void DiagnosisGrid(Canvas c, JsonObject p, double iw, double top)
    {
        var rows = new[]
        {
            (p["PDiagCategories"] as JsonObject, new[] { "E", "W", "I", "C4", "C5", "C6" }),
            (p["PDiagSupervisionTypes"] as JsonObject, new[] { "O", "I", "R", "A", "P", "M" }),
        };
        for (var r = 0; r < rows.Length; r++)
        {
            var (part, symbols) = rows[r];
            if (part?["Visible"] is JsonValue v && v.TryGetValue<bool>(out var vis) && !vis) continue;
            var y = top + 14 + 48 * r;
            var label = new TextBlock { Text = Text(part?["Label"]?["Text"]), Foreground = Brush(TreeBuilder.Str(part?["Label"]?["ForeColor"])) ?? Brushes.Black };
            ApplyFont(label, p["Font"] as JsonObject);
            var lc = new Border { Height = 40, Child = label };
            label.VerticalAlignment = VerticalAlignment.Center;
            Canvas.SetLeft(lc, 4);
            Canvas.SetTop(lc, y);
            c.Children.Add(lc);
            for (var i = 0; i < symbols.Length; i++)
            {
                // field colours: PDiagElements AlternateBackColor/AlternateForeColor (E6E6EB in Extended, 3A4252 in Dark)
                var el = part?["PDiagElements"] as JsonObject;
                var tb = new TextBlock { Text = symbols[i], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brush(TreeBuilder.Str(el?["AlternateForeColor"])) ?? Brushes.Black };
                ApplyFont(tb, part?["SymbolFont"] as JsonObject);
                var cell = new Border { Width = 43, Height = 40, Child = tb,
                    Background = Brush(TreeBuilder.Str(el?["AlternateBackColor"])) ?? new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xEB)) };
                Canvas.SetLeft(cell, 80 + 50.6 * i);
                Canvas.SetTop(cell, y);
                c.Children.Add(cell);
            }
        }
        var line = new System.Windows.Shapes.Rectangle { Width = 2, Height = Math.Max(0, c.Height - top), Fill = Brush(TreeBuilder.Str(p["LineColor"])) };
        Canvas.SetLeft(line, iw - 63);
        Canvas.SetTop(line, top);
        c.Children.Add(line);
    }

    /// <summary>Default tool bar of a control type as the runtime shows it without configuration (164, 2026-10-31):
    /// number of buttons, and for the web control the address box. Openness lists no tool bar elements for them.</summary>
    private static readonly Dictionary<string, (int Buttons, double TextBox)> DefaultToolBars = new()
    {
        ["HmiTrendControl"] = (10, 0), ["HmiTrendCompanion"] = (2, 0), ["HmiAlarmControl"] = (14, 0),
        ["HmiFunctionTrendControl"] = (7, 0), ["HmiDetailedParameterControl"] = (10, 0),
        ["HmiSystemDiagnosisControl"] = (4, 0), ["HmiMediaControl"] = (6, 0), ["HmiWebControl"] = (2, 180),
        ["HmiProcessDiagnosisPlcCodeViewerControl"] = (6, 0),
    };

    /// <summary>Tool bar buttons without their symbols: size and margins from the style part HmiControlBarButtonPart
    /// (42 × 38, margin 4/16), first button 11 px from the left, 48 px apart (runtime); vertical gradient BackColor
    /// lighter by 0.5 → darker by 0.17, frame BorderColor at half opacity.</summary>
    private static void ToolBarButtons(Canvas c, JsonObject p, string type, double iw, double top)
    {
        if (!DefaultToolBars.TryGetValue(type, out var tb)) return;
        var part = (p["ToolBar"] as JsonObject)?["Elements"]?["HmiControlBarButtonPart"] as JsonObject;
        double bw = Num(part?["Width"]) ?? 42, bh = Num(part?["Height"]) ?? 38, mt = Num(part?["Margin"]?["Top"]) ?? 16;
        var back = Brush(TreeBuilder.Str(part?["BackColor"]) ?? "0xFFF2F4FF") is SolidColorBrush sb ? sb.Color : Colors.White;
        var border = Brush(TreeBuilder.Str(part?["BorderColor"]) ?? "0xFF64646A") is SolidColorBrush bb ? bb.Color : Colors.Gray;
        Color Mix(Color a, Color b, double f) => Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * f), (byte)Math.Round(a.G + (b.G - a.G) * f), (byte)Math.Round(a.B + (b.B - a.B) * f));
        // Flat styles (template include.style.flat.controlbarbutton): BackColor without gradient, frame BorderColor
        // (transparent in FlatStyle_Dark) of BorderWidth, margin all round
        var flat = IsFlatStyle;
        Brush fill = flat ? new SolidColorBrush(back) : new LinearGradientBrush(Mix(back, Colors.White, 0.5), Mix(back, Colors.Black, 0.17), 90);
        var stroke = flat ? new SolidColorBrush(border) : new SolidColorBrush(Color.FromArgb(128, border.R, border.G, border.B));
        var bwidth = flat ? Num(part?["BorderWidth"]) ?? 0 : 1;
        var x = flat ? Num(part?["Margin"]?["Left"]) ?? 11 : 11.0;
        var gap = flat ? (Num(part?["Margin"]?["Left"]) ?? 0) + (Num(part?["Margin"]?["Right"]) ?? 0) - 10 : 6;
        for (var i = 0; i < tb.Buttons && x + bw <= iw; i++, x += bw + gap)
        {
            // Flat template: a button that cannot be operated (no data at rest, as most in the runtime) has opacity 0.6
            var r = new Border { Width = bw, Height = bh, CornerRadius = new CornerRadius(flat ? 0 : 3), Background = fill, BorderBrush = stroke, BorderThickness = new Thickness(bwidth), Opacity = flat ? 0.6 : 1,
                Child = ToolBarIcon(type, i, Brush(TreeBuilder.Str(part?["ForeColor"])), Math.Min(bw, bh)) };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, top + mt);
            c.Children.Add(r);
        }
        // Flat: the address box fills the tool bar up to the right margin
        if (flat && tb.TextBox > 0) tb.TextBox = Math.Max(tb.TextBox, iw - x - (Num(part?["Margin"]?["Right"]) ?? 10));
        if (tb.TextBox > 0 && x + tb.TextBox <= iw)
        {
            // address box: style part HmiControlBarTextBoxPart (dark in FlatStyle_Dark), else white
            var tp = (p["ToolBar"] as JsonObject)?["Elements"]?["HmiControlBarTextBoxPart"] as JsonObject;
            var t = new Border { Width = tb.TextBox, Height = bh - 6, CornerRadius = new CornerRadius(flat ? 0 : 3),
                Background = Brush(TreeBuilder.Str(tp?["BackColor"])) ?? Brushes.White,
                BorderBrush = Brush(TreeBuilder.Str(tp?["BorderColor"])) ?? new SolidColorBrush(border), BorderThickness = new Thickness(1) };
            Canvas.SetLeft(t, x);
            Canvas.SetTop(t, top + mt + 3);
            c.Children.Add(t);
        }
    }

    /// <summary>The table view of a control: the first style/property object with RowHeight and HeaderSettings.</summary>
    private static JsonObject? TableView(JsonObject p) =>
        p.Select(kv => kv.Value).OfType<JsonObject>().FirstOrDefault(o => o["RowHeight"] != null && o["HeaderSettings"] != null);

    private static string Fmt(double v) => v.ToString(CultureInfo.InvariantCulture);

    // ---------- graphics (R3) ----------

    /// <summary>Graphic view: background, frame and the project graphic (Graphic) scaled by GraphicStretchMode.</summary>
    private static FrameworkElement GraphicView(JsonObject p, double w, double h, string name, bool graphicBound = false)
    {
        var gname = TreeBuilder.Str(p["Graphic"]);
        var img = Graphic(gname);
        UIElement? child = img != null
            ? new Image
            {
                Source = img,
                Stretch = TreeBuilder.Str(p["GraphicStretchMode"]) switch
                {
                    "Fill" => Stretch.Fill, "None" => Stretch.None, "UniformToFill" => Stretch.UniformToFill, _ => Stretch.Uniform,
                },
            }
            : gname != null
                ? new TextBlock { Text = gname, FontSize = 10, Foreground = Brushes.DimGray, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2) }
                : null;
        return new Border
        {
            Width = w,
            Height = h,
            ClipToBounds = true,
            // with a graphic the runtime leaves the background empty (WebRH DOM: rect fill=none, 2026-10-31); without
            // one it shows BackColor (164 Screen_1); a dynamized Graphic without a value (faceplate interface left
            // empty in the TIA screen editor) shows nothing
            Background = img != null || (graphicBound && gname == null) ? null : Brush(TreeBuilder.Str(p["BackColor"])),
            BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])),
            BorderThickness = new Thickness(Num(p["BorderWidth"]) ?? 0),
            Child = child,
        };
    }

    // ---------- geometry (R4) ----------

    private static FrameworkElement Ellipse(JsonObject p, double w, double h) =>
        CenteredStroke(new EllipseGeometry(new Point(w / 2, h / 2), w / 2, h / 2), p, w, h);

    /// <summary>Filled outline with BorderColor/BorderWidth centred on the outline (half outside, half inside) and
    /// anti-aliased, as the runtime draws rectangles and ellipses (WPF shapes and Border keep the frame inside).</summary>
    private static FrameworkElement CenteredStroke(Geometry g, JsonObject p, double w, double h) => new Canvas
    {
        // the path has no size of its own: with Width/Height set WPF would clip the outer half of the frame
        Width = w,
        Height = h,
        Children =
        {
            new System.Windows.Shapes.Path
            {
                Data = g,
                Fill = Brush(TreeBuilder.Str(p["BackColor"])),
                Stroke = Brush(TreeBuilder.Str(p["BorderColor"])),
                StrokeThickness = Num(p["BorderWidth"]) ?? 0,
                StrokeDashArray = Dash(TreeBuilder.Str(p["DashType"])),
                SnapsToDevicePixels = false,
            },
        },
    };

    /// <summary>Arcs (line only) and segments (pie: arc closed through the centre). StartAngle: 0° = 3 o'clock,
    /// AngleRange clockwise (help ObjectModelWCCU, CircleSegment.StartAngle / AngleRange); y grows downwards on the
    /// screen, so clockwise is the positive angle direction.</summary>
    private static FrameworkElement Arc(JsonObject p, double w, double h, bool segment)
    {
        double rx = w / 2, ry = h / 2, start = Num(p["StartAngle"]) ?? 0, range = Num(p["AngleRange"]) ?? 0;
        Point At(double deg) => new(rx + rx * Math.Cos(deg * Math.PI / 180), ry + ry * Math.Sin(deg * Math.PI / 180));
        var fig = new PathFigure { StartPoint = segment ? new Point(rx, ry) : At(start), IsClosed = segment, IsFilled = segment };
        if (segment) fig.Segments.Add(new LineSegment(At(start), true));
        if (Math.Abs(range) >= 360)
        {
            // full turn: two half arcs (a single ArcSegment with identical end points draws nothing)
            fig.Segments.Add(new ArcSegment(At(start + 180), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new ArcSegment(At(start + 360), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true));
        }
        else
            fig.Segments.Add(new ArcSegment(At(start + range), new Size(rx, ry), 0, Math.Abs(range) > 180,
                range >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
        var cap = TreeBuilder.Str(p["CapType"]) switch { "Flat" => PenLineCap.Flat, "Square" => PenLineCap.Square, _ => PenLineCap.Round };
        return new System.Windows.Shapes.Path
        {
            Width = w,
            Height = h,
            Data = new PathGeometry([fig]),
            Fill = segment ? Brush(TreeBuilder.Str(p["BackColor"])) : null,
            Stroke = Brush(TreeBuilder.Str(segment ? p["BorderColor"] : p["LineColor"])),
            StrokeThickness = Num(segment ? p["BorderWidth"] : p["LineWidth"]) ?? 1,
            StrokeStartLineCap = cap,
            StrokeEndLineCap = cap,
            StrokeDashArray = Dash(TreeBuilder.Str(p["DashType"])),
        };
    }

    /// <summary>HmiPolyline / HmiPolygon: Points (X/Y relative to Left/Top, from the parser).</summary>
    private static FrameworkElement Poly(JsonObject p, double w, double h, bool closed)
    {
        var pts = new PointCollection(TreeBuilder.Arr(p["Points"]).Select(q => new Point(Num(q["X"]) ?? 0, Num(q["Y"]) ?? 0)));
        var join = TreeBuilder.Str(p["JoinType"]) switch { "Miter" => PenLineJoin.Miter, "Bevel" => PenLineJoin.Bevel, _ => PenLineJoin.Round };
        System.Windows.Shapes.Shape s = closed
            ? new System.Windows.Shapes.Polygon
            {
                Points = pts,
                Fill = Brush(TreeBuilder.Str(p["BackColor"])),
                Stroke = Brush(TreeBuilder.Str(p["BorderColor"])),
                StrokeThickness = Num(p["BorderWidth"]) ?? 0,
            }
            : new System.Windows.Shapes.Polyline
            {
                Points = pts,
                Stroke = Brush(TreeBuilder.Str(p["LineColor"])),
                StrokeThickness = Num(p["LineWidth"]) ?? 1,
                StrokeStartLineCap = TreeBuilder.Str(p["CapType"]) == "Flat" ? PenLineCap.Flat : PenLineCap.Round,
                StrokeEndLineCap = TreeBuilder.Str(p["CapType"]) == "Flat" ? PenLineCap.Flat : PenLineCap.Round,
            };
        s.StrokeLineJoin = join;
        s.StrokeDashArray = Dash(TreeBuilder.Str(p["DashType"]));
        return new Canvas { Width = w, Height = h, Children = { s } };
    }

    /// <summary>Types without their own drawing yet: frame and background in the default colours of the type, name.</summary>
    private static FrameworkElement Placeholder(JsonObject p, double w, double h, string name) => new Border
    {
        Width = w > 0 ? w : 60,
        Height = h > 0 ? h : 24,
        Background = Brush(TreeBuilder.Str(p["BackColor"])) ?? new SolidColorBrush(Color.FromArgb(0x30, 0x4A, 0x90, 0xD9)),
        BorderBrush = Brush(TreeBuilder.Str(p["BorderColor"])) ?? new SolidColorBrush(Color.FromRgb(0x2B, 0x5C, 0x9E)),
        BorderThickness = new Thickness(Math.Max(1, Num(p["BorderWidth"]) ?? 1)),
        Child = new TextBlock
        {
            Text = name,
            FontSize = 11,
            Margin = new Thickness(3, 1, 3, 1),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x2A, 0x37)),
        },
    };

    // ---------- shared property helpers ----------

    /// <summary>Openness font names are written without blanks (SiemensSans); WPF needs the family name.</summary>
    private static readonly Dictionary<string, string> FontNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SiemensSans"] = "Siemens Sans", ["SiemensSansBold"] = "Siemens Sans", ["SiemensSerif"] = "Siemens Serif",
    };

    private static void ApplyFont(TextBlock tb, JsonObject? f)
    {
        var name = TreeBuilder.Str(f?["Name"]) ?? "SiemensSans";
        tb.FontFamily = new FontFamily((FontNames.TryGetValue(name, out var n) ? n : name) + ", Segoe UI");
        tb.FontSize = Num(f?["Size"]) is { } size && size > 0 ? size : 14;
        tb.FontWeight = TreeBuilder.Str(f?["Weight"]) switch
        {
            "Bold" => FontWeights.Bold, "SemiBold" => FontWeights.SemiBold, "Light" => FontWeights.Light,
            "Thin" => FontWeights.Thin, "Black" => FontWeights.Black, _ => FontWeights.Normal,
        };
        if (f?["Italic"] is JsonValue it && it.TryGetValue<bool>(out var italic) && italic) tb.FontStyle = FontStyles.Italic;
        var deco = new TextDecorationCollection();
        if (f?["Underline"] is JsonValue u && u.TryGetValue<bool>(out var ul) && ul) deco.Add(TextDecorations.Underline);
        if (f?["StrikeOut"] is JsonValue so && so.TryGetValue<bool>(out var st) && st) deco.Add(TextDecorations.Strikethrough);
        if (deco.Count > 0) tb.TextDecorations = deco;
    }

    /// <summary>Hide items with Visible=false as the runtime does (option --runtime-visibility, tool bar switch);
    /// off = like the TIA screen editor, which draws them.</summary>
    public static bool RuntimeVisibility { get; set; }

    /// <summary>Called when the switch "Like runtime" of a preview changes (the main window draws the screen again).</summary>
    public static Action? RuntimeVisibilityChanged { get; set; }

    /// <summary>Runtime language for texts (option --lang, e.g. en-US); null = the first language with a text.</summary>
    public static string? Language { get; set; }

    /// <summary>Multilingual text: the text of <see cref="Language"/> (the parser fills a language without own text
    /// with the default text, as the runtime shows it), else the first language (the parser gives texts per culture).</summary>
    private static string Text(JsonNode? t) => t switch
    {
        JsonObject o when Language != null && o.ContainsKey(Language) => TreeBuilder.Str(o[Language]) ?? "",
        JsonObject o => o.Select(kv => TreeBuilder.Str(kv.Value)).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "",
        JsonValue v => v.ToString(),
        _ => "",
    };

    private static DoubleCollection? Dash(string? dashType) => dashType switch
    {
        "Dash" => [4, 2], "Dot" => [1, 2], "DashDot" => [4, 2, 1, 2], "DashDotDot" => [4, 2, 1, 2, 1, 2], _ => null,
    };

    // ---------- PNG export and comparison ----------

    /// <summary>Renders the element at 96 dpi (1 screen pixel = 1 image pixel) to a bitmap.</summary>
    public static BitmapSource ToBitmap(FrameworkElement el)
    {
        el.Measure(new Size(el.Width, el.Height));
        el.Arrange(new Rect(0, 0, el.Width, el.Height));
        el.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(el.Width), (int)Math.Ceiling(el.Height), 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);
        bmp.Freeze();
        return bmp;
    }

    public static void SavePng(BitmapSource bmp, string file)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(file);
        enc.Save(fs);
    }

    /// <summary>Compares the viewer image with a reference (cropped at its top left corner to the viewer size).
    /// Writes <paramref name="outFile"/> as "reference | viewer | difference" (differing pixels red) and returns the
    /// share of pixels whose largest channel difference exceeds <paramref name="tolerance"/>.</summary>
    public static double Compare(BitmapSource viewer, string referenceFile, string outFile, int tolerance = 24)
    {
        var refImg = new FormatConvertedBitmap(new BitmapImage(new Uri(Path.GetFullPath(referenceFile))), PixelFormats.Bgra32, null, 0);
        var vImg = new FormatConvertedBitmap(viewer, PixelFormats.Bgra32, null, 0);
        int w = vImg.PixelWidth, h = vImg.PixelHeight;
        var a = Pixels(refImg, w, h);
        var b = Pixels(vImg, w, h);
        var d = new byte[w * h * 4];
        long diff = 0;
        for (var i = 0; i < w * h; i++)
        {
            var o = i * 4;
            var m = Math.Max(Math.Abs(a[o] - b[o]), Math.Max(Math.Abs(a[o + 1] - b[o + 1]), Math.Abs(a[o + 2] - b[o + 2])));
            var bad = m > tolerance;
            if (bad) diff++;
            // difference image: matching pixels as a faint grey copy, differing ones red
            var g = (byte)(200 + (a[o] + a[o + 1] + a[o + 2]) / 3 * 55 / 255);
            d[o] = bad ? (byte)0 : g;
            d[o + 1] = bad ? (byte)0 : g;
            d[o + 2] = bad ? (byte)255 : g;
            d[o + 3] = 255;
        }
        const int gap = 8;
        var all = new WriteableBitmap(w * 3 + gap * 2, h, 96, 96, PixelFormats.Bgra32, null);
        all.WritePixels(new Int32Rect(0, 0, w, h), a, w * 4, 0);
        all.WritePixels(new Int32Rect(w + gap, 0, w, h), b, w * 4, 0);
        all.WritePixels(new Int32Rect(2 * (w + gap), 0, w, h), d, w * 4, 0);
        SavePng(all, outFile);
        return (double)diff / (w * h);
    }

    /// <summary>BGRA pixels of the top left w×h area; outside the source (smaller reference) white.</summary>
    private static byte[] Pixels(BitmapSource src, int w, int h)
    {
        var res = new byte[w * h * 4];
        Array.Fill(res, (byte)255);
        int cw = Math.Min(w, src.PixelWidth), ch = Math.Min(h, src.PixelHeight);
        var row = new byte[cw * 4];
        for (var y = 0; y < ch; y++)
        {
            src.CopyPixels(new Int32Rect(0, y, cw, 1), row, cw * 4, 0);
            Buffer.BlockCopy(row, 0, res, y * w * 4, cw * 4);
        }
        return res;
    }

    // ---------- helpers ----------

    internal static Brush? Brush(string? argb)
    {
        if (string.IsNullOrEmpty(argb)) return null;
        // a color interface value of a faceplate instance comes as decimal ARGB (4288059030 = 0xFF969696)
        if (argb.All(char.IsDigit) && uint.TryParse(argb, NumberStyles.None, CultureInfo.InvariantCulture, out var dv))
            argb = "0x" + dv.ToString("X8", CultureInfo.InvariantCulture);
        if (!argb.StartsWith("0x") || argb.Length != 10) return null;
        if (!uint.TryParse(argb[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return new SolidColorBrush(Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v));
    }

    private static double? Num(JsonNode? n) =>
        n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
}
