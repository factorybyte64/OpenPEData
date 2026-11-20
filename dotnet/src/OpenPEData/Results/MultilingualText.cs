using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenPEData;

/// <summary>A text in several project languages (culture name such as "en-US" → text), e.g. a comment or a title.
/// Empty if the object has no text.</summary>
public sealed class MultilingualText : IReadOnlyDictionary<string, string>
{
    readonly Dictionary<string, string> items;

    MultilingualText(Dictionary<string, string> items) => this.items = items;

    /// <summary>A text without any language.</summary>
    public static MultilingualText Empty { get; } = new([]);

    /// <summary>The text in the given culture, else in the first language that has one; null if there is none.</summary>
    public string? Get(string? culture = null) =>
        culture is not null && items.TryGetValue(culture, out var t) ? t : items.Values.FirstOrDefault();

    /// <inheritdoc/>
    public string this[string key] => items[key];

    /// <inheritdoc/>
    public IEnumerable<string> Keys => items.Keys;

    /// <inheritdoc/>
    public IEnumerable<string> Values => items.Values;

    /// <inheritdoc/>
    public int Count => items.Count;

    /// <inheritdoc/>
    public bool ContainsKey(string key) => items.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(string key, out string value) => items.TryGetValue(key, out value!);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The first text, or "" (for display).</summary>
    public override string ToString() => Get() ?? "";

    /// <summary>From a JSON object culture → text; a plain string counts as text without culture (key "").</summary>
    internal static MultilingualText From(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (k, v) in o)
                    if (v is JsonValue s && s.GetValueKind() == JsonValueKind.String)
                        d[k] = s.GetValue<string>();
                return d.Count == 0 ? Empty : new MultilingualText(d);
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                return new MultilingualText(new Dictionary<string, string> { [""] = v.GetValue<string>() });
            default:
                return Empty;
        }
    }
}
