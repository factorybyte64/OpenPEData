# OpenPEData – knowledge base on the TIA Portal V21 file format

Entry point for every session. Read this file first, then `00-overview.md` and `hypotheses.md` in `docs\spec\` of this repository.

Goals and non-goals: see ..\docs\GOALS.md (the only authoritative source).

## Storage
| Path | Content | Rule |
|---|---|---|
| `docs\spec\` (this repository) | **The entire format documentation in one directory** (since 2026-10-23, User). Structure files `<structure>.md` (established, with evidence), parser versions `<structure>.parser.md`, `hypotheses.md`, `LOG.md` (lab notebook), `unified-*.md`, research; its own `README.md` there. | New format documents only there. Shared git: commit only your own paths. |
| `samples/` | Copies/excerpts; authoritative are the samples, recipes (`MANIFEST.md`), tools and parser code in this repository (`samples\`, `tools\`, `dotnet\`, `parser\`). Here: `MANIFEST.md` (what each variant contains, what changed relative to the predecessor variant). | Never change originals. |
| `tools/` | Analysis scripts (hex dump, diff, entropy, structure templates). | Every script with a header comment "what for". |
| `parser/` | Parser code + regression tests against `samples/`. | Later possibly its own git repo. |
| `HANDOFF-4f-writing.md` | General task description for another AI model: writing project files without engineering software (4f). | Update when there are results. |

## Phases
1. **Inventory** – folder structure of an empty project, catalog all files (size, magic bytes, entropy, compressed/encrypted?).
2. **Container** – split the main store (expected: `System/PEData.plf`) into blocks/headers/indexes, identify the compression.
3. **Difference analysis** – diff minimal changes (one tag, one block, one comment) against the base project, map objects and type IDs.
4. **Semantics** – match the object model (devices, blocks, tags, types, HMI) against the ground truth from Openness/SimaticML export.
5. **Parser** – incremental, every confirmed structure immediately as code + test.

## Creating variants and ground truth
We create variants ourselves via User's TIA MCP server (Openness): open the base project, exactly one change, save, back up the project folder, export SimaticML, enter it in the sample manifest. Every change is noted as a recipe (MCP calls) in the manifest so that it is reproducible. The export serves as the ground truth against which every byte hypothesis is checked automatically.

## Rules
- **Prio 1+ (User, 2026-10-19): Do not read, run, install or adopt anything from jfk-solutions "TiaFileFormat" / TiaProjectBrowser (code, docs, binaries, NuGet).** Reason: commercial product; OpenPEData stays independent and is created without its code.
- `C:\GitHub\factorybyte\TiaMcpXE` on User's device: **read only**, never write.
- Legal: analysis only for interoperability purposes on our own projects.
