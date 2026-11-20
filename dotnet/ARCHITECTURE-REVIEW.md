# Architecture review `OpenPEData.dll`

Revision 2, as of: 2026-11-02. Reviewed was the working state in `dotnet\src\OpenPEData` (net10.0,
27 source files, about 10,400 lines), plus `OpenPEData.Cli`, `tests\OpenPEData.Tests`, the call site in the viewer
(`ui\OpenPEData.Viewer\ParserRunner.cs`) and, for revision 2, the viewer's Unified renderer (`ui\OpenPEData.Viewer`:
`ScreenRenderer.cs`, `SvgRenderer.cs`, `TemplateRenderer*.cs`, `TemplateExpr.cs`, `ScriptEval.cs`, `HmiDefaults.json`).
Read only; nothing changed, nothing built or committed.

**Assumptions of this revision (roadmap according to the user, 2026-11-02; not yet included in `docs\GOALS.md`):**

- **A1** Today the DLL is built only for TIA Portal V21. In future, further TIA versions are to be supported.
- **A2** The entire Unified renderer moves from the viewer into the DLL. Rendered HMI screens can then be exported
  directly with the DLL, without the viewer.

Both are future, not the next step. For this review it mainly means: whatever is fixed now in the public
interface must not block A1 and A2. Where a recommendation from revision 1 turns out differently for that reason,
this is stated.

## 1. Summary

The DLL is built as a **literal, byte-identical port** of the Python parser, and for that it is well done: clear
folders (Runtime / Format / Model), one file per Python module, no third-party packages, strong differential tests
against a baseline over 444 inputs × 7 modes. As a port, the architecture is consistent.

As a **standalone .NET library** that is shipped professionally or offered as a NuGet package, however, it has a
fundamental problem: the Python semantics are not hidden behind an interface, they **are** the interface.
Results are untyped `PyDict` trees, errors are `PyKeyError`/`PyStopIteration` with Python messages,
and all 490 public members of the three inner layers are `public`. Every future change to the internals is thus
a break of the public API, and callers (already the viewer today) bypass the facade.

The most important recommendation remains **a thin, clean public facade in front of the unchanged core**; the core
becomes `internal`. A1 and A2 add three points:

1. **Version as a concept of the API.** The facade detects the TIA version, reports it and rejects unknown ones with
   its own exception. Version-dependent knowledge (file extensions, header formats, names in the metamodel, default
   values, enum tables) is bundled instead of scattered (3.14).
2. **A screen is a result like any other.** The facade gets a render interface. Underneath lies a
   platform-neutral scene model with SVG as the first output format. The viewer becomes a consumer of this model
   (3.15).
3. **Python equality ends at V21.** The Python parser is now only a test and analysis tool and will not be kept up
   to date for new versions. For V22+ and for the renderer, the .NET outputs themselves become the reference
   (golden files). As a result, `PyDict` as the result form and the Python runtime layer at the outer edge
   lose their justification earlier than assumed in revision 1. Typed results move up from P3 to P2 (3.6).

| Prio | Topic | Effort |
| --- | --- | --- |
| P1 | Reduce the public surface to a facade, core `internal` (+ `InternalsVisibleTo` for tests) | M |
| P1 | Load the project only once: session object instead of static path functions, **with version detection** | S–M |
| P1 | Close mutable public tables (among others `Verify.Sha256DigestInfo`) | S |
| P1 | Own exception hierarchy at the facade, **incl. `UnsupportedVersionException`**; Python error types stay internal | S–M |
| P1 | **API names and docs version-neutral** (no “V21” in type, method or package names) | S |
| P2 | Limits for untrusted inputs (zip/zlib size, DTD) | S |
| P2 | Narrow error catching (`IsDecodeError`, `catch (Exception)`) | M |
| P2 | Viewer only via the facade; avoid the JSON detour string → `JsonNode` | S |
| P2 | Do not disable nullable warnings project-wide | M |
| P2 | NuGet metadata, `Directory.Build.props`, license file | S |
| P2 | Typed result classes, starting with Verify/ProgInfo **and the HMI screen model** (prerequisite for 3.15) | L |
| P2 | **Version profile:** bundle version-dependent knowledge in one place (3.14) | M |
| P2 | **Renderer into the DLL:** separate logic from WPF drawing, scene model, SVG output (3.15) | L |
| P3 | Raster output (PNG) without WPF, text measurement without WPF (3.15.5) | L |
| P3 | Naming, documentation gaps, CLI arguments, test framework | S–M |

## 2. Current architecture and target picture

### 2.1 Current

```
Viewer (ui, net10.0-windows, WPF)
   │  ScreenRenderer / SvgRenderer / TemplateRenderer / TemplateExpr / ScriptEval / HmiDefaults.json
   │  (work on JsonNode, global static state)
   ▼
ParserRunner ──►  PEData (static)  ──►  Model (ProjectModel, Hmi, Hardware, Library, Faceplates, Code, …)
   │                  │                      │
   └──► Runtime.*  ◄──┴──────────────────────┴──►  Format (Project, Plf, ObjectRecord, Metamodel, Attrs,
        (PyDict, Py, PyJson directly)                       Verify, Xref, SqliteReader)
                                                              │
                                                              └──► Runtime (PyDict, Py, PyJson, Bin, Zlib, Et, PyErrors)
```

The layering **inside** is clean: Runtime knows nothing of Format, Format nothing of Model, no cycles. The
weaknesses are at the **outer edge**:

- `PEData` is the intended facade, but `Format`, `Model`, `Runtime` and `TextTree` are just as public. The
  viewer uses `Runtime.PyDict`, `Runtime.Py.Str` and `Runtime.PyJson.DumpsStrict` directly
  (`ParserRunner.cs`, lines with `OpenPEData.Runtime.`), the CLI uses `Format.Verify.TextReport` and `PyJson.Dumps`.
- Viewer and CLI each implement the same flag logic themselves (`--verify` before `--xref` before `--proginfo`, error
  lines “not readable: …”, exit code from `rep["ok"]`). This logic belongs in the library.
- About 3,200 lines of domain logic for HMI screens (default values, style resolution, faceplates, visibility rules,
  template evaluation) live in the viewer and are interwoven with WPF there. Other users of the DLL cannot get at
  this logic.
- V21 is hard-wired in several places in the code (3.14), without a central place.

### 2.2 Target (with A1 and A2)

