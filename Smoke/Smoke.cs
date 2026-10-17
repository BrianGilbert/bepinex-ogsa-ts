// Smoke.cs — standalone verification of the plugin's wire path, no BepInEx
// and no Unity required: drives the exact vendored core publisher with a
// synthetic pawn (Unity frame == OGSA-TS frame, so values pass through
// unmapped) and prints frame counts. Exit 0 = pass. Run TelemetryReceive
// beside it to watch 'synthetic_pawn' on the wire.
//
// Also decodes the standard's seven conformance corpus vectors through
// THIS repo's vendored core (manifest-declared semantics) — guards the
// vendored schema against drift (the corpus v3 ContactMedium/ObjectType
// renumbering class of bug).
//
// Corpus dir: sibling OGSA-Telemetry-Standard (local dev) or nested
// checkout at the repo root (CI: path: OGSA-Telemetry-Standard).
using System;
using System.IO;
using System.Numerics;
using Capnp;
using Ogsa.Telemetry;

using var sender = new TelemetrySender();
using var pub = new TelemetryPublisher(sender, "bepinex_smoke", motionHz: 60);
pub.DeclareObject("synthetic_pawn");
var rpm = pub.DeclareChannel("engine.rpm", Unit.Rpm, "Synthetic", 0, 9000);
pub.BindChannelRaw(rpm, () => 6500.0 * (0.5 + 0.5 * Math.Sin(0.0)));

var deriver = new BodyDynamicsDeriver();
pub.BindBodyDynamics(() => (deriver.SpecificForce, deriver.AngularVelocity));

// runtime-tracked label swap (injected-mod pattern): name stays
// "synthetic_pawn", the discovery typeLabel updates mid-stream
bool labelSwapped = false;

const double dt = 1.0 / 60.0;
const double yawRate = 90.0;                                  // deg/s
const double w = yawRate * Math.PI / 180.0;                  // rad/s
const double r = 10.0;                                       // metres
double t = 0; int frames = 0;
for (int i = 0; i < 150; i++)
{
    // pawn circling horizontally at 10 m, nose tangential, turning
    // nose-right at yawRate — centripetal ω²r points +sway in the body
    // frame the whole time (Unity/OGSA ground plane is X-Z, Y up — NOT
    // UE's X-Y)
    double yr = w * t;
    Vector3 pos = new(10f * MathF.Cos((float)yr), 0f, 10f * MathF.Sin((float)yr));
    float heading = (float)(yr + Math.PI / 2.0);
    Quaternion q = new(0f, MathF.Sin(heading / 2f), 0f, MathF.Cos(heading / 2f));
    deriver.Sample(t, pos, q);
    pub.BindPose(() => pos, () => q);
    if (i == 75) { pub.SetObjectTypeLabel("BP_SyntheticPawn_C"); labelSwapped = true; }
    t += dt;
    pub.Tick(t);
    frames++;
}
if (frames != 150 || !labelSwapped) { Console.WriteLine("FAIL frame count"); return 1; }
Console.WriteLine("smoke: 150 ticks published (discovery + motion sent, typeLabel swapped at tick 75)");

// ---- derived body dynamics assertions (plugin wiring, delta velocity) ----
{
    const float g = BodyDynamicsDeriver.Gravity;
    Vector3 av = deriver.AngularVelocity;
    Vector3 sf = deriver.SpecificForce;
    Console.WriteLine($"dynamics: av=({av.X:F4}, {av.Y:F4}, {av.Z:F4}) " +
                      $"sf=({sf.X:F4}, {sf.Y:F4}, {sf.Z:F4}) " +
                      $"[expect av.y={w:F4}, sf.y={g:F4}, horiz={w * w * r:F4}]");
    bool ok = true;
    ok &= Math.Abs(av.Y - w) < 0.05 && Math.Abs(av.X) < 0.01 && Math.Abs(av.Z) < 0.01;
    Console.WriteLine((Math.Abs(av.Y - w) < 0.05 && Math.Abs(av.X) < 0.01 && Math.Abs(av.Z) < 0.01)
        ? "PASS dynamics: yaw rate ≈ 90°/s nose-right (+y, C-4)"
        : "FAIL dynamics: yaw rate");
    ok &= Math.Abs(sf.Y - g) < 0.2;
    Console.WriteLine(Math.Abs(sf.Y - g) < 0.2
        ? "PASS dynamics: heave ≈ +1 g (accelerometer-style)"
        : "FAIL dynamics: heave");
    // centripetal on +sway; surge leakage bounded by the accel LP phase
    // lag atan(ωτ) on the co-rotating vector (documented filter property)
    bool sway = Math.Abs(sf.X - (float)(w * w * r)) < 1.5f &&
                Math.Abs(sf.Z) < (float)(w * w * r) * 0.25f;
    ok &= sway;
    Console.WriteLine(sway
        ? "PASS dynamics: centripetal ω²r on +sway (C-4 right-turn)"
        : "FAIL dynamics: centripetal");
    float horiz = MathF.Sqrt(sf.X * sf.X + sf.Z * sf.Z);
    ok &= Math.Abs(horiz - (float)(w * w * r)) < 1.5f;
    Console.WriteLine(Math.Abs(horiz - (float)(w * w * r)) < 1.5f
        ? "PASS dynamics: horizontal specific force ≈ centripetal ω²r"
        : "FAIL dynamics: horizontal");
    if (!ok) return 1;
}

