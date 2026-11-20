# PLC tests (Z1.2 against Openness ground truth)

| Sample | Source | Checks | Result |
| --- | --- | --- | --- |
| 041_plc_alarm_fb | zap21 | 3 | OK |
| 042_plc_alarm_idb | zap21 | 17 | OK |
| 043_plc_alarm_text | zap21 | 17 | OK |
| 044_plc_alarm_class | zap21 | 17 | OK |
| 045_plc_textlist | zap21 | 26 | OK |
| 046_plc_textlist_entry | zap21 | 26 | OK |
| 047_plc_watch_table | zap21 | 4 | OK |
| 048_plc_watch_entry | zap21 | 8 | OK |
| 049_plc_unit | zap21 | 9 | OK |
| 050_plc_unit_b | zap21 | 10 | OK |
| 051_plc_unit_relation | zap21 | 10 | OK |
| 052_plc_to_param | zap21 | 195 | OK |
| 053_plc_to_inverse | zap21 | 196 | OK |
| 054_plc_opcua_interface | zap21 | 5 | OK |
| 055_plc_opcua_disabled | zap21 | 5 | OK |
| 056_plc_prodiag_fb | zap21 | 4 | OK |
| 057_plc_prodiag_supervision | zap21 | 10 | OK |
| 058_plc_textlist_range | zap21 | 28 | OK |
| 078_plc_force_entry | zap21 | 2 | OK (partly; force table entries: PlcForceTableTests, checked by the .NET tests) |
| 079_plc_to_hw_connect | zap21 | 1 | OK (partly; TO hardware connection: PlcToConnectionTests, checked by the .NET tests) |
| 081_plc_opcua_nodes | zap21 | 1 | OK (partly; OPC UA server interface nodes: PlcOpcUaNodeTests, checked by the .NET tests) |
| 082_plc_opcua_refns | zap21 | 6 | OK (partly; OPC UA reference namespace content: PlcOpcUaNodeTests, checked by the .NET tests) |
