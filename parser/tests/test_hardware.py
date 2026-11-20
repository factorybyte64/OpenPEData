"""Checks the hardware part of the parser (openpedata.hardware, GOALS.md Z1.1) against the Openness ground truth:
device item tree (samples\\*_truth\\device_*_details.json from TiaDevice details), CAx export
(samples\\*_truth\\hardware.aml) and the recipes in samples\\MANIFEST.md (126_hmi_subnet), for the project folder and
the .zap21 archive of each sample. Result table: tests\\RESULTS_HARDWARE.md.

Openness lists only part of the device items (not e.g. CPU exec unit, CpuProxy, IOController, LicenseManager);
the check is therefore "every Openness item exists with the same parent and position", not equality of the trees.

Usage: python -I parser\\tests\\test_hardware.py
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "parser"))
from openpedata.model import Model  # noqa: E402

SAMPLES = os.path.join(ROOT, "samples")

# Openness attribute (DeviceItem path, name) -> stored expando (subtype of the carrying item, item name or None,
# expando name, Openness value -> stored value); evidenced by 200-204 (docs\spec\hw-config.md, H-HW-01)
ACCESS_LEVEL = {"FullAccess": 1}
OPENNESS_TO_EXPANDO = {
    ("PLC_1", "CycleMaximumCycleTime"): ("S71500.CPU.CPUexecUnit", None, "MaxCycleConfig", int),
    ("PLC_1", "StartupActionAfterPowerOn"): ("S71500.CPU.CPUexecUnit", None, "PowerOnAction", int),
    ("PLC_1", "ClockMemoryByte"): ("S71500.CPU.CpuCommon", None, "UseClockFlags", bool),
    ("PLC_1", "ClockMemoryByteAddress"): ("S71500.CPU.CpuCommon", None, "ClockFlags_Local", int),
    ("PLC_1", "WebserverActivate"): ("S71500.CPU", None, "WebServerActive", bool),
    ("PLC_1/PROFINET interface_1", "UseWebServerForInterface"): ("S71500.CPU.Interface.IE", "PROFINET interface_1",
                                                                 "EnableWebServerAccess4IE", bool),
    ("PLC_1", "PlcProtectionAccessLevel"): ("S71500.CPU.CpuCommon", None, "ProtectionLevel", ACCESS_LEVEL.get),
}


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def flat(items, parent=""):
    """{path: item} of a device item tree; path = parent path + '/' + name."""
    out = {}
    for it in items:
        path = parent + "/" + (it["name"] or "")
        out.setdefault(path, it)
        out.update(flat(it["items"], path))
    return out


def flat_all(items, parent=""):
    """{path: [items]} like flat, keeping items whose paths repeat."""
    out = {}
    for it in items:
        path = parent + "/" + (it["name"] or "")
        out.setdefault(path, []).append(it)
        for q, v in flat_all(it["items"], path).items():
            out.setdefault(q, []).extend(v)
    return out


def aml_items(path):
    """[(name, {attr: value})] of the InternalElements in a CAx export, in document order."""
    s = open(path, encoding="utf-8-sig").read()
    out = []
    for m in re.finditer(r'<InternalElement [^>]*Name="([^"]*)"|<Attribute Name="([^"]*)"[^>]*>\s*(?:<Value>([^<]*)</Value>)?', s):
        if m.group(1) is not None:
            out.append((m.group(1), {}))
        elif out:
            out[-1][1][m.group(2)] = m.group(3)
    return out


def check(sample, src):
    d = Model(src).to_dict()
    devs = {x["name"]: x for x in d["project"]["devices"]}
    errs, n = [], 0

    def eq(what, got, exp):
        nonlocal n
        n += 1
        if got != exp:
            errs.append("%s: parser %r, truth %r" % (what, got, exp))

    truth = os.path.join(SAMPLES, sample + "_truth")
    for f in glob.glob(os.path.join(truth, "device_*_details.json")):
        t = load(f)
        dev = devs.get(t["name"])
        eq("device %s" % t["name"], dev is not None, True)
        if not dev:
            continue
        # Openness: modules of a rack are siblings of the rack; our tree has the same top level (Parent -> station)
        got = flat(dev["hardware"]["items"])

        def walk(items, parent):
            for it in items:
                path = parent + "/" + it["name"]
                g = got.get(path)
                if g is not None and g["positionNumber"] != it["positionNumber"]:  # same name twice (HMI_1 rack/runtime, 229)
                    g = next((x for x in flat_all(dev["hardware"]["items"]).get(path, []) if x["positionNumber"] == it["positionNumber"]), g)
                if g is None:  # ET 200SP head submodule: stored name '_1', Openness shows 'IM_1' (H-HW-04): match by slot
                    g = next((x for q, x in got.items() if q.rsplit("/", 1)[0] == parent
                              and x["positionNumber"] == it["positionNumber"] and (x["name"] or "").startswith("_")), None)
                eq("item %s exists" % path, g is not None, True)
                if g:
                    eq("item %s position" % path, g["positionNumber"], it["positionNumber"])
                    ti = it.get("typeIdentifier") or ""
                    if ti.startswith("OrderNumber:"):
                        on, _, fw = ti[len("OrderNumber:"):].partition("/")
                        eq("item %s order number" % path, g["orderNumber"], on)
                        if fw:
                            eq("item %s firmware" % path, g["firmware"], fw)
                    if ti.startswith("GSD:") and (g["gsd"] or {}).get("typeIdentifier"):  # sample 229 (interface/port via GSDML)
                        eq("item %s GSD type" % path, (g["gsd"] or {}).get("typeIdentifier"), ti)
                walk(it["deviceItems"], path)
        walk(t["deviceItems"], "")
        if t.get("isGsd"):
            eq("device %s GSD type" % t["name"], (dev["hardware"]["gsd"] or {}).get("typeIdentifier"), t["typeIdentifier"])
    f = os.path.join(truth, "hardware.aml")
    if os.path.exists(f):
        items = aml_items(f)
        allitems, nodes = {}, {}  # first occurrence in tree order (names repeat, e.g. PLC_1 = CPU and CpuCommon)
        for dev in devs.values():
            for it in flat(dev["hardware"]["items"]).values():
                allitems.setdefault(it["name"], it)
                for nd in it["nodes"]:
                    nodes.setdefault(nd["nodeId"], []).append(nd)  # node names repeat (IE1 on several IO devices, 229)
        for name, a in items:
            if "PositionNumber" in a and name in allitems:
                poss = sorted({str(it["positionNumber"]) for dev in devs.values()  # module and submodule may share a name
                               for it in flat(dev["hardware"]["items"]).values() if it["name"] == name})
                eq("aml %s position" % name, a["PositionNumber"] in poss, True)
            if a.get("TypeName") and a.get("TypeIdentifier", "").startswith("OrderNumber:") and name in allitems:
                eq("aml %s type name" % name, allitems[name]["typeName"], a["TypeName"])
            if a.get("FirmwareVersion") and name in allitems:
                eq("aml %s firmware" % name, allitems[name]["firmware"], a["FirmwareVersion"])
            if "NetworkAddress" in a:
                nds = nodes.get(name, [])
                eq("aml node %s exists" % name, bool(nds), True)
                if nds:
                    addr = [nd["ipAddress"] if nd["nodeType"] == 3 else str(nd["profibusAddress"]) for nd in nds]
                    eq("aml node %s address" % name, a["NetworkAddress"] in addr, True)
                    if "SubnetMask" in a:  # PROFIBUS nodes: address only
                        eq("aml node %s mask" % name, a["SubnetMask"] in [nd["subnetMask"] for nd in nds], True)
    fb, fa = os.path.join(truth, "attributes_before.json"), os.path.join(truth, "attributes_after.json")
    if os.path.exists(fb) and os.path.exists(fa):  # H2 samples 200-204 (H-HW-01)
        before, after = load(fb), load(fa)
        for p in ("before", "after"):
            al = os.path.join(truth, "accessLevel_%s.json" % p)
            if os.path.exists(al):
                (before if p == "before" else after)["PLC_1"]["PlcProtectionAccessLevel"] = {
                    "value": load(al)["properties"]["PlcProtectionAccessLevel"]}
        items = flat(devs["PLC_1"]["hardware"]["items"])
        for (oitem, oattr), (subtype, iname, expando, conv) in OPENNESS_TO_EXPANDO.items():
            ob, oa = before[oitem].get(oattr, {}).get("value"), after[oitem].get(oattr, {}).get("value")
            hits = [it for it in items.values() if it["subtype"] == subtype and (iname is None or it["name"] == iname)]
            eq("item %s exists" % subtype, len(hits), 1)
            got = hits[0]["parameters"].get(expando) if hits else None
            if oa != ob:  # changed: stored as expando with the Openness value
                eq("%s.%s = expando %s" % (oitem, oattr, expando), got, conv(oa))
                oname = oattr if oattr != "PlcProtectionAccessLevel" else "PlcAccessLevelProvider." + oattr
                eq("expando %s Openness name" % expando, hits[0]["opennessNames"].get(expando) if hits else None, oname)
            else:  # catalog default: not stored (H-HW-01)
                eq("%s.%s default, expando %s absent" % (oitem, oattr, expando), got, None)
    fa = os.path.join(truth, "addressList.json")
    if os.path.exists(fa):  # H3 samples 205-209: I/O addresses (TiaDeviceItem addressList)
        items = flat(devs["PLC_1"]["hardware"]["items"])
        for row in load(fa)["items"]:
            path = "/" + row["deviceItemPath"]
            got = [x for x in (items[path]["addresses"] if path in items else []) if x.get("ioType") == row["ioType"]]
            eq("address %s %s exists" % (path, row["ioType"]), len(got), 1)
            if len(got) == 1:
                eq("address %s start" % path, got[0]["startAddress"], row["startAddress"])
                eq("address %s length bits" % path, got[0]["lengthBits"], row["lengthBits"])
        ob = os.path.join(truth, "OB30.attributes.json")
        if os.path.exists(ob):  # process image partition of the DI module = partition of the cyclic interrupt OB
            pip = load(ob)["attributes"]["ProcessImagePartNumber"]
            got = [x["processImage"] for x in items.get("/DI_1/DI_1", {}).get("addresses", [])]
            eq("DI_1 process image", got, [0 if pip == 65535 else pip])
    fh = os.path.join(truth, "hwIdentifierList.json")
    if os.path.exists(fh):  # hardware identifiers (TiaDeviceItem hwIdentifierList)
        hw = devs["PLC_1"]["hardware"]
        items = flat(hw["items"])
        mine = [hw["hwIdentifier"]["value"]] + [it["hwIdentifier"]["value"] for it in items.values() if it["hwIdentifier"]]
        rows = load(fh)["items"]
        eq("hardware identifiers (all)", sorted(mine), sorted(r["identifier"] for r in rows))
        for r in rows:  # unique Openness paths; the CPU item lists the identifiers of its hidden submodules too
            if [x["deviceItemPath"] for x in rows].count(r["deviceItemPath"]) == 1:
                got = hw["hwIdentifier"] if not r["deviceItemPath"] else (items.get("/" + r["deviceItemPath"]) or {}).get("hwIdentifier")
                eq("hardware identifier %s" % (r["deviceItemPath"] or "station"), got and got["value"], r["identifier"])
    if glob.glob(os.path.join(truth, "node_*.json")):  # H4 samples 210-225: nodes, subnets, IO systems, connections
        net = d["project"]["network"]
        nodes = {}  # (station, interface path) -> node
        for dev in devs.values():
            for path, it in flat(dev["hardware"]["items"]).items():
                for nd in it["nodes"]:
                    nodes.setdefault((dev["name"], path[1:]), nd)
        for f in glob.glob(os.path.join(truth, "node_*.json")):
            t = load(f)
            a, nd = t["attributes"], nodes.get((t["deviceKey"], t["deviceItemPath"]))
            eq("node %s exists" % t["deviceItemPath"], nd is not None, True)
            if not nd:
                continue
            eq("node %s name" % t["deviceItemPath"], nd["name"], a["Name"])
            eq("node %s subnet" % t["deviceItemPath"], nd["subnet"], t["connectedSubnetName"])
            if a.get("NodeType") == "Ethernet":
                eq("node %s IP" % t["deviceItemPath"], nd["ipAddress"], a["Address"])
                if "SubnetMask" in a:
                    eq("node %s mask" % t["deviceItemPath"], nd["subnetMask"], a["SubnetMask"])
                if "PnDeviceNameAutoGeneration" in a:
                    eq("node %s name auto" % t["deviceItemPath"], nd["parameters"].get("PnPnNoSAutoGenerate"),
                       a["PnDeviceNameAutoGeneration"] == "True")
                if a.get("PnDeviceNameAutoGeneration") == "False":  # generated names are stored stale (H-HW-04)
                    eq("node %s PROFINET name" % t["deviceItemPath"], nd["profinetDeviceName"], a["PnDeviceName"])
            elif a.get("NodeType") == "Profibus":
                eq("node %s PROFIBUS address" % t["deviceItemPath"], nd["profibusAddress"], int(a["Address"]))
        fs = os.path.join(truth, "subnets.json")
        if os.path.exists(fs):
            subs = {s["name"]: s for s in net["subnets"]}
            for s in load(fs)["items"]:
                eq("subnet %s exists" % s["name"], s["name"] in subs, True)
                if s["name"] in subs:
                    eq("subnet %s nodes" % s["name"], len(subs[s["name"]]["nodes"]), s["nodeCount"])
            eq("subnet count", len(net["subnets"]), load(fs)["count"])
        systems = {s["name"]: s for s in net["ioSystems"]}
        for f in glob.glob(os.path.join(truth, "iosystem_*.json")):
            t = load(f)
            s = systems.get(t["name"])
            eq("IO system %s exists" % t["name"], s is not None, True)
            if s:
                eq("IO system %s number" % t["name"], s["number"], t["number"])
                eq("IO system %s subnet" % t["name"], s["subnet"], t["subnet"])
                eq("IO system %s devices" % t["name"], len(s["devices"]), t["connectedIoDeviceCount"])
        if not glob.glob(os.path.join(truth, "iosystem_*.json")):
            eq("no IO system", net["ioSystems"], [])
        cons = {c["name"]: c for c in net["connections"] if c["kind"] != "HmiConnection"}
        for f in glob.glob(os.path.join(truth, "connections_*.json")):
            rows = load(f)["items"]
            eq("connection count", len(cons), len(rows))
            for r in rows:
                pr = r["properties"]
                c = cons.get(pr["LocalConnectionName"])
                eq("connection %s exists" % pr["LocalConnectionName"], c is not None, True)
                if c:
                    eq("connection %s kind" % c["name"], c["kind"], r["kind"] + "Point" if r["kind"].endswith("Connection") else r["kind"])
                    eq("connection %s id" % c["name"], c["connectionId"], pr["LocalConnectionId"])
                    if "LocalPort" in pr:
                        eq("connection %s local port" % c["name"], c["parameters"].get("ConnTcpPortLocal"), pr["LocalPort"])
    if glob.glob(os.path.join(truth, "portDetails_*.json")):  # H5 samples 226-228: ports, send clock, sync domain
        net = d["project"]["network"]
        for f in glob.glob(os.path.join(truth, "portDetails_*.json")):
            t = load(f)
            items = flat(devs[t["deviceKey"]]["hardware"]["items"])
            for r in t["items"]:
                it = items.get("/" + r["deviceItemPath"]) or next(
                    (x for q, x in items.items() if q.endswith("/" + r["deviceItemPath"])), None)
                eq("port %s/%s exists" % (t["deviceKey"], r["deviceItemPath"]), it is not None, True)
                if it:
                    eq("port %s/%s partners" % (t["deviceKey"], r["deviceItemPath"]), len(it["partnerPorts"]),
                       r["connectedPortCount"])
        fc = os.path.join(truth, "change.json")
        ch = load(fc) if os.path.exists(fc) else {"operation": None}
        if ch["operation"] == "portConnect":
            a = ch["arguments"]
            eq("port link", [[p.split("/")[0] + "/" + p.split("/")[-1] for p in l] for l in net["portLinks"]],
               [sorted([a["deviceA"] + "/" + a["deviceItemPathA"].split("/")[-1],
                        a["deviceB"] + "/" + a["deviceItemPathB"].split("/")[-1]])])
        for f in glob.glob(os.path.join(truth, "iface_*.json")):
            t = load(f)
            clock = {x["name"]: x["value"] for x in t["attributes"]}.get("PnSendClock")
            if clock is None:
                continue
            it = flat(devs[t["deviceKey"]]["hardware"]["items"]).get("/" + t["deviceItemPath"])
            # stored only when changed (227); otherwise the catalog default 1 ms (H-HW-01)
            eq("send clock %s" % t["deviceItemPath"], it and it["sendClockNs"], clock if it and it["sendClockNs"] else None)
            if not (it and it["sendClockNs"]):
                eq("send clock %s default" % t["deviceItemPath"], clock, 1000000)
        doms = {x["name"]: x for x in net["syncDomains"]}
        for f in glob.glob(os.path.join(truth, "syncDomains_*.json")):
            for r in load(f)["items"]:
                g = doms.get(r["name"])
                eq("sync domain %s exists" % r["name"], g is not None, True)
                if g:
                    eq("sync domain %s participants" % r["name"], [x.split("/", 1)[0] + "/" + x.split("/")[-1] for x in g["participants"]],
                       sorted(x.split("/", 1)[0] + "/" + x.split("/")[-1] for x in r["participants"]))
                    at = {x["name"]: x["value"] for x in r["attributes"]}
                    eq("sync domain %s high performance" % r["name"], g["parameters"].get("PnIrtSyncDomainExpertMode"),
                       at["HighPerformanceActive"])
    if glob.glob(os.path.join(truth, "item_*.json")):  # H7 samples 230 ff.: stored parameters = Openness attributes
        oa = {os.path.basename(f)[5:-5]: {x["name"]: x["value"] for x in load(f)["attributes"]}
              for f in glob.glob(os.path.join(truth, "item_*.json"))}
        mapped = 0
        fs = os.path.join(truth, "services_PLC_1.json")
        svcs = load(fs) if os.path.exists(fs) else {}
        for path, it in flat(devs["PLC_1"]["hardware"]["items"]).items():
            if not (it["subtype"] or "").startswith("S71500.CPU"):
                continue  # I/O modules are compared via module_*.json (samples 243 ff.)
            for par, att in it["opennessNames"].items():
                if "." in att:  # service property; PlcAccessLevelProvider is not offered for the CPU 1516 V4.1 (UMAC)
                    sv, prop = att.split(".", 1)
                    props = (svcs.get(sv) or {}).get("properties") or {}
                    if prop in props:
                        eq("parameter %s %s = %s" % (path, par, att), it["parameters"][par], props[prop])
                        mapped += 1
                    continue
                # Openness shows hidden submodules (CpuCommon, OPC UA_1) and the NTP role at the CPU item
                own = oa.get("PLC_1_" + it["name"].replace(" ", "_"), {}) if it["subtype"] == "S71500.CPU.Interface.IE" else {}
                exp = own.get(att, oa.get("PLC_1", {}).get(att))
                eq("parameter %s %s = %s" % (path, par, att), it["parameters"][par], exp)
                mapped += 1
        if not glob.glob(os.path.join(truth, "safety_*.json")):  # a new F-CPU stores no changed parameter yet (246)
            eq("mapped parameters present", mapped > 0, True)
        fsec = os.path.join(truth, "security_PLC_1.json")
        if os.path.exists(fsec):  # samples 234 ff.: confidential data, UMAC users and roles
            sec = load(fsec)
            cpu = flat(devs["PLC_1"]["hardware"]["items"]).get("/PLC_1/PLC_1") or {}
            eq("confidential PLC configuration protected", bool((cpu.get("parameters") or {}).get("IsMasterSecretConfigured")),
               sec["TiaProjectSecurity.plcMasterSecretGet"]["masterSecretConfiguration"] == "WithPassword")
            certs = next((x["certificates"] for x in flat(devs["PLC_1"]["hardware"]["items"]).values() if x.get("certificates")), [])
            exp = sec["TiaProjectSecurity.plcCertificateList"]["items"]
            eq("certificate count", len(certs), len(exp))
            for e in exp:
                c = next((x for x in certs if str(x["id"]) == e["id"]), None)
                eq("certificate %s exists" % e["id"], c is not None, True)
                if c:
                    import datetime
                    vu = datetime.datetime.fromisoformat(e["validUntil"][:26] + e["validUntil"][27:]).astimezone(datetime.timezone.utc)
                    eq("certificate %s name" % e["id"], c["name"], e["subjectCommonName"])
                    eq("certificate %s valid until" % e["id"], c["notAfter"], vu.strftime("%Y-%m-%dT%H:%M:%SZ"))
                    eq("certificate %s private key" % e["id"], c["hasPrivateKey"], e["hasPrivateKey"])
            ws = sec["TiaProjectSecurity.webserverCertificateGet"]
            cpu_t = next((x for x in flat(devs["PLC_1"]["hardware"]["items"]).values() if x["subtype"] == "S71500.CPU"), {})
            eq("web server certificate", (cpu_t.get("parameters") or {}).get("WebServerCertificateId"),
               int(ws["certificateId"]) if ws.get("assigned") else None)
            um = d["project"]["umac"]
            eq("UMAC users", [u["name"] for u in um["users"]], sorted(x["name"] for x in sec["TiaUmacUser.usersList"]["items"]))
            eq("UMAC custom roles", [r["name"] for r in um["roles"] if r["objectType"] == "CustomRole"],
               sorted(x["name"] for x in sec["TiaUmacRole.list"]["items"]))
            for ch in load(os.path.join(truth, "change.json")) if os.path.exists(os.path.join(truth, "change.json")) else []:
                a = ch["arguments"]
                if ch["operation"] == "deviceRightAssign":
                    r = next((x for x in um["roles"] if x["name"] == a["role"]), {})
                    eq("role %s right" % a["role"], r.get("deviceRights"), [a["functionRight"]])
                    eq("role %s device" % a["role"], r.get("devices"), [a["device"]])
                if ch["operation"] == "userRoleAssign":
                    u = next((x for x in um["users"] if x["name"] == a["user"]), {})
                    eq("user %s roles" % a["user"], [x["role"] for x in u.get("roles", [])], [a["role"]])
    fsaf = os.path.join(truth, "safety_PLC_1.json")
    if os.path.exists(fsaf):  # samples 246-253: safety administration
        t = load(fsaf)
        sf = devs["PLC_1"]["hardware"]["safety"] or {}
        ps = sf.get("programSettings") or {}
        st = t["safetySettingsGet"]
        eq("safety system version", ps.get("SafetySystemVersion"), st["safetySystemVersion"])
        eq("F change history", ps.get("ActivateFChangelog"), st["activationOfFChangeHistory"])
        eq("safety mode can be disabled", ps.get("SafetyModeCanBeDisabled"), st["safetyModeCanBeDisabled"])
        for mine, theirs in (("DatablockFrom", "fromDB"), ("DatablockTo", "toDB"), ("FunctionblockFrom", "fromFB"),
                             ("FunctionblockTo", "toFB"), ("FunctionsFrom", "fromFC"), ("FunctionsTo", "toFC")):
            eq("F block range %s" % theirs, ps.get(mine), st[theirs])
        eq("F password set", ps.get("passwordSet"), t["safetyPasswordIsSet"]["isSafetyOfflineProgramPasswordSet"])
        rg = {x["name"]: x for x in sf.get("runtimeGroups", [])}
        for r in t["safetyRuntimeGroupList"]["runtimeGroups"]:
            g = rg.get(r["name"])
            eq("F-runtime group %s exists" % r["name"], g is not None, True)
            if g:  # values are microseconds; older TiaMcpXE versions labelled them ...Ms, newer ones ...Us
                eq("F-runtime group %s max cycle" % r["name"], g["maxCycleTimeUs"],
                   r.get("maximumCycleTimeUs", r.get("maximumCycleTimeMs")))
                eq("F-runtime group %s warn cycle" % r["name"], g["warnCycleTimeUs"],
                   r.get("warnCycleTimeUs", r.get("warnCycleTimeMs")))
                eq("F-runtime group %s info DB" % r["name"], g["infoDb"], r["infoDb"])
        for f2 in glob.glob(os.path.join(truth, "module_*.json")):
            m = load(f2)
            at = {x["name"]: x["value"] for x in m["attributes"]}
            if "Failsafe_FIODBName" in at:
                fm = next((x for x in sf.get("fModules", []) if x["deviceItem"].endswith("/" + m["deviceItemPath"])), None)
                eq("F-module %s exists" % m["deviceItemPath"], fm is not None, True)
                if fm:
                    eq("F-module %s F-I/O DB" % m["deviceItemPath"], fm["fIoDb"], at["Failsafe_FIODBName"])
                    eq("F-module %s F-I/O DB number" % m["deviceItemPath"], fm["fIoDbNumber"], at["Failsafe_FIODBNumber"])
                    eq("F-module %s monitoring time" % m["deviceItemPath"], fm["monitoringTime"], at["Failsafe_FMonitoringtime"])
                    eq("F-module %s CRC" % m["deviceItemPath"], fm["parameterCrc"], at["Failsafe_FParameterSignatureWithAddresses"])
        cpu = {x["name"]: x["value"] for x in load(os.path.join(truth, "item_PLC_1.json"))["attributes"]}
        tgt = next((x for x in flat(devs["PLC_1"]["hardware"]["items"]).values() if x["objectType"] == "S7ControllerTargetData"), {})
        if "Failsafe_Central_F_WD_Time" in tgt.get("parameters", {}):
            eq("central F monitoring time", tgt["parameters"]["Failsafe_Central_F_WD_Time"], cpu["Failsafe_DefaultFmonitoringTimeCentral"])
    for f in glob.glob(os.path.join(truth, "module_*.json")):  # samples 243-245: module attributes
        t = load(f)
        attrs = {x["name"]: x["value"] for x in t["attributes"]}
        items = flat(devs[t["deviceKey"]]["hardware"]["items"])
        eq("module %s exists" % t["deviceItemPath"], "/" + t["deviceItemPath"] in items, True)
        for path, it in items.items():
            if path == "/" + t["deviceItemPath"] or path.startswith("/" + t["deviceItemPath"] + "/"):
                for par, att in it["opennessNames"].items():
                    if att in attrs:  # submodule parameters appear at the module in Openness
                        eq("module parameter %s %s = %s" % (path, par, att), it["parameters"][par], attrs[att])
    for f in glob.glob(os.path.join(truth, "addressList_*.json")):  # sample 229: addresses per station
        rows = load(f)["items"]
        for r in rows:
            items = flat(devs[r["station"]]["hardware"]["items"])
            it = items.get("/" + r["deviceItemPath"])
            got = [(a["ioType"], a["startAddress"], a["lengthBits"]) for a in (it or {}).get("addresses", []) if "ioType" in a]
            eq("address %s/%s %s" % (r["station"], r["deviceItemPath"], r["ioType"]),
               (r["ioType"], r["startAddress"], r["lengthBits"]) in got, True)
        if rows:
            st = rows[0]["station"]
            n_got = sum(len([a for a in it["addresses"] if "ioType" in a]) for it in flat(devs[st]["hardware"]["items"]).values())
            eq("address count %s" % st, n_got, len(rows))
    if sample == "126_hmi_subnet":  # MANIFEST: PN/IE_1 with PLC_1 (192.168.0.1) and HMI_1 (192.168.0.2)
        sub = {s["name"]: s for s in d["project"]["network"]["subnets"]}.get("PN/IE_1")
        eq("subnet PN/IE_1", sub is not None, True)
        if sub:
            eq("subnet PN/IE_1 stations", sorted({p.split("/")[0] for p in sub["nodes"]}), ["HMI_1", "PLC_1"])
        ips = {p.split("/")[0]: nd["ipAddress"] for dev in devs.values() for p, it in flat(dev["hardware"]["items"], dev["name"]).items()
               for nd in it["nodes"] if nd["subnet"] == "PN/IE_1"}
        eq("IP PLC_1", ips.get("PLC_1"), "192.168.0.1")
        eq("IP HMI_1", ips.get("HMI_1"), "192.168.0.2")
        conns = d["project"]["network"]["connections"]
        eq("HMI connection", [(c["name"], len(c["nodes"]), len(c["partnerNodes"])) for c in conns if c["kind"] == "HmiConnection"],
           [("HMI_Connection_1", 1, 1)])
    return n, errs


def main():
    samples = sorted({os.path.basename(p)[:-len("_truth")] for p in glob.glob(os.path.join(SAMPLES, "*_truth"))
                      if glob.glob(os.path.join(p, "device_*_details.json")) or os.path.exists(os.path.join(p, "hardware.aml"))}
                     | {"126_hmi_subnet"})
    rows, failed = [], 0
    for sample in samples:
        for kind, src in (("folder", os.path.join(SAMPLES, sample)), ("zap21", os.path.join(SAMPLES, sample + ".zap21"))):
            if not os.path.exists(src):
                continue
            try:
                n, errs = check(sample, src)
            except Exception as e:  # noqa: BLE001
                n, errs = 0, ["%s: %s" % (type(e).__name__, e)]
            failed += bool(errs)
            rows.append((sample, kind, n, errs))
    lines = ["# Hardware tests (openpedata.hardware against Openness ground truth)", "",
             "| Sample | Source | Checks | Result |", "| --- | --- | --- | --- |"]
    for sample, kind, n, errs in rows:
        lines.append("| %s | %s | %d | %s |" % (sample, kind, n, "OK" if not errs else "%d errors" % len(errs)))
    for sample, kind, n, errs in rows:
        for e in errs:
            lines.append("- %s %s: %s" % (sample, kind, e))
    text = "\n".join(lines) + "\n"
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "RESULTS_HARDWARE.md"), "w", encoding="utf-8") as f:
        f.write(text)
    sys.stdout.reconfigure(encoding="utf-8")
    print(text)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
