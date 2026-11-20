"""Block code: the networks of FB/FC/OB (samples 026-028).

Objects (relation names from the metamodel):
  block --Sources--> CompileUnitData            one per network (ICompileUnitData.ProgrammingLanguage SCL | LAD_CLASSIC |
                                                FBD_CLASSIC; an SCL block has one unit holding the whole source)
      ICoreAttributes.Comment                   network title (CoreText layout, per culture)
      --CompileUnitComment--> CoreText          network comment (ICoreTextRepository.Text, same layout)
      ICompileUnitData.Data                     blob (attrs.decode_blob) with UTF-8-BOM XML:
          <SCLSource>   token tree of the SCL editor: <Symbols> and <RootStatements>
          <FlgNet>      LAD/FBD network: <Parts> (Part Gate=.., ORef = operand reference), <Wires>
  block --CoreObject2IdentContainer--> IdentContainerData --IdentParts--> IdentPartData
      IIdentPartData.PayLoad                    blob with <IdentXmlPart><InterfaceAccess> entries: <ID RID=..> <CS><C NID=network
                                                RefID UID=part UId/></CS>, <SSD S=section MID=member id><AOS><AO N=name/>...
                                                (operand names of LAD/FBD networks; ORef RefId = ID@RID)
The compiled program (LoadablePlusCodeBlockData, LoadableDependentData) is separate and not read here.

SCL token tree -> text: LC line comment (TE = text after //), NL newline, BL blanks (NumBLs, default 1), SymVa/SymPa/Sub
operand text (ODN), Const (TE), Op*/Kw* operators and keywords (KEYWORDS/OPERATORS: verified ones only), FiSt ';', Comma ',', BracO '(', BracC ')'. A Statement
may carry its leading keyword in TE (IF, FOR). Operators are only mapped when verified against a sample; any other
token is rendered as <Tag> and listed in "unknownTokens".
"""
import xml.etree.ElementTree as ET

from . import attrs

# token tag -> text. Every entry was verified against ground truth (samples 026, 030; tools/scl_cov.py); a tag that is not
# listed is rendered as <Tag> and reported under "unknownTokens" (never guessed).
OPERATORS = {
    "OpAs": ":=", "OpPlAs": "+=", "OpMinAs": "-=", "OpMulAs": "*=", "OpDivAs": "/=", "OpPa": "=>",
    "OpPl": "+", "OpMi": "-", "OpMu": "*", "OpDi": "/", "OpMOD": "MOD", "OpPo": "**",
    "OpNOT": "NOT", "OpAND": "AND", "OpOR": "OR", "OpXOR": "XOR", "OpAAND": "&",
    "OpRef": "REF", "OpDeref": "^", "OpE": "=", "OpU": "<>", "OpG": ">", "OpL": "<", "OpGE": ">=", "OpLE": "<=",
}
KEYWORDS = {"KwTHEN": "THEN", "KwELSIF": "ELSIF", "KwELSE": "ELSE", "KwENDIF": "END_IF", "KwDO": "DO", "KwTO": "TO",
            "KwENDFOR": "END_FOR", "KwBY": "BY", "KwOF": "OF", "KwENDC": "END_CASE", "KwENDW": "END_WHILE",
            "KwENDR": "END_REPEAT", "KwUNTIL": "UNTIL", "KwEndRegion": "END_REGION"}
PUNCTUATION = {"FiSt": ";", "Comma": ",", "BracO": "(", "BracC": ")", "Dot": ".", "BoxO": "[", "BoxC": "]",
               "Colon": ":", "LDots": "..", "BCE": "*)"}
# tags that render an attribute: tag -> attribute
# structural tags: no text of their own, children are rendered in order (SI attribute = kind of construct)
CONTAINERS = {"Statement", "Statements", "Expression", "Fold", "InstCa", "Param", "BC", "CaseElem", "CaseRange", "Label",
              "SymVaP", "RootStatements", "FctCa", "CaseSRange"}
