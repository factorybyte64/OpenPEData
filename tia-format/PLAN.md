# Work plan TIA V21 file format

Status 2026-10-20 18:35. 4a–4d, 5a, 5b, multi-instance test, parser fix (packages), **5c** (interfaces, instance DB), **5d** (SCL/LAD/FBD code, docs\spec\block-code.md), **5e** (--verify) and **5f part 1** (PEData.idx, XRef.db, savepoints, docs\spec\derived-stores.md) **done**; **5g done** (2026-10-21: LAD, FBD, SCL and all instruction catalogs 100 %, commits 0b624d9/e7b8b67). **4e/4f dropped** (User 2026-10-22 19:22: "we restrict ourselves to reading the PEData"; only exception: thread "Inject faceplate via library" on throwaway copies). **5f done** (2026-10-21, commit 73757e5). **Step 6** with Siemens libraries, MCP test projects and DemoProject_BestPractices_V4 done (2026-10-22, e847b6c); further real projects at any time with tools\scan_project.py and check_real.py.

Noted for later (when convenient, hint from the Unified thread): fix an error in `parser\openpedata\metamodel.py`. `Metamodel.packages` is keyed by `id >> 16`, several packages share this value (0x0040: 10, 0x0010: 6), so only 9 instead of 28 or 15 instead of 42 packages. Fix: key by the full package ID, add a test on the package count, all tests, local commit. Will be handed to the next device session after 5d.

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

## Achieved so far (steps 1–3, done)
| Step | Result |
|---|---|
| 1 Inventory | Files of an empty project cataloged; the content is in `System/PEData.plf` (+ `.idx`, two SQLite DBs). |
| 2 Container | plf = chain of SHA-256-secured records as a transaction log; idx = references to records. |
| 3 Types + diffs | Every record can be assigned 100 % to a type via the metamodel embedded in the project; diffs for device group, CPU, tag table, FB evaluated. |

