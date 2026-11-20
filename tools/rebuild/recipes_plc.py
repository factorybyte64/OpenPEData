"""Recipes 030-082 (PLC samples: step 5g, Z1.2, STL/GRAPH, DB symbols; samples/MANIFEST.md)."""
import json
import os

from rhelp import CPU1513, OLD, chg, hold, pause, compile_plc, doc_write, doc_write_indented, plc, src, sys_blocks, transfer, \
    xlsx_import
from truth import _raw, g_copy_old

FB_ALARM_SCL = ("FUNCTION_BLOCK \"FB_Alarm\"\n{ S7_Optimized_Access := 'TRUE' }\nVERSION : 0.1\n   VAR_INPUT\n      Sig : Bool;\n"
                "   END_VAR\n\n   VAR\n      Alarm_1 {InstructionName := 'Program_Alarm'; LibVersion := '1.0'} : Program_Alarm;\n"
                "   END_VAR\n\n\nBEGIN\n\t#Alarm_1(SIG := #Sig);\nEND_FUNCTION_BLOCK\n")


# --- truth generators: sd\<block>.s7dcl/.s7res (SD export) and simaticml\<block>.xml -----------------------------------
def g_sd_dir(ctx, out, rel):
    d, fn = os.path.split(rel)
    name = fn.rsplit(".", 1)[0]
    if os.path.exists(os.path.join(out, d, name + ".s7dcl")):
        return
    _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="documentExport", objectKind="block", name=name,
         targetPath=os.path.join(out, d))


def g_simaticml_dir(ctx, out, rel):
    _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="export", objectKind="block", name=os.path.basename(rel)[:-4],
         targetPath=os.path.join(out, rel))


def g_dev_obj(ctx, out, rel):
    """<device>/[blocks/]<name>.<ext>: xml = SimaticML (block, else technology object, else PLC data type), awl/db/scl =
    sourceExport, members.json = memberGet (maxDepth 30), s7dcl/s7res = SD export."""
    parts = rel.split("/")
    dn, fn = parts[0], parts[-1]
    target = os.path.join(out, rel)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    if fn.endswith(".members.json"):
        txt = _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="memberGet", objectKind="block", device=dn,
                   name=fn[:-len(".members.json")], maxDepth="30")
        with open(target, "w", encoding="utf-8-sig") as f:
            f.write(txt)
        return
    name, ext = fn.rsplit(".", 1)
    if ext == "xml":
        for kind in ("block", "technologyObject", "type"):
            txt = _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="export", objectKind=kind, device=dn, name=name,
                       targetPath=target)
            if os.path.exists(target):
                return
        ctx.log.append({"truth_export_failed": rel, "last": txt[:500]})
    elif ext in ("awl", "db", "scl"):
        _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="sourceExport", objectKind="block", device=dn,
             names=name, targetPath=os.path.dirname(target))
        if not os.path.exists(target):  # sourceExport may name the file differently
            ctx.log.append({"truth_source_missing": rel})
    elif ext in ("s7dcl", "s7res"):
        if not os.path.exists(os.path.join(os.path.dirname(target), name + ".s7dcl")):
            _raw(ctx, "TiaPlcSoftware", ok_errors=True, operation="documentExport", objectKind="block", device=dn,
                 name=name, targetPath=os.path.dirname(target))


def g_force_table(ctx, out, rel):
    d = json.loads(_raw(ctx, "TiaPlcSoftware", operation="get", objectKind="watchTable", name="Force table"))
    _dumpc(os.path.join(out, rel), {"source": "TiaPlcSoftware(get, watchTable Force table)", "table": "Force table",
                                    "entries": d.get("entries")})


def g_hw_connection(ctx, out, rel):
    hc = json.loads(_raw(ctx, "TiaPlcSoftware", operation="hardwareConnectionGet", objectKind="technologyObject",
                         name="TO_Speed_5_0"))
    to = json.loads(_raw(ctx, "TiaPlcSoftware", operation="get", objectKind="technologyObject", name="TO_Speed_5_0",
                         parameterFilter="Actor.Type"))
    conns = [{"interface": i.get("interface"), "isConnected": i.get("isConnected"), "outputTag": i.get("outputTag"),
              "outputAddress": i.get("outputAddress")} for i in hc.get("interfaces", []) if i.get("isConnected")]
    params = {p["name"]: p.get("value") for p in to.get("parameters", []) if p["name"] == "Actor.Type"}
    _dumpc(os.path.join(out, rel), {"source": "TiaPlcSoftware(hardwareConnectionGet/get, technologyObject TO_Speed_5_0)",
                                    "to": "TO_Speed_5_0", "connections": conns, "parameters": params})


def _dumpc(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(obj, ensure_ascii=False, separators=(",", ":")) + "\n")


def g_opcua_export(ctx, out, rel):
    """<interface>.xml: opcUaInterfaceExport (server or reference interface)."""
    name = rel[:-4]
    lst = json.loads(_raw(ctx, "TiaPlcSoftware", operation="opcUaInterfaceList", objectKind="block"))
    kind = next((i["interfaceKind"] for i in lst.get("items", []) if i["name"] == name), "server")
    _raw(ctx, "TiaPlcSoftware", operation="opcUaInterfaceExport", objectKind="block", interfaceKind=kind, name=name,
         targetPath=os.path.join(out, rel))


def g_top_block_xml(ctx, out, rel):
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="block", name=os.path.basename(rel)[:-4],
         targetPath=os.path.join(out, rel))


DEV_OBJ = [(r"[^/]+/(blocks/)?[^/]+\.(xml|awl|db|scl|members\.json|s7dcl|s7res)", g_dev_obj)]

SD_ML = [(r"sd/[^/]+\.(s7dcl|s7res)", g_sd_dir), (r"simaticml/[^/]+\.xml", g_simaticml_dir)]


# 057: row of the supervision import (values as in the export of the original sample)
SUP057 = {"B2": "ProDiag_FB", "C2": "1", "D2": '"Tag_Bool_1"', "E2": "Sup_1", "G2": "False", "H2": "O", "I2": "1",
          "L2": "T#0ms", "N2": "False", "P2": "False", "R2": "False", "S2": "Testueberwachung"}

def tr(name, pred, **kw):
    """Transfer recipe: content of the old sample (rhelp.transfer), then compile."""
    return {"from": pred, "steps": [transfer(name, **kw), compile_plc], "generators": SD_ML + DEV_OBJ}


# 036: technology objects (not exportable in the old sample: never compiled), in the order of their DB numbers
# TO_Speed_5_0 is created as version 5.0; TIA upgrades it to 6.0 when SpeedAx_6_0 (6.0) is added (its parameter set
# stays that of 5.0, as in the original)
TOS_036 = [("PID_Comp_2_3", "PID_Compact", "2.3"), ("TO_Speed_5_0", "TO_SpeedAxis", "5.0"),
           ("High_Spe_3_0", "High_Speed_Counter", "3.0"), ("TO_Posit_6_0", "TO_PositioningAxis", "6.0"),
           ("PID_3Ste_2_3", "PID_3Step", "2.3"), ("PID_Temp_1_1", "PID_Temp", "1.1"),
           ("TO_Exter_6_0", "TO_ExternalEncoder", "6.0"), ("SpeedAx_6_0", "TO_SpeedAxis", "6.0")]


