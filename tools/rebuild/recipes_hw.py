"""Recipes 200-255 (hardware samples: steps H2-H7, further modules, Safety, channel parameters; samples/MANIFEST.md).

Every sample is one change against its predecessor (folder copy -> open -> change -> save -> truth -> archive -> close).
The ground truth files of these samples are raw results of TiaDeviceItem/TiaNetwork*/TiaProjectSecurity/TiaUmac* calls
or small JSON documents built from them; the generators below make the same calls. Where the original truth lists
objects (attribute maps, services, security/safety calls, nodes), the list is read from the old truth file so that
exactly the same objects are queried."""
import json
import os
import re

from rhelp import src
from truth import _raw, _w

PLC = "PLC_1"
PN1 = "PLC_1/PROFINET interface_1"
# test passwords of these samples (samples/MANIFEST.md, sections H7 and Safety; only for these test projects)
PW_237, PW_240, PW_253 = "OpenPEData-Test-237", "OpenPEData-Test-240", "OpenPEData-Test-253"


def _jd(txt):
    try:
        return json.loads(txt)
    except ValueError:
        return {"raw": txt}


def _old(ctx, rel):
    with open(os.path.join(ctx.old_truth, rel), encoding="utf-8-sig") as f:
        return json.load(f)


def _dump(path, obj):
    _w(path, json.dumps(obj, ensure_ascii=False))


def _suffix_dev(rel, default=PLC):
    """Station of <name>_<station>.json (e.g. addressList_IO_1.json); default for <name>.json."""
    m = re.match(r"[A-Za-z]+_(PLC_1|IO_1|DP_1)\.json$", os.path.basename(rel))
    return m.group(1) if m else default


# --- change steps ------------------------------------------------------------------------------------------------------
def chg(tool, **a):
    """Change call recorded for change.json; passwords are written as "(test value)" as in the original truth."""
    def step(ctx):
        args = {k: v for k, v in a.items() if v is not None}
        res = _jd(ctx.call(tool, **args))
        if not hasattr(ctx, "changes"):
            ctx.changes = []
        ctx.changes.append({"tool": tool, "operation": args.get("operation"),
                            "arguments": {k: ("(test value)" if k == "password" else v) for k, v in args.items()
                                          if k not in ("operation", "path")}, "result": res})
    return step


def dev_item(op, device=PLC, **a):
    return ("TiaDeviceItem", dict(operation=op, device=device, **a))


def set_details(item, attrs, device=PLC):
    return dev_item("setDetails", device=device, deviceItemPath=item, attributesJson=json.dumps(attrs, separators=(",", ":")))


def before(*rels):
    """Step: truth files taken before the change (H2: attributes_before.json, accessLevel_before.json)."""
    def step(ctx):
        for rel in rels:
            for pat, gen in HW_TRUTH:
                if re.fullmatch(pat, rel):
                    gen(ctx, ctx.tdir, rel)
                    break
    return step


# --- truth generators ----------------------------------------------------------------------------------------------------
def g_attr_map(ctx, out, rel):
    """attributes*.json (H2/H3): {deviceItemPath: {attribute: {value, writable, error}}} for the items of the old file."""
    if rel.endswith("_before.json") and os.path.exists(os.path.join(out, rel)):
        return
    res = {}
    for item in _old(ctx, rel):
        d = json.loads(_raw(ctx, "TiaDeviceItem", operation="getDetails", device=PLC, deviceItemPath=item))
        res[item] = {a["name"]: {"value": a.get("value"), "writable": a.get("writable"), "error": a.get("error")}
                     for a in d.get("attributes", [])}
    _dump(os.path.join(out, rel), res)


def g_access_level(ctx, out, rel):
    if rel.endswith("_before.json") and os.path.exists(os.path.join(out, rel)):
        return
    _w(os.path.join(out, rel), _raw(ctx, "TiaDeviceItem", operation="serviceGet", device=PLC, deviceItemPath=PLC,
                                    service="PlcAccessLevelProvider"))


def g_cax(ctx, out, rel):
    """hardware.aml (TiaCax export of the project) and cax_export.json (result of that call)."""
    if os.path.exists(os.path.join(out, "cax_export.json")):
        return
    txt = _raw(ctx, "TiaCax", operation="export", targetPath=os.path.join(out, "hardware.aml"))
    _w(os.path.join(out, "cax_export.json"), txt)


