"""WinCC Unified HMI software of a device: connections, tag tables and tags, alarm classes, discrete and analog
alarms, screens (samples 100-114, steps U1-U4).

Navigation (relation names from the metamodel):
  HmiApplicationData 0x00101006     the HMI software (ICoreAttributes.Name, IDeviceItemData type/MLFB/firmware);
                                    LogicalContainer -> DeviceData; every object below has Target -> it
  HmiConnectionData  0x00b21002     connection (Subtype Integrated), IHmiConnectionAttributes.PlcId
  HmiTagTableData    0x00e51004     tag table; AggregatingFolder -> FolderData (Subtype HmiTag.Subfolder = group)
  HmiTagData         0x00e51008     tag, TagTable -> table; data type IStructureItem.DisplayTypeName;
                                    IHmiTagAttributes_Connection -> connection (none = internal tag);
                                    IHmiTagAttributes_AcquisitionCycle -> HmiCycleData (name T100ms ... T10s);
                                    PLC tag: ScopedLinkMaintainerData with Consumer -> tag, Provider -> EAMTZTagData;
                                    ITagAddress.LogicalAddress = copy of the PLC address; comment ICoreAttributes.Comment
                                    (CoreText, value XHTML <body><p>...</p></body>)
  HmiAlarmClassData  0x00c01002     alarm class; HmiOnlyAlarmClassPeAlarmClassRelation -> AlarmServices
                                    AlarmClassData 0x00401003, its IAlarmClassData.Id = Openness Id
  HmiDiscreteAlarmData 0x00c01009 / HmiAnalogAlarmData 0x00c01006
                                    ICoreAttributes.Name, IAlarmData.AlarmId,
                                    HmiAlarmClassDataRelation -> class, I...AlarmAttributes_TriggerTag -> tag,
                                    text: ExpandoChildren -> HmiTextData ICoreTextRepository.Text (CoreText)
  ILScreenData       0x00cd1002     screen; AggregatingFolder -> FolderData (ILScreenFolder, Subtype ILScreen.Subfolder
                                    = screen group). ICoreAttributes (needs Metamodel.ancestors_topological);
                                    properties in the expando IPropertyExpandoAttributes (see screen_properties)
"""
import re
import base64
import html
import struct
import uuid

from . import attrs as attrcodec
from .model import core_text
from . import styles as stylecodec

_TAG = re.compile(r"<[^>]+>")
# Expando dictionary "names" of Unified properties (raw bytes) -> (Openness property, value kind); derived with
# tools\unified_propmap.py from F01_hmi_allobjects and samples 115-124, every entry 100 % equal to the truth.
# int: value inline in the offset field (code 5/19); color: varint 8 + 3 bytes + u32 ARGB (code 90125);
# str: varint(n+1) + UTF-8 (code 11). Not yet identified: 08, 0e, 0103, 0402, 0d02, 3409, 4f02, 7904.
PROPERTY_NAMES = {
    b"\x05": ("Width", "int"), b"\x06": ("Height", "int"), b"\x15": ("CenterX", "int"), b"\x16": ("CenterY", "int"),
    b"\x2d": ("AngleRange", "int"), b"\x2e": ("StartAngle", "int"), b"\x63": ("Left", "int"), b"\x64": ("Top", "int"),
    b"\x66": ("BackColor", "color"), b"\x69": ("BorderColor", "color"), b"\x78": ("Radius", "int"),
    b"\x7c": ("RadiusY", "int"), b"\x7d": ("RadiusX", "int"), b"\x7f": ("X2", "int"), b"\x02\x02": ("Y2", "int"),
    # 0502 X1, 0702 Y1 (code 5)
    b"\x05\x02": ("X1", "int"), b"\x07\x02": ("Y1", "int"),
    # 0102 EndType, 0602 StartType (code 5, HmiLineEndType 2 = Arrow; same lines, Openness StartType/EndType)
    b"\x01\x02": ("EndType", "enum"), b"\x06\x02": ("StartType", "enum"),
    b"\x04\x03": ("FlashingColor", "color"), b"\x23\x02": ("ForeColor", "color"), b"\x2b\x21": ("LineColor", "color"),
    b"\x43\x02": ("OutputFormat", "str"), b"\x49\x02": ("ProcessValue", "str"),
    # U10 (samples 128/129 and F01): code 2/18 inline, Points list, Text and SelectionItems as references
    b"\x04\x02": ("LineWidth", "int"), b"\x0e": ("TabIndex", "int"), b"\x0d\x02": ("Points", "points"),
    b"\x4f\x02": ("Text", "ref"), b"\x34\x09": ("SelectionItems", "selection"),
    # from the Faceplate thread (F01..F03): 08 = layer (reference whose value is the layer GUID), 1f CaptionColor,
    # 7508 StyleItemClass
    b"\x08": ("Layer", "ref"), b"\x1f": ("CaptionColor", "color"), b"\x75\x08": ("StyleItemClass", "str"),
    # Z1.5 R3 (F40): 7402 Graphic of graphic view and button; the reference value (u32 big endian + 1 byte) indexes
    # the item's ExpandoReferences links -> HmiImageData (project graphic)
    b"\x74\x02": ("Graphic", "ref"),
    # Z1.5 (F40): 050c Screen of a screen window, static value = reference -> the shown ILScreenData
    b"\x05\x0c": ("Screen", "ref"),
    # Z1.5 (F40, Openness 164 Screen_R8 ScrWin_V): 07 Visible, code 1 inline, stored only when false
    b"\x07": ("Visible", "bool"),
    # Z1.5 (F40, Openness 164 Screen_R8 ScrWin_V/ScrWin_F): 0409 WindowFlags of a screen window, code 5 inline,
    # stored only when not the default; HmiWindowFlag bits (17 = ShowCaption, CanMove; 10 = ShowBorder, CanSize)
    b"\x04\x09": ("WindowFlags", "flags"),
    # Z1.5 (164 Screen_R8, Openness): 7502 BarMode of bar/gauge/slider, code 5 inline; default Unicolor not stored
    b"\x75\x02": ("BarMode", "barmode"),
    # Z1.5 (164 Screen_R8, Openness OpBtn_05/OpBtn_0): 0d Opacity, code 7, float32 inline (0.5 -> 0x3F000000)
    b"\x0d": ("Opacity", "float"),
    # Z1.5 (164 Screen_R8, Openness AlTxt_CB/AlTxt_RT): 7a08 HorizontalTextAlignment, 7908 VerticalTextAlignment,
    # code 5 inline, Openness enum values (Center 1 / Right 2, Top 0 / Bottom 2)
    b"\x7a\x08": ("HorizontalTextAlignment", "enum"), b"\x79\x08": ("VerticalTextAlignment", "enum"),
    # Z1.5 (164 Screen_R8, Openness SymOut): 7f08 IOFieldType, code 5 inline, 0 = Output (InputOutput not stored)
    b"\x7f\x08": ("IOFieldType", "enum"),
    # 6e BorderWidth (F40 dynamized; Openness 164 Screen_R8 Bw0/Bw3: 0 -> 0, 3 -> 3): code 2 = 1 byte inline
    b"\x6e": ("BorderWidth", "int"),
    # 0a RotationAngle, code 4 inline (RotationAngle 270 -> 270)
    b"\x0a": ("RotationAngle", "int"),
    # 56 ResourceList of a symbolic I/O field: reference -> ExpandoReferences -> HmiTextListData (Openness
    # ResourceList = the linked text list)
    b"\x56": ("ResourceList", "ref"),
}
BAR_MODES = {0: "Segmented", 2: "SegmentedStatic"}  # confirmed values; others stay numbers
# property ids seen only as dynamized properties so far (_#Dynamics), value kind unknown
# F40 (against the truth): 6e BorderWidth, 6e24 IsAlternateState, 71 AlternateBackColor, 050c Screen (screen window;
# Range values are screen references), 7402 Graphic (graphic view, ResourceList)
DYNAMIC_ONLY_NAMES = {b"\x07": ("Visible", None), b"\x6e": ("BorderWidth", None), b"\x6e\x24": ("IsAlternateState", None),
                      b"\x71": ("AlternateBackColor", None), b"\x05\x0c": ("Screen", None), b"\x74\x02": ("Graphic", None)}
# nested expandos: type codes whose value sits inline in the value table, with its size
NESTED_INLINE = {0x01: 1, 0x02: 1, 0x05: 4, 0x0C: 16, 0x13: 4, 0x14: 8}  # 0x02: 1-byte value (F40 ranges/expressions),
# 0x14: u64 inline (Relevant/Condition bit masks of Singlebit mapping entries, sample F01)
# logging (samples 136-144): HmiBackupMode, HmiLoggingMode as stored numbers
BACKUP_MODES = {0: "NoBackup", 1: "PrimaryPath"}
LOGGING_MODES = {0: "OnChange", 1: "Cyclic"}
# MappingTable.ConditionType (Openness names); only Range is verified (sample 132)
CONDITION_TYPES = {0: "None", 1: "Range", 5: "Singlebit", 6: "Expression"}  # 6: F40 Expression dynamizations


def _plain(texts):
    """CoreText values of HMI texts are XHTML (<body><p>..</p></body>); drop markup and empty cultures."""
    out = {}
    for c, t in texts.items():
        t = _TAG.sub("", t or "")
        if t:
            out[c] = t
    return out


def image_info(p, k):
    """HmiImageData k (project graphic or content of an ImageLibraryVersionData): link
    IHmiImageAttributes_DefaultInternalImage -> HmiInternalImageData with FileExtension, ImageSize = u32 size + u32 width
    + u32 height, GenuineContent = the original file as paged blob (attrs.decode_blob: 4096-byte pages, zlib per page).
    data = the file, base64."""
    ii = next(iter(p.targets(k, "IHmiImageAttributes_DefaultInternalImage")), None)
    a = p.attributes(ii)[0].get("IHmiInternalImageAttributes", {}) if ii else {}
    size = a.get("ImageSize")
    w = h = None
    if isinstance(size, (bytes, bytearray)) and len(size) >= 12:
        w, h = struct.unpack_from("<II", size, 4)
    blob = a.get("GenuineContent")
    data = (attrcodec.decode_blob(blob) or None) if isinstance(blob, (bytes, bytearray)) else None
    return {"name": p.name(k), "extension": a.get("FileExtension"), "type": a.get("ImageType"),
            "width": w, "height": h, "bytes": len(data) if data else None,
            "data": base64.b64encode(data).decode("ascii") if data else None, "key": k}


