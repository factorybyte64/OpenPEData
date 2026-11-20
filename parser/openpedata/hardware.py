"""Hardware and network configuration (GOALS.md Z1.1, docs/spec/hw-config.md).

Navigation (relation names from the metamodel):
  DeviceData                   station; device items with Parent -> station are its top level (rack, CPU, HMI panel)
  objects with IDeviceItemData DeviceItemData, S7ControllerTargetData, NonProgrammableTargetData, HmiApplicationData;
                               tree via Parent, order by IDeviceItemData.PositionNumber (as in Openness DeviceItems)
  NodeData                     address node of an interface: DeviceItem -> interface item, Subnet -> SubnetData
  SubnetData                   subnet (ConfigObjectTypeName SubnetIE, ...)
  DomainData                   sync domain (Subtype Internal.SyncDomain) / MRP domain; InterfaceSubmodules -> interfaces
  port DeviceItemData          PortToPorts -> partner port (port interconnection, sample 226; stored on both ports)
  HmiConnectionPointData       end point of an HMI connection: Conn2Nodes -> NodeData, HmiConnection -> HmiConnectionData

Parameters are the named expando attributes of IPersistentExpandoData. Only values that differ from the hardware
catalog are stored (H-HW-01, confirmed); everything else is "default per catalog" and is not output (decision
2026-10-24). The catalog entry is given as `catalog` (IConfigBaseData.ConfigObjectTypeName); `opennessNames` maps
stored parameters to their Openness attribute where a sample confirms it.
IConfigBaseData.AdditionalInfos / PersistentExpandoAttributeFlags and the address blobs are not decoded yet (raw hex).
"""
import re

# expando parameters that hold secrets: only report that they are set
SECRET = ("EncryptedPassword", "RngCryptoSalt", "PkiContainerCache")  # PKI: certificates are output decoded, the
# container also holds the encrypted private keys
# attribute sets shown as dedicated fields or without information for the user
HIDDEN_SETS = {"ICoreAttributes", "ICoreExpandoAttributes", "IConfigBaseData", "IDeviceItemData", "IDeviceData",
               "IPersistentExpandoData", "IBrowsingAttributes", "ICompileLoadConfigExpando", "INodeData",
               "IAddressAttributes"}
# stored as int, meant as u32 (Openness shows the unsigned value; F-parameter CRC, sample 251)
U32_PARAMS = {"Failsafe_CRC_3"}
IP_PARAMS = {"NodeIPAddress", "NodeIPSubnetMask", "NodeIPDefaultRouterAddress"}
IP_PROTOCOL_SELECTIONS = ["Project", "Dhcp", "UserProgram", "OtherPath", "ViaIoController", "None"]
# (subtype of the carrying item, expando) -> Openness attribute; only pairs confirmed by samples 200-204, 230-233
# (docs/spec/hw-config.md). Values are identical (enums as stored numbers). Openness shows the parameters of the
# hidden submodules CpuCommon and CPUexecUnit at the CPU DeviceItem.
OPENNESS_NAMES = {
    ("S71500.CPU.CPUexecUnit", "MaxCycleConfig"): "CycleMaximumCycleTime",
    ("S71500.CPU.CPUexecUnit", "PowerOnAction"): "StartupActionAfterPowerOn",
    ("S71500.CPU.CpuCommon", "UseClockFlags"): "ClockMemoryByte",
    ("S71500.CPU.CpuCommon", "ClockFlags_Local"): "ClockMemoryByteAddress",
    ("S71500.CPU.CpuCommon", "ProtectionLevel"): "PlcAccessLevelProvider.PlcProtectionAccessLevel",
    ("S71500.CPU", "WebServerActive"): "WebserverActivate",
    ("S71500.CPU.Interface.IE", "EnableWebServerAccess4IE"): "UseWebServerForInterface",
    # samples 230-233 (CPU 1516-3 PN/DP V4.1; OPC UA server and port on OPC UA_1 have no Openness attribute)
    ("S71500.CPU", "SelectedOpcUaLicense"): "OpcUaPurchasedLicense",
    # samples 248-251: F-parameters (F-CPU 1513F-1 PN V2.9, F-DI 16x24VDC); the F-DI parameters sit on the submodule
    ("S71500.CPU", "Failsafe_Central_F_WD_Time"): "Failsafe_DefaultFmonitoringTimeCentral",
    ("S71500.SM.DI.DI16", "Failsafe_F_Dest_Add"): "Failsafe_FDestinationAddress",
    ("S71500.SM.DI.DI16", "Failsafe_F_Par_CRC"): "Failsafe_FParameterSignatureWithAddresses",
    ("S71500.SM.DI.DI16", "Failsafe_F_Par_CRC_WithoutAddresses"): "Failsafe_FParameterSignatureWithoutAddresses",
    ("S71500.SM.DI.DI16", "Failsafe_CRC_3"): "Failsafe_FParameterSignatureIndividualParameters",
    ("S71500.SM.DI.DI16", "Failsafe_Short_Circuit_Test_0"): "Failsafe_ShortCircuitTest_0",
    # sample 244: value status of an I/O module; the submodule stores it, Openness shows it at the module
    ("S71500.SM.AI.4AI2AO", "QualityInformation"): "EnableValueStatus",
    # samples 234-239 (Openness attribute, or service.property where the value sits in an Openness service)
    ("S71500.CPU", "SuppressDeactivatingSystemDiagnosticsAlarms"): "SuppressDeactivatingSystemDiagnosticsAlarms",
    ("S71500.CPU.CpuCommon", "EnableLockUserAccountAtRunTime"): "PlcAccountLockingAtRuntimeFeature.EnableLockUserAccountAtRunTime",
    ("S71500.CPU.CpuCommon", "AccountLockedFor"): "PlcAccountLockingAtRuntimeFeature.LockAccountForDuration",
    ("S71500.CPU.CpuCommon", "NoOfFailedLoginAttempts"): "PlcAccountLockingAtRuntimeFeature.NumberOfFailedLoginAttempts",
    ("S71500.CPU.CpuCommon", "TimeBetweenFailedLoginAttempts"): "PlcAccountLockingAtRuntimeFeature.LoginRetryInterval",
    ("S71500.CPU.CpuCommon", "LocalTimeZone"): "TimeOfDayLocalTimeZone",
    ("S71500.CPU.CpuCommon", "EnableDaylightSavingsTime"): "TimeOfDayActivateDaylightSavingTime",
    ("S71500.CPU.Interface.IE", "TimeSyncRole"): "TimeSynchronizationNtpV2",
    ("S71500.CPU.Interface.IE", "TimeSyncInterval"): "TimeSynchronizationUpdateInterval",
    ("S71500.CPU.Interface.IE", "TimeSyncNtpServer1"): "TimeSynchronizationServer1",
    ("S71500.CPU.Interface.IE", "TimeSyncNtpServer2"): "TimeSynchronizationServer2",
    ("S71500.CPU.Interface.IE", "TimeSyncNtpServer3"): "TimeSynchronizationServer3",
    ("S71500.CPU.Interface.IE", "TimeSyncNtpServer4"): "TimeSynchronizationServer4",
}


