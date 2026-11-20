"""Recipes 000-029 (phase 3, steps 4b, 5a-5d of samples/MANIFEST.md)."""
import json

from rhelp import *  # noqa: F401,F403
from rhelp import CPU1513, FB_EMPTY_SD, block_truth, compile_plc, doc_write, doc_write_indented, plc, src, tag

RECIPES = {
    # --- phase 3 ---
    "000_empty": {"from": None},
    "000_empty2": {"from": None, "archive": False},
    "000_empty_resave": {"from": "000_empty", "archive": False},
    "001_devgroup": {"from": "000_empty", "steps": [("TiaDevice", dict(operation="groupCreate", name="Group_1"))]},
    "002_cpu": {"from": "000_empty", "steps": [("TiaDevice", dict(operation="create", name="PLC_1", deviceType=CPU1513))]},
    "003_tagtable": {"from": "002_cpu", "steps": [
        plc("create", "tagTable", name="TagTable_1"),
        tag("tagCreate", tagName="Tag_Bool_1", dataType="Bool", logicalAddress="%M0.0")]},
    "004_fb": {"from": "002_cpu", "steps": [plc("documentWrite", "block", source=FB_EMPTY_SD, compileAfter="false")]},
    # --- 4b tag variants ---
    "005_tag_int": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", dataType="Int")]},
    "006_tag_addr": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", logicalAddress="%M10.3")]},
    "007_tag_comment": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", comment="Test")]},
    "008_tag_flags": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", externalWritable="false")]},
    "009_tag_rename": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", newName="Tag_Renamed")]},
    # --- 5a gaps ---
    "010_dev_in_group": {"from": "001_devgroup", "steps": [
        ("TiaDevice", dict(operation="create", name="PLC_G", deviceType=CPU1513, parentGroup="Group_1"))]},
    "011_tagtable_folder": {"from": "003_tagtable", "steps": [
        plc("groupCreate", "tagTable", name="TagGroup_1"), plc("create", "tagTable", name="TagTable_2", groupPath="TagGroup_1")]},
    "012_tags_multi": {"from": "003_tagtable", "steps": [tag("tagCreate", tags=json.dumps([
        {"tagName": "In_Bool", "dataType": "Bool", "logicalAddress": "%I0.1"},
        {"tagName": "Out_Bool", "dataType": "Bool", "logicalAddress": "%Q2.0"},
        {"tagName": "M_Int", "dataType": "Int", "logicalAddress": "%MW20"},
        {"tagName": "M_Real", "dataType": "Real", "logicalAddress": "%MD24"},
        {"tagName": "In_Word", "dataType": "Word", "logicalAddress": "%IW4"},
        {"tagName": "Out_Byte", "dataType": "Byte", "logicalAddress": "%QB3"},
        {"tagName": "M_DInt", "dataType": "DInt", "logicalAddress": "%MD28", "comment": "DInt Kommentar"},
        {"tagName": "M_Time", "dataType": "Time", "logicalAddress": "%MD32"}], ensure_ascii=False))]},
    "013_tag_umlaut": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", comment="Prüfung äöüß")]},
    "014_tag_delete": {"from": "003_tagtable", "steps": [tag("tagDelete", tagName="Tag_Bool_1")]},
    "015_cpu2": {"from": "002_cpu", "steps": [("TiaDevice", dict(operation="create", name="PLC_2", cloneTypeFromDevice="PLC_1"))]},
    "016_tag_accessible": {"from": "003_tagtable", "steps": [tag("tagSet", tagName="Tag_Bool_1", externalAccessible="false")]},
    "021_mastercopy": {"from": "004_fb", "steps": [
        ("TiaProjectLibrary", dict(operation="masterCopyCreate", device="PLC_1", blockName="FB_Empty")),
        ("TiaProjectLibrary", dict(operation="masterCopiesList"))]},
    # --- 5b data blocks and PLC data types ---
    "017_udt": {"from": "004_fb", "steps": [doc_write("type", src("UDT_Motor.s7dcl"))]},
    "018_db_global": {"from": "017_udt", "steps": [doc_write("block", src("DB_Global.s7dcl"))]},
    "019_db_instance": {"from": "018_db_global", "steps": [
        plc("create", "block", name="FB_Empty_DB", blockType="InstanceDB", instanceOf="FB_Empty")]},
    "020_block_group": {"from": "019_db_instance", "steps": [
        plc("groupCreate", "block", name="BlockGroup_1"), doc_write("block", src("DB_InGroup.s7dcl"), groupPath="BlockGroup_1")]},
    "022_db_udt": {"from": "020_block_group", "steps": [doc_write("block", src("DB_Motor.s7dcl"))]},
    # --- 5c interfaces ---
    "023_fb_iface": {"from": "022_db_udt", "steps": [doc_write_indented("block", src("FB_Iface.s7dcl"))],
                     "generators": block_truth("interface")},
    "024_fc_iface": {"from": "023_fb_iface", "steps": [doc_write_indented("block", src("FC_Iface.s7dcl"))],
                     "generators": block_truth("interface")},
    "025_fb_idb": {"from": "023_fb_iface", "steps": [
        plc("create", "block", name="FB_Iface_DB", blockType="InstanceDB", instanceOf="FB_Iface"), compile_plc],
        "generators": block_truth("interface")},
    # --- 5d block code ---
    "026_fc_scl": {"from": "025_fb_idb", "steps": [doc_write("block", src("FC_Scl.s7dcl")), compile_plc],
                   "generators": block_truth("full")},
    "027_fc_lad": {"from": "026_fc_scl", "steps": [doc_write("block", src("FC_Lad.s7dcl")), compile_plc],
                   "generators": block_truth("full")},
    "028_fc_fbd": {"from": "027_fc_lad", "steps": [doc_write("block", src("FC_Fbd.s7dcl")), compile_plc],
                   "generators": block_truth("full")},
    "029_fb_idb_value": {"from": "025_fb_idb", "steps": [doc_write("block", src("FB_Iface_DB_value.s7dcl")), compile_plc],
                         "generators": block_truth("interface")},
}

