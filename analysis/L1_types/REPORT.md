# L1 – Project library: PLC types with versions

As of: 2026-10-20. Own TIA instance PID 33032 (started via `TiaInstance(acquire)`, lock 3 h). I did not touch the
instances 12860 and 10084. All projects are saved and closed. PID 33032 keeps running, without an
open project. I did not change `parser\` and `tools\`.

## Samples

Recipes are in `samples\MANIFEST.md`, section "Step L1". For each sample there is the folder, the `.zap21`,
`<sample>_truth\` and `<sample>_attrdiff.txt`.

| Sample | Base | Content according to Openness |
|---|---|---|
| L01_udt_type_inwork | 017_udt | UDT_Motor 0.0.1 InWork |
| L02_udt_type_released | L01 | UDT_Motor 0.0.1 Committed, default version |
| L03_udt_type_v2 | L02 | 0.0.1 + 1.0.0 Committed, default 1.0.0, instance on 1.0.0 |
| L04_default_changed | L03 | default 0.0.1, instance stays on 1.0.0 |
| L05_fb_type_inwork | 004_fb | FB_Typed 0.0.1 InWork |
| L06_fb_type_released | L05 | FB_Typed 0.0.1 Committed |
| L07_fc_type | L06 | additionally FC_Typed 2.3.4 Committed |
| L09_type_depends | L02 | FB_Motor 1.2.3 (uses UDT_Motor) + UDT_Motor 0.0.1, both Committed |

Deviations from the plan:

- **L08 is missing.** TiaMcpXE cannot create folders in the project library. `typeCreateFromDocument` always puts the
  type in the root folder.
- **L01 needed two calls.** `CreateFromDocuments` creates the type *and* the instance in the PLC. If the UDT already
  exists, the call aborts ("instance with the same name already exists"). So I first deleted the UDT
  and then created the type.
- **Bridge workaround:** The bridge does not recognize the SD export of a UDT (line `TYPE` alone, name on the next line)
  as a UDT. It then chooses the blocks folder, and TIA reports "block type 'UDT' is not supported". I therefore
  used the original source `tools\sources\UDT_Motor.s7dcl` (`TYPE "UDT_Motor" : STRUCT`). The cause is
  in `DeclaresPlcDataType` in TiaMcpXE, which only checks `"TYPE "` with a space.
- **First release:** Openness assigns 0.0.1, not 1.0.0. I set further releases with `versionNumber`
  (1.0.0, 2.3.4, 1.2.3), so that the encoding can be determined unambiguously.
- **L03 without content change:** The content of the InWork version 0.0.2 could not be changed via MCP. Both
  `documentWrite type` and `documentImport type` with Override report "already exists". I therefore released it
  unchanged as 1.0.0. How a real content change is reflected in the version therefore remains open.

## Findings per question

### 1. Objects and type names (metamodel)

| Object | Type ID (lo:hi in the key) | Metamodel type |
|---|---|---|
| Type (UDT) | 140b:0022 | `UserTypeDataByCopyTypeObject` (Openness: PlcTypeLibraryType) |
| Version (UDT) | 1408:0022 | `UserTypeDataByCopyVersionObject` |
| Type (FB/FC) | 140a:0022 | `CodeBlockDataByCopyTypeObject` (Openness: CodeBlockLibraryType) |
| Version (FB/FC) | 1407:0022 | `CodeBlockDataByCopyVersionObject` |

Both pairs belong to the package Simatic.Lang and inherit from TypeObjectBase or VersionObjectBase (UserTypes). Further
facts:

- The type hangs off the `LibraryTypeSystemFolder` via `AggregatingFolder` and has `Environment` → LibraryData.
- The version also has `Environment` → LibraryData. But it is not in the folder; it is only reachable via the
  type links.

### 2. Links

**Type ↔ version**

- `TypeObjectToVersionObject` (type → every version) and `VersionObjectToTypeObject` (version → type).

**Default version**

- `TypeToDefaultVersion` (type → version) and `DefaultVersionToType` (version → type).
- As long as only one InWork version exists, both links are missing. That matches typesList: `isDefault false`.
- L04 (`typeVersionSetDefault`): only these two links change (plus ModifiedTime), nothing else (attrdiff with
  18 lines).

**Previous/Next**

- Do not occur in any sample, not even with two versions (L03).
- The order results only from the version number.

**Instance link (open point 3 from L5, resolved)**

- The typed block or UDT in the PLC points to the version with `IsInstanceOf`. The version points back to the
  instance with `IsTypeOf`. This only applies to the version the instance is connected to.
- L03: `typeVersionEdit` creates a new version object. After the release as 1.0.0, `IsInstanceOf` and
  `CurrentLibraryInterface` of the instance jump to this object. 0.0.1 loses `IsTypeOf`. This agrees with
  `instancesList version=…`.
- After the release the instance additionally has:
  - `CurrentLibraryInterface` → InterfaceVersionRootData `_BIVE:<Name>/<VersionGuid>`. It is located in the
    LibraryBlockInterfaceContainer `UDT_TypeLibraryPartContainer` or `FB_TypeLibraryPartContainer` below
    `BlockInterfaceContainer`.
  - `BlockToVersionElements` → its own interface root. In the process it is renamed from `BIVE:` to `_BIVE:`.

**Dependency (L09)**

- `DependsOn` goes from version to version (FB_Motor 1.2.3 → UDT_Motor 0.0.1), inverse `DependsOnInverse`.
- There is no `DependsOn` on the type object.
- In addition there is `IDependencyInfo.DependencyIdentifiers` (blob, details under question 4).

**Content of a released version**

- The version has:
  - `VisibleParts` → a copy of the object
  - `Devices` → a shell DeviceData PLC_1 (`IsShell=True`) with S7ControllerTargetData
  - folders, loadables, IdentContainer, comment part and DataBag, all with `Environment` → version
  - `Categories`, `RelatedPackage` → S7PSW
- This is the same pattern as with the master copy from L0, only with `Environment` → version instead of → LibraryMasterCopyData.
- Size: 11 to 14 copied objects per version (UDT 11–12, FB 13, FC 14).
- The copy carries the generated name `hg1jahs3gd0jgsa<GUID>0"0`. The real name is in
  `IExpandoInformationData.CustomerTypePartName`.