def ip4(v):
    """u32 as stored (3232235521) -> '192.168.0.1'."""
    return ".".join(str(v >> s & 0xFF) for s in (24, 16, 8, 0)) if isinstance(v, int) else v


def mac(v):
    """u64 as stored -> 'XX-XX-XX-XX-XX-XX' (6 bytes, big endian)."""
    return "-".join("%02X" % (v >> s & 0xFF) for s in (40, 32, 24, 16, 8, 0)) if isinstance(v, int) else v


IO_TYPES = {2: "Input", 4: "Output"}
SEND_CLOCK_BASE_NS = 31250  # PnIoSendClockFactor x 31.25 us = Openness PnSendClock (sample 227: 64 -> 2000000 ns)


def address_data(b):
    """IAddressAttributes.AM_x_AddrData_y (34 bytes, samples 205-209): u32 length, u32 IO type (2 input, 4 output),
    u16 at 8 (40 in all samples, meaning open), u16 length in bits, u32 at 18 process image partition (0 = automatic
    update), u32 at 22 start address in bits. Field widths of the zero runs are not proven."""
    if not isinstance(b, (bytes, bytearray)) or len(b) < 26 or int.from_bytes(b[0:4], "little") != len(b):
        return {"raw": b.hex() if isinstance(b, (bytes, bytearray)) else b}
    u16 = lambda o: int.from_bytes(b[o:o + 2], "little")  # noqa: E731
    u32 = lambda o: int.from_bytes(b[o:o + 4], "little")  # noqa: E731
    code, start = u32(4), u32(22)
    return {"ioType": IO_TYPES.get(code, code), "startAddress": start // 8, "startBit": start % 8,
            "lengthBits": u16(10), "processImage": u32(18), "raw": b.hex()}


def address_map(b):
    """AddressManagerV2Data.AddressMap (samples 002, 205-209): versioned header (u16 at 30 = length of the version
    string at 32, e.g. '14.0.0.0'), u32 count, then count entries of 16 bytes: u16 kind (8 = station, 2 = device item),
    u16 0x10, u32 index into the manager's RegisteredDeviceItems (kind 2), u32 hardware identifier, u32 0.
    Returns [(kind, index, hwid)] or None when the layout does not fit."""
    if not isinstance(b, (bytes, bytearray)) or len(b) < 36:
        return None
    off = 32 + int.from_bytes(b[30:32], "little")
    n = int.from_bytes(b[off:off + 4], "little")
    if off + 4 + 16 * n != len(b):
        return None
    out = []
    for i in range(n):
        e = b[off + 4 + 16 * i:off + 20 + 16 * i]
        out.append((int.from_bytes(e[0:2], "little"), int.from_bytes(e[4:8], "little"), int.from_bytes(e[8:12], "little")))
    return out


