# bepinex-ogsa-ts

[BepInEx](https://github.com/BepInEx/BepInEx) 5 plugin that publishes
[OGSA Telemetry Standard (OGSA-TS)](https://github.com/Open-Game-Standards-Alliance/OGSA-Telemetry-Standard)
motion telemetry from **any Unity (Mono) game BepInEx injects** — zero
game modification, LAN-only UDP multicast. Motion platforms, haptic
hardware, and dashboards on the local network can follow the game.
Companion plugins: [uevr-ogsa-ts](https://github.com/BrianGilbert/uevr-ogsa-ts) (UE VR) ·
[ue4ss-ogsa-ts](https://github.com/BrianGilbert/ue4ss-ogsa-ts) (UE flatscreen).

Support & discussion: [Discord](https://discord.gg/yyCp5D2Ccp)

Status: **smoke-verified** — standalone wire-path checks pass (150-tick
synthetic pawn, observed end-to-end on a real receiver) and the plugin
DLL builds against BepInEx 5.4.23. **In-game testing pending.**

## What it tracks — the pawn, not the camera

The motion object is the **player pawn** (tag `Player` by default, or
the main camera's root transform as a flatscreen pawn proxy). Unity's
world frame matches OGSA-TS natively — values pass through unmapped.
The camera itself is engine-smoothed and a rig following it would feed
platform motion back into telemetry.

## Compatibility

| Unity band | Status | Notes |
|---|---|---|
| 2018.3 – 2021.1 | ✅ best case | .NET 4.x runtime is the default (Mono ≈ 4.7.1) |
| 2021.2 – 2023.x / Unity 6 (Mono builds) | ✅ | Mono 6 / .NET 4.8-era runtime |
| 2017.2 – 2018.2 | ⚠️ | works if the developer selected the then-experimental ".NET 4.x" scripting profile |
| ≤ 2017.1 (or any game on the legacy .NET 3.5 profile) | ❌ | the serializer can't target net35 — older BepInEx catalog |
| Any IL2CPP game | ❌ | needs the (not yet built) BepInEx 6 variant |

The binding constraint is the game's **.NET runtime**, not Unity's API — every Unity surface this plugin touches has been ABI-stable since the Unity 5 era. **Quick check** for a specific game, look in `<game>/<Game>_Data/`:

- `GameAssembly.dll` present → **IL2CPP** → out of scope
- only `Assembly-CSharp.dll` / `UnityEngine.dll` → **Mono** → check the Unity version (BepInEx prints it on first run) → 2018.3+ should work

If the game is Mono and 2018.3+ and the plugin still doesn't load, check the BepInEx console/log — a `TargetFramework` load failure means the game shipped on the legacy profile.

## Install

1. [Install BepInEx 5](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
   (x64) into the game. Mono games with a .NET 4.7.2+ runtime only —
   IL2CPP games are not supported (yet).
   **Where:** extract the BepInEx zip into the game's installation root —
   the folder containing the game's `.exe` (Steam: right-click the game →
   Manage → **Browse local files**). This creates the `BepInEx\` folder
   next to the exe; run the game once to finish setup.
2. Drop `ogsa_telemetry.dll` into that game's
   `<game root>\BepInEx\plugins\` — e.g.
   `C:\...\steamapps\common\<GameName>\BepInEx\plugins\`.
   If `plugins\` doesn't exist yet, create it. BepInEx plugins are
   per-game: the DLL only loads for the game you place it in.
3. Run the game once; configure `<game>/BepInEx/config/ogsa.telemetry.cfg`:
   ```ini
   [Transport] Group = 239.255.17.17   ; Port = 40123   ; Ttl = 1
   [Telemetry] MotionHz = 60           ; ObjectName = player
   [Pawn] Tag = Player                 ; CameraRootFallback = true
   ```
4. Pair with a receiver — e.g. ogsa-ts-dotnet's
   `dotnet run --project src/TelemetryReceive`.

If the tag finds nothing, the plugin falls back to the camera root and
logs a pointer — set `Pawn.Tag` to your game's player tag (BepInEx
console `logoutput.log`) if the default misses.

## Standalone test (no game)

`dotnet run --project Smoke` — drives the same vendored core publisher
with a synthetic pawn; exit 0 = pass. TelemetryReceive beside it shows
`bepinex_smoke` on the wire.

## How it builds without Unity's DLLs

`vendor/UnityEngineStub` is a compile-only stub assembly (named
`UnityEngine`) mirroring the small stable API surface the plugin
touches; the real game's UnityEngine provides the implementation at
runtime. `prepare-refs.ps1` fetches the real LGPL BepInEx.dll
reference. The entire Ogsa.Telemetry C# core is vendored
(`sync-core.ps1`) and compiled into the plugin — one DLL, no extra deps.

## Layout

| Path | Contents |
|---|---|
| `plugin/` | BepInEx glue (config, pawn ladder, Update tick) |
| `OgsaTelemetry/` | vendored engine-agnostic core (source) |
| `vendor/UnityEngineStub/` | compile-only UnityEngine stub |
| `Smoke/` | standalone wire-path verification |
