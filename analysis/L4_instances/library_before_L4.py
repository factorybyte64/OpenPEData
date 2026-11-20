"""L5 preparation: project library view (folders, master copies, types / versions) of a TIA Portal V21 project.

Usage: python library.py <project folder | .ap21 | .zap21> [--json] [--keys]

Read-only use of parser/openpedata (not integrated there yet). extract_library(store) takes an
openpedata.project.Project (or an openpedata.model.Model, its .p is used) and returns a dict.

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

Deleted objects (tombstones) are not in Project.objects; links to them are dropped. The package of a type is
resolved from the metamodel XML (Metamodel.packages is keyed by id >> 16 and merges packages sharing that half).
"""
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "parser"))

from openpedata.metamodel import NS  # noqa: E402
from openpedata.model import core_text  # noqa: E402
from openpedata.project import Project  # noqa: E402

TYPE_BASES = ("TypeObjectBase", "CoreLibraryType")
VERSION_BASES = ("VersionObjectBase",)
INSTANCE_BASES = ("InstanceObject",)
# metamodel type of the type object -> LibraryType kind as Openness reports it (TiaProjectLibrary typesList, L1)
OPENNESS_TYPE_KIND = {"UserTypeDataByCopyTypeObject": "PlcTypeLibraryType",
                      "CodeBlockDataByCopyTypeObject": "CodeBlockLibraryType"}
SYSTEM_FOLDER_ORDER = ("LibraryTypeSystemFolder", "LibraryMasterCopySystemFolder", "LibrarySystemTypeSystemFolder",
                       "LibrarySystemMasterCopySystemFolder", "CommonDataFolder")
# VersionData structure elements in metamodel declaration order (encoding not yet seen in a sample)
VERSION_ELEMENTS = ("Build", "Major", "Minor", "Revision")


def type_packages(mm):
    """type id -> name of the Package element that declares it (from the metamodel XML)."""
    cache = mm.__dict__.get("_l5_type_packages")
    if cache is None:
        cache = {}
        for pkg in ET.fromstring(mm.xml).iter(NS + "Package"):
            for ns in pkg.iter(NS + "Namespace"):
                for el in ns:
                    if el.get("id"):
                        cache[int(el.get("id"), 16)] = pkg.get("name")
        mm.__dict__["_l5_type_packages"] = cache
    return cache


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
    """IVersionInfo.Version (structure VersionData), confirmed by L1 (0.0.1, 1.0.0, 1.2.3, 2.3.4): 19 bytes =
    3-byte prefix (always 00 00 00) + 4 x i32 little endian in metamodel declaration order Build, Major, Minor,
    Revision; Revision -1 = not set (like System.Version with three parts). Text = Major.Minor.Build[.Revision].
    L2: the "prefix" is the rest of the u32 LE total length (20) of the structure value, whose first byte the parser
    reads as varint (see struct_value).
    Returns (text or None, raw hex, dict of the four parts or None)."""
    if raw is None:
        return None, None, None
    if isinstance(raw, dict):
        v = raw
    elif isinstance(raw, (bytes, bytearray)) and len(raw) in (16, 19):
        import struct
        v = dict(zip(VERSION_ELEMENTS, struct.unpack("<4i", bytes(raw[-16:]))))
    else:
        return None, raw.hex() if isinstance(raw, (bytes, bytearray)) else str(raw), None
    txt = "%s.%s.%s" % (v.get("Major"), v.get("Minor"), v.get("Build"))
    if isinstance(v.get("Revision"), int) and v["Revision"] >= 0:
        txt += ".%d" % v["Revision"]
    return txt, raw.hex() if isinstance(raw, (bytes, bytearray)) else None, v


CLIPBOARD_ROLES = {0: "None", 1: "SelectedObject", 2: "ViewSpecificParent", 4: "ExtendedObject", 8: "ViewObject"}


