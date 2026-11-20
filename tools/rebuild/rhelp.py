"""Shared helpers for the recipe modules recipes_*.py (MCP step builders, block truth generators)."""
import json
import os

from truth import _raw, _w

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(ROOT, "tools", "sources")
CPU1513 = "OrderNumber:6ES7 513-1AL02-0AB0/V2.9"
TT = {"objectKind": "tagTable"}


def src(*p):
    return os.path.join(SRC, *p)


def plc(op, kind, **a):
    return ("TiaPlcSoftware", dict(operation=op, objectKind=kind, **a))


def tag(op, **a):
    return plc(op, "tagTable", name="TagTable_1", **a)


# --- block truth generators (samples 023-029) ---------------------------------------------------------------------
def _strip(txt):
    d = json.loads(txt)
    for k in ("durationMs", "queueMs", "defaulted", "source"):
        d.pop(k, None)
    return d


def gen_block_get(detail):
    def g(ctx, out, rel):
        name = rel[:-len(".get.json")]
        d = _strip(_raw(ctx, "TiaPlcSoftware", operation="get", objectKind="block", name=name, detail=detail, refresh="true"))
        if "items" in d and len(d["items"]) == 1:
            item = d["items"][0]
            d = item
        os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, rel), "w", encoding="utf-8") as f:
            f.write(json.dumps(d, ensure_ascii=False, separators=(",", ":")))
    return g


def gen_block_xml(ctx, out, rel):
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="block", name=rel[:-4], targetPath=os.path.join(out, rel))


def gen_sd_pair(ctx, out, rel):
    """<name>.s7dcl and <name>.s7res: SD export of TIA (ExportAsDocuments)."""
    name = rel.rsplit(".", 1)[0]
    if rel.endswith(".s7res") and os.path.exists(os.path.join(out, name + ".s7dcl")):
        return
    _raw(ctx, "TiaPlcSoftware", operation="documentExport", objectKind="block", name=name, targetPath=out)


def gen_sd_txt(ctx, out, rel):
    d = json.loads(_raw(ctx, "TiaPlcSoftware", operation="documentGet", objectKind="block", name=rel[:-len(".sd.txt")]))
    with open(os.path.join(out, rel), "w", encoding="utf-8", newline="") as f:
        f.write(d["source"])


def block_truth(detail):
    return [(r"[^/]+\.get\.json", gen_block_get(detail)), (r"[^/]+\.(s7dcl|s7res)", gen_sd_pair),
            (r"[^/]+\.sd\.txt", gen_sd_txt), (r"[^/]+\.xml", gen_block_xml)]


def chg(tool, **a):
    """Change call recorded for change.json (tool, operation, arguments without operation/path, parsed result)."""
    def step(ctx):
        args = {k: v for k, v in a.items() if v is not None}
        txt = ctx.call(tool, **args)
        try:
            res = json.loads(txt)
        except ValueError:
            res = txt
        if not hasattr(ctx, "changes"):
            ctx.changes = []
        ctx.changes.append({"tool": tool, "operation": args.get("operation"),
                            "arguments": {k: v for k, v in args.items() if k not in ("operation", "path")}, "result": res})
    return step


def pause(note, timeout_s=4 * 3600):
    """Step for samples made by hand in the TIA user interface: writes <stage>\\work\\<sample>.pause with `note` and waits
    until that file is deleted (the change is done in TIA meanwhile, e.g. via VNC; do not save in TIA unless the note
    says so)."""
    def step(ctx):
        import time
        work = os.path.join(ctx.stage, "work")
        os.makedirs(work, exist_ok=True)
        flag = os.path.join(work, ctx.name + ".pause")
        with open(flag, "w", encoding="utf-8") as f:
            f.write(note + "\n")
        print("PAUSE %s: %s (delete %s to continue)" % (ctx.name, note, flag), flush=True)
        t0 = time.time()
        while os.path.exists(flag):
            if time.time() - t0 > timeout_s:
                raise RuntimeError("manual step not finished: " + note)
            time.sleep(2)
        ctx.log.append({"manual": note})
    return step


