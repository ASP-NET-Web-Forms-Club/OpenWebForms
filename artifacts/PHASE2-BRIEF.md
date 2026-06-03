# System.Web Reimplementation — Phase 2 Brief (hands-off)

> **For a fresh session:** read this file plus `artifacts/SUMMARY.md`, then begin at Tier 0.
> Phase 1 is complete; the public API surface is already extracted and frozen.

---

## 0. Mission & hard rules

**Mission:** fill in the implementation bodies of the `System.Web` skeleton in `src/` so that classic
ASP.NET **Web Forms** apps run on modern, cross-platform .NET (Linux included).

**Architecture — LOCKED by the user:** **FULL STANDALONE.** System.Web owns its entire runtime:
`HttpRuntime`, the `HttpApplication` pipeline, `HttpContext`, and **its own HTTP server**. It is
**completely separated from Kestrel and ASP.NET Core** — do **not** reference
`Microsoft.AspNetCore.*` or take `Microsoft.AspNetCore.Http` as a dependency anywhere. The model is
Mono/XSP-style: a managed HTTP server feeds our pipeline.

**Non-negotiable rules (tell every sub-agent):**
1. **Clean-room.** Implement from the public signatures (`artifacts/system.web.api.json`) and documented
   behavior only. **Never** copy, decompile, paraphrase, or transcribe the original System.Web IL/source.
   Behavior may be *referenced*; code is written fresh.
2. **Do NOT change public/protected signatures.** They are frozen for binary compatibility. Fill bodies
   only. If a signature looks wrong, **flag it** in a notes file — do not edit it.
3. **No LINQ.** Use explicit `for`/`foreach` loops, not `Select`/`Where`/etc. (Applies to all `src/`
   implementation code. Build-time tooling under `tools/` may keep LINQ.)
4. **Keep it compiling at all times.** Every slice must leave `src/` building green before moving on.
5. **Sub-agents run on the Opus model.**

---

## 1. Target framework & project setup (Tier 0, do first)

- Convert `src/` from the loose csc skeleton into a real SDK project: `src/System.Web.csproj`.
- **Target `net8.0`** as the floor (LTS, cross-platform). Multi-targeting `net8.0;net10.0` is fine later;
  SDK 10.0.300 is installed. Output assembly name **`System.Web`**, version `4.0.0.0` to preserve
  reference identity for downstream consumers.
- The skeleton currently compiles against **.NET Framework 4.8 reference assemblies** (see
  `tools/compile-stubs.ps1`). Moving to net8.0 means resolving System.Web's 22 external dependencies on
  modern .NET. Use `artifacts/dependencies.txt` + **SUMMARY.md §6** as the roadmap:
  - **BCL-covered:** mscorlib/System/System.Core/System.Data/System.Xml → no action.
  - **NuGet:** `System.Configuration.ConfigurationManager`, `System.Drawing.Common`,
    `System.Runtime.Caching`, `System.ComponentModel.Annotations`, `System.DirectoryServices*`,
    `System.ServiceProcess.ServiceController`, `System.Security.*`.
  - **Sister System.Web assemblies (circular):** `System.Web.Services`, `System.Web.ApplicationServices`,
    `System.Web.RegularExpressions` — for now provide **minimal internal shim types** for whatever our
    public surface touches, or extract+stub them with the Phase-1 tool. Track as a sub-workstream.
  - **Cross-platform BLOCKERS:** `System.Windows.Forms`, `System.Design`, `System.EnterpriseServices`.
    These appear only in narrow design-time/legacy-COM surface. Strategy: isolate those members behind
    our own minimal shim types or `PlatformNotSupported` bodies so the core runtime never needs the real
    desktop/COM assemblies on Linux. Do **not** let them leak into the runtime dependency graph.
  - **MSBuild (`Microsoft.Build.*`):** referenced by `System.Web.Compilation`. For cross-platform page
    compilation use **Roslyn (`Microsoft.CodeAnalysis.CSharp`)** instead of MSBuild tasks.
