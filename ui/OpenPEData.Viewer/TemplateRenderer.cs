using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace OpenPEData.Viewer;

/// <summary>Draws a screen item with the rendering template of the selected style (parser hmi style templates, SVG with
/// hmi-bind; docs\spec\hmi-styles.md), in the resting state: no hover, not pressed, no focus. The template is expanded
/// into plain SVG (bindings evaluated, hmi:if applied, localDef evaluated in order, hmi:include expanded with its own
/// scope) and drawn with SvgRenderer. hmi-element:textField becomes single-line text; other runtime controls
/// (hmi-element:table, chart, ...), master-include, actions and events are not supported yet (left out).</summary>
public static partial class TemplateRenderer
{
    private static readonly XNamespace Hmi = "http://svg.siemens.com/hmi/";
    private static readonly XNamespace Bind = "http://svg.siemens.com/hmi/bind/";
    private static readonly XNamespace BindXlink = "http://svg.siemens.com/hmi/bind/xlink/";
    private static readonly XNamespace Element = "http://svg.siemens.com/hmi/element/";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";
    private static readonly Dictionary<string, XDocument?> Parsed = new();
    // {{ expr }} or { path } inside text, e.g. "translate({ LocalProps.CenterX },{ LocalProps.CenterY })" (Flat gauge)
    private static readonly Regex Embedded = new(@"\{\{(.*?)\}\}|\{([^{}]+)\}", RegexOptions.Singleline);
    private static string EmbeddedExpr(Match m) => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;

    /// <summary>Item types drawn with their template (prototype).</summary>
    public static readonly HashSet<string> Types = ["HmiButton", "HmiIOField", "HmiTextBox", "HmiToggleSwitch", "HmiBar", "HmiSlider", "HmiGauge", "HmiClock"];

    /// <summary>Thresholds without a stored Value are 0 in the runtime: all markers lie on 0, Minimum on top
    /// (WinCC Unified runtime reference 2026-11-08, sample 171). The TIA editor instead previews 90/80/50/20/10 % of
    /// the scale for Maximum/Upper/Normal/Lower/Minimum; that preview is not stored and not used here.</summary>
    private static JsonObject WithThresholdValues(JsonObject props)
    {
        if (props["Thresholds"] is not JsonArray list || list.OfType<JsonObject>().All(t => t["Value"] != null)) return props;
        props = (JsonObject)props.DeepClone();
        foreach (var t in ((JsonArray)props["Thresholds"]!).OfType<JsonObject>())
            t["Value"] ??= 0;
        return props;
    }

    /// <summary>The item as image of w × h, or null when the style has no usable template for it.</summary>
    public static FrameworkElement? Render(JsonObject props, JsonObject? style, double w, double h)
    {
        if (style?["templates"] is not JsonObject templates || w <= 0 || h <= 0) return null;
        if (props["RenderingTemplate"]?.ToString() is not { } name || Load(templates, name) is not { } doc) return null;
        // without a tag the runtime shows 0 on bar, slider and gauge, but an empty I/O field
        if (props["ProcessValue"] == null && !name.Contains("iofield"))
        {
            props = (JsonObject)props.DeepClone();
            props["ProcessValue"] = 0;
        }
        props = WithThresholdValues(props);
        var scope = new TemplateScope { Hmi = props, Width = w, Height = h };
        var counter = 0;
        var root = new XElement(Svg + "svg", new XAttribute("width", Fmt(w)), new XAttribute("height", Fmt(h)),
            new XAttribute("viewBox", $"0 0 {Fmt(w)} {Fmt(h)}"));
        Expand(doc.Root!, root, scope, templates, ref counter, 0);
        if (Environment.GetEnvironmentVariable("OPENPEDATA_TEMPLATE_DUMP") is { Length: > 0 } dumpDir)
            System.IO.File.WriteAllText(System.IO.Path.Combine(dumpDir, name + ".expanded.svg"), root.ToString());
        var img = SvgRenderer.Load(root.ToString());
        if (img == null) return null;
        return new Image { Source = img, Width = w, Height = h, Stretch = Stretch.Fill };
    }

