# Extending OpenWebForms — Contributor & Maintainer Playbook

This is the hands‑on guide for **adding a feature** (filling a stubbed member, wiring a new behavior) to
OpenWebForms without breaking its core invariants. It encodes the methods, strategy, and hard‑won lessons
from building the project, plus worked examples (`Global.asax`, route registration, the `CompleteRequest`
short‑circuit pattern).

Read this once before your first contribution. Then keep it next to you.

---

## 0. The prime directive

OpenWebForms is a **drop‑in, clean‑room `System.Web`**. Its entire value is that *existing* ASP.NET Web
Forms apps run **unchanged**. That gives us a few invariants that are **never** worth violating for a feature:

1. **The public/protected API surface is frozen.** It must stay byte‑identical (type + member shape) to the
   real .NET Framework 4.8 `System.Web.dll`. We fill **bodies**; we never change a public/protected signature.
2. **It's clean‑room.** Implemented from the **public signatures** (`artifacts/system.web.api.json`) and
   **documented / standard / RFC behavior** — never from decompiled or copied Microsoft source.
3. **`__VIEWSTATE` is byte‑compatible.** Don't touch `ObjectStateFormatter` wire output without re‑checking
   the golden fixtures.

Everything below serves those invariants.

---

## 1. The five hard rules (and why)

| Rule | Why |
|------|-----|
| **No public/protected signature changes** — bodies + `private`/`internal` helpers only | Binary compatibility. Adding/changing a public member makes the assembly diverge from real `System.Web` and breaks the surface‑completeness proof. |
| **Clean‑room** — public signatures + documented behavior only | Legal cleanliness. If you've read decompiled framework source for a type, don't implement that type. |
| **No LINQ in `src/`** — explicit `for`/`foreach` | Project convention (perf/clarity/auditability). `tools/` may use LINQ. |
| **Keep it green** — `dotnet build` 0 errors, tests pass | Every change is an isolated, reviewable, green increment. |
| **No `Microsoft.AspNetCore.*`** | This is a standalone runtime; it owns its HTTP host and pipeline. |

New helper **types** you add must be `internal`. Out‑of‑scope members get an honest documented stub
(`throw new NotImplementedException("TODO: …")` or `PlatformNotSupportedException` + a comment) — never a
silent fake.

---

## 2. Repository map

```
src/                       ONE file per namespace; bodies you fill live here
  System.Web.cs            HttpContext/Request/Response/Runtime/Application, Cookies, SiteMap, ...
  System.Web.UI.cs         Control, Page, StateBag, ObjectStateFormatter, HtmlTextWriter, ControlBuilder, MasterPage
  System.Web.UI.WebControls.cs   all server controls (the 671 KB file)
  System.Web.*.cs          Caching, Configuration, Security, SessionState, Profile, Routing, Compilation, ModelBinding, Management, ...
  Server/                  internal standalone HTTP host (HttpListenerServer, ListenerWorkerRequest) [INTERNAL]
  Http/                    vendored cshttp parser (namespace System.Web.Http.CsHttp) [INTERNAL]
  Shims/                   minimal clean-room shims for sibling-assembly forwarded types (Membership/Role/etc.)
samples/host/              the standalone host you run (Program -> AlcBootstrap -> RealEntry); wwwroot/ = content
tests/                     xUnit suite; SystemWebUnderTest.cs = the custom-ALC bridge
tools/                     extract-api (Cecil API extractor), gapdiff (surface diff), fixtures-gen (net48 ViewState capture)
fixtures/viewstate/        ground-truth __VIEWSTATE captured from the REAL framework
artifacts/system.web.api.json   THE SPEC — the source of truth for every type/member signature
```

**`artifacts/system.web.api.json` is the authority.** Whenever you wonder "is this type/member real? what's
its exact signature?" — grep the spec. It has full member detail (methods/properties/fields/events/ctors,
types, modifiers). Do **not** trust memory or even an audit claim over the spec (see §6, the
`ICodeBlockTypeAccessor` lesson).

---

## 3. Runtime mental model (where a feature plugs in)

