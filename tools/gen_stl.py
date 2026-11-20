"""STL (AWL) sources for the catalog coverage (C3): one network per STL mnemonic page of docs/catalog/stl.txt, the network title
is "<catalog no> <mnemonic>". Writes tools/sources/stl_all/*.awl|.db for TiaPlcSoftware(sourceImport) on an S7-1500.
Usage: python tools/gen_stl.py"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "sources", "stl_all")

# (group suffix, mnemonic) -> (block, statements). Group suffix = last part of the catalog group path.
I2 = ["L     #i1;", "L     #i2;"]
D2 = ["L     #d1;", "L     #d2;"]
R2 = ["L     #r1;", "L     #r2;"]
CODE = {
    # bit logic (FB_StlBit)
    ("Bit logic operations", "A"): ("Bit", ["A     #a;", "=     #q;"]),
    ("Bit logic operations", "AN"): ("Bit", ["AN    #a;", "=     #q;"]),
    ("Bit logic operations", "O"): ("Bit", ["O     #a;", "O     #b;", "=     #q;"]),
    ("Bit logic operations", "ON"): ("Bit", ["ON    #a;", "=     #q;"]),
    ("Bit logic operations", "X"): ("Bit", ["X     #a;", "X     #b;", "=     #q;"]),
    ("Bit logic operations", "XN"): ("Bit", ["XN    #a;", "=     #q;"]),
    ("Bit logic operations", "O#2"): ("Bit", ["A     #a;", "A     #b;", "O     ;", "A     #c;", "=     #q;"]),
    ("Bit logic operations", "A("): ("Bit", ["A(    ;", "O     #a;", "O     #b;", ")     ;", "=     #q;"]),
    ("Bit logic operations", "AN("): ("Bit", ["AN(   ;", "O     #a;", "O     #b;", ")     ;", "=     #q;"]),
    ("Bit logic operations", "O("): ("Bit", ["O(    ;", "A     #a;", "A     #b;", ")     ;", "=     #q;"]),
    ("Bit logic operations", "ON("): ("Bit", ["ON(   ;", "A     #a;", ")     ;", "=     #q;"]),
    ("Bit logic operations", "X("): ("Bit", ["X(    ;", "A     #a;", ")     ;", "X     #b;", "=     #q;"]),
    ("Bit logic operations", "XN("): ("Bit", ["XN(   ;", "A     #a;", ")     ;", "=     #q;"]),
    ("Bit logic operations", ")"): ("Bit", ["A(    ;", "A     #a;", ")     ;", "=     #q;"]),
    ("Bit logic operations", "="): ("Bit", ["A     #a;", "=     #q;"]),
    ("Bit logic operations", "R"): ("Bit", ["A     #a;", "R     #q;"]),
    ("Bit logic operations", "S"): ("Bit", ["A     #a;", "S     #q;"]),
    ("Bit logic operations", "NOT"): ("Bit", ["A     #a;", "NOT   ;", "=     #q;"]),
    ("Bit logic operations", "SET"): ("Bit", ["SET   ;", "=     #q;"]),
    ("Bit logic operations", "CLR"): ("Bit", ["CLR   ;", "=     #q;"]),
    ("Bit logic operations", "SAVE"): ("Bit", ["A     #a;", "SAVE  ;"]),
    ("Bit logic operations", "FN"): ("Bit", ["A     #a;", "FN    #edgeN;", "=     #q;"]),
    ("Bit logic operations", "FP"): ("Bit", ["A     #a;", "FP    #edgeP;", "=     #q;"]),
    # S5 timers and counters (FB_StlTimer)
    ("Timer operations", "FR"): ("Timer", ["A     #a;", "FR    %T1;"]),
    ("Timer operations", "L"): ("Timer", ["L     %T1;", "T     #wr;"]),
    ("Timer operations", "LC"): ("Timer", ["LC    %T1;", "T     #wr;"]),
    ("Timer operations", "R"): ("Timer", ["A     #a;", "R     %T1;"]),
    ("Timer operations", "SP"): ("Timer", ["A     #a;", "L     S5T#1S;", "SP    %T1;"]),
    ("Timer operations", "SE"): ("Timer", ["A     #a;", "L     S5T#2S;", "SE    %T2;"]),
    ("Timer operations", "SD"): ("Timer", ["A     #a;", "L     S5T#3S;", "SD    %T3;"]),
    ("Timer operations", "SS"): ("Timer", ["A     #a;", "L     S5T#4S;", "SS    %T4;"]),
    ("Timer operations", "SF"): ("Timer", ["A     #a;", "L     S5T#5S;", "SF    %T5;"]),
    ("Counter operations", "FR"): ("Timer", ["A     #a;", "FR    %C1;"]),
    ("Counter operations", "L"): ("Timer", ["L     %C1;", "T     #wr;"]),
    ("Counter operations", "LC"): ("Timer", ["LC    %C1;", "T     #wr;"]),
    ("Counter operations", "R"): ("Timer", ["A     #a;", "R     %C1;"]),
    ("Counter operations", "S"): ("Timer", ["A     #a;", "L     C#5;", "S     %C1;"]),
    ("Counter operations", "CU"): ("Timer", ["A     #a;", "CU    %C1;"]),
    ("Counter operations", "CD"): ("Timer", ["A     #a;", "CD    %C1;"]),
    # comparison, math, conversion, word logic, shift, accumulator (FB_StlMath)
    ("Comparator operations", "? I"): ("Math", [*I2, "==I   ;", "=     #q;", *I2, "<>I   ;", "=     #q;", *I2, ">I    ;", "=     #q;",
                                                *I2, "<I    ;", "=     #q;", *I2, ">=I   ;", "=     #q;", *I2, "<=I   ;", "=     #q;"]),
    ("Comparator operations", "? D"): ("Math", [*D2, "==D   ;", "=     #q;", *D2, "<>D   ;", "=     #q;", *D2, ">D    ;", "=     #q;",
                                                *D2, "<D    ;", "=     #q;", *D2, ">=D   ;", "=     #q;", *D2, "<=D   ;", "=     #q;"]),
    ("Comparator operations", "? R"): ("Math", [*R2, "==R   ;", "=     #q;", *R2, "<>R   ;", "=     #q;", *R2, ">R    ;", "=     #q;",
                                                *R2, "<R    ;", "=     #q;", *R2, ">=R   ;", "=     #q;", *R2, "<=R   ;", "=     #q;"]),
}
for op in ("+I", "-I", "*I", "/I"):
    CODE[("Integers", op)] = ("Math", [*I2, op + ";", "T     #ir;"])
for op in ("+D", "-D", "*D", "/D", "MOD"):
    CODE[("Integers", op)] = ("Math", [*D2, op + ";", "T     #dr;"])
CODE[("Integers", "+")] = ("Math", ["L     #i1;", "+     5;", "T     #ir;"])
CODE[("Integers", "INC")] = ("Math", ["L     #by1;", "INC   1;", "T     #by1;"])
CODE[("Integers", "DEC")] = ("Math", ["L     #by1;", "DEC   1;", "T     #by1;"])
for op in ("+R", "-R", "*R", "/R"):
    CODE[("Floating-point numbers", op)] = ("Math", [*R2, op + ";", "T     #rr;"])
CODE[("Floating-point numbers", "ABS")] = ("Math", ["L     #r1;", "ABS   ;", "T     #rr;"])
for op in ("SQR", "SQRT", "EXP", "LN", "SIN", "ASIN", "COS", "ACOS", "TAN", "ATAN"):
    CODE[("Advanced floating-point numbers", op)] = ("Math", ["L     #r1;", op + ";", "T     #rr;"])
for op, src, dst in (("BTI", "#w1", "#ir"), ("ITB", "#i1", "#wr"), ("BTD", "#dw1", "#dr"), ("ITD", "#i1", "#dr"), ("DTB", "#d1", "#dwr"),
                     ("DTR", "#d1", "#rr"), ("INVI", "#i1", "#ir"), ("INVD", "#d1", "#dr"), ("NEGI", "#i1", "#ir"), ("NEGD", "#d1", "#dr"),
                     ("NEGR", "#r1", "#rr"), ("CAW", "#w1", "#wr"), ("CAD", "#dw1", "#dwr"), ("RND", "#r1", "#dr"), ("TRUNC", "#r1", "#dr"),
                     ("RND+", "#r1", "#dr"), ("RND-", "#r1", "#dr")):
    CODE[("Conversion operations", op)] = ("Math", ["L     %s;" % src, op + ";", "T     %s;" % dst])
for op in ("AW", "OW", "XOW"):
    CODE[("Word logic operations", op)] = ("Math", ["L     #w1;", "L     #w2;", op + ";", "T     #wr;",
                                                    "L     #w1;", op + "   W#16#00FF;", "T     #wr;"])
for op in ("AD", "OD", "XOD"):
    CODE[("Word logic operations", op)] = ("Math", ["L     #dw1;", "L     #dw2;", op + ";", "T     #dwr;",
                                                    "L     #dw1;", op + "   DW#16#0000_FFFF;", "T     #dwr;"])
for op, src, dst in (("SSI", "#i1", "#ir"), ("SSD", "#d1", "#dr"), ("SLW", "#w1", "#wr"), ("SRW", "#w1", "#wr"),
                     ("SLD", "#dw1", "#dwr"), ("SRD", "#dw1", "#dwr")):
    CODE[("Shift", op)] = ("Math", ["L     %s;" % src, op + "   2;", "T     %s;" % dst, "L     2;", "L     %s;" % src, op + ";", "T     %s;" % dst])
for op in ("RLD", "RRD"):
    CODE[("Rotate", op)] = ("Math", ["L     #dw1;", op + "   3;", "T     #dwr;"])
for op in ("RLDA", "RRDA"):
    CODE[("Rotate", op)] = ("Math", ["L     #dw1;", op + ";", "T     #dwr;"])
CODE[("Accumulator", "TAK")] = ("Math", [*I2, "TAK   ;", "T     #ir;"])
CODE[("Accumulator", "PUSH")] = ("Math", ["L     #i1;", "PUSH  ;", "T     #ir;"])
CODE[("Accumulator", "POP")] = ("Math", [*I2, "POP   ;", "T     #ir;"])
CODE[("Null instructions", "BLD")] = ("Math", ["BLD   100;"])
CODE[("Null instructions", "NOP 0")] = ("Math", ["NOP   0;"])
CODE[("Null instructions", "NOP 1")] = ("Math", ["NOP   1;"])
# load / transfer, address registers (FB_StlLoad, not optimized)
CODE[("Load", "L")] = ("Load", ["L     #i1;", "T     #ir;", "L     %MW10;", "T     #ir;", "L     P#M10.0;", "T     #dwr;", "L     B#16#12;", "T     #by1;",
                                "L     'AB';", "T     #wr;", "L     2.5;", "T     #rr;", "L     T#1S;", "T     #dr;"])
CODE[("Load", "L STW")] = ("Load", ["L     STW;", "T     #wr;"])
CODE[("Load", "LAR1")] = ("Load", ["L     P#M10.0;", "LAR1  ;"])
CODE[("Load", "LAR1 <D>")] = ("Load", ["LAR1  P#M20.0;", "LAR1  #dw1;"])
CODE[("Load", "LAR1 AR2")] = ("Load", ["LAR1  AR2;"])
CODE[("Load", "LAR2")] = ("Load", ["L     P#M10.0;", "LAR2  ;"])
CODE[("Load", "LAR2 <D>")] = ("Load", ["LAR2  P#M30.0;", "LAR2  #dw1;"])
CODE[("Transfer", "T")] = ("Load", ["L     #i1;", "T     #ir;", "T     %MW12;", "L     2;", "T     W [AR1, P#0.0];", "T     %MW [#dw1];"])
CODE[("Transfer", "T STW")] = ("Load", ["L     #w1;", "T     STW;"])
CODE[("Transfer", "CAR")] = ("Load", ["CAR   ;"])
CODE[("Transfer", "TAR1")] = ("Load", ["TAR1  ;", "T     #dwr;"])
CODE[("Transfer", "TAR1 <D>")] = ("Load", ["TAR1  #dwr;"])
CODE[("Transfer", "TAR1 AR2")] = ("Load", ["TAR1  AR2;"])
CODE[("Transfer", "TAR2")] = ("Load", ["TAR2  ;", "T     #dwr;"])
CODE[("Transfer", "TAR2 <D>")] = ("Load", ["TAR2  #dwr;"])
CODE[("Address register", "+AR1")] = ("Load", ["L     +4;", "+AR1  ;", "+AR1  P#2.0;"])
CODE[("Address register", "+AR2")] = ("Load", ["L     +4;", "+AR2  ;", "+AR2  P#2.0;"])
# jumps (FB_StlJump)
CODE[("Jumps", "Jump label")] = ("Jump", ["JU    LB01;", "L     1;", "T     #ir;", "LB01: NOP   0;"])
for n, op in enumerate(("JU", "JC", "JCN", "JCB", "JNB", "JBI", "JNBI", "JO", "JOS", "JZ", "JN", "JP", "JM", "JPZ", "JMZ", "JUO"), 1):
    lab = "J%03d" % n
    pre = ["A     #a;"] if op in ("JC", "JCN", "JCB", "JNB") else ["L     #i1;", "L     #i2;", "-I    ;", "T     #ir;"] \
        if op in ("JO", "JOS", "JZ", "JN", "JP", "JM", "JPZ", "JMZ") else ["L     #r1;", "L     #r2;", "/R    ;", "T     #rr;"] if op == "JUO" else \
        ["A     #a;", "SAVE  ;"] if op in ("JBI", "JNBI") else []
    CODE[("Jumps", op)] = ("Jump", pre + ["%s   %s;" % (op.ljust(3), lab), "L     1;", "T     #ir;", "%s: NOP   0;" % lab])
CODE[("Jumps", "JL")] = ("Jump", ["L     #i1;", "JL    LST1;", "JU    SG0;", "JU    SG1;", "LST1: JU    SEND;", "SG0:  L     10;", "T     #ir;",
                                  "JU    SEND;", "SG1:  L     11;", "T     #ir;", "SEND: NOP   0;"])
CODE[("Jumps", "LOOP")] = ("Jump", ["L     5;", "NXT1: T     #ir;", "L     #ir;", "LOOP  NXT1;"])
# data blocks (FB_StlDb, not optimized)
CODE[("Data blocks", "OPN")] = ("Db", ["OPN   \"DB_StlStd\";", "L     %DBW0;", "T     #wr;", "OPN   %DB100;"])
CODE[("Data blocks", "OPNDI")] = ("Db", ["OPNDI \"DB_StlStd\";", "L     %DIW0;", "T     #wr;"])
CODE[("Data blocks", "CDB")] = ("Db", ["CDB   ;"])
CODE[("Data blocks", "L DBLG")] = ("Db", ["L     DBLG;", "T     #dr;"])
CODE[("Data blocks", "L DBNO")] = ("Db", ["L     DBNO;", "T     #dr;"])
CODE[("Data blocks", "L DILG")] = ("Db", ["L     DILG;", "T     #dr;"])
CODE[("Data blocks", "L DINO")] = ("Db", ["L     DINO;", "T     #dr;"])
# code blocks (FB_StlCall)
CODE[("Code blocks", "BE")] = ("Be", ["L     1;", "T     #ir;", "BE    ;"])
CODE[("Code blocks", "BEC")] = ("Call", ["A     #a;", "BEC   ;"])
CODE[("Code blocks", "BEU")] = ("Call", ["A     #b;", "JCN   NBEU;", "BEU   ;", "NBEU: NOP   0;"])
CODE[("Code blocks", "CALL")] = ("Call", ['CALL  "FC_StlPar"', "(  x := #i1,", "   y := 5,", "   q := #q,", "   r := #ir", ");",
                                          'CALL  #inner', "(  start := #a,", "   cnt := #ir", ");",
                                          'CALL  "FB_StlInner", "DB_StlInner"', "(  start := #b,", "   cnt := #ir", ");"])
CODE[("Code blocks", "CC")] = ("Call", ["A     #a;", 'CC    "FC_StlNoPar";'])
CODE[("Code blocks", "UC")] = ("Call", ['UC    "FC_StlNoPar";'])

VARS = {
    "a": "Bool", "b": "Bool", "c": "Bool", "q": "Bool", "by1": "Byte", "i1": "Int", "i2": "Int", "ir": "Int", "d1": "DInt", "d2": "DInt",
    "dr": "DInt", "r1": "Real", "r2": "Real", "rr": "Real", "w1": "Word", "w2": "Word", "wr": "Word", "dw1": "DWord", "dw2": "DWord",
    "dwr": "DWord",
}
OUTPUTS = {"q", "by1", "ir", "dr", "rr", "wr", "dwr"}
BLOCKS = {  # block suffix -> (optimized, extra static)
    "Bit": (True, {"edgeN": "Bool", "edgeP": "Bool"}), "Timer": (True, {}), "Math": (True, {}), "Load": (False, {}),
    "Jump": (True, {}), "Db": (False, {}), "Call": (True, {"inner": '"FB_StlInner"'}), "Be": (True, {}),
}


def catalog():
    """[(no, group suffix, mnemonic)] of the STL mnemonic pages, numbered from 1 in catalog order; a repeated (group, mnemonic)
    gets #2."""
    out, seen = [], set()
    for line in open(os.path.join(ROOT, "docs", "catalog", "stl.txt"), encoding="utf-8"):
        group, title = [x.strip() for x in line.split("|", 1)]
        if "STL Mnemonic" not in group:
            continue
        mn = title.split(":")[0].strip() if ":" in title else title.split(" (S7")[0].strip()
        key = (group.split(" / ")[-1], mn)
        if key in seen:
            key = (key[0], mn + "#2")
        seen.add(key)
        out.append((len(out) + 1, key[0], key[1]))
    return out