```
                 ┌───────────────── public ───────────────────┐
Viewer (WPF) ──► │ TiaProject (facade)        HmiRenderer      │ ◄── CLI, third-party applications
CLI          ──► │  .Version  .Structure()    .Render(screen)  │
                 │  .Verify() .ProgramInfo()  → Scene → SVG    │
                 │  typed results, OpenPEDataException         │
                 └───────────────┬──────────────────┬──────────┘
                                 │ internal         │ internal
                    Model  ◄─────┘        Rendering (scene model, layout, templates, scripts,
                      │                              SVG reader/writer, text metrics)
                    Format ◄── VersionProfile (per TIA version: extensions, header, names, defaults, enums)
                      │
                    Runtime
```

- One assembly `OpenPEData` for reading **and** rendering up to SVG output. The renderer needs internal model types
  as soon as there are typed HMI objects. In the same assembly this works without additional public surface. It
  stays platform-neutral (`net10.0`, no WPF), so splitting into two packages is not necessary.
- Rasterization (PNG) is the one part that needs a platform or package decision. It sits behind an
  interface (3.15.5) and can move into its own optional package without burdening the core package.
- The viewer keeps only a thin WPF backend that draws the scene, plus the user interface.

## 3. Findings in detail

### 3.1 Public interface (P1)

**Finding.** 490 `public` members, by far the largest part of them implementation detail with Python names and
abbreviations: `ProjectModel.P`, `Project.Mm`, `ObjectRecord.B27`, `Flags16`, `Py.Or`, `Py.OrElse`, `Py.T`,
`Bin.U32s`, `Attrs.BindStore`, `Hmi.TextLines` etc. `CS1591` (missing XML documentation) is disabled, so most of it
remains public and undocumented.

**Why it matters.** Everything public is contract. With today's layout, every format insight that changes a
signature in `Format` or `Model` is a breaking change for NuGet users. A1 makes this sharper: a new
TIA version almost certainly brings changes in `Format` and `Model`. If the core is public, every new version
becomes a major release of the DLL.

**Proposal.**
- Set all types under `Runtime`, `Format`, `Model` as well as `TextTree` to `internal`. The same applies to the future
  rendering layer, except for the scene and option types from 3.15.
- `[assembly: InternalsVisibleTo("OpenPEData.Tests")]` and `("OpenPEData.Cli")`, so that tests and CLI keep
  running unchanged. The differential test thus stays equally meaningful.
- Only the facade from 3.2 plus the result, scene and exception types stay public.
- Optionally later: pin the public API with a `PublicAPI.Shipped.txt` (the analyzer is only a
  development package and does not go into the shipped DLL; approval per project rule required).

### 3.2 Loading and lifecycle (P1)

**Finding.** Every function of `PEData` loads the project from scratch:

- `Structure`, `Xref`, `ProgramInfo` and `Text` each create a new `ProjectModel(path)`, i.e. read plf, compute all
  record hashes, parse the metamodel XML, build the reverse index (`PEData.cs`).
- The viewer calls several modes per opened project; that is several complete load operations.
- Within `Structure` the library is extracted twice: `Model.ToDict` calls `Library.Extract(p, keys)`,
  `Faceplates.Extract` calls `Library.Extract(p, true)` once more (`Faceplates.cs` ~805).
- The `.zap21` is opened anew for plf, `PEData.idx`, `XRef.db` and GSDML (`Hardware.GsdmlSubmodules`) each time.
- There are only paths as input, no `Stream`/`byte[]`, no `CancellationToken`, no progress.
- The version of the project is determined nowhere. It follows implicitly from the fact that only `.ap21`/`.zap21`/
  `.al21`/`.zal21` are recognized.

**Proposal.** A session object that loads once, detects the version and produces the partial results lazily:

```csharp
public sealed class TiaProject : IDisposable
{
    public static TiaProject Open(string path, OpenOptions? options = null, CancellationToken ct = default);
    public static TiaProject Open(Stream archive, OpenOptions? options = null, CancellationToken ct = default);
    public static TiaVersion? Probe(string path);       // only detect, do not load

    public TiaVersion Version { get; }                  // e.g. V21; from extension, plf header and metamodel
    public ProjectKind Kind { get; }                    // Project | GlobalLibrary
    public IReadOnlyList<ParseIssue> Issues { get; }    // today statistics.errors
    public JsonObject Structure(bool includeKeys = false);
    public IntegrityReport Verify();                    // without Model, only plf/idx
    public JsonObject? CrossReferences();               // null without XRef.db
    public JsonObject ProgramInfo();
    public HmiRenderer Hmi { get; }                     // 3.15: list and render screens
    public string ToText();
    public string ToJson(JsonFormat format = JsonFormat.PythonCompatible);
}

public readonly record struct TiaVersion(int Major, int Minor = 0)   // V21 = (21, 0); updates possibly as Minor
{
    public static IReadOnlyList<TiaVersion> Supported { get; }
}
```

- Internally it holds a `Project` and a `ProjectModel` (both unchanged) and caches `Library.Extract` once.
- The archive is opened once and passed to `ProjectFiles` as `IProjectSource` (folder or zip). This replaces
  the scattered `EndsWithCi(".zap21")` switches in `ProjectFiles.ReadPlf`, `Member`, `Verify` and
  `Hardware.GsdmlSubmodules`. The extensions come from the version profile (3.14).
- `OpenOptions` gets `AllowUnknownVersion` (default `false`): a project of a newer, not yet evidenced
  version is then read tentatively with the closest profile, and the deviation is reported in `Issues`. Without the
  option there is `UnsupportedVersionException` (3.4).
- The JSON results carry the detected version (`"tiaVersion": "V21"` at the top level), so that stored
  outputs of different versions remain distinguishable. For V21 in Python-compatible mode the key is absent,
  otherwise the differential test breaks.
- The static `PEData.*` functions can remain as thin forwarders marked `[Obsolete]`, so that
  nothing breaks.
- Document: an instance is **not thread-safe** (the caches `Project.attrs`, `Project.isA` are plain
  `Dictionary`s); different instances in parallel are no problem. This also applies to rendering (3.15.3).

### 3.3 Mutable global state (P1, small and important)

**Finding.** Several decoding tables are `public static readonly`, but mutable:

| Location | Effect if a caller changes them |
| --- | --- |
| `Format\Verify.cs:15` `Sha256DigestInfo` (byte[]) | Signature check reports wrong results, process-wide. Security-relevant, because `--verify` is meant precisely to detect tampering. |
| `Format\Attrs.cs:312/324` `ExpandoCodes`, `InlineCodes` | Expando decoding of all projects changes |
| `Model\Hmi.cs:16` `PropertyNames` | HMI property names wrong |
| `Model\Code.cs:14–120` `Operators`, `Keywords`, `Punctuation`, `Containers`, `AttributeText`, `IdentKinds` | SCL/LAD text wrong |
| `Model\Styles.cs:19`, `Model\Interface.cs:13` | likewise for styles and section order |

**Proposal.** Make them `private`/`internal` (taken care of by 3.1) and additionally switch to `FrozenDictionary`/
`FrozenSet` or `ReadOnlySpan<byte>`/`ImmutableArray`. Small, risk-free, safeguarded by the differential test.

A1 adds: part of these tables is version-dependent, such as `ExpandoCodes`, `PropertyNames`, names of
instructions and enum names. They should not only be frozen but assigned to the version profile
(3.14). Then a V22 project never accidentally reads a V21 table.

The renderer in the viewer brings even more global state today: static registries for graphics, screens,
faceplates and text lists, `[ThreadStatic]` fields for style and nesting, plus `RuntimeVisibility` and
`Language` as global properties that `TemplateExpr` also reads (3.15.3). It must not move into the DLL like this.

Side finding: `Styles.Enums()` loads `unified_enums.json` into a static field without a lock. Harmless (at worst
loaded twice), cleaner with `Lazy<T>`. `PyJson` controls the strict mode via a `[ThreadStatic]` field; that is
thread-safe, but hidden ambient state. Better to pass it as a parameter through `Write`.

### 3.4 Error handling (P1/P2)

**Finding at the outer edge (P1).** A .NET caller gets Python exceptions with Python texts:

- no plf found → `PyIndexError("list index out of range")` (`Format\Project.cs` ~126),
- no metamodel record → `PyStopIteration` with an empty message (~132),
- file is neither a zip nor a folder → `InvalidDataException` from `ZipArchive`,
- `PyKeyError`, `PyTypeError("can only concatenate str (not \"NoneType\") to str")` etc. from the model.

A project of a different TIA version (`.ap20`, `.ap22`) is today not recognized as a project at all (wrong extension),
or it fails somewhere in the model with one of these Python messages. The caller does not learn that the version
is the reason.

Many of these exceptions are created **artificially** in the core, only so that message text and type match
Python (e.g. `throw new PyAttributeError("'NoneType' object has no attribute 'decode'")` in
`PlcTables.cs` ~200, `Xref.cs` for the `location` field). For byte equality this is necessary; to the outside it is noise.

**Proposal.** A small, dedicated hierarchy at the facade:

```
OpenPEDataException
 ├─ NotATiaProjectException      (no folder/zip, no System\PEData.plf, no metamodel)
 ├─ UnsupportedVersionException  (TIA version detected, but no profile; property Version)
 ├─ UnsupportedFormatException   (version unclear, unknown header)
 ├─ CorruptProjectException      (structure not readable; InnerException = original PyError)
 └─ RenderException              (screen not renderable; individual screen items instead as an issue, 3.15.4)
```

The facade translates; the core and thus the byte-identical CLI output stay unchanged. `PyError` and its derived
types become `internal`.

**Finding in the core (P2).**

- `Attrs.IsDecodeError` (`Attrs.cs:29`) treats `ArgumentException`, `ArgumentOutOfRangeException`,
  `IndexOutOfRangeException` and `OverflowException` as “data not decodable”. In .NET, however, these are also the
  typical programming errors. A real bug in the port thus silently becomes “blob not readable” and is only noticed if
  a baseline hash happens to break. With A1 this weighs more: for a new version, “not decodable” is an
  expected, important hint at a changed format. It must not be mixed up with programming errors.
- Blanket `catch (Exception)` without a filter: `PlcTables.cs` ~202, `Hmi.cs` ~130, `Interface.cs` ~41 (return
  `null`/empty), `ProjectModel.Guarded` (`Model.cs:58`, writes every exception into `statistics.errors`, including
  `OutOfMemoryException` or `OperationCanceledException`).

**Proposal.** In `Guarded`, at least let `OperationCanceledException` and `OutOfMemoryException` pass through. For
`IsDecodeError`, check at which places Python really throws `IndexError` (index access on bytes/list) and use
`Py.At`/`Bin.*` specifically there, which already throw `PyIndexError`/`StructError`; then remove the .NET types from
the list. The differential test immediately shows whether a behavior breaks in the process.

### 3.5 Untrusted inputs (P2)

A parser for third-party project files should have limits:

- `ProjectFiles.ReadEntry` (`Project.cs` ~26) reads every zip entry completely into memory, without an upper limit.
- `Zlib` doubles the output buffer without limit (`Zlib.cs` ~339); a small blob can produce gigabytes.
- `Et` parses with `DtdProcessing.Parse` (`Et.cs:13`). External entities are closed off by `XmlResolver = null`, but
  internal entity expansion (“billion laughs”) is not explicitly limited.

A2 adds inputs that today only the viewer processes:

- SVG graphics from the project and the SVG rendering templates of the styles (XML, in the viewer with
  `DtdProcessing.Ignore` or `XDocument.Parse`), embedded Base64 raster images of arbitrary size.
- Scripts from dynamizations that `ScriptEval` evaluates. A script with an endless loop stops the viewer today.
- Nesting: faceplates (up to depth 4), screen windows (up to 4), template includes (up to 24) and SVG elements (up to 16)
  are limited, but not centrally.

**Proposal.** `OpenOptions` with `MaxEntryBytes`, `MaxDecompressedBytes` (default e.g. 1 GiB and 256 MiB respectively);
set `MaxCharactersFromEntities` in the `XmlReaderSettings` explicitly, or `DtdProcessing.Ignore` if the samples
need no DTD (checkable via the differential test). For rendering, `RenderOptions.MaxImageBytes`, a step limit for
`ScriptEval` and the nesting limits in one place.

### 3.6 Data model of the results (P2, strategic; P3 in revision 1)

**Finding.** Results are `PyDict`/`List<object?>`/`PyTuple`/`long`/`ulong`/`double`/`byte[]`. That is exactly the
Python value and therefore correct for byte equality. For .NET users, however, it means: keys as strings, casts
everywhere, no IntelliSense, no compiler help on format changes. `ToJsonNode` and the viewer take the detour
tree → JSON string → `JsonNode.Parse`, i.e. double memory and double work for large projects.