```
HTTP request
  → HttpListenerServer (src/Server)                      standalone managed host
    → ListenerWorkerRequest : HttpWorkerRequest          maps the socket to the abstract worker request
      → HttpRuntime.ProcessRequest(wr)                   builds HttpContext, drives the app
        → HttpApplication pipeline                        BeginRequest … AuthenticateRequest …
          → MapRequestHandler → IHttpHandlerFactory       *.aspx → PageHandlerFactory
            → BuildManager                                .aspx parse → C# codegen → Roslyn compile
              → Page lifecycle                            Init/Load/PreRender/SaveViewState/Render/Unload
                → Response flushed to the worker request
```

- **Per‑request services** hang off `HttpContext` (`Request`/`Response`/`Session`/`User`/`Items`/`Cache`).
- **Pipeline behavior** lives in `HttpApplication` (events) and `IHttpModule`s.
- **Page/control behavior** lives in `System.Web.UI.*`.
- **`.aspx` → class** lives in `System.Web.Compilation` (`TemplateParser`, `BuildManager`, `ControlBuilder`).
- **Everything loads through a custom `AssemblyLoadContext`** (see §6) — keep that in mind when something
  "can't find" a System.Web type at runtime.

---

## 4. The repeatable recipe for adding a feature

> Works for "implement this stubbed member" and "wire this behavior." Small features need no orchestration —
> just follow this.

**Step 1 — Locate it in the spec.**
```bash
# Is it real? What's the exact shape?
grep -n "TrySkipIisCustomErrors" artifacts/system.web.api.json
```
If it's **not** in the spec, it doesn't belong in this assembly (it may be MVC / `System.Web.Extensions` /
another assembly — see the ListView lesson in §6). Don't add it.

**Step 2 — Find the skeleton.** Open the right `src/*.cs` file and find the `throw new NotImplementedException()`
body for the member. (`grep -n "TrySkipIisCustomErrors" src/System.Web.cs`.)

**Step 3 — Implement the body.** Fill it. Add `private`/`internal` helpers/fields as needed. Reuse existing
building blocks (HttpEncoder, the cshttp parsers, Cache, config sections, ObjectStateFormatter). **No LINQ.
No signature change.** If a skeleton ctor chains `: base(default(X))` (a known generator artifact that passes
null/garbage to the base), fix the **body's base‑initializer** to pass the real argument — that is a body fix,
not a signature change (see §6).

**Step 4 — Wire it in** if it's behavioral (not just a leaf method). Find the call site in the pipeline /
lifecycle and connect it. The worked examples in §8 show this.

**Step 5 — Add a test** through the ALC bridge (see §5). Prove the behavior; don't just prove it compiles.

**Step 6 — Build green + run tests (Windows):**
```bash
dotnet build src/System.Web.csproj -c Debug && dotnet test tests/System.Web.Tests.csproj
```

**Step 7 — Validate on Linux (see §7).** Cross‑platform is the whole point; confirm it there too.

**Step 8 — Self‑audit (the checklist):**
- [ ] No public/protected signature changed; new types `internal`
- [ ] No `using System.Linq` / LINQ operators in `src/`
- [ ] No copied/decompiled source
- [ ] Build green; tests pass (Windows **and** Linux)
- [ ] Public type count unchanged vs the spec (`dotnet run --project tools/gapdiff` if you added/changed types)
- [ ] `__VIEWSTATE` byte‑compat tests still pass if you touched `ObjectStateFormatter`/`StateBag`/controls

---

## 5. The test harness (how to actually test this)

Tests can't just call `new HttpContext(...)` directly, because the .NET shared framework ships a **throw‑only
`System.Web` facade** that shadows our assembly in the default load context (see §6). So tests go through
**`tests/SystemWebUnderTest.cs`** — a helper that loads *our* `System.Web.dll` into a dedicated
`AssemblyLoadContext` and exposes reflection helpers.

**The pattern:** a thin `[Fact]` plus a "worker" class that runs *inside* the ALC.

```csharp
// XxxTests.cs — the test (runs in the default ALC; drives the worker via the bridge)
public class XxxTests
{
    [Fact]
    public void MyFeature_Works()
    {
        object[] r = (object[])SystemWebUnderTest.Instance.RunInAlc(
            "System.Web.Tests.XxxWorker", "Run");
        Assert.Equal("expected", (string)r[0]);
    }
}

// XxxWorker.cs — runs INSIDE the ALC, so System.Web types bind to OURS
namespace System.Web.Tests
{
    public static class XxxWorker
    {
        public static object[] Run()
        {
            // free to use System.Web.* directly here
            var wr = new CapturingWorkerRequest("/page.aspx", "", "GET", null);
            System.Web.HttpRuntime.ProcessRequest(wr);
            return new object[] { wr.GetBodyText() };
        }
    }
}
```

