# Getting Started — Deploy ASP.NET Web Forms on Linux with OpenWebForms

This guide walks you through running a classic ASP.NET **Web Forms** application on **Linux** (Ubuntu)
using OpenWebForms — **without IIS, without ASP.NET Core, without Mono**. OpenWebForms ships its own
standalone managed HTTP host that compiles and runs `.aspx` pages directly on modern .NET.

> **Prerequisite:** a working Linux box with the .NET SDK. (If you're using WSL, see the separate
> "Install WSL + .NET" guide first — this page assumes `dotnet` already works in your shell.)
>
> **Status:** OpenWebForms is **alpha**. The path documented here — inline‑code `.aspx` pages served by
> the standalone host — is the proven, supported flow. Full separate‑code‑behind app deployment is
> evolving; see [Limitations](#limitations).

---

## 1. Install the .NET SDK (if you haven't)

OpenWebForms targets **.NET 8.0 (LTS)**. The cleanest setup is to install the .NET 8 SDK so no runtime
roll‑forward is needed:

```bash
# Ubuntu (Microsoft feed)
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version          # expect 8.0.x
```

> **Already have a newer SDK only (e.g. .NET 9/10) and no .NET 8 runtime?** That's fine — you can run
> OpenWebForms's net8.0 assemblies on a newer runtime by setting **`DOTNET_ROLL_FORWARD=Major`** (shown
> in the run steps below). Installing the .NET 8 runtime is the alternative.

You also need `git`:

```bash
sudo apt-get install -y git
```

---

## 2. Get OpenWebForms

```bash
git clone https://github.com/<you>/openwebforms.git
cd openwebforms
```

Repository layout you'll use:

| Path | What it is |
|------|------------|
| `src/System.Web.csproj` | The clean‑room `System.Web` assembly |
| `samples/host/`         | The standalone HTTP host (your application host) |
| `samples/host/wwwroot/` | The web content root — **your `.aspx` pages go here** |

---

## 3. Build

```bash
# Build the clean-room System.Web
dotnet build src/System.Web.csproj -c Release

# Build the standalone host (references System.Web; copies it app-local)
dotnet build samples/host/SampleHost.csproj -c Release
```

Both should report **0 errors**.

---

## 4. Add your Web Forms page

Drop your `.aspx` files into **`samples/host/wwwroot/`**. For a first run, create
`samples/host/wwwroot/hello.aspx` with an inline‑code page:

```aspx
<%@ Page Language="C#" AutoEventWireup="true" %>
<script runat="server">
    protected void Page_Load(object sender, System.EventArgs e)
    {
        if (!IsPostBack) Message.Text = "Hello from Web Forms on Linux!";
    }
    protected void Submit_Click(object sender, System.EventArgs e)
    {
        Message.Text = "You typed: " + Name.Text;
    }
</script>
<!DOCTYPE html>
<html>
<head><title>OpenWebForms on Linux</title></head>
<body>
  <form id="form1" runat="server">
    <asp:Label id="Message" runat="server" />
    <p><asp:TextBox id="Name" runat="server" /></p>
    <asp:Button id="Submit" runat="server" Text="Send" OnClick="Submit_Click" />
  </form>
</body>
</html>
```

(The bundled `default.aspx` is a richer example — a `GridView` bound to data plus a postback button.)

---

## 5. Run the host

Run the host **via its portable DLL** (most reliable across environments), passing a port:

```bash
# If you installed the .NET 8 runtime:
dotnet samples/host/bin/Release/net8.0/SampleHost.dll 8080

# If you only have a newer runtime (e.g. .NET 9/10):
DOTNET_ROLL_FORWARD=Major dotnet samples/host/bin/Release/net8.0/SampleHost.dll 8080
```

You should see:

```
---- System.Web load diagnostic ----
  Assembly:        System.Web
  AssemblyVersion: 4.0.0.0
  Location:        .../samples/host/bin/Release/net8.0/System.Web.dll
  LoadContext:     SystemWebHostContext
------------------------------------
System.Web standalone host listening at http://localhost:8080/
Serving physical directory: .../wwwroot
```

The `LoadContext: SystemWebHostContext` line confirms the host loaded **OpenWebForms's** clean‑room
`System.Web` (not the .NET shared‑framework facade).

> `dotnet run --project samples/host -- 8080` also works on a normal Linux filesystem. Prefer the
> portable‑DLL form if you're running from a network/`/mnt` mount where the native apphost may not launch.