def compile_blocks(*names):
    def step(ctx):
        for n in names:
            ctx.call("TiaPlcSoftware", operation="compile", objectKind="block", name=n)
    return step


# System blocks that the original samples kept without a call (the generator runs inserted instructions in blocks that
# were deleted or regenerated later). Carriers that call them: generator inputs in tools\sources, the old 039 export
# (GET/PUT) and tools\sources\syscarrier. See rhelp.sys_blocks.
def _lad(*n):
    return [src("xml_lad_lad", "FX_Lad_%s.xml" % x) for x in n]


def _sd(*n):
    return [src("lad", "FB_Lad_%s.s7dcl" % x) for x in n]


# 031/032: the first generator run (tools\gen_lad.py, SIMATIC SD) tried every catalog instruction in turn; a block that
# did not compile was deleted again, its instruction (library type, system block) stayed. Legacy (157-161: DCAT, MCAT,
# IMC, SMC, LEAD_LAG; 163 BCDCPL), 093/094 Deserialize/Serialize, 107-109 WriteToArrayDB, ReadFromArrayDBL,
# WriteToArrayDBL (SD: newest instruction version, as in the original; the SimaticML inputs FX_Lad_108/109 carry
# version 1.0 and would add a second library type), 102 GATHER, 148 ENCO. Not writable as SD any more with the
# last candidate left in tools\sources\lad: SCATTER_BLK (101), GATHER_BLK (103), DECO (147), FILL (119) and
# AssignmentAttempt (the 039 block FB_Lad_104 as carrier): SimaticML
CAR_032 = _sd("157", "158", "159", "160", "161", "163", "093", "094", "107", "108", "109", "102", "148") + _lad(
    "101", "103", "147")
CAR_031 = CAR_032 + _lad("119") + [src("syscarrier", "FB_CarrierAssign.xml")]
# 037: legacy in the order of the original numbers (DCAT, MCAT, IMC, SMC, LEAD_LAG, DRUM; DRUM is not writable as SD
# with the candidate left in tools\sources\lad). TONR (coil form, FX_Lad_027), GATHER, T_COMP, T_COMBINE, WR_LOC_T,
# S_COMP, Strg_TO_Chars, Chars_TO_Strg: instructions of the SD runs whose blocks did not stay. Get_Alarm from SD
# (newest version only; the SimaticML input FX_Lad_ex122 would add version 1.0 as a second library type)
CAR_037 = _sd("157", "158", "159", "160", "161") + _lad("156", "094", "093", "027") + _sd("102") + [
    src("extended_lad", "FB_Lad_ex%s.s7dcl" % x) for x in ("001", "006", "010", "016")] + [
    src("typed_extended_lad", "FB_Lad_ex%s.s7dcl" % x) for x in ("020", "021", "122")] + [
    src("xml_extended_lad", "FX_Lad_ex%s.xml" % x) for x in ("086", "159", "090", "092", "093")] + [
    os.path.join(OLD, "039_fw41", "export", "PLC_1", "block", n + ".xml") for n in ("FB_Lad_Get", "FB_Lad_Put")] + [
    src("syscarrier", "FC_CarrierDtl.scl"), src("syscarrier", "FC_CarrierTconIp.scl")]
# 039: TCON, TSEND, TRCV and TMAIL_C are named <name>_1 in the original
HOLD_039 = ("TCON", "TSEND", "TRCV", "TMAIL_C")
# 039: blocks written from the hand-written rungs of tools\gen_lad_manual.py (tools\sources\manual_lad; the same file
# names exist in tools\sources\lad, so transfer does not find them itself). The SD export of the old truth has the
# statements in another order (other part uids), and FB_Lad_104 (AssignmentAttempt) comes back as "?="
P2P_039 = ("FX_Lad_pt001", "FX_Lad_pt002", "FX_Lad_pt003", "FX_Lad_pt004", "FX_Lad_pt005")
EARLY_039 = ("FX_Fbd_112", "FX_Fbd_113", "FX_Lad_108", "FX_Lad_109")
MANUAL_039 = {"FB_Lad_%s" % x: src("manual_lad", "FB_Lad_%s.s7dcl" % x)
              for x in ("030", "031", "032", "033", "034", "043", "044", "045", "104")}


# DB_Std and DB_Arr: written from their input sources with documentWrite in the original (the old truth has no SD
# export of them). A SimaticML import of the old export compiles to other blocks (DB_Std 1111 instead of 1132 bytes,
# DB_Arr 1348 instead of 1362 bytes load memory). Written from the source, both vary from compile to compile like
# every DB (same source written repeatedly: DB_Arr 1362-1366, DB_Std 1127-1129 bytes, 2026-11-10)
SD_DB = {"DB_Std": src("scl", "DB_Std.s7dcl"), "DB_Arr": src("scl", "DB_Arr.s7dcl")}

