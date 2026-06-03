$ErrorActionPreference = "Stop"
$root   = "D:\Claude Files\System.Web Project"
$ref    = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
$csc    = "C:\Program Files\dotnet\sdk\10.0.300\Roslyn\bincore\csc.dll"
$src    = Join-Path $root "src"
$outDir = Join-Path $root "artifacts"
$rsp    = Join-Path $outDir "compile.rsp"
$outDll = Join-Path $outDir "SystemWeb.dll"

# Exactly System.Web's real external dependency set (from dependencies.txt), minus System.Web itself.
$deps = @("mscorlib","System","System.Core","System.Data","System.Xml","System.Configuration","System.Drawing",
  "System.Web.Services","System.Web.ApplicationServices","System.Web.RegularExpressions","System.Design",
  "System.DirectoryServices","System.DirectoryServices.Protocols","System.EnterpriseServices","System.Runtime.Caching",
  "System.Security","System.ServiceProcess","System.Windows.Forms","System.ComponentModel.DataAnnotations",
  "Microsoft.Build.Framework","Microsoft.Build.Tasks.v4.0","Microsoft.Build.Utilities.v4.0")

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("/noconfig")
$lines.Add("/nostdlib+")
$lines.Add("/target:library")
$lines.Add("/langversion:latest")
$lines.Add("/unsafe+")
$lines.Add("/nowarn:1701,1702,0108,0114,0628,0809,0612,0618,0672,1591,3005,0419,0465,0469")
$lines.Add("/out:`"$outDll`"")
foreach ($d in $deps) {
  $p = Join-Path $ref "$d.dll"
  $lines.Add("/reference:`"$p`"")
}
Get-ChildItem -Path $src -Filter *.cs -Recurse | ForEach-Object { $lines.Add("`"$($_.FullName)`"") }

Set-Content -Path $rsp -Value $lines -Encoding UTF8
Write-Host "Response file: $rsp  ($($lines.Count) lines)"

& dotnet exec "$csc" "@$rsp"
Write-Host "csc exit code: $LASTEXITCODE"