# --- XLSX import files (alarm texts, text lists, supervisions) --------------------------------------------------------
def xlsx_edit(src_path, dst_path, cells, sheet=1):
    """Copy of the XLSX export `src_path` with `cells` ({"D2": "text"}) set as inline strings in worksheet `sheet`
    (missing rows/cells are added). TIA writes the prefix x: for the spreadsheetml namespace."""
    import re
    import zipfile
    from xml.sax.saxutils import escape
    name = "xl/worksheets/sheet%d.xml" % sheet
    with zipfile.ZipFile(src_path) as z:
        parts = [(i, z.read(i.filename)) for i in z.infolist()]
    out = []
    for info, data in parts:
        if info.filename == name:
            xml = data.decode("utf-8-sig")
            for ref, text in cells.items():
                col, row = re.match(r"([A-Z]+)(\d+)$", ref).groups()
                cell = '<x:c r="%s" t="inlineStr"><x:is><x:t xml:space="preserve">%s</x:t></x:is></x:c>' % (ref, escape(text))
                m = re.search(r'<x:c r="%s"[^>]*?(/>|>.*?</x:c>)' % ref, xml)
                if m:
                    xml = xml[:m.start()] + cell + xml[m.end():]
                    continue
                rm = re.search(r'<x:row r="%s"[^>]*>(.*?)</x:row>' % row, xml)
                if rm:
                    body = rm.group(1)
                    pos = len(body)  # insert in column order
                    for cm in re.finditer(r'<x:c r="([A-Z]+)\d+"', body):
                        c = cm.group(1)
                        if (len(c), c) > (len(col), col):
                            pos = cm.start()
                            break
                    body = body[:pos] + cell + body[pos:]
                    xml = xml[:rm.start(1)] + body + xml[rm.end(1):]
                else:
                    xml = xml.replace("</x:sheetData>", '<x:row r="%s">%s</x:row></x:sheetData>' % (row, cell))
            data = xml.encode("utf-8")
        out.append((info, data))
    with zipfile.ZipFile(dst_path, "w", zipfile.ZIP_DEFLATED) as z:
        for info, data in out:
            z.writestr(info, data)
    return dst_path


def xlsx_import(export_op, import_op, cells, fname, sheet=1):
    """Step: export the current XLSX (export_op), set `cells`, import it again (import_op, recorded for change.json)."""
    def step(ctx):
        work = os.path.join(ctx.stage, "work")
        os.makedirs(work, exist_ok=True)
        base = os.path.join(work, ctx.name + "_base.xlsx")
        if os.path.exists(base):
            os.remove(base)
        ctx.call("TiaPlcSoftware", operation=export_op, objectKind="block", targetPath=base)
        dst = os.path.join(work, fname)
        xlsx_edit(base, dst, cells, sheet)
        chg("TiaPlcSoftware", operation=import_op, objectKind="block", sourcePath=dst)(ctx)
    return step


# --- content transfer from the old sample ---------------------------------------------------------------------------
# Samples whose content came from long generator runs (tools\gen_lad.py, gen_xml.py ... over many iterations) have no
# replayable recipe. Their content is taken over object by object as SimaticML from the old archive: OLD\<sample>\ holds
# the retrieved old project, inv.json (lists) and export\<device>\<kind>\*.xml (bulk export), written by
# tools\rebuild\old_inventory.py. Imported are the objects that are new or whose export differs from the old
# predecessor; objects missing in the old sample are deleted. Inconsistent objects (not exportable) come from the SD
# export of the old truth (sd\<name>.s7dcl/.s7res) or from `sd_fallback`.
OLD = os.path.abspath(os.environ.get("FBR_OLD", os.path.join(os.sep, "Temp", "FBR", "_old")))  # absolute: the server rejects relative paths
SAMPLES = os.path.join(ROOT, "samples")


def _inv(name):
    p = os.path.join(OLD, name, "inv.json")
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def _items(inv, dn, kind):
    p = (inv or {}).get("plc", {}).get(dn, {}).get(kind, {})
    return {it["name"]: it for it in (p.get("items") or [])}


