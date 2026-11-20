"""Runs every parser/tests/test_*.py twice, plain and on the .NET results (truth_on_dotnet.py), and compares stdout, exit
code and the result tables. python dotnet/tools/truth_compare.py <.NET output dir> <work dir> [--jobs N]"""
import concurrent.futures
import os
import re
import subprocess
import sys

# per-sample run time column of the faceplate and library tables ("| 0.3 s |" at the end of a row): not a result
TIME_COLUMN = re.compile(rb"\| [0-9.]+ s \|(?=\r?\n|$)", re.M)


def content(path):
    with open(path, "rb") as f:
        return TIME_COLUMN.sub(b"| s |", f.read())

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))


def run(test, how, net, work):
    out = os.path.join(work, how, os.path.splitext(os.path.basename(test))[0])
    os.makedirs(out, exist_ok=True)
    cmd = [sys.executable, os.path.join(HERE, "truth_on_dotnet.py")] + (["--net", net] if how == "net" else ["--python"]) + ["--out", out, test]
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, timeout=7200)
    for name, data in (("stdout.txt", p.stdout), ("stderr.txt", p.stderr), ("rc.txt", str(p.returncode).encode())):
        with open(os.path.join(out, name), "wb") as f:
            f.write(data)
    return test, how, p.returncode


def main(argv):
    net, work = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    jobs = int(argv[argv.index("--jobs") + 1]) if "--jobs" in argv else 4
    tests = sorted(os.path.join(ROOT, "parser", "tests", f) for f in os.listdir(os.path.join(ROOT, "parser", "tests"))
                   if f.startswith("test_") and f.endswith(".py"))
    with concurrent.futures.ThreadPoolExecutor(jobs) as ex:
        for f in concurrent.futures.as_completed([ex.submit(run, t, h, net, work) for t in tests for h in ("python", "net")]):
            t, h, rc = f.result()
            print("%-24s %-6s rc=%d" % (os.path.basename(t), h, rc), flush=True)
    bad = 0
    for t in tests:
        n = os.path.splitext(os.path.basename(t))[0]
        a, b = os.path.join(work, "python", n), os.path.join(work, "net", n)
        files = sorted(set(os.listdir(a)) | set(os.listdir(b)) - {"stderr.txt"})
        diff = [x for x in files if not (os.path.exists(os.path.join(a, x)) and os.path.exists(os.path.join(b, x))
                                          and content(os.path.join(a, x)) == content(os.path.join(b, x)))]
        rc = open(os.path.join(a, "rc.txt")).read()
        print("%-24s python rc=%s  %s" % (n, rc, "same results on .NET" if not diff else "DIFFERENT: " + ", ".join(diff)))
        bad += bool(diff)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