def _plain(tool, op, **fixed):
    def g(ctx, out, rel):
        _w(os.path.join(out, rel), _raw(ctx, tool, operation=op, **fixed))
    return g


def _per_dev(tool, op, **fixed):
    def g(ctx, out, rel):
        _w(os.path.join(out, rel), _raw(ctx, tool, operation=op, device=_suffix_dev(rel), **fixed))
    return g


def g_from_old_item(tool, op):
    """node_*/iface_*/item_*/module_*.json: same call for the deviceKey/deviceItemPath of the old file."""
    def g(ctx, out, rel):
        o = _old(ctx, rel)
        _w(os.path.join(out, rel), _raw(ctx, tool, operation=op, device=o["deviceKey"], deviceItemPath=o["deviceItemPath"]))
    return g


IOSYSTEMS = {"PLC_1_PROFINET_interface_1": (PLC, PN1), "CM_DP_PROFIBUS_interface": (PLC, "CM_DP/PROFIBUS interface")}


def g_iosystem(ctx, out, rel):
    dn, item = IOSYSTEMS[rel[len("iosystem_"):-len(".json")]]
    _w(os.path.join(out, rel), _raw(ctx, "TiaNetworkIoSystem", operation="get", device=dn, deviceItemPath=item))


def g_port_details(ctx, out, rel):
    """portDetails_<station>.json: getDetails of every port (attribute values) and NetworkPort.ConnectedPorts count."""
    dn = _old(ctx, rel)["deviceKey"]
    items = []
    for p in json.loads(_raw(ctx, "TiaNetwork", operation="portsDiscover", device=dn))["items"]:
        path = p["deviceItemPath"]
        d = json.loads(_raw(ctx, "TiaDeviceItem", operation="getDetails", device=dn, deviceItemPath=path))
        s = _jd(_raw(ctx, "TiaDeviceItem", ok_errors=True, operation="serviceGet", device=dn, deviceItemPath=path,
                     service="NetworkPort"))
        cp = ((s.get("properties") or {}).get("ConnectedPorts") or {}).get("count")
        items.append({"deviceItemPath": path, "attributes": {a["name"]: a.get("value") for a in d.get("attributes", [])},
                      "connectedPortCount": cp})
    _dump(os.path.join(out, rel), {"deviceKey": dn, "items": items})


def g_sync_domains(ctx, out, rel):
    sub = "PN/IE_1" if rel == "syncDomains_PN_IE_1.json" else None
    _w(os.path.join(out, rel), _raw(ctx, "TiaNetworkSubnet", operation="syncDomainList", subnet=sub))


def g_services(ctx, out, rel):
    """services_PLC_1.json: serviceGet of the services listed in the old file."""
    res = {}
    for sv in _old(ctx, rel):
        res[sv] = _jd(_raw(ctx, "TiaDeviceItem", ok_errors=True, operation="serviceGet", device=PLC, deviceItemPath=PLC,
                           service=sv))
    _dump(os.path.join(out, rel), res)


def g_security(ctx, out, rel):
    """security_PLC_1.json: {"Tool.operation": result} for the calls of the old file (CPU calls with device)."""
    res = {}
    for key in _old(ctx, rel):
        tool, op = key.split(".")
        a = {"device": PLC} if tool == "TiaProjectSecurity" else {}
        res[key] = _jd(_raw(ctx, tool, ok_errors=True, operation=op, **a))
    _dump(os.path.join(out, rel), res)


def g_safety(ctx, out, rel):
    """safety_PLC_1.json: {operation: result} of the TiaProjectSecurity safety reads of the old file. The original calls
    carried the project path (safetyGlobalSettingsGet rejects it: that error is part of the truth)."""
    res = {}
    for op in _old(ctx, rel):
        a = {} if op == "safetyGlobalSettingsGet" else {"device": PLC}
        res[op] = _jd(ctx.call("TiaProjectSecurity", ok_errors=True, operation=op, path=ctx.ap, **a))
    _dump(os.path.join(out, rel), res)


def g_change(ctx, out, rel):
    """change.json: H5 (226-228) wrote the single call as object, the later samples a list."""
    ch = getattr(ctx, "changes", [])
    _dump(os.path.join(out, rel), ch[0] if isinstance(_old(ctx, rel), dict) and len(ch) == 1 else ch)


