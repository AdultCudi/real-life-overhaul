using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The tech sector and the professions: the player's professional staff (roles, levels, skills, reviews; Tech/People).
    /// Each part has its own option and its own line of the save, and its own partial file of this class.
    ///
    /// This class keeps it going and nothing more. It has no Update. A one-second timer on the real clock waits for the
    /// save, starts and stops each part with its option, puts the apps on the phone and keeps the handlers of the game's
    /// hour, day and save in place (the game empties its events on every load). The work is done on the game's hour (a
    /// count of who worked it: one pass over the player's professionals) and on its day (after the game's daily run:
    /// everything else). With "Developer diagnostics" on each day's pass says what it did and how long it took.
    ///
    /// A fault switches the tech sector off for the session with one line; the game's hour and day are left alone.
    /// </summary>
    public sealed partial class TechDirector : MonoBehaviour
    {
        public const string RolesAppId = "RealLifeOpenRoles";

        public static IModLogger Logger;
        public static TechDirector Instance { get; private set; }

        private Action _onSave, _onHour, _onDay, _hourSeen, _daySeen;
        private bool _broken;
        private TechNews _news;
        private GameInstance _newsOf;   // the save the stories were read from

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;
        private static int Year => GameCalendar.DaysPerYear;

        private void Awake()
        {
            Instance = this;
            TalentGame.Logger = Logger;
            HiringGame.Logger = Logger;
            FirmsGame.Logger = Logger;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            try
            {
                StopVenture(false);
                StopFirms(false, true);
                StopHiring(false);
                StopPeople(false, true);
                _interview?.Dispose();
                _pitch?.Dispose();
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
                _news = TechNews.Read(Save.modData != null && Save.modData.TryGetValue(TechNews.Key, out line) ? line : null);
                _newsOf = Save;
            }
            bool people = Settings.TechPeople;
            if (people && !_peopleOn)
            {
                StartPeople();
            }
            else if (!people && _peopleOn)
            {
                StopPeople(true, false);
            }
            bool jobs = Settings.TechJobs && _peopleOn;
            if (jobs && !_hiringOn)
            {
                StartHiring();
            }
            else if (!jobs && _hiringOn)
            {
                StopHiring(true);
            }
            bool sector = Settings.TechSector && _peopleOn;
            if (sector && !_firmsOn)
            {
                StartFirms();
            }
            else if (!sector && _firmsOn)
            {
                StopFirms(true, false);
            }
            bool startups = Settings.TechStartups && _firmsOn;
            if (startups && !_ventureOn)
            {
                StartVenture();
            }
            else if (!startups && _ventureOn)
            {
                StopVenture(true);
            }
            _interview?.Watch();
            _pitch?.Watch();
            if (!_peopleOn)
            {
                return;
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
            if (_rolesApp == null && !_rolesGaveUp)
            {
                InstallRoles();
            }
            if (_firmsOn && _techApp == null && !_techGaveUp)
            {
                InstallTech();
            }
        }

        // ---------------------------------------------------------------- the game's hour, day and save

        private static readonly int HotOnHour = HotSpots.Slot("Tech: TechDirector.OnHour");
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (_broken || Save == null)
            {
                return;
            }
            try
            {
                if (_peopleOn)
                {
                    PeopleHour();
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Tech: TechDirector.OnDay");
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
                if (_hiringOn)
                {
                    HiringDay(Save.Day);
                }
                if (_peopleOn)
                {
                    PeopleDay(Save.Day);
                }
                if (_firmsOn)
                {
                    FirmsDay(Save.Day);
                    InfraDay(Save.Day);
                }
                if (_ventureOn)
                {
                    VentureDay(Save.Day);
                }
                Changed();
                if (Verbose)
                {
                    Logger?.Info("Tech check (day " + Save.Day + "): " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
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
                WritePeople(Save.modData);
                if (_hiringOn)
                {
                    WriteHiring(Save.modData);
                }
                if (_firmsOn)
                {
                    WriteFirms(Save.modData);
                    WriteInfra(Save.modData);
                }
                if (_ventureOn)
                {
                    WriteVenture(Save.modData);
                }
                if (_news != null && ReferenceEquals(_newsOf, Save))
                {
                    if (_news.Stories.Count > 0)
                    {
                        Save.modData[TechNews.Key] = _news.Write();
                    }
                    else
                    {
                        Save.modData.Remove(TechNews.Key);
                    }
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
                StopVenture(false);
                StopFirms(false, false);
                StopHiring(false);
                StopPeople(false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Tech: switched off for this session after an error in " + where + ". The game's own staff and businesses are not affected; the files are kept as they were last saved.");
        }

        /// <summary>A notification of the game's that opens the Open Roles app on a tab with a row picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, () => OpenRoles(tab, pick), true, false);
        }

        /// <summary>Something changed: the open apps are written again.</summary>
        internal void Changed()
        {
            _rolesView?.Changed();
            RolesBadge();
            ChangedTech();
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>For the save tools: every part stops (its state written to the save first); the timer starts each again if its option is on.</summary>
        public void StopAll()
        {
            try
            {
                StopVenture(true);
                StopFirms(true, false);
                StopHiring(true);
                StopPeople(true, false);
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
                StopVenture(false);
                StopFirms(false, false);
                StopHiring(false);
                StopPeople(false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _talent = null;
            _hiring = null;
            _firmsFile = null;
            _venture = null;
            _infra = null;
            _news = new TechNews();
        }
    }
}
