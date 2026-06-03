# Contributing to OpenWebForms

Thanks for your interest! OpenWebForms is a **clean-room** reimplementation of `System.Web`. To keep
the project legally clean and binary-compatible, all contributions must follow these rules.

> 📘 **New contributor? Read [`docs/extending-openwebforms.md`](docs/extending-openwebforms.md) first** —
> the maintainer playbook with the step-by-step recipe, the test harness, the gotchas, and worked examples
> (`Global.asax`, route registration, the `CompleteRequest` pattern).

## The non-negotiable rules

1. **Clean-room only.** Implement from the **public API surface** (`artifacts/system.web.api.json`) and
   **documented / standard / RFC behavior** only. **Never** copy, decompile, paraphrase, or transcribe
   Microsoft's `System.Web` source or IL. If you've seen decompiled framework source for a type, don't
   work on that type. Behavior may be *referenced*; code must be written fresh.
2. **Do not change public/protected signatures.** They are frozen for binary compatibility with the real
   `System.Web` (assembly `System.Web`, v4.0.0.0). Fill method bodies and add `private`/`internal` helpers
   only. New helper types must be `internal`. If a signature looks wrong, **flag it in your PR** — don't
   edit it. (Verify "is this type/member in the API?" against `artifacts/system.web.api.json`.)
3. **No LINQ in `src/`.** Use explicit `for`/`foreach`. (Build tooling under `tools/` may use LINQ.)
4. **Keep it green.** Every change must leave `src/` building with 0 errors and the test suite passing.
5. **No `Microsoft.AspNetCore.*`.** This is a fully standalone runtime — it owns its own HTTP host and
   pipeline. Never take a dependency on ASP.NET Core / Kestrel.
6. Documented stubs are fine for out-of-scope members: `throw new NotImplementedException("TODO: …")` or
   `PlatformNotSupportedException` with a comment — never a silent fake.

## Building & testing

```bash
# Windows or Linux
dotnet build src/System.Web.csproj -c Debug
dotnet test  tests/System.Web.Tests.csproj

# Run the standalone host serving the sample app
dotnet run --project samples/host -- 8080   # then: curl http://localhost:8080/default.aspx
```

**Testing on Linux (WSL/Docker), if only a newer runtime is installed:**

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet test tests/System.Web.Tests.csproj
# run the host via the portable DLL:
dotnet samples/host/bin/Debug/net8.0/SampleHost.dll 8080
```

Tests use a custom `AssemblyLoadContext` bridge (`tests/SystemWebUnderTest.cs`) so they bind to *our*
`System.Web` rather than the .NET shared-framework facade — keep new tests on that pattern.

## High-value areas to help with

- **Stubbed namespaces:** `System.Web.ModelBinding`, `System.Web.Management`, `System.Web.Security.AntiXss`,
  `System.Web.Globalization`, `System.Web.WebSockets`, the `*.Adapters` namespaces.
- **Provider implementations:** SQL/Active Directory Membership/Role/Profile providers, StateServer/SQL
  session, `SqlPersonalizationProvider`.
- **Golden fixtures:** more ground-truth `__VIEWSTATE` / rendered-HTML captures from real ASP.NET to lock
  in byte-compatibility (`tools/fixtures-gen`, `fixtures/`).
- **Real-world testing:** drop a real Web Forms app on Linux and report what breaks.
- **Rendering fidelity:** Menu/TreeView image-sets, Wizard `LayoutTemplate`, nested master pages.

## PR checklist

- [ ] Clean-room (no copied/decompiled source)
- [ ] No public/protected signature changed; new types are `internal`
- [ ] No LINQ in `src/`
- [ ] `dotnet build` + `dotnet test` green (ideally on Linux too)
- [ ] Tests added for new behavior