GSD_KINDS = (("Comp_", "_Rack", "R"), ("HM_", "", "DAP"), ("Module_", "", "M"), ("Submodule_", "", "SM"),
             ("Interface_", "", "Interface"), ("Port_", "", "Port"))
GSD_ESCAPES = (("_SPA_", " "), ("_DOT_", "."))


def gsd_type(catalog):
    """Catalog entry of a GSD object, 'GSD\\<gsdml file>\\<kind>_<id>' (sample 229, not in the repo) -> file, kind, id and, where the
    id is stored, the Openness TypeIdentifier 'GSD:<FILE>/<kind>/<id>' (DEVICE -> '/D'; R, DAP, M, SM). Interface and
    port submodules are stored as Interface_/Port_<DAP id>_<subslot>; their Openness id comes from the GSDML only."""
    parts = (catalog or "").split("\\")
    if len(parts) != 3 or parts[0] != "GSD":
        return None
    fn, ent = parts[1], parts[2]
    if ent == "DEVICE":
        return {"file": fn, "kind": "DEVICE", "id": None, "typeIdentifier": "GSD:%s/D" % fn.upper()}
    for pre, suf, kind in GSD_KINDS:
        if ent.startswith(pre) and ent.endswith(suf):
            ident = ent[len(pre):len(ent) - len(suf)]
            for a, b in GSD_ESCAPES:
                ident = ident.replace(a, b)
            tid = "GSD:%s/%s/%s" % (fn.upper(), kind, ident) if kind in ("R", "DAP", "M", "SM") else None
            return {"file": fn, "kind": kind, "id": ident, "typeIdentifier": tid}
    return {"file": fn, "kind": None, "id": ent, "typeIdentifier": None}


def _der(b, o):
    """(tag, content start, content end) of the DER element at o."""
    tag, n = b[o], b[o + 1]
    o += 2
    if n & 0x80:
        k = n & 0x7F
        n = int.from_bytes(b[o:o + k], "big")
        o += k
    return tag, o, o + n


def _der_children(b, o, e):
    out = []
    while o < e:
        t, cs, ce = _der(b, o)
        out.append((t, cs, ce))
        o = ce
    return out


def _der_time(b, t, cs, ce):
    v = b[cs:ce].decode("ascii", errors="replace")
    if t == 0x17:  # UTCTime YYMMDDhhmmssZ
        v = ("19" if int(v[:2]) >= 50 else "20") + v
    return "%s-%s-%sT%s:%s:%sZ" % (v[0:4], v[4:6], v[6:8], v[8:10], v[10:12], v[12:14])


def _der_name(b, cs, ce):
    """RDN sequence -> 'CN=..., O=...' (common attribute types only)."""
    oids = {b"\x55\x04\x03": "CN", b"\x55\x04\x0a": "O", b"\x55\x04\x0b": "OU", b"\x55\x04\x06": "C"}
    parts = []
    for _, ss, se in _der_children(b, cs, ce):
        for _, as_, ae in _der_children(b, ss, se):
            (_, os_, oe), (_, vs, ve) = _der_children(b, as_, ae)[:2]
            parts.append("%s=%s" % (oids.get(bytes(b[os_:oe]), bytes(b[os_:oe]).hex()), bytes(b[vs:ve]).decode("utf-8", "replace")))
    return ", ".join(parts)


def x509_summary(der):
    """Subject, issuer, serial and validity of a DER certificate (enough for listing, no verification)."""
    _, cs, ce = _der(der, 0)
    tbs = _der_children(der, *_der(der, cs)[1:])
    i = 1 if tbs[0][0] == 0xA0 else 0  # optional [0] version
    serial, issuer, validity, subject = tbs[i], tbs[i + 2], tbs[i + 3], tbs[i + 4]
    nb, na = _der_children(der, validity[1], validity[2])
    return {"subject": _der_name(der, subject[1], subject[2]), "issuer": _der_name(der, issuer[1], issuer[2]),
            "serialNumber": bytes(der[serial[1]:serial[2]]).hex(),
            "notBefore": _der_time(der, *nb), "notAfter": _der_time(der, *na)}


PKI_ENTRY = re.compile(rb"(?P<len>[\x01-\x7f])(?P<name>[\x20-\x7e]{1,127})(?P<id>[\x00-\xff]{4})\x00\x00\x00\x00\x01\x00\x00\x00"
                       rb"\x02\x00\x00\x00\x00\x00\x00\x00\x14\x00(?P<thumb>[\x00-\xff]{20})", re.S)


