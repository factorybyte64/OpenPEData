# OPC UA server interfaces and ProDiag (Z1.2 P6/P7; samples 054–057)

As of 2026-10-25. Truth: `TiaPlcSoftware(opcUaInterfaceList)`, `supervisionExport` (xlsx). Samples via MCP from 003_tagtable.

## OPC UA server interface

| Sample | Change | Finding |
|---|---|---|
| 054_plc_opcua_interface | Create server interface SI_Test | `OpcUaServerConfigurationData` "SI_Test" (Subtype `OpcUa.ServerConfiguration`, link `OpcUaServerConfigToParent` → OpcUaServerFolderData), plus `HwcnCommentTextCategoryData` on the CPU target |
| 055_plc_opcua_disabled | Disable interface (`Enabled` false) | `IOpcUaServerConfigurationData.IncludeForDownload` False (missing = active) |

The content (nodes) of a server interface comes via a nodeset import; an empty interface cannot be exported ("Interface is empty").

| 081_plc_opcua_nodes | Nodeset with object `Machine` and the variables `Speed` (Int16, read/write) and `Running` (Boolean, read) via `opcUaInterfaceImport` into SI_Test, then in the editor "Generate local data" ( via VNC, new global DB `SI_Test_Data`) | Expando `IOpcUaExpandoData.ServerInterfaceFile`: varint length, then a compressed blob = the imported nodeset byte for byte. The mapping to PLC tags is stored separately in `ServerInterfaceXmlExtensions` (varint length, compressed blob): u32 1, u32 count, per entry 8 bytes, length-prefixed "namespace URI;i=number", u32, length-prefixed the PLC tag (`"SI_Test_Data"."SI_Test.Machine.Speed"`). The Openness export combines both: on each mapped node `<Extensions><Extension><si:VariableMapping>` (namespace `http://www.siemens.com/OPCUA/2017/SimaticNodeSetExtensions`). |

The editor column "Local data" does not accept pasted text; the mapping is created only by dragging or via "Generate local data".

## ProDiag

| Sample | Change | Finding |
|---|---|---|
| 056_plc_prodiag_fb | Create ProDiag FB (`CreateFB`, language ProDiag) | FB plus `SupervisionContainerData` (link `ParentSupervisionFB`), settings (`SupervisionSettingData` per kind), supervision types, system data types (PC_Flags_V2, SV_State_V2 …) |
| 057_plc_prodiag_supervision | Operand supervision "Sup_1" on `"Tag_Bool_1"`, category 1, text "Testueberwachung" (test supervision) (xlsx import) | `ISupervisionContainerData.Data` = zlib XML `<Supervisions>` with `<GlobalTagSupervision>` (Type Operand, Number 1, SupervisedStatus False, CategoryNumber 1, SupervisionName, StateStruct `Tag_Bool_1_O_1`, SpecificTextId 0); the text as `SpecificText` (`FormattedBlockSupervisionSpecificTexts`); the supervised operand as a reference (RefId) into an ident container |

- On import: category as a number (1–8 from the sheet "ProDiag Settings"), an empty trigger yields `False`; the import rejects "Rising edge" and "Error".

Parser: `project.plcOpcUaInterfaces` (name, Subtype, active, author, comment, `nodeset` = text of the nodeset, `nodes` with class, NodeId, BrowseName, DisplayName, description, data type, access level, parent node and `mapping`) and `project.plcSupervisions` (per ProDiag FB the supervisions with kind, type, number, name, trigger, categories, state structure, text) in `openpedata\plc_tables.py`. Test: test_plc compares name and active of the OPC UA interfaces as well as number, name, type, trigger, category and text of the supervisions with Openness.

Test for 081: `PlcOpcUaNodeTests` (NodeId, class, BrowseName, DisplayName, data type, access level and mapping of each node = Openness export).

Reference namespace (sample 082, `opcUaInterfaceCreate` kind reference with a nodeset of ObjectType `PumpType` and variable `Flow`): also `OpcUaServerConfigurationData`, Subtype `OpcUa.ReferenceNamespace`, the nodeset likewise in `ServerInterfaceFile` (Openness export = import). Parser `interfaceKind` server/reference; test `PlcOpcUaNodeTests.ReferenceNamespaceAgainstExport`.

Open: SIMATIC server interfaces, resolving the supervised operand (RefId → ident container), further supervision kinds (interlock, action, reaction, position).