// ---- conformance corpus guard (vendored schema vs golden vectors) ----
int corpusFail = 0;
void CorpusCheck(string name, bool ok)
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")} corpus: {name}");
    if (!ok) corpusFail++;
}
string? corpusDir = null;
for (var d = new DirectoryInfo(AppContext.BaseDirectory);
     d != null && corpusDir == null; d = d.Parent!)
{
    string?[] candidates =
    {
        d.Parent is DirectoryInfo p
            ? Path.Combine(p.FullName, "OGSA-Telemetry-Standard", "conformance") : null,
        Path.Combine(d.FullName, "OGSA-Telemetry-Standard", "conformance"),
    };
    foreach (var c in candidates)
        if (c != null && Directory.Exists(c)) { corpusDir = c; break; }
}
if (corpusDir == null)
{
    Console.WriteLine("FAIL corpus: OGSA-Telemetry-Standard/conformance not found "
                    + "(sibling or nested checkout)");
    return 1;
}

DeserializerState LoadVector(string id)
{
    var hex = string.Concat(File.ReadAllLines(
        Path.Combine(corpusDir!, "vectors", id + ".hex")));
    var bytes = Convert.FromHexString(hex);
    if (!Envelope.TryUnwrap(bytes, out _, out var payload))
        throw new InvalidOperationException($"envelope: {id}");
    return DeserializerState.CreateRoot(Envelope.DecodeMessage(payload.Span));
}

{ // discovery-minimal (Medium/Type pin the corpus v3 enum ordinals)
    var d = CapnpGen.DiscoveryFrame.READER.create(LoadVector("discovery-minimal"));
    CorpusCheck("discovery-minimal semantics",
        d.SchemaVersion == 1 && d.GameName == "conformance"
        && d.SessionStartUnixUs == 1_730_000_000_000_000L
        && d.Objects.Count == 1 && d.Objects[0].Name == "obj" && d.Objects[0].Primary
        && d.Objects[0].Type == CapnpGen.ObjectType.vehicle
        && d.Environment.Medium == CapnpGen.ContactMedium.asphalt);
}
{ // discovery-channels
    var d = CapnpGen.DiscoveryFrame.READER.create(LoadVector("discovery-channels"));
    var ch = d.Objects[0].Channels;
    CorpusCheck("discovery-channels semantics",
        d.Objects[0].Name == "demo_car" && d.Objects[0].TypeLabel == "conformance car"
        && ch.Count == 3
        && ch[0].Name == "engine.rpm" && ch[0].Unit == "rpm"
        && ch[0].Range.Min == 0f && ch[0].Range.Max == 9000f
        && ch[1].Name == "vehicle.speed" && ch[1].Unit == "m/s" && ch[1].Range.Max == 30f
        && ch[2].Name == "lights.headlights" && ch[2].Unit == "");
}
{ // motion-pose
    var m = CapnpGen.MotionFrame.READER.create(LoadVector("motion-pose"));
    var o = m.Objects[0];
    CorpusCheck("motion-pose semantics",
        m.Timestamp == 1.25 && o.Name == "obj"
        && o.Position.X == 1.5f && o.Position.Y == 0.25f && o.Position.Z == -3.75f
        && o.Orientation.W == 1f && o.Orientation.X == 0f);
}
{ // motion-channels
    var m = CapnpGen.MotionFrame.READER.create(LoadVector("motion-channels"));
    var o = m.Objects[0];
    CorpusCheck("motion-channels semantics",
        m.Timestamp == 2.5 && o.Name == "demo_car" && o.Orientation.Y == 1f
        && o.Channels.Count == 3
        && o.Channels[0].Number == 3250.5f && o.Channels[1].Number == 8f
        && o.Channels[2].Boolean);
}
{ // motion-multi
    var m = CapnpGen.MotionFrame.READER.create(LoadVector("motion-multi"));
    CorpusCheck("motion-multi semantics",
        m.Timestamp == 3.75 && m.Objects.Count == 2
        && m.Objects[0].Name == "player_car" && m.Objects[0].Position.X == 1f
        && m.Objects[1].Name == "wingman"
        && m.Objects[1].Position.Y == 0.5f && m.Objects[1].Position.Z == 2.5f);
}
{ // motion-bodydynamics (union variant through the vendored schema)
    var m = CapnpGen.MotionFrame.READER.create(LoadVector("motion-bodydynamics"));
    var o = m.Objects[0];
    CorpusCheck("motion-bodydynamics semantics",
        m.Timestamp == 4.5 && o.Name == "obj"
        && o.which == CapnpGen.MotionObject.WHICH.BodyDynamics
        && o.BodyDynamics.SpecificForce.X == 0.25f
        && o.BodyDynamics.SpecificForce.Y == 9.5f
        && o.BodyDynamics.SpecificForce.Z == -1.5f
        && o.BodyDynamics.AngularVelocity.X == -0.125f
        && o.BodyDynamics.AngularVelocity.Y == 0.375f
        && o.BodyDynamics.AngularVelocity.Z == 0.75f);
}
{ // event-impact
    var e = CapnpGen.EventFrame.READER.create(LoadVector("event-impact"));
    var ev = e.Events[0];
    CorpusCheck("event-impact semantics",
        e.Timestamp == 0.125 && e.Events.Count == 1
        && ev.Id == 7u && ev.Name == "body.impact"
        && ev.Intensity == 0.75f && ev.Duration == 0.25f
        && ev.Direction.X == -1f);
}
if (corpusFail > 0) return 1;
return 0;
