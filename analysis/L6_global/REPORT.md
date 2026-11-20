# L6 – Global library (.al21 / .zal21)

As of: 2026-10-21. I worked with my own TIA instance: **PID 55072** (`acquire`, `lock` 4 h). At the end I
terminated it with `terminate`, without force. I did not touch foreign instances (16168, 35092). All
projects and libraries are saved and closed.

## Samples

Recipes are in `samples\MANIFEST.md`, section "Step L6". For each sample there is the folder, the archive
(`.zal21` for global libraries, `.zap21` for projects), `<sample>_truth\` and `<sample>_attrdiff.txt`.

| Sample | Kind | Content |
|---|---|---|
| L30_glib_empty | global | empty global library |
| L31_glib_type | global | UDT_Motor 0.0.1 + 1.0.0, taken over from a project via `update` |
| L32_glib_mc_device | global | plus the device master copy PLC_1 (`masterCopyCreateFromDevice`) |
| L36_glib_mc_deleted | global | master copy deleted again |
| L33_proj_update_from_glib | project | L02 + `update` from the global library: 1.0.0 new in the project library, instance outdated |
| L34_proj_mc_from_glib | project | device master copy copied from the global into the project library |
| L35_device_from_mc | project | device instantiated from the master copy (PLC_2 with typed UDT on 1.0.0) |

**Not possible (TIA limit):** creating a type directly in the global library. `typeCreateFromDocument` returns
"It is not currently supported to modify global libraries." Types only get in via `update` from another
library. For folders and version operations in the global library the MCP offers nothing.

## Findings

### 1. File format: like a project, with a different root

**Folder** (`<Name>\`):

- `<Name>.al21`
- `LibraryInfo.txt` (YAML, TIA version like ProjectInfo.txt)
- `System\PEData.plf` and `System\PEData.idx`
- `Vci\Vci.db`
- in the archive additionally empty folders (AdditionalFiles, IM\SearchIndex, src, TMP, UserFiles, XRef), plus `Logs\…zip`

**`.al21`:**

- The same XML header file as `.ap21`: `<Data Type="Library" … BrowsableType="…LibraryData.GlobalLib">` with
  icon and `ProjectVersion`, about 151 KB.
- In the `.zal21` it is called `<archive name>.zal21.al21`.

**PEData.plf:**

- The same record chain with hashes, hash chain and RSA-signed commits. `--verify` is ok, the parser reads it
  without changes.
- **The root object is `LibraryData`** at **OID 0x433** (key 1014:0002:433). This is the OID that `ProjectData` has
  in the project.
  - `ICoreLibAttributes.LibraryType = GlobalLib`, `ICoreAttributes.Subtype = GlobalLib`
  - Name = library name, `LastModifiedBy`
- In the project, by contrast, the project library is at 1014:0002:443.
- There is no `ProjectData`.

**Empty global library versus empty project** (L30 against 000_empty): 312 versus 344 objects, 248 identical keys
(data type system, packages, culture).

- Missing are ProjectData, all alarm and system diagnostics objects, CoreText, NotAssignedObjectsData and 13 folders.
- Present are only the 5 library system folders (`Folders` on LibraryData) with `Environment` → LibraryData.

**Archive:** As with `.zap21` the plf is compacted. L36 has 208 tombstones in the folder, 0 in the `.zal21`.

### 2. Types between libraries (L31, L33)

- `UpdateLibrary` copies type, versions, content copies and the `_BIVE` roots (UDT_TypeLibraryPartContainer) with
  **new OIDs**.
- **TypeGuid and VersionGuid stay the same**, as do the names of the content copies (with the InWork GUID).
- The `CreationTime` of the version becomes the copy time, `FinalizationTime` stays.
- **`ICoreLibObjectAttributes.OriginalLibrary`:** After the import from the global library into the project (L33),
  1.0.0 has `L30_glib_empty`, the already existing 0.0.1 still has `000_empty`.
  - Hypothesis H-L6-2: OriginalLibrary = library from which the version was last copied. Project libraries
    are named like the project.
- **Default version:** The MCP default is `forceUpdateMode=SetOnlyHigherUpdatedVersionAsDefault`, so 1.0.0 becomes
  the default.
- The existing instance stays on 0.0.1 and is outdated afterwards (L33). The same pattern as in L4 applies to instances.

### 3. Device master copy (L32, L34, L35)

**`masterCopyCreateFromDevice`:**

- creates `LibraryMasterCopyData` with Subtype **`Hardware`**
- `VisibleParts`/`Devices` → copied DeviceData PLC_1
- 207 copied objects with `Environment` → master copy
- CoreObjectData with 1 × SelectedObject, TypeDescriptor `…DeviceData.S71500.Device`
- source scope `Project={135208-1075}` plus Persistence path of the source plf

**Typed objects in the master copy stay bound to their type:**

- The UDT copy in the copied PLC_1 has `IsInstanceOf` → version 1.0.0 **of the global library** and
  `CurrentLibraryInterface` → its `_BIVE` root.
- This corresponds to Openness `MasterCopiesContainingInstances`.
- `library.py` does not count such copies as instances, but shows them per version as
  `masterCopiesContainingInstances`.

**Further steps:**

- `masterCopyInstantiate` (global → project library, L34) copies the master copy including its content. The
  master copy gets a new creation time; the typed copy afterwards points to the version of the
  project library.
- When instantiating as a device (L35), PLC_2 is created with Main, FB_Empty and UDT_Motor as a real instance on 1.0.0.
  The own interface root keeps the GUID of the source (`_BIVE:UDT_Motor/290cf206…`).
- `newName=PLC_3` is ignored (like MCP-041 for blocks).

**Deleting (L36):** 208 tombstones, namely the master copy with all 207 copies. The type stays unchanged.

### 4. Side finding

The source scope of a master copy (`{135208-1075}` = ProjectData 1028:0002:433) points to the **source project**. In
L34/L35, `library.py` happens to resolve it to the ProjectData of the target project, because all samples are copies of
000_empty and the key is the same. The Persistence path remains authoritative.

## New hypotheses

| ID | Hypothesis | Evidence |
|---|---|---|
| H-L6-1 | Global library = project storage format with root LibraryData (GlobalLib) at OID 0x433 instead of ProjectData; only the 5 library system folders. | L30–L32, L36 (folder and .zal21) |
| H-L6-2 | Types move between libraries with new OIDs, but the same type and version GUIDs. OriginalLibrary names the source library of the last copy. | L31, L33 |
| H-L6-3 | Typed objects in master copies keep `IsInstanceOf` to the version of the same library (MasterCopiesContainingInstances). | L32, L34, L35 |

## Parser (`parser\`)

Commit `5fff475`:

- `Model.to_dict()` recognizes the global library (`project.kind = GlobalLibrary`, name from LibraryData) and lists
  only devices of the root, i.e. no shell devices from versions or master copies. Text output: "Global library <Name>".
- `library.py`:
  - `version_instances()` excludes typed copies in master copies and version copies
  - new `masterCopiesContainingInstances` per version
- `test_library.py`:
  - `.zal21`
  - GlobalLib expectation for global libraries
  - device master copies (Subtype Hardware, DeviceData as content and context)
  - samples with types and master copies
  - version GUIDs from the global typesList
  - expectations for L30–L36

**Tests:** `test_library` with **229 samples green**, plus `test_samples`, `test_hmi`, `test_faceplates` and
`test_units` green (outputs in `tests\`).

## MCP and TIA

- **TIA limit:** `TiaLibraryContent(typeCreateFromDocument)` on a global library → "It is not currently
  supported to modify global libraries."
- **MCP:** `TiaProjectLibrary(masterCopyInstantiate, targetKind=Device, newName=…)` ignores newName. TIA assigns
  PLC_2. This is the same pattern as MCP-041.
- **MCP (wish):** no folder operations, no `typesList` with versions in one call and no
  `instancesList` for global libraries (for master copies with typed objects:
  `MasterCopiesContainingInstances`).
- The `.zal21` archive content is called `<archive name>.zal21.al21`. This is only an observation; TIA opens it.

## Files

`analysis\L6_global\REPORT.md`, `analysis\L6_global\tests\` (test outputs)
