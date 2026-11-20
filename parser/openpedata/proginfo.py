r"""Program information of a PLC (TIA "Program info": resources, call structure, dependency structure, assignment list),
derived from the plf and XRef\XRef.db without TIA. Rules and evidence: docs\spec\program-info.md.

Objects of a PLC are the objects with Target -> its ControllerTargetData (the plf also holds library copies and
older controller objects with the same name; they do not count).

Resources (checked against TIA screenshots of 025, 031, 036, 039, 040, all values exact):
  - System blocks and system UDTs exist once per target family (R_TRIG_1200, R_TRIG_1500, R_TRIG ...). Only the
    object with IExpandoInformationData.TargetFamily counts; the others and S7BLOCK instructions (MIN, CONCAT) do not.
  - Load memory of a code block: IGeneralBlockResultData.LoadMemoryRequired of its LoadableBinaries target; for system
    blocks (where that is 0) IExpandoInformationData.LoadMemorySize. A DB counts with its expando LoadMemorySize.
  - Code work memory: WorkMemoryRequired of the loadable, except for blocks with IGeneralBlockSourceData.ResourceHandling
    = NotCounted (technology FBs such as PID_Compact, F-system blocks); their load memory still counts.
  - Data work memory: WorkMemoryRequired of the DBs that have a LoadMemorySize.
  - Retain memory: IExpandoInformationData.RetentiveMemoryLength of the DBs (RetainMemoryRequired is always 0).
  - Data types: LoadMemorySize of the UDTs with TargetFamily. PLC tags: sum of the TagLoadMemory_* expandos of the
    controller.
  - A block without a value (not compiled, CompileArtifacts = Binary, protected OB without loadable) makes its row
    "incomplete"; TIA shows ">" (or "?" if no block of the row has a value).
  - Totals (memory of the CPU) are catalog values and not stored in the project.

Call structure (XRef relations, usage 1): flags 12 / 15 = call of an FB or FC, 16 = instance DB of a call; DB members
used by a block (DBMember, InstanceDB) appear as children like in TIA. Roots: the OBs and every block that is not
reachable from an OB. Local data = ILoadablePlusCodeBlockData.LocalNenaDataLength.
"""
from . import xref

ROW_TYPES = ("OB", "FC", "FB", "DB")
CALL_FLAGS = (12, 15)
INSTANCE_FLAG = 16
SIZE_NAMES = {1: "bit", 8: "B", 16: "W", 32: "D", 64: "L"}


def _loadable(p, key):
    """(IGeneralBlockResultData, ILoadablePlusCodeBlockData) of the block's loadable or ({}, {})."""
    for t in p.targets(key, "LoadableBinaries"):
        a = p.attributes(t)[0]
        return a.get("IGeneralBlockResultData") or {}, a.get("ILoadablePlusCodeBlockData") or {}
    return {}, {}


def _address(block_type, number):
    return "%s%s" % (block_type, number) if number is not None else None


