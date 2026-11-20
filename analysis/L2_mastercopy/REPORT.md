# L2 – Project library: master copies

As of: 2026-10-20. I worked with my own TIA instance: **PID 44260**, newly started via `TiaInstance(acquire)`,
lease 3 h.

- The L1 instance 33032 was still running, without a project. But its write lease belonged to the old L1 client until 18:34 UTC,
  which is why `lock` was rejected.
- `terminate` on 33032 (request via the coordinator, according to it from User) was blocked three times by the auto-mode classifier of this
  session. I did not circumvent that.
- At the end `TiaInstance(info)` reported: "TIA Portal process 44260 exited". So 44260 was terminated outside this session,
  after all my projects had been saved and closed. 33032 is also no longer in the
  list. After that I made no more TIA calls, so as not to trigger an auto-attach to foreign instances.
- I did not touch 12860 and 10084. All projects are saved and closed.
- I did not change `parser\` and `tools\`. `library.py` and `run_tests.py` remain in
  `analysis\L5_library\`. I stored the versions before L2 here as `library_before_L2.py` and `run_tests_before_L2.py`.

## Samples

Recipes are in `samples\MANIFEST.md`, section "Step L2". For each sample there is the folder, the `.zap21`,
`<sample>_truth\` (`masterCopiesList.json`, `calls.json`, more for L13) and `<sample>_attrdiff.txt`.

| Sample | Base | Action | Copied objects |
|---|---|---|---|
| L10_mc_fc | 026_fc_scl | MC from FC_Scl (calls `"FB_Iface_DB"`) | 26 |
| L11_mc_fb_udt | 023_fb_iface | MC from FB_Iface (input of type `"UDT_Motor"`) | 27 |
| L12_mc_two | 024_fc_iface | MC FC_Iface, then MC FB_Iface | 25 + 27 |
| L13_mc_instantiate | L11 | Instantiate into PLC_1 and into a new PLC_2 | – |
| L14_mc_delete | L12 | Delete MC FC_Iface | 26 tombstones |
| L15_mc_db | 022_db_udt | MC from global DB DB_Motor (tag of type `"UDT_Motor"`) | 15 |

**Not possible (missing MCP operations, not done by hand instead):**

- Master copy in a subfolder: folders cannot be created (see MCP part). I closed the throwaway copy of 024
  unchanged and deleted it.
- Multiple selection.
- Master copies from UDT, tag table or device: `masterCopyCreate` only knows `PlcCodeBlock`. Devices only work
  globally, and the global library belongs to L6.

## Findings per question

### 1. Structure with different contents

A master copy is **always** the same skeleton of live objects. Every copied object has
`Environment` → LibraryMasterCopyData; L0 already found this for 021. With FB, FC and DB, with and without
dependencies, the skeleton looks like this:

```
LibraryMasterCopyData (Subtype IECPL for FB, FC and DB)
  -AggregatingFolder-> LibraryMasterCopySystemFolder   -Environment-> LibraryData
  -Devices->      DeviceData PLC_1 (IsShell=True, 584 B)  -ParentProject-> MC
                    -DeviceItems-> S7ControllerTargetData PLC_1 (copy, but as a *generic* CPU, see below)
  -VisibleParts-> the selected block (full copy: code, interface, loadables, CompileUnit, IdentContainer …)
  -Categories->   BlockCommentCategoryData (only for blocks with comments: L10–L12, not 021/L15)
  -RelatedPackage-> PackageContainerData S7PSW
  Culture links as before (EditingCulture, ReferenceCulture, UsedCultures)
