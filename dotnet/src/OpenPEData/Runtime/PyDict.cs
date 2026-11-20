using System.Collections;
using System.Numerics;

namespace OpenPEData.Runtime;

/// <summary>Immutable Python tuple: structural equality and ordering, JSON output as an array.</summary>
internal sealed class PyTuple : IEnumerable<object?>
{
    public readonly object?[] Items;

    public PyTuple(params object?[] items) => Items = items;

    public int Count => Items.Length;

    public object? this[int i] => Items[i < 0 ? i + Items.Length : i];

    public IEnumerator<object?> GetEnumerator() => ((IEnumerable<object?>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();

    public override bool Equals(object? obj) => obj is PyTuple t && t.Items.Length == Items.Length &&
                                                Items.Zip(t.Items).All(p => Py.Eq(p.First, p.Second));

    public override int GetHashCode()
    {
        var h = new HashCode();
        foreach (var x in Items)
            h.Add(PyKeyComparer.Instance.GetHashCode(x ?? PyKeyComparer.NoneKey));
        return h.ToHashCode();
    }
}

/// <summary>Key equality of Python dicts and sets: 1 == 1.0 == True, bytes and tuples by value, None as a key.</summary>
internal sealed class PyKeyComparer : IEqualityComparer<object>
{
    public static readonly PyKeyComparer Instance = new();
    public static readonly object NoneKey = new();

    static object Norm(object o) => o switch
    {
        bool b => b ? 1L : 0L,
        int i => (long)i,
        ulong u when u <= long.MaxValue => (long)u,
        BigInteger bi when bi >= long.MinValue && bi <= long.MaxValue => (long)bi,
        double d when d == Math.Floor(d) && d >= long.MinValue && d < 9.2e18 => (long)d,
        _ => o,
    };

    public new bool Equals(object? x, object? y)
    {
        if (x is null || y is null)
            return ReferenceEquals(x, y);
        x = Norm(x);
        y = Norm(y);
        if (x is byte[] a && y is byte[] b)
            return a.AsSpan().SequenceEqual(b);
        return x.Equals(y);
    }

    public int GetHashCode(object obj)
    {
        obj = Norm(obj);
        if (obj is byte[] a)
        {
            var h = new HashCode();
            h.AddBytes(a);
            return h.ToHashCode();
        }
        return obj.GetHashCode();
    }
}

/// <summary>Python dict: insertion ordered, any hashable key (None included), replacing a value keeps its position.</summary>
internal sealed class PyDict : IEnumerable<KeyValuePair<object?, object?>>
{
    readonly OrderedDictionary<object, object?> d = new(PyKeyComparer.Instance);

    public PyDict() { }

    public PyDict(PyDict other)
    {
        foreach (var kv in other.d)
            d[kv.Key] = kv.Value;
    }

    static object K(object? k) => k switch
    {
        null => PyKeyComparer.NoneKey,
        PyDict => throw new PyTypeError("unhashable type: 'dict'"),
        System.Collections.IList and not byte[] => throw new PyTypeError("unhashable type: 'list'"),
        _ => k,
    };

    static object? U(object k) => ReferenceEquals(k, PyKeyComparer.NoneKey) ? null : k;

    public int Count => d.Count;

    public object? this[object? key]
    {
        get => d.TryGetValue(K(key), out var v) ? v : throw new PyKeyError(Py.Repr(key));
        set => d[K(key)] = value;
    }

    /// <summary>dict.get(key, default).</summary>
    public object? Get(object? key, object? dflt = null) => d.TryGetValue(K(key), out var v) ? v : dflt;

    public bool TryGetValue(object? key, out object? value) => d.TryGetValue(K(key), out value);

    public bool ContainsKey(object? key) => d.ContainsKey(K(key));

    public bool Remove(object? key) => d.Remove(K(key));

    /// <summary>dict.pop(key, default).</summary>
    public object? Pop(object? key, object? dflt = null) => d.Remove(K(key), out var v) ? v : dflt;

    /// <summary>dict.setdefault(key, value).</summary>
    public object? SetDefault(object? key, object? value)
    {
        if (d.TryGetValue(K(key), out var v))
            return v;
        d[K(key)] = value;
        return value;
    }

    public void Update(PyDict other)
    {
        foreach (var kv in other.d)
            d[kv.Key] = kv.Value;
    }

    public IEnumerable<object?> Keys => d.Keys.Select(U);

    public IEnumerable<object?> Values => d.Values;

    public IEnumerable<KeyValuePair<object?, object?>> Items => d.Select(kv => new KeyValuePair<object?, object?>(U(kv.Key), kv.Value));

    public IEnumerator<KeyValuePair<object?, object?>> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Builds a dict from name/value pairs (keys in order).</summary>
    public static PyDict Of(params object?[] kv)
    {
        var r = new PyDict();
        for (var i = 0; i + 1 < kv.Length; i += 2)
            r[kv[i]] = kv[i + 1];
        return r;
    }
}