**Why the priority rises.**
- A1: The Python parser is now only a test and analysis tool and will not be kept up to date for further TIA
  versions. Byte equality with Python is thus a check criterion only for V21. For every new version the .NET
  output alone decides. Then a typed, version-neutral model is the natural place where
  version differences are absorbed: the reading layer is version-dependent, the result model stays the same.
- A2: The renderer today accesses the JSON tree via about 150 string keys (`p["Left"]`,
  `it["dynamizations"]`, `TreeBuilder.Str(...)`). In the DLL it should work on a typed HMI screen model,
  otherwise every rename in the JSON becomes a silent rendering error.

**Proposal, staged.**
1. Short term: remove `PyDict` from the public surface and output `System.Text.Json.Nodes.JsonObject` as the
   default result. Write a direct converter `PyDict → JsonNode` for this (without the string detour). `JsonNode` is
   in-box, familiar to every .NET developer and already the target format of the viewer anyway.
2. Medium term: typed `record`s for the small, stable results, first `IntegrityReport` (Verify), then
   `ProgramInfo` and `CrossReferences`. **In parallel the HMI screen model** (`HmiScreen`, `HmiItem` with type,
   position, layer, zIndex, parent, properties, dynamizations; `HmiStyle`, `HmiGraphic`, `FaceplateType`,
   `TextList`), because the renderer (3.15) builds on it. All are created from the `PyDict`; the core stays the same.
3. Long term: a typed object model (`Device`, `Plc`, `Block`, `TagTable`, `HmiScreen` …) as the actual
   API, with JSON only as its serialization. That is the direction that also fits Z2 (writing), because for
   writing you need a model with identity, not a tree. With A1 this model is at the same time the version-neutral
   layer.

### 3.7 Python runtime layer and home-grown components (soundness)

| Component | Sensible? |
| --- | --- |
| `Runtime\Py`, `PyDict`, `PyJson`, `PyTuple` | Yes, as long as byte equality with Python is the check criterion. Without this layer the differential test would not be possible. But it belongs in `internal`. With A1 this applies only to the V21 path. Code for new versions should no longer newly use this layer, but read directly into typed structures. |
| `Runtime\Bin` | Yes; clean, small. |
| `Runtime\Et` | Yes as an adapter, cleanly solved. |
| `Runtime\Zlib` (367 lines) | Only needed because of the exact zlib error messages and `decompressobj` partial outputs. `System.IO.Compression.ZLibStream` is in-box (no NuGet). If Python equality is dropped at some point, replace it: less code, faster, hardened. |
| `Format\SqliteReader` | Yes. There is no in-box SQLite reader, and `Microsoft.Data.Sqlite` would bring a package with a native DLL. For pure reading of rowid tables the home-grown solution is appropriate. For new versions, watch for a changed `info.version` of XRef.db. |
| RSA in `Verify` with `BigInteger.ModPow` | Works, because it only verifies and processes nothing secret. The output length is fixed to 256 bytes (`RsaOpen`), so it only fits 2048-bit keys; a different key yields a misleading message. More relevant with A1, because a new version may change the key. `RSA.ImportParameters` + `VerifyHash(…, RSASignaturePadding.Pkcs1)` would be the usual in-box solution. |
| `SvgRenderer` (viewer, 382 lines) | Yes as a separate, small SVG reader without a third-party library. Today, however, it directly produces WPF `Drawing`s and parses paths with `Geometry.Parse`. In the DLL it needs its own path and scene model (3.15.2). |
| `ScriptEval` (viewer, 361 lines) | Yes: a small interpreter only for what visibility scripts need, already platform-neutral today. In the DLL with a step limit (3.5). |

### 3.8 Types and structures in the core

- Public fields instead of properties (`PlfRecord`, `ObjectRecord`, `Project`, `Metamodel`); `readonly` only protects
  the reference, the lists and dictionaries behind it (`Project.Objects`, `Deleted`, `ExpandoDicts`, `Metamodel.Types`)
  are mutable from outside. `Link.Slot` is a publicly writable field (`ObjectRecord.cs` ~12). Taken care of with 3.1
  (`internal`); otherwise hand out `IReadOnlyList`/`IReadOnlyDictionary`.
- Record keys are strings (`"0000:0007:1"`), built with `MakeKey` and passed around everywhere as `string?`.
  A `readonly record struct RecordKey(ushort TypeLo, ushort TypeHi, ulong Oid)` with `ToString()` in today's format
  would be type-safe, faster (no string hashing) and save allocations. Only worthwhile after 3.1, because internal.
- `AttrSets : OrderedDictionary<string, PyDict>` inherits publicly; inheriting from collection types is a
  .NET anti-rule (prefer composition). Uncritical internally.
- Per object, all data contents are copied as a separate `byte[]` (`Plf.ReadRecords`, `AsSpan(...).ToArray()`); for
  large projects, `ReadOnlyMemory<byte>` views on the plf buffer are considerably more economical. Optimization, P3.
- The fixed metamodel key and type id `0x00070000` (`Project.cs` ~131) as well as the plf header (64 + 1 bytes, SHA-256,
  `0xFF`; `Plf.cs`) are V21 findings. They belong in the version profile (3.14), even if they turn out to be the same
  in V22.

### 3.9 Nullability and compiler help (P2)

`<NoWarn>CS8600;CS8602;CS8625</NoWarn>` disables the three most important nullable warnings **project-wide**, i.e.
also in Format and Runtime, where no dynamic trees are iterated. This makes `Nullable enable` effectively mere
decoration. Proposal: re-enable the warnings and suppress them only in the Model files via `#pragma warning disable` at
the top of the file (or via `.editorconfig` per folder). New code (version profiles, rendering) must build without
these exceptions. Additionally consider `TreatWarningsAsErrors` for Release.

### 3.10 Naming

- `PEData` (class) next to `OpenPEData` (namespace/assembly) and `Format.Verify` (class) next to `PEData.Verify`
  (method) as well as `Format.Xref` next to `PEData.Xref` are confusing. Proposal: facade `TiaProject` (or
  `OpenPEDataProject`), methods with verb or noun per .NET guidelines (`Verify()`, `GetCrossReferences()`,
  `GetProgramInfo()`, `ToText()`), file name “XRef” uniformly as `CrossReferences` in the API.
