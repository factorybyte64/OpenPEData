# WinCC Unified: plant view, OPC UA alarm types, system tags (Z1.3 U6; samples 160–163)

As of 2026-10-29. Truth: `TiaHmiUnified` list `plantView`, `plantViewNode`, `systemTag`, `connection` and get `opcUaAlarmType`. Chain from 102_hmi_tag_int, one change per sample via MCP.

## Findings

| Sample | Change | Finding in the store |
|---|---|---|
| 160_hmi_plantview | Plant view `PV_Plant` (`PlantViewComposition.Create`; TIA rejects assignment to a Comfort Panel, stays empty) | new `CpmData` (link `Parent` = ProjectData) → `CpmHierarchyRootChildren` → `CpmHierarchyNodeData` "PV_Plant" (Subtype CpmHierarchyNode, `ICpmHierarchyDataAttributes.IsRoot` True, `AssignedHmiDeviceID` empty, `DisplayedName`) and a `PackageContainerData`. Almost all other records appear changed, but only with different header bytes and shifted slot numbers, because new types (CPM) are registered; nothing changes in content |
| 161_hmi_plantview_node | Node `Area1` under the plant view | `CpmHierarchyNodeData` "Area1" (Subtype HierarchyNode, `ICpmDataBaseAttributes.Index` 0), link `CpmHierarchyNodeParent` ↔ `CpmHierarchyNodeChildren` at the root |
| 162_hmi_opcua_connection | Connection `OPCUA_Conn` with driver "OPC UA" (prerequisite for 163) | `HmiConnectionData` (driver ID `ILRT_OPC_UA`), plus 11 area pointers, 2 internal system tags, 7 system events |
| 163_hmi_opcua_alarmtype | OPC UA alarm type `AT_Test`, NodeId `ns=3;s=MyAlarmType`, connection OPCUA_Conn | `ILOpcUaAlarmTypeData` "AT_Test" (`IOpcUaAlarmTypeAttributeSet` NotifyNodeId = Openness NodeId, ConditionTypeId `ns=0;i=10523`, Area `System/HMI/DriverCommunication`; links to the alarm class "Alarm" and to the connection). TIA additionally creates `ILOpcUaAlarmData` "AT_Test_MultiInstanceAlarm" (`IAlarmAttributeSet.ID` 700000) |

System tags: `HmiSystemTagData` on the HMI (29 on the Comfort Panel). Openness can only list them. In addition there is `HmiInternalSystemTagData` without an Openness view (only counted).

## Parser

- `project.plantViews` (`Hmi.plant_views`): name, `assignedHmiDevice`, nodes with path (separated by "/") and Subtype.
- Per HMI software:
  - `opcUaAlarmTypes`: name, nodeId, conditionTypeId, area, alarmClass, connection;
  - `systemTags`: names.

Test: test_hmi compares plant view and node paths, the OPC UA alarm types with all Openness properties, connection names and system tag names; 160–163 green.

Viewer:

- node "Plant view <name>" at project level, with tab Nodes;
- tab "OPC UA alarm types" on the HMI.

## Plant object type (sample 170, 2026-11-08)

Created via VNC: project tree "Plant objects", context menu of a node "Show plant object types", in the task card "Plant object types" new type, renamed to "Mixer" (for the first type TIA reports "Preparing plant object type definition"). Then via MCP `TiaHmiUnified(create, plantViewNode, name Mixer1, parentPath Area1, plantObjectType Mixer)`.

- The first type creates a hidden HMI module device "PlantModel" (`DeviceData`, Subtype `HMI.ModuleDeviceILCpm`, catalog `HMI\Device\ModuleDevice\ModuleDeviceILCpm`) with its own HMI application (alarm classes, cycles, system tags etc. are duplicated). Openness does not list it as a device.
- Type: `CpmTypeData` "Mixer" (link `Parent` → `CpmData`, `LinkedCpmUdt` → `CpmUdtData` "Mixer" in the tag table "Plant Object Types" of the PlantModel application, `CpmObjects` → its objects).
- Object: node `CpmHierarchyNodeData` "Mixer1" with `LinkedCpmObject` → `CpmObjectData` "Mixer1" (`CpmType` → type, `LinkedStructureTag` → `CpmStructureTagData` "Mixer1" = the interface tag, Openness `interfaceTags`).

Parser: on each node `plantObjectType` and `interfaceTag`, `project.plantObjectTypes` (name, structure type, member count, objects), on the PlantModel device `hiddenModuleDevice`. Test: `HmiPlantObjectTypeTests`.

Not covered: members of a plant object type (type editor) and `plantObjectTag` properties.
