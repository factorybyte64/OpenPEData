namespace OpenPEData;

/// <summary>Base class of the errors <see cref="TiaProject"/> reports. The original error of the reader, if any, is the
/// InnerException.</summary>
public class OpenPEDataException : Exception
{
    /// <summary>The path that was opened.</summary>
    public string? Path { get; }

    public OpenPEDataException(string message, string? path = null, Exception? inner = null) : base(message, inner) => Path = path;
}

/// <summary>The path is no TIA Portal project or global library: neither folder nor archive, or no System\PEData.plf, or no
/// metamodel record in it.</summary>
public sealed class NotATiaProjectException(string message, string? path = null, Exception? inner = null)
    : OpenPEDataException(message, path, inner);

/// <summary>A project of a TIA Portal version this library has not been tested with (see <see cref="TiaVersion.Supported"/>).
/// <see cref="OpenOptions.AllowUnknownVersion"/> reads it anyway.</summary>
public sealed class UnsupportedVersionException(TiaVersion version, string? path = null)
    : OpenPEDataException($"TIA Portal {version} is not supported (supported: {string.Join(", ", TiaVersion.Supported)}).", path)
{
    public TiaVersion Version { get; } = version;
}

/// <summary>The project has a PEData.plf, but its TIA version cannot be told (no .apNN/.alNN file).
/// <see cref="OpenOptions.AllowUnknownVersion"/> reads it anyway.</summary>
public sealed class UnsupportedFormatException(string message, string? path = null)
    : OpenPEDataException(message, path);

/// <summary>The project was recognized, but its content could not be read as a whole. Single objects that cannot be read do
/// not throw; they are listed by <see cref="TiaProject.GetIssues"/>.</summary>
public sealed class CorruptProjectException(string message, string? path = null, Exception? inner = null)
    : OpenPEDataException(message, path, inner);
