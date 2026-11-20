# L5 – Groundwork: library output (without TIA)

As of: 2026-10-19. I worked without TIA and without MCP, did not use git and did not change any tracked file.
`parser\openpedata` is only imported. All files are in `analysis\L5_library\`.

## What the module outputs

`library.py` only needs the stdlib and openpedata. The function `extract_library(store, keys=False)` takes an
`openpedata.project.Project` or a `Model` (then `.p` is used) and returns a dict.

CLI: `python analysis\L5_library\library.py <project folder|.ap21|.zap21> [--json] [--keys]`. Without `--json` there is
a tree view as text. `--keys` appends the record keys (for debugging).

- **`libraries[]`**: every CoreLibrary outside of master copies, today always exactly "Project library".
  - Fields: name, `libraryType` (ICoreLibAttributes), write protection, author, times, comment.
  - **`systemFolders[]`** in a fixed order: LibraryTypeSystemFolder, LibraryMasterCopySystemFolder,
    LibrarySystemTypeSystemFolder, LibrarySystemMasterCopySystemFolder, CommonDataFolder.
  - Each folder recursively with `folders[]` (subfolders) and `elements[]` (type name, package, name, subtype).
  - Subfolders are searched via `Folders`/`FolderParent`, elements via `AggregatedElements`/`AggregatingFolder`,
    each in both directions.
  - **`masterCopies[]`**: name, `systemFolder`, `folderPath` (user path below the system folder), subtype, author,
    LastModifiedBy, creation and modification time, comment, `state` (ICoreLibObjectAttributes), OriginalLibrary/-Name.
    - `source`: parsed SharedRawData. Contains projectId, `sourceScopePath` (Persistence/Project/Target with
      scopeId), `typeDescriptors` (typeName, roles as a list, soc/eoc/poc/voc), selectedIds and resolvers.
    - `contents`: VisibleParts with type, full type name, package, name, subtype and, for blocks, BlockType.
    - `parts` and `invisibleParts`.
    - `context`: device context separated from the content, namely `Devices` (with IsShell) and the `Target` of the VisibleParts.
    - `copiedObjects`: number of objects with Environment → master copy. With `--keys` the key list is added.
    - `relatedPackage`, expando `customAttributes`.
    - `raw`: CoreObjectData as hex, lengths of BinaryData and NonCoreObjectData.
  - **`types[]`**: all objects whose type inherits from `TypeObjectBase` (UserTypes) or `CoreLibraryType` (these are
    LibraryTypeData, IECPLLibraryTypeData, TextlistLibraryTypeData, IECPLInstructionData).
    - Fields: name, type, package, folder, version Major.Minor.Subminor (ICoreLibTypeAttributes), State, TypeGuid,
      OriginalTypeGuid, TypeNamespace, IsObsolete, default version, DependsOn, content (VisibleParts).
    - **`versions[]`** come from TypeObjectToVersionObject or inversely VersionObjectToTypeObject. Fields:
      IVersionInfo.Version, VersionGuid, FinalizationTime, MinimumTargetDeviceVersion, State, Manufacturer,
      SystemVersionOrderNumber, `isDefault`, Previous/Next, DependsOn, content.
  - `versionsWithoutType[]` and `unassigned` take versions without a type and objects without a recognizable library.
- **`instances[]`**: InstanceObject is the base type of every CodeBlockData and UserTypeData. A blind "inherits from
  InstanceObject" would therefore list all FBs and UDTs. So the module only includes InstanceObjects that are linked to a
  type or version object (in both directions). The total count is in
  `statistics.instanceCapableObjects`.
- **`statistics`**: number of master copies, types and versions, `libraryObjectsInsideMasterCopies` as well as
  `deletedLibraryObjectsHidden` (hidden tombstones of library objects, counted by type).

Further rules:

- **Deleted objects**: tombstones are not in `Project.objects`. Links to them are dropped.
- **Copies in master copies** (Environment → LibraryMasterCopyData) only appear as content or context, never as
  their own library, master copy or own type.
- **Package resolution**: The module builds the mapping type ID → `<Package name>` from the metamodel XML. It does
  not use `Metamodel.packages` (id>>16). Example: LibraryMasterCopyData is in
  `…Library.MasterCopies`, not in DomainModel.

## Test result

Details are in `RESULTS.md`. I ran `run_tests.py` over **48 samples** (24 project folders and 24
.zap21), with hash check: **all expectations met, no exception, 0 hash errors**.

- **021_mastercopy** (folder and .zap21) shows exactly the master copy `FB_Empty` in the LibraryMasterCopySystemFolder.
  It has Subtype IECPL, State Committed and the content CodeBlockData FB_Empty (FB). As context
  DeviceData PLC_1 (IsShell=true) and S7ControllerTargetData PLC_1 appear. The source is CodeBlockData.FB with the role
  SelectedObject. There are 10 copied objects, which matches L0.
- All other samples show an empty project library with five system folders and without subfolders, types,
  versions or instances.
- No sample has types or versions. Therefore I applied the paths `lib_type()`/`version()` as a smoke test
  to up to 400 arbitrary objects per sample. There was no exception; missing sets and relations become
  `null` or `[]`.
- The JSON output of 021 with keys is in `021_library.json`.

## Open points

1. **Types and versions are untested.** The module only follows the metamodel. Whether the real objects (L1) are connected via
   `TypeObjectToVersionObject` or a typed link slot is open. Equally open is where
   InWork/Committed is stored: in ICoreLibTypeAttributes.State, ICoreLibObjectAttributes.State or only in
   FinalizationTime (H-L0-2). The module outputs State and FinalizationTime separately. For types, State falls back to
   ICoreLibObjectAttributes.State.
2. **The encoding of `IVersionInfo.Version`** (structure VersionData with Build/Major/Minor/Revision without ordinals)
   is unknown. Provisionally: 16 bytes are read as 4×i32 in declaration order, `versionRaw` always contains
   the hex value. This must be checked with the first L1 sample.
3. **Link instance → type version unknown.** Neither InstanceObject nor CodeBlockData declare a relation
   to the version. Candidates are expando attributes, typed link slots (slot → relation still unknown) and
   ICoreTypeObjectAttributes (TypeObjectId/VersionId, but that hangs on CoreTypeObject).
4. **CoreObjectData encoding**: The array LibraryClipboardCoreObject (33 B in 021) is only output as hex. The
   interpretation as Role=SelectedObject, ParentObjectId=-1, SourceObjectId=0 is unconfirmed. Only master copies with
   multiple selection (L2) give enough variation.
5. **Scope IDs** in SharedRawData (`{135208-1075}`, `{1052700-2067}`) are unexplained; I output them unchanged.
   The Persistence scope contains the computer name and the old path of the source project. This is personal data and should
   possibly be optionally hidden in a later publication.
6. **Subfolders** in library folders do not exist in any sample. The navigation via
   `Folders`/`FolderParent`/`AggregatingFolder` is transferred from the device folders, but not yet checked.
7. **Empty fields:** `ILibraryProtectionInfo.IsLibraryContentWriteProtected` is `null` in all samples (not
   stored or slot empty). Likewise `ICoreLibIdAttributes` (LibraryObjId, UserVersion, version GUID) returns no values on
   the master copy. Both should be looked at again with L1/L2.
8. **Global libraries (.al21)** and system types (LibrarySystemTypeSystemFolder) do not occur. The structure
   is set up generically (every CoreLibrary), but untested.

## Proposal for integration into `parser\openpedata` (not implemented)

1. New module `parser\openpedata\library.py` with the class `LibraryView` (or as a mixin in `Model`). The own
   `incoming` directory and the culture table are then dropped, because `Model` already has both (`Model.incoming`,
   `Model.sources`, `Model.cultures`, `core_text`).
2. `type_packages()` belongs in `metamodel.py`, as `Metamodel.type_package(tid)` from the XML. In doing so one should fix the
   side finding from L0: key `Metamodel.packages` by the full package ID instead of by `id >> 16`.
3. In `Model.to_dict()` add a key `"library": extract_library(self)` next to `"project"`. In
   `OpenPEData.py` add the section to the text output, possibly with a switch `--library`.
4. Include `parse_shared_raw_data()` and `decode_version_data()` as helper functions in `attrs.py` or `library.py`.
5. Move the checks from `run_tests.py` as a test case to `parser\tests`: 021 has a master copy, the
   other samples an empty library. With L1/L2, expectations for types, versions and subfolders are added.

## Generated files (all in `analysis\L5_library\`)

`library.py` (module + CLI), `run_tests.py` (test over all samples), `RESULTS.md`, `021_library.json`, `REPORT.md`.
