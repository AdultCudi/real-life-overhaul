using System;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Household
{
    /// <summary>
    /// Household staff: the state and the timing. Each member works at a home of the player's they were taken on for (or
    /// moved to). Every game hour the staff's visits are gone through (<see cref="Hour"/>): a visit is paid for through the
    /// game's money call when it begins and its work is done at that member's home when it ends (the home cleaned, portions
    /// put by in the fridge, the cars at that home washed), the mechanic's round at once; the day's note comes at nine in
    /// the evening. The rules are <see cref="HouseholdRules"/>, the game <see cref="HouseholdGame"/>, the character
    /// <see cref="HouseholdAtHome"/>. Cost: each frame one flag test and one clock compare; a look every five seconds, once
    /// a second in a staffed home. A late look makes up the hours missed, three days at most.
    /// </summary>
    public sealed partial class HouseholdDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("home_household", "HouseholdDirector", typeof(HouseholdDirector));   // its Update (Core/Guard)

        public const string TestKey = "reallife:home_household_before_tests";

        private const float AwayEvery = 5f;       // seconds between looks while the player is not in the staff's home
        private const float HomeEvery = 1f;       // ...and while they are
        private const int WakeLooks = 4;          // looks a second apart after the player comes through a door

        public static IModLogger Logger;
        public static HouseholdDirector Instance { get; private set; }

        private readonly List<HomeCar> _cars = new List<HomeCar>(8);
        private readonly List<StaffMember> _round = new List<StaffMember>(HouseholdRules.Most);
        private readonly List<BuildingRegistration> _homes = new List<BuildingRegistration>(4);   // the player's homes, filled for a move and emptied after
        private readonly List<HomeKey> _homeKeys = new List<HomeKey>(4);
        private HouseholdState _state = new HouseholdState();
        private HouseholdAtHome _atHome;
        private string _beforeTests;   // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _loaded;
        private bool _broken;
        private float _next;
        private int _wake;
        private StaffMember _forced;   // shown at home by the test tool whatever the hour, until _forcedUntil
        private float _forcedUntil;
        private Action _onSave;
        private Action<Address> _onEnter, _enterSeen;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            HouseholdGame.Logger = logger;
            root.AddComponent<HouseholdDirector>();
        }

        public bool Running => _loaded && !_broken && Settings.HouseholdStaff;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public HouseholdState State => _state;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _atHome = new HouseholdAtHome(transform, Logger);
            _onSave = OnSave;
            _onEnter = Guard.Wrap<Address>(OnEnter);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onEnterBuilding -= _onEnter;
            try
            {
                _atHome.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            HouseholdPhone.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Home: HouseholdDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
            if (_atHome.Out)
            {
                _atHome.Frame(Time.deltaTime);
            }
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + AwayEvery;
            if (_broken)
            {
                return;
            }
            try
            {
                Look(now);
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Household staff: switched off for this session after the error above. The save keeps the staff as they were.");
                _atHome.Leave();
            }
        }

        private static readonly int HotOnEnter = HotSpots.Slot("Home: HouseholdDirector.OnEnter");
        // The game's own event: the player has come into a building. The next frame looks, so the staff are there at once.
        private void OnEnter(Address address)
        {
            using var hotSpot = HotSpots.Time(HotOnEnter);
            _next = 0f;
            _wake = WakeLooks;
        }

        private void Look(float now)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                _next = now + HomeEvery;   // no save yet: asked again in a second
                return;
            }
            if (!Settings.HouseholdStaff)
            {
                if (_loaded)
                {
                    PutAway();
                }
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);

            int stamp = HouseholdGame.Stamp;
            if (stamp != _state.LastStamp)
            {
                Rounds(stamp);
            }
            if (_state.Any && HouseholdGame.InsideHome(out string street, out int number) && HouseholdRules.Staffed(_state.Staff, street, number))
            {
                _next = now + HomeEvery;
                _atHome.Tick(now, AtWorkNow(now, street, number));
            }
            else
            {
                if (_atHome.Inside)
                {
                    _atHome.Leave();
                }
                if (_wake > 0)
                {
                    _wake--;   // just through a door: the game is still setting the building up, so the next looks come at once
                    _next = now + HomeEvery;
                }
            }
            ShowCheck(now);
        }

        // Who is at work at the home the player is in: only that home's own staff.
        private StaffMember AtWorkNow(float now, string street, int number)
        {
            if (_forced != null)
            {
                if (now < _forcedUntil && _state.Staff.Contains(_forced))
                {
                    if (_forced.WorksAt(street, number))
                    {
                        return _forced;
                    }
                }
                else
                {
                    _forced = null;
                }
            }
            int day = HouseholdGame.Day;
            return HouseholdRules.OnShow(_state.Staff, day, HouseholdGame.Weekday(day), HouseholdGame.Hour, street, number);
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            _state = HouseholdStore.Load(save.modData, out int version) ?? new HouseholdState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);   // once a save, then kept: every candidate comes from it
            }
            // A load, or the option back on: nothing is made up for the hours in between, so nobody is paid for days they were not there.
            _state.LastStamp = HouseholdGame.Stamp;
            _beforeTests = save.modData.TryGetValue(TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _loaded = true;
            HouseholdPhone.Setup();
            Logger?.Info("Household staff: " + (_state.Any ? _state.Staff.Count + " at " + HouseholdRules.HomesStaffed(_state.Staff) + " home(s) (" + Names() + ")" : "nobody on the staff")
                         + " (save version " + version + (version > 0 && version < HouseholdStore.Version ? ": the one home of before is now each member's own" : string.Empty) + ").");
        }

        private string Names()
        {
            string names = null;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffMember m = _state.Staff[i];
                names = (names == null ? string.Empty : names + ", ") + m.Name + " the " + m.Kind + " at " + m.HomeNumber + " " + m.HomeStreet;
            }
            return names ?? string.Empty;
        }

        private void OnSave()
        {
            try
            {
                if (_loaded)
                {
                    Write();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[HouseholdStore.Key] = HouseholdStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(TestKey);
            }
        }

        // The option was switched off: the staff go home, the save keeps them as they are, and nothing runs or is paid.
        private void PutAway()
        {
            _atHome.Leave();
            Write();
            _loaded = false;
            _forced = null;
            Logger?.Info("Household staff: switched off" + (_state.Any ? "; the staff are kept in the save and come back when it is on again" : string.Empty) + ".");
        }

        /// <summary>Takes whoever is shown out of the home; the save is not touched. For the save tools.</summary>
        public void StopAll()
        {
            try
            {
                _forced = null;
                _atHome.Leave();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Forgets everything of this save. The next look opens the feature afresh.</summary>
        public void Forget()
        {
            StopAll();
            GameInstance save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(HouseholdStore.Key);
                save.modData.Remove(TestKey);
            }
            _state = new HouseholdState();
            _beforeTests = null;
            _loaded = false;
            _next = 0f;
        }

        // Every hour since the last look, oldest first.
        private void Rounds(int stamp)
        {
            int first = HouseholdRules.FirstToRun(_state.LastStamp, stamp);
            _state.LastStamp = stamp;
            for (int s = first; s <= stamp; s++)
            {
                Hour(s / 24, s % 24);
            }
        }

        private void Hour(int day, int hour)
        {
            if (!_state.Any)
            {
                return;
            }
            int weekday = HouseholdGame.Weekday(day);
            _round.Clear();
            _round.AddRange(_state.Staff);   // a person who leaves in the round is not met again in it
            for (int i = 0; i < _round.Count; i++)
            {
                StaffMember m = _round[i];
                if (!_state.Staff.Contains(m))
                {
                    continue;
                }
                BuildingRegistration home = WorkPlace(m, day);   // every hour, so a member of a home given up moves or leaves at once
                if (home == null)
                {
                    continue;
                }
                StaffRole role = HouseholdRules.Role(m.Kind);
                if (HouseholdRules.Ends(role, weekday, hour))
                {
                    End(m, role, day, home);
                }
                if (HouseholdRules.Starts(role, weekday, hour))
                {
                    Begin(m, role, day, home);
                }
            }
            _round.Clear();
            if (hour == HouseholdRules.NoteHour)
            {
                SendNote(day);
            }
        }

        /// <summary>
        /// The home a member works at. If the player has given it up they move to another home of the player's that has
        /// nobody of their kind (the first in the save's order, <see cref="HouseholdRules.Rehome"/>); with none they leave.
        /// </summary>
        private BuildingRegistration WorkPlace(StaffMember m, int day)
        {
            BuildingRegistration home = HouseholdGame.HomeAt(m.HomeStreet, m.HomeNumber);
            if (home != null)
            {
                return home;
            }
            HouseholdGame.Homes(_homes);
            _homeKeys.Clear();
            for (int i = 0; i < _homes.Count; i++)
            {
                _homeKeys.Add(new HomeKey(_homes[i].StreetName, _homes[i].StreetNumber));
            }
            int at = HouseholdRules.Rehome(_state.Staff, m, _homeKeys);
            home = at >= 0 ? _homes[at] : null;
            bool anyHome = _homes.Count > 0;
            _homes.Clear();
            _homeKeys.Clear();
            string from = m.HomeNumber + " " + m.HomeStreet;
            if (home != null)
            {
                if (ReferenceEquals(_atHome.Shown, m))
                {
                    _atHome.Leave();
                }
                m.HomeStreet = home.StreetName;
                m.HomeNumber = home.StreetNumber;
                Note(day, HouseholdText.T(HouseholdText.MovedOne, StaffFill.For(m.Name).With(HouseholdText.KindWord(m.Kind)).At(HouseholdGame.Where(home))));
                Logger?.Info("Household staff: " + from + " is no longer the player's home; " + m.Name + " the " + m.Kind + " works at " + home.StreetNumber + " " + home.StreetName + " now.");
                return home;
            }
            Logger?.Info("Household staff: " + from + " is no longer the player's home and "
                         + (anyHome ? "every other home of theirs has a " + m.Kind + " or the most homes have staff" : "they have no home any more")
                         + "; " + m.Name + " the " + m.Kind + " has left.");
            Dismiss(m);
            if (anyHome)
            {
                HouseholdText.Toast(NotificationType.Warning, HouseholdText.LeftNoRoom, StaffFill.For(m.Name).With(HouseholdText.KindWord(m.Kind)), 10f);
            }
            else if (!_state.Any)
            {
                HouseholdText.Toast(NotificationType.Warning, HouseholdText.NoHome, default, 10f);
            }
            return null;
        }

        private static string TransactionKey(StaffKind kind)
        {
            switch (kind)
            {
                case StaffKind.Cook: return "reallife_transaction_household_cook";
                case StaffKind.Butler: return "reallife_transaction_household_butler";
                case StaffKind.Mechanic: return "reallife_transaction_household_mechanic";
                default: return "reallife_transaction_household_cleaner";
            }
        }

        // A visit begins: it is paid for now, wages and the shopping, or it does not happen.
        private void Begin(StaffMember m, StaffRole role, int day, BuildingRegistration home)
        {
            if (role.Kind == StaffKind.Mechanic)
            {
                MechanicRound(m, day, home);
                return;
            }
            int dish = -1, portions = 0;
            if (role.Cooks)
            {
                if (!HouseholdGame.CanStore)
                {
                    if (role.Kind == StaffKind.Cook)
                    {
                        Note(day, HouseholdText.T("reallife_household_note_no_cooking", StaffFill.For(m.Name)));
                        return;
                    }
                }
                else
                {
                    int d = HouseholdRules.DishOn(_state.Seed, day, m.Kind);
                    portions = HouseholdRules.PortionsIn(d, HouseholdGame.Leftovers);
                    if (portions > 0)
                    {
                        dish = d;
                    }
                    else if (role.Kind == StaffKind.Cook)
                    {
                        Note(day, HouseholdText.T("reallife_household_note_full", StaffFill.For(m.Name).Count(HouseholdGame.Leftovers)));
                        return;   // nothing to cook for: they take the evening off, unpaid
                    }
                }
            }
            int total = HouseholdRules.VisitWage(m.Hourly, role) + HouseholdRules.Groceries(portions, HouseholdGame.PriceLevel);
            if (!HouseholdGame.Pay(total, TransactionKey(role.Kind)))
            {
                Unpaid(m, day, total);
                return;
            }
            HouseholdRules.Paid(m, day, total);
            m.Dish = dish;
            if (HouseholdGame.Inside(m.HomeStreet, m.HomeNumber) && HouseholdGame.Quiet)
            {
                HouseholdText.Toast(NotificationType.Info, HouseholdText.Arrived, StaffFill.For(m.Name).With(HouseholdText.KindWord(m.Kind)).Hours(role), 7f);
            }
            Logger?.Info("Household staff: " + m.Name + " the " + m.Kind + " began a visit at " + m.HomeNumber + " " + m.HomeStreet + ", paid " + total
                         + (dish >= 0 ? " with the shopping for " + portions + " portion(s)" : string.Empty) + ".");
        }

        // A visit ends: the work is done.
        private void End(StaffMember m, StaffRole role, int day, BuildingRegistration home)
        {
            if (m.OnDay != day)
            {
                return;   // a visit that was not paid for, or began before a load that made nothing up
            }
            float tidy = -1f;
            int put = 0, washed = 0;
            if (role.Cleans)
            {
                tidy = HouseholdGame.Clean(home, HouseholdRules.CleanPoints(m.Skill));
            }
            if (role.Cooks && m.Dish >= 0)
            {
                put = HouseholdGame.PutBy(HouseholdRules.PortionsOf(m.Dish), m.Dish);
            }
            if (role.Washes)
            {
                HouseholdGame.CarsAt(home, _cars);
                for (int i = 0; i < _cars.Count; i++)
                {
                    if (HouseholdGame.Dirty(_cars[i]))
                    {
                        HouseholdGame.Wash(_cars[i]);
                        washed++;
                    }
                }
                _cars.Clear();
            }
            StaffFill f = StaffFill.For(m.Name).Cost(m.Charged).Count(put).With(HouseholdText.DishName(m.Dish));
            switch (role.Kind)
            {
                case StaffKind.Cleaner:
                    Note(day, HouseholdText.T("reallife_household_note_cleaned", f));
                    break;
                case StaffKind.Cook:
                    Note(day, HouseholdText.T("reallife_household_note_cooked", f));
                    break;
                default:
                    Note(day, HouseholdText.T(put > 0 ? "reallife_household_note_butler" : "reallife_household_note_butler_clean", f));
                    break;
            }
            if (washed > 0)
            {
                Note(day, HouseholdText.T("reallife_household_note_washed", StaffFill.For(m.Name).Count(washed)));
            }
            m.OnDay = -1;
            m.Dish = -1;
            Logger?.Info("Household staff: " + m.Name + " the " + m.Kind + " finished a visit at " + m.HomeNumber + " " + m.HomeStreet
                         + (tidy >= 0f ? ", the home " + Mathf.RoundToInt(tidy) + "% tidy (" + HouseholdGame.FloorCells(home) + " floor cells)" : string.Empty)
                         + (role.Cooks ? ", " + put + " portion(s) put by (" + HouseholdGame.Leftovers + " in the fridge)" : string.Empty)
                         + (role.Washes ? ", " + washed + " car(s) washed" : string.Empty) + ".");
        }

        /// <summary>
        /// The mechanic's round: every car of the player's at home looked over, the call-out and the work charged at a
        /// garage's prices, then washed, repaired and serviced where it stands. With no car at home nothing is charged.
        /// </summary>
        private void MechanicRound(StaffMember m, int day, BuildingRegistration home)
        {
            HouseholdGame.CarsAt(home, _cars);
            if (_cars.Count == 0)
            {
                Note(day, HouseholdText.T("reallife_household_note_no_cars", StaffFill.For(m.Name)));
                return;
            }
            int total = HouseholdRules.CallOutAt(HouseholdGame.PriceLevel);
            for (int i = 0; i < _cars.Count; i++)
            {
                total += (HouseholdGame.Dirty(_cars[i]) ? HouseholdRules.WashFee : 0) + HouseholdGame.RepairCost(_cars[i]) + HouseholdGame.ServiceCost(_cars[i]);
            }
            if (!HouseholdGame.Pay(total, TransactionKey(StaffKind.Mechanic)))
            {
                _cars.Clear();
                Unpaid(m, day, total);
                return;
            }
            HouseholdRules.Paid(m, day, total);
            m.OnDay = -1;   // the round is done at once: nothing is left for the visit's end
            int washes = 0, repairs = 0, services = 0, cars = _cars.Count;
            for (int i = 0; i < _cars.Count; i++)
            {
                HomeCar c = _cars[i];
                if (HouseholdGame.Dirty(c))
                {
                    HouseholdGame.Wash(c);
                    washes++;
                }
                if (HouseholdGame.RepairCost(c) > 0)
                {
                    HouseholdGame.Repair(c);
                    repairs++;
                }
                if (HouseholdGame.ServiceCost(c) > 0)
                {
                    HouseholdGame.Service(c);
                    services++;
                }
            }
            _cars.Clear();
            StaffFill f = StaffFill.For(m.Name).Cost(total).Count(cars);
            if (washes + repairs + services == 0)
            {
                Note(day, HouseholdText.T("reallife_household_note_mechanic_nothing", f));
            }
            else
            {
                Note(day, HouseholdText.T("reallife_household_note_mechanic", f.With(WorkWords(washes, repairs, services))));
            }
            Logger?.Info("Household staff: " + m.Name + " the mechanic saw to " + cars + " car(s) at " + m.HomeNumber + " " + m.HomeStreet + ": " + washes + " washed, " + repairs
                         + " repaired, " + services + " serviced, " + total + " charged with the call-out.");
        }

        private static string WorkWords(int washes, int repairs, int services)
        {
            string text = null;
            text = Word(text, washes, "reallife_household_work_washed");
            text = Word(text, repairs, "reallife_household_work_repaired");
            return Word(text, services, "reallife_household_work_serviced") ?? string.Empty;
        }

        private static string Word(string text, int n, string key)
        {
            if (n <= 0)
            {
                return text;
            }
            string word = HouseholdText.T(key, new StaffFill().Count(n));
            return text == null ? word : text + ", " + word;
        }

        private void Unpaid(StaffMember m, int day, int amount)
        {
            bool quits = HouseholdRules.Unpaid(m);
            HouseholdText.Toast(NotificationType.Warning, HouseholdText.Unpaid, StaffFill.For(m.Name).Cost(amount), 9f);
            Logger?.Info("Household staff: " + m.Name + " the " + m.Kind + " was not paid " + amount + " (" + m.Missed + " in a row)" + (quits ? " and has left." : "."));
            if (quits)
            {
                Dismiss(m);
                HouseholdText.Toast(NotificationType.Warning, HouseholdText.Quit, StaffFill.For(m.Name), 10f);
            }
        }

        private void Dismiss(StaffMember m)
        {
            if (ReferenceEquals(_atHome.Shown, m) || ReferenceEquals(_forced, m))
            {
                _forced = null;
                _atHome.Leave();
            }
            _state.Remove(m);
        }

        // A line of the day's note: kept for the evening's text, or said at once with the phone's contacts off.
        private void Note(int day, string line)
        {
            if (HouseholdPhone.Contact == null)
            {
                HouseholdText.Toast(NotificationType.Info, "reallife_household_note_line", new StaffFill().With(line), 10f);
                return;
            }
            _state.AddNote(day, line);
        }

        private void SendNote(int day)
        {
            string note = _state.TakeNote(day);
            if (!string.IsNullOrEmpty(note))
            {
                HouseholdText.Message(HouseholdText.Note, new StaffFill().With(note));
            }
        }

        /// <summary>Runs something that is not on the director's own look (a page of the call, a test): a fault is logged and goes no further.</summary>
        public void Guarded(Action action)
        {
            if (!Running || SaveGameManager.Current == null || action == null)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A home of the player's: the one they stand in, else the first they have. Null with none.</summary>
        public BuildingRegistration Home => HouseholdGame.AnyHome();

        /// <summary>The home a member works at, while it is still the player's; null otherwise.</summary>
        public BuildingRegistration HomeOf(StaffMember m) => m != null ? HouseholdGame.HomeAt(m.HomeStreet, m.HomeNumber) : null;

        /// <summary>Fills the list with the player's homes, in the save's order.</summary>
        public void Homes(List<BuildingRegistration> into) => HouseholdGame.Homes(into);

        /// <summary>The person the agency puts forward now for a kind, and what they ask an hour (cents; 0 for the mechanic).</summary>
        public StaffCandidate Candidate(StaffKind kind, out int hourly)
        {
            StaffCandidate c = HouseholdRules.Candidate(_state.Seed, kind, _state.RollsOf(kind));
            StaffRole role = HouseholdRules.Role(kind);
            hourly = role != null && role.ShareBp > 0 ? HouseholdRules.Hourly(HouseholdGame.GoingRate(), role, c.Skill) : 0;
            return c;
        }

        /// <summary>The agency's next person for a kind is somebody else.</summary>
        public void PassOver(StaffKind kind)
        {
            _state.NextRoll(kind);
        }

        /// <summary>
        /// Takes on the person the agency puts forward for a kind, to work at a home of the player's. Null if they are on the
        /// staff now; otherwise why not (a locale key, filled with the most homes as {n}).
        /// </summary>
        public string Hire(StaffKind kind, BuildingRegistration home)
        {
            StaffRole role = HouseholdRules.Role(kind);
            if (home == null || !GoingPublic.Mine(home))
            {
                return "reallife_household_page_no_home";
            }
            switch (HouseholdRules.CanHire(_state.Staff, kind, home.StreetName, home.StreetNumber))
            {
                case HireRefusal.NoHome:
                    return "reallife_household_page_no_home";
                case HireRefusal.HomesFull:
                    return "reallife_household_no_homes_most";
                case HireRefusal.Taken:
                case HireRefusal.Full:
                    return "reallife_household_no_already";
            }
            if (kind == StaffKind.Cook && !HouseholdGame.CanStore)
            {
                return "reallife_household_no_cooking";
            }
            StaffCandidate c = Candidate(kind, out int hourly);
            StaffMember m = StaffMember.From(c, hourly, HouseholdGame.Day, home.StreetName, home.StreetNumber);
            _state.Staff.Add(m);
            _state.NextRoll(kind);
            Logger?.Info("Household staff: " + m.Name + " was taken on as the " + kind + " at " + m.HomeNumber + " " + m.HomeStreet + " (skill " + m.Skill + ", "
                         + (hourly > 0 ? (hourly / 100f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " an hour, "
                             + HouseholdRules.VisitWage(hourly, role) + " a visit" : "the call-out and the work at a garage's prices") + ").");
            return null;
        }

        /// <summary>
        /// Moves a member to another home of the player's. They work there from now on: a visit under way finishes there, and
        /// the next is paid as any. Null when done; otherwise why not (a locale key).
        /// </summary>
        public string Move(StaffMember m, BuildingRegistration home)
        {
            if (m == null || !_state.Staff.Contains(m) || home == null || !GoingPublic.Mine(home))
            {
                return "reallife_household_page_no_home";
            }
            if (!HouseholdRules.CanMove(_state.Staff, m, home.StreetName, home.StreetNumber))
            {
                return HouseholdRules.FindAt(_state.Staff, m.Kind, home.StreetName, home.StreetNumber) != null ? "reallife_household_no_already" : "reallife_household_no_homes_most";
            }
            if (ReferenceEquals(_atHome.Shown, m))
            {
                _atHome.Leave();   // they go now; the look puts whoever is due at either home
            }
            string from = m.HomeNumber + " " + m.HomeStreet;
            m.HomeStreet = home.StreetName;
            m.HomeNumber = home.StreetNumber;
            Logger?.Info("Household staff: " + m.Name + " the " + m.Kind + " moved from " + from + " to " + m.HomeNumber + " " + m.HomeStreet + ".");
            return null;
        }

        /// <summary>Lets somebody go, at once: nothing more is paid.</summary>
        public void LetGo(StaffMember m)
        {
            if (m == null || !_state.Staff.Contains(m))
            {
                return;
            }
            Logger?.Info("Household staff: " + m.Name + " the " + m.Kind + " was let go after " + m.Visits + " visit(s).");
            Dismiss(m);
        }
    }
}