def fb(name, optimized, static, nets):
    o = ['FUNCTION_BLOCK "%s"' % name, "TITLE = STL catalog %s" % name, "{ S7_Optimized_Access := '%s' }" % ("TRUE" if optimized else "FALSE"),
         "VERSION : 0.1", "   VAR_INPUT"]
    o += ["      %s : %s;" % (n, t) for n, t in VARS.items() if n not in OUTPUTS]
    o += ["   END_VAR", "", "   VAR_OUTPUT"]
    o += ["      %s : %s;" % (n, t) for n, t in VARS.items() if n in OUTPUTS]
    o += ["   END_VAR", "", "   VAR"]
    o += ["      %s : %s;" % (n, t) for n, t in static.items()] or ["      dummy : Bool;"]
    o += ["   END_VAR", "", "", "BEGIN"]
    for title, code in nets:
        o += ["NETWORK", "TITLE = " + title] + ["      " + c if not c[:5].rstrip().endswith(":") else c for c in code]
    o += ["END_FUNCTION_BLOCK", ""]
    return "\n".join(o)


AUX = {
    "DB_StlStd.db": 'DATA_BLOCK "DB_StlStd"\n{ S7_Optimized_Access := \'FALSE\' }\nVERSION : 0.1\nNON_RETAIN\n   STRUCT\n      w0 : Word;\n'
                    "      w1 : Word;\n   END_STRUCT;\n\n\nBEGIN\n\nEND_DATA_BLOCK\n",
    "FC_StlNoPar.awl": 'FUNCTION "FC_StlNoPar" : Void\n{ S7_Optimized_Access := \'FALSE\' }\nVERSION : 0.1\n\nBEGIN\nNETWORK\nTITLE = \n'
                       "      NOP   0;\nEND_FUNCTION\n",
    "FC_StlPar.awl": 'FUNCTION "FC_StlPar" : Void\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\n   VAR_INPUT\n      x : Int;\n      y : Int;\n'
                     "   END_VAR\n\n   VAR_OUTPUT\n      q : Bool;\n      r : Int;\n   END_VAR\n\n\nBEGIN\nNETWORK\nTITLE = \n      L     #x;\n"
                     "      L     #y;\n      +I    ;\n      T     #r;\n      >I    ;\n      =     #q;\nEND_FUNCTION\n",
    "FB_StlInner.awl": 'FUNCTION_BLOCK "FB_StlInner"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\n   VAR_INPUT\n      start : Bool;\n'
                       "   END_VAR\n\n   VAR_OUTPUT\n      cnt : Int;\n   END_VAR\n\n   VAR\n      n : Int;\n   END_VAR\n\n\nBEGIN\nNETWORK\nTITLE = \n"
                       "      A     #start;\n      JCN   E1;\n      L     #n;\n      +     1;\n      T     #n;\nE1:   L     #n;\n      T     #cnt;\n"
                       "END_FUNCTION_BLOCK\n",
    "DB_StlInner.db": 'DATA_BLOCK "DB_StlInner"\n{ S7_Optimized_Access := \'TRUE\' }\nVERSION : 0.1\nNON_RETAIN\n"FB_StlInner"\n\nBEGIN\n\n'
                      "END_DATA_BLOCK\n",
}


def main():
    os.makedirs(OUT, exist_ok=True)
    nets = {b: [] for b in BLOCKS}
    missing = []
    for no, grp, mn in catalog():
        if (grp, mn) not in CODE:
            missing.append("%03d %s / %s" % (no, grp, mn))
            continue
        blk, code = CODE[(grp, mn)]
        nets[blk].append(("%03d %s" % (no, mn.replace("#2", " (2)")), code))
    for b, (opt, static) in BLOCKS.items():
        with open(os.path.join(OUT, "FB_Stl%s.awl" % b), "w", encoding="utf-8", newline="\n") as f:
            f.write(fb("FB_Stl" + b, opt, static, nets[b]))
    for name, text in AUX.items():
        with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print("networks:", sum(len(v) for v in nets.values()), {b: len(v) for b, v in nets.items()})
    print("missing:", missing)


if __name__ == "__main__":
    main()
