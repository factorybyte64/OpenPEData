#pragma warning disable CS8600, CS8602
using OpenPEData.Runtime;

namespace OpenPEData.Model;

internal sealed partial class Code
{
    /// <summary>Hardware connections of a technology object DB (sample 079): a compile unit with ProgrammingLanguage
    /// Motion_DB and ProgrammingContext "ioif" holds &lt;section&gt; with one item per connected interface (first = interface,
    /// e.g. Actor; second/ids = operands by RefId into the ident table of the DB, second/props = further values). Result:
    /// [{interface, operands, properties}], empty when the TO is not connected.</summary>
    public List<object?> ToHardwareConnections(string db)
    {
        var outl = new List<object?>();
        var idents = new Idents(this, db);
        foreach (var k in Linked(db, "Sources", "CompileUnitData"))
        {
            var cu = p.Sets(k).Set("ICompileUnitData");
            if (!Py.Eq(cu.Get("ProgrammingContext"), "ioif"))
                continue;
            var root = XmlOfBlob(cu.Get("Data"));
            if (root is null || Et.Local(root) != "section")
                continue;
            foreach (var item in Et.Kids(root).Where(x => Et.Local(x) == "item"))
            {
                var second = First(item, "second");
                var operands = Et.Kids(First(second, "ids")).Where(x => Et.Local(x) == "item")
                    .Select(x => (object?)(idents.Text(idents.Get(Et.Text(First(x, "refId")))) ?? "")).ToList();
                var props = new PyDict();
                foreach (var pr in Et.Kids(First(second, "props")).Where(x => Et.Local(x) == "item"))
                    props[Et.Text(First(pr, "first")) ?? ""] = Et.Text(First(First(pr, "second"), "value"));
                outl.Add(PyDict.Of("interface", Et.Text(First(item, "first")), "destination", Et.Text(First(second, "dst")),
                    "operands", operands, "properties", props));
            }
        }
        return outl;
    }
}