- `TestWorkerRequests.cs` has reusable `HttpWorkerRequest` test doubles (feed a request, capture the response).
  Use `ContentLengthHonoringWorkerRequest` when a test must mimic real `HttpListener` truncation behavior.
- **Worker/test types that derive from `Control`/`Page` must be `internal`**, or xUnit's default‑ALC type
  scan will try to load them and fail (their base only resolves inside the bridge ALC).
- **Golden fixtures:** for byte‑exact compatibility (ViewState, and you can extend it to rendered HTML),
  capture ground truth from the **real framework** with `tools/fixtures-gen` (a net48 app that references the
  GAC `System.Web`), store under `fixtures/`, and assert our output matches byte‑for‑byte. This is how
  ViewState compatibility is locked in — reuse the approach for any "must match real ASP.NET exactly" feature.

---

## 6. Hard‑won gotchas (read these — they will save you hours)

1. **Framework‑facade shadowing.** `Microsoft.NETCore.App` ships a strong‑named, throw‑only `System.Web.dll`
   (same name, same v4.0.0.0) on the Trusted Platform Assemblies list. In the **default** ALC it wins over our
   app‑local copy → `typeof(System.Web.HttpContext)` binds to the facade and throws. **Solution (already in
   place):** the host loads our assembly through a custom ALC (`samples/host/AlcBootstrap.cs`,
   `SystemWebHostContext`), and `SampleHost.csproj` carries MSBuild targets that strip the framework's
   `System.Web` reference so ours ships app‑local. **Any new host or test path must use the same pattern.**
2. **`System.Configuration` type‑string resolution.** Config section type strings (`"…, System.Web"`) are
   resolved by `System.Configuration` in the *default* ALC → the facade again. Drive config under the custom
   ALC, or read raw section XML and feed it to our section's `DeserializeSection` (what the config tests do).
3. **`HttpResponse.Output` BOM/flush bug class.** A `StreamWriter` with a BOM‑emitting encoding + `AutoFlush`
   can flush a 3‑byte BOM early and lock `Content-Length: 3`, after which `HttpListener` **truncates the whole
   body**. Lesson: be careful with response encoding/flush timing; the in‑memory test worker won't catch
   truncation — use `ContentLengthHonoringWorkerRequest`.
4. **"Is this type real?" — trust the spec, not claims.** An audit once called `ICodeBlockTypeAccessor` an
   "invented" public type; it was made `internal` — but it **is** a real public type in the spec, so that
   *under‑exposed* a frozen type. **Always verify against `artifacts/system.web.api.json`.**
5. **Same namespace ≠ same assembly.** `ListView`/`DataPager` live in `System.Web.UI.WebControls` *namespace*
   but in **`System.Web.Extensions.dll`**, not `System.Web.dll`. They're correctly **out of scope**. Verify a
   type's owning assembly (reflect the real GAC DLL) before implementing.
6. **Phase‑1 skeleton gaps.** A few spec types were missing from the generated skeleton (e.g.
   `PersonalizationAdministration`). If `tools/gapdiff` reports a missing type, **regenerate its stub from the
   spec** (read its members from `system.web.api.json`, emit `global::`‑qualified `NIE` bodies) and add it.
7. **`: base(default(X))` ctor artifacts.** The skeleton generator sometimes chained base ctors with
   `default(...)`, which passes null/zero and throws/NREs. Fix the base‑initializer in the body to pass the
   real argument. (Common in `*EventArgs` and collection ctors.) Not a signature change.
8. **`System.Drawing` on Linux.** `System.Drawing.Common` GDI+ is Windows‑only on modern .NET. `System.Drawing.Color`
   (the struct, in `System.Drawing.Primitives`) is cross‑platform — use it for styling. Don't pull in GDI+
   (`Bitmap`/`Graphics`) on the request path.
9. **Linux is case‑sensitive.** `.aspx` filenames and references must match case.