RECIPES = {
    # --- 5g SCL/LAD/FBD coverage (generator runs; content taken over from the old sample) ---
    "030_scl_all": tr("030_scl_all", "029_fb_idb_value", sd_fallback=dict(SD_DB, FC_SclArr=src("scl", "FC_SclArr.s7dcl"))),
    # 031: the carriers first (FB1-FB4 exist): their system FBs get the library numbers FB80-FB84, which the later
    # FB_Lad_* skip as in the original (FB_Lad_123 FB79, FB_Lad_124 FB85). DB_Arr before the FBs: FB_Lad_117/118
    # reference it, and the reference order of a block (refId) depends on whether the DB exists when the block is written
    "031_lad_all": dict(tr("031_lad_all", "030_scl_all"), steps=[
        sys_blocks("031_lad_all", CAR_031, whole=False, single=True, check=False),
        transfer("031_lad_all", only={"DB_Arr"}, sd_fallback=SD_DB, kinds=("block",)),
        transfer("031_lad_all", skip={"DB_Arr"}), sys_blocks("031_lad_all", final=True)]),
    # 032: when the original run reached the legacy instructions, FB80-FB84 were taken by FB_Fbd_* and FB104 was the
    # only free number below FB105: DCAT FB0, MCAT FB105, IMC FB106, SMC FB107, LEAD_LAG FB108
    "032_fbd_all": dict(tr("032_fbd_all", "030_scl_all"), steps=[
        transfer("032_fbd_all", only={"DB_Arr"}, sd_fallback=SD_DB, kinds=("block",)),
        transfer("032_fbd_all", skip={"DB_Arr"}),
        sys_blocks("032_fbd_all", CAR_032, whole=False, single=True, fill=(101, 102, 103), check=False),
        sys_blocks("032_fbd_all", final=True)]),
    "034_flg_misc": tr("034_flg_misc", "030_scl_all", sd_fallback=SD_DB),
    "035_scl_rest": tr("035_scl_rest", "030_scl_all"),
    "036_tech": {"from": "030_scl_all", "steps": [
        plc("create", "technologyObject", name=n, toType=t, toVersion=v) for n, t, v in TOS_036] + [
        transfer("036_tech", kinds=("type", "tagTable", "block"), skip={"MC_Servo", "MC_Interpolator"}),
        sys_blocks("036_tech", [src("syscarrier", "FB_CarrierMcPower.scl")], whole=False),
        compile_blocks("FB_LadTech", "FB_FbdTech", "FB_SclTech")],
        "generators": SD_ML},
    # 037: DB_Arr first (operand reference order of the blocks that use it). FX_* blocks from their generator inputs
    # where these have the content of the sample (input_xml). The carriers one by one while FB112 (FX_T) is free:
    # in the original every SimaticML import of that phase replaced the block FX_T (FB112), so the legacy system
    # FBs got DCAT FB0 (FB81 taken) and FB113-FB117. FX_T last: its final content is the input of FX_Lad_057
    "037_lad_xml": dict(tr("037_lad_xml", "030_scl_all"), steps=[
        transfer("037_lad_xml", only={"DB_Arr"}, sd_fallback=SD_DB, kinds=("block",)),
        transfer("037_lad_xml", skip={"DB_Arr", "FX_T"}, input_xml=True),
        sys_blocks("037_lad_xml", CAR_037, whole=False, single=True, check=False),
        transfer("037_lad_xml", only={"FX_T"}, kinds=("block",),
                 xml_as={"FX_T": src("xml_lad_lad", "FX_Lad_057.xml")}),
        sys_blocks("037_lad_xml", final=True)]),
    # 038-040: own projects with another CPU / a communication module (instructions that need this hardware)
    "038_comm_hw": dict(tr("038_comm_hw", None, skip={"Main"}), **{"steps": [
        ("TiaDevice", dict(operation="create", name="PLC_1", deviceType=CPU1513)),
        ("TiaDeviceItem", dict(operation="plugNew", device="PLC_1", typeIdentifier="OrderNumber:6ES7 541-1AB00-0AB0/V1.0",
                               name="CM_PtP_1", positionNumber="2", parentDeviceItemPath="Rail_0")),
        transfer("038_comm_hw", pred="-", skip={"Main"}),
        # MB_CLIENT / MB_SERVER (with TCON, TSEND, TRCV ... and the TCP_MB_* types): left over in the original from the
        # Modbus TCP blocks of the generator run (tools\sources	yped_modbustcp_lad), which were deleted again
        # the original was never compiled as a whole (Main has no compile result): only the blocks one by one
        sys_blocks("038_comm_hw", [src("typed_modbustcp_lad", "FB_Lad_mt001.s7dcl"),
                                   src("typed_modbustcp_lad", "FB_Lad_mt006.s7dcl"),
                                   src("syscarrier", "FB_CarrierTrig.scl")], whole=False),
        compile_blocks("FB_Lad_pt006", "FB_Lad_pt007", "FB_Lad_pt008", "FB_Lad_us004", "FB_Lad_mb001", "FB_Lad_us003",
                       "FB_Lad_mb002", "FB_Lad_mb008", "FB_Lad_us005", "FB_Lad_us006")]}),
    # FB_Lad_104 (AssignmentAttempt): its SD export would come back as "?=", so SimaticML
    # Main: the OB that TIA creates with the device, as in the original (its old export is the same; a SimaticML import
    # of it compiles to some 370 bytes more load memory)
    "039_fw41": dict(tr("039_fw41", None), **{"steps": [
        ("TiaDevice", dict(operation="create", name="PLC_1", deviceType="OrderNumber:6ES7 516-3AP03-0AB0/V4.1")),
        # Names of system blocks that exist in two instruction versions for a moment. A SimaticML input without
        # version brings the oldest instruction version: its system block takes the plain name if that is free, else
        # <name>_1. tools\gen_xml.py compiled the whole program after every import, and that compile keeps the block
        # of the imported (old) version under its name, upgrades it and drops the other one (measured 2026-11-11 on
        # scratch projects and in the stage builds; a compile of the single block does the opposite). So the order
        # of the original decides the names:
        # - WRREC_SFB, ReadFromArrayDBL, WriteToArrayDBL (plain): FX_Lad_pt001-pt005 and FX_Fbd_112/113, FX_Lad_108/109
        #   came before the SD blocks that use these instructions (FB_Fbd_ex046, FB_Scl_sl053/sl054)
        # - TMAIL_C_1, TCON_1, TSEND_1, TRCV_1: SD blocks with these instructions existed when FX_Lad_te001 came
        #   (here FB_Fbd_ou010 ... and FB_Lad_ou005, written before it)
        transfer("039_fw41", pred="-", kinds=("type", "tagTable")),
        transfer("039_fw41", pred="-", only={"DB_Arr"}, kinds=("block",), sd_fallback=SD_DB),
        transfer("039_fw41", pred="-", only=set(P2P_039), kinds=("block",), input_xml=True), compile_plc,
        transfer("039_fw41", pred="-", only=set(EARLY_039), kinds=("block",), input_xml=True), compile_plc,
        transfer("039_fw41", pred="-", only={"FB_Lad_ou005"}, kinds=("block",)),
        transfer("039_fw41", pred="-", skip={"Main", "DB_Arr", "FB_Lad_ou005"} | set(P2P_039) | set(EARLY_039),
                 sd_fallback=MANUAL_039, input_xml=True, kinds=("block", "technologyObject")),
        compile_plc,
        # Get_Alarm 1.0 (second library type): SimaticML input of the extended run, after the compiled SD blocks
        sys_blocks("039_fw41", [src("syscarrier", "FC_CarrierDtl.scl"), src("xml_extended_lad", "FX_Lad_ex122.xml")],
                   whole=False, single=True, check=False),
        sys_blocks("039_fw41", check=False)]}),
    "040_s71200": dict(tr("040_s71200", None), **{"steps": [
        ("TiaDevice", dict(operation="create", name="PLC_1", deviceType="OrderNumber:6ES7 214-1AG40-0XB0/V4.7")),
        transfer("040_s71200", pred="-", skip={"Main"}), compile_plc]}),
    # --- DB symbols and attributes ---
    # 059/060 as in the manifest: DB_AllTypes from its SD source (keeps the MLC ids of the comments), memberSet
    "059_db_symbols": dict(tr("059_db_symbols", "029_fb_idb_value"), steps=[
        transfer("059_db_symbols", sd_fallback={"DB_AllTypes": src("dbsym", "DB_AllTypes.s7dcl")}),
        plc("memberSet", "block", name="DB_AllTypes", members=json.dumps({
            "vInt": {"ExternalAccessible": False}, "vDInt": {"ExternalWritable": False},
            "vReal": {"ExternalVisible": False}, "vWord": {"Retain": True}})),
        compile_plc]),
    "060_db_attrs": dict(tr("060_db_attrs", "059_db_symbols"), steps=[
        plc("memberSet", "block", name="DB_AllTypes", members=json.dumps({
            "vUSInt": {"ExternalWritable": False}, "vUInt": {"Setpoint": True}, "vUDInt": {"ExternalAccessible": False},
            "vLInt": {"ExternalVisible": False, "ExternalWritable": False},
            "vULInt": {"ExternalWritable": False, "Setpoint": True}, "vSetpoint": {"Setpoint": True},
            "deep.l1.l2.v": {"Setpoint": True}, "aStruct": {"ExternalVisible": False}, "aUdt": {"Retain": True}})),
        plc("memberSet", "block", name="DB_NonOpt", members=json.dumps({"i": {"Retain": True},
                                                                        "st.y": {"ExternalWritable": False}})),
        doc_write("type", src("dbattr", "UDT_Inner.s7dcl")),
        # inline source: keeps the indentation of the second code line as in the original
        doc_write_indented("block", src("dbattr", "FB_Multi.s7dcl")),
        compile_plc]),
    # --- STL (generated sources) and stress ---
    # FC_StlNoPar: imported alone as standard FC first, ParameterPassing set via SimaticML at the end (MANIFEST)
    "064_stl_all": dict(tr("064_stl_all", "062_fb_stl"), steps=[
        plc("sourceImport", "block", sourcePath=src("stl_all", "FC_StlNoPar.awl")),
        transfer("064_stl_all", skip={"FC_StlNoPar"}),
        transfer("064_stl_all", only={"FC_StlNoPar"}, xml_only={"FC_StlNoPar"}, kinds=("block",)),
        # FB_StlCall again, now calling FC_StlNoPar with UC (source export of the original sample)
        compile_blocks("FC_StlNoPar"), plc("sourceImport", "block", sourcePath=src("stl_fix", "FB_StlCall.uc.awl")), compile_plc]),
    # FB_BigScl: the SD export of the original (tools\gen_stress.py regenerates FB_BigScl.scl differently)
    "073_code_stress": tr("073_code_stress", "064_stl_all", xml_only={"FB_BigScl"}),
    "075_stl_calls": tr("075_stl_calls", "064_stl_all"),
    # --- system FB instances ---
    # the 10 IEC DBs (DB_IEC_*, type IEC_TIMER ...) are not listed by Openness: written from their SD sources after
    # the transfer (as in the original, after the other DBs)
    # DB_TSEND/DB_TRCV (single DBs of TSEND/TRCV) exist until the compile drops them: they hold DB12/DB13 while the IEC
    # DBs get their numbers, as in the original (067 creates them again with these numbers)
    "066_sys_fb_instances": dict(tr("066_sys_fb_instances", "029_fb_idb_value"), steps=[transfer("066_sys_fb_instances"),
        plc("create", "block", name="DB_TSEND", blockType="InstanceDB", instanceOf="TSEND"),
        plc("create", "block", name="DB_TRCV", blockType="InstanceDB", instanceOf="TRCV")] + [
        doc_write("block", src("sysfb", "DB_IEC_%s.s7dcl" % t), compileAfter="false")
        # in name order: TIA numbers them in the order of creation; the original has DB36 DB_IEC_COUNTER ... DB41
        # DB_IEC_TIMER, DB42 DB_IEC_UCOUNTER ... DB45 DB_IEC_USCOUNTER
        for t in ("COUNTER", "DCOUNTER", "LCOUNTER", "LTIMER", "SCOUNTER", "TIMER", "UCOUNTER", "UDCOUNTER", "ULCOUNTER",
                  "USCOUNTER")] + [compile_plc]),
    "067_tsend_trcv_single": dict(tr("067_tsend_trcv_single", "066_sys_fb_instances"),
                                  generators=DEV_OBJ + [(r"[^/]+\.xml", g_top_block_xml)]),
    # --- Z1.2 P1 PLC alarms ---
    "041_plc_alarm_fb": {"from": "002_cpu", "steps": [
        chg("TiaPlcSoftware", operation="sourceImport", objectKind="block", sourceText=FB_ALARM_SCL, fileName="FB_Alarm.scl")],
        "generators": SD_ML},
    "042_plc_alarm_idb": {"from": "041_plc_alarm_fb", "steps": [
        chg("TiaPlcSoftware", operation="create", objectKind="block", name="FB_Alarm_DB", blockType="InstanceDB", instanceOf="FB_Alarm")]},
    "043_plc_alarm_text": {"from": "042_plc_alarm_idb", "steps": [
        xlsx_import("alarmTextExport", "alarmTextImport", {"D2": "Testmeldung Pumpe", "F2": "Infotext Pumpe"}, "alarm043.xlsx")]},
    "044_plc_alarm_class": {"from": "043_plc_alarm_text", "steps": [
        xlsx_import("alarmTextExport", "alarmTextImport",
                    {"C2": "Acknowledgement", "D2": "Testmeldung Pumpe", "F2": "Infotext Pumpe"}, "alarm044.xlsx")]},
    # --- Z1.2 P2 text lists (045 by hand, see MANUAL) ---
    "045_plc_textlist": {"from": "044_plc_alarm_class", "steps": [
        pause("PLC_1 > PLC alarm text lists: user text list TL_Test, selection Decimal, 0 = Aus, 1 = Ein")]},
    "046_plc_textlist_entry": {"from": "045_plc_textlist", "steps": [
        xlsx_import("alarmTextListExport", "alarmTextListImport", {"D3": "An"}, "tl046.xlsx", sheet=2)]},
    "058_plc_textlist_range": {"from": "046_plc_textlist_entry", "steps": [
        xlsx_import("alarmTextListExport", "alarmTextListImport",
                    {"A4": "TL_Test", "B4": "2", "C4": "5", "D4": "Stoerung"}, "tl058.xlsx", sheet=2)]},
    # --- Z1.2 P3/P4 watch tables and software units ---
    "047_plc_watch_table": {"from": "003_tagtable", "steps": [
        chg("TiaPlcSoftware", operation="create", objectKind="watchTable", name="WT_Test")]},
    "048_plc_watch_entry": {"from": "047_plc_watch_table", "steps": [
        chg("TiaPlcSoftware", operation="entryAdd", objectKind="watchTable", name="WT_Test",
            entry=json.dumps({"Name": '"Tag_Bool_1"', "DisplayFormat": "Bool", "ModifyValue": "TRUE"}, separators=(",", ":")))]},
    "049_plc_unit": {"from": "048_plc_watch_entry", "steps": [
        chg("TiaPlcSoftware", operation="unitCreate", objectKind="block", name="Unit_A")]},
    "050_plc_unit_b": {"from": "049_plc_unit", "steps": [
        chg("TiaPlcSoftware", operation="unitCreate", objectKind="block", name="Unit_B")]},
    "051_plc_unit_relation": {"from": "050_plc_unit_b", "steps": [
        chg("TiaPlcSoftware", operation="unitRelationCreate", objectKind="block", name="Unit_A", relatedObject="Unit_B")]},
    # --- Z1.2 P5 technology objects ---
    "052_plc_to_param": {"from": "036_tech", "steps": [
        chg("TiaPlcSoftware", operation="parameterSet", objectKind="technologyObject", name="TO_Speed_5_0",
            parameters='{"DynamicDefaults.Velocity":750}')]},
    "053_plc_to_inverse": {"from": "052_plc_to_param", "steps": [
        chg("TiaPlcSoftware", operation="parameterSet", objectKind="technologyObject", name="TO_Speed_5_0",
            parameters='{"Actor.InverseDirection":true}')]},
    # --- Z1.2 P6/P7 OPC UA and ProDiag ---
    "054_plc_opcua_interface": {"from": "003_tagtable", "steps": [
        chg("TiaPlcSoftware", operation="opcUaInterfaceCreate", objectKind="block", interfaceKind="server", name="SI_Test")]},
    "055_plc_opcua_disabled": {"from": "054_plc_opcua_interface", "steps": [
        chg("TiaPlcSoftware", operation="opcUaInterfaceSet", objectKind="block", interfaceKind="server", name="SI_Test",
            propertiesJson='{"Enabled":false}')]},
    "056_plc_prodiag_fb": {"from": "003_tagtable", "steps": [
        chg("TiaPlcSoftware", operation="create", objectKind="block", name="ProDiag_FB", blockType="FB", programmingLanguage="ProDiag")]},
    "057_plc_prodiag_supervision": {"from": "056_plc_prodiag_fb", "steps": [
        xlsx_import("supervisionExport", "supervisionImport", SUP057, "sup057.xlsx")]},
    "078_plc_force_entry": {"from": "048_plc_watch_entry", "steps": [
        chg("TiaPlcSoftware", operation="entryAdd", objectKind="watchTable", name="Force table",
            entry=json.dumps({"Name": "%Q0.0", "DisplayFormat": "Bool", "ForceValue": "TRUE"}, separators=(",", ":")))],
        "generators": [(r"forceTable\.json", g_force_table)]},
    "079_plc_to_hw_connect": {"from": "053_plc_to_inverse", "steps": [
        chg("TiaPlcSoftware", operation="parameterSet", objectKind="technologyObject", name="TO_Speed_5_0",
            parameters='{"Actor.Type":0}'),
        plc("tagCreate", "tagTable", name="Default tag table", tagName="AxisOut", dataType="Int", logicalAddress="%QW100"),
        chg("TiaPlcSoftware", operation="hardwareConnect", objectKind="technologyObject", name="TO_Speed_5_0",
            interface="actor", plcTag="AxisOut")],
        "generators": [(r"hardwareConnection\.json", g_hw_connection)]},
    "081_plc_opcua_nodes": {"from": "054_plc_opcua_interface", "steps": [
        plc("opcUaInterfaceImport", "block", interfaceKind="server", name="SI_Test", sourcePath=src("opcua", "si_nodes_import.xml")),
        transfer("081_plc_opcua_nodes", pred="054_plc_opcua_interface", only={"DB_Ua"}, kinds=("block",)),
        pause("PLC_1 > OPC UA communication > Server interfaces > SI_Test: in the interface editor 'Generate local data' "
              "(creates DB SI_Test_Data)"),
        compile_plc],
        "generators": [(r"si_nodes_import\.xml", g_copy_old), (r"SI_Test\.xml", g_opcua_export)]},
    "082_plc_opcua_refns": {"from": "081_plc_opcua_nodes", "steps": [
        plc("opcUaInterfaceCreate", "block", interfaceKind="reference", name="RN_Types", sourcePath=src("opcua", "ref_types_import.xml"))],
        "generators": [(r"ref_types_import\.xml", g_copy_old), (r"RN_Types\.xml", g_opcua_export)]},
    # --- C1 STL ---
    "061_fc_stl": {"from": "029_fb_idb_value", "steps": [
        plc("sourceImport", "block", sourcePath=src("stl", "FC_Stl.awl")), compile_plc], "generators": DEV_OBJ},
    "062_fb_stl": {"from": "061_fc_stl", "steps": [
        plc("sourceImport", "block", sourcePath=src("stl", "FB_Stl.awl")), compile_plc], "generators": DEV_OBJ},
    # --- GRAPH ---
    "074_fb_graph": {"from": "064_stl_all", "steps": [
        plc("import", "block", sourcePath=src("graph", "FB_Graph.xml")), compile_plc], "generators": DEV_OBJ},
    "077_graph_branches": {"from": "076_graph_actions", "steps": [
        plc("import", "block", sourcePath=src("graph", "FB_GraphBranch.xml")),
        plc("import", "block", sourcePath=src("graph", "FB_GraphSim.xml")), compile_plc], "generators": DEV_OBJ},
    "080_graph_events": {"from": "077_graph_branches", "steps": [
        plc("import", "block", sourcePath=src("graph", "FB_GraphEvt.xml")), compile_plc], "generators": DEV_OBJ},
}

