using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OpenPEData.Runtime;

/// <summary>xml.etree.ElementTree on top of XElement: tags as "{ns}local", attributes without namespace declarations,
/// el.text = text before the first child element (None if there is none), parse errors as ParseError.</summary>
internal static class Et
{
    static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Parse,
        // no external entities; internal entity expansion ("billion laughs") limited to 10 million characters
        XmlResolver = null,
        MaxCharactersFromEntities = 10_000_000,
        IgnoreComments = false,
    };

    /// <summary>ET.fromstring(text).</summary>
    public static XElement FromString(string text)
    {
        try
        {
            using var sr = new StringReader(text);
            using var xr = XmlReader.Create(sr, Settings);
            return XDocument.Load(xr, LoadOptions.PreserveWhitespace).Root ?? throw new XmlParseError("no element found");
        }
        catch (XmlException e)
        {
            throw new XmlParseError(e.Message);
        }
    }

    /// <summary>ET.fromstring(bytes): the encoding declaration / BOM decides.</summary>
    public static XElement FromBytes(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var xr = XmlReader.Create(ms, Settings);
            return XDocument.Load(xr, LoadOptions.PreserveWhitespace).Root ?? throw new XmlParseError("no element found");
        }
        catch (Exception e) when (e is XmlException or DecoderFallbackException or ArgumentException)
        {
            throw new XmlParseError(e.Message);
        }
    }

    public static string Tag(XElement el) => el.Name.NamespaceName.Length == 0 ? el.Name.LocalName : "{" + el.Name.NamespaceName + "}" + el.Name.LocalName;

    public static string Local(XElement el) => el.Name.LocalName;

    /// <summary>el.get(name); name may be "{ns}local".</summary>
    public static string? Get(XElement? el, string name) => el?.Attribute(XName.Get(name))?.Value;

    public static string Get(XElement? el, string name, string dflt) => Get(el, name) ?? dflt;

    /// <summary>list(el): the child elements.</summary>
    public static IEnumerable<XElement> Kids(XElement? el) => el?.Elements() ?? [];

    /// <summary>el.iter(): el and all descendants in document order.</summary>
    public static IEnumerable<XElement> Iter(XElement? el) => el?.DescendantsAndSelf() ?? [];

    /// <summary>el.iter(tag) for a tag without namespace.</summary>
    public static IEnumerable<XElement> Iter(XElement el, string tag) => el.DescendantsAndSelf().Where(e => Tag(e) == tag);

    /// <summary>el.find(tag): first child with that tag.</summary>
    public static XElement? Find(XElement el, string tag) => el.Elements().FirstOrDefault(e => Tag(e) == tag);

    public static IEnumerable<XElement> FindAll(XElement el, string tag) => el.Elements().Where(e => Tag(e) == tag);

    /// <summary>len(el) == 0.</summary>
    public static bool NoKids(XElement el) => !el.HasElements;

    /// <summary>el.text.</summary>
    public static string? Text(XElement? el)
    {
        if (el is null)
            return null;
        StringBuilder? sb = null;
        foreach (var n in el.Nodes())
        {
            if (n is XElement)
                break;
            if (n is XText t)
                (sb ??= new StringBuilder()).Append(t.Value);
        }
        return sb?.ToString();
    }

    /// <summary>dict(el.attrib).</summary>
    public static PyDict Attrib(XElement el)
    {
        var d = new PyDict();
        foreach (var a in el.Attributes())
        {
            if (a.IsNamespaceDeclaration)
                continue;
            d[a.Name.NamespaceName.Length == 0 ? a.Name.LocalName : "{" + a.Name.NamespaceName + "}" + a.Name.LocalName] = a.Value;
        }
        return d;
    }
}