def library_image_name(p, k):
    """ImageInstanceData k -IsInstanceOf-> ImageLibraryVersionData -VersionObjectToTypeObject-> ImageLibraryTypeData:
    the name of the image type (library graphic), None when unresolved."""
    v = next(iter(p.targets(k, "IsInstanceOf")), None)
    t = next(iter(p.targets(v, "VersionObjectToTypeObject")), None) if v else None
    return p.name(t) if t else None


_WEIGHTS = {100: "Thin", 200: "ExtraLight", 300: "Light", 400: "Normal", 500: "Medium", 600: "SemiBold", 700: "Bold",
            800: "ExtraBold", 900: "Black"}


# StraightScale part of slider and bar (F40 1116 Slider_1: part 0x2338, TypeId 93433): only values that differ from
# the default are stored; 0x3869 Orientation 0 = Horizontal (default Vertical, a 241 x 90 slider), 0x2273 BeginValue,
# 0x2274 EndValue (20 / 100 against the defaults 0 / 50)
# Confirmed by Openness in the 164 working copy (Screen_R8, 2026-10-31): sliders 20/100 Horizontal and 7/33, and a
# symbolic I/O field with Padding 2/3/4/5.
# Also confirmed there: gauge/bar with logarithmic scale 1..1000 and with enabled thresholds (Gauge_L/Bar_L,
# Gauge_T/Bar_T): CurvedScale part 0x2337 (TypeId 93434), ScalingType 0x232d (1 = Logarithmic), thresholds list part
# 0x0915 (Name 0x0003, Color 0x0871 ARGB, ThresholdMode 0x386d, Enable _#ESEnable; Value is read-only in Openness).
ORIENTATIONS = {0: "Horizontal", 1: "Vertical"}
SCALING_TYPES = {0: "Linear", 1: "Logarithmic"}
THRESHOLD_MODES = {1: "Upper", 2: "Lower", 3: "Normal", 4: "Minimum", 5: "Maximum"}
_SCALE = {"0x2273": "BeginValue", "0x2274": "EndValue", "0x232d": "ScalingType"}
ITEM_PARTS = {
    "0x2338": ("StraightScale", dict(_SCALE, **{"0x3869": "Orientation"})),
    "0x2337": ("CurvedScale", dict(_SCALE)),
    "0x094d": ("Padding", {"0x0063 Left": "Left", "0x0064 Top": "Top", "0x094b": "Right", "0x094c": "Bottom"}),
    # Content part of buttons (Content.HorizontalTextAlignment Center = 1)
    "0x0002": ("Content", {"0x087a HorizontalTextAlignment": "HorizontalTextAlignment"}),
    # Corners of rectangles (all four 5,
    # TIA ES draws rounded corners); which member is which corner is not verified (all values equal so far)
    "0x0947": ("Corners", {"0x0518": "TopLeftRadius", "0x0519": "TopRightRadius", "0x051a": "BottomRightRadius",
                           "0x051b": "BottomLeftRadius"}),
}
THRESHOLDS_PART = "0x0915"
ENUM_MEMBERS = {"Orientation": ORIENTATIONS, "ScalingType": SCALING_TYPES}


def _item_parts(p, k, subtype):
    """Known map-valued parts of a screen item (only members that differ from the default are stored)."""
    from .faceplates import _parts  # late import: faceplates imports this module
    try:
        parts = _parts(p, k) or {}
    except Exception:  # noqa: BLE001  (a part that does not decode must not stop the screen)
        return {}
    out = {}
    for key, (name, members) in ITEM_PARTS.items():
        part = parts.get(key)
        if not isinstance(part, dict):
            continue
        vals = {m: ENUM_MEMBERS[m].get(part[raw], part[raw]) if m in ENUM_MEMBERS
                else stylecodec.enum_name(m, part[raw]) if m.endswith("TextAlignment") else part[raw]
                for raw, m in members.items() if raw in part}
        if vals:
            out[name] = vals
    # Font part 0x021c: member 0x054e names the font list, which the item links by ExpandoReferences; the font list
    # links its font item "<family>;<size>;...;<weight>" (as in the style). WebRH DOM: a text with font list item
    # "Siemens Sans;24;...;700" shows font-size 24px, font-weight 700.
    fpart = next((v for kk, v in parts.items() if kk.startswith("0x021c")), None)
    fl_name = fpart.get("0x054e FontList") if isinstance(fpart, dict) else None
    if fl_name:
        fl = next((t for t in p.targets(k, "ExpandoReferences") if p.name(t) == fl_name), None)
        item = next(iter(p.targets(fl, "ExpandoReferences")), None) if fl else None
        # font list in the extended record layout (typed slots, no relation ids): its linked HmiFontItemData
        if fl and item is None:
            item = next((t for t in p.targets(fl) if p.type_name(t).endswith("HmiFontItemData")), None)
        if item and p.name(item):
            font = _style_fonts({"Description": "f"}, {"f": p.name(item)})
            if "Name" in font:
                out["Font"] = font
    th = parts.get(THRESHOLDS_PART)
    if isinstance(th, list):
        out["Thresholds"] = [
            {"Name": e.get("0x0003"), "ThresholdMode": THRESHOLD_MODES.get(e.get("0x386d"), e.get("0x386d")),
             "Color": "0x%08X" % e["0x0871"] if isinstance(e.get("0x0871"), int) else None,
             "Enable": bool(e.get("_#ESEnable"))}
            for e in th if isinstance(e, dict)]
    return out


def _is_link_object(v):
    """True for a wrapped expando object whose only member is a 4-character text (the key of a dynamic link)."""
    try:
        o = stylecodec.decode_object(bytes(v), 0)
    except (ValueError, IndexError, struct.error):
        return False
    return len(o) == 1 and isinstance(next(iter(o.values())), str) and len(next(iter(o.values()))) == 4


def _style_fonts(v, refs):
    """Style font {Description: reference key} -> {Name, Size, Weight} from the font item name
    "<family>;<size>;...;<weight>" (family without blanks as Openness names it, e.g. SiemensSans)."""
    if isinstance(v, dict):
        if set(v) == {"Description"}:
            f = (refs.get(v["Description"]) or "").split(";")
            if len(f) >= 6 and f[1].isdigit() and f[5].isdigit():
                return {"Name": f[0].replace(" ", ""), "Size": int(f[1]), "Weight": _WEIGHTS.get(int(f[5]), f[5])}
            return {"FontItem": ";".join(f)}
        return {k: _style_fonts(x, refs) for k, x in v.items()}
    return v


