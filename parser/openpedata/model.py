"""Structured view of a project: project -> device groups / devices -> PLC software -> tag tables -> tags, blocks.

Navigation (relation names from the metamodel):
  ProjectData                      the project (ICoreAttributes.Name)
  FolderData Subtype=DeviceGroupFolder, FolderParent/AggregatingFolder -> parent      device groups
  DeviceData  ParentProject -> project | AggregatingFolder -> device group             stations
              (objects inside library master copies have Environment -> LibraryMasterCopyData: skipped)
  *ControllerTargetData  LogicalContainer -> DeviceData                                PLC software / CPU
  EAMTZTagTableData  Target -> controller, AggregatingFolder -> folder chain          tag tables
  EAMTZTagData       TagTable -> tag table                                            tags
  objects with IGeneralBlockSourceData, Target -> controller                          blocks (OB/FB/FC/DB)
              DataBlockData IGeneralDataBlockSourceData.Type SharedDB = global DB, IDBofFB = instance DB
              (IsGeneratedFrom -> FB); members see interface.py
  UserTypeData (BlockType UDT), Target -> controller                                  PLC data types
"""
import struct

from .code import Code
from .interface import Interface, clear_setpoint, index_elements
from .project import Project

LCID = {1031: "de-DE", 1033: "en-US", 1036: "fr-FR", 1040: "it-IT", 2052: "zh-CN", 3082: "es-ES", 0xFFFF: ""}
# Openness ProgrammingLanguage names for internal BlockLanguage values that differ
LANGUAGE = {"LAD_CLASSIC": "LAD", "FBD_CLASSIC": "FBD"}
# Openness block types of data blocks by IGeneralDataBlockSourceData.Type
DB_TYPE = {"SharedDB": "GlobalDB", "IDBofFB": "InstanceDB", "IDBofFBT": "TechnologyDB", "ArrayDB": "ArrayDB", "DBOfType": "TypedDB",
           "IDBofSDT": "TypedDB", "IDBofSFB": "InstanceDB"}


def core_text(blob, cultures=None):
    """pe:CoreTextAttributeT: u32 total, u32 used, i32 -1, u32 count, count x u16 LCID, count x u32 offset
    (relative to the value start), at each offset u32 n + n bytes UTF-8; after "used" a typed reference
    (u32 type, u64 oid) to the text category. LCID 0xFFFF = no language. Returns {culture: text}."""
    if not blob or len(blob) < 16:
        return {}
    (count,) = struct.unpack_from("<I", blob, 12)
    if 16 + 6 * count > len(blob):
        return {}
    lcids = struct.unpack_from("<%dH" % count, blob, 16)
    offs = struct.unpack_from("<%dI" % count, blob, 16 + 2 * count)
    res = {}
    for lcid, off in zip(lcids, offs):
        (n,) = struct.unpack_from("<I", blob, off)
        culture = (cultures or {}).get(lcid) or LCID.get(lcid) or str(lcid)
        res[culture] = blob[off + 4:off + 4 + n].decode("utf-8", errors="replace")
    return res