---

## 7. Cross‑platform validation (WSL / Linux)

Our assemblies are `net8.0`; if a Linux box only has a newer runtime, run with `DOTNET_ROLL_FORWARD=Major`.

```bash
# from Windows, drive WSL Ubuntu (login shell required for dotnet on PATH):
wsl -d Ubuntu-24.04 -- bash -lic "cd '/mnt/d/Claude Files/System.Web Project' && export DOTNET_ROLL_FORWARD=Major \
  && dotnet build src/System.Web.csproj -c Debug -v quiet | tail -3 \
  && dotnet test tests/System.Web.Tests.csproj 2>&1 | tail -4"

# live .aspx on Linux — run the PORTABLE DLL (the apphost won't launch off a /mnt 9p mount):
wsl -d Ubuntu-24.04 -- bash -lic "cd '/mnt/d/Claude Files/System.Web Project' && export DOTNET_ROLL_FORWARD=Major \
  && (dotnet samples/host/bin/Debug/net8.0/SampleHost.dll 8096 &>/tmp/h.log &) && sleep 12 \
  && curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8096/default.aspx ; pkill -f SampleHost.dll"
```

**Tip for big test/build runs:** delegate them to a sub‑agent (or just `| tail`) so the verbose output doesn't
flood your working context — capture only the pass/fail summary.

---

## 8. Worked examples

### 8a. `Global.asax` / `Global.asax.cs`

**What it is:** an optional app‑root file declaring an `HttpApplication` subclass with "magic" event methods
(`Application_Start`, `Application_End`, `Session_Start`, `Application_BeginRequest`, `Application_Error`, …)
and an `<%@ Application %>` directive (optionally `Inherits="MyApp.Global"` for a code‑behind class).

**Where it plugs in:**
- **Parse:** add an application‑file parser in `System.Web.Compilation` (mirror `PageParser`/`TemplateParser`)
  that reads `global.asax`, honors `<%@ Application Inherits=... %>`, and produces an `HttpApplication`‑derived
  type via Roslyn (`BuildManager.GetGlobalAsaxType()` / `CreateInstanceFromVirtualPath(".../global.asax", typeof(HttpApplication))`).
- **Instantiate:** in `HttpRuntime` (`src/System.Web.cs`), the application factory currently pools a plain
  `HttpApplication`. Change it to: if `~/global.asax` exists, compile it and use **that** type as the pooled
  application instance; else fall back to plain `HttpApplication`.
- **Event wireup:** in `HttpApplication.Init`/the factory, reflect the instance's methods and hook the
  "magic" names: `Application_Start(object,EventArgs)` is called **once** on first app init;
  `Application_End` on shutdown; `Application_BeginRequest`/`AuthenticateRequest`/`Error`/`EndRequest`/… are
  bound to the corresponding pipeline events by name; `Session_Start`/`Session_End` are raised by
  `SessionStateModule`. (Real ASP.NET matches by method name; replicate that name table.)

**Steps:**
1. `grep "class HttpApplicationFactory\|GetGlobalAsaxType\|ApplicationFileParser" artifacts/system.web.api.json src/*.cs` to find the exact skeleton members.
2. Implement the global.asax parser + `BuildManager.GetGlobalAsaxType`.
3. In `HttpRuntime`, resolve & pool the global.asax‑derived `HttpApplication`; call `Application_Start` once
   (guard with a flag), wire per‑request magic methods in `HttpApplication.InitInternal`.
4. Test: a worker that writes a `global.asax` (with `Application_Start` setting an `Application["x"]` value and
   `Application_BeginRequest` setting a response header), runs two requests, and asserts the start ran once and
   the per‑request hook ran each time.

**Note:** this unlocks the *idiomatic* place users call route registration (8b) and module config.

### 8b. Route registration (`RouteConfig.RegisterRoutes(RouteTable.Routes)` / `routes.MapPageRoute(...)`)

**Good news: the routing engine already exists** (Tier 7): `RouteTable.Routes`, `RouteCollection.MapPageRoute`,
`Route`, `RouteData`, `UrlRoutingModule`, `PageRouteHandler`. So "supporting `Routes.Map(...)`" is mostly about
**making sure the registration runs and the module is active**:

