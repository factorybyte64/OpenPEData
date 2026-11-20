# Plan Z1.2: Code blocks (OB, FB, FC) in STL, FBD, LAD, SCL

As of 2026-11-06, branch `p1-fassade` (local e1d6fe7). Goals and non-goals are only in [GOALS.md](GOALS.md) (Z1.2: code identical to the TIA text or SimaticML, coverage report over all language elements and instructions).

## 1. Current state

Source: [spec/block-code.md](spec/block-code.md), [spec/block-code.parser.md](spec/block-code.parser.md), [spec/instruction-coverage.md](spec/instruction-coverage.md), parser `dotnet/src/OpenPEData/Model/Code.cs`, viewer `ui/OpenPEData.Viewer/DetailViews.cs` (tab "Networks").

Common to all languages: the code is stored as source in the plf (block --Sources--> `CompileUnitData`, one entry per network, `ProgrammingLanguage`, blob with XML). Network title and comment are read per language. Operands come from the ident table (`IdentXmlPart`): local variables, tags, DB accesses, constants, absolute addresses, jump labels, called blocks with instance. The compiled MC7plus is not read.

| Language | Status | Evidence |
|---|---|---|
| **SCL** | Done. Source text is reconstructed losslessly from the token tree (`SCLSource`), identical to the TIA text and the SD export (indentation, comments, regions). Unknown tokens turn the test red. | 026, 030 (15 blocks), 035; catalog scl 119/119 |
| **LAD** | Done as a netlist. `FlgNet` with parts, calls, operands, wires, template values, negations, labels, CALCULATE equation. Checked against SimaticML via a canonical netlist. | 027, 031, 034, 037, 039; catalog lad 164/164, extended 141/141, communication/Modbus/PtP/USS etc. complete |
| **FBD** | Like LAD, same `FlgNet` format. | 028, 032, 034; catalog fbd 165/165, extended 141/141 |
| **STL** | Status before C1: missing. No sample, no format knowledge, no parser branch; STL networks appeared with title but without code. Since C1/C2 (see below) read as text. | 061, 062 |

Further gaps:
1. **LAD/FBD output form:** the output is a netlist of our own (JSON, shown in the viewer as text "parts/wire"). There is neither SimaticML output nor a graphical representation. The Z1.2 criterion "identical to SimaticML" is thus only met indirectly via the test comparison.
2. **OB Main from the template:** CompileUnit without a readable network ("no network source"), cause open (presumably an empty network).
3. **Network structure:** inserting/reordering in the middle, multilingual network comments, mixed languages in one block (LAD/FBD block with SCL or STL network) not varied.
4. **SCL remainder:** access to Any/Pointer parameters in code, VARIANT dereferencing, MC_Power call (operand types).
5. **Unobserved FlgNet elements** (SD does not know them): Openbranch, Invisible, Part-Comment, address/indirect accesses, AlarmConstant etc.
6. **Know-how protected blocks:** behavior not investigated (source encrypted?).
7. **Real project:** `analysis/real/DemoProject_BestPractices_V4` yields 11 networks (6 FBD, 5 SCL) without errors; LAD and STL do not occur there.

## 2. Plan

Way of working as in the DB attribute thread: per step one project copy in TIA via MCP, recipe into the MANIFEST, truth via Openness (SimaticML export, text/SD export, get), result in `docs/spec/block-code.md`, `hypotheses.md`, `LOG.md`, test in `dotnet/tests` (and Python analysis only as a tool). Samples from **061**.