def struct_value(raw):
    """Full bytes of a structure / array attribute value (L2). Such a value starts with a u32 LE total length
    (incl. itself) like an AttributeSet block; attrs.read_var takes its first byte for the varint length prefix and
    returns the rest, so the value appears with a 3-byte prefix 00 00 00. Returns the reconstructed bytes (offsets
    inside the value are relative to its start) or None. Only valid for values shorter than 128 bytes (a longer
    length byte has the varint continuation bit set and is misread by the parser)."""
    import struct
    if not isinstance(raw, (bytes, bytearray)) or not raw:
        return None
    full = bytes([(len(raw) + 1) & 0xFF]) + bytes(raw)
    return full if len(full) >= 4 and struct.unpack_from("<I", full, 0)[0] == len(full) else None


def decode_core_object_data(raw):
    """ICoreLibMasterCopyAttributes.CoreObjectData = ArrayLibraryClipboardCoreObject (L2, samples 021, L10-L15):
    u32 total length, u32 count, count x u32 offset of the element (relative to the value start), elements. Element
    = structure LibraryClipboardCoreObject encoded like a set block: u32 length (22), ObjectRole i16 (enum
    LibraryClipboardObjectRole), Option string (u32 offset, 0 = null), ParentObjectId i32, SourceObjectId i32,
    VisualPartIndex i32. Returns {"count", "entries": [...]} or {"error", "hex"}."""
    import struct
    full = struct_value(raw)
    if full is None:
        return None if raw is None else {"error": "no u32 length head", "hex": bytes(raw).hex()}
    try:
        (count,) = struct.unpack_from("<I", full, 4)
        offs = struct.unpack_from("<%dI" % count, full, 8)
        entries = []
        for off in offs:
            ln, role, opt, parent, src, vpi = struct.unpack_from("<IhIiii", full, off)
            option = None
            if opt:
                from openpedata.attrs import read_var
                o, _ = read_var(full, off + opt)
                option = o.decode("utf-8", "replace") if o is not None else None
            entries.append({"objectRole": CLIPBOARD_ROLES.get(role, role), "option": option,
                            "parentObjectId": parent, "sourceObjectId": src, "visualPartIndex": vpi,
                            "elementLength": ln})
        return {"count": count, "entries": entries}
    except struct.error as e:
        return {"error": str(e), "hex": full.hex()}


