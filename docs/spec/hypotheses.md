# Hypotheses

Format: `H-NNN | Status | Statement | Evidence/Test | Date`

H-001 | open | Project content lives essentially in `System/PEData.plf`; the `.ap21` file is only a small entry/metadata stub. | Inventory of an empty project | 2026-10-19
H-002 | open | `.zap21` is a (possibly modified) ZIP of the project folder. | `file`/`unzip -l` on archive | 2026-10-19
H-003 | open | Objects are partly stored compressed (high-entropy regions). | Entropy scan of PEData.plf | 2026-10-19
H-001 → confirmed 2026-10-19: .ap21 is only an XML descriptor (icon + ProjectVersion); content in System/PEData.plf. See 00-overview.md.
H-002 → confirmed 2026-10-19: .zap21 is a standard ZIP; without IM/* and AdditionalFiles/PLCM/*.
H-003 → partly confirmed 2026-10-19: zlib streams in plf at 2228, 166083, 241510; extent open.
H-004 | open | PEData.plf is append-/log-structured: Close after archiving grew the plf from 230885 to 247043 B (idx 14275→14962). | Compare plf from zap21 vs. folder | 2026-10-19
H-005 | open | First DWORD in the plf/idx header is the header length (0x40 resp. 0x80); GUID = project/store ID. | Create a second project, compare GUID | 2026-10-19
H-006 | open | Saving again without a change modifies the plf (timestamps/noise) → noise baseline needed for diffs. | Open+save without change | 2026-10-19
H-004 → refuted in the form "append only", replaced: plf is a transaction log; archive contains a compacted version (plf-container.md). 2026-10-19
H-005 → refuted: DWORD 0 = file kind (0x40/0x80); header GUID = generation per write operation; @24 = writing TIA version. 2026-10-19
H-006 → confirmed: resave without a change does not rewrite the plf; noise between fresh creations = GUIDs, ticks, keys, commits. 2026-10-19
H-007 | open | Record class @+4 can be resolved via the IDs in the embedded metamodel (0000:0007:1). | Parse metamodel, map classes against keys | 2026-10-19
H-008 | open | $$COMMIT$ records are signed with the project RSA key (0016:0007:18). | Check signature against public key | 2026-10-19
H-009 | open | 1028:0002:433 is the project object with name. | Change project comment/name | 2026-10-19
H-007 → confirmed 2026-10-19: TypId = (u16@+6<<16)|u16@+4, 100 % resolvable via the metamodel (object-model.md).
H-009 → confirmed 2026-10-19: 1028:0002:433 = ProjectData.
H-010 | open | Payload header @60/@72 are offsets to the attribute and link block respectively (fits for FolderData, not clear for EAMTZTagData with several sections). | Generic header/link-block parser over all records | 2026-10-19
H-011 | open | Order of the attributes in the attribute block follows the AttributeSet definitions (persistent="true") in the metamodel. | Tag variants (type/address/comment) | 2026-10-19
H-012 | open | Store records 0011:0007 = name registry for expando attributes; 0013:0007 appears with a CPU. | More samples | 2026-10-19
H-010 → clarified 2026-10-19: @60/@72 = slot 4/7 of the slot table from +44, meaning depends on type (record-payload.md).
H-011 → confirmed in essence 2026-10-19: attribute order = ordinal of the persistent AttributeSet attributes; tag variants in 4b check the detail.
H-013 | open | Remaining errors in attribute decoding: expando sets and off-by-one for large blobs (varint length?). | 4b/4d | 2026-10-19
H-011 → confirmed 2026-10-19: tag layout fully from AttributeSets, each of the five changes in one decoded attribute (tag.md).
H-014 | open | ExternalVisible = IStructureItem.HmiVisible; ExternalAccessible not yet mapped; Part4 = area code (3 = M). | Variants with I/Q address, visibility | 2026-10-19
H-015 | open | Comment texts are UTF-8 (umlauts unchecked). | Comment with ä/ö/ü/ß | 2026-10-19
H-008 → confirmed 2026-10-19: commits are signed RSA-2048/PKCS#1 v1.5/SHA-256 over commit bytes [0,128) incl. chain hash over all record hashes (integrity.md).
H-016 | open | Private key in 0016:0007:18 is protected with PBKDF2 (10000 iterations) + block cipher; secret unknown. | Structure only, no circumvention | 2026-10-19
H-017 | open | TIA checks signatures on opening (rejection on tampering). | TIA attempt, not approved | 2026-10-19
H-018 | open | How TIA records the deletion of an object in the log (tombstone record? index entry?). Example versioning: 009_tag_rename contains the tag twice (old Tag_Bool_1, new Tag_Renamed); plf 003 folder 711 KB vs. archive 539 KB. | Sample with deleted tag/object | 2026-10-19
H-012 → confirmed 2026-10-19: 0x00070011 = expando dictionary per object type (names + type codes).
H-013 → solved 2026-10-19: varint = total size incl. itself; expando format decoded; 99.25 % complete.
H-014 → confirmed 2026-10-19: ExternalVisible = HmiVisible, ExternalAccessible = HmiStructureTypeRelevant.
H-015 → confirmed 2026-10-19: comments UTF-8.
H-018 → confirmed 2026-10-19: deletion = 49-B tombstone with the same key, @24 = 4.
H-019 | open | Remaining 0.75 %: nested array/structure data at the end of the block, expando="simple". | Further analysis | 2026-10-19
H-020 | open | Slot order: alphabetical by qualified name (TIAconv/ERNW) or declaration (us) – identical in the samples; decidable only on LibraryMasterCopyData with occupied slots. | Extend 021_mastercopy | 2026-10-19
H-L01 | open | Unified/faceplate types appear in the metamodel only with a Unified device in the project (like Simatic.Lang only with a CPU). | Sample with Unified device (Unified plan) | 2026-10-19
H-L02 | open | Types/versions = objects of TypeObjectBase/VersionObjectBase (UserTypes) or derivations in Simatic.Lang; status InWork/Committed in IVersionInfo.FinalizationTime or ICoreLibObjectAttributes.State. | L1 | 2026-10-19
H-L03 → rejected 2026-10-19: "master copy = blob" does not hold for the V21 project library; content is live objects (library.md). Open for global libraries .al21.
H-L04 | open | LibraryMasterCopyData.Subtype names the domain (IECPL = PLC); device/HMI have other subtypes. | L2 | 2026-10-19
H-021 | open | Unified content lives as normal records in PEData.plf; when the HMI device is created, the Hmi packages (names like Simatic-Hmi-HmiTag, -HmiScreen …) are added to the embedded metamodel. | U1 (100_hmi_device) | 2026-10-19
H-022 | confirmed | The interface of FB/FC/OB lives in InterfaceVersionRootData (Block --CurrentInterface-->). The root payload names the sections as members with fixed IDs 2 Input, 3 Output, 4 InOut, 5 Static, 6 Temp, 7 Return, 8 Constant; a section references a part in UsedParts via SubPartIndex (parts Input/Output/InOut/Static/TempSection) or holds the members inline (Return, Constant). | Samples 023_fb_iface, 024_fc_iface against SimaticML and get JSON | 2026-10-20
H-023 | confirmed | UsedParts is position-stable: unused slots are null links 0000:0000:0, SubPartIndex counts this list including the nulls. A parser must not discard nulls (otherwise indices shift, also for DB/UDT). | 023/024, parser tests green | 2026-10-20
H-024 | confirmed | Member IDs are unique block-wide (from 51); comment and start value paths use only the member ID (nesting ID:ID, array element 55[1]), not the section. | 023/024 | 2026-10-20
H-025 | confirmed | Start values: for the FB in the Values part of UsedParts, for the FC in the root ICoreExpandoAttributes.Values; constants have start values like tags. Comments never live in the part but in InterfaceComments. | 023/024 | 2026-10-20
H-026 | open | System comments of OB Main (e.g. Initial_Call) carry, in addition to the culture key en-US, a key 65535 (culture-neutral?). | Observation 5c, not checked against truth | 2026-10-20
H-027 | confirmed | Instance DB: DataBlockData with Type=IDBofFB, OfType=FB, OfNumber; link IsGeneratedFrom → FB (on the FB: GeneratedByThis → DB). CurrentInterface of the IDB is an InterfaceVersionRootData with Kind=Values and empty payload; its UsedParts target is the interface root of the FB (Kind=BlockSource). Structure and members come from the FB, the IDB has only Input, Output, InOut, Static. | Sample 025_fb_idb against SimaticML and get JSON, 118 checks | 2026-10-20
H-028 | open | A start value changed in the instance DB presumably lives in the Values payload of the IDB root. | No sample yet (025b) | 2026-10-20
H-028 → confirmed 2026-10-20 (029_fb_idb_value): start values changed in the IDB are in the payload of the DB's own InterfaceVersionRootData (Kind=Values) as `<Values xmlns=".../BlockInterface/Values/V11_01.xsd"><V p="60" v="99"/><V p="61" v="TRUE"/></Values>` (UTF-8 BOM), p = member ID of the FB; counter block and PayloadCRC change along with it. In the unchanged IDB the payload is the empty 20-byte skeleton. documentWrite on the DB replaces the object (new OID, new interface root; DB number stays), no in-place update.
H-029 | confirmed (SCL, LAD, FBD) | Block code lives as source in `CompileUnitData` objects (Block --Sources-->, one per network; ProgrammingLanguage SCL / LAD_CLASSIC / FBD_CLASSIC). `ICompileUnitData.Data` is a blob (page format) with UTF-8 BOM XML; SCL = `<SCLSource Version="3.4.0.0">` with `<Symbols>` and `<RootStatements>` as a lossless token tree (LC line comment, NL line break, BL blanks, Statement/Expression/SymVa/Const/Op*/Kw*), source text reconstructible. Network title = `ICoreAttributes.Comment` of the CompileUnitData (multilingual), network comment = link `CompileUnitComment` → CoreText. The compiled program lives separately in `LoadablePlusCodeBlockData` / `LoadableDependentData` (MC7plus binary), not touched. | 026 to 028, 5d part B, block-code.md | 2026-10-20
H-030 | confirmed | Operands of LAD/FBD networks are resolved via IdentPartData: ORef@RefId = InterfaceAccess/ID@RID; mapping CS/C with NID = ICompileUnitData.RefID and UID = ORef UId; name from SSD/AOS/AO@N, section SSD@S (I, O seen), member ID SSD@MID. | 026 to 028 against SimaticML | 2026-10-20
H-031 | confirmed | ORef without RefId is an open input (SimaticML OpenCon), in FBD at the Add box EN. | 027/028 | 2026-10-20
H-032 | confirmed | SCL indentation is stored in the token tree as NL + BL NumBLs; get and SD export output it unchanged (imported source text keeps the indentation of the following lines, the first line has none). | 026 | 2026-10-20
H-033 | open | The OB Main from the template (LAD) has a CompileUnitData without a readable network; presumably an empty network. | 5d, not investigated | 2026-10-20
H-021 → partly confirmed 2026-10-20: Hmi packages are added to the metamodel with the HMI device (42 → 100 packages), device/connection/alarm classes/system tags/text lists as normal plf records (hmi-unified-container.md). Additionally, runtime files (IM\HMI\*.rdf, RtIdFiles) live outside the plf.
H-022 | open | The files in IM\HMI (RDF, RTID) are pure compile results; tags, alarms and screens of the engineering are fully contained in the plf. | Compare sample with tag/alarm/screen without compiling vs. after compiling (U2) | 2026-10-20
H-023 | open | Expando block with 6 bytes (`06 00 00 00 01 00`) = empty IAdditionalAttributes (ReportStorageLocationData); decode_expando must accept it. | Parser fix, test on 100_hmi_device | 2026-10-20
H-024 | open | Unified screens and screen items live as ILScreenData/ILScreenItemData (DL.ModernUI), not as Classic GraphX types. | U4/U5 | 2026-10-20
H-022 → confirmed 2026-10-20 (for saving): IM\HMI and RtIdFiles stay identical after saving, tag names only in the plf (hmi-tag.md). Relation to compiling untested.
H-025 | open | IM\HMI files (RDF) are only filled with tags/screens/alarms on compiling and are derived. | Sample with tag, then compile and compare IM\HMI | 2026-10-20
H-026 | open | TypeSafeAddress (28-B blob) in IAdditionalAttributes encodes type ID/address of the PLC tag (`0810c772`, `52000000`). | Samples with other PLC data type/address | 2026-10-20
H-027 | open | Alarms carry the name in ICoreAttributes.Subtype (IsShell=true) instead of in Name; does this hold for all alarm types and other Hmi objects (screens, screen items)? | Check U4/U5 | 2026-10-20
H-028 | open | Multilingual alarm texts: blob has one LCID entry per language, text UTF-8 plain text (format as in 108). | Sample with a second project language (must be created by hand) | 2026-10-20
H-L02 → confirmed 2026-10-20: types/versions = TypeObjectBase/VersionObjectBase derivations (140a/140b, 1407/1408 in Simatic.Lang); status in ICoreLibObjectAttributes.State of the version (library.md).
H-L1-1 | open | Release = copy: only release freezes the content as a subtree (Environment → version, shell device) under the version; writing would need the same subtree plus interface root _BIVE:<Name>/<VersionGuid>. | L02, L06, L07, L09 (11–14 objects), L01/L05 none | 2026-10-20
H-L1-2 | open | VersionGuid is reassigned on release, the copy keeps the InWork GUID in its name; whether the old GUID is referenced elsewhere is open. | L01 → L02 | 2026-10-20
H-L1-3 | open | typeVersionEdit creates a new version object, the old version stays Committed and only changes links/ModifiedTime. | L03 | 2026-10-20
H-L1-4 → confirmed 2026-10-20: type status is derived (InWork if a version is InWork), not stored.
H-023 → confirmed 2026-10-20: expando block < 8 bytes = empty; fix in attrs.decode_expando (commit 69b535a), tests green.
H-027 → rejected 2026-10-20: alarms have ICoreAttributes.Name (Subtype empty, IsShell false); the finding from U3 was a decoding error in Metamodel.ancestors (24 types affected, hmi-alarm.md).
H-024 → partly confirmed 2026-10-20: Unified screens are ILScreenData 0x00cd1002 (hmi-screen.md); screen items still open.
H-029 | open | Unified properties are stored in the expando as a numeric property ID (length prefix + number), only changed values; full ID table derivable from the metamodel. | U5 | 2026-10-20
H-020 → decided 2026-10-20: slot order = ordinal-alphabetical by qualified set name (924/924 disputed slots in the Unified samples clean, by declaration 768 errors; ordinal, not case-insensitive). Parser recommendation: attribute_sets_ordered = sorted(sets).
H-L2-1 | open | Structure and array values start with a u32 LE total length (explains the "3-byte prefix" of VersionData); array = count + offsets + elements in set-block format. Parser truncates values ≥ 128 B incorrectly (e.g. LibParameter in 030/036). | struct_len_check (54 combinations < 128 B fit) | 2026-10-20
H-L2-2 | open | CoreObjectData contains only the clipboard roots (here 1 × SelectedObject: length 22, role 1, ParentObjectId −1, SourceObjectId 0); soc/eoc/poc/voc count the roles. | 8 master copies, multiple selection missing | 2026-10-20
H-L2-3 | open | Referenced objects (UDT, called blocks) go into the master copy as name stubs (only ICoreAttributes, TRefParent ↔ TextualReferences), UDTs additionally with a frozen interface root _BIVE:…; instantiating creates the same stub. | L10–L13, L15 | 2026-10-20
H-L2-4 | open | The device context of the master copy is a generic CPU (CPU_15XX_Unspecific, version 17.0.0.0), independent of the original. | 021, L10, L11, L15 | 2026-10-20
H-L2-5 → confirmed 2026-10-20: scope ID and SourceObjectID = {type ID decimal-OID decimal} of the source object ({135208-1075} = ProjectData 433, {1052700-2067} = S7ControllerTargetData 813); projectId = session GUID.
H-L2-6 | open | Instantiating leaves no trace of origin; GUIDs are reassigned, an existing UDT interface is duplicated. | L13 | 2026-10-20
H-F01 | open | Unified dynamizations (tag, SingleBit, range, flashing, script) are not separate records but entries in IPropertyDynamicExpandoAttributes of the screen item; script text lives in its own HmiScriptEmbeddedData under one HmiScriptModuleContainerData per screen. Events appear as IAdditionalAttributes keys (Tapped, Down, Loaded), property event Change not found yet. | F01 vs. 103 | 2026-10-20
H-024 → confirmed 2026-10-20: all Unified screen items are ILScreenItemData 0x00cd1003, type in Subtype (hmi-screenitem.md).
H-029 → partly confirmed 2026-10-20: object properties as expando with numeric property ID, only deviations from the default; evidenced 5 Width, 6 Height, 99 Left, 102 BackColor.
H-030 | open | The script text lives in IAdditionalAttributes of HmiScriptEmbeddedData in its own blob format (plain text UTF-8, without used/alloc header). | Determine layout on sample 123 (U6) | 2026-10-20
H-029 → largely confirmed 2026-10-20: 20 property IDs decoded, 76 % of the stored values (hmi-properties.md). H-030 → partly: script code is stored as a token table in HmiScriptEmbeddedData, parser assembles it; whitespace still open.
H-022 → confirmed 2026-10-20 (U8): IM\HMI and RtIdFiles are created when the device is created and are irrelevant for all engineering content (tags, alarms, screens, objects, scripts); the parser does not need them. H-025 remains open/deferred (compiling needs a runtime password that Openness cannot set).
H-025 → deferred 2026-10-20: IM\HMI are derived; format only with the task "panel runtime file" (hmi-runtime-files.md).
H-L4-1 | open | Version status only as a link (IsInstanceOf/IsTypeOf, CurrentLibraryInterface → _BIVE:<Name>/<VersionGuid>, expando UserTypeVersionGUID); "outdated" derived, not stored. | L16–L21 | 2026-10-20
H-L4-2 | open | Discard = delete version+instance and re-instantiate the instance from the previous version; UpdateProject presumably the same. | L16, L19, L21 | 2026-10-20
H-L4-3 | open | InWork number = highest + 0.0.1; release links only the edit instance; context device of the copy = its device. | L03, L17–L19 | 2026-10-20
H-L4-4 | open | `_` prefix of the interface root ⇔ IsConstant. | 1591/1591 roots | 2026-10-20
H-L4-5 | open | Tombstones also for objects that were never persisted. | L16 (6), L19 (3), L21 (12) | 2026-10-20
H-F02 | confirmed (F05, 2026-10-21) | IFaceplateInterface.InterfaceEvents/InterfaceProperties/TagInterfaceProperties are arrays of pe:MapT (format: u32 length, n, value offset, n type codes, n keys, values); interface tags point to HMI tags in the table of the hidden FaceplateDevice. | F05 (tag interface); F16/F18 for properties/events | 2026-10-21
H-F03 | confirmed/refined (F05, F40, 2026-10-22): container → version directly; mediator is not an instance part but a script reference to a type (popup) | Faceplate instance: ILFaceplateContainerData carries IsInstanceOf → FaceplateLibraryVersionData directly (no mediator), mediator only for interface binding. | F05; mediator for interconnection still F26 | 2026-10-21
H-030 → confirmed 2026-10-20: script code as a token table incl. whitespace/comment tokens, exactly reconstructible (sample 130).
H-031 | confirmed (modified) | The number in Tags("…") is not a tag ID but the name of an entry in IScriptLinkExpandoAttributes; its value (u32 BE) indexes the ExpandoReferences of the script (0 = object, 1.. = tags). 10/10 F01 scripts. | U11 | 2026-10-20
H-L6-1 | open | Global library = project format; root LibraryData (GlobalLib) at OID 0x433, .al21 XML like .ap21. | L30–L36 | 2026-10-21
H-L6-2 | open | Types move between libraries with new OIDs, same Type/VersionGuids; OriginalLibrary = source library. | L31, L33, L34 | 2026-10-21
H-L6-3 | open | Typed objects in master copies keep IsInstanceOf → version of the same library. | L32, L34 | 2026-10-21
H-HW-01 | confirmed (with restriction) | Module parameters (CPU cycle time, clock memory, startup …) are stored only when deviating from the hardware catalog, as an expando attribute on the DeviceItemData; the default values are not in the project file. | 200–204 against 002_cpu: per change exactly one new expando (for 201/203 two resp. three related ones) with the Openness value, name differing (`MaxCycleConfig`, `PowerOnAction` on the CPUexecUnit, `UseClockFlags`/`ClockFlags_Local`, `ProtectionLevel` on the CpuCommon, `WebServerActive`, `EnableWebServerAccess4IE`); default values (150 ms, startup 3, NoAccess, 13 time-of-day parameters, 11 web server parameters) are missing from the file. Restriction: TIA writes some default values already on creation (`PnDnsConfigNameResolve` 0, `UseFixSysDiagAlarmIds`). Evidence docs\spec\hw-config.md, test_hardware | 2026-10-24
H-HW-02 | partly confirmed, partly refuted | `AddressMap` (AddressManagerV2Data) and `IAddressAttributes.AM_x_AddrData_y` (DeviceItemData) encode the I/O address ranges. | `AM_x_AddrData_y` confirmed: I/O kind, length in bits, process image partition, start address in bits (205, 206, 208, 209 against Openness addressList/OB30). `AddressMap` refuted: it contains no addresses (206 does not change it) but the HW identifiers of all items via the index in RegisteredDeviceItems (209: 17 = Openness hwIdentifierList). Evidence docs\spec\hw-config.md, test_hardware | 2026-10-24
H-HW-03 | open | Openness lists only part of the DeviceItems (not CpuCommon, CpuProxy, CPUexecUnit, LicenseManager, Internal.IOController, virtual interface); the controlling attribute is unknown (DeviceItemType, UICapabilities, BrowsingParameters do not separate). | Comparison with AdditionalInfos/PersistentExpandoAttributeFlags, more devices | 2026-10-24
H-HW-04 | open | Automatically generated names are not updated in the file: `PnNameOfStation` with `PnPnNoSAutoGenerate` True is the state at creation (IO_1: "io device_1", Openness "im_1"), and Openness builds display names of submodules itself (`_1` → `IM_1`). | 214–217; 229 (GSD device_3: file "sew-eurodrive-uhx45", Openness "sew-eurodrive-uhx45_1"); counter-check: rename station, set name without automatic | 2026-10-24
H-HW-05 | confirmed | GSD devices reference the GSDML via `ConfigObjectTypeName` `GSD\<gsdml>\<kind>_<id>`, which TIA copies to `AdditionalFiles\GSD`; from this follows the Openness type identifier `GSD:<FILE>/<R|DAP|M|SM>/<id>` (`_SPA_` = space, `_DOT_` = dot). | 229: all R/DAP/M/SM items of the four devices = Openness; interfaces/ports only via the GSDML. Evidence docs\spec\hw-config.md, test_hardware | 2026-10-25
H-HW-06 | open | UMAC attributes `…Encrypted` are only encrypted if the project is protected with encryption; otherwise the data is in plain text after a 24-byte header (byte 19/20 = 1). | 240–242: user name, role name, comments and timeout in plain text. Counter-check: project with project protection and password | 2026-10-25
H-034 | confirmed | STL networks live as source in the plf: CompileUnitData with ProgrammingLanguage STL, blob <Statements> with Statement/Token(Kw, DispName, NumBLs)/OpdAccess(RefId -> ident table)/Label/LC; the text is reconstructible identically to the .awl source export. | Samples 061, 062; ProjectTests.StlAgainstSource 33/33 | 2026-11-06
H-035 | confirmed | Blocks with many networks store Sources and CoreObject2IdentContainer as untagged links (relation implicit through the typed link list); the parser takes untagged links to CompileUnitData/IdentContainerData/IdentPartData. | Sample 064 FB_StlMath (69 networks, 70 untagged links) | 2026-11-07
H-036 | confirmed | BlockInterfaceInfo of the ident table carries the parameters under BIID/BPIL/BPI; ParaExpression@FPNum (STL) counts this list from 1. | Sample 064 FB_StlCall, StlAgainstSource | 2026-11-07
Resolutions 2026-11-20 (review of the open entries against the spec documents; only explicit evidence is used, LOG.md 2026-11-20):
H-003 → confirmed 2026-11-20 (extent clarified): blob kind 1 = zlib pages, 1324/1324 BlobT/XmlT/expando blobs of all samples decode to the stated size (record-payload.md, Addendum 5a); metamodel 0000:0007:1 zlib XML (plf-container.md); kind 4 store pages 147/147 (record-payload.md, Addendum 2026-10-30).
H-019 → partly confirmed 2026-11-20: expando="simple" decoded (db-udt.md, "now decoded (part of H-019)"); struct/array values with u32 length (LOG 2026-10-20, fix 4e43505). Open: blob AdditionalInfos (hw-config.md, open points).
H-026 (PLC, OB Main comments with key 65535) → partly confirmed 2026-11-20: LCID 0xFFFF = without language (record-payload.md, evidence Main comment 0904/ffff). Open: not checked against an Openness truth (block-interface.parser.md).
H-033 → confirmed 2026-11-20: the OB Main of the template has an empty network, header without elements (block-code.parser.md; block-code.md still says "not investigated").
H-028 (HMI, multilingual alarm texts) → partly confirmed 2026-11-20: a second language adds one LCID entry per HmiTextData (hmi-languages.md, sample 153, 97 HmiTextData get an entry for 1031); alarm texts are HmiTextData (hmi-alarm.md). Open: an alarm text with filled de-DE content.
H-F01 → largely confirmed 2026-11-20: dynamizations are entries of `_#Dynamics` / IPropertyDynamicExpandoAttributes of the screen item (hmi-screenitem.md, Addendum U10); a `_#Dynamics` name without a dynamic entry belongs to a property event (unified-properties.md, F40). Open: detailed layout of the Change event.
H-L01 → confirmed 2026-11-20: the Siemens.Simatic.Hmi.* packages (42 → 100) appear in the metamodel only with the HMI device (hmi-unified-container.md, U1; unified-metamodel.md).
H-L03 → rejected for global libraries .al21 as well 2026-11-20: a global library has the project format with live objects (library.md, L6: "Format = project format", device master copy LibraryMasterCopyData Subtype Hardware).
H-L04 → confirmed 2026-11-20: Subtype IECPL for PLC master copies (library.md, L2), Hardware for device master copies (library.md, L6).
H-L1-1 → confirmed 2026-11-20: release = copy, the Committed version has VisibleParts → copy of the object, 11–14 objects (library.md, L1).
H-L1-2 → partly confirmed 2026-11-20: VersionGuid is reassigned on release, the copy name keeps the GUID of the InWork period (library.md, L1). Open: further references of the old GUID.
H-L2-1 → partly confirmed 2026-11-20: structure/array values with u32 length (hmi-faceplate.md; LOG 2026-10-20, fix 4e43505).
H-L2-2 → partly confirmed 2026-11-20: CoreObjectData layout with 1 × SelectedObject decoded (library.md, L2). Open: multiple selection (not creatable via MCP).
H-L2-3 → confirmed 2026-11-20: called blocks and UDTs go in as name stubs, the UDT additionally with interface root `_BIVE:…`; instantiating creates the stub again (library.md, L2).
H-L2-4 → confirmed 2026-11-20: device context = copy of a generic CPU CPU_15XX_Unspecific (library.md, L2).
H-L2-6 → confirmed 2026-11-20: instantiating leaves no trace of origin, new GUIDs, an existing UDT gets a second interface root with the same name (library.md, L13).
H-L4-1 → confirmed 2026-11-20: "Outdated" is not stored but derived (library.md, L4).
H-L4-2 → confirmed 2026-11-20: discard (library.md, L4) and updateProject (library.md, Addendum L4 2026-10-21: "H-L4-2 confirmed") = re-instantiation.
H-L4-3 → confirmed 2026-11-20: release links only the edit instance, context device = its device, InWork number = highest + 0.0.1 (library.md, L4).
H-L4-4 → confirmed and refined 2026-11-20: no prefix = not constant, `_` = constant, `x` = constant root of a replaced instance (library.md, L4 and Addendum 2026-10-21, 3302 roots).
H-L4-5 → confirmed 2026-11-20: discard writes tombstones also for objects never persisted (library.md, L4).
H-L6-1 → confirmed 2026-11-20: global library = project format, root LibraryData GlobalLib at OID 0x433 (library.md, L6).
H-L6-2 → confirmed 2026-11-20: new OIDs, same TypeGuid/VersionGuid, OriginalLibrary = source library (library.md, L6).
H-L6-3 → confirmed 2026-11-20: typed objects in master copies keep IsInstanceOf → version of the same library (library.md, L6).
H-HW-04 → partly confirmed 2026-11-20: PnNameOfStation with automatic assignment is an outdated value, Openness forms the name from the current names (hw-config.md, sample 063; checked on 23/23 interfaces, LOG 2026-11-08). Open: display names of submodules (`_1` → `IM_1`).
