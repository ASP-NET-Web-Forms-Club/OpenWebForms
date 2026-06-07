# Pageless Web Forms on OpenWebForms / WSL — Test & Analysis Report

**Date:** 2026-06-06
**Goal:** Take the *Pageless Web Forms* architecture (full HTML rendered in C# from
`Global.asax`, no `.aspx`) down to its smallest possible scale — **one route, one
hello-world page, no `.aspx`** — and deploy it on **WSL (Ubuntu)** using the
**OpenWebForms** clean-room `System.Web` standalone host.

**Verdict:** ✅ **Pageless Web Forms runs on OpenWebForms/Linux** — *with one
important caveat*. The architecture (discovery + Roslyn compile of `Global.asax`,
`Application_Start`, `Application_BeginRequest` routing, `Response.Write`,
`CompleteRequest()` short-circuit) works end-to-end on .NET 8 / Ubuntu. **But** the
single most common pageless helper — `System.Web.HttpUtility.HtmlEncode(...)` — does
**not** work as-is on the host: it triggers a *silently-swallowed compile error* that
makes the whole app fall back to a stub handler. Root cause, a verified one-line fix,
and the workaround used in this sample are documented below.

---

## 1. Environment

| Item | Value |
|------|-------|
| Host OS | Windows 11, WSL2 |
| Linux distro | Ubuntu 24.04.4 LTS (`mingj`) |
| .NET SDK (Linux) | 8.0.127 |
| .NET runtime (Linux) | Microsoft.NETCore.App 8.0.27 |
| OpenWebForms | `D:\Claude Files\OpenWebForms` (alpha), `src/System.Web.csproj` + `samples/host` |
| Build | `dotnet build src/System.Web.csproj -c Release` and `samples/host/SampleHost.csproj -c Release` → **0 errors** |
| Run | portable DLL: `dotnet samples/host/bin/Release/net8.0/SampleHost.dll <port>` |
| App root | the host serves `…/bin/Release/net8.0/wwwroot`; `Global.asax` is discovered there |

All WSL commands were driven from Windows through MCP2 `run_command` (program
`wsl.exe`). HTTP verification used MCP2 `http_get` / `http_post` from the Windows side
against `http://localhost:<port>` (WSL2 forwards `localhost`).

---

## 2. The app under test (smallest scale)

A single self-contained `Global.asax` (inline `<script runat="server">`, **no**
code-behind, **no** `.aspx`, **no** DB). One routing point, one page:

```csharp
void Application_BeginRequest(object sender, EventArgs e)
{
    string path = (Context.Request.Path ?? "/").ToLowerInvariant().TrimEnd('/');
    if (path.Length == 0) path = "/";
    switch (path)
    {
        case "/":
        case "/hello":
            RenderHello();      // builds the whole HTML doc in C#, then CompleteRequest()
            return;
    }
}
```

`RenderHello()` writes a complete `<!DOCTYPE html>…</html>` document with
`Response.Write`, sets `text/html`, flushes, and calls `CompleteRequest()` — the
pageless "EndResponse" pattern that short-circuits the pipeline so no page handler
runs.

See [`Global.asax`](Global.asax). This maps exactly onto the reference architecture in
`csharpcms/Pageless-Architecture.txt` and the real CMS `Global.asax.cs`, reduced to one
route.

---

## 3. Result — success evidence

Host running on Ubuntu, requested from Windows via MCP2 HTTP:

```
GET  http://localhost:8080/        → HTTP 200, text/html, 1203 bytes  (Request method: GET)
GET  http://localhost:8080/hello   → HTTP 200, text/html, 1208 bytes  (Request method: GET)
POST http://localhost:8080/hello   → HTTP 200, text/html, 1209 bytes  (Request method: POST)
```

Rendered page (excerpt):

```html
<h1>Hello World</h1>
<p>This page was rendered entirely in C# from <code>Global.asax</code> …</p>
…
.NET runtime: 8.0.27
OS: Ubuntu 24.04.4 LTS
Request path: /hello
Request method: GET
```

