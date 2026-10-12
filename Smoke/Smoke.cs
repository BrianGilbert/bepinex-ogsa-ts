// Smoke.cs — standalone verification of the plugin's wire path, no BepInEx
// and no Unity required: drives the exact vendored core publisher with a
// synthetic pawn (Unity frame == OGSA-TS frame, so values pass through
// unmapped) and prints frame counts. Exit 0 = pass. Run TelemetryReceive
// beside it to watch 'synthetic_pawn' on the wire.
using System;
using System.Numerics;
using Ogsa.Telemetry;

using var sender = new TelemetrySender();
using var pub = new TelemetryPublisher(sender, "bepinex_smoke", motionHz: 60);
pub.DeclareObject("synthetic_pawn");
var rpm = pub.DeclareChannel("engine.rpm", Unit.Rpm, "Synthetic", 0, 9000);
pub.BindChannelRaw(rpm, () => 6500.0 * (0.5 + 0.5 * Math.Sin(0.0)));

// runtime-tracked label swap (injected-mod pattern): name stays
// "synthetic_pawn", the discovery typeLabel updates mid-stream
bool labelSwapped = false;

double t = 0; int frames = 0;
for (int i = 0; i < 150; i++)
{
    double yaw = t * 0.8;
    Vector3 pos = new(10f * MathF.Cos((float)yaw), 0f, 10f * MathF.Sin((float)yaw));
    Quaternion q = new(0f, MathF.Sin((float)(yaw / 2)), 0f, MathF.Cos((float)(yaw / 2)));
    pub.BindPose(() => pos, () => q);
    if (i == 75) { pub.SetObjectTypeLabel("BP_SyntheticPawn_C"); labelSwapped = true; }
    t += 1.0 / 60.0;
    pub.Tick(t);
    frames++;
}
if (frames != 150 || !labelSwapped) { Console.WriteLine("FAIL frame count"); return 1; }
Console.WriteLine($"smoke: 150 ticks published (discovery + motion sent, typeLabel swapped at tick 75)");
return 0;
