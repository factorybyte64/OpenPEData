| Sample | Source | Devices | Tag tables | Tags | Blocks | UDTs | Tags | Checks | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 000_empty | zap21 | 0 | 0 | 0 | 0 | 0 | 0 | 5 | OK |
| 000_empty2 | folder | 0 | 0 | 0 | 0 | 0 | 0 | 5 | OK |
| 000_empty_resave | folder | 0 | 0 | 0 | 0 | 0 | 0 | 5 | OK |
| 001_devgroup | zap21 | 0 | 0 | 0 | 0 | 0 | 0 | 5 | OK |
| 002_cpu | zap21 | 1 | 1 | 0 | 1 | 0 | 0 | 10 | OK |
| 003_tagtable | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 004_fb | zap21 | 1 | 1 | 0 | 2 | 0 | 0 | 10 | OK |
| 005_tag_int | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 006_tag_addr | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 007_tag_comment | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 008_tag_flags | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 009_tag_rename | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 18 | OK |
| 010_dev_in_group | zap21 | 1 | 1 | 0 | 1 | 0 | 0 | 10 | OK |
| 011_tagtable_folder | zap21 | 1 | 3 | 1 | 1 | 0 | 0 | 21 | OK |
| 012_tags_multi | zap21 | 1 | 2 | 9 | 1 | 0 | 0 | 67 | OK |
| 013_tag_umlaut | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 19 | OK |
| 014_tag_delete | zap21 | 1 | 2 | 0 | 1 | 0 | 0 | 13 | OK |
| 015_cpu2 | zap21 | 2 | 2 | 0 | 2 | 0 | 0 | 16 | OK |
| 016_tag_accessible | zap21 | 1 | 2 | 1 | 1 | 0 | 0 | 19 | OK |
| 017_udt | zap21 | 1 | 1 | 0 | 2 | 1 | 3 | 24 | OK |
| 018_db_global | zap21 | 1 | 1 | 0 | 3 | 1 | 10 | 82 | OK |
| 019_db_instance | zap21 | 1 | 1 | 0 | 4 | 1 | 10 | 84 | OK |
| 020_block_group | zap21 | 1 | 1 | 0 | 5 | 1 | 11 | 94 | OK |
| 021_mastercopy | zap21 | 1 | 1 | 0 | 2 | 0 | 0 | 11 | OK |
| 022_db_udt | zap21 | 1 | 1 | 0 | 6 | 1 | 15 | 111 | OK |

| Sample (interfaces FB/FC/IDB) | Source | Interface tags | Checks | Result |
| --- | --- | --- | --- | --- |
| 023_fb_iface | zap21 | 32 | 142 | OK |
| 024_fc_iface | zap21 | 30 | 134 | OK |
| 025_fb_idb | zap21 | 26 | 118 | OK |
| 026_fc_scl | zap21 | 18 | 86 | OK |
| 027_fc_lad | zap21 | 22 | 70 | OK |
| 028_fc_fbd | zap21 | 20 | 70 | OK |
| 029_fb_idb_value | zap21 | 26 | 118 | OK |

Integrity (`--verify`): header hash, record hashes, hash chain and RSA signature of all $$COMMIT$ records; tamper test (5 checks): OK

