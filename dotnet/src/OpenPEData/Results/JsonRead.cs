using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenPEData;

/// <summary>Tolerant access to the JSON trees of the reader for the typed results: a missing key or a value of another
/// kind gives null (or an empty list), never an exception, so that a typed view never fails where the JSON tree is fine.</summary>
internal static class JsonRead
{
    static readonly JsonObject Empty = [];

    public static JsonObject Obj(JsonObject? o, string key) => o?[key] as JsonObject ?? Empty;

    public static JsonObject? ObjOrNull(JsonObject? o, string key) => o?[key] as JsonObject;

    public static string? Str(JsonObject? o, string key) =>
        o?[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    public static string StrOrEmpty(JsonObject? o, string key) => Str(o, key) ?? "";

    public static long? Long(JsonObject? o, string key) =>
        o?[key] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<long>(out var l) ? l
        : o?[key] is JsonValue d && d.GetValueKind() == JsonValueKind.Number && d.TryGetValue<double>(out var x) && x == Math.Floor(x) && Math.Abs(x) < 9e15 ? (long)x
        : null;

    public static double? Double(JsonObject? o, string key) =>
        o?[key] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<double>(out var d) ? d : null;

    public static bool? Bool(JsonObject? o, string key) => o?[key] is JsonValue v && v.GetValueKind() is JsonValueKind.True or JsonValueKind.False
        ? v.GetValue<bool>() : null;

    /// <summary>A scalar as text (string as is, number and bool in JSON notation); null for null, objects and arrays.</summary>
    public static string? Scalar(JsonObject? o, string key) => o?[key] is JsonValue v
        ? v.GetValueKind() switch
        {
            JsonValueKind.String => v.GetValue<string>(),
            JsonValueKind.Null => null,
            _ => v.ToJsonString(),
        }
        : null;

    public static IEnumerable<JsonObject> Objects(JsonObject? o, string key) =>
        (o?[key] as JsonArray ?? []).OfType<JsonObject>();

    public static IReadOnlyList<T> List<T>(JsonObject? o, string key, Func<JsonObject, T> make) =>
        new ReadOnlyCollection<T>([.. Objects(o, key).Select(make)]);

    public static IReadOnlyList<string> Strings(JsonObject? o, string key) =>
        new ReadOnlyCollection<string>([.. (o?[key] as JsonArray ?? []).Select(n => n is JsonValue v && v.GetValueKind() == JsonValueKind.String
            ? v.GetValue<string>() : n?.ToJsonString() ?? "")]);

    public static MultilingualText Text(JsonObject? o, string key) => MultilingualText.From(o?[key]);
}
