# OpenPEData – TIA Portal V21 project file format

OpenPEData is an independent analysis and parser project; it is not related to the commercial product "TiaFileFormat" by jfk-solutions.

Parser and tests are in `dotnet\` (library `OpenPEData.dll`, CLI `OpenPEData.Cli.exe`, see `dotnet\README.md`);
the viewer is in `ui\`. The Python parser `parser\` and the Python scripts in `tools\` and `analysis\` are
**for test and analysis purposes only** (reference, truth tests, format research) and are not shipped.

Goals and non-goals: see docs\GOALS.md (the only authoritative source).

## Approach

Differential analysis with controlled samples: TiaMcpXE (Openness) is used to create projects that differ from
a predecessor by exactly **one** change (empty project → +1 CPU → +1 block → rename …).
The byte diff between two samples shows where and how this change is stored. Openness supplies the
truth (names, values, exports) against which the parser is later tested automatically.

Order (from the outside in):
1. Container: folder structure, `.zap21` (ZIP), `.ap21` (XML), `ProjectInfo.txt` – done for 000_empty
2. Standard formats: `Vci.db`, `XRef.db` (SQLite), `IM\SearchIndex` (Lucene) – read with existing tools
3. Core: `System\PEData.plf` + `PEData.idx` (proprietary binary format, partly zlib) – header, record frame, index,
   compression, then object/type model
4. Let the parser grow iteratively; every finding gets a test against a sample

## Where findings are kept

**This repository is authoritative:** `docs/spec/` for the format description (incl. hypotheses and lab notebook) and
`docs/GOALS.md` for goals and non-goals. In addition there are `samples/` (incl. `MANIFEST.md`), `tools/`, analysis outputs
and the parser code.

| Location | Content |
| --- | --- |
| `docs/spec/` | Established specification, one file per file kind. Every statement with status `confirmed` / `likely` / `conjecture` and evidence (sample + offset). |
| `docs/spec/hypotheses.md` | Open questions and hypotheses with ID (H-001 …), status and the test that would settle them. |
| `docs/spec/LOG.md` | Chronological lab notebook: what was done, what was observed, including dead ends (append only). |
| `docs/GOALS.md` | Goals and non-goals (the only authoritative source). |
| `samples/MANIFEST.md` | Every sample: name, predecessor, exact change, TIA version, creation method. |
| `samples/<nnn>_<name>.zap21` | Test project as an archive (a few only as project folders) + `<nnn>_<name>_truth/` with the Openness truth. |
| `tools/` | Analysis scripts (inventory, MCP client, diff, dumpers). |
| `dotnet/` | Parser (.NET: `OpenPEData.dll`, CLI, tests), see `dotnet\README.md`. |
| `parser/` | Python parser, test and analysis only (truth tests, format research), see `parser\README.md`. |

Rules: samples are never changed after they have been created (analysis only on copies). If a statement in
`docs/spec` turns out to be wrong, it is corrected and noted in `docs/spec/LOG.md`. Versioning the folder with git
is recommended so that the state of knowledge stays traceable.

## License and legal

Code and documentation of this repository are licensed under the Mozilla Public License 2.0 (`LICENSE`). Excluded are
third-party contents in the test projects under `samples/`, see `NOTICE`.

OpenPEData is not affiliated with Siemens AG and is neither endorsed nor reviewed by it. SIMATIC, STEP 7,
TIA Portal and WinCC are trademarks of Siemens AG; all trademarks belong to their respective owners and are used here only
descriptively. The file format was worked out exclusively by observing and testing our own test projects
in order to achieve interoperability; no Siemens program code was decompiled or copied.
The software is provided without warranty.