| Sample | Source | Commits (chain+signature) | Checks | Result |
| --- | --- | --- | --- | --- |
| 000_empty | zap21 | 1 | 2 | OK |
| 000_empty2 | folder | 2 | 2 | OK |
| 000_empty_resave | folder | 2 | 2 | OK |
| 001_devgroup | zap21 | 1 | 2 | OK |
| 002_cpu | zap21 | 1 | 2 | OK |
| 003_tagtable | zap21 | 1 | 2 | OK |
| 004_fb | zap21 | 1 | 2 | OK |
| 005_tag_int | zap21 | 1 | 2 | OK |
| 006_tag_addr | zap21 | 1 | 2 | OK |
| 007_tag_comment | zap21 | 1 | 2 | OK |
| 008_tag_flags | zap21 | 1 | 2 | OK |
| 009_tag_rename | zap21 | 1 | 2 | OK |
| 010_dev_in_group | zap21 | 1 | 2 | OK |
| 011_tagtable_folder | zap21 | 1 | 2 | OK |
| 012_tags_multi | zap21 | 1 | 2 | OK |
| 013_tag_umlaut | zap21 | 1 | 2 | OK |
| 014_tag_delete | zap21 | 1 | 2 | OK |
| 015_cpu2 | zap21 | 1 | 2 | OK |
| 016_tag_accessible | zap21 | 1 | 2 | OK |
| 017_udt | zap21 | 1 | 2 | OK |
| 018_db_global | zap21 | 1 | 2 | OK |
| 019_db_instance | zap21 | 1 | 2 | OK |
| 020_block_group | zap21 | 1 | 2 | OK |
| 021_mastercopy | zap21 | 1 | 2 | OK |
| 022_db_udt | zap21 | 1 | 2 | OK |
| 023_fb_iface | zap21 | 1 | 2 | OK |
| 024_fc_iface | zap21 | 1 | 2 | OK |
| 025_fb_idb | zap21 | 1 | 2 | OK |
| 026_fc_scl | zap21 | 1 | 2 | OK |
| 027_fc_lad | zap21 | 1 | 2 | OK |
| 028_fc_fbd | zap21 | 1 | 2 | OK |
| 029_fb_idb_value | zap21 | 1 | 2 | OK |
| 030_scl_all | zap21 | 1 | 2 | OK |
| 031_lad_all | zap21 | 1 | 2 | OK |
| 032_fbd_all | zap21 | 1 | 2 | OK |
| 034_flg_misc | zap21 | 1 | 2 | OK |
| 035_scl_rest | zap21 | 1 | 2 | OK |
| 036_tech | zap21 | 1 | 2 | OK |
| 037_lad_xml | zap21 | 1 | 2 | OK |
| 038_comm_hw | zap21 | 1 | 2 | OK |
| 039_fw41 | zap21 | 1 | 2 | OK |
| 040_s71200 | zap21 | 1 | 2 | OK |
| 041_plc_alarm_fb | zap21 | 1 | 2 | OK |
| 042_plc_alarm_idb | zap21 | 1 | 2 | OK |
| 043_plc_alarm_text | zap21 | 1 | 2 | OK |
| 044_plc_alarm_class | zap21 | 1 | 2 | OK |
| 045_plc_textlist | zap21 | 1 | 2 | OK |
| 046_plc_textlist_entry | zap21 | 1 | 2 | OK |
| 047_plc_watch_table | zap21 | 1 | 2 | OK |
| 048_plc_watch_entry | zap21 | 1 | 2 | OK |
| 049_plc_unit | zap21 | 1 | 2 | OK |
| 050_plc_unit_b | zap21 | 1 | 2 | OK |
| 051_plc_unit_relation | zap21 | 1 | 2 | OK |
| 052_plc_to_param | zap21 | 1 | 2 | OK |
| 053_plc_to_inverse | zap21 | 1 | 2 | OK |
| 054_plc_opcua_interface | zap21 | 1 | 2 | OK |
| 055_plc_opcua_disabled | zap21 | 1 | 2 | OK |
| 056_plc_prodiag_fb | zap21 | 1 | 2 | OK |
| 057_plc_prodiag_supervision | zap21 | 1 | 2 | OK |
| 058_plc_textlist_range | zap21 | 1 | 2 | OK |
| 059_db_symbols | zap21 | 1 | 2 | OK |
| 060_db_attrs | zap21 | 1 | 2 | OK |
| 061_fc_stl | zap21 | 1 | 2 | OK |
| 062_fb_stl | zap21 | 1 | 2 | OK |
| 063_multi_plc | zap21 | 1 | 2 | OK |
| 064_stl_all | zap21 | 1 | 2 | OK |
| 065_db_iec_timer | zap21 | 1 | 2 | OK |
| 066_sys_fb_instances | zap21 | 1 | 2 | OK |
| 067_tsend_trcv_single | zap21 | 1 | 2 | OK |
| 068_motion_tos | zap21 | 1 | 2 | OK |
| 069_safety_compiled | zap21 | 1 | 2 | OK |
| 070_db_khp_units | zap21 | 1 | 2 | OK |
| 071_db_1200_300 | zap21 | 1 | 2 | OK |
| 072_motion_tcpu | zap21 | 1 | 2 | OK |
| 073_code_stress | zap21 | 1 | 2 | OK |
| 074_fb_graph | zap21 | 1 | 2 | OK |
| 075_stl_calls | zap21 | 1 | 2 | OK |
| 076_graph_actions | zap21 | 1 | 2 | OK |
| 077_graph_branches | zap21 | 1 | 2 | OK |
| 078_plc_force_entry | zap21 | 1 | 2 | OK |
| 079_plc_to_hw_connect | zap21 | 1 | 2 | OK |
| 080_graph_events | zap21 | 1 | 2 | OK |
| 081_plc_opcua_nodes | zap21 | 1 | 2 | OK |
| 082_plc_opcua_refns | zap21 | 1 | 2 | OK |
| 100_hmi_device | zap21 | 1 | 2 | OK |
| 101_hmi_base | zap21 | 1 | 2 | OK |
| 102_hmi_tag_int | zap21 | 1 | 2 | OK |
| 103_hmi_tag_ext | zap21 | 1 | 2 | OK |
| 104_hmi_tag_cycle | zap21 | 1 | 2 | OK |
| 105_hmi_tag_comment | zap21 | 1 | 2 | OK |
| 106_hmi_tagtable_group | zap21 | 1 | 2 | OK |
| 107_hmi_alarmclass | zap21 | 1 | 2 | OK |
| 108_hmi_alarm_discrete | zap21 | 1 | 2 | OK |
| 109_hmi_alarm_analog | zap21 | 1 | 2 | OK |
| 110_hmi_screen | zap21 | 1 | 2 | OK |
| 111_hmi_screen_rename | zap21 | 1 | 2 | OK |
| 112_hmi_screen_backcolor | zap21 | 1 | 2 | OK |
| 113_hmi_screen_group | zap21 | 1 | 2 | OK |
| 114_hmi_screen2 | zap21 | 1 | 2 | OK |
| 115_hmi_item_rect | zap21 | 1 | 2 | OK |
| 116_hmi_item_rect_pos | zap21 | 1 | 2 | OK |
| 117_hmi_item_rect_color | zap21 | 1 | 2 | OK |
| 118_hmi_item_text | zap21 | 1 | 2 | OK |
| 119_hmi_screen_tag | zap21 | 1 | 2 | OK |
| 120_hmi_item_iofield | zap21 | 1 | 2 | OK |
| 121_hmi_item_iofield_pvtext | zap21 | 1 | 2 | OK |
| 122_hmi_item_button | zap21 | 1 | 2 | OK |
| 123_hmi_button_event | zap21 | 1 | 2 | OK |
| 124_hmi_item_iofield_dyn | zap21 | 1 | 2 | OK |
| 126_hmi_subnet | zap21 | 1 | 2 | OK |
| 128_hmi_item_polyline | zap21 | 1 | 2 | OK |
| 129_hmi_item_listbox | zap21 | 1 | 2 | OK |
| 130_hmi_script_multiline | zap21 | 1 | 2 | OK |
| 131_hmi_event_down | zap21 | 1 | 2 | OK |
| 132_hmi_dyn_color | zap21 | 1 | 2 | OK |
| 133_hmi_dyn_script | zap21 | 1 | 2 | OK |
| 134_hmi_dyn_range3 | zap21 | 1 | 2 | OK |
| 135_hmi_item_alarmind | zap21 | 1 | 2 | OK |
| 136_hmi_datalog | zap21 | 1 | 2 | OK |
| 137_hmi_datalog_size | zap21 | 1 | 2 | OK |
| 138_hmi_logging_tag | zap21 | 1 | 2 | OK |
| 139_hmi_alarmlog | zap21 | 1 | 2 | OK |
| 140_hmi_logging_cyclic | zap21 | 1 | 2 | OK |
| 141_hmi_datalog_period | zap21 | 1 | 2 | OK |
| 142_hmi_alarmlog_size | zap21 | 1 | 2 | OK |
| 143_hmi_logging_cycle | zap21 | 1 | 2 | OK |
| 144_hmi_datalog_backup | zap21 | 1 | 2 | OK |
| 145_hmi_rt_gmp | zap21 | 1 | 2 | OK |
| 146_hmi_audittrail_size | zap21 | 1 | 2 | OK |
| 147_hmi_rt_panning | zap21 | 1 | 2 | OK |
| 148_hmi_rt_maxlogin | zap21 | 1 | 2 | OK |
| 149_hmi_rt_maxlogin_errors | zap21 | 1 | 2 | OK |
| 150_hmi_rt_prodiag | zap21 | 1 | 2 | OK |
| 151_hmi_rt_opcua | zap21 | 1 | 2 | OK |
| 152_hmi_rt_reporting | zap21 | 1 | 2 | OK |
| 153_hmi_lang_de | zap21 | 1 | 2 | OK |
| 154_hmi_lang_enable | zap21 | 1 | 2 | OK |
| 155_hmi_lang_logging | zap21 | 1 | 2 | OK |
| 156_hmi_script_module | zap21 | 1 | 2 | OK |
| 157_hmi_script_globaldef | zap21 | 1 | 2 | OK |
| 158_hmi_textlist | zap21 | 1 | 2 | OK |
| 159_hmi_graphiclist | zap21 | 1 | 2 | OK |
| 160_hmi_plantview | zap21 | 1 | 2 | OK |
| 161_hmi_plantview_node | zap21 | 1 | 2 | OK |
| 162_hmi_opcua_connection | zap21 | 1 | 2 | OK |
| 163_hmi_opcua_alarmtype | zap21 | 1 | 2 | OK |
| 164_hmi_pc_base | zap21 | 1 | 2 | OK |
| 165_hmi_item_defaults | zap21 | 1 | 2 | OK |
| 166_hmi_style_dark | folder | 8 | 2 | OK |
| 167_hmi_style_bright | folder | 8 | 2 | OK |
| 168_hmi_graphiclist_entry | zap21 | 1 | 2 | OK |
| 169_hmi_start_screen | zap21 | 1 | 2 | OK |
| 170_hmi_plant_object_type | zap21 | 1 | 2 | OK |
| 171_hmi_thresholds | zap21 | 1 | 2 | OK |
| 172_hmi_bar_segmented | zap21 | 1 | 2 | OK |
| 200_hw_cycle_time | zap21 | 1 | 2 | OK |
| 201_hw_clock_memory | zap21 | 1 | 2 | OK |
| 202_hw_startup | zap21 | 1 | 2 | OK |
| 203_hw_webserver | zap21 | 1 | 2 | OK |
| 204_hw_access_level | zap21 | 1 | 2 | OK |
| 205_hw_di_module | zap21 | 1 | 2 | OK |
| 206_hw_io_start | zap21 | 1 | 2 | OK |
| 207_hw_ob_cyclic | zap21 | 1 | 2 | OK |
| 208_hw_process_image | zap21 | 1 | 2 | OK |
| 209_hw_dq_module | zap21 | 1 | 2 | OK |
| 210_hw_ip_address | zap21 | 1 | 2 | OK |
| 211_hw_pn_name | zap21 | 1 | 2 | OK |
| 212_hw_subnet | zap21 | 1 | 2 | OK |
| 213_hw_io_system | zap21 | 1 | 2 | OK |
| 214_hw_et200sp_station | zap21 | 1 | 2 | OK |
| 215_hw_io_subnet | zap21 | 1 | 2 | OK |
| 216_hw_io_device | zap21 | 1 | 2 | OK |
| 217_hw_io_module | zap21 | 1 | 2 | OK |
| 218_hw_s7_connection | zap21 | 1 | 2 | OK |
| 219_hw_tcp_connection | zap21 | 1 | 2 | OK |
| 220_hw_cm_dp | zap21 | 1 | 2 | OK |
| 221_hw_profibus | zap21 | 1 | 2 | OK |
| 222_hw_dp_master | zap21 | 1 | 2 | OK |
| 223_hw_dp_station | zap21 | 1 | 2 | OK |
| 224_hw_dp_subnet | zap21 | 1 | 2 | OK |
| 225_hw_dp_slave | zap21 | 1 | 2 | OK |
| 226_hw_port_link | zap21 | 1 | 2 | OK |
| 227_hw_send_clock | zap21 | 1 | 2 | OK |
| 228_hw_sync_domain | zap21 | 1 | 2 | OK |
| 230_hw_opcua_on | zap21 | 1 | 2 | OK |
| 231_hw_opcua_port | zap21 | 1 | 2 | OK |
| 232_hw_ntp | zap21 | 1 | 2 | OK |
| 233_hw_timezone | zap21 | 1 | 2 | OK |
| 234_hw_sysdiag | zap21 | 1 | 2 | OK |
| 236_hw_access_control | zap21 | 1 | 2 | OK |
| 237_hw_confidential | zap21 | 1 | 2 | OK |
| 238_hw_certificate | zap21 | 1 | 2 | OK |
| 239_hw_webserver | zap21 | 1 | 2 | OK |
| 240_hw_umac_user | zap21 | 1 | 2 | OK |
| 241_hw_umac_role | zap21 | 1 | 2 | OK |
| 242_hw_umac_user_role | zap21 | 1 | 2 | OK |
| 243_hw_aiaq_module | zap21 | 1 | 2 | OK |
| 244_hw_aiaq_value_status | zap21 | 1 | 2 | OK |
| 245_hw_tm_count | zap21 | 1 | 2 | OK |
| 246_hw_fcpu | zap21 | 1 | 2 | OK |
| 247_hw_f_settings | zap21 | 1 | 2 | OK |
| 248_hw_f_monitoring | zap21 | 1 | 2 | OK |
| 249_hw_fdi_module | zap21 | 1 | 2 | OK |
| 250_hw_fdi_dest_address | zap21 | 1 | 2 | OK |
| 251_hw_fdi_shortcircuit | zap21 | 1 | 2 | OK |
| 252_hw_f_runtime_group | zap21 | 1 | 2 | OK |
| 253_hw_f_password | zap21 | 1 | 2 | OK |
| 254_hw_channel_param | zap21 | 1 | 2 | OK |
| 255_hw_channel_param2 | zap21 | 1 | 2 | OK |
| F01_hmi_allobjects | zap21 | 1 | 2 | OK |
| F02_hmi_faceplates_crash | folder | 8 | 2 | OK |
| F03_hmi_faceplates_user | folder | 11 | 2 | OK |
| F05_fp_iface_instance | zap21 | 1 | 2 | OK |
| F12_iface_tags | zap21 | 1 | 2 | OK |
| F15_local_tag | zap21 | 1 | 2 | OK |
| F16_iface_props | zap21 | 1 | 2 | OK |
| F18_iface_event | zap21 | 1 | 2 | OK |
| F18b_event_handler | zap21 | 1 | 2 | OK |
| F18c_event_funclist | zap21 | 1 | 2 | OK |
| F20_type_props | zap21 | 1 | 2 | OK |
| F30_nested | zap21 | 1 | 2 | OK |
| F32_multilingual | zap21 | 1 | 2 | OK |
| F33_control_columns | zap21 | 1 | 2 | OK |
| F36_popup_alwaysdownload | zap21 | 1 | 2 | OK |
| L01_udt_type_inwork | zap21 | 1 | 2 | OK |
| L02_udt_type_released | zap21 | 1 | 2 | OK |
| L03_udt_type_v2 | zap21 | 1 | 2 | OK |
| L04_default_changed | zap21 | 1 | 2 | OK |
| L05_fb_type_inwork | zap21 | 1 | 2 | OK |
| L06_fb_type_released | zap21 | 1 | 2 | OK |
| L07_fc_type | zap21 | 1 | 2 | OK |
| L08_type_in_folder | zap21 | 1 | 2 | OK |
| L09_type_depends | zap21 | 1 | 2 | OK |
| L10_mc_fc | zap21 | 1 | 2 | OK |
| L11_mc_fb_udt | zap21 | 1 | 2 | OK |
| L12_mc_two | zap21 | 1 | 2 | OK |
| L13_mc_instantiate | zap21 | 1 | 2 | OK |
| L14_mc_delete | zap21 | 1 | 2 | OK |
| L15_mc_db | zap21 | 1 | 2 | OK |
| L16_inst_two_devices | zap21 | 1 | 2 | OK |
| L17_inst_outdated | zap21 | 1 | 2 | OK |
| L18_inst_edit_outdated | zap21 | 1 | 2 | OK |
| L19_version_discarded | zap21 | 1 | 2 | OK |
| L20_default_two_devices | zap21 | 1 | 2 | OK |
| L21_fb_inst_outdated | zap21 | 1 | 2 | OK |
| L22_update_removes_unused | zap21 | 1 | 2 | OK |
| L23_inst_called | zap21 | 1 | 2 | OK |
| L24_inst_update_fb | zap21 | 1 | 2 | OK |
| L25_version_deleted | zap21 | 1 | 2 | OK |
| L26_type_obsolete | zap21 | 1 | 2 | OK |
| L27_inst_update_udt | zap21 | 1 | 2 | OK |
| L28_mc_in_folder | zap21 | 1 | 2 | OK |
| L29_version_content | zap21 | 1 | 2 | OK |
| L33_proj_update_from_glib | zap21 | 1 | 2 | OK |
| L34_proj_mc_from_glib | zap21 | 1 | 2 | OK |
| L35_device_from_mc | zap21 | 1 | 2 | OK |
| L37_hmi_lib_types | zap21 | 1 | 2 | OK |
| L38_hmi_lib_type_content | zap21 | 1 | 2 | OK |
| L39_hmi_lib_textlist_type | zap21 | 1 | 2 | OK |
| L40_hmi_lib_graphiclist_type | zap21 | 1 | 2 | OK |