def create_test_plc(name, device_type):
    """TiaDevice(create) and, for test projects, the operations of the option "testOnly" of the returned
    securityPrerequisites (master secret off, full access / anonymous user), as for the original 063."""
    def step(ctx):
        r = json.loads(ctx.call("TiaDevice", operation="create", name=name, deviceType=device_type))
        for opt in (r.get("securityPrerequisites") or {}).get("options") or []:
            if opt.get("label") == "testOnly":
                for op in opt.get("operations", []):
                    ctx.call(op["tool"], operation=op["operation"], **{k: (v if isinstance(v, str) else json.dumps(v))
                                                                     for k, v in (op.get("args") or {}).items()})
    return step


def node(device, item, attrs):
    return ("TiaNetworkNode", dict(operation="setAttributes", device=device, deviceItemPath=item,
                                   attributesJson=json.dumps(attrs)))


def connect(device, item, subnet):
    return ("TiaNetworkSubnet", dict(operation="nodeConnect", device=device, deviceItemPath=item, subnet=subnet))


def g_network(ctx, out, rel):
    """network.json as tools\\Truth-MultiPlc.ps1: node attributes of every network interface of every station."""
    net = []
    for d in json.loads(_raw(ctx, "TiaDevice", operation="list"))["items"]:
        ifs = json.loads(_raw(ctx, "TiaNetwork", operation="interfacesDiscover", device=d["name"])).get("items", [])
        for i in ifs:
            if not i.get("hasNetworkInterface"):
                continue
            a = json.loads(_raw(ctx, "TiaNetworkNode", operation="getAttributes", device=d["name"], deviceItemPath=i["deviceItemPath"]))
            net.append({"device": d["name"], "deviceItemPath": i["deviceItemPath"], "attributes": a.get("attributes"),
                        "connectedSubnetName": a.get("connectedSubnetName")})
    _dumpc(os.path.join(out, rel), net)