- **An InWork version has no copy.** Its content is the instance in the PLC.

### 3. Attributes

**Version: `IVersionInfo`**

- `Version`: encoding under question 4.
- `VersionGuid`: **is reassigned on release.** In L01 (InWork) it is `00fe4bcc-…`, in L02 (Committed)
  `f7655f76-…`. The GUID from the InWork time afterwards still sits in the name of the content copy.
- `FinalizationTime`: `0001-01-01T00:00:00Z` while InWork, afterwards the release time. It is identical to
  `modified` from typesList (to the microsecond).
- `MinimumTargetDeviceVersion`: empty.

**Version: further attributes**

- **`ICoreLibObjectAttributes.State`** = `InWork` / `Committed`. This is the location of InWork/Committed (open point 1
  from L5 or H-L0-2). `FinalizationTime` goes hand in hand with it.
- `ICoreLibObjectAttributes.OriginalLibrary` = project name (`000_empty`).
- `ICoreAttributes.Author` = author from typesList. `ICoreAttributes.Comment` = comment of the release, multilingual
  ("L03 release", "L07 FC release"). It is also in the `typeExport` under `Comment`.

**Type**

- `ITypeInfo.TypeGuid` corresponds to `guid` from typesList.
- `ITypeBaseAttributesInfo`: IsObsolete False, IsSetAsNonUpdateable False, OriginalTypeGuid = null GUID,
  TypeNamespace empty.
- `ICoreAttributes.Name` and `Author` are set. **The type has no state.**
- **`ICoreLibTypeAttributes` (Major/Minor/SubminorVersion) is missing on user types.** It is only set on the
  system types (see side finding).

**Instance (Openness)**

- After the release the instance reports `LibraryConformanceStatus` "library-conformant", before that "n.a.".
- For a released FB, HeaderVersion/-Author/-Family/-Name are no longer writable.
- The release of an FB compiles it (CompileDate set, loadables are created).

### 4. VersionData encoding (open point 2 from L5, resolved)

`IVersionInfo.Version` is 19 bytes long:

- 3 bytes prefix `00 00 00`
- then 4 × i32 **little endian** in declaration order Build, Major, Minor, Revision
- Revision = -1 (`ff ff ff ff`) means "not set", as with System.Version with three parts
- Text = Major.Minor.Build

| Version | Bytes |
|---|---|
| 0.0.1 | `000000 01000000 00000000 00000000 ffffffff` |
| 1.0.0 | `000000 00000000 01000000 00000000 ffffffff` |
| 1.2.3 | `000000 03000000 01000000 02000000 ffffffff` |
| 2.3.4 | `000000 04000000 02000000 03000000 ffffffff` |

**`IDependencyInfo.DependencyIdentifiers`** (only partially understood): 24-byte blob header, then u32 total length
(0x77) and u32 count (1). Each entry contains:

- the type name (`UDT_Motor`)
- the VersionGuid of the used version, 16 bytes in .NET order (`765f65f7a29c9843…` = `f7655f76-9ca2-4398-…`)
- the name of its content copy

library.py reads count, GUIDs and strings from it heuristically. The `DependsOn` link is authoritative.

### 5. Subfolders (open point 6 from L5)