def _xml(name, dn, kind, obj):
    p = os.path.join(OLD, name, "export", dn, kind, obj + ".xml")
    return p if os.path.isfile(p) else None


def _same(a, b):
    if not a or not b:
        return False
    import re
    with open(a, "rb") as fa, open(b, "rb") as fb:
        rx = re.compile(rb"<Created>[^<]*</Created>")  # export timestamp
        return rx.sub(b"", fa.read()) == rx.sub(b"", fb.read())


_SRC_INDEX = None


def _src_sd(n, ext=".s7dcl"):
    """The input source <n><ext> of block n in tools\\sources (only when exactly one folder has it)."""
    global _SRC_INDEX
    if _SRC_INDEX is None:
        _SRC_INDEX = {}
        for dp, _, fns in os.walk(SRC):
            for fn in fns:
                if fn.endswith((".s7dcl", ".xml", ".awl", ".scl", ".db")):
                    _SRC_INDEX.setdefault(fn, []).append(os.path.join(dp, fn))
    c = _SRC_INDEX.get(n + ext, [])
    return c[0] if len(c) == 1 else None


def _old_sd(sample, dn, n):
    """SD of block n: the input source in tools\\sources when unique (keeps the operand order of the original
    write), else the SD export of the old truth (sd\\<n>.s7dcl or <device>\\blocks\\<n>.s7dcl), else None."""
    truth = None
    for p in (os.path.join(SAMPLES, sample + "_truth", "sd", n + ".s7dcl"),
              os.path.join(SAMPLES, sample + "_truth", dn, "blocks", n + ".s7dcl")):
        if os.path.isfile(p):
            truth = p
    sdj = os.path.join(SAMPLES, sample + "_truth", dn, "blocks", n + ".sd.json")
    if not truth and os.path.isfile(sdj):  # documentGet result of the old truth (source + resources)
        with open(sdj, encoding="utf-8-sig") as f:
            d = json.load(f)
        if d.get("source"):
            work = os.path.join(OLD, "_sd", sample, dn)
            os.makedirs(work, exist_ok=True)
            truth = os.path.join(work, n + ".s7dcl")
            with open(truth, "w", encoding="utf-8", newline="") as f:
                f.write(d["source"])
            res = truth[:-6] + ".s7res"
            if d.get("resources"):
                with open(res, "w", encoding="utf-8", newline="") as f:
                    f.write(d["resources"])
            elif os.path.exists(res):
                os.remove(res)
            return truth  # exact state of the old sample (e.g. after memberSet), not the input source
    s = _src_sd(n)
    # the input source only for the step 5g coverage samples (truth in sd\): their generator inputs are fixed, while
    # e.g. tools\gen_stress.py regenerates its sources
    if truth and s and os.sep + "sd" + os.sep in truth and _has_res(truth) == _has_res(s):
        return s
    return truth


def _has_res(sd):
    """True if the .s7res next to `sd` holds a text that the source uses (generator inputs may carry an unused one,
    e.g. typed_scl_scl\\FB_Scl_sl001; the SD export of such a block has no .s7res)."""
    import re
    res = sd[:-6] + ".s7res"
    if not os.path.isfile(res):
        return False
    with open(res, encoding="utf-8-sig") as f:
        ids = re.findall(r"id:\s*(\S+)", f.read())
    with open(sd, encoding="utf-8-sig") as f:
        text = f.read()
    return any(i in text for i in ids)


def _set_number(ctx, dn, n, number):
    ctx.call("TiaPlcSoftware", ok_errors=True, operation="attributeSet", objectKind="block", device=dn, name=n,
             attributes=json.dumps({"AutoNumber": False, "Number": number}))
    ctx.call("TiaPlcSoftware", ok_errors=True, operation="attributeSet", objectKind="block", device=dn, name=n,
             attributes=json.dumps({"AutoNumber": True}))


def _is_input(path):
    """True if `path` is an input source in tools\\sources (not an SD export of an old truth)."""
    return os.path.normcase(os.path.abspath(path)).startswith(os.path.normcase(SRC) + os.sep)