| Step | Content | Samples | Truth / test |
|---|---|---|---|
| **C1 STL basics** | S7-1500 CPU, FC and FB in STL with a few networks (A/O/=, L/T, jumps, comments per line, network title). Determine the `ProgrammingLanguage` value and blob format (token tree like SCL or line format). | 061_fc_stl, 062_fb_stl | SimaticML export (STL networks) and STL text source (`.awl` export) |
| **C2 STL parser** | Branch in `Code.Networks` for STL; text reconstruction identical to the STL source; unknown tokens turn the test red (inventory as with SCL). | 061, 062 | Test comparison text + token inventory |
| **C3 STL coverage** | STL catalog from the TIA help (`tools/instr_catalog.py`), sample with all instructions (bit logic, load/transfer, accumulator, jumps, indirect addressing, address registers, calls CALL/UC/CC, timers/counters). STL exists only on S7-1500 (not S7-1200). | 063_stl_all | Catalog coverage in instruction-coverage.md |
| **C4 Mixed languages and structure** | LAD block with SCL and STL network; network inserted in the middle and moved; comments in two languages; empty network (clarifies the OB Main case). | 064_mixed_lang, 065_net_order, 066_net_comment_ml | SimaticML + test comparison |
| **C5 OBs** | OB types (Main, cycle, cyclic interrupt, startup, error OBs) with code in one language each, OB attributes (number, priority, event class). Check what already comes via Z1.1 (207_hw_ob_cyclic). | 067_ob_types | Openness attributes + SimaticML |
| **C6 SCL/FlgNet remainder** | Any/Pointer in code, VARIANT deref, MC_Power, force Part-Comment/Openbranch via SimaticML import. | 068_scl_rest2, 069_flg_rest | SimaticML |
| **C7 Know-how protection** | Block with know-how protection: what is in the plf, what can be emitted. | 070_khp | Openness (interface remains readable) |
| **C8 SimaticML output (optional)** | Emit LAD/FBD/STL/SCL as SimaticML-like XML so that "identical to SimaticML" can be checked directly; the viewer shows the text. A graphical LAD/FBD representation in the viewer is a separate, later step (Z1.5). | all code samples | Byte/canonical comparison with the Openness export |

Status 2026-11-06: C1 and C2 done (samples 061/062, format in [spec/block-code.md](spec/block-code.md#stl-c1c2-samples-061_fc_stl-062_fb_stl-2026-11-06), H-034, test `StlAgainstSource`). STL is now emitted as text. C3 done 2026-11-07: all 157 STL mnemonic instructions (sample 064_stl_all, because 063 is meanwhile taken by 063_multi_plc), H-035/H-036. Open from C3: the 83 system block calls of the STL catalog. C4 stress test done 2026-11-07 (sample 073_code_stress: mixed languages incl. STL, hundreds of networks, large interfaces, 13149 checks). Open from C4: insert/move a network in the middle, multilingual comments. Next step after that with the next free sample number in each case (the numbers in the plan above are only placeholders; 063 and 065 have been taken by other threads).

Status 2026-11-07 (C8, LAD/FBD): done. `TiaProject.GetLadFbdSimaticMl()`, CLI `--simaticml`, viewer tab "SimaticML" emit every LAD/FBD network as the `<FlgNet>` of the Openness export. Test `SimaticMlTests.LadFbdAgainstExport`: 1512 of 1512 networks of all truth exports equal (elements, attribute order, values). Rules in [spec/block-code.parser.md](spec/block-code.parser.md#simaticml-output-c8). SCL and STL remain text (identical to the TIA text or .awl export).

Order: C1 → C2 → C3 first, because STL is the only completely missing language. C4/C5 after that, C6/C7 small, C8 after consultation.

GRAPH (as of 2026-11-07): sample 074_fb_graph via SimaticML import, format in [spec/block-code.md](spec/block-code.md#graph-sample-074_fb_graph-2026-11-07), output as sequence and as SimaticML `<Graph>` equal to the export. Open: actions with content, branches, permanent operations (template by hand in TIA required). CEM: out of scope per GOALS.md section 4 as far as the code is concerned. The Openness help (TIAPortalOpennessenUS, page on Openness V17) says that CEM blocks do not support the export method in any version; Openness only knows the language on the block and the creation of instance DBs. There is also no CEM schema under PublicAPI/V21/Schemas.