MULTI = "multiplc"
RECIPES["063_multi_plc"] = {"from": None, "steps": [
    create_test_plc("PLC_A", "OrderNumber:6ES7 516-3AN02-0AB0/V2.9"),
    create_test_plc("PLC_B", "OrderNumber:6ES7 214-1AG40-0XB0/V4.6"),
    create_test_plc("PLC_C", "OrderNumber:6ES7 513-1AL02-0AB0/V2.9"),
    create_test_plc("PLC_D", "OrderNumber:6ES7 512-1DM03-0AB0/V4.1"),
    # CP_1 after PLC_D: the object ids of the original show this creation order
    ("TiaDeviceItem", dict(operation="plugNew", device="PLC_C", typeIdentifier="OrderNumber:6GK7 543-1AX00-0XE0/V2.2",
                           name="CP_1", positionNumber="2", parentDeviceItemPath="Rail_0")),
    ("TiaNetworkSubnet", dict(operation="create", subnetType="Ethernet", subnetName="PN/IE_1")),
    ("TiaNetworkSubnet", dict(operation="create", subnetType="Ethernet", subnetName="PN/IE_Werk")),
    ("TiaNetworkSubnet", dict(operation="create", subnetType="Ethernet", subnetName="IE_Leitebene")),
    ("TiaNetworkSubnet", dict(operation="create", subnetType="Profibus", subnetName="PROFIBUS_1")),
    connect("PLC_A", "PLC_A/PROFINET interface_1", "PN/IE_1"),
    node("PLC_A", "PLC_A/PROFINET interface_1", {"Address": "192.168.10.1", "SubnetMask": "255.255.255.0", "UseRouter": True,
                                                 "RouterAddress": "192.168.10.254"}),
    node("PLC_A", "PLC_A/PROFINET interface_2", {"Address": "10.0.0.1", "SubnetMask": "255.0.0.0"}),
    connect("PLC_A", "PLC_A/DP interface_1", "PROFIBUS_1"),
    node("PLC_A", "PLC_A/DP interface_1", {"Address": "2"}),
    connect("PLC_B", "PLC_B/PROFINET interface_1", "PN/IE_1"),
    node("PLC_B", "PLC_B/PROFINET interface_1", {"Address": "192.168.10.2", "SubnetMask": "255.255.255.0",
                                                 "PnDeviceNameAutoGeneration": False, "PnDeviceName": "plc-b-manuell"}),
    connect("PLC_C", "PLC_C/PROFINET interface_1", "PN/IE_Werk"),
    node("PLC_C", "PLC_C/PROFINET interface_1", {"Address": "172.16.0.5", "SubnetMask": "255.255.0.0"}),
    connect("PLC_C", "CP_1/Ethernet interface_1", "IE_Leitebene"),
    node("PLC_C", "CP_1/Ethernet interface_1", {"Address": "10.10.10.10", "SubnetMask": "255.255.255.240", "UseRouter": True,
                                                "RouterAddress": "10.10.10.1"}),
    node("PLC_D", "PLC_D/PROFINET interface_1", {"IpProtocolSelection": "OtherPath"}),
    # blocks as in the original: documentWrite (compiles each block) in the order of the original object ids, then
    # compile of the whole project. The hardware compile adds 'Server module_1' to PLC_D, writes the compile results
    # (NetworkFaultsAsMaintenance, GeoAddressSupportedSubmdodul, *ESIdent ...) and replaces the communication
    # certificates (subject 'CN=<PLC>/Communication-1' only)
    doc_write_indented("block", src(MULTI, "PLC_A", "DB_Common.s7dcl"), device="PLC_A"),
    doc_write_indented("block", src(MULTI, "PLC_A", "DB_A_Std.s7dcl"), device="PLC_A"),
    doc_write_indented("block", src(MULTI, "PLC_B", "DB_Common.s7dcl"), device="PLC_B"),
    doc_write_indented("block", src(MULTI, "PLC_D", "DB_D.s7dcl"), device="PLC_D"),
    doc_write_indented("block", src(MULTI, "PLC_C", "FB_Motor.s7dcl"), device="PLC_C"),
    doc_write_indented("block", src(MULTI, "PLC_C", "Motor1_DB.s7dcl"), device="PLC_C"),
    doc_write_indented("block", src(MULTI, "PLC_C", "DB_Common.s7dcl"), device="PLC_C"),
    ("TiaProject", dict(operation="compile"))],
    "generators": DEV_OBJ + [(r"network\.json", g_network)]}