class ProgramInfo:
    def __init__(self, model, path):
        self.m = model
        self.p = model.p
        self.xr = xref.resolved(path, self.p)

    # --- objects of a PLC -------------------------------------------------------------------------
    def objects(self, plc):
        """[(key, attributes)] of the blocks and UDTs of a PLC that count (system objects only with TargetFamily)."""
        out = []
        for k in self.m.sources(plc, "Target"):
            a = self.p.attributes(k)[0]
            g = a.get("IGeneralBlockSourceData")
            if not g or g.get("BlockType") is None:
                continue
            system = (a.get("ICoreAttributes") or {}).get("Protection") == "SystemKnowHowProtection"
            if system and not (a.get("IExpandoInformationData") or {}).get("TargetFamily"):
                continue
            out.append((k, a))
        return out

    # --- resources --------------------------------------------------------------------------------
    def resources(self, plc, objs):
        rows = {t: {"loadMemory": 0, "codeWorkMemory": 0, "dataWorkMemory": 0, "retainMemory": 0, "blocks": 0,
                    "withoutValue": 0} for t in ROW_TYPES}
        blocks = []
        udt = {"loadMemory": 0, "count": 0, "withoutValue": 0}
        for k, a in objs:
            g = a["IGeneralBlockSourceData"]
            e = a.get("IExpandoInformationData") or {}
            bt = g.get("BlockType")
            system = (a.get("ICoreAttributes") or {}).get("Protection") == "SystemKnowHowProtection"
            if bt == "UDT":
                udt["count"] += 1
                if e.get("LoadMemorySize") is None:
                    udt["withoutValue"] += 1
                udt["loadMemory"] += e.get("LoadMemorySize") or 0
                continue
            if bt not in rows:
                continue
            res, code = _loadable(self.p, k)
            row = rows[bt]
            row["blocks"] += 1
            counted = g.get("ResourceHandling") != "NotCounted"
            b = {"name": self.p.name(k), "type": bt, "number": g.get("Number"), "system": system,
                 "resourceHandling": g.get("ResourceHandling"), "key": k}
            if bt == "DB":
                lm = e.get("LoadMemorySize")
                b.update(loadMemory=lm, dataWorkMemory=res.get("WorkMemoryRequired") if lm is not None else None,
                         retainMemory=e.get("RetentiveMemoryLength"))
                row["dataWorkMemory"] += b["dataWorkMemory"] or 0
                row["retainMemory"] += b["retainMemory"] or 0
            else:
                lm = res.get("LoadMemoryRequired") or e.get("LoadMemorySize")
                wm = res.get("WorkMemoryRequired") if lm else None
                b.update(loadMemory=lm, codeWorkMemory=wm, codeWorkMemoryCounted=counted,
                         localData=code.get("LocalNenaDataLength"))
                if counted:
                    row["codeWorkMemory"] += wm or 0
            if not lm:
                row["withoutValue"] += 1
            row["loadMemory"] += lm or 0
            blocks.append(b)
        tags = {}
        for d in self.p.attributes(plc)[0].values():
            for n, v in d.items():
                if n.startswith("TagLoadMemory_"):
                    tags[n[len("TagLoadMemory_"):]] = v
        out_rows = []
        for t in ROW_TYPES:
            r = rows[t]
            out_rows.append({"objects": t, "loadMemory": r["loadMemory"], "codeWorkMemory": r["codeWorkMemory"] if t != "DB" else None,
                             "dataWorkMemory": r["dataWorkMemory"] if t == "DB" else None,
                             "retainMemory": r["retainMemory"] if t == "DB" else None,
                             "blocks": r["blocks"], "incomplete": r["withoutValue"] > 0,
                             "noValue": r["blocks"] > 0 and r["withoutValue"] == r["blocks"]})
        out_rows.append({"objects": "DataTypes", "loadMemory": udt["loadMemory"], "blocks": udt["count"],
                         "incomplete": udt["withoutValue"] > 0, "noValue": False})
        out_rows.append({"objects": "PlcTags", "loadMemory": sum(tags.values()) if tags else None, "detail": tags,
                         "retainMemory": 0, "incomplete": not tags, "noValue": not tags})
        used = {"loadMemory": sum(r["loadMemory"] or 0 for r in out_rows),
                "codeWorkMemory": sum(r.get("codeWorkMemory") or 0 for r in out_rows),
                "dataWorkMemory": rows["DB"]["dataWorkMemory"], "retainMemory": rows["DB"]["retainMemory"]}
        blocks.sort(key=lambda x: (ROW_TYPES.index(x["type"]), x["number"] or 0, x["name"] or ""))
        return {"rows": out_rows, "used": used, "blocks": blocks}

    # --- XRef based views -------------------------------------------------------------------------
    def _refs(self, plc_name):
        return [r for r in (self.xr or {}).get("references", []) if r["scope"] == plc_name]

    def call_structure(self, plc, objs):
        info = {}
        for k, a in objs:
            g = a["IGeneralBlockSourceData"]
            if g.get("BlockType") not in ROW_TYPES:
                continue  # UDTs; SFB/SFC (e.g. CTRL_PWM_SFB) are not listed by TIA
            _res, code = _loadable(self.p, k)
            info[k] = {"name": self.p.name(k), "type": g.get("BlockType"), "number": g.get("Number"),
                       "localData": code.get("LocalNenaDataLength")}
        by_name = {}
        for k, v in info.items():
            by_name.setdefault(v["name"], k)
        calls, dbs = {}, {}
        for r in self._refs(self.p.name(plc)):
            u, w = r["userKey"], r["usedKey"]
            if u not in info or r["usage"] != 1:
                continue
            if r["usedKind"] in ("FB", "FC") and r["flags"] in CALL_FLAGS and w in info:
                calls.setdefault(u, {}).setdefault(w, None)
            elif r["usedKind"] == "Instruction" and r["flags"] in CALL_FLAGS and r["used"] in by_name:
                # instruction proxy whose system block is in the program (031: FC_SclVar -> MOVE_BLK_VARIANT FC900)
                calls.setdefault(u, {}).setdefault(by_name[r["used"]], None)
            elif r["usedKind"] == "InstanceDB" and r["flags"] == INSTANCE_FLAG and w in info:
                dbs.setdefault(u, set()).add(w)
            elif r["usedKind"] in ("DBMember", "InstanceDB") and w in info:
                dbs.setdefault(u, set()).add(w)
        reach = set()

        def node(k, path):
            n = dict(info[k], address=_address(info[k]["type"], info[k]["number"]))
            reach.add(k)
            local = n["localData"] or 0
            n["localDataInPath"] = path + local
            kids = []
            if k not in path_keys:
                path_keys.add(k)
                for w in sorted(calls.get(k, {}), key=lambda x: (info[x]["type"], info[x]["number"] or 0)):
                    kids.append(node(w, path + local))
                path_keys.discard(k)
            else:
                n["recursive"] = True
            for d in sorted(dbs.get(k, ()), key=lambda x: info[x]["number"] or 0):
                kids.append(dict(info[d], address=_address("DB", info[d]["number"]), localData=None, localDataInPath=None,
                                 children=[]))
            n["children"] = kids
            return n

        path_keys = set()
        order = lambda k: (("OB", "DB", "FC", "FB").index(info[k]["type"]) if info[k]["type"] in ("OB", "DB", "FC", "FB") else 4,
                           info[k]["number"] or 0, info[k]["name"] or "")  # noqa: E731
        # TIA lists every block that is not reachable from an OB as a root of its own, even if another such block calls
        # it (036: FC_SclOps is a root and also below FB_SclCalls)
        roots = [node(k, 0) for k in sorted((k for k in info if info[k]["type"] == "OB"), key=order)]
        from_ob = set(reach)
        roots += [node(k, 0) for k in sorted(info, key=order) if info[k]["type"] != "OB" and k not in from_ob]
        return roots

    def dependency_structure(self, plc, objs):
        names = {k: (self.p.name(k), a["IGeneralBlockSourceData"].get("BlockType"), a["IGeneralBlockSourceData"].get("Number"))
                 for k, a in objs if a["IGeneralBlockSourceData"].get("BlockType") in ROW_TYPES + ("UDT",)}
        users = {}
        for r in self._refs(self.p.name(plc)):
            u, w = r["userKey"], r["usedKey"]
            if u == w or u not in names or w not in names:
                continue
            users.setdefault(w, set()).add(u)
        out = []
        rank = ("UDT", "DB", "FC", "FB", "OB")  # order of the TIA dependency structure (025, 031, 040)
        for k, (n, t, num) in sorted(names.items(), key=lambda x: (rank.index(x[1][1]) if x[1][1] in rank else len(rank),
                                                                  x[1][2] or 0, x[1][0] or "")):
            out.append({"name": n, "type": t, "address": _address(t, num) if t != "UDT" else None,
                        "usedBy": [{"name": names[u][0], "type": names[u][1], "address": _address(names[u][1], names[u][2])}
                                   for u in sorted(users.get(k, ()), key=lambda x: names[x][0] or "")]})
        return out

    def assignment_list(self, plc):
        """Addresses of the tags used in the program (TIA lists only used ones): area, byte, bit or size."""
        refs = self._refs(self.p.name(plc))
        used = {r["usedKey"] for r in refs if r["usedKind"] == "Tag"}
        out = []
        for a in (self.xr or {}).get("addresses", []):
            if a["scope"] != self.p.name(plc) or a["key"] not in used:
                continue
            n = a["lastBit"] - a["firstBit"] + 1
            out.append({"tag": a["tag"], "area": a["area"], "byte": a["firstBit"] // 8,
                        "bit": a["firstBit"] % 8 if n == 1 else None, "size": SIZE_NAMES.get(n, "%d bits" % n),
                        "firstBit": a["firstBit"], "lastBit": a["lastBit"]})
        out.sort(key=lambda x: (str(x["area"]), x["firstBit"]))
        return out

    # --- entry ------------------------------------------------------------------------------------
    def plcs(self):
        p = self.p
        proj = next(iter(p.of_type("ProjectData")), None)
        out = []
        for d in p.of_type("DeviceData"):
            if proj and self.m.rel1(d, "Environment") != proj:
                continue
            for c in sorted(self.m.sources(d, "LogicalContainer")):
                if not p.is_a(c, "ControllerTargetData"):
                    continue
                di = p.attributes(c)[0].get("IDeviceItemData", {})
                objs = self.objects(c)
                out.append({"device": p.name(d), "name": p.name(c), "orderNumber": di.get("OrderNumber"),
                            "typeName": di.get("InvariantTypeName"), "firmware": di.get("FwVersion"),
                            "resources": self.resources(c, objs), "callStructure": self.call_structure(c, objs),
                            "dependencyStructure": self.dependency_structure(c, objs),
                            "assignmentList": self.assignment_list(c), "xref": self.xr is not None, "key": c})
        out.sort(key=lambda x: (x["device"] or "", x["name"] or ""))
        return out


def extract(model, path):
    """Program information of all PLCs of a project (option --proginfo)."""
    return {"plcs": ProgramInfo(model, path).plcs()}