Derived stores: `PEData.idx` against PEData.plf (folders only), `XRef.db` (parts -> plf keys, relations, tag addresses against LogicalAddress, savepoint)

| Sample | Source | XRef relations | Checks | Result |
| --- | --- | --- | --- | --- |
| 000_empty | zap21 | 0 | 2 | OK |
| 000_empty2 | folder | 0 | 5 | OK |
| 000_empty_resave | folder | 0 | 5 | OK |
| 001_devgroup | zap21 | 0 | 2 | OK |
| 002_cpu | zap21 | 0 | 2 | OK |
| 003_tagtable | zap21 | 0 | 4 | OK |
| 004_fb | zap21 | 0 | 2 | OK |
| 005_tag_int | zap21 | 0 | 3 | OK |
| 006_tag_addr | zap21 | 0 | 4 | OK |
| 007_tag_comment | zap21 | 0 | 4 | OK |
| 008_tag_flags | zap21 | 0 | 4 | OK |
| 009_tag_rename | zap21 | 0 | 4 | OK |
| 010_dev_in_group | zap21 | 0 | 2 | OK |
| 011_tagtable_folder | zap21 | 0 | 4 | OK |
| 012_tags_multi | zap21 | 0 | 12 | OK |
| 013_tag_umlaut | zap21 | 0 | 4 | OK |
| 014_tag_delete | zap21 | 0 | 3 | OK |
| 015_cpu2 | zap21 | 0 | 2 | OK |
| 016_tag_accessible | zap21 | 0 | 4 | OK |
| 017_udt | zap21 | 0 | 2 | OK |
| 018_db_global | zap21 | 0 | 2 | OK |
| 019_db_instance | zap21 | 1 | 3 | OK |
| 020_block_group | zap21 | 1 | 3 | OK |
| 021_mastercopy | zap21 | 0 | 2 | OK |
| 022_db_udt | zap21 | 2 | 3 | OK |
| 023_fb_iface | zap21 | 16 | 3 | OK |
| 024_fc_iface | zap21 | 32 | 3 | OK |
| 025_fb_idb | zap21 | 17 | 3 | OK |
| 026_fc_scl | zap21 | 36 | 3 | OK |
| 027_fc_lad | zap21 | 41 | 3 | OK |
| 028_fc_fbd | zap21 | 46 | 3 | OK |
| 029_fb_idb_value | zap21 | 17 | 3 | OK |
| 030_scl_all | zap21 | 624 | 23 | OK |
| 031_lad_all | zap21 | 1856 | 23 | OK |
| 032_fbd_all | zap21 | 1755 | 23 | OK |
| 034_flg_misc | zap21 | 737 | 23 | OK |
| 035_scl_rest | zap21 | 653 | 23 | OK |
| 036_tech | zap21 | 790 | 23 | OK |
| 037_lad_xml | zap21 | 2374 | 23 | OK |
| 038_comm_hw | zap21 | 401 | 3 | OK |
| 039_fw41 | zap21 | 4570 | 3 | OK |
| 040_s71200 | zap21 | 18 | 3 | OK |
| 041_plc_alarm_fb | zap21 | 8 | 3 | OK |
| 042_plc_alarm_idb | zap21 | 10 | 3 | OK |
| 043_plc_alarm_text | zap21 | 10 | 3 | OK |
| 044_plc_alarm_class | zap21 | 10 | 3 | OK |
| 045_plc_textlist | zap21 | 10 | 3 | OK |
| 046_plc_textlist_entry | zap21 | 10 | 3 | OK |
| 047_plc_watch_table | zap21 | 0 | 4 | OK |
| 048_plc_watch_entry | zap21 | 0 | 4 | OK |
| 049_plc_unit | zap21 | 0 | 4 | OK |
| 050_plc_unit_b | zap21 | 0 | 4 | OK |
| 051_plc_unit_relation | zap21 | 0 | 4 | OK |
| 052_plc_to_param | zap21 | 790 | 23 | OK |
| 053_plc_to_inverse | zap21 | 790 | 23 | OK |
| 054_plc_opcua_interface | zap21 | 0 | 4 | OK |
| 055_plc_opcua_disabled | zap21 | 0 | 4 | OK |
| 056_plc_prodiag_fb | zap21 | 4 | 4 | OK |
| 057_plc_prodiag_supervision | zap21 | 7 | 4 | OK |
| 058_plc_textlist_range | zap21 | 10 | 3 | OK |
| 059_db_symbols | zap21 | 65 | 3 | OK |
| 060_db_attrs | zap21 | 61 | 3 | OK |
| 061_fc_stl | zap21 | 29 | 3 | OK |
| 062_fb_stl | zap21 | 41 | 3 | OK |
| 063_multi_plc | zap21 | 6 | 3 | OK |
| 064_stl_all | zap21 | 514 | 11 | OK |
| 065_db_iec_timer | zap21 | 66 | 3 | OK |
| 066_sys_fb_instances | zap21 | 695 | 3 | OK |
| 067_tsend_trcv_single | zap21 | 701 | 3 | OK |
| 068_motion_tos | zap21 | 84 | 3 | OK |
| 069_safety_compiled | zap21 | 13 | 3 | OK |
| 070_db_khp_units | zap21 | 22 | 3 | OK |
| 071_db_1200_300 | zap21 | 25 | 3 | OK |
| 072_motion_tcpu | zap21 | 203 | 4 | OK |
| 073_code_stress | zap21 | 8224 | 11 | OK |
| 074_fb_graph | zap21 | 630 | 11 | OK |
| 075_stl_calls | zap21 | 1048 | 11 | OK |
| 076_graph_actions | zap21 | 633 | 11 | OK |
| 077_graph_branches | zap21 | 749 | 11 | OK |
| 078_plc_force_entry | zap21 | 0 | 4 | OK |
| 079_plc_to_hw_connect | zap21 | 791 | 24 | OK |
| 080_graph_events | zap21 | 811 | 11 | OK |
| 081_plc_opcua_nodes | zap21 | 0 | 4 | OK |
| 082_plc_opcua_refns | zap21 | 0 | 4 | OK |
| 100_hmi_device | zap21 | 108 | 3 | OK |
| 101_hmi_base | zap21 | 108 | 4 | OK |
| 102_hmi_tag_int | zap21 | 109 | 4 | OK |
| 103_hmi_tag_ext | zap21 | 111 | 4 | OK |
| 104_hmi_tag_cycle | zap21 | 111 | 4 | OK |
| 105_hmi_tag_comment | zap21 | 111 | 4 | OK |
| 106_hmi_tagtable_group | zap21 | 108 | 4 | OK |
| 107_hmi_alarmclass | zap21 | 111 | 4 | OK |
| 108_hmi_alarm_discrete | zap21 | 113 | 4 | OK |
| 109_hmi_alarm_analog | zap21 | 114 | 4 | OK |
| 110_hmi_screen | zap21 | 108 | 4 | OK |
| 111_hmi_screen_rename | zap21 | 108 | 4 | OK |
| 112_hmi_screen_backcolor | zap21 | 108 | 4 | OK |
| 113_hmi_screen_group | zap21 | 108 | 4 | OK |
| 114_hmi_screen2 | zap21 | 108 | 4 | OK |
| 115_hmi_item_rect | zap21 | 108 | 4 | OK |
| 116_hmi_item_rect_pos | zap21 | 108 | 4 | OK |
| 117_hmi_item_rect_color | zap21 | 108 | 4 | OK |
| 118_hmi_item_text | zap21 | 108 | 4 | OK |
| 119_hmi_screen_tag | zap21 | 109 | 4 | OK |
| 120_hmi_item_iofield | zap21 | 109 | 4 | OK |
| 121_hmi_item_iofield_pvtext | zap21 | 109 | 4 | OK |
| 122_hmi_item_button | zap21 | 108 | 4 | OK |
| 123_hmi_button_event | zap21 | 109 | 4 | OK |
| 124_hmi_item_iofield_dyn | zap21 | 110 | 4 | OK |
| 126_hmi_subnet | zap21 | 110 | 4 | OK |
| 128_hmi_item_polyline | zap21 | 108 | 4 | OK |
| 129_hmi_item_listbox | zap21 | 108 | 4 | OK |
| 130_hmi_script_multiline | zap21 | 109 | 4 | OK |
| 131_hmi_event_down | zap21 | 109 | 4 | OK |
| 132_hmi_dyn_color | zap21 | 111 | 4 | OK |
| 133_hmi_dyn_script | zap21 | 109 | 4 | OK |
| 134_hmi_dyn_range3 | zap21 | 111 | 4 | OK |
| 135_hmi_item_alarmind | zap21 | 108 | 4 | OK |
| 136_hmi_datalog | zap21 | 139 | 4 | OK |
| 137_hmi_datalog_size | zap21 | 139 | 4 | OK |
| 138_hmi_logging_tag | zap21 | 140 | 4 | OK |
| 139_hmi_alarmlog | zap21 | 166 | 4 | OK |
| 140_hmi_logging_cyclic | zap21 | 166 | 4 | OK |
| 141_hmi_datalog_period | zap21 | 166 | 4 | OK |
| 142_hmi_alarmlog_size | zap21 | 166 | 4 | OK |
| 143_hmi_logging_cycle | zap21 | 166 | 4 | OK |
| 144_hmi_datalog_backup | zap21 | 166 | 4 | OK |
| 145_hmi_rt_gmp | zap21 | 141 | 4 | OK |
| 146_hmi_audittrail_size | zap21 | 141 | 4 | OK |
| 147_hmi_rt_panning | zap21 | 141 | 4 | OK |
| 148_hmi_rt_maxlogin | zap21 | 141 | 4 | OK |
| 149_hmi_rt_maxlogin_errors | zap21 | 141 | 4 | OK |
| 150_hmi_rt_prodiag | zap21 | 143 | 4 | OK |
| 151_hmi_rt_opcua | zap21 | 143 | 4 | OK |
| 152_hmi_rt_reporting | zap21 | 152 | 4 | OK |
| 153_hmi_lang_de | zap21 | 109 | 4 | OK |
| 154_hmi_lang_enable | zap21 | 109 | 4 | OK |
| 155_hmi_lang_logging | zap21 | 109 | 4 | OK |
| 156_hmi_script_module | zap21 | 110 | 4 | OK |
| 157_hmi_script_globaldef | zap21 | 111 | 4 | OK |
| 158_hmi_textlist | zap21 | 109 | 4 | OK |
| 159_hmi_graphiclist | zap21 | 109 | 4 | OK |
| 160_hmi_plantview | zap21 | 109 | 4 | OK |
| 161_hmi_plantview_node | zap21 | 109 | 4 | OK |
| 162_hmi_opcua_connection | zap21 | 114 | 4 | OK |
| 163_hmi_opcua_alarmtype | zap21 | 116 | 4 | OK |
| 164_hmi_pc_base | zap21 | 215 | 4 | OK |
| 165_hmi_item_defaults | zap21 | 108 | 4 | OK |
| 166_hmi_style_dark | folder | 108 | 7 | OK |
| 167_hmi_style_bright | folder | 108 | 7 | OK |
| 168_hmi_graphiclist_entry | zap21 | 111 | 4 | OK |
| 169_hmi_start_screen | zap21 | 108 | 4 | OK |
| 170_hmi_plant_object_type | zap21 | 240 | 4 | OK |
| 171_hmi_thresholds | zap21 | 215 | 4 | OK |
| 172_hmi_bar_segmented | zap21 | 215 | 4 | OK |
| 200_hw_cycle_time | zap21 | 0 | 2 | OK |
| 201_hw_clock_memory | zap21 | 0 | 12 | OK |
| 202_hw_startup | zap21 | 0 | 2 | OK |
| 203_hw_webserver | zap21 | 0 | 2 | OK |
| 204_hw_access_level | zap21 | 0 | 2 | OK |
| 205_hw_di_module | zap21 | 0 | 2 | OK |
| 206_hw_io_start | zap21 | 0 | 2 | OK |
| 207_hw_ob_cyclic | zap21 | 0 | 2 | OK |
| 208_hw_process_image | zap21 | 0 | 2 | OK |
| 209_hw_dq_module | zap21 | 0 | 2 | OK |
| 210_hw_ip_address | zap21 | 0 | 2 | OK |
| 211_hw_pn_name | zap21 | 0 | 2 | OK |
| 212_hw_subnet | zap21 | 0 | 2 | OK |
| 213_hw_io_system | zap21 | 0 | 2 | OK |
| 214_hw_et200sp_station | zap21 | 0 | 2 | OK |
| 215_hw_io_subnet | zap21 | 0 | 2 | OK |
| 216_hw_io_device | zap21 | 0 | 2 | OK |
| 217_hw_io_module | zap21 | 0 | 2 | OK |
| 218_hw_s7_connection | zap21 | 0 | 2 | OK |
| 219_hw_tcp_connection | zap21 | 0 | 2 | OK |
| 220_hw_cm_dp | zap21 | 0 | 2 | OK |
| 221_hw_profibus | zap21 | 0 | 2 | OK |
| 222_hw_dp_master | zap21 | 0 | 2 | OK |
| 223_hw_dp_station | zap21 | 0 | 2 | OK |
| 224_hw_dp_subnet | zap21 | 0 | 2 | OK |
| 225_hw_dp_slave | zap21 | 0 | 2 | OK |
| 226_hw_port_link | zap21 | 0 | 2 | OK |
| 227_hw_send_clock | zap21 | 0 | 2 | OK |
| 228_hw_sync_domain | zap21 | 0 | 2 | OK |
| 230_hw_opcua_on | zap21 | 4550 | 3 | OK |
| 231_hw_opcua_port | zap21 | 4550 | 3 | OK |
| 232_hw_ntp | zap21 | 4550 | 3 | OK |
| 233_hw_timezone | zap21 | 4550 | 3 | OK |
| 234_hw_sysdiag | zap21 | 4550 | 3 | OK |
| 236_hw_access_control | zap21 | 4550 | 3 | OK |
| 237_hw_confidential | zap21 | 4550 | 3 | OK |
| 238_hw_certificate | zap21 | 4550 | 3 | OK |
| 239_hw_webserver | zap21 | 4550 | 3 | OK |
| 240_hw_umac_user | zap21 | 4550 | 3 | OK |
| 241_hw_umac_role | zap21 | 4550 | 3 | OK |
| 242_hw_umac_user_role | zap21 | 4550 | 3 | OK |
| 243_hw_aiaq_module | zap21 | 0 | 2 | OK |
| 244_hw_aiaq_value_status | zap21 | 0 | 2 | OK |
| 245_hw_tm_count | zap21 | 0 | 2 | OK |
| 246_hw_fcpu | zap21 | 5 | 3 | OK |
| 247_hw_f_settings | zap21 | 5 | 3 | OK |
| 248_hw_f_monitoring | zap21 | 5 | 3 | OK |
| 249_hw_fdi_module | zap21 | 5 | 3 | OK |
| 250_hw_fdi_dest_address | zap21 | 5 | 3 | OK |
| 251_hw_fdi_shortcircuit | zap21 | 5 | 3 | OK |
| 252_hw_f_runtime_group | zap21 | 5 | 3 | OK |
| 253_hw_f_password | zap21 | 5 | 3 | OK |
| 254_hw_channel_param | zap21 | 0 | 2 | OK |
| 255_hw_channel_param2 | zap21 | 0 | 2 | OK |
| F01_hmi_allobjects | zap21 | 127 | 4 | OK |
| F02_hmi_faceplates_crash | folder | 127 | 7 | OK |
| F03_hmi_faceplates_user | folder | 137 | 7 | OK |
| F05_fp_iface_instance | zap21 | 139 | 4 | OK |
| F12_iface_tags | zap21 | 187 | 4 | OK |
| F15_local_tag | zap21 | 202 | 4 | OK |
| F16_iface_props | zap21 | 153 | 4 | OK |
| F18_iface_event | zap21 | 169 | 4 | OK |
| F18b_event_handler | zap21 | 173 | 4 | OK |
| F18c_event_funclist | zap21 | 210 | 4 | OK |
| F20_type_props | zap21 | 202 | 4 | OK |
| F30_nested | zap21 | 203 | 4 | OK |
| F32_multilingual | zap21 | 226 | 4 | OK |
| F33_control_columns | zap21 | 206 | 4 | OK |
| F36_popup_alwaysdownload | zap21 | 211 | 4 | OK |
| L01_udt_type_inwork | zap21 | 0 | 3 | OK |
| L02_udt_type_released | zap21 | 0 | 3 | OK |
| L03_udt_type_v2 | zap21 | 0 | 3 | OK |
| L04_default_changed | zap21 | 0 | 3 | OK |
| L05_fb_type_inwork | zap21 | 4 | 3 | OK |
| L06_fb_type_released | zap21 | 2 | 3 | OK |
| L07_fc_type | zap21 | 5 | 3 | OK |
| L08_type_in_folder | zap21 | 3 | 3 | OK |
| L09_type_depends | zap21 | 6 | 3 | OK |
| L10_mc_fc | zap21 | 36 | 3 | OK |
| L11_mc_fb_udt | zap21 | 16 | 3 | OK |
| L12_mc_two | zap21 | 32 | 3 | OK |
| L13_mc_instantiate | zap21 | 38 | 3 | OK |
| L14_mc_delete | zap21 | 32 | 3 | OK |
| L15_mc_db | zap21 | 2 | 3 | OK |
| L16_inst_two_devices | zap21 | 0 | 3 | OK |
| L17_inst_outdated | zap21 | 0 | 3 | OK |
| L18_inst_edit_outdated | zap21 | 0 | 3 | OK |
| L19_version_discarded | zap21 | 0 | 3 | OK |
| L20_default_two_devices | zap21 | 0 | 3 | OK |
| L21_fb_inst_outdated | zap21 | 4 | 3 | OK |
| L22_update_removes_unused | zap21 | 0 | 3 | OK |
| L23_inst_called | zap21 | 10 | 3 | OK |
| L24_inst_update_fb | zap21 | 10 | 3 | OK |
| L25_version_deleted | zap21 | 10 | 3 | OK |
| L26_type_obsolete | zap21 | 10 | 3 | OK |
| L27_inst_update_udt | zap21 | 3 | 3 | OK |
| L28_mc_in_folder | zap21 | 36 | 3 | OK |
| L29_version_content | zap21 | 0 | 3 | OK |
| L33_proj_update_from_glib | zap21 | 0 | 3 | OK |
| L34_proj_mc_from_glib | zap21 | 0 | 3 | OK |
| L35_device_from_mc | zap21 | 0 | 3 | OK |
| L37_hmi_lib_types | zap21 | 108 | 4 | OK |
| L38_hmi_lib_type_content | zap21 | 108 | 4 | OK |
| L39_hmi_lib_textlist_type | zap21 | 108 | 4 | OK |
| L40_hmi_lib_graphiclist_type | zap21 | 108 | 4 | OK |

