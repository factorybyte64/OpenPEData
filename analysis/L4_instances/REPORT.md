# L4 – Project library: usages and version state of type instances

As of: 2026-10-20. I worked with my own TIA instance: **PID 45856**, newly started via `TiaInstance(acquire)`
(no free instance available), `lock` 4 h. I did not touch the foreign instances 25328, 17892 and 40268.
All projects are saved and closed.

## Samples

Recipes are in `samples\MANIFEST.md`, section "Step L4". For each sample there is the folder, the `.zap21`,
`<sample>_truth\` and `<sample>_attrdiff.txt`.

| Sample | Base | Content according to Openness |
|---|---|---|
| L16_inst_two_devices | L02 | UDT_Motor 0.0.1 (default), instances in PLC_1 and PLC_2 on 0.0.1 |
| L17_inst_outdated | L16 | new version 1.0.0 via PLC_1 (default), PLC_2 stays on 0.0.1 |
| L18_inst_edit_outdated | L17 | outdated instance in PLC_2 edited and released as 1.1.0; PLC_1 now on the older 1.0.0 |
| L19_version_discarded | L18 | InWork version 1.1.1 created via PLC_1 and discarded |
| L20_default_two_devices | L18 | default version 1.1.0 → 1.0.0, instances on 1.0.0 (PLC_1) and 1.1.0 (PLC_2) |
| L21_fb_inst_outdated | L06 | FB_Typed: PLC_1 on 1.0.0 (default), PLC_2 on 0.0.1 (outdated) |

**Deviations from the plan** (missing MCP operations, nothing done by hand in the TIA UI instead):

- **Second instance (L16, L21):** Openness cannot instantiate a type directly into a PLC. For blocks and
  PLC data types there is no `CreateFrom(LibraryTypeVersion)`, only for HMI screens and VB scripts. I therefore used
  `typeCreateFromDocument(addVersionTypeName, device=PLC_2)`. That creates an InWork version 0.0.2 with an
  instance in PLC_2. Then I discarded it via `typeVersionDiscard`, and the PLC_2 instance hangs on 0.0.1.
- **L18 instead of "update":** `UpdateProject` (update instances to another version) is missing in the MCP. My
  attempt in a throwaway copy: `typeVersionEdit` via the PLC_2 instance and then discard. That only restores 0.0.1;
  I discarded the copy. L18 instead shows what happens when **editing and releasing an
  outdated instance**. The instance changes its version in the process (0.0.1 → 1.1.0), with the same object.
- **L19 instead of "version deleted":** A Committed version cannot be deleted via MCP. `LibraryTypeVersion.Delete`
  is missing, and `cleanup` is broken (see MCP part). L19 instead shows the tombstone pattern when **discarding**
  an InWork version.
- **L20/L21 instead of NonUpdateable/Obsolete:** There are no setters for `IsSetAsNonUpdateable`/`IsObsolete`. Replacement:
  - L20: change of the default version with instances in two devices (from the assignment)
  - L21: the same outdated pattern for an FB type
- **Content change of an InWork version** is still not possible. `networkWrite` on the typed FB_Typed
  reports "object with the name 'FB_Typed' already exists", as in L1 for the UDT. All new versions therefore have
  identical content.

## Findings

### 1. Where the version state of an instance is stored

It is stored on the instance itself, in three places (UDT and FB alike, consistent in all Committed instances of all samples, checked by `check_instance_state`):

| Location | Content |
|---|---|
| Link `IsInstanceOf` → version object (inverse `IsTypeOf`) | authoritative, already known from L1 |
| Link `CurrentLibraryInterface` → interface root `_BIVE:<Name>/<VersionGuid>` in the `<UDT/FB/FC>_TypeLibraryPartContainer` | The name contains the VersionGuid of the linked version. The root is shared by all instances of this version **and** by the content copy of the version (`UsedByLibraryBlocks`). |
| Expando `UserTypeVersionGUID` on the instance's **own** interface root (`CurrentInterface`) | VersionGuid of the linked version |

- The version number itself is not stored on the instance, only on the version object (`IVersionInfo.Version`).
- On the type object there is no instance list and no counter. Openness `instancesList` corresponds exactly to the
  `IsTypeOf` links of the version, filtered by `Target` (device).
- **InWork:** The instance only has `IsInstanceOf`. `CurrentLibraryInterface` and the expando are missing (L01, L05).

### 2. "Outdated" is not stored (L17, L21)

- `typeVersionEdit` and `typeVersionRelease` via PLC_1 change **only** the PLC_1 instance, the old and the new
  version object, the type (default link) and the interface. On the PLC_2 object **not a single byte** changes: it is missing in the
  attrdiff L16→L17, and its record is not rewritten.
- The old version loses in the process:
  - `DefaultVersionToType`
  - `IsTypeOf` → PLC_1 instance

  It keeps `IsTypeOf` → PLC_2 instance.
- Openness still reports **`LibraryConformanceStatus` "The object is library-conformant."** for the outdated instance.
  So this attribute does not show the version state.
- "Outdated" is therefore **derived**: the instance hangs on a Committed version, and there is a Committed version
  with a higher number. Separate from that is the question whether the instance hangs on the default version. In L20, PLC_2 hangs
  on 1.1.0, which is newer than the default 1.0.0. PLC_1 is on the default version and still "outdated".

### 3. Release with instances in two devices

- The release only links the instance via which it was edited (L17: PLC_1, L18: PLC_2).
- The **context device of the content copy** is the device of this instance:
  - L18: 1.1.0 has `Devices` → shell DeviceData `PLC_2`
  - 0.0.1 and 1.0.0 have `PLC_1`
- **Editing via an outdated instance (L18):**
  - `typeVersionEdit(version=0.0.1, device=PLC_2)` creates InWork **1.0.1**, i.e. the highest number + 0.0.1 and not
    0.0.2.
  - The instance object stays the same (key 1003:0022:1cfa, same CreationTime).
  - After the release, `IsInstanceOf`, `CurrentLibraryInterface` and the expando `UserTypeVersionGUID` point to
    1.1.0.
  - In addition there is the link `BlockToVersionElements` → own interface root.

### 4. Discarding an InWork version (L16, L19, L21)

`typeVersionDiscard` does not only remove the InWork version object. It **also deletes the instance with its parts and creates
it anew**, linked to the previous version:

- **Tombstones (L19, appended section L18→L19):**
  - the old instance 1003:0022:1420, which had existed since L01, together with IdentContainer, own interface root,
    InterfaceComments, DataBag and LoadablePlusDataTypeData
  - the InWork version object 1408:0022:2848 and its MiscellaneousTextCategoryData
  - a UserTypeData 1003:0022:2850 that was never written live
- **New instance:** new OIDs (284a–284f), CreationTime = time of the discard (agrees with Openness `CreationDate`).
  It has a new interface root `_BIVE:UDT_Motor/0ef5194c…` with expando `UserTypeVersionGUID` of the
  previous version, `CurrentLibraryInterface` → its `_BIVE` root, but **no `BlockToVersionElements`**.
- **L16 and L21:** the same pattern for the PLC_2 instance created via CreateFromDocuments. In L21 the newly
  created FB in PLC_2 gets number 1 (FB1), the original in PLC_1 has FB2.
- **Tombstones for never saved objects:** TIA also writes a tombstone for objects that were created and
  deleted again in the same session (L16: 6 of them, no live record before). As in L2, the .zap21 contains
  no tombstones.

### 5. Default version with two devices (L20)

Only `TypeToDefaultVersion`/`DefaultVersionToType` on type and versions as well as ModifiedTime change. This is
the same pattern as L04, 18 lines of attrdiff. The instances stay unchanged.

### 6. Side findings

- **`_` prefix of the interface root ⇔ `ICoreAttributes.IsConstant = True`:** checked over all sample folders, 1591
  roots, 1009 × (`_`, True) and 582 × (without, False), no exception.
  - A newly created, not yet compiled FB has `BIVE:` (L21 PLC_2).
  - The UDT has `_BIVE:` immediately.
  - This explains the "renaming" from L1: it follows IsConstant.
- **`BlockToVersionElements`** (inverse `VersionElementToBlock`) is only present on instances that were released in place
  (L02/L06 pattern, L18). Instances created by discard or CreateFromDocuments do not have it.
- **InWork number:** first edit 0.0.1 → 0.0.2 (L03, L17). With several versions the highest existing
  number + 0.0.1 applies, independent of the version of the edited instance (L18: from 0.0.1 → 1.0.1, L19: from 1.0.0 →
  1.1.1).

## New hypotheses

| ID | Hypothesis | Evidence |
|---|---|---|
| H-L4-1 | The version state of an instance is only a link: `IsInstanceOf` (+`IsTypeOf`), `CurrentLibraryInterface` → `_BIVE:<Name>/<VersionGuid>`, expando `UserTypeVersionGUID` of the own interface root. "Outdated" is not stored but derived (higher Committed version exists). | L16–L21, all instances consistent; L17 does not change the PLC_2 instance; LibraryConformanceStatus stays "conformant" |
| H-L4-2 | Discard = delete InWork version and instance and **re-instantiate** the instance from the previous version (new OIDs, new interface GUID, without BlockToVersionElements). Presumably `UpdateProject` works the same way (unchecked, MCP missing). | L16, L19, L21 (tombstones, new CreationTime = Openness CreationDate) |
| H-L4-3 | Edit creates InWork number = highest version + 0.0.1. Release only links the edit instance. The context device of the version copy is its device. | L03, L17, L18, L19 |
| H-L4-4 | `_` prefix of an interface root ⇔ IsConstant=True. | 1591/1591 roots of all samples |
| H-L4-5 | Tombstones are also created for objects that were never persisted live (created and deleted before saving). | L16 (6), L19 (3), L21 (12) |

## Corrections to `analysis\L5_library\library.py`

- **New methods:**
  - `instance_versions()`
  - `instance_version_state()`: per instance `versionState` with typeName, version, versionGuid, state,
    defaultVersion, latestVersion (highest Committed version), `usesDefaultVersion`, `newerVersionExists` and
    `outdated` (derived, None for InWork)
  - In addition `interface`: name of the library root, own root, `interfaceVersionGuid` (expando), the
    consistency flags `interfaceVersionGuidMatches`/`libraryInterfaceMatches` and `hasBlockToVersionElements`.
- **Instances:** new field `creationTime`.
- **Version:** `instances` with device.
- **Type:** `latestVersion`, `instanceCount`, `outdatedInstances`.
- **Text output:**
  - type line with latest/instances/outdated
  - instance line with `OUTDATED, latest x` / `not default y` / `INTERFACE VERSION MISMATCH`
  - version instances with device
- Module docstring extended.
- **`run_tests.py`:**
  - `instancesList` key `<device> version <x>`
  - comparison CreationTime ↔ Openness `CreationDate` from `<Name>.<Device>.attributes.json`
  - new check `check_instance_state()`: both version references consistent, InWork without outdated, expected
    version state per instance for L03, L04, L16–L21 (`L4_STATE`) and sum `outdatedInstances`
  - additionally writes `L17_library.json` and `L19_library.json`
- The versions before L4 are here as `library_before_L4.py` and `run_tests_before_L4.py`.

## Test result

`python analysis\L5_library\run_tests.py` ran over **167 samples** (all folders and .zap21): **all expectations
met, no exception, 0 hash errors.**

- L16–L21 (folder and archive): 17 to 26 compared values each against typesList, instancesList per device and version,
  CreationDate and the expected version state.
- L01–L15 and 021 are unchanged ok.

The console output is in `run_tests_out.txt`, the details in `analysis\L5_library\RESULTS.md`.

## L5 integration and parser fixes (after L4)

All commits are local, no push. I only staged targeted paths each time.

| Commit | Content |
|---|---|
| `f3444f4` | **H-020:** `Metamodel.attribute_sets_ordered` sorts ordinally by the qualified set name. `hmi.py` read three expando slots via their old name; they are now called `IPropertyExpandoAttributes` (properties of screen and screen item), `IReferenceExpandoAttributes` (texts) and `IScriptAttributes` (script code). They are the same slots, just correctly named. Objects with decode problems over all sample folders: 3922 → 2109 (`h020_problems.py`). |
| `4e43505` | **H-L2-1:** `attrs.decode_set` reads values of metamodel type Structure/Array via their u32 length (`read_struct`) and returns them including the header. It reads all other values (strings, blobs, XML) unchanged. New is `attrs.decode_structure` as a generic decoder: Structure like a set block, Array with offset table, fixed-size arrays inline, `pe:CharT` 2 bytes. `Metamodel` now knows `MetaType.elements` and `item_type`. Result of `struct_scan.py`: 0 changed non-structure values, objects with set decode problems 2109 → 0, all non-empty values of the 35 structure/array types fully decoded, e.g. LibParameter in 030 with 184 instead of 54 bytes. |
| `bcf43ee` | **L5 integration:** new module `parser\openpedata\library.py`, integrated into `Model.to_dict()["library"]`, and a library text output in `OpenPEData.py` as soon as types or master copies exist. `Metamodel.type_package()` comes from the XML nesting. VersionData and CoreObjectData are now decoded by `decode_structure`; the H-020 warning is gone. New test `parser\tests\test_library.py` (all samples, result `RESULTS_LIBRARY.md`), README extended. |
| (commit with this report) | Samples L16–L21 (folder, .zap21, _truth, attrdiff), `samples\MANIFEST.md` section L4, `analysis\L4_instances\` and `analysis\L5_library\` |

**Test result after the integration** (outputs in `parser_integration\`):

- `parser\tests\test_samples.py`: green, exit 0. The output is identical to the state before the fixes
  (`parser_baseline\`), except for samples that other threads created in the meantime.
- `parser\tests\test_library.py` (new): **179 samples, all expectations met.**
  - L16–L21 have 17–26 compared values each.
  - F03 (faceplate types of the Unified thread) has 50 compared values against typesList, among them doNotUse and
    setForUpdate.
- `parser\tests\test_hmi.py`:
  - With the committed `hmi.py` it is green.
  - With the current working state it is red, namely for the texts of the screen items in 118, 122 and F01. The
    cause are the **uncommitted changes of another thread (U10) in `hmi.py`**, not H-020. I checked
    this by running `test_hmi` with `hmi.py` from HEAD: everything OK.
- `OpenPEData --verify`: L17, L21.zap21 and 030 ok.

**Addendum 20.10. evening:** The updated MCP server now has `TiaProjectLibrary(updateProject)`,
`typeVersionDelete`, `typeDelete`, `typeSet` (doNotUse = obsolete), `folderCreate`/`folderDelete` and
`masterCopyMove`. With these, the cases skipped in L4 can be made up for:

- real update of an outdated instance
- deleted version
- obsolete type
- type or master copy in a folder (L08)

According to the server help, `setForUpdate` (IsSetAsNonUpdateable) is read-only in V21.

## Addendum L4 (2026-10-21, updated MCP server)

Own instance PID 55072. The recipes are in `samples\MANIFEST.md`, "Addendum L4". The number L08 is now
taken, L6 starts at L30.

| Sample | Content |
|---|---|
| L22_update_removes_unused | `updateProject` on an FB that is not called anywhere: TIA deletes the typed FBs in both PLCs |
| L23_inst_called | Instance DBs + call in Main of both PLCs (PLC_2 still outdated on 0.0.1) |
| L24_inst_update_fb | real `updateProject`: PLC_2 0.0.1 → 1.0.0 |
| L25_version_deleted | `typeVersionDelete` 0.0.1 |
| L26_type_obsolete | `typeSet doNotUse=true` |
| L27_inst_update_udt | UDT used in both PLCs via DB_Motor + `updateProject` |
| L08_type_in_folder | Type folder Motors/Pumps with FC_Typed 1.0.0 |
| L28_mc_in_folder | Master copy folder MC_Group/MC_Sub + `masterCopyMove` |
| L29_version_content | **real content change** of a version (Override with additional element `Temp`) |

### Findings

1. **Update = re-instantiation (H-L4-2 confirmed, L24, L27):**
   - The outdated instance is deleted and created anew from the new version. It gets a new key
     (L24: 1001:0022:2038), CreationTime = update time (= Openness CreationDate) and a new, not yet constant
     interface root `BIVE:FB_Typed/<new GUID>`.
   - The old interface root `_BIVE:<Name>/<old GUID>` is deleted (tombstone) and created again as a **new object
     `xBIVE:<Name>/<old GUID>`**: new key, same GUID in the name, IsConstant=True, expando
     `UserTypeVersionGUID` = old version.
   - The new instance points to it with `BlockToVersionElements`. From this one can read from which version it was
     updated (`library.py`: `previousVersion`). The instance DBs are preserved.
2. **`updateProject` deletes unused typed objects (L22):**
   - This applies not only to PLC data types, as the server help says, but also to FBs that are not
     called.
   - The current instance in PLC_1 (on the default version) is deleted as well. The versions remain.
3. **Correction to finding 6 (BlockToVersionElements):**
   - The link is created on **compiling**, not on releasing in place. In L23, compiling (via
     `networkWrite`) makes the PLC_2 FB root constant (`BIVE:` → `_BIVE:`) and sets `BlockToVersionElements`.
   - Prefix rule over all 3302 roots of the samples:
     - without prefix ⇔ IsConstant=False (1236)
     - `_` ⇔ constant (2062)
     - `x` ⇔ constant root of the replaced instance after an update (4)
4. **Deleting a version (L25):**
   - Removed are the version object, its whole content copy (FB, shell device, target, folders, loadables,
     CompileUnit, IdentContainer) and the `_BIVE` root together with InterfaceParts in the FB_TypeLibraryPartContainer.
   - In addition the back links on type, package and culture. The folder has tombstones, the .zap21 has none.
5. **Obsolete (L26):** Only `ITypeBaseAttributesInfo.IsObsolete` changes False → True (typesList `doNotUse`).
   `IsSetAsNonUpdateable` (typesList `setForUpdate` = not IsSetAsNonUpdateable) cannot be set in V21.
6. **Folders (L08, L28):**
   - User folders are `FolderData` with Subtype `LibraryTypeSystemFolder.Subfolder` or
     `LibraryMasterCopySystemFolder.Subfolder` and `IsUndeletable=False`.
   - They hang off the parent folder via `AggregatingFolder` (subfolders and elements are in `AggregatedElements`)
     and have `Environment` → LibraryData.
   - `library.py` returns the paths (`folderPath` Motors/Pumps or MC_Group/MC_Sub) like Openness.
   - `masterCopyMove` creates a new master copy with all copies anew and deletes the old one (tombstones).
7. **Content change (L29):**
   - `typeCreateFromDocument(addVersion, Override)` replaces the InWork instance with a newly created object
     (new key and CreationDate, **new TrackingGuid** of the interface root, because the interface changed).
   - The release freezes the changed copy. typeExport of 1.0.0 contains `Temp`, 0.0.1 does not.

### library.py and tests

- `instance_version_state`: new `previousVersion` as well as `interface.previousInterfaceRoot`/`previousVersionGuid`
  (from the `xBIVE` root). The text output shows "updated from x".
- `test_library.py`:
  - folder paths recursively against typesList/masterCopiesList (`folder_tree`)
  - master copy path "Master copies/…"
  - deleted master copies per sample (`DELETED_MC`)
  - expected version state L08 and L22–L29 (`L4_STATE`)
  - predecessor version after update (`L4_PREVIOUS`)
  - faceplate instances allowed (F samples), interface consistency only for PLC instances
- Result: `test_library` **215 samples green**, `test_samples` and `test_hmi` green (`parser_addendum\`).

## MCP bugs and wishes (for the MCP backlog)

1. **`TiaProjectLibrary(cleanup)` is broken in V21:**
   - The bridge looks for the enum `Siemens.Engineering.CleanupLibraryMode` and the method `Cleanup`. V21 has
     `Siemens.Engineering.Library.Types.CleanUpMode` (DeleteUnusedTypes, PreserveDefaultVersionOfUnusedTypes) and
     `ProjectLibrary.CleanUpLibrary(IEnumerable<ILibraryTypeOrFolderSelection>, CleanUpMode)`.
   - Result: NOT_FOUND "CleanupLibraryMode enum not found in Openness assembly."
   - The default "Obsolete" does not exist either.
2. **Missing: update instances.** Wish: `ProjectLibrary.UpdateProject(selection, scopes[, DeleteUnusedVersionsMode])`
   or `LibraryType.UpdateProject(scope, …)` as `TiaProjectLibrary(updateProject, typeNameList, device…)`.
3. **Missing: delete a single version** (`LibraryTypeVersion.Delete`), e.g. `TiaProjectLibrary(typeVersionDelete,
   typeName, version)`.
4. **Missing: set type flags**, e.g. `TiaProjectLibrary(typeAttributeSet)` for `IsSetAsNonUpdateable` or
   `IsObsolete` (LibraryType.SetAttribute), so that L20/L21 from the plan become possible.
5. **Missing: instantiate a type into a device.** For PLC types Openness can do this only via master copies or documents.
   The route via `typeCreateFromDocument(addVersion, device)` + `typeVersionDiscard` works, but is a
   detour. Wish: a documented recipe operation or a hint in `tia-mcp://docs/library`.
6. **Changing the content of an InWork version does not work:** `networkWrite` on a typed FB (InWork) → IMPORT_FAILED
   "object with the name … already exists in the plc" (like `documentWrite`/`documentImport type` in L1). The
   SD import routes cannot overwrite typed objects.
7. Without `version`, `instancesList` only returns the instances of the default version. A mode "all versions"
   that reports the version per instance is missing. A field `version` per item would simplify the ground truth.

## Files (`analysis\L4_instances\`)

- `REPORT.md` (this report)
- `dump_instances.py` and `dump_instances_out.txt`: types, versions, instances with all version references and
  tombstones for L01–L21
- `appended.py`, `appended_out.txt`, `appended_L16.txt`, `appended_L21.txt`: appended records per step
  (order, tombstones)
- `run_tests_out.txt`: console output of the test run
- `library_before_L4.py`, `run_tests_before_L4.py`: state before L4
