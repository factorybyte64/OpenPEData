# OpenPEData – Parser for TIA Portal V21 projects

> **For test and analysis purposes only.** This Python parser is no longer the parser of OpenPEData and must not be
> shipped or used by the viewer, the setup or other programs. The parser is the .NET library
> `OpenPEData.dll` in `dotnet\` (see `dotnet\README.md`). The Python parser is used for the truth tests in `parser\tests` and for format
> research with `tools\` and `analysis\`. It was the reference of the byte comparison with the .NET version up to
> 2026-11-01; format findings made after that date exist in the .NET parser only (see `dotnet\README.md`).

Reads a project without TIA Portal and outputs it in structured form. Supported are a project folder, an `.ap21` file or a `.zap21` archive.

```
parser\OpenPEData.cmd <project folder | .ap21 | .zap21> [--json] [--keys] [--verify] [--xref] [--proginfo]
```

`--proginfo` outputs, per PLC, the program info as in TIA as JSON (`proginfo.extract`): `resources` (rows OB, FC,
FB, DB, data types, PLC tags with load, code/data work and retain memory, `incomplete` = TIA ">", totals
`used`, values per block), `callStructure` (call trees from the OBs and from the blocks not reached by an OB, with
address and local data), `dependencyStructure` (who uses which object) and `assignmentList` (addresses of the I/Q/M tags used in the
program). Total sizes of the CPU are not stored in the project and are missing. Rules and evidence:
`docs\spec\program-info.md`, test `tests\test_proginfo.py`.

`--xref` outputs the cross-references from `XRef\XRef.db` as JSON (`xref.resolved`): usages "left uses right"
with object names, kind (type code or plf type such as `HmiTagData`), member ID, `usage`/`flags` (meaning open), count and
`scope` (PLC or HMI software), plus the tag addresses. Entries whose objects are no longer in the plf
are only counted (`staleRelations`). Instruction proxies are named after their version GUID in the plf; the output uses
the name from `Author` (e.g. `R_TRIG`). Without XRef.db the output is `null`. The WPF UI (`ui\`) shows both.

`--verify` checks only the integrity (header hash, record hashes, hash chain and RSA signature of each `$$COMMIT$` record, without a secret; module `openpedata/verify.py`, exit code 1 on deviation; additionally reports whether `PEData.idx` matches the plf and whether the savepoints of XRef.db/SearchIndex match the plf, without changing the exit code). Without `--json` the tool outputs a text tree. `--keys` includes the record keys (`typeLo:typeHi:oid`) in the JSON output.
The output is structured hierarchically:

- Project
- Device groups
- Devices
- PLC (CPU order number, firmware)
- Tag tables (group)
- Tags (name, data type, address, comment per language, HMI visible/writable)
- Blocks (name, type, number, language, group). Data blocks are GlobalDB or InstanceDB (with `instanceOf`) and
  have tags: name, data type, start value, start values of array elements, comment per language, sub-elements
  of Struct and UDT tags
  Instance DBs additionally have `interface` (Input, Output, InOut, Static from the FB; `defaultValue` = start value in the FB,
  `startValue` = the DB's own value from its root payload).
  Blocks that are not DBs (FB, FC, OB) have `interface`: sections (Input, Output, InOut, Static, Temp, Constant,
  Return) with the same information on the tags (UDT parameters with sub-elements), and `networks`: per network title,
  comment (per language), for SCL the source text (`scl`), for LAD/FBD the parts (`parts`: name, template values, operands with name/section/
  member ID/access) and the wiring (`wires`: powerrail, operands, part + pin)
- PLC data types (UDTs) with the same information on their elements
- `hardware` per device (module `openpedata\hardware.py`, GOALS Z1.1, docs\spec\hw-config.md): catalog entry, stored
  parameters and DeviceItem tree as in TIA (name, slot, order number, firmware, type name, catalog entry, parameters
  = named expandos, encrypted values only as "set"), per interface the nodes with IP address, mask, router,
  MAC, PROFINET device name and subnet. Apparently only parameters that differ from the catalog are stored (H-HW-01).
  `project.network`: subnets with participants, sync domains, HMI connections (both sides).
- `library`: the project library (module `openpedata\library.py`): system folders, master copies (content, context,
  name stubs, clipboard header), types with versions (number, InWork/Committed, default version, release time,
  comment, dependencies, content copy) and typed instances with device and version status (`versionState`:
  version, default and highest version, `outdated` derived, consistency of the stored version references).
  The text output shows the library only if it contains types or master copies.
- `faceplates`: WinCC Unified faceplates (module `openpedata\faceplates.py`, step F3, `analysis\F3_faceplate`):
  per faceplate type GUID, folder, "do not use", "always load", default and highest version; per version number,
  InWork/Committed, default, author, release time, content (`ILFaceplateTypeData`: size, screen items with type, position,
  size and events), interface (`tags`: interface tags with name, data type, tag and acquisition cycle from
  `IFaceplateInterface.TagInterfaceProperties`, CoreMapArray of pe:MapT; `tagTable`; `properties`/`events` raw until
  samples are available), tag shells without a tag table (`unresolvedTags`, cause of a
  compile error), script count, objects of the hidden device of the version (`environmentObjects`, of which
  `standardObjects` = fixed core, so far always 79) and typed instances (with `containedType` as in Openness, e.g. `V0.0.1\FP_Basic`). Faceplate containers without a type are in
  one entry with `name` = null. Without faceplates the list is empty, and the text output shows no section.

## Language: Python 3 (standard library only)

- The format can be read completely with `zipfile`, `zlib`, `hashlib`, `struct` and `xml.etree`. There is no dependency on .NET, Openness or TIA, and the scripts run without a build step.
- The analysis is still exploratory. Analysis tools (`tools\graph.py`, `attrdiff.py`, `layout.py`) and the parser use the same modules, so every finding flows into the parser immediately.
- The previous C# helpers (`tools\PeData.cs`) only run via `Add-Type` in PowerShell 5.1. A standalone C# tool would additionally need a project, an SDK and a build.

## Structure

| Module | Content |
| --- | --- |
| `openpedata\plf.py` | `System\PEData.plf`: record chain (length, key, SHA-256), last version per key |
| `openpedata\metamodel.py` | Metamodel from record `0000:0007:1` (zlib-compressed XML): types, base types, AttributeSets, relations, stored attributes |
| `openpedata\record.py` | Object record: header, slot table, attribute blocks, tagged link blocks and typed link lists |
| `openpedata\attrs.py` | Decoding of the attribute blocks |
| `openpedata\project.py` | Loading of folder, `.ap21` or `.zap21`; access to attributes and links |
| `openpedata\model.py` | Navigation project → device → PLC → tag tables/tags/blocks/UDTs, comment texts |
| `openpedata/verify.py` | Integrity check (`--verify`): header, record hashes, hash chain, RSA PKCS#1 v1.5 signatures of the commits |
| `openpedata/pidx.py` | Read `System\PEData.idx` and check it against the plf; savepoint link plf ↔ XRef.db / SearchIndex (`docs/spec/derived-stores.parser.md`) |
| `openpedata/xref.py` | Read `XRef\XRef.db` (parts → plf keys, objects, relations, tag addresses); `resolved` with object names for `--xref` |
| `openpedata/proginfo.py` | Program info per PLC (`--proginfo`): resources, call structure, dependency structure, assignment list (`docs/spec/program-info.md`) |
| `tests\test_proginfo.py` | Program info of samples 006, 025, 031, 036, 039, 040 against TIA screenshots (result `tests\RESULTS_PROGINFO.md`) |
| `tools\scan_project.py` | Gap check of any project/library (read-only): verify, unknown record types, objects with a decode problem (grouped), unknown tokens, errors of the model (`--brief`: one line) |
| `openpedata\code.py` | Networks of the code blocks: SCL source text from the token tree, LAD/FBD parts, operands and wiring |
| `openpedata\interface.py` | Interface of data blocks, UDTs and code blocks (FB/FC/OB, sections): tags, data types, start values, comments |
| `openpedata\hardware.py` | Device and network configuration: DeviceItem tree, parameters, nodes, subnets, sync domains, connections (docs\spec\hw-config.md) |
| `tests\test_hardware.py` | Hardware against the Openness device tree (`device_*_details.json`), CAx export (`hardware.aml`) and recipe 126 (result `tests\RESULTS_HARDWARE.md`) |
| `openpedata\library.py` | Project library: folders, master copies, types/versions, instances and their version status (steps L0–L5, `analysis\L4_instances\REPORT.md`) |
| `openpedata\faceplates.py` | WinCC Unified faceplates: types, versions, content, hidden device, instances (step F3, `analysis\F3_faceplate`) |
| `tests\test_faceplates.py` | Faceplates of all samples: F02/F03 against typesList.json and the F3 expectations, otherwise empty list (result `tests\RESULTS_FACEPLATES.md`) |
| `tests\test_library.py` | Library of all samples against typesList/instancesList/masterCopiesList and the L1/L2/L4 expectations (result `tests\RESULTS_LIBRARY.md`) |
| `tests\test_samples.py` | Comparison of all samples (folder and `.zap21`) with the Openness ground truth in `tests\truth\` (created with `tools\Make-TruthSet.ps1` from copies of the samples) |

Test run (result in `tests\RESULTS.md`):

```
"%LOCALAPPDATA%\Programs\Python\Python312\python.exe" parser\tests\test_samples.py
```

## Format knowledge (summary, details in the module docstrings)

- An object record consists of these parts:
  - `+4` key (type ID, object ID), `+26` number of slots, `+28` GUID
  - from `+44` a slot table with u32 offsets
  - the blocks
- Slot order: first the AttributeSets of the type (including inherited ones), sorted by declaration order in the metamodel XML, then the link slots.
- Stored attributes are all attributes of the set that are neither `constant`, `relation=` nor `intrinsic`. The flags are merged from the set definition and the `Implements` overrides of the base types. The attributes are in ordinal order:
  - fixed types inline
  - strings and blobs as a u32 offset relative to the block start; there a varint (n+1) follows, then the bytes
- A link is 16 bytes long: relation ID, target type ID, target OID. In typed lists the relation ID is missing; there an entry is 12 bytes long.
- Interface (DB, UDT): `CurrentInterface` → `InterfaceVersionRootData`. Its `IInterfacePartData.Payload` is
  XML with `<Member ID Name RID [Type] [SubPartIndex]>`.
  - RID `0x02xxxxxx`: elementary type; the lower 16 bits are the `DataTypeID` of the `DataTypeItemData` in the scope of the
    CPU family.
  - `0x92…`: Struct; `0x91…`: UDT; arrays and UDTs carry the declaration in `Type`.
  - `SubPartIndex` points into the ordered link list `UsedParts`: `InterfacePartData` Kind=Structure contains the
    Struct elements, an `InterfaceVersionRootData` the elements of the used UDT.
  - Start values of DBs are in the part Kind=Values as `<V p="55:52" v="-5"/>`; the path consists of member IDs, an
    array element appends `[i]`. For UDTs they are in the expando `Values` of the root object.
  - Comments: `InterfaceComments` → `InterfaceCommentsPartData`, set `ICommentsExpandoAttributeSet` in the format
    expando="simple" (see `attrs.decode_simple_expando`): path → CoreText.
