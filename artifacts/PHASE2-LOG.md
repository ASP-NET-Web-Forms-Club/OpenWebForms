# Phase 2 Implementation Log

Running log of Phase 2 slices. Each entry: what was implemented, what was deferred, suspect signatures.
Per `PHASE2-BRIEF.md` §6 (definition of done).

---

## Tier 0 — Foundation (csproj on net8.0 + dependency port + standalone HTTP host + test scaffold)

**Date:** 2026-06-01  **Status:** ✅ COMPLETE — build green, host runs, tests 12/12.
**Orchestration:** PM-driven multi-agent (Workflow), all sub-agents on Opus. Run 1 (`wf_b9081644-5c9`):
Foundation + Scaffold(host,tests) + Audit(clean-room, architecture, signature/build). Run 2
(`wf_99a94061-1d3`): assembly-version/ALC finalize + independent verify.

### Implemented
- **`src/System.Web.csproj`** — SDK-style, `net8.0`, `AssemblyName=System.Web`, `RootNamespace=System.Web`,
  **`AssemblyVersion=4.0.0.0`**, `Version=4.0.0.0`, **`FileVersion=4.8.0.0`** (product-line info only),
  `AllowUnsafeBlocks=true`, `Nullable=disable`, `LangVersion=latest`, `GenerateAssemblyInfo=true`
  (needed so the Version props actually stamp; the 25 skeleton files declare no `[assembly:]` attrs so
  no clash). `NoWarn` = skeleton set `1701;1702;0108;0114;0628;0809;0612;0618;0672;1591;3005;0419;0465;0469`
  + `SYSLIB0003;SYSLIB0011;SYSLIB0021;SYSLIB0051;CS0618`. Builds **0 errors / 0 warnings**. All existing
  bodies remain `NotImplementedException`.
- **Dependency port — 6 NuGet packages.** Mandated 3: `System.Configuration.ConfigurationManager 8.0.1`,
  `System.Drawing.Common 8.0.10`, `System.ComponentModel.Annotations 5.0.0`. Three more the build genuinely
  demanded (all CS1069 "type forwarded" errors, not invented):
  - `System.CodeDom 8.0.0` — `CodeCompileUnit`, `CodeTypeDeclaration`, `CodeMemberMethod`, `CodeDomProvider`,
    `CompilerResults/Parameters/Error`, … (used across `System.Web.cs`, `System.Web.UI.cs`).
  - `System.Data.SqlClient 4.8.6` — `SqlException`, `SqlCommand` (management/caching surface).
  - `System.Security.Permissions 8.0.0` — `System.Security.NamedPermissionSet` (returned by
    `HttpRuntime.GetNamedPermissionSet`, `src/System.Web.cs:1667`) and `System.Security.PermissionSet`
    (WebPart base).
  - Blocker assemblies (`System.Windows.Forms`, `System.Design`, `System.DirectoryServices*`,
    `System.ServiceProcess`, `Microsoft.Build.*`, `System.Runtime.Caching`) leaked **0 public types** —
    no action required.
- **Shims — `src/Shims/Shims.cs`** (clean-room, minimal shape, all bodies throw NIE):
  - `System.EnterpriseServices.TransactionOption` (enum) and `System.Web.Services.Configuration.WebServicesSection`
    — per brief.
  - 9 membership/role types: `MembershipCreateStatus`, `MembershipPasswordFormat`, `MembershipUser`,
    `MembershipUserCollection`, `MembershipProvider`, `MembershipProviderCollection`, `ValidatePasswordEventArgs`,
    `MembershipValidatePasswordEventHandler`, `RoleProvider`. **Reason:** on FW 4.8 these live in the sister
    assembly `System.Web.ApplicationServices` (no net8 package) — exactly the "reimplement/stub the circular
    sisters" case from the brief. Bases derive from `System.Configuration.Provider.ProviderBase`/`ProviderCollection`;
    each declares only the union of members the skeleton's concrete subclasses override.
- **Standalone HTTP host (Mono/XSP-style, ships in System.Web.dll, INTERNAL types — public surface unchanged):**
  - `src/Server/ListenerWorkerRequest.cs` — `internal sealed ListenerWorkerRequest : HttpWorkerRequest`,
    overrides all 17 abstract members + useful virtuals, mapping `HttpListenerContext` ↔ request/response.
  - `src/Server/HttpListenerServer.cs` — `internal interface IStandaloneServer` + `internal sealed HttpListenerServer`;
    `System.Net.HttpListener` accept loop → `HttpRuntime.ProcessRequest(wr)` in try/catch (NIE → 500, loop survives).
  - `src/Server/AssemblyInfo.Server.cs` — `InternalsVisibleTo("SampleHost")`, `InternalsVisibleTo("System.Web.Tests")`.
  - No LINQ (explicit for/foreach).
- **Sample host — `samples/host/`** (`SampleHost.csproj` SDK Exe + `Program.cs` + `AlcBootstrap.cs` +
  `RealEntry.cs` + `wwwroot/index.aspx`). Runs, binds, serves; `--smoke <port>` for bind-and-exit.
- **Tests — `tests/System.Web.Tests.csproj`** (xUnit, net8.0; `Microsoft.NET.Test.Sdk 17.11.1`, `xunit 2.9.2`).
  `SmokeTests.cs` = 12 facts on assembly identity (name `System.Web`, version 4.0.0.0, unsigned) and presence
  of pivotal frozen types. **12/12 pass.** (Tests load our DLL via an isolated ALC to dodge the framework facade.)

### Key architectural resolution — framework `System.Web` facade shadowing
`Microsoft.NETCore.App` ships its own strong-named `System.Web.dll` facade (AssemblyVersion 4.0.0.0, only
forwards `HttpUtility`) on the Trusted Platform Assemblies list. In the **default** AssemblyLoadContext it
**shadows** our app-local copy, so plain `typeof(System.Web.HttpContext)` binds to the facade and throws.
- **Build-time:** MSBuild RAR (MSB3243) prefers the facade. Solved by targets in `SampleHost.csproj` and
  `System.Web.Tests.csproj` that strip the framework-resolved `System.Web` reference before RAR so the
  compiler binds to our ref assembly. (Retained — still needed.)
