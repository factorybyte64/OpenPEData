namespace OpenPEData.Runtime;

/// <summary>Size limits for untrusted project files: one archive entry, one decompressed blob. TiaProject sets them from
/// OpenOptions for the duration of a call (Use); without a scope the defaults apply.</summary>
internal sealed record ReadLimits(long MaxEntryBytes, long MaxDecompressedBytes)
{
    public static readonly ReadLimits Default = new(1L << 30, 256L << 20);

    [ThreadStatic]
    static ReadLimits? current;

    public static ReadLimits Current => current ?? Default;

    /// <summary>Makes these limits current on this thread until the returned scope is disposed.</summary>
    public Scope Use()
    {
        var previous = current;
        current = this;
        return new Scope(previous);
    }

    public readonly struct Scope(ReadLimits? previous) : IDisposable
    {
        public void Dispose() => current = previous;
    }
}
