# Code of blocks (draft)

Status: samples 023, 026, 027, 028 (TIA V21 Update 3). Implementation: `parser\openpedata\code.py`, test:
`parser\tests\test_samples.py` (`check_interfaces`, network part). The program lives in the plf as **source**; the
compiled program is separate from it and is not read.

## Objects

```
Block (CodeBlockData) --Sources--> CompileUnitData                 (one network per object; SCL: one object for the source text)
   CompileUnitData: ICoreAttributes.Comment      = network title (CoreText blob, per culture)
                    --CompileUnitComment--> CoreText             = network comment (ICoreTextRepository.Text, same layout)
                    ICompileUnitData.ProgrammingLanguage         = SCL | LAD_CLASSIC | FBD_CLASSIC
                    ICompileUnitData.RefID                       = network number in the block (2, 3, ...; 1 is the block)
                    ICompileUnitData.Data                        = blob with the network source text (XML, see below)
                    --CompileUnitExtensionDataBags--> StandardCompileUnitDataBagData
                         Data = <Expando><S7_NetworkTitle>MLC_N1T</S7_NetworkTitle><S7_NetworkComment>…</…></Expando>
                         (text references MLC_* of the SD source; the texts themselves are in Title/CompileUnitComment)
Block --CodeBlockExtensionDataBags--> StandardCodeBlockDataBagData      Data = <Expando><M52>MLC_start</M52>…</Expando>
                                                                         (MLC references of the interface comments, M<Member-ID>)
Block --CoreObject2IdentContainer--> IdentContainerData --IdentParts--> IdentPartData
   IIdentPartData.PayLoad = <IdentXmlPart> with <InterfaceAccess> per operand (see below)
Block --LoadableBinaries--> LoadablePlusCodeBlockData (+ LoadableDependentData): compiled program (MC7plus), not read
```

`Data` and `PayLoad` are blobs in the known page format (`attrs.decode_blob`, optionally zlib); the content is
UTF-8 with BOM. The order of the networks in the block corresponds to the order of the Sources links (and the
RefID); reordering/inserting in the middle was not investigated.

## SCL: token tree

`<SCLSource Version="3.4.0.0">` contains `<Symbols>` (references to tags, constants, called blocks with
SymID/RefId) and `<RootStatements>` – a lossless token tree of the editor. The source text results from
outputting the leaves in document order:

| Element | Text |
| --- | --- |
| `LC TE=" …"` | `//` + TE (line comment) |
| `NL` | line break |
| `BL [NumBLs=n]` | n spaces (default 1); indentation is stored explicitly as `BL NumBLs` after `NL` |
| `SymVa`, `SymPa`, `Sub` | attribute `ODN` (`#tmp`, `"FB_Iface_DB"`, formal parameter `iBool`) |
| `Const TE=…` | TE (literal, text only; type in `SI`/`Result`, e.g. ConstLInt) |
| `OpAs` `OpPl` `OpG` `OpL` | `:=` `+` `>` `<` |
| `KwTHEN/ELSIF/ELSE/DO/TO` | keyword of the same name; `KwENDIF` = `END_IF`, `KwENDFOR` = `END_FOR` |
| `FiSt`, `Comma`, `BracO`, `BracC` | `;` `,` `(` `)` |
| `Statement TE="IF"/"FOR"` | leading keyword of the statement (TE) |

Skeleton: `Statement SI=STAss|IFEIE|STFOR|STSub`, `Expression Result=… SI=ExprAdd|ExprEquG|ExprPrimD|ExprPrimC`, `Fold` (collapsible
region), `Statements`, `InstCa` (call with instance DB), `Param SI=ParaIn|ParaInOut`. The test evidences the text for exactly these
tokens; other operators/keywords (-, *, /, AND, OR, NOT, WHILE, CASE, …) are **not verified**, the parser outputs them as
`<Tag>` and lists them under `unknownTokens`.

## LAD/FBD: FlgNet

