using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Pressure on the businesses the player runs: rival moves, a protection racket, inspections,
    /// staff and supplier trouble, customer incidents, a tax audit, and opportunities. Each is a
    /// small story with choices (BusinessIssues), listed with its deadline on the Business desk
    /// (a phone contact), and each has a clear end.
    ///
    /// The game's own rivals are left to themselves: where one of its special rivals is already
    /// active in a neighbourhood (price cuts, competitor openings, poaching in bulk), no rival
    /// move of the mod's is started there, and the one poaching the mod does start is the game's
    /// own offer to a single employee.
    ///
    /// It also keeps the closures the utilities make (a business without power, water or a
    /// connection), each with its maker: those are kept and ended on their hour whether or not
    /// business pressure is on, its off switch ends only its own, and the utilities' end when the
    /// utilities are off.
    ///
    /// Cost: Update is two comparisons a frame. Once a second it looks at the clock and whether
    /// the utilities are on; everything else happens when the game's hour changes (a handful of
    /// list walks over at most two open issues, a few running effects, the closures and the
    /// player's own businesses). A visitor in the shop is ticked four times a second, only while
    /// there is one. A fault is logged once, everything the mod changed is put back, and the
    /// feature stays off for the session.
    /// </summary>
    public sealed class BusinessDirector : MonoBehaviour
    {
        private const float VisitTick = 0.25f;
        private const int DefaultDueHour = 18;

        public static IModLogger Logger;
        public static BusinessDirector Instance { get; private set; }

        /// <summary>What the morning's chance that something comes up is multiplied by (the city's economy: Wiring/CityLinks). One while it is not set.</summary>
        public static Func<float> RollScale;

        /// <summary>A kind's share of the deal, given its own (the city's economy makes rivals undercut more often in bad times). Its own while this is not set.</summary>
        public static Func<string, int, int> WeightOf;

        /// <summary>
        /// True while the utilities run (Wiring/UtilityLinks): switched off, or stopped by a fault, every business they
        /// closed is opened again, as their own off switch. While it is not set, their closures end on their hour.
        /// </summary>
        public static Func<bool> UtilitiesOn;

        private readonly BusinessVisit _visit = new BusinessVisit();
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(8);
        private readonly List<string> _opening = new List<string>(8);   // the businesses whose last closure of the mod's has just ended
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
            _onSave = OnSaveGame;   // one delegate for the session: hooking it again each second allocates nothing
            BusinessState.Reset();
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

        private void Update()
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
                        BusinessDesk.OpenInPerson(asking);   // the visitor is standing in front of them and wants an answer
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

        // ---------------------------------------------------------------- once a second

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
                _lastHour = hour;   // nothing is decided in the first hour of a session
                _lastDay = day;
                Logger?.Info("Business pressure: ready. " + BusinessState.Issues.Count + " open issue(s), " + BusinessState.Effects.Count + " footfall change(s), "
                             + BusinessState.Closures.Count + " closure(s), racket " + BusinessState.Racket + ", first seen on day " + BusinessState.FirstDay + ".");
            }

            if (Settings.Business && !BusinessDesk.Ready)
            {
                BusinessDesk.Setup();   // switched on after the save was loaded: the contact is added now (tried once)
            }

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave);   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            bool turned = hour != _lastHour || day != _lastDay, newDay = day != _lastDay;
            _lastHour = hour;
            _lastDay = day;

            // The closures are kept whether or not business pressure is on: the utilities' are made either way. Each ends
            // on its hour, and the utilities' all end once the utilities are off.
            if (turned)
            {
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
                    StandDown(true, false, false);   // the off switch: nothing of business pressure's own is left running on any business
                }
                return;
            }
            if (turned)
            {
                Hourly(day, hour, newDay);
            }
        }

        // The businesses listed as having no closure of the mod's left on them, opened again; the list emptied, whatever happens.
        private void ReopenListed()
        {
            try
            {
                for (int i = 0; i < _opening.Count; i++)
                {
                    BusinessGame.Reopen(BusinessGame.Held(_opening[i]));   // (Held, not Find: the utilities close a headquarters too, which takes no money)
                }
            }
            finally
            {
                _opening.Clear();
            }
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

        // A business the game gives no switch to open (a headquarters, a warehouse), found closed as the save is read
        // with no closure of the mod's behind it, is opened again: the game itself never closes one, and the player cannot
        // open it. A closure the utilities made on one used to be dropped unopened (its business was looked for among
        // those that take money), so a save may have some left so. One still without power, water or a connection is
        // closed again within the hour, and told why.
        private static void Unstick(GameInstance save)
        {
            List<BuildingRegistration> all = save.BuildingRegistrations;
            List<string> opened = null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !GoingPublic.Mine(reg) || !reg.HasEstablishedBusiness || !reg.HasValidAddress || !reg.temporarilyClosed || BusinessGame.HasSwitch(reg)
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

        // ---------------------------------------------------------------- once a game hour

        private void Hourly(int day, int hour, bool newDay)
        {
            // (The closures have ended on their hour already, in Slow: they are kept whether or not business pressure is on.)

            // Footfall changes: ended on their day; the rest put back on top of the game's own figure, which the game
            // recomputes at midnight and whenever marketing changes.
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
                    BusinessGame.Footfall(reg, 0, true);
                }
                else
                {
                    BusinessGame.Footfall(reg, effect.Percent, newDay);
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

            // A group switched off (or "easy business" switched on) takes its open issues with it, without their outcome.
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
                    // The business was sold or closed down: the issue went with it.
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

        /// <summary>
        /// For another feature (through Wiring): one kind of issue starts at a business now, if the rules that hold
        /// for every issue allow it there today: the feature and the kind's group on, "easy business" off for anything
        /// but an opportunity, past the grace days, under the most that may be open, the business and the kind rested,
        /// and whatever the story itself needs. False, with nothing changed, if any of them says no.
        /// </summary>
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
        /// For another feature (through Wiring): a business's promotion figure is written again as it should stand
        /// now: its own, an issue's change if one is running there, and whatever <see cref="BusinessGame.Ambient"/>
        /// adds. With <paramref name="redraw"/> the customers still to come today are drawn again, unless it is a new
        /// day and a change of this feature's own runs there: then this feature does exactly that itself within the
        /// second. Works whether or not business pressure is switched on. False where the game keeps no promotion
        /// figure for the business; otherwise the business's own figure and the one written.
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
                // Nothing half-started is left open.
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
                Visit(BusinessVisit.Kind.Collector, reg, issue);   // in person, if the player is standing in the shop
            }
            return true;
        }

        // ---------------------------------------------------------------- what issues ask of the director

        /// <summary>
        /// Sets a business's one footfall change: so many percent of customers for so many days (zero removes it).
        /// Kept inside the catalogue's bounds, remembered with its end day, and applied now.
        /// </summary>
        internal void Effect(BuildingRegistration reg, int percent, int days)
        {
            percent = days > 0 ? BusinessCatalogue.ClampFootfall(percent) : 0;
            BusinessState.SetEffect(BusinessGame.Place(reg), percent, TimeHelper.CurrentDay + days);
            BusinessGame.Footfall(reg, percent, true);
        }

        /// <summary>
        /// Keeps a business closed for at least a few hours with the game's own switch, for one maker, and remembers when
        /// to reopen it. An open business is closed. One the mod keeps closed already stays closed at least that long: the
        /// maker's closure on it is made to last until then if that is later (never shorter), so a closure asked for again
        /// each hour lasts while its cause does. True if the business is kept closed so. False if it is closed by someone
        /// else (the player's own switch), or if the reopening could not be kept: before the save's closures are read
        /// (reading them starts the list afresh) or after a fault (nothing reopens anything then).
        /// </summary>
        internal bool CloseFor(BuildingRegistration reg, int hours, ClosureSource source = ClosureSource.Issue)
        {
            hours = Mathf.Clamp(hours, 1, BusinessCatalogue.MostClosedHours);
            if (_broken || !BusinessState.Loaded || reg == null)
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

        /// <summary>
        /// One maker's closure on a business ended now, as what kept it closed is over (the utilities: the business has its
        /// power, water and connection again). It opens at once unless another closure of the mod's is left on it.
        /// </summary>
        internal void Release(BuildingRegistration reg, ClosureSource source)
        {
            if (_broken || !BusinessState.Loaded || reg == null || !BusinessState.Holds(source))
            {
                return;   // (asked each hour of every business that has what it needs: nothing is worked out while the maker holds none)
            }
            BusinessState.EndAt(BusinessGame.Place(reg), source, _opening);
            ReopenListed();
        }

        /// <summary>Says something about a business: a toast now, and a line in the Business desk's messages to read back.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data)
        {
            Notifications.Show(type, key, data, 13f);
            BusinessDesk.Text(key, data);
        }

        /// <summary>Somebody comes into the shop about it, if the player is standing in that shop. False if not (the issue goes on by phone).</summary>
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

        // Everything business pressure has running on any business is ended and put back: issues closed without their
        // outcome, footfall back to the game's own figure, its closures ended (a business opens again once no closure of
        // the mod's is left on it). The utilities' closures are theirs and stay, but for <everything>, after a fault: then
        // nothing would ever end them. The racket goes quiet (a racket ended for good stays ended unless a test asks
        // otherwise).
        private void StandDown(bool quiet, bool racketToo, bool everything)
        {
            int day = TimeHelper.CurrentDay;
            BusinessState.EndOf(ClosureSource.Issue, everything, _opening);
            try
            {
                for (int i = BusinessState.Effects.Count - 1; i >= 0; i--)
                {
                    BuildingRegistration reg = BusinessGame.Find(BusinessState.Effects[i].Place);
                    if (reg != null)
                    {
                        BusinessGame.Footfall(reg, 0, true);
                    }
                }
                ReopenListed();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
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
                Notifications.Show(NotificationType.Info, "reallife_biz_test_resolved", null, 8f);
            }
        }

        // ---------------------------------------------------------------- test buttons

        /// <summary>
        /// Options page: starts the next kind of issue of a group on the business the player is standing in, or
        /// their first one. The grace periods, rests, odds and the two-open limit are waived; what the story needs
        /// to exist at all is not, and the button says which.
        /// </summary>
        public void Test(PressureGroup group)
        {
            TestRefusal why = _broken || !BusinessState.Loaded ? TestRefusal.NotLoaded : !Settings.Business ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Business issue", why);
                return;
            }
            BuildingRegistration reg = BuildingManager.IsInsideBuilding ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            if (!BusinessGame.IsBusiness(reg))
            {
                BusinessGame.Businesses(_shops);
                reg = _shops.Count > 0 ? _shops[0] : null;
                _shops.Clear();
            }
            if (reg == null)
            {
                Logger?.Info("Business issue test: not started (the player runs no business that takes money).");
                Notifications.Show(NotificationType.Warning, "reallife_biz_test_no_business", null, 9f);
                return;
            }
            string place = BusinessGame.Place(reg);
            var data = new Dictionary<string, string>(2) { { "business", BusinessGame.Name(reg) } };
            if (BusinessState.IssueAt(place) != null)
            {
                Logger?.Info("Business issue test: not started (" + BusinessGame.Name(reg) + " already has an open issue).");
                Notifications.Show(NotificationType.Warning, "reallife_biz_test_busy", data, 9f);
                return;
            }
            // The next kind of this group that can start here; if none can, the reason of the one whose turn it was.
            IssueDef[] all = BusinessCatalogue.All;
            _deal.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Group == group)
                {
                    _deal.Add(all[i]);
                }
            }
            string firstWhy = null;
            IssueDef firstDef = null;
            int start = _testAt[(int)group]++;
            for (int k = 0; k < _deal.Count; k++)
            {
                IssueDef def = _deal[(start + k) % _deal.Count];
                string reason = BusinessState.IssueOf(def.Id) != null ? "reallife_biz_test_kind_open" : BusinessIssues.Why(def, reg, true);
                if (reason == null)
                {
                    if (Open(def, reg, TimeHelper.CurrentDay))
                    {
                        _deal.Clear();
                        return;
                    }
                    reason = "reallife_biz_test_failed";
                }
                if (firstWhy == null)
                {
                    firstWhy = reason;
                    firstDef = def;
                }
            }
            _deal.Clear();
            data["issue"] = firstDef != null ? JobText.T(firstDef.NameKey) : string.Empty;
            Logger?.Info("Business issue test: not started (" + (firstDef != null ? firstDef.Id : group.ToString()) + ": " + firstWhy + ").");
            Notifications.Show(NotificationType.Warning, firstWhy ?? "reallife_biz_test_failed", data, 10f);
        }

        /// <summary>Options page: every open issue closed, every footfall change and closure of business pressure's put back, the racket forgotten.</summary>
        public void TestResolveAll()
        {
            if (_broken || !BusinessState.Loaded)
            {
                TestRefusals.Tell(Logger, "Business resolve", TestRefusal.NotLoaded);
                return;
            }
            _visit.End();
            StandDown(false, true, false);
        }

        /// <summary>Options page: the Business desk, face to face, wherever the player is.</summary>
        public void TestDesk()
        {
            TestRefusal why = _broken || !BusinessState.Loaded ? TestRefusal.NotLoaded : !Settings.Business ? TestRefusal.FeatureOff
                : UiOpen() ? TestRefusal.SceneActive : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Business desk", why);
                return;
            }
            BusinessDesk.OpenInPerson(null);
        }
    }
}
