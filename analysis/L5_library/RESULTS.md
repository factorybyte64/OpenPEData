# L5 – test result library.py (as of L4)

Run: `python analysis\L5_libraryun_tests.py` over all samples (project folders with System\PEData.plf and .zap21) in `samples\`, hash check enabled.

**Overall: all expectations met, no exception** (175 samples)

Expectation: exactly one project library (ProjectLib) with the five system folders, no subfolders. Samples with `<sample>_truth	ypesList.json` (L01–L09, folder and .zap21): types, TypeGuid, Openness kind, folder path, version list, per version State, isDefault, author and FinalizationTime (= `modified` of typesList, to microseconds) as in the ground truth; instances (name, for `version x` also the linked version) as `instancesList`; type folder elements = types; L09: FB_Motor 1.2.3 depends on UDT_Motor 0.0.1. L4 (L16–L21, plus L03/L04): instances per device and version as `instancesList`, CreationTime of the instance = `CreationDate` from `<Name>.<Device>.attributes.json`, both stored version references (CurrentLibraryInterface, Expando UserTypeVersionGUID) match IsInstanceOf, derived version state per instance (version, default version yes/no, outdated, BlockToVersionElements) as expected. Samples with `<sample>_truth\masterCopiesList.json` (L10–L15): master copies in Openness order, folder, contents (name, block type), Subtype IECPL/Committed, CoreObjectData = one SelectedObject (SourceObjectId 0, Parent -1), no NonCoreObjects, TypeDescriptor, source scopes resolved (ProjectData, S7ControllerTargetData), context shell device + target, referenced objects (name stubs) and frozen UDT interfaces per sample, no H-020 warning, L14: exactly one deleted master copy (only in the folder; the .zap21 contains no tombstones). 021_mastercopy: exactly the master copy FB_Empty (as before). All others: empty library without types, versions and instances.

| Sample | Check | compared values | Master copies (contents) | Types | Versions | Instances | Types [Versions, * = default] | Objects in versions (copies) | Smoke test type paths / hash errors | Time |
|---|---|---|---|---|---|---|---|---|---|---|
| 000_empty | ok | – | – | 0 | 0 | 0 | – | 0 | 344/0 | 0.1 s |
| 000_empty.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 344/0 | 0.2 s |
| 000_empty2 | ok | – | – | 0 | 0 | 0 | – | 0 | 344/0 | 0.2 s |
| 000_empty_resave | ok | – | – | 0 | 0 | 0 | – | 0 | 344/0 | 0.2 s |
| 001_devgroup | ok | – | – | 0 | 0 | 0 | – | 0 | 345/0 | 0.1 s |
| 001_devgroup.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 345/0 | 0.1 s |
| 002_cpu | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 002_cpu.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 003_tagtable | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 003_tagtable.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 004_fb | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 004_fb.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 005_tag_int | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 005_tag_int.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 006_tag_addr | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 006_tag_addr.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 007_tag_comment | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 007_tag_comment.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 008_tag_flags | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 008_tag_flags.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 009_tag_rename | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 009_tag_rename.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 010_dev_in_group | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 010_dev_in_group.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 011_tagtable_folder | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 011_tagtable_folder.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 012_tags_multi | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 012_tags_multi.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 013_tag_umlaut | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 013_tag_umlaut.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 014_tag_delete | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 014_tag_delete.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 015_cpu2 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 015_cpu2.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 016_tag_accessible | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 016_tag_accessible.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 017_udt | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 017_udt.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 018_db_global | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 018_db_global.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 019_db_instance | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 019_db_instance.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 020_block_group | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 020_block_group.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 021_mastercopy | ok | – | FB_Empty (CodeBlockData FB_Empty) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 021_mastercopy.zap21 | ok | – | FB_Empty (CodeBlockData FB_Empty) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 022_db_udt | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 022_db_udt.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 023_fb_iface | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 023_fb_iface.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 024_fc_iface | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 024_fc_iface.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 025_fb_idb | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 025_fb_idb.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 026_fc_scl | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 026_fc_scl.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 027_fc_lad | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 027_fc_lad.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 028_fc_fbd | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 028_fc_fbd.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| 029_fb_idb_value | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 029_fb_idb_value.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| 030_scl_all | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 030_scl_all.zap21 | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 031_lad_all | ok | – | – | 77 | 0 | 0 | 77 system types | 0 | 400/0 | 2.0 s |
| 031_lad_all.zap21 | ok | – | – | 77 | 0 | 0 | 77 system types | 0 | 400/0 | 0.5 s |
| 032_fbd_all | ok | – | – | 74 | 0 | 0 | 74 system types | 0 | 400/0 | 1.1 s |
| 032_fbd_all.zap21 | ok | – | – | 74 | 0 | 0 | 74 system types | 0 | 400/0 | 0.4 s |
| 034_flg_misc | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 034_flg_misc.zap21 | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 035_scl_rest | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 035_scl_rest.zap21 | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 036_tech | ok | – | – | 194 | 0 | 0 | 194 system types | 0 | 400/0 | 0.5 s |
| 036_tech.zap21 | ok | – | – | 194 | 0 | 0 | 194 system types | 0 | 400/0 | 0.5 s |
| 037_lad_xml | ok | – | – | 38 | 0 | 0 | 38 system types | 0 | 400/0 | 0.3 s |
| 100_hmi_device | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 100_hmi_device.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 101_hmi_base | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 101_hmi_base.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 102_hmi_tag_int | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 102_hmi_tag_int.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 103_hmi_tag_ext | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 103_hmi_tag_ext.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 104_hmi_tag_cycle | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 104_hmi_tag_cycle.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 105_hmi_tag_comment | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 105_hmi_tag_comment.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.8 s |
| 106_hmi_tagtable_group | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 106_hmi_tagtable_group.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 107_hmi_alarmclass | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 107_hmi_alarmclass.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 108_hmi_alarm_discrete | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 108_hmi_alarm_discrete.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 109_hmi_alarm_analog | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 109_hmi_alarm_analog.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 110_hmi_screen | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 110_hmi_screen.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 111_hmi_screen_rename | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 111_hmi_screen_rename.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 112_hmi_screen_backcolor | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 112_hmi_screen_backcolor.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 113_hmi_screen_group | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 113_hmi_screen_group.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 114_hmi_screen2 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 114_hmi_screen2.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 115_hmi_item_rect | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 115_hmi_item_rect.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 116_hmi_item_rect_pos | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 116_hmi_item_rect_pos.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 117_hmi_item_rect_color | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 117_hmi_item_rect_color.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 118_hmi_item_text | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 118_hmi_item_text.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 119_hmi_screen_tag | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 119_hmi_screen_tag.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 120_hmi_item_iofield | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 120_hmi_item_iofield.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 121_hmi_item_iofield_pvtext | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 121_hmi_item_iofield_pvtext.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 122_hmi_item_button | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 122_hmi_item_button.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 123_hmi_button_event | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 123_hmi_button_event.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 124_hmi_item_iofield_dyn | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 124_hmi_item_iofield_dyn.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.4 s |
| 126_hmi_subnet | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 126_hmi_subnet.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.8 s |
| 128_hmi_item_polyline | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.7 s |
| 129_hmi_item_listbox | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 130_hmi_script_multiline | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 131_hmi_event_down | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| 132_hmi_dyn_color | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| 133_hmi_dyn_script | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| F01_hmi_allobjects | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.5 s |
| F01_hmi_allobjects.zap21 | ok | – | – | 0 | 0 | 0 | – | 0 | 400/0 | 0.6 s |
| L01_udt_type_inwork | ok | 10 | – | 1 | 1 | 1 | UDT_Motor [0.0.1 InWork] | 0 | 400/0 | 0.3 s |
| L01_udt_type_inwork.zap21 | ok | 10 | – | 1 | 1 | 1 | UDT_Motor [0.0.1 InWork] | 0 | 400/0 | 0.3 s |
| L02_udt_type_released | ok | 10 | – | 1 | 1 | 1 | UDT_Motor [0.0.1 Committed*] | 11 | 400/0 | 0.3 s |
| L02_udt_type_released.zap21 | ok | 10 | – | 1 | 1 | 1 | UDT_Motor [0.0.1 Committed*] | 11 | 400/0 | 0.3 s |
| L03_udt_type_v2 | ok | 18 | – | 1 | 2 | 1 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*] | 23 | 400/0 | 0.3 s |
| L03_udt_type_v2.zap21 | ok | 18 | – | 1 | 2 | 1 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*] | 23 | 400/0 | 0.3 s |
| L04_default_changed | ok | 16 | – | 1 | 2 | 1 | UDT_Motor [0.0.1 Committed*, 1.0.0 Committed] | 23 | 400/0 | 0.3 s |
| L04_default_changed.zap21 | ok | 16 | – | 1 | 2 | 1 | UDT_Motor [0.0.1 Committed*, 1.0.0 Committed] | 23 | 400/0 | 0.2 s |
| L05_fb_type_inwork | ok | 10 | – | 1 | 1 | 1 | FB_Typed [0.0.1 InWork] | 0 | 400/0 | 0.2 s |
| L05_fb_type_inwork.zap21 | ok | 10 | – | 1 | 1 | 1 | FB_Typed [0.0.1 InWork] | 0 | 400/0 | 0.3 s |
| L06_fb_type_released | ok | 10 | – | 1 | 1 | 1 | FB_Typed [0.0.1 Committed*] | 13 | 400/0 | 0.3 s |
| L06_fb_type_released.zap21 | ok | 10 | – | 1 | 1 | 1 | FB_Typed [0.0.1 Committed*] | 13 | 400/0 | 0.2 s |
| L07_fc_type | ok | 19 | – | 2 | 2 | 2 | FB_Typed [0.0.1 Committed*], FC_Typed [2.3.4 Committed*] | 27 | 400/0 | 0.2 s |
| L07_fc_type.zap21 | ok | 19 | – | 2 | 2 | 2 | FB_Typed [0.0.1 Committed*], FC_Typed [2.3.4 Committed*] | 27 | 400/0 | 0.2 s |
| L09_type_depends | ok | 20 | – | 2 | 2 | 2 | FB_Motor [1.2.3 Committed*], UDT_Motor [0.0.1 Committed*] | 24 | 400/0 | 0.2 s |
| L09_type_depends.zap21 | ok | 20 | – | 2 | 2 | 2 | FB_Motor [1.2.3 Committed*], UDT_Motor [0.0.1 Committed*] | 24 | 400/0 | 0.2 s |
| L10_mc_fc | ok | 14 | FC_Scl (CodeBlockData FC_Scl) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L10_mc_fc.zap21 | ok | 14 | FC_Scl (CodeBlockData FC_Scl) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L11_mc_fb_udt | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| L11_mc_fb_udt.zap21 | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| L12_mc_two | ok | 25 | FC_Iface (CodeBlockData FC_Iface), FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L12_mc_two.zap21 | ok | 25 | FC_Iface (CodeBlockData FC_Iface), FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L13_mc_instantiate | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L13_mc_instantiate.zap21 | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| L14_mc_delete | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| L14_mc_delete.zap21 | ok | 14 | FB_Iface (CodeBlockData FB_Iface) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L15_mc_db | ok | 14 | DB_Motor (DataBlockData DB_Motor) | 0 | 0 | 0 | – | 0 | 400/0 | 0.2 s |
| L15_mc_db.zap21 | ok | 14 | DB_Motor (DataBlockData DB_Motor) | 0 | 0 | 0 | – | 0 | 400/0 | 0.3 s |
| L16_inst_two_devices | ok | 17 | – | 1 | 1 | 2 | UDT_Motor [0.0.1 Committed*] | 11 | 400/0 | 0.3 s |
| L16_inst_two_devices.zap21 | ok | 17 | – | 1 | 1 | 2 | UDT_Motor [0.0.1 Committed*] | 11 | 400/0 | 0.3 s |
| L17_inst_outdated | ok | 23 | – | 1 | 2 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*] | 23 | 400/0 | 0.3 s |
| L17_inst_outdated.zap21 | ok | 23 | – | 1 | 2 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*] | 23 | 400/0 | 0.3 s |
| L18_inst_edit_outdated | ok | 26 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed, 1.1.0 Committed*] | 35 | 400/0 | 0.3 s |
| L18_inst_edit_outdated.zap21 | ok | 26 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed, 1.1.0 Committed*] | 35 | 400/0 | 0.3 s |
| L19_version_discarded | ok | 24 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed, 1.1.0 Committed*] | 35 | 400/0 | 0.3 s |
| L19_version_discarded.zap21 | ok | 24 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed, 1.1.0 Committed*] | 35 | 400/0 | 0.3 s |
| L20_default_two_devices | ok | 24 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*, 1.1.0 Committed] | 35 | 400/0 | 0.4 s |
| L20_default_two_devices.zap21 | ok | 24 | – | 1 | 3 | 2 | UDT_Motor [0.0.1 Committed, 1.0.0 Committed*, 1.1.0 Committed] | 35 | 400/0 | 0.3 s |
| L21_fb_inst_outdated | ok | 21 | – | 1 | 2 | 2 | FB_Typed [0.0.1 Committed, 1.0.0 Committed*] | 27 | 400/0 | 0.3 s |
| L21_fb_inst_outdated.zap21 | ok | 21 | – | 1 | 2 | 2 | FB_Typed [0.0.1 Committed, 1.0.0 Committed*] | 27 | 400/0 | 0.4 s |

Column “Smoke test”: number of objects to which `lib_type()` and `version()` were applied on a trial basis (without exception, checks missing sets and relations).

## Text output L samples

### L01_udt_type_inwork

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state InWork default None latest None instances 1 (outdated 0)
    version 0.0.1 InWork finalized 0001-01-01T00:00:00Z guid 00fe4bcc-4665-43a8-85d0-9022f202a3bf author factorybyte
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 0.0.1 InWork
Statistics: {"instanceCapableObjects": 3, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 0, "types": 1, "versions": 1, "deletedLibraryObjectsHidden": {}}
```
### L02_udt_type_released

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 0.0.1 latest 0.0.1 instances 1 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 0.0.1 Committed
Statistics: {"instanceCapableObjects": 4, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 11, "masterCopies": 0, "types": 1, "versions": 1, "deletedLibraryObjectsHidden": {}}
```
### L03_udt_type_v2

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 1.0.0 latest 1.0.0 instances 1 (outdated 0)
    version 0.0.1 Committed finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
    version 1.0.0 Committed (default) finalized 2026-10-20T15:30:47.078130Z guid 93de3ab8-1ab4-42c4-978b-90725fc8bc3d author factorybyte
      comment {'en-US': 'L03 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa0618c186-5812-4fd4-9f92-f5ecc416f00c0"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed
Statistics: {"instanceCapableObjects": 5, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 23, "masterCopies": 0, "types": 1, "versions": 2, "deletedLibraryObjectsHidden": {}}
```
### L04_default_changed

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 0.0.1 latest 1.0.0 instances 1 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
    version 1.0.0 Committed finalized 2026-10-20T15:30:47.078130Z guid 93de3ab8-1ab4-42c4-978b-90725fc8bc3d author factorybyte
      comment {'en-US': 'L03 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa0618c186-5812-4fd4-9f92-f5ecc416f00c0"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed (not default 0.0.1)
Statistics: {"instanceCapableObjects": 5, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 23, "masterCopies": 0, "types": 1, "versions": 2, "deletedLibraryObjectsHidden": {}}
```
### L05_fb_type_inwork

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - CodeBlockDataByCopyTypeObject 'FB_Typed'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'FB_Typed' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid db6472d3-4ebc-40a5-886d-41617676e068 state InWork default None latest None instances 1 (outdated 0)
    version 0.0.1 InWork finalized 0001-01-01T00:00:00Z guid 418a7512-4f87-43df-aa75-a4e65b6c9775 author factorybyte
      instance CodeBlockData/FB 'FB_Typed' on PLC_1
Instance CodeBlockData/FB 'FB_Typed' on PLC_1 -> FB_Typed 0.0.1 InWork
Statistics: {"instanceCapableObjects": 3, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 0, "types": 1, "versions": 1, "deletedLibraryObjectsHidden": {}}
```
### L06_fb_type_released

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - CodeBlockDataByCopyTypeObject 'FB_Typed'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'FB_Typed' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid db6472d3-4ebc-40a5-886d-41617676e068 state Committed default 0.0.1 latest 0.0.1 instances 1 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:33:00.295910Z guid 6e3c4c8b-b807-4972-81fc-f6fca0b8a18f author factorybyte
      content copy CodeBlockData/FB 'hg1jahs3gd0jgsa418a7512-4f87-43df-aa75-a4e65b6c97750"0' = 'FB_Typed'
      instance CodeBlockData/FB 'FB_Typed' on PLC_1
Instance CodeBlockData/FB 'FB_Typed' on PLC_1 -> FB_Typed 0.0.1 Committed
Statistics: {"instanceCapableObjects": 4, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 13, "masterCopies": 0, "types": 1, "versions": 1, "deletedLibraryObjectsHidden": {}}
```
### L07_fc_type

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - CodeBlockDataByCopyTypeObject 'FB_Typed'
    - CodeBlockDataByCopyTypeObject 'FC_Typed'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'FB_Typed' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid db6472d3-4ebc-40a5-886d-41617676e068 state Committed default 0.0.1 latest 0.0.1 instances 1 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:33:00.295910Z guid 6e3c4c8b-b807-4972-81fc-f6fca0b8a18f author factorybyte
      content copy CodeBlockData/FB 'hg1jahs3gd0jgsa418a7512-4f87-43df-aa75-a4e65b6c97750"0' = 'FB_Typed'
      instance CodeBlockData/FB 'FB_Typed' on PLC_1
  Type 'FC_Typed' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid be23b613-2c15-4b80-b49a-70183c2a31db state Committed default 2.3.4 latest 2.3.4 instances 1 (outdated 0)
    version 2.3.4 Committed (default) finalized 2026-10-20T15:33:42.340391Z guid 18704781-9938-473d-8e07-34f335049c29 author factorybyte
      comment {'en-US': 'L07 FC release'}
      content copy CodeBlockData/FC 'hg1jahs3gd0jgsaf18300e6-d050-4388-8b03-5c3930bc58bc0"0' = 'FC_Typed'
      instance CodeBlockData/FC 'FC_Typed' on PLC_1
Instance CodeBlockData/FB 'FB_Typed' on PLC_1 -> FB_Typed 0.0.1 Committed
Instance CodeBlockData/FC 'FC_Typed' on PLC_1 -> FC_Typed 2.3.4 Committed
Statistics: {"instanceCapableObjects": 6, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 27, "masterCopies": 0, "types": 2, "versions": 2, "deletedLibraryObjectsHidden": {}}
```
### L09_type_depends

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - CodeBlockDataByCopyTypeObject 'FB_Motor'
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'FB_Motor' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid 9e46e9e1-3956-4dba-8186-2968c98c7d70 state Committed default 1.2.3 latest 1.2.3 instances 1 (outdated 0)
    version 1.2.3 Committed (default) finalized 2026-10-20T15:34:19.044989Z guid df4ceb84-6da5-4cbd-927f-6a0768b0a004 author factorybyte
      content copy CodeBlockData/FB 'hg1jahs3gd0jgsa8cf399cf-c16b-4808-9063-22c4984255f60"0' = 'FB_Motor'
      depends on UDT_Motor 0.0.1 (f7655f76-9ca2-4398-a27e-a4bd9dd32e38)
      instance CodeBlockData/FB 'FB_Motor' on PLC_1
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 0.0.1 latest 0.0.1 instances 1 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
      used by FB_Motor 1.2.3
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance CodeBlockData/FB 'FB_Motor' on PLC_1 -> FB_Motor 1.2.3 Committed
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 0.0.1 Committed
Statistics: {"instanceCapableObjects": 6, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 24, "masterCopies": 0, "types": 2, "versions": 2, "deletedLibraryObjectsHidden": {}}
```
### L10_mc_fc

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FC_Scl'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FC_Scl' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:55:25.809926Z, modified 2026-10-20T15:55:25.809926Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FC roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L10_mc_fc\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FC 'FC_Scl'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references CodeBlockData 'FB_Iface' (name stub)
    references DataBlockData 'FB_Iface_DB' (name stub)
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 26
Statistics: {"instanceCapableObjects": 7, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```
### L11_mc_fb_udt

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FB_Iface'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FB_Iface' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:58:52.870164Z, modified 2026-10-20T15:58:52.870164Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L11_mc_fb_udt\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FB 'FB_Iface'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 27
Statistics: {"instanceCapableObjects": 6, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```
### L12_mc_two

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FB_Iface'
    - LibraryMasterCopyData 'FC_Iface'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FC_Iface' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:59:11.157732Z, modified 2026-10-20T15:59:11.157732Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FC roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L12_mc_two\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FC 'FC_Iface'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 25
  Master copy 'FB_Iface' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:59:11.670761Z, modified 2026-10-20T15:59:11.670761Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L12_mc_two\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FB 'FB_Iface'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 27
Statistics: {"instanceCapableObjects": 9, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 2, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```
### L13_mc_instantiate

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FB_Iface'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FB_Iface' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:58:52.870164Z, modified 2026-10-20T15:58:52.870164Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L11_mc_fb_udt\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FB 'FB_Iface'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 27
Statistics: {"instanceCapableObjects": 10, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```
### L14_mc_delete

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FB_Iface'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FB_Iface' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T15:59:11.670761Z, modified 2026-10-20T15:59:11.670761Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L12_mc_two\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FB 'FB_Iface'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 27
Statistics: {"instanceCapableObjects": 7, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {"LibraryMasterCopyData": 1}}
```
### L15_mc_db

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'DB_Motor'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'DB_Motor' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-20T16:05:18.665767Z, modified 2026-10-20T16:05:18.665767Z
    source type Siemens.Simatic.Lang.Model.Blocks.DataBlockData.DB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\OpenPEData\samples\L15_mc_db\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains DataBlockData/DB 'DB_Motor'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    references UserTypeData 'UDT_Motor' (name stub)
    frozen interface '_BIVE:UDT_Motor/a1e9a937-ba10-4845-9d11-f06e36ada58c'
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 15
Statistics: {"instanceCapableObjects": 4, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```
### L16_inst_two_devices

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 0.0.1 latest 0.0.1 instances 2 (outdated 0)
    version 0.0.1 Committed (default) finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
      instance UserTypeData/UDT 'UDT_Motor' on PLC_2
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 0.0.1 Committed
Instance UserTypeData/UDT 'UDT_Motor' on PLC_2 -> UDT_Motor 0.0.1 Committed
Statistics: {"instanceCapableObjects": 6, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 11, "masterCopies": 0, "types": 1, "versions": 1, "deletedLibraryObjectsHidden": {"UserTypeDataByCopyVersionObject": 1}}
```
### L17_inst_outdated

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 1.0.0 latest 1.0.0 instances 2 (outdated 1)
    version 0.0.1 Committed finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_2
    version 1.0.0 Committed (default) finalized 2026-10-20T16:24:27.581669Z guid 4986ea4d-7f51-4cc0-ad94-c0ab079c6965 author factorybyte
      comment {'en-US': 'L17 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa60db311e-b909-44f8-98d5-0f0c4f7ed3440"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed
Instance UserTypeData/UDT 'UDT_Motor' on PLC_2 -> UDT_Motor 0.0.1 Committed (OUTDATED, latest 1.0.0; not default 1.0.0)
Statistics: {"instanceCapableObjects": 7, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 23, "masterCopies": 0, "types": 1, "versions": 2, "deletedLibraryObjectsHidden": {"UserTypeDataByCopyVersionObject": 1}}
```
### L18_inst_edit_outdated

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 1.1.0 latest 1.1.0 instances 2 (outdated 1)
    version 0.0.1 Committed finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
    version 1.0.0 Committed finalized 2026-10-20T16:24:27.581669Z guid 4986ea4d-7f51-4cc0-ad94-c0ab079c6965 author factorybyte
      comment {'en-US': 'L17 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa60db311e-b909-44f8-98d5-0f0c4f7ed3440"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
    version 1.1.0 Committed (default) finalized 2026-10-20T16:25:52.977869Z guid c8dae256-44bd-461f-a087-1027879164f2 author factorybyte
      comment {'en-US': 'L18 release from PLC_2'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsadb9b348e-a680-4bad-97b2-9af2f9ca80030"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_2
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed (OUTDATED, latest 1.1.0; not default 1.1.0)
Instance UserTypeData/UDT 'UDT_Motor' on PLC_2 -> UDT_Motor 1.1.0 Committed
Statistics: {"instanceCapableObjects": 8, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 35, "masterCopies": 0, "types": 1, "versions": 3, "deletedLibraryObjectsHidden": {"UserTypeDataByCopyVersionObject": 1}}
```
### L19_version_discarded

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 1.1.0 latest 1.1.0 instances 2 (outdated 1)
    version 0.0.1 Committed finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
    version 1.0.0 Committed finalized 2026-10-20T16:24:27.581669Z guid 4986ea4d-7f51-4cc0-ad94-c0ab079c6965 author factorybyte
      comment {'en-US': 'L17 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa60db311e-b909-44f8-98d5-0f0c4f7ed3440"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
    version 1.1.0 Committed (default) finalized 2026-10-20T16:25:52.977869Z guid c8dae256-44bd-461f-a087-1027879164f2 author factorybyte
      comment {'en-US': 'L18 release from PLC_2'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsadb9b348e-a680-4bad-97b2-9af2f9ca80030"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_2
Instance UserTypeData/UDT 'UDT_Motor' on PLC_2 -> UDT_Motor 1.1.0 Committed
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed (OUTDATED, latest 1.1.0; not default 1.1.0)
Statistics: {"instanceCapableObjects": 8, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 35, "masterCopies": 0, "types": 1, "versions": 3, "deletedLibraryObjectsHidden": {"UserTypeDataByCopyVersionObject": 2}}
```
### L20_default_two_devices

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - UserTypeDataByCopyTypeObject 'UDT_Motor'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'UDT_Motor' (UserTypeDataByCopyTypeObject / PlcTypeLibraryType) in LibraryTypeSystemFolder guid b0605f73-475d-4439-a3cd-6d1751576af9 state Committed default 1.0.0 latest 1.1.0 instances 2 (outdated 1)
    version 0.0.1 Committed finalized 2026-10-20T15:29:40.945482Z guid f7655f76-9ca2-4398-a27e-a4bd9dd32e38 author factorybyte
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa00fe4bcc-4665-43a8-85d0-9022f202a3bf0"0' = 'UDT_Motor'
    version 1.0.0 Committed (default) finalized 2026-10-20T16:24:27.581669Z guid 4986ea4d-7f51-4cc0-ad94-c0ab079c6965 author factorybyte
      comment {'en-US': 'L17 release'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsa60db311e-b909-44f8-98d5-0f0c4f7ed3440"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_1
    version 1.1.0 Committed finalized 2026-10-20T16:25:52.977869Z guid c8dae256-44bd-461f-a087-1027879164f2 author factorybyte
      comment {'en-US': 'L18 release from PLC_2'}
      content copy UserTypeData/UDT 'hg1jahs3gd0jgsadb9b348e-a680-4bad-97b2-9af2f9ca80030"0' = 'UDT_Motor'
      instance UserTypeData/UDT 'UDT_Motor' on PLC_2
Instance UserTypeData/UDT 'UDT_Motor' on PLC_1 -> UDT_Motor 1.0.0 Committed (OUTDATED, latest 1.1.0)
Instance UserTypeData/UDT 'UDT_Motor' on PLC_2 -> UDT_Motor 1.1.0 Committed (not default 1.0.0)
Statistics: {"instanceCapableObjects": 8, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 35, "masterCopies": 0, "types": 1, "versions": 3, "deletedLibraryObjectsHidden": {"UserTypeDataByCopyVersionObject": 1}}
```
### L21_fb_inst_outdated

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
    - CodeBlockDataByCopyTypeObject 'FB_Typed'
  [LibraryMasterCopySystemFolder]
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Type 'FB_Typed' (CodeBlockDataByCopyTypeObject / CodeBlockLibraryType) in LibraryTypeSystemFolder guid db6472d3-4ebc-40a5-886d-41617676e068 state Committed default 1.0.0 latest 1.0.0 instances 2 (outdated 1)
    version 0.0.1 Committed finalized 2026-10-20T15:33:00.295910Z guid 6e3c4c8b-b807-4972-81fc-f6fca0b8a18f author factorybyte
      content copy CodeBlockData/FB 'hg1jahs3gd0jgsa418a7512-4f87-43df-aa75-a4e65b6c97750"0' = 'FB_Typed'
      instance CodeBlockData/FB 'FB_Typed' on PLC_2
    version 1.0.0 Committed (default) finalized 2026-10-20T16:28:44.815976Z guid e9197cb2-8470-4ad0-89c4-d761c72c3c9f author factorybyte
      comment {'en-US': 'L21 release'}
      content copy CodeBlockData/FB 'hg1jahs3gd0jgsa8c7ce552-eb58-4524-b026-b67af47e3c0c0"0' = 'FB_Typed'
      instance CodeBlockData/FB 'FB_Typed' on PLC_1
Instance CodeBlockData/FB 'FB_Typed' on PLC_1 -> FB_Typed 1.0.0 Committed
Instance CodeBlockData/FB 'FB_Typed' on PLC_2 -> FB_Typed 0.0.1 Committed (OUTDATED, latest 1.0.0; not default 1.0.0)
Statistics: {"instanceCapableObjects": 7, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 27, "masterCopies": 0, "types": 1, "versions": 2, "deletedLibraryObjectsHidden": {"CodeBlockDataByCopyVersionObject": 1}}
```

## Text output 021_mastercopy

```
Library 'Project library' (ProjectLib)
  [LibraryTypeSystemFolder]
  [LibraryMasterCopySystemFolder]
    - LibraryMasterCopyData 'FB_Empty'
  [LibrarySystemTypeSystemFolder]
  [LibrarySystemMasterCopySystemFolder]
  [CommonDataFolder]
  Master copy 'FB_Empty' in LibraryMasterCopySystemFolder
    subtype IECPL, state Committed, author factorybyte, created 2026-10-19T18:19:33.265831Z, modified 2026-10-19T18:19:33.265831Z
    source type Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB roles SelectedObject
    source scope Persistence=fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\TiaFileFormat\samples\021_mastercopy\System\PEData.plf / Project={135208-1075} / Target={1052700-2067}
    contains CodeBlockData/FB 'FB_Empty'
    context  DeviceData 'PLC_1' via Devices
    context  S7ControllerTargetData 'PLC_1' via Target (of visible parts)
    clipboard object SelectedObject sourceId 0 parent -1 visualPartIndex 0
    scope Project = 1028:0002:433 ProjectData '000_empty'
    scope Target = 101c:0010:813 S7ControllerTargetData 'PLC_1'
    copied objects: 10
Statistics: {"instanceCapableObjects": 3, "libraryObjectsInsideMasterCopies": 0, "objectsInsideVersions": 0, "masterCopies": 1, "types": 0, "versions": 0, "deletedLibraryObjectsHidden": {}}
```

The complete JSON output for 021, L09, L12 and L13 (with record keys, `--keys`) is in `021_library.json`, `L09_library.json`, `L12_library.json` and `L13_library.json`.
