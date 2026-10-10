# bepinex-ogsa-ts — working conventions

- Motion object is the player PAWN (tag Player / camera-root proxy),
  not the camera — KB ruling C-20261010-MBP-08. Unity frame maps to
  OGSA-TS natively (no conversion); the vendored C# core IS the wire.
- OgsaTelemetry/ is vendored source from ../ogsa-ts-dotnet (sync-core.ps1,
  receiver excluded). The core carries a netstandard2.0 TFM specifically
  for this plugin (BepInEx 5 Mono runs .NET Framework).
- vendor/UnityEngineStub is a compile-only stub ASSEMBLY NAMED
  "UnityEngine" (BepInEx.dll's MonoBehaviour lives in the classic
  facade name) — extend only with what the plugin references; the real
  game module provides implementation at runtime.
- lib/BepInEx.dll is fetched (never committed) by prepare-refs.ps1
  (LGPL; asset resolved via gh api — set GH_TOKEN in CI).
- Plugin targets net472 (Capnp.net ns2.0 assets need ≥4.7.1). IL2CPP
  games unsupported: Capnp reflection doesn't survive interop.
- Product docs describe current state only; history lives in the
  Open-Game-Standards-Alliance knowledge base.
