using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Education and careers: the courses of the city's schools and the player's own skills (Careers/Study), and each part
    /// that follows has its own option and its own partial file of this class.
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for the
    /// save, starts and stops each part with its option, puts the Campus app on the phone and keeps the handlers of the
    /// game's hour, day and save in place (the game empties its events on every load). The work is done on the game's hour
    /// (classes that are missed, reminders) and on its day (fees, exams that lapse, skills that fade). An hour or more of
    /// study is one of the game's own activities, left to its clock (<see cref="Sitting"/>).
    ///
    /// A fault switches education and careers off for the session with one line; the game's hour and day are left alone.
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
            StartCoroutine(Tick());
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

        // ---------------------------------------------------------------- the game's hour, day and save

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
                    Logger?.Info("Careers check (day " + Save.Day + "): " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        private void OnSave()
        {
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

        // ---------------------------------------------------------------- an hour or more left to the clock

        /// <summary>
        /// Offers a sitting on the game's activity panel. Null (having said why, as a key) if the game is busy or another
        /// is open.
        /// </summary>
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
                            WorkEnded(sitting, minutes, whole);
                        }
                        break;
                    case SittingKind.Interview:
                        if (_workOn)
                        {
                            InterviewEnded(sitting, minutes, whole);
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

        /// <summary>A sitting of a later part (contract work, tutoring, a ceremony): settled by that part.</summary>
        partial void OtherEnded(Sitting sitting, int minutes, bool whole);

        private void CloseSitting()
        {
            ShiftSkip skip = _skip;
            _skip = null;
            _sitting = null;
            skip?.Close();
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

        // ---------------------------------------------------------------- the morning paper

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

        // ---------------------------------------------------------------- faults and the player

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
            Notifications.Show(type, key, data, 10f, null, () => OpenCampus(tab, pick), true, false);
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

        // ---------------------------------------------------------------- for the save tools

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
