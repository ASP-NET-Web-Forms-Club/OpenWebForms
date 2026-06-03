# System.Web — Public API Surface Extraction (Phase 1)

**Date:** 2026-06-01
**Goal:** Produce a complete, machine-readable inventory of the public/protected API surface of
.NET Framework 4.8 `System.Web.dll`, as the clean-room specification baseline for a cross-platform
reimplementation on modern .NET.

---

## 1. Source & identity verification

Two copies of `System.Web.dll` exist on this machine and were both used:

| Role | Path | Size |
|------|------|------|
| **Primary spec source** (metadata-only reference assembly — no IL bodies) | `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Web.dll` | 2.70 MB |
| **Cross-check** (runtime implementation assembly) | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.dll` | 5.40 MB |

Identity (verified via Mono.Cecil, matches task requirement exactly):

```
Assembly        : System.Web
Version         : 4.0.0.0
PublicKeyToken  : b03f5f7f11d50a3a
FrameworkTarget : .NET Framework 4.8
```

**Why the reference assembly is the primary source:** reference assemblies are already stripped to the
public/protected contract with **no method bodies** — they are precisely the compile-time surface that
downstream binary references resolve against, and they make any source/IL contamination structurally
impossible. The runtime DLL was used only to confirm the reference assembly is not missing anything
(see §4).

---

## 2. Method

- **Metadata-only** extraction with **Mono.Cecil 0.11.5**. No IL method bodies were read, decompiled,
  reproduced, or paraphrased anywhere. Output is the public contract only.
- A reusable console tool (`tools/extract-api`) walks every public/protected type (recursing into nested
  types) in stable metadata order and emits:
  1. the JSON inventory,
  2. a compilable C# stub skeleton (`throw new NotImplementedException()` bodies),
  3. the external assembly-reference (dependency) list.
- The tool is **reusable for the sister assemblies** (just point it at a different DLL).

---

## 3. Deliverables

| Deliverable | Location |
|-------------|----------|
| Extraction tool (Cecil, reusable) | `tools/extract-api/` |
| Stub compile harness | `tools/compile-stubs.ps1` |
| **JSON inventory** (canonical, exact schema from the task) | `artifacts/system.web.api.json` (5.76 MB) |
| **C# stub skeleton** (one file per namespace) | `src/*.cs` (25 files, 17,581 LOC) |
| External dependency list | `artifacts/dependencies.txt` |
| Compiled proof assembly | `artifacts/SystemWeb.dll` (724 KB) |
| This report | `artifacts/SUMMARY.md` |

---

## 4. Counts

**1,402 public/protected types** across **26 namespaces**.

| Kind | Count |
|------|-------|
| class | 1,022 |
| interface | 116 |
| enum | 149 |
| delegate | 113 |
| struct | 2 |

**13,796 public/protected members total:**

| Member kind | Count |
|-------------|-------|
| methods | 5,685 |
| properties | 5,249 |
| constructors | 1,325 |
| fields + enum members | 1,245 |
| events | 292 |

Types per namespace:

```
410  System.Web.UI.WebControls
185  System.Web.UI
150  System.Web.Configuration
119  System.Web.UI.WebControls.WebParts
103  System.Web
 99  System.Web.ModelBinding
 49  System.Web.Hosting
 44  System.Web.Management
 41  System.Web.UI.HtmlControls
 40  System.Web.Security
 34  System.Web.Compilation
 25  System.Web.Caching
 21  System.Web.SessionState
 20  System.Web.Profile
 16  System.Web.Routing
  9  System.Web.Util
  6  System.Web.Mail
  6  (global namespace)
  6  System.Web.Security.AntiXss
  5  System.Web.UI.WebControls.Adapters
  3  System.Web.Instrumentation
  3  System.Web.WebSockets
  3  System.Web.Globalization
  2  System.Web.Handlers
  2  System.Web.UI.Adapters
  1  System.Web.Configuration.Internal
```

---

## 5. Validation (acceptance criteria)

| Criterion | Result |
|-----------|--------|
| **Deterministic** — two runs on the same binary produce byte-identical JSON | ✅ SHA-256 identical |
| **Every public/protected type present** | ✅ 1,402 / 1,402 |
| **Stub skeleton compiles** | ✅ `csc` exit 0, 0 errors → `SystemWeb.dll` |
| **No IL / method bodies / source in output** | ✅ metadata-only by construction |
| **Reference vs runtime DLL surface** | ✅ 0 type differences, 0 member-count differences across all 1,402 types |
| **Round-trip** — re-extracting the surface from our compiled `SystemWeb.dll` matches the original | ✅ 0 missing, 0 extra, 0 member-count diffs |

The reference assembly and the runtime assembly expose an **identical** public surface, and our compiled
skeleton reproduces that surface shape exactly. This is the strongest available proof that the inventory
is complete and the signatures are self-consistent.

**Note on the compile target:** the stubs were compiled against the genuine **.NET Framework 4.8
reference assemblies**, referencing exactly System.Web's real external dependency set (minus System.Web
itself). This validates our surface against the true external contract. Compiling against `net8.0`
directly is *not* meaningful at this stage because System.Web's dependencies (System.Drawing,
System.Configuration, the System.Web.* sisters, …) do not all exist on net8.0 — resolving/shimming those
is Phase 2 work, for which §6 is the roadmap.

---

## 6. External dependency roadmap (input for the net8.0 port)

System.Web references **22 assemblies**. Classified for cross-platform portability:

**Covered by the modern .NET BCL (no action):**
`mscorlib`, `System`, `System.Core`, `System.Data`, `System.Xml`

**Available via NuGet / OOB packages (mostly Windows-leaning):**
`System.Configuration` → `System.Configuration.ConfigurationManager`;
`System.Drawing` → `System.Drawing.Common`;
`System.Runtime.Caching` → `System.Runtime.Caching`;
`System.ComponentModel.DataAnnotations` → `System.ComponentModel.Annotations`;
`System.DirectoryServices`, `System.DirectoryServices.Protocols`, `System.ServiceProcess` (ServiceController), `System.Security`

**Sister System.Web assemblies (circular with System.Web — must be reimplemented/stubbed alongside it):**
`System.Web.Services`, `System.Web.ApplicationServices`, `System.Web.RegularExpressions`

**Cross-platform BLOCKERS (Windows/desktop-only — likely confined to design-time/legacy surface, must be isolated, stubbed, or shimmed):**
`System.Windows.Forms`, `System.Design`, `System.EnterpriseServices`

**Build-system (used by `System.Web.Compilation` surface):**
`Microsoft.Build.Framework`, `Microsoft.Build.Tasks.v4.0`, `Microsoft.Build.Utilities.v4.0`

---

## 7. Deferred follow-up tasks (sister assemblies)

Per the task scope, these were **not** extracted in this pass. The same tool extracts each:

- `System.Web.Extensions` (and `.Design`)
- `System.Web.Services`
- `System.Web.ApplicationServices`
- `System.Web.Mobile`
- `System.Web.RegularExpressions`, `System.Web.Routing`, `System.Web.Abstractions`,
  `System.Web.DynamicData`, `System.Web.Entity`, `System.Web.DataVisualization`

---

## 8. Known simplifications (to revisit when filling in implementations)

These do **not** affect the type/member inventory or stub compilation, but matter for full binary
compatibility and should be layered in during Phase 2:

1. **Type-level attributes** are recorded in the JSON (`[Serializable]`, `[Flags]`, `[ParseChildren]`,
   `[ControlBuilder]`, `[DefaultProperty]`, `[TypeConverter]`, etc.) but are **not** emitted onto the
   stubs (to maximize compilability of the skeleton). Binary-faithful attribute replay is Phase 2.
2. **Explicit implementations of *internal* interfaces** (e.g. `IISAPIRuntime2`, `ISyncContext`,
   `IPrincipalContainer`) are excluded from both JSON and stubs — they are effectively private members
   (the interface itself cannot be named by consumers), consistent with the task's "ignore internal"
   scope.
3. **`[IndexerName]`** customization is not replayed; indexers are emitted as `this[...]` (CLR default
   name `Item`). The vast majority already use `Item`.
4. **Optional-parameter default values** are emitted only when they render as a safe constant; otherwise
   the parameter is emitted as required (the real default is still recorded in the JSON).
5. Synthesized `internal` constructors are emitted for types whose only constructor was `internal`
   (and therefore stripped from the reference assembly) so the skeleton stays non-constructable and
   chains correctly to its base.

---

## 9. Recommended next step (Phase 2)

The `src/*.cs` skeleton is the scaffold to fill in. Recommended order, smallest/leaf-most first so each
layer compiles against already-done layers:

1. `System.Web.Util`, `System.Web.Caching`, `System.Web.Configuration` (leaf utility/config)
2. `System.Web` core (`HttpContext`, `HttpRequest`, `HttpResponse`, handlers pipeline)
3. `System.Web.SessionState`, `System.Web.Security`, `System.Web.Profile`
4. `System.Web.UI` (control lifecycle, `Page`, `Control`, ViewState)
5. `System.Web.UI.HtmlControls`, `System.Web.UI.WebControls`, `WebParts`

This is where parallel agent fan-out becomes worthwhile (one namespace/subsystem per agent, each
verified against the behavioral fixtures the original handover mentioned).
