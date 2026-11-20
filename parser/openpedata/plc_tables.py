"""Watch and force tables, software units (GOALS.md Z1.2, docs/spec/plc-tables-units.md; samples 047-051).

Navigation (relation names from the metamodel):
  VarTableData / VarTableForceData   watch table / force table (every PLC has one force table "Force table";
                                     Openness PlcWatchTableComposition lists only the watch tables);
                                     AggregatingFolder -> folder, AggregatedVarItemContainer -> VarItemContainerData
  VarItemContainerData               AggregatedVarItems -> VarItemData
  VarItemData                        IVarItemAttributes.ItemData = XML <VarItems><VarLines><VarLine ...> (one line per
                                     entry: Format, Address, Name, MonitorValue, UserValue = modify value, Comment)
  OpcUaServerConfigurationData       OPC UA server interface of the PLC (Subtype OpcUa.ServerConfiguration, samples
                                     054/055); IOpcUaServerConfigurationData.IncludeForDownload False = Openness
                                     Enabled false (absent = true)
  SupervisionContainerData           ProDiag supervisions of one ProDiag FB (ParentSupervisionFB, samples 056/057):
                                     ISupervisionContainerData.Data = XML <Supervisions> (zlib) with
                                     GlobalTagSupervision (Type, Number, SupervisedStatus, CategoryNumber,
                                     SupervisionName, StateStruct, SpecificTextId ...); specific texts are SpecificText
                                     records (FormattedBlockSupervisionSpecificTexts), in the order of SpecificTextId
  UserTextRangeListData              PLC user text list (samples 045, 046, 058): IObjectRangeListData.ListRange
                                     (Decimal ...), Index, comment; Entries -> UserTextRangeData (IRangeData.RangeFrom,
                                     RangeTo, Text multilingual; name 'Range n' / 'Range a - b'). System text lists
                                     (SystemTextRangeListData) are output with kind "system"
  UnitData                           software unit; References -> related unit (Openness unit relation of type
                                     SoftwareUnit), ReferencedBy the other way; own folders below FolderParent
"""
import re
import xml.etree.ElementTree as ET


def _blob_text(b):
    """Text of a blob in the 24-byte header format (u32 length at 1, data from 24), else the bytes decoded."""
    if not isinstance(b, (bytes, bytearray)):
        return b
    if len(b) >= 24 and b[19] == 1:
        n = int.from_bytes(b[1:5], "little")
        b = b[24:24 + n]
    elif len(b) <= 24:
        return ""
    return bytes(b).decode("utf-8-sig", errors="replace")


def var_lines(xml_text):
    """Entries of a VarItems XML: [{name, address, displayFormat, modifyValue, forceValue, comment, monitorTrigger,
    modifyTrigger, isComment}]; empty trailing lines are skipped."""
    out = []
    if not xml_text:
        return out
    try:
        root = ET.fromstring(xml_text)
    except ET.ParseError:
        return out
    strip = lambda t: re.sub(r"^\{.*\}", "", t)  # noqa: E731
    for vl in root.iter():
        if strip(vl.tag) != "VarLine":
            continue
        ch = {strip(c.tag): c for c in vl}
        if not ch and not vl.get("Format"):
            continue
        uv, fv, mv = ch.get("UserValue"), ch.get("ForceValue"), ch.get("MonitorValue")
        out.append({"name": (ch["Name"].text if "Name" in ch else None), "address": (ch["Address"].text if "Address" in ch else None),
                    "displayFormat": vl.get("Format"), "isComment": vl.get("IsCommentLine") == "True",
                    "comment": ch["Comment"].text if "Comment" in ch else None,
                    "modifyValue": uv.text if uv is not None else None, "modifyTrigger": uv.get("TriggerName") if uv is not None else None,
                    "forceValue": fv.text if fv is not None else None,
                    "monitorTrigger": mv.get("TriggerName") if mv is not None else None})
    return out


def extract_watch_tables(model):
    p = model.p
    res = []
    for k in sorted(set(p.of_type("VarTableData")) | set(p.of_type("VarTableForceData"))):  # of_type includes subtypes
        entries = []
        for c in p.targets(k, "AggregatedVarItemContainer"):
            for v in p.targets(c, "AggregatedVarItems"):
                entries += var_lines(_blob_text(p.attr(v, "IVarItemAttributes", "ItemData")))
        tgt = model.rel1(k, "Target")
        res.append({"name": p.name(k), "kind": "force" if p.type_name(k) == "VarTableForceData" else "watch",
                    "plc": p.name(tgt) if tgt else None, "folder": model.folder_path(k) if hasattr(model, "folder_path") else None,
                    "comment": model.comment(k), "entries": entries, "key": k})
    res.sort(key=lambda x: (x["plc"] or "", x["kind"], x["name"] or ""))
    return res


