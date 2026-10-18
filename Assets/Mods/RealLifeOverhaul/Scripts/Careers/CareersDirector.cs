using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Education and careers, each part behind its own option in a partial file of this class. No Update, nothing per frame: a
    /// one-second timer starts and stops the parts and keeps the game's hour, day and save handlers in place (the game empties
    /// its events on every load). A fault switches it off for the session.
    /// </summary>
    public sealed partial class CareersDirector : MonoBehaviour
    {
        public const string CampusAppId = "RealLifeCampus";

        public static IModLogger Logger;
        public static CareersDirector Instance { get; private set; }

        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private bool _broken;
        private ShiftSkip _skip;
        private Sitting _sitting;
        private TechNews _news;
        private GameInstance _newsOf;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            CareersGame.Logger = Logger;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
            _onDay = Guard.Wrap(OnDay);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
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
            if (_skip != null && !_skip.Open)
            {
                _skip = null;
                _sitting = null;
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
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (_broken || Save == null)
            {
                return;
            }
            try
            {
                float started = Time.realtimeSinceStartup;
                if (_studyOn)
                {
                    StudyDay(Save.Day);
                }
                if (_workOn)
                {
                    WorkDayRun(Save.Day);
                }
                Changed();
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
            _sitting = sitting;
            return null;
        }

        /// <summary>A sitting is over: the minutes that passed are settled by its kind.</summary>
        internal void Ended(Sitting sitting, int minutes, bool whole)
        {
            if (!ReferenceEquals(sitting, _sitting))
            {
                return;
            }
            _skip = null;
            _sitting = null;
            try
            {
                switch (sitting.Kind)
                {
                    case SittingKind.Class:
                    case SittingKind.Homework:
                    case SittingKind.Exam:
                        if (_studyOn)
                        {
                            StudyEnded(sitting, minutes, whole);
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
            Enrolment e = _studyOn && ReferenceEquals(sitting, _sitting) ? _study.Live(sitting.Of) : null;
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
            if (!ReferenceEquals(sitting, _sitting))
            {
                return;
            }
            _sitting = null;
            Logger?.Info("Careers: a " + sitting.Kind + " sitting of " + sitting.Of + " was called off on the way to its desk or computer; nothing is counted.");
        }

        private void CloseSitting()
        {
            ShiftSkip skip = _skip;
            DeskWork desk = _deskWork;
            ComputerSitting computer = _computer;
            _skip = null;
            _sitting = null;
            _deskWork = null;
            _computer = null;
            skip?.Close();
            desk?.Close();
            computer?.Close();
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
            _broken = true;
            Logger?.Error(e);
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
            Logger?.Warn("Careers: switched off for this session after an error in " + where + ". The game's own courses and jobs are not affected; the records are kept as they were last saved.");
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
            _news = new TechNews();
        }
    }
}