def _inline_written(truth_sd):
    """True if the SD export `truth_sd` shows a source written inline: the code lines after the first one of each SCL
    network are indented by 16 spaces or more (a source written as file comes back with 8)."""
    if not os.path.isfile(truth_sd):
        return False
    with open(truth_sd, encoding="utf-8-sig") as f:
        lines = f.read().replace("\r\n", "\n").split("\n")
    bodies, cur, prev = [], None, ""
    for ln in lines:
        if ln.strip() == "NETWORK":
            cur = [] if 'S7_Language := "SCL"' in prev else None
            if cur is not None:
                bodies.append(cur)
        elif cur is not None and ln.strip() and len(ln) - len(ln.lstrip(" ")) <= 4:
            cur = None  # end of the network (next attribute block, END_FUNCTION ...)
        elif cur is not None and ln.strip():
            cur.append(len(ln) - len(ln.lstrip(" ")))
        if ln.strip():
            prev = ln
    rest = [i for b in bodies for i in b[1:]]
    return bool(rest) and sum(i >= 16 for i in rest) >= 0.8 * len(rest)  # FC_SclVar: one line of tabs


def _declares_members(src_path, xml_path):
    """True if the external source declares every interface member of the old SimaticML export (block names are not
    unique across tools\\sources: e.g. DB_Arr of 075 (stl_calls3) has two of the six members of DB_Arr in 031/039)."""
    import re
    import xml.etree.ElementTree as ET
    if not xml_path:
        return True
    root = ET.parse(xml_path).getroot()
    if any(e.tag == "InstanceOfName" for e in root.iter()):
        return True  # instance DB: the interface comes from the FB
    members = set()
    secs = next((e for e in root.iter() if e.tag.endswith("}Sections")), None)
    for sec in (secs if secs is not None else []):  # top-level members only (no UDT/instance expansion)
        members.update(m.get("Name") for m in sec if m.tag.endswith("}Member") and m.get("Name") != "Ret_Val")
    with open(src_path, encoding="utf-8-sig", errors="replace") as f:
        words = {a or b for a, b in re.findall(r'"([^"]+)"|(\w+)', f.read())}
    return members <= words


def _xml_sig(path):
    """Content of a SimaticML block file that does not depend on the export: top-level interface members, instruction
    names, operands and constants, number of wires."""
    import xml.etree.ElementTree as ET
    root = ET.parse(path).getroot()

    def loc(e):
        return e.tag.rsplit("}", 1)[-1]
    members, parts, acc, wires = [], [], [], 0
    iface = next((e for e in root.iter() if loc(e) == "Interface"), None)
    secs = next((e for e in (iface if iface is not None else []) if loc(e) == "Sections"), None)
    for sec in (secs if secs is not None else []):
        members += [(m.get("Name"), (m.get("Datatype") or "").lower()) for m in sec if loc(m) == "Member"]
    for e in root.iter():
        t = loc(e)
        if t in ("Part", "Call"):
            parts.append((e.get("Name") or "").lower())
        elif t == "Component":
            acc.append(e.get("Name"))
        elif t in ("ConstantValue", "TemplateValue"):
            acc.append((e.text or "").strip().lower())
        elif t == "Wire":
            wires += 1
    return sorted(members), sorted(parts), sorted(a for a in acc if a), wires


def _xml_equiv(a, b):
    try:
        return _xml_sig(a) == _xml_sig(b)
    except Exception:  # noqa: BLE001
        return False


def _renamed_xml(path, name, number=None):
    """Copy of the SimaticML block file `path` with the block name `name` and the block number `number` (in
    OLD\\_xml). A generator input has no number: the block would take the lowest free number on import and a system
    block that comes with it the next one, while the original block got its number when other numbers were free."""
    import re
    work = os.path.join(OLD, "_xml")
    os.makedirs(work, exist_ok=True)
    with open(path, "rb") as f:
        data = f.read()
    m = re.search(rb"<Name>[^<]*</Name>(\s*<Namespace />)?", data)
    if not m:
        raise RuntimeError("no block name in " + path)
    head = b"<Name>" + name.encode("utf-8") + b"</Name>" + (m.group(1) or b"")
    rest = data[m.end():]
    if number:
        rest = re.sub(rb"^\s*<Number>\d+</Number>", b"", rest, count=1)
        head += b"\r\n      <Number>%d</Number>" % number
    data = data[:m.start()] + head + rest
    out = os.path.join(work, name + ".xml")
    with open(out, "wb") as f:
        f.write(data)
    return out


