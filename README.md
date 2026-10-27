# bepinex-ogs-ts

> [!IMPORTANT]
> **In active development.** Validated in Subnautica Below Zero, Hyper
> Storm and Exo One (Mono) and Sable (IL2CPP), but every Unity game sets
> up its player differently, so expect the odd title where tracking
> needs a config tweak. Please report what you find on
> [Discord](https://discord.gg/yyCp5D2Ccp). If a game misbehaves and I
> don't already own it, a key for it is the fastest way to get it fixed.
>
> Development is funded by
> [sponsorship](https://github.com/sponsors/BrianGilbert), which moves this
> and the rest of the [OGS](https://github.com/Open-Game-Standards)
> work forward.

[BepInEx](https://github.com/BepInEx/BepInEx) plugin that publishes
[OGS Telemetry Standard (OGS-TS)](https://github.com/Open-Game-Standards/OGS-Telemetry-Standard)
motion telemetry from **any Unity game BepInEx injects — Mono or
IL2CPP** — zero game modification, LAN-only UDP multicast. Motion
platforms, haptic hardware, and dashboards on the local network can
follow the game. Motion frames carry pose **plus body dynamics**
(specific force + angular rates — the direct motion-cueing inputs,
derived from the pawn pose). The release zip carries both flavors in `mono/` and
`il2cpp/` subfolders; each is the same plugin built for its BepInEx
generation. Companion plugins:
[uevr-ogs-ts](https://github.com/BrianGilbert/uevr-ogs-ts) (UE VR) ·
[ue4ss-ogs-ts](https://github.com/BrianGilbert/ue4ss-ogs-ts) (UE flatscreen).

Support & discussion: [Discord](https://discord.gg/yyCp5D2Ccp)

Status: **in-game verified** (Mono + IL2CPP) — both flavors
observed publishing motion telemetry in live games (Mono on BepInEx 5,
IL2CPP on BepInEx 6 be.788), on top of the shared core's standalone
wire-path checks (150-tick synthetic pawn with body-dynamics
assertions, observed end-to-end on a real receiver).

## What it tracks — the pawn, not the camera

The motion object is the **player pawn** (tag `Player` by default, or
the main camera's root transform as a flatscreen pawn proxy). Unity's
world frame matches OGS-TS natively — values pass through unmapped.
The camera itself is engine-smoothed and a rig following it would feed
platform motion back into telemetry.

Body dynamics ride the `bodyDynamics` union variant in every motion
frame: **specific force** (m/s², accelerometer-style: `f = a − g`,
+1 g on the up axis at rest — what an IMU reads) and **angular
velocity** (rad/s, C-4 body-frame sense: +x nose-up, +y nose-right,
+z right-side-down), derived each frame from the pawn's pose by the
vendored `BodyDynamicsDeriver` (position-delta velocity, τ = 0.1 s
acceleration low-pass, teleport reset, quaternion-delta rates with no
Euler representation-flip artifacts — see GameConn `docs/cueing.md`
for the receiver-side value). Disable with the `Dynamics` config entry
(default on; takes effect when the pawn is (re)resolved).

Pauses are reported on the ratified `session.paused` channel (standard
CONVENTIONS C-11): while the game's `Time.timeScale` is 0 the plugin
keeps streaming on real time with the pause flag set and no dynamics, so
motion rigs come to rest instead of freezing mid-move or dropping the
session. Games that pause without touching `timeScale` aren't detected.
Disable with the `PauseDetection` config entry (default on).

The session gravity in discovery is the game's own `Physics.gravity`
(standard CONVENTIONS C-12), polled once a second, and the derived
specific force reacts to the same gravity, so heave at rest reads the
game's |g| rather than an assumed 9.80665. Games without Unity's 3D
physics module keep standard gravity. Disable with the
`GravityFromPhysics` config entry (default on).

## Compatibility

| Unity band | Status | Notes |
|---|---|---|
| Any IL2CPP game (Unity 2019+ typical) | ✅ | `il2cpp/` flavor on [BepInEx 6](https://builds.bepinex.dev/projects/bepinex_be) (be.788+); plugin code runs on BepInEx's .NET 6 sidecar, not inside IL2CPP |
| 2018.3 – 2021.1 (Mono) | ✅ | .NET 4.x runtime is the default (Mono ≈ 4.7.1) |
| 2021.2 – 2023.x / Unity 6 (Mono builds) | ✅ | Mono 6 / .NET 4.8-era runtime |
| 2017.2 – 2018.2 (Mono) | ⚠️ | works if the developer selected the then-experimental ".NET 4.x" scripting profile |
| ≤ 2017.1, or any game on the legacy .NET 3.5 profile | ❌ | the vendored core (System.Memory) can't target net35 — older BepInEx catalog |

The binding constraint on Mono is the game's **.NET runtime**, not
Unity's API — every Unity surface this plugin touches has been
ABI-stable since the Unity 5 era. **Quick check** for a specific game,
look in `<game>/<Game>_Data/`:

- `GameAssembly.dll` present → **IL2CPP** → BepInEx 6 + the `il2cpp/`
  flavor
- only `Assembly-CSharp.dll` / `UnityEngine.dll` → **Mono** → check the
  Unity version (BepInEx prints it on first run) → 2018.3+ should work

If the game is Mono and 2018.3+ and the plugin still doesn't load, check the BepInEx console/log — a `TargetFramework` load failure means the game shipped on the legacy profile.

**Two 10-second checks** (no guessing) for a Mono game:

- **Before installing anything:** right-click
  `<game>\<Game>_Data\Managed\mscorlib.dll` → Properties → Details —
  File version `2.0.x` = legacy .NET 3.5 (incompatible);
  `4.0.x` = .NET 4.x (compatible)
- **From the BepInEx log** (`<game root>\BepInEx\LogOutput.log`):
  `CLR runtime version: 2.0.50727.x` = legacy (incompatible);
  `4.0.30319.x` = .NET 4.x (compatible). `0 plugins to load` next to a
  `Chainloader` line means BepInEx rejected the plugin — look up for
  the reason.

## Install

1. Install BepInEx (x64) into the game — **which flavor depends on the
   game's scripting backend**:

   - **Mono game** (no `GameAssembly.dll`): [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
     (5.4.22+). Mono games with a .NET 4.7.2+ runtime only.
   - **IL2CPP game** (`GameAssembly.dll` present): [BepInEx 6 Unity-IL2CPP](https://builds.bepinex.dev/projects/bepinex_be)
     (be.788 or newer) — grab the `BepInEx-Unity.IL2CPP-win-x64` zip.

   **Where:** extract the BepInEx zip into the game's installation root —
   the folder containing the game's `.exe` (Steam: right-click the game →
   Manage → **Browse local files**). This creates the `BepInEx\` folder
   next to the exe; run the game once to finish setup (BepInEx 6 spends
   the first launch generating interop assemblies — expect a longer
   black screen and check `LogOutput.log` before assuming failure).
2. From the release zip, extract the **matching subfolder's contents** —
   `mono/` for BepInEx 5, `il2cpp/` for BepInEx 6 — into that game's
   `<game root>\BepInEx\plugins\` — **every DLL in it** (IL2CPP:
   `ogs_telemetry.dll` alone; Mono: it plus four .NET Framework
   backport DLLs such as `System.Memory.dll`). E.g.
   `C:\...\steamapps\common\<GameName>\BepInEx\plugins\`.
   If `plugins\` doesn't exist yet, create it. BepInEx plugins are
   per-game: the DLLs only load for the game you place them in.
   Don't mix flavors — the `il2cpp/` DLL is a .NET 6 assembly BepInEx 5
   cannot load, and vice versa.
3. Run the game once; configure `<game>/BepInEx/config/ogs.telemetry.cfg`:
   ```ini
   [Transport] Group = 239.255.17.17   ; Port = 40123   ; Ttl = 1
   [Telemetry] MotionHz = 60           ; ObjectName = player   ; PauseDetection = true
               GravityFromPhysics = true
   [Pawn] Tag = Player                 ; CameraRootFallback = true
   ```
4. Pair with a receiver — e.g. ogs-ts-dotnet's
   `dotnet run --project src/TelemetryReceive`.

If the tag finds nothing, the plugin falls back to the camera root and
logs a pointer — set `Pawn.Tag` to your game's player tag (BepInEx
console `logoutput.log`) if the default misses.

### Rai Pal deployments

Rai Pal ships its own BepInEx copy and installs it under
`%APPDATA%\raicuparta\rai-pal\data\installed-mods\<hash>\bepinex\`
(hash-named per game) — **not** the game root. Plugins go into that
folder's `BepInEx\plugins\`, and logs land there too. The plugin
derives the game name from the process (Rai Pal's hash folder name is
meaningless), so expect `game=<exe name>` on the wire.

## Standalone test (no game)

`dotnet run --project Smoke` — drives the same vendored core publisher
with a synthetic pawn; exit 0 = pass. TelemetryReceive beside it shows
`bepinex_smoke` on the wire.

## How it builds without Unity's DLLs

Both flavors compile the entire vendored Ogs.Telemetry core
(`sync-core.ps1`) straight into the plugin — it writes Cap'n Proto frames
itself, so there is no serializer DLL, and no game DLLs are required at
build time:

- **Mono** (`plugin/`, net472): `vendor/UnityEngineStub` is a
  compile-only stub assembly (named `UnityEngine`) mirroring the small
  stable API surface the plugin touches; the real game's UnityEngine
  provides the implementation at runtime. `prepare-refs.ps1` fetches
  the real LGPL BepInEx.dll reference.
- **IL2CPP** (`plugin-il2cpp/`, net6.0): references the official
  BepInEx NuGet feed — `BepInEx.Unity.IL2CPP` (be.788, the current
  IL2CPP distribution) plus its interop-mapped UnityEngine reference
  libs, both compile-only. The plugin runs on BepInEx's .NET 6
  CoreCLR sidecar, not inside IL2CPP — only game-object access crosses
  the interop boundary.

The Mono flavor's .NET Framework backport DLLs (System.Memory and its
closure) ride in the release zip as siblings under `mono/`; the IL2CPP
flavor is the single plugin DLL under `il2cpp/`.

## Layout

| Path | Contents |
|---|---|
| `plugin/` | BepInEx 5 Mono glue (config, pawn ladder, Update tick) |
| `plugin-il2cpp/` | BepInEx 6 IL2CPP glue (BasePlugin + injected tick component) |
| `OgsTelemetry/` | vendored engine-agnostic core (source) |
| `vendor/UnityEngineStub/` | compile-only UnityEngine stub (Mono flavor) |
| `Smoke/` | standalone wire-path verification |