`<FlgNet xmlns="http://www.siemens.com/automation/2015/FunctionLadderDiagram" Version="14.0.0.2" Lang="LAD_CLASSIC|FBD_CLASSIC" Routed="true">`
(different namespace than the SimaticML export `…/Openness/SW/NetworkSource/FlgNet/v5`):

- `<Part UId Gate="Contact|Coil|Add|…" [DisableENO="true"]>` with `<TemplateValue Name Type>` (Card, SrcType); SimaticML calls this
  `Name` and `DisabledENO`.
- `<ORef UId RefId>`: operand (instead of `Access` in SimaticML); **without** RefId = open connection (SimaticML `OpenCon`, e.g. EN of the Add box in FBD).
- `<Wire UId>` with two or more ends: `Powerrail`, `OCon UId` (operand reference), `PCon UId PinName` (pin of a part).
  SimaticML: `IdentCon`, `NameCon`.
- UIds are per network (from 21, parts, operands and wires counted together); the order of the parts is not the same as in the export
  (FBD: Part before ORef; SimaticML: Access before Part), the test therefore compares a canonical netlist (part name + index,
  operand name, pin).

### Operands (IdentPartData)

`ORef RefId=r` references `<InterfaceAccess><ID N="" S="Local" RID="r" IS="1"><CS><C NID=n UID=u [OID] [AK="Write"]/></CS></ID>`,
where `NID` is the RefID of the network (`ICompileUnitData.RefID`) and `UID` the UId of the ORef; `AK` is missing for read access.
`<SSD S="I|O" AO="0" MID="52"><AOS><AO N="start" BO BS><TD T="Bool:33554433:Bool"/>…` gives section (I Input, O Output; other abbreviations
not seen), member ID in the block, names (nested `AO` = structure path) and type (`Name:DataTypeID:Name`).

## SimaticML output (C8)

As of 2026-11-07. `Code.SimaticMl.cs` converts the internal `FlgNet` with the ident table into the `<FlgNet>` of the Openness export
(`…/Openness/SW/NetworkSource/FlgNet/v5`). Evidenced with all 1512 LAD/FBD networks of the truth exports (test
`SimaticMlTests.LadFbdAgainstExport`, elements, attribute order and values equal).

- **UIds** are reassigned, from 21: label declarations (`Labels/LabelDeclaration`), operands (`Access`, in the
  order of the `ORef`), parts (`Part`/`Call`, each directly followed by its `Instance`), open connections
  (`ORef` without RefId → `OpenCon`), then the wires.
- **Access scope** from the ident kind: InterfaceAccess → LocalVariable; GlobalAccess and tag (SimpleAccess with name) →
  GlobalVariable; SimpleAccess without name → Address (`Area`, `Type`, `BitOffset`); LiteralConstant → LiteralConstant
  with `ConstantType`, but TypedConstant without type if the literal text contains a `#` (`T#5S`, `S5T#2S`, `DINT#5`);
  LocalConstant/GlobalConstant → `Constant Name`; Label → `Label Name`. Array index: `Component AccessModifier="Array"`
  with inner `Access` without UId; slice `SliceAccessModifier`; peripheral access of a tag (`SAD@SAM=Peripherie`) →
  `SimpleAccessModifier="Periphery"`.
- **Instruction version** (`Part Version`): `MajorVersion.MinorVersion` of the `IECPLInstructionData` whose
  `ICoreTypeObjectAttributes.VersionId` equals `INSTD@V` of the ident entry (219 instructions, all unique).
- **TemplateValue** in the order Cardinality, Operation, Type; operations in upper case (plf `Eq`, export `EQ`).
  `Equation` (CALCULATE) comes before the TemplateValues, `AutomaticTyped` and `Negated` after them.
- **DisabledENO** as in the plf (`DisableENO`); additionally `false` for CALCULATE and for MIN, MAX, LIMIT, SCATTER (observed,
  without a distinguishing attribute in the plf).
- **Call**: `CallInfo Name BlockType`, `Instance` (instance DB GlobalVariable, multi-instance LocalVariable), `Parameter` with
  section from the ident table and data type from the interface of the called block.