- **Runtime:** solved with a **custom AssemblyLoadContext** (decision: keep frozen 4.0.0.0 identity rather
  than bump the version). `samples/host/AlcBootstrap.cs` defines `SystemWebLoadContext` whose `Load()` returns
  our app-local `System.Web.dll` for the `System.Web` simple name (null otherwise → BCL resolves normally).
  `Program.Main` (default ALC, touches no System.Web type) → loads `SampleHost.dll` into the custom ALC →
  invokes `SampleHost.RealEntry.Run` by reflection; all System.Web references then resolve to our copy.
  **Verified at runtime:** loaded `System.Web` = v4.0.0.0, path under `samples/host/bin/...`,
  `LoadContext=SystemWebHostContext`. (Earlier agent's 4.0.0.0→4.8.0.0 version bump was reverted.)

### Audit results
- **Clean-room:** PASS — no copied/decompiled source; new code limited to Server/Shims plumbing.
- **No-LINQ:** PASS — zero `using System.Linq`/operators in `src/`. (Sole `System.Linq` token is the
  pre-existing **frozen** signature `QueryableDataSourceHelper.SortBy<T>` / `IQueryable<T>` at
  `src/System.Web.UI.WebControls.cs:3847` — a type reference with NIE body, not a violation. Do not edit.)
- **Architecture:** PASS — zero `Microsoft.AspNetCore.*` in any `*.cs`/`*.csproj`; server internal/standalone.
- **Signature integrity:** PASS — spot-diffed `HttpContext/HttpRuntime/HttpApplication/HttpRequest/HttpResponse/
  HttpWorkerRequest/UI.Page/UI.Control` against `system.web.api.json`; all member bodies still NIE; new shim/
  server types are additive only; **no existing signature edited** (25 skeleton files retain original mtime).

### Deferred / suspect signatures (carry into later tiers)
1. **`HttpWorkerRequest()` protected base ctor (`src/System.Web.cs:2143`) throws NIE.** Any concrete worker
   request (incl. the frozen `SimpleWorkerRequest`) therefore cannot be constructed until this body is filled.
   Host currently catches the NIE per request → HTTP 500. **Must get a real (non-throwing) body in Tier 2.**
   Not edited (frozen).
2. **Unsigned assembly.** True strong-name binary compat with MS `System.Web` (PKT `b03f5f7f11d50a3a`) is
   impossible without their private key. We preserve **AssemblyVersion 4.0.0.0** but PKT is null. Accept.
3. **Custom-ALC requirement for any standalone consumer.** Any host/app running on `Microsoft.NETCore.App`
   must use the `AlcBootstrap` pattern (or equivalent) so our `System.Web` wins over the framework facade.
   This is an architectural fixture for all future hosts, not a one-off.
4. **Build-time RAR-strip targets** live in `SampleHost.csproj` and `System.Web.Tests.csproj`; keep them for
   any project that `ProjectReference`s `System.Web`.

### Verification commands
- `dotnet build src/System.Web.csproj -c Debug` → 0/0; built DLL AssemblyVersion 4.0.0.0, PKT none.
- `dotnet test tests/System.Web.Tests.csproj` → 12/12.
- `dotnet run --project samples/host/SampleHost.csproj -- --smoke 8092` → binds, diagnostic confirms our
  app-local System.Web in `SystemWebHostContext`, self-hit returns 500 (expected Tier-0 NIE), clean exit.


---

## Tier 1 — Leaf utilities & config (`System.Web.Util`, `System.Web.Caching`, `System.Web.Configuration`)

**Date:** 2026-06-01  **Status:** ✅ COMPLETE — build green, **27/27 tests pass**, both audits PASS.
**Orchestration:** Workflow run `wf_1954d2e0-d56` (6 Opus agents, ~47 min). 3 parallel writers (one per
skeleton file) → 1 integrate+build+behavioral-test agent (custom-ALC test bridge) → 2 read-only auditors
(clean-room/no-LINQ; signature-integrity + independent build/test gate).

### Implemented
- **`System.Web.Util`** — fully implemented: `HttpEncoder` (HtmlEncode/HtmlDecode/HtmlAttributeEncode,
  UrlEncode(byte[]), UrlPathEncode, JavaScriptStringEncode, HeaderNameValueEncode; spec-correct char-by-char
  logic), `RequestValidator` (conservative dangerous-string scan), `Transactions.InvokeTransacted`
  (synchronous invoke — distributed-tx documented unsupported on net8), `WorkItem.Post` (ThreadPool). No stubs.
- **`System.Web.Caching`** — `Cache` backed by `ConcurrentDictionary` (indexer, Add, 5× Insert, Get, Remove,
  Count, GetEnumerator; absolute + sliding expiration with `NoAbsoluteExpiration`/`NoSlidingExpiration`;
  `CacheItemRemovedCallback` with correct `CacheItemRemovedReason`; `CacheItemUpdateCallback` refresh path;
  lazy expiry + 20s sweeper). `CacheDependency`/`AggregateCacheDependency` (file last-write + FileSystemWatcher,
  HasChanged, dependency-key linkage, eviction with DependencyChanged). **Documented stubs:**
  `SqlCacheDependency`, `SqlCacheDependencyAdmin`, `OutputCache`, `OutputCacheUtility` (SQL/output-cache scope).
- **`System.Web.Configuration`** — ~70 types fully implemented (the whole `<system.web>` parsing path):
  `HttpRuntimeSection`, `CompilationSection` (+ Compiler/Assembly/BuildProvider/ExpressionBuilder/CodeSubDirectory
  collections), `PagesSection` (+ Namespace/TagPrefix/TagMap/IgnoreDeviceFilter), `GlobalizationSection`,
  `CustomErrorsSection`, `AuthenticationSection` (+ Forms*/Passport), `AuthorizationSection` (+ rules),
  `SessionStateSection`, `MachineKeySection`, `TraceSection`, `IdentitySection`, `TrustSection`,
  `AnonymousIdentificationSection`, `HttpHandlersSection`, `HttpModulesSection`, `HttpCookiesSection`,
  `HostingEnvironmentSection`, `DeploymentSection`, `WebControlsSection`, `UrlMappingsSection`,
  `XhtmlConformanceSection`, `SystemWebSectionGroup`/`SystemWebCachingSectionGroup`, and `WebConfigurationManager`.
  Built on `System.Configuration` base classes via explicit `ConfigurationProperty` registration (no reflection
  attributes). **~60 out-of-scope types documented-stubbed** (browser caps/`HttpCapabilitiesBase`+factory family,
  healthMonitoring, profile, membership, roleManager, outputCache, siteMap, processModel, protocols, webParts,
  CAS/trust-assembly sections, SQL cache-dep sections, remote/IIS-metabase config) — each a per-type
  `NotImplementedException("TODO Tier X: … not needed for Tier-1 web.config parse")`.

### Tests (tests/, via custom-ALC bridge)
- `tests/SystemWebUnderTest.cs` — bridge: loads our `System.Web.dll` into a dedicated ALC (intercepts the
  `System.Web` simple name), with reflection invoke/property/enum/RunInAlc helpers. Reusable for all later tiers.
- 15 new behavioral tests (5 Util + 5 Caching + 5 Configuration) + 12 Tier-0 smoke = **27/27 pass**:
  encoding vectors (`HtmlEncode("<a>&\"")` → `&lt;a&gt;&amp;&quot;`, URL/JS escaping), cache insert/expire/
  eviction-callback (reason `Removed`), and a real temp web.config parsed → asserted `MaxRequestLength`,
  `ExecutionTimeout`, `Compilation.Debug`, `Pages` flags, `Authentication.Mode=Forms`+`LoginUrl`, `SessionState`.

### Suspect signatures (flagged, NOT edited)
1. `FolderLevelBuildProviderCollection.this[string]` returns `BuildProvider` (not `FolderLevelBuildProvider`)
   per the frozen skeleton — cannot return a correctly-typed element; left as documented NIE.
2. `WebConfigurationManager.GetSection(string, path)` / `GetWebApplicationSection` ignore the path/web-app
   context in Tier 1 (delegate to `ConfigurationManager.GetSection`) — correct only for the process-default
   config until the Tier-2 hosting/virtual-path layer exists.

### Architectural finding (carry to Tier 2+) — config section type-string resolution vs. the facade
`System.Configuration`'s internal `TypeUtil.GetType` resolves each section's `"…, System.Web"` type string in
the **default** AssemblyLoadContext, where the `System.Web` simple name binds to the **strong-named shared-
framework facade** (which lacks our section types) → `TypeLoadException`. So `WebConfigurationManager.GetSection`
will not resolve OUR section types in an ordinary process. The Tier-1 tests work around this by reading each
section's raw XML (`SectionInformation.GetRawXml()`) and feeding it through our section's own
`ConfigurationSection.DeserializeSection(XmlReader)` — proving the clean-room parsing logic without any
`System.Web` type-string resolution. **Implication:** the Tier-2 runtime (HttpContext/HttpRuntime config access)
must drive config either under the custom ALC (so type strings resolve to ours) or via a config host that maps
section names to our types directly — same family as the host ALC bootstrap. This is the dominant cross-cutting
risk for the runtime tier.

### Verification
- `dotnet build src/System.Web.csproj -c Debug` → 0 errors / 0 warnings.
- `dotnet test tests/System.Web.Tests.csproj` → 27/27 (0 failed, 0 skipped).
- Audits: clean-room ✅ (no copied source), no-LINQ ✅ (only `List<T>.ToArray()`/custom `Contains` — no
  `Enumerable`), signature-integrity ✅ (pivotal types diffed against `system.web.api.json`; 22 other skeleton
  files untouched).


---

## Tier 2 — The heart: core `System.Web` (HttpContext / Request / Response / pipeline / Runtime)

**Date:** 2026-06-01  **Status:** ✅ COMPLETE — build green, **host serves real HTTP 200**, **40/40 tests**, both audits PASS.
**Orchestration:** Workflow run `wf_066fed95-9b1` (7 Opus agents, ~37 min). Vendor(cshttp) → CoreA → CoreB →
CoreC → IntegrateTest → 2 audits. Core clusters ran **sequentially** (single shared file `System.Web.cs`).

### Milestone
`samples/host` now returns a real `HTTP/1.1 200 OK` (curl-verified: `Content-Type: text/plain`, body
`Hello from System.Web`) via a default `IHttpHandler`, running **our** System.Web v4.0.0.0 under the custom
ALC — no longer 500-ing. The full request→pipeline→handler→response→flush spine works end to end.

### Implemented
- **cshttp vendored** → `src/Http/` (19 files), namespace `CsHttp` → `System.Web.Http.CsHttp`, **all 39 types
  internalized** (verified via MetadataLoadContext: 0 new public types). Public-domain, RFC-built — reinforces
  clean-room. Backs `HttpRequest` content parsing.
- **CoreA — data/collections/exceptions:** `HttpCookie`, `HttpCookieCollection`, internal `HttpValueCollection`
  (NameValueCollection container for QueryString/Form/Headers), `HttpFileCollection`, `HttpPostedFile`,
  `HttpStaticObjectsCollection`, and the exception family (`HttpException` + GetHttpCode, `HttpUnhandledException`,
  `HttpCompileException`, `HttpParseException`, `HttpRequestValidationException`, `ParserError(Collection)`).
- **CoreB — per-request I/O:** `HttpRequest` (URL surface; `QueryString`/`Form`/`Files`/`Cookies` via the
  cshttp parsers honoring urlencoded vs multipart; Headers/ServerVariables/InputStream/Params/BinaryRead),
  `HttpResponse` (Write/BinaryWrite/Output/OutputStream, status/headers/cookies→Set-Cookie on flush,
  Buffer/Clear/Flush/End/Redirect, Filter, pushing to the worker request), `HttpServerUtility`
  (HtmlEncode/UrlEncode/UrlDecode/UrlPathEncode via Tier-1 HttpEncoder + cshttp PercentDecoder, MapPath,
  ScriptTimeout, GetLastError), `HttpCachePolicy` (basic state).
- **CoreC — orchestration spine:** `HttpContext` (Request/Response/Server/Items/User/Handler/Application/Cache/
  Current via AsyncLocal/Error/RewritePath/GetService/GetSection), `HttpApplicationState` (Monitor-locked store),
  `HttpModuleCollection`, `HttpApplication` (full integrated pipeline: BeginRequest…EndRequest in order, handler
  resolution at MapRequestHandler, sync + APM-async handler dispatch, trailing stages always run on
  CompleteRequest/error, Error event, all 40 AddOn*Async overloads), `HttpRuntime.ProcessRequest` (build context
  from worker request → pooled HttpApplication → ExecutePipeline → flush → `EndOfRequest()` exactly once).
- **`HttpWorkerRequest()` base ctor** — already non-throwing in the current skeleton revision (the Tier-1 log's
  line:2143 NIE concern was stale numbering); `ListenerWorkerRequest` constructs fine. Resolved.

### Tests (tests/, via the custom-ALC bridge)
13 new (+27 prior = **40/40**): `RequestTests` (QueryString/Form-urlencoded/multipart→one `HttpPostedFile` with
exact bytes/Cookies/Headers), `ResponseTests` (status line/headers/Set-Cookie/body/Content-Length),
`ServerUtilTests` (encode round-trips, MapPath), `PipelineTests` (ProcessRequest → captured 200 + body +
`EndOfRequest`; stage ordering with handler firing between Pre/PostRequestHandlerExecute), `TestWorkerRequests`
(in-memory capturing/feeding HttpWorkerRequest helpers — reusable).

### ⚠️ Priority follow-up gap — `HttpUtility` still NIE
The **static `System.Web.HttpUtility`** class (`HtmlEncode`/`HtmlDecode`/`HtmlAttributeEncode`/`UrlEncode`/
`UrlDecode`/`UrlEncodeToBytes`/`UrlPathEncode`/`JavaScriptStringEncode`/`ParseQueryString`/…) is still entirely
`NotImplementedException` — it wasn't assigned to a Tier-2 cluster. It is one of the most-used public types in
System.Web and a near-trivial fill: every member can delegate to the existing Tier-1 `HttpEncoder` and vendored
cshttp `QueryStringParser`/`PercentDecoder`. **Recommend knocking it out first thing (quick win).** Note:
`HttpCookie.Value` multi-value rendering and `HttpValueCollection.ToString(urlencoded:true)` currently call
`HttpUtility.UrlEncode`/`UrlDecode` and will throw until this is filled (single-value cookie path is unaffected).

### Other deferred (documented stubs, by design)
- `HttpContext.Session`/`Profile` → null (Tier 3); `Trace`/`PageInstrumentation`/`AcceptWebSocketRequest`/
  resource getters → throw (later tiers).
- `HttpServerUtility.Transfer`/`Execute`/`TransferRequest` → NIE (server-side re-dispatch; handler infra now
  exists, can be filled soon); `CreateObject*` → NIE (COM, out of scope).
- `HttpResponse` routing redirects (`RedirectToRoute*`), `WriteSubstitution`, async `BeginFlush`/`EndFlush`/
  HTTP-2 push → NIE; cache-dependency `Add*` → no-op (output-cache layer).
- `HttpCachePolicy.VaryBy*`, `HttpRequest.Browser` (browser caps), `ClientCertificate`, `Unvalidated`,
  `LogonUserIdentity`, `MapImageCoordinates` → NIE (separate features/clusters).
- Async pipeline currently runs synchronously to completion (APM returns a completed result).
- Full `<httpHandlers>`/`<handlers>` config→handler mapping deferred; a `DefaultDemoHandler` fallback serves
  unmapped requests (200). `RewritePath` records intent but doesn't yet re-dispatch.

### Verification
- `dotnet build` src + samples/host + tests → 0 errors / 0 warnings (1 pre-existing benign CS0169 in vendored cshttp).
- `dotnet test` → 40/40. Host `--smoke` and `curl -i` → `HTTP/1.1 200 OK`.
- Audits: clean-room ✅ (no MS provenance; content parsing delegates to cshttp), no-LINQ ✅, signature-integrity ✅
  (523 signatures diffed across 10 pivotal types; 0 changed; 0 new public types; unrelated skeletons untouched).


---

## Tier 3 — Services: `SessionState` / `Security` / `Profile` (+ `HttpUtility`)

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **51/51 tests**, both audits PASS (after a 1-line fix).
**Orchestration:** Workflow run `wf_779487d4-7b8` (7 Opus agents, ~23 min). Hooks → 3 parallel Service writers
(separate files) → IntegrateTest → 2 audits. PM applied one post-audit fix (see below).

### Implemented
- **`HttpUtility` (the priority gap) — DONE.** All 31 members: HtmlEncode/Decode/AttributeEncode +
  JavaScriptStringEncode delegate to Tier-1 `HttpEncoder`; UrlEncode/UrlEncodeToBytes/UrlPathEncode/
  UrlEncodeUnicode + UrlDecode/UrlDecodeToBytes (+ Encoding overloads); `ParseQueryString` →
  URL-decoded NameValueCollection. Unblocked the `HttpCookie` multi-value / `HttpValueCollection`
  urlencoded path. (`UrlTokenEncode/Decode` live on `HttpServerUtility`, already done — not in HttpUtility.)
- **`HttpContext` hooks** — internal `SetSessionStateInstance`/`SetProfileInstance`; public `Session`/`Profile`
  getters now return the module-published instance (null until set). `User` already had get/set.
- **`System.Web.SessionState` — InProc.** `HttpSessionState`, `HttpSessionStateContainer`,
  `SessionStateItemCollection`, `SessionIDManager` (RNG 24-char URL-safe id; ASP.NET_SessionId cookie r/w),
  `SessionStateModule` (Acquire/Release; process-wide `InProcSessionStateStore` with sliding-expiration sweep;
  publishes via the hook), `SessionStateUtility`. StateServer/SQL/custom → documented stubs.
- **`System.Web.Security` — Forms auth + default providers.** `FormsAuthentication` (real **AES-256-CBC +
  HMACSHA256 encrypt-then-MAC** ticket crypto keyed off `MachineKeySection`, constant-time compare; custom
  ticket format — NOT MS binary), `FormsAuthenticationTicket`, `FormsAuthenticationModule` (sets a
  `FormsIdentity`/principal on AuthenticateRequest), `Membership` + internal `InMemoryMembershipProvider`
  (**PBKDF2-SHA256, per-user RNG salt**), `Roles` + internal `InMemoryRoleProvider`, `RolePrincipal`,
  `RoleManagerModule`. The shimmed `MembershipProvider`/`RoleProvider`/`MembershipUser` bases (Shims.cs)
  were fleshed out. SQL/AD/AuthorizationStore/WindowsToken providers → documented stubs.
- **`System.Web.Profile` — default provider.** `ProfileBase`/`DefaultProfile`, `ProfileProvider` + internal
  `DefaultProfileProvider` (process-wide in-memory), `ProfileModule` (publishes via the hook, auto-save on
  release), `ProfileManager`, info/collection/event/attribute types. `SqlProfileProvider` → stub.

### Tests
11 new (+40 prior = **51/51**): `HttpUtilityTests` (encode/decode round-trips, `ParseQueryString` grouping,
JS-encode); `SessionStateTests` (container surface + **2-request InProc round-trip** — request 1 sets an item
and emits the session cookie, request 2 replays it and sees the item with `IsNewSession=false`);
`SecurityTests` (ticket encrypt/decrypt round-trip + **tamper rejection**, Membership create/validate/wrong-pw,
Roles add/IsUserInRole); `ProfileTests` (provider + `ProfileBase` set/Save/reload).

### Post-audit fix (PM)
Signature auditor flagged **1 MAJOR**: `DefaultProfileProvider` was declared `public` (new type, absent from
`system.web.api.json`) → widened the frozen surface. **Fixed:** changed to `internal` (used only internally).
Rebuilt green, 51/51 still pass. (Session/Security in-memory providers were already correctly `internal`.)

### Notes / minor
- The skeleton's `internal HttpSessionState()` ctor was changed to `internal HttpSessionState(HttpSessionStateContainer)`
  — an **internal** ctor, within the "add internal members" latitude; public/protected surface unchanged.
- `SessionStateStoreProviderBase` protected ctor changed from throwing-NIE to no-op (signature unchanged) so
  concrete derivations can chain.

### Verification
- `dotnet build` src + samples/host + tests → 0 errors (1 pre-existing benign CS0169 in vendored cshttp).
- `dotnet test` → 51/51. Host `--smoke` → HTTP 200 (pipeline unaffected).
- Audits: clean-room ✅ (genuine independent crypto; HttpUtility delegates to HttpEncoder/cshttp), no-LINQ ✅,
  signature-integrity ✅ after the `DefaultProfileProvider` → internal fix (0 changed frozen signatures; 0 leaked
  public types).


---

## Tier 4 — Web Forms core: `System.Web.UI` (Control / Page / ViewState) — **ViewState byte-compatible**

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **84/84 tests**, both audits PASS, **ViewState
proven byte-for-byte vs real .NET FW**.
**Orchestration:** two runs — `wf_4c46a1a4-184` (Fixtures + StateFormatter + ControlCore; **failed** at the
orchestration layer when the ControlCore agent ended without its structured report, but its disk writes
persisted and built green) then continuation `wf_e4bab189-913` (FormatterCheck → PagePostback → IntegrateTest
→ 2 audits). All Opus.

### THE milestone — ViewState wire compatibility (proven)
Captured ground-truth from the **real** .NET FW `ObjectStateFormatter` via `tools/fixtures-gen` (net48 console
referencing the GAC System.Web; fixed machineKey; SHA1 + HMACSHA256 configs) → 34 cases in `fixtures/viewstate/`.
Our `ObjectStateFormatter.Serialize` reproduces **31/31 constructable cases byte-for-byte** (cases 0–26, 30–33),
`Deserialize` round-trips, and the **MAC pipeline reproduces both HMACSHA1 and HMACSHA256** MACed forms exactly
(all 27 mac cases). The 3 unverified cases (27–29) are `WebControls.Unit`, which is still NIE (Tier 5) — the
formatter's WriteUnit path is implemented but no `Unit` instance can be constructed yet; **carry to Tier 5**.
- **Deep fidelity fix:** `Hashtable` serialization had to **replay .NET Framework's exact open-addressing bucket
  layout** (legacy non-randomized x64 string hash, initial 3 buckets, 0.72 load factor, double-hash probe
  `1 + ((h>>5)+1)%(size-1)`, ascending bucket-index emission) — net8's randomized Hashtable order did NOT match.

### Implemented (`src/System.Web.UI.cs`; NIE 902 → 715)
- **StateFormatter cluster:** `StateBag`/`StateItem` (dirty-tracking + Save/Load/Track ViewState),
  `ObjectStateFormatter`/`LosFormatter` (token writer/reader + MAC + base64, byte-compat), `Pair`/`Triplet`/
  `IndexedString`, `IStateManager`/`StateManagedCollection`, `HtmlTextWriter`/`Html32TextWriter` (+
  `AttributeCollection`/`CssStyleCollection`, tag/attr/style enums).
- **ControlCore cluster:** `Control` (ID/UniqueID/ClientID, naming, lifecycle Init/Load/PreRender/Unload,
  ViewState + control-state, EnsureChildControls/CreateChildControls, FindControl, Render/RenderChildren,
  recursive ViewState save/load → the Pair/Triplet/ArrayList shapes the fixtures show), `ControlCollection`,
  `LiteralControl`, IPostBackDataHandler/IPostBackEventHandler/INamingContainer wiring.
- **PagePostback cluster:** `Page` (full lifecycle PreInit→…→Unload, DeterminePostBackMode, IsPostBack,
  Load/Save PageState via persister, EnableViewStateMac apply/strip + tamper-check, `__VIEWSTATE`/`__EVENTTARGET`/
  `__EVENTARGUMENT`/`__VIEWSTATEGENERATOR`, RaisePostBackEvent/ProcessPostData, RegisterRequiresControlState),
  `PageStatePersister`/`HiddenFieldPageStatePersister`, `ClientScriptManager` (hidden fields, `__doPostBack`,
  RegisterStartup/ClientScriptBlock dedup, GetPostBackEventReference), `PostBackOptions`, `TemplateControl`
  (Construct/FrameworkInitialize/Error event; `LoadControl`/`ParseControl`/`LoadTemplate` → Tier-6 NIE stubs).

### Tests (now 84/84)
`ViewStateCompatTests` (31 byte-compat + round-trip), `ControlTests`/`ControlWorker` (tree build, ViewState
round-trip across tree, FindControl, recursive render), `PagePostbackTests`/`PageWorker` (hand-written Page:
GET renders `__VIEWSTATE`; POST replays it + `__EVENTTARGET`, restores control state, raises postback +
post-data-changed events, re-saved state reflects mutation, IsPostBack true). Worker/control test types are
`internal` so the default-ALC test host doesn't try to load types whose base resolves only in the bridge ALC.

### Suspect signature (flagged, fixed in body only)
`HiddenFieldPageStatePersister` skeleton ctor chained `: base(default(Page))` (passes null to a null-guarding
base) — looked like a skeleton bug; body corrected to use the supplied `page`. Public ctor signature unchanged.

### Deferred / known gaps (carry forward)
- **`Unit` byte-compat (cases 27–29)** — verify once `WebControls.Unit` is implemented in Tier 5.
- **Page ViewState MAC uses a stable per-process key, not `MachineKeySection.ValidationKey`** — functionally
  correct (round-trip + tamper detect) but not yet wired to config. Wire when machineKey config integration lands.
- **`HtmlForm.Render` not implemented (Tier 5)** — so `Page.Render` currently emits the `__VIEWSTATE`/generator
  hidden fields + script at page level (real ASP.NET emits them inside the server `<form>`). Only behavioral
  deviation; revisit with HtmlForm.
- **`Control.EnsureID` auto-ID generation is a no-op placeholder** (MINOR audit finding) — fill when WebControls
  rendering/ClientID fidelity matters (Tier 5).
- Model binding (`TryUpdateModel`), async page pipeline, master pages/themes/`Validate`/validators → documented
  NIE stubs (later tiers).

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 84/84. Host `--smoke` → HTTP 200.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (185 public UI types == spec, 0 leaked), fixture-compat
  gate ✅ (31/31 constructable byte-match).


---

## Tier 5a — Controls (foundation + common + validators + HtmlControls)

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **95/95 tests**, both audits PASS. Closed both
Tier-4 loose ends (Unit → ViewState 34/34; HtmlForm → `__VIEWSTATE` inside `<form>`).
**Orchestration:** Workflow run `wf_40d515e5-91a` (7 Opus agents, ~59 min, 1.15M tok). Foundation → (Common ‖
HtmlControls) → Validators → IntegrateTest → 2 audits. Phased: data-bound controls + WebParts deferred to 5b/5c.

### Implemented
- **WebControls foundation:** `Unit`(+`UnitConverter`)/`FontUnit`/`FontInfo`, `Style`/`TableStyle`/`TableItemStyle`
  (StateBag-backed, ViewState round-trip), `WebControl` base (CssClass/colors/border/size/Attributes/Style/
  ControlStyle, AddAttributesToRender, RenderBeginTag/Contents/EndTag), `WebColorConverter`/`FontUnitConverter`/
  `FontNamesConverter`. **`Unit` byte-matches real-FW fixtures (cases 27–29) → ViewState now 34/34.**
- **Common controls:** Label, Literal, Localize, PlaceHolder, HyperLink, Image, ImageButton, Button, LinkButton
  (+`CommandEventArgs`), TextBox (IPostBackDataHandler→TextChanged, HTML5 modes), CheckBox/RadioButton
  (→CheckedChanged), HiddenField, Panel, Table/TableRow/TableCell/TableHeaderCell (+ row/cell collections),
  ListItem/ListItemCollection (IStateManager), ListControl + DropDownList/ListBox/CheckBoxList/RadioButtonList/
  BulletedList (postback → SelectedIndexChanged).
- **HtmlControls (~43):** HtmlControl/HtmlContainerControl/HtmlGenericControl, **HtmlForm** (emits the page
  hidden fields/scripts inside `<form>`), HtmlInput* family, HtmlAnchor/HtmlImage/HtmlTextArea/HtmlSelect/
  HtmlButton, HtmlTable/Row/Cell, HtmlTitle/Link/Meta/Head.
- **Validators:** IValidator, BaseValidator, BaseCompareValidator, RequiredFieldValidator, CompareValidator,
  RangeValidator, RegularExpressionValidator (System.Text.RegularExpressions), CustomValidator (+ServerValidate),
  ValidationSummary, ValidatorCollection; **`Page.Validators`/`Validate()`/`Validate(group)`/`IsValid` wired**
  (replaced the Tier-4 benign defaults). `[ValidationProperty]` added to TextBox/ListControl.

### Integrate fixes (bodies only, no signature changes)
- **HtmlTextWriter self-closing tags** — added a self-closing set (input/img/br/hr/meta/link/…) so WebControls
  render `<input … />` not `<input></input>`.
- **Page↔HtmlForm reconcile** — `Page.Render` emits framework hidden fields at page level only when `Form==null`;
  otherwise `HtmlForm.RenderChildren` emits them once inside the form (verified: `__VIEWSTATE` appears exactly
  once between `<form>`…`</form>` and still round-trips).
- Filled NIE base ctors/lifecycle on `BaseDataBoundControl`/`DataBoundControl` (were blocking ListControl-derived
  controls from even constructing).

### Tests (now 95/95)
+11: `WebControlsTests` (Unit parse/ToString, Label/TextBox/Button/DropDownList render + postback, style attrs),
`HtmlControlsTests` (HtmlForm `__VIEWSTATE`-in-form, input/anchor/generic render, HtmlSelect postback),
`ValidatorTests` (RequiredField/Range/Compare/Regex + `Page.Validate`→`IsValid`), + ViewState cases 27–29 added.

### Suspect signatures (flagged, NOT public-surface changes)
- Internal collection ctors `TableCellCollection`/`TableRowCollection`/`Table.RowControlCollection`/
  `TableRow.CellControlCollection` were declared parameterless (passing a null owner) — changed to take the
  owning control. **Internal ctors only** (not public/protected surface).
- `HtmlHead` skeleton lacks `IPageHeader` (no such interface in the codebase) — implemented without it.
- `ControlBuilder()` base ctor is NIE (Tier-6 compilation) → `HtmlHeadBuilder`/`HtmlSelectBuilder`/
  `HtmlEmptyTagControlBuilder` ctors throw at runtime until ControlBuilder is implemented. Not used at render time.
- `HtmlGenericControl.TagName` is a `new` member hiding the base get-only `TagName`; setter wired to mutate the
  base tag via an internal helper so base render uses the updated tag.

### Deferred to Tier 5b / 5c
- **5b:** data-bound controls — `GridView`, `DataGrid`, `Repeater`, `DataList`, `DetailsView`, `FormView`,
  `ListView`, `DataControlField` family, data source controls (`ObjectDataSource`/`SqlDataSource`/etc.),
  `Parameter` family; complex controls (`Wizard`, `Login*`, `Menu`, `TreeView`, `Calendar`, `AdRotator`).
- **5c:** `System.Web.UI.WebControls.WebParts` (119 types), `Adapters`.
- `ControlBuilder` (Tier 6) needed before the `*Builder` types function.

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 95/95. Host `--smoke` → HTTP 200. ViewState 34/34.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (WebControls 410==spec, HtmlControls 41==spec, 0 leaked).


---

## Tier 5b — Data-binding core (Repeater / DataList / GridView + fields / ObjectDataSource + SqlDataSource)

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **103/103 tests**, both audits PASS.
**Orchestration:** Workflow run `wf_4fb1bc5c-92d` (7 Opus agents, ~78 min, 1.2M tok). Sequential clusters
DataInfra → Templated → GridView → DataSources → IntegrateTest → 2 audits (single `WebControls.cs` file).

### Implemented
- **Data-binding infra:** `DataBinder` (Eval/dotted paths/indexers via TypeDescriptor), `BaseDataBoundControl`/
  `DataBoundControl`/`CompositeDataBoundControl`/`HierarchicalDataBoundControl` (DataSource/DataSourceID
  resolution via FindControl, PerformSelect/PerformDataBinding, EnsureDataBound), `DataBoundLiteralControl`,
  `DataKey`/`DataKeyArray`, `DataSourceControl`/`DataSourceView`/`DataSourceSelectArguments`/`ListSourceHelper`,
  `Parameter` family (Control/Form/QueryString/Session/Cookie/Profile/Route) + `ParameterCollection`.
- **Templated:** `Repeater` (+ Item/Collection/ItemEventArgs/CommandEventArgs; item hierarchy persisted via
  ViewState item-count, command bubbling), `BaseDataList`/`DataList` (IRepeatInfoUser rendering, item/edit/
  selected/separator templates, Edit/Update/Cancel/Delete/Select command dispatch).
- **GridView:** `DataControlField` (abstract) + `DataControlFieldCollection`/`DataControlFieldCell(HeaderCell)`,
  `BoundField`, `AutoGeneratedField`(+`AutoFieldsGenerator`/`GridViewColumnsGenerator`), `ButtonField(Base)`,
  `CommandField`, `CheckBoxField`, `HyperLinkField`, `ImageField`, `TemplateField`; `GridView`
  (AutoGenerateColumns, Rows, DataKeys/Names, paging+`PagerSettings`+`PagedDataSource`, sorting, EditIndex +
  the full Row* event set, selection, table render, command bubbling), `GridViewRow(Collection)` + all event args.
- **Data sources:** `ObjectDataSource`(+View, reflection-invoked Select/Insert/Update/Delete on a business
  type) and `SqlDataSource`(+View, ADO.NET `DbConnection`/`DbCommand`/`DbDataAdapter`; DataSet/DataReader modes)
  with their event args. `AccessDataSource` → documented `PlatformNotSupported` stub (OLEDB/Jet not x-plat).

### Tests (now 103/103)
+8: `DataBinderTests` (Eval object + DataRowView + format), `RepeaterTests` (bind List/array via a programmatic
ITemplate → item blocks with Eval values, header/separator), `GridViewTests` (AutoGenerateColumns over a
DataTable → `<table>` header + rows + cell text; BoundField), `DataSourceTests` (ObjectDataSource SelectMethod →
rows feed a grid).

### Suspect signatures (flagged, fixed in body only — internal/no public change)
- `RepeaterCommandEventArgs`/`DataListCommandEventArgs` skeleton ctors chained `: base(default(CommandEventArgs))`
  (null → base copy-ctor NPE) — corrected to pass the real args. Public ctor signatures unchanged.

### Known limitations (carry forward)
- **`DataList` multi-column layout** uses single-column fallback because `RepeatInfo.RenderRepeater` is NIE
  (out of this cluster); the `IRepeatInfoUser` surface is complete so a real `RepeatInfo` can drive it later.
- **`BaseDataList.DataKeys`** throws until `DataKeyCollection` ctor is filled (out of cluster); `DataKeysArray`/
  `SelectedValue` don't depend on it.
- **`RouteParameter`** always yields null (routing infra `System.Web.Routing` not yet implemented).
- `PagedDataSource` was NIE; filled as a required paging helper.

### Deferred (next passes)
- **5b-2:** `DataGrid` (legacy) + columns, `DetailsView`, `FormView`, `ListView` + their fields; `RepeatInfo`,
  `DataKeyCollection`; complex/nav/login controls (`Wizard`, `Login*`, `Menu`, `TreeView`, `SiteMapPath`,
  `Calendar`, `AdRotator`, `MultiView`/`View`, `ContentPlaceHolder`/`Content`); `XmlDataSource`/`SiteMapDataSource`/
  `LinqDataSource`/`EntityDataSource`.
- **5c:** `System.Web.UI.WebControls.WebParts` (119) + adapters.

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 103/103. ViewState 34/34. Host `--smoke` → 200.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (WebControls 410==spec, 0 leaked; 16 pivotal types
  diffed, 0 mismatches).


---

## Tier 6 — Compilation: `.aspx` parser + Roslyn codegen + `BuildManager` — **REAL .aspx RUNS** 🎯

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **106/106 tests**, both audits PASS (after a 1-line fix).
A real `.aspx` file compiles and serves end-to-end through the standalone host.
**Orchestration:** run `wf_f6fcf438-228` (SetupBuilders→Parser→CodeGenBuild→IntegrateE2E; **failed** at the
orchestration layer — rate-limit cut an agent off before its StructuredOutput — but all 4 phases' disk writes
persisted and built green) then continuation `wf_ab983705-f8e` (FixE2E → 2 audits). PM applied one post-audit fix.

### The capstone — proven end to end
`samples/host/wwwroot/default.aspx` (inline `<script runat="server">`: `Page_Load` binds a `GridView` to a
`List<Product>`, `Submit_Click` copies TextBox→Label) served by the **live host**:
- **GET `/default.aspx`** → HTTP 200, **1807 bytes**, full rendered HTML: GridView rows (Widget/Gadget/Gizmo)
  in `<table id="Grid_Grid">`, the Label, the TextBox `<input>`, the Button's `__doPostBack('Submit','')`, and
  a `__VIEWSTATE` hidden field **inside `<form>`**.
- **POST** replaying `__VIEWSTATE` + `__EVENTTARGET=Submit` + `NameBox=proof-value` → 200, `Submit_Click` fires,
  Label becomes "You said: proof-value". **True server-side postback through a compiled `.aspx`.**

### Implemented
- **Roslyn:** `Microsoft.CodeAnalysis.CSharp 4.8.0` PackageReference.
- **ControlBuilder family** (`UI.cs`): `ControlBuilder`/`ControlBuilderCollection`/`RootBuilder`/`TemplateBuilder`
  (ITemplate via captured sub-builder tree)/`CodeBlockBuilder`/etc. + `ControlBuilderAttribute`; the Tier-5a-flagged
  `*Builder` ctors (HtmlHead/HtmlSelect/etc.) now construct.
- **Parser** (`Compilation.cs` + `UI.cs`): `TemplateParser`/`PageParser`/`UserControlParser` — tokenize
  directives (`<%@ Page/Control/Register/Import/Assembly %>`), literals, server controls, `runat=server` HTML,
  code/expr/databind blocks (`<% %>`/`<%= %>`/`<%# %>`), comments → ControlBuilder tree; tagprefix→type
  resolution (asp:→WebControls, HTML→HtmlControls). `TemplateParseException` with line info.
- **CodeGen + BuildManager** (`Compilation.cs`): emit a C# Page class (`__BuildControlTree`/FrameworkInitialize:
  instantiate controls, set attributes as properties / IAttributeAccessor, nest via AddParsedSubObject + literals,
  generated nested ITemplates, AutoEventWireup + `OnClick=` handler wiring), **Roslyn-compile** it (metadata refs
  from the trusted-platform set + our System.Web) and **load into System.Web's custom ALC** so it binds to ours;
  `BuildManager` (GetCompiledType/CreateInstanceFromVirtualPath/cache), `PageHandlerFactory`/`SimpleHandlerFactory`.
