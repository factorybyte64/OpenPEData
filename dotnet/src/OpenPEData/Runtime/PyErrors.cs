namespace OpenPEData.Runtime;

/// <summary>Errors with the semantics of the Python exceptions the reference parser catches (struct.error, IndexError,
/// ValueError, ...). PyType is the Python class name, used where the parser reports "Type: message".</summary>
internal class PyError : Exception
{
    public string PyType { get; }

    public PyError(string pyType, string message) : base(message) => PyType = pyType;
}

/// <summary>struct.error (buffer too short for an unpack).</summary>
internal sealed class StructError(string message) : PyError("error", message);

internal sealed class PyIndexError(string message) : PyError("IndexError", message);

internal class PyValueError(string message, string type = "ValueError") : PyError(type, message);

/// <summary>UnicodeDecodeError is a ValueError in Python.</summary>
internal sealed class PyUnicodeDecodeError(string message) : PyValueError(message, "UnicodeDecodeError");

internal sealed class PyKeyError(string message) : PyError("KeyError", message);

internal sealed class PyTypeError(string message) : PyError("TypeError", message);

internal sealed class PyAttributeError(string message) : PyError("AttributeError", message);

/// <summary>zlib.error.</summary>
internal sealed class ZlibError(string message) : PyError("error", message);

/// <summary>xml.etree.ElementTree.ParseError.</summary>
internal sealed class XmlParseError(string message) : PyError("ParseError", message);

internal sealed class PyStopIteration() : PyError("StopIteration", "");

/// <summary>What Python's "except Exception" catches in the reference parser: any error of the data, but not cancellation,
/// OutOfMemoryException or a too deep recursion, which must reach the caller.</summary>
internal static class PyCatch
{
    public static bool Any(Exception e) => e is not (OperationCanceledException or OutOfMemoryException or InsufficientExecutionStackException);
}
