using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.Tech;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Education and careers, each part behind its own option in a partial file of this class. No Update: a one-second timer
    /// starts and stops the parts and keeps the game's hour, day and save handlers in place (the game empties its events on
    /// every load); the frame cost is LateUpdate's (.Prompt). A fault, or the guard switching education off (Core/Guard, which
    /// also disables the component), stops it for the session: a sitting needs it enabled, as ShiftSkip starts on its host.
    /// </summary>
    public sealed partial class CareersDirector : MonoBehaviour
    {
        public const string CampusAppId = "RealLifeCampus";

        public static IModLogger Logger;
        public static CareersDirector Instance { get; private set; }

        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private Func<bool> _vacanciesStep;               // the job board's renewal, a stage a frame (Core/DaySpread)
        private int _vacanciesDay, _vacanciesStage;
        private bool _vacanciesReferrals;                // whether the second stage drew by referrals, for the third
        private bool _broken;
        private ShiftSkip _skip;
        private Sitting _sitting;                            // the sitting handed out last, for the busy checks; what counts is the book
        private readonly SittingBook _book = new SittingBook();   // every sitting handed out and not yet settled, by its own record
        private SittingRecord _finishing;                    // the record whose activity the once-a-second look is finishing, for its self-check line
        private TechNews _news;
        private GameInstance _newsOf;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameUi.GameSprites.Want(CareerGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            GameUi.GameSprites.Want(CampusGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            CareersGame.Logger = Logger;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            _vacanciesStep = VacanciesStep;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            DaySpread.Shared.Drop(this);   // the board's renewal in hand is left: the game's current save may be another one by now
            try
            {
                ClassPromptGone();
                CloseSitting();
                StopWork(true);
                StopStudy(false, true);
                CampusGone();
                ComputerButtons.Remove();
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
                if (FrameGuard.Off)
                {
                    StandDown("its feature was switched off after its faults (Core/Guard)");
                    continue;
                }
                while (StartLine.Holds(StartSeats.Careers))
                {
                    yield return null;   // a save's first load waits for a frame of its own (Core/StartLine)
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

        private static readonly int HotTick = HotSpots.Slot("Careers: CareersDirector.Tick");
        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            if (Save == null || LoadScene.isLoading || !ModState.Loaded || Meta.SaveTools.Dormant)
            {
                return;
            }
            if (!ReferenceEquals(_newsOf, Save))
            {
                string line = null;
                _news = TechNews.Read(Save.modData != null && Save.modData.TryGetValue(CareersStore.NewsKey, out line) ? line : null);
                _newsOf = Save;
            }
            bool study = Settings.CareersStudy;
            if (study && !_studyOn)
            {
                StartStudy();
            }
            else if (!study && _studyOn)
            {
                StopStudy(true, false);
            }
            bool work = Settings.CareersJobs && _studyOn;
            if (work && !_workOn)
            {
                StartWork();
            }
            else if (!work && _workOn)
            {
                StopWork(false);
            }
            CampusTick();
            if (_workOn)
            {
                WorkDesks();
            }
            LookAtSittings();
            if (_skip != null && !_skip.Open)
            {
                _skip = null;   // its sitting, if any, is the book's: settled, or let go by the look above
            }
            if (_visit != null && !_visit.Open)
            {
                _visit = null;   // likewise an interview at a place, once over
            }
            if (!_studyOn)
            {
                ClassPromptGone();
                return;
            }
            ClassPrompt();
            if (!ComputerButtons.Installed)
            {
                ComputerButtons.Install();   // once a city; a flag test after
            }
            PersonaEducation.Keep();   // the mod's courses in the Persona app; a null test once it is in
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_campusApp == null && !_campusGaveUp)
            {
                InstallCampus();
            }
            if (_workOn && _careerApp == null && !_careerGaveUp)
            {
                InstallCareer();
            }
        }

        private static readonly int HotOnHour = HotSpots.Slot("Careers: CareersDirector.OnHour");
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (_broken || Save == null)
            {
                return;
            }
            try
            {
                DaySpread.Shared.Flush(this);   // the hour reads what the day made: the board is renewed first
                if (_studyOn)
                {
                    StudyHour(Save.Day, Save.Hour);
                }
                if (_workOn)
                {
                    WorkHour(Save.Day, Save.Hour);
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Careers: CareersDirector.OnDay");
        private static readonly int HotDayStudy = HotSpots.Slot("Careers: day, the course fees and classes");
        private static readonly int HotDayWork = HotSpots.Slot("Careers: day, the working life");
        private static readonly int HotDayApps = HotSpots.Slot("Careers: day, apps redrawn");
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (_broken || Save == null)
            {
                return;
            }
            try
            {
                DaySpread.Shared.Flush(this);   // a board still owed its renewal for the day before gets it first
                float started = Time.realtimeSinceStartup;
                if (_studyOn)
                {
                    using var study = HotSpots.Time(HotDayStudy);   // an instalment is taken from the player's money at midnight, as the game's own are
                    StudyDay(Save.Day);
                }
                if (_workOn)
                {
                    using var work = HotSpots.Time(HotDayWork);   // days settled, pay and benefits paid; the job board's renewal is queued (Core/DaySpread)
                    WorkDayRun(Save.Day);
                }
                using (HotSpots.Time(HotDayApps))
                {
                    Changed();
                }
                if (Verbose)
                {
                    Logger?.Info("Careers check (day " + Save.Day + "): " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private static readonly int HotSave = HotSpots.Slot("Careers: CareersDirector.OnSave");
        private void OnSave()
        {
            using var hotSpot = HotSpots.Time(HotSave);
            try
            {
                if (Save == null)
                {
                    return;
                }
                DaySpread.Shared.Flush(this);
                if (Save.modData == null)
                {
                    Save.modData = new Dictionary<string, string>();
                }
                WriteStudy(Save.modData);
                WriteCampus(Save.modData);
                if (_news != null && ReferenceEquals(_newsOf, Save))
                {
                    if (_news.Stories.Count > 0)
                    {
                        Save.modData[CareersStore.NewsKey] = _news.Write();
                    }
                    else
                    {
                        Save.modData.Remove(CareersStore.NewsKey);
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Offers a sitting on the game's activity panel; null if offered, else why not, as a locale key.</summary>
        internal string Offer(Sitting sitting)
        {
            if (_broken || !isActiveAndEnabled)
            {
                return "reallife_study_test_off";   // switched off: ShiftSkip would call a sitting off at Start, as it needs its host enabled
            }
            if (_skip != null && _skip.Open)
            {
                return "reallife_study_why_busy";
            }
            if (!CareersGame.Free())
            {
                return "reallife_study_why_game_busy";
            }
            if (CareersGame.Energy < StudyCatalogue.TiredEnergy)
            {
                return "reallife_study_why_tired";
            }
            CloseApps();
            ShiftSkip skip = ShiftSkip.Offer(this, sitting);
            if (skip == null)
            {
                return "reallife_study_why_game_busy";
            }
            _skip = skip;
            Hold(sitting, skip);
            return null;
        }

        /// <summary>A sitting handed to the activity that runs it on the game's panel: in the book until it is settled, and the director's current one.</summary>
        internal void Hold(Sitting sitting, PlayerActivity.IPlayerActivity holder)
        {
            if (sitting == null)
            {
                return;
            }
            if (sitting.Record.Day < 0 && Save != null)
            {
                sitting.Record.Day = Save.Day;
            }
            sitting.Record.Holder = holder;
            _book.Hold(sitting.Record);
            _sitting = sitting;
        }

        // A sitting out of the book, and no longer the director's current one or its in-place panel's.
        private void Release(Sitting sitting)
        {
            _book.Release(sitting.Record);
            if (ReferenceEquals(sitting, _sitting))
            {
                _sitting = null;
            }
            if (_skip != null && ReferenceEquals(sitting.Record.Holder, _skip))
            {
                _skip = null;
            }
        }

        /// <summary>
        /// Once a second: every open sitting by its own record. One whose activity the game's panel has let go is finished (its
        /// minutes so far counted, once, as its own Finish counts them), and one its activity leaves unsettled is let go with
        /// nothing counted, so that it never leaves every class and day "under way". A loop over the open sittings, rarely one.
        /// </summary>
        private void LookAtSittings()
        {
            for (int i = _book.Count - 1; i >= 0; i--)
            {
                if (i >= _book.Count)
                {
                    continue;   // a finish settled more than one
                }
                SittingRecord r = _book[i];
                PlayerActivity.IPlayerActivity holder = r.Holder as PlayerActivity.IPlayerActivity;
                SittingLook look = SittingRules.Look(r.Settled, holder != null && DeskGame.Shows(holder));
                if (look == SittingLook.Keep)
                {
                    continue;
                }
                if (look == SittingLook.Finish && holder != null)
                {
                    _finishing = r;
                    try
                    {
                        holder.Finish();   // the game's panel let it go: its minutes so far counted and the player stood up, by its own Finish
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e);
                    }
                    _finishing = null;
                }
                if (!r.Settled && r.Settle(SittingEnd.LetGo))
                {
                    if (r.Begun && r.Tiring)
                    {
                        CareersGame.Tiring(false);   // its energy spent no more
                    }
                    Logger?.Info("Careers: a " + r.Kind + " sitting of " + r.Of + " was left with no desk, computer or panel of its own and "
                                 + (r.Begun ? "its activity did not settle it" : "had not begun") + "; let go, nothing counted.");
                }
                _book.Release(r);
                if (_sitting != null && ReferenceEquals(_sitting.Record, r))
                {
                    _sitting = null;
                }
            }
        }

        /// <summary>A sitting is over: the minutes that passed are settled by its kind.</summary>
        internal void Ended(Sitting sitting, int minutes, bool whole)
        {
            // Settled by its own record (Sitting.SkipEnded lets this run once), whatever the director's current sitting is now.
            bool current = ReferenceEquals(sitting, _sitting);
            bool open = _book.Holds(sitting.Record);   // handed out and not let go: the book had it until now
            Release(sitting);
            if (_broken || !open)
            {
                return;   // let go by the director before its end (switched off, or the part stopping): nothing counted
            }
            try
            {
                switch (sitting.Kind)
                {
                    case SittingKind.Class:
                    case SittingKind.Homework:
                    case SittingKind.Exam:
                        if (_studyOn)
                        {
                            StudyEnded(sitting, minutes, whole, current);   // by its own record, whatever is current now
                        }
                        break;
                    case SittingKind.Work:
                    case SittingKind.Overtime:
                        if (_workOn)
                        {
                            WorkEnded(sitting, minutes);
                        }
                        break;
                    case SittingKind.Interview:
                        if (_workOn)
                        {
                            InterviewEnded(sitting, whole);
                        }
                        break;
                    default:
                        OtherEnded(sitting, minutes, whole);
                        break;
                }
                Changed();
            }
            catch (Exception e)
            {
                Fault("the end of a sitting", e);
            }
        }

        /// <summary>Charges a sitting's fee (a retake's, at a desk) as the player sits down for it.</summary>
        internal void ChargeAtStart(Sitting sitting, int fee)
        {
            Enrolment e = _studyOn && _book.Holds(sitting.Record) ? _study.Live(sitting.Of) : null;
            if (e == null || fee <= 0)
            {
                return;
            }
            CareersGame.Take(fee, CareersGame.FeeKey, CareersText.Course(sitting.Of));
            e.Fee += fee;
        }

        /// <summary>A sitting called off before it began (the walk to its desk or computer stopped): let go, with nothing counted.</summary>
        internal void Dropped(Sitting sitting)
        {
            bool open = _book.Holds(sitting.Record);
            Release(sitting);
            if (!open)
            {
                return;
            }
            Logger?.Info("Careers: a " + sitting.Kind + " sitting of " + sitting.Of + " was called off on the way to its desk or computer; nothing is counted.");
        }

        // The director lets every sitting go with nothing counted (switched off, a fault, the city going), and closes their activities.
        private void CloseSitting()
        {
            ShiftSkip skip = _skip;
            DeskWork desk = _deskWork;
            ComputerSitting computer = _computer;
            InterviewVisit visit = _visit;
            for (int i = _book.Count - 1; i >= 0; i--)
            {
                _book[i].Settle(SittingEnd.LetGo);   // a later end of its activity counts nothing
                _book.Release(_book[i]);
            }
            _skip = null;
            _sitting = null;
            _deskWork = null;
            _computer = null;
            _visit = null;
            skip?.Close();
            desk?.Close();
            computer?.Close();
            visit?.Close();   // out at the door and the black screen given back
            CareersGame.Tiring(false);
        }

        private static void CloseApps()
        {
            try
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                if (uis != null && uis.fullMenu != null && UI.Smartphone.FullMenu.IsOpen)
                {
                    uis.fullMenu.Toggle(false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A story for tomorrow's paper (the City Ledger prints what happened the day before).</summary>
        internal void Story(string key, string aName, string a, string bName = "", string b = "")
        {
            if (_news != null && Save != null)
            {
                _news.Add(Save.Day + 1, key, aName, a, bName, b);
            }
        }

        /// <summary>The stories for the paper of a day: the same however often it is asked.</summary>
        public void Stories(int day, List<TechStory> into)
        {
            if (_news != null && !_broken)
            {
                _news.For(day, into);
            }
        }

        private void Fault(string where, Exception e)
        {
            Logger?.Error(e);
            StandDown("an error in " + where);
        }

        // Off for the session, with nothing left running: no sitting is offered on a director that is not there for it.
        private void StandDown(string why)
        {
            _broken = true;
            DaySpread.Shared.Drop(this);
            try
            {
                CloseSitting();
                StopWork(false);
                StopStudy(false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Careers: switched off for this session after " + why + ". The game's own courses and jobs are not affected; the records are kept as they were last saved.");
        }

        /// <summary>A notification of the game's that opens the Campus app on a tab with a row picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, Guard.Wrap(() => OpenCampus(tab, pick)), true, false);
        }

        internal static Dictionary<string, string> Data(string a, string av, string b = null, string bv = null, string c = null, string cv = null)
        {
            var d = new Dictionary<string, string>(3) { { a, av ?? string.Empty } };
            if (b != null)
            {
                d[b] = bv ?? string.Empty;
            }
            if (c != null)
            {
                d[c] = cv ?? string.Empty;
            }
            return d;
        }

        /// <summary>Something changed: the open apps are written again.</summary>
        internal void Changed()
        {
            _campusView?.Changed();
            CampusBadge();
            _careerView?.Changed();
            CareerBadge();
        }

        /// <summary>For the save tools: every part stops (its state written to the save first); the timer starts each again if its option is on.</summary>
        public void StopAll()
        {
            try
            {
                CloseSitting();
                StopWork(false);
                StopStudy(true, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's lines have been removed, and so is what was read from them.</summary>
        public void Forget()
        {
            try
            {
                CloseSitting();
                StopWork(false);
                StopStudy(false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _study = null;
            LadderForget();
            _news = new TechNews();
        }
    }
}