- Export name differs: `Scale` → `Scale_X`.

## Open

- Operands other than local tags (tags, DB accesses, constants, addresses), section abbreviations for InOut/Static/Temp/Constant in the ident part.
- Further LAD/FBD parts (branches, timers/counters, calls of FC/FB, comparators, edge contacts), multiple wiring (wire with >2 ends).
- Inserting/reordering networks in the middle; networks with comments in several languages.
- SCL: further operators, keywords, regions, strings, multi-line comments `(* *)`, CASE/WHILE/REPEAT, access to tags/DB elements.
- The Symbols table of the SCL tree (`LocalTag SymID RefId`, `DBBlock`, `BlockParameter ONr`, `LiteralConst Typ RefCount`) is not evaluated.
- The OB `Main` of the template (LAD) has an empty network (only header without elements; `empty: true`, `error: no network source` only for a missing source with content).

## LAD/FBD in detail (status 5g, samples 031 to 034)

The older version above described only Contact/Coil/Add. The reader (`code.py`) now reads all elements that occur in the samples;
every element and attribute it does not evaluate turns the test red (inventory tables in `parser\tests\RESULTS.md`).

### Mapping SimaticML schema (SW.PlcBlocks.LADFBD_v5.xsd, Access_v5.xsd) to the internal FlgNet

| SimaticML (Openness) | internal (plf `ICompileUnitData.Data`, `FlgNet`) | Status |
| --- | --- | --- |
| `Parts/Part Name=…` (contacts, coils, logic, boxes) | `Part Gate=…` (only simple gates: Contact, Coil, A, O, X, Jump, Move, Add, …) | observed |
| `Part` of an instruction with `Version`, `TemplateValue`, `DisabledENO`, `Instance` | `LRef RefId EN ENO` → ident `Instruction` (ID@N = name, INSTD/IUS/IU/TV = template values per usage, LPI/LP = pins) + child `Instance RefId` → `MultInstAccess` | observed |
| `Call` with `CallInfo` (Name, BlockType, Parameter, Instance) | `CRef RefId` → ident `BlockInterfaceInfo` (BPIL/BPI parameters) + children `CodeBlock RefId` (→ `FCBlock`/`FBBlock`) and `Instance RefId` (→ `AufDBBlock` or `MultInstAccess`) | observed |
| `Access Scope=LocalVariable` + `Symbol/Component` | `ORef RefId` → `InterfaceAccess` (SSD@S section, @MID, AOS/AO path) | observed |
| `Access Scope=GlobalVariable` (tag) | `ORef` → `SimpleAccess` (ID@N tag name, SAD@R area) | observed |
| `Access Scope=GlobalVariable` (DB member, `DB.a.b`) | `ORef` → `GlobalAccess` (AOS/AO: DB, member…; BAD@BIRID) | observed |
| `Access Scope=LocalConstant` / `GlobalConstant` / `LiteralConstant` + `Constant/ConstantValue` | `ORef` → `LocalConstant` / `GlobalConstant` / `LiteralConstant` (CB@T type, @SV literal, @V value) | observed |
| `Component AccessModifier=Array` + `Access` (index) | `AO@RIDI` = RID(s) of the index operand (literal or tag), several separated by commas | observed |
| `Component SliceAccessModifier=x3` | `SSD@AM=x3` | observed |
| `Access Scope=Label` | `ORef` → `Label` (ID@N, C@AK Jump \| Definition) | observed (Jump) |
| `Labels/LabelDeclaration/Label` | `FlgNet/Labels/ORef RefId` (definition) | observed |
| `Part/Equation` (Calculate) | `Part/Equation Valid` (text with IN1…) + `Expression` in the ident part | observed |
| `Negated Name=pin` | `Part/Negated PinName` | observed |
| `AutomaticTyped` | `Part/AutomaticTyped Name` | observed |
| `Wire` with `Powerrail` / `IdentCon` / `NameCon` / `OpenCon` | `Wire` with `Powerrail` / `OCon UId` (operand ORef) / `PCon UId PinName` / (ORef without RefId = open) | observed; wires with more than two ends (branch) ok |
| `Access Scope=Address` (absolute address, `Address Area/Type/BitOffset`) | `ORef` → `SimpleAccess` with empty `ID@N` (with a tag at the address: tag name), `SSD@AO` = bit offset (byte × 8 + bit), `SAD@R` = `Memory` \| `Input` \| `Output` \| `Timer` \| `Counter`, `SAD@SAM=Peripherie` (`:P`), `OD@S` = size in bits; Timer/Counter: `AO` = number. Text: `%M10.3`, `%MW2`, `%IW0:P`, `T1`, `C5` (`code.address_text`) | observed (037_lad_xml: FB_LadScopeAddr, S5 coils) |
| `Part DisabledENO` for boxes with instruction reference | `LRef@DisableENO` | observed |
| `Openbranch` | – | not observed; import attempt (branch with open end) so far without an accepted structure, see `tools/make_scope_xml.py` |
| `Invisible` (invisible pins) | – | not observed; TIA: "Invisible pins are only permitted for user calls and LRefs, not for built-ins", with a call of a user FC no valid structure so far |
| `Comment` on a part | – | not observed; TIA: "The comment is not supported at this position" (contact) |
| `Access Scope=AddressConstant / AlarmConstant / NamedValueConstant / PredefinedVariable / Reference / ReferenceToArray / Statusword / Expression / Indirect` | – | not observed (according to the schema only SCL or STL; not creatable in LAD/FBD) |

