# Hardware and network configuration (Z1.1)

Status 2026-10-24. Evidence: 126_hmi_subnet, 038_comm_hw, 039_fw41, 040_s71200, 036_tech (`.zap21`), 002_cpu_truth (Openness). Plan: ..\PLAN-Z1.1-hardware.md.

## Object types (confirmed)

| Type | Package | Role | Example |
|---|---|---|---|
| `DeviceData` | HwConfiguration.Model | Station | `PLC_1` “S7-1500 station“ |
| `DeviceItemData` | HwConfiguration.Model | Rack, module, submodule, interface, port, IO controller | `Rail_0` (Subtype `S71500.Rack`), `PROFINET interface_1` (`S71500.CPU.Interface.IE`), `IOController_PROFINET` (`Internal.IOController`) |
| `NodeData` | HwConfiguration.Model | Address node of an interface | `X1`, `NodeType` 3 (Ethernet, likely), `NodeID` `E1` |
| `SubnetData` | HwConfiguration.Model | Subnet | `PN/IE_1`, `ConfigObjectTypeName` `SubnetIE` |
| `S7ControllerTargetData`, `NonProgrammableTargetData` | HwConfiguration.Model | Software target of the module | PLC or HMI side |
| `AddressManagerV2Data`, `AddressManagerNGData` | HwConfiguration.AddressV2.Model / Model | Address management | Blob `AddressMap` |
| `HmiConnectionPointData` | HwConfiguration.Model | Endpoint of an HMI connection | Link `Node2Conns` from NodeData |

## Attributes (decoding without problems; confirmed against Openness: slot, order number, firmware, type name, IP address and mask in 002_cpu (device tree + CAx export, test_hardware 34/34) as well as IP and subnet in 126 according to the recipe; other meanings likely)

- `IDeviceItemData`: `PositionNumber` (slot; interfaces 32768, ports 32769 ff., OPC UA 254 as in Openness `device_PLC_1_details.json`), `OrderNumber`, `FwVersion`, `InvariantTypeName`, `DeviceItemType`.
- `IConfigBaseData.ConfigObjectTypeName`/`…Version`: catalog entry of the module (e.g. `S71500\RACK\MDD_6ES5xx-xxxxx-xxxx_1.0`, `HMI\UnifiedRack\UnifiedComfortPanels\MTP700_Unified_ComfortPanel_21.0.1.0`).
- `IPersistentExpandoData` (named parameters): on `NodeData` among others `NodeIPAddress`/`NodeIPSubnetMask`/`NodeIPDefaultRouterAddress` as u32 (3232235521 = 192.168.0.1, 4294967040 = 255.255.255.0), `NodeMacAddress` as u64, `PnNameOfStation`, `NodeGetsAddressAutomatically`; on `SubnetData` `NetDefaultSubnetIE`, `NetLastUsedIPAddress` …; on the CPU `ProtectionLevel`, `EnablePutGetConnections`, `EncryptedPassword1..3`/`…Failsafe`; on the IO controller `PnIoWatchdogFactor`, `PnIrtSyncRole`, `PnStationNumber` …
- Links: DeviceItem `Parent`/`Container` (tree), `AddressManagers`, `Elements` → Target; NodeData `DeviceItem`, `Subnet`, `Target`.

## Tree (confirmed, 002_cpu against the Openness device tree)
Top level = objects with `IDeviceItemData` and `Parent` → `DeviceData` (rack and CPU as siblings, as in Openness); below that via `Parent`, sorted by `PositionNumber`. The CPU itself is the `S7ControllerTargetData` (DeviceItemType 10); an HMI panel has `DeviceItemData` (rack), `HmiApplicationData` and one `NonProgrammableTargetData` per interface card. The node of an interface (`NodeData`) is attached to it via `DeviceItem`; the node name in AML is the `NodeID` (`E1`).

## Parser
`openpedata\hardware.py`: per device `hardware` (catalog entry, parameters with `opennessNames` for the evidenced mappings of the table above, tree with nodes; values that are not stored count as "default according to catalog" and are not output, decision 2026-10-24), at project level `network` (subnets with participants, sync domains, HMI connections with both sides). Test `parser\tests\test_hardware.py`.

## Module parameters (H2, H-HW-01 confirmed; samples 200–204 against 002_cpu, exactly one change each via Openness)

A changed CPU setting appears as a **new expando attribute** (`IPersistentExpandoData`) on exactly one DeviceItem; otherwise the only things that change in the plf are `ModifiedTime`, `ProjectSavePoint` and the blob `IConfigBaseData.PersistentExpandoAttributeFlags` of the same object (grows by one entry per new expando, counter in the header +1). The value is stored as a simple value and equals the Openness value (enumerations as a number). The name differs from the Openness name, and the carrying object is often a submodule that is invisible in Openness (H-HW-03): on the DeviceItem `PLC_1`, Openness combines the parameters of `S71500.CPU`, `CpuCommon` and `CPUexecUnit`.

