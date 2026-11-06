using System;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>One thing the Planner should say about the roommate: made again when something changes, never on a timer.</summary>
    public struct RoommateAlert
    {
        public int A;           // what it is: the Planner's id for the row
        public int Day;
        public bool Money;
        public string Title, What, Sum, IfMissed;
    }

    /// <summary>
    /// Sharing the flat: the state and the timing; the rules are <see cref="RoommateRules"/>, the game <see cref="RoommateGame"/>,
    /// the character <see cref="RoommateFlat"/>. Cost: each frame one flag test and one clock compare; a look once a minute, once
    /// a second in the shared home. A late look catches up the days missed, fourteen at most.
    /// </summary>
    public sealed partial class RoommateDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("home_roommates", "RoommateDirector", typeof(RoommateDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const string TestKey = "reallife:home_roommate_before_tests";

        private const float AwayEvery = 60f;      // seconds between looks while the player is not in the home with a roommate
        private const float HomeEvery = 1f;       // ...and while they are (as the partner's)
        private const int CatchUpDays = 14;       // the most days a late look makes up for
        private const int WakeLooks = 4;          // looks a second apart after the player comes through a door, before the minute's pace again
        private const string RentKey = "reallife_transaction_roommate_rent";
        private const string LodgerKey = "reallife_transaction_roommate_lodger";

        public static IModLogger Logger;
        public static RoommateDirector Instance { get; private set; }

        /// <summary>True while the roommate's "Talk" panel is on screen: the mod's own use of the interact key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._flat != null && Instance._flat.TakesKey;

        private readonly List<RoommateAlert> _alerts = new List<RoommateAlert>(6);
        private readonly List<Flatmate> _rolled = new List<Flatmate>(RoommateRules.CandidatesMost);
        private RoommateState _state = new RoommateState();
        private RoommateFlat _flat;
        private string _beforeTests;   // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _loaded;
        private bool _broken;
        private float _next;
        private int _wake;
        private Action _onSave;
        private Action<Address> _onEnter, _enterSeen;

        public static void Attach(GameObject root, IModLogger logger)
        {
            Logger = logger;
            RoommateGame.Logger = logger;
            root.AddComponent<RoommateDirector>();
        }

        public bool Running => _loaded && !_broken && RoommateSettings.Enabled;

        /// <summary>The state in use. Read it only while <see cref="Running"/>.</summary>
        public RoommateState State => _state;

        public IReadOnlyList<RoommateAlert> Alerts => _alerts;

        public string MateName => _state.Mate != null ? _state.Mate.Person.Name : string.Empty;

        public int Level => _state.Mate != null ? RoommateRules.Level(_state.Mate.Person) : 0;

        public bool HasHome => RoommateGame.AnyHome() != null;

        public RoomWhere WhereNow
        {
            get
            {
                Flatmate m = _state.Mate;
                int day = RoommateGame.Day;
                return m == null ? RoomWhere.Out : RoommateRules.Where(m.Person.Job, m.Traits, m.Person.Seed, day, RoommateGame.Weekday(day), RoommateGame.Hour);
            }
        }

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            _flat = new RoommateFlat(transform, Logger);
            _onSave = OnSave;
            _onEnter = Guard.Wrap<Address>(OnEnter);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onEnterBuilding -= _onEnter;
            try
            {
                _flat.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            RoommatePhone.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Home: RoommateDirector.Update");
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
            if (_flat.Out)
            {
                _flat.Frame(Time.deltaTime);
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
                Logger?.Warn("Roommates: switched off for this session after the error above. The save keeps the arrangement as it was.");
                _flat.Leave();
            }
        }

        private static readonly int HotOnEnter = HotSpots.Slot("Home: RoommateDirector.OnEnter");
        // The game's own event: the player has come into a building. The next frame looks, so the roommate is there at once.
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
            if (!RoommateSettings.Enabled)
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

            int stamp = RoommateGame.Stamp;
            if (stamp != _state.LastStamp)
            {
                Rounds(stamp);
            }
            if (_state.Living && RoommateGame.Inside(_state.HomeStreet, _state.HomeNumber))
            {
                _next = now + HomeEvery;
                RoomWhere where = WhereNow;
                _flat.Tick(Time.time, _state, where);
                Company(where);
            }
            else
            {
                if (_flat.Inside)
                {
                    _flat.Leave();
                }
                if (_wake > 0)
                {
                    _wake--;   // just through a door: the game is still setting the building up, so the next looks come at once
                    _next = now + HomeEvery;
                }
            }
        }

        /// <summary>
        /// One identity for the roommate: if they are one of the social life's people, their copy takes that person's look (Alex
        /// and Mom the fixed look of FixedPeople), so they are the same at home, at a gathering and on the phone. Called when the
        /// save is read, before they are shown at home and before their picture is drawn; a repair is noted for the log and the
        /// one notice (FixedPeople). True if anything was put right. Cheap: a few comparisons, no allocation unless it repaired.
        /// </summary>
        public bool Mend()
        {
            Flatmate mate = _state?.Mate;
            if (mate == null)
            {
                return false;
            }
            bool changed = RoommateRules.Mend(mate, mate.Friend >= 0 ? RoommateBridges.Record?.Invoke(mate.Friend) : null);
            if (changed)
            {
                FixedPeople.Mended(mate.Person.Name, "the roommate's record", mate.Person.Female, mate.Person.Age, mate.Person.Seed);
            }
            return changed;
        }

        private void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            _state = RoommateStore.Load(save.modData, out version) ?? new RoommateState();
            if (_state.Seed == 0)
            {
                _state.Seed = UnityEngine.Random.Range(1, int.MaxValue);
            }
            int day = RoommateGame.Day;
            int gap = _state.LastDay > 0 ? day - _state.LastDay : 0;
            if (_state.LastDay <= 0 || gap > 1 || gap < 0)
            {
                // A first day, or days that went by with the option off: nothing is owed for them and nobody left in them.
                _state.Shift(gap);
                _state.LastDay = day;
                _state.LastStamp = RoommateGame.Stamp;
            }
            string before;
            _beforeTests = save.modData.TryGetValue(TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            _loaded = true;
            Mend();
            RoommatePhone.Setup();
            BuildAlerts();
            Logger?.Info("Roommates: " + (_state.Living ? _state.Mate.Person.Name + " lives in (level " + Level + ", next rent day " + _state.RentDay + ")"
                : _state.AdUp ? "a notice is up with " + _state.Candidates.Count + " applicant(s)" : "nobody lives in") + " (save version " + version
                + (gap > 1 ? ", " + gap + " day(s) went by with the option off and every date moved on by as much" : string.Empty) + ").");
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
            save.modData[RoommateStore.Key] = RoommateStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(TestKey);
            }
        }

        // The option was switched off: they are put away, the arrangement is written into the save as it stands, and nothing runs.
        private void PutAway()
        {
            _flat.Leave();
            Write();
            _loaded = false;
            _alerts.Clear();
            Logger?.Info("Roommates: switched off" + (_state.Living ? "; " + _state.Mate.Person.Name + " is put away and the arrangement is kept in the save" : string.Empty) + ".");
        }

        /// <summary>Takes the roommate out of the flat; the save is not touched. For the save tools.</summary>
        public void StopAll()
        {
            try
            {
                _flat.Leave();
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
                save.modData.Remove(RoommateStore.Key);
                save.modData.Remove(TestKey);
            }
            _state = new RoommateState();
            _beforeTests = null;
            _loaded = false;
            _alerts.Clear();
            _next = 0f;
        }

        /// <summary>Runs something that is not on the director's own look (a button, a reply): a fault is logged and goes no further.</summary>
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

        private BuildingRegistration Home => _state.Living ? RoommateGame.HomeAt(_state.HomeStreet, _state.HomeNumber) : RoommateGame.AnyHome();

        private static bool PartnerIn => RoommateBridges.PartnerLivesIn != null && RoommateBridges.PartnerLivesIn();

        public int WeeklyFor(int sharePct)
        {
            return RoommateRules.Weekly(RoommateGame.RentPerDay(Home, out _), sharePct);
        }

        /// <summary>The room the home has for somebody new, what they would pay a week at a middling share, and whether they would be a lodger.</summary>
        public RoomKind RoomNow(out int weekly, out bool lodger)
        {
            BuildingRegistration home = RoommateGame.AnyHome();
            RoomKind room = RoommateGame.Room(home, PartnerIn);
            weekly = RoommateRules.Weekly(RoommateGame.RentPerDay(home, out lodger), RoommateRules.SharePct(RoommateRules.FriendSharePct, room));
            return room;
        }

        /// <summary>Puts a notice up. Null if it is up; otherwise why not (a locale key).</summary>
        public string Advertise()
        {
            RoommateState s = _state;
            BuildingRegistration home = RoommateGame.AnyHome();
            if (home == null)
            {
                return "reallife_roommate_page_no_home";
            }
            if (s.Living && !s.OnNotice)
            {
                return "reallife_roommate_no_already";
            }
            if (s.AdUp)
            {
                return "reallife_roommate_no_ad_up";
            }
            RoomKind room = RoommateGame.Room(home, PartnerIn);
            if (room == RoomKind.None)
            {
                return "reallife_roommate_page_empty_none";
            }
            if (!RoommateGame.Pay(RoommateRules.AdFee, "reallife_transaction_roommate_notice"))
            {
                return "reallife_roommate_no_money";
            }
            PutUp(room, false);
            return null;
        }

        private void PutUp(RoomKind room, bool allNow)
        {
            RoommateState s = _state;
            s.Ads++;
            s.AdUp = true;
            s.AdStamp = RoommateGame.Stamp;
            RoommateRules.Candidates(s.Seed, s.Ads, s.AdStamp, room, s.Candidates);
            for (int i = 0; allNow && i < s.Candidates.Count; i++)
            {
                s.Candidates[i].Answers = s.AdStamp;
                s.Candidates[i].Told = true;
            }
            BuildAlerts();
            Logger?.Info("Roommates: a notice is up (number " + s.Ads + ", " + s.Candidates.Count + " will answer, the room is a " + room + ").");
        }

        public void WithdrawAd()
        {
            _state.ClearAd();
            BuildAlerts();
        }

        /// <summary>Offers the room to somebody who answered. Null if they moved in; otherwise why not (a locale key).</summary>
        public string Offer(Flatmate who)
        {
            if (who == null || !_state.Candidates.Contains(who))
            {
                return "reallife_roommate_talk_nothing";
            }
            return MoveIn(who);
        }

        /// <summary>Asks a friend to move in. Null if they did; otherwise why not (a locale key).</summary>
        public string AskFriend(SocialPerson friend)
        {
            if (!RoommateRules.FriendWould(friend))
            {
                return "reallife_roommate_friend_no";
            }
            BuildingRegistration home = RoommateGame.AnyHome();
            return MoveIn(RoommateRules.Friend(friend, RoommateGame.Room(home, PartnerIn)));
        }

        private string MoveIn(Flatmate who)
        {
            RoommateState s = _state;
            if (s.Living)
            {
                return "reallife_roommate_no_still_in";
            }
            BuildingRegistration home = RoommateGame.AnyHome();
            if (home == null)
            {
                return "reallife_roommate_page_no_home";
            }
            bool partner = PartnerIn;
            RoomKind room = RoommateGame.Room(home, partner);
            if (room == RoomKind.None)
            {
                return "reallife_roommate_page_empty_none";
            }
            int weekly = RoommateRules.Weekly(RoommateGame.RentPerDay(home, out bool owned), who.SharePct);
            int day = RoommateGame.Day;
            s.MoveIn(who, day, home.StreetName, home.StreetNumber, owned, room == RoomKind.Sofa, weekly);
            RoommatePhone.Picture();   // the contact shows who lives in now
            s.PartnerSeen = partner;
            RoommateGame.Mood(ModState.MoodUp);
            RoommateText.Message("reallife_text_roommate_moved_in", Fill.For(who.Person.Name).Cost(weekly).On(RoommateText.When(s.RentDay)));
            BuildAlerts();
            Logger?.Info("Roommates: " + who.Person.Name + " moved in at " + home.StreetNumber + " " + home.StreetName + " (" + (owned ? "a lodger" : "sharing the rent") + ", "
                         + who.SharePct + "% = " + weekly + " a week, " + (room == RoomKind.Sofa ? "on the sofa" : "a bed of their own") + ", traits " + who.Traits
                         + ", known " + who.Known + (who.Friend >= 0 ? ", a friend already" : string.Empty) + ").");
            return null;
        }

        // Something happened between the two: the person's own figures move, and social life is told if they are one of its people.
        private void Moment(RoomMoment moment)
        {
            Flatmate m = _state.Mate;
            if (m == null)
            {
                return;
            }
            RoommateRules.Apply(m.Person, moment);
            if (m.Friend >= 0 && RoommateBridges.FriendMoved != null)
            {
                RoommateRules.Effect(moment, out _, out float affection, out float trust);
                if (affection != 0f || trust != 0f)
                {
                    RoommateBridges.FriendMoved(m.Friend, affection, trust);
                }
            }
        }

        private void Seen(RoomTrait trait)
        {
            Flatmate m = _state.Mate;
            if (m != null && (m.Traits & trait) != 0 && (m.Known & trait) == 0)
            {
                m.Known |= trait;
                RoommateText.Toast(NotificationType.Info, "reallife_roommate_trait_seen", Fill.For(m.Person.Name).With(RoommateText.Trait(trait)), 8f);
            }
        }

        private void BuildAlerts()
        {
            _alerts.Clear();
            RoommateState s = _state;
            int day = RoommateGame.Day;
            if (s.AdUp)
            {
                Alert(5, s.AdStamp / 24 + RoommateRules.AdDays, false, "reallife_roommate_plan_ad", "reallife_roommate_plan_ad_what", default, null);
            }
            if (!s.Living)
            {
                return;
            }
            Fill f = Fill.For(s.Mate.Person.Name);
            if (s.OnNotice)
            {
                Alert(4, s.LeaveDay, false, "reallife_roommate_plan_leaves", "reallife_roommate_plan_leaves_what", f.With(RoommateText.T(RoommateText.WhyKey(s.Notice))), null);
            }
            if (!s.OnNotice || s.RentDay < s.LeaveDay)
            {
                int weekly = WeeklyFor(s.Mate.SharePct);
                Alert(1, s.RentDay, true, "reallife_roommate_plan_rent", s.Lodger ? "reallife_roommate_plan_rent_lodger" : "reallife_roommate_plan_rent_what", f,
                    RoommateText.T("reallife_roommate_plan_pays", f.Cost(weekly)));
            }
            if (s.Owed > 0)
            {
                Alert(2, Math.Max(day, s.OwedDue), true, "reallife_roommate_plan_owed", "reallife_roommate_plan_owed_what", f, RoommateText.T("reallife_roommate_plan_pays", f.Cost(s.Owed)));
            }
            if (s.Loan > 0)
            {
                Alert(3, Math.Max(day, s.LoanDue), true, "reallife_roommate_plan_loan", "reallife_roommate_plan_loan_what", f, RoommateText.T("reallife_roommate_plan_pays", f.Cost(s.Loan)));
            }
        }

        private void Alert(int a, int day, bool money, string titleKey, string whatKey, in Fill f, string sum)
        {
            _alerts.Add(new RoommateAlert
            {
                A = a, Day = day, Money = money, Title = RoommateText.T(titleKey, f), What = RoommateText.T(whatKey, f), Sum = sum,
                IfMissed = a == 2 ? RoommateText.T("reallife_roommate_plan_owed_missed") : a == 4 ? RoommateText.T("reallife_roommate_plan_leaves_missed") : null
            });
        }
    }
}