# --- samples with steps in the TIA user interface (rhelp.pause) ---------------------------------------------------------
def pre_truth(files):
    """Step: truth files taken in the middle of the recipe (e.g. before know-how protection); kept in <out>\\<sample>_pre
    and copied over the regenerated truth after the archive (after_archive)."""
    def step(ctx):
        pre = os.path.join(ctx.out, ctx.name + "_pre")
        for rel in files:
            g_dev_obj(ctx, pre, rel)
    return step


def pre_truth_copy(ctx):
    import shutil
    pre = os.path.join(ctx.out, ctx.name + "_pre")
    for dp, _, fns in os.walk(pre):
        for fn in fns:
            rel = os.path.relpath(os.path.join(dp, fn), pre)
            os.makedirs(os.path.dirname(os.path.join(ctx.tdir, rel)), exist_ok=True)
            shutil.copyfile(os.path.join(dp, fn), os.path.join(ctx.tdir, rel))
    shutil.rmtree(pre)


def g_protected(ctx, out, rel):
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="block", name=rel[:-len(".protected.xml")],
         targetPath=os.path.join(out, rel))


def g_units(ctx, out, rel):
    with open(os.path.join(out, rel), "w", encoding="utf-8-sig") as f:
        f.write(_raw(ctx, "TiaPlcSoftware", operation="unitList", objectKind="block", device=rel.split("/")[0]))


def g_db_global_1(ctx, out, rel):
    """DB_Global_1 lies in software unit Unit_A, which the export does not reach: as in the original truth, the export
    of its source DB_Global (identical content)."""
    _raw(ctx, "TiaPlcSoftware", operation="export", objectKind="block", name="DB_Global", targetPath=os.path.join(out, rel))


KHP = "khp"
KHP_PRE = ["PLC_1/blocks/DB_Khp.xml", "PLC_1/blocks/DB_Khp.members.json", "PLC_1/blocks/FB_Khp_DB.xml",
           "PLC_1/blocks/FB_Khp_DB.members.json"]
RECIPES["070_db_khp_units"] = {"from": "029_fb_idb_value", "steps": [
    doc_write("block", src(KHP, "DB_Khp.s7dcl")), doc_write_indented("block", src(KHP, "FB_Khp.s7dcl")),
    plc("create", "block", name="FB_Khp_DB", blockType="InstanceDB", instanceOf="FB_Khp"), compile_plc,
    pre_truth(KHP_PRE),
    plc("protection", "block", name="DB_Khp", action="protect", password="Khp!Test2026"),
    plc("protection", "block", name="FB_Khp", action="protect", password="Khp!Test2026"),
    plc("unitCreate", "block", name="Unit_A"),
    pause("PLC_1 > Program blocks: copy DB_Global, paste into Software units > Unit_A > Program blocks (DB_Global_1)"),
    compile_plc],
    "after_archive": [pre_truth_copy],
    "generators": [(r"[^/]+\.protected\.xml", g_protected), (r"PLC_1/units\.json", g_units), (r"editor/.+", g_copy_old),
                   (r"PLC_1/blocks/DB_Global_1\.xml", g_db_global_1)] + DEV_OBJ}

MOTION_TOS = [("mTO_SpeedAxis", "TO_SpeedAxis"), ("mTO_PositioningAxis", "TO_PositioningAxis"),
              ("mTO_SynchronousAxis", "TO_SynchronousAxis"), ("mTO_ExternalEncoder", "TO_ExternalEncoder")]
def compile_no_errors(ctx):
    """Compile of the PLC software that must end without errors (a compile with errors leaves the unused type sets of
    the technology objects, e.g. DX_TEL_SyncOp, in the program)."""
    r = json.loads(ctx.call("TiaPlcSoftware", operation="compile", objectKind="block"))
    if r.get("errorCount"):
        raise RuntimeError("compile errors: " + json.dumps(r.get("messages"))[:800])