Both **GET and POST** hit the same `Application_BeginRequest` route and render — a true
pageless request with no `.aspx` anywhere. The host's load diagnostic confirms it is
**our** clean-room assembly serving the request:

```
Assembly: System.Web  AssemblyVersion: 4.0.0.0  LoadContext: SystemWebHostContext
```

What the runtime exercised successfully:

- `Global.asax` discovery at the app root (case-insensitive — finds `Global.asax` on Linux)
- `.asax` parse → C# code-gen → **Roslyn compile** of an `HttpApplication`-derived type
- `Application_Start` raised once; `Application_BeginRequest` bound to the pipeline `BeginRequest` stage
- string routing in `BeginRequest`, `Response.Write`, `Response.Flush`, `Response.ContentType`
- `CompleteRequest()` short-circuit (no page handler / lifecycle runs after the handler)
- identical behavior for GET and POST

---

## 4. The caveat — `HttpUtility.HtmlEncode` silently breaks the whole app

### 4.1 Symptom

The *first* version of this sample used the canonical pageless idiom for safe output:

```csharp
sb.Append("Request path: " + System.Web.HttpUtility.HtmlEncode(Context.Request.Path));
```

With that line present, **every** request returned the host's built-in stub:

```
HTTP/1.1 200 OK
Content-Type: text/plain; charset=utf-8
Content-Length: 22

Hello from System.Web
```

i.e. the `Application_BeginRequest` handler **never ran at all** — not even an
`Application_Error` handler fired. Removing/replacing the `HttpUtility` call made the
page render correctly. The failure is **all-or-nothing and silent**: there is no error
in the console, no 500 — the app just isn't there.

### 4.2 Root cause — duplicate `System.Web.HttpUtility` type → CS0433 → swallowed

The .NET 8 shared framework ships an **out-of-box assembly**
`System.Web.HttpUtility.dll` (≈39 KB, present at
`/usr/lib/dotnet/shared/Microsoft.NETCore.App/8.0.27/System.Web.HttpUtility.dll`). It
defines the public type **`System.Web.HttpUtility`** (and `System.Web.Util.HttpEncoder`)
— the *same* types the OpenWebForms clean-room `System.Web.dll` defines.

When the host Roslyn-compiles `Global.asax`, it builds the reference set from the
**entire** Trusted-Platform-Assemblies list and explicitly removes only `System.Web.dll`:

`src/System.Web.Compilation.Engine.cs` → `ComputeBaseReferences()`:

```csharp
string fileName = Path.GetFileName(p);
if (string.Equals(fileName, "System.Web.dll", StringComparison.OrdinalIgnoreCase)) { continue; }
//  ↑ strips the framework System.Web facade, but NOT System.Web.HttpUtility.dll
```

So the compile sees `System.Web.HttpUtility` in **two** referenced assemblies
(our `System.Web.dll` *and* the OOB `System.Web.HttpUtility.dll`) → **CS0433
"ambiguous reference"** → the `Global.asax` fails to compile.

That compile error is then **swallowed** during first-time app init:

`src/System.Web.cs` → `EnsureFirstTimeInit()`:

```csharp
try { appType = BuildManager.GetGlobalAsaxType(); }
catch (Exception) { appType = null; }            // ← compile error discarded here
if (appType == null || !typeof(HttpApplication).IsAssignableFrom(appType))
{
    appType = typeof(HttpApplication);           // ← falls back to BASE HttpApplication
}
```

With the base `HttpApplication`, there are no magic methods to bind, so
`BeginRequest` has no handler; the pipeline proceeds to `MapRequestHandler`, finds no
`.aspx`, and serves the built-in `DefaultDemoHandler` ("Hello from System.Web",
`src/System.Web.cs:1673`). Hence the silent fallback.

This is impactful for pageless apps specifically because they lean on `HttpUtility`
(HtmlEncode / HtmlAttributeEncode / UrlEncode) for **all** output encoding — see the
reference CMS, which calls `HttpUtility.HtmlEncode` throughout its handlers and
`PageTemplate`.

### 4.3 Verified fix (one line)

