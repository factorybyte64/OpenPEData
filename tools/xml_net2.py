"""Helper: SimaticML block with two networks built from a template export (used for label/jump probes)."""
import re
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_xml  # noqa: E402

NS = gen_xml.NS


def two_net(tpl, net1, net2, name, lang):
    x = gen_xml.build_block(tpl, name, lang, net1)
    cu = re.search(r'<SW\.Blocks\.CompileUnit ID="3".*?</SW\.Blocks\.CompileUnit>', x, flags=re.S).group(0)
    cu2 = re.sub(r"<NetworkSource>.*?</NetworkSource>", lambda k: "<NetworkSource>" + net2 + "</NetworkSource>", cu, flags=re.S)
    for old, new in (('ID="3"', 'ID="20"'), ('ID="4"', 'ID="21"'), ('ID="5"', 'ID="22"'), ('ID="6"', 'ID="23"'), ('ID="7"', 'ID="24"')):
        cu2 = cu2.replace(old, new)
    return x.replace(cu, cu + cu2)


def simple_net(first, second, parts, lang="LAD"):
    pass