class Model:
    def __init__(self, path):
        self.p = p = Project(path)
        self.cultures = {}
        for k in p.of_type("CoreCulture"):
            n = p.name(k) or ""
            if n.endswith(")") and "(" in n:
                name, lcid = n[:-1].split("(", 1)
                if lcid.isdigit():
                    self.cultures[int(lcid)] = name
        # reverse index: target key -> [(relation name, source key)]
        self.incoming = {}
        for k, o in p.objects.items():
            for l in o.links:
                r = p.mm.relations.get(l.relation)
                self.incoming.setdefault(l.key, []).append((r.name if r else None, k))
        self.errors = []  # objects whose content could not be read; listed in statistics.errors, the rest of the output stays

    def _guarded(self, entry, what, fn):
        """Runs fn() for one object; an exception becomes entry["error"] and an entry of self.errors, so that one
        unreadable object (unknown layout) does not abort the whole project."""
        try:
            return fn()
        except Exception as ex:  # noqa: BLE001 - reported per object
            msg = "%s: %s" % (type(ex).__name__, ex)
            entry["error"] = msg
            self.errors.append({"object": what, "key": entry.get("key"), "error": msg})
            return None

    # --- helpers ----------------------------------------------------------------------------------
    def culture(self, key):
        """CoreCulture record name 'de-DE(1031)' -> 'de-DE'."""
        n = self.p.name(key) or ""
        return n.split("(", 1)[0] if n.endswith(")") else n

    def project_languages(self, proj):
        """Project languages (sample 153): ProjectData links UsedCultures (active languages, in order),
        EditingCulture, ReferenceCulture -> CoreCulture."""
        if not proj:
            return None
        ed, ref = self.rel1(proj, "EditingCulture"), self.rel1(proj, "ReferenceCulture")
        return {"active": [self.culture(c) for c in self.p.targets(proj, "UsedCultures")],
                "editing": self.culture(ed) if ed else None, "reference": self.culture(ref) if ref else None}

    def rel1(self, key, relation):
        t = self.p.targets(key, relation)
        return t[0] if t else None

    def sources(self, key, relation):
        return [s for r, s in self.incoming.get(key, []) if r == relation]

    def folder_path(self, key, stop_subtypes=()):
        """User folder path of an object below its system root folder (via AggregatingFolder)."""
        parts = []
        f = self.rel1(key, "AggregatingFolder")
        while f:
            sub = self.p.attr(f, "ICoreAttributes", "Subtype")
            if self.rel1(f, "AggregatingFolder") is None and sub not in ("DeviceGroupFolder",):
                break  # system root folder (ControllerTagsFolder, ProgramBlocksFolder, ...)
            parts.append(self.p.name(f))
            f = self.rel1(f, "AggregatingFolder")
        return "/".join(reversed(parts))

    def comment(self, key):
        return core_text(self.p.attr(key, "ICoreAttributes", "Comment"), self.cultures)

    def core_text_of(self, blob):
        return core_text(blob, self.cultures)

    def elementary_types(self, scope):
        """{DataTypeID: name} of the DataTypeItemData of one target family scope (e.g. S71500)."""
        if not hasattr(self, "_types"):
            self._types = {}
            for k in self.p.of_type("DataTypeItemData"):
                c = self.p.attributes(k)[0].get("IDataTypeContent", {})
                if c.get("DataTypeID") is not None:
                    self._types.setdefault(c.get("Scope"), {}).setdefault(c["DataTypeID"], _display_type_name(
                        self.p.name(k), c.get("DataTypeKind")))
        # some types exist only in another S7-1200/1500 scope with the same DataTypeID (Variant 63 only in S71200)
        # S7-300/400 blocks (TargetFamily S7300, S7400) use the scope S7300400 (sample 071)
        r = dict(self._types.get(scope, {}))
        for other in (("S7300400",) if scope in ("S7300", "S7400") else ("S71200", "S71500")):
            for k, v in self._types.get(other, {}).items():
                r.setdefault(k, v)
        return r

    def members(self, key):
        scope = self.p.attr(key, "IExpandoInformationData", "TargetFamily") or "S71500"
        it = Interface(self, key, scope)
        it.standard = self.is_standard(key)
        return it.members()

    def interface_sections(self, key):
        scope = self.p.attr(key, "IExpandoInformationData", "TargetFamily") or "S71500"
        it = Interface(self, key, scope)
        it.standard = self.is_standard(key)
        return it.sections()

    def is_standard(self, key):
        """Standard (not optimized) block access: IGeneralBlockSourceData.OnlySymbolicAccess False (sample 059)."""
        return self.p.attr(key, "IGeneralBlockSourceData", "OnlySymbolicAccess") is False

    def instance_sections(self, idb, fb):
        """Instance DB: the interface comes from the FB (the DB's UsedParts points to the FB's interface root,
        Kind=BlockSource); only Input, Output, InOut and Static exist. startValue is the DB's own start value
        (root payload <Values> of the DB, p = member ID of the FB; null unless changed in the DB), defaultValue is the
        start value declared in the FB. Comments of the FB are not shown (as in Openness)."""
        scope = self.p.attr(fb, "IExpandoInformationData", "TargetFamily") or "S71500"
        own = Interface(self, idb, scope).values
        fbi = Interface(self, fb, scope)
        fbi.keep_path = True
        fbi.standard = self.is_standard(fb)
        out = []
        for sec in fbi.sections():
            if sec["section"] in ("Input", "Output", "InOut", "Static"):
                ms = [_idb_member(m, own) for m in sec["members"]]
                if sec["section"] != "Static":
                    clear_setpoint(ms)  # Openness: setpoints only in the Static section (UDT parameter, sample 060)
                out.append({"section": sec["section"], "members": ms})
        return out

    def typed_members(self, db, udt):
        """DB of a PLC data type (Type DBOfType, sample 059): GeneratedFromMaster points to the UDT. Same for a DB of a
        system data type (Type IDBofSDT, e.g. IEC_TIMER in Program resources, sample 065): the system type has no
        CurrentInterface, its interface root is the UsedParts entry of the DB's own root."""
        scope = self.p.attr(db, "IExpandoInformationData", "TargetFamily") or "S71500"
        own_if = Interface(self, db, scope)
        own = own_if.values
        root = self.rel1(udt, "CurrentInterface") or next(
            (k for k in own_if.parts if k and self.p.is_a(k, "InterfaceVersionRootData")), None)
        if root is None:
            return []
        ti = Interface(self, udt, scope, root=root)
        ti.keep_path = True
        ti.standard = self.is_standard(db)
        return [_idb_member(m, own) for m in ti.members()]

    def to_sections(self, db):
        """Technology object DB (Type IDBofFBT, samples 036, 052, 053): the own interface root holds the changed
        start values (<Values>, p = member path in the TO type) and its UsedParts point to the TO type interface
        ('_BIVE:<type>/<version>/<guid>' in a LibraryBlockInterfaceContainer). Members as for an instance DB."""
        scope = self.p.attr(db, "IExpandoInformationData", "TargetFamily") or "S71500"
        own_if = Interface(self, db, scope)
        troot = next((k for k in own_if.parts if k and self.p.is_a(k, "InterfaceVersionRootData")), None)
        if troot is None:
            return []
        ti = Interface(self, db, scope, root=troot)
        ti.keep_path = True
        out = []
        for sec in ti.sections():
            if sec["section"] in ("Input", "Output", "InOut", "Static"):
                out.append({"section": sec["section"], "members": [_idb_member(m, own_if.values) for m in sec["members"]]})
        return out

    # --- content ----------------------------------------------------------------------------------
    def tag(self, k):
        p = self.p
        si = p.attributes(k)[0].get("IStructureItem", {})
        ta = p.attributes(k)[0].get("ITagAddress", {})
        acc = si.get("HmiStructureTypeRelevant")
        vis = None if acc is None or si.get("HmiVisible") is None else acc and si["HmiVisible"]
        return {
            "name": p.name(k),
            "dataType": si.get("DisplayTypeName"),
            "address": ta.get("LogicalAddress"),
            "comment": self.comment(k),
            # Openness ExternalAccessible/Visible/Writable cascade: not accessible -> not visible -> not writable
            "hmiAccessible": acc,
            "hmiVisible": vis,
            "hmiWritable": None if vis is None or si.get("HmiReadOnly") is None else vis and not si["HmiReadOnly"],
            "key": k,
        }

    def tag_tables(self, plc):
        out = []
        for t in sorted(self.sources(plc, "Target")):
            if not self.p.is_a(t, "TagTableData"):
                continue
            tags = [self.tag(x) for x in self.sources(t, "TagTable") if self.p.is_a(x, "EAMTZTagData")]
            tags.sort(key=lambda x: x["name"] or "")
            out.append({"name": self.p.name(t), "group": self.folder_path(t), "tags": tags, "key": t})
        out.sort(key=lambda x: (x["group"], x["name"] or ""))
        return out

    def blocks(self, plc):
        out = []
        for b in self.sources(plc, "Target"):
            gb = self.p.attributes(b)[0].get("IGeneralBlockSourceData")
            if not gb or gb.get("BlockType") is None:
                continue
            if gb.get("BlockType") == "UDT":
                continue  # PLC data type, see types()
            lang = gb.get("BlockLanguage")
            blk = {"name": self.p.name(b), "type": gb.get("BlockType"), "number": gb.get("Number"),
                   "language": LANGUAGE.get(lang, lang), "languageInternal": lang,
                   "group": self.folder_path(b), "key": b}
            # know-how protection and software unit (sample 070): the interface stays readable in the file
            if self.p.attr(b, "ICoreAttributes", "IsKnowHowProtected"):
                blk["knowHowProtected"] = True
            unit = self.rel1(b, "SWObjectData_IUnitElementAttributes_BaseUnitData")
            if unit and self.p.is_a(unit, "UnitData"):
                blk["unit"] = self.p.name(unit)
            if gb.get("BlockType") == "DB":
                dt = self.p.attr(b, "IGeneralDataBlockSourceData", "Type")
                blk["type"] = DB_TYPE.get(dt, "DB:%s" % dt)
            is_db = gb.get("BlockType") == "DB"
            self._guarded(blk, "block %s" % blk["name"], lambda: self._block_content(b, blk, is_db))
            out.append(blk)
        out.sort(key=lambda x: (x["type"] or "", x["number"] or 0, x["name"] or ""))
        return out

    def _block_content(self, b, blk, is_db):
        """Interface, members and networks of a block (blk already holds name, type, number, language, group)."""
        if is_db:
            fb = self.rel1(b, "IsGeneratedFrom")
            blk["members"] = self.members(b)
            if blk["type"] == "ArrayDB":  # hidden Header (ArrayDBHeader) dropped, THIS named like the DB (Openness)
                blk["members"] = [dict(x, name=blk["name"]) if x["name"] == "THIS" else x
                                  for x in blk["members"] if x["dataType"] != "ArrayDBHeader"]
            udt = self.rel1(b, "GeneratedFromMaster") if blk["type"] == "TypedDB" else None
            if udt:  # DB of a PLC data type: members from the UDT, own root holds the changed start values
                blk["instanceOf"] = self.p.name(udt)
                blk["members"] = self.typed_members(b, udt)
            if blk["type"] == "TechnologyDB":  # technology object instance (Type IDBofFBT): type name in OfName
                blk["instanceOf"] = self.p.attr(b, "IGeneralDataBlockSourceData", "OfName")
                blk["interface"] = self.to_sections(b)
            if fb:
                blk["instanceOf"] = self.p.name(fb)
                blk["interface"] = self.instance_sections(b, fb)
                blk["members"] = next((x["members"] for x in blk["interface"] if x["section"] == "Static"), [])
            blk["optimized"] = not self.is_standard(b)
            if self.is_standard(b):  # not optimized DB: retentive as a whole (IExpandoInformationData.NonRetain False)
                retain = self.p.attr(b, "IExpandoInformationData", "NonRetain") is False
                _set_retain(blk["members"], retain)
                for sec in blk.get("interface", []):
                    _set_retain(sec["members"], retain)
        else:
            blk["interface"] = self.interface_sections(b)
            blk["networks"] = Code(self).networks(b)

    def types(self, plc):
        out = []
        for t in self.sources(plc, "Target"):
            if self.p.attr(t, "IGeneralBlockSourceData", "BlockType") != "UDT":
                continue
            udt = {"name": self.p.name(t), "group": self.folder_path(t), "members": [], "key": t}
            self._guarded(udt, "PLC data type %s" % udt["name"], lambda: udt.update(members=self.members(t)))
            out.append(udt)
        out.sort(key=lambda x: (x["group"], x["name"] or ""))
        return out

    def plcs(self, device):
        out = []
        for c in sorted(self.sources(device, "LogicalContainer")):
            if not self.p.is_a(c, "ControllerTargetData"):
                continue
            di = self.p.attributes(c)[0].get("IDeviceItemData", {})
            out.append({"name": self.p.name(c), "orderNumber": di.get("OrderNumber"), "firmware": di.get("FwVersion"),
                        "typeName": di.get("InvariantTypeName"), "tagTables": self.tag_tables(c),
                        "blocks": self.blocks(c), "types": self.types(c), "key": c})
        return out

    def to_dict(self, keys=False):
        p = self.p
        proj = next(iter(p.of_type("ProjectData")), None)
        # L6: a user global library (.al21 / .zal21) has no ProjectData; its root object is the LibraryData with
        # LibraryType GlobalLib (same OID 0x433). Devices there are only shell devices of versions / master copies.
        glib = None if proj else next((k for k in p.of_type("LibraryData")
                                       if p.attr(k, "ICoreLibAttributes", "LibraryType") == "GlobalLib"), None)
        groups = []
        for f in p.of_type("FolderData"):
            if p.attr(f, "ICoreAttributes", "Subtype") == "DeviceGroupFolder":
                parent = self.folder_path(f)
                groups.append({"name": p.name(f), "path": (parent + "/" if parent else "") + p.name(f), "key": f})
        groups.sort(key=lambda g: g["path"])
        from .hmi import Hmi  # late import: hmi uses core_text from this module
        hmi = Hmi(self)
        from .hardware import Hardware
        hw = Hardware(self)
        devices = []
        for d in p.of_type("DeviceData"):
            if (proj or glib) and self.rel1(d, "Environment") != (proj or glib):
                continue  # copy inside a library master copy (Environment = LibraryMasterCopyData)
            dd = p.attributes(d)[0].get("IDeviceData", {})
            devices.append({"name": p.name(d), "typeName": dd.get("InvariantTypeName"), "group": self.folder_path(d),
                            "comment": self.comment(d), "hardware": hw.device(d), "plcs": self.plcs(d), "hmi": hmi.softwares(d),
                            "key": d})
        devices.sort(key=lambda x: (x["group"], x["name"] or ""))
        from .library import extract_library  # late import: library uses core_text from this module
        from .plc_alarms import extract_plc_alarms
        from .plc_tables import extract_watch_tables, extract_units, extract_opcua_interfaces, extract_supervisions, extract_text_lists
        from .faceplates import extract_faceplates
        res = {"project": {"name": p.name(proj or glib) if (proj or glib) else None,
                           "kind": "GlobalLibrary" if glib else "Project",
                           "comment": self.comment(proj) if proj else {},
                           "deviceGroups": groups, "devices": devices,
                           "languages": self.project_languages(proj), "plantViews": hmi.plant_views(),
                           "graphics": hmi.graphics(),
                           "network": hw.network({x["key"] for x in devices}), "umac": hw.umac(),
                           "plcAlarms": extract_plc_alarms(self), "plcWatchTables": extract_watch_tables(self),
                           "plcUnits": extract_units(self), "plcOpcUaInterfaces": extract_opcua_interfaces(self),
                           "plcSupervisions": extract_supervisions(self), "plcTextLists": extract_text_lists(self)},
               "library": extract_library(p, keys),
               "faceplates": extract_faceplates(self, keys),
               "statistics": {"plfFiles": p.plf_files, "records": len(p.records), "objects": len(p.objects),
                              "deletedObjects": len(p.deleted),
                              "hashErrors": sum(not r.hash_ok for r in p.records)}}
        if self.errors:
            res["statistics"]["errors"] = self.errors
        if not keys:
            _strip(res)
        return res


