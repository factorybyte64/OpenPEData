"""GET/PUT (S7 communication): REMOTE pointer pins as InOut, all four areas wired. python tools/gen_getput.py -> FB_<Lad|Fbd>_{Get,Put} and the result entries sc001/sc002."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from tiaclient import TiaClient, default_pid  # noqa: E402

TPL = '''{
    S7_Optimized := "FALSE";
    S7_PreferredLanguage := "%(lang)s"
}
FUNCTION_BLOCK "%(fb)s"
    VAR_INPUT
        req : Bool;
        id : Word;
    END_VAR
    VAR_OUTPUT
        done : Bool;
        err : Bool;
        status : Word;
    END_VAR
    VAR_IN_OUT
        addr1 : Remote;
        addr2 : Remote;
        addr3 : Remote;
        addr4 : Remote;
    END_VAR
    VAR
        inst : %(ins)s;
    END_VAR
    VAR_TEMP
        rd1 : Array[0..9] of Byte;
        rd2 : Array[0..9] of Byte;
        rd3 : Array[0..9] of Byte;
        rd4 : Array[0..9] of Byte;
    END_VAR
    {
        S7_Language := "%(lang)s";
        S7_NetworkTitle := "MLC_N1"
    }
    NETWORK
        %(head)s
            #inst.%(ins)s( REQ := #req, ID := #id, %(done)s => #done, ERROR => #err, STATUS => #status, %(extra)s )
        END_RUNG
    END_NETWORK
END_FUNCTION_BLOCK
'''


def main():
    c = TiaClient(default_pid())
    out = os.path.join(HERE, "sources", "getput")
    os.makedirs(out, exist_ok=True)
    res = {}
    for lang in ("LAD", "FBD"):
        rp = os.path.join(HERE, "..", "docs", "catalog", "s7comm_%s_results.json" % lang.lower())
        res = json.load(open(rp))
        for key, ins, done, area in (("sc001", "GET", "NDR", "RD"), ("sc002", "PUT", "DONE", "SD")):
            fb = "FB_%s_%s" % (lang.title(), ins.title())
            extra = ", ".join(["ADDR_%d := #addr%d" % (i, i) for i in range(1, 5)] + ["%s_%d := #rd%d" % (area, i, i) for i in range(1, 5)])
            src = TPL % {"lang": lang, "fb": fb, "ins": ins, "done": done, "extra": extra, "head": "RUNG wire#powerrail" if lang == "LAD" else "RUNG"}
            p = os.path.join(out, fb + ".s7dcl")
            open(p, "w", newline="").write(src.replace("\n", "\r\n"))
            open(p[:-6] + ".s7res", "w").write("MultiLingualTexts:\n  - id: MLC_N1\n    en-US: '%s'\n" % ins)
            e, r = c.json("TiaPlcSoftware", operation="documentWrite", objectKind="block", sourceFile=p, waitMs="45000")
            comp = (r.get("compile") or {}) if isinstance(r, dict) else {}
            ok = (not e) and comp.get("errorCount", 1) == 0
            print(fb, "ok" if ok else "FAIL " + (str(r)[:300] if e else str([m["description"] for m in comp.get("messages", []) if m.get("state") == "Error"][:3])))
            if ok:
                res[key] = {"title": res.get(key, {}).get("title", ins), "ok": True, "block": fb, "sample": "039_fw41", "manual": True}
        json.dump(res, open(rp, "w"), indent=1, sort_keys=True)


main()
