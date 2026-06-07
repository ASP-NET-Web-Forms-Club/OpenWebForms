# Small Pageless Web Forms Test (OpenWebForms on WSL)

The smallest possible **Pageless Web Forms** app — **one route, one hello-world page,
no `.aspx`** — rendered entirely in C# from `Global.asax` and served on **WSL (Ubuntu /
.NET 8)** by the **OpenWebForms** clean-room `System.Web` standalone host.

- `Global.asax` — the whole app. `Application_BeginRequest` routes `/` and `/hello` to a
  handler that builds a complete HTML document with `Response.Write` and ends the request
  with `CompleteRequest()` (the pageless "EndResponse" pattern). No master page, no
  ViewState, no server controls, no page lifecycle.
- `Web.config` — minimal pageless config (informational for the standalone host).
- `run.sh` — deploy into the host's `wwwroot` and run.
- `ANALYSIS-REPORT.md` — full test methodology, result, and the one alpha-stage bug
  found (and fixed): `System.Web.HttpUtility` collides with .NET's OOB
  `System.Web.HttpUtility.dll` at Roslyn code-gen time, silently breaking the app.

## Status

✅ **Works on stock OpenWebForms / Linux** (verified via HTTP GET + POST).
⚠️ Avoids `System.Web.HttpUtility.HtmlEncode` (uses a tiny local encoder) because of the
alpha bug documented in the report — a verified one-line fix is included there.

## Run (from Windows, driving WSL)

```bash
wsl -d Ubuntu-24.04 -- bash -lic "cd '/mnt/d/Claude Files/OpenWebForms' && \
  bash 'samples/small pageless web forms test/run.sh' 8080"
# then, from Windows:
curl http://localhost:8080/hello
```