### What was created/changed on User's machine in the process
Only in this repository:
- `samples\`: projects 000_empty, 000_empty_resave, 000_empty2, 001_devgroup, 002_cpu, 003_tagtable, 004_fb (+ `.zap21`, `_truth`, `_diff.txt`, `_analysis`), `MANIFEST.md`
- `tools\`: McpCall.ps1, Tia.ps1, Inventory.ps1, TiaAnalysis.cs, PeData.cs, Analyze-/Compare-/Diff-PeData.ps1, Export-Metamodel.ps1, New-Sample.ps1
- local TiaMcpXE endpoint configuration, `README.md`, local git repo (without remote)
- In TIA Portal only these test projects were created/opened/saved/closed. `C:\GitHub\factorybyte\TiaMcpXE` was only read. Nothing installed.

Note: The session was ended in the middle of step 4. It may already have started with 4a or 4b (e.g. first tag variants or changes to `tools\PeData.cs`); after approval I check this first via `git status`, without changing anything.

## Proposed next steps (each to be approved individually)

| No | What is done | What is created/changed on the machine | What for |
|---|---|---|---|
| 4a | Build a generic record reader: header, sections, links of all records; output object graph as JSON. | Only code in `tools\`, JSON outputs in `samples\*_analysis\`. **No TIA action.** | Recognize which object is connected to which (project → CPU → table → tag). |
| 4b | Five tag variants from 003_tagtable, each with exactly one change: data type Int, address %M10.3, comment "Test", retentivity/visibility, rename. | 5 new test projects `samples\005…009` (copies of 003, changed via MCP, saved, archived). | Find out where and how type, address, comment and flags of a tag are stored. |
| 4c | Check whether the commit records are signed with the project RSA key. | Only analysis code, **no TIA action.** | Clarify whether a parser can check signatures and whether writing would ever be possible. Optional, low priority. |
| 4d | First parser command line `OpenPEData <project> --json`: project, devices, tag tables, tags, blocks. | New code in `parser\`, test against all samples, local git commit. | First usable result. |
| 5 | Block contents (interface, SCL/LAD/FBD code), DBs, UDTs with further one-change samples. | Further test projects in `samples\`. | Extend the parser with program contents. |
| 5e | Parser option `--verify`: check record hashes, hash chain and signatures. | Only code in `parser\`. No TIA action. | Integrity check. |
| 5f | Format complete: PEData.idx completely, slot assignment of large objects directly from the metamodel, role of Vci.db, XRef.db, IM\SearchIndex, .ap21 (content or only cache?). | Only analysis code. No TIA action. | Prerequisite for later writing. |
| 4f | Depending on the result of 4e: minimal writer (append record, update index, chain + signature), checked against Openness. | Code + manipulated copies, TIA opens them. | Long-term goal writing. |
| 6 | HMI, network, libraries. | New samples. | Coverage and robustness. |

## Result 4a/4b/4d
- 4a: object graph of all records, links 100 % validated, attributes 96–99 % decoded (docs\spec\record-payload.md).
- 4b: samples 005–009 (Int, %M10.3, comment, HMI writable, rename) – tag layout complete (docs\spec\tag.md).
- 4d: `OpenPEData` (Python), 22/22 sources OK against Openness (parser/README.md). Committed locally ef43202.

## Proposed next steps (new, each to be approved individually)
| No | What is done | What is created/changed on the machine | What for |
|---|---|---|---|
| 5a | Close gaps in the parser: device in device group, tags in subfolders, comment with umlauts, I/Q addresses, retentivity/ExternalAccessible, remaining attribute decoding errors (3 %), deleting an object (H-018). | approx. 5–6 new test projects in `samples\` (copies), code in `parser\`. | Make the parser complete and robust for the existing contents. |
| 5b | Data blocks and UDTs: global DB with a few tags, one UDT, instance DB. | approx. 3–4 new test projects. | Output data structures. |
| 5c | Block interfaces (Input/Output/Static/Temp) for FB/FC. | approx. 3 new test projects. | Output interfaces. |
| 5d | Block code (SCL text, LAD/FBD networks). | approx. 3–4 new test projects. | Output program content; probably the most laborious part. |
| 4c | done (docs\spec\integrity.md) | – | – |
| 4e | Check whether TIA checks signatures when opening (H-017): on a copy, change a value directly in the plf and append a closing record, a) without a valid signature, b) with a self-generated key record + own signature; observe TIA's reaction when opening. | 2–3 manipulated copies in `samples\`, TIA opens them. Originals untouched. | Clarify whether writing without TIA is possible in principle. Note: every .zap21 gets a new key pair, so the key is not tied to the project/license. |
| 6 | HMI, network, libraries, other TIA versions (V19/V20 if available). | New samples. | Coverage. |

## Step 5g code coverage LAD, FBD, SCL at 100 % (User 2026-10-20 12:51 card "Code coverage", 12:51 "LAD FBD SCL must be covered 100 percent")

**What "100 %" means measurably**
1. **SCL:** every language element of S7-1500 SCL in V21: all statements (IF, CASE, FOR, WHILE, REPEAT, CONTINUE, EXIT, RETURN, GOTO/labels, REGION), all operators, all literal forms of all elementary types (Int family, Real/LReal with exponent, 16#/2#/8#, typed literals such as INT#5, Time/LTime, S5Time, Date, TOD/LTOD, DT/LDT, Char/WChar, String/WString), all access kinds (local, PLC tag, DB, nested, array with expression, absolute address, slice `.%X0`, PEEK/POKE, REF/dereferencing), all call kinds (FC, FB single, multi and parameter instance, system instructions with template, return value, ENO), comments (`//`, `(* *)`, `/* */`), pragmas/attributes. Criterion: parser text == TIA text (get/SD) for every test block, no unknown tokens.
2. **LAD and FBD, structure:** all elements of the FlgNet (part kinds, operand forms incl. all sections, PLC tag, DB, constant, absolute address, slice, indirect; branches, parallel branches, OR/AND boxes, negation, edges, empty box, jumps/labels/RET, call boxes FC/FB/system with instance kinds, template values, DisableENO, network title/comment multilingual, free comments). Criterion: canonical netlist == SimaticML, no unknown element or attribute.
3. **LAD and FBD, instructions:** every instruction of the LAD/FBD instruction catalog of TIA V21 for S7-1500 (basic, extended, technology, communication instructions), one instruction per network each, generated. Criterion as in 2; instructions that cannot be created (e.g. hardware-dependent) are listed individually with a reason.
4. **Proof:** The tests produce a coverage report: seen vs. handled tokens/elements, list of all instructions with status.

| Part | What is done | What is created/changed on the machine |
|---|---|---|
| 5g-1 | SCL complete: generated test blocks, parser, tests | Test projects (copies) in `samples\`, code in `parser\`, `tools\` |
| 5g-2 | LAD/FBD structure complete | Test projects, parser, tests |
| 5g-3 | Determine the instruction catalog (list from TIA itself, read-only), generate networks, import, compile, export truth, compare | Test projects per category, generator in `tools\` |
| 5g-4 | Coverage report, complete docs\spec\block-code.md, OB Main without network (H-033) | Report, spec |

Not part of 5g: STL (AWL), GRAPH, CEM, compiled program (MC7plus).

## Rules for execution
- **Since 2026-10-20 (User): no more approval per step, the plans are worked through.** Only things that only User can do are asked (confirming a dialog on the machine, manual work in TIA, goal decisions). New steps are still entered here beforehand.
- **Prio 1+ (User, 2026-10-19): Do not read, run, install or adopt anything from jfk-solutions "TiaFileFormat" / TiaProjectBrowser (code, docs, binaries, NuGet).** Reason: commercial product; OpenPEData stays independent and is created without its code.
- Writing only in this repository; `TiaMcpXE` read only.
- Every TIA change is made on a copy, originals stay unchanged.
- Nothing is installed, no git push.
- After every step: result in `docs\spec\`, `docs\spec\hypotheses.md`, `docs\spec\LOG.md` and a short message in the thread.

## Open questions to User
1. Which steps are approved (e.g. "4a–4d")?
2. May Python be installed? (Otherwise continue with PowerShell/C#.)
