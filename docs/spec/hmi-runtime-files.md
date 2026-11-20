# WinCC Unified: runtime files in the project folder (established, U8)

Evidence: samples 101–126, commit 42b0e7a on the user's machine, details and file table (size, magic, entropy) in `samples\UNIFIED-RUNTIME.md` there.

## Result
`IM\HMI\*`, `AdditionalFiles\RtIdFiles` and the `ValidationFiles` are **derived compile or download data**. The parser does not need them; they belong only in the inventory.
- They are created when the HMI device is created.
- In samples 102–124 all 48 files are byte-identical to 101_hmi_base (SHA-256). Tags, alarms, screens, objects, scripts and all names are only in `PEData.plf`.
- Sample 126_hmi_subnet (124 plus subnet PN/IE_1 for PLC and HMI): deletes the snapshot `IM\HMI\I\0\Saved` (31 files: RDF, zips, pbb, json, fonts, DownloadTask.xml, CompileTimestamp.txt); CAI.bin, HIDS.bin, LCSI.bin and one ValidationMessages file change. Parser and tests keep running (test_hmi 54/54).
- A failed compile changes no file.

## Deferred (user)
What a successful compile writes and the format of the runtime files (RDF, magic `52 44 46 00 15 00 02 01`). Prerequisite: set the panel's runtime password once in TIA (Openness V21 cannot), on the PLC side configure protection of confidential configuration data (`plcMasterSecretUnprotect` rejected by the permission check, `accessLevelSet FullAccess` worked). MCP-027 in the backlog.