- **User folders** in the types library: cannot be created via MCP (L08), so unchecked.
- **Side finding:** The samples 030–032 contain system types, and on them navigation via `Folders`/`FolderParent`
  is confirmed:
  - The system types are `IECPLInstructionData`/`IECPLLibraryTypeData` (CoreLibraryType), e.g. TON, TP, LEN, IEC_TIMER.
    TIA creates them in the `LibrarySystemTypeSystemFolder` when an instruction is used. There are 38 and 77 of them respectively.
  - The subfolders carry GUIDs as names (Subtype `LibrarySystemTypeSystemFolder.Subfolder`) and have two levels.
  - The system types have `ICoreLibTypeAttributes` (e.g. TP 1.0.3, State `InWork`), `DependsOn` between the types
    (TP → IEC_TIMER, IEC_LTIMER) and `VisibleParts` (IECPLSequence, CodeBlockData SFB).
  - They have no type versions.

## New hypotheses

- **H-L1-1:** Release = copy. Only `typeVersionRelease` freezes the content as a copy below the version (subtree
  with `Environment` → version, shell device as with master copies). So to write a released version one has to
  create the same subtree as for a master copy (H-L0-4) plus the interface root
  `_BIVE:<Name>/<VersionGuid>` in the TypeLibraryPartContainer.
  - Evidence: L02, L06, L07 and L09 have 11 to 14 new objects per version with `Environment` → version, L01 and L05 none.
- **H-L1-2:** The VersionGuid is reassigned on release, and the copy keeps the InWork GUID in its name.
  - Evidence: L01 → L02 as well as the copy names in L03, L07 and L09.
  - Open: whether the old GUID is referenced anywhere else.
- **H-L1-3:** `typeVersionEdit` creates a new version object that receives its number on release. The old
  version stays unchanged and Committed.
  - Evidence: In L03 the object 1408:0022:1c30 is new. The old version 1426 only changes links and ModifiedTime.
- **H-L1-4:** The type state in Openness ("InWork" or type has a version in work) is derived and not
  stored. library.py outputs the state of the type as "InWork if a version is InWork".

## Corrections to `analysis\L5_library\library.py`

- `decode_version_data`: 19 bytes (prefix + 4×i32 LE), Revision -1 = not set. Returns text, hex and parts.
  Before: only 16 bytes, which is why the version was always `None`.
- Version:
  - new fields `versionParts`, `instances` (via IsTypeOf/IsInstanceOf), `usedBy`, `context` (shell device),
    `copiedObjects`
  - `dependsOn` as version reference (type name, version, GUID, State)
  - `dependencyIdentifiers` (heuristic) and raw data
  - `contents` with `customerName`
- Type:
  - `state` is derived from the versions. The old field `ICoreLibTypeAttributes` is now called
    `coreLibVersion`/`coreLibState`.
  - `defaultVersion` is now the version number instead of the (empty) name. The versions are sorted by number.
  - new fields `opennessKind` (PlcTypeLibraryType / CodeBlockLibraryType), `category` (user/system) and
    `isSetAsNonUpdateable`
- Instances:
  - only objects with `IsInstanceOf` → version or `IsTypeOf` ← version
  - Copies with `Environment` → version or master copy are excluded. Before, the content copy would have appeared
    as an "instance" via `VisibleParts`.
  - output with device and linked version
- Statistics: new field `objectsInsideVersions`.
- Text output and docstring are adapted to the findings.
- `run_tests.py`:
  - for samples with `_truth\typesList.json`, compares types, GUID, kind, folder, versions, State, isDefault, author
    and FinalizationTime with typesList, the instances with `instancesList` (also per version) and the dependency in
    L09
  - allows system types in the LibrarySystemTypeSystemFolder
  - writes the text output of the L samples to `RESULTS.md` and `L09_library.json`

## Test result

`python analysis\L5_library\run_tests.py` ran over **105 samples** (folders and .zap21, including the new
samples 030–032 and 107–109 from other steps): **all expectations met, no exception, 0 hash errors**
(details in `analysis\L5_library\RESULTS.md`).

The L samples agree with typesList and instancesList in all 9 to 18 compared values per sample:

- name, GUID, kind
- versions 0.0.1 / 1.0.0 / 1.2.3 / 2.3.4
- InWork/Committed, default version, author, FinalizationTime = modified
- instance per version

021_mastercopy returns the master copy FB_Empty unchanged.

## Open

- Folders in the types library (L08). This needs a manual recipe or a new MCP operation.
- An InWork version with actually changed content next to a Committed version (the content could not be
  changed via MCP).
- The field structure of `DependencyIdentifiers` in detail and the 3-byte prefix of VersionData. It was 0 in all
  samples.
- HMI types, global libraries and `ByRef` variants (`…ByRefTypeObject`) do not occur.
- Bridge bug `DeclaresPlcDataType` (see above): report to TiaMcpXE. I only read it, did not change it.

## Files

- `analysis\L1_types\`: `REPORT.md`, `dump_versions.py`, `dump_versions_out.txt` (all type and version objects
  of the L samples with attributes and links)
- `analysis\L5_library\`: `library.py`, `run_tests.py`, `RESULTS.md`, `021_library.json`, `L09_library.json`
- `samples\`: `L01`–`L07` and `L09`, each with folder, `.zap21`, `_truth\` and `_attrdiff.txt`; plus `MANIFEST.md`
