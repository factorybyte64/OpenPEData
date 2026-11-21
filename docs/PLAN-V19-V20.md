# Plan: reading projects of TIA Portal V19 and V20

Status 2026-11-21. Goal: OpenPEData reads projects and global libraries of TIA Portal V19 and V20 with the same
results as V21, each statement backed by test projects of that version (see `dotnet\ARCHITECTURE-REVIEW.md` 3.14).

## First findings (2026-11-21)

Test projects created via Openness on Robb's machine (TIA V19 and V20 installed next to V21), working folders
`<temp>\Z19` and `<temp>\Z20`, same steps as the V21 samples 000, 002, 003 and 004:

| Project | Content |
|---|---|
| v20_000_empty.zap20 | empty project |
| v20_002_cpu.zap20, v19_002_cpu.zap19 | CPU 1513-1 PN (6ES7 513-1AL02-0AB0 V2.9), security settings left at their defaults |
| v20_001_basic.zap20, v19_001_basic.zap19 | as 002 plus `TagTable_1` with `Tag_Bool_1` Bool %M0.0 and `FB_Empty` (SCL, generated from source, not compiled) |

Result with the V21 profile (`OpenOptions.AllowUnknownVersion`, CLI prints a warning):

- Both archives open; `--verify` is OK: plf header and record hashes, commit chain and RSA-2048 signature, xref and
  search index savepoints. V20: 708 records, V19: 704 records.
- Hardware tree, PROFINET node, tag table with tag, FB and Main OB with its interface and comments are output as for
  V21. The metamodel record key and type id are the same as in V21.
- The Python parser also reads the V20 archive.

So the container, the record format and the metamodel principle are the same in V19, V20 and V21. Differences are to be
expected in the content: decoding tables that are V21 findings (expando codes, HMI property names, code operators,
Unified enums) and new or changed object types.

## Progress

- 2026-11-21: PLC samples v19_/v20_ 001–004 (tags, UDT, global DB, FB/FC interfaces, SCL, LAD, FBD, STL) with
  SimaticML truth, recorded in `samples\MANIFEST.md`. `VersionProfile.V19` and `V20` added (same data as V21); test
  `ProjectTests.OlderVersionSamples` (version, integrity, members = SimaticML) and `SimaticMlTests.LadFbdAgainstExport`
  now cover them.
- 2026-11-21: v19_/v20_ 005 (Program_Alarm FB, instance DBs, watch table, software units with relation) and 006
  (alarm text, info text, alarm text list). Output equals V21 in kind; the test checks the texts.
- 2026-11-21: 007 (TO_SpeedAxis 6.0 with a changed parameter, OPC UA server interface, ProDiag FB) and 008 (force
  table entry). Open in PLC: ProDiag supervisions (`supervisionWrite` fails on V20 because the export sheet has another
  layout). Next: HMI (WinCC Unified).
- 2026-11-21: HMI 101 (MTP700 Unified Comfort with firmware 19.0.0.2 / 20.0.0.0, screen with button, I/O field,
  rectangle, text and bar, HMI tag with dynamization, discrete alarm): parsed like V21, the viewer draws the screen.
  Next HMI steps: faceplates, alarm/trend controls, styles, logging, scripts; then libraries.
- 2026-11-21: HMI 102 (alarm control, gauge, trend control, data log with logging tag, global script module); an HMI
  text list could not be created (V19: not in Openness V19, V20: tool error). Libraries 201 (project library with
  master copies; V20 also a released type from documents) and 202 (global library .zal19/.zal20). The test checks
  1115 points.
- 2026-11-21: hardware 301 (ET 200SP on subnet PN/IE_1 in the PROFINET IO system; the integrated HMI connection needs
  Openness V21) and HMI 103 (Dark style, set via the TIA UI; style names FlatStyle_DarkV1902 / V2000). The test checks
  1143 points, the full suite is green.
- 2026-11-21: PLC 009 (ProDiag operand supervision; V19/V20 sheets lack the Name/Label columns, so the row is added
  with `tools\New-ProDiagSupervisionXlsx.ps1` and imported) and HMI 104 (faceplate type FPBasic created and, in V19,
  released via the TIA UI; instance via Openness). The test checks 1169 points.

## Status

Every area that has V21 samples now has V19 and V20 samples of the same kind, and the parser reads them like V21.
What Openness V19/V20 cannot create was built in the TIA UI via VNC (2026-11-21): the integrated HMI connection and
an HMI text list (105, both versions) and a library type from a PLC data type in V19 (203). The test checks 1192
points.

## Steps (each 10 to 20 minutes)

1. Sample sets per version: repeat the core V21 samples in V19 and V20 with Openness ground truth (tags and tag
   tables, DBs and UDTs, FB/FC interfaces, SCL/LAD/FBD/STL code, project library types, a WinCC Unified screen with the
   common items, hardware with a network). Naming `v19_NNN_*` / `v20_NNN_*`, numbers as in the V21 samples they repeat;
   recorded in `samples\MANIFEST.md`.
2. Test harness: run the existing golden and differential tests over the version samples (the tests take the sample
   list from the folder; the extensions .zap19/.zap20 must be accepted).
3. Version profiles: add `VersionProfile.V19` and `V20` once their samples pass; move a decoding table into the profile
   only where a version is shown to differ.
4. Viewer and CLI: show the TIA version, drop the warning for supported versions.
5. Documentation: version per format statement in `docs\spec`, coverage table per version.

## Priorities (user decision 2026-11-21)

The goal is full parity: V19 and V20 get the same support as V21 in every area. The list below is only the order in
which the areas are worked on.

1. PLC (tags, DBs, UDTs, block interfaces, code in all languages, alarms, text lists)
2. HMI (WinCC Unified)
3. Libraries (project and global library)
4. Everything else (hardware details, networks, safety, technology objects …)

V18 and older are not installed and are not planned.