Excluding the OOB assembly from the code-gen reference set makes our `System.Web`'s
`HttpUtility` the only definition, so the ambiguity disappears.

In `ComputeBaseReferences()` (right after the existing `System.Web.dll` skip):

```csharp
// Skip the OOB System.Web.HttpUtility.dll too: it defines System.Web.HttpUtility /
// HttpEncoder, which OUR System.Web also defines -> CS0433 ambiguous-type otherwise.
if (string.Equals(fileName, "System.Web.HttpUtility.dll", StringComparison.OrdinalIgnoreCase)) { continue; }
```

**Tested:** with this patch applied and the host rebuilt, a `Global.asax` calling
`System.Web.HttpUtility.HtmlEncode("<a&b>")` compiled and ran, returning:

```html
<h1>HttpUtility works: &lt;a&amp;b&gt;</h1>
```

(confirming both that it compiles *and* that our clean-room `HtmlEncode` encodes
correctly). The patch was then **reverted** so the OpenWebForms tree in this repo
remains pristine — apply it when ready.

> Recommended hardening (separate from this sample): instead of a single filename
> skip, prefer *our* `System.Web.dll` for **any** TPA whose simple assembly name we
> also ship, and consider surfacing swallowed `Global.asax` compile errors (log them /
> serve a 500 with the Roslyn diagnostics) rather than discarding them in
> `EnsureFirstTimeInit` — the silent fallback to "Hello from System.Web" cost most of
> the debugging time here.

### 4.4 Workaround used in this sample (so it runs on stock OpenWebForms)

[`Global.asax`](Global.asax) avoids `HttpUtility` and uses a tiny local encoder:

```csharp
static string Enc(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    return s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
}
```

With that, the sample runs on **unmodified** OpenWebForms (evidence in §3).

---

## 5. How to reproduce

From Windows (PowerShell / MCP2 `run_command` with `program = wsl.exe`):

```bash
# 1) build the clean-room System.Web + the standalone host
cd '/mnt/d/Claude Files/OpenWebForms'
export DOTNET_ROLL_FORWARD=Major
dotnet build src/System.Web.csproj -c Release
dotnet build samples/host/SampleHost.csproj -c Release

# 2) deploy this pageless app into the host's app root (wwwroot)
DST=samples/host/bin/Release/net8.0/wwwroot
cp 'samples/small pageless web forms test/Global.asax' "$DST/Global.asax"

# 3) run the host (portable DLL — works off the /mnt 9p mount)
dotnet samples/host/bin/Release/net8.0/SampleHost.dll 8080

# 4) verify (from Windows; WSL2 forwards localhost)
curl http://localhost:8080/        # → 200, full HTML
curl http://localhost:8080/hello   # → 200, full HTML
curl -X POST http://localhost:8080/hello   # → 200, "Request method: POST"
```

`run.sh` in this folder wraps steps 2–3.

---

## 6. Files in this folder

| File | Purpose |
|------|---------|
| `Global.asax` | the tiny pageless app (one route, hello world, no `.aspx`) — runs on stock OpenWebForms |
| `Web.config` | minimal pageless config (passthrough errors, session off) — informational for the standalone host |
| `run.sh` | build-if-needed + deploy + run the host on WSL |
| `ANALYSIS-REPORT.md` | this report |
| `README.md` | quick overview |

---

## 7. Bottom line

- **Pageless Web Forms is viable on OpenWebForms / Linux today.** The `Global.asax`-only,
  no-`.aspx` model — discovery, Roslyn compile, `Application_Start`,
  `Application_BeginRequest` routing, `Response.Write`, `CompleteRequest()` — all work,
  for GET and POST, on .NET 8 / Ubuntu, served by the clean-room `System.Web`.
- **One alpha blocker matters for real pageless apps:** `System.Web.HttpUtility` (used
  pervasively for output encoding) collides with the framework's OOB
  `System.Web.HttpUtility.dll` at code-gen time, and the resulting compile error is
  swallowed — the app silently degrades to the stub handler. A one-line reference-set
  exclusion fixes it (verified). Until applied, encode without `HttpUtility`.
