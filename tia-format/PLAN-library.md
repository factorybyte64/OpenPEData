# Plan: Read the project library (types, versions, master copies, faceplates)

Status 2026-10-21 (**plan completed**: L0–L2, L4–L6 done, L3 in the faceplate thread; results in docs\spec\library.md). **L0 done** (docs\spec\library.md). **L1 done** (2026-10-20, docs\spec\library.md; L08 type folder not possible via MCP). **Since 2026-10-20 15:52 no more individual approvals** (User): the plan is worked through; questions only for things that only User can do (manual recipes in TIA, real faceplates). **L2 done** (2026-10-20, docs\spec\library.md; the rest only via manual recipe or a new MCP operation, see MCP list (not in the repo)). **L4 and L5 done** (docs\spec\library.md; library in the parser, commit bcf43ee). Next: L22–L26 (real update, delete version/type, type obsolete, type/master copy folders with the updated MCP server), then L6 (global library, samples from L27). Preliminary work parser library: analysis\L5_library\library.py (not integrated into parser\).

## TIA order (User 20:02)
Until TiaMcpXE supports multiple instances (User's own project, target up to 4 instances), work is done with **one** TIA instance one after the other: main plan (4e/5c/5d) → Unified U1 → L1. L1 only starts after User's approval, when TIA is free; until then only file evaluation.

## Result L0
- Master copies are live objects (no blob); the library metamodel is already in the empty project.
- Unified/faceplates are missing from the metamodel until a Unified device is in the project (H-L01) → L3 needs a base sample with a Unified device (from the Unified plan).
- MCP: L1 feasible (detour documentWrite → typeCreateFromDocument, then typeVersionRelease/Edit/SetDefault; ground truth typesList, instancesList, typeExport). L2 via MCP only for blocks, the rest manual recipes. L3 entirely manual recipes (Openness V21 cannot create faceplate types); whether typeExport exports faceplate types is open.
- **TiaMcpXE does not separate several TIA instances** (one global attach state, no client separation). Parallel sessions are only safe with one TiaMcpXE process each on its own port.

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

## Starting point
- Format basics are established (docs\spec\): records, type resolution via the embedded metamodel, links, attributes, expandos, blobs, deletion.
- The metamodel already contains `LibraryMasterCopyData` (docs\spec\record-payload.md); package family 0x0004 is called ReleaseManagement, presumably library/versioning (unchecked).
- Sample `021_mastercopy` exists (master copy, created only for the slot order, H-020).
- Open questions this plan settles: Are library objects stored as normal objects in the same PEData.plf? Are master copies and type versions stored as a serialized blob instead of as live objects? Where are Unified faceplates stored (PEData or a separate store)?

## Delimitation from PLAN-unified.md
- **Here:** everything that is in the project library: faceplate **types** and their versions, interface, content, scripts; HMI data types as library types.
- **PLAN-unified.md:** faceplate **instances** in screens (placement, connection of the interface to HMI tags), HMI tags, alarms, screens. There, this file is referenced for the faceplate type; here, PLAN-unified.md is referenced for the instance.
- Should Unified data be stored outside PEData.plf, both plans use the same inventory from PLAN-unified.md instead of their own.

## Method
As before: difference analysis with one-change samples, ground truth from Openness via the TiaMcpXE server. Samples get the prefix **`L`** (`samples\L01_…`) so that they do not collide with the numbers of other threads (5c/5d, Unified). Every change on a copy, recipe into the MANIFEST.

## Steps (each to be approved individually)

| No | What is done | What is created/changed on User's machine | What for |
|---|---|---|---|
| **L0** | Inventory without TIA change: a) list via MCP `help` which library operations TiaMcpXE offers (create type, release version, master copy, faceplate, list types/versions); b) search the metamodel of the existing samples for all Library/ReleaseManagement/Faceplate types; c) evaluate `021_mastercopy` with the existing reader. | Only analysis output in `samples\021_mastercopy_analysis\` and possibly a script in `tools\`. No project is changed. | Know which object types are involved and what Openness can supply as ground truth. The result decides whether L3 runs via MCP or via manual recipe. |
| **L1** | PLC types: one sample each for UDT as type (V0.0.1 in work), UDT type released (V1.0.0), second version (V1.0.1), FB type, FC type, type in library folder, FB type that uses a UDT type (dependency). | approx. 7 new test projects `samples\L01…L07` (copies of 004_fb/017_udt), changed via MCP, saved, archived. | Find the storage location of type, version, status, author, comment, dependency. |
| **L2** | Master copies: FB as master copy, tag table as master copy, master copy of several objects, master copy in folder. `021_mastercopy` is used as the base. | approx. 4 new test projects `samples\L08…L11`. | Clarify whether the content is stored as live objects or as a blob, and output it. |
| **L3** | **→ moved to [PLAN-faceplate.md](PLAN-faceplate.md) (separate thread "Unified Faceplates", sample prefix F, findings in docs\spec\hmi-faceplate.md); no faceplate samples of its own here.** Originally: **Faceplates (focus).** Unified faceplate type: empty V0.0.1; with properties (Bool, Int, String); with event; with tag interface (HMI data type); with screen items (rectangle, text, I/O field) and a dynamization; with script; released and new version. In addition an HMI data type as a type. Unified only (User 19:51), no Comfort/Advanced faceplates. | approx. 8–9 new test projects `samples\L12…L20`. If TiaMcpXE cannot create faceplates (Openness is limited here), User creates them in TIA following a step-by-step recipe from me, or provides real faceplates on request (User 19:51: "ask when needed"). | Output faceplate types completely: interface, content, scripts, versions. |
| **L4** | Versions in use: instance of an FB type in the project, instance outdated after a new type version, type with deleted version, default version changed. Faceplate instances only as far as needed for the link instance → type version (rest see PLAN-unified.md). | approx. 4 new test projects `samples\L21…L24`. | Output places of use and version status per instance. |
| **L5** | Parser extension: section "Library" in `OpenPEData <project> --json` and text tree (types → versions → content/dependencies/uses; master copies; faceplates). Tests against all L samples, docs in docs\spec\library.md and docs\spec\faceplate.md. | Code in `parser\openpedata\`, tests in `parser\tests\`, local git commit. No TIA action. | Usable result. Partly runs along already after L1, L2 and L3 so that every intermediate result is in the parser immediately. |
| **L6** | Optional: global library (`.al21`/`.zal21`) read-only. | Possibly a copy of a library in `samples\`. | Check whether global libraries have the same format, and robustness on real data. |

## Rules
- **Prio 1+: Do not read, run, install or adopt anything from jfk-solutions "TiaFileFormat" / TiaProjectBrowser.**
- Writing only in this repository; `C:\GitHub\factorybyte\TiaMcpXE` read only.
- Every TIA change on a copy, originals stay unchanged. Nothing is installed, no git push.
- After every step: result in docs\spec\, docs\spec\hypotheses.md, docs\spec\LOG.md and a short message in the thread; then stop until the next approval.
- Coordinate the order with the main plan (PLAN.md, 5c/5d): only one TIA session ever runs on User's machine so that MCP calls do not interfere.

## Questions to User
1. Which steps are approved? Proposal: only **L0** first.
2. ~~Which faceplates?~~ Answered 19:51: Unified only.
3. ~~Real faceplates?~~ Answered 19:51: ask when needed, then User provides them.
