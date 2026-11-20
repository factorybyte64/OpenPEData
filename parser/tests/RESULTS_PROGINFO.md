# Program info (openpedata.proginfo against TIA screenshots)

| Sample | Checks | Result | Known deviations |
| --- | --- | --- | --- |
| 036_tech | 39 | OK | - |
| 025_fb_idb | 24 | OK | - |
| 006_tag_addr | 7 | OK | PlcTags: TIA 1232 bytes, without compiling the plf has no TagLoadMemory_* values |
| 031_lad_all | 38 | OK | - |
| 040_s71200 | 14 | OK | assign: TIA lists IB0-1, IB64-67, IB1000-1023 (onboard DI, AI, HSC) without marking: hardware addresses that the output does not contain |
| 039_fw41 | 47 | OK | Local data Ack_Alarms: 0 instead of 2; Local data RT_INFO: 16 instead of 18 |