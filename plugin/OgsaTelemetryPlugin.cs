// OgsaTelemetryPlugin.cs — OGSA-TS telemetry for BepInEx (Unity, Mono).
//
// Publishes the player pawn's world pose (the controlled body — not the
// camera; KB ruling C-20261010-MBP-08) as OGSA-TS motion telemetry from
// any Unity game BepInEx injects. Unity's world frame matches OGSA-TS
// natively (LH, Z-fwd, Y-up, metres) — pose values pass through with no
// remap. Zero game modification; LAN-only UDP multicast.
//
// The entire Ogsa.Telemetry C# core (vendored source, the same files the
// Unity package ships) is compiled into this assembly — one DLL, no deps
// beyond the game's own UnityEngine.
//
// Pawn discovery ladder (first hit wins; all overridable via config):
//   1. Configured GameObject tag (default "Player")
//   2. Camera.main transform root (flatscreen: camera sits on the player)
// Re-resolves automatically if the pawn is destroyed (scene change).
//
// The object NAME stays the stable role name ("player" by default) —
// receivers match motion frames to discovery by it. What is actually
// being tracked is surfaced as the discovery typeLabel: the pawn's
// GameObject name with Unity instantiation suffixes stripped
// ("Player(Clone)" / "Player (2)" -> "Player"), updated on every
// (re)resolution; a changed label re-sends discovery per the standard's
// re-send-on-change rule. Config ObjectTypeLabel overrides.
using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Ogsa.Telemetry;

namespace Ogsa.BepInEx
{
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class OgsaTelemetryPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "ogsa.telemetry";
    public const string PluginName = "OGSA-TS Telemetry";
    public const string PluginVersion = "0.1.0";

    TelemetryPublisher _publisher = null!;
    TelemetrySender _sender = null!;
    Transform _pawn = null!;
    float _lastResolveAttempt = -1f;
    bool _warnedNoPawn;

    ConfigEntry<string> _group = null!;
    ConfigEntry<int> _port = null!;
    ConfigEntry<int> _ttl = null!;
    ConfigEntry<double> _motionHz = null!;
    ConfigEntry<string> _objectName = null!;
    ConfigEntry<string> _typeLabel = null!;
    ConfigEntry<string> _pawnTag = null!;
    ConfigEntry<bool> _useCameraRoot = null!;
    ConfigEntry<bool> _dynamics = null!;
    ConfigEntry<bool> _pauseDetect = null!;
    bool _wasPaused;

    // pose → body-dynamics derivation (vendored core, same semantics as
    // the C++ twin in ogsa-ts-unreal): specific force accelerometer-style
    // (+9.80665 heave at rest), angular velocity in the C-4 component
    // sense — see BodyDynamicsDeriver's class doc
    BodyDynamicsDeriver _deriver = new();

    void Awake()
    {
        _group = Config.Bind("Transport", "Group", "239.255.17.17",
                             "UDP multicast group");
        _port = Config.Bind("Transport", "Port", 40123, "UDP multicast port");
        _ttl = Config.Bind("Transport", "Ttl", 1,
                           "Multicast TTL (1 = local subnet)");
        _motionHz = Config.Bind("Telemetry", "MotionHz", 60.0,
                                "Motion frame rate cap");
        _objectName = Config.Bind("Telemetry", "ObjectName", "player",
                                  "Object name in discovery");
        _typeLabel = Config.Bind("Telemetry", "ObjectTypeLabel", "",
                                 "Object typeLabel in discovery (display " +
                                 "flavor). Empty = auto: the tracked pawn's " +
                                 "GameObject name with instantiation " +
                                 "suffixes stripped");
        _pawnTag = Config.Bind("Pawn", "Tag", "Player",
                               "GameObject tag that marks the player pawn");
        _useCameraRoot = Config.Bind("Pawn", "CameraRootFallback", true,
                                     "If the tag finds nothing, use the main " +
                             "camera's root transform (flatscreen pawn proxy)");
        _dynamics = Config.Bind("Telemetry", "Dynamics", true,
                                "Send the bodyDynamics union variant " +
                                "(specific force + angular rates, derived " +
                                "from the pawn pose — the direct motion-cueing " +
                                "inputs). Takes effect when the pawn is " +
                                "(re)resolved");
        _pauseDetect = Config.Bind("Telemetry", "PauseDetection", true,
                                   "Report the game as paused (OGSA-TS " +
                                   "session.paused, CONVENTIONS C-11) while " +
                                   "Time.timeScale is 0, so motion rigs come " +
                                   "to rest instead of freezing mid-move");
    }

    void OnEnable()
    {
        _sender = new TelemetrySender(_group.Value, _port.Value, _ttl.Value,
                                      loopback: true);
        _publisher = new TelemetryPublisher(_sender, GameName(), _motionHz.Value);
        _publisher.DeclareObject(_objectName.Value);
        _publisher.DeclarePause();
        if (!string.IsNullOrEmpty(_typeLabel.Value))
            _publisher.SetObjectTypeLabel(_typeLabel.Value); // config wins
        UnityEngine.Debug.Log("[ogsa] OGSA-TS telemetry enabled: pawn '" +
                              _objectName.Value + "' -> " + _group.Value + ":" +
                              _port.Value);
    }

