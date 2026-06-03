# OpenWebForms

**Run classic ASP.NET Web Forms on modern, cross‑platform .NET — no IIS, no ASP.NET Core, no rewrite.**

A clean‑room reimplementation of `System.Web` that targets **.NET 8+** and runs your existing
`.aspx` applications on **Linux, macOS, Windows, and containers** — through its own self‑contained
managed HTTP host. Same API. Same assembly name. **Byte‑compatible `__VIEWSTATE`.** Your pages,
controls, master pages, and postbacks just work.

> **Status: experimental / alpha.** The core runtime, the Web Forms control library, and the `.aspx`
> compiler are implemented and **proven on Linux** (see [Status](#status)). Some peripheral namespaces
> are still stubbed. Not yet production‑ready — but it genuinely runs real Web Forms pages today.

---

## What this is

Existing "Web Forms on .NET Core" projects are *new frameworks* you port your application *to*.
OpenWebForms takes the other approach: it reimplements `System.Web` itself, clean‑room, from the
public API surface — same types, same assembly identity, byte‑compatible `__VIEWSTATE` — so an
existing `.aspx` application runs on modern, cross‑platform .NET without being rewritten.

Because the runtime is now open source rather than a frozen, Windows‑only black box, it also becomes
something it never could be before: a base to build *on*. The same clean‑room `System.Web` that runs
today's apps unchanged is also a foundation for something new that could grow from, evolved, extended or enhanced, such as "Web Forms 2.0".

---

## Highlights

- **Cross‑platform — proven on Linux.** A real `.aspx` page (data‑bound `GridView`, `<form>`,
  postback) parses → code‑generates → Roslyn‑compiles → renders to `HTTP 200` on Ubuntu. Full test
  suite green on Linux *and* Windows.
- **Drop‑in `System.Web`.** Same public types, same assembly identity (`System.Web`, v4.0.0.0). The
  public surface is **verified complete** — diffed against the real .NET Framework 4.8 `System.Web.dll`:
  **all 1,402 types present**.
- **Byte‑compatible ViewState.** Our `ObjectStateFormatter` reproduces the real `__VIEWSTATE` wire
  format **byte‑for‑byte** (verified against ground‑truth captured from the real framework), including
  the HMAC‑SHA1/SHA256 MAC — so postbacks round‑trip exactly like classic ASP.NET.
- **No ASP.NET Core, no Kestrel, no IIS.** A self‑contained managed HTTP host (Mono/XSP‑style) feeds
  the real `HttpRuntime` → `HttpApplication` pipeline → `Page` lifecycle. Completely standalone.
- **Real `.aspx`/`.ascx` compilation** via Roslyn — directives, server controls, code/data‑binding
  blocks, code‑behind, `AutoEventWireup`, master pages.
- **The full control library** — `Label`/`TextBox`/`Button`/lists, validators, `GridView`/`Repeater`/
  `DataList`/`DataGrid`/`DetailsView`/`FormView`, `Menu`/`TreeView`/`SiteMapPath`, `Login`/`Wizard`,
  `Calendar`, `WebParts`, `MasterPage`/`ContentPlaceHolder`, HTML controls, `ObjectDataSource`/
  `SqlDataSource`/`XmlDataSource`.
- **Services included** — InProc Session, Forms Authentication (real AES+HMAC tickets), a default
  Membership/Role/Profile provider model, `Cache`, `web.config` parsing, Routing, SiteMap.

---

## Quick start

> Requires the .NET SDK (8.0+). On a box with only a newer runtime, set
> `DOTNET_ROLL_FORWARD=Major` to run the net8.0 assemblies.

```bash
git clone https://github.com/ASP-NET-Web-Forms-Club/OpenWebForms.git
cd OpenWebForms

# Build the clean-room System.Web
dotnet build src/System.Web.csproj -c Release

# Run the standalone host serving the sample Web Forms app
dotnet run --project samples/host -- 8080
#   (or run the portable DLL directly:)
#   dotnet samples/host/bin/Release/net8.0/SampleHost.dll 8080

curl http://localhost:8080/default.aspx
```

You'll get a fully rendered Web Forms page — a data‑bound `GridView`, a `<form>` with a real
`__VIEWSTATE` hidden field, and a server‑side button whose `Click` handler fires on postback.

### On Linux (e.g. WSL / Docker)

```bash
export DOTNET_ROLL_FORWARD=Major          # if only a newer runtime is installed
dotnet build src/System.Web.csproj -c Release
dotnet test  tests/System.Web.Tests.csproj
dotnet samples/host/bin/Release/net8.0/SampleHost.dll 8080 &
curl http://localhost:8080/default.aspx   # → HTTP 200, fully rendered .aspx
```

---

## Documentation

- **[Getting Started on Linux](docs/getting-started-linux.md)** — deploy a Web Forms app on Ubuntu/Linux
  (build, add pages, run the host, `systemd`/Docker/nginx).
- **[Getting Started on Windows](docs/getting-started-windows.md)** — run on Windows + modern .NET, no IIS.
- **[Extending OpenWebForms — Contributor & Maintainer Playbook](docs/extending-openwebforms.md)** — how to add
  features (the recipe, the test harness, the gotchas, worked examples: `Global.asax`, routing, `CompleteRequest`).

## How it works

```
               ┌─────────────────────────────────────────────────────────┐
 HTTP request  │  Standalone managed HTTP host (System.Net.HttpListener) │
--------------->  -> HttpWorkerRequest  → HttpRuntime.ProcessRequest     │
               │    -> HttpApplication pipeline (modules, events)        │
               │      -> PageHandlerFactory → BuildManager               │
               │        -> .aspx parse → codegen → Roslyn compile        │
               │          -> Page lifecycle → ViewState → Render → flush │
               └─────────────────────────────────────────────────────────┘
```

- **Standalone host.** A managed HTTP server (cross‑platform `HttpListener`) behind a swappable
  `HttpWorkerRequest` abstraction — no ASP.NET Core anywhere in the graph.
- **`.aspx` compiler.** `System.Web.Compilation` parses pages into a `ControlBuilder` tree, generates
  a C# page class, and compiles it with **Roslyn** at runtime.
- **Content parsing.** Request body / form / multipart / cookie parsing is handled by the embedded,
  RFC‑9112‑correct [**cshttp**](https://github.com/adriancs2/cshttp) parser (public domain).
- **Assembly loading.** Because the .NET shared framework ships a throw‑only `System.Web` facade, the
  host loads the real implementation through a dedicated `AssemblyLoadContext` — preserving the
  frozen `System.Web` v4.0.0.0 identity while ensuring *our* code runs.

---

## Status

Built and verified incrementally; full per‑tier detail in [`artifacts/PHASE2-LOG.md`](artifacts/PHASE2-LOG.md).

| Area | Status |
|---|---|
| Public API surface (vs. real `System.Web.dll`) | ✅ **Complete — all 1,402 types** |
| Standalone HTTP host + `HttpRuntime`/`HttpApplication` pipeline | ✅ |
| `HttpContext` / `HttpRequest` / `HttpResponse` / `HttpServerUtility` | ✅ |
| `web.config` parsing, `Cache`, `HttpUtility`/`HttpEncoder` | ✅ |
| Session (InProc), Forms auth, Membership/Role/Profile (default providers) | ✅ |
| `Control` / `Page` lifecycle, **ViewState (byte‑compatible)**, postback | ✅ |
| WebControls (common, validators, data‑bound, nav, login, calendar) | ✅ |
| HTML controls, WebParts, Master pages, Routing, SiteMap | ✅ |
| `Global.asax` (compiled app type, `Application_*`/`Session_*` events, `Application_Start` once) | ✅ |
| Default modules (`UrlRoutingModule`, `SessionStateModule`) + `MapPageRoute`/`Routes.Map` from `Application_Start` | ✅ |
| `.aspx`/`.ascx` parser + Roslyn compilation (`BuildManager`) | ✅ |
| **Linux** (build + tests + live `.aspx`) | ✅ **Proven** |
| Model binding (`System.Web.ModelBinding`) — value providers, binders, validators, `ModelDataSource` | ✅ |
| Management (health monitoring), AntiXss, Globalization, WebSockets, Instrumentation, control Adapters | ✅ |
| SQL/AD providers, StateServer/SQL session, out‑of‑proc | ⛔ documented stubs |

**Engineering principles enforced throughout:** clean‑room (public signatures + documented behavior
only — never decompiled/copied source), no public/protected signature changes (binary‑compatible),
and a growing cross‑platform test suite (unit + golden ViewState fixtures + end‑to‑end host tests).

---

## Clean‑room & trademark notice

This is an independent, **clean‑room** reimplementation built solely from the **public API surface**
(type/member signatures) and **documented/standard behavior** of `System.Web`, plus public RFCs for
the HTTP layer. No Microsoft source code or IL was copied, decompiled, or paraphrased.

It is **not affiliated with, endorsed by, or sponsored by Microsoft.** "ASP.NET", ".NET", and
"Web Forms" are trademarks of Microsoft Corporation, used here only descriptively. The assembly is
named `System.Web` for drop‑in compatibility; it is unsigned and distinct from Microsoft's
strong‑named assembly.

---

## Credits & license

- HTTP/1.1 parsing by [**cshttp**](https://github.com/adriancs2/cshttp) — public domain.
- License: **MIT**