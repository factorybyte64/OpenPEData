"""TRCV_C / TRCV (open user communication) with their special pins (CONNECT/ADDR as InOut). python tools/gen_trcv.py -> FB_<Lad|Fbd>_{TrcvC,Trcv} and the entries ou003/ou015."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from tiaclient import TiaClient, default_pid  # noqa: E402

TPL = '''{
    S7_Optimized := "TRUE";
    S7_PreferredLanguage := "%(lang)s"
}
FUNCTION_BLOCK "%(fb)s"
    VAR_INPUT
        en_r : Bool;
        cont : Bool;
        len : UInt;
        id : CONN_OUC;
        adhoc : Bool;
    END_VAR
    VAR_OUTPUT
        done : Bool;
        busy : Bool;
        err : Bool;
        status : Word;
        rcvdlen : UInt;
    END_VAR
    VAR_IN_OUT
        connect : TCON_IP_v4;
        data : Array[0..99] of Byte;
        comrst : Bool;
        addr : Variant;
    END_VAR
    VAR
        inst : %(ins)s;
    END_VAR
    {
        S7_Language := "%(lang)s";
        S7_NetworkTitle := "MLC_N1"
    }
    NETWORK
        %(head)s
            #inst.%(ins)s( %(args)s )
        END_RUNG
    END_NETWORK
END_FUNCTION_BLOCK
'''
ARGS = {"TRCV_C": "EN_R := #en_r, CONT := #cont, LEN := #len, CONNECT := #connect, DATA := #data, COM_RST := #comrst, DONE => #done, BUSY => #busy, ERROR => #err, STATUS => #status, RCVD_LEN => #rcvdlen, ADHOC := #adhoc, ADDR := #addr",
        "TRCV": "EN_R := #en_r, ID := #id, LEN := #len, NDR => #done, BUSY => #busy, ERROR => #err, STATUS => #status, RCVD_LEN => #rcvdlen, DATA := #data, ADHOC := #adhoc"}


def main():
    c = TiaClient(default_pid())
    out = os.path.join(HERE, "sources", "getput")
    os.makedirs(out, exist_ok=True)
    for lang in ("LAD", "FBD"):
        rp = os.path.join(HERE, "..", "docs", "catalog", "openuc_%s_results.json" % lang.lower())
        res = json.load(open(rp))
        for key, ins in (("ou003", "TRCV_C"), ("ou015", "TRCV")):
            fb = "FB_%s_%s" % (lang.title(), ins.title().replace("_", ""))
            src = TPL % {"lang": lang, "fb": fb, "ins": ins, "head": "RUNG wire#powerrail" if lang == "LAD" else "RUNG", "args": ARGS[ins]}
            p = os.path.join(out, fb + ".s7dcl")
            open(p, "w", newline="").write(src.replace("\n", "\r\n"))
            open(p[:-6] + ".s7res", "w").write("MultiLingualTexts:\n  - id: MLC_N1\n    en-US: '%s'\n" % ins)
            e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=p, waitMs="45000")
            comp = (r.get("compile") or {}) if isinstance(r, dict) else {}
            ok = (not e) and comp.get("errorCount", 1) == 0
            print(fb, "ok" if ok else "FAIL " + (str(r)[:300] if e else str([m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"][:3])))
            if ok:
                res[key] = {"title": res.get(key, {}).get("title", ins), "ok": True, "block": fb, "sample": "039_fw41", "manual": True}
            elif not e:
                c.call("TiaPlcSoftware", operation="delete", objectKind="block", name=fb)
        json.dump(res, open(rp, "w"), indent=1, sort_keys=True)


main()
