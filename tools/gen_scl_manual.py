"""SCL calls of catalog instructions whose parameter table does not give the SCL call form (array/DB operands, return values, instances):
hand-written statements, one FB each. python tools/gen_scl_manual.py [num ...] [--sample 039_fw41]
Result keys sl<NNN> in docs/catalog/scl_scl_results.json (same format as gen_typed.py)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_lad  # noqa: E402
from tiaclient import TiaClient, default_pid  # noqa: E402

NL = "\r\n"
STD_IN = ["vin : Variant", "dba : DB_ANY"]
STD_IO = ["by8 : Array[0..99] of Byte", "arrA : Array[0..9] of Int", "arrB : Array[0..9] of Int", "barr : Array[0..15] of Bool", "va : Array[*] of Int", "pos : DInt"]
STD_TEMP = ["i : Int", "di : DInt", "r : Real", "lr : LReal", "w : Word", "dw : DWord", "b : Bool", "by : Byte", "t : Time", "ec : Int", "o0 : Int", "o1 : Int", "v : Variant", "udi : UDInt", "b1 : Bool", "b2 : Bool", "b3 : Bool"]
STD_STAT = ["t1 : TON_TIME", "mem : LReal"]

# number -> (statement, extra variable declarations by section)
MANUAL = {
    7: "RESET_TIMER(TIMER := #t1);",
    8: "PRESET_TIMER(PT := T#2S, TIMER := #t1);",
    20: "#b := TypeOf(#vin) = Int;",
    21: "#b := TypeOfElements(#vin) = Int;",
    22: "#b := IS_ARRAY(#vin);",
    23: "#b := TypeOfDB(#dba) = Int;",
    40: "#ec := Serialize(SRC_VARIABLE := #di, DEST_ARRAY => #by8, POS := #pos);",
    42: "#ec := MOVE_BLK_VARIANT(SRC := #arrA, COUNT := 5, SRC_INDEX := 0, DEST_INDEX := 0, DEST => #arrB);",
    44: ["FILL_BLK(IN := #i, COUNT := 5, OUT => #arrA);", "FILL_BLK(IN := #i, COUNT := 5, OUT := #arrA);", "FILL_BLK(IN := #i, COUNT := 5, DSTBLK => #arrA);", "FILL_BLK(IN := #i, COUNT := 5, OUT => #arrA[0]);", "FILL_BLK(IN := #i, COUNT := 5, OUT => \"DB_Arr\".a[0]);", "FILL_BLK(IN := #i, COUNT := 5, OUT => \"DB_Arr\".a);"],
    45: ["UFILL_BLK(IN := #i, COUNT := 5, OUT => #arrA);", "UFILL_BLK(IN := #i, COUNT := 5, OUT := #arrA);", "UFILL_BLK(IN := #i, COUNT := 5, OUT => #arrA[0]);", "UFILL_BLK(IN := #i, COUNT := 5, OUT => \"DB_Arr\".a[0]);", "UFILL_BLK(IN := #i, COUNT := 5, OUT => \"DB_Arr\".a);"],
    46: "SCATTER(IN := #w, OUT => #barr);",
    48: ["GATHER(IN := #barr, OUT => #w);", "GATHER(IN := #barr, OUT := #w);", "#w := GATHER(#barr);"],
    51: ["#ec := ReadFromArrayDB(DB := #dba, INDEX := #di, VALUE => #i);", "#ec := ReadFromArrayDB(DB := #dba, INDEX := #di, VALUE := #i);", "ReadFromArrayDB(DB := #dba, INDEX := #di, VALUE => #i);"],
    52: ["#ec := WriteToArrayDB(DB := #dba, INDEX := #di, VALUE := #i);", "WriteToArrayDB(DB := #dba, INDEX := #di, VALUE := #i);"],
    60: "#ec := READ_LITTLE(SRC_ARRAY := #by8, DEST_VARIABLE => #di, POS := #pos);",
    61: ["WRITE_LITTLE(SRC_VARIABLE := #di, DEST_ARRAY => #by8, POS := #pos);", "#ec := WRITE_LITTLE(SRC_VARIABLE := #di, DEST_ARRAY := #by8, POS := #pos);"],
    62: "#ec := READ_BIG(SRC_ARRAY := #by8, DEST_VARIABLE => #di, POS := #pos);",
    63: ["WRITE_BIG(SRC_VARIABLE := #di, DEST_ARRAY => #by8, POS := #pos);", "#ec := WRITE_BIG(SRC_VARIABLE := #di, DEST_ARRAY := #by8, POS := #pos);"],
    66: ["#udi := CountOfElements(#arrA);", "#di := CountOfElements(#arrA);", "#dw := CountOfElements(#arrA);", "#b := CountOfElements(#arrA) > 0;", "#r := CountOfElements(#arrA);", "#di := UDINT_TO_DINT(CountOfElements(#arrA));", "#udi := CountOfElements(#vin);", "#b := CountOfElements(#vin) > 0;"],
    67: "#di := LOWER_BOUND(ARR := #va, DIM := 1);",
    68: "#di := UPPER_BOUND(ARR := #va, DIM := 1);",
    69: "#ec := BLKMOV(SRCBLK := \"DB_Arr\".a, DSTBLK => \"DB_Arr\".b);",
    70: "#ec := UBLKMOV(SRCBLK := \"DB_Arr\".a, DSTBLK => \"DB_Arr\".b);",
    71: ["#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".a[0]);", "#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".w);", "#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".b);", "#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".by);", "#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".r);", "#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".a[1]);", "@noopt:#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".a);", "@noopt:#ec := FILL(BVAL := #i, BLK => \"DB_Arr\".a[0]);", "@noopt:#ec := FILL(BVAL := #i, BLK => #arrA);"],
    72: "#r := INT_TO_REAL(#i);",
    80: ["#dba := VARIANT_TO_DB_ANY(IN := #vin, ERR => #ec);", "#dba := VARIANT_TO_DB_ANY(IN := #vin);"],
    81: ["#v := DB_ANY_TO_VARIANT(IN := #dba, ERR => #ec);", "#v := DB_ANY_TO_VARIANT(IN := #dba);"],
    82: "#ec := SCALE(IN := #i, HI_LIM := 100.0, LO_LIM := 0.0, BIPOLAR := TRUE, OUT => #r);",
    83: "#ec := UNSCALE(IN := #r, HI_LIM := 100.0, LO_LIM := 0.0, BIPOLAR := TRUE, OUT => #i);",
    95: "#w := ENDIS_PW(REQ := #b, F_PWD := #b, FULL_PWD := #b, R_PWD := #b, HMI_PWD := #b, F_PWD_ON => #b1, FULL_PWD_ON => #b2, R_PWD_ON => #b3, HMI_PWD_ON => #b1);",
    100: ["#ec := INIT_RD(#b);", "#b := INIT_RD(#b);", "INIT_RD(#b);"],
    101: ["WAIT(WT := 100);", "WAIT(100);"],
    102: "#lr := RUNTIME(#mem);",
    105: "#i := SEL(G := #b, IN0 := 1, IN1 := 2);",
    106: "#i := MUX(K := #di, IN0 := 1, IN1 := 2);",
    107: "DEMUX(K := #di, IN := #i, OUT0 => #o0, OUT1 => #o1);",
}


def source(num, stmt):
    noopt = stmt.startswith("@noopt:")
    stmt = stmt[7:] if noopt else stmt
    fb = "FB_Scl_m%03d" % num
    sec = lambda kw, lines: ("    %s%s" % (kw, NL) + "".join("        %s;%s" % (x, NL) for x in lines) + "    END_VAR" + NL) if lines else ""  # noqa: E731
    src = "{" + NL + '    S7_EditorMode := "SCL";' + NL + '    S7_Optimized := "%s";' % ("FALSE" if noopt else "TRUE") + NL + '    S7_Version := "0.1"' + NL + "}" + NL
    src += 'FUNCTION_BLOCK "%s"' % fb + NL + sec("VAR_INPUT", STD_IN) + sec("VAR_IN_OUT", [x for x in STD_IO if not (noopt and "[*]" in x)]) + sec("VAR", STD_STAT) + sec("VAR_TEMP", STD_TEMP)
    src += '    { S7_Language := "SCL" }' + NL + "    NETWORK" + NL + "        " + stmt + NL + "    END_NETWORK" + NL + "END_FUNCTION_BLOCK" + NL
    return fb, src


def main(argv):
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d  # noqa: E731
    sel = [int(a) for a in argv if a.isdigit()]
    c = TiaClient(default_pid())
    out = os.path.join(HERE, "sources", "scl_manual")
    os.makedirs(out, exist_ok=True)
    rp = os.path.join(HERE, "..", "docs", "catalog", "scl_scl_results.json")
    res = json.load(open(rp))
    items = json.load(open(os.path.join(HERE, "..", "docs", "catalog", "scl.json"), encoding="utf-8"))
    for num, stmt in sorted(MANUAL.items()):
        if sel and num not in sel:
            continue
        key = "sl%03d" % num
        if not sel and res.get(key, {}).get("ok"):
            continue
        for st in (stmt if isinstance(stmt, list) else [stmt]):
            fb, src = source(num, st)
            path = os.path.join(out, fb + ".s7dcl")
            open(path, "w", encoding="utf-8").write(src)
            e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=path, waitMs="45000")
            if e:
                msg = gen_lad.condense(r if isinstance(r, str) else json.dumps(r))
                ok = False
            else:
                comp = r.get("compile") or {}
                msg = "; ".join(m["description"] for m in comp.get("messages", []) if m.get("state") == "Error")
                ok = comp.get("errorCount", 1) == 0
                if not ok:
                    c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
            if ok:
                break
        title = items[num - 1]["title"].split(" (S7")[0]
        if ok:
            res[key] = {"title": title, "ok": True, "block": fb, "sample": opt("--sample", "039_fw41"), "manual": True}
        else:
            res.setdefault(key, {"title": title, "ok": False})["manualLast"] = msg[:300]
        print(key, title[:30], "ok" if ok else "FAIL " + msg[:200], flush=True)
        json.dump(res, open(rp, "w"), indent=1, sort_keys=True)


if __name__ == "__main__":
    main(sys.argv[1:])