ATTRIBUTE_TEXT = {"LC": ("//", "TE"), "BCL": ("", "TE"), "SymVa": ("", "ODN"), "SymPa": ("", "ODN"), "Sub": ("", "ODN"),
                  "SymLa": ("", "ODN"), "Const": ("", "TE")}


def _local(tag):
    return tag.rsplit("}", 1)[-1]


def _xml(blob):
    """XML root of a blob value (pages in store records are resolved through the StoredBlob, attrs.decode_blob)."""
    if not isinstance(blob, bytes) or not blob:
        return None
    b = attrs.decode_blob(blob)
    return ET.fromstring(b.decode("utf-8-sig")) if b else None


def scl_text(root):
    """(text, unknown tokens) of an <SCLSource> element."""
    out, unknown = [], set()

    def walk(el):
        t = _local(el.tag)
        if t == "NL":
            out.append(chr(10))
        elif t == "BL":
            out.append(" " * int(el.get("NumBLs", "1")))
        elif t in ATTRIBUTE_TEXT:
            pre, att = ATTRIBUTE_TEXT[t]
            out.append(pre + (el.get(att) or (el.get("ODN", "") if t == "Const" else "")))  # Const SI=ConstType: data type name in ODN
        elif t == "AdMod" and el.get("SI") == "MoP":
            out.append(":P")  # peripheral access modifier
        elif t in OPERATORS:
            out.append(OPERATORS[t])
        elif t in KEYWORDS:
            out.append(KEYWORDS[t])
        elif t in PUNCTUATION:
            out.append(PUNCTUATION[t])
        elif len(el) == 0 and t not in ("Statements", "Fold"):
            unknown.add(t)
            out.append("<%s>" % t)
        else:
            if t not in CONTAINERS and t not in ("Statements", "Fold"):
                unknown.add(t)
            if t == "BC":
                out.append("(*")
            elif t in ("Label", "SymVaP"):  # name, then the Colon / AdMod child
                out.append(el.get("ODN", ""))
            if el.get("TE") and t == "Statement":
                out.append(el.get("TE"))
            for c in el:
                walk(c)

    rs = next((c for c in root if _local(c.tag) == "RootStatements"), None)
    if rs is not None:
        for c in rs:
            walk(c)
    return "".join(out).strip("\n"), sorted(unknown)


def token_status(tag):
    """How the SCL renderer treats a tag: text (rendered from a table or attribute), container, special or None."""
    if tag in OPERATORS or tag in KEYWORDS or tag in PUNCTUATION:
        return "text"
    if tag in ATTRIBUTE_TEXT:
        return "attribute"
    if tag in CONTAINERS:
        return "container"
    if tag in ("NL", "BL", "AdMod", "Statements", "Fold"):
        return "special"
    return None


def tag_counts(root, into):
    """Counts every tag below <RootStatements> (symbol table excluded) into the dict `into`."""
    rs = next((c for c in root if _local(c.tag) == "RootStatements"), None)
    for el in (rs.iter() if rs is not None else []):
        into[_local(el.tag)] = into.get(_local(el.tag), 0) + 1


# ---- ident table (IdentXmlPart) -------------------------------------------------------------------------------------
# Every child of <IdentXmlPart> is one resolvable object, addressed by ID@RID (unique per block). FlgNet elements refer to it:
# ORef/LRef/CRef/Instance/CodeBlock@RefId. The kinds handled are listed in IDENT_KINDS; the inventory test fails when a sample
# contains a child tag that is not listed.
IDENT_KINDS = {
    "InterfaceAccess": "variable of the block interface (SSD@S section, @MID member id, AOS/AO path)",
    "MultInstAccess": "multi-instance (static variable of an FB type)",
    "SimpleAccess": "global tag (ID@N, SAD@R = Input/Output/Memory area)",
    "GlobalAccess": "member of a data block (AOS/AO path: DB name, members), BAD@BIRID = interface info of the DB",
    "LocalConstant": "constant of the block interface (LCD@MID, CB@V value)",
    "GlobalConstant": "user constant of the tag table (CB@V value)",
    "LiteralConstant": "literal (CB@T type, CB@SV literal text, CB@V value)",
    "Label": "jump label (ID@N, LD@LID; C@AK Jump | Definition)",
    "Expression": "equation of a Calculate box (ED@SU, ED@CV)",
    "Instruction": "instruction box (ID@N = name, INSTD, TPI/TP template parameters, LPI/LP pins, IUS/IU/TV template values)",
    "FCBlock": "called/used FC",
    "FBBlock": "called/used FB",
    "AufDBBlock": "instance DB of a call (ID@N)",
    "DepDBBlock": "data block used by an access",
    "BlockInterfaceInfo": "interface snapshot of a called block (BIID, BPIL/BPI = parameters N, S section, A, R, F)",
}


