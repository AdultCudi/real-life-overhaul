using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>
    /// Permadeath: an ending the player can choose, off unless switched on, kept for each save. Once a second it looks at
    /// the save: a save read for the first time takes its state (Permadeath/PermadeathState), the options page is told
    /// what holds for it, the businesses it runs are remembered once a game day, and a save that has ended is not played:
    /// its obituary is shown and the game goes back to the main menu.
    ///
    ///   a dangerous moment   the feature where it happens asks <see cref="Mortality"/>, which judges it here
    ///                        (Permadeath/PermadeathRules): nothing, a near miss that says so, or a death
    ///   a death              the game's own "the player is dead" and walking blocked, the game's fade, the death written
    ///                        into the save and the game's own save made at once (quitting cannot undo it; the character
    ///                        is kept in the machine's memorial too, for older saves), the City Ledger's front page in
    ///                        memory (Wiring/PermadeathLinks), and the main menu when it is put down
    ///   an ended save        the same page, read from the save's record, and the main menu
    ///
    ///   going without food   with it on, the game's hunger bar is looked at once a second: from a day at its bottom
    ///                        a warning on the hour, three in all, with a low mood and then a faster drain of energy;
    ///                        anything eaten ends it; past the third a rare death, only at a moment the game can take it
    ///
    /// Per frame: one flag test, and while the obituary is up the cancel key (the game reads no keys of its own then).
    /// Once a second: two reference comparisons, a day comparison and the hunger bar; once a game hour while the bar is
    /// at its bottom, the rules of going without food; once a game day, one walk over the businesses.
    /// </summary>
    public sealed partial class PermadeathDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("permadeath", "PermadeathDirector", typeof(PermadeathDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;
        public static PermadeathDirector Instance { get; private set; }

        private static readonly string[] IllKeys = { "reallife_pd_ill_1", "reallife_pd_ill_2", "reallife_pd_ill_3" };
        private static readonly string[] StarveKeys = { "reallife_pd_starve_1", "reallife_pd_starve_2", "reallife_pd_starve_3" };
        private const int StarveCatchUpMost = 48;   // game hours caught up at most in one look (the game run on fast)

        private readonly LifeStory _story = new LifeStory();     // the life being printed
        private readonly List<string> _keys = new List<string>(8);
        private readonly List<string> _names = new List<string>(8);
        private GameInstance _save;
        private int _day = -1;
        private bool _ending;       // a death, a rehearsal or an ended save's obituary is under way
        private bool _elsewhere;    // this save is an older one of a character who has died since (the memorial says so)
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
                // What follows must be done whatever this did: on a game lacking something Hold names it failed, and the
                // switch below was left on for the next game begun (Core/GameMembers).
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
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
                    yield return null;   // its first load after a save loads waits for a frame of its own (Core/StartLine)
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    _broken = true;
                    Logger?.Error(e);
                    Logger?.Warn("Permadeath: switched off for this session after the error above. Nothing can end the character's life until the game is loaded again; the save's state is untouched.");
                }
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Permadeath: PermadeathDirector.Tick");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);   // the timer's tick (Core/HotSpots)
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

        // ---------------------------------------------------------------- going without food

        // Once a second: the bar read; something eaten ends the danger; at its bottom, once a game hour (the hours missed
        // caught up, one warning at most a look), the rules. Off, dead, or with the game's energy off, nothing is kept.
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
                    Logger?.Info("Permadeath check (going without food, " + hours.ToString("F0") + " hours at the bottom of the bar, all three warnings shown): roll " + roll.ToString("F3")
                                 + " under " + PermadeathRules.StarvePerHour.ToString("F4") + ": the end.");
                    PermadeathGame.Weak(0);
                    Die(DeathCause.Starvation);
                    return;
                }
            }
            if (!safe && PermadeathRules.Allowed(m) && PermadeathState.StarveTold >= PermadeathRules.IllStages - 1
                && PermadeathRules.StarveStage((now - PermadeathState.EmptySince) / 60f) >= PermadeathRules.IllStages)
            {
                Logger?.Info("Permadeath: going without food, an hour of the fatal stretch while the game was busy (asleep, a dialog, a ride, in hospital): not judged.");
            }
            PermadeathGame.Weak(PermadeathRules.Allowed(m) ? PermadeathState.StarveTold : 0);
        }

        /// <summary>For the test button: the bar at its bottom, as if for so many hours, the warnings not yet shown.</summary>
        internal void StarvedFor(float hours)
        {
            PermadeathGame.Starve();
            PermadeathState.Fed();
            PermadeathState.Empty(PermadeathGame.NowMinutes - hours * 60f);
            _lastHunger = 0f;
            _starveHour = -1;
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
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            PermadeathState.Read(save.modData);
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
                _elsewhere = true;   // an older save of a character who has died since, from a time it was on
            }
            Sync(false);
            Logger?.Info("Permadeath: save read; " + (PermadeathState.Dead ? "the character has died (" + PermadeathState.Record.Cause + ", day " + PermadeathState.Record.Day + "): the save is read, not played"
                         : _elsewhere ? "an older save of a character who has died since: it is read, not played"
                         : PermadeathState.Armed ? "on since day " + PermadeathState.ArmedDay + ", near misses had: " + PermadeathState.Warned
                         : "off") + (PermadeathState.Fresh ? " (first time with this feature" + (armNext ? ", switched on in the main menu" : string.Empty) + ")" : string.Empty) + ".");
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = PermadeathGame.Save;
                if (save != null && ReferenceEquals(save, _save) && PermadeathState.Loaded)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    PermadeathState.Write(save.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Once a game day: the businesses the save runs now are remembered, so that one closed or sold still counts.
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
        /// The save's data of the mod was removed (Meta's save tools): what is held of it is forgotten and read afresh on the
        /// next second, off. A death is the one thing not forgotten: it is written back at once, and the save stays ended.
        /// </summary>
        public void Forget()
        {
            GameInstance save = PermadeathGame.Save;
            if (PermadeathState.Dead && save != null && ReferenceEquals(save, _save))
            {
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                PermadeathState.Write(save.modData);
                return;
            }
            PermadeathState.Reset();
            _save = null;
        }

        // ---------------------------------------------------------------- the switch

        /// <summary>The options page's switch was moved (or the page was drawn, which says what it holds): it speaks for the save loaded, or in the main menu for the next game begun.</summary>
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
                return;   // the page drawing itself with what holds already
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

        // Switching it on is asked first, in the game's own red question: the switch is moved back if the answer is no.
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

        // The options page shows what holds for this save; drawn again when it did not.
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

        // ---------------------------------------------------------------- a dangerous moment (through Mortality)

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
                Logger?.Info("Permadeath check (" + cause + ", measure " + measure.ToString("F1") + "): allowed " + PermadeathRules.Allowed(m) + " (session " + m.InSession + ", mission or tutorial " + m.MissionOrTutorial
                             + ", busy " + m.Busy + "), warned " + warned + ", odds " + PermadeathRules.Odds(cause, measure).ToString("P0") + ", roll " + roll.ToString("F2") + ": " + v + ".");
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

        /// <summary>One hour of a flu, from Home's health (HomeHealth.Hourly). True while it no longer clears up by itself.</summary>
        internal bool FluHour(bool flu, float began, bool treated)
        {
            if (!flu || treated)
            {
                if (PermadeathState.IllSince >= 0f)
                {
                    PermadeathState.NoFlu();   // treated, or over: its warnings are over too
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
                    Logger?.Info("Permadeath check (the flu, " + hours.ToString("F0") + " hours untreated, all three warnings shown): roll " + roll.ToString("F3") + " under " + PermadeathRules.IllPerHour.ToString("F4") + ": the end.");
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

        // ---------------------------------------------------------------- the end

        // The life as it stands now, for the obituary: the game's figures, the street, the businesses, then the rest of the mod's records.
        private void Gather(LifeStory s, DeathCause cause)
        {
            s.Clear();
            PermadeathGame.Fill(s);
            s.Cause = cause;
            s.Place = cause == DeathCause.Illness || cause == DeathCause.Starvation || cause == DeathCause.Test || cause == DeathCause.None ? string.Empty : PermadeathGame.Street();
            _keys.Clear();
            _names.Clear();
            try
            {
                PermadeathBridges.Businesses?.Invoke(_keys, _names);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            for (int i = 0; i < _keys.Count; i++)
            {
                PermadeathState.See(_keys[i]);
            }
            for (int i = 0; i < _names.Count; i++)
            {
                LifeStory.Keep(s.Businesses, _names[i], LifeStory.BusinessesMost);
            }
            s.Owned = _keys.Count;
            s.Founded = Mathf.Max(s.Founded, Mathf.Max(PermadeathState.Seen.Count, s.Owned));
            try
            {
                PermadeathBridges.Story?.Invoke(s);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A death (real) or the options page's rehearsal (not real: nothing is saved and the character lives on).
        private IEnumerator Dying(DeathCause cause, bool real)
        {
            _ending = true;
            PermadeathGame.CloseQuestion();
            try
            {
                Gather(_story, cause);
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the page is written from what could be read
            }
            GameInstance save = PermadeathGame.Save;
            if (real)
            {
                // Written down before anything is shown: in the save's data, and on this machine for the older saves.
                PermadeathState.Dead = true;
                PermadeathState.Armed = true;
                PermadeathState.Record.CopyFrom(_story);
                PermadeathGame.Memorial.Keep(PermadeathGame.CharacterId, _story);
                if (save != null)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    PermadeathState.Write(save.modData);
                }
                Sync(false);
                Logger?.Info("Permadeath: " + (_story.Name.Length > 0 ? _story.Name : "the character") + " has died (" + cause + ") on day " + _story.Day + ", aged " + _story.Age + ".");
            }
            // Walking is held at once; the game's own "the player is dead" only once the screen is black, so that a fall
            // (Ragdoll/PlayerKnockdown) or a crash is seen into the fade rather than cut off at its first frame.
            PermadeathGame.Hold(true, false);
            yield return Safe(PermadeathGame.Fade(1.6f, "reallife_pd_blackout"));
            if (real)
            {
                PermadeathGame.Hold(true, true);
                yield return SaveIt();
            }
            yield return Safe(PermadeathGame.OutOfVehicle());
            yield return Print(_story, !real);
            if (real)
            {
                yield return Safe(PermadeathGame.Fade(0.6f, null));
                Logger?.Info("Permadeath: the obituary was put down; back to the main menu. The save's files are kept.");
                PermadeathGame.MainMenu();
                yield break;
            }
            PermadeathGame.Hold(false, false);
            Notifications.Show(NotificationType.Info, "reallife_pd_test_over", null, 10f);
            _ending = false;
        }

        // The game's own save, at once: tried again a few times if the game will not save this second.
        private IEnumerator SaveIt()
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                bool saved = false;
                try
                {
                    saved = PermadeathGame.SaveNow();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                if (saved)
                {
                    float until = Time.realtimeSinceStartup + 30f;
                    while (PermadeathGame.Saving && Time.realtimeSinceStartup < until)
                    {
                        yield return null;
                    }
                    Logger?.Info("Permadeath check (the death is saved): the game's save " + (PermadeathGame.Saving ? "is still being written after 30 s: FAIL" : "is on disk at attempt " + attempt + ": PASS") + ".");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            Logger?.Warn("Permadeath: the game would not save (five tries). The death is kept on this machine, so this character's saves from a time Permadeath was on are read as ended.");
        }

        // A save that has ended: its obituary, then the main menu. Nothing of it is played.
        private IEnumerator Ended()
        {
            _ending = true;
            PermadeathGame.CloseQuestion();
            PermadeathGame.Hold(true, true);
            LifeStory story = _story;
            if (!_elsewhere)
            {
                if (PermadeathState.Record.Name.Length > 0)
                {
                    story.CopyFrom(PermadeathState.Record);
                }
                else
                {
                    Gather(story, PermadeathState.Record.Cause);   // dead with no record that could be read: written from what the save says now
                }
            }
            Logger?.Info("Permadeath: this save has ended" + (_elsewhere ? " (the character died in another save since)" : string.Empty) + "; its obituary, then the main menu.");
            yield return Safe(PermadeathGame.Fade(0.5f, "reallife_pd_ended"));
            yield return Safe(PermadeathGame.OutOfVehicle());
            yield return Print(story, false);
            yield return Safe(PermadeathGame.Fade(0.6f, null));
            PermadeathGame.MainMenu();
        }

        // A step of the game's (a fade, leaving a car) that faults is logged and passed over: the ending goes on to its end
        // whatever happens, so that nobody is left behind a black screen.
        private static IEnumerator Safe(IEnumerator step)
        {
            while (true)
            {
                object current;
                try
                {
                    if (step == null || !step.MoveNext())
                    {
                        yield break;
                    }
                    current = step.Current;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    yield break;
                }
                yield return current;
            }
        }

        // The obituary on the City Ledger's page (or, if that cannot be had, as a message), until it is put down.
        private IEnumerator Print(LifeStory story, bool test)
        {
            ObituaryPage page = PermadeathText.Page(story, test);
            _closed = false;
            bool shown = false;
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until)
            {
                // The game's menu will not open over a question of its own (it closes the question instead): none is left up.
                PermadeathGame.CloseQuestion();
                bool asked = false;
                try
                {
                    asked = PermadeathBridges.ShowObituary != null && PermadeathBridges.ShowObituary(page, _onClosed);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                if (asked)
                {
                    float open = Time.realtimeSinceStartup + 2f;
                    while (!PermadeathGame.MenuOpen && Time.realtimeSinceStartup < open)
                    {
                        yield return null;
                    }
                    if (PermadeathGame.MenuOpen)
                    {
                        shown = true;
                        break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (!shown)
            {
                Logger?.Warn("Permadeath: the City Ledger's page could not be had; the obituary is shown as a message.");
                Meta.MetaUi.Message(page.Header, page.Plain(), new KeyValuePair<string, Action>("reallife_meta_ok", _onClosed));
            }
            else
            {
                Logger?.Info("Permadeath check (the obituary): on the City Ledger's page, " + page.Texts.Count + " sections, " + (page.BoxLines.Count > 0 ? page.BoxLines[0].Count : 0) + " figures: PASS.");
            }
            _pageUp = shown;
            if (UI.UiFader.isFading)
            {
                yield return Safe(PermadeathGame.UnFade(0.8f));
            }
            while (!_closed)
            {
                yield return null;
            }
            _pageUp = false;
        }

        private void PageClosed()
        {
            _closed = true;
        }
    }
}
