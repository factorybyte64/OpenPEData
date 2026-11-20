"""Project library view (folders, master copies, types / versions, typed instances) of a TIA Portal V21 project
(steps L0-L5, see analysis/L1_types, L2_mastercopy, L4_instances REPORT.md).

extract_library(store) takes an openpedata.project.Project (or an openpedata.model.Model, its .p is used) and
returns a dict; Model.to_dict() puts it under "library". Standalone (from the parser directory):
  python -m openpedata.library <project folder | .ap21 | .zap21> [--json] [--keys]

Navigation (relation names from the metamodel):
  ProjectData                       LibraryData 'Project library' (ICoreLibAttributes.LibraryType = ProjectLib)
  LibraryData  -Folders->           system folders (FolderData, Subtype = LibraryTypeSystemFolder,
                                    LibraryMasterCopySystemFolder, LibrarySystemTypeSystemFolder,
                                    LibrarySystemMasterCopySystemFolder, CommonDataFolder)
  FolderData   -Folders->/<-FolderParent  sub folders;  -AggregatedElements->/<-AggregatingFolder  elements
  LibraryMasterCopyData             ICoreAttributes (Name, Subtype, Author, times), ICoreLibObjectAttributes.State,
                                    ICoreLibMasterCopyAttributes.SharedRawData (clipboard header XML),
                                    -VisibleParts-> selected objects (copies), -Devices-> shell device (context),
                                    copies of the source subtree carry Environment -> LibraryMasterCopyData;
                                    referenced blocks / PLC data types only as name stubs (-TRefParent-> copied
                                    folder), a used PLC data type with its interface root (L2, see
                                    analysis/L2_mastercopy/REPORT.md); CoreObjectData = clipboard root objects
  types / versions (checked with samples L01-L09, see analysis/L1_types/REPORT.md): objects of a type derived
    from Library.UserTypes TypeObjectBase / VersionObjectBase (seen: UserTypeDataByCopyTypeObject/-VersionObject
    for PLC data types, CodeBlockDataByCopyTypeObject/-VersionObject for FB/FC) or ObjectFrame CoreLibraryType.
    TypeObject  ITypeInfo.TypeGuid, -AggregatingFolder-> LibraryTypeSystemFolder, -TypeObjectToVersionObject->
                versions, -TypeToDefaultVersion-> default version
    VersionObject  IVersionInfo (Version 19 bytes, VersionGuid, FinalizationTime), ICoreLibObjectAttributes.State
                InWork/Committed, ICoreAttributes Author/Comment (release comment), -DependsOn-> used versions,
                -IsTypeOf-> instance, Committed: -VisibleParts-> content copy, -Devices-> shell device
  instances: InstanceObject is a base of every CodeBlockData / UserTypeData; a typed block / UDT has
    -IsInstanceOf-> version. Copies with Environment -> version / master copy are not instances.
    Version state (L4, samples L16-L21): -IsInstanceOf-> version, -CurrentLibraryInterface-> the version's
    interface root '_BIVE:<name>/<VersionGuid>', expando UserTypeVersionGUID on the own interface root. Whether an
    instance is outdated is not stored; it is derived (a committed version with a higher number exists).

Deleted objects (tombstones) are not in Project.objects; links to them are dropped. The package of a type is
the Package element that declares it in the metamodel XML (Metamodel.type_package).
"""
import json
import re
import sys
import xml.etree.ElementTree as ET

from .attrs import decode_structure
from .model import core_text
from .project import Project

TYPE_BASES = ("TypeObjectBase", "CoreLibraryType")
VERSION_BASES = ("VersionObjectBase",)
INSTANCE_BASES = ("InstanceObject",)
# metamodel type of the type object -> LibraryType kind as Openness reports it (TiaProjectLibrary typesList, L1)
OPENNESS_TYPE_KIND = {"UserTypeDataByCopyTypeObject": "PlcTypeLibraryType",
                      "CodeBlockDataByCopyTypeObject": "CodeBlockLibraryType",
                      # WinCC Unified types (Siemens.Simatic.Hmi.DL.*): Openness has no own class for them and reports the
                      # base class LibraryType - faceplates (F01-F03, F40), text lists, images, dynamic SVGs (F40 typesList)
                      "FaceplateLibraryTypeData": "LibraryType", "TextListLibraryTypeData": "LibraryType",
                      "ImageLibraryTypeData": "LibraryType", "DynamicSVGLibraryTypeData": "LibraryType",
                      # same package family, not yet in a sample: by analogy LibraryType
                      "GraphicListLibraryTypeData": "LibraryType", "HmiColorPaletteLibraryTypeData": "LibraryType",
                      "ScriptModuleLibraryTypeData": "LibraryType",
                      # WinCC Comfort/Advanced (Siemens.Simatic.Hmi.Utah.*): own Openness classes in Siemens.Engineering.WinCC
                      # (FaceplateLibraryType, ScreenLibraryType, StyleLibraryType, StyleSheetLibraryType, HmiUdtLibraryType,
                      # CScriptLibraryType, VBScriptLibraryType); mapping by name, not yet in a sample
                      "HmiScreenModuleTypeObjectData": "FaceplateLibraryType", "HmiScreenTypeData": "ScreenLibraryType",
                      "HmiStyleTypeData": "StyleLibraryType", "HmiStyleSheetTypeData": "StyleSheetLibraryType",
                      "HmiUdtTypeData": "HmiUdtLibraryType", "HmiCScriptTypeData": "CScriptLibraryType",
                      "HmiVBScriptTypeData": "VBScriptLibraryType"}
SYSTEM_FOLDER_ORDER = ("LibraryTypeSystemFolder", "LibraryMasterCopySystemFolder", "LibrarySystemTypeSystemFolder",
                       "LibrarySystemMasterCopySystemFolder", "CommonDataFolder")
# VersionData structure elements in metamodel declaration order (encoding not yet seen in a sample)
VERSION_ELEMENTS = ("Build", "Major", "Minor", "Revision")


def key_type_id(key):
    lo, hi, _ = key.split(":")
    return (int(hi, 16) << 16) | int(lo, 16)