### Instructions that SD does not create: SimaticML import with names from the Siemens DLLs

The SimaticML `Part` names are the internal names of the instructions. They are in `Siemens.Simatic.Lang.FLDiagram.dll` and `Siemens.Simatic.Lang.PLInterface.dll` (name tables; coil enum `SpCoil, SdCoil, SeCoil, SsCoil, SfCoil, ScCoil, CuCoil, CdCoil, PtCoil, ResetIECTimerCoil, TriggerCoil, NCoil, PCoil, RCoil, SCoil …`). `tools/xml_namecheck.py` checks each name against the importer ("An instruction with the name X cannot be found" = unknown). Confirmed (import and compile): `SpCoil, SeCoil, SdCoil, SsCoil, SfCoil, CuCoil, CdCoil, PtCoil, ResetIECTimerCoil, SBitfield, RBitfield, BLKMOV, UBLKMOV, FILL, Inc, Dec, Deco, Enco, Serialize, Deserialize, FieldRead, FieldWrite, GATHER, SCATTER_BLK, Calc, JmpList, Switch, Jump, JumpNot, Ok, DRUM…`; **S5 boxes and special cases** (SD and SimaticML know them under the names of the gates, not the catalog heading): `S_PULSE=Sp, S_PEXT=Se, S_ODT=Sd, S_ODTS=Ss, S_OFFDT=Sf` (pins `timer s tv r rt rtbcd q`), `S_CU=Cu, S_CD=Cd, S_CUD=Cud` (`counter s pv r cv cvbcd q`, for `cud` additionally `cd`), `NOT_OK=nok` (gate `NOKGate`), `MOVE_BLK/UMOVE_BLK=MoveBlockI/MoveBlockU`, `FILL_BLK/UFILL_BLK=FillBlockI/FillBlockU` (pin `OUT` is an array element, e.g. `arr[0]`; `BLKMOV/UBLKMOV/FILL` are separate, older instructions) and `T_COMP/S_COMP=T_Comp/S_Comp` (SimaticML with TemplateValues `date_type`/`src_type` and `relation` (operation, e.g. `EQ`); `en` on the power flow, `IN1`/`IN2` tags, Boolean `OUT` wired directly to the tag, without `DisabledENO`). In LAD the pin `S`/`CU` of the S5 boxes is the power flow (a contact before it, `S := #x` yields "Operand is not valid for this instruction"), in FBD the first operand (`RUNG #a`). `AssignmentAttempt` (`?=`) needs in SD the pragma line `{ S7_Templates := "[src_type := Variant, dest_type := Int]" }` and the pins `IN`/`OUT` (target is a `Ref_To` tag). In FBD `ok( operand := #x )` plus `Coil( #q )` is the form of the check. The message "Block 'X' is not supported by the CPU or library version" is triggered not by the firmware but by the structure (wiring, version, ENO); `tools/audit_results.py` checks that the exported block really contains the instruction of the catalog entry. For S5 coils: operand `Address Area=Timer/Counter`, time value as `TypedConstant S5T#2S`, a contact before it (otherwise "A preceding Boolean logic operation is missing"); `DisabledENO` only where TIA allows it ("ENO cannot be deactivated"). `tools/gen_xml.py` repairs pins, template values (type candidates per name), tag types (additional InOut tags) and array/DB operands from the messages of TIA.