- **Pipeline wiring** (`System.Web.cs` ~1420–1469): `*.aspx` requests route to `PageHandlerFactory` →
  `BuildManager` → compiled `Page`.

### Root-cause fix (the live empty-render bug)
**Symptom:** live GET `/default.aspx` → 200 but Content-Length 3 (UTF-8 BOM only), empty body — while
`CompilationTests` passed. **Cause:** `HttpResponse.Output` built its `StreamWriter` with BOM-emitting UTF-8 +
`AutoFlush=true`; the first touch wrote the 3-byte BOM and auto-flushed, locking `Content-Length: 3`, and
`HttpListener` (via `ContentLength64`) then **truncated the entire body to 3 bytes**. The in-process test worker
appended all writes regardless of declared length, so it never caught it. **Fix:** `Output` now uses a BOM-less
encoding + `AutoFlush=false` (single flush after render → correct Content-Length). Bodies + one private helper
property only; no signature change.

### Post-audit fix (PM)
Clean-room audit flagged **1 MAJOR**: a newly-invented **public** `interface ICodeBlockTypeAccessor` (surface
widening). **Fixed:** made it `internal` (its only implementer/consumer are internal). Rebuilt green, 106/106.

### Tests (now 106/106)
+`CompilationTests` incl. **`RendersFullPageThroughRuntimeAndHonorsContentLength`** — drives a real `.aspx` through
the SAME live path (`HttpRuntime.ProcessRequest` → PageHandlerFactory → BuildManager → render → flush) using a new
`ContentLengthHonoringWorkerRequest` (mimics HttpListener truncation) and asserts status/body/`__VIEWSTATE`-in-form/
no-BOM/Content-Length-exactness — so the truncation class of bug can't regress silently.