---

## 6. Browse / verify

From another shell (or your browser):

```bash
curl -i http://localhost:8080/hello.aspx
# → HTTP/1.1 200 OK, an HTML page with a __VIEWSTATE hidden field inside <form>
```

The first request to a page triggers a one‑time Roslyn compile (parse → code‑gen → compile), so it may
take a moment; subsequent requests are fast. Submitting the form posts back and runs your
`Submit_Click` handler — a real Web Forms postback, with byte‑compatible `__VIEWSTATE`.

Press **Ctrl+C** to stop the host.

---

## 7. Deploy for real

### Option A — publish a self‑contained folder

```bash
dotnet publish samples/host/SampleHost.csproj -c Release -o /opt/mywebapp
# put your .aspx pages in the published content root:
cp -r myapp/*.aspx /opt/mywebapp/wwwroot/
cd /opt/mywebapp
dotnet SampleHost.dll 8080      # add DOTNET_ROLL_FORWARD=Major if needed
```

### Option B — run as a systemd service

`/etc/systemd/system/mywebapp.service`:

```ini
[Unit]
Description=OpenWebForms app
After=network.target

[Service]
WorkingDirectory=/opt/mywebapp
ExecStart=/usr/bin/dotnet /opt/mywebapp/SampleHost.dll 8080
Environment=DOTNET_ROLL_FORWARD=Major
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now mywebapp
curl http://localhost:8080/hello.aspx
```

### Option C — Docker

`Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /s
COPY . .
RUN dotnet publish samples/host/SampleHost.csproj -c Release -o /app
# copy your app's .aspx pages into the content root
RUN cp -r /s/myapp/*.aspx /app/wwwroot/ 2>/dev/null || true

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SampleHost.dll", "8080"]
```

```bash
docker build -t mywebapp .
docker run -p 8080:8080 mywebapp
```

### Putting it behind a real domain / port 80/443

Run the host on a high port (e.g. 8080) and put **nginx** in front as a reverse proxy for TLS and
port 80/443 — this is the recommended production pattern (don't bind privileged ports directly):

```nginx
server {
    listen 80;
    server_name myapp.example.com;
    location / { proxy_pass http://127.0.0.1:8080; proxy_set_header Host $host; }
}
```

---

## How it works (1‑minute version)

```
HTTP → standalone host (System.Net.HttpListener)
     → HttpWorkerRequest → HttpRuntime.ProcessRequest → HttpApplication pipeline
     → PageHandlerFactory → BuildManager (.aspx parse → C# codegen → Roslyn compile)
     → Page lifecycle → ViewState → Render → flush
```

The .NET shared framework ships a throw‑only `System.Web` facade, so the host loads OpenWebForms's real
`System.Web` through a dedicated **`AssemblyLoadContext`** (`SystemWebHostContext`). The
`samples/host/SampleHost.csproj` also carries MSBuild targets that strip the framework's `System.Web`
reference so our copy ships app‑local. **If you build your own host project, copy those bits from
`SampleHost.csproj` and `AlcBootstrap.cs`** (or just use/clone `samples/host`).

---

## Limitations

- **Inline‑code `.aspx`** (`<script runat="server">`) is the proven path. Separate **code‑behind**
  assemblies and full `web.config`‑driven `<httpHandlers>`/`<httpModules>` wiring are evolving — test
  before relying on them.
- A few features are intentionally stubbed (SQL/AD membership providers, out‑of‑proc session,
  Windows‑only WMI/IIS bits). See the README status matrix.
- Filesystem is **case‑sensitive** on Linux — make sure your `.aspx` filenames and references match case.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `TypeLoadException` / `System.Web.Server.*` not found | The framework facade got loaded instead of ours. Run via the **portable DLL** and ensure you're using the unmodified `SampleHost` (with its RAR‑strip targets + `AlcBootstrap`). |
| `You must install .NET to run this application` / wrong runtime | Install the .NET 8 runtime, **or** set `DOTNET_ROLL_FORWARD=Major`. |
| `Access denied` binding a port | Use a high port (≥1024) and put nginx in front for 80/443. Don't run as root just to bind 80. |
| Page returns 500 on first hit | Check the host console — Roslyn compile errors for the `.aspx` are reported there. |

You now have classic ASP.NET Web Forms running on Linux. 🐧
