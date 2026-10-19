using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A two-wheeler got on again after a save is loaded (Garage/BikeRules.cs, the mass): its centre of mass and inertia are
// kept only from a body free to turn and written back only into one. Checked on a model of what the game and the mod do
// to the scooter's body, step for step, with the engine as Unity documents Rigidbody.inertiaTensor: a bike saved standing
// and loaded, one saved in the saddle, one just bought; got on and off, fallen from, stood back and held, dressed again;
// each of the four kinds turns on the game's own inertia whenever it is ridden, and the game's hold on a standing bike is
// never undone. The same model run the way before these rules shows a player's report (19 Oct 2026): fine until a save
// is loaded, then a bike whose front wheel steers and which goes straight on. Then, over the sources, that BikeMount keeps
// and writes through these rules, keeps again on getting on, writes the body's mass nowhere else, and checks itself then.
internal static partial class Program
{
    // kg m2: the game's scooter, every two-wheeler's body, as its prefab has it (VariableCenterOfMass with useDefaultInertia
    // off, read from the game's bundle): the game writes this each time it lets the body go (VehicleController.SetFreeze).
    private static readonly float[] ScooterInertia = { 234.375f, 208.33334f, 67.708336f };
    private const float ArcadeSteerTorque = 250f * 9.81f * 0.4f;   // N m about the upright: the game's ArcadeModule at full steer (mass x g x 0.4)
    private const float TurnSeconds = 0.5f;                       // s of full steering a turn is measured over
    private const float LeastTurnDeg = 20f;                       // degrees half a second of that turns a ridden bike at least (33.7 on the game's inertia)

    // A scooter's body as the engine holds it. A frozen turn is an inertia of zero about it: read so while frozen, and not
    // given back when the freeze ends (the game writes its own then). A zero component is an infinite inertia: no turn.
    private sealed class TurnBody
    {
        public bool Frozen;
        public float X, Y, Z;
        public int WrittenWhileFrozen;

        // As the game makes it, bought or spawned from a save: VariableCenterOfMass.Awake has written the game's inertia.
        public static TurnBody Made()
        {
            return new TurnBody { X = ScooterInertia[0], Y = ScooterInertia[1], Z = ScooterInertia[2] };
        }

        // VehicleController.SetFreeze(true): every constraint, the engine's inertia about each frozen turn zero.
        public void Hold()
        {
            Frozen = true;
            X = Y = Z = 0f;
        }

        // SetFreeze(false), then VariableCenterOfMass.UpdateAllProperties: the game's own inertia written.
        public void LetGo()
        {
            Frozen = false;
            X = ScooterInertia[0];
            Y = ScooterInertia[1];
            Z = ScooterInertia[2];
        }

        public void Write(float x, float y, float z)
        {
            if (Frozen)
            {
                WrittenWhileFrozen++;   // (the engine's hold on the turn would be undone)
            }
            X = x;
            Y = y;
            Z = z;
        }

        // Degrees the game's full steering torque turns the body by in TurnSeconds, nothing else holding it.
        public float Turn()
        {
            if (Frozen || !(Y > 0f) || float.IsInfinity(Y))
            {
                return 0f;
            }
            return (float)(0.5 * ArcadeSteerTorque / Y * TurnSeconds * TurnSeconds * 180.0 / Math.PI);
        }
    }

    // BikeMount's part, call for call: Attach keeps, shapes the boxes (the body's own values are set, so they do not move)
    // and writes back; Mounted keeps again and writes back as the bars box goes on; getting off writes back inside the
    // game's ExitVehicle, after its hold (Exited, BarsOff); a fall writes back before the game's ExitVehicle; Detach writes
    // back. 'Before' is the way before the mass rules: kept and written whatever the body, not kept again on getting on.
    private sealed class TurnMount
    {
        private readonly bool _before;
        private bool _kept;
        private float _x, _y, _z;

        public TurnMount(bool before)
        {
            _before = before;
        }

        private void Keep(TurnBody body)
        {
            if (_before || BikeRules.KeepMass(!body.Frozen, body.X, body.Y, body.Z))
            {
                _kept = true;
                _x = body.X;
                _y = body.Y;
                _z = body.Z;
            }
        }

        private void PutBack(TurnBody body)
        {
            if (_before ? _kept : BikeRules.PutMassBack(_kept, !body.Frozen))
            {
                body.Write(_x, _y, _z);
            }
        }

        public void Attach(TurnBody body)
        {
            Keep(body);
            PutBack(body);
        }