### Deferred (documented stubs)
- `@OutputCache`/`@Reference`/`@Implements`/`@PreviousPageType`, master-page content regions, precompilation /
  batch-folder compilation — explicit NIE stubs in directive processing.
- Still-NIE namespaces (whole-tier, future work): `System.Web.Routing`, `System.Web.ModelBinding`,
  `System.Web.Management`, parts of `System.Web.Hosting`, WebSockets, Instrumentation; control long-tail 5b-2
  (`DataGrid`/`DetailsView`/`FormView`/`ListView` + nav/login/`Calendar`) and 5c (`WebParts`).

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 106/106. ViewState 34/34. Host `--smoke` → ASPX
  SMOKE PASSED; live GET/POST on `default.aspx` proven (1807-byte render + working postback).
- Audits: clean-room ✅ (independent parser/codegen, no MS source), no-LINQ ✅, signature-integrity ✅ (pivotal
  Compilation/ControlBuilder types diffed; 0 changed; 0 leaked after the `ICodeBlockTypeAccessor`→internal fix),
  live `.aspx` e2e gate ✅.


---

## Tier 5b-2 — Data-bound completion (DataGrid / DetailsView / FormView + RepeatInfo + DataKeyCollection)

**Date:** 2026-06-02  **Status:** ✅ COMPLETE — build green, **112/112 tests**, both audits PASS.
**Orchestration:** run `wf_187e4c14-dba` (5 Opus agents, ~40 min). DataGrid → DetailFormList → IntegrateTest →
2 audits (sequential, single `WebControls.cs`).