| Openness (DeviceItem, attribute) | File: object (Subtype) | Expando | Value Openness → file | Sample |
|---|---|---|---|---|
| PLC_1 `CycleMaximumCycleTime` | `CPU exec unit_1` (S71500.CPU.CPUexecUnit) | `MaxCycleConfig` | 200 → 200 (ms) | 200 |
| PLC_1 `ClockMemoryByte` | `PLC_1` (S71500.CPU.CpuCommon) | `UseClockFlags` | True → True | 201 |
| PLC_1 `ClockMemoryByteAddress` | `PLC_1` (CpuCommon) | `ClockFlags_Local` (plus `ClockFlags_CreatedAt`, same value) | 100 → 100 (MB100) | 201 |
| PLC_1 `StartupActionAfterPowerOn` | `CPU exec unit_1` (CPUexecUnit) | `PowerOnAction` | 1 → 1 (default 3 not stored) | 202 |
| PLC_1 `WebserverActivate` | `PLC_1` (S7ControllerTargetData, S71500.CPU) | `WebServerActive` | True → True | 203 |
| PROFINET interface_1 `UseWebServerForInterface` | `PROFINET interface_1` (S71500.CPU.Interface.IE) | `EnableWebServerAccess4IE` | True → True | 203 |
| PLC_1 service `PlcAccessLevelProvider.PlcProtectionAccessLevel` | `PLC_1` (CpuCommon) | `ProtectionLevel` | FullAccess → 1 (default NoAccess not stored; 039_fw41 carries 4, presumably NoAccess) | 204 |

Further findings:
- The clock memory additionally creates 9 PLC tags (`Clock_Byte`, `Clock_10Hz` … `Clock_0.5Hz`, `EAMTZTagData`, Default tag table) and changes `IControllerTarget.MaxUsedLidForRange`.
- **In Openness, not in the file:** In 002 the 8 DeviceItems visible in Openness have 196 attributes (97 writable); the whole tree carries 69 expando values in the plf. Values such as `CycleMaximumCycleTime` 150, `CycleMinimumCycleTime` 1, `CycleCommunicationLoad` 50, `StartupConfigurationTimeout` 60000, all 13 `TimeOfDay*` (e.g. time zone 22) do not occur in the file. With the web server, 11 more attributes appear in Openness (`WebserverDBNumber` 333, `WebserverFragmentDBStartNumber` 334, `WebserverHttpsOnly` True, `WebserverUpdateInterval` 10, `WebserverDefaultHTMLPage` with the project path …), in the file only `WebServerActive`; with the access level, `ProtectionEnablePutGetCommunication` False appears without an expando. TIA computes these values from the catalog or from default rules.
- **Not only deviations:** Some expandos carry default values already in the unchanged 002 (`PnDnsConfigNameResolve` 0 = Openness “Deactivated“, `UseFixSysDiagAlarmIds` True = `UseFixedSystemDiagnosticsAlarmIds`, `EnableAlarmTextsTransmission` True, port/interface flags). The rule therefore is: whatever TIA does not explicitly write on creation is a catalog default and is not in the file.
- Not checked: whether a value reset to the default remains as an expando; whether `ClockMemoryByte` alone (address 0) writes `ClockFlags_Local` = 0.

Test: `parser\tests\test_hardware.py` checks, for 200–204 per folder and archive, each pair of the table: changed → expando = Openness value, unchanged → no expando.

## I/O addresses, HW identifiers, process image (H3; samples 205–209 against Openness `addressList`, `hwIdentifierList`, OB attributes)

**I/O address** = `IAddressAttributes.AM_<n>_AddrData_<m>` on the submodule that carries the address (DI_1/DI_1, not the module), 34 bytes, little endian:

| Offset | Width | Meaning | Evidence |
|---|---|---|---|
| 0 | u32 | Overall length (34) | all |
| 4 | u32 | I/O kind: 2 = input, 4 = output | 205 DI, 209 DQ |
| 8 | u16 | 40 in all samples, meaning open | – |
| 10 | u16 | Length in bits (32) | 205, 209 |
| 18 | u32 (width conjectured) | Process image partition: 0 = automatic update, 1 = PIP 1 | 208 (OB30 `ProcessImagePartNumber` 1, `IOBInfoData.PIP` 65535 → 1) |
| 22 | u32 (width conjectured) | Start address in bits (80 = byte 10) | 206 |

