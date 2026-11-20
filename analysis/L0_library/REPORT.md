# L0 – Project library: inventory (types, versions, master copies, Unified faceplates)

As of: 2026-10-19. I only read and analyzed:

- no TIA opened, no MCP call (not even `help`)
- no tracked file changed, no git command run
- All scripts and outputs are in `analysis\L0_library\`. The package `parser\openpedata` is only imported there.

## a) MCP capabilities (from the TiaMcpXE source code)

The complete table (TiaMcpXE-internal) is not in the repo.

| Capability | Operation(s) | Ground truth | Create sample |
|---|---|---|---|
| List types | `TiaProjectLibrary(typesList)`: type GUID, versions, state InWork/Committed, isDefault. `TiaLibraryContent(typesList, typeVersionsList)` also returns the version GUID in the global library | yes | – |
| Create type (UDT/FB/FC) | Detour: create a block with `TiaPlcSoftware(documentWrite)`, export it with `documentExport`, then `TiaProjectLibrary(typeCreateFromDocument, device)`. Also `typeCreateFromVersion` | – | yes (detour) |
| Release / edit version | `typeVersionRelease`, `typeVersionEdit` (PLC type needs an instance), `typeVersionDiscard`, `typeVersionSetDefault`, only for the project library | – | yes |
| Master copies | `masterCopyCreate` (only `sourceKind=PlcCodeBlock`), global `masterCopyCreateFromDevice` (devices only), `masterCopiesList`, `masterCopyInstantiate` | list yes | yes, but only block or device |
| Unified faceplate types (properties, events, screen items, scripts) | **not available**. The server guide says: "Not offered by Openness V21 for WinCC Unified … faceplate and custom control types" (`McpServerInstructions.cs:859-862`). Only faceplate *instances* on screens are covered: `get`/`partGet`/`partSet` on `Interface` | instances only | no |
| HMI data types | not available | – | no |
| Export type version | `typeExport` (LibraryTypeVersion.Export → XML, in the project library only Committed). For the instantiated object additionally SimaticML via `TiaPlcSoftware(export)` and SD via `documentExport` | yes | – |
| Usages / instances | `TiaProjectLibrary(instancesList)` with scope PlcSoftware/HmiTarget; no Unified scope, not available in the global library | yes | – |
| Update / compare / archive | `TiaLibraryContent(update, compare, harmonize)` (compare only coarse), `TiaLibrary(create/open/archive/retrieve)` | partially | yes |

## Instance and client separation of the MCP server (important for L1–L3)

I only read the source code. I verified the key statements myself in the code.

**Result: The server separates neither instances nor clients. Several sessions with different TIA instances on the
same server are not safe.**

- **One process, one state:** The UI app creates exactly one `TiaBridgeService` (`TiaMcpXE.UI\MainWindow.xaml.cs:36`).
  HTTP (`127.0.0.1:47124/mcp`), TCP and stdio all run on this one bridge service.
- **The `Mcp-Session-Id` has no effect:** The server creates it anew on `initialize`
  (`UiMcpHttpServer.cs:208-209`), but does not store it and never evaluates it. There is no per-client state.
- **There is only one global "current" TIA instance:** the field `_tiaPortalInstance` with `_attachedProcessId`
  (`TiaBridgeService.cs:14-24`).
  - `TiaInstance(attach)` and `start` **discard the previous attachment** (Dispose) and replace it
    (`TiaBridgeService.cs:5649-5657`, `:5669-5677`).
  - Only `attach` and `terminate` take a `processId`. No other tool can choose an instance per call.
  - Example: session B does `attach` to PID 2; afterwards all calls of session A run **against PID 2 without
    warning**. A `disconnect` or `terminate` by B also disconnects A.
  - Auto-attach (when exactly one TIA process is running) and re-attach after idle also act globally.
- **Projects have no stored handle:** `path` is resolved on every call in the currently attached instance,
  via path, name or index (`TiaOpennessProjectOperations.cs:301-358`). Without `path` the server takes the
  only open project.
- **Concurrency:** All Openness calls run over one STA thread, strictly one after another
  (`TiaOpennessStaThread.cs`). That prevents COM damage, but calls of different clients interleave.
  The following states are also global:
  - Job store: `TiaJob(list)` shows the jobs of all clients. An identical call from B attaches itself to the job of A
    (`McpJobManager.cs:239-274`).
  - Cancellation IDs: same JSON-RPC ID from two clients → B can cancel A.
  - SimaticML cache, exclusive-access and dialog state.
- **`help` is stateless and safe:** The server answers it in the dispatcher before any validation, any job and
  any bridge call (`McpJsonRpcDispatcher.cs:419-422`, `McpToolRegistry.TryBuildHelp`). `tools/list`,
  `resources/*`, `TiaInstance(info)` and `TiaServer(status)` also only return snapshots. I still made
  no `help` calls, because the source code was sufficient for L0.

**Conclusion for L1–L3 (up to 3 TIA instances):** Safe separation only exists with **one TiaMcpXE process per
session on its own ports**. Each UI instance has its own bridge service; for that the ports (HTTP 47124, TCP) must
be configured differently per instance and entered in the local MCP client configuration of the respective session. Otherwise the
sessions must strictly alternate: before every block a new `attach` to their own PID, and in between nobody else may
attach. Even then a residual risk remains. Simultaneous work of two sessions on one server is only harmless for pure
`help`, `info` and `status`.

## b) Metamodel

Evaluated samples: 000_empty, 002_cpu, 004_fb, 021_mastercopy, 022_db_udt. Details are in `metamodel_library_types.md`
(84 matches with type ID, package, AttributeSets and relations) and `metamodel_packages.txt`.

- **The metamodel grows with the content.** 000_empty has 561 types and 28 packages. From 002_cpu on there are 980 types and 42 packages; added
  are among others Simatic.Lang, Safety, OpcUa and TechTrace. 002, 004, 021 and 022 have a byte-identical
  metamodel XML. So the master copy does not bring a new package.
- **Unified and faceplates do not occur in the metamodel.** Neither "Faceplate", "Unified" nor "Screen" is in the XML.
  "Hmi" only appears in the hardware namespace `Siemens.Simatic.HwConfiguration.Hmi.Model` (HmiTargetData,
  HmiApplicationData, enums). Hypothesis H-L0-1: The Unified and faceplate packages are only loaded once a
  Unified device is in the project. The same pattern shows with the PLC package Simatic.Lang, which is only added
  with a CPU.
- These library packages already exist in the empty project:
  - `ObjectFrame.Core`:
    - CoreLibrary 00011011
    - CoreLibraryObject 00011013 (Parts, VisibleParts, InvisibleParts)
    - CoreLibraryType 00011014 (ICoreLibTypeAttributes Major/Minor/SubminorVersion; DependsOn)
    - CoreLibraryMasterCopy 00011015
    - ICoreLibMasterCopyAttributes 00013014: BinaryData Blob, CoreObjectData/NonCoreObjectData (arrays of
      LibraryClipboard*Object), SharedRawData string
    - Enum CoreLibraryRootType: GlobalLib/SystemLib/ProjectLib
    - Enum LibraryClipboardObjectRole
  - `DomainModel`: LibraryData 00021014 (ILibraryProtectionInfo)
  - `Library.SystemTypes`: LibraryTypeData 00021015
  - `Library.MasterCopies`: LibraryMasterCopyData 00021016 (IMasterCopyCustomAttributes, expando "simple")
  - `Library.UserTypes` (package 0006): core of the type and version model:
    - TypeObjectBase 00061008, abstract. Sets: ITypeInfo.TypeGuid, ITypeBaseAttributesInfo (IsObsolete,
      IsSetAsNonUpdateable, OriginalTypeGuid, TypeNamespace, MinimumTargetDeviceVersion), ITypeProtectionInfo.
      Relations: TypeObjectToVersionObject, TypeToDefaultVersion
    - VersionObjectBase 00061001, abstract. Sets: IVersionInfo (Version, VersionGuid, FinalizationTime,
      MinimumTargetDeviceVersion), IDependencyInfo, ISystemVersionOrderInfo, IVersionManufacturingInfo.
      Relations: Previous/Next, DependsOn, VersionObjectToTypeObject, DefaultVersionToType
    - Variants ByCopy/ByRef for Type, Version and Instance (InstanceObject 00061005)
  - `CommonModel.ReleaseManagement` (0004): PackageContainerData and ProductVersionInfoData. This is
    product/package management, not the release of type versions.
  - `Simatic.Lang` (from CPU on):
    - IECPLLibraryTypeData 00221019, IECPLLibraryTargetBlockData 0022101c, IECPLLibraryTargetData 0022101d
    - LibraryBlockInterfaceRootContainer/-Container
    - IInterfaceLibraryData, Enum InterfaceLibraryFlag
    - TypeInstanceConnectorData (AlarmServices), TextlistLibraryTypeData (SystemDiagnosis), Safety ILibraryBlockInfo
- A state "InTest" or "Released" does not exist as an enum in the metamodel. ICoreLibTypeAttributes.State is
  `removed`. L1 must find the state of a type version in the record itself (hypothesis H-L0-2: FinalizationTime set
  means Committed).
- Objects present: In all samples there is exactly one LibraryData ("Project library", LibraryType=ProjectLib) with
  five system folders: LibrarySystemTypeSystemFolder, LibraryTypeSystemFolder, LibraryMasterCopySystemFolder,
  LibrarySystemMasterCopySystemFolder, CommonDataFolder. Type and version objects (UserTypes) do not exist yet in any
  sample.
- Side finding about the parser (not changed): `Metamodel.packages` is keyed by `id >> 16`. Several packages with
  the same half (e.g. 0x0002: DomainModel, MasterCopies, SystemTypes, TextLists) therefore collapse into one package.
  My scripts resolve the package via the XML.

## c) 021_mastercopy

**Recipe (MANIFEST):** Predecessor is 004_fb, the action `TiaProjectLibrary(masterCopyCreate, device=PLC_1,
blockName=FB_Empty)`. So the master copy is the empty FB `FB_Empty` from PLC_1, stored in the project library.

**Diff to 004_fb** (`mc_021_out.txt`):

- 12 new records:
  - the LibraryMasterCopyData `1016:0002:1018` "FB_Empty" (1741 B)
  - 10 copied domain objects
  - 1 $$COMMIT record `0014:0007:1400`
- Changed are LibraryMasterCopySystemFolder (new AggregatedElements link), PackageContainerData S7PSW
  (RelatedObject), CoreCulture (back links), ProjectData, MddCache, CorePersistenceInfo and store records.
  LibraryData itself is unchanged.

**Attachment:**

```
ProjectData ── LibraryData 'Project library' (1014:0002:443)
                 └─Folders→ FolderData 'LibraryMasterCopySystemFolder' (446)
                              └─AggregatedElements→ LibraryMasterCopyData 'FB_Empty' (1016:0002:1018)
LibraryMasterCopyData: AggregatingFolder→446, Environment→LibraryData, Devices→DeviceData(copy),
                       VisibleParts→CodeBlockData(copy), RelatedPackage→PackageContainerData S7PSW,
                       Editing/Reference/UsedCultures→CoreCulture
```

**The content exists as live objects, not as a blob.** The master copy contains copies of a complete
subtree. Each copy has its own OID and GUID, and its relation `Environment` points to the
LibraryMasterCopyData instead of the project. The copies are:

| Copy | Type | Original in the project | Remark |
|---|---|---|---|
| 100e:0010:1019 | DeviceData PLC_1 | 100e:0010:808 | **IsShell=True**, ParentProject→MasterCopy (584 instead of 872 B) |
| 101c:0010:101b | S7ControllerTargetData PLC_1 | 101c:0010:813 | 25934 instead of 29474 B |
| 100e:0002:101c / :1022 | FolderData ProgramBlocksFolder / ProgramUnitsFolder | 916 / 834 | |
| 1001:0022:101a | CodeBlockData FB_Empty | 1001:0022:c10 | own fingerprint, new timestamps |
| 160c:0022:1020 | InterfaceVersionRootData BIVE:FB_Empty/… | 160c:0022:c12 | same size (946 B) |
| 1016:0022:101d, 1019:0001:1021, 1202:0022:101e, 1607:0022:101f | CompileUnit, ResourceAllocator, IdentContainer, TargetBlockInterfaceContainer | – | |

The selected object (FB) hangs off the master copy via `VisibleParts`. The device context (shell device and
target) is copied along so that the block has a valid environment and target context.

**Attributes of the LibraryMasterCopyData** (`mc_021_detail_out.txt`, raw blocks `021_mc_slot*.bin`):

- `ICoreAttributes`: Name FB_Empty, **Subtype `IECPL`**, author and times.
- `ICoreLibMasterCopyAttributes`:
  - `BinaryData` is a 20-byte blob header without payload (`00…00 10 01 00 00 00 00`). So the blob is **empty**.
  - `NonCoreObjectData` is empty (7 B).
  - `CoreObjectData` is an array with 1 entry (33 B: `00 00 00 01 | 00 00 00 0c | 00 00 00 16 | 00 00 00 01 | … ff ff ff ff …`).
    Likely interpretation: ObjectRole=1 (SelectedObject), ParentObjectId=-1, SourceObjectId=0. The encoding is open.
  - `SharedRawData` is UTF-16-declared XML (clipboard header). Beginning:
    ```
    <SharedRawData><ClipboardHeader formatVersion="1.0.0.0" projectId="af063a50-…"><SourceScopePath>
    <ObjectScope scopeTypeName="Persistence" scopeId="fstore://WIN11-FACTORYBYTE/C:\GitHub\factorybyte\TiaFileFormat\samples\021_mastercopy\System\PEData.plf"/>
    <ObjectScope scopeTypeName="Project" scopeId="{135208-1075}"/><ObjectScope scopeTypeName="Target" scopeId="{1052700-2067}"/>
    </SourceScopePath><TypeDescriptor roles="SelectedObject" typeName="Siemens.Simatic.Lang.Model.Blocks.CodeBlockData.FB" soc="1" eoc="0" poc="0" voc="0"/>
    </ClipboardHeader><ClipboardData formatVersion="1.0.0.0"><SelectedIds>0</SelectedIds><ObjectResolvers>
    <Resolver sid="Siemens.Automation.ObjectFrame.CoreObjectClipboardResolver"/></ObjectResolvers></ClipboardData></SharedRawData>
    ```
    The master copy remembers the source path. The sample was still created under the old folder name. The
    numbers in the scope IDs ({135208-1075} etc.) are unexplained.
- `ICoreLibObjectAttributes`: State `Committed`, IconData = PNG 16×16 behind a 24-byte blob header.

The parser (`model.py:189`) already skips the copies correctly: the output of 021 lists FB_Empty only once.

## New hypotheses

- **H-L0-1:** Unified and faceplate types only appear in the metamodel once a Unified device is in the project. To
  check this, a sample with a Unified panel or PC is needed.
- **H-L0-2:** Types and versions are objects of the types ByCopy/ByRef TypeObjectBase/VersionObjectBase (package
  UserTypes) or their concrete derivations in Simatic.Lang (IECPLLibraryTypeData …). The state InWork/Committed
  is presumably in IVersionInfo.FinalizationTime or ICoreLibObjectAttributes.State.
- **H-L0-3 ("master copy = blob"): refuted for project library master copies in V21.** The content exists as
  live objects with Environment→LibraryMasterCopyData. BinaryData and NonCoreObjectData are empty.
  NonCoreObjectData (XML RawData) is presumably only used for non-CoreObjects, BinaryData possibly for older or
  global libraries. This is open for .al21 libraries.
- **H-L0-4:** A master copy copies the minimal context: a shell device with IsShell=True and the target. So for
  writing, one has to create a whole subtree with new OIDs/GUIDs and redirected Environment links.
- **H-L0-5:** LibraryMasterCopyData.Subtype names the domain (`IECPL` = PLC program). Other contents (device,
  HMI) presumably have other subtypes.

## Recommendation for L1–L3

- **L1 (types and versions UDT/FB/FC) is feasible via MCP**, with the detour via documents: `documentWrite` →
  `documentExport` → `typeCreateFromDocument(device)` → sample (InWork). Then `typeVersionRelease` → sample
  (Committed). Then `typeVersionEdit` → sample (new InWork version) and `typeVersionSetDefault`. Ground truth
  is provided by `typesList`, `instancesList`, `typeExport` (Committed) and globally `typeVersionsList` (version GUIDs).
- **L2 (master copies) is only partially feasible via MCP.** Blocks work via `masterCopyCreate`, devices only in
  the global library (`masterCopyCreateFromDevice`; then `update`/`masterCopyInstantiate` into the project). For
  master copies of a UDT, tag table, folder groups or multiple selection, **manual recipes in TIA** are needed.
- **L3 (Unified faceplates) needs manual recipes.** Openness V21 does not offer faceplate types. User creates type,
  version, properties, events, screen items and scripts by hand. Via MCP afterwards only instances on screens are
  readable (`get`/`partGet`), plus `typesList`/`typeExport` of the library. Before that a base sample with a
  Unified device is needed (H-L0-1). HMI data types are likewise only possible by hand.
- For the ground truth of faceplate types only `typeExport` (XML) remains. Whether it exports faceplate types is not
  recognizable in the code; this should be clarified later via `help` or a trial.

## Generated files (all in `analysis\L0_library\`)

`REPORT.md`, `metamodel_library_types.md`, `metamodel_packages.txt`, `mc_021_out.txt`,
`mc_021_detail_out.txt`, `mc_021_context_out.txt`, `021_mc_slot4_ICoreAttributes.bin`,
`021_mc_slot8_ICoreLibMasterCopyAttributes.bin`, `021_mc_slot9_ICoreLibObjectAttributes.bin`; scripts
`mm_library_types.py`, `mm_packages.py`, `mc_021.py`, `mc_021_detail.py`, `mc_021_context.py`.