def pki_certificates(blob):
    """Certificates of S7ControllerTargetData.PkiContainerCache (samples 237-239): after a 24/28-byte header the
    container is a sequence of zlib streams (4096-byte pages, each with its u16 compressed length in front). The
    pages hold PEM blocks; each certificate is preceded by: varint text subject alternative names (may be empty),
    u8 length + name, u32 certificate ID (= Openness LocalCertificateStore ID), u32 0, u32 1, u32 2, u32 0,
    u16 20 + SHA-1 thumbprint. An 'ENCRYPTED PRIVATE KEY' block follows certificates with private key (the key is not
    output)."""
    import base64
    import zlib
    if not isinstance(blob, (bytes, bytearray)):
        return None
    data, pos = b"", 0
    while True:
        i = blob.find(b"\x78\x5e", pos)
        if i < 0:
            break
        d = zlib.decompressobj()
        try:
            data += d.decompress(blob[i:])
        except zlib.error:
            pos = i + 1
            continue
        pos = len(blob) - len(d.unused_data)
    pem = list(re.finditer(rb"-----BEGIN ([A-Z ]+)-----\r?\n(.*?)-----END \1-----", data, re.S))
    out = []
    for j, m in enumerate(pem):
        if m.group(1) != b"CERTIFICATE":
            continue
        prev_end = pem[j - 1].end() if j else 0
        meta = None
        for meta in PKI_ENTRY.finditer(data, prev_end, m.start()):
            pass
        if meta is not None and meta.group("len")[0] != len(meta.group("name")):
            meta = None
        cert = {"id": int.from_bytes(meta.group("id"), "little") if meta else None,
                "name": meta.group("name").decode("ascii") if meta else None,
                "thumbprintSha1": meta.group("thumb").hex() if meta else None,
                "hasPrivateKey": j + 1 < len(pem) and pem[j + 1].group(1) == b"ENCRYPTED PRIVATE KEY"}
        if meta:
            k = prev_end
            seg = data[k:meta.start()]
            san = re.search(rb"([\x01-\x7f])((?:IP|DNS|URI|EMAIL):[\x20-\x7e]+)$", seg)
            cert["subjectAlternativeNames"] = san.group(2).decode("ascii").split(",") if san and san.group(1)[0] == len(san.group(2)) else []
        try:
            cert.update(x509_summary(base64.b64decode(m.group(2))))
        except (ValueError, IndexError):
            pass
        out.append(cert)
    return out


def umac_value(b):
    """UMAC `...Encrypted` attribute (samples 240-242): u8 0, u32 length n at 1, flags at 14..23, n data bytes at 24.
    With bytes 19 and 20 = 1 the data is stored plain (project without encryption): returns the bytes, else None.
    Empty values are 20 bytes with byte 19 = 0."""
    if not isinstance(b, (bytes, bytearray)) or len(b) < 24 or b[19] != 1 or b[20] != 1:
        return None
    n = int.from_bytes(b[1:5], "little")
    return bytes(b[24:24 + n]) if len(b) == 24 + n else None


def umac_text(b):
    v = umac_value(b)
    return v.decode("utf-16-le", errors="replace") if v is not None else None


def gsdml_submodules(project_path):
    """{(gsdml file name lower, DAP id): {subslot: submodule ID}} of the interface and port submodules of every
    DeviceAccessPointItem in the GSDML files TIA copied into AdditionalFiles/GSD (project folder or .zap21; sample 229, not in the repo).
    The catalog entry stores these submodules only as Interface_/Port_<DAP id>_<subslot>; Openness names them by the
    GSDML submodule ID ('GSD:<FILE>/SM/<ID>')."""
    import os
    import xml.etree.ElementTree as ET
    import zipfile
    files = {}
    try:
        if os.path.isdir(project_path):
            d = os.path.join(project_path, "AdditionalFiles", "GSD")
            for n in os.listdir(d) if os.path.isdir(d) else []:
                if n.lower().endswith(".xml"):
                    with open(os.path.join(d, n), "rb") as fh:
                        files[n] = fh.read()
        elif zipfile.is_zipfile(project_path):
            with zipfile.ZipFile(project_path) as z:
                for n in z.namelist():
                    if n.startswith("AdditionalFiles/GSD/") and n.lower().endswith(".xml"):
                        files[n.rsplit("/", 1)[1]] = z.read(n)
    except OSError:
        return {}
    res = {}
    for n, data in files.items():
        try:
            root = ET.fromstring(data)
        except ET.ParseError:
            continue
        for dap in root.iter():
            if not dap.tag.endswith("DeviceAccessPointItem"):
                continue
            subs = {}
            for e in dap.iter():
                if e.tag.endswith("InterfaceSubmoduleItem") or e.tag.endswith("PortSubmoduleItem"):
                    if e.get("SubslotNumber") and e.get("ID"):
                        subs[int(e.get("SubslotNumber"))] = e.get("ID")
            res[(n.lower(), dap.get("ID"))] = subs
    return res


def _value(name, v):
    if any(name.startswith(s) for s in SECRET):
        return "(set, %d bytes)" % len(v) if v else None
    return v