1. **Run the registration.** Apps call `RouteConfig.RegisterRoutes(RouteTable.Routes)` from
   `Application_Start` — so this depends on **8a (Global.asax)**. Once `Application_Start` fires, user code
   populates `RouteTable.Routes` exactly as on .NET Framework. No new API needed.
2. **Ensure `UrlRoutingModule` is in the default module set.** Confirm `HttpApplication`'s default module
   initialization registers `UrlRoutingModule` (it subscribes `PostResolveRequestCache` and remaps matched
   requests to the route handler). If not, add it to the default modules (or honor `<system.webServer><modules>`
   / `<httpModules>` config).
3. **Verify `MapPageRoute` end‑to‑end:** a test that registers `routes.MapPageRoute("p","products/{id}","~/Product.aspx")`,
   issues `GET /products/5`, and asserts `Product.aspx` ran with `RouteData.Values["id"] == "5"`.

If you want attribute‑style helpers beyond the framework API, put them in a **separate** package — don't add
public members to `System.Web` (rule #1).

### 8c. The `CompleteRequest` short‑circuit pattern

```csharp
Response.TrySkipIisCustomErrors = true;
try { Response.Flush(); } catch { }
Response.SuppressContent = true;
HttpContext.Current.ApplicationInstance.CompleteRequest();
```

Status of each piece and how to finish it:
- **`HttpApplication.CompleteRequest()`** — already implemented (Tier 2): it sets a flag that makes the
  pipeline skip remaining stages and jump to `EndRequest`. ✔
- **`HttpResponse.Flush()`** — implemented (pushes buffered output to the worker request). ✔
- **`HttpResponse.SuppressContent`** — check it's wired: the getter/setter store a `bool`, and **`Flush`/`End`/
  the final write path must emit no body when `SuppressContent` is true** (headers/status only). If it's a bare
  auto‑property, add the gate in the flush path. (Small body change.)
- **`HttpResponse.TrySkipIisCustomErrors`** — IIS‑specific; in our standalone host there is no IIS custom‑error
  layer, so implement it as a stored `bool` **no‑op** (get/set a backing field). Document that it has no effect
  outside IIS. ✔ by definition.

**Test:** a handler that runs the snippet, then assert the response has the already‑written status/headers, an
**empty body** (SuppressContent honored), and that **no later pipeline stage ran** (CompleteRequest honored).

---

## 9. The orchestration strategy (how this was built at scale)

You don't need this for a one‑member fix — but it's how the bulk was done and it scales to big chunks:

- **Decompose by namespace / cohesive type‑cluster.** One writer owns one file (the big files are edited by
  **sequential** clusters; independent files are done **in parallel**).
- **Pipeline per push:** *implement → integrate (one green build + behavioral tests) → read‑only audit*. The
  audit is independent agents checking clean‑room, no‑LINQ, no signature change/leak, and the build/test gate.
- **The spec is the checklist.** Drive each cluster from the `system.web.api.json` members for its types so
  nothing is missed; the audit diffs against it.
- **Always leave a green, committed checkpoint.** If a long run is interrupted, the **disk state persists** —
  inspect it, confirm it builds, and continue from there (don't blindly replay).
- **Make agents return compact structured results, not raw logs** — keeps the orchestrator's context lean
  (this is also why heavy `dotnet test` output should be summarized, not pasted).
- **Validate cross‑platform every milestone** (the WSL recipe in §7).

---

## 10. Where help is most valuable next

- **`Global.asax`** (8a) — unlocks idiomatic startup, routing registration, and custom modules.
- **`web.config` → handlers/modules wiring** — honor `<httpHandlers>`/`<httpModules>` (and the IIS‑integrated
  `<system.webServer>`) so existing apps' handler/module registrations take effect.
- **Real providers** — SQL/Postgres/SQLite Membership/Role/Profile/session for a self‑contained cross‑platform
  story (currently default in‑memory + documented SQL stubs).
- **Code‑behind app deployment** — exercise separate code‑behind assemblies + a `bin/` of app DLLs end‑to‑end.
- **Rendering fidelity** — Menu/TreeView image‑sets, nested master pages, designer surface.

Welcome aboard — and thank you for carrying this forward. Keep it clean‑room, keep it green, keep it
cross‑platform. 🧱🐧
