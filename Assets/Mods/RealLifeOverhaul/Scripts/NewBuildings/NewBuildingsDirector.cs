using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using BAModAPI;
using Buildings;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The mod's own buildings and the copy engine (docs/features/new-buildings.md). Behind the loading screen, once every
    /// director is there, each building a feature declared (<see cref="MadeBuildings"/>) is made: the city's designs are
    /// measured, a design of its size is picked, the first free lot it may stand on is found, a working copy of the design
    /// stands there with the game's own controller, and its record and registration go into the game's private lookups,
    /// so the game treats it as one of its own (a door, an interior by its size, the map, the closest-building label).
    /// Nothing of it is saved: the district save takes out whatever names a made address while a save is written (the
    /// buildings are named to it here; the save guard does its own only where the district save is not present). A made
    /// building is never destroyed while the city is loaded; switching the option off, or a Going Public session whose
    /// machines are not all known to run this mod in this version, hides it. Decorative copies for other features
    /// (<see cref="BuildingCopies"/>) change here, one a frame and only out of view, or all behind the loading screen.
    /// Cost: idle, one time comparison a frame and a look once a second; while a copy's change waits, the camera's planes
    /// and a box test for each a frame, one change (or a millisecond of measuring) a frame.
    /// </summary>
    public sealed partial class NewBuildingsDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("newbuildings", "NewBuildingsDirector", typeof(NewBuildingsDirector));   // its Update, coroutines and handlers

        private const float SlowEvery = 1f;          // s of real time between two looks
        private const float ClosestWait = 30f;       // s after the load the game's closest-building job is waited for, for the made buildings' check
        private const string TestPref = "reallife.newbuildings.test";   // not a locale key: a dot, not an underscore; the developer's ask for the test building
        private const string TestHerePref = "reallife.newbuildings.test.here";   // not a locale key: where the developer stood when they asked
        private const string PlacesOwner = "new buildings";   // its name among the district save's providers of places

        public static IModLogger Logger;
        public static NewBuildingsDirector Instance { get; private set; }

        private readonly List<MadeSpec> _specs = new List<MadeSpec>(8);
        private readonly List<MadeBuilding> _made = new List<MadeBuilding>(8);
        private readonly List<Address> _homes = new List<Address>(2);
        private readonly SaveTaken _taken = new SaveTaken();
        private readonly StringBuilder _why = new StringBuilder(256);
        private int _tookOther;   // made at this load with another of their structures, no design of their own size fitting their lot
        private Action _onSave;
        private UnityAction _contactAdded;   // a branch's contact put back to its head office's address, on the game's own contact event
        private bool _contactsHooked;
        private float _nextLook, _nextSlow, _loadedAt;
        private bool _optionWas, _agreedWas, _checked, _heldHidden;   // _heldHidden: the switch-off test or the clean-up hid them

        /// <summary>True while the developer has asked for the test building (made at the next load with Developer diagnostics on).</summary>
        public static bool TestAsked => UnityEngine.PlayerPrefs.GetInt(TestPref, 0) != 0;

        /// <summary>Where the developer stood, and the way the camera looked, when they asked for the test building; false when asked from the main menu or indoors.</summary>
        public static bool TestHere(out float x, out float z, out float yaw)
        {
            return NewBuildingsRules.ReadSpot(UnityEngine.PlayerPrefs.GetString(TestHerePref, string.Empty), out x, out z, out yaw);
        }

        private void Awake()
        {
            Instance = this;
            MadeGame.Logger = Logger;
            CopyGame.Logger = Logger;
            CopyGame.Begin(transform);
            _onSave = OnSave;
            _onProbe = Guard.Wrap(FrameGuard, OnProbe);
            _contactAdded = Guard.Listener(OnContactAdded);
            _optionWas = Settings.NewBuildings;
            _agreedWas = true;
            _loadedAt = Time.unscaledTime;
            DistrictSave.DistrictPlaces.Add(PlacesOwner, MadeGame.NamePlaces);   // the district save keeps what names them out of every save
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
            UnhookContacts();
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

        // Preparing a save for removal stops the mod in a city that stays: every made building stays hidden where it stands
        // (its controller is never destroyed) until the city goes, and what names an address of the mod's own is taken out
        // of the save in memory for good: by the district save, which the save tools ask before their save, or, where the
        // district save is not present, by the save guard here.
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
            bool district = DistrictSave.DistrictSaveDirector.Present;
            int taken = 0;
            if (!district)
            {
                taken = MadeGame.Strip(_taken);
                _taken.Clear();
            }
            Logger?.Info("Made buildings: the save tools stopped the mod: " + hidden + " made building(s) left hidden where they stand until the city goes, and "
                         + (district ? "the district save takes what names their addresses out of the save for good."
                                     : taken + " thing(s) naming an address of the mod's own taken out of the save for good (no district save in this city)."));
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
            if (!_contactsHooked && MadeGame.HasBranches)
            {
                HookContacts();   // the phone's contacts app makes the game's event when it first wakes
            }
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
            MadeBuildings.Wanted(_specs);
            if (!Settings.NewBuildings)
            {
                for (int i = _specs.Count - 1; i >= 0; i--)
                {
                    if (!_specs[i].Life)
                    {
                        _specs.RemoveAt(i);   // a district's buildings follow their district's own switch, as the game's own buildings stand whatever this is
                    }
                }
                Logger?.Info("Made buildings: the option is off: nothing of the mod's own is made in this city"
                             + (_specs.Count > 0 ? " (but " + _specs.Count + " building(s) of a district of the game's, by their district's own switch)" : string.Empty) + ".");
            }
            else if (TestAsked && Meta.Dev.Verbose && MadeBuildings.Get(NewBuildingsTestPlot.Id) == null && !Declares(NewBuildingsTestPlot.Id))
            {
                bool here = TestHere(out float hereX, out float hereZ, out float hereYaw);
                _specs.Add(NewBuildingsTestPlot.Spec(here, hereX, hereZ, hereYaw));
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
            MadeBuildings.Arrange(CopyGame.Catalogue, e => Logger?.Error(e));
            _homes.Clear();
            _tookOther = 0;
            long measured = Stopwatch.GetTimestamp();
            int made = 0;
            double slowest = 0;
            string slowestId = null;
            for (int i = 0; i < _specs.Count; i++)
            {
                long one = Stopwatch.GetTimestamp();
                made += Make(_specs[i]) ? 1 : 0;
                double ms = Ms(one, Stopwatch.GetTimestamp());
                if (ms > slowest)
                {
                    slowest = ms;
                    slowestId = _specs[i].Id;
                }
            }
            if (_homes.Count > 0 && !MadeGame.LeaveOutHomes(_homes))
            {
                Logger?.Warn("Made buildings: the food deliveries could not be told to leave the made homes alone; they are hidden.");
            }
            long done = Stopwatch.GetTimestamp();
            double each = _specs.Count > 0 ? Ms(measured, done) / _specs.Count : 0;
            Logger?.Info("Made buildings: " + made + " of " + _specs.Count + " made behind the loading screen in " + Ms(began, done).ToString("F0", Plain) + " ms (the city's designs and "
                         + "ground measured in " + Ms(began, measured).ToString("F0", Plain) + " ms, its three searches among them; the buildings in " + Ms(measured, done).ToString("F0", Plain)
                         + " ms, " + each.ToString("F2", Plain) + " ms each, the slowest " + slowest.ToString("F1", Plain) + " ms ('" + slowestId + "'); " + streets
                         + " street(s) of the mod's own among the game's" + (_tookOther > 0 ? "; " + _tookOther + " with another of their sizes, no design of their own fitting" : string.Empty)
                         + ").");
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Made buildings check (load time): " + _specs.Count + " building(s) declared, each made in " + each.ToString("F2", Plain) + " ms on average (wanted under "
                             + NewBuildingsRules.MostMakeMs.ToString("F1", Plain) + " ms), the slowest in " + slowest.ToString("F1", Plain) + " ms: "
                             + (each <= NewBuildingsRules.MostMakeMs ? "PASS" : "FAIL"));
            }
            return true;
        }

        private static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

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

        /// <summary>
        /// One building of a district made in play, by the code that makes every building at a load (<see cref="Make"/>), the
        /// city's designs and ground already measured: the made building, or null with why (Player.log has the line). The caller
        /// has arranged its declaration (Uptown's FrontageRules) and puts the building in its district's lists.
        /// </summary>
        internal MadeBuilding MakeNow(MadeSpec spec, out string why)
        {
            why = null;
            if (spec == null || !spec.Life)
            {
                why = "only a district's building is made in play";
                return null;
            }
            if (!CopyGame.Ready || CopyGame.DesignCount == 0 || MadeGame.Loading)
            {
                why = "the city's designs are not measured, or a load is under way";
                return null;
            }
            int before = _made.Count;
            _standWhy = null;
            if (!Make(spec, true) || _made.Count <= before)
            {
                why = _standWhy ?? "it stood down";
                return null;
            }
            return _made[_made.Count - 1];
        }

        private string _standWhy;   // why the last building made stood down: MakeNow says it

        /// <summary>
        /// A district's building knocked down in play, let go from the made buildings for good (it stays hidden where it stands, as a made
        /// building's copy does, until the city goes): out of the game's lookup of its address, out of the list of the made buildings
        /// and of the ground they keep, so a building made on its lot in play is found at that address and held off by nothing. It leaves
        /// the city's list and the city map's filters with it (MadeGame.LeaveCity), so no marker is left on the map where it stood.
        /// </summary>
        internal void Retire(MadeBuilding old)
        {
            if (old == null)
            {
                return;
            }
            if (old.Building != null)
            {
                MadeGame.Leave(old.Building);
            }
            if (!MadeGame.LeaveCity(old, out string mapWhy))
            {
                Logger?.Warn("Made buildings: '" + old.Spec.Id + "' (knocked down) could not leave the city's list and the map's filters (" + mapWhy + "); it leaves them at the next load.");
            }
            CopyGame.ReleaseMade(old.Spec.Id);
            _made.Remove(old);
            MadeBuildings.Drop(old);
            Logger?.Info("Made buildings: '" + old.Spec.Id + "' (knocked down) let go from the made buildings; its ground and its address are free to build on in play.");
        }

        // One building: refused, stood down or made. Every way it does not stand is one log line with the reason.
        // inPlay: made after the load, so the copies the mod itself stands (fillers, a site's look) are not in its way.
        private bool Make(MadeSpec spec, bool inPlay = false)
        {
            string why = NewBuildingsRules.Refusal(spec, MadeGame.StructureTakes, MadeGame.DistrictKnown, MadeGame.HomesLeftOut());
            if (why == null && MadeGame.GameHas(spec.Street, spec.Number))
            {
                why = "the game (or another mod) already has a building at " + MadeGame.Written(spec.Street, spec.Number);
            }
            if (why != null)
            {
                _standWhy = why;
                Logger?.Info("Made buildings: '" + (spec != null ? spec.Id : "(none)") + "' stands down: " + why + ".");
                return false;
            }
            IList<DesignEntry> designs = CopyGame.Catalogue;
            int design = -1;
            Vector3 root = default, door = default;
            float yaw = 0f;
            _why.Length = 0;
            // A special-type place wears the look of the ordinary type it stands as without its service: the copy engine never copies a special building's look.
            string size = spec.Size, type = spec.Type == NewBuildingsRules.SpecialType && !string.IsNullOrEmpty(spec.PlainType) ? spec.PlainType : spec.Type;
            bool noDesign = true;   // every lot so far failed for want of a design of the size, none for its ground
            int lot = -1, took = -1;
            // Its own structure first; only where no design of its size fits any lot, the others it may take, in their order:
            // a lot whose ground refused a design refuses the others' too, as its roads and walking mesh stay what they are.
            for (int s = -1; lot < 0 && noDesign && s < (spec.Others != null ? spec.Others.Length : 0); s++)
            {
                if (s >= 0)
                {
                    if (!NewBuildingsRules.OtherTakes(spec, spec.Others[s], MadeGame.StructureTakes, MadeGame.HomesLeftOut()))
                    {
                        continue;
                    }
                    size = spec.Others[s].Size;
                    type = spec.Others[s].Type;
                }
                lot = NewBuildingsRules.FirstFree(spec.Lots.Length, spec.Preferred, k =>
                {
                    MadeLot each = spec.Lots[k];
                    int pick = NewBuildingsRules.Pick(designs, size, type, spec.Design, each.Width, each.Depth, spec.Id, spec.Whole);
                    noDesign &= pick < 0;
                    string ground = pick < 0 ? "no design of size " + NewBuildingsRules.StructureName(size, 0).TrimEnd('0') + " that can be copied fits the lot"
                        : CopyGame.GroundWhy(designs[pick], each, out root, out yaw, out door, inPlay);
                    if (ground != null)
                    {
                        _why.Append(_why.Length > 0 ? "; " : string.Empty).Append("lot ").Append(k).Append(": ").Append(ground);
                        return false;
                    }
                    design = pick;
                    return true;
                });
                took = lot >= 0 ? s : took;
            }
            if (lot < 0)
            {
                _standWhy = "none of its " + spec.Lots.Length + " lot(s) is free now (" + _why + ")";
                Logger?.Info("Made buildings: '" + spec.Id + "' stands down: " + _standWhy + ".");
                return false;
            }
            if (took >= 0)
            {
                MadeStructure other = spec.Others[took];   // the design is of this structure's size: the record and the registration are made with it
                spec.Size = other.Size;
                spec.Version = other.Version;
                spec.Type = other.Type;
                _tookOther++;
            }
            // The registration first: whether the record may carry its service depends on whose the save says the lot is.
            BuildingRegistration registration = MadeGame.MakeRegistration(spec, out bool fresh);
            Building record = MadeGame.MakeRecord(spec, spec.DesignArea ? CopyGame.SourceArea(design) : 0, registration, fresh, out why);
            if (record == null)
            {
                _standWhy = why;
                MadeGame.Forsake(registration, fresh);
                Logger?.Info("Made buildings: '" + spec.Id + "' stands down: " + why + ".");
                return false;
            }
            if (!MadeGame.Enter(record, registration, out why))
            {
                _standWhy = why;
                MadeGame.Forsake(registration, fresh);
                Logger?.Warn("Made buildings: '" + spec.Id + "' stands down: " + why + ".");
                return false;
            }
            DesignEntry chosen = designs[design];
            CityBuildingController copy = CopyGame.MakeWorking(design, root, yaw, record, registration, "RealLife Made " + spec.Id, spec.Id);
            if (copy == null)
            {
                _standWhy = "the game's building of the design " + chosen.Name + " is gone";
                MadeGame.Leave(record);
                MadeGame.Forsake(registration, fresh);
                Logger?.Warn("Made buildings: '" + spec.Id + "' stands down: " + _standWhy + ".");
                return false;
            }
            NewBuildingsRules.Footprint(chosen, spec.Lots[lot], out float cx, out float cz, out float hw, out float hd);
            CopyGame.KeepMade(cx, cz, spec.Lots[lot].Facing, hw, hd, spec.Id);
            int litter = CopyGame.PutAwayLitter();
            if (litter > 0)
            {
                Logger?.Info("Made buildings: " + litter + " bag(s) of the game's litter put away from the ground of '" + spec.Id + "'.");
            }
            if (!spec.Life)
            {
                MadeSpec claimed = spec;   // a district's building is the game's to let and its rivals' to run: never held in the book
                MadeGame.Claim(spec, () => Settings.NewBuildings && (claimed.Wanted == null || claimed.Wanted()));
            }
            var made = new MadeBuilding
            {
                Spec = spec, Design = chosen.Name, Lot = lot, Controller = copy, Building = record, Registration = registration,
                X = root.x, Z = root.z, Yaw = yaw, DoorX = door.x, DoorY = door.y, DoorZ = door.z, DoorFacing = NewBuildingsRules.Turn(yaw + chosen.DoorFacing), Shown = true,
                Address = MadeGame.Written(spec.Street, spec.Number), DesignIndex = design, NewRegistration = fresh
            };
            if (spec.Type == NewBuildingsRules.HomeType && !spec.Life)
            {
                _homes.Add(new Address(spec.Street, spec.Number));
            }
            _made.Add(made);
            MadeBuildings.Add(made);
            if (!spec.Life || Meta.Dev.Verbose)   // a district's hundreds are counted in the load's line; each one's with Developer diagnostics on
            {
                Logger?.Info("Made buildings: '" + spec.Id + "' made at " + made.Address + " on lot " + lot + " of " + spec.Lots.Length + " (the design " + chosen.Name + " of "
                             + chosen.Uses + " building(s) of size " + chosen.Size + ", root (" + root.x.ToString("F1", Plain) + ", " + root.y.ToString("F1", Plain) + ", "
                             + root.z.ToString("F1", Plain) + ") turned " + yaw.ToString("F0", Plain) + ", door at (" + door.x.ToString("F1", Plain) + ", " + door.z.ToString("F1", Plain) + "))"
                             + (_why.Length > 0 ? "; lots passed by: " + _why : string.Empty) + ".");
            }
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
            if (MadeGame.HasBranches)
            {
                int moved = MadeGame.Rebranch();
                HookContacts();
                if (moved > 0)
                {
                    Logger?.Info("Made buildings: " + moved + " contact(s) met at a branch given their head office's address again.");
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- a branch's contact

        private void HookContacts()
        {
            if (_contactsHooked || ContactsApp.onContactAdded == null)
            {
                return;
            }
            ContactsApp.onContactAdded.AddListener(_contactAdded);
            _contactsHooked = true;
        }

        private void UnhookContacts()
        {
            if (_contactsHooked && ContactsApp.onContactAdded != null)
            {
                ContactsApp.onContactAdded.RemoveListener(_contactAdded);
            }
            _contactsHooked = false;
        }

        // The game raises it as a contact is made, before the counter that made it opens its dialog with it (Contact.CreateContact).
        private void OnContactAdded()
        {
            int moved = MadeGame.Rebranch();
            if (moved > 0)
            {
                Logger?.Info("Made buildings: a contact met at a branch keeps its head office's address, as the game's own bank does (" + moved + ").");
            }
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
        // A district's building is left to its district (Scripts/Uptown): its registration is the game's own, and hiding it
        // under a business the game runs would leave a door nobody can find.
        private void Apply(string why)
        {
            bool want = Settings.NewBuildings && !_heldHidden && MadeGame.SessionAgrees();
            int changed = 0;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                if (made.Spec.Life)
                {
                    continue;
                }
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
        // not in the save's registrations (a district's building: in them, as the game's own are), and the game's
        // closest-building job as long as the city's list.
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
            int districts = 0, districtsPassed = 0;
            for (int i = 0; i < _made.Count; i++)
            {
                MadeBuilding made = _made[i];
                bool lookup = MadeGame.InLookup(made, out bool inSave);
                bool listed = MadeGame.InCityList(made, out int count);
                bool sized = job == count;
                bool pass = lookup && listed && inSave == made.Spec.Life && sized;
                if (made.Spec.Life)
                {
                    districts++;
                    districtsPassed += pass ? 1 : 0;
                    if (pass)
                    {
                        continue;   // a district's hundreds are one line below; a failure is said on its own
                    }
                }
                string name = made.Spec.Id == NewBuildingsTestPlot.Id ? "test building" : "'" + made.Spec.Id + "'";
                Logger?.Info("Made buildings check (" + name + ", " + made.Address + "): in the game's building lookup " + (lookup ? "YES" : "NO") + ", in the city's list "
                             + (listed ? "YES" : "NO") + ", in the save's registrations " + (inSave ? "YES" : "NO") + ", the closest-building job sized to the list"
                             + (job < 0 ? " (not made in " + ClosestWait.ToString("F0", Plain) + " s)" : sized ? string.Empty : " (" + job + " for " + count + ")") + ": "
                             + (pass ? "PASS" : "FAIL"));
            }
            if (districts > 0)
            {
                Logger?.Info("Made buildings check (a district's buildings): " + districtsPassed + " of " + districts + " in the game's building lookup, the city's list and the save's "
                             + "registrations, the closest-building job sized to the list" + (job < 0 ? " (not made in " + ClosestWait.ToString("F0", Plain) + " s)" : string.Empty) + ": "
                             + (districtsPassed == districts ? "PASS" : "FAIL"));
            }
        }

        // ---------------------------------------------------------------- the save guard

        // The game writes the save right after its event: whatever names a made address is taken out now and put back a
        // frame later. A second save in the same frame puts the first's back first. While the district save is present it
        // alone does this (one owner of every record), and the guard takes nothing.
        private void OnSave()
        {
            if (DistrictSave.DistrictSaveDirector.Present)
            {
                return;
            }
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
