"""Minimal MCP (streamable HTTP) client for TiaMcpXE that keeps ONE session and binds it explicitly to a given TIA process
before any TIA call (no auto-attach to foreign instances).

    c = TiaClient(pid=12860); c.call("TiaProject", operation="list")

A call returns (is_error, text). Never run it while another client calls the same instance in parallel."""
import atexit
import json
import urllib.request

URL = "http://127.0.0.1:47124/mcp"


def default_pid():
    """TIA process of this work: env TIA_PID, else the number in tools/.tia_pid."""
    import os
    if os.environ.get("TIA_PID"):
        return int(os.environ["TIA_PID"])
    f = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".tia_pid")
    return int(open(f).read().strip()) if os.path.isfile(f) else 12860


class TiaClient:
    def __init__(self, pid, url=URL, timeout=600):
        self.url, self.timeout, self.sid, self.n = url, timeout, None, 0
        r = self._rpc("initialize", {"protocolVersion": "2025-03-26", "capabilities": {},
                                      "clientInfo": {"name": "OpenPEData-TiaClient", "version": "0.1"}})
        try:
            self._rpc("notifications/initialized", None, notify=True)
        except Exception:  # noqa: BLE001
            pass
        # explicit binding: never rely on auto-attach
        err, txt = self.call("TiaInstance", operation="attach", processId=str(pid))
        info = json.loads(txt) if not err else {}
        if err or info.get("processId") != pid:
            raise RuntimeError("could not bind to TIA process %s: %s" % (pid, txt[:300]))
        self.pid = pid
        atexit.register(self.close)

    def close(self):
        """End the MCP session (otherwise the server keeps one stale client per script run)."""
        if self.sid:
            try:
                req = urllib.request.Request(self.url, method="DELETE", headers={"Mcp-Session-Id": self.sid})
                urllib.request.urlopen(req, timeout=5).read()
            except Exception:  # noqa: BLE001
                pass
            self.sid = None

    def _rpc(self, method, params, notify=False):
        self.n += 1
        body = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            body["params"] = params
        if not notify:
            body["id"] = self.n
        req = urllib.request.Request(self.url, data=json.dumps(body).encode("utf-8"), method="POST",
                                     headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"})
        if self.sid:
            req.add_header("Mcp-Session-Id", self.sid)
        with urllib.request.urlopen(req, timeout=self.timeout) as resp:
            self.sid = resp.headers.get("Mcp-Session-Id") or self.sid
            raw = resp.read().decode("utf-8")
            ctype = resp.headers.get("Content-Type", "")
        if notify:
            return None
        if ctype.startswith("text/event-stream"):
            raw = "".join(l[5:].strip() for l in raw.splitlines() if l.startswith("data:"))
        return json.loads(raw)

    def call(self, tool, **args):
        r = self._rpc("tools/call", {"name": tool, "arguments": {k: (v if isinstance(v, str) else json.dumps(v)) for k, v in args.items()}})
        if "error" in r:
            return True, json.dumps(r["error"])
        res = r["result"]
        text = "\n".join(c.get("text", "") for c in res.get("content", []))
        return bool(res.get("isError")), text

    def json(self, tool, **args):
        err, txt = self.call(tool, **args)
        try:
            return err, json.loads(txt)
        except ValueError:
            return err, txt


if __name__ == "__main__":
    import sys
    c = TiaClient(int(sys.argv[1]))
    print(c.call("TiaProject", operation="list")[1][:500])