| Sample (SCL vs. SD export) | Source | Networks | Checks | Result |
| --- | --- | --- | --- | --- |
| 030_scl_all | zap21 | 15 | 45 | OK |
| 031_lad_all | zap21 | 0 | 0 | OK |
| 032_fbd_all | zap21 | 0 | 0 | OK |
| 034_flg_misc | zap21 | 4 | 12 | OK |
| 035_scl_rest | zap21 | 1 | 3 | OK |
| 036_tech | zap21 | 1 | 3 | OK |
| 037_lad_xml | zap21 | 4 | 12 | OK |
| 038_comm_hw | zap21 | 0 | 0 | OK |
| 039_fw41 | zap21 | 103 | 309 | OK |
| 040_s71200 | zap21 | 0 | 0 | OK |
| 041_plc_alarm_fb | zap21 | 1 | 3 | OK |

SCL tokens in the source text (count over all samples), status: text/attribute/container/special = handled, - = not handled

| Tag | Count | Status |
| --- | --- | --- |
| AdMod | 2 | special |
| BC | 5 | container |
| BCE | 5 | text |
| BCL | 6 | attribute |
| BL | 2699 | special |
| BoxC | 41 | text |
| BoxO | 41 | text |
| BracC | 241 | text |
| BracO | 241 | text |
| CaseElem | 7 | container |
| CaseRange | 9 | container |
| CaseSRange | 3 | container |
| Colon | 8 | text |
| Comma | 375 | text |
| Const | 361 | attribute |
| Dot | 92 | text |
| Expression | 1857 | container |
| FctCa | 177 | container |
| FiSt | 505 | text |
| Fold | 251 | container |
| InstCa | 43 | container |
| KwBY | 2 | text |
| KwDO | 8 | text |
| KwELSE | 4 | text |
| KwELSIF | 3 | text |
| KwENDC | 3 | text |
| KwENDFOR | 6 | text |
| KwENDIF | 10 | text |
| KwENDR | 2 | text |
| KwENDW | 2 | text |
| KwEndRegion | 3 | text |
| KwOF | 3 | text |
| KwTHEN | 13 | text |
| KwTO | 6 | text |
| KwUNTIL | 2 | text |
| LC | 29 | attribute |
| LDots | 3 | text |
| Label | 1 | container |
| NL | 456 | special |
| OpAAND | 1 | text |
| OpAND | 5 | text |
| OpAs | 829 | text |
| OpDeref | 3 | text |
| OpDi | 5 | text |
| OpDivAs | 1 | text |
| OpE | 9 | text |
| OpG | 8 | text |
| OpGE | 1 | text |
| OpL | 4 | text |
| OpLE | 4 | text |
| OpMOD | 1 | text |
| OpMi | 9 | text |
| OpMinAs | 1 | text |
| OpMu | 13 | text |
| OpMulAs | 1 | text |
| OpNOT | 6 | text |
| OpOR | 3 | text |
| OpPa | 94 | text |
| OpPl | 30 | text |
| OpPlAs | 9 | text |
| OpPo | 1 | text |
| OpRef | 3 | text |
| OpU | 3 | text |
| OpXOR | 3 | text |
| Param | 575 | container |
| RootStatements | 129 | container |
| Statement | 508 | container |
| Statements | 37 | container |
| Sub | 220 | attribute |
| SymLa | 1 | attribute |
| SymPa | 575 | attribute |
| SymVa | 1215 | attribute |
| SymVaP | 2 | container |

