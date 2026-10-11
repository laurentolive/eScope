"""Client of the eScope automation REST API (Python standard library only).

The automation server is enabled in eScope > Tools > AI automation, which shows the port and the token.

Library:
    from escope_api import EScope
    scope = EScope(token="...")                 # or ESCOPE_TOKEN / ESCOPE_URL environment variables
    print(scope.call("get_workspace"))
    scope.call("acquire", duration_ms=2000, clear=True)
    open("plot.png", "wb").write(scope.screenshot())

Command line:
    python escope_api.py tools                              # list of the tools and their arguments
    python escope_api.py get_workspace
    python escope_api.py acquire '{"duration_ms": 2000}'
    python escope_api.py screenshot > plot.png
"""
import json
import os
import sys
import urllib.error
import urllib.request

DEFAULT_URL = "http://localhost:47653"


class EScopeError(Exception):
    """Error returned by eScope: code (NOT_FOUND, BUSY, NOT_CONNECTED...), message, HTTP status."""

    def __init__(self, code, message, status):
        super().__init__(f"{code}: {message}")
        self.code = code
        self.message = message
        self.status = status


class EScope:
    def __init__(self, token=None, url=None, timeout=660):
        self.url = (url or os.environ.get("ESCOPE_URL") or DEFAULT_URL).rstrip("/")
        self.token = token or os.environ.get("ESCOPE_TOKEN")
        if not self.token:
            raise ValueError("token required: eScope > Tools > AI automation (or ESCOPE_TOKEN)")
        self.timeout = timeout  # acquire waits for the end of the recording (600 s max)

    def _request(self, method, path, body=None):
        data = json.dumps(body if body is not None else {}).encode() if method == "POST" else None
        req = urllib.request.Request(self.url + path, data=data, method=method, headers={
            "Authorization": f"Bearer {self.token}",
            "Content-Type": "application/json",
        })
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                return r.headers.get("Content-Type", ""), r.read()
        except urllib.error.HTTPError as e:
            raw = e.read()
            try:
                err = json.loads(raw)["error"]
                raise EScopeError(err["code"], err["message"], e.code) from None
            except (ValueError, KeyError):
                raise EScopeError("HTTP", raw.decode(errors="replace"), e.code) from None

    def call(self, tool, **args):
        """Calls a tool; arguments by name (None values are left out). Returns the decoded JSON result."""
        body = {k: v for k, v in args.items() if v is not None}
        ctype, raw = self._request("POST", f"/api/{tool}", body)
        if ctype.startswith("image/"):
            return raw
        return json.loads(raw) if raw else None

    def tools(self):
        """Tools with their description and the JSON schema of their arguments."""
        return json.loads(self._request("GET", "/api/tools")[1])

    def screenshot(self, trace=None):
        """PNG bytes of the current trace as displayed."""
        return self.call("screenshot", trace=trace)


def main(argv):
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print(__doc__)
        return 0
    scope = EScope()
    tool = argv[1]
    if tool == "tools":
        for t in scope.tools():
            args = ", ".join(t["input_schema"].get("properties", {}))
            print(f"{t['name']}({args})\n    {t['description']}\n")
        return 0
    args = json.loads(argv[2]) if len(argv) > 2 else {}
    try:
        result = scope.call(tool, **args)
    except EScopeError as e:
        print(e, file=sys.stderr)
        return 1
    if isinstance(result, bytes):
        sys.stdout.buffer.write(result)
    else:
        print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
