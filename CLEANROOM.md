# Clean-Room Methodology

OpenWebForms is a **clean-room** reimplementation of `System.Web`. This document records how the
project is built and the engineering rules it holds itself to. It is the project's standing
methodology — not a contribution guide. Ideas and bug reports are welcome via Issues / Discussions.

These rules exist so the project stays **legally clean** (no Microsoft source enters the codebase)
and **binary-compatible** (drop-in for the real `System.Web`).

## The non-negotiable rules

1. **Clean-room only.** Code is implemented from the **public API surface**
   (`artifacts/system.web.api.json`) and from **documented / standard / RFC behavior** only.
   Microsoft's `System.Web` source or IL is **never** copied, decompiled, paraphrased, or
   transcribed. Where decompiled framework source for a type has been seen, that type is not worked on. Observable behavior may be *referenced*; all code is written fresh.

2. **Public/protected signatures are frozen.** They are fixed for binary compatibility with the real
   `System.Web` (assembly `System.Web`, v4.0.0.0). Only method bodies are filled and
   `private`/`internal` helpers added; new helper types are `internal`. A signature is verified
   against `artifacts/system.web.api.json` ("is this type/member actually in the API?") rather than
   adjusted. If a signature looks wrong, it is flagged and investigated — not edited.

3. **No LINQ on runtime paths in `src/`.** The request pipeline, control rendering, and ViewState
   serialization run on every request, so `src/` favors explicit `for`/`foreach` to keep allocations explicit and behavior predictable. (Build tooling under `tools/` uses LINQ freely.)

4. **Always green.** Every change leaves `src/` building with 0 errors and the test suite passing.

5. **No `Microsoft.AspNetCore.*`.** This is a fully standalone runtime — it owns its own HTTP host
   and pipeline, and never takes a dependency on ASP.NET Core / Kestrel.

6. **No silent fakes.** Out-of-scope members are documented stubs —
   `throw new NotImplementedException("TODO: …")` or `PlatformNotSupportedException` with a comment —
   never a method that pretends to work.

## Verification

- **Public surface** is diffed against the real .NET Framework 4.8 `System.Web.dll`
  (all 1,402 types present).
- **ViewState** is checked byte-for-byte against ground-truth `__VIEWSTATE` captures from the real
  framework (`tools/fixtures-gen`, `fixtures/`), including the HMAC-SHA1/SHA256 MAC.
- **Tests** use a custom `AssemblyLoadContext` bridge (`tests/SystemWebUnderTest.cs`) so they bind to
  *our* `System.Web` rather than the .NET shared-framework facade.

## Building & testing

```bash
# Windows or Linux
dotnet build src/System.Web.csproj -c Debug
dotnet test  tests/System.Web.Tests.csproj

# Run the standalone host serving the sample app
dotnet run --project samples/host -- 8080   # then: curl http://localhost:8080/default.aspx
```

On Linux (WSL/Docker), if only a newer runtime is installed:

```bash
export DOTNET_ROLL_FORWARD=Major
dotnet test tests/System.Web.Tests.csproj
# run the host via the portable DLL:
dotnet samples/host/bin/Debug/net8.0/SampleHost.dll 8080
```

## Trademark notice

OpenWebForms is an independent project, **not affiliated with, endorsed by, or sponsored by
Microsoft.** "ASP.NET", ".NET", and "Web Forms" are trademarks of Microsoft Corporation, used here
only descriptively. The assembly is named `System.Web` for drop-in compatibility; it is unsigned and
distinct from Microsoft's strong-named assembly.