- **Name version-neutrally (A1):** no `V21` in public type, method, namespace or package names. The
  package description “Reads TIA Portal V21 projects …” (`OpenPEData.csproj`) and XML docs such as “Loads a TIA Portal V21
  project” (`Project.cs:100`, `Verify.cs:9`) should document the supported versions via `TiaVersion.Supported`
  instead of in the name.
- Internal abbreviations (`P`, `Mm`, `B27`, `Rel1`, `Py.T`, `Py.Or`) are understandable as port mirrors and okay
  internally, not publicly.
- Python origin in doc comments (“like model._strip”, “dict.get”) is helpful internally, but does not belong in
  the public XML documentation.

### 3.11 CLI

- Unknown switches are silently ignored (`--jsn` runs as text output); several mode switches at the same time
  are silently resolved in a fixed order. This is Python-equal, but should at least be stated in the usage; once the
  equality requirement is dropped, reject with an error message.
- `Main` accesses `Runtime.PyJson` and `Format.Verify.TextReport`; still possible with `InternalsVisibleTo`, better
  via the facade (`ToJson`, `IntegrityReport.ToText()`).
- With A2 the CLI gets the rendering that today only the viewer can do (`--screen-png`, `--compare`, `--lang`,
  `--runtime-visibility` in `MainWindow.xaml.cs:63–67`). Proposal: `OpenPEData render <project> --screen <name>
  [--format svg|png] [--out <file|folder>] [--lang de-DE] [--view editor|runtime]`, all screens with `--screen *`.
  The image comparison (`--compare`) is a check tool and belongs in the tests or `dotnet\tools`, not in the
  shipped DLL (3.15.6).
- With A1: `OpenPEData probe <path>` (detect version and kind without loading) is cheap and useful for users with
  projects in mixed states.

### 3.12 Tests (P3, more important with A1/A2)

Strong: differential tests over all samples and modes, golden tests for the Python semantics, process invocation of the CLI.
Weaknesses:

- Own test runner without a framework: no integration into the Visual Studio Test Explorer or `dotnet test`, no
  parallel or targeted re-running of individual cases, no standard output for CI (TRX/JUnit). The project rule
  “no packages” speaks for the home-grown solution; test packages, however, are not shipped. Decision with the user.
- Baseline comparison only via SHA-256: on a deviation you do not see *what* deviates without repeating the Python
  run. Proposal: on failure, write the .NET output into a folder so that it can be diffed directly.
- The tests depend on locally present samples and silently skip missing ones; on someone else's machine
  “green” then means little. The number of skipped inputs should be in the result.
- Tests of the **public contracts** as such are missing (exceptions on broken input, behavior with empty/
  truncated files, parallel calls of several instances). They come naturally with the facade from 3.2.

**With A1:**
- The baseline is today a Python baseline, thus only possible for V21. Every further version needs
  **.NET golden files** (output checked once and frozen, diff on deviation). Arrange samples and baselines by
  version (`samples\V21\…`, `samples\V22\…`), so that it is clear which version is covered how well.
- Test version detection: every extension, every known header, an unknown version (expected
  `UnsupportedVersionException`).
- Cross test “same project, upgraded with TIA”: a V21 sample migrated to V22 should yield the same result model
  except for evidenced differences. That is the best touchstone for a version-neutral model.

**With A2:** For the renderer there is no automated test today, only the manual comparison in the viewer
(`--screen-png`/`--compare`). Proposal in 3.15.6.

### 3.13 Packageability (NuGet) (P2)

For a clean package, `OpenPEData.csproj` is missing:

```xml
<PackageId>OpenPEData</PackageId>
<Authors>…</Authors>
<PackageLicenseExpression>…</PackageLicenseExpression>   <!-- LGPL-3.0-only or MPL-2.0, user's decision -->
<PackageReadmeFile>README.md</PackageReadmeFile>
<RepositoryUrl>…</RepositoryUrl>
<PackageTags>tia-portal;siemens;plc;s7;wincc;unified;hmi;parser</PackageTags>
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
<ContinuousIntegrationBuild Condition="'$(CI)'=='true'">true</ContinuousIntegrationBuild>
<IsTrimmable>true</IsTrimmable>     <!-- no reflection found; check with the trim analyzer -->
<IsAotCompatible>true</IsAotCompatible>
```

In addition:

- Version and `Product` are duplicated in two csproj files; move them into a `dotnet\Directory.Build.props`.
- There is no `LICENSE` file in the repository. Required for a package, regardless of which license it will be.
  Note on the open question: with LGPL-3.0, users may link the DLL into proprietary programs, but must make
  replacing the DLL possible; with MPL-2.0 the disclosure obligation applies only to modified files of the library
  itself. For a NuGet library MPL-2.0 is the lower-friction choice; LGPL protects the parser more strongly. This remains
  the user's decision.
- Only `net10.0`. That is LTS and fits. An additional `net8.0` would not be possible without rework, because the generic
  `OrderedDictionary<TKey,TValue>` only exists from .NET 9 on. No action needed, just for information. **Important with A2:**
  The DLL must remain `net10.0`, not `net10.0-windows`. The renderer must therefore not bring WPF along (3.15).
- The embedded resource `unified_enums.json` has a global `LogicalName`; better with a prefix
  (`OpenPEData.Data.unified_enums.json`), so that it does not collide in bundles. With A1 and A2 further
  data files are added (enums and `HmiDefaults` per version). Proposal: `OpenPEData.Data.V21.unified_enums.json`,
  `OpenPEData.Data.V21.hmi_defaults.json` etc.
- Versioning of the package (SemVer): a newly supported TIA version is a **minor** release as long as the
  facade stays the same. This only works if 3.1 is implemented.
- Fonts: Siemens Sans must not go into the package (Siemens license). Text measurement must work with installed fonts
  or substitute metrics (3.15.5).

### 3.14 Multiple TIA versions (new, A1)

**Finding: where is V21 today?**