def parse_shared_raw_data(text):
    """Clipboard header of ICoreLibMasterCopyAttributes.SharedRawData -> dict (None if empty / unparsable)."""
    if not text:
        return None
    if isinstance(text, bytes):
        for enc in ("utf-8", "utf-16"):
            try:
                text = text.decode(enc)
                break
            except UnicodeDecodeError:
                continue
        else:
            return {"error": "undecodable", "raw": text.hex()}
    # the declaration says utf-16 but the value is a str already
    body = re.sub(r"^\s*<\?xml[^>]*\?>", "", text)
    try:
        root = ET.fromstring(body)
    except ET.ParseError as e:
        return {"error": "xml: %s" % e, "raw": text}
    res = {"formatVersion": None, "projectId": None, "sourceScopePath": [], "typeDescriptors": [],
           "selectedIds": None, "resolvers": []}
    hdr = root.find("ClipboardHeader")
    if hdr is not None:
        res["formatVersion"] = hdr.get("formatVersion")
        res["projectId"] = hdr.get("projectId")
        for sc in hdr.iter("ObjectScope"):
            res["sourceScopePath"].append({"type": sc.get("scopeTypeName"), "id": sc.get("scopeId")})
        for td in hdr.iter("TypeDescriptor"):
            d = dict(td.attrib)
            d["roles"] = [r.strip() for r in (d.get("roles") or "").split(",") if r.strip()]
            res["typeDescriptors"].append(d)
    data = root.find("ClipboardData")
    if data is not None:
        sel = data.find("SelectedIds")
        res["selectedIds"] = sel.text if sel is not None else None
        res["resolvers"] = [r.get("sid") for r in data.iter("Resolver")]
    return res


def decode_version_data(raw):
    """IVersionInfo.Version (structure VersionData), confirmed by L1 (0.0.1, 1.0.0, 1.2.3, 2.3.4): u32 total length
    (20) + 4 x i32 little endian in metamodel declaration order Build, Major, Minor, Revision; Revision -1 = not set
    (like System.Version with three parts). Text = Major.Minor.Build[.Revision]. Accepts the dict of
    attrs.decode_structure or the raw value (20 bytes with the u32 head; 19 / 16 bytes from older parser versions).
    Returns (text or None, raw hex, dict of the four parts or None)."""
    if raw is None:
        return None, None, None
    if isinstance(raw, dict):
        v = raw
    elif isinstance(raw, (bytes, bytearray)) and len(raw) in (16, 19, 20):
        import struct
        v = dict(zip(VERSION_ELEMENTS, struct.unpack("<4i", bytes(raw[-16:]))))
    else:
        return None, raw.hex() if isinstance(raw, (bytes, bytearray)) else str(raw), None
    txt = "%s.%s.%s" % (v.get("Major"), v.get("Minor"), v.get("Build"))
    if isinstance(v.get("Revision"), int) and v["Revision"] >= 0:
        txt += ".%d" % v["Revision"]
    return txt, raw.hex() if isinstance(raw, (bytes, bytearray)) else None, v


def decode_core_object_data(mm, raw):
    """ICoreLibMasterCopyAttributes.CoreObjectData = ArrayLibraryClipboardCoreObject (L2, samples 021, L10-L15):
    array of structures LibraryClipboardCoreObject (ObjectRole enum LibraryClipboardObjectRole, Option string,
    ParentObjectId, SourceObjectId, VisualPartIndex), decoded by attrs.decode_structure.
    Returns {"count", "entries": [...]} or {"error", "hex"}."""
    if raw is None:
        return None
    d = decode_structure(mm, "ArrayLibraryClipboardCoreObject", raw)
    if not isinstance(d, list) or not all(isinstance(e, dict) for e in d):
        return {"error": "not decodable", "hex": bytes(raw).hex()}
    return {"count": len(d), "entries": [{"objectRole": e.get("ObjectRole"), "option": e.get("Option"),
                                          "parentObjectId": e.get("ParentObjectId"),
                                          "sourceObjectId": e.get("SourceObjectId"),
                                          "visualPartIndex": e.get("VisualPartIndex")} for e in d]}


def decode_struct_array_count(mm, typ, raw):
    """Item count of an array value (NonCoreObjectData = ArrayLibraryClipboardNonCoreObject: only the empty array
    is seen). Returns the count or None."""
    d = decode_structure(mm, typ, raw) if raw is not None else None
    return len(d) if isinstance(d, list) else None


def parse_scope_id(text):
    """SharedRawData ObjectScope scopeId '{<type id decimal>-<object id decimal>}' (L2) -> record key 'lo:hi:oid'
    of the SOURCE object (Project scope = ProjectData, Target scope = the PLC's S7ControllerTargetData). The same
    format is used by IInterfaceVersionData.SourceObjectID. Returns None for other forms (e.g. Persistence)."""
    m = re.fullmatch(r"\{(\d+)-(\d+)\}", text or "")
    if not m:
        return None
    tid, oid = int(m.group(1)), int(m.group(2))
    return "%04x:%04x:%x" % (tid & 0xFFFF, tid >> 16, oid)


def parse_dependency_identifiers(raw, known_guids=()):
    """IDependencyInfo.DependencyIdentifiers (L09, layout only partly understood): 24-byte blob header, then u32 total
    length, u32 entry count, ...; each entry holds the type name, the VersionGuid of the used version (16 bytes,
    .NET little-endian GUID layout) and the name of that version's content copy. Returns the entry count, the
    known version GUIDs found in the bytes and the readable strings."""
    import uuid
    if not isinstance(raw, (bytes, bytearray)) or len(raw) < 32:
        return None
    body = bytes(raw[24:])
    count = int.from_bytes(body[4:8], "little")
    guids = [g for g in known_guids if g and uuid.UUID(g).bytes_le in body]
    strings = [m.decode("ascii") for m in re.findall(rb"[\x20-\x7e]{4,}", body)]
    return {"count": count, "versionGuids": guids, "strings": strings}


def version_key(text):
    """Sort key of a version text 'a.b.c[.d]' (None last)."""
    try:
        return (0,) + tuple(int(x) for x in text.split("."))
    except (AttributeError, ValueError):
        return (1,)


