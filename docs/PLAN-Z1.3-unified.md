# Plan Z1.3: Fully break down WinCC Unified (gaps per COVERAGE.md)

Goals and non-goals: see [GOALS.md](GOALS.md) (the only valid source). As of 2026-10-29.

## Starting point

- Done and tested (samples 100–135, test_hmi): devices, HMI tags and tag tables, connections, alarm classes, discrete and analog alarms, screens with screen items, dynamizations, events and scripts, text and graphic lists (partly), cycles.
- Gaps per docs\COVERAGE.md: logging, recipes, user management/runtime security, task scheduling, reports, styles, script modules, controls, layers, system events, graphics, runtime settings.
- Constraint (GOALS.md section 4): only what Openness can read or write. Checked 2026-10-29 with the Openness help for Unified (help ZIP `OpennessWCCUenUS`) and `help` of TiaMcpXE (`TiaHmiUnified`).

## Delimitation by Openness scope

| Area | Openness / TiaMcpXE | in scope |
|---|---|---|
| Data logs, alarm logs, logging tags, audit trail | `HmiDataLog`, `HmiAlarmLog`, `HmiLoggingTag`, `HmiAuditTrail`; TiaMcpXE `dataLog`, `alarmLog`, `loggingTag`, `auditTrail` (list/get/create/set) | yes |
| Runtime settings (incl. reporting, diagnostics, screen settings) | `RuntimeSettings`; TiaMcpXE `runtimeSetting` get/set | yes |
| Languages and fonts | TiaMcpXE `languageAndFont` | yes |
| Global script modules | `HmiScriptModule`; TiaMcpXE `scriptModule` | yes |
| Graphic lists, system text lists | `HmiGraphicList`, system text lists; TiaMcpXE export/import | yes |
| System tags, plant view, OPC UA alarm types | TiaMcpXE `systemTag`, `plantView`, `plantViewNode`, `opcUaAlarmType` | yes |
| User management / runtime security | Users and roles are the project UMAC (Z1.1 H7, `project.umac`) | done |
| Recipes (parameter sets) | no Openness API for Unified V21 (only the control "Parameter set" as a screen item) | no |
| Task scheduling, system events | no Openness API | no |
| Styles/designs | only the property `StyleItemClass` on screen items | no (the property is emitted with the screen items) |
| Graphics (image library) | only references in properties (`BackGraphic` …), no graphic objects | no |
| Layers | no Openness API: the Unified Openness help knows no layers, the Openness truth of the screen samples has no property `Layer` (checked 2026-10-29, U7). Stored are 32 layers per screen, `_#ActiveLayer` and the layer GUID on every screen item (hmi-screen.md, unified-properties.md) | no (findings remain) |

## Steps

Samples get the number range **136–199** (HMI). The base is a copy of 101_hmi_base. Every change via MCP on a copy, recipe into the MANIFEST, truth via Openness (TiaHmiUnified get/list, export), result in docs\spec\, LOG.md, test in test_hmi, update the viewer accordingly.

| No | What | Samples |
|---|---|---|
| U1 | **Logging:** create data log, change property, logging tag, alarm log, audit trail | 136–144, audit trail 145–146 (done) |
| U2 | **Runtime settings:** one setting each from General, Screens, Alarms, Reporting; GMP with audit trail | 145–152 (done) |
| U3 | **Languages and fonts** | 153–155 (done) |
| U4 | **Global script modules** | 156–157 (done) |
| U5 | **Graphic lists, system text lists** | 158–159 (done; graphic list entries open, need a project graphic) |
| U6 | **Plant view, OPC UA alarm types, system tags** | 160–163 (done; plant objects with type open) |
| U7 | **Layers:** check whether Openness knows them as a screen property | – (done: out of scope) |
| U8 | **Comparison and coverage:** regenerate the coverage matrix, note in docs\COVERAGE.md | – (done 2026-10-29, 212 samples) |

Out of scope (no Openness interface): recipes, task scheduling, system events, styles/designs, graphics as objects, reports as objects.