def g_channels(ctx, out, rel):
    """channels.json (254/255): channels of DI_1/DI_1 with an active hardware interrupt (channelGet)."""
    names = ["HardwareInterruptRisingEdgeActive", "HardwareInterruptFallingEdgeActive"]
    d = json.loads(_raw(ctx, "TiaDeviceItem", operation="channelGet", device=PLC, deviceItemPath="DI_1/DI_1",
                        attributeNames=",".join(names)))
    ch = {n: [] for n in names}
    for it in d.get("items", []):
        for a in it.get("attributes", []):
            if a["name"] in ch and a.get("value") is True:
                ch[a["name"]].append(it["number"])
    with open(os.path.join(out, rel), "w", encoding="utf-8") as f:
        f.write(json.dumps({"source": "TiaDeviceItem(channelGet) PLC_1 DI_1/DI_1", "channels": ch,
                            "channelCount": len(d.get("items", []))}, separators=(",", ":")))


HW_TRUTH = [
    (r"attributes(_before|_after)?\.json", g_attr_map),
    (r"accessLevel_(before|after)\.json", g_access_level),
    (r"hardware\.aml|cax_export\.json", g_cax),
    (r"addressList(_[A-Z]+_\d)?\.json", _per_dev("TiaDeviceItem", "addressList")),
    (r"addressSpaceList(_[A-Z]+_\d)?\.json", _per_dev("TiaDeviceItem", "addressSpaceList")),
    (r"hwIdentifierList(_[A-Z]+_\d)?\.json", _per_dev("TiaDeviceItem", "hwIdentifierList")),
    (r"channelList_[A-Z]+_\d\.json", _per_dev("TiaDeviceItem", "channelList")),
    (r"interfaces_[A-Z]+_\d\.json", _per_dev("TiaNetwork", "interfacesDiscover")),
    (r"ports_[A-Z]+_\d\.json", _per_dev("TiaNetwork", "portsDiscover")),
    (r"OB30\.attributes\.json", _plain("TiaPlcSoftware", "attributeGet", objectKind="block", name="Cyclic interrupt")),
    (r"node_.+\.json", g_from_old_item("TiaNetworkNode", "getAttributes")),
    (r"(iface|item|module)_.+\.json", g_from_old_item("TiaDeviceItem", "getDetails")),
    (r"iosystem_.+\.json", g_iosystem),
    (r"subnets\.json", _plain("TiaNetworkSubnet", "list")),
    (r"connections_PLC_1\.json", _plain("TiaNetwork", "connectionList", device=PLC, deviceItemPath=PLC)),
    (r"portDetails_.+\.json", g_port_details),
    (r"syncDomains_.+\.json", g_sync_domains),
    (r"services_PLC_1\.json", g_services),
    (r"security_PLC_1\.json", g_security),
    (r"safety_PLC_1\.json", g_safety),
    (r"change\.json", g_change),
    (r"channels\.json", g_channels),
]

H2_BEFORE = before("attributes_before.json", "accessLevel_before.json")


def hw(pred, *steps, pre=()):
    return {"from": pred, "steps": list(pre) + list(steps), "generators": HW_TRUTH}