        public void Mounted(TurnBody body)
        {
            if (!_before)
            {
                Keep(body);
            }
            PutBack(body);
        }

        public void BarsOff(TurnBody body)
        {
            PutBack(body);
        }

        public void Detach(TurnBody body)
        {
            PutBack(body);
            _kept = false;
        }
    }

    // One bike's life, the game's steps and the mod's in the order they come, and what every ride had.
    private sealed class TurnRun
    {
        private readonly bool _before;
        private TurnBody _body;
        private TurnMount _mount;

        public float LeastTurn = float.MaxValue;   // degrees: the least any ride turned in TurnSeconds
        public float MostOff;                      // kg m2: the most a ridden body's inertia was off the game's
        public int Rides, HeldRides;

        public TurnRun(bool before)
        {
            _before = before;
        }

        public int WrittenWhileFrozen => _body != null ? _body.WrittenWhileFrozen : 0;

        // Bought, hired or made again (TwoWheelers.Spawn): the mod dresses it the moment the game makes it, then the game
        // holds it as it stands (ScooterController's look for a scooter at rest, OnVehicleStationaryAfterLoad).
        public TurnRun Bought()
        {
            _body = TurnBody.Made();
            _mount = new TurnMount(_before);
            _mount.Attach(_body);
            _body.Hold();
            return this;
        }

        // A save loaded that was made with the bike standing: the game spawns it and holds it within a frame or two; the
        // mod dresses it at its next second (TwoWheelers.Slow), or, by chance, just before the hold.
        public TurnRun LoadedStanding(bool dressedFirst)
        {
            _body = TurnBody.Made();
            _mount = new TurnMount(_before);
            if (dressedFirst)
            {
                _mount.Attach(_body);
            }
            _body.Hold();
            if (!dressedFirst)
            {
                _mount.Attach(_body);
            }
            return this;
        }

        // A save loaded that was made in the saddle: the game spawns it and gets the player on a frame later
        // (GameManager.SpawnPlayerVehicles); its look for a scooter at rest may hold it for that frame first. The mod dresses
        // it before the hold (0), while held (1) or once ridden (2), and seats the rider on the frame after (Mounted).
        public TurnRun LoadedRidden(int dressedAt, bool heldFirst)
        {
            _body = TurnBody.Made();
            _mount = new TurnMount(_before);
            if (dressedAt == 0)
            {
                _mount.Attach(_body);
            }
            if (heldFirst)
            {
                _body.Hold();
                if (dressedAt == 1)
                {
                    _mount.Attach(_body);
                }
                _body.LetGo();   // the game's EnterVehicle
            }
            if (dressedAt == 2 || (dressedAt == 1 && !heldFirst))
            {
                _mount.Attach(_body);
            }
            _mount.Mounted(_body);
            Ride();
            return this;
        }

        // The game's EnterVehicle lets the body go and writes its own values; TwoWheelers.Frame then calls Mounted.
        public TurnRun GetOn()
        {
            if (_body.Frozen)
            {
                _body.LetGo();
            }
            _mount.Mounted(_body);
            Ride();
            return this;
        }

        // The game's ExitVehicle holds it, then its onExitVehicle (Exited, BarsOff); Dismounted finds the bars box off.
        public TurnRun GetOff()
        {
            _body.Hold();
            _mount.BarsOff(_body);
            return this;
        }

        // TwoWheelers.Fall: the bars box off first, then the game's ExitVehicle.
        public TurnRun Fall()
        {
            _mount.BarsOff(_body);
            _body.Hold();
            return this;
        }

        // TwoWheelers.Settled: stood back where it was put and held as the game holds one (SetFreeze(true)). "Bring it
        // here" and the clear-place move only move it: nothing of its mass.
        public TurnRun HeldAgain()
        {
            _body.Hold();
            return this;
        }

        // The option off (Detach, standing), then on again: dressed again at the next second, standing.
        public TurnRun Redressed()
        {
            _mount.Detach(_body);
            _mount = new TurnMount(_before);
            _mount.Attach(_body);
            return this;
        }

        private void Ride()
        {
            Rides++;
            if (_body.Frozen)
            {
                HeldRides++;
            }
            LeastTurn = Math.Min(LeastTurn, _body.Turn());
            MostOff = Math.Max(MostOff, Math.Max(Math.Abs(_body.X - ScooterInertia[0]), Math.Max(Math.Abs(_body.Y - ScooterInertia[1]), Math.Abs(_body.Z - ScooterInertia[2]))));
        }
    }