class Hardware:
    def __init__(self, model):
        self.m = model
        self.p = model.p
        self._hw = None
        self._gsdml = None

    # --- helpers ----------------------------------------------------------------------------------
    def _attrs(self, k):
        return self.p.attributes(k)[0]

    def _params(self, k):
        ex = self._attrs(k).get("IPersistentExpandoData", {})
        return {n: (v & 0xFFFFFFFF if n in U32_PARAMS and isinstance(v, int) else _value(n, v)) for n, v in sorted(ex.items())}

    def _other_sets(self, k):
        return {s: v for s, v in self._attrs(k).items() if s not in HIDDEN_SETS and v}

    def _catalog(self, k):
        cb = self._attrs(k).get("IConfigBaseData", {})
        return cb.get("ConfigObjectTypeName"), cb.get("ConfigObjectTypeVersion")

    def _hwmap(self):
        """{object key: hardware identifier} from AddressManagerV2Data.AddressMap of all stations (see address_map)."""
        if self._hw is None:
            self._hw = {}
            for k in self.p.of_type("AddressManagerV2Data"):
                regs = self.p.targets(k, "RegisteredDeviceItems")
                for kind, idx, hwid in address_map(self.p.attr(k, "IPersistentExpandoData", "AddressMap")) or []:
                    if kind == 8:
                        self._hw.setdefault(self.m.rel1(k, "Parent"), hwid)
                    elif kind == 2 and idx < len(regs):
                        self._hw.setdefault(regs[idx], hwid)
        return self._hw

    def _hwid(self, k):
        """Hardware identifier of a station or device item; `constant` = linked system constant (Local~<item>)."""
        v = self._hwmap().get(k)
        consts = self.p.targets(k, "ConstantTags")
        if v is None:  # not in an AddressMap (e.g. IO controller: its constant carries the IO system identifier)
            v = next((int(d) for d in (self.p.attr(c, "IDefaultStrategyData", "DefaultValue") for c in consts)
                      if d is not None and str(d).isdigit()), None)
        const = next((self.p.name(c) for c in consts), None)
        return {"value": v, "constant": const} if v is not None else None

    def _system_hwid(self, master, name):
        """Hardware identifier of an IO/DP master system: the system constant Local~<name> (blanks as '_') that the
        master interface/controller links as ConstantTags (samples 213, 222)."""
        for c in self.p.targets(master, "ConstantTags") if master else []:
            v = self.p.attr(c, "IDefaultStrategyData", "DefaultValue")
            if (self.p.name(c) or "").endswith("~" + (name or "").replace(" ", "_")) and str(v).isdigit():
                return {"value": int(v), "constant": self.p.name(c)}
        return None

    def _gsd(self, cat):
        """gsd_type plus, for interface and port submodules, the Openness TypeIdentifier from the GSDML."""
        g = gsd_type(cat)
        if g and g["kind"] in ("Interface", "Port") and g["id"] and "_" in g["id"]:
            if self._gsdml is None:
                self._gsdml = gsdml_submodules(self.p.path)
            dap, _, sub = g["id"].rpartition("_")
            sid = self._gsdml.get((g["file"].lower(), dap), {}).get(int(sub)) if sub.isdigit() else None
            if sid:
                g = dict(g, gsdmlId=sid, typeIdentifier="GSD:%s/SM/%s" % (g["file"].upper(), sid))
        return g

    def _is_item(self, k):
        return "IDeviceItemData" in self._attrs(k)

    def _path(self, k):
        """Station/.../item names, for references from subnets and connections."""
        parts = []
        while k in self.p.objects:
            parts.append(self.p.name(k) or "")
            if self.p.is_a(k, "DeviceData"):
                break
            k = self.m.rel1(k, "Parent") or self.m.rel1(k, "DeviceItem")
        return "/".join(reversed(parts))

    # --- device items -----------------------------------------------------------------------------
    def node(self, n):
        a = self._attrs(n)
        nd = a.get("INodeData", {})
        par = self._params(n)
        sub = self.m.rel1(n, "Subnet")
        # NodeIPConfiguration = Openness IpProtocolSelection; only Project (0) has a configured address (sample 063)
        sel = par.get("NodeIPConfiguration") or 0
        proj = sel == 0
        res = {"name": self.p.name(n), "nodeType": nd.get("NodeType"), "nodeId": nd.get("NodeID"),
               "subtype": a.get("ICoreAttributes", {}).get("Subtype"), "catalog": self._catalog(n)[0],
               "subnet": self.p.name(sub) if sub else None,
               "ipAddress": ip4(par.get("NodeIPAddress")) if proj else None, "subnetMask": ip4(par.get("NodeIPSubnetMask")) if proj else None,
               "router": ip4(par.get("NodeIPDefaultRouterAddress")) if proj and par.get("NodeIPDefaultRouterAddressUsed") else None,
               "macAddress": mac(par.get("NodeMacAddress")), "profinetDeviceName": par.get("PnNameOfStation"),
               "profibusAddress": par.get("NodePBMpiAddress"),
               "parameters": {k: v for k, v in par.items() if k not in IP_PARAMS}, "key": n}
        if not proj:
            res["ipProtocolSelection"] = IP_PROTOCOL_SELECTIONS[sel] if 0 <= sel < 6 else str(sel)
        return res

    def item(self, k):
        a = self._attrs(k)
        core, di = a.get("ICoreAttributes", {}), a.get("IDeviceItemData", {})
        cat, catver = self._catalog(k)
        children = [c for c in self.m.sources(k, "Parent") if self._is_item(c)]
        children.sort(key=lambda c: (self.p.attr(c, "IDeviceItemData", "PositionNumber") or 0, self.p.name(c) or ""))
        nodes = sorted(self.m.sources(k, "DeviceItem"), key=lambda n: self.p.name(n) or "")
        par = self._params(k)
        onames = {e: OPENNESS_NAMES[(core.get("Subtype"), e)] for e in par if (core.get("Subtype"), e) in OPENNESS_NAMES}
        addrs = [dict(address_data(v), name=n) for n, v in sorted(a.get("IAddressAttributes", {}).items())
                 if re.match(r"AM_\d+_AddrData_\d+$", n)]
        return {"name": self.p.name(k), "objectType": self.p.type_name(k), "subtype": core.get("Subtype"),
                "positionNumber": di.get("PositionNumber"), "orderNumber": di.get("OrderNumber") or None,
                "firmware": di.get("FwVersion") or None, "typeName": di.get("InvariantTypeName") or None,
                "deviceItemType": di.get("DeviceItemType"), "catalog": cat, "catalogVersion": catver, "gsd": self._gsd(cat),
                "hwIdentifier": self._hwid(k), "addresses": addrs,
                "partnerPorts": sorted(self._path(t) for t in self.p.targets(k, "PortToPorts")),
                "certificates": pki_certificates(self.p.attr(k, "IPersistentExpandoData", "PkiContainerCache"))
                if "PkiContainerCache" in par else None,
                "sendClockNs": par["PnIoSendClockFactor"] * SEND_CLOCK_BASE_NS
                if isinstance(par.get("PnIoSendClockFactor"), int) else None,
                "comment": self.m.comment(k), "parameters": par, "opennessNames": onames,
                "attributes": self._other_sets(k),
                "nodes": [self.node(n) for n in nodes if self.p.is_a(n, "NodeData")],
                "items": [self.item(c) for c in children], "key": k}

    def safety(self, d):
        """Safety administration of the F-CPUs of station d (samples 246-253): F-program settings
        (FailsafeProgramSettings, password only as set/not set), F-runtime groups (RuntimegroupInfo, times in
        microseconds) and F-I/O (FModuleInfo, linked to the device item). None for stations without F-CPU."""
        tgts = [t for t in self.m.sources(d, "Parent") if self.p.is_a(t, "S7ControllerTargetData")]
        tgts += [t for x in self.m.sources(d, "Parent") for t in self.m.sources(x, "Parent") if self.p.is_a(t, "S7ControllerTargetData")]
        res = None
        for t in tgts:
            for k in self.m.sources(t, "Target"):
                tn = self.p.type_name(k)
                if tn not in ("FailsafeProgramSettings", "RuntimegroupInfo", "FModuleInfo"):
                    continue
                res = res or {"programSettings": None, "runtimeGroups": [], "fModules": []}
                a = self._attrs(k)
                if tn == "FailsafeProgramSettings":
                    ps = dict(a.get("IFailsafeProgramSettings", {}))
                    pw = ps.pop("FProgramPasswordPlus", None)
                    ps.update(a.get("IFailsafeProgramSettingsExpando", {}))
                    ps["passwordSet"] = bool(pw)
                    res["programSettings"] = ps
                elif tn == "RuntimegroupInfo":
                    r = a.get("IRuntimegroupInfo", {})
                    res["runtimeGroups"].append({"name": self.p.name(k), "maxCycleTimeUs": r.get("MaxCycleTime"),
                                                 "warnCycleTimeUs": r.get("WarningCycletime"),
                                                 "infoDb": r.get("Control1IDBSymbol") or None,
                                                 "preProcessing": r.get("PreProcessorFCName") or None,
                                                 "postProcessing": r.get("PostProcessorFCName") or None, "key": k})
                else:
                    mi = a.get("IFModuleInfo", {})
                    di = self.m.rel1(k, "FModuleInfoToDeviceItemData")
                    res["fModules"].append({"deviceItem": self._path(di) if di else None, "fIoDb": mi.get("StoredNameStorage"),
                                            "fIoDbNumber": mi.get("IDBNr"), "monitoringTime": mi.get("MonitoringTime"),
                                            "parameterCrc": mi.get("ModuleCRC"), "key": k})
        if res:
            res["runtimeGroups"].sort(key=lambda x: x["name"] or "")
            res["fModules"].sort(key=lambda x: x["deviceItem"] or "")
        return res

    def device(self, d):
        """Hardware of one station: catalog entry, parameters and the device item tree."""
        cat, catver = self._catalog(d)
        top = [c for c in self.m.sources(d, "Parent") if self._is_item(c)]
        top.sort(key=lambda c: (self.p.attr(c, "IDeviceItemData", "PositionNumber") or 0, self.p.name(c) or ""))
        return {"isCentral": self.p.attr(d, "IDeviceData", "IsCentral"), "catalog": cat, "catalogVersion": catver,
                "gsd": gsd_type(cat),
                "hwIdentifier": self._hwid(d),
                "parameters": self._params(d), "items": [self.item(c) for c in top], "safety": self.safety(d)}

    # --- project level ----------------------------------------------------------------------------
    def network(self, devices):
        """Subnets, sync/MRP domains, IO systems, port interconnections and connections of the project's stations
        (keys in `devices`)."""
        def mine(k):
            while k in self.p.objects and not self.p.is_a(k, "DeviceData"):
                k = self.m.rel1(k, "Parent") or self.m.rel1(k, "DeviceItem") or self.m.rel1(k, "Target")
            return k in devices
        subnets = []
        for s in self.p.of_type("SubnetData"):
            nodes = [n for n in self.m.sources(s, "Subnet") if self.p.is_a(n, "NodeData") and mine(n)]
            if not nodes:
                continue
            subnets.append({"name": self.p.name(s), "subtype": self.p.attr(s, "ICoreAttributes", "Subtype"),
                            "catalog": self._catalog(s)[0], "parameters": self._params(s),
                            "nodes": sorted(self._path(n) for n in nodes), "key": s})
        domains = []
        for k in self.p.of_type("DomainData"):
            sub = self.m.rel1(k, "Subnet")
            domains.append({"name": self.p.name(k), "subtype": self.p.attr(k, "ICoreAttributes", "Subtype"),
                            "catalog": self._catalog(k)[0], "subnet": self.p.name(sub) if sub else None,
                            "participants": sorted(self._path(i) for i in self.p.targets(k, "InterfaceSubmodules")),
                            "parameters": self._params(k),
                            "links": sorted({self.m.p.mm.relations[l.relation].name for l in self.p.links(k)
                                             if l.relation in self.p.mm.relations}), "key": k})
        conns = []
        for k in self.p.of_type("HmiConnectionPointData"):
            hc = self.m.rel1(k, "HmiConnection")
            nodes = self.p.targets(k, "Conn2Nodes")
            remote = [n for n in self.m.sources(k, "Node2RemoteConns") if n in self.p.objects]  # partner side
            if nodes and not any(mine(n) for n in nodes + remote):
                continue
            conns.append({"name": self.p.name(k), "kind": "HmiConnection",
                          "hmiConnection": self.p.name(hc) if hc else None,
                          "nodes": sorted(self._path(n) for n in nodes),
                          "partnerNodes": sorted(self._path(n) for n in remote), "parameters": self._params(k), "key": k})
        for k in self.p.of_type("BlockConnectionPointData"):  # configured connections (samples 218, 219)
            nodes = self.p.targets(k, "Conn2Nodes")
            tgt = self.m.rel1(k, "Parent") or self.m.rel1(k, "Target")
            if not (tgt and mine(tgt)):
                continue
            par = self._params(k)
            cid = par.get("ConnId")
            conns.append({"name": self.p.name(k), "kind": self.p.attr(k, "ICoreAttributes", "Subtype"),
                          "target": self.p.name(tgt), "nodes": sorted(self._path(n) for n in nodes), "partnerNodes": [],
                          # ConnId: long (stored 9 bytes before fixed-size values were cut, see attrs.FIXED_SIZES);
                          # TIA shows the ID in hex (W#16#100 = '100')
                          "connectionId": "%X" % cid if isinstance(cid, int) else
                          ("%X" % int.from_bytes(cid[:8], "little") if isinstance(cid, bytes) and len(cid) >= 8 else None),
                          "parameters": par, "key": k})
        iosys = []
        for k in self.p.of_type("MastersystemData"):  # PROFINET IO system / DP master system (samples 213, 216, 222, 225)
            dev = self.m.rel1(k, "Device") or self.m.rel1(k, "Target")
            if not (dev and mine(dev)):
                continue
            sub, master = self.m.rel1(k, "Subnet"), self.m.rel1(k, "Master")
            par = self._params(k)
            iosys.append({"name": self.p.name(k), "subtype": self.p.attr(k, "ICoreAttributes", "Subtype"),
                          "catalog": self._catalog(k)[0], "number": par.get("PositionNumber"),
                          "subnet": self.p.name(sub) if sub else None, "master": self._path(master) if master else None,
                          "devices": sorted(self._path(h) for h in self.p.targets(k, "HeadModules")),
                          "hwIdentifier": self._system_hwid(master, self.p.name(k)), "parameters": par, "key": k})
        links = set()
        for k in self.p.of_type("DeviceItemData"):  # port interconnections (sample 226), one entry per pair
            if mine(k):
                links.update(tuple(sorted((self._path(k), self._path(t)))) for t in self.p.targets(k, "PortToPorts"))
        for L in (subnets, domains, conns, iosys):
            L.sort(key=lambda x: x["name"] or "")
        return {"subnets": subnets, "syncDomains": domains, "ioSystems": iosys, "connections": conns,
                "portLinks": [list(x) for x in sorted(links)]}

    # --- project level: UMAC users and roles (samples 240-242) -------------------------------------
    def umac(self):
        """Project users and custom roles of the user management (UMAC). Names and session timeouts are decoded
        when stored unencrypted (umac_value); passwords are never output."""
        def name(k, s):
            return umac_text(self.p.attr(k, s, "NameEncrypted"))

        def timeout(k):
            v = umac_value(self.p.attr(k, "ICommonEditableRoleAttributes", "SessionTimeoutEncrypted")
                           or self.p.attr(k, "IProjectUserAttributes", "SessionTimeoutEncrypted"))
            return int.from_bytes(v, "little") if v and len(v) == 4 else None
        roles, users = [], []
        for root in self.p.of_type("UmacRootData"):
            for r in self.p.targets(root, "AllRoles"):
                roles.append({"name": name(r, "IEditableRoleAttributes") or self.p.attr(r, "ISystemRoleProxyAttributes", "ExternalId"),
                              "objectType": self.p.type_name(r),
                              "comment": umac_text(self.p.attr(r, "ICommonEditableRoleAttributes", "CommentEncrypted")) or None,
                              "sessionTimeout": timeout(r),
                              "devices": sorted(self.p.name(d) or "" for d in self.p.targets(r, "RoleToDevice")),
                              "deviceRights": sorted((self.p.name(x) or "").split(".")[-1]
                                                     for x in self.p.targets(r, "AssignedDeviceFunctionRight")),
                              "key": r})
            for u in self.p.targets(root, "AllUsers"):
                ur = []
                for a in self.p.targets(u, "UserRoles"):
                    ro = self.m.rel1(a, "AssociationToRole")
                    dev = self.m.rel1(a, "RoleAndUserAssociationToDevice")
                    ur.append({"role": name(ro, "IEditableRoleAttributes") if ro else None,
                               "device": self.p.name(dev) if dev else None})
                ua = self._attrs(u).get("IUserExtendedAttributes", {})
                users.append({"name": name(u, "IUserAttributes"), "objectType": self.p.type_name(u),
                              "alias": ua.get("Alias") or None, "sessionTimeout": timeout(u), "roles": ur, "key": u})
        for L in (users, roles):
            L.sort(key=lambda x: x["name"] or "")
        return {"users": users, "roles": roles}