def _display_type_name(name, kind):
    """System data types (DataTypeKind 33: Conn_Ouc, Hw_Any, OB_Any, ...) are shown in capitals by TIA and Openness
    (CONN_OUC, HW_ANY; sample 066); the OPC UA helper types keep their name."""
    if name is not None and str(kind) == "33" and not name.startswith("OPC_UA_"):
        return name.upper()
    return name


def _set_retain(members, retain):
    for m in members:
        m["retain"] = retain
        _set_retain(m.get("members", []), retain)


def _idb_member(m, own):
    path = m["_path"]
    e = {"name": m["name"], "dataType": m["dataType"], "startValue": own.get(path), "comment": {}}
    for k in ("retain", "externalAccessible", "externalVisible", "externalWritable", "setpoint", "accessibility", "offset"):
        if k in m:
            e[k] = m[k]
    if m.get("startValue") is not None:
        e["defaultValue"] = m["startValue"]
    elems = dict(index_elements(own).get(path, {}))
    if elems:
        e["elementStartValues"] = elems
    if m.get("elementStartValues"):
        e["defaultElementStartValues"] = m["elementStartValues"]
    if m.get("members"):
        e["members"] = [_idb_member(k, own) for k in m["members"]]
    return e


def _strip(o):
    if isinstance(o, dict):
        o.pop("key", None)
        for v in o.values():
            _strip(v)
    elif isinstance(o, list):
        for v in o:
            _strip(v)
