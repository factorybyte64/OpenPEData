"""PLC alarms (GOALS.md Z1.2, docs/spec/plc-alarms.md; samples 041-044).

Navigation (relation names from the metamodel):
  SimpleControllerAlarmData   one Program_Alarm: Subtype TypeAlarm (in the FB) or InstanceAlarm (in the instance DB);
                              PhysicalParent -> block, Target -> CPU target, AlarmClass -> AlarmClassData (instance
                              alarms only when the class is set there, ISimpleControllerAlarmData.AlarmClassIsSet;
                              otherwise the class is the one of the type alarm), TypeAlarm -> type alarm (instance
                              alarms), Texts -> AlarmText (11 records)
  AlarmText                   ICoreTextRepository.Text = multilingual text; the n-th Texts link has the text index
                              IAlarmData.TextIndexes[n] (u32 size, u32 count, count x u16)
  AlarmClassData              alarm class (Acknowledgement, No Acknowledgement, user classes); Alarms -> alarms
Text index -> meaning (sample 043, against the Openness alarm text export): 0 info text, 1 alarm text, 2..10
additional texts 1..9. An empty instance text means "inherited from type".
"""
import struct

TEXT_NAMES = ["infoText", "alarmText"] + ["additionalText%d" % i for i in range(1, 10)]


def text_indexes(b):
    if not isinstance(b, (bytes, bytearray)) or len(b) < 8:
        return []
    n = struct.unpack_from("<I", b, 4)[0]
    return list(struct.unpack_from("<%dH" % n, b, 8)) if len(b) >= 8 + 2 * n else []


def extract_plc_alarms(model):
    p = model.p
    classes = []
    for k in p.of_type("AlarmClassData"):
        a = p.attributes(k)[0].get("IAlarmClassData", {})
        classes.append({"name": p.name(k), "acknowledgement": a.get("Acknowledgement"), "readOnly": a.get("IsReadOnly"),
                        "priority": a.get("Priority"), "globalId": a.get("GlobalId"), "key": k})
    alarms = []
    for k in p.of_type("SimpleControllerAlarmData"):
        at = p.attributes(k)[0]
        ad, sc = at.get("IAlarmData", {}), at.get("ISimpleControllerAlarmData", {})
        blk, tgt = model.rel1(k, "PhysicalParent"), model.rel1(k, "Target")
        cls = model.rel1(k, "AlarmClass") or next((c for c in model.sources(k, "Alarms") if p.is_a(c, "AlarmClassData")), None)
        typ = model.rel1(k, "TypeAlarm")  # instance alarm -> type alarm in the FB
        if cls is None and typ:  # class inherited from the type alarm (samples 042, 043)
            cls = model.rel1(typ, "AlarmClass")
        idx = text_indexes(ad.get("TextIndexes"))
        texts = {}
        for n, t in enumerate(p.targets(k, "Texts")):
            i = idx[n] if n < len(idx) else n
            v = {c: s for c, s in (model.core_text_of(p.attr(t, "ICoreTextRepository", "Text")) or {}).items() if s and c != "65535"}
            if v:
                texts[TEXT_NAMES[i] if i < len(TEXT_NAMES) else "text%d" % i] = v
        alarms.append({"name": p.name(k), "kind": at.get("ICoreAttributes", {}).get("Subtype"),
                       "alarmType": at.get("IAlarmDataExt", {}).get("AlarmType"),
                       "block": p.name(blk) if blk else None, "blockType": p.type_name(blk) if blk else None,
                       "plc": p.name(tgt) if tgt else None, "alarmClass": p.name(cls) if cls else None,
                       "alarmClassSetHere": bool(sc.get("AlarmClassIsSet")), "priority": ad.get("Priority"),
                       "typeAlarm": ("%s/%s" % (p.name(model.rel1(typ, "PhysicalParent")), p.name(typ))) if typ else None,
                       "texts": texts, "key": k})
    alarms.sort(key=lambda x: (x["plc"] or "", x["block"] or "", x["name"] or ""))
    classes.sort(key=lambda x: x["name"] or "")
    return {"alarms": alarms, "alarmClasses": classes}