BLOCK_ORDER = {"OB": 2, "FB": 0, "FC": 1, "GlobalDB": 3, "ArrayDB": 3, "InstanceDB": 4}


def transfer(sample, pred=None, devices=None, sd_fallback=None, skip=(), delete=True, kinds=("type", "tagTable", "block", "technologyObject"),
             only=None, xml_only=(), input_xml=False, xml_as=None, compile_input=False):
    """Step: take over the PLC content of the old `sample` (see above). `pred`: old predecessor sample to diff against
    (default: the recipe predecessor); `only`: restrict to these object names; `xml_only`: blocks taken over as
    SimaticML although an input source exists; `sd_fallback`: {block: path of .s7dcl} for blocks without SimaticML export
    or whose original was written from SD although the old truth has no SD export; `input_xml`: import the SimaticML
    generator input of a block (tools\\sources) instead of the old export when both have the same content;
    `xml_as`: {block: SimaticML input of another block} imported under the name of the block; `compile_input`
    (True or block names): compile a block right after the import of its generator input, as the generator runs did (the instance of an
    instruction that came in its oldest version is then upgraded to the system block that compiled code already
    uses; a compile of the whole program later keeps the other, renamed block <name>_1)."""
    def step(ctx):
        new, old = _inv(sample), _inv(pred or ctx.rec.get("from") or "")
        if new is None:
            raise RuntimeError("no old inventory of %s in %s (run tools\\rebuild\\old_inventory.py)" % (sample, OLD))
        report = {"imported": [], "deleted": [], "sd": [], "missing": []}
        for dn in (devices or list(new["plc"])):
            for kind in kinds:
                cur, prev = _items(new, dn, kind), _items(old, dn, kind)
                todo = []
                for n, it in cur.items():
                    if n in skip or (only is not None and n not in only):
                        continue
                    x = _xml(sample, dn, kind, n)
                    if n in prev and prev[n].get("groupPath") == it.get("groupPath") and _same(x, _xml(pred or ctx.rec.get("from") or "", dn, kind, n)):
                        continue
                    if n in prev and old is not None and x is None and not _xml(pred or ctx.rec.get("from") or "", dn, kind, n) \
                            and {k: v for k, v in prev[n].items()} == {k: v for k, v in it.items()}:
                        continue  # unchanged and not exportable in both
                    todo.append((n, it, x))
                if kind == "block":
                    todo.sort(key=lambda t: (BLOCK_ORDER.get(t[1].get("blockType"), 5), t[1].get("number") or 0))
                groups = sorted({t[1].get("groupPath") for t in todo if t[1].get("groupPath")})
                for g in groups:
                    parts = g.split("/")
                    for i in range(len(parts)):
                        ctx.call("TiaPlcSoftware", ok_errors=True, operation="groupCreate", objectKind=kind, device=dn,
                                 name=parts[i], groupPath="/".join(parts[:i]) or None)
                def place(n, it, x, sd, xml_file):
                    if n in xml_only:
                        sd = None
                    src_xml = _src_sd(n, ".xml") if kind == "block" else None
                    from_input = False
                    if src_xml:  # created by SimaticML import in the original (FX_* blocks): SimaticML again, the
                        # old export first (the input file may be a later generator version), then the input file
                        sd = None
                        if x and (n in (xml_as or {}) or (input_xml and _xml_equiv(src_xml, x))):
                            src_xml = _renamed_xml((xml_as or {}).get(n) or src_xml, n, it.get("number"))
                            # the generator input itself (same content as the old export): the import of the old
                            # export gives other part uids and keeps the instruction versions of the export, while
                            # the input (no version) made TIA add the oldest instruction version and upgrade the
                            # instance, as in the original (second library type, other member id of the instance)
                            x, xml_file = src_xml, x
                            from_input = True
                        else:
                            x, xml_file = (x, src_xml) if x else (src_xml, src_xml)
                    if x and kind == "block" and sd and (os.path.isfile(sd[:-6] + ".s7res") or _is_input(sd)):
                        # LAD/FBD with text resources: written from SD in the original (keeps the MLC ids of the
                        # texts; a SimaticML import would generate new ones). Input sources in tools\sources (step 5g
                        # coverage samples) were written with documentWrite as well: a SimaticML import compiles to
                        # another block (e.g. SCL blocks some 40 bytes more load memory)
                        x = None
                    if x:
                        r = ctx.call("TiaPlcSoftware", ok_errors=kind == "block" and bool(sd or (xml_file and x != xml_file)),
                                     operation="import", objectKind=kind, device=dn, sourcePath=x,
                                     groupPath=it.get("groupPath") or None)
                        if ctx.log[-1]["error"] and xml_file and x != xml_file:  # rejected: the other XML
                            r = ctx.call("TiaPlcSoftware", ok_errors=bool(sd), operation="import", objectKind=kind,
                                         device=dn, sourcePath=xml_file, groupPath=it.get("groupPath") or None)
                        if not ctx.log[-1]["error"]:
                            if from_input and x == src_xml and (compile_input is True or n in (compile_input or ())):
                                ctx.call("TiaPlcSoftware", ok_errors=True, operation="compile", objectKind="block",
                                         device=dn, name=n)
                                ctx.log[-1]["error"] = False  # a compile error is no import error
                            # the input XML may carry another number; instance DBs of system FBs ignore it
                            if (src_xml or "DB" in (it.get("blockType") or "")) and it.get("number"):
                                _set_number(ctx, dn, n, it["number"])
                            report["imported"].append("%s/%s/%s" % (dn, kind, n))
                            return
                        # SimaticML exported WithDefaults is not always importable (e.g. TemplateValue Card of SUB):
                        # the block came from SD in the original, so write the SD export of the old truth
                        report.setdefault("import_failed", []).append("%s: %s" % (n, r[:200]))
                    if kind == "block" and sd and os.path.isfile(sd):
                        text = None
                        if _is_input(sd) and not os.path.isfile(sd[:-6] + ".s7res"):
                            # SCL without text resources: the original wrote some input sources inline (indentation
                            # of the code lines kept, see doc_write_indented) and others as file (dropped). The SD
                            # export of the old truth shows which; the text is part of the load memory
                            if _inline_written(os.path.join(SAMPLES, sample + "_truth", "sd", n + ".s7dcl")):
                                with open(sd, encoding="utf-8-sig") as f:
                                    text = f.read().replace("\r\n", "\n")
                        ctx.call("TiaPlcSoftware", ok_errors=bool(xml_file), operation="documentWrite", objectKind="block",
                                 device=dn, sourceFile=None if text else sd, source=text,
                                 groupPath=it.get("groupPath") or None, compileAfter="false")
                        if ctx.log[-1]["error"]:  # not writable as SD (e.g. absolute addresses): SimaticML after all
                            ctx.call("TiaPlcSoftware", operation="import", objectKind=kind, device=dn, sourcePath=xml_file,
                                     groupPath=it.get("groupPath") or None)
                            report["imported"].append("%s/%s/%s" % (dn, kind, n))
                            return
                        if it.get("number"):  # SD carries no block number: keep the one of the old sample
                            _set_number(ctx, dn, n, it["number"])
                        report["sd"].append("%s/%s" % (dn, n))
                    else:
                        report["missing"].append("%s/%s/%s" % (dn, kind, n))

                def source_import(n, it, src_ext):
                    ctx.call("TiaPlcSoftware", ok_errors=True, operation="sourceImport", objectKind="block", device=dn,
                             sourcePath=src_ext, groupPath=it.get("groupPath") or None)
                    if not ctx.log[-1]["error"] and "\"errorCount\":0" in ctx.log[-1]["result"].replace(" ", ""):
                        if it.get("number"):
                            _set_number(ctx, dn, n, it["number"])
                        report.setdefault("source", []).append("%s/%s" % (dn, n))
                        return True
                    return False

                deferred = []
                for n, it, x in todo:
                    sd = (sd_fallback or {}).get(n) or _old_sd(sample, dn, n)
                    xml_file = x
                    src_ext = None if kind != "block" or n in xml_only else \
                        (_src_sd(n, ".awl") or _src_sd(n, ".scl") or _src_sd(n, ".db"))
                    if src_ext and not _declares_members(src_ext, x):
                        src_ext = None  # an external source of another sample with the same block name
                    if src_ext:  # created by sourceImport of an external source (STL/SCL/DB) in the original
                        if source_import(n, it, src_ext):
                            continue
                        deferred.append((n, it, x, sd, xml_file, src_ext))  # may need blocks imported later
                        continue
                    place(n, it, x, sd, xml_file)
                for _ in range(3):  # sources that depend on each other: further passes
                    deferred = [d for d in deferred if not source_import(d[0], d[1], d[5])]
                for n, it, x, sd, xml_file, _src in deferred:
                    place(n, it, x, sd, xml_file)
                if delete and only is None:
                    for n, it in prev.items():
                        if n not in cur:
                            ctx.call("TiaPlcSoftware", operation="delete", objectKind=kind, device=dn, name=n,
                                     groupPath=it.get("groupPath") or None)
                            report["deleted"].append("%s/%s/%s" % (dn, kind, n))
        ctx.log.append({"transfer": report})
        if report["missing"]:
            raise RuntimeError("transfer %s: no source for %s" % (sample, report["missing"]))
    return step


