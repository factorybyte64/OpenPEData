# Program info of 039_fw41: load memory of the current archive (2026-11-20)

## Question

`parser\tests\test_proginfo.py` failed on `039_fw41` with load memory values that differ by a few bytes (OB +5, FC +6,
FB +608, DB -9). Is the parser wrong, or do the expected values belong to an earlier build of the archive?

## Background

`039_fw41.zap21` was rebuilt on 2026-11-10 (owner rename, `samples\MANIFEST.md`, "Rebuild 2026-11-09") and once more
on 2026-11-11 01:49 with corrected recipes. TIA compiles a block to a slightly different load memory size on every
build (MANIFEST, "Rebuilt samples with open points"). The expected values in the test were measured in TIA "Program
info" on 2026-11-10.

## Measurement

- Copy of `samples\039_fw41.zap21` (SHA-256 `b4dce988284af9e1985bcba11ce26fe2e8634ea805dd226bac330158b7aafdf7`, equal to
  the sample) retrieved into a temporary folder outside the repository in an own TIA Portal V21 instance (TiaMcpXE
  `TiaProject(retrieve)`), opened **without compiling**.
- `TiaPlcSoftware(memoryList/block)`: `PlcBlock.LoadMemoryLength` / `WorkMemoryLength` of all 462 blocks (system blocks
  included), totals per block type "like the program information of TIA Portal". No block was unavailable
  (no NOT_CONSISTENT). Full result: `memoryList_2026-11-20.json`.
- Project closed without saving, instance ended. The sample was not touched.

| Row | TIA (Openness) 2026-11-20 | Parser | Old expectation (2026-11-10) |
| --- | --- | --- | --- |
| OB load memory | 2416 | 2416 | 2411 |
| FC load memory | 397605 | 397605 | 397599 |
| FB load memory | 3045659 | 3045659 | 3045051 |
| DB load memory | 1376 | 1376 | 1385 |
| Used (incl. data types 28164, PLC tags 836) | 3476056 | 3476056 | 3475446 |

## Result

The parser is right: it reads the compiler results stored in the archive, and TIA reports the same values for the
archive now in `samples\`. The old expectations belonged to the build of 2026-11-10. The expectations in
`test_proginfo.py` were updated to the measured values. Work memory, local data, call and dependency structure were
not affected. The two known local data deviations (Ack_Alarms, RT_INFO: TIA shows 2 bytes more) stay known
deviations; Openness offers no API for the local data column, so they were not measured again.