| Location | What | Kind |
| --- | --- | --- |
| `Format\Project.cs:18–98`, `Verify.cs:329–372` | Extensions `.ap21`, `.zap21`, `.al21`, `.zal21` as fixed strings | File system |
| `Format\Plf.cs:6–9` | plf header 64 + 1 bytes, SHA-256, `0xFF`; record frame and keys | Binary format |
| `Format\Project.cs:131` | Metamodel record via fixed key or type id `0x00070000` | Binary format |
| `Format\Attrs.cs:312/324` | Expando and inline codes | Binary format |
| `Format\Verify.cs` | idx structure, RSA-2048 signature of `$$COMMIT$` | Binary format, cryptography |
| `Model\*.cs` | about 210 string literals with metamodel type, attribute and relation names (`"IVersionInfo"`, `"VersionToContent"`, `"ILScreenItemParent"` …) | Semantics |
| `Model\Hmi.cs:16`, `Code.cs:14–120` | Property names, instruction and operator tables (parts from Siemens name tables) | Semantics |
| `Data\unified_enums.json` | Openness enum names of the Unified styles | Data |
| `ui\OpenPEData.Viewer\HmiDefaults.json` | Openness default values per screen item type (from sample 165, V21) | Data, in the DLL after A2 |
| Renderer in the viewer | Dimensions and colors measured on the V21 runtime (title bar 32 px, toolbar 70 px, table header 30 px …) | Presentation |

What is favorable: the metamodel is stored **in the project itself** (`Metamodel.FromRecord`). Type ids are already
resolved via names from the file today, not hard-wired. If a new TIA version changes type ids but not the names,
the core runs without change. That is a good starting position.

**Proposal.**

1. **Version detection first, separate from loading.** `TiaVersion Probe(path)` reads extension, plf header and
   metamodel identifier without decoding records. The facade then selects the profile.
2. **`VersionProfile` (internal) bundles what is version-dependent**, as data instead of as code branches:
   ```csharp
   internal sealed record VersionProfile(
       TiaVersion Version,
       IReadOnlyList<string> ProjectExtensions, IReadOnlyList<string> ArchiveExtensions,  // .ap21 / .zap21 / .al21 / .zal21
       PlfLayout Plf,                        // header length, hash, record frame
       MetamodelNames Names,                 // type, attribute, relation names that Model uses
       FrozenDictionary<…> ExpandoCodes, FrozenDictionary<…> PropertyNames, …,
       string EnumsResource, string HmiDefaultsResource);
   ```
   The core gets the profile passed through `ProjectModel` and asks for `profile.Names.VersionToContent` instead of
   `"VersionToContent"`. For V21 this is a pure renaming; the differential test stays the same.
3. **Branch on deviations only where they are evidenced.** No precautionary `if (version >= 22)` in the model. If
   a version really stores a structure differently, it gets its own reader behind a small
   interface (e.g. `IHmiItemReader`). The result model (3.6) stays the same.
4. **Evidence requirement per version.** Every format statement in `docs\spec` gets the version on which it is evidenced.
   Profiles are only created once there are test projects of that version. A profile “V22 = V21” without evidence is worse
   than `UnsupportedVersionException`.
5. **Version switch in the outputs.** `tiaVersion` in the JSON (3.2), in `IntegrityReport` and in the text output.
6. **Older versions (V17–V20):** extensions and probably parts of the format differ; according to the format
   description the metamodel principle is the same. Whether older versions are a goal is open and determines how far the
   profile must reach back (see open points, section 6).

### 3.15 Unified renderer in the DLL (new, A2)

#### 3.15.1 Inventory in the viewer

| File | Lines | Role | WPF binding |
| --- | --- | --- | --- |
| `ScreenRenderer.cs` | 1431 | Screen, all screen item types, faceplates, screen windows, graphics, PNG, comparison | strong: builds `Canvas`/`Border`/`TextBlock`/`Shapes` trees |
| `HmiDefaults.json` | 1875 | Openness default values per type (V21, sample 165) | none |
| `SvgRenderer.cs` | 382 | SVG reader → `DrawingImage` | strong: `Geometry.Parse`, `FormattedText` |
| `TemplateRenderer.cs` + `.Dial.cs` | 595 | Style templates (svghmi) → plain SVG XML | low: result `Image`, otherwise `System.Windows.Point` |
| `TemplateExpr.cs` | 436 | Expressions, Converter.*, FormatPattern | low: `Measure` via `FormattedText` |
| `ScriptEval.cs` | 361 | Visible scripts | none |

About 1,900 lines are (almost) WPF-free already today, about 1,800 are WPF-bound. The WPF part, however, also
contains logic and measured values. The data source is exclusively the `JsonNode` tree from `PEData.Structure`
(`project.graphics`, `library`, `faceplates`, `hmi.screens/textLists/style`). The renderer knows no
ViewModels or controls. The only coupling to the viewer is the helpers `TreeBuilder.Str/Arr`
(`ProjectTree.cs:542/545`).

What has to be solved with the move:

- **Logic and drawing mixed.** `ScreenRenderer.Item` (`:493–614`) decides visibility, text choice,
  bounding box and pivot and creates the WPF element in the same go. `Render` (`:357–411`) sorts by
  layer/zIndex, computes group offsets and at the same time inserts into the `Canvas`. The type painters
  (`ControlWindow` `:907–1057` etc.) contain dimensions measured on the runtime directly in the WPF code.
- **Text layout belongs to WPF today.** Wrapping, truncation with ellipsis, alignment and padding are handled by
  `TextBlock` in a `Border`. Without WPF, own text measurement and own line wrapping are needed.
- **Global state** (3.3): static registries and `[ThreadStatic]` fields, `RuntimeVisibility` and `Language`
  global.
- **Fonts resolved twice and inconsistently:** `ScreenRenderer.FontNames` (`:1301`) and
  `TemplateExpr.FontFamilyOf` (`:271`). `Measure` ignores weight and italic.
- **EMF is not drawn** (`ScreenRenderer.Image` `:290–313`, only PNG/JPG/BMP/GIF/SVG).
- **No automated tests.**

#### 3.15.2 Target architecture

Three stages, each with a clear boundary:

```
HmiScreen (typed model, 3.6)  +  RenderOptions (language, view editor/runtime, limits)
        │
        ▼  1. Resolve (pure, platform-neutral)
ResolvedScreen: effective properties per object (defaults ⊕ style ⊕ stored), visible yes/no,
                text in language, faceplate/screen window content inserted, order layer/zIndex, absolute position
        │
        ▼  2. Layout (platform-neutral, needs text metrics)
Scene: list of drawing commands with origin (screen item name) –
       Rect / Ellipse / Path / Polyline / Text(Box, Font, alignment, lines) / Image(Ref) / Group(Transform, Clip, Opacity)
        │
        ▼  3. Output
   ISceneWriter:  SvgSceneWriter (in the DLL, default)
                  WpfSceneRenderer (in the viewer, thin)
                  PngSceneWriter (optional, 3.15.5)
```