def _unescape(s):
    """XML name escapes of the ident table (_x0030_ = '0')."""
    import re
    return re.sub(r"_x([0-9A-Fa-f]{4})_", lambda m: chr(int(m.group(1), 16)), s or "")


def _first(el, tag):
    if el is None:
        return None
    return next((c for c in el if _local(c.tag) == tag), None)


# FlgNet elements and attributes evaluated by Code.flgnet/flg_part (the test fails when a sample contains anything else)
FLG_HANDLED = {
    "FlgNet": {"Version", "Lang", "Routed"}, "Parts": set(), "Wires": set(), "Labels": set(), "Equation": {"Valid"},
    "Part": {"UId", "Gate", "DisableENO"}, "TemplateValue": {"Name", "Type"}, "Negated": {"PinName"}, "AutomaticTyped": {"Name"},
    "ORef": {"UId", "RefId"}, "LRef": {"UId", "RefId", "EN", "ENO", "DisableENO"}, "CRef": {"UId", "RefId"}, "Instance": {"UId", "RefId"},
    "CodeBlock": {"UId", "RefId"}, "Wire": {"UId"}, "Powerrail": set(), "PCon": {"UId", "PinName"}, "OCon": {"UId"},
}
# ident children read (value used in the output) / seen but only layout, time stamps or compiler metadata (not evaluated)
IDENT_READ = {"ID", "CS", "C", "OD", "TD", "SSD", "AOS", "AO", "CB", "SAD", "INSTD", "IUS", "IU", "TV", "LPI", "LP", "BPIL", "BPI", "TOD", "BIID"}