When the start address is changed (206), `IAddressRegistriesLinkAttributes.AssignedAM_1` on the submodule and `IAddresseesLinkAttributes.Addressee_<n>` on the `AddressManagerNGData` also change, in the 5th byte from 0 to 1 (presumably "address set by the user", no evidence).

**HW identifier** (Openness `hwIdentifierList`): `AddressManagerV2Data.AddressMap` of the station. Structure: versioned header (u16 at 30 = length of the version string from 32, `14.0.0.0`), u32 count, then per entry 16 bytes: u16 kind (8 = station, 2 = DeviceItem), u16 0x10, u32 index into the manager's `RegisteredDeviceItems` links, u32 HW identifier, u32 0. 209: 17 entries = the 17 Openness identifiers (station 32, Rail 256, CPU 48, CpuProxy 49, CpuCommon 50, card reader 51, CPUexecUnit 52, display 54, OPC UA 117, PROFINET 64/65/66, virtual interface 135/136, LicenseManager 42, DI_1 257, DQ_1 258). Openness assigns the identifiers of the invisible submodules to the CPU item `PLC_1`. Most items additionally link their system constant via `ConstantTags` (`Local~DI_1`, `IDefaultStrategyData.DefaultValue` 257). The start address is not in the `AddressMap` (206 leaves it unchanged). Also decoded in 039 (with DP and a second PROFINET interface) and 126 (HMI station 256–264).

**Process image partition:** on the submodule (offset 18) and on the OB (`IOBInfoData.PIP`, 65535 = none). The cyclic interrupt OB (207) can only be created via SimaticML, not via SD.

Parser: per item `addresses` (ioType, startAddress, startBit, lengthBits, processImage, raw) and `hwIdentifier` (value, constant), on the station `hwIdentifier`; the text output shows the addresses. Test: test_hardware compares, for 205–209, address, length and kind with `addressList`, the PIP with the OB, and in 209 all 17 HW identifiers with `hwIdentifierList`.

## Networks, IO systems, distributed I/O, connections (H4; samples 210–225 against Openness getAttributes, subnet/IO system lists, connectionList, AML)

- **Node** (`NodeData`, expandos): IP address u32 `NodeIPAddress` (210: 3232235521 → 3232235530 = 192.168.0.10); when set by the user, additionally `NodeGetsAddressAutomatically` True → False and `NodeIPAddresseSetByUser` False → True. PROFINET device name `PnNameOfStation`, automatic name generation `PnPnNoSAutoGenerate` (211: “plc-test“, False). `INodeData.NodeType` 3 = Ethernet, 1 = PROFIBUS; PROFIBUS address `NodePBMpiAddress` (master 2, slave 3), plus `NodePartylineBaudrate` 6 and `NodePartylineHsa` 126 (meaning of the numbers open).
- **Subnet** (212, 221): `SubnetData`, `ICoreAttributes.Subtype` “3“ = Ethernet, “1“ = PROFIBUS, catalog `SubnetIE` / `SubnetPB`; with the Ethernet subnet, TIA creates two `DomainData` (`Sync-Domain_1`, `mrpdomain-1`).
- **IO system / DP master system** (213, 222): `MastersystemData`, Subtype `BusSystem.IOSystem` or `BusSystem.Mastersystem`, catalog `IOSystem_PROFINET` / `Mastersystem_PROFIBUS`, expando `PositionNumber` = number of the system (100 or 1, as in Openness), links `Master` (IO controller `IOController_PROFINET` or PROFIBUS interface of the CM), `Subnet`, `HeadModules` (connected devices). HW identifier: system constant `Local~<Name>` on the master item (257 or 260; not checked against Openness).
- **IO device** (214–217): station `DeviceData` with `HeadDeviceItemData` (IM); when connected to the IO system, `IODeviceModuleData` (Subtype `Internal.IODevice`) hangs below the PROFINET interface of the IM; `PnStationNumber` = Openness device number (1). Connecting creates the constants `IM_1~Proxy`, `~IODevice`, `~Head`, `~PROFINET_interface`, `~Port_1/2`. The I/O of the IO device (217 DI_8) carries its address as in the central case (`AM_1_AddrData_1`, input 0, 8 bits).
- **DP slave** (223–225): `HeadDeviceItemData` `IM_DP` (Subtype `ET200SP.IM.PB`) directly in `HeadModules` of the master system; constants `IM_DP~Head`, `~DPSlave`.
- **Configured connections** (218, 219): `BlockConnectionPointData`, Subtype `S7ConnectionPoint` / `TcpConnectionPoint`, `Parent` = CPU target, `Conn2Nodes` = local node. `ConnId` 9 bytes, the first 8 as u64 little endian = connection ID, which TIA shows in hexadecimal (256 → “100“ = Openness `LocalConnectionId`). TCP: `ConnTcpPortLocal` 2000 = Openness `LocalPort`, `ConnTcpPortRemote` = Int64.MinValue (not set). Unlike module parameters, the connection stores its whole parameter set (20 or 10 expandos), including default values. In 126 the PLC side of the HMI connection also carries a `BlockConnectionPointData` (Subtype `HmiConnectionPoint`).
- **Observation H-HW-04:** (a) With automatic name generation, the stored `PnNameOfStation` is not reliable: IO_1 stores “io device_1“ (name at the time of creation), Openness shows “im_1“ (formed from the current IM name). (b) The head submodule of the IM is called `_1` in the file, Openness shows `IM_1`.

