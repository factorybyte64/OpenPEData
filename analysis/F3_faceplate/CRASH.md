# F02 crash while building faceplates (2026-10-20)

The user built faceplates from the screens directly in `samples\F01_hmi_allobjects`; TIA crashed twice. Saved state: `samples\F02_hmi_faceplates_crash` (1:1 copy at 17:55 UTC, 85 files, source unchanged during copying; TIA PID 26144 had F01 open at that time, so `System\~PEData.1`, `IM\SearchIndex\write.lock` and `000_empty.info` are included).

## Crash (TIA error reports `<shared folder>\*.dr`; excerpt not in the repo)

| Report | Time (local) | PID | Last action |
| --- | --- | --- | --- |
| b2f865ed | 19:45:47 | 44168 (start 19:23:27) | screen FP_Flash opened in the editor (`Application.EditObject FP_Flash`) |
| fed543be | 19:50:53 | 25316 (start 19:47:35) | after restart, FP_Flash opened again |

Both: `ArgumentNullException` in `Dictionary.get_Item` ← `ScreenZOrderService.AddScreenItemToDictionaryIfCanMove` ← `CanMoveUpOneLayer` ← `LayersCommandTarget.UpdateMoveNodesOneLayerUp` (toolbar update "move up one layer", Siemens.Simatic.Hmi.BL.ModernUI 2100.300.4.1).

Cause (finding in the file): X_Rect1 on FP_Flash is the only one of the 47 (F01) or 62 (copy) screen items without `IReferenceExpandoAttributes` with the layer reference (`c130bbc-f35a-4068-aaab-1eba6135e5b0`, present on all others) and without the corresponding layer property in `IPropertyExpandoAttributes`. X_Rect1 was created via TiaMcpXE `exportXml` → extend XML → `importXml` (F01, step 7). The editor looks up the object's layer with key null. Evidence: `crash_fpflash_layer.txt` (X_Rect1, I_Rect1, FP_Flash).

## Project state of the copy

- `PEData.plf` 3 256 367 → 5 859 243 bytes, `PEData.idx` 88 928 → 98 434; `--verify` OK (2478 records, hash chain and signatures ok, idx consistent, xref/searchIndex savepoints equal), two new commit chains (@4530692, @5858689).
- Otherwise only the search index (Lucene segments) and the three session files are new; no savepoint, backup or temp files.

| Faceplate type | Version | State | Content (ILFaceplateTypeData) |
| --- | --- | --- | --- |
| FP_Basic (0002:00d3:2018, TypeGuid f10ac243-…) | 0003:00d3:2019, per TIA command log "V 0.0.1", FinalizationTime 17:49:59 UTC, default version | Committed | 12 basic objects B_Rect1 … B_GraphicView1 (copies) |
| FP_Controls (0002:00d3:207e, TypeGuid cb3ad79d-…) | 0003:00d3:207f, no default version | InWork | C_AlarmControl1, C_Trend1, C_DetParam1, C_Faceplate1 (copy of the typeless container) |

- Per the command log, FP_Elements was created, renamed and discarded (`DiscardVersion`); not saved.
- One hidden device per faceplate version: HmiApplicationData `HiddenHmiRTDeviceFaceplateTypes` with DeviceData/DeviceItemData `FaceplateDevice`, AddressManager, cycles, system tags, fonts, styles, color palette, tag folders, folder `ILFaceplateTypeFolder` (dump `crash_faceplatedevice.txt`).
- No FaceplateInstanceMediatorData, no faceplate instance with a type; the typeless container C_Faceplate1 now exists twice (screen FP_Controls and in faceplate FP_Controls).
- IFaceplateInterface is empty for both types.

## Compile error FP_Controls

The wording of the user's error is stored neither in the shared folder nor in the project copy. Likely cause: `ValidationMessages` (from the F01 run) contains `ContainedType` → `ILFaceplateContainerValidator/Error_NonExistingFaceplateType` for C_Faceplate1; this typeless container was taken over into FP_Controls. In addition, `Error_EmptyTag` for `PlcSource_Name` of the ProDiag overview (not in the faceplate).

Note: the slot naming in the dumps depends on the current state of `parser\openpedata\metamodel.py` (currently changed uncommitted by another thread).