    private static void BikeTurn(string scripts, string catalogue)
    {
        // ---- the rules: an inertia a body turns on, kept only from a free body, written only into one.
        float gx = ScooterInertia[0], gy = ScooterInertia[1], gz = ScooterInertia[2];
        Check(BikeRules.Turns(gx, gy, gz), "bikes turn: the game's scooter inertia (" + gx + ", " + gy.ToString("F1") + ", " + gz.ToString("F1") + " kg m2) is one a body turns on");
        Check(!BikeRules.Turns(0f, 0f, 0f) && !BikeRules.Turns(0f, gy, gz) && !BikeRules.Turns(gx, 0f, gz) && !BikeRules.Turns(gx, gy, 0f),
            "bikes turn: an inertia with a zero on any axis is the engine's lock on a frozen turn, never one a body turns on");
        Check(!BikeRules.Turns(float.NaN, gy, gz) && !BikeRules.Turns(gx, float.PositiveInfinity, gz) && !BikeRules.Turns(gx, gy, -1f)
              && !BikeRules.Turns(gx, BikeRules.LeastInertia, gz) && BikeRules.Turns(gx, BikeRules.LeastInertia * 2f, gz),
            "bikes turn: not a number, an infinite or a negative inertia, or one at the least, is not one a body turns on; just over the least is");
        Check(BikeRules.KeepMass(true, gx, gy, gz) && !BikeRules.KeepMass(false, gx, gy, gz) && !BikeRules.KeepMass(true, 0f, 0f, 0f) && !BikeRules.KeepMass(false, 0f, 0f, 0f),
            "bikes turn: the body's mass is kept only from a body free to turn with an inertia it turns on, never from one the game holds");
        Check(BikeRules.PutMassBack(true, true) && !BikeRules.PutMassBack(true, false) && !BikeRules.PutMassBack(false, true) && !BikeRules.PutMassBack(false, false),
            "bikes turn: what was kept is written back only into a body free to turn, and nothing when nothing was kept");

        // ---- the model, for each of the four kinds (one scooter body under all of them).
        MatchCollection kinds = Regex.Matches(catalogue, @"Kind = BikeKind\.(\w+)");
        Check(kinds.Count == 4, "bikes turn: the four kinds are read from BikeBody.cs: " + kinds.Count);
        foreach (Match match in kinds)
        {
            string kind = match.Groups[1].Value;
            Turned(kind, "bought, got on, off and on again", new TurnRun(false).Bought().GetOn().GetOff().GetOn().GetOff());
            Turned(kind, "saved standing, loaded and got on", new TurnRun(false).LoadedStanding(false).GetOn());
            Turned(kind, "saved standing, loaded, dressed in the frame before the game held it, got on", new TurnRun(false).LoadedStanding(true).GetOn());
            Turned(kind, "saved standing, loaded, got on, off and on again", new TurnRun(false).LoadedStanding(false).GetOn().GetOff().GetOn());
            Turned(kind, "saved standing, loaded, fallen from and got on again", new TurnRun(false).LoadedStanding(false).GetOn().Fall().GetOn());
            Turned(kind, "saved standing, loaded, stood back and held, got on", new TurnRun(false).LoadedStanding(false).HeldAgain().GetOn());
            Turned(kind, "saved standing, loaded, dressed again (the option off and on), got on", new TurnRun(false).LoadedStanding(false).Redressed().GetOn());
            for (int at = 0; at < 3; at++)
            {
                foreach (bool held in new[] { false, true })
                {
                    Turned(kind, "saved in the saddle, loaded, dressed " + (at == 0 ? "first" : at == 1 ? "in the held frame" : "once ridden") + (held ? ", held a frame first" : string.Empty)
                                 + ", then off and on again", new TurnRun(false).LoadedRidden(at, held).GetOff().GetOn());
                }
            }
        }

        // ---- the same model the way before the rules: fine in the session the bike was bought in, then the report.
        TurnRun bought = new TurnRun(true).Bought().GetOn().GetOff().GetOn();
        TurnRun loaded = new TurnRun(true).LoadedStanding(false).GetOn();
        Check(bought.LeastTurn >= LeastTurnDeg && loaded.Rides == 1 && loaded.LeastTurn == 0f && loaded.MostOff > 200f,
            "bikes turn: the model shows the report the way before: a bike bought and ridden turns " + bought.LeastTurn.ToString("F1")
            + " degrees, saved standing and loaded it turns " + loaded.LeastTurn.ToString("F1") + " (an inertia of nothing kept as the game held it, written back as it was got on)");
        Check(bought.WrittenWhileFrozen > 0, "bikes turn: the way before, getting off also wrote the body's mass into the bike the game had just held");

        // ---- over the sources: BikeMount keeps and writes through the rules, keeps again on getting on, and checks itself.
        string mount = Source(scripts, @"Garage\BikeMount.cs");
        string keep = Body(mount, "private void KeepMass(");
        string put = Body(mount, "private void PutMassBack(");
        string mounted = Body(mount, "public void Mounted(");
        string attach = Body(mount, "public static BikeMount Attach(");
        string check = Body(mount, "private void TurnCheck(");
        int rule = keep.IndexOf("BikeRules.KeepMass(TurnFree, inertia.x, inertia.y, inertia.z)", StringComparison.Ordinal);
        Check(rule > 0 && keep.IndexOf("_inertia = inertia;", StringComparison.Ordinal) > rule && keep.IndexOf("_massKept = true;", StringComparison.Ordinal) > rule
              && !keep.Contains("_inertia = _rigidbody.inertiaTensor"),
            "bikes turn source: BikeMount keeps the body's mass only once the rules say the body is free to turn on an inertia it turns on");
        Check(put.Contains("BikeRules.PutMassBack(_massKept, TurnFree)") && put.IndexOf("BikeRules.PutMassBack(", StringComparison.Ordinal) < put.IndexOf("inertiaTensor =", StringComparison.Ordinal),
            "bikes turn source: BikeMount writes the body's mass back only when the rules say so");
        Check(Regex.IsMatch(mount, @"private bool TurnFree => _rigidbody != null && \(_rigidbody\.constraints & RigidbodyConstraints\.FreezeRotation\) == 0;"),
            "bikes turn source: free to turn is no frozen turn in the body's constraints (the game's hold freezes every one)");
        int kept = mounted.IndexOf("KeepMass();", StringComparison.Ordinal), on = mounted.IndexOf("BarsOn();", StringComparison.Ordinal);
        int told = mounted.IndexOf("TurnCheck();", StringComparison.Ordinal);
        Check(kept > 0 && on > kept && told > on,
            "bikes turn source: getting on keeps the game's own values first, as the game has just let the bike go, then the bars box, then the check");
        int keptFirst = attach.IndexOf("mount.KeepMass();", StringComparison.Ordinal), shaped = attach.IndexOf("mount.Shape();", StringComparison.Ordinal);
        Check(keptFirst > 0 && shaped > keptFirst && attach.IndexOf("mount.PutMassBack();", StringComparison.Ordinal) > shaped,
            "bikes turn source: dressing a bike keeps before the boxes change and writes back after");
        Check(Count(mount, "inertiaTensor =") == 1 && Count(mount, "centerOfMass =") == 1 && Count(mount, "inertiaTensorRotation =") == 1 && Count(put, "inertiaTensor =") == 1,
            "bikes turn source: the body's mass is written in BikeMount only by PutMassBack");
        foreach (string file in new[] { @"Garage\TwoWheelers.cs", @"Garage\BikePlaces.cs", @"Garage\BikeBody.cs", @"Garage\BikeNames.cs", @"Garage\BikeClearRules.cs" })
        {
            string code = Source(scripts, file);
            Check(code.Length > 0 && !code.Contains("inertiaTensor") && !code.Contains("centerOfMass"), "bikes turn source: " + file + " writes nothing of a body's mass");
        }
        Check(check.Contains("BikeRules.Turns(inertia.x, inertia.y, inertia.z)") && check.Contains("TurnFree") && check.Contains("Logger?.Warn(") && check.Contains(": FAIL")
              && check.Contains("Meta.Dev.Verbose") && check.Contains(": PASS"),
            "bikes turn source: getting on checks the bike is free to turn on an inertia it turns on: a fault always in the log, a pass with Developer diagnostics");
    }

    // A bike's life on the model with the rules: every ride turns on the game's own inertia, and the game's hold is never undone.
    private static void Turned(string kind, string what, TurnRun run)
    {
        Check(run.Rides > 0 && run.HeldRides == 0 && run.LeastTurn >= LeastTurnDeg && run.MostOff < 0.001f,
            "bikes turn: a " + kind + " " + what + " turns on the game's own inertia every ride (" + run.Rides + " rides, the least " + run.LeastTurn.ToString("F1")
            + " degrees in " + TurnSeconds + " s of full steering, " + run.MostOff.ToString("F3") + " kg m2 off)");
        Check(run.WrittenWhileFrozen == 0, "bikes turn: a " + kind + " " + what + ": nothing is written into the bike while the game holds it");
    }
}