Internal and exported instruction names differ slightly (`Scale` ↔ `Scale_X`); the test records the difference in `INSTRUCTION_EXPORT_NAME`.

### Typed creation of the catalog instructions (`tools/gen_typed.py`)

One FB per instruction, whose interface contains per pin a tag with the data type from the parameter table of the help; the call uses these tags. From TIA's messages the generator repairs: input → InOut ("declared as an input, write access not permitted"), types ("data type A of the actual parameter does not match the data type B"), Variant/Any only in InOut, constant pins, system types. **FB instructions** (TON, TCON, TSEND_C, MB_CLIENT …) need a **static instance tag** (`#inst.NAME(…)`; without it TIA only reports "The operand required at the input or output is missing or has an incorrect data type"). In **SCL** they are function calls (`#ret := ABS(#x)`, positional or named, return value depending on the instruction) or calls on an instance (`TP_TIME`, `CTU_INT`, `R_TRIG`). The communication instructions are named differently from the catalog headings and are only partly available without hardware: sample `038_comm_hw` (S7-1513 + CM PtP RS422/485 HF) with `Modbus_Comm_Load/Master/Slave`, `Port_Config`, `Send_Config`, `Receive_Config`, `Send_P2P`, `Receive_P2P`, `Receive_Reset`, `Signal_Get/Set`, `USS_Port_Scan`, `USS_Drive_Control`, `USS_Read_Param`, `USS_Write_Param`, `MB_CLIENT/SERVER`, `TMAIL_C`. The message "Block 'X' is not supported by the CPU or library version" does **not** depend on the firmware (same with CPU 1516-3 V4.1, sample 039_fw41) but on the structure (instruction version, instance): the SimaticML route resolves it via `Version="1.0"` and instance.

Sample mapping: 031/032 (SD, LAD/FBD), 034 (Jump/Label/Calculate via SimaticML), 037_lad_xml (SimaticML import and typed extended instructions, CPU 1513 V2.9), 038_comm_hw (hardware CM PtP), 039_fw41 (CPU 1516-3 V4.1: further extended instructions, FBD variants, SCL calls, S5 boxes), 040_s71200 (CPU 1214C DC/DC/DC V4.7: `CTRL_PWM` with the system constant `Local~Pulse_1` (Hw_Pwm)). Hand-written rungs: `tools/gen_lad_manual.py` (LAD/FBD SD), `tools/gen_xml_manual.py` (SimaticML), `tools/gen_scl_manual.py` (SCL).

### Instruction catalog

Source: table of contents of the installed Siemens help (`tools\instr_catalog.py`, `docs\catalog\*.txt|json`). The matrix
`docs\spec\instruction-coverage.md` is generated by `tools\lad_matrix.py` from the result files (never by hand). Creation of the instructions:
`tools\gen_lad.py` (SD text with error-feedback candidate search) and `tools\gen_xml.py` (SimaticML import with pin detection);
`tools\lad_probe.py` / `lad_xprobe.py` check whether TIA knows a name as an instruction.