class Hmi:
    def __init__(self, model):
        self.m = model
        self.p = model.p

    def _short(self, k):
        return (self.p.type_name(k) or "").rsplit(".", 1)[-1]

    def _of(self, app, short):
        return sorted(k for k in self.m.sources(app, "Target") if self._short(k) == short)

    def softwares(self, device):
        out = []
        for a in sorted(self.m.sources(device, "LogicalContainer")):
            if self._short(a) != "HmiApplicationData":
                continue
            di = self.p.attributes(a)[0].get("IDeviceItemData", {})
            # the languages of this HMI software resolve its texts (every faceplate type has its own settings object,
            # the first one found is not the device's)
            self._langs = self._app_languages(a)
            out.append({"name": self.p.name(a), "typeName": di.get("InvariantTypeName"),
                        "orderNumber": di.get("OrderNumber"), "firmware": di.get("FwVersion"),
                        "connections": self.connections(a), "tagTables": self.tag_tables(a),
                        "alarmClasses": self.alarm_classes(a), "discreteAlarms": self.alarms(a, "HmiDiscreteAlarmData"),
                        "analogAlarms": self.alarms(a, "HmiAnalogAlarmData"), "screens": self.screens(a),
                        "textLists": self.text_lists(a), "cycles": self.cycles(a),
                        "dataLogs": self.logs(a, "HmiDataLogData"), "alarmLogs": self.logs(a, "HmiAlarmLogData"),
                        "auditTrails": self.logs(a, "HmiAuditLogData"),
                        "runtimeSettings": self.runtime_settings(a), "style": self.style(a),
                        "runtimeLanguages": self.runtime_languages(a),
                        "scriptModules": self.script_modules(a), "graphicLists": self.graphic_lists(a),
                        "opcUaAlarmTypes": self.opc_ua_alarm_types(a),
                        "systemTags": sorted(self.p.name(k) or "" for k in self._of(a, "HmiSystemTagData")),
                        "counts": {"systemTags": len(self._of(a, "HmiSystemTagData")),
                                   "internalSystemTags": len(self._of(a, "HmiInternalSystemTagData")),
                                   "systemEvents": len(self._of(a, "HmiSystemEventData"))}, "key": a})
        return out

    def text_lists(self, app):
        """HmiTextListData 0x00e81005 (IAdditionalAttributes.IsSystemTextList), entries via
        HmiTextListEntryDataChildren -> HmiTextListEntryData 0x00e81007 (IHmiListEntryAttributes From/To/EntryType/
        DefaultEntry, text: ExpandoChildren -> HmiTextData)."""
        out = []
        for k in self._of(app, "HmiTextListData"):
            ad = self.p.attributes(k)[0].get("IAdditionalAttributes", {})
            entries = []
            # text list in the extended record layout (typed slots, no relation ids;
            # 47 entries): the entries are the links without relation that point to HmiTextListEntryData
            children = self.p.targets(k, "HmiTextListEntryDataChildren") or [
                l.key for l in self.p.links(k) if l.relation is None and l.key in self.p.objects
                and self._short(l.key) == "HmiTextListEntryData"]
            for e in children:
                la = self.p.attributes(e)[0].get("IHmiListEntryAttributes", {})
                entries.append({"from": la.get("From"), "to": la.get("To"), "type": la.get("EntryType"),
                                "default": la.get("DefaultEntry"), "text": self._text(e)})
            out.append({"name": self.p.name(k), "system": ad.get("IsSystemTextList"), "entries": entries, "key": k})
        out.sort(key=lambda x: (bool(x["system"]), x["name"] or ""))
        return out

    def opc_ua_alarm_types(self, app):
        """OPC UA alarm types (sample 163): ILOpcUaAlarmTypeData 0x00c11001 (IOpcUaAlarmTypeAttributeSet NotifyNodeId =
        Openness NodeId, ConditionTypeId, Area; links to the alarm class and the OPC UA connection). TIA adds one
        ILOpcUaAlarmData '<name>_MultiInstanceAlarm' per type (IAlarmAttributeSet.ID from 700000)."""
        out = []
        for k in self._of(app, "ILOpcUaAlarmTypeData"):
            a = self.p.attributes(k)[0].get("IOpcUaAlarmTypeAttributeSet", {})
            ac = self.m.rel1(k, "ILOpcUaAlarmTypeData_HmiAlarmClassData")
            cn = self.m.rel1(k, "ILOpcUaAlarmTypeData_HmiConnectionData")
            out.append({"name": self.p.name(k), "nodeId": a.get("NotifyNodeId"), "conditionTypeId": a.get("ConditionTypeId"),
                        "area": a.get("Area"), "alarmClass": self.p.name(ac) if ac else None,
                        "connection": self.p.name(cn) if cn else None, "key": k})
        out.sort(key=lambda x: x["name"] or "")
        return out

    def plant_views(self):
        """Plant view (samples 160, 161; one per project): CpmData -> CpmHierarchyRootChildren -> CpmHierarchyNodeData
        root (ICpmHierarchyDataAttributes IsRoot, AssignedHmiDeviceID), nodes via CpmHierarchyNodeChildren (Subtype
        HierarchyNode). Node paths are slash-separated below the plant view, as in Openness."""
        out = []

        def walk(k, path, nodes):
            for c in self.p.targets(k, "CpmHierarchyNodeChildren"):
                cp = (path + "/" if path else "") + (self.p.name(c) or "")
                nodes.append({"name": self.p.name(c), "path": cp,
                              "subtype": self.p.attr(c, "ICoreAttributes", "Subtype"), "key": c})
                walk(c, cp, nodes)
        for cpm in self.p.of_type("CpmData"):
            for r in self.p.targets(cpm, "CpmHierarchyRootChildren"):
                ha = self.p.attributes(r)[0].get("ICpmHierarchyDataAttributes", {})
                nodes = []
                walk(r, "", nodes)
                out.append({"name": self.p.name(r), "assignedHmiDevice": ha.get("AssignedHmiDeviceID") or "",
                            "nodes": nodes, "key": r})
        return out

    def graphic_lists(self, app):
        """HmiGraphicListData 0x00e81003 (sample 159, via HmiGraphicLists.Import): IHmiGraphicListAttributes.Mode
        ('Enhanced'); Openness exposes only the name. Entries need project graphics, which Openness cannot create;
        their storage is not covered by a sample yet."""
        out = []
        for k in self._of(app, "HmiGraphicListData"):
            ga = self.p.attributes(k)[0].get("IHmiGraphicListAttributes", {})
            out.append({"name": self.p.name(k), "mode": ga.get("Mode"), "key": k})
        out.sort(key=lambda x: x["name"] or "")
        return out

    def runtime_languages(self, app):
        """Runtime languages (samples 153-155), one entry per project language (Openness LanguageAndFonts):
        enabled languages and their order = IHmiLanguageAndFontSettingsAttributes.OnlineLanguageOrder ('en-US;de-DE')
        of HmiLanguageAndFontSettingsData; logging languages = IAdditionalAttributes.LoggingLanguages of
        HmiRuntimeSettingsData (u32 size, u32 count, count x u32 LCID). The fonts (DefaultFont, FixedFont1..4) are not
        stored per language: DefaultFontList has no culture data, Openness reports the default 'Siemens Sans'."""
        ls = next(iter(self._of(app, "HmiLanguageAndFontSettingsData")), None)
        rs = next(iter(self._of(app, "HmiRuntimeSettingsData")), None)
        order = (self.p.attributes(ls)[0].get("IHmiLanguageAndFontSettingsAttributes", {}).get("OnlineLanguageOrder")
                 if ls else None) or ""
        order = [c for c in order.split(";") if c]
        blob = self.p.attributes(rs)[0].get("IAdditionalAttributes", {}).get("LoggingLanguages") if rs else None
        logging = set()
        if isinstance(blob, (bytes, bytearray)) and len(blob) >= 8:
            cnt = int.from_bytes(blob[4:8], "little")
            logging = {int.from_bytes(blob[8 + 4 * i:12 + 4 * i], "little") for i in range(cnt) if 12 + 4 * i <= len(blob)}
        proj = next(iter(self.p.of_type("ProjectData")), None)
        out = []
        for c in (self.p.targets(proj, "UsedCultures") if proj else []):
            name = self.m.culture(c)
            lcid = (self.p.name(c) or "").rsplit("(", 1)[-1].rstrip(")")
            out.append({"language": name, "order": order.index(name) if name in order else None,
                        "enable": name in order, "enableForLogging": lcid.isdigit() and int(lcid) in logging})
        # no UsedCultures found (project with library ProjectData objects): the enabled languages at least
        if not out:
            out = [{"language": c, "order": i, "enable": True, "enableForLogging": None} for i, c in enumerate(order)]
        return out

    def style(self, app):
        """Selected style of the HMI software (docs/spec/hmi-styles.md): name, values per item type (enums as Openness
        names, fonts as Name/Size/Weight from the font item of the referenced font list) and the rendering templates
        (SVG text) by name. None without StyleSettingsData."""
        for k, dev, sel, _ in stylecodec.settings(self.p):
            if app not in self.p.targets(k, "Target") or sel is None:
                continue
            props = self.p.attributes(sel)[0].get("IPropertyExpandoAttributes", {})
            # reference key -> font item name "<family>;<size>;<?>;<?>;<?>;<weight>" of the linked font list
            refs = {}
            links = self.p.targets(sel, "ExpandoReferences")
            for key, v in self.p.attributes(sel)[0].get("IReferenceExpandoAttributesSimple", {}).items():
                i = int.from_bytes(v[:4], "big")
                fl = links[i] if i < len(links) else None
                item = next(iter(self.p.targets(fl, "ExpandoReferences")), None) if fl else None
                refs[key] = self.p.name(item) if item else (self.p.name(fl) if fl else None)
            # a style from a library (F40: SiemensStyleLibrary_1_0) also carries plain expandos IsSystemStyle (bool),
            # Version and RuntimeVersion (text); they describe the style itself and go to "info", not to "values"
            values, info = {}, {}
            for t, v in props.items():
                if t == "SystemStyleName":
                    continue
                raw = getattr(v, "raw", v)
                if not isinstance(raw, (bytes, bytearray, memoryview)):
                    info[t] = raw
                    continue
                values[t] = _style_fonts(stylecodec.enum_names(stylecodec.strip_meta(
                    stylecodec.decode_style_value(raw))), refs)
            tpl = {n: (svg or b"").decode("utf-8-sig", "replace") for n, svg in stylecodec.templates(self.p, sel).items()}
            out = {"name": self.p.name(sel), "systemStyle": props.get("SystemStyleName"), "values": values,
                   "templates": tpl}
            if info:
                out["info"] = info
            return out
        return None

    def runtime_settings(self, app):
        """Runtime settings (samples 145-152). GMPEnabled = IHmiApplicationAttributes.IsRegulatedApplication of the
        HMI software; all other settings are expandos in IHmiILSettingsExpandoAttributes of one HmiILSettingsData record
        per area (Target = the HMI software; ILScreenSettings = Openness root, MaxLogin, OpcUaServer,
        ProcessDiagnostics, Reporting, Telemetry, UnifiedTagSettings, ...). Only values set in the project are stored;
        an absent value is the default. DomainFunctionConfigurations (binary record of the runtime functions) is
        left out."""
        sections = {}
        for k in self._of(app, "HmiILSettingsData"):
            name = self.p.name(k)
            if name == "DomainFunctionConfigurations":
                continue
            ex = self.p.attributes(k)[0].get("IHmiILSettingsExpandoAttributes", {})
            vals = {n: v for n, v in sorted(ex.items()) if not n.startswith("#") and not isinstance(v, (bytes, bytearray))}
            if vals:
                sections[name] = vals
        gmp = self.p.attributes(app)[0].get("IHmiApplicationAttributes", {}).get("IsRegulatedApplication")
        return {"gmpEnabled": gmp, "sections": sections}

    @staticmethod
    def _duration(ticks):
        """Log/segment time period stored in 100 ns ticks -> {days, hours, minutes, seconds} (Openness LogDuration)."""
        if not isinstance(ticks, int):
            return None
        sec = ticks // 10_000_000
        return {"days": sec // 86400, "hours": sec % 86400 // 3600, "minutes": sec % 3600 // 60, "seconds": sec % 60}

    def logs(self, app, short):
        """Data logs, alarm logs, audit trails (samples 136-144): IHmiLogAttributes (LogMaxSize in MB, LogTimePeriod
        and SegmentTimePeriod in 100 ns ticks, SegmentMaxSize, SegmentStartTime, StoragePath, StorageDevice) and the
        backup strategy (link IHmiLogAttributes_BackupStrategy -> HmiBackupStrategyData: BackupMode 0 NoBackup,
        1 PrimaryPath; PrimaryPath, SecondaryPath). IHmiCentralLogAttributes (central logging) has no Openness
        property."""
        out = []
        for k in self._of(app, short):
            la = self.p.attributes(k)[0].get("IHmiLogAttributes", {})
            bs = self.m.rel1(k, "IHmiLogAttributes_BackupStrategy")
            ba = self.p.attributes(bs)[0].get("IHmiBackupStrategyAttributes", {}) if bs else {}
            st = la.get("SegmentStartTime")
            out.append({"name": self.p.name(k), "logMaxSize": la.get("LogMaxSize"),
                        "logTimePeriod": self._duration(la.get("LogTimePeriod")),
                        "storagePath": la.get("StoragePath") or None, "storageDevice": la.get("StorageDevice"),
                        "segmentMaxSize": la.get("SegmentMaxSize"),
                        "segmentTimePeriod": self._duration(la.get("SegmentTimePeriod")),
                        "segmentStartTime": st[:19] if isinstance(st, str) else st,
                        "backupMode": BACKUP_MODES.get(ba.get("BackupMode"), ba.get("BackupMode")),
                        "backupPrimaryPath": ba.get("PrimaryPath") or None, "backupSecondaryPath": ba.get("SecondaryPath") or None,
                        "key": k})
        out.sort(key=lambda x: x["name"] or "")
        return out

    def logging_tags(self, k):
        """Logging tags of an HMI tag (samples 138, 140, 143): one nested expando per logging tag in
        IHmiLoggingTagExpandoAttributes (LoggingTag_n: Name, LoggingMode 1 = Cyclic, absent = OnChange,
        LoggingCycleFactor, ...); the data log and the logging cycle are link attributes LoggingTag_n#LogConfiguration
        / #LoggingCycle in IHmiLoggingTagExpandoLinkAttributes whose first 4 bytes (big endian) index the tag's
        ExpandoReferences links."""
        a = self.p.attributes(k)[0]
        ex = a.get("IHmiLoggingTagExpandoAttributes", {})
        la = a.get("IHmiLoggingTagExpandoLinkAttributes", {})
        refs = [l.key for l in self.p.links(k) if self.p.mm.relations.get(l.relation) is not None
                and self.p.mm.relations[l.relation].name == "ExpandoReferences"]

        def ref(v):
            if isinstance(v, (bytes, bytearray)) and len(v) >= 4:
                i = int.from_bytes(v[0:4], "big")
                return self.p.name(refs[i]) if i < len(refs) else None
            return None
        out = []
        for slot, blob in sorted(ex.items()):
            if slot.startswith("#") or not isinstance(blob, (bytes, bytearray)):
                continue
            f = {nm.decode("utf-8", "replace"): (c, v) for nm, c, v in self.nested(blob)}
            def num(name):
                c, v = f.get(name, (None, None))
                return int.from_bytes(v, "little", signed=True) if isinstance(v, (bytes, bytearray)) and len(v) == 4 else None
            name = f.get("Name", (None, b""))[1]
            mode = num("LoggingMode")
            out.append({"name": name[1:].decode("utf-8", "replace") if name else None,
                        "loggingMode": LOGGING_MODES.get(mode or 0, mode), "cycleFactor": num("LoggingCycleFactor"),
                        "dataLog": ref(la.get(slot + "#LogConfiguration")), "cycle": ref(la.get(slot + "#LoggingCycle"))})
        return out

    def cycles(self, app):
        """HmiCycleData 0x00b41001: IHmiCycleAttributes CycleTime/CycleUnit/Number."""
        out = []
        for k in self._of(app, "HmiCycleData"):
            ca = self.p.attributes(k)[0].get("IHmiCycleAttributes", {})
            out.append({"name": self.p.name(k), "time": ca.get("CycleTime"), "unit": ca.get("CycleUnit"),
                        "number": ca.get("Number"), "key": k})
        out.sort(key=lambda x: (x["number"] if x["number"] is not None else 1 << 30))
        return out

    def connections(self, app):
        out = []
        for c in self._of(app, "HmiConnectionData"):
            ca = self.p.attributes(c)[0].get("IHmiConnectionAttributes", {})
            out.append({"name": self.p.name(c), "kind": self.p.attr(c, "ICoreAttributes", "Subtype"),
                        "plcId": ca.get("PlcId"), "key": c})
        return out

    def plc_tag(self, tag):
        """(PLC tag name, PLC name) linked through a ScopedLinkMaintainerData (Consumer -> HMI tag)."""
        for slm in self.m.sources(tag, "Consumer"):
            prov = self.m.rel1(slm, "Provider")
            if prov:
                plc = None
                table = self.m.rel1(prov, "TagTable")
                ctl = self.m.rel1(table, "Target") if table else None
                if ctl:
                    plc = self.p.name(ctl)
                return self.p.name(prov), plc
        return None, None

    def tag(self, k):
        p = self.p
        attrs = p.attributes(k)[0]
        conn = self.m.rel1(k, "IHmiTagAttributes_Connection")
        cyc = self.m.rel1(k, "IHmiTagAttributes_AcquisitionCycle")
        plc_tag, plc = self.plc_tag(k)
        return {"name": p.name(k), "dataType": attrs.get("IStructureItem", {}).get("DisplayTypeName"),
                "connection": p.name(conn) if conn else None, "plcName": plc, "plcTag": plc_tag,
                "address": attrs.get("ITagAddress", {}).get("LogicalAddress") or None,
                "acquisitionCycle": p.name(cyc) if cyc else None,
                "persistent": attrs.get("IHmiTagAttributes", {}).get("Persistency"),
                "loggingTags": self.logging_tags(k),
                "comment": _plain(self.m.comment(k)), "key": k}

    def tag_tables(self, app):
        out = []
        for t in self._of(app, "HmiTagTableData"):
            tags = [self.tag(k) for k in self.p.targets(t, "Content") if self._short(k) == "HmiTagData"]
            tags.sort(key=lambda x: x["name"] or "")
            out.append({"name": self.p.name(t), "group": self.m.folder_path(t), "tags": tags, "key": t})
        out.sort(key=lambda x: (x["group"], x["name"] or ""))
        return out

    def alarm_classes(self, app):
        out = []
        for c in self._of(app, "HmiAlarmClassData"):
            # Openness Id = IAlarmClassData.Id of the AlarmServices mirror (Id_ForInstanceFilter is 0 for the
            # predefined classes)
            pe = self.m.rel1(c, "HmiOnlyAlarmClassPeAlarmClassRelation")
            cid = self.p.attr(pe, "IAlarmClassData", "Id") if pe else None
            if cid is None:
                cid = self.p.attr(c, "IHmiAlarmClassAttributes", "Id_ForInstanceFilter")
            out.append({"name": self.p.name(c), "id": cid, "key": c})
        out.sort(key=lambda x: (x["id"] if x["id"] is not None else 1 << 30, x["name"] or ""))
        return out

    def _text(self, alarm):
        res = {}
        for t in self.p.targets(alarm, "ExpandoChildren"):
            if self._short(t) == "HmiTextData":
                res.update(_plain(self.m.core_text_of(self.p.attr(t, "ICoreTextRepository", "Text"))))
        return res

    def alarms(self, app, short):
        out = []
        for k in self._of(app, short):
            attrs = self.p.attributes(k)[0]
            cls = self.m.rel1(k, "HmiAlarmClassDataRelation")
            kind = "IHmiDiscreteAlarmAttributes" if short == "HmiDiscreteAlarmData" else "IHmiAnalogAlarmAttributes"
            trig = self.m.rel1(k, kind + "_TriggerTag")
            e = {"name": self.core(k).get("Name"), "id": attrs.get("IAlarmData", {}).get("AlarmId"),
                 "alarmClass": self.p.name(cls) if cls else None, "triggerTag": self.p.name(trig) if trig else None,
                 "text": self._text(k), "key": k}
            if short == "HmiDiscreteAlarmData":
                d = attrs.get("IHmiDiscreteAlarmAttributes", {})
                e.update({"triggerBit": d.get("TriggerBitNumber"), "triggerMode": d.get("TriggerMode")})
            else:
                lim = attrs.get("IHmiAnalogAlarmLimitAttributes", {})
                e.update({"condition": lim.get("LimitMode"), "limit": lim.get("LimitValueConstant")})
            out.append(e)
        out.sort(key=lambda x: (x["id"] if x["id"] is not None else 1 << 30, x["name"] or ""))
        return out

    def core(self, k):
        return self.p.attributes(k)[0].get("ICoreAttributes", {})

    def screen_properties(self, k):
        """Non-default properties of a screen from IPropertyExpandoAttributes: expando entries whose
        dictionary name is a property id (PROPERTY_NAMES); int values inline, colors as varint(n+1) +
        3 bytes 00 + u32 ARGB."""
        o = self.p.objects[k]
        i = next((j for j, s in enumerate(o.set_names) if s.endswith(".IPropertyExpandoAttributes")), None)
        if i is None or i not in o.blocks:
            return {}
        a, e = o.blocks[i]
        b = o.data[a:e]
        if len(b) < 18:
            return {}
        used, cnt, tid = struct.unpack_from("<I", b, 0)[0], struct.unpack_from("<I", b, 8)[0], struct.unpack_from("<I", b, 14)[0]
        if 18 + 8 * cnt > used:
            return {}
        ids = struct.unpack_from("<%dI" % cnt, b, 18)
        offs = struct.unpack_from("<%dI" % cnt, b, 18 + 4 * cnt)
        base = 18 + 8 * cnt
        names = self.p.expando_dicts.get(tid, {})
        out = {}
        for eid, off in zip(ids, offs):
            raw = names.get(eid, ("", None))[0].encode("utf-8", errors="replace")
            prop = PROPERTY_NAMES.get(raw)
            if not prop:
                continue
            p0 = base + off
            try:
                if prop[1] == "int":
                    out[prop[0]] = struct.unpack("<i", struct.pack("<I", off))[0]
                elif prop[1] == "bool":
                    out[prop[0]] = bool(off)
                elif prop[1] == "float":
                    out[prop[0]] = round(struct.unpack("<f", struct.pack("<I", off))[0], 6)
                elif prop[1] == "enum":
                    out[prop[0]] = stylecodec.enum_name(prop[0], off)
                elif prop[1] == "flags":
                    out[prop[0]] = stylecodec.enum_name(prop[0], off)
                elif prop[1] == "barmode":
                    out[prop[0]] = BAR_MODES.get(off, off)
                elif prop[1] == "color":
                    n, p = attrcodec.varint(b, p0)
                    if n == 8:
                        (argb,) = struct.unpack_from("<I", b, p + 3)
                        out[prop[0]] = "0x%08X" % argb
                elif prop[1] == "points":  # u32 total, u32 count, count x u32 offset; entries u32 12, i32 X, i32 Y
                    _, cnt = struct.unpack_from("<II", b, p0)
                    offs = struct.unpack_from("<%dI" % cnt, b, p0 + 8)
                    out[prop[0]] = [dict(zip(("X", "Y"), struct.unpack_from("<ii", b, p0 + o + 4))) for o in offs]
                elif prop[1] == "ref":  # varint 5 + 4 bytes = name of an entry in IReferenceExpandoAttributes
                    n, p = attrcodec.varint(b, p0)
                    out[prop[0]] = ("ref", b[p:p + n - 1])
                elif prop[1] == "selection":  # list of nested expandos (_TypeId, Text ref 4f02, _Guid)
                    _, cnt = struct.unpack_from("<II", b, p0)
                    offs = struct.unpack_from("<%dI" % cnt, b, p0 + 8)
                    refs = []
                    for j, o in enumerate(offs):
                        end = p0 + (offs[j + 1] if j + 1 < cnt else _)
                        seg = b[p0 + o:end]
                        g = seg.find(b"_Guid")
                        q = seg.find(b"\x18\x00\x00\x00", g) if g >= 0 else -1
                        if q >= 0 and seg[q + 20:q + 21] == b"\x05":
                            refs.append(("ref", seg[q + 21:q + 25]))
                    out[prop[0]] = refs
                else:
                    # varint = total size incl. itself (attrcodec.read_var; 2-byte prefix for >= 127 bytes)
                    v, _ = attrcodec.read_var(b, p0)
                    # a dynamized ProcessValue (F40) is stored as a wrapped object {4902: 4-character key of the
                    # dynamic link}, not as a value; Openness reports the property as unset -> left out
                    if v and v[:1] == b"\x00" and prop[0] == "ProcessValue" and _is_link_object(v):
                        continue
                    text = v.decode("utf-8", errors="replace") if v else ""
                    # bytes that contain a wrapped object (header 9 x 00, 0x10, u32 1) are no value: in F40 1040 the
                    # entry covers the Padding part (0x094d) of the symbolic I/O field
                    if prop[0] == "ProcessValue" and v and bytes(9) + b"\x10\x01\x00\x00\x00" in v:
                        continue
                    # other stored encodings that are not text are kept as hex
                    if prop[0] == "ProcessValue" and any(c < " " or c == "�" for c in text):
                        out["ProcessValueRaw"] = bytes(v).hex()
                        continue
                    out[prop[0]] = text
            except (struct.error, IndexError):
                pass
        return out

    @staticmethod
    def nested(blob):
        """Nested expando value (e.g. _#Dynamics, entries of IPropertyDynamicExpandoAttributes): optional varint
        total size, u8 0, u32 size, 9 bytes 0, 10 bytes type reference, u32 size, u32 count, u32 offset,
        count x u32 type code (| 0x80000000), count x varint(n+1) name, count x u32 value offset (relative to the
        start of that table), values. Returns [(name bytes, code, value bytes)]; strings (code 0x0b) as
        varint(n+1) + UTF-8, references as varint 5 + 4 bytes."""
        try:
            p = 0
            if blob[:1] != b"\x00":
                _, p = attrcodec.varint(blob, 0)
            p += 1 + 4 + 9 + 10 + 4
            cnt, _ = struct.unpack_from("<II", blob, p)
            p += 8
            codes = struct.unpack_from("<%dI" % cnt, blob, p)
            p += 4 * cnt
            names = []
            for _ in range(cnt):
                n, p = attrcodec.varint(blob, p)
                names.append(bytes(blob[p:p + n - 1]))
                p += n - 1
            # value table: scalars inline (bool 1 byte, int 4, guid 16), everything else a u32 offset counted from
            # the start of this table (U11, mapping table of sample 132)
            base, q, fields = p, p, []
            for code in codes:
                c = code & 0x7FFFFFFF
                size = NESTED_INLINE.get(c)
                if size:
                    fields.append((c, True, bytes(blob[q:q + size])))
                    q += size
                else:
                    fields.append((c, False, struct.unpack_from("<I", blob, q)[0]))
                    q += 4
            starts = sorted(v for _, inline, v in fields if not inline)
            out = []
            for nm, (c, inline, v) in zip(names, fields):
                if inline:
                    out.append((nm, c, v))
                else:
                    nxt = [s for s in starts if s > v]
                    out.append((nm, c, bytes(blob[base + v:base + nxt[0] if nxt else len(blob)])))
            return out
        except (struct.error, IndexError):
            return []

    @classmethod
    def mapping(cls, entry):
        """Mapping table of a tag dynamization: entry fields ValueConverter -> MappingTable -> Entries (array:
        u32 total, u32 count, count x u32 offset; elements nested with Value, From, To, RangeType) and
        ConditionType (1 = Range). Values in the encoding of the dynamized property (colors: varint 8 + 3 bytes +
        u32 ARGB)."""
        f = {n: v for n, _, v in cls.nested(entry)}
        vc = {n: v for n, _, v in cls.nested(f.get(b"ValueConverter", b""))} if f.get(b"ValueConverter") else {}
        mt = {n: v for n, _, v in cls.nested(vc.get(b"MappingTable", b""))} if vc.get(b"MappingTable") else {}
        if not mt:
            return None
        res = {"conditionType": CONDITION_TYPES.get(struct.unpack("<i", mt.get(b"ConditionType", b"\0\0\0\0"))[0]),
               "entries": []}
        arr = mt.get(b"Entries", b"")
        if len(arr) >= 8:
            _, cnt = struct.unpack_from("<II", arr, 0)
            offs = struct.unpack_from("<%dI" % cnt, arr, 8)
            for j, o in enumerate(offs):
                el = {n: v for n, _, v in cls.nested(arr[o:offs[j + 1] if j + 1 < cnt else len(arr)])}
                val = el.get(b"Value", b"")
                if len(val) == 8 and val[0] == 8:  # color
                    val = "0x%08X" % struct.unpack_from("<I", val, 4)[0]
                elif len(val) == 4:  # integer property (e.g. Width, code 0x13), sample 134
                    val = struct.unpack("<i", val)[0]
                elif len(val) == 1:  # 1-byte value (code 0x02: bool/enum, e.g. Visible), sample F40
                    val = val[0]
                else:
                    val = val.hex()
                cond = el.get(b"Condition", b"")
                cn, cp = attrcodec.varint(cond, 0) if cond else (0, 0)
                if cn and cn == len(cond):  # Expression: Condition is a string (varint = total size incl. itself)
                    # latin-1 keeps the 4-byte reference keys ($<key>$) byte-exact for the caller
                    res["entries"].append({"condition": cond[cp:].decode("latin-1"), "value": val})
                elif b"From" in el:  # Range: From/To/Value/RangeType (an empty bound = open range, reported as None)
                    bound = lambda v: struct.unpack("<i", v)[0] if len(v) == 4 else None  # noqa: E731
                    res["entries"].append({"from": bound(el[b"From"]), "to": bound(el.get(b"To", b"\0\0\0\0")), "value": val})
                else:  # Singlebit: BitmaskType, Value, Relevant/Condition bit masks (u64, code 0x14 inline)
                    mask = lambda v: struct.unpack("<Q", v)[0] if len(v) == 8 else None  # noqa: E731
                    res["entries"].append({"value": val, "relevant": mask(el.get(b"Relevant", b"")),
                                           "condition": mask(el.get(b"Condition", b""))})
        return res

    @staticmethod
    def _nested_str(v):
        s = attrcodec.read_var(v, 0)[0] if v else None  # varint = total size incl. itself
        return s.decode("utf-8", errors="replace") if s else ""

    def dynamizations(self, k, scripts):
        """Dynamized properties of an item: IPropertyExpandoAttributes._#Dynamics maps property ids (as in
        PROPERTY_NAMES; 07 = Visible) to keys of IPropertyDynamicExpandoAttributes entries with Type (Tag, Script),
        Tag and ValueConverter/MappingTable (ranges not decoded yet). Tag: the item links ExpandoReferences ->
        HmiTagData; Script: HmiScriptEmbeddedData '<Property>Trigger' (scope Screen.Trigger)."""
        attrs = self.p.attributes(k)[0]
        raw = attrs.get("IPropertyExpandoAttributes", {}).get("_#Dynamics")
        if not isinstance(raw, bytes):
            return []
        dyn = attrs.get("IPropertyDynamicExpandoAttributes", {})
        link = attrs.get("IPropertyDynamicLinkExpandoAttributes", {})
        # reference fields (Tag, ResourceList, Script, TagRefs[]) hold varint 5 + 4-byte key = name of an
        # IPropertyDynamicLinkExpandoAttributes entry; its value (u32 big endian + 1 byte) indexes the item's
        # ExpandoReferences links in stored order incl. empty entries, as for scripts (H-031; F40 Text_3:
        # ResourceList -> 2 -> HmiTextListData Units, Tag -> 3 -> HmiTagData UnitIndex)
        refs = [l.key for l in self.p.links(k, "ExpandoReferences")]

        def target(v):
            n, p = attrcodec.varint(v, 0) if v else (0, 0)
            lv = link.get(v[p:p + n - 1].decode("utf-8", errors="replace")) if n else None
            if isinstance(lv, bytes) and len(lv) >= 4:
                idx = int.from_bytes(lv[:4], "big")
                # flag 0x80 in the fifth byte: index into ExpandoChildren (as texts with fields, resolve_text); real
                # ResourceList -> TextListInstanceData
                if len(lv) >= 5 and lv[4] & 0x80:
                    children = [l.key for l in self.p.links(k, "ExpandoChildren")]
                    if idx < len(children) and self._short(children[idx]) == "TextListInstanceData":
                        return children[idx]
                if idx < len(refs) and refs[idx] in self.p.objects:
                    return refs[idx]
            return None

        def name(v):  # structure members with the path of their root tag (Tag.member), as Openness shows them (F40)
            t, parts = target(v), []
            if t and self._short(t) == "TextListInstanceData":
                # a library text list used in a faceplate type: the name of its library type
                ver = self.m.rel1(t, "IsInstanceOf")
                typ = self.m.rel1(ver, "VersionObjectToTypeObject") if ver else None
                return self.p.name(typ) if typ else None
            while t and self._short(t) == "HmiStructureMemberTagData":
                parts.append(self.p.name(t))
                t = self.m.rel1(t, "MembersInverse")
            if t:
                parts.append(self.p.name(t))
            return ".".join(reversed(parts)) or None

        out = []
        for nm, _, val in self.nested(raw):
            prop = PROPERTY_NAMES.get(nm, DYNAMIC_ONLY_NAMES.get(nm, ("#" + nm.hex(), None)))[0]
            n, p = attrcodec.varint(val, 0) if val else (0, 0)
            entry = dyn.get(val[p:p + n - 1].decode("utf-8", errors="replace")) if n else None
            fields = {f.decode("utf-8", errors="replace"): v for f, _, v in self.nested(entry)} if isinstance(entry, bytes) else {}
            kind = self._nested_str(fields.get("Type", b""))
            d = {"property": prop, "type": kind or None}
            if kind == "Tag":
                d["tag"] = name(fields.get("Tag", b""))
                d["mapping"] = self.mapping(entry) if isinstance(entry, bytes) else None
            elif kind == "ResourceList":  # text/graphic list selected by an index tag
                d["resourceList"] = name(fields.get("ResourceList", b""))
                d["tag"] = name(fields.get("Tag", b""))
            elif kind == "Expression":  # tags (TagRefs, array of references) + conditions in the mapping table
                arr = fields.get("TagRefs", b"")
                tags = []
                if len(arr) >= 8:
                    _, cnt = struct.unpack_from("<II", arr, 0)
                    for o in struct.unpack_from("<%dI" % cnt, arr, 8):
                        tags.append(name(arr[o:o + 5]))
                d["tags"] = tags
                d["mapping"] = self.mapping(entry) if isinstance(entry, bytes) else None
                # conditions reference tags as $<4-byte key>$ (key = IPropertyDynamicLinkExpandoAttributes entry);
                # shown as 'tag name' (operators are stored without blanks, e.g. $k1$AND$k2$)
                def tagref(m):
                    t = name(b"\x05" + m.group(1).encode("latin-1"))
                    return "'%s'" % t if t else m.group(0)
                for e in (d["mapping"] or {}).get("entries", []):
                    if isinstance(e.get("condition"), str):  # Expression (Singlebit: int bit mask)
                        e["condition"] = re.sub(r"\$(.{4})\$", tagref, e["condition"], flags=re.S)
            elif kind == "Script":
                t = target(fields.get("Script", b""))
                code = self.script(t).get("code") if t else None
                d["code"] = code if code is not None else next(
                    (s.get("code") for s in scripts if s.get("name") == prop + "Trigger"), None)
            elif kind == "Flashing":  # Value/AlternateValue u32 ARGB, FlashingRate, FlashingCondition (F40)
                argb = lambda v: "0x%08X" % struct.unpack("<I", v)[0] if len(v) == 4 else None  # noqa: E731
                d["color"] = argb(fields.get("Value", b""))
                d["alternateColor"] = argb(fields.get("AlternateValue", b""))
                d["flashingRate"] = self._nested_str(fields.get("FlashingRate", b"")) or None
                d["flashingCondition"] = self._nested_str(fields.get("FlashingCondition", b"")) or None
            elif kind == "FaceplateInterface":  # property of a faceplate content item fed by an interface property
                d["interfaceProperty"] = self._nested_str(fields.get("PropertyName", b"")) or None
            elif not kind:  # _#Dynamics names a missing entry: a property event (propertyEvents), no dynamization
                continue    # (F40 txtAlarm_MachineUnitAssyPartColumn Text)
            for e in (d.get("mapping") or {}).get("entries", []):
                # Range values of reference properties (Screen of a screen window) are varint 5 + 4-byte key ->
                # name of the referenced object; an empty value is not set (Openness leaves it out), F40
                v = e.get("value")
                if isinstance(v, str) and len(v) == 10 and v[:2] == "05":
                    e["value"] = name(bytes.fromhex(v))
                elif v == "":
                    del e["value"]
            out.append(d)
        return out

    def references(self, k):
        """IReferenceExpandoAttributes: {entry name: value}; property values of kind 'ref' are entry names (4 bytes)
        and resolve here, e.g. Text -> CoreText (LCID 127 = GUID of the text, 0xFFFF = default text)."""
        return self.p.attributes(k)[0].get("IReferenceExpandoAttributes", {})

    def resolve_link(self, k, refs, ref):
        """Reference property whose value (u32 big endian + 1 byte) indexes the item's links (Graphic): flag byte 0x00
        -> ExpandoReferences -> HmiImageData (project graphic, its name); flag 0x80 -> ExpandoChildren ->
        ImageInstanceData -> library image type (its name). None when unset."""
        v = refs.get(ref[1].decode("utf-8", errors="replace"))
        if not isinstance(v, (bytes, bytearray)) or len(v) < 4:
            return None
        i = int.from_bytes(v[:4], "big")
        if len(v) > 4 and v[4] & 0x80:
            links = [l.key for l in self.p.links(k, "ExpandoChildren")]
            return library_image_name(self.p, links[i]) if i < len(links) and links[i] in self.p.objects else None
        links = [l.key for l in self.p.links(k, "ExpandoReferences")]
        return self.p.name(links[i]) if i < len(links) and links[i] in self.p.objects else None

    def graphics(self):
        """Project graphics (Z1.5 R3): HmiImageData without ContentToVersion; with it the image is the content of an
        ImageLibraryVersionData (library graphic, library.py: version image)."""
        out = [image_info(self.p, k) for k in self.p.of_type("HmiImageData")
               if not self.p.targets(k, "ContentToVersion")]
        out.sort(key=lambda x: x["name"] or "")
        return out

    def _field_text_blob(self, refs, ref, k):
        """ICoreTextRepository.Text of the HmiTextData child behind a text with output fields (5-byte link, flag
        0x80: u32 big endian index into ExpandoChildren), else None."""
        v = refs.get(ref[1].decode("utf-8", errors="replace"))
        if not (isinstance(v, bytes) and len(v) == 5 and v[4] & 0x80 and k is not None):
            return None
        links = [l.key for l in self.p.links(k, "ExpandoChildren")]
        i = int.from_bytes(v[:4], "big")
        td = links[i] if i < len(links) else None
        blob = self.p.attr(td, "ICoreTextRepository", "Text") if td and self._short(td) == "HmiTextData" else None
        return bytes(blob) if isinstance(blob, (bytes, bytearray, memoryview)) else None

    def text_fields(self, refs, ref, k):
        """Output fields of a text (e.g. '<field 0>: <field 1>'):
        after the plain texts the blob holds one FormattedText XML per culture in the same order
        ('<p>fixed<field ref="0" /></p>' and per field <format displaytype length precision ...>). Returns
        {"cultures": {culture: [text | {"field": n}]}, "fields": [{displayType, length, precision}]} or None."""
        blob = self._field_text_blob(refs, ref, k)
        if blob is None:
            return None
        plain = core_text(blob, self.m.cultures)
        raw = blob.decode("utf-8", "replace").replace("\x00", "")
        xmls = re.findall(r"<ProjectText.*?</ProjectText>", raw, re.S)
        fields, cultures = [], {}
        for c, x in zip(plain, xmls):
            body = re.search(r"<p>(.*?)</p>", x, re.S)
            if not body:
                continue
            parts = []
            for t, f in re.findall(r"([^<]+)|<field ref=\"(\d+)\"\s*/>", body.group(1)):
                parts.append({"field": int(f)} if f else html.unescape(t))
            cultures["65535" if c == "" else c] = parts
            if not fields:
                for fi in re.finditer(r"<fieldinfo name=\"(\d+)\".*?<format ([^>]*)/>", x, re.S):
                    a = dict(re.findall(r"(\w+)=\"([^\"]*)\"", fi.group(2)))
                    fields.append({"field": int(fi.group(1)), "displayType": a.get("displaytype"),
                                   "length": int(a["length"]) if a.get("length", "").isdigit() else None,
                                   "precision": int(a["precision"]) if a.get("precision", "").isdigit() else None})
        cultures.pop("127", None)
        default = cultures.pop("65535", None)
        langs = sorted(set(self.languages()) | set(cultures)) or [""]
        out = {c: cultures.get(c, default) for c in langs if cultures.get(c, default) is not None}
        return {"cultures": out, "fields": fields} if out and fields else None

    # members of the Font part with dynamizations of their own (part expando _#Dynamics: member id -> dynamic entry)
    FONT_PART_MEMBERS = {" ": "Font.Size", "u": "Font.Weight"}

    def part_dynamizations(self, k):
        """Dynamizations of Font members (Font part 0x021c
        _#Dynamics 0x0220 -> FaceplateInterface FontSize, 0x0475 -> FontBold with a value converter false/true ->
        400/700). Only FaceplateInterface links are returned: {property: Font.Size, type, interfaceProperty, mapping}."""
        attrs = self.p.attributes(k)[0]
        dyn = attrs.get("IPropertyDynamicExpandoAttributes", {})
        font = attrs.get("IPropertyExpandoAttributes", {}).get("")
        if not isinstance(font, (bytes, bytearray)) or not dyn:
            return []
        from .faceplates import decode_map  # late import: faceplates imports this module
        part = decode_map(bytes(font))
        dyns = part.get("_#Dynamics") if isinstance(part, dict) else None
        out = []
        for member, key in (dyns or {}).items():
            prop = self.FONT_PART_MEMBERS.get(member)
            entry = dyn.get(key) if isinstance(key, str) else None
            if not prop or not isinstance(entry, bytes):
                continue
            fields = {f.decode("utf-8", errors="replace"): v for f, _, v in self.nested(entry)}
            if self._nested_str(fields.get("Type", b"")) != "FaceplateInterface":
                continue
            out.append({"property": prop, "type": "FaceplateInterface",
                        "interfaceProperty": self._nested_str(fields.get("PropertyName", b"")) or None,
                        "mapping": self.mapping(entry)})
        return out

    def resolve_text(self, refs, ref, k=None):
        v = refs.get(ref[1].decode("utf-8", errors="replace"))
        if isinstance(v, str):  # e.g. Layer: the layer GUID as text
            return v
        if not isinstance(v, bytes):
            return {}
        if len(v) == 5 and v[4] & 0x80 and k is not None:
            # a text with output fields ("<text><field ref=0/>" showing a tag) is a child object: u32 big
            # endian index into ExpandoChildren -> HmiTextData, its ICoreTextRepository.Text per culture; the field
            # values exist only in the runtime, the fixed part is what the file holds (fields: text_fields)
            blob = self._field_text_blob(refs, ref, k)
            t = core_text(bytes(blob), self.m.cultures) if isinstance(blob, (bytes, bytearray, memoryview)) else {}
            t = {("65535" if c == "" else c): x for c, x in t.items()}
        else:
            t = self.m.core_text_of(v)
        t.pop("127", None)
        default = t.pop("65535", None)
        # as Openness shows it: per HMI language the own text, else the default text (LCID 0xFFFF)
        langs = sorted(set(self.languages()) | set(t)) or [""]
        return {c: t.get(c, default) for c in langs if t.get(c, default) is not None}

    def _screen_items(self, screen):
        """ILScreenItems of a screen in their stored order. A screen record in the extended layout (u32 @20 = 2, one
        typed link slot per relation, record.py) has no relation ids: then the typed slot whose targets all point back
        with ILScreenItemParent (the runtime shows them in this order)."""
        items = self.p.targets(screen, "ILScreenItems")
        if items:
            return items
        by_slot = {}
        for l in self.p.links(screen):
            if l.relation is None and l.key in self.p.objects:
                by_slot.setdefault(l.slot, []).append(l.key)
        for keys in by_slot.values():
            if keys and all(screen in self.p.targets(t, "ILScreenItemParent") for t in keys):
                return keys
        return []

    def _app_languages(self, app):
        """OnlineLanguageOrder of the HmiLanguageAndFontSettingsData that targets this HMI software."""
        ls = next(iter(self._of(app, "HmiLanguageAndFontSettingsData")), None)
        order = (self.p.attr(ls, "IHmiLanguageAndFontSettingsAttributes", "OnlineLanguageOrder") if ls else None) or ""
        return [x.strip() for x in re.split(r"[,;]", order) if x.strip()]

    def languages(self):
        """HMI languages: HmiLanguageAndFontSettingsData.IHmiLanguageAndFontSettingsAttributes.OnlineLanguageOrder
        (e.g. 'en-US'); the CoreCulture objects list all cultures known to the project, not its languages."""
        if not hasattr(self, "_langs"):
            self._langs = []
            for k in self.p.objects:
                if self._short(k) == "HmiLanguageAndFontSettingsData":
                    order = self.p.attr(k, "IHmiLanguageAndFontSettingsAttributes", "OnlineLanguageOrder") or ""
                    self._langs = [x.strip() for x in re.split(r"[,;]", order) if x.strip()]
                    break
        return self._langs

    def screens(self, app):
        out = []
        for k in self._of(app, "ILScreenData"):
            props = self.screen_properties(k)
            layers = self.layers(k)
            out.append({"name": self.core(k).get("Name"), "group": self.m.folder_path(k),
                        "width": props.get("Width"), "height": props.get("Height"),
                        "backColor": props.get("BackColor"), "layers": layers, "items": self.items(k, layers), "key": k})
        out.sort(key=lambda x: (x["group"], x["name"] or ""))
        return out

    def layers(self, screen):
        """The 32 layers of a screen (Z1.5 R5): screen expando 'K\\x02' (property id 0x24b), u32 total, u32 count,
        count x u32 offset; per layer a nested expando with _Guid (code 12, .NET GUID bytes; screen items refer to it in
        their Layer property), name in field 3 (varint(n+1) text, Layer_0 ... Layer_31) and _#ESVisible when set.
        Index = drawing order (layer 0 at the bottom)."""
        blob = self.p.attributes(screen)[0].get("IPropertyExpandoAttributes", {}).get("K\x02")
        out = []
        if not isinstance(blob, (bytes, bytearray)) or len(blob) < 8:
            return out
        try:
            _, cnt = struct.unpack_from("<II", blob, 0)
            offs = list(struct.unpack_from("<%dI" % cnt, blob, 8)) + [len(blob)]
            for i in range(cnt):
                f = {n.decode("utf-8", "replace"): v for n, _c, v in self.nested(blob[offs[i]:offs[i + 1]])}
                g = f.get("_Guid")
                name = f.get("\x03")
                vis = f.get("_#ESVisible")
                out.append({"index": i, "name": name[1:].decode("utf-8", "replace") if isinstance(name, (bytes, bytearray)) and name else None,
                            "guid": str(uuid.UUID(bytes_le=bytes(g))) if isinstance(g, (bytes, bytearray)) and len(g) == 16 else None,
                            "esVisible": bool(vis[0]) if isinstance(vis, (bytes, bytearray)) and vis else None})
        except (struct.error, IndexError, ValueError):
            pass
        return out

    def items(self, screen, layers=None):
        """Screen items: ILScreenItemData 0x00cd1003 (ILScreenItems -> item, ILScreenItemParent -> screen), item type
        in ICoreAttributes.Subtype (HmiRectangle, HmiText, HmiIOField, HmiButton, ...). Tag dynamizations: the item
        links ExpandoReferences -> HmiTagData (expando _#Dynamics). Events: EmbeddedScriptChildren ->
        HmiScriptEmbeddedData 0x00e31002 (name On<Event>, script code in its IScriptAttributes)."""
        out = []
        layer_index = {l["guid"]: l["index"] for l in layers or [] if l.get("guid")}
        # ILScreenItems link order = Openness ScreenItems order = drawing order within a layer (checked F01)
        for z, k in enumerate(self._screen_items(screen)):
            c = self.core(k)
            props = self.screen_properties(k)
            refs = self.references(k)
            text = {}
            for pn, pv in list(props.items()):
                if isinstance(pv, tuple) and pv[0] == "ref" and pn in ("Graphic", "Screen", "ResourceList"):
                    props[pn] = self.resolve_link(k, refs, pv)
                elif isinstance(pv, tuple) and pv[0] == "ref":
                    props[pn] = self.resolve_text(refs, pv, k)
                    if pn == "Text":
                        text = props[pn]
                        fields = self.text_fields(refs, pv, k)
                        if fields:
                            props["TextFields"] = fields
                elif isinstance(pv, list) and pv and isinstance(pv[0], tuple):
                    props[pn] = [{"Text": self.resolve_text(refs, r, k)} for r in pv]
            tags = [self.p.name(t) for t in self.p.targets(k, "ExpandoReferences") if self._short(t) == "HmiTagData"]
            # custom widget container (F40): ExpandoReferences -> HmiDynamicScreenItemResourceData, the widget package
            # (name e.g. extended.NatureFlame, file NatureFlame.svghmi; its Content is a paged blob not decoded yet)
            widget = next((self.p.name(t) for t in self.p.targets(k, "ExpandoReferences")
                           if self._short(t) == "HmiDynamicScreenItemResourceData"), None)
            if widget:
                props["Widget"] = widget
            for pname, pval in _item_parts(self.p, k, c.get("Subtype")).items():
                props[pname] = pval
            scripts = [dict(self.script(e), name=self.core(e).get("Name"))
                       for e in self.p.targets(k, "EmbeddedScriptChildren")]
            dyn = self.dynamizations(k, scripts) + self.part_dynamizations(k)
            # a dynamized ProcessValue is reported as unset by Openness (F01 truth); what is stored there is a link key
            # (4 bytes, wrapped or not, F40) and no value
            if any(z.get("property") == "ProcessValue" for z in dyn):
                props.pop("ProcessValue", None)
                props.pop("ProcessValueRaw", None)
            # event handlers (scope Screen.Event, name On<Event>); script dynamizations (Screen.Trigger) go to dynamics
            events = [s for s in scripts if s.get("kind") != "Screen.Trigger"]
            out.append({"name": c.get("Name"), "type": c.get("Subtype"), "left": props.get("Left", 0),
                        "width": props.get("Width"), "height": props.get("Height"), "backColor": props.get("BackColor"),
                        "text": text, "properties": props, "tags": tags, "events": events,
                        "dynamizations": dyn, "zIndex": z,
                        # group member (Z1.5 R6): ContainerParent -> HmiGroup; Left/Top are relative to the group
                        "parent": self.p.name(self.m.rel1(k, "ContainerParent")) if self.m.rel1(k, "ContainerParent") else None,
                        "layerIndex": layer_index.get(props.get("Layer")), "key": k})
        out.sort(key=lambda x: x["name"] or "")
        return out

    def item_text(self, k):
        """Text of an item: CoreText value in IReferenceExpandoAttributes (LCID 127 = GUID of the text,
        0xFFFF = default text, other LCIDs = project languages). Returns {culture: text}, default under ''."""
        vals = self.p.attributes(k)[0].get("IReferenceExpandoAttributes", {})
        for v in vals.values():
            if isinstance(v, bytes) and len(v) > 16:
                t = self.m.core_text_of(v)
                t.pop("127", None)
                if t:
                    return {("" if c == "65535" else c): x for c, x in t.items()}
        return {}

    def _params(self, b):
        """IScriptSignatureAttributes.ParameterList: u32 total, u32 count, count x u32 offset; per parameter u32 size and
        three u32 field offsets relative to the parameter, the third field is varint(n+1) name."""
        out = []
        if not isinstance(b, (bytes, bytearray)) or len(b) < 8:
            return out
        try:
            _, cnt = struct.unpack_from("<II", b, 0)
            for off in struct.unpack_from("<%dI" % cnt, b, 8):
                f3 = struct.unpack_from("<I", b, off + 12)[0]
                t, _ = attrcodec.read_var(b, off + f3)
                out.append(t.decode("utf-8", errors="replace") if t else "")
        except (struct.error, IndexError):
            pass
        return out

    def script_modules(self, app):
        """Global script modules (samples 156, 157): HmiScriptGlobalModuleData (IScriptModuleAttributes Language,
        Scope Global), link GlobalSection -> HmiScriptGlobalSectionData (code of the global definition area incl. TIA's
        'End of Global definition area' comment), links SimpleScriptChildren -> one HmiScriptGlobalData per function
        (name = record name, code = function body; IScriptSignatureAttributes IsExported, IsAsync, HasReturnValue,
        ParameterList). `code` is the module as Openness exports it (<name>.hmi.js)."""
        out = []
        for k in self._of(app, "HmiScriptGlobalModuleData"):
            g = self.m.rel1(k, "GlobalSection")
            glob = (self.script(g)["code"] or "") if g else ""
            funcs = []
            for f in self.p.targets(k, "SimpleScriptChildren"):
                sig = self.p.attributes(f)[0].get("IScriptSignatureAttributes", {})
                funcs.append({"name": self.p.name(f), "parameters": self._params(sig.get("ParameterList")),
                              "exported": sig.get("IsExported"), "async": sig.get("IsAsync"),
                              "hasReturnValue": sig.get("HasReturnValue"), "body": self.script(f)["code"] or ""})
            code = glob + "\r\n" + "".join(
                "%s%sfunction %s(%s) {%s\r\n}\r\n" % ("export " if fn["exported"] else "", "async " if fn["async"] else "",
                                                     fn["name"], ", ".join(fn["parameters"]), fn["body"]) for fn in funcs)
            ma = self.p.attributes(k)[0].get("IScriptModuleAttributes", {})
            out.append({"name": self.p.name(k), "language": ma.get("Language"), "functions": funcs, "code": code})
        out.sort(key=lambda x: x["name"] or "")
        return out

    def script(self, k):
        """HmiScriptEmbeddedData 0x00e31002: IScriptAttributes Language (JavaScript), Scope (Screen.Event = event
        handler On<Event>, Screen.Trigger = script dynamization <Property>Trigger) and Code, a token table (u32 total,
        u32 count, count x u32 offset relative to the table; token: u32 size, u32 kind 9, u8, varint(n+1) text).
        Whitespace, line breaks and comments are tokens of their own (sample 130), so the code is the exact
        concatenation of the token texts."""
        sa = self.p.attributes(k)[0].get("IScriptAttributes", {})
        b = sa.get("Code")
        res = {"language": sa.get("Language"), "kind": sa.get("Scope"), "code": None, "references": {}}
        # Tags("1285367001") etc.: the number is the name of an IScriptLinkExpandoAttributes entry whose value
        # (u32 big endian + 1 byte) indexes the script's ExpandoReferences links (H-031, F01 ForeColorTrigger:
        # 1285367001 -> index 1 -> HmiTag_Ext1; index 0 = the screen item itself)
        # stored order incl. empty entries (0000:0000:0), which count for the index (F05: [I_Text1, empty, HmiTag_Ext1])
        refs = [l.key for l in self.p.links(k, "ExpandoReferences")]
        for name, v in self.p.attributes(k)[0].get("IScriptLinkExpandoAttributes", {}).items():
            if isinstance(v, bytes) and len(v) >= 4:
                idx = int.from_bytes(v[:4], "big")
                if idx < len(refs) and refs[idx] in self.p.objects:
                    res["references"][name] = self.p.name(refs[idx])
        if not isinstance(b, bytes):
            return res
        try:
            _, cnt = struct.unpack_from("<II", b, 0)
            offs = struct.unpack_from("<%dI" % cnt, b, 8)
            toks = []
            for off in offs:
                # varint = total size incl. itself (attrcodec.read_var): a 2-byte prefix is not n+1 (F40 1013
                # Rectangle_1: a6 01 = 166 = 164 bytes + 2; read as n+1 the text took the next token's first byte)
                t, _ = attrcodec.read_var(b, off + 9)
                toks.append(t.decode("utf-8", errors="replace") if t else "")
            # the stored token is the reference id; Openness shows the referenced name (Tags("HmiTag_Ext1"))
            res["code"] = "".join(res["references"].get(t, t) for t in toks)
        except (struct.error, IndexError):
            pass
        return res


def text_lines(sw, indent):
    pad = " " * indent
    out = ["%sHMI %s  %s %s %s" % (pad, sw["name"], sw["typeName"], sw["orderNumber"], sw["firmware"])]
    for c in sw["connections"]:
        out.append("%s  Connection %s (%s, %s)" % (pad, c["name"], c["kind"], c["plcId"]))
    for t in sw["tagTables"]:
        out.append("%s  HMI tag table %s%s" % (pad, (t["group"] + "/") if t["group"] else "", t["name"]))
        for g in t["tags"]:
            src = "%s %s" % (g["connection"], g["plcTag"] or "") if g["connection"] else "<internal>"
            out.append("%s    %-24s %-8s %-30s %-8s %s" % (pad, g["name"], g["dataType"], src.strip(),
                                                         g["acquisitionCycle"] or "",
                                                         "; ".join("%s: %s" % kv for kv in g["comment"].items())))
    c = sw["counts"]
    out.append("%s  System tags %d, internal system tags %d, system events %d" % (
        pad, c["systemTags"], c["internalSystemTags"], c["systemEvents"]))
    if sw["cycles"]:
        out.append("%s  Cycles: %s" % (pad, ", ".join("%s(%s %s)" % (x["name"], x["time"], x["unit"]) for x in sw["cycles"])))
    user = [t for t in sw["textLists"] if not t["system"]]
    out.append("%s  Text lists: %d user, %d system" % (pad, len(user), len(sw["textLists"]) - len(user)))
    for t in user:
        out.append("%s    %s" % (pad, t["name"]))
        for e in t["entries"]:
            out.append("%s      %s..%s %s%s  %s" % (pad, e["from"], e["to"], e["type"], " default" if e["default"] else "",
                                                  "; ".join("%s: %s" % kv for kv in e["text"].items())))
    if sw["alarmClasses"]:
        out.append("%s  Alarm classes: %s" % (pad, ", ".join("%s(%s)" % (c["name"], c["id"]) for c in sw["alarmClasses"])))
    for a in sw["discreteAlarms"]:
        out.append("%s  Discrete alarm %s id=%s class=%s trigger=%s.%s %s  %s" % (
            pad, a["name"], a["id"], a["alarmClass"], a["triggerTag"], a["triggerBit"], a["triggerMode"],
            "; ".join("%s: %s" % kv for kv in a["text"].items())))
    for a in sw["analogAlarms"]:
        out.append("%s  Analog alarm %s id=%s class=%s trigger=%s %s %s  %s" % (
            pad, a["name"], a["id"], a["alarmClass"], a["triggerTag"], a["condition"], a["limit"],
            "; ".join("%s: %s" % kv for kv in a["text"].items())))
    for s in sw["screens"]:
        out.append("%s  Screen %s%s  %sx%s%s" % (pad, (s["group"] + "/") if s["group"] else "", s["name"], s["width"],
                                                s["height"], "  BackColor " + s["backColor"] if s["backColor"] else ""))
        for it in s["items"]:
            out.append("%s    %-16s %-14s %s%s%s" % (
                pad, it["name"], it["type"], " ".join("%s=%s" % kv for kv in sorted(it["properties"].items())),
                "  tags " + ",".join(it["tags"]) if it["tags"] else "",
                "  text " + "; ".join("%s: %s" % kv for kv in it["text"].items()) if it["text"] else ""))
            for ev in it["events"]:
                out.append("%s      %s [%s]: %s" % (pad, ev["name"], ev.get("language"), ev.get("code")))
            for d in it.get("dynamizations", []):
                m = d.get("mapping")
                mt = ""
                if m and m.get("conditionType") != "None":
                    mt = " %s: %s" % (m.get("conditionType"), ", ".join(
                        "%s..%s->%s" % (e["from"], e["to"], e.get("value", "")) if "from" in e else
                        "[%s]->%s" % (e["condition"], e["value"]) if "condition" in e else str(e["value"])
                        for e in m.get("entries", [])))
                if d["type"] == "Tag":
                    what = str(d.get("tag") or "") + mt
                elif d["type"] == "ResourceList":
                    what = "%s index %s" % (d.get("resourceList"), d.get("tag"))
                elif d["type"] == "Expression":
                    what = "tags %s%s" % (",".join(str(t) for t in d.get("tags", [])), mt)
                elif d["type"] == "Flashing":
                    what = "%s/%s %s %s" % (d.get("color"), d.get("alternateColor"), d.get("flashingRate"),
                                            d.get("flashingCondition"))
                else:
                    what = d.get("code")
                out.append("%s      dyn %s <- %s %s" % (pad, d["property"], d["type"], what))
    return out