def compile_plc(ctx):
    ctx.call("TiaPlcSoftware", operation="compile", objectKind="block")


def doc_write(kind, path, **a):
    return plc("documentWrite", kind, sourceFile=path, **a)


def doc_write_indented(kind, path, **a):
    """documentWrite with the source passed inline (`source`/`resources`) as for the original samples 023/024. Inline
    text keeps the indentation of the code lines after the first one of a network (8 spaces), `sourceFile` drops it."""
    def step(ctx):
        with open(path, encoding="utf-8-sig") as f:
            text = f.read().replace("\r\n", "\n")
        res = os.path.splitext(path)[0] + ".s7res"
        resources = open(res, encoding="utf-8-sig").read() if os.path.exists(res) else None
        ctx.call("TiaPlcSoftware", operation="documentWrite", objectKind=kind, source=text, resources=resources, **a)
    return step


FB_EMPTY_SD = '{\r\n    S7_EditorMode := "SCL";\r\n    S7_Optimized := "TRUE";\r\n    S7_Version := "0.1"\r\n}\r\nFUNCTION_BLOCK "FB_Empty"\r\nEND_FUNCTION_BLOCK'



PLACEHOLDER_DB = "\r\n".join(['{', '    S7_Optimized := "TRUE"', '}', 'DATA_BLOCK "%s"', '    VAR', '        x : Bool;',
                                '    END_VAR', 'END_DATA_BLOCK'])


