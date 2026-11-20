"""Rebuild of the samples from their recipes (samples/MANIFEST.md) in a staging directory.

Every sample is created like the original: copy of the closed predecessor folder -> open -> change (MCP calls of the
recipe) -> save -> ground truth -> archive -> close. Samples without predecessor start with TiaProject(create).
The ground truth is regenerated for exactly the files the old `<sample>_truth\\` folder has (generators in truth.py);
files without a generator are listed in `<sample>_truth_missing.txt` of the staging output.

    python -I tools\\rebuild\\rebuild.py --pid 2056 --stage <stage> 000_empty 001_devgroup ...
    python -I tools\\rebuild\\rebuild.py --pid 2056 --stage <stage> --upto 016_tag_accessible

Output: <stage>\\<sample>\\ (closed project folder, predecessor for later samples), <stage>\\out\\<sample>.zap21,
<stage>\\out\\<sample>_truth\\, <stage>\\log\\<sample>.json (every call with result).
"""
import argparse
import json
import os
import shutil
import sys
import time
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
ROOT = os.path.dirname(TOOLS)
SAMPLES = os.path.join(ROOT, "samples")
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)
from tiaclient import TiaClient  # noqa: E402
import recipes  # noqa: E402
import truth  # noqa: E402


class Ctx:
    """State of one sample build: client, paths, call log."""

    def __init__(self, client, stage, name, rec):
        self.c, self.stage, self.name, self.rec = client, stage, name, rec
        self.dir = os.path.join(stage, name)
        self.out = os.path.join(stage, "out")
        self.tdir = os.path.join(self.out, name + "_truth")
        self.old_truth = os.path.join(SAMPLES, name + "_truth")
        self.log = []
        self.ap = None

    def call(self, tool, ok_errors=False, **args):
        args = {k: v for k, v in args.items() if v is not None}
        if self.ap and "path" not in args and tool not in ("TiaInstance", "TiaServer", "TiaJob", "TiaLibrary",
                                                            "TiaLibraryContent"):
            args["path"] = self.ap
        t0 = time.time()
        err, txt = self.c.call(tool, **args)
        # long running calls return a jobId: wait for the result
        if not err:
            try:
                j = json.loads(txt)
            except ValueError:
                j = None
            while isinstance(j, dict) and j.get("jobId") and set(j) & {"status", "state", "jobId"} and "result" not in j \
                    and j.get("state") not in ("Completed", "Failed", "Canceled"):
                err, txt = self.c.call("TiaJob", operation="result", jobId=j["jobId"], waitMs="50000")
                try:
                    j2 = json.loads(txt)
                except ValueError:
                    break
                if isinstance(j2, dict) and j2.get("jobId") == j["jobId"] and j2.get("state") in ("Running", "Queued"):
                    j = j2
                    continue
                break
            txt = self._full(txt)
        self.log.append({"tool": tool, "args": args, "error": err, "ms": int((time.time() - t0) * 1000),
                         "result": txt if len(txt) < 4000 else txt[:4000] + "..."})
        if err and not ok_errors:
            raise RuntimeError("%s %s failed: %s" % (tool, args.get("operation"), txt[:1500]))
        return txt

    @staticmethod
    def _full(txt):
        """Results above ~40k characters are written to fullResultPath; return the full text."""
        if "fullResultPath" in txt[:2000] or "fullResultPath" in txt[-2000:]:
            try:
                p = json.loads(txt).get("fullResultPath")
            except ValueError:
                p = None
            if p and os.path.isfile(p):
                with open(p, encoding="utf-8-sig") as f:
                    return f.read()
        return txt

    def j(self, tool, **args):
        return json.loads(self.call(tool, **args))


def strip_hmi_cache(zap):
    """Cleanup for publication (MANIFEST 2026-11-08): drop IM/HMI/**/Saved/ from the archive."""
    with zipfile.ZipFile(zap) as z:
        names = z.namelist()
        drop = [n for n in names if n.replace("\\", "/").split("/")[:2] == ["IM", "HMI"] and "/Saved/" in "/" + n.replace("\\", "/")]
        if not drop:
            return 0
        tmp = zap + ".tmp"
        with zipfile.ZipFile(tmp, "w") as o:
            for info in z.infolist():
                if info.filename in drop:
                    continue
                o.writestr(info, z.read(info.filename), compress_type=info.compress_type)
    os.replace(tmp, zap)
    return len(drop)


def strip_hmi_cache_dir(folder):
    n = 0
    hmi = os.path.join(folder, "IM", "HMI")
    for dp, dns, _ in os.walk(hmi):
        for d in list(dns):
            if d == "Saved":
                shutil.rmtree(os.path.join(dp, d))
                dns.remove(d)
                n += 1
    return n


