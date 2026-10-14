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
    /// Standing: a city that reacts to the player's success. Three parts: wealth (what they are worth, a week's income,
    /// their businesses), recognition (how many people know them: wealth, Flare's followers, the papers, the brand) and
    /// reputation (what people think of them), the last in four: customers, workers, neighbourhoods and integrity. See
    /// the Guide's Standing page.
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for the
    /// save, keeps the event handlers in place (the game empties its events on every load), looks every fifteen seconds
    /// whether somebody near the player in the street knows them (a field read and a roll; the walk over the game's
    /// passers-by only when the roll says so), and notices when the player walks into one of their own businesses
    /// (customers who know them, staff proud or nervous). The work is done on the game's own events: the day (the daily
    /// round of FameRules, from what FameGame reads), the hour, the save, and what Wiring/StandingLinks tells it. PER
    /// FRAME: NOTHING.
    ///
    /// Notifications follow Flare's rule: a routine thing (somebody in the street, a customer, the staff, a new tier) is
    /// one notice a day at most, all of them together; only what asks for a decision has its own. The rules are game-free
    /// (FameRules, FameModel, FameStore). Nothing is written into the game's own records. A fault anywhere switches
    /// Standing off for the session with one line and leaves the game and every other feature alone.
    /// </summary>
    public sealed partial class FameDirector : MonoBehaviour
    {
        private const int StreetEvery = 15;        // real seconds between two looks at the street
        private const float StreetWithin = 9f;     // metres: somebody this near can be the one who reacts
        private const float ShopSettle = 4f;       // real seconds inside before the shop is looked at: its customers are in by then

        public static IModLogger Logger;
        public static FameDirector Instance { get; private set; }

        private readonly FameInputs _inputs = new FameInputs();
        private FameFile _file;
        private bool _running, _broken;
        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen, _openPage;
        private int _seconds;
        private string _inside;                    // the business of the player's they stand in, as last seen
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

        // The first look is a second after the city's objects are made, never in the frame they are made in.
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

        // Once a second: start when the save is ready, stand down when the option goes off, hold the handlers, the street and the shop.
        private void Slow()
        {
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
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            int day = Save.Day;
            if (_file.SettledDay < 0)
            {
                PlayDay(day);   // a new file: the first round at once, so the page has figures from the start
            }
            Shop(day);
            if (++_seconds >= StreetEvery)
            {
                _seconds = 0;
                Street(day, FameGame.MinuteOfDay);
            }
            SlowMore(day);
        }

        partial void SlowMore(int day);
        partial void HourMore(int day, int hour);
        partial void DayMore(int day);
        partial void StandDownMore();

        // ---------------------------------------------------------------- starting and stopping

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
            Logger?.Info("Standing: " + (fresh ? "a file is opened for this save on day " + day + " (seed " + file.Seed + ")"
                                               : "the file is read: wealth " + file.Wealth + ", recognition " + file.Recognition + " (" + file.Tier + "), customers " + file.Rep[0]
                                                 + ", workers " + file.Rep[1] + ", neighbourhoods " + file.Rep[2] + ", integrity " + file.Rep[3] + ", " + file.Districts.Count + " district(s)") + ".");
        }

        // Standing stops. With 'keep' the file goes into the save's data first (a stop in the middle of play).
        private void StandDown(bool keep)
        {
            if (keep && _running && Save != null && _file != null)
            {
                FameStore.Save(Save.modData, _file);
            }
            StandDownMore();
            _running = false;
            _file = null;
            _inside = null;
        }

        /// <summary>A fault: one line, and nothing more of Standing this session. What the save holds of it is left as it is.</summary>
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
            Logger?.Warn("Standing: switched off for this session after an error in " + where + ". What the city thinks of the player stands still; what the save holds of it is untouched, and the game and the rest of the mod are not affected.");
        }

        /// <summary>For the options page: everything of Standing's for this save is forgotten, and a new file opens on the next second.</summary>
        public void Forget()
        {
            StandDownMore();
            _running = false;
            _file = null;
        }

        // ---------------------------------------------------------------- the hour and the day

        private static readonly int HotOnHour = HotSpots.Slot("Fame: FameDirector.OnHour");
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!Running || Save == null)
            {
                return;
            }
            try
            {
                HourMore(Save.Day, Save.Hour);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Fame: FameDirector.OnDay");
        // GlobalEvents.onNewDay: after the game's daily run, so yesterday's statement of every business is there.
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
            Logger?.Info("Standing: day " + day + ": wealth " + r.WealthBefore + " to " + r.WealthAfter + " (worth " + _file.NetWorth + ", a week " + _file.Weekly + ", " + _file.Businesses
                         + " business(es)), recognition " + r.RecognitionBefore + " to " + r.RecognitionAfter + " (" + _file.Followers + " followers, press " + _file.Press
                         + "), customers " + _file.Rep[0] + ", workers " + _file.Rep[1] + ", neighbourhoods " + _file.Rep[2] + ", integrity " + _file.Rep[3] + " (overall " + r.OverallBefore
                         + " to " + r.OverallAfter + "), " + _file.Districts.Count + " district(s).");
            if (r.TierAfter != r.TierBefore && FameRules.MayNotice(_file, day))
            {
                Tell(r.TierAfter > r.TierBefore ? NotificationType.Success : NotificationType.Info, r.TierAfter > r.TierBefore ? "reallife_fame_note_tier_up" : "reallife_fame_note_tier_down",
                    _openPage, "tier", FameText.Tier((FameTier)r.TierAfter));
            }
            DayMore(day);
        }

        // ---------------------------------------------------------------- the street

        // Somebody near the player knows them: a face over their head and, once a day at most, a few words.
        private void Street(int day, int minute)
        {
            if (!Settings.StandingStreet || _file == null || !FameGame.OnFootOutside())
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
            Logger?.Info("Standing: somebody in the street knew the player (" + reaction + ", recognition here " + known + ", overall " + _file.Overall + (d != null ? ", the district's opinion " + d.Opinion : string.Empty)
                         + (said ? ", and said so" : string.Empty) + "; " + _file.StreetToday + " today).");
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

        // ---------------------------------------------------------------- the player's own businesses

        // Once a second: has the player walked into one of their businesses? A few seconds in, its customers and staff take it in.
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
            FameDistrict d = _file.District(reg.Neighborhood);
            int known = d != null ? d.Known : FameRules.DistrictKnown(_file.Recognition, 1, 0);
            if (Settings.StandingStreet && FameRules.ShopMay(_file, day, known))
            {
                int customers = _file.Rep[(int)FamePart.Customers];
                CharacterEmojiName face = customers >= 45 ? CharacterEmojiName.CustomerGoodService : customers < 35 ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral;
                int shown = FameGame.Customers(face, 2);
                if (shown > 0)
                {
                    if (FameRules.MayNotice(_file, day))
                    {
                        Tell(NotificationType.Info, customers < 35 ? "reallife_fame_shop_cold" : "reallife_fame_shop_known", null, "business", name);
                    }
                    Logger?.Info("Standing: " + shown + " customer(s) at " + name + " knew the player (recognition there " + known + ", customers' reputation " + customers + ").");
                }
            }
            int count, standing, low;
            bool strike;
            if (FameBridges.Staff == null || !FameBridges.Staff(place, out count, out standing, out low, out strike) || count <= 0 || !FameRules.VisitCounts(_file, place, day))
            {
                return;
            }
            int mood = FameRules.VisitMood(_file.Rep[(int)FamePart.Workers]);
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
                Tell(mood > 0 ? NotificationType.Success : NotificationType.Warning, mood > 0 ? "reallife_fame_staff_proud" : "reallife_fame_staff_nervous", null, "business", name);
            }
            Logger?.Info("Standing: the staff at " + name + " are " + (mood > 0 ? "proud" : "nervous") + " to see the player (workers' reputation " + _file.Rep[(int)FamePart.Workers] + "): "
                         + moved + " of them " + (mood > 0 ? "+" : string.Empty) + mood * FameRules.VisitStanding + " standing through the company.");
        }

        // ---------------------------------------------------------------- what the other features tell it

        /// <summary>Something the player did moves a part (told by Wiring/StandingLinks). Small and capped by FameRules.Move; one line.</summary>
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
                    Logger?.Info("Standing: " + part + " " + (applied > 0 ? "+" : string.Empty) + applied + " (" + cause + (district != null ? ", " + district : string.Empty) + (applied != delta ? ", " + delta + " asked, the rest over the day's most" : string.Empty)
                                 + "): now " + _file.Rep[(int)part] + ".");
                }
                return applied;
            }
            catch (Exception e)
            {
                Fault("a change", e);
                return 0;
            }
        }

        /// <summary>What the papers and the television made of something the player did (told by Wiring): tenths of a point of recognition, within the bounds.</summary>
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

        // ---------------------------------------------------------------- saying things

        /// <summary>A notification of the game's: the key's text with its names filled in (name, value, name, value).</summary>
        internal void Tell(NotificationType type, string key, Action click, params string[] pairs)
        {
            Notifications.Show(type, key, pairs != null && pairs.Length > 1 ? FameText.Data(pairs) : null, 10f, null, click, true, false);
        }

        private void OnSave()
        {
            try
            {
                if (_running && Save != null && _file != null)
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