def hold(*names, dn="PLC_1"):
    """Step: temporary global DBs that hold `names`, so that TIA names system blocks added meanwhile <name>_1 (as in
    the original, where these names were taken when the instructions were inserted). sys_blocks(release=...) deletes them."""
    def step(ctx):
        for n in names:
            ctx.call("TiaPlcSoftware", operation="documentWrite", objectKind="block", device=dn, source=PLACEHOLDER_DB % n,
                     compileAfter="false")
    return step


def sys_blocks(sample, carriers=(), release=(), dn="PLC_1", whole=True, single=False, fill=(), check=True, final=False):
    r"""Step: system blocks of the old `sample` that no block calls any more (the original called them from blocks that
    were deleted or replaced later; TIA keeps such system blocks). `carriers` (paths in tools\sources: SimaticML .xml,
    external source .scl, SIMATIC SD .s7dcl) call the instructions: they are added, compiled and deleted again. `release`: placeholder
    DBs of hold() to delete. `whole`: compile the whole program, else only the carriers (036: the technology objects
    stay uncompiled as in the original). System block numbers that differ from the old inventory are logged.

    Numbers: a new block takes the lowest free number (from 1); the system block that comes with its instruction then
    takes its library number (LEAD_LAG 80, DCAT 81, MCAT 82, IMC 83, SMC 84, DRUM 85 ...) if that is free, else the
    lowest free number from 0 (measured 2026-11-10). `single`: add, compile and delete the carriers one by one (each
    takes the same lowest free number, as the generator runs of the original deleted a block that did not compile
    before the next one); `fill`: FB numbers held by temporary empty FBs meanwhile; `check`: fail if a system block of
    the old sample is still missing (False for a step that runs before the content exists); `final`: fail if a
    system block number differs from the old sample."""
    def names(ctx):
        j = json.loads(ctx.call("TiaPlcSoftware", operation="list", objectKind="block", device=dn,
                                includeSystemBlocks="true"))
        return {b["name"]: b for b in j.get("systemBlocks") or []}

    def add(ctx, path):
        if path.endswith(".s7dcl"):  # SIMATIC SD source (with its .s7res)
            ctx.call("TiaPlcSoftware", ok_errors=single, operation="documentWrite", objectKind="block", device=dn,
                     sourceFile=path)
        else:
            op = "import" if path.endswith(".xml") else "sourceImport"
            ctx.call("TiaPlcSoftware", ok_errors=single, operation=op, objectKind="block", device=dn, sourcePath=path)
        return not ctx.log[-1]["error"]

    def step(ctx):
        inv = (_inv(sample) or {}).get("plc", {}).get(dn, {}).get("block", {})
        own = {b["name"] for b in inv.get("items") or []}
        added, holders = [], []
        for path in carriers:
            if os.path.splitext(os.path.basename(path))[0] in own:
                raise RuntimeError("sys_blocks %s: carrier %s is a block of the sample" % (sample, path))
        for number in fill:
            n = "FB_SysHold_%d" % number
            ctx.call("TiaPlcSoftware", operation="documentWrite", objectKind="block", device=dn,
                     source=FB_EMPTY_SD.replace("FB_Empty", n), compileAfter="false")
            ctx.call("TiaPlcSoftware", operation="attributeSet", objectKind="block", device=dn, name=n,
                     attributes=json.dumps({"AutoNumber": False, "Number": number}))
            holders.append(n)
        for path in carriers:
            n = os.path.splitext(os.path.basename(path))[0]
            if not single:
                add(ctx, path)
                added.append(n)
            elif add(ctx, path):
                ctx.call("TiaPlcSoftware", ok_errors=True, operation="compile", objectKind="block", device=dn, name=n)
                ctx.call("TiaPlcSoftware", operation="delete", objectKind="block", device=dn, name=n)
        for n in ([None] if whole else added):
            ctx.call("TiaPlcSoftware", ok_errors=True, operation="compile", objectKind="block", device=dn, name=n)
        for n in added + holders + list(release):
            ctx.call("TiaPlcSoftware", operation="delete", objectKind="block", device=dn, name=n)
        old = {b["name"]: b for b in inv.get("systemBlocks") or []}
        cur = names(ctx)
        # TiaPlcSoftware cannot address system blocks for attributeSet: the numbers follow from the block numbers
        # that are free when the carrier is added (see above)
        report = {"missing": sorted(set(old) - set(cur)), "extra": sorted(set(cur) - set(old)),
                  "otherNumber": ["%s %s (old %s)" % (n, cur[n].get("number"), b.get("number")) for n, b in old.items()
                                  if n in cur and cur[n].get("number") != b.get("number")]}
        ctx.log.append({"sys_blocks": report})
        if report["missing"] and check:
            raise RuntimeError("sys_blocks %s: still missing %s" % (sample, report["missing"]))
        if final and (report["extra"] or report["otherNumber"]):
            raise RuntimeError("sys_blocks %s: %s" % (sample, report))
    return step