# The original first created every technology object with the versions 3.0, 4.0, 5.0 and 6.0 and deleted it again
# (object ids of the usedProducts entries 'S7-1500 Motion Control' V14..V17 and of the type blocks). Deleting a TO
# keeps its type blocks and data types, so the 6.0 types are created as TO_*_3 / TO_Struct_*_3 with the block numbers
# 58, 59, 65, 67, 71, 75; the compile removes the unused sets and names the 6.0 types TO_*_2 (measured, see
# C:\Temp\Fix\fix_progress.md).
RECIPES["068_motion_tos"] = {"from": "029_fb_idb_value", "steps": [
    s for n, t in MOTION_TOS for v in ("3.0", "4.0", "5.0", "6.0") for s in (
        plc("create", "technologyObject", name=n, toType=t, toVersion=v),
        plc("delete", "technologyObject", name=n))] + [
    # second pass, per TO: the four versions again, then the final 6.0. The alarm text lists live as long as a TO
    # exists, so the lists 104/307/308/341/411/533/534/541 keep the texts of the 3.0 positioning axis that created
    # them while mTO_SpeedAxis already existed ('HW error encoder'), and 511 comes later (order of the indices)
    s for n, t in MOTION_TOS for s in [x for v in ("3.0", "4.0", "5.0", "6.0") for x in (
        plc("create", "technologyObject", name=n, toType=t, toVersion=v),
        plc("delete", "technologyObject", name=n))] + [
        plc("create", "technologyObject", name=n, toType=t, toVersion="6.0")]] + [
    plc("parameterSet", "technologyObject", name=n, parameters='{"VirtualAxis.Mode":1}') for n, _ in MOTION_TOS[:3]] + [
    ("TiaDeviceItem", dict(operation="plugNew", device="PLC_1", typeIdentifier="OrderNumber:6ES7 550-1AA01-0AB0/V2.0",
                           name="TM_Count_1", positionNumber="2", parentDeviceItemPath="Rail_0")),
    transfer("068_motion_tos", only={"DB_Enc"}, kinds=("block",)),
    # DB_Enc must be compiled before the data connection is entered: the editor cannot resolve DB_Enc.tel.Sensor_1 of
    # an uncompiled DB and the compile of the TO then reports missing telegram addresses
    plc("compile", "block", name="DB_Enc"),
    pause("PLC_1 > Technology objects > mTO_ExternalEncoder > Configuration > Hardware interface: encoder data "
          "connection 'Data block', DB_Enc.tel.Sensor_1; then leave the field (select another page of the "
          "configuration) so that the entry is applied before the compile"),
    compile_no_errors],
    "generators": DEV_OBJ}

RECIPES["065_db_iec_timer"] = {"from": "060_db_attrs", "steps": [
    pause("PLC_1 > Program blocks > Add new block > Data block 'DBTon', type IEC_TIMER; in DBTon start value of PT T#2S"),
    # no compile of the whole program: the original has DBTon without memory values (never compiled); only FC_Ton
    # was compiled, by documentWrite (the "compile 0/0" of the MANIFEST is the compile result of that call)
    doc_write_indented("block", src("dbattr", "FC_Ton.s7dcl"))],
    "generators": [(r"DBTon.*", g_copy_old), (r"FC_Ton\.xml", g_top_block_xml)]}

RECIPES["076_graph_actions"] = {"from": "074_fb_graph", "steps": [
    pause("PLC_1 > FB_Graph in the GRAPH editor, single step view: actions Step1 'N #q2', Step2 'S #q3', Step3 'R #q3'"),
    compile_plc],
    "generators": DEV_OBJ}

def g_sub_tos(ctx, out, rel):
    """Output cams, cam tracks and measuring inputs (sub-objects of a TO): exported with tools\\Export-SubTos.ps1
    (Openness directly; the MCP server has no operation for them)."""
    import subprocess
    d = os.path.dirname(os.path.join(out, rel))
    if os.path.exists(os.path.join(out, rel)) or getattr(ctx, "subtos_done", False):
        return
    ctx.subtos_done = True
    ps1 = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Export-SubTos.ps1")
    r = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1, "-TiaPid",
                        str(ctx.c.pid), "-Out", d], capture_output=True, text=True, timeout=600)
    ctx.log.append({"export_subtos": r.stdout[-2000:] + r.stderr[-2000:]})


TOS_072_A = ["mTO_PositioningAxis", "mTO_SynchronousAxis", "mTO_Cam", "mTO_Cam_10k"]
TOS_072_B = ["mTO_Kinematics", "mTO_SpeedAxis", "mTO_ExternalEncoder", "mTO_Interpreter", "mTO_InterpreterMapping",
             "mTO_InterpreterProgram"]
def _to072(n):
    return transfer("072_motion_tcpu", pred="-", kinds=("technologyObject",), only={n})


def _probe(n, version="7.0"):
    """Create and delete the TO with `version`: the type blocks stay and take block numbers (see below)."""
    return [plc("create", "technologyObject", name=n, toType=n[1:], toVersion=version),
            plc("delete", "technologyObject", name=n)]


def _db_enc_072(ctx):
    import rhelp
    ctx.call("TiaPlcSoftware", operation="import", objectKind="block", device="PLC_T",
             sourcePath=rhelp._xml("068_motion_tos", "PLC_1", "block", "DB_Enc"))
    rhelp._set_number(ctx, "PLC_T", "DB_Enc", 15)  # the original created it after the sub-TOs (DB12-14)


# The original created the CPU without the test security settings (protection level 4, no user, no certificate) and
# created every TO kind with version 7.0 first, then with 8.0 (usedProducts 'Motion Control Components' V7 and V8;
# block numbers of the 8.0 types: TO_PositioningAxis 55 / TO_SpeedAxis 56 / TO_Axis 57 / TO_Object 63 behind the 7.0
# types 54 / 53 / 52 / 62, and so on up to TO_ExternalEncoder 76). The interpreter types 72-74 were created between
# TO_Kinematics (71) and TO_ExternalEncoder (75 for 7.0, 76 for 8.0), their DBs 9-11 after mTO_ExternalEncoder (DB8).
RECIPES["072_motion_tcpu"] = {"from": None, "steps": [
    ("TiaDevice", dict(operation="create", name="PLC_T", deviceType="OrderNumber:6ES7 515-2TN03-0AB0/V3.1")),
    transfer("072_motion_tcpu", pred="-", kinds=("tagTable",), only={"Default tag table"}),
    # the cams with create as in the original (defaults): a SimaticML import of the same cam compiles to 362 bytes
    # more load memory
    ] + [s for n in TOS_072_A for s in _probe(n) + [
        plc("create", "technologyObject", name=n, toType=n[1:], toVersion="8.0") if "Cam" in n else _to072(n)]
         ] + _probe("mTO_LeadingAxisProxy") + [
    plc("create", "technologyObject", name="mTO_LeadingAxisProxy", toType="TO_LeadingAxisProxy", toVersion="8.0"),
    ] + _probe("mTO_Kinematics") + [_to072("mTO_Kinematics")] + [
    s for n in TOS_072_B[3:] for s in _probe(n, "8.0")] + [_to072("mTO_SpeedAxis")] + _probe("mTO_ExternalEncoder") + [
    # DB_Enc as in the original "from 068": the SimaticML of the 068 DB (PD_TEL81 version 6.0). TIA takes the 6.0
    # system data types into the project library and uses 8.0 for this CPU, so the library holds PD_TEL81 ... twice
    _db_enc_072] + [
    _to072(n) for n in TOS_072_B[2:]] + [
    # TM_Timer_1 after the TOs as in the original: the system constant Local~TM_Timer_1 then follows OB_MC_Servo,
    # OB_MC_Interpolator and OB_MC_LookAhead (with the module plugged first, the constants of the tag table take
    # 943 instead of 939 bytes of load memory)
    ("TiaDeviceItem", dict(operation="plugNew", device="PLC_T", typeIdentifier="OrderNumber:6ES7 552-1AA00-0AB0/V1.0",
                           name="TM_Timer_1", positionNumber="2", parentDeviceItemPath="Rail_0")),
    # TM Timer: channel configuration '8 inputs, 8 outputs', DI0/DI1 'Use inputs individually', both Timer DI;
    # PROFINET interface_1 as IO controller and IO device (values of the original, C:\Temp\FBR\_old details.json)
    ("TiaDeviceItem", dict(operation="setDetails", device="PLC_T", deviceItemPath="TM_Timer_1/TM_Timer_1",
                           attributesJson='{"ChannelConfiguration":255}')),
    ("TiaDeviceItem", dict(operation="setDetails", device="PLC_T", deviceItemPath="TM_Timer_1/TM_Timer_1",
                           attributesJson='{"ConfigurationDiGroup0":2}')),
    ] + [("TiaDeviceItem", dict(operation="channelSet", device="PLC_T", deviceItemPath="TM_Timer_1/TM_Timer_1",
                                channelNumber=ch, channelType="Technology", attributesJson='{"OperatingMode":34}'))
         for ch in ("0", "1")] + [
    ("TiaDeviceItem", dict(operation="setDetails", device="PLC_T", deviceItemPath="PLC_T/PROFINET interface_1",
                           attributesJson='{"InterfaceOperatingMode":"IoController, IoDevice"}')),
    pause("PLC_T: see MANUAL['072_motion_tcpu'] (sub-TOs, measuring input connection, I-device transfer area)"),
    ("TiaNetworkNode", dict(operation="transferAreaSet", device="PLC_T", deviceItemPath="PLC_T/PROFINET interface_1",
                            positionNumber="1000", propertiesJson='{"PartnerToLocalLength":32}')),
    plc("compile", "block", device="PLC_T")],
    "generators": [(r"editor/.+", g_copy_old),
                   (r"PLC_T/blocks/(OutputCam_1|CamTrack_1|MeasuringInput_1)\.xml", g_sub_tos)] + DEV_OBJ}