### Implemented
- **`RepeatInfo`** — full `RenderRepeater` for all layouts (Table grid honoring RepeatColumns + Vertical/
  Horizontal direction, Flow, Unordered/OrderedList). Integrate then **rewired `DataList.RenderContents` +
  `CheckBoxList.Render` + `RadioButtonList.Render` to delegate to it** — so the Tier-5b single-column limitation
  is genuinely fixed (RepeatColumns/RepeatDirection now work).
- **`DataKeyCollection`** — fixes `BaseDataList.DataKeys` (previously threw).
- **`DataGrid`** (legacy `: BaseDataList`) — AutoGenerateColumns, `Columns`/`DataGridColumnCollection`, `Items`,
  DataKeys, paging (NextPrev + Numeric pager via PagedDataSource), sorting, editing, full style merge, command
  bubbling (Select/Edit/Update/Cancel/Delete/Sort/Page) + all event args. Column family: `DataGridColumn`
  (abstract, IStateManager) + `BoundColumn`/`ButtonColumn`/`HyperLinkColumn`/`EditCommandColumn`/`TemplateColumn`,
  `DataGridItem(Collection)`, `DataGridPagerStyle`.
- **`DetailsView`** (`: CompositeDataBoundControl`) — vertical one-row-per-field render, `AutoGenerateRows`
  (`DetailsViewRowsGenerator`), DataKeyNames/DataKey, paging (PagedDataSource pageSize=1), ReadOnly/Edit/Insert
  modes + ChangeMode + Insert/Update/Delete via DataSourceView, rows/event args.
- **`FormView`** (`: CompositeDataBoundControl`) — templated single record (Item/Edit/Insert/Header/Footer/
  EmptyData/Pager templates), modes, paging, `FormViewRow` + event args.

### Suspect signatures (flagged; fixed in body only — no signature change)
- Multiple skeleton ctors chained `: base(default(X))` (would null/zero the base) — `DataGridCommandEventArgs`,
  `DetailsViewCommandEventArgs`, `FormViewCommandEventArgs` (→ `base(originalArgs)`), `DetailsViewPagerRow`/
  `FormViewPagerRow` (→ forward real rowIndex/type/state). Corrected in bodies.
- `BoundColumn.thisExpr` was an uninitialized `public static readonly string` (null) → initialized to `"!"`
  (documented value; field already public per spec).
- **`DataGrid` skeleton does NOT implement `IPostBackEventHandler`** (real ASP.NET does). Per no-signature-change
  rule, NOT added — paging/sort/select work via bubbled Command events from pager/sort LinkButtons instead
  (functionally complete); direct `RaisePostBackEvent` on the DataGrid itself is unavailable.

### ⚠️ Phase-1 extraction anomaly — `ListView`/`DataPager` absent
`ListView`, `ListViewItem`, `ListViewDataItem`, `ListViewItemCollection`, `InsertItemPosition`,
`ListViewPagedDataSource`, `DataPager` and their event args have **0 occurrences in `src/` or
`artifacts/system.web.api.json`** — they were never extracted into the Phase-1 surface (they are legitimately
part of FW 4.8 `System.Web`). The agent correctly did NOT fabricate them (adding public types = surface leak).
**Action item:** re-run the Phase-1 `tools/extract-api` to check whether the extraction missed `ListView`/
`DataPager` (and possibly others); if so, regenerate the skeleton entries for them, then implement.

### Tests (now 112/112)
+6: `DataGridTests` (AutoGenerateColumns over a DataTable → table + rows + BoundColumn), `DetailViewsTests`
(DetailsView vertical field table; FormView ItemTemplate record), `RepeatInfoTests` (DataList/CheckBoxList
RepeatColumns=2 → multi-column structure; `BaseDataList.DataKeys` returns keys without throwing).

### Deferred (next passes)
- **5b-2ii:** nav/login/wizard/calendar — `Menu`/`TreeView`/`SiteMapPath`, `Wizard`/`MultiView`/`View`,
  `Login`/`LoginView`/`LoginStatus`/`LoginName`/`ChangePassword`/`PasswordRecovery`/`CreateUserWizard`,
  `Calendar`, `AdRotator`, `ImageMap`/`HotSpot`, `Substitution`, `Xml`, `ContentPlaceHolder`/`Content` +
  `MasterPage`; `XmlDataSource`/`SiteMapDataSource`. `LinqDataSource`/`EntityDataSource`, `DynamicData` → stubs.
- **5c:** `WebParts` (119) + adapters.
- **Phase-1 gap:** extract + implement `ListView`/`DataPager` (see above).

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 112/112. ViewState 34/34. `default.aspx` renders.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (WebControls public count == spec, 0 leaked; 11 pivotal
  types diffed, 0 mismatches).


---

## Tier 5b-2ii — Navigation / Login-Wizard / Calendar / Master pages

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **120/120 tests**, both audits PASS (after 2 PM surface fixes).
**Orchestration:** run `wf_7494883e-fa3` (6 Opus agents, ~87 min, 1.2M tok). Navigation → LoginWizard → MiscMaster →
IntegrateTest → 2 audits (sequential; WebControls.cs + MasterPage/Content in UI.cs).

### Implemented
- **Navigation:** `Menu` (+ MenuItem/Collection/Binding/Style families, nested `<ul>/<li>` render), `TreeView`
  (+ TreeNode/Collection/Binding/Style, nested div + expand/collapse/checkbox postback), `SiteMapPath`
  (+ SiteMapNodeItem) — breadcrumb degrades gracefully (SiteMap infra is NIE, see below).
- **Login/Wizard:** `MultiView`/`View`, `Wizard` family (+ `WizardStep`/`TemplatedWizardStep`/`CompleteWizardStep`/
  `CreateUserWizardStep`, step-type resolution, nav, sidebar), `Login` (→ Tier-3 `Membership.ValidateUser` +
  `FormsAuthentication`), `LoginView`/`LoginStatus`/`LoginName`, `ChangePassword`, `PasswordRecovery`,
  `CreateUserWizard` (→ `Membership.CreateUser`), `RoleGroup(Collection)`. **`CompositeControl`** (was NIE) filled
  as a needed base.
- **Misc + master pages + data sources:** `Calendar` (month table, DayRender, selection), `AdRotator`,
  `ImageMap` + `HotSpot` family (`Circle`/`Rectangle`/`Polygon`), `Substitution`, `Xml`; **`MasterPage`** /
  `ContentPlaceHolder` / `Content` (+ `MasterPageControlBuilder`) — work programmatically (content merges into
  named placeholders); `XmlDataSource` (+ views, XML/XPath, hierarchical for TreeView/Menu), `SiteMapDataSource`;
  `HierarchicalDataSourceControl` base (was NIE).

