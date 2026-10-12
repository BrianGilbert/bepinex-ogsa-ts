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
- Two flavors, one release zip: `plugin/` (BepInEx 5 Mono, net472)
  and `plugin-il2cpp/` (BepInEx 6 Unity-IL2CPP, net6.0), zipped under
  `mono/` and `il2cpp/` subfolders. Same GUID/config keys/wire
  behaviour; keep the glue files mirrored when either changes.
- IL2CPP works because BepInEx 6 runs plugins on its own .NET 6
  CoreCLR sidecar — Capnp reflection is fine (earlier "doesn't survive
  interop" ruling superseded); only game-object access crosses
  Il2CppInterop. `plugin-il2cpp` refs BepInEx.Unity.IL2CPP 6.0.0-be.788
  (== current official IL2CPP distribution; pin, don't float) + its
  Il2CppInterop.ReferenceLibs from nuget.bepinex.dev, compile-only —
  none of their DLLs ship in the zip. Do NOT use UnityEngine.Modules
  (the BepInEx-6-Mono template default): it ships vanilla Mono refs —
  MonoBehaviour isn't injectable and value types aren't interop-mapped.
  net-core classlibs need
  CopyLocalLockFileAssemblies=true to emit the serializer closure.
- Il2CppInterop (1.5.x lineage) does NOT map UnityEngine value types to
  System.Numerics — proxies keep Unity's names/layout (lowercase
  fields); convert per component exactly like the Mono flavor.
- Plugin targets net472 (Capnp.net ns2.0 assets need ≥4.7.1). IL2CPP
  games run the net6.0 flavor on BepInEx 6 be.788+.
- Product docs describe current state only; history lives in the
  Open-Game-Standards-Alliance knowledge base.