def decode_struct_array_empty(raw):
    """NonCoreObjectData (ArrayLibraryClipboardNonCoreObject): only the empty array is seen (u32 length 8, count 0).
    Returns the element count or None."""
    import struct
    full = struct_value(raw)
    return struct.unpack_from("<I", full, 4)[0] if full and len(full) >= 8 else None


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
        self.pkg = type_packages(self.mm)
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
             "package": self.pkg.get(tid)}
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
             "coreObjects": decode_core_object_data(mca.get("CoreObjectData")),
             "nonCoreObjects": decode_struct_array_empty(mca.get("NonCoreObjectData")),
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
             "package": self.pkg.get(p.objects[mc].rec.type_id)}
        # H-020 (decided in L2): slots are ordered by the ordinal-sorted qualified AttributeSet name; parser uses the
        # declaration order. LibraryMasterCopyData is the only PLC type where both differ (slots 0-3); they were empty
        # in all samples. Flag a filled one, its decoding by the parser would then be wrong.
        o = p.objects[mc]
        decl = self.mm.attribute_sets_ordered(o.rec.type_id)
        alpha = sorted(decl)
        filled = [i for i in range(len(decl)) if decl[i] != alpha[i] and i in o.blocks]
        if filled:
            d["slotOrderWarning"] = {"filledContestedSlots": filled,
                                     "setsByOrdinalOrder": [alpha[i].split(".")[-1] for i in filled]}
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

    def version_instances(self, v):
        return list(dict.fromkeys([x for x in self.p.targets(v, "IsTypeOf") if x in self.p.objects] +
                                  self.sources(v, "IsInstanceOf")))

    def version(self, v, keys):
        p = self.p
        attrs = p.attributes(v)[0]
        vi = attrs.get("IVersionInfo", {})
        txt, raw, parts = decode_version_data(vi.get("Version"))
        typ = self.rel1(v, "VersionObjectToTypeObject")
        dep_raw = attrs.get("IDependencyInfo", {}).get("DependencyIdentifiers")
        copies = self.sources(v, "Environment")
        d = {"name": p.name(v), "type": p.type_name(v), "package": self.pkg.get(p.objects[v].rec.type_id),
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
             "instances": [self.obj_ref(x, keys) for x in self.version_instances(v)],
             "contents": [self.obj_ref(x, keys) for x in p.targets(v, "VisibleParts")],
             "context": [dict(self.obj_ref(x, keys), isShell=p.attr(x, "ICoreAttributes", "IsShell"))
                         for x in p.targets(v, "Devices")],
             "copiedObjects": len(copies),
             **self.core(v)}
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
        sysf, path = self.folder_info(t)
        major = lt.get("MajorVersion")
        ver = None if major is None else "%s.%s.%s" % (major, lt.get("MinorVersion"), lt.get("SubminorVersion"))
        versions = p.targets(t, "TypeObjectToVersionObject") + self.sources(t, "VersionObjectToTypeObject")
        versions = [self.version(v, keys) for v in dict.fromkeys(versions)]
        versions.sort(key=lambda x: version_key(x["version"]))
        default = self.rel1(t, "TypeToDefaultVersion") or next(iter(self.sources(t, "DefaultVersionToType")), None)
        states = {v["state"] for v in versions}
        d = {"name": p.name(t), "type": p.type_name(t), "opennessKind": OPENNESS_TYPE_KIND.get(p.type_name(t)),
             # user = Library.UserTypes TypeObjectBase (typesList); system = CoreLibraryType, e.g. the instruction
             # types TON/TP/... TIA puts into LibrarySystemTypeSystemFolder when they are used (samples 030-032)
             "category": "user" if p.is_a(t, "TypeObjectBase") else "system",
             "package": self.pkg.get(p.objects[t].rec.type_id),
             "systemFolder": sysf, "folderPath": path,
             # ICoreLibTypeAttributes (CoreLibraryType, e.g. system types) - not present on UserTypes objects
             "coreLibVersion": ver, "coreLibState": lt.get("State"),
             # derived: the type is InWork while one of its versions is InWork
             "state": "InWork" if "InWork" in states else ("Committed" if states else None),
             "typeGuid": ti.get("TypeGuid"), "originalTypeGuid": tb.get("OriginalTypeGuid"),
             "typeNamespace": tb.get("TypeNamespace"), "isObsolete": tb.get("IsObsolete"),
             "isSetAsNonUpdateable": tb.get("IsSetAsNonUpdateable"),
             "defaultVersion": decode_version_data(p.attr(default, "IVersionInfo", "Version"))[0] if default else None,
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
            vers = list(dict.fromkeys([x for x in p.targets(k, "IsInstanceOf") if self.is_any(x, VERSION_BASES)] +
                                      [s for s in self.sources(k, "IsTypeOf") if self.is_any(s, VERSION_BASES)]))
            if not vers:
                continue
            d = dict(self.obj_ref(k, keys), instanceOf=[self.version_ref(v) for v in vers])
            tgt = self.rel1(k, "Target")
            d["device"] = p.name(tgt) if tgt else None
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
            if m.get("slotOrderWarning"):
                out.append("    WARNING H-020: %s" % m["slotOrderWarning"])
            out.append("    copied objects: %d" % m["copiedObjects"])
        for t in L["types"]:
            out.append("  Type %r (%s / %s) in %s%s guid %s state %s default %s" % (
                t["name"], t["type"], t["opennessKind"], t["systemFolder"], "/" + t["folderPath"] if t["folderPath"] else "",
                t["typeGuid"], t["state"], t["defaultVersion"]))
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
                    out.append("      instance %s" % _fmt_obj(x))
    if d.get("unassigned"):
        out.append("Unassigned: %s" % json.dumps(d["unassigned"], ensure_ascii=False, default=str)[:2000])
    for i in d["instances"]:
        out.append("Instance %s on %s -> %s" % (_fmt_obj(i), i["device"], ", ".join(
            "%s %s %s" % (x["typeName"], x["version"], x["state"]) for x in i["instanceOf"])))
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
