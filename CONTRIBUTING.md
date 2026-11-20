# Contributing to OpenPEData

## Language

The repository is English only: source code, identifiers, comments, log and console output, UI texts, documentation,
specs, analysis reports, test names, scripts, file names and commit messages. Pull requests that add text in another
language are not accepted.

Exception: values quoted verbatim from TIA Portal projects, Openness exports or the test samples (for example default
object names of a German project language, or umlaut test data) stay as they are. Add an English gloss where useful.

## Findings and samples

- Format findings go to `docs/spec/` with a status (`confirmed` / `likely` / `conjecture`) and evidence (sample +
  offset). Open questions go to `docs/spec/hypotheses.md`, the chronological record to `docs/spec/LOG.md` (append only).
- Samples under `samples/` (signed TIA archives and project folders) are never modified after they were created,
  and their `*_truth/` folders with the Openness ground truth are not edited either; analyse copies only. Samples
  are not renamed without the owner's consent. Every sample is listed in `samples/MANIFEST.md`.
- New format knowledge is implemented in the .NET parser (`dotnet/`), the only parser that is shipped and used. Add a
  check to the .NET test program (`dotnet/tests/OpenPEData.Tests`) against the Openness truth of the sample, and
  regenerate `baseline.json` after checking the differences (see `dotnet/README.md`).
- The Python parser (`parser/`) is for test and analysis only. It was byte-identical to the .NET parser up to
  2026-11-01 and is behind for findings made after that date. Implementing a finding in Python too is optional (useful
  when the Python truth tests or the analysis tools need it). If a Python truth test cannot check a sample because the
  Python parser lacks the feature, the test says so explicitly (as `test_library.py` and `test_plc.py` do with
  `DOTNET_ONLY`) instead of reporting OK with 0 checks.

## License

By contributing you agree that your contribution is licensed under the Mozilla Public License 2.0 (`LICENSE`).

## Continuous integration

GitHub Actions (`.github/workflows/`):

- `ci.yml` runs on every push and on pull requests from forks: Release build of `dotnet/OpenPEData.sln` and the viewer,
  the .NET test program, the viewer tests (Windows runner) and the eight Python truth tests in `parser/tests` (Linux
  runner). All of them must pass.
- `release.yml` runs when a tag `vX.Y.Z` is pushed (X.Y.Z equal to `<Version>` in
  `ui/OpenPEData.Viewer/OpenPEData.Viewer.csproj`), or by hand for an existing tag. It runs the full CI first, then
  builds the setup with `ui/scripts/Pack-OpenPEData.ps1` and attaches `OpenPEData-Setup-V*.exe` to the GitHub release
  of the tag (created if missing). The setup is unsigned unless the secret `OPENPEDATA_SIGN_PARAMS` is set and the
  certificate is available on the runner.
