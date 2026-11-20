# OpenPEData – Goals and non-goals

Project: analysis of the TIA Portal V21 project file format and development of a parser
As of: 2026-10-25

---

## 1. Purpose

OpenPEData makes the content of TIA Portal V21 projects readable without the engineering software. It is based on an independently developed format description, confirmed with our own test projects.

## 2. Goals

| No. | Goal | Result / criterion |
|---|---|---|
| **Z1** | **Fully understand the format and build a parser.** Projects (project folder `.ap21`, archive `.zap21`) and global libraries (library folder `.al21`, archive `.zal21`) are fully decoded. | The parser emits the content of projects and libraries in structured form as JSON (later possibly close to SimaticML). Every statement about the format is backed by a sample and a test. |
| **Z1.1** | **Fully understand devices and networks:** device view, network view, topology view and all related settings. This includes: devices and modules with slot, article number and firmware; all module parameters; I/O address ranges and process images; interfaces (PROFINET, PROFIBUS, Ethernet) with IP addresses and device names; subnets and IO systems; connections (HMI, S7, open communication); port interconnection and topology; synchronization (sync domains, IRT, isochronous mode); integrated device descriptions (GSD/GSDML); distributed I/O; Safety parameters; protection and security settings (access levels, certificates); device services (web server, OPC UA, time of day, diagnostics). | Every element of the hardware and network configuration is emitted with all properties that Openness can read or write (see 4.); comparison with Openness. |
| **Z1.2** | **Fully understand the PLC as a device:** everything that belongs to a controller in the project. This includes: CPU properties and settings (startup, cycle, clock memory, protection, web server, OPC UA, etc.); program blocks (OB, FB, FC, DB, instance DBs) with interfaces, attributes and code in all languages (LAD, FBD, SCL, STL, GRAPH, CEM); every language element and every instruction of the instruction catalog; PLC tags, tag tables and constants; PLC data types (UDTs); technology objects; watch and force tables; PLC alarms and text lists; software units, namespaces and external sources; online/load status information, as far as stored in the project. | Every PLC object is emitted with all properties and contents that Openness can read or write (see 4.). Code is identical to the TIA text or SimaticML; coverage report over all language elements and instructions. |
| **Z1.3** | **Fully break down and understand WinCC Unified:** everything that belongs to a Unified device (Comfort Panel, PC runtime) in the project. This includes: device and runtime settings; HMI connections; HMI tags, tag tables and acquisition cycles; alarms (alarm classes, alarm groups, discrete, analog and controller alarms); screens, pop-up and screen windows, layers; all screen items and controls with all properties; dynamizations, events, scripts and global script modules; text and graphic lists, graphics; logging (data and alarm logs); parameter sets (recipes); user management, roles and rights; task scheduling; reports; styles and designs; faceplate instances; languages and translations. | Every Unified object is emitted with all properties and contents that Openness can read or write (see 4.); comparison with Openness. |
| **Z1.4** | **Fully understand libraries:** all library elements that exist, in the project library and in global libraries (`.al21`, `.zal21`). This includes: types of every kind (PLC blocks, PLC data types, faceplates, HMI data types, scripts, styles and all other type kinds); type versions with status (in work, in test, released), default version, author, time, comment and GUID; dependencies between types; instances and usage locations in the project, including outdated instances; master copies of every kind (blocks, tag tables, devices, screen items, etc.) with content; folder structure; library properties, guidelines and documentation. Faceplate types with interface, content, dynamizations and scripts per version. | Every element of the project library is emitted with all properties, versions and relationships, also beyond the scope of Openness (exception per 4.); global libraries within the scope of Openness; every documented library and faceplate function is mapped to a storage location and confirmed. |
| **Z1.5** | **Display of all parser results as a WPF UI.** Project navigation as in TIA Portal with a detail view for every object; in particular, HMI screens are rendered and displayed with their screen items. | Every parser result is visible in the UI; HMI screens are displayed true to the original as in TIA Portal. |
| **Z2** | **Later long-term goal: writing.** | Project files can be created and validly signed. Not in work for now (see N1). The first step is the feasibility proof 4f (`tia-format\HANDOFF-4f-writing.md`): first a confirmed answer as to which check levels the engineering software enforces and what follows from that for writing; only if that permits it, a minimal writer that changes an existing, already decoded value in a project copy (append records, recompute record and chain hash, create the closing record, update or omit the index) so that TIA opens the file without complaint and displays the changed value. Not part of 4f: full creation of arbitrary objects from scratch. |

## 3. Non-goals

| No. | Non-goal |
|---|---|
| **N1** | Writing or modifying PEData files in the current phase. Work is strictly read-only (long-term goal Z2 remains). |
| **N2** | Use of jfk-solutions "TiaFileFormat" or "TiaProjectBrowser" in any form: nothing of it is read, executed, installed or adopted (code, documentation, binaries, NuGet). OpenPEData is not related to this commercial product. |
| **N3** | Adoption or execution of third-party code. Public prior work (e.g. ERNW tia-parser, TIAconv) serves only as a hint; its code is not executed, not installed and not copied (TIAconv, GPL: not at all; ERNW only with attribution); every statement is confirmed with our own samples. |
| **N4** | Changes to the TiaMcpXE project; it is only read and used. |

## 4. Constraints

- Scope of the analysis: the PEData are analyzed to the extent that the Openness interface can read and write data. Exception: the project library is analyzed completely.
- Existing findings in case of conflicts: findings obtained before this rule that go beyond it remain stored. Further plans are adapted to the rule.
- Method: differential analysis with test projects that differ by exactly one change; reference data from Openness.
- TIA changes only on copies; originals remain unchanged.
- No installations without approval, no publishing (git push) of the repository.
- Lower priority, but not excluded: runtime files (`IM\HMI`), multilingual support.

## 5. Validity

This document is the only valid description of the goals and non-goals of OpenPEData. Other documents (READMEs, plans) do not describe goals of their own but refer here.