S12 = os.path.join("s71200_300")
RECIPES["071_db_1200_300"] = {"from": None, "steps": [
    ("TiaDevice", dict(operation="create", name="PLC_1200", deviceType="OrderNumber:6ES7 214-1AG40-0XB0/V4.7")),
    ("TiaDevice", dict(operation="create", name="PLC_300", deviceType="OrderNumber:6ES7 315-2EH14-0AB0/V3.2")),
    doc_write("type", src(S12, "UDT_12.s7dcl"), device="PLC_1200"),
    doc_write("block", src(S12, "DB_12Opt.s7dcl"), device="PLC_1200"),
    doc_write("block", src(S12, "DB_12Std.s7dcl"), device="PLC_1200"),
    doc_write_indented("block", src(S12, "FB_12.s7dcl"), device="PLC_1200"),
    plc("create", "block", device="PLC_1200", name="FB_12_DB", blockType="InstanceDB", instanceOf="FB_12"),
    plc("sourceImport", "block", device="PLC_300", sourcePath=src(S12, "S7300.scl")),
    plc("compile", "block", device="PLC_1200"), plc("compile", "block", device="PLC_300")],
    "generators": DEV_OBJ}

# compile of the PLC software (incl. safety program); TiaProject(compile) of the current server also compiles the
# hardware, which stops at the security settings of the test CPU (master secret / protection level, as in the original)
RECIPES["069_safety_compiled"] = {"from": "249_hw_fdi_module", "steps": [compile_plc],
                                  "generators": DEV_OBJ + [(r"editor/.+", g_copy_old)]}

# Predecessors of 069 outside this module's range (hardware step "Safety", MANIFEST): built here only as predecessors,
# without ground truth, and only when no other recipes_*.py defines them.
_HW_PRED = {
    "246_hw_fcpu": {"from": "000_empty", "steps": [
        ("TiaDevice", dict(operation="create", name="PLC_1", deviceType="OrderNumber:6ES7 513-1FL02-0AB0/V2.9"))]},
    "247_hw_f_settings": {"from": "246_hw_fcpu", "steps": [
        ("TiaProjectSecurity", dict(operation="safetySettingsSet", device="PLC_1", attributes='{"ActivationOfFChangeHistory":true}'))]},
    "248_hw_f_monitoring": {"from": "247_hw_f_settings", "steps": [
        ("TiaDeviceItem", dict(operation="setDetails", device="PLC_1", deviceItemPath="PLC_1",
                               attributesJson='{"Failsafe_DefaultFmonitoringTimeCentral":200}'))]},
    "249_hw_fdi_module": {"from": "248_hw_f_monitoring", "steps": [
        ("TiaDeviceItem", dict(operation="plugNew", device="PLC_1", typeIdentifier="OrderNumber:6ES7 526-1BH00-0AB0/V1.0",
                               name="FDI_1", positionNumber="2", parentDeviceItemPath="Rail_0"))]},
}


def _defined_elsewhere(name):
    here = os.path.dirname(os.path.abspath(__file__))
    for fn in os.listdir(here):
        if fn.startswith("recipes_") and fn.endswith(".py") and fn != os.path.basename(__file__):
            with open(os.path.join(here, fn), encoding="utf-8-sig") as f:
                if '"%s"' % name in f.read():
                    return True
    return False


for _n, _r in _HW_PRED.items():
    if not _defined_elsewhere(_n):
        RECIPES[_n] = dict(_r, truth=lambda ctx: None)

MANUAL = {
    "045_plc_textlist": "by hand in the TIA user interface (VNC): PLC text list TL_Test, decimal, 0 = 'Aus', 1 = 'Ein' "
                        "(recipe step rhelp.pause; save, truth and archive via MCP)",
    "065_db_iec_timer": "partly by hand (VNC): DB 'DBTon' of type IEC_TIMER with PT T#2S (Openness cannot create or list it)",
    "068_motion_tos": "partly by hand (VNC): encoder data connection of mTO_ExternalEncoder to DB_Enc.tel.Sensor_1",
    "070_db_khp_units": "partly by hand (VNC): DB_Global copied into software unit Unit_A (DB_Global_1)",
    "076_graph_actions": "by hand in the GRAPH editor (VNC): actions N #q2 / S #q3 / R #q3 in Step1..Step3",
    "072_motion_tcpu": "partly by hand (TIA user interface): under mTO_PositioningAxis > Output cam 'Add new output cam' "
                       "(OutputCam_1, DB12) and 'Add new cam track' (CamTrack_1, DB13), > Measuring input 'Add new "
                       "measuring input' (MeasuringInput_1, DB14); MeasuringInput_1 > Configuration > Hardware "
                       "interface: 'Measuring using Timer DI', measuring input PLC_T.TM_Timer_1.Channel 0, then "
                       "select another page; Device configuration > PROFINET interface_1 [X1] > Properties > "
                       "Operating mode > I-device communication: <Add new> (Transfer area_1, type CD, direction "
                       "partner to local, I 44; an area created by Openness is local to partner and keeps output "
                       "parameters when the direction is changed). The TM Timer channel configuration, the "
                       "operating mode of the interface and the transfer area length 32 are recipe steps; the "
                       "encoder connection DB_Enc.tel.Sensor_1 and the kinematics axes come with the SimaticML "
                       "import of the TOs. Export-SubTos.ps1 attaches via Openness: confirm the TIA 'Openness "
                       "access' dialog with Yes",
    "081_plc_opcua_nodes": "partly by hand (VNC): 'Generate local data' in the server interface editor (DB SI_Test_Data)",
}
