"""Runs a parser CLI for every sample input and every output mode and stores stdout, stderr and the exit code.

  python run_cli.py <out dir> python            reference: parser/OpenPEData.py (the oracle)
  python run_cli.py <out dir> <exe>             any CLI with the same arguments (e.g. the .NET OpenPEData.exe)
Options: --jobs N (default 6), --only <substring> (inputs whose path contains it)
Layout: <out dir>/<mode>/<input with / -> __>.out|.err|.rc
"""
import concurrent.futures
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sample_inputs import ROOT, inputs  # noqa: E402

MODES = {"json_keys": ["--json", "--keys"], "json": ["--json"], "text": [], "verify_json": ["--verify", "--json"],
         "verify_text": ["--verify"], "xref": ["--xref"], "proginfo": ["--proginfo"]}


def run(cmd, inp, mode, out):
    base = os.path.join(out, mode, inp.replace("/", "__"))
    p = subprocess.run(cmd + [inp] + MODES[mode], cwd=ROOT, capture_output=True, timeout=1800)
    for ext, data in ((".out", p.stdout), (".err", p.stderr), (".rc", str(p.returncode).encode())):
        with open(base + ext, "wb") as f:
            f.write(data)
    return inp, mode, p.returncode


def main(argv):
    jobs = int(argv[argv.index("--jobs") + 1]) if "--jobs" in argv else 6
    only = argv[argv.index("--only") + 1] if "--only" in argv else None
    out, exe = os.path.abspath(argv[0]), argv[1]
    cmd = [sys.executable, os.path.join(ROOT, "parser", "OpenPEData.py")] if exe == "python" else [os.path.abspath(exe)]
    for m in MODES:
        os.makedirs(os.path.join(out, m), exist_ok=True)
    todo = [(i, m) for i in inputs() if not only or only in i for m in MODES]
    with concurrent.futures.ThreadPoolExecutor(jobs) as ex:
        for n, f in enumerate(concurrent.futures.as_completed([ex.submit(run, cmd, i, m, out) for i, m in todo]), 1):
            i, m, rc = f.result()
            if n % 100 == 0 or n == len(todo):
                print("%d/%d %s %s rc=%d" % (n, len(todo), m, i, rc), flush=True)


if __name__ == "__main__":
    main(sys.argv[1:])
