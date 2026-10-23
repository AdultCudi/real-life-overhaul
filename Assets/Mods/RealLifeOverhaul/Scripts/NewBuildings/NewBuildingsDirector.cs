using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using Buildings;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The mod's own buildings and the copy engine (docs/features/new-buildings.md). Behind the loading screen, once every
    /// director is there, each building a feature declared (<see cref="MadeBuildings"/>) is made: the city's designs are
    /// measured, a design of its size is picked, the first free lot it may stand on is found, a working copy of the design
    /// stands there with the game's own controller, and its record and registration go into the game's private lookups,
    /// so the game treats it as one of its own (a door, an interior by its size, the map, the closest-building label).
    /// Nothing of it is saved: the save guard takes out whatever names a made address while a save is written. A made
    /// building is never destroyed while the city is loaded; switching the option off, or a Going Public session whose
    /// machines are not all known to run this mod in this version, hides it. Decorative copies for other features
    /// (<see cref="BuildingCopies"/>) are made here one a frame. Cost: idle, one time comparison a frame and a look once a
    /// second; while copies wait, one copy (or a millisecond of measuring) a frame.
    /// </summary>
    public sealed partial class NewBuildingsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("newbuildings", "NewBuildingsDirector", typeof(NewBuildingsDirector));   // its Update, coroutines and handlers

        private const float SlowEvery = 1f;          // s of real time between two looks
        private const float ClosestWait = 30f;       // s after the load the game's closest-building job is waited for, for the made buildings' check
        private const string TestPref = "reallife.newbuildings.test";   // not a locale key: a dot, not an underscore; the developer's ask for the test building

        public static IModLogger Logger;
        public static NewBuildingsDirector Instance { get; private set; }

        private readonly List<MadeSpec> _specs = new List<MadeSpec>(8);
        private readonly List<MadeBuilding> _made = new List<MadeBuilding>(8);
        private readonly List<Address> _homes = new List<Address>(2);
        private readonly SaveTaken _taken = new SaveTaken();
        private readonly StringBuilder _why = new StringBuilder(256);
        private Action _onSave;
        private float _nextLook, _nextSlow, _loadedAt;
        private bool _optionWas, _agreedWas, _checked, _heldHidden;   // _heldHidden: the switch-off test or the clean-up hid them

        /// <summary>True while the developer has asked for the test building (made at the next load with Developer diagnostics on).</summary>
        public static bool TestAsked => UnityEngine.PlayerPrefs.GetInt(TestPref, 0) != 0;

        private void Awake()
        {
            Instance = this;
            MadeGame.Logger = Logger;
            CopyGame.Logger = Logger;
            CopyGame.Begin(transform);
            _onSave = OnSave;
            _onProbe = Guard.Wrap(FrameGuard, OnProbe);
            _optionWas = Settings.NewBuildings;
            _agreedWas = true;
            _loadedAt = Time.unscaledTime;
            ScenePrewarm.Add("new buildings: the mod's own buildings made (the city's designs measured, a search of the scene only when any is declared)", MakeAll);
            ScenePrewarm.AddLate("new buildings: the mod's own buildings found in the city's list", Listed);
        }

        private void OnDisable()
        {
            PutBackNow("its updates stopped");
        }

        private void OnDestroy()
        {
            bool stays = Meta.SaveTools.Dormant;   // the save tools stopped the mod in a city that stays
            PutBackNow(stays ? "the save tools stopped the mod" : "the city is unloading");
            if (stays)
            {
                KeepOutOfSaves();
            }
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onProbe;
            BuildingCopies.RemoveAll(stays ? "the save tools stopped the mod" : "the city is unloading");
            MadeGame.Unload(!stays);
            FarDistricts.Release();
            MadeBuildings.Forget();
            CopyGame.Forget();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Preparing a save for removal stops the mod in a city that stays: no save guard from here on, so what names an
        // address of the mod's own is taken out of the save in memory for good, and every made building stays hidden
        // where it stands (its controller is never destroyed) until the city goes.
        private void KeepOutOfSaves()
        {
            int hidden = 0;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                if (made.Controller != null)
                {
                    CopyGame.Show(made.Controller, false);
                    made.Shown = false;
                    hidden++;
                }
            }
            int taken = MadeGame.Strip(_taken);
            _taken.Clear();
            Logger?.Info("Made buildings: the save tools stopped the mod: " + hidden + " made building(s) left hidden where they stand until the city goes, and "
                         + taken + " thing(s) naming an address of the mod's own taken out of the save for good.");
        }

        /// <summary>A copy has been asked for: the next frame looks at once.</summary>
        internal void Wake()
        {
            _nextLook = 0f;
        }

        private static readonly int HotUpdate = HotSpots.Slot("New buildings: NewBuildingsDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _nextLook)
            {
                return;
            }
            bool busy = Copies();
            if (now >= _nextSlow)
            {
                _nextSlow = now + SlowEvery;
                Slow(now);
            }
            _nextLook = busy ? 0f : _nextSlow;
        }

        // Once a second: the save guard kept on the game's event, the option and the session followed, the made buildings' check.
        private void Slow(float now)
        {
            SaveHook.KeepEssential(_onSave);   // whatever is switched off: a player inside a hidden building still saves
            if (_probeArmed)
            {
                EventHold.Keep(ref GlobalEvents.onNewHour, _onProbe, ref _probeSeen);   // the game empties its events at every load
            }
            if (MadeGame.Loading)
            {
                return;
            }
            bool option = Settings.NewBuildings;
            if (option != _optionWas)
            {
                _optionWas = option;
                OptionChanged(option);
            }
            bool agreed = MadeGame.SessionAgrees();
            if (agreed != _agreedWas)
            {
                _agreedWas = agreed;
                Apply(agreed ? "every machine of the Going Public session runs this mod in this version" : "a Going Public session whose machines are not all known to run this mod in this version");
            }
            if (!_checked && _made.Count > 0)
            {
                Check(now);
            }
        }

        // ---------------------------------------------------------------- made at the load

        // Behind the loading screen, once every director is there: every building wanted is made. A load with none declared
        // searches nothing.
        private bool MakeAll()
        {
            _specs.Clear();
            if (!Settings.NewBuildings)
            {
                Logger?.Info("Made buildings: the option is off: nothing of the mod's own is made in this city.");
                return true;
            }
            MadeBuildings.Wanted(_specs);
            if (TestAsked && Meta.Dev.Verbose && MadeBuildings.Get(NewBuildingsTestPlot.Id) == null && !Declares(NewBuildingsTestPlot.Id))
            {
                _specs.Add(NewBuildingsTestPlot.Spec());
            }
            if (_specs.Count == 0)
            {
                return true;
            }
            long began = Stopwatch.GetTimestamp();
            int streets = MadeGame.AddStreets();
            CopyGame.MeasureAll(SceneFinds.All<CityBuildingController>(
                "new buildings: the city's buildings measured for their designs and their ground (a search of the scene, at a load with made buildings)",
                FindObjectsInactive.Include));
            CopyGame.MeasureOthers(
                SceneFinds.All<NavMeshObstacle>("new buildings: the game's fixed obstacles listed for the made buildings' ground (a search of the scene)", FindObjectsInactive.Include),
                SceneFinds.All<ToggleObjectDLC>("new buildings: the DLC content listed for the made buildings' ground (a search of the scene)", FindObjectsInactive.Include));
            _specs.Sort(NewBuildingsRules.CompareSpecs);
            _homes.Clear();
            int made = 0;
            for (int i = 0; i < _specs.Count; i++)
            {
                made += Make(_specs[i]) ? 1 : 0;
            }
            if (_homes.Count > 0 && !MadeGame.LeaveOutHomes(_homes))
            {
                Logger?.Warn("Made buildings: the food deliveries could not be told to leave the made homes alone; they are hidden.");
            }
            Logger?.Info("Made buildings: " + made + " of " + _specs.Count + " made behind the loading screen in "
                         + ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency).ToString("F0", Plain) + " ms (" + streets + " street(s) of the mod's own among the game's).");
            return true;
        }

        private bool Declares(string id)
        {
            for (int i = 0; i < _specs.Count; i++)
            {
                if (_specs[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        // One building: refused, stood down or made. Every way it does not stand is one log line with the reason.
        private bool Make(MadeSpec spec)
        {
            string why = NewBuildingsRules.Refusal(spec, MadeGame.StructureTakes, MadeGame.DistrictKnown, MadeGame.HomesLeftOut());
            if (why == null && MadeGame.GameHas(spec.Street, spec.Number))
            {
                why = "the game (or another mod) already has a building at " + MadeGame.Written(spec.Street, spec.Number);
            }
            if (why != null)
            {
                Logger?.Info("Made buildings: '" + (spec != null ? spec.Id : "(none)") + "' stands down: " + why + ".");
                return false;
            }
            IList<DesignEntry> designs = CopyGame.Catalogue;
            int design = -1;
            Vector3 root = default, door = default;
            float yaw = 0f;
            _why.Length = 0;
            int lot = NewBuildingsRules.FirstFree(spec.Lots.Length, spec.Preferred, k =>
            {
                MadeLot each = spec.Lots[k];
                int pick = NewBuildingsRules.Pick(designs, spec.Size, spec.Type, spec.Design, each.Width, each.Depth, spec.Id);
                string ground = pick < 0 ? "no design of its size that can be copied fits the lot" : CopyGame.GroundWhy(designs[pick], each, out root, out yaw, out door);
                if (ground != null)
                {
                    _why.Append(_why.Length > 0 ? "; " : string.Empty).Append("lot ").Append(k).Append(": ").Append(ground);
                    return false;
                }
                design = pick;
                return true;
            });
            if (lot < 0)
            {
                Logger?.Info("Made buildings: '" + spec.Id + "' stands down: none of its " + spec.Lots.Length + " lot(s) is free now (" + _why + ").");
                return false;
            }
            Building record = MadeGame.MakeRecord(spec);
            BuildingRegistration registration = MadeGame.MakeRegistration(spec);
            if (!MadeGame.Enter(record, registration, out why))
            {
                Logger?.Warn("Made buildings: '" + spec.Id + "' stands down: " + why + ".");
                return false;
            }
            DesignEntry chosen = designs[design];
            CityBuildingController copy = CopyGame.MakeWorking(design, root, yaw, record, registration, "RealLife Made " + spec.Id);
            if (copy == null)
            {
                MadeGame.Leave(record);
                Logger?.Warn("Made buildings: '" + spec.Id + "' stands down: the game's building of the design " + chosen.Name + " is gone.");
                return false;
            }
            NewBuildingsRules.Footprint(chosen, spec.Lots[lot], out float cx, out float cz, out float hw, out float hd);
            CopyGame.KeepMade(cx, cz, spec.Lots[lot].Facing, hw, hd, spec.Id);
            MadeSpec claimed = spec;
            MadeGame.Claim(spec, () => Settings.NewBuildings && (claimed.Wanted == null || claimed.Wanted()));
            var made = new MadeBuilding
            {
                Spec = spec, Design = chosen.Name, Lot = lot, Controller = copy, Building = record, Registration = registration,
                X = root.x, Z = root.z, Yaw = yaw, DoorX = door.x, DoorY = door.y, DoorZ = door.z, DoorFacing = spec.Lots[lot].Facing, Shown = true,
                Address = MadeGame.Written(spec.Street, spec.Number)
            };
            if (spec.Type == NewBuildingsRules.HomeType)
            {
                _homes.Add(new Address(spec.Street, spec.Number));
            }
            _made.Add(made);
            MadeBuildings.Add(made);
            Logger?.Info("Made buildings: '" + spec.Id + "' made at " + made.Address + " on lot " + lot + " of " + spec.Lots.Length + " (the design " + chosen.Name + " of "
                         + chosen.Uses + " building(s) of size " + chosen.Size + ", root (" + root.x.ToString("F1", Plain) + ", " + root.y.ToString("F1", Plain) + ", "
                         + root.z.ToString("F1", Plain) + ") turned " + yaw.ToString("F0", Plain) + ", door at (" + door.x.ToString("F1", Plain) + ", " + door.z.ToString("F1", Plain) + "))"
                         + (_why.Length > 0 ? "; lots passed by: " + _why : string.Empty) + ".");
            return true;
        }

        // In the game's late call after its own start of the city: every made building is in the city's list (the game
        // listed it as one of its own), the session and the option are applied, and the copies asked for during the load
        // stand, the loading screen still up.
        private bool Listed()
        {
            for (int i = 0; i < _made.Count; i++)
            {
                if (!MadeGame.InCityList(_made[i], out int listed))
                {
                    Logger?.Warn("Made buildings: '" + _made[i].Spec.Id + "' is not in the city's list of " + listed + " buildings: its door and the map do not know it.");
                }
            }
            Apply("the city has loaded");
            CopiesNow();
            return true;
        }

        // ---------------------------------------------------------------- shown or hidden

        private void OptionChanged(bool on)
        {
            if (!on)
            {
                int copies = BuildingCopies.RemoveAll("the option is off");
                Apply("the option is switched off");
                Logger?.Info("New buildings: switched off: the made buildings are hidden (they go at the next load) and " + copies + " copies taken away.");
                return;
            }
            _heldHidden = false;
            Apply("the option is switched on");
            if (_made.Count == 0)
            {
                Logger?.Info("New buildings: switched on: the mod's own buildings are made at the next load.");
            }
        }

        // Every made building shown or hidden as the option, the session and the switch-off test want: hidden is never destroyed.
        private void Apply(string why)
        {
            bool want = Settings.NewBuildings && !_heldHidden && MadeGame.SessionAgrees();
            int changed = 0;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                if (made.Shown != want && made.Controller != null)
                {
                    CopyGame.Show(made.Controller, want);
                    made.Shown = want;
                    changed++;
                }
            }
            if (changed > 0)
            {
                MadeBuildings.Changed();
                Logger?.Info("Made buildings: " + changed + " " + (want ? "shown" : "hidden") + " (" + why + ").");
            }
        }

        /// <summary>For the options page's clean-up: every copy taken away and every made building hidden, as the option off does.</summary>
        public void StopAll(string why)
        {
            BuildingCopies.RemoveAll(why);
            _heldHidden = true;
            Apply(why);
        }

        // ---------------------------------------------------------------- the made buildings' check

        // Once, after the load, with Developer diagnostics on: each made building in the game's lookup and the city's list,
        // not in the save's registrations, and the game's closest-building job as long as the city's list.
        private void Check(float now)
        {
            if (!Meta.Dev.Verbose)
            {
                _checked = true;
                return;
            }
            int job = MadeGame.ClosestJobLength();
            if (job < 0 && now - _loadedAt < ClosestWait)
            {
                return;   // the game makes the job on the first frame it asks which building is closest
            }
            _checked = true;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                bool lookup = MadeGame.InLookup(made, out bool inSave);
                bool listed = MadeGame.InCityList(made, out int count);
                bool sized = job == count;
                string name = made.Spec.Id == NewBuildingsTestPlot.Id ? "test building" : "'" + made.Spec.Id + "'";
                Logger?.Info("Made buildings check (" + name + ", " + made.Address + "): in the game's building lookup " + (lookup ? "YES" : "NO") + ", in the city's list "
                             + (listed ? "YES" : "NO") + ", in the save's registrations " + (inSave ? "YES" : "NO") + ", the closest-building job sized to the list"
                             + (job < 0 ? " (not made in " + ClosestWait.ToString("F0", Plain) + " s)" : sized ? string.Empty : " (" + job + " for " + count + ")") + ": "
                             + (lookup && listed && !inSave && sized ? "PASS" : "FAIL"));
            }
        }

        // ---------------------------------------------------------------- the save guard

        // The game writes the save right after its event: whatever names a made address is taken out now and put back a
        // frame later. A second save in the same frame puts the first's back first.
        private void OnSave()
        {
            if (_taken.Count > 0)
            {
                PutBackNow("another save came in the same frame");
            }
            int taken = 0;
            try
            {
                taken = MadeGame.Strip(_taken);
            }
            finally
            {
                if (_taken.Count > 0)
                {
                    StartCoroutine(Guard.Routine(PutBackAfter()));   // after a fault too: each thing is noted before it is taken out
                }
            }
            if (taken > 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Made buildings: " + taken + " thing(s) naming an address of the mod's own taken out of the save while it is written ("
                             + (_taken.Current ? "the player's address " + MadeGame.Written(_taken.Street, _taken.Number) + ", the door outside written in its place; " : string.Empty)
                             + (_taken.Destination != null ? "the destination; " : string.Empty) + (_taken.Mission != null ? "the mission; " : string.Empty)
                             + _taken.Done.Count + " of the day's delivery addresses, " + _taken.Offers.Count + " food offers, " + _taken.Registrations.Count + " registrations).");
            }
        }

        private IEnumerator PutBackAfter()
        {
            yield return null;
            PutBackNow(null);
        }

        private void PutBackNow(string why)
        {
            if (_taken.Count == 0)
            {
                return;
            }
            int count = _taken.Count;
            bool back = MadeGame.PutBack(_taken);
            if (Meta.Dev.Verbose || !back)
            {
                Logger?.Info("Save check (put back): " + count + " thing(s) naming an address of the mod's own put back in the save in memory "
                             + (why != null ? "(" + why + ") " : "a frame after it was written ") + "as they were: " + (back ? "PASS" : "FAIL"));
            }
            _taken.Clear();
        }
    }
}