class Idents:
    """RID -> entry of the ident table of one block."""

    def __init__(self, code, block):
        self.by_rid, self.unknown = {}, set()
        ic = code.m.rel1(block, "CoreObject2IdentContainer") or next(iter(code.untagged(block, "IdentContainerData")), None)
        for part in code.linked(ic, "IdentParts", "IdentPartData") if ic else []:
            root = _xml(code.p.attr(part, "IIdentPartData", "PayLoad"))
            for el in list(root) if root is not None else []:
                self.add(el)

    def add(self, el):
        t = _local(el.tag)
        if t not in IDENT_KINDS:
            self.unknown.add(t)
        idel = _first(el, "ID")
        if idel is None:
            return
        e = {"kind": t, "rid": int(idel.get("RID")), "scope": idel.get("S"), "name": _unescape(idel.get("N")), "uses": {}}
        cs = _first(idel, "CS")
        for c in list(cs) if cs is not None else []:
            e["uses"][(int(c.get("NID")), int(c.get("UID")))] = c.get("AK") or "Read"
        td = _first(_first(el, "OD"), "TD")
        if td is not None and td.get("T"):
            e["type"] = td.get("T").split(":")[-1]
            e["typeKey"] = td.get("T")
        ssd = _first(el, "SSD")
        if ssd is not None:
            aos = _first(ssd, "AOS")
            aos_l = [a for a in (list(aos) if aos is not None else []) if _local(a.tag) == "AO"]
            e["path"] = [_unescape(a.get("N")) for a in aos_l]
            e["indexRefs"] = [[int(x) for x in (a.get("RIDI") or "").split(",") if x.strip().isdigit()] for a in aos_l]
            e["slice"] = ssd.get("AM")
            e["section"] = ssd.get("S")
            e["memberId"] = ssd.get("MID")
            if t in ("InterfaceAccess", "MultInstAccess", "GlobalAccess") and e["path"]:
                e["name"] = ".".join(e["path"])
        cb = next((c for c in el.iter() if _local(c.tag) == "CB"), None)
        if cb is not None:
            e["constant"] = {"type": cb.get("T"), "value": cb.get("V"), "text": _unescape(cb.get("SV")), "kind": cb.get("ST")}
        sad = _first(el, "SAD")
        if sad is not None:
            e["area"] = sad.get("R")
            e["periphery"] = sad.get("SAM") == "Peripherie"
            if ssd is not None and (ssd.get("AO") or "").lstrip("-").isdigit():
                e["bitOffset"] = int(ssd.get("AO"))
            od = _first(el, "OD")
            if od is not None and (od.get("S") or "").isdigit():
                e["size"] = int(od.get("S"))
        if t == "Instruction":
            instd = _first(el, "INSTD")
            e["templateUses"] = {}
            ius = _first(instd, "IUS")
            for iu in list(ius) if ius is not None else []:
                e["templateUses"][(int(iu.get("NId")), int(iu.get("UId")))] = {
                    tv.get("N"): tv.get("V") for tv in iu if _local(tv.tag) == "TV"}
            lpi = _first(instd, "LPI")
            e["pins"] = [{"name": lp.get("N"), "section": lp.get("S"), "type": lp.get("TN") or lp.get("T")}
                         for lp in (list(lpi) if lpi is not None else [])]
        if t == "BlockInterfaceInfo":
            bpil = _first(el, "BPIL")
            if bpil is None:
                bpil = _first(_first(el, "BIID"), "BPIL")
            e["parameters"] = [{"name": b.get("N"), "section": b.get("S")} for b in (list(bpil) if bpil is not None else [])]
        if t in ("FCBlock", "FBBlock", "AufDBBlock", "DepDBBlock"):
            tod = _first(el, "TOD")
            e["blockNumber"] = tod.get("N") if tod is not None else None
            e["blockType"] = tod.get("BT") if tod is not None else None
        self.by_rid[e["rid"]] = e

    def get(self, rid):
        return self.by_rid.get(int(rid)) if rid is not None else None

    def text(self, e, depth=0):
        """Operand text: path with array indices in brackets (indices are other ident entries) and slice access (.%X3)."""
        if e is None:
            return ""
        if e["kind"] == "LiteralConstant" and e.get("constant"):
            return e["constant"]["text"]
        if not e["name"] and e.get("area") and e.get("bitOffset") is not None:
            return address_text(e["area"], e["bitOffset"], e.get("size"), e.get("periphery"))
        if not e.get("path") or depth > 6:
            return e["name"]
        parts = []
        for name, refs in zip(e["path"], e.get("indexRefs") or [[] for _ in e["path"]]):
            idx = ",".join(self.text(self.get(r), depth + 1) for r in refs)
            parts.append(name + ("[%s]" % idx if refs else ""))
        out = ".".join(parts)
        if e.get("slice"):
            out += ".%" + e["slice"][0].upper() + e["slice"][1:]
        return out

    def operand(self, e, net, uid):
        """Readable operand of an ident entry used by part uid of network net."""
        if e is None:
            return None
        o = {"identKind": e["kind"], "name": self.text(e), "scope": e["scope"], "access": e["uses"].get((net, uid), "Read")}
        for k in ("type", "section", "memberId", "area", "constant"):
            if e.get(k) is not None:
                o[k] = e[k]
        if e.get("area") and e.get("bitOffset") is not None:  # absolute address (also behind a tag name)
            o["address"] = address_text(e["area"], e["bitOffset"], e.get("size"), e.get("periphery"))
        return o


AREA_LETTER = {"Memory": "M", "Input": "I", "Output": "Q", "Data": "DB", "XData": "DI"}


