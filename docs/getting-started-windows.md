# Getting Started — Run ASP.NET Web Forms on Windows with OpenWebForms

This guide shows how to run a classic ASP.NET **Web Forms** application on **Windows using modern .NET
(8.0+)** with OpenWebForms — **without IIS and without ASP.NET Core**. OpenWebForms provides its own
self‑contained managed HTTP host that compiles and runs `.aspx` pages directly.

> Use this to run legacy Web Forms apps on current .NET on a Windows box (dev machine, Windows Server,
> or a container) — no IIS site, app pool, or `inetmgr` required.
>
> **Status:** OpenWebForms is **alpha**. The supported flow here is inline‑code `.aspx` pages served by
> the standalone host; full separate‑code‑behind deployment is evolving (see [Limitations](#limitations)).

---

## 1. Prerequisites

- **.NET SDK 8.0** (LTS). Install via winget or the official installer:
  ```powershell
  winget install Microsoft.DotNet.SDK.8
  dotnet --version          # expect 8.0.x
  ```
  > Only have a newer SDK (e.g. .NET 9/10) and no .NET 8 runtime? You can still run OpenWebForms's
  > net8.0 binaries by setting **`DOTNET_ROLL_FORWARD=Major`** (shown below), or install the .NET 8 runtime.
- **git** (`winget install Git.Git`).
- *(Optional)* Visual Studio 2022 — you can build/run from the IDE instead of the CLI.

---

## 2. Get OpenWebForms

```powershell
git clone https://github.com/<you>/openwebforms.git
cd openwebforms
```

| Path | What it is |
|------|------------|
| `src\System.Web.csproj` | The clean‑room `System.Web` assembly |
| `samples\host\`         | The standalone HTTP host (your application host) |
| `samples\host\wwwroot\` | The web content root — **your `.aspx` pages go here** |

---

## 3. Build

```powershell
dotnet build src\System.Web.csproj -c Release
dotnet build samples\host\SampleHost.csproj -c Release
```

Both should report **0 errors**. *(In Visual Studio: open the folder/solution and build `SampleHost`.)*

---

## 4. Add your Web Forms page

Put your `.aspx` files in **`samples\host\wwwroot\`**. For a first run, create
`samples\host\wwwroot\hello.aspx`:

```aspx
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    protected void Page_Load(object sender, System.EventArgs e)
    {
        if (!IsPostBack) Message.Text = "Hello from Web Forms on Windows + .NET 8!";
    }
    protected void Submit_Click(object sender, System.EventArgs e)
    {
        Message.Text = "You typed: " + Name.Text;
    }
</script>
<!DOCTYPE html>
<html>
<head><title>OpenWebForms on Windows</title></head>
<body>
  <form id="form1" runat="server">
    <asp:Label id="Message" runat="server" />
    <p><asp:TextBox id="Name" runat="server" /></p>
    <asp:Button id="Submit" runat="server" Text="Send" OnClick="Submit_Click" />
  </form>
</body>
</html>
```

(The bundled `default.aspx` is a richer example — a data‑bound `GridView` plus a postback button.)

---

## 5. Run the host

```powershell
dotnet run --project samples\host -- 8080
```

or run the built binary directly:

```powershell
.\samples\host\bin\Release\net8.0\SampleHost.exe 8080
# (newer-runtime-only machines:)  $env:DOTNET_ROLL_FORWARD="Major"; dotnet .\samples\host\bin\Release\net8.0\SampleHost.dll 8080
```

You should see the load diagnostic confirming **OpenWebForms's** `System.Web` is active:

```
---- System.Web load diagnostic ----
  Assembly:        System.Web
  AssemblyVersion: 4.0.0.0
  Location:        ...\samples\host\bin\Release\net8.0\System.Web.dll
  LoadContext:     SystemWebHostContext
------------------------------------
System.Web standalone host listening at http://localhost:8080/
Serving physical directory: ...\wwwroot
```

> **Binding to `localhost` needs no admin rights.** If you change the host to listen on a hostname or
> `http://+:port/` (all interfaces), Windows `HttpListener` requires a URL ACL — either run the host
> elevated once, or reserve it:
> ```powershell
> netsh http add urlacl url=http://+:8080/ user=Everyone
> ```

---

## 6. Browse / verify

Open `http://localhost:8080/hello.aspx` in a browser, or:

```powershell
curl.exe -i http://localhost:8080/hello.aspx
# → HTTP/1.1 200 OK with a __VIEWSTATE hidden field inside <form>
```

The first hit to a page triggers a one‑time Roslyn compile; subsequent requests are fast. Submit the
form — your `Submit_Click` runs server‑side, a true Web Forms postback with byte‑compatible `__VIEWSTATE`.

Press **Ctrl+C** to stop.

---

## 7. Deploy for real

### Publish a folder

```powershell
dotnet publish samples\host\SampleHost.csproj -c Release -o C:\MyWebApp
copy myapp\*.aspx C:\MyWebApp\wwwroot\
cd C:\MyWebApp
.\SampleHost.exe 8080
```

### Run as a Windows Service

Using the built‑in service controller (the host runs as a console app; wrap it):

```powershell
# Simplest: use a service wrapper such as NSSM, or sc.exe with a small launcher.
sc.exe create MyWebApp binPath= "\"C:\MyWebApp\SampleHost.exe\" 8080" start= auto
sc.exe start MyWebApp
```

*(For graceful service lifecycle, NSSM — "the Non‑Sucking Service Manager" — wrapping `SampleHost.exe`
is a popular, robust option.)*

### Containers / reverse proxy

You can run the same way in a Windows container, or put a reverse proxy (nginx, **YARP**, or even IIS
as ARR) in front for TLS and port 80/443 while OpenWebForms listens on a high port. The point of
OpenWebForms is that **IIS is optional** — but nothing stops you from proxying through it if you already have it.

---

## How it works (1‑minute version)

```
HTTP → standalone host (System.Net.HttpListener)
     → HttpWorkerRequest → HttpRuntime.ProcessRequest → HttpApplication pipeline
     → PageHandlerFactory → BuildManager (.aspx parse → C# codegen → Roslyn compile)
     → Page lifecycle → ViewState → Render → flush
```

Because the .NET shared framework ships a throw‑only `System.Web` facade, the host loads OpenWebForms's
real `System.Web` through a dedicated **`AssemblyLoadContext`** (`SystemWebHostContext`), and
`SampleHost.csproj` carries MSBuild targets that keep our `System.Web` app‑local. **If you build your
own host instead of using `samples\host`, copy those targets + `AlcBootstrap.cs` from it.**

---

## Limitations

- **Inline‑code `.aspx`** (`<script runat="server">`) is the proven path; separate **code‑behind**
  assemblies and full `web.config` handler/module wiring are still evolving.
- Some features are intentionally stubbed (SQL/AD membership, out‑of‑proc session, WMI/IIS‑COM bits) —
  see the README status matrix.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `TypeLoadException` / `System.Web.Server.*` missing | The framework facade loaded instead of ours — use the unmodified `SampleHost` (it has the RAR‑strip targets + `AlcBootstrap`); run the published `.exe`/`.dll`. |
| `HttpListenerException: Access is denied` | You're binding a non‑`localhost` prefix without a URL ACL — run elevated once or `netsh http add urlacl ...`. |
| Wrong runtime / "install .NET" prompt | Install the .NET 8 runtime, or set `DOTNET_ROLL_FORWARD=Major`. |
| 500 on first request | Check the host console for the `.aspx` Roslyn compile error. |

You're now running ASP.NET Web Forms on Windows with modern .NET — no IIS required. 🪟
