using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The test entries (Options, Test tools): the test building asked for at the next load, walked into and out of,
    /// saved inside and read back, a copy of the nearest building, the frame with ten copies, and the switch-off with its
    /// hourly check. Each works when pressed or says why not in a notice, and writes its "... check (...)" lines with
    /// Developer diagnostics on. Nothing here runs a frame unless a test is under way.
    /// </summary>
    public sealed partial class NewBuildingsDirector
    {
        private const string TestName = "New buildings";
        private const float WayWait = 20f;        // s of real time the game is given to take the player in or out
        private const float InsideSeconds = 3f;   // s inside before the walk-in test walks out again
        private const float SaveWait = 60f;       // s a save and its reading back are waited for
        private const float SettleSeconds = 1f;   // s after the ten copies stand before the frame is measured with them

        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        private Action _onProbe, _probeSeen;
        private bool _probeArmed;
        private bool _testing;                   // a test with steps of its own is under way
        private CopyHandle _testCopy;            // the copy test's copy, until the next one or the load
        private PerfSampler _sampler;
        private readonly List<CopyLook> _costCopies = new List<CopyLook>(NewBuildingsRules.CostCopies);
        private readonly List<Vector3> _costAt = new List<Vector3>(NewBuildingsRules.CostCopies);
        private readonly List<float> _costYaw = new List<float>(NewBuildingsRules.CostCopies);

        /// <summary>
        /// "New buildings: make the test building near you at the next load": asks for it, or stops asking (a developer's
        /// choice kept for the next load). Out in the city it also keeps where the player stands and the way the camera looks, so
        /// the ground round them is tried first; from the main menu, the open ground by the airport alone.
        /// </summary>
        public static void TestMake()
        {
            try
            {
                if (!Meta.Dev.Verbose)
                {
                    TestRefusals.TellKey(Logger, TestName, NewBuildingsText.NeedsDiagnostics, "Developer diagnostics is off");
                    return;
                }
                bool ask = !TestAsked;
                UnityEngine.PlayerPrefs.SetInt(TestPref, ask ? 1 : 0);
                // Out in the city, the ground round the player (up the screen first) is tried at the next load, so the building
                // stands where the developer is looking whenever that ground is free.
                float look = 0f;
                bool here = ask && !MadeGame.Loading && !MadeGame.Indoors && MadeGame.Camera(out _, out look);
                Vector3 at = here ? MadeGame.PlayerAt() : Vector3.zero;
                UnityEngine.PlayerPrefs.SetString(TestHerePref, here ? NewBuildingsRules.SpotText(at.x, at.z, look) : string.Empty);
                UnityEngine.PlayerPrefs.Save();
                string address = MadeGame.Written(NewBuildingsTestPlot.Street, NewBuildingsTestPlot.Number);
                string where = here ? "on the first free ground of " + NewBuildingsTestPlot.RingLots + " lots round where the player stands ("
                                      + at.x.ToString("F0", CultureInfo.InvariantCulture) + ", "
                                      + at.z.ToString("F0", CultureInfo.InvariantCulture) + ", looking " + look.ToString("F0", CultureInfo.InvariantCulture)
                                      + " degrees), else on the open ground by the airport." : "on the open ground by the airport.";
                Logger?.Info("New buildings test: the test building " + (ask ? "is made at " + address + " from the next load, " + where : "is no longer made from the next load."));
                MadeGame.Tell(ask ? NewBuildingsText.Armed : NewBuildingsText.Disarmed, "address", address);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"New buildings: walk into the test building and out": the game's own way in, a look at what it loaded, and out at its door.</summary>
        public void TestWalk()
        {
            try
            {
                MadeBuilding made = TestBuilding();
                if (made == null)
                {
                    return;
                }
                if (MadeGame.Indoors)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                    return;
                }
                if (MadeGame.InVehicle)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.InVehicle);
                    return;
                }
                StartCoroutine(Guard.Routine(Walk(made, true)));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"New buildings: save inside the test building and read the file back": in if not there, a recovery save, the file read back.</summary>
        public void TestSave()
        {
            try
            {
                MadeBuilding made = TestBuilding();
                if (made == null)
                {
                    return;
                }
                if (MadeGame.InVehicle)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.InVehicle);
                    return;
                }
                if (MadeGame.Indoors && !MadeGame.InsideOf(made))
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors, 0, "inside another building");
                    return;
                }
                StartCoroutine(Guard.Routine(SaveInside(made)));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The test building standing and shown, the save loaded, no other test under way; null after saying why not.
        private MadeBuilding TestBuilding()
        {
            if (MadeGame.Loading)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return null;
            }
            if (!Settings.NewBuildings)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return null;
            }
            if (_testing)
            {
                TestRefusals.TellKey(Logger, TestName, NewBuildingsText.Busy, "another of its tests is under way");
                return null;
            }
            MadeBuilding made = MadeBuildings.Get(NewBuildingsTestPlot.Id);
            if (made == null || !made.Shown || made.Controller == null)
            {
                TestRefusals.TellKey(Logger, TestName, NewBuildingsText.Missing, made == null ? "it was not made at this load (Player.log says why, if it was asked for)" : "it is hidden");
                return null;
            }
            return made;
        }

        // In by the game's own way; with thenOut, out again a few seconds later by its exit zone.
        private IEnumerator Walk(MadeBuilding made, bool thenOut)
        {
            _testing = true;
            try
            {
                MadeGame.Tell(NewBuildingsText.Walking, null, null);
                if (!MadeGame.InsideOf(made))
                {
                    if (!MadeGame.Enter(made))
                    {
                        Logger?.Info("Made buildings check (way in): the game would not begin to take the player into " + made.Address + ": FAIL");
                        yield break;
                    }
                    float until = Time.unscaledTime + WayWait;
                    while (!MadeGame.InsideOf(made))
                    {
                        if (Time.unscaledTime > until)
                        {
                            Logger?.Info("Made buildings check (way in): the player was not inside " + made.Address + " " + WayWait.ToString("F0", Plain) + " s later: FAIL");
                            yield break;
                        }
                        yield return null;
                    }
                }
                MadeGame.Structure(out string loaded, out int exits);
                Logger?.Info("Made buildings check (way in): entered on structure " + NewBuildingsRules.StructureName(made.Spec.Size, made.Spec.Version) + " (the game loaded "
                             + (loaded ?? "nothing") + "), exit zone found" + (exits > 0 ? ": PASS" : ": FAIL, the game found no exit zone in it"));
                if (!thenOut)
                {
                    yield break;
                }
                float inside = Time.unscaledTime + InsideSeconds;
                while (Time.unscaledTime < inside)
                {
                    yield return null;
                }
                if (!MadeGame.InsideOf(made) || !MadeGame.Exit())
                {
                    Logger?.Info("Made buildings check (way out): the player had left by another way first: not checked.");
                    yield break;
                }
                float leaving = Time.unscaledTime + WayWait;
                while (MadeGame.Indoors || MadeGame.Moving)
                {
                    if (Time.unscaledTime > leaving)
                    {
                        Logger?.Info("Made buildings check (way out): the player was still inside " + WayWait.ToString("F0", Plain) + " s after the game was asked to take them out: FAIL");
                        yield break;
                    }
                    yield return null;
                }
                Vector3 at = MadeGame.PlayerAt();
                float dx = at.x - made.DoorX, dz = at.z - made.DoorZ;
                float apart = Mathf.Sqrt(dx * dx + dz * dz);
                Logger?.Info("Made buildings check (way out): the player is at its door" + (apart <= NewBuildingsRules.DoorNear ? ": PASS"
                                 : " (" + apart.ToString("F1", Plain) + " m from it, wanted " + NewBuildingsRules.DoorNear.ToString("F0", Plain) + " m or less): FAIL"));
                MadeGame.Tell(NewBuildingsText.Walked, null, null);
            }
            finally
            {
                _testing = false;
            }
        }

        // A recovery save made inside the test building, as the game's autosave makes one, and its file read back.
        private IEnumerator SaveInside(MadeBuilding made)
        {
            if (!MadeGame.InsideOf(made))
            {
                yield return Walk(made, false);
                if (!MadeGame.InsideOf(made))
                {
                    yield break;   // the way in has said why
                }
            }
            _testing = true;
            try
            {
                float settle = Time.unscaledTime + SettleSeconds;
                while (Time.unscaledTime < settle)
                {
                    yield return null;
                }
                string path = MadeGame.SaveRecovery(out string name);
                if (path == null)
                {
                    Logger?.Info("Save check: the game would not save inside " + made.Address + " now: not checked.");
                    MadeGame.Tell(NewBuildingsText.NotSaved, null, null);
                    yield break;
                }
                MadeGame.Tell(NewBuildingsText.Saving, "save", name);
                float until = Time.unscaledTime + SaveWait;
                while (MadeGame.Saving)
                {
                    if (Time.unscaledTime > until)
                    {
                        Logger?.Info("Save check: the game was still writing " + name + " " + SaveWait.ToString("F0", Plain) + " s later: FAIL");
                        yield break;
                    }
                    yield return null;
                }
                Task<GameInstance> reading = MadeGame.ReadBack(path);
                until = Time.unscaledTime + SaveWait;
                while (!reading.IsCompleted)
                {
                    if (Time.unscaledTime > until)
                    {
                        Logger?.Info("Save check: " + name + " was not read back " + SaveWait.ToString("F0", Plain) + " s later: FAIL");
                        yield break;
                    }
                    yield return null;
                }
                GameInstance read = reading.IsFaulted || reading.IsCanceled ? null : reading.Result;
                if (read == null)
                {
                    Logger?.Info("Save check: " + path + " could not be read back by the game's own reader"
                                 + (reading.Exception != null ? " (" + reading.Exception.GetBaseException().Message + ")" : string.Empty) + ": FAIL");
                    yield break;
                }
                SaveNames names = MadeGame.NamesIn(read);
                Logger?.Info("Save check: the written save names no made address (registrations " + names.Registrations + ", current address "
                             + (names.CurrentEmpty ? "empty" : names.Current ? "a made one" : "another") + ", mission " + (names.Mission ? "names one" : "none") + ", destination "
                             + (names.Destination ? "a made one" : "none") + ", other lists " + names.Others + "; " + name + "): "
                             + (names.None && names.CurrentEmpty ? "PASS" : "FAIL"));
                MadeGame.Tell(NewBuildingsText.Saved, "save", name);
            }
            finally
            {
                _testing = false;
            }
        }

        /// <summary>"New buildings: copy the nearest building": a decorative copy of it 40 m ahead, its door toward the player; a building in a static batch is refused.</summary>
        public void TestCopy()
        {
            try
            {
                if (!StreetTest())
                {
                    return;
                }
                Vector3 player = MadeGame.PlayerAt();
                CityBuildingController near = CopyGame.Nearest(player, out float metres);
                string why = "there is no building";
                DesignEntry d = near != null ? CopyGame.DescribeOne(near, out why) : null;
                if (d == null)
                {
                    Logger?.Info("Construction check (copy): the nearest building" + (near != null ? ", " + near.name + ", is not copied: " + why : " was not found") + ": refused.");
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NothingFits);
                    return;
                }
                if (d.Batched)
                {
                    CopyGame.Batched(near, out int batched, out int renderers);
                    Logger?.Info("Construction check (copy): " + d.Name + " is drawn in a static batch with its neighbours (" + batched + " of " + renderers
                                 + " renderers): a copy would draw at the original's place, so none is made: refused.");
                    TestRefusals.TellKey(Logger, TestName, NewBuildingsText.Batched, d.Name + " is in a static batch");
                    return;
                }
                float facing = MadeGame.PlayerFacing();
                Vector3 ahead = player + Quaternion.Euler(0f, facing, 0f) * Vector3.forward * NewBuildingsRules.CopyAhead;
                MadeLot lot = LotOf(d, ahead.x, ahead.z, facing + 180f);
                string ground = CopyGame.GroundWhy(d, lot, out Vector3 root, out float yaw, out _);
                if (ground != null)
                {
                    Logger?.Info("Construction check (copy): no free ground " + NewBuildingsRules.CopyAhead.ToString("F0", Plain) + " m ahead for " + d.Name + ": " + ground + ": refused.");
                    TestRefusals.TellKey(Logger, TestName, NewBuildingsText.NoGround, ground);
                    return;
                }
                if (_testCopy != null)
                {
                    BuildingCopies.Remove(_testCopy);
                }
                CopyLook look = CopyGame.MakeLookOf(near, root, yaw, "RealLife Test Copy " + d.Name);
                if (look == null)
                {
                    Logger?.Info("Construction check (copy): " + d.Name + " could not be copied: FAIL");
                    return;
                }
                _testCopy = BuildingCopies.Stood(look, d.Name, d.Size, root, yaw, "the copy test");
                bool linked = look.FadeLinked(out int parts);
                Logger?.Info("Construction check (copy): " + d.Name + ", 0 in a static batch, fade linked" + (linked ? ": PASS" : ": FAIL, the game's fade does not find it")
                             + " (" + parts + " parts; the original " + metres.ToString("F0", Plain) + " m from the player; the copy at (" + root.x.ToString("F1", Plain) + ", "
                             + root.z.ToString("F1", Plain) + ") until the next load or the next copy).");
                MadeGame.Tell(NewBuildingsText.Copied, "design", d.Name);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A lot as big as the design's own footprint, its middle at (x, z), its street the way it faces.
        private static MadeLot LotOf(DesignEntry d, float x, float z, float facing)
        {
            NewBuildingsRules.DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            return new MadeLot(x, z, NewBuildingsRules.Turn(facing), uMax - uMin, vMax - vMin);
        }

        // Out of doors, on foot, a save loaded, the option on, no other test under way.
        private bool StreetTest()
        {
            if (MadeGame.Loading)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.NewBuildings)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff);
                return false;
            }
            if (MadeGame.Indoors)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                return false;
            }
            if (MadeGame.InVehicle)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.InVehicle);
                return false;
            }
            if (_testing)
            {
                TestRefusals.TellKey(Logger, TestName, NewBuildingsText.Busy, "another of its tests is under way");
                return false;
            }
            return true;
        }

        /// <summary>"New buildings: frame cost with ten copies in view": the frame measured without, then with ten copies made one a frame, against +0.5 ms.</summary>
        public void TestCost()
        {
            try
            {
                if (!StreetTest())
                {
                    return;
                }
                if (!MadeGame.Camera(out Vector3 eye, out float facing))
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                    return;
                }
                CityBuildingController source = CopyGame.NearestCopyable(MadeGame.PlayerAt(), out DesignEntry d);
                if (source == null)
                {
                    Logger?.Info("Copy check (frame cost): none of the buildings nearest the player can be copied (each drawn in a static batch, of a DLC or of a kind never copied): refused.");
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NothingFits);
                    return;
                }
                _costAt.Clear();
                _costYaw.Clear();
                string first = null;
                for (int i = 0; i < NewBuildingsRules.CostCopies; i++)
                {
                    NewBuildingsRules.CostSpot(i, eye.x, eye.z, facing, out float x, out float z);
                    string ground = CopyGame.GroundWhy(d, LotOf(d, x, z, facing + 180f), out Vector3 root, out float yaw, out _);
                    if (ground == null)
                    {
                        _costAt.Add(root);
                        _costYaw.Add(yaw);
                    }
                    else
                    {
                        first = first ?? ground;
                    }
                }
                if (_costAt.Count < NewBuildingsRules.CostCopies)
                {
                    Logger?.Info("Copy check (frame cost): only " + _costAt.Count + " of the " + NewBuildingsRules.CostCopies + " copies of " + d.Name
                                 + " found free ground in view (the first refused: " + first + "): refused.");
                    MadeGame.Tell(NewBuildingsText.CostRoom, "count", _costAt.Count.ToString(Plain));
                    return;
                }
                StartCoroutine(Guard.Routine(Cost(source, d)));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private IEnumerator Cost(CityBuildingController source, DesignEntry d)
        {
            _testing = true;
            Transform template = null;
            try
            {
                MadeGame.Tell(NewBuildingsText.CostStart, null, null);
                _sampler = _sampler != null ? _sampler : gameObject.AddComponent<PerfSampler>();
                _sampler.Open();
                float until = Time.unscaledTime + NewBuildingsRules.CostWindow;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                PerfWindow without = _sampler.Close();
                template = CopyGame.LookTemplate(source);
                for (int i = 0; i < _costAt.Count && template != null; i++)
                {
                    CopyLook copy = CopyGame.Stand(template, _costAt[i], _costYaw[i], "RealLife Cost Copy " + i);
                    if (copy != null)
                    {
                        _costCopies.Add(copy);
                    }
                    yield return null;   // one a frame: never a burst
                }
                until = Time.unscaledTime + SettleSeconds;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                _sampler.Open();
                until = Time.unscaledTime + NewBuildingsRules.CostWindow;
                while (Time.unscaledTime < until)
                {
                    yield return null;
                }
                PerfWindow with = _sampler.Close();
                int stood = _costCopies.Count;
                RemoveCostCopies();
                float added = with.AverageMs - without.AverageMs;
                bool pass = stood == NewBuildingsRules.CostCopies && NewBuildingsRules.CostPasses(with.AverageMs, without.AverageMs);
                Logger?.Info("Copy check (frame cost): " + stood + " copies of " + d.Name + " in view, made one a frame: the frame took " + with.AverageMs.ToString("F2", Plain)
                             + " ms with them against " + without.AverageMs.ToString("F2", Plain) + " ms without (main thread " + Ms(with.MainMs) + " against " + Ms(without.MainMs)
                             + ", graphics card " + Ms(with.GpuMs) + " against " + Ms(without.GpuMs) + "; " + with.Frames + " and " + without.Frames + " frames), "
                             + added.ToString("+0.00;-0.00", Plain) + " ms (wanted under +" + NewBuildingsRules.MostAddedMs.ToString("F1", Plain) + " ms): " + (pass ? "PASS" : "FAIL"));
                MadeGame.Tell(NewBuildingsText.CostDone, "ms", added.ToString("0.00", Plain));
            }
            finally
            {
                RemoveCostCopies();
                CopyGame.Drop(template);
                if (_sampler != null && _sampler.IsOpen)
                {
                    _sampler.Close();
                }
                _testing = false;
            }
        }

        private static string Ms(float ms) => ms >= 0f ? ms.ToString("F2", Plain) + " ms" : "not read";

        private void RemoveCostCopies()
        {
            for (int i = 0; i < _costCopies.Count; i++)
            {
                CopyGame.Remove(_costCopies[i]);
            }
            _costCopies.Clear();
        }

        /// <summary>
        /// "New buildings: switch them off": every made building hidden as the option off hides it, none destroyed; at the
        /// next game hour, a check placed after their own hourly handlers says whether those ran without an error.
        /// </summary>
        public void TestOff()
        {
            try
            {
                if (MadeGame.Loading)
                {
                    TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                    return;
                }
                if (_made.Count == 0)
                {
                    TestRefusals.TellKey(Logger, TestName, NewBuildingsText.NothingMade, "no building of the mod's own stands in this city");
                    return;
                }
                _heldHidden = true;
                BuildingCopies.RemoveAll("the switch-off test");
                Apply("the switch-off test");
                // A district's building is left to its district (Apply), so only those the switch hides are counted: with
                // Uptown's buildings standing, counting every made building failed the test whatever the switch did.
                int hides = 0, hidden = 0, kept = 0;
                for (int i = 0; i < _made.Count; i++)
                {
                    if (_made[i].Spec.Life)
                    {
                        continue;
                    }
                    hides++;
                    hidden += _made[i].Shown ? 0 : 1;
                    kept += _made[i].Controller != null ? 1 : 0;
                }
                _probeArmed = true;
                _probeSeen = null;
                EventHold.Keep(ref GlobalEvents.onNewHour, _onProbe, ref _probeSeen);
                bool after = MadeGame.HourAfterMade(_onProbe, out int handlers);
                Logger?.Info("Made buildings check (switched off): " + hidden + " of " + hides + " hidden, " + kept + " still there (none destroyed; " + (_made.Count - hides)
                             + " of a district's left to their district), the hourly check placed after their " + handlers + " hourly handler(s) in the game's list"
                             + (hidden == hides && kept == hides && after ? ": PASS" : ": FAIL"));
                MadeGame.Tell(NewBuildingsText.OffDone, null, null);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game's next hour, after the made buildings' own hourly handlers: had one thrown, the game's plain call would not have come this far.
        private void OnProbe()
        {
            if (!_probeArmed)
            {
                return;
            }
            _probeArmed = false;
            GlobalEvents.onNewHour -= _onProbe;
            int ran = 0;
            string fault = null;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                if (made.Controller == null || made.Shown)
                {
                    continue;
                }
                if (MadeGame.HourlyLight(made.Controller, out string why))
                {
                    ran++;
                }
                else
                {
                    fault = fault ?? made.Address + ": " + why;
                }
            }
            Logger?.Info("Made buildings check (hidden, the next hour): the hidden building's hourly handlers ran without an error"
                         + (fault == null ? ": PASS (" + ran + " hidden, this check after them in the game's hourly list, and their hourly light run again here)" : ": FAIL, " + fault));
        }
    }
}