- **Stage 1** takes in the logic from `ScreenRenderer` that is already pure today: `Props`/`Merge` (`:433–452`),
  `SetFaceplates`/`InterfaceValues`/`WithInterfaceValues` (`:117–267`), `ScriptVisible`/`TagVisible`/`EsHiddenLayers`
  (`:341–491`), the editor and runtime rules from `Item` (`:497–548`), `Text` (`:1332`) as well as `ScriptEval`.
  Added to this is the template expansion from `TemplateRenderer`/`TemplateExpr`, which already produces SVG XML today.
- **Stage 2** replaces the WPF type painters with functions that produce drawing commands. The measured dimensions become
  named constants per version and style (title bar, toolbar, table header …). Then they can be checked and changed in
  one place and belong to the version profile (3.14). Rotation (`RotationCenterPlacement`, `:598–610`),
  bounding box from `CenterX`/`Radius` (`:553–558`) and arcs (`:1224–1252`) are pure geometry.
- **The SVG reader** (`SvgRenderer`) is split in two. A parser reads SVG into scene commands (own path parser
  instead of `Geometry.Parse`, own transform matrix instead of WPF `Transform`). Project graphics and expanded templates
  thus become sub-scenes. With `SvgSceneWriter` as the output, the path template → SVG → scene → SVG is closed.
  For SVG output, project graphics could basically also be embedded unchanged. The scene is needed
  nevertheless so that clip, stretch mode and rotation act uniformly.
- **Origin in the scene command** (name, type, bounding box of the screen item) preserves the viewer's tooltips
  (`ScreenRenderer.cs:596`). In the SVG it becomes `id`/`data-*` attributes. That also makes the SVG useful for
  third-party applications (click targets, search).

#### 3.15.3 Public interface

```csharp
public sealed class HmiRenderer                         // via TiaProject.Hmi
{
    public IReadOnlyList<HmiScreenInfo> Screens { get; }          // device, name, size, folder
    public Scene Render(HmiScreenInfo screen, RenderOptions? options = null, CancellationToken ct = default);
    public Scene RenderFaceplate(FaceplateVersionInfo version, RenderOptions? options = null, CancellationToken ct = default);
    public Scene RenderGraphic(HmiGraphicInfo graphic);           // graphic preview
    public void WriteSvg(HmiScreenInfo screen, Stream output, RenderOptions? options = null);
}

public sealed record RenderOptions(
    string? Language = null,                      // today ScreenRenderer.Language
    RenderView View = RenderView.Editor,          // today ScreenRenderer.RuntimeVisibility
    IFontMetricsProvider? Fonts = null,           // 3.15.5
    RenderLimits? Limits = null);                 // 3.5

public sealed class Scene { public double Width, Height; public IReadOnlyList<SceneNode> Nodes; public IReadOnlyList<RenderIssue> Issues; }
```

- No global state: everything that is registered statically today (graphics, screens by name, faceplates, types,
  text lists, style per screen) is held by the `HmiRenderer` of the project instance. What is `[ThreadStatic]` today
  (style, screen name, depth, outer interface values) becomes a context object that is passed through the recursion.
- `Scene` and `SceneNode` are the only new public types besides the info and option types. They are
  deliberately simple (rectangle, ellipse, path, text, image, group), so that users can build their own outputs.
- Thread safety as in 3.2: a project instance is not thread-safe. Parallel rendering of different
  screens works only via separate instances or, later, via a frozen, read-only renderer view.

#### 3.15.4 Error behavior

Today the viewer replaces a screen item that cannot be drawn with a placeholder showing the
error type (`ScreenRenderer.cs:392–401`). That is correct and should stay so: placeholder nodes in the scene
and a `RenderIssue` (object, reason). Unsupported types (custom widgets, EMF, unknown template elements)
likewise appear as `RenderIssue`. Only if the whole screen cannot be rendered is there a `RenderException`.

#### 3.15.5 Platform questions: text, raster, EMF

These are the three places where “platform-neutral” costs work.

**Text measurement and wrapping.** `TemplateExpr.Measure` and the `TextBlock` wrapping depend on WPF today.
Converter.TextWidth/TextHeight/TextEllipsis, the layout of TextField and every wrap in text fields depend on them.
Proposal:
- Interface `IFontMetricsProvider` (width per character and pair, ascent, descent, line height per font, size,
  weight, italic).
- Default in the DLL: a small, own reader for the tables `cmap`/`hmtx`/`hhea`/`OS/2`/`kern` from
  TrueType/OpenType files. Feasible in-box without a package, a few hundred lines. It reads installed fonts
  (Windows `%WINDIR%\Fonts`, Linux fontconfig paths), looks for Siemens Sans there and otherwise falls back to a
  metrically similar font. It reports this as a `RenderIssue`, because wraps may then deviate.
- Implement line wrapping and ellipsis ourselves (word wrap after space and hyphen as before
  with `TextWrapping.Wrap`). The baseline via ascent instead of a flat `0.8 × line height`
  (`TemplateRenderer.cs:246`).
- The duplicated font name mapping (`SiemensSans` → “Siemens Sans”) in exactly one place.
- In the SVG output, text is written as `<text>` with `font-family`, not as a path. The measurement is needed
  nevertheless for alignment, wrapping and truncation; otherwise the SVG looks different from TIA.

**Rasterization (PNG).** Without WPF there is no in-box rasterizer for vector graphics in .NET. Options:
1. Only SVG from the DLL. PNG via an external tool or the browser. Simplest, but “export directly”
   then applies only to SVG.
2. Own, small scanline rasterizer for the scene commands (filled paths with antialiasing, lines with caps
   and joins, scaled images, text via glyph outlines from `glyf`/`CFF`). Possible without a package, but the largest
   single effort (L–XL), above all text.
3. A package such as SkiaSharp in a **separate**, optional package `OpenPEData.Rendering.Skia`. The core package stays
   free of native dependencies. Contradicts the project rule “no packages” only for this add-on package.
   Decision with the user.
4. Windows-only package `OpenPEData.Rendering.Wpf` that encapsulates today's WPF path (`RenderTargetBitmap`). Already
   present for the viewer anyway, no solution for servers and Linux.

Recommendation: SVG as the binding output format of the DLL (stages 1–3), PNG initially via option 4. The viewer
already has this part; it only has to move from the viewer into a small add-on project. Option 2 or 3 only once PNG
without Windows is really needed.

**EMF.** Two EMFs in F40, not drawn today. An EMF reader for the common records (polygons, lines,
text, bitmaps) is feasible in scene commands (M), but only sensible once EMF occurs more often in projects.
Until then it stays a placeholder with `RenderIssue`.

