# Block code (SCL, LAD, FBD, STL)

Status: confirmed on samples 026_fc_scl, 027_fc_lad, 028_fc_fbd and 023 (5d, 2026-10-20), parser `parser\openpedata\code.py` (commit 1042d9a on the user's machine). Hypotheses H-029 to H-033.

## Location
The code lives as **source** in the plf, separate from the compiled program.
- Block --Sources--> `CompileUnitData`, one per network (SCL: one for the whole source text). `ICompileUnitData`: `ProgrammingLanguage` (SCL | LAD_CLASSIC | FBD_CLASSIC | STL), `RefID` (network number, from 2), `Data` = blob (page format as elsewhere, UTF-8 with BOM, XML).
- Network title = `ICoreAttributes.Comment` of the CompileUnitData (CoreText layout, per culture). Network comment = link `CompileUnitComment` → CoreText (`ICoreTextRepository.Text`, same layout; language 65535 empty).
- Additionally `StandardCompileUnitDataBagData` (expando `S7_NetworkTitle`/`S7_NetworkComment` = MLC references) and `StandardCodeBlockDataBagData` (expando `M<Member-ID>` = MLC references of the interface comments).
- The compiled program lives in `LoadablePlusCodeBlockData` / `LoadableDependentData` (MC7plus binary), not read.

## SCL
`<SCLSource Version="3.4.0.0">` with `<Symbols>` and `<RootStatements>`: lossless token tree of the editor (LC line comment, NL line break, BL blanks with NumBLs, SymVa/SymPa/Sub@ODN, Const@TE, Op*, Kw*, FiSt, Comma, BracO/BracC, Statement@TE for IF/FOR, Fold, InstCa/Param). The source text can be reconstructed from it and is identical to the get text of TIA and the body of the SD export (indentation, blank lines, `//` comments). The indentation is stored as NL + BL NumBLs in the tree. The `<Symbols>` table (SymID/RefId, types, RefCount) is not evaluated.

## LAD/FBD
`<FlgNet xmlns=…/2015/FunctionLadderDiagram Version=14.0.0.2 Lang=… Routed=true>` with `<Parts>` (Part@Gate, DisableENO, TemplateValue; ORef@RefId = operand; ORef without RefId = open connection, e.g. EN of the Add box in FBD) and `<Wires>` (Powerrail, OCon@UId, PCon@UId + PinName). Differences to SimaticML: different namespace, Part@Gate instead of Name, ORef instead of Access, UIds and part order differ (FBD: Part before ORef).

Operand names are not in the FlgNet but in the IdentContainer: Block --CoreObject2IdentContainer--> `IdentContainerData` --IdentParts--> `IdentPartData`, payload `<IdentXmlPart><InterfaceAccess><ID S=Local RID=r><CS><C NID=<RefID of the network> UID=<UId of the ORef> [AK=Write]/>…<SSD S=I|O MID=<Member-ID>><AOS><AO N=Name><TD T=Bool:33554433:Bool/>`. Mapping: ORef@RefId = ID@RID, NID = ICompileUnitData.RefID.

## Parser output
Per network: `title`, `comment`, `parts [{name, templateValues, disableEno}]`, operands `[{name, section, memberId, access}]`, `wires [{ends: powerrail | operand name | open | part+pin}]`; for SCL the source text. Unrecognised SCL tokens appear as `<Tag>` and under `unknownTokens` (no guessing).

## Check
SCL against get JSON (detail=full) and SD export; LAD/FBD against SimaticML via a canonical netlist (part name + index, operand name, pin, template values, DisabledENO) plus title and comment. 026: 86, 027/028: 70 each, 023: 142 checks, all OK.

## SCL coverage (5g-1, sample 030_scl_all, commit 960f575)
15 SCL blocks; parser text identical to the SD export for all, no unknown token. About 60 token tags learned, among others OpPlAs/OpMinAs/OpMulAs/OpDivAs (`+= -= *= /=`), OpPo (`**`), OpAAND (`&`), OpRef, KwBY/OF/ENDC/ENDW/ENDR/UNTIL/EndRegion, Dot, BoxO/BoxC, Colon, LDots, BC/BCL/BCE (block comment), Label, SymVaP + AdMod (`:P`), FctCa, CaseSRange. The tests write a token inventory to RESULTS.md; an unhandled token makes the test fail.
- Multi-instance interfaces: SubPartIndex applies per sub-root. Hidden system members (MFlags & 7 == 0, e.g. ST/RU in IEC_TIMER) are hidden as in Openness.
- Notations rejected by the compiler (not a formatting question, only for building samples): LOG, EXPT(), TO_REAL/TO_INT/TO_STRING, MOD(), ODD, String+String, 2D array with variable index in FC ("Indirect access exceeds 48 bytes").
- Still open: PEEK/POKE variants, VARIANT dereferencing, pragmas in the code, technology objects, pointer/ANY parameters.

## LAD/FBD architecture in the plf (5g-3, commit 37c1e43)
The network is present as `<FlgNet>` plus ident table.
- FlgNet parts: `Part@Gate`, `LRef` (instruction/box), `CRef` (call), `ORef` (operand or open). Wires: Powerrail, OCon, PCon.
- Ident table (`IdentXmlPart`, Block --CoreObject2IdentContainer--> IdentParts) with all resolvable objects per RID:
  - `InterfaceAccess` (local tag: `SSD@S` section, `@MID` member ID, `AOS/AO` path), `MultInstAccess`, `SimpleAccess` (tag, `SAD@R` area), `GlobalAccess` (DB path), `LocalConstant`/`GlobalConstant`/`LiteralConstant` (`CB@T/SV/V`), `Label` (AK Jump|Definition), `Expression`,
  - `Instruction` (`ID@N` = instruction name, `LPI/LP` pins with types, `IUS/IU/TV` template values per usage),
  - `FCBlock`/`FBBlock`/`AufDBBlock`/`DepDBBlock` (called blocks, IDB), `BlockInterfaceInfo` (parameter list of the callee).
- References: `ORef/LRef/CRef/Instance/CodeBlock@RefId` = `ID@RID`. Usages `CS/C`: `NID` = network RefID, `UID` = part UId, `AK` = Write|Jump|Definition|Call|Multiinstance.
- SCL blocks also have this table (symbols).
- Internal instruction names partly differ from the export (Scale ↔ Scale_X); documentGet prints canonical SD names (Blkmov → S_BlockMove, SR → S_SR).
- Coverage check: RESULTS.md automatically contains the SCL token inventory, the FlgNet element/attribute inventory and the ident table inventory; anything unhandled turns the test red. Reference for structure elements: SimaticML schema `SW.PlcBlocks.LADFBD_v5.xsd` (mapping schema ↔ internal).
- LAD status: 100 of 164 catalog instructions created and checked against SimaticML (113 networks, 511 wires, all equal), sample 031_lad_all. Not yet creatable via SD: legacy timer/counter coils, S5 boxes, JMP/JMPN/LABEL/JMP_LIST/SWITCH, SET_BF/RESET_BF, OK/NOT_OK, CALCULATE, INC/DEC, MOVE_BLK family, Serialize/Deserialize, ReadFrom/WriteToArrayDB, FieldRead/Write, DRUM/DCAT/MCAT/IMC/SMC/LEAD_LAG/SEG/BCDCPL etc.

## Structure, SCL remainder and technology (5g, commits 3b42ea1, 9316a70, 93f8c9e)
- Operand kinds evidenced (sample 034_flg_misc): array index constant and variable, slice, DB/UDT member, type constants, user constants, local constants, tag/DB array; jump + label and CALCULATE with `<Equation>` created via SimaticML import. Parser output e.g. `arr[ia]`, `wa.%X3`, `label:lbl1`, `const:…`. 034: 186 checks against SimaticML, all equal; inventory without unhandled elements.
- Mapping SimaticML schema ↔ plf in [block-code.parser.md](block-code.parser.md) (parser version from the user's machine). Not observed (SD does not know them as text): Openbranch, Invisible, Part-Comment, access scopes Address, AddressConstant, AlarmConstant, NamedValueConstant, PredefinedVariable, Reference, Statusword, Indirect.
- SCL remainder (035_scl_rest): PEEK/POKE variants, REF/NULL/`^` (token `OpDeref`), setpoint pragma. Any/pointer parameters declarable, access in the code still open.
- Technology (036_tech): created via Openness PID_Compact 2.3, PID_3Step 2.3, PID_Temp 1.1, High_Speed_Counter 3.0, TO_SpeedAxis 6.0, TO_PositioningAxis 6.0, TO_ExternalEncoder 6.0 (PID_3Step 2.0/2.1 and PID_Temp 1.0 not creatable according to Openness). Technology instance DBs: `IGeneralDataBlockSourceData.Type=IDBofFBT`, `OfName` = TO type; parser type `TechnologyDB` with `instanceOf`. PID_Compact call in LAD, FBD and SCL compiled; MC_Power not yet (operand types).
- Instruction status: LAD 100/164, FBD 95/168; extended instructions: 137 of 161 catalog entries are real instruction names, S7 communication 6/6, Open User Communication 15/24, web server 1/1; Modbus, PtP, USS, Teleservice without SD names in this CPU configuration.

## STL (C1/C2, samples 061_fc_stl, 062_fb_stl, 2026-11-06)
Evidenced on two blocks created via STL source (`TiaPlcSoftware sourceImport`, `tools\sources\stl\*.awl`) on a CPU 1516 (S7-1500; S7-1200 does not know STL). Hypothesis H-034.
- `ICompileUnitData.ProgrammingLanguage` = `STL`, `RefID` = network number from 1. Title and comment as with LAD/FBD.
- Blob: `<Statements Version="14.0.0.0">`, one `<Statement UId TokenProperty="1">` per instruction line:
  - `<Token Kw=<number> DispName=<mnemonic> NumBLs=<blanks before>/>`: mnemonic in the project mnemonics (here international: `A`, `AN`, `O`, `=`, `L`, `T`, `+I`, `>I`, `JC`, `JCN`, `FP`, `SET`, `SAVE`, `A(`, `)`, `+`). SimaticML writes names like `A_BRACK` instead.
  - `<OpdAccess RefId UId NumBLs/>`: operand, resolved via the ident table as with LAD/FBD (`RefId` = `ID@RID`): local tag, literal (`L 0`, `+ 1`), jump label.
  - `<Label><OpdAccess RefId UId/><Token Kw=":"/></Label>` before the token: jump label of the line.
  - `<LC DispName=<text> NumBLs UId/>`: line comment `//` after the instruction.
  - Empty `<Statement/>` = blank line.
- `NumBLs` holds the spacing of the editor; the STL source export ignores it (one space between mnemonic and operand, 6 blanks indentation, `Label:` + 6 blanks).
- Kw numbers seen: A 1, AN 2, O 3, = 9, FP 11, L 16, T 17, >I 100, JC 113, JCN 119, + 131, A( 153, +I 210, SET 231, ) 235, SAVE 237.
- Parser: network field `stl` with the text in the layout of the `.awl` export (`A #a ;// and a` with comment). Unknown elements appear under `unknownTokens`. Networks with an unknown root element now get `error` instead of silently staying empty.
- Check: `ProjectTests.StlAgainstSource` compares title, comment and code of each network with the `.awl` export, 33/33.
- Catalog coverage (C3, sample 064_stl_all, 2026-11-07): all 157 STL mnemonic pages of the catalog (`docs/catalog/stl.txt`), one each as a network, text = `.awl` export (`StlAgainstSource` 866/866, table in [instruction-coverage.md](instruction-coverage.md); `tools/lad_matrix.py` overwrites the file, afterwards re-append the STL section with `tools/gen_stl.py` data). Further elements:
  - second `<Token Type="PartialKeyword" DispName=…/>` after the mnemonic: `L DBLG`, `L STW`, `LAR1 AR2`, indirect addresses `T W[ AR1, P#0.0]` (tokens without DispName with `Kw` `[` `]` `,` attach to the predecessor without a blank);
  - `<UserToken Type="Constant" DispName=…/>`: constant directly in the text (`NOP 0`, `BLD 100`);
  - `CALL` with `<CallInfo RefId>` (BlockInterfaceInfo) and `<ParaExpression FPNum=n>` (n = n-th parameter of the callee's interface, value as OpdAccess/UserToken); FB call with instance DB as `OpdAccess`, `<Token Kw=","/>`, `OpdAccess`. The export writes the parameters as `(  name` padded to 28 characters `:= value , ` per line and `);`.
  - Absolute addresses create tags `Tag__<number>` in the "Standard-Variablentabelle" (default tag table) on source import; code and export then use this name. DB register accesses `%DBW0`/`%DIW0`: SimpleAccess with `SAD@R` = `Data` or `XData`. `OPN %DB100` without DB: AufDBBlock without name, number in `TOD@N`.
  - Large blocks (FB_StlMath, 69 networks) carry `Sources` and the ident container as untagged links (typed link list, relation implicit); the parser falls back to untagged links to the type `CompileUnitData` or `IdentContainerData`/`IdentPartData` (H-035).
  - BlockInterfaceInfo holds the parameter list under `BIID/BPIL` (not directly under the element). Until 2026-11-07 the `parameters` of LAD/FBD calls (CRef) therefore also stayed empty (H-036).
  - UC/CC require "Parameter passing via registers" on the called FC (SimaticML `ParameterPassing`, only for standard blocks; cannot be set in the STL source, set via SimaticML import). Such an FC cannot be called with CALL.
- Open: the 83 system block calls of the STL catalog (TP/TON/CTU, MOVE_BLK, Serialize …), comment lines without instruction, German mnemonics.
## Stress test (C4, sample 073_code_stress, 2026-11-07)
Created with `tools/gen_stress.py` (SD via `documentWrite`, STL/SCL via `sourceImport`) and `tools/make_mixed_stl.py` (SimaticML import), compile 0/0:
- `FB_BigLad`: 300 LAD networks; `FC_BigFbd`: 300 FBD networks; `FB_BigStl`: 600 STL networks; `FB_BigScl`: one SCL network with 3024 lines and 12 regions;
- `FB_Mixed`: 120 networks alternating LAD/FBD/SCL (SD: `S7_Language` per network); `FB_MixedStl`: the same plus 40 STL networks (SD does not know STL, therefore inserted via SimaticML);
- Interface per FB 785 tags: Input 123, Output 120, InOut 60, Static 302 (start values, array 2D, struct), Temp 150, Constant 30, comments, titles and network comments.
- Check `python tools/check_code_truth.py 073_code_stress`: interfaces, network count, language, title, comment and LAD/FBD netlists against SimaticML, SCL against SD export, STL against `.awl`: 13149 checks, 0 errors. `.NET StlAgainstSource` also covers FB_BigStl.
- Findings: an anonymous struct as FC parameter has the RID class `0x94` (previously output as `RID 0x94010000`, fixed). An FC with 123 Input/Output parameters compiles with 2 InOut, with 5 InOut TIA reports "Too many passed parameters at the block call" (FB: 303 parameters without problems). The SD export of an SCL network contains leading and trailing blank lines that the parser does not output.
## STL calls of system instructions (sample 075_stl_calls, 2026-11-07)

- Source: `CALL MIN` or `CALL #ton1` (multi-instance), followed by a line `{value_type := 'Int'}` with the type templates
  (TPI/TP `T=type` of the instruction, values from the template usages IUS/IU of the call elements in the network), then the
  parameter list. Without a cardinality template MIN has three inputs in STL (IN1–IN3).
- In the plf: `CallInfo RefId` points to the instruction ident (instead of BlockInterfaceInfo); each `ParaExpression` carries
  `DispName` = parameter name. The parser takes `DispName`, otherwise parameter no. `FPNum` of the callee.
- Parameters with keyword names are in quotation marks in the source: `"TIMER"` (RESET_TIMER), `"db"` (ReadFromArrayDB).

## GRAPH (sample 074_fb_graph, 2026-11-07)

Created via SimaticML import (`tools/sources/graph/FB_Graph.xml`, schema `SW.PlcBlocks.Graph_v6.xsd`). A GRAPH FB has in the plf
`ProgrammingLanguage` GRAPH_PLUS and several CompileUnits instead of a single network:

| CU language | Root | Content |
|---|---|---|
| GRAPH_ADDINFOS | `<AddInfos>` | Alarm settings: display class per supervision category 1–8, categories for interlock, supervision, warning |
| GRAPH_SEQUENCE | `<GraphStructure>` | `Sequence/Steps/Step UId RefId [Init]`, `Transitions/Transition UId RefId`, `Branches`, `Structure/Connection` (two nodes `StepRef No` / `TransitionRef No`, `LinkType="Jump"` on the target node, otherwise Direct) |
| GRAPH_ACTIONS | `<Actions RefId UId>` | per step; a step without actions has an empty `Action Event="" Interlock="false"` with `Operator Operation=""` |
| LAD/FBD | `<FlgNet RefId UId>` | per step supervision (coil `SvCoil`) and interlock (`IlCoil`), per transition the condition (`TrCoil`) |

- Step number = UId − 100000, transition number = UId − 200000.
- `RefId` of the steps and transitions points into the ident table: InterfaceAccess to the static element
  (`Step1` of type `G7_StepPlus_V6`, `T1` of type `G7_TransitionPlus_V6`). Its `CS/C` names the CompileUnit per role:
  `NID` = RefID of the CU, `AK` = Definition (GraphStructure), Actions, Supervision, Interlock, Transition.
- Step times are stored only as start values in the static element: `T_MAX` (MaximumStepTime), `T_WARN` (WarningTime),
  plus `SNO` = step number, `TNO` = transition number.
- The runtime blocks are present as instruction idents (`G7_RT_Plus_1_V6` … `G7_RT_Plus_SUB_3_V6`).
- Parser: networks with `sequence` (steps, transitions, connections), `alarmSettings`, `actions`, on the sub-networks
  `graphRole` and `graphElement`. SimaticML: a `<Graph>` (Graph v6) like the export, test `SimaticMlTests` equal.
- Actions (sample 076, created in the editor): in the plf `Action Event Interlock` with `Operator Operation` = qualifier
  (N, S, R …), `OpdAccess RefId` = operand, `Token Text="&#10;"`; in the export `<Action Qualifier="N"><Token Text="#q2" />
  <Token Text="&#xA;" /></Action>`, i.e. the operand as token text. That is why the import with `Access` failed.
  Parser: `actions` with `qualifier` and `operands`, SimaticML equal to the export.
- Branches (sample 077): `Branches/Branch No Type (AltBegin, AltEnd, SimBegin, SimEnd) Cardinality`, in the
  connections `BranchRef No` with `In`/`Out` (branch number); export `Branch Number …`, `BranchRef Number In|Out`.
- Permanent operations (077): own LAD/FBD units without step/transition; before the GRAPH_SEQUENCE unit =
  pre-instructions (PreOperations), after it = post-instructions. Parser `graphRole` PreOperation/PostOperation;
  SimaticML equal to the export (1517/1517).
- Events and interlock on actions (sample 080, `tools/sources/graph/FB_GraphEvt.xml`, from FB_GraphBranch with
  `Event` S1/S0/V1 and `Interlock="true"`): in the plf the attributes `Event` and `Interlock` on the `Action` element of the
  GRAPH_ACTIONS unit; the export writes `<Action Event="S1" Qualifier="N">`, `<Action Interlock="true" Qualifier="S">`
  (order Event, Interlock, Qualifier). SimaticML equal to the export.
- Not evidenced: post-instructions, several sequences, step and transition names in several languages.

## Open (not varied)
- SCL: seen only `:= + > < ,`, THEN ELSIF ELSE END_IF, DO TO END_FOR. Not seen: `- * /`, AND/OR/NOT, WHILE/CASE/REPEAT, strings, `(* *)` comments, tag and DB accesses.
- LAD/FBD: only Contact, Coil, Add (Int), Powerrail, Operand, open seen. Operands only local tags (no tag, DB access, constant, address). Section abbreviations only I/O. Branches, calls, timers, comparators not varied.
- Order of the networks = order of the Sources links (= RefID); inserting or reordering in the middle, multilingual network comments not investigated.
- OB Main (template, LAD): CompileUnit without readable network (parser: 'no network source'), cause not investigated (presumably empty network).
