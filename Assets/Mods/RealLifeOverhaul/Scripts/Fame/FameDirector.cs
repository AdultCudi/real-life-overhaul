using System;
using System.Collections;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// Standing: a city that reacts to the player's wealth, recognition and reputation. Per frame: nothing; a one-second
    /// timer starts it and watches the street and the player's own businesses, and the rest runs on the game's hour
    /// and day. Routine notices share one a day. A fault switches Standing off for the session.
    /// </summary>
    public sealed partial class FameDirector : MonoBehaviour
    {
        private const int StreetEvery = 15;        // real seconds between two looks at the street
        private const float StreetWithin = 9f;     // metres
        private const float ShopSettle = 4f;       // real seconds inside before the shop reacts: its customers are in by then

        private static readonly int HotTick = HotSpots.Slot("Fame: FameDirector.Tick");
        private static readonly int HotOnHour = HotSpots.Slot("Fame: FameDirector.OnHour");
        private static readonly int HotOnDay = HotSpots.Slot("Fame: FameDirector.OnDay");

        public static IModLogger Logger;
        public static FameDirector Instance { get; private set; }

        private readonly FameInputs _inputs = new FameInputs();
        private FameFile _file;
        private bool _running, _broken;
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openPage;
        private int _seconds;
        private string _inside;                    // the player's business they stand in, as last seen
        private float _insideSince;
        private bool _insideDone;

        public bool Running => _running && !_broken;

        /// <summary>The save's file; null while Standing is not running.</summary>
        public FameFile File => Running ? _file : null;

        private static GameInstance Save => SaveGameManager.Current;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _openPage = () => FameBridges.Open?.Invoke();
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            try
            {
                StandDown(false);
                _panel?.Destroy();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The first look waits a second, never the frame the city's objects are made in.
        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (_broken)
                {
                    continue;
                }
                while (StartLine.Holds(StartSeats.Fame))
                {
                    yield return null;   // the first tick after a load gets a frame of its own (Core/StartLine)
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Fault("the timer", e);
                }
            }
        }

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!Settings.Standing)
            {
                if (_running)
                {
                    StandDown(true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);   // the game empties its events on every load
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            int day = Save.Day;
            if (_file.SettledDay < 0)
            {
                PlayDay(day);   // a new file: figures for the page at once
            }
            Shop(day);
            if (++_seconds >= StreetEvery)
            {
                _seconds = 0;
                Street(day, FameGame.MinuteOfDay);
            }
            PressSecond(day);
        }

        private void StartUp()
        {
            int day = Save.Day;
            FameFile file = FameStore.Load(Save.modData);
            bool fresh = file == null;
            if (fresh)
            {
                int seed;
                unchecked
                {
                    seed = (int)(Environment.TickCount * 31 + day * 7919 + (int)Save.Money);
                }
                file = FameRules.Open(seed, day);
            }
            _file = file;
            _running = true;
            _inside = null;
            Logger?.Info("Standing: " + (fresh
                ? "a file is opened for this save on day " + day + " (seed " + file.Seed + ")"
                : "the file is read: wealth " + file.Wealth + ", recognition " + file.Recognition + " (" + file.Tier + "), customers " + file.Rep[0]
                  + ", workers " + file.Rep[1] + ", neighbourhoods " + file.Rep[2] + ", integrity " + file.Rep[3] + ", " + file.Districts.Count + " district(s)") + ".");
        }

        // keep: the file goes into the save's data first (a stop in the middle of play).
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null)
            {
                FameStore.Save(Save.modData, _file);
            }
            PressStandDown();
            _running = false;
            _file = null;
            _inside = null;
        }

        /// <summary>One line, and nothing more of Standing this session; the save keeps what it holds.</summary>
        internal void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown(false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Standing: switched off for this session after an error in " + where
                         + ". What the city thinks of the player stands still; what the save holds of it is untouched, and the game and the rest of the mod are not affected.");
        }

        /// <summary>For the options page: Standing forgets this save's file, and a new one opens on the next second.</summary>
        public void Forget()
        {
            PressStandDown();
            _running = false;
            _file = null;
        }

        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                PressHour(Save.Day, Save.Hour);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // The game's onNewDay comes after its daily run, so yesterday's statement of every business is there.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day != _file.SettledDay)
                {
                    PlayDay(day);
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private void PlayDay(int day)
        {
            FameGame.Read(_inputs);
            FameDayReport r = FameRules.Day(_file, _inputs, day);
            Logger?.Info("Standing: day " + day + ": wealth " + r.WealthBefore + " to " + r.WealthAfter + " (worth " + _file.NetWorth + ", a week " + _file.Weekly
                         + ", " + _file.Businesses + " business(es)), recognition " + r.RecognitionBefore + " to " + r.RecognitionAfter + " (" + _file.Followers
                         + " followers, press " + _file.Press + "), customers " + _file.Rep[0] + ", workers " + _file.Rep[1] + ", neighbourhoods " + _file.Rep[2]
                         + ", integrity " + _file.Rep[3] + " (overall " + r.OverallBefore + " to " + r.OverallAfter + "), " + _file.Districts.Count + " district(s).");
            if (r.TierAfter != r.TierBefore && FameRules.MayNotice(_file, day))
            {
                bool up = r.TierAfter > r.TierBefore;
                Tell(up ? NotificationType.Success : NotificationType.Info, up ? "reallife_fame_note_tier_up" : "reallife_fame_note_tier_down",
                    _openPage, "tier", FameText.Tier((FameTier)r.TierAfter));
            }
            PressDay(day);
        }

        private void Street(int day, int minute)
        {
            if (!Settings.StandingStreet || !FameGame.OnFootOutside())
            {
                return;
            }
            FameDistrict d = _file.District(FameGame.DistrictHere());
            int known = d != null ? d.Known : FameRules.DistrictKnown(_file.Recognition, 0, 0);
            int chance = FameRules.StreetChance(known);
            if (chance <= 0 || Random.Range(0, 1000) >= chance)
            {
                return;
            }
            ThirdPersonCharacter who = FameGame.PasserBy(StreetWithin);
            if (who == null || !FameRules.StreetMay(_file, day, minute))
            {
                return;
            }
            int roll = Random.Range(0, 1000);
            FameReaction reaction = FameRules.React(_file.Overall, _file.Wealth, d, roll);
            FameGame.Face(who, Emoji(reaction));
            string line = FameRules.Speaks(reaction) ? FameText.StreetLine(reaction, _file.Wealth >= 50, Random.Range(0, 1000)) : null;
            bool said = line != null && FameRules.MayNotice(_file, day);
            if (said)
            {
                Notifications.Show(reaction >= FameReaction.Rents ? NotificationType.Warning : NotificationType.Info, line, null, 7f);
            }
            Logger?.Info("Standing: somebody in the street knew the player (" + reaction + ", recognition here " + known + ", overall " + _file.Overall
                         + (d != null ? ", the district's opinion " + d.Opinion : string.Empty) + (said ? ", and said so" : string.Empty) + "; "
                         + _file.StreetToday + " today).");
        }

        private static CharacterEmojiName Emoji(FameReaction reaction)
        {
            switch (reaction)
            {
                case FameReaction.Praise: return CharacterEmojiName.CustomerGoodService;
                case FameReaction.Greeting:
                case FameReaction.Remark: return CharacterEmojiName.CommonGreenHappy;
                case FameReaction.Rents:
                case FameReaction.Complaint: return CharacterEmojiName.CommonRedAngry;
                default: return CharacterEmojiName.CommonYellowNeutral;
            }
        }

        // Its customers and staff react a few seconds after the player walks into one of their businesses.
        private void Shop(int day)
        {
            BuildingRegistration reg = FameGame.Inside();
            string place = reg != null ? FameGame.Place(reg) : null;
            if (place != _inside)
            {
                _inside = place;
                _insideSince = Time.realtimeSinceStartup;
                _insideDone = false;
                return;
            }
            if (place == null || _insideDone || Time.realtimeSinceStartup - _insideSince < ShopSettle)
            {
                return;
            }
            _insideDone = true;
            string name = FameGame.Name(reg);
            FameDistrict d = _file.District(GameRegistrations.District(reg));
            int known = d != null ? d.Known : FameRules.DistrictKnown(_file.Recognition, 1, 0);
            if (Settings.StandingStreet && FameRules.ShopMay(_file, day, known))
            {
                ShopCustomers(day, name, known);
            }
            int count, standing, low;
            bool strike;
            if (FameBridges.Staff == null || !FameBridges.Staff(place, out count, out standing, out low, out strike) || count <= 0
                || !FameRules.VisitCounts(_file, place, day))
            {
                return;
            }
            int workers = _file.Rep[(int)FamePart.Workers];
            int mood = FameRules.VisitMood(workers);
            if (mood == 0)
            {
                return;
            }
            int moved = FameBridges.Visited != null ? FameBridges.Visited(place, mood * FameRules.VisitStanding) : 0;
            if (moved <= 0)
            {
                return;
            }
            if (FameRules.MayNotice(_file, day))
            {
                Tell(mood > 0 ? NotificationType.Success : NotificationType.Warning, mood > 0 ? "reallife_fame_staff_proud" : "reallife_fame_staff_nervous",
                    null, "business", name);
            }
            Logger?.Info("Standing: the staff at " + name + " are " + (mood > 0 ? "proud" : "nervous") + " to see the player (workers' reputation " + workers + "): "
                         + moved + " of them " + (mood > 0 ? "+" : string.Empty) + mood * FameRules.VisitStanding + " standing through the company.");
        }

        private void ShopCustomers(int day, string name, int known)
        {
            int customers = _file.Rep[(int)FamePart.Customers];
            CharacterEmojiName face = customers >= 45 ? CharacterEmojiName.CustomerGoodService
                : customers < 35 ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral;
            int shown = FameGame.Customers(face, 2);
            if (shown <= 0)
            {
                return;
            }
            if (FameRules.MayNotice(_file, day))
            {
                Tell(NotificationType.Info, customers < 35 ? "reallife_fame_shop_cold" : "reallife_fame_shop_known", null, "business", name);
            }
            Logger?.Info("Standing: " + shown + " customer(s) at " + name + " knew the player (recognition there " + known + ", customers' reputation " + customers + ").");
        }

        /// <summary>Something the player did moves a part, within the day's most (told through Wiring/StandingLinks).</summary>
        public int Moved(FamePart part, FameCause cause, int delta, string district = null)
        {
            if (!Running || Save == null)
            {
                return 0;
            }
            try
            {
                int applied = FameRules.Move(_file, part, cause, delta, Save.Day, district);
                if (applied != 0)
                {
                    Logger?.Info("Standing: " + part + " " + (applied > 0 ? "+" : string.Empty) + applied + " (" + cause + (district != null ? ", " + district : string.Empty)
                                 + (applied != delta ? ", " + delta + " asked, the rest over the day's most" : string.Empty) + "): now " + _file.Rep[(int)part] + ".");
                }
                return applied;
            }
            catch (Exception e)
            {
                Fault("a change", e);
                return 0;
            }
        }

        /// <summary>What the papers and the television made of something the player did, in tenths of a point of recognition.</summary>
        public int Publicised(int tenths, FameCause cause)
        {
            if (!Running || Save == null || tenths == 0)
            {
                return 0;
            }
            try
            {
                return FameRules.Publicity(_file, tenths, Save.Day, cause);
            }
            catch (Exception e)
            {
                Fault("publicity", e);
                return 0;
            }
        }

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Notifications.Show(type, key, pairs.Length > 1 ? FameText.Data(pairs) : null, 10f, null, Guard.Wrap(click), true, false);
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null)
                {
                    FameStore.Save(Save.modData, _file);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
