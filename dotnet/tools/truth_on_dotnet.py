"""Runs a Python truth test of parser/tests against the results of the .NET port (or, for comparison, unchanged).

  python dotnet/tools/truth_on_dotnet.py (--net <.NET output dir> | --python) --out <dir> parser/tests/test_xxx.py [args]

--net: the model entry points the tests use are replaced by the JSON the .NET CLI wrote (run_cli.py <dir> <exe>):
Model.to_dict (json / json_keys), extract_library, extract_faceplates, proginfo.extract, verify.verify_project,
xref.resolved. Everything else (Project, metamodel, record access the tests do themselves) stays Python.
Result tables the tests write (parser/tests/RESULTS_*.md) go to --out instead, so the repository is not touched.
Comparing stdout and the result tables of a --net run with a --python run shows whether the .NET results pass the same
truth checks."""
import builtins
import json
import os
import runpy
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
TESTS = os.path.join(ROOT, "parser", "tests")
sys.path.insert(0, os.path.join(ROOT, "parser"))

argv = sys.argv[1:]
net, out, rest, i = None, None, [], 0
while i < len(argv):
    if argv[i] in ("--net", "--out") and not rest:
        net, out = (argv[i + 1], out) if argv[i] == "--net" else (net, os.path.abspath(argv[i + 1]))
        i += 2
        continue
    if argv[i] == "--python" and not rest:
        i += 1
        continue
    rest.append(argv[i])
    i += 1
os.makedirs(out, exist_ok=True)

_open = builtins.open


def _redirect_open(file, mode="r", *a, **k):
    if isinstance(file, str) and any(c in mode for c in "wax"):
        p = os.path.abspath(file)
        if os.path.dirname(p).lower() == TESTS.lower():
            file = os.path.join(out, os.path.basename(p))
    return _open(file, mode, *a, **k)


builtins.open = _redirect_open

if net:
    from openpedata import faceplates, library, model, proginfo, verify, xref  # noqa: E402
    NET = os.path.abspath(net)
    _cache = {}

    def _load(mode, path):
        rel = os.path.relpath(os.path.abspath(path), ROOT).replace("\\", "/")
        key = (mode, rel)
        if key not in _cache:
            fn = os.path.join(NET, mode, rel.replace("/", "__") + ".out")
            if not os.path.exists(fn):
                raise FileNotFoundError("no .NET output for %s %s" % (mode, rel))
            with _open(fn, encoding="utf-8") as f:
                _cache[key] = f.read()
        return json.loads(_cache[key])

    def _path_of(store):
        return getattr(getattr(store, "p", store), "path")

    model.Model.to_dict = lambda self, keys=False: _load("json_keys" if keys else "json", self.p.path)
    library.extract_library = lambda store, keys=False: _load("json_keys" if keys else "json", _path_of(store))["library"]
    faceplates.extract_faceplates = lambda m, keys=False: _load("json_keys" if keys else "json", m.p.path)["faceplates"]
    proginfo.extract = lambda m, path: _load("proginfo", path)
    verify.verify_project = lambda path: _load("verify_json", path)
    xref.resolved = lambda path, proj: _load("xref", path)

test = os.path.abspath(rest[0])
sys.argv = [test] + rest[1:]
sys.path.insert(0, os.path.dirname(test))
runpy.run_path(test, run_name="__main__")