Parser: `network.ioSystems` (name, subtype, catalog, number, subnet, master, devices, HW identifier, parameters), `network.connections` additionally with the configured connections (kind = Subtype, `connectionId`), nodes with `profibusAddress`. Test: test_hardware compares, for 210–225, nodes (name, subnet, IP, mask, manually assigned PROFINET name, automatic mode, PROFIBUS address), subnets (count, participants), IO systems (number, subnet, devices), connections (kind, ID, port) and the device tree of the IO/DP stations.

## Topology and synchronization (H5; samples 226–228 against Openness getDetails of the ports/interfaces, NetworkPort.ConnectedPorts, syncDomainList, AML)

- **Port interconnection** (226, `portConnect` PLC_1 Port_1 ↔ IO_1 Port_1): link `PortToPorts` on both port DeviceItemData, each pointing to the partner. In addition, TIA writes the expandos `PnIrtLineLength` 600, `PnIrtSignalDelayTime` 600, `PnIrtLineDelaySelection` 0, `PnPTPBoundary` 0 on both ports, and on the CPU port additionally `PnIsLeadingPort` True. Openness shows the same cable as `CableLength` “Length100m“, `SignalDelaySelection` “CableLength“ (values of the cable expandos not yet mapped to any Openness attribute). The AML lists the interconnection as `InternalLink` “Link To Port_1“.
- **Send clock** (227, PnSendClock 1 ms → 2 ms on the CPU interface): new expando `PnIoSendClockFactor` 64 on the interface; send clock = factor × 31.25 µs (64 × 31 250 ns = 2 000 000 ns = Openness `PnSendClock`). With the default 1 ms, no factor is in the file (H-HW-01).
- **Sync domain** (228, `HighPerformanceActive` true): `DomainData` Sync-Domain_1, expando `PnIrtSyncDomainExpertMode` False → True; nothing else changed. The domain stores its parameters completely (`PnIrtBandwidthLevel`, `PnIrtMrpdDiagnosis`, `PnIrtSyncDomainFFW`, `PnIsDefaultDomain`, `PnResSyncCyclicRatio`, `PnSyncDomainUuid`), participants via the link `InterfaceSubmodules` (= Openness `DomainParticipants`), subnet via `Subnet`.
- **Not created:** sync role (`SyncRole` on the IoController), RT class IRT (`RtClass` on the IoConnector) and isochronous mode (`IsochronousMode` on the IO device interface, on the address and on the OB) – TiaMcpXE cannot reach IoController/IoConnector (MCP-086). The IM 155-6 PN ST shows no `IsochronousMode` on its interface.

Parser: per item `partnerPorts` and `sendClockNs`, `network.portLinks` (pairs), domains with `subnet` and `participants`. Test: test_hardware compares, for 226–228, the number of partners per port (`ConnectedPorts`), the interconnected pair, the send clock (stored = Openness, otherwise default 1 ms) as well as participants and high performance of the sync domain.

## GSD devices (H6; evidence: sample 229 with vendor GSDML, not in the repo; against Openness getDetails, addressList, hwIdentifierList, nodes, IO system, AML)

