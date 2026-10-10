# prepare-refs.ps1 — fetch the real BepInEx.dll reference (LGPL,
# redistributable) into lib/. The UnityEngine reference is our committed
# compile-only stub (vendor/UnityEngineStub) — Unity's own DLLs are not
# redistributed.
param(
    [string]$Version = "5.4.23.5"
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
New-Item -ItemType Directory -Force -Path "$root/lib" | Out-Null
if (Test-Path "$root/lib/BepInEx.dll") { Write-Host "lib/BepInEx.dll present"; exit 0 }

$tag = "v$Version"
$urls = gh api "repos/BepInEx/BepInEx/releases/tags/$tag" --jq '.assets[].browser_download_url'
$url = $urls | Where-Object { $_ -match 'win_x64' } | Select-Object -First 1
if (-not $url) { throw "no win_x64 asset in BepInEx $tag" }
$zip = "$env:TEMP\bepinex-$Version.zip"
Invoke-WebRequest -Uri $url -OutFile $zip
Expand-Archive -Path $zip -DestinationPath "$env:TEMP\bepinex-$Version" -Force
Copy-Item "$env:TEMP\bepinex-$Version/BepInEx/core/BepInEx.dll" "$root/lib/BepInEx.dll"
Write-Host "fetched BepInEx.dll $Version -> lib/"
exit 0