    void OnDisable()
    {
        _publisher?.Dispose();
        _publisher = null!;
        _sender?.Dispose();
        _sender = null!;
    }

    void Update()
    {
        var pub = _publisher;
        if (pub == null) return;

        if (_pawn == null && Time.time - _lastResolveAttempt > 1f)
        {
            _lastResolveAttempt = Time.time;
            _pawn = ResolvePawn();
            if (_pawn != null)
            {
                pub.BindPose(() => SafePos(), () => SafeRot());
                if (_dynamics.Value)
                    pub.BindBodyDynamics(() =>
                        (_deriver.SpecificForce, _deriver.AngularVelocity));
                _deriver = new BodyDynamicsDeriver(); // fresh state per pawn
                if (string.IsNullOrEmpty(_typeLabel.Value))
                    pub.SetObjectTypeLabel(CleanPawnName(_pawn.name));
                _warnedNoPawn = false;
            }
        }
        // derivation runs every frame the pawn exists (not just at the
        // publisher's cadence), so derivative denominators stay small
        // (is-pattern: a plain reference check — a destroyed transform
        // still flows into SafePos's catch, same as the pose getters)
        // C-11: Update keeps running while Time.timeScale is 0, so the
        // stream continues through a pause on real time, reporting the
        // pause and deriving nothing across it
        bool paused = _pauseDetect.Value && Time.timeScale == 0f;
        if (paused != pub.Paused) pub.SetPaused(paused);
        if (_pawn is { } && !paused)
        {
            if (_wasPaused)
                _deriver.Reset(SafePos(), SafeRot(), (double)Time.time);
            else
                _deriver.Sample((double)Time.time, SafePos(), SafeRot());
        }
        _wasPaused = paused;
        pub.Tick((double)Time.realtimeSinceStartup);
    }

    // pose getters that survive scene changes and menu phases:
    // a destroyed transform (scene unload) re-triggers resolution, and a
    // degenerate quaternion (menu-state fallback reads) ships identity
    // instead of a zero norm that trips receiver validation
    System.Numerics.Vector3 SafePos()
    {
        try { var p = _pawn.position; return new System.Numerics.Vector3(p.x, p.y, p.z); }
        catch { _pawn = null!; return default; }
    }

    System.Numerics.Quaternion SafeRot()
    {
        try
        {
            var r = _pawn.rotation;
            if (r.x == 0f && r.y == 0f && r.z == 0f && r.w == 0f)
                return System.Numerics.Quaternion.Identity; // menu-phase garbage
            return new System.Numerics.Quaternion(r.x, r.y, r.z, r.w);
        }
        catch { _pawn = null!; return System.Numerics.Quaternion.Identity; }
    }

    Transform ResolvePawn()
    {
        try
        {
            var byTag = GameObject.FindWithTag(_pawnTag.Value);
            if (byTag != null) return byTag.transform;
        }
        catch { /* tag not defined in this game */ }

        if (_useCameraRoot.Value)
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform.root;
        }

        if (!_warnedNoPawn)
        {
            _warnedNoPawn = true;
            UnityEngine.Debug.LogWarning(
                "[ogsa] no pawn found (tag '" + _pawnTag.Value + "' / camera " +
                "root); retrying every second — set Pawn.Tag in the config");
        }
        return null!;
    }

    // "Player(Clone)" / "Player (Clone)" / "Player (2)" -> "Player": the
    // class-level name stays stable across respawn/instantiation, matching
    // the schema's stable-name requirement (instance-suffixed names would
    // churn discovery re-sends on every respawn)
    static string CleanPawnName(string name)
    {
        var n = name.TrimEnd();
        if (n.EndsWith("(Clone)", StringComparison.Ordinal))
            n = n.Substring(0, n.Length - "(Clone)".Length).TrimEnd();
        if (n.EndsWith(")", StringComparison.Ordinal))
        {
            var cut = n.Length - 1;              // Unity duplicate suffix " (2)"
            while (cut > 0 && char.IsDigit(n[cut - 1])) cut--;
            if (cut >= 2 && n[cut - 1] == '(' && n[cut - 2] == ' ')
                n = n.Substring(0, cut - 2).TrimEnd();
        }
        return n;
    }

    static string GameName()
    {
        try
        {
            // Paths.ProcessName is the game's actual executable name; do NOT
            // derive from the plugin location — mod managers (Rai Pal) deploy
            // plugins into per-game hash folders under AppData
            var name = Paths.ProcessName;
            if (!string.IsNullOrEmpty(name)) return name;
            return "unknown-game";
        }
        catch
        {
            return "unknown-game";
        }
    }
}
}