class LibraryView:
    def __init__(self, store):
        self.p = p = getattr(store, "p", store)
        self.mm = p.mm
        self.incoming = {}  # target key -> [(relation name, source key)]
        for k, o in p.objects.items():
            for l in o.links:
                r = self.mm.relations.get(l.relation)
                self.incoming.setdefault(l.key, []).append((r.name if r else None, k))
        self.cultures = {}
        for k in p.of_type("CoreCulture"):
            n = p.name(k) or ""
            if n.endswith(")") and "(" in n:
                name, lcid = n[:-1].split("(", 1)
                if lcid.isdigit():
                    self.cultures[int(lcid)] = name

    # --- helpers ----------------------------------------------------------------------------------
    def sources(self, key, relation):
        return [s for r, s in self.incoming.get(key, []) if r == relation and s in self.p.objects]

    def rel1(self, key, relation):
        t = self.p.targets(key, relation)
        return t[0] if t else None

    def is_any(self, key, bases):
        return any(self.p.is_a(key, b) for b in bases)

    def obj_ref(self, key, keys):
        p = self.p
        tid = p.objects[key].rec.type_id
        d = {"name": p.name(key), "type": p.type_name(key), "typeFullName": self.mm.type_name(tid),
             "package": self.mm.type_package(tid)}
        sub = p.attr(key, "ICoreAttributes", "Subtype")
        if sub:
            d["subtype"] = sub
        bt = p.attr(key, "IGeneralBlockSourceData", "BlockType")
        if bt:
            d["blockType"] = bt
        # copies below a library object carry a generated name ('hg1jahs3gd0jgsa<guid>0"0'); the real name is here
        cn = p.attr(key, "IExpandoInformationData", "CustomerTypePartName")
        if cn:
            d["customerName"] = cn
        if keys:
            d["key"] = key
        return d

    def core(self, key):
        a = self.p.attributes(key)[0].get("ICoreAttributes", {})
        return {"subtype": a.get("Subtype"), "author": a.get("Author"), "lastModifiedBy": a.get("LastModifiedBy"),
                "creationTime": a.get("CreationTime"), "modifiedTime": a.get("ModifiedTime"),
                "comment": core_text(a.get("Comment"), self.cultures) if isinstance(a.get("Comment"), bytes) else {}}

    def environment_mc(self, key):
        """Master copy whose copied content this object is (Environment -> LibraryMasterCopyData), else None."""
        env = self.rel1(key, "Environment")
        return env if env and self.p.is_a(env, "CoreLibraryMasterCopy") else None

    def environment_version(self, key):
        """Type version whose content copy this object is (Environment -> VersionObject), else None. A released
        version holds a copy of the typed object plus a shell device / target context, like a master copy (L1)."""
        env = self.rel1(key, "Environment")
        return env if env and self.is_any(env, VERSION_BASES) else None

    def library_of(self, key):
        """LibraryData of an object via Environment / AggregatingFolder / FolderParent chain."""
        seen = set()
        while key and key not in seen:
            seen.add(key)
            if self.p.is_a(key, "CoreLibrary"):
                return key
            key = self.rel1(key, "Environment") or self.rel1(key, "AggregatingFolder") or self.rel1(key, "FolderParent")
        return None

    def folder_info(self, key):
        """(system folder subtype, user path below it) of an element or folder (via AggregatingFolder/FolderParent)."""
        parts, f, seen = [], self.rel1(key, "AggregatingFolder") or self.rel1(key, "FolderParent"), set()
        while f and f not in seen and self.p.is_a(f, "FolderData"):
            seen.add(f)
            parent = self.rel1(f, "FolderParent") or self.rel1(f, "AggregatingFolder")
            if parent is None or self.p.is_a(parent, "CoreLibrary"):
                return self.p.attr(f, "ICoreAttributes", "Subtype"), "/".join(reversed(parts))
            parts.append(self.p.name(f))
            f = parent
        return None, "/".join(reversed(parts))

    # --- folders ----------------------------------------------------------------------------------
    def children(self, f):
        p = self.p
        sub, elems = [], []
        cand = p.targets(f, "Folders") + self.sources(f, "FolderParent") + \
            p.targets(f, "AggregatedElements") + self.sources(f, "AggregatingFolder")
        for c in dict.fromkeys(cand):
            if c == f or c not in p.objects:
                continue
            (sub if p.is_a(c, "FolderData") else elems).append(c)
        return sub, elems

    def folder(self, f, path, keys, seen):
        p = self.p
        seen.add(f)
        sub, elems = self.children(f)
        d = {"name": p.name(f), "subtype": p.attr(f, "ICoreAttributes", "Subtype"), "path": path,
             "modifiedTime": p.attr(f, "ICoreAttributes", "ModifiedTime"),
             "elements": sorted((self.obj_ref(e, keys) for e in elems), key=lambda x: (x["type"], x["name"] or "")),
             "folders": []}
        for s in sorted(sub, key=lambda k: p.name(k) or ""):
            if s in seen:
                continue
            d["folders"].append(self.folder(s, (path + "/" if path else "") + (p.name(s) or ""), keys, seen))
        if keys:
            d["key"] = f
        return d

    # --- master copies ----------------------------------------------------------------------------
    def master_copy(self, mc, keys):
        p = self.p
        attrs, problems = p.attributes(mc)
        mca = attrs.get("ICoreLibMasterCopyAttributes", {})
        lo = attrs.get("ICoreLibObjectAttributes", {})
        sysf, path = self.folder_info(mc)
        copies = [s for s in self.sources(mc, "Environment")]
        visible = p.targets(mc, "VisibleParts")
        context = []
        for d in p.targets(mc, "Devices"):
            context.append(dict(self.obj_ref(d, keys), relation="Devices",
                                isShell=p.attr(d, "ICoreAttributes", "IsShell")))
        tgts = []
        for v in visible:
            for t in p.targets(v, "Target"):
                if t not in tgts:
                    tgts.append(t)
        for t in tgts:
            context.append(dict(self.obj_ref(t, keys), relation="Target (of visible parts)"))

        def blob_len(v):
            return len(v) if isinstance(v, (bytes, bytearray)) else None

        # L2: objects the copied content only references (called blocks / instance DBs, used PLC data types) are
        # not copied in full but as name stubs: few ICoreAttributes, -TRefParent-> copied system folder (inverse
        # TextualReferences). A used PLC data type additionally brings its interface version root
        # ('_BIVE:<name>/<guid>', -VersionElementToBlock-> stub) so the copied interface stays resolvable.
        # (a copied system folder that only holds such stubs, e.g. ControllerDataTypeFolder, is itself attached by
        # TRefParent instead of FolderParent; it is not a referenced object)
        stubs = [c for c in copies if p.targets(c, "TRefParent") and not p.is_a(c, "FolderData")]
        frozen = [c for c in copies if p.is_a(c, "InterfaceVersionRootData")
                  and any(t in stubs for t in p.targets(c, "VersionElementToBlock"))]
        by_type = {}
        for c in copies:
            by_type[p.type_name(c)] = by_type.get(p.type_name(c), 0) + 1
        source = parse_shared_raw_data(mca.get("SharedRawData"))
        if source and not source.get("error"):
            for sc in source["sourceScopePath"]:
                k = parse_scope_id(sc["id"])
                if k:
                    sc["sourceKey"] = k
                    sc["sourceObject"] = self.obj_ref(k, keys) if k in p.objects else None
        rp = self.rel1(mc, "RelatedPackage")
        fold = self.rel1(mc, "AggregatingFolder")
        order = p.targets(fold, "AggregatedElements") if fold else []
        d = {"name": p.name(mc), "systemFolder": sysf, "folderPath": path,
             # position in the folder's AggregatedElements = creation order = order of Openness masterCopiesList (L12)
             "folderIndex": order.index(mc) if mc in order else None,
             **self.core(mc),
             "state": lo.get("State"), "isSystemElement": lo.get("IsSystemElement"),
             "originalLibrary": lo.get("OriginalLibrary"), "originalName": lo.get("OriginalName"),
             "source": source,
             "coreObjects": decode_core_object_data(self.mm, mca.get("CoreObjectData")),
             "nonCoreObjects": decode_struct_array_count(self.mm, "ArrayLibraryClipboardNonCoreObject", mca.get("NonCoreObjectData")),
             "contents": [self.obj_ref(v, keys) for v in visible],
             "parts": [self.obj_ref(v, keys) for v in p.targets(mc, "Parts")],
             "invisibleParts": [self.obj_ref(v, keys) for v in p.targets(mc, "InvisibleParts")],
             "context": context,
             "referencedObjects": [dict(self.obj_ref(s, keys), stub=True) for s in sorted(stubs, key=lambda k: p.name(k) or "")],
             "frozenInterfaces": [self.obj_ref(f, keys) for f in frozen],
             "copiedObjects": len(copies),
             "copiedObjectsByType": dict(sorted(by_type.items())),
             "relatedPackage": p.name(rp) if rp else None,
             "categories": [self.obj_ref(c, keys) for c in p.targets(mc, "Categories")],
             "customAttributes": attrs.get("IMasterCopyCustomAttributes") or {},
             "raw": {"coreObjectData": mca.get("CoreObjectData").hex() if blob_len(mca.get("CoreObjectData")) else None,
                     "binaryDataBytes": blob_len(mca.get("BinaryData")),
                     "nonCoreObjectDataBytes": blob_len(mca.get("NonCoreObjectData"))},
             "package": self.mm.type_package(p.objects[mc].rec.type_id)}
        if problems:
            d["decodeProblems"] = problems
        if keys:
            d["key"] = mc
            d["copiedObjectKeys"] = sorted(copies)
        return d

    # --- types / versions / instances -------------------------------------------------------------
    # Findings L1 (samples L01-L09): type object <-> version objects via TypeObjectToVersionObject /
    # VersionObjectToTypeObject, default via TypeToDefaultVersion / DefaultVersionToType (no default while the only
    # version is InWork). State InWork/Committed = ICoreLibObjectAttributes.State of the VERSION (the type has no
    # state; ICoreLibTypeAttributes is not present). FinalizationTime = 0001-01-01 while InWork, release time once
    # Committed (= 'modified' of typesList). Previous/Next are not written by TIA V21. Instance link: the typed
    # block / UDT in the PLC -IsInstanceOf-> version, inverse version -IsTypeOf-> instance (only the version the
    # instance is connected to). Dependencies: version -DependsOn-> version (inverse DependsOnInverse) plus
    # IDependencyInfo.DependencyIdentifiers (blob with name, version GUID and copy name of the used version).
    # A released version holds a content copy (-VisibleParts-> copy with generated name, -Devices-> shell
    # DeviceData, objects with Environment -> version); an InWork version has no copy, its content is the instance.
    def version_ref(self, v):
        """Short reference of a version: type name, version text, GUID, state."""
        p = self.p
        typ = self.rel1(v, "VersionObjectToTypeObject")
        txt = decode_version_data(p.attr(v, "IVersionInfo", "Version"))[0]
        return {"typeName": p.name(typ) if typ else None, "version": txt,
                "versionGuid": p.attr(v, "IVersionInfo", "VersionGuid"),
                "state": p.attr(v, "ICoreLibObjectAttributes", "State")}

    def version_guids(self):
        if not hasattr(self, "_version_guids"):
            self._version_guids = [self.p.attr(k, "IVersionInfo", "VersionGuid") for k in self.p.objects
                                   if self.is_any(k, VERSION_BASES)]
        return self._version_guids

    def version_instances(self, v, in_copies=False):
        """Typed objects connected to version v. Copies inside a master copy or a version content copy (L6: a device
        master copy keeps the IsInstanceOf link of its typed UDT) are excluded, or only returned with in_copies."""
        cand = dict.fromkeys([x for x in self.p.targets(v, "IsTypeOf") if x in self.p.objects] +
                             self.sources(v, "IsInstanceOf"))
        return [x for x in cand if bool(self.environment_mc(x) or self.environment_version(x)) == in_copies]

    def master_copies_containing(self, v):
        """Master copies holding a typed copy connected to version v (Openness MasterCopiesContainingInstances)."""
        return list(dict.fromkeys(self.environment_mc(x) for x in self.version_instances(v, in_copies=True)
                                  if self.environment_mc(x)))

    def instance_versions(self, k):
        """Versions a typed block / UDT is connected to (-IsInstanceOf->, inverse -IsTypeOf-)."""
        p = self.p
        return list(dict.fromkeys([x for x in p.targets(k, "IsInstanceOf") if self.is_any(x, VERSION_BASES)] +
                                  [s for s in self.sources(k, "IsTypeOf") if self.is_any(s, VERSION_BASES)]))

    # Findings L4 (samples L16-L21): the version state of an instance is stored only as its link: -IsInstanceOf->
    # version (inverse -IsTypeOf-), -CurrentLibraryInterface-> the version's interface root '_BIVE:<name>/<VersionGuid>'
    # in the <kind>_TypeLibraryPartContainer, and the expando UserTypeVersionGUID (= VersionGuid of that version) on
    # the instance's own interface root (-CurrentInterface->). "Outdated" is NOT stored: releasing a new version leaves
    # instances in other devices unchanged (L17: no record of the PLC_2 instance changes) and Openness still reports
    # LibraryConformanceStatus "library-conformant". It is derived here from the version numbers. -BlockToVersionElements->
    # (= the own root, inverse VersionElementToBlock) exists only on instances that went through an edit/release in
    # place; an instance (re)created by typeVersionDiscard or CreateFromDocuments lacks it (L16, L19, L21).
    def instance_version_state(self, k, v, keys=False):
        """Version state of instance k connected to version v: the version, the type's default and newest committed
        version, derived flags usesDefaultVersion / newerVersionExists (= outdated) and the interface evidence."""
        p = self.p
        typ = self.rel1(v, "VersionObjectToTypeObject")
        own = self.version_ref(v)
        res = {"typeName": own["typeName"], "version": own["version"], "versionGuid": own["versionGuid"],
               "state": own["state"], "defaultVersion": None, "latestVersion": None, "usesDefaultVersion": None,
               "newerVersionExists": None, "outdated": None}
        if typ:
            allv = list(dict.fromkeys(p.targets(typ, "TypeObjectToVersionObject") +
                                      self.sources(typ, "VersionObjectToTypeObject")))
            committed = [x for x in allv if p.attr(x, "ICoreLibObjectAttributes", "State") == "Committed"]
            default = self.rel1(typ, "TypeToDefaultVersion") or next(iter(self.sources(typ, "DefaultVersionToType")), None)
            latest = max(committed, key=lambda x: version_key(self.version_ref(x)["version"]), default=None)
            res["defaultVersion"] = self.version_ref(default)["version"] if default else None
            res["latestVersion"] = self.version_ref(latest)["version"] if latest else None
            res["usesDefaultVersion"] = v == default if default else None
            if own["state"] == "Committed" and latest:
                res["newerVersionExists"] = version_key(own["version"]) < version_key(res["latestVersion"])
                res["outdated"] = res["newerVersionExists"]
        lib_if = self.rel1(k, "CurrentLibraryInterface")
        cur_if = self.rel1(k, "CurrentInterface")
        cur_exp = p.attributes(cur_if)[0].get("ICoreExpandoAttributes", {}) if cur_if else {}
        res["interface"] = {
            "libraryInterface": p.name(lib_if) if lib_if else None,
            "interfaceRoot": p.name(cur_if) if cur_if else None,
            "interfaceVersionGuid": cur_exp.get("UserTypeVersionGUID"),
            # consistency of the two stored version references (None: no expando, e.g. InWork)
            "interfaceVersionGuidMatches": (cur_exp.get("UserTypeVersionGUID") == own["versionGuid"])
            if cur_exp.get("UserTypeVersionGUID") else None,
            "libraryInterfaceMatches": (p.name(lib_if) or "").endswith("/" + (own["versionGuid"] or "?")) if lib_if else None,
            "hasBlockToVersionElements": bool(p.targets(k, "BlockToVersionElements"))}
        # L4 addendum (L24, L27): updateProject re-creates the instance; -BlockToVersionElements-> then points to the
        # previous instance's interface root, re-created as 'xBIVE:<name>/<old guid>' with the old version GUID
        btve = self.rel1(k, "BlockToVersionElements")
        if btve and btve != cur_if:
            old_guid = p.attributes(btve)[0].get("ICoreExpandoAttributes", {}).get("UserTypeVersionGUID")
            old = next((x for x in self.p.objects if self.is_any(x, VERSION_BASES)
                        and p.attr(x, "IVersionInfo", "VersionGuid") == old_guid), None) if old_guid else None
            res["interface"]["previousInterfaceRoot"] = p.name(btve)
            res["interface"]["previousVersionGuid"] = old_guid
            res["previousVersion"] = self.version_ref(old)["version"] if old else None
        if keys:
            res["interface"]["keys"] = {"libraryInterface": lib_if, "interfaceRoot": cur_if, "version": v}
        return res

    def version(self, v, keys):
        p = self.p
        attrs = p.attributes(v)[0]
        vi = attrs.get("IVersionInfo", {})
        txt, raw, parts = decode_version_data(vi.get("Version"))
        typ = self.rel1(v, "VersionObjectToTypeObject")
        dep_raw = attrs.get("IDependencyInfo", {}).get("DependencyIdentifiers")
        copies = self.sources(v, "Environment")
        d = {"name": p.name(v), "type": p.type_name(v), "package": self.mm.type_package(p.objects[v].rec.type_id),
             "version": txt, "versionParts": parts, "versionRaw": raw, "versionGuid": vi.get("VersionGuid"),
             "finalizationTime": vi.get("FinalizationTime"),
             "minimumTargetDeviceVersion": vi.get("MinimumTargetDeviceVersion"),
             "state": attrs.get("ICoreLibObjectAttributes", {}).get("State"),
             "originalLibrary": attrs.get("ICoreLibObjectAttributes", {}).get("OriginalLibrary"),
             "manufacturer": attrs.get("IVersionManufacturingInfo", {}).get("Manufacturer"),
             "systemVersionOrderNumber": attrs.get("ISystemVersionOrderInfo", {}).get("SystemVersionOrderNumber"),
             "isDefault": bool(p.targets(v, "DefaultVersionToType")) or bool(typ and v in p.targets(typ, "TypeToDefaultVersion")),
             "ofType": p.name(typ) if typ else None,
             "previous": [self.version_ref(x) for x in p.targets(v, "Previous")],
             "next": [self.version_ref(x) for x in p.targets(v, "Next")],
             "dependsOn": [self.version_ref(x) if self.is_any(x, VERSION_BASES) else self.obj_ref(x, keys)
                           for x in p.targets(v, "DependsOn")],
             "usedBy": [self.version_ref(x) for x in
                        dict.fromkeys(p.targets(v, "DependsOnInverse") + self.sources(v, "DependsOn"))
                        if self.is_any(x, VERSION_BASES)],
             "dependencyIdentifiers": parse_dependency_identifiers(dep_raw, self.version_guids()),
             "dependencyIdentifiersRaw": dep_raw.hex() if isinstance(dep_raw, (bytes, bytearray)) else dep_raw,
             "instances": [dict(self.obj_ref(x, keys), device=p.name(self.rel1(x, "Target")) if self.rel1(x, "Target") else None)
                           for x in self.version_instances(v)],
             "masterCopiesContainingInstances": [self.p.name(m) for m in self.master_copies_containing(v)],
             "contents": [self.obj_ref(x, keys) for x in p.targets(v, "VisibleParts")],
             "context": [dict(self.obj_ref(x, keys), isShell=p.attr(x, "ICoreAttributes", "IsShell"))
                         for x in p.targets(v, "Devices")],
             "copiedObjects": len(copies),
             **self.core(v)}
        # ImageLibraryVersionData: HmiImageData -ContentToVersion-> version holds the image (as a project graphic)
        img = next(iter(self.sources(v, "ContentToVersion")), None)
        if img and p.is_a(img, "HmiImageData"):
            from .hmi import image_info  # late import: hmi uses core_text from model
            d["image"] = image_info(p, img)
        # TextListLibraryVersionData: -VersionToContent-> HmiTextListData with entries as in the HMI text lists
        # (Hmi.text_lists; shown by a faceplate text)
        tl = next(iter(p.targets(v, "VersionToContent")), None)
        if tl and p.is_a(tl, "HmiTextListData"):
            from .hmi import _plain  # late import as above
            entries = []
            # extended record layout (typed slots without relation ids) as in Hmi.text_lists
            children = p.targets(tl, "HmiTextListEntryDataChildren") or [
                l.key for l in p.links(tl) if l.relation is None and l.key in p.objects and p.is_a(l.key, "HmiTextListEntryData")]
            for e in children:
                la = p.attributes(e)[0].get("IHmiListEntryAttributes", {})
                text = {}
                for t in p.targets(e, "ExpandoChildren"):
                    if p.is_a(t, "HmiTextData"):
                        text.update(_plain(core_text(p.attr(t, "ICoreTextRepository", "Text"))))
                entries.append({"from": la.get("From"), "to": la.get("To"), "type": la.get("EntryType"),
                                "default": la.get("DefaultEntry"), "text": text})
            d["textList"] = {"entries": entries}
        if keys:
            d["key"] = v
            d["copiedObjectKeys"] = sorted(copies)
        return d

    def lib_type(self, t, keys):
        p = self.p
        attrs = p.attributes(t)[0]
        lt = attrs.get("ICoreLibTypeAttributes", {})
        ti = attrs.get("ITypeInfo", {})
        tb = attrs.get("ITypeBaseAttributesInfo", {})
        # system types (CoreLibraryType) have no version objects; they carry type / version id and origin instead
        ct = attrs.get("ICoreTypeObjectAttributes", {})
        lo = attrs.get("ICoreLibObjectAttributes", {})
        sysf, path = self.folder_info(t)
        major = lt.get("MajorVersion")
        ver = None if major is None else "%s.%s.%s" % (major, lt.get("MinorVersion"), lt.get("SubminorVersion"))
        versions = p.targets(t, "TypeObjectToVersionObject") + self.sources(t, "VersionObjectToTypeObject")
        versions = [self.version(v, keys) for v in dict.fromkeys(versions)]
        versions.sort(key=lambda x: version_key(x["version"]))
        default = self.rel1(t, "TypeToDefaultVersion") or next(iter(self.sources(t, "DefaultVersionToType")), None)
        states = {v["state"] for v in versions}
        committed = [v["version"] for v in versions if v["state"] == "Committed"]
        latest = committed[-1] if committed else None  # versions are sorted by number
        n_inst = sum(len(v["instances"]) for v in versions)
        n_old = sum(len(v["instances"]) for v in versions if v["state"] == "Committed" and latest
                    and version_key(v["version"]) < version_key(latest))
        d = {"name": p.name(t), "type": p.type_name(t), "opennessKind": OPENNESS_TYPE_KIND.get(p.type_name(t)),
             # user = Library.UserTypes TypeObjectBase (typesList); system = CoreLibraryType, e.g. the instruction
             # types TON/TP/... TIA puts into LibrarySystemTypeSystemFolder when they are used (samples 030-032)
             "category": "user" if p.is_a(t, "TypeObjectBase") else "system",
             "package": self.mm.type_package(p.objects[t].rec.type_id),
             "systemFolder": sysf, "folderPath": path,
             # ICoreLibTypeAttributes (CoreLibraryType, e.g. system types) - not present on UserTypes objects
             "coreLibVersion": ver, "coreLibState": lt.get("State"),
             "typeObjectId": ct.get("TypeObjectId"), "versionId": ct.get("VersionId"),
             "originalLibrary": lo.get("OriginalLibrary"), "libObjectState": lo.get("State"),
             # derived: the type is InWork while one of its versions is InWork
             "state": "InWork" if "InWork" in states else ("Committed" if states else None),
             "typeGuid": ti.get("TypeGuid"), "originalTypeGuid": tb.get("OriginalTypeGuid"),
             "typeNamespace": tb.get("TypeNamespace"), "isObsolete": tb.get("IsObsolete"),
             "isSetAsNonUpdateable": tb.get("IsSetAsNonUpdateable"),
             "defaultVersion": decode_version_data(p.attr(default, "IVersionInfo", "Version"))[0] if default else None,
             # L4: highest committed version number; instances connected to a lower one are outdated (derived)
             "latestVersion": latest, "instanceCount": n_inst, "outdatedInstances": n_old,
             "dependsOn": [self.obj_ref(x, keys) for x in p.targets(t, "DependsOn")],
             "contents": [self.obj_ref(x, keys) for x in p.targets(t, "VisibleParts")],
             "versions": versions,
             **self.core(t)}
        if keys:
            d["key"] = t
        return d

    def types_and_versions(self, keys):
        p = self.p
        types, versions, in_mc, in_ver = [], [], 0, 0
        for k in p.objects:
            if self.environment_version(k):
                in_ver += 1
            if self.is_any(k, TYPE_BASES):
                (types if not self.environment_mc(k) else []).append(k)
                in_mc += bool(self.environment_mc(k))
            elif self.is_any(k, VERSION_BASES):
                (versions if not self.environment_mc(k) else []).append(k)
                in_mc += bool(self.environment_mc(k))
        n_inst_capable = 0
        inst = []
        for k in p.objects:
            if not self.is_any(k, INSTANCE_BASES):
                continue
            n_inst_capable += 1
            if self.environment_version(k) or self.environment_mc(k):
                continue  # content copy of a version / master copy, not an instance
            vers = self.instance_versions(k)
            if not vers:
                continue
            d = dict(self.obj_ref(k, keys), instanceOf=[self.version_ref(v) for v in vers])
            tgt = self.rel1(k, "Target")
            d["device"] = p.name(tgt) if tgt else None
            d["creationTime"] = p.attr(k, "ICoreAttributes", "CreationTime")
            d["versionState"] = self.instance_version_state(k, vers[0], keys)
            inst.append(d)
        typed = [self.lib_type(t, keys) for t in types]
        owned = {v for t in types for v in p.targets(t, "TypeObjectToVersionObject") + self.sources(t, "VersionObjectToTypeObject")}
        orphan = [self.version(v, keys) for v in versions if v not in owned]
        key = lambda x: (x[0].get("systemFolder") or "", x[0].get("folderPath") or "", x[0]["name"] or "")  # noqa: E731
        return (sorted(zip(typed, types), key=key), sorted(orphan, key=lambda x: x["name"] or ""),
                sorted(inst, key=lambda x: (x["type"], x["name"] or "")),
                {"instanceCapableObjects": n_inst_capable, "libraryObjectsInsideMasterCopies": in_mc,
                 "objectsInsideVersions": in_ver})

    # --- all --------------------------------------------------------------------------------------
    def to_dict(self, keys=False):
        p = self.p
        libs = []
        for L in sorted(p.of_type("CoreLibrary")):
            if self.environment_mc(L):
                continue
            la = p.attributes(L)[0]
            seen = set()
            sysfolders = sorted(p.targets(L, "Folders") + [s for s in self.sources(L, "FolderParent") if p.is_a(s, "FolderData")],
                                key=lambda k: (SYSTEM_FOLDER_ORDER.index(p.attr(k, "ICoreAttributes", "Subtype"))
                                               if p.attr(k, "ICoreAttributes", "Subtype") in SYSTEM_FOLDER_ORDER else 99,
                                               p.name(k) or ""))
            folders = [self.folder(f, "", keys, seen) for f in dict.fromkeys(sysfolders)]
            d = {"name": p.name(L), "libraryType": la.get("ICoreLibAttributes", {}).get("LibraryType"),
                 "isWriteProtected": la.get("ICoreLibAttributes", {}).get("IsWriteProtected"),
                 "isContentWriteProtected": la.get("ILibraryProtectionInfo", {}).get("IsLibraryContentWriteProtected"),
                 **self.core(L), "systemFolders": folders, "masterCopies": [], "types": [], "versionsWithoutType": []}
            if keys:
                d["key"] = L
            libs.append(d)
        by_key = {}
        for i, L in enumerate(k for k in sorted(p.of_type("CoreLibrary")) if not self.environment_mc(k)):
            by_key[L] = libs[i]
        other = {"masterCopies": [], "types": [], "versionsWithoutType": []}
        mcs = [m for m in p.of_type("CoreLibraryMasterCopy") if not self.environment_mc(m)]
        for m in mcs:
            by_key.get(self.library_of(m), other)["masterCopies"].append(self.master_copy(m, keys))
        types, orphan, inst, stats = self.types_and_versions(keys)
        for t, tk in types:
            by_key.get(self.library_of(tk), other)["types"].append(t)
        other["versionsWithoutType"].extend(orphan)
        for L in libs:
            L["masterCopies"].sort(key=lambda x: (x["systemFolder"] or "", x["folderPath"],
                                                  x["folderIndex"] if x["folderIndex"] is not None else 1 << 30,
                                                  x["name"] or ""))
        lib_bases = TYPE_BASES + VERSION_BASES + ("CoreLibraryMasterCopy", "CoreLibrary")
        deleted = {}
        for k in p.deleted:
            t = self.mm.types.get(key_type_id(k))
            if t and any(a.endswith("." + b) for a in self.mm.ancestors(t.full_name) for b in lib_bases):
                deleted[t.name] = deleted.get(t.name, 0) + 1
        res = {"libraries": libs, "instances": inst,
               "statistics": dict(stats, masterCopies=len(mcs), types=len(types),
                                  versions=sum(len(t["versions"]) for t, _ in types) + len(orphan),
                                  deletedLibraryObjectsHidden=deleted)}
        if any(other.values()):
            res["unassigned"] = other
        return res