- **Storage:** A GSD device is stored like a catalog device (DeviceData, DeviceItemData, NodeData), except that `IConfigBaseData.ConfigObjectTypeName` refers to the GSDML instead of the Siemens catalog: `GSD\<gsdml-file-name>\<kind>_<id>`, version 1.0.0.0. Station `…\DEVICE`, rack `Comp_<DAP-id>_Rack`, head module `HM_<DAP-id>`, modules `Module_<id>`, submodules `Submodule_<id>`, PN interface `Interface_<DAP-id>_<subslot>` (32768), ports `Port_<DAP-id>_<subslot>` (32769, 32770). Subtypes `GSD.Rack.PN`, `GSD.IM.PN`, `GSD.DeviceItem.PN`, `GSD.IM.PN.Interface`, `GSD.IM.PN.Port`. In the id, `_SPA_` stands for a space and `_DOT_` for a dot (further replacements not yet evidenced).
- **Openness type identifier:** `GSD:<FILE NAME IN UPPER CASE>/<kind>/<id>` with the kinds D (station), R, DAP, M, SM; for R, DAP, M and SM it follows unambiguously from the catalog entry (229: all items of these kinds of the four devices). Interface and ports are called `SM/<GSDML-id>` in Openness (e.g. “DIM3 InterfaceSubmodule UHX45 V2.0“, “IDS_P1“); this id is not in the catalog entry, only in the GSDML.
- **GSDML in the project:** TIA stores the GSDML files used, together with images, in `AdditionalFiles\GSD\` of the project folder (229: `gsdml-v2.3-danfoss-fc-20141020.xml`, `gsdml-v2.33-sew-movi-c-controller-uhx25-uhx45-20180423-103544.xml`, three .bmp), with the same file name as in the catalog entry. A parser could read missing names (interface, ports, parameter descriptions) from the accompanying GSDML (not yet implemented).
- **Addresses:** as with catalog modules, `AM_x_AddrData_y` on the carrying submodule, here two per submodule (SEW “Module I/O (10 words)“: input 10, output 2, 160 bits each), equal to Openness addressList, also for the devices that are not networked.
- **Parameters:** only a few expandos on the GSD items (`IsAutocreated`, `IsSharedDeviceCapable`, `SharedIoAssignment`, on the interface `PnIoArUuid`, `PnIoExternalSendClockFactor` …); GSD parameter records (Openness `GetPrmData`) not yet examined.
- **Networked / not networked:** The devices that are not networked (device_3, device_4) have the same items, the same parameters (except `PnIoArUuid`), a node with IP address and the same two `IODevice_PROFINET` items as the networked ones. The only things missing are the link `Subnet` on the node and the inclusion in `HeadModules` of the IO system (Openness: no subnet, no device number). Evidence for H-HW-04: device_3 stores `PnNameOfStation` “sew-eurodrive-uhx45“, Openness shows “sew-eurodrive-uhx45_1“.

- **Samples only local:** Samples with vendor GSDML do not go into the repo (the GSDML files belong to the vendors). They are in `samples/local_gsd/` (excluded via `.gitignore`, not part of the baseline) with `<name>_truth/truth.json`; the generic test `GsdLocalSamplesTests` checks every sample located there against Openness and passes without checks if the folder is empty. The rules above are vendor-independent; the unit tests check the derivation of the type identifier with invented names.
- **Second device (2026-11-08, local):** a PROFINET light curtain from another vendor (DAP with interface and two ports, three modules: one output, two inputs) confirms storage, type identifiers (station `/D`, rack `/R/DAP`, head `/DAP/DAP`, modules `/M/<id>`, submodules `/SM/<id>`, interface and ports via the GSDML), addresses and HW identifiers of the items (47/47 checks). The automatically assigned PROFINET name is the name of the head module (“cml_1“; the stored one was the GSD default name). HW identifiers of station and rack: on creation TIA assigns 256/257; on connecting to the IO system of the CPU they are reassigned (here 266/267), the identifiers of the other items stay. Parser and Openness agree on the saved state (the first query was made before connecting).

Parser: `gsd` (file, kind, id, typeIdentifier) on station and item. Test: test_hardware compares, for 229, the Openness type identifiers (station, R/DAP/M/SM), all addresses per station (addressList), nodes, subnet and IO system.

## Services (H7; samples 230–233, by hand by User starting from 039_fw41, CPU 1516-3 PN/DP V4.1, against Openness getDetails of the CPU and the interfaces)

- **OPC UA server** (230): `OpcUaEnableServer` True on the submodule `OPC UA_1` (Subtype `S71500.CPU.OPC_UA`, not visible in Openness) and `SelectedOpcUaLicense` 2 on the `S7ControllerTargetData` (= Openness `OpcUaPurchasedLicense`). **Port** (231): `OpcUaPortNumber` 4841 on `OPC UA_1`. For server activation and port, Openness offers no attribute on the CPU; the values are only in the file.
- **NTP** (232): on both PROFINET interfaces `TimeSyncRole` 2 (= Openness `TimeSynchronizationNtpV2` on the CPU, on the interface `TimeSynchronizationNtp` True) and `TimeSyncNtpServer1` “192.168.0.100“ (= `TimeSynchronizationServer1`); X2 additionally stores `TimeSyncInterval` 10 (= `TimeSynchronizationUpdateInterval`) and the empty servers 2–4, X1 does not (Openness shows them on both).
- **Time zone** (233, “(UTC +02:00) Beirut“, daylight saving time off): on the CpuCommon `LocalTimeZone` 22 → 82 (= `TimeOfDayLocalTimeZone`; the default 22 was not in the file before) and `EnableDaylightSavingsTime` False (= `TimeOfDayActivateDaylightSavingTime`). At the same time TIA writes the whole daylight saving block (`DaylightSavingsDeltaTime` 60, `DaylightStart*`/`StandardStart*` day, week, month, hour), which Openness no longer shows when daylight saving time is switched off. The stored hours (0/0) differ from the default values previously reported by Openness (1/2); meaning open.

Parser: the confirmed pairs are in `OPENNESS_NAMES` (output `opennessNames`). Test: test_hardware compares, for 230 ff., each mapped stored parameter with the Openness attribute of the CPU or the interface.

### Protection, user management, web server (H7 part 2; samples 234, 236–242 via MCP, chain starting from 233)

- **System diagnostics** (234, `SuppressDeactivatingSystemDiagnosticsAlarms` true): expando of the same name on the `S7ControllerTargetData` = Openness attribute of the CPU; plus `SysDiagLastCompileTime` newly set.
- **Access control** (236, `PlcAccessControlConfiguration` EnabledWithAccessControl → Disabled): on the CpuCommon `AccessControlAtRuntime` 0 (= Disabled), `EnableLegacyAccessControlViaAccessLevel`, `LocalUserConfiguration`, `CentralUserConfiguration` (all False) and the account lock `EnableLockUserAccountAtRunTime` False, `AccountLockedFor` 2, `NoOfFailedLoginAttempts` 5, `TimeBetweenFailedLoginAttempts` 2 (= Openness service `PlcAccountLockingAtRuntimeFeature`: `EnableLockUserAccountAtRunTime`, `LockAccountForDuration`, `NumberOfFailedLoginAttempts`, `LoginRetryInterval`). The default (EnabledWithAccessControl) was not in the file before. The CPU 1516 V4.1 no longer has access levels (`PlcAccessLevelProvider` is missing).
- **Confidential configuration data** (237, `PlcMasterSecretConfigurator.Protect`): on the CpuCommon `IsMasterSecretConfigured` True (= Openness `MasterSecretConfiguration` WithPassword) and `OmsCertificateId` 1; on the `S7ControllerTargetData` a new `PkiContainerCache` (1904 bytes, certificate store with the communication certificate “PLC-1/Communication-1“). The password is not readable in the file.
- **Certificate** (238, `LocalCertificateStore.Certificates.Create`, CN “PLC_1-Test“, ID 2): only `PkiContainerCache` grows (1904 → 2820 bytes). Content of the blob not yet decoded.
- **Web server** (239, `WebserverActivate` true): `WebServerActive` True, `EnableWebServerAccess4IE` True on X1 (as in 203) and `WebServerCertificateId` 3; in doing so TIA itself generates the web server certificate “PLC-1/Webserver-3“ (`PkiContainerCache` 2820 → 4114 bytes).
- **UMAC** (240–242): user `CustomProjectUser` (link `AllUsers` on `UmacRootData`), role `CustomRole` (`AllRoles`, `RoleToDevice` → CPU target, `AssignedDeviceFunctionRight` → `SystemDeviceFunctionRightProxy` “PlcDeviceFunctionRights.WebAllowDiagnostics“), assignment `RoleToUserAssociation` (Parent = user, `AssociationToRole`, `RoleAndUserAssociationToDevice`). System roles are `SystemRoleProxy` with `ExternalId` (PLCAdministrator, PLCFAdministrator, PLCOperator). The `…Encrypted` attributes have the format u8 0, u32 length n, flags (byte 19/20 = 1: data unencrypted), n data bytes from offset 24; in this project name, comment and session timeout (30) are in plain text (UTF-16 or u32). The password field is empty (`PasswordEncrypted` without data, `PasswordHash` empty); where the password is stored is open.
- **Decoder finding:** In an expando block there can be free space after a value if TIA moves a larger value to the end (238: `SelectedOpcUaLicense` read as a 1912-byte blob). Fixed-size values (bool, int, long, double, dateTime, guid) are now truncated to their size (`attrs.FIXED_SIZES`); as a result `ConnId` (218/219, previously 9 bytes) is a long.

Parser: `project.umac` (users with name, alias, timeout, roles per device; roles with name, type, comment, devices, device rights), new pairs in `OPENNESS_NAMES` also for service properties (`Service.Property`). Test: test_hardware compares, for 234 ff., the mapped parameters with Openness attributes or services, the protection of confidential data, users, custom roles, device rights and role assignment.

### Certificates, GSDML names, further modules (rounding off Z1.1, 2026-10-25)

- **Certificates** (237–239, `S7ControllerTargetData.PkiContainerCache`): header like the UMAC fields (u32 length at offset 1, flags), then zlib streams (pages of 4096 bytes, each preceded by u16 compressed length). Unpacked, there are PEM blocks: first a `PUBLIC KEY`, then per certificate a metadata record (varint text subject alternative names, u8 length + name, u32 certificate ID = Openness ID in the `LocalCertificateStore`, u32 0/1/2/0, u16 20 + SHA-1 fingerprint), the `CERTIFICATE` and, with an own key, an `ENCRYPTED PRIVATE KEY` (not output). Parser: per CPU target `certificates` (id, name, SAN, fingerprint, subject, issuer, serial number, notBefore/notAfter, hasPrivateKey); test_hardware compares ID, name, validity (Openness `validUntil` in local time = `notAfter` UTC) and private key with `plcCertificateList`, plus `WebServerCertificateId` with `webserverCertificateGet`.
- **GSD interfaces and ports** (229): The catalog entry `Interface_/Port_<DAP-id>_<subslot>` is resolved via the accompanying GSDML (`AdditionalFiles\GSD`, in the folder and in the .zap21): `DeviceAccessPointItem ID=<DAP-id>` → `InterfaceSubmoduleItem`/`PortSubmoduleItem` with `SubslotNumber` → `ID`; from that the Openness type identifier `GSD:<FILE>/SM/<ID>` (tested for all interfaces and ports of the networked GSD devices).
- **Analog I/O** (243, AI 4xU/I/RTD/TC / AQ 2xU/I ST, 6ES7 534-7QE00-0AB0 V1.0 in slot 4): module `S71500.SM.AIO`, carrying submodule `S71500.SM.AI.4AI2AO` with two addresses (`AM_1_AddrData_1` input 4, 64 bits; `AM_1_AddrData_2` output 4, 32 bits) = Openness addressList. **Value status** (244, `EnableValueStatus` true on the module): submodule expando `QualityInformation` True, the input length grows to 80 bits (Openness likewise).
- **Technology module** (245, TM Count 2x24V, 6ES7 550-1AA01-0AB0 V2.0 in slot 5): input 32, 96 bits; output 24, 64 bits = Openness addressList. TiaMcpXE does not offer channel parameters (counter operating mode etc.) for writing; they remain without a sample.
- **HMI device with interfaces**: contained in 229 (MTP1200 Unified with two PROFINET CPs, ports) and 126 and tested against Openness (device tree, nodes); no separate sample needed.

### Safety (F-CPU, F-I/O, F-program; samples 246–253 via MCP, CPU 1513F-1 PN V2.9, F-DI 16x24VDC)

- **F-CPU** (246): On creation, TIA generates the Safety Administration as separate records: `FailsafeProgramSettings` (link `Target` → `S7ControllerTargetData`; safety system version V2.8, F-block number ranges, options), `RuntimegroupInfo` “F-runtime group 1“ (`MaxCycleTime` 120000, `WarningCycletime` 110000 µs, info DB `RTG1SysInfo`), `FBlockInfo` per F-block (Main_Safety_RTG1, FOB_RTG1 …) and the F-program itself.
- **F-settings** (247, F-change history on): `FailsafeProgramSettings.ActivateFChangelog` True (= Openness `SafetySettings.ActivationOfFChangeHistory`). **Central F-monitoring time** (248, `Failsafe_DefaultFmonitoringTimeCentral` 200): expando `Failsafe_Central_F_WD_Time` on the CPU target.
- **F-I/O** (249, F-DI in slot 2): submodule `S71500.SM.DI.DI16` with F-parameters as expandos, plus `FModuleInfo` (F-I/O DB `F00000_FDI_1` no. 30002, monitoring time, `ModuleCRC`), `FChannelInfo` per channel and the F-I/O DB together with the constant `Local~FDI_1`. **F-destination address** (250, 10): `Failsafe_F_Dest_Add` 10, the CRCs `Failsafe_F_Par_CRC` (= `Failsafe_FParameterSignatureWithAddresses` = `FModuleInfo.ModuleCRC`) change along with it. **Short-circuit test channel 0 off** (251): `Failsafe_Short_Circuit_Test_0` 0, plus `Failsafe_CRC_3` (= `Failsafe_FParameterSignatureIndividualParameters`, stored as int, Openness shows u32) and `Failsafe_F_Par_CRC_WithoutAddresses`.
- **F-runtime group** (252, maximum cycle time 200 ms): `RuntimegroupInfo.MaxCycleTime` 200000 (µs).
- **F-password** (253): `FailsafeProgramSettings.FProgramPasswordPlus` (64 bytes, encrypted) and `FProgramPasswordChanged` True. The parser outputs only `passwordSet`.

Parser: per station `hardware.safety` (programSettings without password, runtimeGroups, fModules), F-pairs in `OPENNESS_NAMES`. Test: test_hardware compares safety system version, F-change history, number ranges, F-password set, F-runtime groups (times, info DB), F-modules (F-I/O DB, number, monitoring time, CRC) and the mapped F-parameters with Openness.

### Multiple PLCs, IP addresses and IP protocol (sample 063_multi_plc, 2026-11-06)

- Four PLCs with seven network interfaces (X1/X2/DP of the 1516-3, 1214C, 1513 with CP 1543-1, 1512SP), three Ethernet subnets and one PROFIBUS subnet: node, IP address, mask, router, subnet and PROFIBUS address match Openness `getAttributes` per interface (test `MultiPlcTests.InterfacesAgainstOpenness`).
- **IP protocol:** expando `NodeIPConfiguration` on the node = Openness `IpProtocolSelection`, evidenced 0 = Project, 1 = Dhcp, 3 = OtherPath (“Set IP address directly at the device”); 2 = UserProgram, 4 = ViaIoController, 5 = None only by enum order. With OtherPath, TIA additionally sets `PnIoIpSuiteViaOtherPath`/`PnPnIpSuiteViaOtherPath` True. If it is not 0, the old address (`NodeIPAddress` …) remains stored, but Openness shows no address; the parser then outputs `ipAddress`/`subnetMask`/`router` empty and `ipProtocolSelection`.
- **PROFINET device name with automatic assignment** (`PnPnNoSAutoGenerate` True): `PnNameOfStation` is an outdated value (063: stored “plc_1.profinet interface_1“, Openness “plc_a.profinet interface_1“; CP “s7-1500/et200mp station_4.cp 1543-1_1“ instead of “plc_c.cp_1“; 1512SP “plc_1“ instead of “plc_d“). Openness forms the name from the current names. Rule (evidenced with 063, 210–228 and a GSD device Leuze CML 730i, which is not in the repo because of the vendor GSDML): the base is the name of the CPU or of the head module (IM, GSD DAP). An Ethernet interface of the base module is called "base" if the module has only one (1512SP “plc_d“, IM “im_1“, GSD “cml_1“; stored were “plc_1“, “io device_1“, “cml730i“), otherwise "base.interface" (the virtual communication interface counts); the interface of another module (CP) is called "base.module" (“plc_c.cp_1“). Everything in lower case. Stations without CPU or head module (HMI) keep the stored name (unchecked). Parser: `profinetDeviceName` according to this rule, the stored value in `profinetDeviceNameStored`. Manually assigned names match (PLC_B). Test: `PnDeviceNameTests` (23 interfaces of samples 210–228) and `MultiPlcTests.InterfacesAgainstOpenness`.

## Channel parameters (samples 254/255, 2026-11-07, against Openness channelGet)

- Channel parameters of a module (DI 32x24VDC: hardware interrupt rising/falling edge per channel) are on the module item
  as sparse arrays in expandos: `EnableRisingEdgeArray`, `EnableFallingEdgeArray`, plus `ProcessAlarm` (per channel
  1 = rising, 2 = falling, 3 = both).
- Format: u32 overall size, u32 20 (header), u32 element count (channels), u32 0, u32 offset of the value part; from 20 the
  index part (u32 size = 8 + 4n, u32 n, n × u32 channel number), at the offset the value part (u32 size, u32 n, n values of
  (size − 8)/n bytes). Only channels that deviate from the default are stored.
- Parser: `{"arrayLength": 32, "entries": {"0": 1, "17": 1}}` (`Hardware.SparseArray`), test
  `HardwareChannelTests.ChannelArraysAgainstOpenness`. Event name and priority of the hardware interrupt (Openness
  `HardwareInterruptRisingEdgeEventName/Priority`) only appear with the interrupt and are not yet mapped.

## Open
- Derive the PROFINET device name with automatic assignment from station/CPU/interface names (see 063).
- H-HW-03: Openness lists only part of the DeviceItems (not `CpuCommon`, `CpuProxy`, `CPUexecUnit`, `LicenseManager`, `Internal.IOController`, virtual interface). Which attribute controls this is open; `DeviceItemType`, `UICapabilities` and `BrowsingParameters` do not separate it.
- Blobs `AdditionalInfos`, `PersistentExpandoAttributeFlags`, `DnsAddressContainer`; in `AM_x_AddrData_y` the field at offset 8 and the widths of the zero fields; `AssignedAM_x`/`Addressee_x`; header of the `AddressMap`. Multiple addresses per submodule (e.g. analog/technology module with inputs and outputs) and distributed I/O (IO device) still without a sample.