    private static XDocument? Load(JsonObject templates, string name)
    {
        name = name.EndsWith(".svghmi") ? name[..^7] : name;
        lock (Parsed)
        {
            var key = name + "|" + templates.GetHashCode();
            if (Parsed.TryGetValue(key, out var d)) return d;
            d = null;
            if (templates[name]?.ToString()?.TrimStart('﻿') is { } text)  // some templates start with a BOM
                try
                {
                    // the DTD is not available: parse without it
                    var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore };
                    using var r = System.Xml.XmlReader.Create(new System.IO.StringReader(text), settings);
                    d = XDocument.Load(r);
                }
                catch (Exception) { d = null; }
            Parsed[key] = d;
            return d;
        }
    }

    /// <summary>Expands the children of a template element (the template root or an element) into target.</summary>
    private static void Expand(XElement src, XElement target, TemplateScope s, JsonObject templates, ref int counter, int depth)
    {
        if (depth > 24) return;
        foreach (var e in src.Elements())
        {
            if (e.Attribute(Hmi + "if") is { } cond && !TemplateExpr.Truthy(TemplateExpr.EvalAttr(cond.Value, s))) continue;
            if (e.Name == Element + "textField")
            {
                TextField(e, target, s);
                continue;
            }
            if (e.Name == Element + "arcDial" || e.Name == Element + "straightDial")
            {
                var d = depth;
                ExpandDial(e, target, s, (holder, box, sc, w, h) => MasterContent(holder, box, sc, w, h, templates, d));
                continue;
            }
            if (e.Name == Hmi + "self" || e.Name == Hmi + "action" || e.Name.Namespace == Element) continue;
            if (e.Name == Hmi + "localDef")
            {
                var n = e.Attribute("name")?.Value;
                if (n == null) continue;
                object? v = e.Attribute(Bind + "value") is { } bv ? TemplateExpr.EvalAttr(bv.Value, s)
                    : Literal(e.Attribute("value")?.Value, e.Attribute("type")?.Value);
                s.Local[n] = v;
                continue;
            }
            if (e.Name == Hmi + "include")
            {
                Include(e, target, s, templates, ref counter, depth);
                continue;
            }
            if (e.Name == Hmi + "text")
            {
                target.Add(TemplateExpr.Str(e.Attribute(Bind + "value") is { } tv ? TemplateExpr.EvalAttr(tv.Value, s) : e.Value));
                continue;
            }
            if (e.Name.Namespace == Hmi) continue;  // master-include, template, param, ... (not yet)
            if (e.Name.Namespace != Svg && e.Name.Namespace != XNamespace.None) continue;
            var copy = new XElement(Svg + e.Name.LocalName);
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration) continue;
                if (a.Name.Namespace == Bind)
                {
                    if (a.Name.LocalName is "scope" or "for" or "condition") continue;
                    var t = a.Value.Trim();
                    // a whole expression, or text with embedded {{ }} such as "translate( {{ x }} , {{ y }} )"
                    var v = t.StartsWith('{') && t.EndsWith('}') && Embedded.Matches(t).Count <= 1 ? TemplateExpr.EvalAttr(t, s)
                        : Embedded.Replace(a.Value, m => TemplateExpr.Str(TemplateExpr.Eval(EmbeddedExpr(m), s)));
                    if (v != null) copy.SetAttributeValue(a.Name.LocalName, TemplateExpr.Str(v));
                }
                else if (a.Name.Namespace == BindXlink)
                {
                    var v = TemplateExpr.Str(TemplateExpr.EvalAttr(a.Value, s));
                    if (v != "") copy.SetAttributeValue(Xlink + a.Name.LocalName, v);
                }
                else if (a.Name.Namespace == XNamespace.None || a.Name.Namespace == Xlink)
                {
                    // plain attributes may embed expressions: transform="translate( {{ a }} , {{ b }} )"
                    var v = a.Value.Contains("{{") ? Embedded.Replace(a.Value, m => m.Groups[1].Success ? TemplateExpr.Str(TemplateExpr.Eval(m.Groups[1].Value, s)) : m.Value) : a.Value;
                    if (copy.Attribute(a.Name) == null) copy.SetAttributeValue(a.Name, v);
                }
            }
            if (!e.HasElements && !string.IsNullOrWhiteSpace(e.Value) && e.Name.LocalName == "text") copy.Add(e.Value.Trim());
            Expand(e, copy, s, templates, ref counter, depth + 1);
            target.Add(copy);
        }
    }

    /// <summary>hmi:include: the named template with its own scope (HmiProps = the object of hmi-bind:for, ParamProps =
    /// the hmi:param values), placed as nested svg at x/y with width/height (= Layout.ActualWidth/Height).</summary>
    private static void Include(XElement e, XElement target, TemplateScope s, JsonObject templates, ref int counter, int depth)
    {
        if (e.Attribute("template")?.Value is not { } name || Load(templates, name) is not { } doc) return;
        double A(string n) => TemplateExpr.Num(e.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s)
            : e.Attribute(n)?.Value);
        double x = A("x"), y = A("y"), w = A("width"), h = A("height");
        if (w <= 0) w = s.Width;
        if (h <= 0) h = s.Height;
        var inner = new TemplateScope
        {
            Hmi = e.Attribute(Bind + "for") is { } f && TemplateExpr.EvalAttr(f.Value, s) is JsonObject fo ? fo : s.Hmi,
            Width = w,
            Height = h,
        };
        foreach (var p in e.Elements(Hmi + "param"))
            if (p.Attribute("name")?.Value is { } pn)
                inner.Param[pn] = p.Attribute(Bind + "value") is { } pv ? TemplateExpr.EvalAttr(pv.Value, s)
                    : Literal(p.Attribute("value")?.Value, p.Attribute("type")?.Value);
        foreach (var pd in doc.Root!.Descendants(Hmi + "paramDef"))
            if (pd.Attribute("name")?.Value is { } dn && !inner.Param.ContainsKey(dn))
                inner.Param[dn] = Literal(pd.Attribute("value")?.Value ?? pd.Attribute("default")?.Value, pd.Attribute("type")?.Value);
        var wrap = new XElement(Svg + "svg", new XAttribute("x", Fmt(x)), new XAttribute("y", Fmt(y)),
            new XAttribute("width", Fmt(w)), new XAttribute("height", Fmt(h)));
        Expand(doc.Root!, wrap, inner, templates, ref counter, depth + 1);
        Uniquify(wrap, ++counter);
        target.Add(wrap);
    }

    private static int _masterCounter;

    /// <summary>Content of a pole marker: hmi:master-inline (its hmi:template svg) or hmi:master-include (a named
    /// template), with ParamProps from hmi:param (and the paramDef defaults of an included template), HmiProps of
    /// the item and Layout = content size.</summary>
    private static void MasterContent(XElement holder, XElement box, TemplateScope s, double w, double h, JsonObject templates, int depth)
    {
        var inner = new TemplateScope { Hmi = s.Hmi, Width = w, Height = h };
        foreach (var p in holder.Elements(Hmi + "param"))
            if (p.Attribute("name")?.Value is { } pn)
                inner.Param[pn] = p.Attribute(Bind + "value") is { } pv ? TemplateExpr.EvalAttr(pv.Value, s)
                    : Literal(p.Attribute("value")?.Value, p.Attribute("type")?.Value);
        XElement? root = holder.Name == Hmi + "master-inline"
            ? holder.Element(Hmi + "template")?.Elements().FirstOrDefault()
            : holder.Attribute("template")?.Value is { } name ? Load(templates, name)?.Root : null;
        if (root == null) return;
        foreach (var pd in root.Descendants(Hmi + "paramDef"))
            if (pd.Attribute("name")?.Value is { } dn && !inner.Param.ContainsKey(dn))
                inner.Param[dn] = Literal(pd.Attribute("value")?.Value ?? pd.Attribute("default")?.Value, pd.Attribute("type")?.Value);
        var counter = 0;
        Expand(root, box, inner, templates, ref counter, depth + 1);
        Uniquify(box, 100000 + Interlocked.Increment(ref _masterCounter));
    }

    /// <summary>hmi-element:textField (runtime text control) as single-line SVG text: start-x/start-y, width/height,
    /// padding, horizontal/vertical-text-alignment, value, fore-color, font from hmi-feature:font scope.</summary>
    private static void TextField(XElement e, XElement target, TemplateScope s)
    {
        object? B(string n) => e.Attribute(Bind + n) is { } b ? TemplateExpr.EvalAttr(b.Value, s) : e.Attribute(n)?.Value;
        // output-format of the element (E/A field: "{I} %" etc.) applies to a number
        var raw = B("value");
        var rawText = TemplateExpr.Str(raw);
        var text = TemplateExpr.Str(B("output-format")) is { Length: > 0 } fmt
                   && double.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
            ? TemplateExpr.FormatPattern(num, fmt) : rawText;
        if (text.Length == 0) return;
        double x = TemplateExpr.Num(B("start-x")), y = TemplateExpr.Num(B("start-y"));
        double w = TemplateExpr.Num(B("width")), h = TemplateExpr.Num(B("height"));
        double pl = 0, pr = 0, pt = 0, pb = 0;
        switch (B("padding"))
        {
            case JsonObject po:
                double P(string k) => TemplateExpr.Num(TemplateExpr.FromJson(po[k]));
                (pl, pr, pt, pb) = (P("Left"), P("Right"), P("Top"), P("Bottom"));
                break;
            case double pd:
                pl = pr = pt = pb = pd;
                break;
            // Flat textbox template: "a" (all), "a b" (Left = Top = a, Right = Bottom = b) or "L T R B"
            case string ps when ps.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: 1 or 2 or 4 } pp:
                var n = pp.Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0).ToArray();
                (pl, pt, pr, pb) = n.Length switch { 1 => (n[0], n[0], n[0], n[0]), 2 => (n[0], n[0], n[1], n[1]), _ => (n[0], n[1], n[2], n[3]) };
                break;
        }
        var font = e.Elements().FirstOrDefault(f => f.Name.LocalName == "font")?.Attribute(Bind + "scope") is { } fs
            ? TemplateExpr.EvalAttr(fs.Value, s) as JsonObject : null;
        var size = TemplateExpr.Num(TemplateExpr.FromJson(font?["Size"]));
        if (size <= 0) size = 14;
        var m = TemplateExpr.Measure(text, font);
        double iw = Math.Max(0, w - pl - pr), ih = Math.Max(0, h - pt - pb);
        // Extended templates write Center/Bottom, Flat templates center/bottom
        var ha = TemplateExpr.Str(B("horizontal-text-alignment")).ToLowerInvariant() switch { "center" => "Center", "right" => "Right", _ => "Left" };
        var va = TemplateExpr.Str(B("vertical-text-alignment")).ToLowerInvariant() switch { "top" => "Top", "bottom" => "Bottom", _ => "Middle" };
        double tx = pl + (ha == "Center" ? iw / 2 : ha == "Right" ? iw : 0);
        // multi-line text (line breaks in the value): every line aligned on its own, the block as a whole vertically
        var lines = text.Replace("\r\n", "\n").Split('\n', '\r');
        var lineH = lines.Length > 1 ? TemplateExpr.Measure("Xg", font).Height : m.Height;
        var blockH = lineH * lines.Length;
        var baseline = lineH * 0.8;  // ascent share of the line height (SiemensSans / Segoe UI)
        double ty = pt + (va == "Top" ? baseline : va == "Bottom" ? ih - blockH + baseline : (ih - blockH) / 2 + baseline);
        var color = B("fore-color") is double c ? TemplateExpr.Str(TemplateExpr.Eval("Converter.RGBA(" + c.ToString(CultureInfo.InvariantCulture) + ")", s))
            : TemplateExpr.Str(B("fore-color"));
        var box = new XElement(Svg + "svg", new XAttribute("x", Fmt(x)), new XAttribute("y", Fmt(y)),
            new XAttribute("width", Fmt(w)), new XAttribute("height", Fmt(h)));
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            box.Add(new XElement(Svg + "text", new XAttribute("x", Fmt(tx)), new XAttribute("y", Fmt(ty + i * lineH)),
                new XAttribute("fill", color == "" ? "black" : color), new XAttribute("font-size", Fmt(size)),
                new XAttribute("font-family", font?["Name"]?.ToString() ?? "SiemensSans"),
                new XAttribute("font-weight", font?["Weight"]?.ToString() ?? "Normal"),
                new XAttribute("font-style", TemplateExpr.Truthy(TemplateExpr.FromJson(font?["Italic"])) ? "italic" : "normal"),
                new XAttribute("text-anchor", ha == "Center" ? "middle" : ha == "Right" ? "end" : "start"), lines[i]));
        }
        target.Add(box);
    }

    /// <summary>ids of an expanded template made unique (includes reuse ids such as ItemClipRect).</summary>
    private static void Uniquify(XElement root, int n)
    {
        var ids = root.Descendants().Select(d => d.Attribute("id")).Where(a => a != null).ToList();
        if (ids.Count == 0) return;
        var map = ids.Select(a => a!.Value).Distinct().ToDictionary(v => v, v => $"i{n}_{v}");
        foreach (var a in ids) a!.Value = map[a.Value];
        foreach (var a in root.DescendantsAndSelf().SelectMany(d => d.Attributes()))
        {
            if (a.Name == "id") continue;
            if (a.Value.Contains("url(#"))
                a.Value = Regex.Replace(a.Value, @"url\(#([^)]+)\)", m => map.TryGetValue(m.Groups[1].Value, out var v) ? $"url(#{v})" : m.Value);
            else if (a.Name.LocalName == "href" && a.Value.StartsWith('#') && map.TryGetValue(a.Value[1..], out var hv))
                a.Value = "#" + hv;
        }
    }

    private static object? Literal(string? v, string? type)
    {
        if (v == null) return type switch { "number" => 0.0, "boolean" => false, "string" => "", _ => null };
        return type switch
        {
            "number" or "HmiColor" => TemplateExpr.Num(v),
            "boolean" => v == "true",
            _ => v,
        };
    }

    private static string Fmt(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
}