def extract_library(store, keys=False):
    """Library view of an openpedata Project (or Model). keys=True adds record keys for debugging."""
    return LibraryView(store).to_dict(keys)


# --- text output ----------------------------------------------------------------------------------
def _fmt_obj(o):
    s = "%s %r" % (o.get("blockType") and "%s/%s" % (o["type"], o["blockType"]) or o["type"], o["name"])
    return s


def to_text(d):
    out = []
    for L in d["libraries"]:
        out.append("Library %r (%s)%s" % (L["name"], L["libraryType"], " write-protected" if L["isWriteProtected"] else ""))

        def walk(f, ind):
            out.append("%s[%s]%s" % ("  " * ind, f["name"], "" if f["name"] == f["subtype"] else " (%s)" % f["subtype"]))
            for e in f["elements"]:
                out.append("%s- %s" % ("  " * (ind + 1), _fmt_obj(e)))
            for s in f["folders"]:
                walk(s, ind + 1)
        for f in L["systemFolders"]:
            walk(f, 1)
        for m in L["masterCopies"]:
            out.append("  Master copy %r in %s%s" % (m["name"], m["systemFolder"], "/" + m["folderPath"] if m["folderPath"] else ""))
            out.append("    subtype %s, state %s, author %s, created %s, modified %s" % (
                m["subtype"], m["state"], m["author"], m["creationTime"], m["modifiedTime"]))
            src = m["source"] or {}
            for td in src.get("typeDescriptors", []):
                out.append("    source type %s roles %s" % (td.get("typeName"), ",".join(td.get("roles", []))))
            if src.get("sourceScopePath"):
                out.append("    source scope " + " / ".join("%s=%s" % (s["type"], s["id"]) for s in src["sourceScopePath"]))
            for c in m["contents"]:
                out.append("    contains %s" % _fmt_obj(c))
            for c in m["context"]:
                out.append("    context  %s via %s" % (_fmt_obj(c), c["relation"]))
            for c in m.get("referencedObjects", []):
                out.append("    references %s (name stub)" % _fmt_obj(c))
            for c in m.get("frozenInterfaces", []):
                out.append("    frozen interface %r" % c["name"])
            co = m.get("coreObjects") or {}
            for e in co.get("entries", []):
                out.append("    clipboard object %s sourceId %s parent %s visualPartIndex %s%s" % (
                    e["objectRole"], e["sourceObjectId"], e["parentObjectId"], e["visualPartIndex"],
                    " option %r" % e["option"] if e["option"] else ""))
            if co.get("error"):
                out.append("    CoreObjectData not decoded: %s" % co["error"])
            for sc in src.get("sourceScopePath", []):
                if sc.get("sourceKey"):
                    so = sc.get("sourceObject")
                    out.append("    scope %s = %s %s" % (sc["type"], sc["sourceKey"], _fmt_obj(so) if so else "(not in project)"))
            out.append("    copied objects: %d" % m["copiedObjects"])
        for t in L["types"]:
            if t["category"] == "system":
                # no versions / default / TypeGuid (IVersionInfo), the core library attributes instead
                out.append("  System type %r (%s) in %s%s version %s (%s) typeObjectId %s versionId %s from %s" % (
                    t["name"], t["type"], t["systemFolder"], "/" + t["folderPath"] if t["folderPath"] else "",
                    t["coreLibVersion"], t["coreLibState"], t["typeObjectId"], t["versionId"], t["originalLibrary"]))
                for c in t["contents"]:
                    out.append("    contains %s" % _fmt_obj(c))
                if t["dependsOn"]:
                    out.append("    depends on %s" % ", ".join(x["name"] for x in t["dependsOn"]))
                continue
            out.append("  Type %r (%s / %s) in %s%s guid %s state %s default %s latest %s instances %d (outdated %d)" % (
                t["name"], t["type"], t["opennessKind"], t["systemFolder"], "/" + t["folderPath"] if t["folderPath"] else "",
                t["typeGuid"], t["state"], t["defaultVersion"], t.get("latestVersion"), t.get("instanceCount", 0),
                t.get("outdatedInstances", 0)))
            for v in t["versions"]:
                out.append("    version %s %s%s finalized %s guid %s author %s" % (
                    v["version"], v["state"], " (default)" if v["isDefault"] else "", v["finalizationTime"],
                    v["versionGuid"], v["author"]))
                if v["comment"]:
                    out.append("      comment %s" % v["comment"])
                for c in v["contents"]:
                    out.append("      content copy %s%s" % (_fmt_obj(c), " = %r" % c["customerName"] if c.get("customerName") else ""))
                for x in v["dependsOn"]:
                    out.append("      depends on %s %s (%s)" % (x.get("typeName"), x.get("version"), x.get("versionGuid")))
                for x in v["usedBy"]:
                    out.append("      used by %s %s" % (x.get("typeName"), x.get("version")))
                for x in v["instances"]:
                    out.append("      instance %s on %s" % (_fmt_obj(x), x.get("device")))
                for x in v.get("masterCopiesContainingInstances", []):
                    out.append("      typed copy inside master copy %r" % x)
    if d.get("unassigned"):
        out.append("Unassigned: %s" % json.dumps(d["unassigned"], ensure_ascii=False, default=str)[:2000])
    for i in d["instances"]:
        vs = i.get("versionState") or {}
        flags = []
        if vs.get("outdated"):
            flags.append("OUTDATED, latest %s" % vs.get("latestVersion"))
        if vs.get("usesDefaultVersion") is False:
            flags.append("not default %s" % vs.get("defaultVersion"))
        if (vs.get("interface") or {}).get("previousInterfaceRoot"):
            flags.append("updated from %s" % (vs.get("previousVersion") or vs["interface"].get("previousVersionGuid")))
        itf = vs.get("interface") or {}
        if itf.get("interfaceVersionGuidMatches") is False or itf.get("libraryInterfaceMatches") is False:
            flags.append("INTERFACE VERSION MISMATCH")
        out.append("Instance %s on %s -> %s%s" % (_fmt_obj(i), i["device"], ", ".join(
            "%s %s %s" % (x["typeName"], x["version"], x["state"]) for x in i["instanceOf"]),
            " (%s)" % "; ".join(flags) if flags else ""))
    out.append("Statistics: %s" % json.dumps(d["statistics"], ensure_ascii=False))
    return "\n".join(out)


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__.split("\n\n")[1].strip(), file=sys.stderr)
        return 2
    d = extract_library(Project(args[0]), keys="--keys" in argv)
    sys.stdout.reconfigure(encoding="utf-8")
    if "--json" in argv:
        print(json.dumps(d, indent=1, ensure_ascii=False, default=str))
    else:
        print(to_text(d))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