def build(c, stage, name):
    rec = recipes.RECIPES[name]
    if rec.get("build"):  # own build function (e.g. global libraries, recipes_lib.py)
        return rec["build"](c, stage, name, Ctx)
    ctx = Ctx(c, stage, name, rec)
    os.makedirs(ctx.out, exist_ok=True)
    os.makedirs(os.path.join(stage, "log"), exist_ok=True)
    if os.path.exists(ctx.dir):
        shutil.rmtree(ctx.dir)
    for p in (ctx.tdir, os.path.join(ctx.out, name + ".zap21")):
        if os.path.isdir(p):
            shutil.rmtree(p)
        elif os.path.exists(p):
            os.remove(p)
    frm = rec.get("from")
    if frm:
        src = os.path.join(stage, frm)
        if not os.path.isdir(src):
            raise RuntimeError("predecessor %s not built" % frm)
        shutil.copytree(src, ctx.dir)
        ap = [x for x in os.listdir(ctx.dir) if x.endswith(".ap21")][0]
        ctx.ap = os.path.join(ctx.dir, ap)
        ctx.call("TiaProject", operation="open", path=ctx.ap)
    else:
        pname = rec.get("project", name)
        if pname == name:
            ctx.call("TiaProject", operation="create", directory=stage, name=pname)
            ctx.ap = os.path.join(ctx.dir, pname + ".ap21")
        else:  # project name differs from the sample name: create under _new, move after close
            tmp_parent = os.path.join(stage, "_new")
            if os.path.exists(tmp_parent):
                shutil.rmtree(tmp_parent)
            os.makedirs(tmp_parent)
            ctx.call("TiaProject", operation="create", directory=tmp_parent, name=pname)
            ctx.ap = os.path.join(tmp_parent, pname, pname + ".ap21")
    closed, failed = False, True
    try:
        for step in rec.get("steps", []):
            if callable(step):
                step(ctx)
            else:
                tool, args = step
                ctx.call(tool, **args)
        if rec.get("save", True):
            ctx.call("TiaProject", operation="save")
        truth.make(ctx)
        if rec.get("archive", True):
            zap = os.path.join(ctx.out, name + ".zap21")
            ctx.call("TiaProject", operation="archive", archivePath=zap)
            strip_hmi_cache(zap)
        for step in rec.get("after_archive", []):
            step(ctx)
        failed = False
    finally:
        try:
            ctx.call("TiaProject", operation="close",
                     discardChanges="true" if rec.get("discard_on_close") or failed else None)
            closed = True
        except Exception as e:  # noqa: BLE001
            ctx.log.append({"close_error": str(e)})
        with open(os.path.join(stage, "log", name + ".json"), "w", encoding="utf-8") as f:
            json.dump(ctx.log, f, indent=1, ensure_ascii=False)
    if closed and rec.get("project") and rec["project"] != name and not frm:
        # created under _new\<project>: move to <stage>\<name>
        shutil.rmtree(ctx.dir, ignore_errors=True)
        shutil.move(os.path.join(stage, "_new", rec["project"]), ctx.dir)
        shutil.rmtree(os.path.join(stage, "_new"), ignore_errors=True)
    strip_hmi_cache_dir(ctx.dir)
    for step in rec.get("after_close", []):
        step(ctx)
    return ctx


def order(names):
    """Predecessors first."""
    seen, out = set(), []

    def visit(n):
        if n in seen:
            return
        seen.add(n)
        f = recipes.RECIPES[n].get("from")
        if f and f in names:
            visit(f)
        out.append(n)
    for n in names:
        visit(n)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pid", type=int, required=True)
    ap.add_argument("--stage", required=True)
    ap.add_argument("--skip-built", action="store_true", help="skip samples whose archive already exists in out")
    ap.add_argument("--with-deps", action="store_true", help="also build missing predecessors")
    ap.add_argument("names", nargs="*")
    a = ap.parse_args()
    names = a.names or list(recipes.RECIPES)
    if a.with_deps:
        need = set(names)
        for n in list(names):
            f = recipes.RECIPES[n].get("from")
            while f and not os.path.isdir(os.path.join(a.stage, f)):
                need.add(f)
                f = recipes.RECIPES[f].get("from")
        names = sorted(need)
    names = order(names)
    c = TiaClient(a.pid)
    status = {}
    for n in names:
        zap = os.path.join(a.stage, "out", n + recipes.RECIPES[n].get("out_ext", ".zap21"))
        if a.skip_built and (os.path.exists(zap) or (not recipes.RECIPES[n].get("archive", True) and os.path.isdir(os.path.join(a.stage, n)))):
            continue
        t0 = time.time()
        try:
            ctx = build(c, a.stage, n)
            missing = getattr(ctx, "truth_missing", [])
            status[n] = "OK" + (" (truth missing: %d)" % len(missing) if missing else "")
        except Exception as e:  # noqa: BLE001
            status[n] = "FAIL " + str(e)[:300]
        print("%-32s %5.0fs %s" % (n, time.time() - t0, status[n]), flush=True)
    bad = [n for n, s in status.items() if s.startswith("FAIL")]
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