RECIPES = {
    # --- H2: module parameters of the CPU (truth before and after the change) ---
    "200_hw_cycle_time": hw("002_cpu", set_details(PLC, {"CycleMaximumCycleTime": 200}), pre=[H2_BEFORE]),
    # ClockMemoryByteAddress only exists after the activation
    "201_hw_clock_memory": hw("002_cpu", set_details(PLC, {"ClockMemoryByte": True}),
                              set_details(PLC, {"ClockMemoryByteAddress": 100}), pre=[H2_BEFORE]),
    "202_hw_startup": hw("002_cpu", set_details(PLC, {"StartupActionAfterPowerOn": 1}), pre=[H2_BEFORE]),
    "203_hw_webserver": hw("002_cpu", set_details(PLC, {"WebserverActivate": True}), pre=[H2_BEFORE]),
    "204_hw_access_level": hw("002_cpu", dev_item("serviceSet", deviceItemPath=PLC, service="PlcAccessLevelProvider",
                                                  propertiesJson='{"PlcProtectionAccessLevel":"FullAccess"}'),
                              pre=[H2_BEFORE]),
    # --- H3: I/O addresses, HW identifiers, process image ---
    "205_hw_di_module": hw("002_cpu", dev_item("plugNew", typeIdentifier="OrderNumber:6ES7 521-1BL00-0AB0/V2.2", name="DI_1",
                                               positionNumber="2", parentDeviceItemPath="Rail_0")),
    "206_hw_io_start": hw("205_hw_di_module", dev_item("addressSetStart", deviceItemPath="DI_1/DI_1", startAddress="10")),
    "207_hw_ob_cyclic": hw("205_hw_di_module", ("TiaPlcSoftware", dict(operation="import", objectKind="block",
                                                                       sourcePath=src("OB_CyclicInterrupt.xml"),
                                                                       importMode="None"))),
    "208_hw_process_image": hw("207_hw_ob_cyclic", dev_item("processImageAssign", deviceItemPath="DI_1/DI_1", plcDevice=PLC,
                                                            plcDeviceItemPath=PLC, obName="Cyclic interrupt",
                                                            processImage="1")),
    "209_hw_dq_module": hw("205_hw_di_module", dev_item("plugNew", typeIdentifier="OrderNumber:6ES7 522-1BL01-0AB0/V1.1",
                                                        name="DQ_1", positionNumber="3", parentDeviceItemPath="Rail_0")),
    # --- H4: networks, IO systems, distributed I/O, connections ---
    "210_hw_ip_address": hw("002_cpu", ("TiaNetworkNode", dict(operation="setIpv4", device=PLC, deviceItemPath=PN1,
                                                               address="192.168.0.10"))),
    "211_hw_pn_name": hw("002_cpu", ("TiaNetworkNode", dict(operation="setProfinetName", device=PLC, deviceItemPath=PN1,
                                                            profinetName="plc-test"))),
    "212_hw_subnet": hw("002_cpu", ("TiaNetworkSubnet", dict(operation="nodeCreateAndConnect", device=PLC, deviceItemPath=PN1,
                                                             subnetName="PN/IE_1", subnetType="Profinet"))),
    "213_hw_io_system": hw("212_hw_subnet", ("TiaNetworkIoSystem", dict(operation="create", device=PLC, deviceItemPath=PN1))),
    "214_hw_et200sp_station": hw("213_hw_io_system", ("TiaDevice", dict(
        operation="create", name="IO_1", deviceType="OrderNumber:6ES7 155-6AU01-0BN0/V4.2", deviceItem="IM_1"))),
    "215_hw_io_subnet": hw("214_hw_et200sp_station", ("TiaNetworkSubnet", dict(
        operation="nodeConnect", device="IO_1", deviceItemPath="IM_1/PROFINET interface", subnet="PN/IE_1"))),
    "216_hw_io_device": hw("215_hw_io_subnet", ("TiaNetworkIoSystem", dict(
        operation="ioConnect", device="IO_1", deviceItemPath="IM_1/PROFINET interface", targetDevice=PLC,
        targetDeviceItemPath=PN1))),
    "217_hw_io_module": hw("216_hw_io_device", dev_item("plugNew", device="IO_1", typeIdentifier="OrderNumber:6ES7 131-6BF01-0BA0/V0.0",
                                                        name="DI_8", positionNumber="1", parentDeviceItemPath="Rack_0")),
    "218_hw_s7_connection": hw("212_hw_subnet", ("TiaNetwork", dict(operation="connectionCreate", device=PLC, deviceItemPath=PLC,
                                                                    connectionType="S7", localInterfacePath=PN1))),
    "219_hw_tcp_connection": hw("212_hw_subnet", ("TiaNetwork", dict(operation="connectionCreate", device=PLC,
                                                                     deviceItemPath=PLC, connectionType="TCP",
                                                                     localInterfacePath=PN1))),
    "220_hw_cm_dp": hw("002_cpu", dev_item("plugNew", typeIdentifier="OrderNumber:6GK7 542-5DX00-0XE0/V2.0", name="CM_DP",
                                           positionNumber="2", parentDeviceItemPath="Rail_0")),
    "221_hw_profibus": hw("220_hw_cm_dp", ("TiaNetworkSubnet", dict(operation="nodeCreateAndConnect", device=PLC,
                                                                    deviceItemPath="CM_DP/PROFIBUS interface",
                                                                    subnetType="Profibus", subnetName="PROFIBUS_1"))),
    # the server of 2026-10-24 named the DP master system "PROFINET IO-System" (MCP-084, MANIFEST); the current one
    # names it "DP master system": renamed to keep the content of the original sample
    "222_hw_dp_master": hw("221_hw_profibus", ("TiaNetworkIoSystem", dict(operation="create", device=PLC,
                                                                          deviceItemPath="CM_DP/PROFIBUS interface")),
                           ("TiaNetworkIoSystem", dict(operation="setName", device=PLC, deviceItemPath="CM_DP/PROFIBUS interface",
                                                       newName="PROFINET IO-System"))),
    "223_hw_dp_station": hw("222_hw_dp_master", ("TiaDevice", dict(operation="create", name="DP_1",
                                                                   deviceType="OrderNumber:6ES7 155-6BU01-0CN0/V4.2",
                                                                   deviceItem="IM_DP"))),
    "224_hw_dp_subnet": hw("223_hw_dp_station", ("TiaNetworkSubnet", dict(operation="nodeConnect", device="DP_1",
                                                                          deviceItemPath="IM_DP", subnet="PROFIBUS_1"))),
    "225_hw_dp_slave": hw("224_hw_dp_subnet", ("TiaNetworkIoSystem", dict(
        operation="ioConnect", device="DP_1", deviceItemPath="IM_DP", targetDevice=PLC,
        targetDeviceItemPath="CM_DP/PROFIBUS interface"))),
    # --- H5: topology and synchronization ---
    "226_hw_port_link": hw("217_hw_io_module", chg("TiaNetworkNode", operation="portConnect", deviceA=PLC,
                                                  deviceItemPathA=PN1 + "/Port_1", deviceB="IO_1",
                                                  deviceItemPathB="BA 2xRJ45/Port_1")),
    "227_hw_send_clock": hw("226_hw_port_link", chg("TiaDeviceItem", operation="setDetails", device=PLC, deviceItemPath=PN1,
                                                    attributesJson='{"PnSendClock":2000000}')),
    "228_hw_sync_domain": hw("226_hw_port_link", chg("TiaNetworkSubnet", operation="syncDomainSet", subnet="PN/IE_1",
                                                     domain="Sync-Domain_1", attributesJson='{"HighPerformanceActive":true}')),
    # --- H7: services and protection. 230-233 were made by hand in the TIA user interface; the same settings are
    # Openness attributes of the CPU and its OPC UA item (OpcUaPort only exists once the server is active) ---
    "230_hw_opcua_on": hw("039_fw41", set_details("PLC_1/OPC UA_1", {"OpcUaServer": True}),
                          set_details(PLC, {"OpcUaPurchasedLicense": 2})),
    "231_hw_opcua_port": hw("230_hw_opcua_on", set_details("PLC_1/OPC UA_1", {"OpcUaPort": 4841})),
    "232_hw_ntp": hw("231_hw_opcua_port", set_details(PLC, {"TimeSynchronizationNtpV2": 2}),
                     # the update interval stays untouched: 10 s is the default, and the original (made in the user
                     # interface) does not store it; setting it via Openness writes the parameter TimeSyncInterval
                     set_details(PLC, {"TimeSynchronizationServer1": "192.168.0.100"})),
    "233_hw_timezone": hw("232_hw_ntp", set_details(PLC, {"TimeOfDayLocalTimeZone": 82,
                                                          "TimeOfDayActivateDaylightSavingTime": False})),
    "234_hw_sysdiag": hw("233_hw_timezone", chg("TiaDeviceItem", operation="setDetails", device=PLC, deviceItemPath=PLC,
                                                attributesJson='{"SuppressDeactivatingSystemDiagnosticsAlarms":true}')),
    "236_hw_access_control": hw("234_hw_sysdiag", chg("TiaDeviceItem", operation="setDetails", device=PLC, deviceItemPath=PLC,
                                                      attributesJson='{"PlcAccessControlConfiguration":"Disabled"}')),
    "237_hw_confidential": hw("236_hw_access_control", chg("TiaProjectSecurity", operation="plcMasterSecretProtect", device=PLC,
                                                           password=PW_237)),
    "238_hw_certificate": hw("237_hw_confidential", chg("TiaProjectSecurity", operation="plcCertificateCreate", device=PLC,
                                                        subjectCommonName="PLC_1-Test")),
    "240_hw_umac_user": hw("238_hw_certificate", chg("TiaUmacUser", operation="userAdd", user="testuser", password=PW_240)),
    "239_hw_webserver": hw("240_hw_umac_user", chg("TiaDeviceItem", operation="setDetails", device=PLC, deviceItemPath=PLC,
                                                   attributesJson='{"WebserverActivate":true}')),
    "241_hw_umac_role": hw("239_hw_webserver", chg("TiaUmacRole", operation="create", role="WebDiag"),
                           chg("TiaUmacRole", operation="deviceRightAssign", role="WebDiag", device=PLC,
                               functionRight="WebAllowDiagnostics")),
    "242_hw_umac_user_role": hw("241_hw_umac_role", chg("TiaUmacUser", operation="userRoleAssign", user="testuser", role="WebDiag")),
    # --- further modules ---
    "243_hw_aiaq_module": hw("209_hw_dq_module", chg("TiaDeviceItem", operation="plugNew", device=PLC,
                                                     typeIdentifier="OrderNumber:6ES7 534-7QE00-0AB0/V1.0", name="AIAQ_1",
                                                     positionNumber="4", parentDeviceItemPath="Rail_0")),
    "244_hw_aiaq_value_status": hw("243_hw_aiaq_module", chg("TiaDeviceItem", operation="setDetails", device=PLC,
                                                             deviceItemPath="AIAQ_1", attributesJson='{"EnableValueStatus":true}')),
    "245_hw_tm_count": hw("243_hw_aiaq_module", chg("TiaDeviceItem", operation="plugNew", device=PLC,
                                                    typeIdentifier="OrderNumber:6ES7 550-1AA01-0AB0/V2.0", name="TM_Count_1",
                                                    positionNumber="5", parentDeviceItemPath="Rail_0")),
    # --- Safety (also the predecessors of 069_safety_compiled) ---
    "246_hw_fcpu": hw("000_empty", chg("TiaDevice", operation="create", name=PLC, deviceType="OrderNumber:6ES7 513-1FL02-0AB0/V2.9")),
    "247_hw_f_settings": hw("246_hw_fcpu", chg("TiaProjectSecurity", operation="safetySettingsSet", device=PLC,
                                               attributes='{"ActivationOfFChangeHistory":true}')),
    "248_hw_f_monitoring": hw("247_hw_f_settings", chg("TiaDeviceItem", operation="setDetails", device=PLC, deviceItemPath=PLC,
                                                       attributesJson='{"Failsafe_DefaultFmonitoringTimeCentral":200}')),
    "249_hw_fdi_module": hw("248_hw_f_monitoring", chg("TiaDeviceItem", operation="plugNew", device=PLC,
                                                       typeIdentifier="OrderNumber:6ES7 526-1BH00-0AB0/V1.0", name="FDI_1",
                                                       positionNumber="2", parentDeviceItemPath="Rail_0")),
    "250_hw_fdi_dest_address": hw("249_hw_fdi_module", chg("TiaDeviceItem", operation="setDetails", device=PLC,
                                                           deviceItemPath="FDI_1/FDI_1",
                                                           attributesJson='{"Failsafe_FDestinationAddress":10}')),
    "251_hw_fdi_shortcircuit": hw("250_hw_fdi_dest_address", chg("TiaDeviceItem", operation="setDetails", device=PLC,
                                                                 deviceItemPath="FDI_1/FDI_1",
                                                                 attributesJson='{"Failsafe_ShortCircuitTest_0":false}')),
    "252_hw_f_runtime_group": hw("251_hw_fdi_shortcircuit", chg("TiaProjectSecurity", operation="safetyRuntimeGroupSet", device=PLC,
                                                                name="F-runtime group 1",
                                                                attributes='{"MaximumCycleTime":200000}')),
    "253_hw_f_password": hw("252_hw_f_runtime_group", chg("TiaProjectSecurity", operation="safetyPasswordSet", device=PLC,
                                                          password=PW_253)),
    # --- channel parameters ---
    "254_hw_channel_param": hw("205_hw_di_module", dev_item("channelSet", deviceItemPath="DI_1/DI_1", channelNumber="0",
                                                            attributesJson='{"HardwareInterruptRisingEdgeActive":true}')),
    "255_hw_channel_param2": hw("254_hw_channel_param",
                                dev_item("channelSet", deviceItemPath="DI_1/DI_1", channelNumber="5",
                                         attributesJson='{"HardwareInterruptFallingEdgeActive":true}'),
                                dev_item("channelSet", deviceItemPath="DI_1/DI_1", channelNumber="17",
                                         attributesJson='{"HardwareInterruptRisingEdgeActive":true}')),
}
