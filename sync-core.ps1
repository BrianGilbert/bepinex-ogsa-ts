# sync-core.ps1 — vendor the engine-agnostic C# core from ogs-ts-dotnet
# (the same source the Unity package ships), flattening Schema/ into
# OgsTelemetry/. Vendored files that no longer exist upstream are
# removed. Run from the repo root after pulling upstream changes.
$ErrorActionPreference = 'Stop'
$src = "../ogs-ts-dotnet/src/Ogs.Telemetry"
$dst = "OgsTelemetry"
if (-not (Test-Path "$src/TelemetryPublisher.cs")) {
    throw "source not found: $src (is ogs-ts-dotnet checked out as a sibling?)"
}
$changed = 0
$upstream = @(Get-ChildItem "$src/*.cs") + @(Get-ChildItem "$src/Schema/*.cs")
foreach ($f in $upstream) {
    if (-not (Test-Path "$dst/$($f.Name)") -or
        (Get-FileHash $f.FullName).Hash -ne (Get-FileHash "$dst/$($f.Name)").Hash) {
        Copy-Item $f.FullName "$dst/$($f.Name)"
        Write-Host "updated: $dst/$($f.Name)"
        $changed = 1
    }
}
$names = $upstream | ForEach-Object { $_.Name }
foreach ($f in Get-ChildItem "$dst/*.cs") {
    if ($names -notcontains $f.Name) {
        Remove-Item $f.FullName
        Write-Host "removed: $dst/$($f.Name)"
        $changed = 1
    }
}
if ($changed -eq 0) { Write-Host "core up to date" }
exit 0