```

**New: The target copy is a neutral CPU.** `IConfigBaseData.ConfigObjectTypeName` in the copy is
`S71500\UnspecificCPU\CPU_15XX_Unspecific\MDD_6ES75XX_XXXXX_XXXX_2.9` in version 17.0.0.0. The original is
`…CPU_1513_1_PN\MDD_6ES7513-1AL02-0AB0_2.9` in version 20.0.0.0.

- The firmware line (2.9) is preserved, the MLFB is generalized.
- This applies in 021, L10, L11 and L15.
- The shell device keeps Subtype `S71500.Device`.

**The subtree is closed.** No copied object has a link to an object outside the master copy
(checked for all links of all copies in 021 and L10–L15). The only exceptions are null entries `0000:0000:0` in
the typed `UsedParts` lists of the interface roots, and those are exactly the same in the original. Only the master copy itself points outward (folder, library,
culture, package).

**Copy versus original** (`copy_vs_orig_L1x.txt`):

- Unchanged are all domain attributes, the object names and also the GUID in the name of the interface root
  (`BIVE:FB_Iface/64dcd5af…`).
- InterfaceVersionRootData and InterfacePartData are byte-for-byte the same size and have the same TrackingGuid and
  SourceObjectID.
- New are object GUID, OID, CreationTime and ModifiedTime.
- The fingerprint is recalculated (`ICommonFingerprintData.ValidState` 1 → 0, new DataVersion/DataStore).
- For the shell device, IsShell changes from False to True.
- The copied ControllerDataTypeFolder has `IsUndeletable=False` (original True).
- The target copy loses the expando `Siemens.Simatic.Lang.Model.MetaAttributes`.

**OID assignment on creation:** consecutive in a fixed order. Example L10: MC 3058, shell device 3059,
block 305a, target 305b, ProgramBlocksFolder 305c, loadables, CompileUnit, IdentContainer/-Parts, interface,
stubs, … and finally ProgramUnitsFolder or ResourceAllocator.

**Changed shared objects on creation:**

- LibraryMasterCopySystemFolder: new `AggregatedElements` link
- PackageContainerData S7PSW: new `RelatedObject` link
- CoreCulture: back links
- ProjectData and CorePersistenceInfo: only ModifiedTime or header bytes
- LibraryData: unchanged

### 2. Dependencies (called blocks, used UDTs) – L10, L11, L15

**Dependencies are not copied along, but stored as a name stub:**

- **L10:** FC_Scl calls `"FB_Iface_DB"` (instance DB of FB_Iface). For that the master copy contains
  CodeBlockData `FB_Iface` (369 B instead of 3213 B) and DataBlockData `FB_Iface_DB` (377 B).
  - Both only have ICoreAttributes (name, subtype, times).
  - Links: `TRefParent` → copied ProgramBlocksFolder (there inverse `TextualReferences`), `Target`,
    `SWObjectData2TypeOperandIdentContainerData` → IdentContainer of the copy.
  - The IdentContainer points to the stubs with `TypeOperandToProgramObj`.
  - There is neither interface nor code.
- **L11, L12, L15:** The UDT does **not** come along as a UDT. There is a stub `UserTypeData UDT_Motor` (368 B instead of
  1252 B, only ICoreAttributes).
  - It hangs via `TRefParent` off a copied `ControllerDataTypeFolder`. This folder itself hangs via
    `TRefParent` (not via FolderParent) off the target.
  - **In addition** the interface root of the UDT `_BIVE:UDT_Motor/<guid>` is fully copied (1191 B, same name,
    payload, TrackingGuid and SourceObjectID). It points to the stub via `VersionElementToBlock`. The
    interface root of the FB/DB includes it in `UsedParts`.
  - The interface of the copy thus stays resolvable, even without the UDT.
- Openness only reports the selected block in `masterCopiesList` (ContentDescriptions).

**Answer to the question from the assignment:** The UDT does not come along as an object, only as a name and frozen
interface. When instantiating into a device without the UDT, no UDT is created (L13).

### 3. CoreObjectData (Role, ParentObjectId, SourceObjectId)

The basis for the decoding is a **general encoding finding**, hypothesis **H-L2-1**: attribute values of type
Structure or Array* begin with a **u32 LE total length** (including these 4 bytes), exactly like a
set block.

- `attrs.read_var` reads the first byte of it as a varint. That is why the value looks as if it had a
  "3-byte prefix `00 00 00`".
- This also explains the so far open prefix of VersionData from L1: 1 + 3 + 16 = 20 = u32 length.
- **Array:** u32 length, u32 count, count × u32 offset of the element (relative to the start of the value), then the elements.
- **Element (structure):** encoded like a set block. u32 length, fixed part in element order, variable parts
  via u32 offsets relative to the start of the element (0 = null).
- **Evidence:** `struct_len_check.py` over all sample folders, result in `struct_len_check_out.txt`.
  - The rule holds for all 54 combinations of structure type and attribute with values below 128 B. Examples:
    VersionData, ProductVersion, InterfaceCounter, OffsetInfo, Hash256, System.Drawing.Color/Size,
    AttributeFlagsStructure, CoreResourceReference, ArrayOfTemplateParameter.
  - The only "deviations" are values ≥ 128 B. There the length byte is ≥ 0x80, and the parser reads it as a
    multi-byte varint. Example: 030_scl_all, `IECPLInstructionData.LibParameter` is at block offset 0x42 with
    u32 length 0xB8 = 184 B, the next attribute (PLCArray) begins exactly at 0x42 + 184 = 0xFA. The parser instead returns
    54 B from offset 0x44.
  - **Parser bug:** Structures and arrays ≥ 128 B are truncated incorrectly. Affected are
    LibParameter/TemplateParameter of the instruction types (030, 036). I did not fix it because `parser\` was
    off limits. For a proposal see below.

**CoreObjectData** (ArrayLibraryClipboardCoreObject) is byte-for-byte identical in all 8 master copies (021, L10–L15),
33 B after the varint:

```
22 00 00 00   total length 34 (first byte = the parser's "varint")
01 00 00 00   count 1
0c 00 00 00   offset element 0 = 12
16 00 00 00   element length 22
01 00         ObjectRole (i16, enum LibraryClipboardObjectRole) = 1 SelectedObject
00 00 00 00   Option (string, offset 0 = null)
ff ff ff ff   ParentObjectId = -1 (no parent object in the clipboard)
00 00 00 00   SourceObjectId = 0  (= <SelectedIds>0</SelectedIds> in SharedRawData)
00 00 00 00   VisualPartIndex = 0 (position in VisibleParts)
```

- **Not every copied object gets an entry**, only the roots of the clipboard, here the one
  selected block. Device, target, folders and stubs are not in CoreObjectData.
- The counters in the `TypeDescriptor` of SharedRawData (`soc`/`eoc`/`poc`/`voc`) match the roles SelectedObject,
  ExtendedObject, ViewSpecificParent and ViewObject (hypothesis **H-L2-2**). In all samples `soc=1`, the rest 0.
- **NonCoreObjectData** is always an empty array (u32 length 8, count 0).
- **BinaryData** is always the 20-B header of an empty blob.
- More variation only exists with multiple selection or device master copies, and those are not feasible via MCP.

### 4. Scope IDs in SharedRawData (open point 5 from L5, resolved)

Format `{<type ID decimal>-<OID decimal>}` = record key of the **source object**:

| scopeId | Type ID | OID | Object |
|---|---|---|---|
| `{135208-1075}` | 0x00021028 | 0x433 | ProjectData `000_empty` |
| `{1052700-2067}` | 0x0010101C | 0x813 | S7ControllerTargetData `PLC_1` (original, not the copy) |

- `IInterfaceVersionData.SourceObjectID` has the same format. For the UDT `{2232323-4120}` this is 1003:0022:1018, the
  UDT itself. For instantiated blocks it points to the new block itself (L13).
- `Persistence` is the absolute path of the source plf with computer name. It is preserved after copying the project
  (L13/L14 keep pointing to L11/L12).
- `projectId` is a **session GUID**:
  - In L12 both master copies (one TIA session) have the same one.
  - Each sample has a different one, although all are copies of the same project.
  - It does not appear anywhere else in the file.
- `library.py` now resolves the scopes into key, type and name.

### 5. H-020 slot order: **decided: ordinal-alphabetical by qualified AttributeSet name**

LibraryMasterCopyData does not provide a decision:

- The disputed slots 0–3 (IMasterCopyCustomAttributes, IDisplayNaming, IFolderElementAttributes,
  IFolderParentAttributes) are also empty in L10–L15.
- Declaration order: IMasterCopyCustomAttributes first. Alphabetical: as the fourth.

The Unified samples decide the question (`h020_scan.py`, result in `h020_scan_out.txt`):

- The Unified metamodel (from 100_hmi_device on, 1785 types) has **126 types** where declaration and
  alphabetical order differ.
- In the samples 100–113 there are **924 occupied disputed slots**:
  - **alphabetical: 924/924 decoded cleanly**
  - declaration order: 768 with errors or exceptions
  - Largest block: 708 × HmiTag objects, slot 10 = `IHmiInternalSystemTagAttributes` (alphabetical, ok) instead of
    `IHmiHandshakeTagAttributes` (declaration, "nested: block length 13 …").
- **Ordinal, not case-insensitive:** ReportStorageLocationData has `IReportStorageLocationAttributes` before
  `IReportingPropertyExpandoAttributes`. Ordinally 'S' < 'i'. Only this way do slot 3
  (`StorageLocationType`) and slot 4 (`StorageLocationPath`) decode correctly. Case-insensitive gives an exception.
- In the PLC metamodels (000/002/L01) LibraryMasterCopyData is the only deviating type. So nothing changes for all
  previous PLC findings.

Recommendation for the parser (not implemented): `Metamodel.attribute_sets_ordered()` sorts ordinally by the full
name (`sorted(sets)`) instead of by `doc_order`. Possibly this also settles parts of H-023 (short
expando blocks in Unified), because wrongly assigned slots could be read as expando there. This is
unchecked. `library.py` warns (`slotOrderWarning`) if a disputed slot is occupied on a master copy.

### 6. Order of several master copies (L12)

- `LibraryMasterCopySystemFolder.AggregatedElements` has the **creation order** (FC_Iface before FB_Iface).
  Openness `masterCopiesList` returns the same order.
- `library.py` now sorts by this position (`folderIndex`) instead of by name.
- Both master copies are independent of each other. Each has its own set of shell device, target and folders and
  its own UDT stub with its own copy of the UDT interface root. CoreObjectData is the same.

### 7. masterCopyInstantiate (L13)

**PLC_1** (source device, name already exists):

- TIA creates **FB_Iface_1**. The bridge did not apply `newName=FB_Iface_Copy` (MCP bug).
- The block keeps **number 2** (like the original FB_Iface, number conflict) and is `IsConsistent=false`.
- CompileDate, InterfaceModifiedDate etc. come from the master copy, CreationDate is new.
- `LibraryConformanceStatus` is "library-conformant".

**PLC_2** (new, without UDT):

- FB_Iface (FB2), not consistent.
- `TiaPlcSoftware(list type)` is **empty**: the UDT is not created.
- In the file a **UDT stub** is created again (UserTypeData `UDT_Motor`, 368 B, `TRefParent` →
  ControllerDataTypeFolder of PLC_2 with `TextualReferences`) together with the interface root `_BIVE:UDT_Motor/<same GUID>`.
  This is the same pattern as in the master copy.
- Openness does not show the stub. The SD export of FB_Iface from PLC_2 is identical to the original
  (`iMotor : _.UDT_Motor`).

**Instantiated objects:**

- **No origin link:** No new object links to the master copy or its copies, and there is
  no origin attribute. The master copy itself stays unchanged (not in the CHANGED list).
- The interface roots get a **new GUID in the name** (`BIVE:FB_Iface_1/65a17f3d…`, PLC_2:
  `BIVE:FB_Iface/fd78710c…`).
- They get a new TrackingGuid.
- `IInterfaceVersionData.SourceObjectID` = `{2232321-11344}` = 1001:0022:2c50, i.e. the new block itself. In the
  original it was empty.
- In PLC_1, UDT_Motor already exists. It nevertheless gets a **second** interface root
  `_BIVE:UDT_Motor/a1e9a937…` (same name, same payload, new TrackingGuid, no SourceObjectID). The
  existing UDT gets another `BlockToVersionElements` link to it. Two roots with the same name on one
  UDT are therefore legal.

### 8. Deleting (L14)

- `masterCopyDelete` writes **one tombstone each** for the master copy and **all 25 copies** (26 × 49 B, format
  H-018, u32@20 = 3).
- Order in the appended section: first the changed shared objects (CoreCulture, package, folder, LibraryData
  rewritten unchanged, ProjectData, CorePersistenceInfo, stores). Then the copies in OID order starting from the
  ProgramBlocksFolder. Finally MC, shell device, block and target, then ResourceAllocator. After that follow stores
  and $$COMMIT.
- For folder, package and culture the back links are removed.
- **The .zap21 contains no tombstones:** The archive stores the plf compacted, only current versions. This also applies
  to 014, L10 and L12. `deletedLibraryObjectsHidden` is therefore 1 only in the project folder.

## New hypotheses

| ID | Hypothesis | Evidence |
|---|---|---|
| H-L2-1 | Structure and Array* attribute values begin with a u32 LE total length (the parser reads the first byte as a varint). An array consists of count, offset table and elements in set-block format. | `struct_len_check_out.txt` (all structure types < 128 B), LibParameter ≥ 128 B with exactly matching u32 length |
| H-L2-2 | CoreObjectData only lists the roots of the clipboard. soc/eoc/poc/voc in the TypeDescriptor count the roles SelectedObject/ExtendedObject/ViewSpecificParent/ViewObject. | 8 MCs, always soc=1 and exactly 1 entry SelectedObject |
| H-L2-3 | Referenced objects (calls, instance DBs, UDTs) are stored as name stubs when copying (`TRefParent` ↔ `TextualReferences` of the folder). UDTs additionally bring their frozen interface root. When instantiating, the same stub is created in the target device if the object is missing. | L10, L11, L12, L15, L13/PLC_2 |
| H-L2-4 | The device context of a master copy is a generic CPU (`CPU_15XX_Unspecific`, MLFB `6ES75XX_XXXXX_XXXX_<FW>`). | 021, L10, L11, L15 |
| H-L2-5 | Scope IDs and SourceObjectID have the form `{TypeID dec-OID dec}`. projectId is a session GUID. | L10–L15 |
| H-L2-6 | Instantiating leaves no origin trace. Interface GUIDs are reassigned, an already existing UDT interface is duplicated instead of reused. | L13 |
| H-020 | decided: slot order = ordinally sorted qualified set name | 924/924 Unified slots |

## Corrections to `analysis\L5_library\library.py`

- **New functions:**
  - `struct_value()`: reconstructs a structure value with u32 header.
  - `decode_core_object_data()`: role as enum name, Option, ParentObjectId, SourceObjectId, VisualPartIndex,
    element length.
  - `decode_struct_array_empty()` for NonCoreObjectData.
  - `parse_scope_id()`.
  - Constant `CLIPBOARD_ROLES`.
- **New fields in `master_copy()`:**
  - `coreObjects`, `nonCoreObjects`
  - `referencedObjects`: name stubs via TRefParent, without folders
  - `frozenInterfaces`
  - `copiedObjectsByType`
  - `categories`
  - `source.sourceScopePath[].sourceKey/sourceObject`: resolved source object
  - `folderIndex`
  - `slotOrderWarning` (H-020)
- **Sorting** of the master copies now by folder position (= creation and Openness order) instead of by
  name.
- **Text output:** `references …(name stub)`, `frozen interface`, `clipboard object …`, resolved scopes.
- Docstring of `decode_version_data` (prefix explained) and module docstring extended. The decoding of VersionData
  stays unchanged; it was already correct.
- **`run_tests.py`:** new check `check_master_copies()` for samples with `masterCopiesList.json`:
  - names in Openness order, folder, contents (name and block type FB/FC/GlobalDB), Subtype/State
  - CoreObjectData, NonCoreObjects, TypeDescriptor
  - source scopes resolved to ProjectData/S7ControllerTargetData, context
  - referenced stubs and frozen UDT interfaces per sample, no H-020 warning
  - deleted master copies: L14 folder 1, archives 0
  - In addition the run writes `L12_library.json` and `L13_library.json`.

## Test result

`python analysis\L5_library\run_tests.py` ran over **134 samples** (all folders and .zap21, including
033–036 and 110–114, which other steps added in the meantime):

- **all expectations met, no exception, 0 hash errors**
- L10–L15 (folder and archive): 14 or 25 (L12) compared values each, all equal to the ground truth
- 021 and L01–L09 unchanged ok

Details are in `analysis\L5_library\RESULTS.md`, the console output in `run_tests_out.txt`.

## Proposals for the parser (not implemented, `parser\` was off limits)

1. **H-020:** `attribute_sets_ordered()` → `sorted(self.attribute_sets(tid))` (ordinal). Then run `tools\attrdiff.py`
   over the Unified samples and re-check H-023.
2. **H-L2-1:** In `attrs.decode_set`, for values whose type is a Structure or an Array*, read the length as u32 LE
   (not as varint) and return the full value including the header. Then build a generic structure
   and array decoder following the set-block pattern that uses the structure definitions of the metamodel. That also fixes
   the truncated LibParameter/TemplateParameter (≥ 128 B).
3. Adopt `library.py` as `parser\openpedata\library.py` (proposal from L5 stands). The checks from
   `run_tests.py` belong in `parser\tests`.

## For writing (goal "read and write")

Writing a master copy from a block means:

1. Copy the block subtree with new OIDs/GUIDs. Interface names and GUIDs stay the same, the fingerprint
   has to be invalidated.
2. Create a shell device (IsShell=True) and a generic target copy (UnspecificCPU).
3. Create copied system folders (ControllerDataTypeFolder with IsUndeletable=False).
4. For every reference create a name stub with TRefParent/TextualReferences, for UDTs additionally the
   interface root.
5. Create the master copy with CoreObjectData (1 × SelectedObject) and SharedRawData (scopes `{TypeID-OID}` of the source,
   TypeDescriptor).
6. Enter links in folder, package and culture.

Only the origin of the icon is open (PNG in IconData, different per block kind).

## MCP bugs and wishes (for the MCP backlog)

1. **`masterCopyInstantiate`: `newName` is ignored** if the name exists in the target device. TIA assigns
   `FB_Iface_1`, the response reports `newObjectName=FB_Iface_1`, no renaming happens. Expected: rename to
   `newName` after `CreateFrom`, or report an error. In addition the block number stays duplicated (2). A hint or the
   option `renumber` would be good.
2. **No creation of master copy folders:** `masterCopyCreate masterCopyFolderPath=MC_Group/MC_Sub` returns
   `NOT_FOUND`. Wish: `TiaProjectLibrary(masterCopyFolderCreate/-Delete/-Rename)`, analogously type folders (L1/L08).
3. **`masterCopyCreate` only for blocks:** wish for `sourceKind` PlcType (UDT), PlcTagTable, device in the
   project library (`MasterCopyComposition.Create` with any `IMasterCopySource`) and multiple selection
   (list of sources). Only then can CoreObjectData with several entries, ParentObjectId ≠ -1 and other
   roles be created.
4. **`masterCopiesList` without details:** wish for Author, CreationDate and complete ContentDescriptions
   (type name as in the TypeDescriptor), so that more can be checked against the ground truth.
5. **Instance lease after client end:** The lease of 33032 stayed with the dead client after the end of the L1 session
   (MCP client `9e9a7eba`, until 18:34 UTC). New clients can neither lock nor write. Wish: release the lease when
   the MCP session disconnects, or `TiaInstance(unlock, force)` for the owner of the instance (ownedByServer).
6. (Not MCP, but the classifier of this session) `TiaInstance(terminate)` is blocked as "Interfere With Workloads",
   even for an own empty instance. This needs a permission rule in the MCP client
   settings.

## Files (`analysis\L2_mastercopy\`)

- `REPORT.md` (this report)
- `slots.py`: slot table of LibraryMasterCopyData in both orders
- `h020_scan.py` and `h020_scan_out.txt`: H-020 over all samples
- `dump_mc.py`, `dump_021_L10.txt`, `dump_L11_L12.txt`, `dump_L15.txt`: master copies with all copies and links
- `copy_vs_orig.py`, `copy_vs_orig_L10.txt`, `copy_vs_orig_L11.txt`: copy against original
- `struct_len_check.py` and `struct_len_check_out.txt`: H-L2-1 over all samples
- `run_tests_out.txt`: console output of the test run
- `library_before_L2.py`, `run_tests_before_L2.py`: state before L2
