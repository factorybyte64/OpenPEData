"""L0 b): packages -> namespaces of the metamodel, raw-XML keyword counts. Usage: python mm_packages.py <sample>..."""
import os, re, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "parser"))
import xml.etree.ElementTree as ET
from openpedata.project import Project
from openpedata.metamodel import NS
sys.stdout.reconfigure(encoding="utf-8")
for s in sys.argv[1:]:
    mm = Project(s, verify=False).mm
    root = ET.fromstring(mm.xml)
    print("##", os.path.basename(s))
    for pkg in root.iter(NS + "Package"):
        nss = sorted({ns.get("name") for ns in pkg.iter(NS + "Namespace")})
        print("- %s id=%s version=%s: %s" % (pkg.get("name"), pkg.get("id"), pkg.get("version"), ", ".join(nss)))
    txt = mm.xml.decode("utf-8", "replace")
    for kw in ("Faceplate", "Unified", "Hmi", "Screen", "MasterCopy", "Library", "Clipboard", "TypeVersion", "InTest", "Released"):
        print("  raw '%s': %d" % (kw, len(re.findall(kw, txt))))