def address_text(area, bit_offset, size, periphery=False):
    """Absolute address as TIA prints it: %M10.3, %MW2, %IW0:P, T1, C5 (bit offset = byte * 8 + bit; Timer/Counter: number)."""
    if area == "Timer":
        return "T%d" % bit_offset
    if area == "Counter":
        return "C%d" % bit_offset
    letter = AREA_LETTER.get(area, area)
    if size in (None, 1):
        txt = "%%%s%s%d.%d" % (letter, "X" if letter in ("DB", "DI") else "", bit_offset // 8, bit_offset % 8)
    else:
        txt = "%%%s%s%d" % (letter, {8: "B", 16: "W", 32: "D", 64: "L"}.get(size, "?"), bit_offset // 8)
    return txt + (":P" if periphery else "")


def operand_label(x):
    """Short operand text of a network part: variable path, tag, DB path, 'const:<literal>' for constants."""
    if x.get("identKind") == "LiteralConstant" and x.get("constant"):
        return "const:" + x["constant"]["text"]
    if x.get("identKind") in ("LocalConstant", "GlobalConstant"):
        return "const:" + x["name"]
    if x.get("identKind") == "Label":
        return "label:" + x["name"]
    return x.get("name") or "refId %s" % x.get("refId")


def stl_operand(idents, ref_id):
    """STL operand as the STL source export writes it: #local, "global" (also tags TIA created for absolute addresses),
    literal, jump label, %DB100 for a data block without symbol."""
    e = idents.get(ref_id)
    if e is None:
        return "<refId %s>" % ref_id
    kind = e["kind"]
    text = idents.text(e) or ""
    if kind in ("InterfaceAccess", "MultInstAccess", "LocalConstant"):
        return "#" + text
    if kind == "SimpleAccess" and e["name"]:
        return '"' + text + '"'
    if kind == "GlobalAccess":
        return ".".join('"' + s + '"' if i == 0 else s for i, s in enumerate(text.split(".")))
    if kind in ("FCBlock", "FBBlock", "AufDBBlock", "DepDBBlock") and not e["name"]:
        return "%%%s%s" % (e.get("blockType") or "", e.get("blockNumber") or "")
    if kind in ("GlobalConstant", "FCBlock", "FBBlock", "AufDBBlock", "DepDBBlock"):
        return '"' + text + '"'
    return text


def stl_text(root, idents):
    """(text, unknown elements) of an STL <Statements> network in the layout of the STL source export (.awl): one statement
    per line, 6 blanks indent, "label:" in front, tokens and operands separated by blanks (",", "[" and "]" stick to what
    is before), ";", "//comment". A CALL with parameters (CallInfo / ParaExpression FPNum = parameter of the called block's
    interface) is followed by the parameter list. Editor spacing (NumBLs) is not used."""
    lines, unknown = [], set()
    for st in root:
        if _local(st.tag) != "Statement":
            unknown.add(_local(st.tag))
            continue
        label = comment = call = None
        body = []

        def word(w):
            body.append((" " if body else "") + w)

        for c in st:
            t = _local(c.tag)
            if t == "Label":
                lo = next((x for x in c if _local(x.tag) == "OpdAccess"), None)
                label = stl_operand(idents, lo.get("RefId")) if lo is not None else ""
                for x in c:
                    if _local(x.tag) not in ("OpdAccess", "Token"):
                        unknown.add("Label/" + _local(x.tag))
            elif t == "Token" and c.get("DispName") is None and c.get("Kw") in (",", "[", "]"):
                body.append(c.get("Kw"))
            elif t == "Token":
                word(c.get("DispName") if c.get("DispName") is not None else "<Kw %s>" % c.get("Kw"))
            elif t == "UserToken":
                word(c.get("DispName") or "")
            elif t == "OpdAccess":
                word(stl_operand(idents, c.get("RefId")))
            elif t == "LC":
                comment = c.get("DispName") or ""
            elif t == "CallInfo":
                pars = (idents.get(c.get("RefId")) or {}).get("parameters") or []
                call = []
                for pe in c:
                    if _local(pe.tag) != "ParaExpression":
                        unknown.add("CallInfo/" + _local(pe.tag))
                        continue
                    no = int(pe.get("FPNum") or 0)
                    pname = (pars[no - 1]["name"] or "") if 1 <= no <= len(pars) else "<FPNum %d>" % no
                    vals = []
                    for v in pe:
                        vt = _local(v.tag)
                        if vt == "OpdAccess":
                            vals.append(stl_operand(idents, v.get("RefId")))
                        elif vt == "UserToken":
                            vals.append(v.get("DispName") or "")
                        else:
                            vals.append("<%s>" % vt)
                            unknown.add("ParaExpression/" + vt)
                    call.append(pname.ljust(28) + ":= " + " ".join(vals))
            else:
                unknown.add(t)
        b = "".join(body)
        if not b and label is None and comment is None:
            lines.append("")
            continue
        o = (label + ":" if label is not None else "") + "      "
        if b:
            o += b + ("" if call else " ;" if comment is not None else ";")
        if comment is not None:
            o += "//" + comment
        lines.append(o)
        if call:
            for i, x in enumerate(call):
                lines.append(("      (  " if i == 0 else "         ") + x + (" , " if i < len(call) - 1 else ""))
            lines.append("      );")
    return "\n".join(lines), sorted(unknown)


class Code:
    def __init__(self, model):
        self.m, self.p = model, model.p

    def idents(self, block):
        return Idents(self, block)

    def untagged(self, key, type_name):
        """Targets of the untagged links (typed link list, relation implicit by slot) of key that are of type_name. Large blocks
        (FB_StlMath of sample 064 with 69 networks) keep Sources and the ident container this way."""
        return [l.key for l in self.p.links(key) if l.relation is None and l.key in self.p.objects and self.p.is_a(l.key, type_name)]

    def linked(self, key, relation, type_name):
        """Targets of relation, or the untagged links to objects of type_name when the relation has none."""
        return self.p.targets(key, relation) or self.untagged(key, type_name)

    def networks(self, block):
        units = [(self.p.attr(k, "ICompileUnitData", "RefID"), k) for k in self.linked(block, "Sources", "CompileUnitData")]
        idents = self.idents(block)
        out = []
        for i, (ref, k) in enumerate(units):
            cu = self.p.attributes(k)[0].get("ICompileUnitData", {})
            lang = cu.get("ProgrammingLanguage")
            title = {c: t for c, t in self.m.comment(k).items() if t}
            ct = self.m.rel1(k, "CompileUnitComment")
            comment = self.m.core_text_of(self.p.attr(ct, "ICoreTextRepository", "Text")) if ct else {}
            comment = {c: t for c, t in comment.items() if t}  # the language-neutral entry (65535) is empty
            n = {"index": i + 1, "language": {"LAD_CLASSIC": "LAD", "FBD_CLASSIC": "FBD"}.get(lang, lang),
                 "title": title, "comment": comment, "key": k}
            root = _xml(cu.get("Data"))
            if root is None:
                data = cu.get("Data")
                if isinstance(data, bytes) and b"<" not in data:  # header only: a network without elements (new OB Main)
                    n["empty"] = True
                else:
                    n["error"] = "no network source"
            elif _local(root.tag) == "SCLSource":
                n["scl"], unknown = scl_text(root)
                if unknown:
                    n["unknownTokens"] = unknown
            elif _local(root.tag) == "FlgNet":
                n.update(self.flgnet(root, ref, idents))
            elif _local(root.tag) == "Statements":
                n["stl"], unknown = stl_text(root, idents)
                if unknown:
                    n["unknownTokens"] = unknown
            elif _local(root.tag) == "EncryptedData":
                n["encrypted"] = True
            else:
                n["error"] = "unknown network source <%s>" % _local(root.tag)
            out.append(n)
        return out

    def flgnet(self, root, refid, idents):
        """Parts and wires of a LAD/FBD network. Part kinds: part (Gate), instruction (LRef), call (CRef), operand (ORef with RefId),
        open (ORef without RefId). Elements of the FlgNet that are not handled are listed under "unhandled"."""
        parts, wires, unhandled, labels = [], [], [], []
        for sec in root:
            t = _local(sec.tag)
            if t == "Parts":
                for el in sec:
                    parts.append(self.flg_part(el, refid, idents, unhandled))
            elif t == "Labels":
                for lab in sec:
                    o = idents.operand(idents.get(lab.get("RefId")), refid, int(lab.get("UId")))
                    labels.append({"uid": int(lab.get("UId")), "name": o["name"] if o else None})
            elif t == "Wires":
                for w in sec:
                    ends = []
                    for c in w:
                        ct = _local(c.tag)
                        if ct == "Powerrail":
                            ends.append({"powerrail": True})
                        elif ct == "OCon":
                            ends.append({"operand": int(c.get("UId"))})
                        elif ct == "PCon":
                            ends.append({"part": int(c.get("UId")), "pin": c.get("PinName")})
                        else:
                            unhandled.append("Wire/" + ct)
                            ends.append({ct: dict(c.attrib)})
                    wires.append({"uid": int(w.get("UId")), "ends": ends})
            else:
                unhandled.append(t)
        byuid = {x["uid"]: x for x in parts}
        for w in wires:  # readable endpoints: operand names, part names
            for i, e in enumerate(w["ends"]):
                if "operand" in e:
                    x = byuid.get(e["operand"], {})
                    w["ends"][i] = {"open": True} if x.get("kind") == "open" else {"operand": operand_label(x)}
                elif "part" in e:
                    e["name"] = byuid.get(e["part"], {}).get("name")
        res = {"parts": parts, "wires": wires}
        if labels:
            res["labels"] = labels
        if unhandled:
            res["unhandled"] = sorted(set(unhandled))
        if idents.unknown:
            res["unknownIdentKinds"] = sorted(idents.unknown)
        return res

    def flg_part(self, el, refid, idents, unhandled):
        t = _local(el.tag)
        uid = int(el.get("UId"))
        ref = el.get("RefId")
        if t == "Part":
            e = {"uid": uid, "kind": "part", "name": el.get("Gate")}
            for x in el:
                xt = _local(x.tag)
                if xt == "TemplateValue":
                    e.setdefault("templateValues", {})[x.get("Name")] = x.text
                elif xt == "Negated":
                    e.setdefault("negated", []).append(x.get("PinName"))
                elif xt == "AutomaticTyped":
                    e.setdefault("automaticTyped", []).append(x.get("Name"))
                elif xt == "Equation":
                    e["equation"] = (x.text or "").strip()
                    e["equationValid"] = x.get("Valid") == "true"
                else:
                    unhandled.append("Part/" + xt)
            if el.get("DisableENO"):
                e["disableEno"] = el.get("DisableENO") == "true"
            return e
        if t == "ORef":
            if ref is None:
                return {"uid": uid, "kind": "open"}
            o = idents.operand(idents.get(ref), refid, uid)
            e = {"uid": uid, "kind": "operand", "refId": int(ref)}
            if o:
                e.update(o)
            return e
        if t == "LRef":
            ins = idents.get(ref) or {}
            e = {"uid": uid, "kind": "instruction", "name": ins.get("name"), "refId": int(ref), "en": el.get("EN") == "true",
                 "eno": el.get("ENO") == "true"}
            if el.get("DisableENO") == "true":
                e["disableEno"] = True
            tv = ins.get("templateUses", {}).get((refid, uid))
            if tv:
                e["templateValues"] = tv
            for x in el:
                if _local(x.tag) == "Instance":
                    e["instance"] = idents.operand(idents.get(x.get("RefId")), refid, int(x.get("UId")))
                else:
                    unhandled.append("LRef/" + _local(x.tag))
            return e
        if t == "CRef":
            bii = idents.get(ref) or {}
            e = {"uid": uid, "kind": "call", "refId": int(ref), "parameters": bii.get("parameters", [])}
            for x in el:
                xt = _local(x.tag)
                if xt == "CodeBlock":
                    blk = idents.get(x.get("RefId")) or {}
                    e["name"] = blk.get("name")
                    e["blockType"] = blk.get("blockType")
                elif xt == "Instance":
                    e["instance"] = idents.operand(idents.get(x.get("RefId")), refid, int(x.get("UId")))
                else:
                    unhandled.append("CRef/" + xt)
            return e
        unhandled.append(t)
        return {"uid": uid, "kind": t, "attributes": dict(el.attrib)}
