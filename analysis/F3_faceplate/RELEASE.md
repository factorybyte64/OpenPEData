# F3 list item 12 (part): released version versus InWork

As of 2026-10-21, read-only: `samples\F03_hmi_faceplates_user` (FP_Basic, FP_Elements Committed; FP_Controls, FP_Interface, FP_Script InWork) and `samples\F05_fp_iface_instance.zap21` (FP_Interface released by the user). Diff F03 → F05: `diff_F03_hmi_faceplates_user_F05_fp_iface_instance.txt`.

## No copy on release

For PLC types, a released version has a content copy (`VisibleParts`) and a shell device (`Devices`, library.py, analysis\L1_types) attached. Not for faceplates: every version, whether InWork or Committed, is its own environment space with a hidden device (`Parts` → DeviceData "FaceplateDevice") and content (`VersionToContent` → ILFaceplateTypeData), HIDDEN-DEVICE.md. The link structure of the versions is the same in both states.

**FP_Interface before/after (F03 InWork → F05 Committed):**

| | F03 | F05 |
| --- | --- | --- |
| Version object | `0003:00d3:248a` | `0003:00d3:248a` (the same) |
| Content | `1004:00cd:24de` | `1004:00cd:24de` (the same) |
| `ICoreLibObjectAttributes.State` | InWork | Committed |
| `IVersionInfo.FinalizationTime` | 0001-01-01 | 2026-10-21T04:39:50.850632Z |
| `IVersionInfo.VersionGuid` | 1504957d-2b45-4bdd-a1f6-18d2a48a6f13 | **526b9f5a-3379-4123-ad28-148fb2e7428f** |
| `IVersionInfo.Version` | 0.0.1 | 0.0.1 |
| Links | – | `DefaultVersionToType` → type (on the type `TypeToDefaultVersion` → version) |
| Objects of the version | 88 | 89 (interface table + tag instead of shell, F05.md; due to the editing, not the release) |

So the release changes the version in place and assigns a new VersionGuid. Instances reference via link (`IsInstanceOf`), not via the GUID; an instance created before the release would keep the link.

**Small differences per state:**

- Only the default versions have `DefaultVersionToType` (all Committed here).
- `UsedCultures`: FP_Basic and FP_Elements (released directly on creation) 1, the other three 3 (FP_Interface also after the release). Cultures are likely added when editing in the faceplate editor; not checked further.
- Creating an instance sets `ICoreAttributes.ModifiedTime` of the version (FP_Basic: 2026-10-21T04:38:03, equal to the CreationTime of the instance) and adds `IsTypeOf`.

## Parser and test

- `faceplates.py`: per version additionally `versionGuid`, `creationTime`, `modifiedTime`, `lastModifiedBy`.
- `test_faceplates.py`: VersionGuid of FP_Interface in F03 and F05 (values above are from the original archives); since the rebuild of 2026-11-09 the test checks that F05 has a valid GUID different from F03 instead of fixed values, because GUIDs are random per build.

**Addendum F16 (2026-10-23):** a new version via "Edit type" is a complete copy with its own content, its own hidden device and its own VersionGuid (F16.md: FP_Interface 0.0.1, 0.0.2, 0.0.3).

## Open (F21–F24, F29, F34)

New version via "Edit type" (1.0.1 InWork next to 0.0.1 Committed): is a second version then created with its own copy of content and hidden device? Switching default, Do not use, renaming, duplicating, folders, deleting, updating instances.
