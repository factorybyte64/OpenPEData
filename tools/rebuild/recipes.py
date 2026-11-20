"""Recipe registry: merges RECIPES (and MANUAL) of every module recipes_*.py in this folder.

RECIPES[name] = {
  "from": predecessor sample (copied closed folder) or None for TiaProject(create),
  "project": project name for new projects (default: sample name),
  "steps": list of (tool, args) or callables f(ctx),
  "save"/"archive": default True,
  "generators": extra truth generators [(regex, f(ctx, out, rel))], tried before the generic ones in truth.py,
  "truth": callable f(ctx) replacing the generic truth,
  "build": callable f(client, stage, name, Ctx) replacing rebuild.build (global libraries, recipes_lib.py),
  "out_ext": extension of the output archive in <stage>\\out (default .zap21; .zal21 for global libraries),
}
MANUAL[name] = reason: samples created by hand in the TIA user interface (not rebuilt automatically).
"""
import importlib
import os

HERE = os.path.dirname(os.path.abspath(__file__))
RECIPES, MANUAL = {}, {}
for _fn in sorted(os.listdir(HERE)):
    if _fn.startswith("recipes_") and _fn.endswith(".py"):
        _m = importlib.import_module(_fn[:-3])
        for _k in getattr(_m, "RECIPES", {}):
            if _k in RECIPES:
                raise RuntimeError("recipe %s defined twice" % _k)
        RECIPES.update(getattr(_m, "RECIPES", {}))
        MANUAL.update(getattr(_m, "MANUAL", {}))
