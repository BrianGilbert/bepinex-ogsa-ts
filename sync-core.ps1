# sync-core.ps1 — vendor the engine-agnostic C# core from ogsa-ts-dotnet
# (same source the Unity package ships; excludes the receiver). Run from
# the repo root after pulling upstream changes.
$ErrorActionPreference = 'Stop'
$src = "../ogsa-ts-dotnet/src/Ogsa.Telemetry"
$dst = "OgsaTelemetry"
if (-not (Test-Path "$src/TelemetryPublisher.cs")) {
    throw "source not found: $src (is ogsa-ts-dotnet checked out as a sibling?)"
}
$changed = 0
foreach ($f in Get-ChildItem "$src/*.cs") {
    if ($f.Name -eq "TelemetryReceiver.cs") { continue }
    if (-not (Test-Path "$dst/$($f.Name)") -or
        (Get-FileHash $f.FullName).Hash -ne (Get-FileHash "$dst/$($f.Name)").Hash) {
        Copy-Item $f.FullName "$dst/$($f.Name)"
        Write-Host "updated: $dst/$($f.Name)"
        $changed = 1
    }
}
$schema = "$src/Schema/ogsa_telemetry.capnp.cs"
if ((Get-FileHash $schema).Hash -ne (Get-FileHash "$dst/ogsa_telemetry.capnp.cs").Hash) {
    Copy-Item $schema "$dst/ogsa_telemetry.capnp.cs"
    Write-Host "updated: $dst/ogsa_telemetry.capnp.cs"
    $changed = 1
}
if ($changed -eq 0) { Write-Host "core up to date" }
exit 0