def text_lines(hw, indent):
    """Text tree of one station's hardware (only the main fields)."""
    out = []
    pad = " " * indent

    def walk(it, depth):
        p = pad + "  " * depth
        head = "%s[%s] %s" % (p, it["positionNumber"], it["name"])
        extra = " ".join(x for x in (it["typeName"], it["orderNumber"], it["firmware"]) if x)
        out.append(head + ("  " + extra if extra else ""))
        for ad in it["addresses"]:
            if "ioType" in ad:
                out.append("%s  %s %d.%d, %d bit, process image %d" % (p, ad["ioType"], ad["startAddress"], ad["startBit"],
                                                                    ad["lengthBits"], ad["processImage"]))
        for pp in it["partnerPorts"]:
            out.append("%s  connected to port %s" % (p, pp))
        for n in it["nodes"]:
            out.append("%s  node %s %s %s %s%s%s" % (p, n["name"], n["ipAddress"] or "", n["subnetMask"] or "",
                                                    n["profinetDeviceName"] or "", "  subnet " + n["subnet"] if n["subnet"] else "",
                                                    "  ip " + n["ipProtocolSelection"] if "ipProtocolSelection" in n else ""))
        for c in it["items"]:
            walk(c, depth + 1)
    for it in hw["items"]:
        walk(it, 0)
    return out


def network_text(net, indent):
    pad = " " * indent
    out = []
    for s in net["subnets"]:
        out.append("%sSubnet %s (%s): %s" % (pad, s["name"], s["catalog"], ", ".join(s["nodes"])))
    for d in net["syncDomains"]:
        out.append("%sDomain %s (%s): %s" % (pad, d["name"], d["catalog"], ", ".join(d["participants"])))
    for a, b in net["portLinks"]:
        out.append("%sPort link %s <-> %s" % (pad, a, b))
    for c in net["connections"]:
        out.append("%sConnection %s (%s): %s -> %s" % (pad, c["name"], c["kind"], ", ".join(c["nodes"]),
                                                       ", ".join(c["partnerNodes"])))
    return out