| Sample (LAD/FBD vs. SimaticML) | Source | Wires | Checks | Result |
| --- | --- | --- | --- | --- |
| 030_scl_all | zap21 | 0 | 0 | OK |
| 031_lad_all | zap21 | 511 | 785 | OK |
| 032_fbd_all | zap21 | 419 | 708 | OK |
| 034_flg_misc | zap21 | 94 | 186 | OK |
| 035_scl_rest | zap21 | 0 | 0 | OK |
| 036_tech | zap21 | 44 | 14 | OK |
| 037_lad_xml | zap21 | 1053 | 1327 | OK |
| 038_comm_hw | zap21 | 116 | 70 | OK |
| 039_fw41 | zap21 | 2249 | 2087 | OK |
| 040_s71200 | zap21 | 10 | 14 | OK |

FlgNet elements in the plf (count over all LAD/FBD samples), status: handled = element and attributes are evaluated

| Element | Count | Attributes (count) | Status |
| --- | --- | --- | --- |
| AutomaticTyped | 2 | Name 2 | handled |
| CRef | 8 | RefId 8, UId 8 | handled |
| CodeBlock | 8 | RefId 8, UId 8 | handled |
| Equation | 2 | Valid 2 | handled |
| FlgNet | 748 | Lang 748, Routed 435, Version 748 | handled |
| Instance | 185 | RefId 185, UId 185 | handled |
| LRef | 457 | DisableENO 32, EN 23, ENO 23, RefId 457, UId 457 | handled |
| Labels | 8 |  | handled |
| Negated | 5 | PinName 5 | handled |
| OCon | 3942 | UId 3942 | handled |
| ORef | 3950 | RefId 3615, UId 3950 | handled |
| PCon | 4660 | PinName 4660, UId 4660 | handled |
| Part | 445 | DisableENO 117, Gate 445, UId 445 | handled |
| Parts | 748 |  | handled |
| Powerrail | 393 |  | handled |
| TemplateValue | 235 | Name 235, Type 235 | handled |
| Wire | 4496 | UId 4496 | handled |
| Wires | 748 |  | handled |

