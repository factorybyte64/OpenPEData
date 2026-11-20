using System.Buffers.Binary;
using System.Text;
using OpenPEData.Runtime;

namespace OpenPEData.Format;

/// <summary>Read-only reader for SQLite 3 database files (XRef\XRef.db): full scans of rowid tables in rowid order, which
/// is what "select ... from table" returns for these tables. Values as Python's sqlite3 gives them: long, double,
/// string, byte[], null; an "integer primary key" column is the rowid. No external dependency.</summary>
internal sealed class SqliteReader
{
    readonly byte[] db;
    readonly int pageSize, usable;
    readonly Encoding text;
    readonly Dictionary<string, (int Root, List<string> Columns, int RowidColumn)> tables = new(StringComparer.OrdinalIgnoreCase);

    public SqliteReader(byte[] data)
    {
        db = data;
        if (data.Length < 100 || Encoding.ASCII.GetString(data, 0, 15) != "SQLite format 3")
            throw new PyError("DatabaseError", "file is not a database");
        pageSize = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(16));
        if (pageSize == 1)
            pageSize = 65536;
        usable = pageSize - data[20];
        text = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(56)) switch
        {
            2 => new UnicodeEncoding(false, false),
            3 => new UnicodeEncoding(true, false),
            _ => new UTF8Encoding(false, false),
        };
        foreach (var (_, row) in Scan(1))
        {
            if (row.Count >= 5 && row[0] as string == "table" && row[1] is string name && row[3] is long root && row[4] is string sql)
            {
                var (cols, rid) = ParseColumns(sql);
                tables[name] = ((int)root, cols, rid);
            }
        }
    }

    /// <summary>Column names of a CREATE TABLE statement and the index of its "integer primary key" column (-1 if none).</summary>
    static (List<string>, int) ParseColumns(string sql)
    {
        var a = sql.IndexOf('(');
        var e = sql.LastIndexOf(')');
        var body = a >= 0 && e > a ? sql[(a + 1)..e] : "";
        var defs = new List<string>();
        var depth = 0;
        var cur = new StringBuilder();
        foreach (var c in body)
        {
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;
            if (c == ',' && depth == 0)
            {
                defs.Add(cur.ToString());
                cur.Clear();
            }
            else
                cur.Append(c);
        }
        defs.Add(cur.ToString());
        var cols = new List<string>();
        var rid = -1;
        foreach (var d in defs)
        {
            var toks = Py.SplitWs(d);
            if (toks.Count == 0)
                continue;
            var first = toks[0].ToLowerInvariant();
            if (first is "constraint" or "primary" or "unique" or "check" or "foreign")
                continue;
            var name = toks[0].Trim('"', '`', '[', ']');
            var lower = " " + string.Join(" ", toks.Skip(1)).ToLowerInvariant() + " ";
            if (toks.Count > 1 && toks[1].Equals("integer", StringComparison.OrdinalIgnoreCase) && lower.Contains(" primary key"))
                rid = cols.Count;
            cols.Add(name);
        }
        return (cols, rid);
    }

    public bool HasTable(string name) => tables.ContainsKey(name);

    public List<string> Columns(string table) => tables[table].Columns;

    /// <summary>All rows of a table (rowid order), columns in schema order.</summary>
    public IEnumerable<List<object?>> Rows(string table)
    {
        if (!tables.TryGetValue(table, out var t))
            throw new PyError("OperationalError", "no such table: " + table);
        foreach (var (rowid, row) in Scan(t.Root))
        {
            while (row.Count < t.Columns.Count)
                row.Add(null);
            if (t.RowidColumn >= 0 && row[t.RowidColumn] is null)
                row[t.RowidColumn] = rowid;
            yield return row;
        }
    }

    /// <summary>Rows of a table restricted to the named columns.</summary>
    public IEnumerable<List<object?>> Select(string table, params string[] columns)
    {
        var cols = tables.TryGetValue(table, out var t) ? t.Columns : throw new PyError("OperationalError", "no such table: " + table);
        var idx = columns.Select(c => cols.FindIndex(x => x.Equals(c, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (idx.Any(i => i < 0))
            throw new PyError("OperationalError", "no such column");
        foreach (var row in Rows(table))
            yield return idx.Select(i => row[i]).ToList();
    }

    int PageOff(int page) => (page - 1) * pageSize;

    IEnumerable<(long Rowid, List<object?> Row)> Scan(int root)
    {
        var stack = new Stack<int>();
        stack.Push(root);
        var pending = new Stack<IEnumerator<(long, List<object?>)>>();
        foreach (var x in ScanPage(root, 0))
            yield return x;
    }

    IEnumerable<(long, List<object?>)> ScanPage(int page, int depth)
    {
        if (depth > 64 || page < 1 || PageOff(page) >= db.Length)
            yield break;
        var po = PageOff(page);
        var h = page == 1 ? po + 100 : po;
        var type = db[h];
        var ncells = BinaryPrimitives.ReadUInt16BigEndian(db.AsSpan(h + 3));
        if (type == 0x05)
        {
            var right = (int)BinaryPrimitives.ReadUInt32BigEndian(db.AsSpan(h + 8));
            for (var i = 0; i < ncells; i++)
            {
                var cp = po + BinaryPrimitives.ReadUInt16BigEndian(db.AsSpan(h + 12 + 2 * i));
                var child = (int)BinaryPrimitives.ReadUInt32BigEndian(db.AsSpan(cp));
                foreach (var x in ScanPage(child, depth + 1))
                    yield return x;
            }
            foreach (var x in ScanPage(right, depth + 1))
                yield return x;
        }
        else if (type == 0x0D)
        {
            for (var i = 0; i < ncells; i++)
            {
                var cp = po + BinaryPrimitives.ReadUInt16BigEndian(db.AsSpan(h + 8 + 2 * i));
                var (plen, p1) = Varint(cp);
                var (rowid, p2) = Varint(p1);
                yield return (rowid, Record(Payload(p2, plen)));
            }
        }
    }

    byte[] Payload(int p, long plen)
    {
        var x = usable - 35;
        if (plen <= x)
            return db.AsSpan(p, (int)plen).ToArray();
        var m = (usable - 12) * 32 / 255 - 23;
        var k = m + (int)((plen - m) % (usable - 4));
        var local = k <= x ? k : m;
        var outb = new byte[plen];
        Array.Copy(db, p, outb, 0, local);
        var got = local;
        var next = (int)BinaryPrimitives.ReadUInt32BigEndian(db.AsSpan(p + local));
        while (got < plen && next > 0)
        {
            var po = PageOff(next);
            var n = (int)Math.Min(usable - 4, plen - got);
            Array.Copy(db, po + 4, outb, got, n);
            got += n;
            next = (int)BinaryPrimitives.ReadUInt32BigEndian(db.AsSpan(po));
        }
        return outb;
    }

    (long, int) Varint(int p) => Varint(db, p);

    static (long, int) Varint(byte[] b, int p)
    {
        long v = 0;
        for (var i = 0; i < 8; i++)
        {
            var c = b[p++];
            v = (v << 7) | (long)(c & 0x7F);
            if ((c & 0x80) == 0)
                return (v, p);
        }
        v = (v << 8) | b[p++];
        return (v, p);
    }

    List<object?> Record(byte[] r)
    {
        var (hsize, p) = Varint(r, 0);
        var types = new List<long>();
        while (p < hsize)
        {
            var (t, q) = Varint(r, p);
            types.Add(t);
            p = q;
        }
        var o = (int)hsize;
        var row = new List<object?>();
        foreach (var t in types)
        {
            switch (t)
            {
                case 0:
                    row.Add(null);
                    break;
                case >= 1 and <= 6:
                    var n = t switch { 1 => 1, 2 => 2, 3 => 3, 4 => 4, 5 => 6, _ => 8 };
                    long v = (sbyte)r[o];
                    for (var i = 1; i < n; i++)
                        v = (v << 8) | r[o + i];
                    row.Add(v);
                    o += n;
                    break;
                case 7:
                    row.Add(BinaryPrimitives.ReadDoubleBigEndian(r.AsSpan(o)));
                    o += 8;
                    break;
                case 8:
                    row.Add(0L);
                    break;
                case 9:
                    row.Add(1L);
                    break;
                default:
                    if (t >= 12 && t % 2 == 0)
                    {
                        var len = (int)((t - 12) / 2);
                        row.Add(r.AsSpan(o, len).ToArray());
                        o += len;
                    }
                    else if (t >= 13)
                    {
                        var len = (int)((t - 13) / 2);
                        row.Add(text.GetString(r, o, len));
                        o += len;
                    }
                    else
                        row.Add(null);
                    break;
            }
        }
        return row;
    }
}
