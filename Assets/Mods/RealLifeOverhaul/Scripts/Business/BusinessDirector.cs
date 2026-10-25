using System;
using System.Collections.Generic;
using BAModAPI;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Pressure on the player's businesses: rival moves, a protection racket, inspections, staff and supplier trouble,
    /// customers, a tax audit and opportunities, each a small story with choices (BusinessIssues) on the Business desk.
    /// No rival move starts where one of the game's own special rivals is active. It also keeps the utilities' closures,
    /// ended on their hour whether or not business pressure is on, and hosts the pace of every feature's customer redraws
    /// (CustomerDrawPace). Cost: two comparisons a frame; a clock check once a second; the real work once a game hour; a
    /// visitor ticked four times a second while there is one. A fault puts everything back and stops it for the session.
    /// </summary>
    public sealed partial class BusinessDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("business", "BusinessDirector", typeof(BusinessDirector));

        private const float VisitTick = 0.25f;   // seconds between a visitor's ticks
        private const int DefaultDueHour = 18;   // the game hour an issue is due on its last day

        public static IModLogger Logger;
        public static BusinessDirector Instance { get; private set; }

        /// <summary>What the morning's chance of an issue is multiplied by (the city's economy); one while unset.</summary>
        public static Func<float> RollScale;

        /// <summary>A kind's share of the deal, given its own (rivals undercut more in bad times); its own while unset.</summary>
        public static Func<string, int, int> WeightOf;

        /// <summary>True while the utilities run: once false, every business they closed opens again. Unset, their closures end on their hour.</summary>
        public static Func<bool> UtilitiesOn;

        /// <summary>The bribe an official asks for a kind, place, fee and the issue's seed (so always the same answer); 0: nobody asks.</summary>
        public static Func<string, string, int, int, int> BribeAsk;

        /// <summary>A bribe was paid: the kind, the place and the amount.</summary>
        public static Action<string, string, int> Bribed;

        /// <summary>True while a business falls short of its district's standard, so a complaint is due whatever the figures say.</summary>
        public static Func<BuildingRegistration, bool> Shabby;

        /// <summary>The cleanliness a health inspector judges, from the game's figure (a worn place looks dirtier); the figure while unset.</summary>
        public static Func<BuildingRegistration, int, int> Inspected;

        /// <summary>True while another feature has an offer or a departure in hand for an employee, so the poaching goes elsewhere.</summary>
        public static Func<string, bool> Held;

        /// <summary><see cref="Held"/> asked: false with no answer, or if the other feature faults.</summary>
        public static bool HeldElsewhere(string id)
        {
            try
            {
                return Held != null && !string.IsNullOrEmpty(id) && Held(id);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private readonly BusinessVisit _visit = new BusinessVisit();
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private readonly List<string> _opening = new List<string>(8);   // whose last closure of the mod's has just ended
        private readonly List<string> _letGo = new List<string>(2);     // let go by the player, whose closures were just dropped
        private readonly List<IssueDef> _deal = new List<IssueDef>(BusinessCatalogue.All.Length);
        private readonly int[] _testAt = new int[8];
        private float _nextSlow;
        private float _nextVisit;
        private int _lastHour = -1;
        private int _lastDay = -1;
        private bool _broken;
        private Action _onSave;

        private void Awake()
        {
            Instance = this;
            BusinessGame.Logger = Logger;
            _onSave = OnSaveGame;
            BusinessState.Reset();
            gameObject.AddComponent<CustomerDrawPace>();
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            _visit.End();
            BusinessState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Business: BusinessDirector.Update");
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
            if (_broken)
            {
                return;
            }
            try
            {
                if (_visit.Active && Time.time >= _nextVisit)
                {
                    _nextVisit = Time.time + VisitTick;
                    _visit.Tick(Time.time);
                    BusinessIssue asking = _visit.Asking;
                    if (asking != null && !UiOpen())
                    {
                        _visit.Opened();
                        BusinessDesk.OpenInPerson(asking);
                    }
                }
                if (Time.unscaledTime >= _nextSlow)
                {
                    _nextSlow = Time.unscaledTime + 1f;
                    Slow();
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Business pressure: switched off for this session after the error above; every footfall change and closure of the mod's was put back.");
                _visit.End();
                StandDown(true, false, true);
            }
        }

        private static bool UiOpen()
        {
            return ThirdPersonCamera.IsUiOpen() || BusinessDesk.DialogOpen;
        }

        private void Slow()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || !ModState.Loaded || InstanceBehavior<UIs>.Instance == null)
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            if (!BusinessState.Loaded)
            {
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                BusinessState.Load(save.modData, day);
                BusinessDesk.Setup();
                Unstick(save);
                DropLetGo();   // a building given up while the mod was away
                _lastHour = hour;   // nothing is decided in a session's first hour
                _lastDay = day;
                Logger?.Info("Business pressure: ready. " + BusinessState.Issues.Count + " open issue(s), " + BusinessState.Effects.Count + " footfall change(s), "
                             + BusinessState.Closures.Count + " closure(s), racket " + BusinessState.Racket + ", first seen on day " + BusinessState.FirstDay + ".");
            }

            if (Settings.Business && !BusinessDesk.Ready)
            {
                BusinessDesk.Setup();   // switched on after the load
            }

            // The game clears its event hub on every load, so the hooks are kept attached.
            SaveHook.Keep(_onSave);
            CustomerDraw.Keep();

            bool turned = hour != _lastHour || day != _lastDay, newDay = day != _lastDay;
            _lastHour = hour;
            _lastDay = day;

            // Closures are kept whether or not business pressure is on: the utilities make theirs either way.
            if (turned)
            {
                DropLetGo();
                BusinessState.EndDue(day, hour, _opening);
                ReopenListed();
            }
            Func<bool> utilities = UtilitiesOn;
            if (utilities != null && !utilities() && BusinessState.Holds(ClosureSource.Utilities))
            {
                BusinessState.EndOf(ClosureSource.Utilities, false, _opening);
                ReopenListed();
                Logger?.Info("Business: the utilities are off, so every business they closed is open again.");
            }

            if (!Settings.Business)
            {
                if (BusinessState.Issues.Count > 0 || BusinessState.Effects.Count > 0 || BusinessState.Holds(ClosureSource.Issue)
                    || (BusinessState.Racket != RacketStage.None && BusinessState.Racket != RacketStage.Ended))
                {
                    _visit.End();
                    StandDown(true, false, false);
                }
                return;
            }
            if (turned)
            {
                CustomerDraw.Hold("business pressure's hour");
                try
                {
                    Hourly(day, hour, newDay);
                }
                finally
                {
                    CustomerDraw.Release();
                }
            }
        }

        // Opens the businesses with no closure of the mod's left; one let go since only has the mod's switch cleared.
        private void ReopenListed()
        {
            try
            {
                for (int i = 0; i < _opening.Count; i++)
                {
                    BuildingRegistration reg = BusinessGame.Held(_opening[i]);
                    if (reg != null)
                    {
                        BusinessGame.Reopen(reg);
                    }
                    else
                    {
                        BusinessGame.Freed(_opening[i]);
                    }
                }
            }
            finally
            {
                _opening.Clear();
            }
        }

        private static readonly Predicate<string> StillHeld = place => BusinessGame.Held(place) != null;

        // Drops each closure on a building the player no longer holds and clears the switch it set, so the mod neither
        // keeps it closed for the next tenant nor opens it as the player's.
        private void DropLetGo()
        {
            _letGo.Clear();
            BusinessState.DropWhere(StillHeld, _letGo);
            for (int i = 0; i < _letGo.Count; i++)
            {
                bool freed = BusinessGame.Freed(_letGo[i]);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Business check (a building let go): " + _letGo[i] + " is no longer the player's: the mod's closure on it dropped"
                                 + (freed ? " and the game's switch it set cleared" : string.Empty) + "; it is not closed or opened by the mod again: PASS");
                }
            }
            _letGo.Clear();
        }

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (BusinessState.Loaded && save != null)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    BusinessState.Prune(StillOwned);
                    BusinessState.Save(save.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static readonly Predicate<string> StillOwned = place => BusinessGame.Find(place) != null;

        // Opens a business with no switch of the game's (a headquarters, a warehouse) found closed at load with no closure
        // of the mod's behind it: the game never closes one and the player cannot open it, but older saves can hold one.
        // One still without its utilities is closed again within the hour.
        private static void Unstick(GameInstance save)
        {
            List<BuildingRegistration> all = save.BuildingRegistrations;
            List<string> opened = null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress || !reg.temporarilyClosed
                    || !GameRegistrations.Found(reg) || BusinessGame.HasSwitch(reg)
                    || BusinessState.ClosureAt(BusinessGame.Place(reg)) >= 0)
                {
                    continue;
                }
                BusinessGame.Reopen(reg);
                (opened ?? (opened = new List<string>(4))).Add(BusinessGame.Name(reg));
            }
            if (opened != null)
            {
                Logger?.Warn("Business pressure: opened again, each closed with nothing of the mod's keeping it so and no switch of the game's to open it: "
                             + string.Join(", ", opened) + ".");
            }
        }

        private void Hourly(int day, int hour, bool newDay)
        {
            // Footfall changes end on their day; the rest are put back over the figure the game recomputes at midnight.
            for (int i = BusinessState.Effects.Count - 1; i >= 0; i--)
            {
                FootfallEffect effect = BusinessState.Effects[i];
                BuildingRegistration reg = BusinessGame.Find(effect.Place);
                if (reg == null)
                {
                    BusinessState.Effects.RemoveAt(i);
                }
                else if (day >= effect.UntilDay)
                {
                    BusinessState.Effects.RemoveAt(i);
                    if (BusinessGame.Footfall(reg, 0, true))
                    {
                        BusinessGame.Said(reg, 0);
                    }
                }
                else if (BusinessGame.Footfall(reg, effect.Percent, newDay) && newDay)
                {
                    BusinessGame.Said(reg, effect.Percent);
                }
            }

            if (newDay)
            {
                BusinessGame.Businesses(_shops);
                for (int i = 0; i < _shops.Count; i++)
                {
                    BusinessState.See(BusinessGame.Place(_shops[i]), day);
                }
                _shops.Clear();
            }

            // A group switched off, or "easy business" on, takes its open issues with it, without their outcome.
            for (int i = BusinessState.Issues.Count - 1; i >= 0; i--)
            {
                BusinessIssue open = BusinessState.Issues[i];
                IssueDef def = open.Def;
                if (def != null && BusinessIssues.GroupOn(def.Group) && (def.Positive || !Settings.BusinessEasy))
                {
                    continue;
                }
                BuildingRegistration at = BusinessGame.Find(open.Place);
                if (at != null && BusinessState.EffectAt(open.Place) >= 0)
                {
                    Effect(at, 0, 0);
                }
                BusinessState.Close(open, day);
                Logger?.Info("Business: '" + open.Kind + "' closed without its outcome: its kind is switched off.");
            }
            if ((!Settings.BusinessRacket || Settings.BusinessEasy) && BusinessState.Racket != RacketStage.None && BusinessState.Racket != RacketStage.Ended)
            {
                BusinessState.Racket = RacketStage.None;
                BusinessState.RacketRestUntil = day + BusinessCatalogue.RacketRestDays;
                Logger?.Info("Business: the racket is switched off: nothing more is collected or done.");
            }

            for (int i = BusinessState.Issues.Count - 1; i >= 0; i--)
            {
                BusinessIssue issue = BusinessState.Issues[i];
                BuildingRegistration reg = BusinessGame.Find(issue.Place);
                if (reg == null)
                {
                    if (issue.Kind == BusinessCatalogue.Racket)
                    {
                        BusinessState.Racket = RacketStage.None;
                    }
                    BusinessState.Close(issue, day);
                    Logger?.Info("Business: '" + issue.Kind + "' dropped: the business is no longer the player's.");
                    continue;
                }
                BusinessIssues.Watch(issue, reg);
                if (BusinessState.Issues.Contains(issue) && (day > issue.DueDay || (day == issue.DueDay && hour >= issue.DueHour)))
                {
                    BusinessIssues.Due(issue, reg);
                }
            }

            BusinessIssues.RacketTick(day, hour);

            if (hour >= BusinessCatalogue.RollHour && BusinessState.LastRollDay != day)
            {
                BusinessState.LastRollDay = day;
                Roll(day);
            }
        }

        // One roll a morning: whether anything comes up, then where, then what.
        private void Roll(int day)
        {
            if (!BusinessState.PastGrace(day) || BusinessState.Issues.Count >= BusinessCatalogue.MostOpen
                || Random.value > BusinessCatalogue.RollChance(Settings.BusinessFrequency) * (RollScale != null ? Mathf.Clamp(RollScale(), 0.5f, 2f) : 1f))
            {
                return;
            }
            BusinessGame.Businesses(_shops);
            for (int i = _shops.Count - 1; i >= 0; i--)
            {
                if (!BusinessState.Eligible(BusinessGame.Place(_shops[i]), day))
                {
                    _shops.RemoveAt(i);
                }
            }
            if (_shops.Count == 0)
            {
                return;
            }
            BuildingRegistration reg = _shops[Random.Range(0, _shops.Count)];
            _shops.Clear();

            _deal.Clear();
            int total = 0;
            IssueDef[] all = BusinessCatalogue.All;
            for (int i = 0; i < all.Length; i++)
            {
                IssueDef def = all[i];
                if ((Settings.BusinessEasy && !def.Positive) || !BusinessIssues.GroupOn(def.Group) || !BusinessState.KindRested(def, day)
                    || BusinessIssues.Why(def, reg, false) != null)
                {
                    continue;
                }
                _deal.Add(def);
                total += Weigh(def);
            }
            if (total <= 0)
            {
                return;
            }
            int draw = Random.Range(0, total);
            for (int i = 0; i < _deal.Count; i++)
            {
                draw -= Weigh(_deal[i]);
                if (draw < 0)
                {
                    Open(_deal[i], reg, day);
                    break;
                }
            }
            _deal.Clear();
        }

        private static int Weigh(IssueDef def)
        {
            return WeightOf != null ? Mathf.Clamp(WeightOf(def.Id, def.Weight), 0, def.Weight * 4) : def.Weight;
        }

        /// <summary>For another feature: starts one kind of issue at a business now if every issue's rules allow it; false, with nothing changed, if not.</summary>
        public bool StartIssue(string kind, BuildingRegistration reg)
        {
            try
            {
                IssueDef def = BusinessCatalogue.Find(kind);
                int day = TimeHelper.CurrentDay;
                if (def == null || _broken || !BusinessState.Loaded || !Settings.Business || !BusinessGame.IsBusiness(reg) || !BusinessIssues.GroupOn(def.Group)
                    || (Settings.BusinessEasy && !def.Positive) || !BusinessState.PastGrace(day) || BusinessState.Issues.Count >= BusinessCatalogue.MostOpen
                    || !BusinessState.Eligible(BusinessGame.Place(reg), day) || !BusinessState.KindRested(def, day) || BusinessIssues.Why(def, reg, false) != null)
                {
                    return false;
                }
                return Open(def, reg, day);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        /// <summary>
        /// For another feature: a business's promotion figure written again as it should stand now, on or off; false where
        /// the game keeps none. On a new day with a change of its own there, this feature redraws itself.
        /// </summary>
        public static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
        {
            own = written = 0;
            if (reg == null)
            {
                return false;
            }
            int at = BusinessState.Loaded ? BusinessState.EffectAt(BusinessGame.Place(reg)) : -1;
            bool mine = at >= 0 && newDay && Settings.Business && Instance != null && !Instance._broken;
            if (!BusinessGame.Footfall(reg, at >= 0 ? BusinessState.Effects[at].Percent : 0, redraw && !mine))
            {
                return false;
            }
            own = BusinessGame.LastOwn;
            written = BusinessGame.LastWritten;
            return true;
        }

        private bool Open(IssueDef def, BuildingRegistration reg, int day)
        {
            BusinessIssue issue = BusinessState.Open(def.Id, BusinessGame.Place(reg), day, day + def.DecideDays, DefaultDueHour);
            try
            {
                BusinessIssues.Start(issue, reg);
            }
            catch (Exception e)
            {
                // nothing half-started is left open
                Logger?.Error(e);
                if (def.Id == BusinessCatalogue.Racket)
                {
                    BusinessState.Racket = RacketStage.None;
                }
                BusinessState.Issues.Remove(issue);
                return false;
            }
            Logger?.Info("Business: '" + def.Id + "' opened at " + BusinessGame.Name(reg) + ", to be decided by day " + issue.DueDay + " " + issue.DueHour + ":00.");
            if (def.Id == BusinessCatalogue.Racket)
            {
                Visit(BusinessVisit.Kind.Collector, reg, issue);   // in person, if the player is in the shop
            }
            return true;
        }

        /// <summary>Sets a business's one footfall change, so many percent for so many days (zero days removes it), and applies it now.</summary>
        internal void Effect(BuildingRegistration reg, int percent, int days)
        {
            percent = days > 0 ? BusinessCatalogue.ClampFootfall(percent) : 0;
            BusinessState.SetEffect(BusinessGame.Place(reg), percent, TimeHelper.CurrentDay + days);
            if (BusinessGame.Footfall(reg, percent, true))
            {
                BusinessGame.Said(reg, percent);
            }
        }

        /// <summary>
        /// Keeps a business closed by the game's switch for at least so many hours, for one maker, never shorter if asked again.
        /// False if someone else closed it, before the save is read, or after a fault: nothing would reopen it.
        /// </summary>
        internal bool CloseFor(BuildingRegistration reg, int hours, ClosureSource source = ClosureSource.Issue)
        {
            hours = Mathf.Clamp(hours, 1, BusinessCatalogue.MostClosedHours);
            if (_broken || !BusinessState.Loaded || reg == null || !GoingPublic.Mine(reg))
            {
                return false;
            }
            string place = BusinessGame.Place(reg);
            int hour = TimeHelper.CurrentHour + hours, day = TimeHelper.CurrentDay + hour / 24;
            if (reg.temporarilyClosed)
            {
                if (BusinessState.ClosureAt(place) < 0)
                {
                    return false;   // not the mod's to keep closed or to reopen
                }
                BusinessState.Hold(place, source, day, hour % 24);
                return true;
            }
            if (!BusinessGame.Close(reg))
            {
                return false;
            }
            BusinessState.SetClosure(place, source, day, hour % 24);
            return true;
        }

        /// <summary>Ends one maker's closure on a business now; it opens unless another closure of the mod's is left on it.</summary>
        internal void Release(BuildingRegistration reg, ClosureSource source)
        {
            if (_broken || !BusinessState.Loaded || reg == null || !BusinessState.Holds(source))
            {
                return;   // asked hourly of every business: cheap while the maker holds none
            }
            BusinessState.EndAt(BusinessGame.Place(reg), source, _opening);
            ReopenListed();
        }

        /// <summary>A notice now, and a line in the Business desk's messages.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 13f);
            BusinessDesk.Text(key, data);
        }

        /// <summary>Somebody comes into the shop about it if the player is there; false if not (it goes on by phone).</summary>
        internal bool Visit(BusinessVisit.Kind kind, BuildingRegistration reg, BusinessIssue issue)
        {
            try
            {
                return !_visit.Active && BusinessGame.PlayerInside(reg) && _visit.Begin(kind, issue, Time.time);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _visit.End();
                return false;
            }
        }

        // Ends and puts back everything business pressure runs: issues without their outcome, footfall, its closures. The
        // utilities' closures stay unless <everything> (after a fault nothing would end them). A racket ended for good stays
        // ended unless <racketToo>.
        private void StandDown(bool quiet, bool racketToo, bool everything)
        {
            int day = TimeHelper.CurrentDay;
            BusinessState.EndOf(ClosureSource.Issue, everything, _opening);
            CustomerDraw.Hold("business pressure stood down");
            try
            {
                for (int i = BusinessState.Effects.Count - 1; i >= 0; i--)
                {
                    BuildingRegistration reg = BusinessGame.Find(BusinessState.Effects[i].Place);
                    if (reg != null && BusinessGame.Footfall(reg, 0, true))
                    {
                        BusinessGame.Said(reg, 0);
                    }
                }
                ReopenListed();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                CustomerDraw.Release();
            }
            BusinessState.Effects.Clear();
            for (int i = BusinessState.Issues.Count - 1; i >= 0; i--)
            {
                BusinessState.Close(BusinessState.Issues[i], day);
            }
            if (BusinessState.Racket != RacketStage.Ended || racketToo)
            {
                BusinessState.Racket = RacketStage.None;
                BusinessState.RacketRestUntil = racketToo ? 0 : day + BusinessCatalogue.RacketRestDays;
            }
            Logger?.Info("Business pressure: stood down; nothing of " + (everything ? "the mod's is running on any business." : "its own is running on any business (the utilities' closures stay)."));
            if (!quiet)
            {
                Notifications.Show(NotificationType.Info, "reallife_biz_test_resolved", new Dictionary<string, string>(1) { { "days", BusinessCatalogue.BusinessRestDays.ToString() } }, 12f);
            }
        }
    }
}