def extract_units(model):
    p = model.p
    res = []
    for k in p.of_type("UnitData"):
        tgt = model.rel1(k, "Target")
        res.append({"name": p.name(k), "plc": p.name(tgt) if tgt else None,
                    "relations": sorted(p.name(r) for r in p.targets(k, "References")),
                    "referencedBy": sorted(p.name(r) for r in p.targets(k, "ReferencedBy")), "key": k})
    res.sort(key=lambda x: (x["plc"] or "", x["name"] or ""))
    return res


def extract_opcua_interfaces(model):
    p = model.p
    res = []
    for k in p.of_type("OpcUaServerConfigurationData"):
        tgt = model.rel1(k, "Target")
        inc = p.attr(k, "IOpcUaServerConfigurationData", "IncludeForDownload")
        res.append({"name": p.name(k), "subtype": p.attr(k, "ICoreAttributes", "Subtype"), "plc": p.name(tgt) if tgt else None,
                    "enabled": inc is not False, "author": p.attr(k, "ICoreAttributes", "Author") or None,
                    "comment": model.comment(k), "key": k})
    res.sort(key=lambda x: (x["plc"] or "", x["name"] or ""))
    return res


def extract_supervisions(model):
    """ProDiag supervisions (supervised operands are references into ident containers and are not resolved yet)."""
    from . import attrs
    p = model.p
    res = []
    for k in p.of_type("SupervisionContainerData"):
        fb = model.rel1(k, "ParentSupervisionFB")
        texts = [model.core_text_of(p.attr(t, "ICoreTextRepository", "Text")) or {} for t in p.targets(k, "FormattedBlockSupervisionSpecificTexts")]
        try:
            root = ET.fromstring(attrs.decode_blob(p.attr(k, "ISupervisionContainerData", "Data")).decode("utf-8-sig"))
        except Exception:  # noqa: BLE001 - empty or unknown container
            root = None
        sups = []
        for sv in (root.iter() if root is not None else []):
            tag = re.sub(r"^\{.*\}", "", sv.tag)
            if not tag.endswith("Supervision") or tag in ("GlobalTagSupervisions",):
                continue
            g = {re.sub(r"^\{.*\}", "", c.tag): (c.text or "").strip() for c in sv}
            if "Type" not in g:
                continue
            ti = int(g["SpecificTextId"]) if g.get("SpecificTextId", "").isdigit() else None
            txt = {c: v for c, v in (texts[ti] if ti is not None and ti < len(texts) else {}).items() if v and c != "65535"}
            sups.append({"kind": tag, "type": g.get("Type"), "number": int(g["Number"]) if g.get("Number", "").isdigit() else g.get("Number"),
                         "name": g.get("SupervisionName"), "supervisedStatus": g.get("SupervisedStatus"),
                         "category": int(g["CategoryNumber"]) if g.get("CategoryNumber", "").isdigit() else None,
                         "subCategory1": g.get("SubCategory1Number"), "subCategory2": g.get("SubCategory2Number"),
                         "stateStruct": g.get("StateStruct"), "specificText": txt})
        res.append({"proDiagFb": p.name(fb) if fb else None, "supervisions": sups, "key": k})
    res.sort(key=lambda x: x["proDiagFb"] or "")
    return res


def extract_text_lists(model):
    p = model.p
    res = []
    for t, kind in (("UserTextRangeListData", "user"), ("SystemTextRangeListData", "system")):
        for k in p.of_type(t):
            if p.type_name(k) != t:
                continue  # of_type includes subtypes
            a = p.attributes(k)[0].get("IObjectRangeListData", {})
            tgt = model.rel1(k, "Target")
            ents = []
            for e in p.targets(k, "Entries"):
                r = p.attributes(e)[0].get("IRangeData", {})
                txt = {c: v for c, v in (model.core_text_of(r.get("Text")) or {}).items() if c != "65535" or not v}
                ents.append({"from": r.get("RangeFrom"), "to": r.get("RangeTo"),
                             "text": {c: v for c, v in txt.items() if c != "65535"}})
            ents.sort(key=lambda x: (x["from"] if x["from"] is not None else 0, x["to"] if x["to"] is not None else 0))
            res.append({"name": p.name(k), "kind": kind, "plc": p.name(tgt) if tgt else None, "listRange": a.get("ListRange"),
                        "index": a.get("Index"), "comment": model.comment(k), "entries": ents, "key": k})
    res.sort(key=lambda x: (x["plc"] or "", x["kind"], x["name"] or ""))
    return res
