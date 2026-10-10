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
using System;
using System.IO;
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
    ConfigEntry<string> _pawnTag = null!;
    ConfigEntry<bool> _useCameraRoot = null!;

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
        _pawnTag = Config.Bind("Pawn", "Tag", "Player",
                               "GameObject tag that marks the player pawn");
        _useCameraRoot = Config.Bind("Pawn", "CameraRootFallback", true,
                                     "If the tag finds nothing, use the main " +
                             "camera's root transform (flatscreen pawn proxy)");
    }

    void OnEnable()
    {
        _sender = new TelemetrySender(_group.Value, _port.Value, _ttl.Value,
                                      loopback: true);
        _publisher = new TelemetryPublisher(_sender, GameName(), _motionHz.Value);
        _publisher.DeclareObject(_objectName.Value);
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
                pub.BindPose(() => ToNumerics(_pawn.position),
                             () => ToNumerics(_pawn.rotation));
                _warnedNoPawn = false;
            }
        }
        pub.Tick((double)Time.time);
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

    static string GameName()
    {
        try
        {
            var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(exe ?? "");
            // BepInEx plugins live in <game>/BepInEx/plugins — walk up to the exe
            var gameDir = dir != null
                ? Path.GetDirectoryName(Path.GetDirectoryName(
                      Path.GetDirectoryName(dir)))
                : null;
            var name = gameDir != null
                ? Path.GetFileName(gameDir)
                : Paths.ProcessName;
            return string.IsNullOrEmpty(name) ? Paths.ProcessName : name;
        }
        catch
        {
            return Paths.ProcessName;
        }
    }

    static System.Numerics.Vector3 ToNumerics(Vector3 v) =>
        new System.Numerics.Vector3(v.x, v.y, v.z);
    static System.Numerics.Quaternion ToNumerics(Quaternion q) =>
        new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
}
}