#### 3.15.6 Tests for the renderer

- **Golden SVG per screen** over all samples with HMI (editor and runtime view, one language): scene → SVG,
  normalized (fixed number formatting, sorted attributes), as a file in the repo. Diff on deviation. This is
  fast, without rasterization and platform-neutral.
- **Unit tests for the pure parts:** props merging, visibility (script/tag/layers), FormatPattern, `ScriptEval`,
  template expressions, dial graduation, pivot, arcs.
- **Image comparison against runtime and editor screenshots** (today `ScreenRenderer.Compare`, tolerance 24 per channel)
  becomes a check tool under `dotnet\tools` with the reference images of samples 164–167. With a frozen
  threshold per screen it becomes a regression test. It only runs where rasterization is possible (Windows,
  option 4).
- **Version axis (A1):** golden files are stored per TIA version. `HmiDefaults` and measured dimensions come from the
  profile, so a V22 screen is never silently rendered with V21 default values.

#### 3.15.7 Order for the move

Each step keeps the viewer working and can be checked on its own:

1. **Decouple in the viewer:** replace `TreeBuilder.Str/Arr` with own helpers, pull global state into a
   `RenderContext` object, merge font resolution. No behavior change; check with `--screen-png`
   against the previous state (pixel-identical).
2. **Freeze a golden PNG state** (all sample screens, both views) as a safety net for the following steps.
3. **Stage 1 (resolve) into the DLL**, initially on `JsonObject` (that is its input today). The viewer calls it
   and keeps drawing with WPF. `ScriptEval`, `TemplateExpr` (without `Measure`) and the template expansion
   along with it.
4. **Scene model and `SvgSceneWriter`** in the DLL. In the viewer a `WpfSceneRenderer`; the WPF type painters
   are dropped. Comparison against the golden PNGs from step 2.
5. **Text metrics without WPF** (`IFontMetricsProvider`). Only from here on does the DLL also run fully without Windows.
6. **CLI `render`**, public `HmiRenderer` facade, golden SVG tests.
7. **Switch the input to the typed `HmiScreen` model** (3.6.2) as soon as it exists.
8. As needed: PNG without Windows (3.15.5), EMF.

## 4. Proposed order (overall)

Each step can be delivered on its own and is safeguarded by the existing differential test, because the core
stays the same.

1. **Freeze tables** (3.3) and open `Guarded` for cancellation/OOM (3.4). One hour, no API break.
2. **Facade `TiaProject`** with one-time loading, **version detection (`TiaVersion`, `Probe`)**, `OpenOptions`
   (limits from 3.5), `CancellationToken`, own exception hierarchy incl. `UnsupportedVersionException` and
   `JsonObject` results; `PEData.*` as `[Obsolete]` forwarding (3.2, 3.4, 3.6.1). Names and docs
   version-neutral (3.10).
3. **Switch viewer and CLI to the facade**, remove duplicated flag logic (2, 3.11).
4. **Core to `internal`**, `InternalsVisibleTo` for tests/CLI (3.1). From here on the public API is small and stable,
   and a new TIA version is no longer an API break.
5. **Package metadata, `Directory.Build.props`, license file** after the user's license decision (3.13).
6. **Decouple the renderer in the viewer** (3.15.7 steps 1–2). Independent of 1–5, can run in parallel.
7. **Introduce `VersionProfile` for V21** (3.14): extensions, plf layout, metamodel names, tables, data files in
   one place. Pure restructuring; the differential test stays the same. Along with it, move `HmiDefaults.json` into the
   DLL as a V21 resource.
8. **Nullable warnings** back on, suppressed per folder (3.9); narrow `IsDecodeError` (3.4).
9. **Renderer stages 1–4 into the DLL** (3.15.7 steps 3–4), then text metrics and CLI `render` (steps 5–6).
10. **Typed results** (3.6.2), with the HMI screen model first. Then switch the renderer input.
11. **First further TIA version**, as soon as test projects exist: profile, golden files, cross test (3.12, 3.14).
12. Afterwards as needed: `RecordKey` struct and `ReadOnlyMemory` views (3.8), test framework (3.12), PNG without Windows
    and EMF (3.15.5).

Points 1–4 are unchanged in order compared to revision 1, but are designed for A1 and A2 in content.
They remain the most urgent part: if the API is published before that, without a version concept and with a public
core, every new version and every renderer change costs a break.

## 5. What is explicitly good

- Clean, acyclic inner layering Runtime → Format → Model, one file per Python module, traceable origin.
- No third-party dependencies, everything in-box; fits the project rules and keeps the package light.
- Format knowledge sits in the XML comments directly at the data structures (`PlfRecord`, `ObjectRecord`, `Xref`); that
  is very valuable for a reverse-engineering project and should be preserved in every restructuring.
- The differential test against Python over all samples and modes is an unusually strong safety net and makes all
  the restructurings proposed above low-risk.
- XML parsing without an external resolver; errors in broken blobs end up per object in `statistics.errors` instead of
  aborting the whole project; this robustness is right and the facade should make it visible as `Issues`.
- The metamodel is read from the project, type ids are resolved via names. That is the best prerequisite
  for further TIA versions.
- The renderer has no dependency on ViewModels or controls, works only on the parser JSON, and
  large parts (template expansion, expressions, scripts, property resolution) are WPF-free already today. The
  values measured on the runtime are commented with date and sample. That makes the move easy to plan.

## 6. Open decisions (user)

| No. | Question | Effect |
| --- | --- | --- |
| E1 | Include A1 and A2 in `docs\GOALS.md` (the goals are stated only there) | Basis for the priorities above |
| E2 | Which TIA versions: only newer ones (V22+) or also older ones (V17–V20)? | Reach of the version profile (3.14), samples |
| E3 | PNG export without Windows needed? If so: own rasterizer or add-on package with SkiaSharp (exception to “no packages”)? | 3.15.5, effort L–XL |
| E4 | Renderer in the same assembly (recommendation) or own package `OpenPEData.Rendering`? | 2.2, package layout |
| E5 | From when on is byte equality with Python no longer a check criterion, also for V21? | 3.6, 3.7 (Zlib, PyDict, Python error messages) |
| E6 | Test framework: keep the home-grown one or a test package (not shipped)? | 3.12 |
| E7 | License LGPL-3.0 or MPL-2.0 | 3.13 |
