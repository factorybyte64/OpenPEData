"""Stress blocks for the code parser (C4): mixed network languages, blocks with several hundred networks and large interfaces
(Input/Output/InOut/Static/Temp/Constant with structs, arrays, strings, start values and comments). Writes
tools/sources/stress/: SIMATIC SD sources (.s7dcl + .s7res) for TiaPlcSoftware(documentWrite) and an STL/SCL source for
TiaPlcSoftware(sourceImport). Usage: python tools/gen_stress.py"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "sources", "stress")

NB, NI, NR, ND = 40, 40, 20, 20      # Bool / Int / Real / DInt per Input and Output section
NIO, NST, NSTB, NT, NTR, NC = 60, 200, 100, 100, 50, 30
NIO_FC = 2  # 123 Input/Output + 5 InOut fails in an FC (TIA: "Too many passed parameters at the block call"), 123 + 2 compiles


def iface(sd, static=True, mlc=None):
    """Interface declaration. sd: SIMATIC SD syntax, else STL/SCL source syntax. mlc: dict collecting comment texts (SD)."""
    def com(name, text):
        if mlc is None:
            return [], " // " + text
        mlc["MLC_" + name] = text
        return ['        { S7_MLC := "MLC_%s" }' % name], ""

    ind = "        " if sd else "      "
    o = []

    def sec(kw, rows):
        o.append(("    " if sd else "   ") + kw)
        for name, typ, init, text in rows:
            pre, post = com(name, text) if text else ([], "")
            o.extend(pre)
            o.append("%s%s : %s%s;%s" % (ind, name, typ, " := " + init if init is not None else "", post if not sd else ""))
        o.append(("    " if sd else "   ") + "END_VAR")
        if not sd:
            o.append("")

    st = "Struct" if sd else "Struct"
    est = "END_STRUCT" if sd else "END_STRUCT"

    def struct(name, inner):
        return [(name, st + "\n" + "\n".join("%s   %s : %s;" % (ind, n, t) for n, t in inner) + "\n" + ind + est, None, None)]

    nb, ni, nr, nd = (NB, NI, NR, ND) if static else (NB // 2, NI // 2, NR // 2, ND // 2)  # FC: about half
    rows_in = [("inB%d" % i, "Bool", None, "input bit %d" % i if i % 10 == 0 else None) for i in range(nb)]
    rows_in += [("inI%d" % i, "Int", None, None) for i in range(ni)]
    rows_in += [("inR%d" % i, "Real", None, None) for i in range(nr)]
    rows_in += [("inD%d" % i, "DInt", None, None) for i in range(nd)]
    rows_in += [("inArr", "Array[0..99] of Int", None, "input array"), ("inStr", "String[40]" if static else "String", None, None)]
    rows_in += struct("inSt", [("x", "Int"), ("y", "Real"), ("z", "Bool")])
    sec("VAR_INPUT", rows_in)
    rows_out = [("outB%d" % i, "Bool", None, None) for i in range(nb)]
    rows_out += [("outI%d" % i, "Int", None, "output int %d" % i if i % 10 == 0 else None) for i in range(ni)]
    rows_out += [("outR%d" % i, "Real", None, None) for i in range(nr)]
    rows_out += [("outD%d" % i, "DInt", None, None) for i in range(nd)]
    sec("VAR_OUTPUT", rows_out)
    sec("VAR_IN_OUT", [("ioI%d" % i, "Int", None, None) for i in range(NIO if static else NIO_FC)])
    if static:
        rows = [("stI%d" % i, "Int", str(i) if i % 7 == 0 else None, "static %d" % i if i % 25 == 0 else None) for i in range(NST)]
        rows += [("stB%d" % i, "Bool", "TRUE" if i % 3 == 0 else None, None) for i in range(NSTB)]
        rows += [("stArr", "Array[1..10, 0..4] of Real", None, None)]
        rows += struct("stSt", [("a", "Int"), ("b", "Array[0..3] of Bool"), ("c", "Time")])
        sec("VAR", rows)
    rows = [("tI%d" % i, "Int", None, None) for i in range(NT)] + [("tR%d" % i, "Real", None, None) for i in range(NTR)]
    sec("VAR_TEMP", rows)
    sec("VAR CONSTANT", [("cI%d" % i, "Int", str(i * 3), None) for i in range(NC)])
    return o


def lad_net(k, fbd, nio=NIO, half=False):
    """Body of a LAD/FBD network in SD, varied by k. half: interface of the FC (half the Input/Output members)."""
    b, i = k % (NB // 2 if half else NB), k % (NI // 2 if half else NI)
    v = k % 4
    if v == 0:
        return (["RUNG #inB%d" % b] if fbd else ["RUNG wire#powerrail", "    Contact( #inB%d )" % b]) + ["    Coil( #outB%d )" % b, "END_RUNG"]
    if v == 1:
        return ["RUNG" if fbd else "RUNG wire#powerrail", "    Move( in := #inI%d, out1 => #outI%d )" % (i, i), "END_RUNG"]
    if v == 2:
        return ["RUNG" if fbd else "RUNG wire#powerrail", "    Move( in := #ioI%d, out1 => #tI%d )" % (k % nio, k % NT), "END_RUNG"]
    return ["RUNG" if fbd else "RUNG wire#powerrail", "    Move( in := #cI%d, out1 => #outI%d )" % (k % NC, i), "END_RUNG"]


def scl_net(k):
    return ["#tI%d := #inI%d + #cI%d;" % (k % NT, k % NI, k % NC), "IF #inB%d THEN" % (k % NB),
            "    #outR%d := #inR%d * 2.0;" % (k % NR, k % NR), "END_IF;"]


def sd_block(kind, name, lang, nets, static, res):
    """nets: [(language, body lines, title, comment)]."""
    o = ["{", '    S7_Optimized := "TRUE";', '    S7_PreferredLanguage := "%s"' % lang, "}", '%s "%s"%s' % (kind, name, " : Void" if kind == "FUNCTION" else "")]
    o += iface(True, static, res)
    o.append("")
    for n, (lg, body, title, comment) in enumerate(nets, 1):
        attrs = ['        S7_Language := "%s"' % lg]
        if title:
            res["MLC_%s_T%d" % (name, n)] = title
            attrs.append('        S7_NetworkTitle := "MLC_%s_T%d"' % (name, n))
        if comment:
            res["MLC_%s_C%d" % (name, n)] = comment
            attrs.append('        S7_NetworkComment := "MLC_%s_C%d"' % (name, n))
        o += ["    {", ";\n".join(attrs), "    }", "    NETWORK"] + ["        " + x for x in body] + ["    END_NETWORK"]
    o.append("END_" + kind)
    return "\n".join(o) + "\n"


def write(name, text):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def s7res(res):
    o = ["MultiLingualTexts:"]
    for k, v in res.items():
        o += ["  - id: %s" % k, "    en-US: %s" % v]
    return "\n".join(o) + "\n"


def main():
    os.makedirs(OUT, exist_ok=True)
    # 1. LAD FB with 300 networks and the large interface
    res = {}
    nets = [("LAD", lad_net(k, False), "LAD network %d" % (k + 1) if k % 3 == 0 else None,
             "comment of network %d" % (k + 1) if k % 5 == 0 else None) for k in range(300)]
    write("FB_BigLad.s7dcl", sd_block("FUNCTION_BLOCK", "FB_BigLad", "LAD", nets, True, res))
    write("FB_BigLad.s7res", s7res(res))
    # 2. FBD FC with 300 networks (no Static)
    res = {}
    nets = [("FBD", lad_net(k, True, NIO_FC, True), "FBD network %d" % (k + 1) if k % 2 == 0 else None, None) for k in range(300)]
    write("FC_BigFbd.s7dcl", sd_block("FUNCTION", "FC_BigFbd", "FBD", nets, False, res))
    write("FC_BigFbd.s7res", s7res(res))
    # 3. LAD FB with mixed networks LAD / FBD / SCL (120 networks)
    res = {}
    nets = []
    for k in range(120):
        lg = ("LAD", "FBD", "SCL")[k % 3]
        body = scl_net(k) if lg == "SCL" else lad_net(k, lg == "FBD")
        nets.append((lg, body, "%s network %d" % (lg, k + 1), "mixed %d" % (k + 1) if k % 4 == 0 else None))
    write("FB_Mixed.s7dcl", sd_block("FUNCTION_BLOCK", "FB_Mixed", "LAD", nets, True, res))
    write("FB_Mixed.s7res", s7res(res))
    # 4. STL FB with 600 networks (AWL source)
    o = ['FUNCTION_BLOCK "FB_BigStl"', "TITLE = STL stress block", "{ S7_Optimized_Access := 'TRUE' }", "VERSION : 0.1"]
    o += iface(False, True)
    o += ["", "BEGIN"]
    for k in range(600):
        o += ["NETWORK", "TITLE = STL network %d" % (k + 1)]
        if k % 10 == 0:
            o.append("//comment of STL network %d" % (k + 1))
        v = k % 3
        if v == 0:
            o += ["      A     #inB%d;" % (k % NB), "      AN    #stB%d;" % (k % NSTB), "      =     #outB%d;" % (k % NB)]
        elif v == 1:
            o += ["      L     #inI%d;" % (k % NI), "      L     #stI%d;" % (k % NST), "      +I    ;", "      T     #outI%d;" % (k % NI)]
        else:
            o += ["      L     #inR%d;" % (k % NR), "      L     #tR%d;" % (k % NTR), "      *R    ;", "      T     #outR%d;   // product" % (k % NR)]
    o += ["END_FUNCTION_BLOCK", ""]
    write("FB_BigStl.awl", "\n".join(o))
    # 5. SCL FB with one long network (about 3000 lines)
    o = ['FUNCTION_BLOCK "FB_BigScl"', "{ S7_Optimized_Access := 'TRUE' }", "VERSION : 0.1"]
    o += iface(False, True)
    o += ["BEGIN"]
    for k in range(600):
        if k % 50 == 0:
            o.append("    REGION part %d" % (k // 50))
        o += ["    // step %d" % k] + ["    " + x for x in scl_net(k)]
        if k % 50 == 49:
            o.append("    END_REGION")
    o += ["END_FUNCTION_BLOCK", ""]
    write("FB_BigScl.scl", "\n".join(o))
    print("written to", OUT)


if __name__ == "__main__":
    main()