Ident table (IdentXmlPart): object kinds and sub-elements. read = value goes into the output, metadata = timestamps, memory layout or compiler aids (not evaluated)

| Object kind | Count | Status |
| --- | --- | --- |
| AufDBBlock | 4 | handled |
| BlockInterfaceInfo | 6 | handled |
| DepDBBlock | 21 | handled |
| FBBlock | 4 | handled |
| FCBlock | 2 | handled |
| GlobalAccess | 26 | handled |
| GlobalConstant | 2 | handled |
| Instruction | 457 | handled |
| InterfaceAccess | 3096 | handled |
| Label | 8 | handled |
| LiteralConstant | 67 | handled |
| LocalConstant | 2 | handled |
| MultInstAccess | 158 | handled |
| SimpleAccess | 34 | handled |

| Sub-element | Count | Status |
| --- | --- | --- |
| AO | 3309 | read |
| AOS | 3314 | read |
| ATD | 3309 | metadata |
| BAD | 26 | metadata |
| BIID | 6 | read |
| BPI | 66 | read |
| BPIL | 6 | read |
| C | 4368 | read |
| CB | 71 | read |
| CBOD | 6 | metadata |
| CD | 71 | metadata |
| CS | 3887 | read |
| DBBD | 25 | metadata |
| FBBD | 4 | metadata |
| IAD | 3254 | metadata |
| ID | 3887 | read |
| INSTD | 457 | read |
| IU | 457 | read |
| IUS | 457 | read |
| LCD | 2 | metadata |
| LD | 8 | metadata |
| LP | 2786 | read |
| LPI | 457 | read |
| MIAD | 158 | metadata |
| OD | 3416 | read |
| PBS | 2786 | metadata |
| PIS | 2786 | metadata |
| PSS | 2786 | metadata |
| SAD | 34 | read |
| SD | 3314 | metadata |
| SSD | 3314 | read |
| TD | 6791 | read |
| TOD | 31 | read |
| TP | 472 | metadata |
| TPI | 457 | metadata |
| TV | 472 | read |
