using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// Permadeath: an ending the player can choose, off unless switched on, kept per save. Dangerous moments are judged
    /// here through <see cref="Mortality"/>; a death is written into the save and saved at once (and kept in the
    /// machine's memorial for older saves), its obituary shown on the City Ledger's page, then the main menu. A save that
    /// has ended shows the same page and is not played. With it on, the hunger bar is watched for going without food.
    /// Cost: per frame one flag test (the cancel key while the obituary is up); once a second a few comparisons and the
    /// hunger bar; once a game day one walk over the businesses.
    /// </summary>
    public sealed partial class PermadeathDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("permadeath", "PermadeathDirector", typeof(PermadeathDirector));

        public static IModLogger Logger;
        public static PermadeathDirector Instance { get; private set; }

        private static readonly string[] IllKeys = { "reallife_pd_ill_1", "reallife_pd_ill_2", "reallife_pd_ill_3" };
        private static readonly string[] StarveKeys = { "reallife_pd_starve_1", "reallife_pd_starve_2", "reallife_pd_starve_3" };
        private const int StarveCatchUpMost = 48;   // game hours caught up at most in one look, with the clock run on fast

        private readonly LifeStory _story = new LifeStory();     // the life being printed
        private readonly List<string> _keys = new List<string>(8);
        private readonly List<string> _names = new List<string>(8);
        private GameInstance _save;
        private int _day = -1;
        private bool _ending;       // a death, a rehearsal or an ended save's obituary is under way
        private bool _elsewhere;    // an older save of a character who has died since, by the memorial
        private bool _pageUp;       // the obituary is on screen: the cancel key puts it down
        private bool _closed;
        private bool _asking;       // the question that comes before switching it on is up
        private bool _broken;
        private float _lastHunger = -1f;   // the bar at the last look; -1 not looked at yet
        private int _starveHour = -1;      // the game hour going without food was last judged in
        private Action _onSave;
        private Action _onClosed;
        private Action _onChanged;

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onClosed = PageClosed;
            _onChanged = Resync;
            Settings.Changed += _onChanged;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            Settings.Changed -= _onChanged;
            try
            {
                PermadeathGame.Hold(false, false);
            }
            catch (Exception e)
            {
                // What follows must run whatever this did, or on a game lacking something Hold names the switch below is
                // left on for the next game begun.
                if (!Guard.DoesWithout(e, "letting the player walk again as the city closes"))
                {
                    Logger?.Error(e);
                }
            }
            PermadeathGame.ClearWeak();
            PermadeathState.Reset();
            Settings.SetPermadeath(false);   // in the main menu the switch is no save's: on there, it is for the next game begun
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Permadeath: PermadeathDirector.Update");

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
            if (!_pageUp)
            {
                return;
            }
            if (PermadeathGame.CancelPressed())
            {
                PermadeathGame.CloseMenu();
            }
        }

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
                while (StartLine.Holds(StartSeats.Permadeath))
                {
                    yield return null;   // its first look after a load waits for a frame of its own
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Permadeath: switched off for this session after the error above. "
                                 + "Nothing can end the character's life until the game is loaded again; the save's state is untouched.");
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Permadeath: PermadeathDirector.Tick");

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = PermadeathGame.Save;
            if (save == null || GameManager.isCitySceneBeingUnloaded || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                FirstLook(save);
            }
            SaveHook.Keep(_onSave);
            if (_ending)
            {
                return;
            }
            if (PermadeathRules.Ended(PermadeathState.Dead, PermadeathState.Armed, _elsewhere))
            {
                StartCoroutine(Guard.Routine(Ended()));
                return;
            }
            if (save.Day != _day)
            {
                _day = save.Day;
                Chronicle();
            }
            Hunger();
        }

        // Once a second: something eaten ends the danger; at the bar's bottom the rules are asked once a game hour (hours
        // missed caught up, at most one warning a look). Off, dead, or with the game's energy off, nothing is kept.
        private void Hunger()
        {
            if (!PermadeathState.Armed || PermadeathState.Dead || PermadeathGame.NoEnergy)
            {
                if (PermadeathState.EmptySince >= 0f || PermadeathState.StarveTold > 0)
                {
                    PermadeathState.Fed();
                    PermadeathGame.Weak(0);
                }
                _lastHunger = -1f;
                return;
            }
            float hunger = PermadeathGame.Hunger;
            float now = PermadeathGame.NowMinutes;
            bool ate = _lastHunger >= 0f && PermadeathRules.Ate(_lastHunger, hunger);
            _lastHunger = hunger;
            if (ate || !PermadeathRules.Empty(hunger))
            {
                if (PermadeathState.EmptySince >= 0f)
                {
                    bool warned = PermadeathState.StarveTold > 0;
                    Logger?.Info("Permadeath: going without food is over after " + ((now - PermadeathState.EmptySince) / 60f).ToString("F0") + " hours at the bottom of the bar"
                                 + (ate ? " (something eaten)" : string.Empty) + "; warnings shown " + PermadeathState.StarveTold + ".");
                    PermadeathState.Fed();
                    PermadeathGame.Weak(0);
                    if (warned)
                    {
                        Notifications.Show(NotificationType.Info, "reallife_pd_starve_over", null, 10f);
                    }
                }
                return;
            }
            PermadeathState.Empty(now);
            int hour = (int)Math.Floor(now / 60f);
            if (hour == _starveHour)
            {
                return;
            }
            int passed = _starveHour < 0 ? 1 : Mathf.Clamp(hour - _starveHour, 1, StarveCatchUpMost);
            _starveHour = hour;
            Moment m = PermadeathGame.Now(_ending || _broken);
            bool safe = PermadeathGame.SafeToDie();
            for (int i = passed - 1; i >= 0; i--)
            {
                float hours = (now - PermadeathState.EmptySince) / 60f - i;
                float roll = Random.value;
                Verdict v = PermadeathRules.StarveHour(hours, PermadeathState.StarveTold, m, safe, roll, out int warn);
                if (warn > 0)
                {
                    PermadeathState.StarveTold = warn;
                    PermadeathState.Warn(DeathCause.Starvation);
                    Notifications.Show(NotificationType.Error, StarveKeys[warn - 1], null, 16f);
                    Logger?.Info("Permadeath: " + hours.ToString("F0") + " hours on an empty stomach: warning " + warn + " of 3.");
                    break;
                }
                if (v == Verdict.Death)
                {
                    Logger?.Info("Permadeath check (going without food, " + hours.ToString("F0") + " hours at the bottom of the bar, all three warnings shown): roll "
                                 + roll.ToString("F3", CultureInfo.InvariantCulture)
                                 + " under " + PermadeathRules.StarvePerHour.ToString("F4", CultureInfo.InvariantCulture) + ": the end.");
                    PermadeathGame.Weak(0);
                    Die(DeathCause.Starvation);
                    return;
                }
            }
            if (!safe && PermadeathRules.Allowed(m) && PermadeathState.StarveTold >= PermadeathRules.IllStages - 1
                && PermadeathRules.StarveStage((now - PermadeathState.EmptySince) / 60f) >= PermadeathRules.IllStages)
            {
                Logger?.Info("Permadeath: going without food, an hour of the fatal stretch while the game was busy "
                             + "(asleep, a dialog, a ride, in hospital): not judged.");
            }
            PermadeathGame.Weak(PermadeathRules.Allowed(m) ? PermadeathState.StarveTold : 0);
        }

        /// <summary>For the test button: the bar at its bottom as if for so many hours, no warnings shown yet.</summary>
        internal void StarvedFor(float hours)
        {
            PermadeathGame.Starve();
            PermadeathState.Fed();
            PermadeathState.Empty(PermadeathGame.NowMinutes - hours * 60f);
            _lastHunger = 0f;
            _starveHour = -1;
        }

        private static Dictionary<string, string> Data(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            return save.modData;
        }

        // A save read for the first time this session: its state, what a switch moved in the main menu does to it, and
        // whether its character has died in another save since.
        private void FirstLook(GameInstance save)
        {
            _save = save;
            _day = -1;
            _ending = false;
            _elsewhere = false;
            _lastHunger = -1f;
            _starveHour = -1;
            PermadeathGame.ClearWeak();
            PermadeathState.Read(Data(save));
            bool armNext = PermadeathState.ArmNext;
            PermadeathState.ArmNext = false;   // taken by this save, whatever it is
            if (PermadeathState.Fresh)
            {
                PermadeathState.Armed = PermadeathRules.FirstArmed(true, PermadeathGame.NewGame, armNext);
                if (PermadeathState.Armed)
                {
                    PermadeathState.ArmedDay = PermadeathGame.Day;
                    Notifications.Show(NotificationType.Warning, "reallife_pd_on_new", null, 14f);
                }
            }
            if (!PermadeathState.Dead && PermadeathState.Armed && PermadeathGame.Memorial.Find(PermadeathGame.CharacterId, _story))
            {
                _elsewhere = true;
            }
            Sync(false);
            Logger?.Info("Permadeath: save read; "
                         + (PermadeathState.Dead ? "the character has died (" + PermadeathState.Record.Cause + ", day " + PermadeathState.Record.Day + "): the save is read, not played"
                         : _elsewhere ? "an older save of a character who has died since: it is read, not played"
                         : PermadeathState.Armed ? "on since day " + PermadeathState.ArmedDay + ", near misses had: " + PermadeathState.Warned
                         : "off")
                         + (PermadeathState.Fresh ? " (first time with this feature" + (armNext ? ", switched on in the main menu" : string.Empty) + ")" : string.Empty) + ".");
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = PermadeathGame.Save;
                if (save != null && ReferenceEquals(save, _save) && PermadeathState.Loaded)
                {
                    PermadeathState.Write(Data(save));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Once a game day: the businesses the save runs are remembered, so one closed or sold still counts.
        private void Chronicle()
        {
            Action<List<string>, List<string>> look = PermadeathBridges.Businesses;
            if (look == null)
            {
                return;
            }
            _keys.Clear();
            _names.Clear();
            look(_keys, _names);
            for (int i = 0; i < _keys.Count; i++)
            {
                PermadeathState.See(_keys[i]);
            }
        }

        /// <summary>
        /// The mod's data was removed from the save: what is held is forgotten and read afresh, off, on the next second.
        /// A death is not forgotten: it is written back at once.
        /// </summary>
        public void Forget()
        {
            GameInstance save = PermadeathGame.Save;
            if (PermadeathState.Dead && save != null && ReferenceEquals(save, _save))
            {
                PermadeathState.Write(Data(save));
                return;
            }
            PermadeathState.Reset();
            _save = null;
        }

        /// <summary>The options switch was moved or drawn: it speaks for the save loaded, or in the main menu for the next game begun.</summary>
        public static void OptionChanged(bool on)
        {
            PermadeathDirector director = Instance;
            if (director == null || !ModState.Loaded || !PermadeathState.Loaded || SaveGameManager.Current == null)
            {
                PermadeathState.ArmNext = on;
                return;
            }
            director.Switched(on);
        }

        private void Switched(bool on)
        {
            bool shown = PermadeathState.Armed || PermadeathState.Dead;
            if (on == shown)
            {
                return;   // the page drawing what holds already
            }
            if (PermadeathState.Dead)
            {
                Notifications.Show(NotificationType.Warning, "reallife_pd_refused_dead", null, 10f);
                Sync(true);
                return;
            }
            if (on && GoingPublic.InSession)
            {
                Notifications.Show(NotificationType.Warning, "reallife_pd_refused_session", null, 10f);
                Sync(true);
                return;
            }
            if (!on)
            {
                PermadeathState.Armed = false;
                Notifications.Show(NotificationType.Info, "reallife_pd_off", null, 10f);
                Logger?.Info("Permadeath: switched off for this save on day " + PermadeathGame.Day + ".");
                return;
            }
            Ask();
        }

        // Switching on is asked first in the game's red question; the switch is moved back on a no.
        private void Ask()
        {
            if (_asking)
            {
                return;
            }
            _asking = true;
            GameConfirm.Show("reallife_pd_ask_header", "reallife_pd_ask_body", () =>
            {
                _asking = false;
                if (PermadeathRules.Switches(true, PermadeathState.Armed, PermadeathState.Dead, GoingPublic.InSession) && PermadeathState.Loaded)
                {
                    PermadeathState.Armed = true;
                    PermadeathState.ArmedDay = PermadeathGame.Day;
                    Notifications.Show(NotificationType.Warning, "reallife_pd_on", null, 14f);
                    Logger?.Info("Permadeath: switched on for this save on day " + PermadeathGame.Day + ".");
                }
                Sync(true);
            }, () =>
            {
                _asking = false;
                Sync(true);
            }, "reallife_pd_ask_yes", "reallife_pd_ask_no", allowConfirmationSkip: false, isNegative: true);
        }

        // The options page shows what holds for this save, drawn again when it did not.
        private void Sync(bool redraw)
        {
            bool changed = Settings.SetPermadeath(PermadeathState.Armed || PermadeathState.Dead);
            if (changed || redraw)
            {
                Meta.OptionsLayout.Refresh();
            }
        }

        // The options were read again (a preset, an import, a reset): the switch is still the save's.
        private void Resync()
        {
            if (PermadeathState.Loaded && ModState.Loaded && SaveGameManager.Current != null && ReferenceEquals(SaveGameManager.Current, _save))
            {
                Sync(false);
            }
        }

        internal Verdict Judge(DeathCause cause, float measure)
        {
            if (_broken || _ending)
            {
                return Verdict.None;
            }
            Moment m = PermadeathGame.Now(false);
            bool warned = PermadeathState.HasWarned(cause);
            float roll = Random.value;
            Verdict v = PermadeathRules.Judge(cause, measure, warned, m, roll);
            if (m.Armed && PermadeathRules.Fatal(cause, measure))
            {
                Logger?.Info("Permadeath check (" + cause + ", measure " + measure.ToString("F1", CultureInfo.InvariantCulture) + "): allowed " + PermadeathRules.Allowed(m)
                             + " (session " + m.InSession + ", mission or tutorial " + m.MissionOrTutorial
                             + ", busy " + m.Busy + "), warned " + warned + ", odds " + PermadeathRules.Odds(cause, measure).ToString("P0", CultureInfo.InvariantCulture) + ", roll "
                             + roll.ToString("F2", CultureInfo.InvariantCulture) + ": " + v + ".");
            }
            return v;
        }

        internal void NearMiss(DeathCause cause)
        {
            PermadeathState.Warn(cause);
            string key = NearMissKey(cause);
            if (key != null)
            {
                Notifications.Show(NotificationType.Warning, key, null, 16f);
            }
            Logger?.Info("Permadeath: a near miss (" + cause + "); the next one of its kind can be fatal.");
        }

        private static string NearMissKey(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Crash: return "reallife_pd_near_crash";
                case DeathCause.Struck: return "reallife_pd_near_struck";
                case DeathCause.Mugging: return "reallife_pd_near_mugging";
                case DeathCause.Assault: return "reallife_pd_near_assault";
                default: return null;
            }
        }

        internal bool Die(DeathCause cause)
        {
            if (_broken || _ending || !PermadeathState.Loaded || PermadeathState.Dead)
            {
                return false;
            }
            StartCoroutine(Guard.Routine(Dying(cause, true)));
            return true;
        }

        /// <summary>One hour of a flu, from HomeHealth.Hourly. True while it no longer clears up by itself.</summary>
        internal bool FluHour(bool flu, float began, bool treated)
        {
            if (!flu || treated)
            {
                if (PermadeathState.IllSince >= 0f)
                {
                    PermadeathState.NoFlu();
                }
                return false;
            }
            Moment m = PermadeathGame.Now(_ending || _broken);
            if (!PermadeathRules.Allowed(m))
            {
                return false;   // as without Permadeath: the flu takes its course
            }
            PermadeathState.Flu(began);
            float hours = (PermadeathGame.NowMinutes - began) / 60f;
            float roll = Random.value;
            Verdict v = PermadeathRules.IllHour(hours, PermadeathState.IllTold, m, roll, out int warn);
            if (warn > 0)
            {
                PermadeathState.IllTold = warn;
                PermadeathState.Warn(DeathCause.Illness);
                Notifications.Show(NotificationType.Error, IllKeys[warn - 1], null, 16f);
                Logger?.Info("Permadeath: the flu, " + hours.ToString("F0") + " hours untreated: warning " + warn + " of 3.");
            }
            if (v == Verdict.Death)
            {
                if (LifeEvents.IsSafeForEvent())
                {
                    Logger?.Info("Permadeath check (the flu, " + hours.ToString("F0") + " hours untreated, all three warnings shown): roll "
                                 + roll.ToString("F3", CultureInfo.InvariantCulture) + " under " + PermadeathRules.IllPerHour.ToString("F4", CultureInfo.InvariantCulture) + ": the end.");
                    Die(DeathCause.Illness);
                }
                else
                {
                    Logger?.Info("Permadeath: the flu's hour came out fatal while the game was busy (asleep, a dialog, a ride): not this hour.");
                }
            }
            return PermadeathRules.IllHeld(PermadeathRules.IllStage(hours));
        }

        /// <summary>True while a mugger's demand should say what refusing can cost.</summary>
        internal bool Warns => !_broken && !_ending && PermadeathRules.Allowed(PermadeathGame.Now(false));
    }
}