- Deliverable of Tier 0: `System.Web.csproj` builds on net8.0 with all bodies still `throw new
  NotImplementedException()`. (i.e., port the skeleton's *references* to modern .NET before writing logic.)

---

## 2. The standalone HTTP host (Tier 0/1)

- Build a managed HTTP server with **no ASP.NET Core**. Pragmatic primitive: `System.Net.HttpListener`
  (cross-platform in modern .NET) or, for full control, raw `System.Net.Sockets` + a managed HTTP/1.1
  parser. Put it behind an internal `HttpWorkerRequest`-style abstraction so the server is swappable.
- Wire: socket/listener → `HttpWorkerRequest` → `HttpRuntime.ProcessRequest` → `HttpApplication`
  pipeline → handler → response flush. This is the spine everything else hangs off.
- Provide a tiny sample host project (`samples/host`) that serves a Web Forms app directory, for
  end-to-end testing.

---

## 3. Implementation order (leaf-first; each tier compiles before the next)

| Tier | Namespaces / subsystems | Notes |
|------|--------------------------|-------|
| **0** | csproj on net8.0 + dependency port + HTTP host + `HttpRuntime` bootstrap | foundation |
| **1** | `System.Web.Util`, `System.Web.Caching` (`Cache`), `System.Web.Configuration` (web.config parse) | leaf utilities/config |
| **2** | core `System.Web`: `HttpContext`, `HttpRequest`, `HttpResponse`, `HttpServerUtility`, `HttpApplication`, module/handler pipeline, cookies, `HttpRuntime` | the heart |
| **3** | `System.Web.SessionState`, `System.Web.Security` (Forms auth, Membership/Role providers), `System.Web.Profile` | services |
| **4** | `System.Web.UI`: `Control`, `Page`, lifecycle, `StateBag`/ViewState (`ObjectStateFormatter`), postback, `ControlCollection` | Web Forms core |
| **5** | `System.Web.UI.HtmlControls`, `System.Web.UI.WebControls`, `WebParts`, `Adapters` | controls (largest, fan-out heavily) |
| **6** | `System.Web.Compilation`: `BuildManager`, `.aspx`/`.ascx` parser, codegen via Roslyn | makes real .aspx run |

Tier 6 can be partially pulled forward by **hand-writing** a page class to exercise Tiers 2/4 before the
parser exists.

---

## 4. Verification strategy (compensates for missing fixtures)

We have the **real .NET Framework 4.8 `System.Web`** on this Windows box. Generate our own ground truth:
1. Build a small legacy Web Forms app; host it (IIS Express, or a tiny .NET FW `HttpListener` host).
2. Capture real `__VIEWSTATE` blobs, rendered HTML, cookie/header behavior for representative pages.
3. Store under `fixtures/`. Reimplementation must reproduce these (ViewState round-trips, HTML matches).
- Add an `xUnit`/`MSTest` project `tests/` running cross-platform. Per-type unit tests for logic; golden
  fixture tests for rendering/ViewState. **ViewState compatibility** (`ObjectStateFormatter`, the
  `__VIEWSTATE` wire format + MAC via `machineKey`) is the highest-fidelity-risk area — prioritize its
  tests.

---

## 5. Recommended orchestration (multi-agent)

The work is large and parallelizable **within a tier** (types are mostly independent once the tier below
is done). Suggested pattern per tier:
- Fan out **one agent per namespace or per cohesive type-cluster**, all on **Opus**, all told the hard
  rules in §0 (esp. no-LINQ, no-signature-changes, clean-room).
- Each agent implements its slice, ensures `src/` still compiles, returns a structured report of what it
  implemented / what it stubbed / signatures it thinks are suspect.
- A barrier after each tier: integrate, compile green, run that tier's tests, then proceed.
- Use the spec (`system.web.api.json`) as the per-type checklist so nothing is missed.
- This only counts as opt-in multi-agent orchestration — drive it with the Workflow tool or sequential
  Agent calls as appropriate.

---

## 6. Definition of done per slice

- All targeted members have real bodies (no `NotImplementedException`) **or** an explicit, justified
  `PlatformNotSupported`/documented-stub with a TODO.
- `src/` compiles green on net8.0.
- No LINQ introduced.
- No public/protected signature changed (diff against `system.web.api.json`).
- Tests for the slice pass (unit + any applicable fixtures).
- A short note appended to `artifacts/PHASE2-LOG.md`: what was implemented, what was deferred, suspect
  signatures.

---

## 7. Quick orientation for the cold-start session

- Spec / inventory: `artifacts/system.web.api.json` (1,402 types, 13,796 members).
- Skeleton to fill: `src/*.cs` (one file per namespace).
- Phase-1 report & dependency roadmap: `artifacts/SUMMARY.md`.
- Reusable metadata extractor (for sister assemblies): `tools/extract-api/`.
- Reference identity to preserve: `System.Web` **4.0.0.0**, PKT **b03f5f7f11d50a3a**.
- Real FW assemblies on this machine for fixture generation:
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.dll`.