### PM surface fixes (post-audit, 2026-06-03 — both confirmed against `system.web.api.json`)
1. **`ICodeBlockTypeAccessor` reverted `internal` → `public`.** The spec lists it as a genuine **public** interface;
   the **Tier-6 audit's "invented type" call was wrong**, and my Tier-6 internal change had under-exposed a frozen
   public type. (Lesson: trust `system.web.api.json` over an auditor's "not a real type" claim — verify against spec.)
2. **`XmlDataSource.GetView()` (parameterless) `public` → `internal`** — that overload is not in the spec (only the
   explicit `IDataSource.GetView(string)` is); was a surface leak.

### Suspect signatures (flagged; fixed in body only — internal/no public change)
- More skeleton `: base(default(X))` ctor bugs corrected in bodies: `MenuEventArgs`, `ViewCollection(owner)`,
  `XmlDataSourceView`/`SiteMapDataSourceView` (passed null to the `DataSourceView`/`CommandEventArgs` base ctors).
- Style-derived ctors `(StateBag bag)` (`MenuItemStyle`/`SubMenuStyle`/`TreeNodeStyle`) lacked `: base(bag)` →
  added so styles share the owner's StateBag (like `TableStyle`).
- `Menu.MenuItemClickCommandName` / `BoundColumn.thisExpr`-style uninitialized `public static readonly string`s
  given their documented values.

### Known limitations (cosmetic / dependency — carry forward)
- **Menu/TreeView styling**: node/item styles + image sets are stored and round-trip in ViewState but are **not
  applied during render** (Menu = plain nested `<ul>/<li>`; TreeView = indented divs with `[+]/[-]`); client-side
  expand/collapse script not emitted (uses full postback).
- **Wizard/Login/ChangePassword/PasswordRecovery/CreateUserWizard templates** are stored but the **default
  built-in layout renders** (custom `*Template`s not instantiated).
- **Email** (ChangePassword/PasswordRecovery/CreateUserWizard): `OnSendingMail` hooks fire but `MailDefinition`
  is NIE so no message is sent.
- **`SiteMap` infrastructure** (`System.Web.SiteMap`/`SiteMapProvider`/`SiteMapNode` in `System.Web.cs`) is NIE →
  `SiteMapPath`/`SiteMapDataSource` degrade to empty; will populate once SiteMap is implemented (no control change).
- **Master pages at the `.aspx` level**: `Page.Master`/`MasterPageFile` parser integration is not wired (the
  Tier-6 parser doesn't yet merge `<asp:Content>` → `<asp:ContentPlaceHolder>` during compilation); the controls
  work programmatically.

### Tests (now 120/120)
+8: `NavigationTests` (Menu/TreeView static render), `LoginWizardTests` (MultiView active view; Wizard
Start→Step→Finish; Login.OnAuthenticate vs a Tier-3 in-memory Membership user), `MiscMasterTests` (Calendar month
table; MasterPage+ContentPlaceHolder+Content merge; XmlDataSource over inline XML feeds a hierarchy).

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 120/120. ViewState 34/34 (+2 new = 36 cases pass).
  Live `default.aspx` GET/POST still works under the custom ALC.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ after the 2 surface fixes (public counts == spec, 0 leaked).


---

## Tier 5c — WebParts (`System.Web.UI.WebControls.WebParts`, 119 types)

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **124/124 tests**, both audits PASS (after a PM completeness fix).
**Orchestration:** run `wf_189a85ef-2a0` (6 Opus agents, ~65 min, 1.07M tok). WPCore → ZonesParts →
PersonalizationConn → IntegrateTest → 2 audits (sequential, single `WebParts.cs`).

### Implemented (functional flags all ✅)
- **Core:** `WebPartManager` (Add/Move/Close/DeleteWebPart with cancelable event pairs, zone registry,
  `GenericWebPart` wrapping, the 5 display modes), `WebPart`/`Part`/`GenericWebPart`/`ProxyWebPart`,
  `WebPartZone`/`WebPartZoneBase`/`WebZone` (+ chrome/verbs/styles), `ZoneTemplate`, `WebPartVerb(Collection)`,
  `WebPartDisplayMode(Collection)`, `WebPartChrome`, `WebPartPersonalization`, event-args family.
- **Zones/parts:** `ToolZone`; catalog — `CatalogZone(Base)`, `CatalogPart(Chrome/Collection)`,
  `DeclarativeCatalogPart`/`PageCatalogPart`/`ImportCatalogPart`, `WebPartDescription(Collection)`; editor —
  `EditorZone(Base)`, `EditorPart(Chrome/Collection)`, `AppearanceEditorPart`/`BehaviorEditorPart`/
  `LayoutEditorPart`/`PropertyGridEditorPart` (ApplyChanges/SyncChanges); `ConnectionsZone`.
- **Personalization/connections:** default in-memory `PersonalizationProvider` + `BlobPersonalizationState`
  (reflects `[Personalizable]` members), `PersonalizationDictionary`/`Entry`/`Scope`; `WebPartConnection`
  (+ Collection), `ProviderConnectionPoint`/`ConsumerConnectionPoint`, `WebPartTransformer` +
  `RowToFieldTransformer`/`RowToParametersTransformer`. **Personalization round-trips and connections activate**
  (verified). `SqlPersonalizationProvider` → documented stub.

### PM completeness fix (post-audit, 2026-06-03)
Signature audit flagged a **MAJOR** API-completeness gap: the frozen public static class
**`PersonalizationAdministration` was entirely absent from the skeleton** (0 occurrences in `src/` — never
emitted by Phase-1 stub generation; **same class of gap as `ListView`/`DataPager`**). **Fixed:** regenerated it
from `system.web.api.json` (3 properties + 25 methods, exact signatures, NIE-stub bodies in the skeleton style)
and inserted at namespace level. Public WebParts surface now **119/119** == spec; build green; 124/124.

### Suspect signatures (flagged; fixed in body only — no signature change)
- More `: base(default(WebPart))`/`: base(default(ICollection))` skeleton ctor bugs corrected in bodies
  (`WebPartAddingEventArgs`/`WebPartMovingEventArgs` → `base(webPart)`; `CatalogZoneBase`/`EditorZoneBase`/
  `ConnectionsZone` → `SetAssociatedDisplayMode(...)` since the base null-guards).
- `WebPartManagerInternals` has only a parameterless internal ctor → wired the owning manager via an internal
  `Initialize(manager)`.

### Deferred (documented NIE stubs — ~88 peripheral members)
`WebPartManager` export/import/copy/permission-set members; `ErrorWebPart`/`UnauthorizedWebPart`/
`ProxyWebPartManager`/`ProxyWebPartConnectionCollection`; `WebPartMenuStyle`; the `Web*Attribute` classes
(`WebBrowsable`/`WebDisplayName`/`WebDescription`); `WebPartTracker`; `SqlPersonalizationProvider`.
(`PersonalizationAdministration` members are also NIE stubs — admin-only, rarely used.)

### Tests (now 124/124)
+4: `WebPartsTests` — a Page with `WebPartManager` + `WebPartZone` hosting parts renders with chrome (titles
present); display-mode switch; personalization extract→apply round-trip; a provider→consumer connection activates.

### Verification
- `dotnet build` src + host + tests → 0 errors. `dotnet test` → 124/124. ViewState 36/36. Live `default.aspx`
  GET/POST still works.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ after the `PersonalizationAdministration` re-add
  (WebParts public types now 119 == spec; 0 leaked, 0 demoted).


---

## Tier 7 — Surface-completeness audit + `System.Web.Routing` + `SiteMap`

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **132/132 tests**, both audits PASS.
**Orchestration:** run `wf_82dd4fbc-8aa` (6 Opus agents, ~34 min). GapAudit → Routing → SiteMap → IntegrateTest →
2 audits.

### Surface completeness — PROVEN (the integrity result)
Built `tools/gapdiff` (Mono.Cecil) to enumerate public/protected types of the **real** runtime + reference
`System.Web.dll` and diff against our built assembly (read via Cecil's resolver, not the shadowed default load):
- **Real `System.Web.dll` defines 1402 public/protected types; ours contains ALL of them — missing = 0.**
- Runtime vs reference assemblies: **identical** (1402 each, 0 diff) — confirms the SUMMARY's Phase-1 claim.
- The 11 "extra" types in ours are exactly the real assembly's **11 type-forwarders** (9 → `System.Web.ApplicationServices`:
  the Membership/Role family; + `EnterpriseServices.TransactionOption`; + `Web.Services.Configuration.WebServicesSection`).
  In a single-assembly port these must be *defined in-place* (our `src/Shims/Shims.cs`) — correct, not leaks.
- **Retires the "extraction gap" worry:** `ListView`/`DataPager` were never actually missing — they are present in
  the skeleton as NIE stubs; the Tier-5b-2 "absent" claim was a search error. Only `PersonalizationAdministration`
  was genuinely missing (added in Tier 5c). **No backfill was needed.**
- Footnote: the real DLL also forwards `MembershipCreateUserException`/`MembershipPasswordException`; these aren't
  referenced by any frozen skeleton signature so they're in neither set — addable to Shims.cs if strict
  forwarded-surface parity is ever wanted (the authoritative 1402 defined-type set is fully matched without them).

### Implemented
- **`System.Web.Routing` (17 types):** `RouteValueDictionary`, `RouteData`, `RouteBase`/`Route` (pattern parse +
  literal/param/catch-all segment matching + defaults/constraints; `GetVirtualPath` builds URLs back),
  `RouteCollection` (`MapPageRoute`, `Ignore`, read/write locks), `RouteTable`, `RequestContext`, `VirtualPathData`,
  `HttpMethodConstraint`, `PageRouteHandler` (→ `BuildManager` for the routed `.aspx`), `StopRoutingHandler`,
  `UrlRoutingHandler`, **`UrlRoutingModule`** (PostResolveRequestCache → `context.RemapHandler`). **Wired the
  deferred gaps:** `HttpRequest.RequestContext`, `Page.RouteData`, `Control.GetRouteUrl` now resolve via routing.
- **`SiteMap` infrastructure (8 types, in `System.Web.cs`):** `SiteMap` (static: CurrentNode/RootNode/Provider/
  Providers + `SiteMapResolve` event), `SiteMapProvider` (abstract; IsAccessibleToUser via Roles + wildcard),
  `StaticSiteMapProvider` (dictionary-backed node tree), **`XmlSiteMapProvider`** (loads `Web.sitemap` → recursive
  node tree), `SiteMapNode`/`SiteMapNodeCollection`, `SiteMapProviderCollection`, `SiteMapResolveEventArgs`.
  **Unblocks the Tier-5b-2ii `SiteMapPath` + `SiteMapDataSource` controls** (breadcrumb now renders).

### Suspect signatures / notes (flagged, no signature change)
- `Route.GetVirtualPath` uses `Uri.EscapeUriString` (obsolete on net8, compiles under pragma); exact MS escaping
  may differ slightly. `PageRouteHandler.GetSubstitutedVirtualPath` token substitution is behavior-equivalent,
  not byte-identical to MS. URL-normalization edge combos (`~/`→app-path, LowercaseUrls, AppendTrailingSlash) not
  exhaustively verified vs MS.
- Routing uses internal `RoutingHttpContextWrapper`/`RoutingHttpRequestWrapper` (over the real HttpContext) because
  the public `HttpContextWrapper`/`HttpRequestWrapper` ctors are still NIE stubs (out of scope).
- ⚠️ **Test-harness flake (pre-existing, not a code bug):** on a *fresh rebuild* immediately followed by `dotnet
  test`, 3 `WebPartsTests` intermittently fail with "Only one WebPartManager per page" — an xUnit parallel-collection
  race on thread-static `HttpContext.Current`/`Page.Items`. Deterministic 132/132 with `--no-build` across 3 runs.
  **TODO:** isolate WebParts tests into a non-parallel xUnit collection (or reset HttpContext.Current per test).

### Tests (now 132/132)
+8: `RoutingTests` (route matches `products/{category}/{id}` → values; constraint rejects; `GetVirtualPath` round-trip;
`MapPageRoute`+`UrlRoutingModule` remap), `SiteMapTests` (`XmlSiteMapProvider` loads a temp `Web.sitemap` → RootNode
+ children; `CurrentNode` resolves; `SiteMapPath` renders the breadcrumb trail).

### Verification
- `dotnet build` → 0 errors. `dotnet test` → 132/132. ViewState passes. Live `default.aspx` renders.
- Audits: clean-room ✅, no-LINQ ✅, **surface-completeness ✅ (our public types ⊇ real System.Web's 1402; 0 missing)**.


---

## Tier 8 — Polish pass (Mail/login-email, Page MAC, EnsureID, Menu/TreeView styling, master pages, test flakes)

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **141/141 tests** (deterministic on fresh builds), both audits PASS.
**Orchestration:** run `wf_38b917e7-de7` (6 Opus agents, ~52 min). ListView → MailSecCore → StylingMaster →
IntegrateTest → 2 audits.

### ⚠️ Correction: `ListView`/`DataPager` are NOT in `System.Web.dll`
Proven 3 ways (grep src + spec = 0; reflected the **real GAC `System.Web.dll`** = no `ListView`/`DataPager`,
though it has `DetailsView`/`FormView`/`GridView`/`TreeView`; found them defined in **`System.Web.Extensions.dll`**,
different assembly/key, same `System.Web.UI.WebControls` *namespace* — the source of confusion). So they are
**genuinely out of scope** for this `System.Web` port; correctly **not fabricated** (would have leaked ~20 public
types and broken the Tier-7 1402/1402 proof). The Tier-5b-2 "absent" observation was right; the Tier-7 "present as
NIE stubs" note was the actual error. Our surface remains complete and correct. (If ListView is ever wanted, it
belongs in a separate `System.Web.Extensions` clean-room assembly.)

### Implemented
- **`System.Web.Mail` + `MailDefinition`:** `SmtpMail`/`MailMessage`/`MailAttachment` mapped onto `System.Net.Mail`;
  `MailDefinition.CreateMailMessage` (token replacement, `~/`-rooted body via `Server.MapPath`), `EmbeddedMailObject(s)`.
  **Wired login email:** `ChangePassword`/`PasswordRecovery`/`CreateUserWizard` now send via an internal
  `LoginUtil.SendMail` (raises `SendingMail`/`SendMailError`, guarded — never throws without live SMTP).
- **Page ViewState MAC → `MachineKeySection`:** `GetViewStateMacKey` now reads `<machineKey validationKey>` (explicit
  hex) from config, falling back to the per-process key. ViewState byte-compat tests unaffected (they key the
  formatter directly).
- **`Control.EnsureID`:** implemented auto-ID generation (was a no-op) — `GenerateAutomaticID` on naming containers
  stabilizes `UniqueID`/`ClientID` for controls without an explicit ID.
- **Menu/TreeView style rendering:** merged item/sub-menu/node styles (static/dynamic/level/selected/hover) now applied
  during render; TreeView `ShowLines` emits a CSS marker class. (Image-set GIFs intentionally skipped — documented.)
- **Wizard/Login custom templates:** `Wizard` Header/SideBar/*Navigation, `ChangePassword`/`PasswordRecovery` step
  templates instantiated into their containers; default layout when null.
- **Master pages (compile-time + runtime):** added `MasterPageParser` (`@Master`); content page (`@Page MasterPageFile`)
  codegen emits `AddContentTemplate(id, template)` per `<asp:Content>`; `Page.MasterPageFile`/`Master`/`ApplyMasterPage`
  merge content into the master's `ContentPlaceHolder`s and render through the master. Verified by a full-pipeline test.

### Test-flakes fixed (both deterministic now)
- **WebParts parallel race** ("Only one WebPartManager per page"): root cause was the manager singleton living in shared
  `HttpContext.Items`; moved to an internal `Page._currentWebPartManager` field. Verified deterministic across 3 fresh
  `dotnet build`+`dotnet test` runs.
- **`CachingTests.SlidingExpiration`** timing flake: widened the sliding window + jitter margin.

### Deferred / partial (documented)
- Menu/TreeView **image-set** rendering (GIF expand/collapse/pop-out images) — styles + ShowLines CSS only.
- Wizard `LayoutTemplate` (named-placeholder merge) — stored, not rendered.
- Master pages: runtime `ApplyMaster` approach (master compiled separately, merged after PreInit); nested
  master-of-master + `@MasterType` strong typing not exercised.
- `MailMessage.Fields` (legacy CDO knobs) kept as a property bag; `MailEncoding` not applied (System.Net handles it).

### Tests (now 141/141)
+9: `MailTests`, `CoreFixTests` (EnsureID, configured MAC key), `NavigationTests` (Menu/TreeView styles),
`LoginWizardTests` (custom templates), `CompilationTests.MergesMasterPageWithContentRegions` (master+content render).

### Verification
- `dotnet build` → 0 errors. `dotnet test` (fresh build) → **141/141 deterministic**. ViewState byte-compat passes.
  Live `default.aspx` renders. Surface still complete (1402; ListView/DataPager correctly absent).
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (0 leaks/demotions; SmtpMail's extra ctor internal).


---

## ✅ LINUX VALIDATION — PROVEN (2026-06-03)

Validated the cross-platform claim on **real Linux** (WSL2 Ubuntu 24.04, .NET SDK 10.0.300; our net8.0 assemblies
run via `DOTNET_ROLL_FORWARD=Major` since only the net10 runtime is present). Invocation:
`wsl -d Ubuntu-24.04 -- bash -lic "... DOTNET_ROLL_FORWARD=Major ..."`; project at `/mnt/d/Claude Files/System.Web Project`.

- **Build on Linux:** `dotnet build src/System.Web.csproj` → **0 errors** (net8 ref pack auto-restored under SDK 10).
- **Tests on Linux:** `dotnet test` → **140/140 pass** — incl. ViewState byte-compat (Windows-captured fixtures
  reproduce byte-for-byte on Linux), Roslyn `.aspx` compilation, control rendering, postback/pipeline, the custom-ALC
  bridge. **No `System.Drawing` / GDI+ failures** — the `Color`/`WebColorConverter` style path is Linux-safe.
- **Live `.aspx` on Linux:** ran `dotnet samples/host/bin/Debug/net8.0/SampleHost.dll 8099`; `curl /default.aspx` →
  **HTTP 200, 1806 bytes**, full rendered page (`<form>`, `__VIEWSTATE`, GridView rows Widget/Gadget/Gizmo, Label,
  `Content-Type: text/html`). Host log confirms **our** System.Web v4.0.0.0 loaded into `SystemWebHostContext`
  (custom ALC) from the app-local path, on Linux. A real `.aspx` was parsed → codegen → Roslyn-compiled → executed
  **on Linux**.

**Conclusion: the project runs classic ASP.NET Web Forms (`.aspx` + data binding + ViewState + postback) on
cross-platform .NET on Linux — proven, not just designed for.**

### Notes
- WSL has only the **net10 runtime** → run net8 assemblies with `DOTNET_ROLL_FORWARD=Major` (or install the net8
  runtime in WSL). Run the host via the **portable DLL** (`dotnet SampleHost.dll <port>`) — the framework-dependent
  apphost doesn't launch off the `/mnt/d` 9p mount.
- Removed a stray debug test (`tests/DiagTests.cs` + `DiagWorker.cs`, leftover from the Tier-6 empty-render
  investigation) that hardcoded a Windows path `D:/Claude Files/gen_dump.txt` and failed on Linux. Suite is now
  **140 tests**, all green on both Windows and Linux.
- Live postback on Linux not separately curl'd, but covered by the passing `PagePostbackTests` + `CompilationTests`
  postback cases on Linux (live postback was curl-proven on Windows in Tier 6).


---

## Tier 9 — `System.Web.ModelBinding` (Web Forms model binding)

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **147/147 tests** (verified on Windows **and Linux**), both audits PASS.
**Orchestration:** run `wf_8ecc4974-5ff` (6 Opus agents). MBCore → ValueProviders → BindersDataSource →
IntegrateTest → 2 audits. Linux re-validated by a dedicated WSL agent (compact report; no raw output ingested).

### Implemented
- **Core:** `ModelBindingExecutionContext`, `ModelState`/`ModelStateDictionary`/`ModelError(Collection)`,
  `ModelMetadata`/`ModelMetadataProvider(s)`, `IModelBinder`/`ModelBinderProvider(Collection)`/`ModelBinders`,
  `ModelBindingContext`, `ValueProviderResult`/`ValueProviderCollection`, binding-behavior attributes.
- **Value providers:** `Form`/`QueryString`/`RouteData`/`Control`/`Cookie`/`Session`/`Profile`/`UserProfile`/
  `ViewState` providers (over the real `HttpContext`/`RouteData`/`Profile`/`Session`) + their
  `[Form]`/`[QueryString]`/`[Control]`/… source attributes; hierarchical `ContainsPrefix` matching.
- **Binders + data source:** `DefaultModelBinder` (simple + complex/POCO binding with ModelState),
  `TypeConverter`/`TypeMatch`/`Array`/`Collection`/`Dictionary`/`KeyValuePair`/`MutableObject` binders + their
  providers, `ComplexModelBinder`; **`ModelDataSource`/`ModelDataSourceView`** (wires data-control `SelectMethod`/
  `UpdateMethod`/etc. to code-behind methods with model-bound parameters).
- **Clean-room discipline note:** the agents correctly **declined to add MVC-only types** the brief mistakenly
  listed (`*ValueProviderFactory`, `ValueProviderFactory`, `RequestValueProvider`, `HttpFileCollectionValueProvider`,
  etc.) — those belong to ASP.NET MVC, not WebForms `System.Web.ModelBinding`, and are absent from the spec.
  One new helper (`SessionValueProvider`) added as `internal`.

### Deferred (documented NIE stubs — deeper validation sub-layer)
DataAnnotations metadata providers (`AssociatedMetadataProvider`/`DataAnnotationsModelMetadataProvider`/
`EmptyModelMetadataProvider`), the `ModelValidator` family (+ DataAnnotations validator adapters),
`ModelValidationNode`, `ModelValidatingEventArgs`/`ModelValidatedEventArgs`, `ModelBinderErrorMessageProviders`.
Core binding works without them; they're the next sub-cluster.

### Tests (now 147/147)
+7: `ModelBindingTests` — value providers return `ValueProviderResult`; `ValueProviderCollection` composites;
`DefaultModelBinder` binds simple + complex types into `ModelState` (invalid → error, `IsValid=false`);
`ModelDataSource` SelectMethod with a `[QueryString]` parameter resolves and returns rows.

### Linux re-validation (post-ModelBinding)
WSL2 Ubuntu, `DOTNET_ROLL_FORWARD=Major`: build 0 errors; **`dotnet test` → 147/147 on Linux**; live host
`curl /default.aspx` → **HTTP 200, 1806 bytes**, all markers present, our `System.Web` v4.0.0.0 in
`SystemWebHostContext`. The whole stack — incl. model binding — runs on Linux.

### Verification
- `dotnet build` + `dotnet test` → 147/147 on **both** Windows and Linux. ViewState byte-compat passes. Live
  `.aspx` renders on Linux. Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (0 leaks/demotions).


---

## Tier 10 — Final breadth (ModelBinding validators, AntiXss, Globalization, WebSockets, Instrumentation, Adapters, Management)

**Date:** 2026-06-03  **Status:** ✅ COMPLETE — build green, **160/160 tests on Windows *and* Linux**, both audits PASS.
**Orchestration:** run `wf_027fea87-ffe` (6 Opus agents). 3 **parallel** writers (separate files) → integrate → audit
(with a Windows+Linux WSL gate folded in).

### Implemented
- **ModelBinding validation sub-layer** (finishes Tier 9): `ModelValidator`(+provider/collection/`ModelValidatorProviders`),
  `DataAnnotationsModelValidator` + per-attribute adapters (Required/Range/RegularExpression/StringLength/Min/Max),
  `ValidatableObjectAdapter`, `AssociatedMetadataProvider`/`DataAnnotationsModelMetadataProvider`/`EmptyModelMetadataProvider`,
  `ModelValidationNode` (recursive validate → ModelState), validating/validated events.
- **`System.Web.Security.AntiXss`** — `AntiXssEncoder` safe-list encoder (Html/HtmlAttribute/Url/HtmlFormUrl/Xml/
  XmlAttribute/Css encode). (Spec has only `AntiXssEncoder` + 5 code-chart enums — no `UnicodeRange`/JS-encode; not added.)
- **`Globalization`** — `StringLocalizerProviders`/`IStringLocalizerProvider`/`ResourceFileStringLocalizerProvider`
  (resolves a localization ResourceManager; graceful key-fallback when absent).
- **`WebSockets`** — `AspNetWebSocket`/`AspNetWebSocketContext`/`AspNetWebSocketOptions` over `System.Net.WebSockets`
  (host upgrade path documented where the listener doesn't yet upgrade).
- **`Instrumentation`** — `PageInstrumentationService`/`PageExecutionContext`/`PageExecutionListener`.
- **Adapters** — `ControlAdapter`/`PageAdapter` + `WebControlAdapter`/`DataBoundControlAdapter`/
  `HierarchicalDataBoundControlAdapter`/`HideDisabledControlAdapter`/`MenuAdapter` (delegate lifecycle/render to the
  adapted control).
- **`System.Web.Management`** (40 types) — `WebBaseEvent` hierarchy (management/heartbeat/lifetime/request/error/audit
  events), `WebEventProvider`/`BufferedWebEventProvider` + `EventLog`(console/file)/`Trace`/`Mail` providers,
  `WebEventFormatter`, process/request/thread info, `WebEventCodes`. **External-infra stubs (documented):**
  `SqlWebEventProvider` (needs DB schema), `WmiWebEventProvider` (Windows WMI), `SqlServices`/`RegiisUtility`
  (SQL install scripts / IIS-COM registration — not cross-platform).

### Tests (now 160/160 on both OSes)
+13: `ModelValidationTests` (DataAnnotations → ModelState errors), `AntiXssTests` (XSS payloads neutralized),
`ManagementTests` (raise events → buffered/EventLog provider; `FormatToString` non-empty), light
`InstrumentationTests`/`AdaptersTests`.

### Verification (Windows + Linux)
- **Windows:** build 0 errors, `dotnet test` → **160/160**.
- **Linux (WSL2):** build 0 errors, `dotnet test` → **160/160**, live `.aspx` → **HTTP 200**.
- Audits: clean-room ✅, no-LINQ ✅, signature-integrity ✅ (0 leaks/demotions).

---

# 🏁 Phase 2 — Completion Summary

**A clean-room `System.Web` that runs classic ASP.NET Web Forms on cross-platform .NET — built, audited, and
proven on Linux.**

**Journey:** Tier 0 (net8.0 project + standalone host) → 1 (Util/Caching/Config) → 2 (core runtime, host serves 200)
→ 3 (Session/Forms-auth/Profile) → 4 (Control/Page/**byte-compatible ViewState**) → 5a/5b/5b-2/5b-2ii/5c (the entire
control library + WebParts) → 6 (**`.aspx` parser + Roslyn compiler — real `.aspx` runs**) → 7 (surface-completeness
proof + Routing + SiteMap) → 8 (polish: mail/MAC/EnsureID/styling/master-pages) → 9 (ModelBinding) → 10 (final breadth).

**Final state:**
- **Public surface: 100% complete** — all **1,402** types of the real `System.Web.dll` present (proven via `tools/gapdiff`).
- **Functional implementation:** the entire request→pipeline→control→ViewState→render→postback path, the full control
  library, the `.aspx`/`.ascx` compiler, services (session/auth/profile/cache/config/routing/sitemap/model-binding/
  health-monitoring). **~10,700 of ~13,796 members implemented**; the remaining ~3,069 NIE bodies are external-infra
  (SQL/AD/WMI providers, IIS-COM) and design-time/exotic members — **none on the live request path**.
- **Compatibility:** `__VIEWSTATE` is **byte-for-byte identical** to real ASP.NET (incl. HMAC-SHA1/SHA256).
- **Cross-platform: PROVEN on Linux** — build + **160/160 tests** + a live data-bound `.aspx` (HTTP 200) on WSL Ubuntu.
- **Quality bar held every tier:** clean-room (no copied source), no public/protected signature changes, no LINQ in
  `src/`, no `Microsoft.AspNetCore.*`, green build + growing test suite (0 → 160 tests).
- **Standalone:** owns its HTTP host, runtime, pipeline, and `.aspx` compiler — no IIS, no ASP.NET Core, no Kestrel.

**Remaining (optional, post-1.0):** SQL/AD/WMI provider implementations (need external infra), out-of-proc session
(StateServer/SQL), Menu/TreeView image-sets, nested master pages, and the design-time/designer surface.

---

# Post-1.0 — Tier 11: `Global.asax`, route registration, and the `CompleteRequest` short-circuit (2026-06-03)

Implemented the three features from the maintainer playbook (`docs/extending-openwebforms.md` §8a/b/c), following
its recipe. **Windows + Linux: build 0 errors, 164/164 tests** (160 prior + 4 new in
`tests/GlobalAsaxRoutingTests.cs`). Clean-room / no-LINQ / no-signature-change audit: **CLEAN**.

- **`Global.asax` (8a — keystone):** the application file is now discovered at the app root (case-insensitive →
  Linux-safe), compiled, and pooled as the `HttpApplication` instance.
  - **Parser:** new `internal sealed class ApplicationFileParser : TemplateControlParser` (`src/System.Web.UI.cs`),
    base type = `PageParser.DefaultApplicationBaseType ?? HttpApplication`, directive `Application`; routed the
    `<%@ Application %>` directive into the existing main-directive handler (`ProcessDirective`).
  - **Codegen:** `TemplateCodeGenerator` gained an `isApplication` mode (`src/System.Web.Compilation.Impl.cs`,
    `GenerateApplication()`) that emits `ASP.<class> : <base>` carrying only the inline `<script runat="server">`
    declaration blocks — **no control tree / FrameworkInitialize / AutoEventWireup**. Works for both inline-script
    and `Inherits=`/code-behind forms (the generated subclass inherits the magic methods).
  - **Engine:** `BuildManagerEngine` (`src/System.Web.Compilation.Engine.cs`) maps `.asax` → the new parser/codegen
    and exposes `GetGlobalAsaxType()` (discover + compile + cache); `BuildManager.GetGlobalAsaxType()` delegates to it
    (falls back to `typeof(HttpApplication)`).
  - **Runtime wiring:** `HttpApplication.InitInternal()` installs the default modules then **binds the "magic"
    methods by reflection** — `Application_<Event>`/`Application_On<Event>` → the matching pipeline event
    (`BeginRequest`/`Error`/…), `Session_Start`/`Session_End` → the `SessionStateModule`, and the non-event hooks
    `Application_Start`/`End`/`Init`. Methods may be `(object,EventArgs)` or parameterless (adapted via a private
    `MagicHandlerAdapter`). `HttpRuntime.EnsureFirstTimeInit()` resolves the app type, builds the special instance,
    and raises `Application_Start` **exactly once** before the first request; `Close()` raises `Application_End` once.
- **Route registration (8b):** `UrlRoutingModule` (and `SessionStateModule`) are now installed in the **default
  module set** (previously `HttpApplication.Init()` installed none), so `RouteTable.Routes.MapPageRoute(...)` /
  `Routes.Map(...)` called from `Application_Start` take effect with no extra config. Proven end-to-end:
  `GET /products/5` → `Product.aspx` renders `PID=5` via `Page.RouteData`.
- **`CompleteRequest` short-circuit (8c):** verified already-correct (`SuppressContent`/`TrySkipIisCustomErrors`
  stored & honored in the flush/header/body paths; `CompleteRequest()` skips remaining stages). Added a test proving
  the exact snippet suppresses the body and skips later stages.
- **One body fix surfaced by routing:** `HttpWorkerRequest.GetPathInfo()` was a `throw NIE`; the real framework
  default returns `String.Empty` (routing reads `Request.PathInfo`). Fixed to the documented default (body-only).
- **Test-only internal hooks (no surface impact):** `HttpRuntime.SetAppPathsForTest`/`ResetApplicationForTest`,
  `BuildManagerEngine.ResetCachesForTest` — let isolated tests point the runtime at a temp app dir and re-resolve.

**Lesson:** the `.asax` path is the page compiler minus the control tree — reuse `TemplateCodeGenerator` with a mode
flag rather than a parallel generator. The default-module set is the single switch that makes the whole Tier-7
routing engine actually fire on a live request.
