using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// Interiors and decor: once a game day it reads each of the player's businesses and works out by
    /// <see cref="InteriorRules"/> its scores, wear and district standard, and what they do to customers, prices,
    /// complaints, brands, Standing, staff and bids (through Wiring/InteriorLinks) and the news; each game hour the basket.
    /// Meaningful Interiors and Meaningful Ambience take over their parts. Only the player's own businesses, on this
    /// machine. Cost: one comparison a frame, a look at the clock once a second, one walk over the businesses a day and
    /// over the coming hour's customers an hour, footfall written in one batch. A fault puts everything back and stops it.
    /// </summary>
    public sealed partial class InteriorDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("interiors", "InteriorDirector", typeof(InteriorDirector));

        public static IModLogger Logger;
        public static InteriorDirector Instance { get; private set; }

        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(16);
        private readonly List<InteriorShop> _shops = new List<InteriorShop>(16);
        private readonly List<InteriorShop> _pool = new List<InteriorShop>(16);
        private readonly Dictionary<BuildingRegistration, InteriorShop> _byReg = new Dictionary<BuildingRegistration, InteriorShop>(16);
        private readonly Dictionary<string, InteriorShop> _byPlace = new Dictionary<string, InteriorShop>(16, StringComparer.Ordinal);   // the first shop at each place
        private readonly Dictionary<BuildingRegistration, BasketDue> _baskets = new Dictionary<BuildingRegistration, BasketDue>(8);       // baskets waiting for a redraw
        private readonly Dictionary<BuildingRegistration, int> _officesDue = new Dictionary<BuildingRegistration, int>(4);               // offices' clients waiting so, by hour due
        private readonly List<BuildingRegistration> _dueScratch = new List<BuildingRegistration>(8);
        private InteriorStore _store;
        private float _nextSlow;
        private int _lastDay = -1, _lastHour = -1;
        private long _switches = -1;
        private int _basketToday, _officeToday;
        private readonly Dictionary<string, int> _carry = new Dictionary<string, int>(StringComparer.Ordinal);       // an office's fraction of a client carried on, thousandths
        private readonly Dictionary<string, int> _booked = new Dictionary<string, int>(StringComparer.Ordinal);      // clients booked today, by office
        private readonly HashSet<CustomerEntry> _ours = new HashSet<CustomerEntry>();   // today's clients the feature booked
        private Action<BuildingRegistration> _onRedrawn;
        private bool _broken;
        private Action _onSave;

        /// <summary>True while the feature runs: switched on, a save read and no fault.</summary>
        public bool Running => !_broken && _store != null && Settings.Interiors;

        /// <summary>Today's businesses as the feature sees them; a shared list, not to be kept.</summary>
        public IReadOnlyList<InteriorShop> Shops => _shops;

        private void Awake()
        {
            Instance = this;
            InteriorGame.Logger = Logger;
            InteriorTab.Begin();
            _onSave = OnSaveGame;
            _onRedrawn = Redrawn;
            CustomerDraw.Redrawn += _onRedrawn;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            CustomerDraw.Redrawn -= _onRedrawn;
            InteriorTab.Remove();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Interiors: InteriorDirector.Update");
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
            if (_broken || Time.unscaledTime < _nextSlow)
            {
                return;
            }
            _nextSlow = Time.unscaledTime + 1f;
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Interiors: switched off for this session after the error above; every footfall change of the feature's was put back.");
                StandDown();
            }
        }

        private void Slow()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || !ModState.Loaded || InstanceBehavior<UIs>.Instance == null)
            {
                return;
            }
            if (_store == null)
            {
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                _store = InteriorStore.Load(save.modData);
                OtherMods.Look(Logger);
                Logger?.Info("Interiors: ready. " + _store.Files.Count + " business file(s)"
                             + (OtherMods.MeaningfulInteriors.Loaded ? "; Meaningful Interiors is on, so the district standard's part and the basket stay off" : string.Empty)
                             + (OtherMods.MeaningfulAmbience.Loaded ? "; Meaningful Ambience is on, so the decor's footfall stays off" : string.Empty)
                             + (_store.RulesFrom != InteriorFile.Unset
                                 ? "; the bounded rules first read this save on day " + _store.RulesFrom
                                 : "; the bounded rules meet this save today, and its businesses' effects come in over " + InteriorRules.RampDays + " days")
                             + "; strengths: standard " + Settings.InteriorsStandardStrength + "%, decor " + Settings.InteriorsDecorStrength + "%, prices " + Settings.InteriorsPricesStrength
                             + "%, wear " + Settings.InteriorsWearStrength + "%.");
            }
            SaveHook.Keep(_onSave);
            InteriorTab.Keep();

            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            long switches = Switches();
            if (!Settings.Interiors)
            {
                if (_shops.Count > 0)
                {
                    StandDown();
                }
                _switches = switches;
                _lastDay = day;
                return;
            }
            if (day != _lastDay)
            {
                bool first = _lastDay < 0;
                _lastDay = day;
                _lastHour = hour;
                _switches = switches;
                Round(day, !first);
                if (!first)
                {
                    Hourly();   // the new day's first customers, drawn with today's figures
                }
                return;
            }
            if (switches != _switches)
            {
                _switches = switches;
                Rework(day, false);   // a switch moved: today's figures again with the parts on now
            }
            if (hour != _lastHour)
            {
                _lastHour = hour;
                Hourly();
            }
            if (_baskets.Count > 0 || _officesDue.Count > 0)
            {
                Due(hour);
            }
        }

        // The options and the two other mods as one number, each slider 8 bits (0 to 200): any change changes what applies.
        private static long Switches()
        {
            long flags = (Settings.Interiors ? 1 : 0) | (Settings.InteriorsBasket ? 2 : 0) | (Settings.InteriorsStanding ? 4 : 0) | (Settings.InteriorsStaff ? 8 : 0)
                         | (OtherMods.MeaningfulInteriors.Loaded ? 16 : 0) | (OtherMods.MeaningfulAmbience.Loaded ? 32 : 0);
            return flags | ((long)Slider(Settings.InteriorsStandardStrength) << 6) | ((long)Slider(Settings.InteriorsDecorStrength) << 14)
                   | ((long)Slider(Settings.InteriorsPricesStrength) << 22) | ((long)Slider(Settings.InteriorsWearStrength) << 30);
        }

        private static int Slider(int percent)
        {
            return InteriorRules.Clamp(percent, InteriorRules.StrengthLeast, InteriorRules.StrengthMost);
        }

        /// <summary>The parts that apply now and their strengths, from the options and the two other mods.</summary>
        public static InteriorParts Parts => InteriorRules.Parts(Settings.InteriorsStandardStrength, Settings.InteriorsDecorStrength, Settings.InteriorsPricesStrength,
            Settings.InteriorsWearStrength, Settings.InteriorsBasket, Settings.InteriorsStanding, Settings.InteriorsStaff,
            OtherMods.MeaningfulInteriors.Loaded, OtherMods.MeaningfulAmbience.Loaded);

        /// <summary>The district's standard counts: its strength not off, and Meaningful Interiors not on.</summary>
        public static bool StandardOn => Parts.StandardScale > 0;
        public static bool DecorOn => Parts.DecorMille > 0;
        public static bool BasketOn => Parts.Basket;

        // The morning's round: every business read and aged, footfall written, perhaps a complaint and a piece of news.
        private void Round(int day, bool events)
        {
            if (_basketToday > 0 || _officeToday > 0)
            {
                Logger?.Info("Interiors: yesterday " + _basketToday + " product(s) more in customers' baskets, " + _officeToday + " client(s) more booked at offices.");
            }
            _basketToday = _officeToday = 0;
            _carry.Clear();
            _booked.Clear();
            _ours.Clear();   // yesterday's list of customers is gone
            _firstRound = !events;
            try
            {
                Rework(day, true);
            }
            finally
            {
                _firstRound = false;
            }
            if (!events)
            {
                return;   // nothing is decided in the first moments of a session
            }
            Complaints();
            News(day);
        }

        // Every business read and worked out; with 'age' its wear takes the days since last read.
        private void Rework(int day, bool age)
        {
            var before = new Dictionary<string, int>(_shops.Count, StringComparer.Ordinal);
            for (int i = 0; i < _shops.Count; i++)
            {
                before[_shops[i].Place] = _shops[i].Footfall;
            }
            _pool.AddRange(_shops);
            _shops.Clear();
            _byReg.Clear();
            _byPlace.Clear();
            InteriorGame.Places(_regs);   // kept after the round: _regs[i] is the business of _shops[i]
            InteriorParts parts = Parts;
            if (_store.RulesFrom == InteriorFile.Unset)
            {
                _store.RulesFrom = day;   // the rules meet the save today: trading businesses' effects come in from now
                Logger?.Info("Interiors: the bounded rules meet this save on day " + day + ": every business's effects on customers and takings come in over "
                             + InteriorRules.RampDays + " days, in full from day " + InteriorRules.FullFrom(day) + ".");
            }
            int bestOffice = -1;
            for (int i = 0; i < _regs.Count; i++)
            {
                BuildingRegistration reg = _regs[i];
                InteriorShop s = Take();
                InteriorGame.Read(reg, s);
                InteriorParts p = parts;
                InteriorFile f = _store.File(s.Place, true);
                int wear = 0;
                if (f != null)
                {
                    if (f.FirstDay == InteriorFile.Unset)
                    {
                        f.FirstDay = _firstRound ? InteriorFile.LongAgo : day;   // one there at the session's first round has had its days
                    }
                    s.Fitting = InteriorRules.Fitting(f.FirstDay, day);
                    s.StandardFrom = s.Fitting ? f.FirstDay + InteriorRules.GraceDays : 0;
                    if (age && p.WearScale > 0)
                    {
                        int days = f.LastDay > 0 ? InteriorRules.Clamp(day - f.LastDay, 0, 30) : 0;
                        bool cleaned = InteriorBridges.Cleaned != null && Ask(() => InteriorBridges.Cleaned(s.Place));
                        f.Wear = InteriorRules.Redecorated(f.Wear, f.LastInterior, s.Interior);
                        for (int d = 0; d < days; d++)
                        {
                            f.Wear = InteriorRules.Age(f.Wear, s.Clean, cleaned, p.WearScale);
                        }
                    }
                    if (age)
                    {
                        f.LastDay = day;
                        f.LastInterior = s.Interior;
                    }
                    wear = p.WearScale > 0 ? f.Wear : 0;
                }
                s.PhaseFrom = _store.PhaseFrom(f);
                s.Ramp = InteriorRules.Ramp(s.PhaseFrom, day);
                s.IssueDip = InteriorGame.IssueDip(reg);
                s.Stop = InteriorGame.Stop(reg);
                InteriorRules.Work(s, wear, p);
                if (s.Kind == InteriorKind.Office)
                {
                    bestOffice = Math.Max(bestOffice, s.Overall);
                }
                _shops.Add(s);
                _byReg[reg] = s;
                if (s.Place != null && !_byPlace.ContainsKey(s.Place))
                {
                    _byPlace.Add(s.Place, s);
                }
            }
            _bestOffice = bestOffice;
            int written = 0;
            CustomerDraw.Hold(_firstRound ? "the interiors' first round" : age ? "the interiors' day" : "the interiors' figures again");
            try
            {
                for (int i = 0; i < _regs.Count; i++)
                {
                    InteriorShop s = _byReg[_regs[i]];
                    int was;
                    before.TryGetValue(s.Place, out was);
                    // the session's first round writes every shop, putting right a figure an older version saved
                    if (s.Kind == InteriorKind.Shop && (_firstRound || s.Footfall != 0 || was != 0) && (age || s.Footfall != was) && Write(_regs[i], age))
                    {
                        written++;
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            Logger?.Info("Interiors: day " + day + ", " + _shops.Count + " business(es) read, " + written + " footfall figure(s) written; the best office scores " + Math.Max(0, bestOffice) + ".");
        }

        private int _bestOffice = -1;
        private bool _firstRound;   // a business without a file in the first round was trading before

        // A shop's basket of an hour waiting for a redraw: what it was due with.
        private struct BasketDue
        {
            public int Share, Hour;
            public float Soonest;
            public bool Office;
        }

        // The business of a shop in today's list if it still takes money, as BusinessGame.Find would give without a search.
        private BuildingRegistration Business(int i)
        {
            BuildingRegistration reg = i >= 0 && i < _regs.Count ? _regs[i] : null;
            return reg != null && BusinessGame.IsBusiness(reg) ? reg : null;
        }

        private InteriorShop Take()
        {
            if (_pool.Count == 0)
            {
                return new InteriorShop();
            }
            InteriorShop s = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            s.Fitting = false;
            s.StandardFrom = 0;
            s.Ramp = 1000;
            s.PhaseFrom = 0;
            s.IssueDip = 0;
            return s;
        }

        // A business's footfall written again and today's customers drawn again ('newDay': the morning's round, when
        // business pressure redraws one with its own change running itself).
        private static bool Write(BuildingRegistration reg, bool newDay)
        {
            try
            {
                int own, written;
                return InteriorBridges.Footfall != null && InteriorBridges.Footfall(reg, true, newDay, out own, out written);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        private static bool Ask(Func<bool> question)
        {
            try
            {
                return question();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // A business short of its district's standard may hear of it from a customer, one complaint a day at most.
        private void Complaints()
        {
            if (!StandardOn || InteriorBridges.Complain == null)
            {
                return;
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                InteriorShop s = _shops[i];
                if (s.ComplaintChance <= 0 || Random.Range(0, 1000) >= s.ComplaintChance)
                {
                    continue;
                }
                BuildingRegistration reg = Business(i);
                if (reg != null && Ask(() => InteriorBridges.Complain(reg)))
                {
                    Logger?.Info("Interiors: a complaint about " + s.Name + ": " + s.Overall + " against the district's standard of " + s.Standard + ".");
                    return;
                }
            }
        }

        // A striking or shabby interior in the news: one business a day at most, each once in its rest.
        private void News(int day)
        {
            if (!Settings.InteriorsStanding)
            {
                return;
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                InteriorShop s = _shops[i];
                InteriorFile f = _store.File(s.Place, false);
                int kind = s.Kind == InteriorKind.Shop ? InteriorRules.Newsworthy(s.Overall, s.Standard, s.Wear, StandardOn && !s.Fitting) : 0;
                if (f == null || kind == 0 || !InteriorRules.Story(f.StoryDay, day, Random.Range(0, 1000)))
                {
                    continue;
                }
                f.StoryDay = day;
                try
                {
                    InteriorBridges.Story?.Invoke(kind > 0 ? "reallife_news_story_interior_striking" : "reallife_news_story_interior_shabby");
                    if (kind > 0)
                    {
                        InteriorBridges.Striking?.Invoke(s.Place, s.Name);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                Logger?.Info("Interiors: " + s.Name + " is in the news for "
                             + (kind > 0 ? "a striking interior (" + s.Overall + ")." : "a shabby one (" + s.Overall + ", wear " + s.Wear / 10 + "%)."));
                return;
            }
        }

        // The businesses' footfall written again without the feature's part.
        private void StandDown()
        {
            var had = new List<BuildingRegistration>(_shops.Count);
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Footfall != 0)
                {
                    BuildingRegistration reg = Business(i);
                    if (reg != null)
                    {
                        had.Add(reg);
                    }
                }
            }
            _pool.AddRange(_shops);
            _shops.Clear();
            _byReg.Clear();
            _byPlace.Clear();
            _regs.Clear();
            _baskets.Clear();
            _officesDue.Clear();
            _bestOffice = -1;
            CustomerDraw.Hold("the interiors stood down");
            try
            {
                for (int i = 0; i < had.Count; i++)
                {
                    Write(had[i], false);   // FootfallOf answers nothing now
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            Logger?.Info("Interiors: stood down; " + had.Count + " footfall figure(s) written back without the feature's part.");
        }

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (_store != null && save != null)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    _store.Prune(place => BusinessGame.Held(place) != null);
                    save.modData[InteriorStore.Key] = _store.Write();
                    string rules = _store.WriteRules();
                    if (rules != null)
                    {
                        save.modData[InteriorStore.RulesKey] = rules;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The Guide's reset: every business's wear and news forgotten.</summary>
        public void Forget()
        {
            _store?.Clear();
            Logger?.Info("Interiors: every business file forgotten.");
        }

        /// <summary>A business's figures today, or null.</summary>
        public static InteriorShop At(BuildingRegistration reg)
        {
            InteriorDirector d = Instance;
            InteriorShop s;
            return d != null && d.Running && reg != null && d._byReg.TryGetValue(reg, out s) ? s : null;
        }

        /// <summary>A business's figures today by its place, or null; a dictionary look-up.</summary>
        public static InteriorShop At(string place)
        {
            InteriorDirector d = Instance;
            InteriorShop s;
            return d != null && d.Running && !string.IsNullOrEmpty(place) && d._byPlace.TryGetValue(place, out s) ? s : null;
        }

        /// <summary>The interior's part of a business's footfall, per cent, given way at once to a dip just started or ended there.</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            if (s == null)
            {
                return 0;
            }
            int dip = InteriorGame.IssueDip(reg);
            if (dip != s.IssueDip)
            {
                s.IssueDip = dip;
                InteriorRules.Combine(s);
            }
            return s.Footfall;
        }

        /// <summary>Thousandths more or less of the price the district bears at a business.</summary>
        public static int BearsOf(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            return s != null ? s.Bears : 0;
        }

        /// <summary>True while a business is short enough of its district's standard for a complaint about it.</summary>
        public static bool Shabby(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            return s != null && s.ComplaintChance > 0;
        }

        /// <summary>The cleanliness an inspector judges at a business: a worn place looks dirtier than it is.</summary>
        public static int Inspected(BuildingRegistration reg, int clean)
        {
            InteriorShop s = Parts.WearScale > 0 ? At(reg) : null;
            return s != null ? InteriorRules.InspectedClean(clean, s.Wear) : clean;
        }

        /// <summary>What the best office of the player's adds to the esteem candidates feel, 0 to 2.</summary>
        public static int OfficeEsteem()
        {
            InteriorDirector d = Instance;
            return d != null && d.Running && Settings.InteriorsStaff ? InteriorRules.OfficeEsteem(d._bestOffice) : 0;
        }

        /// <summary>Refreshes a business's finishes at today's prices, its wear back to nothing; true if done.</summary>
        public bool Refresh(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            InteriorFile f = s != null ? _store.File(s.Place, true) : null;
            if (f == null || Parts.WearScale <= 0)
            {
                return false;
            }
            int cost = InteriorGame.RefreshCost(s.Sqm);
            string name = s.Name;   // Rework below sends this shop's object back to the pool
            var data = new Dictionary<string, string>(2) { { "business", name }, { "amount", Economy.FinanceText.Money(cost) } };
            if (!InteriorGame.Charge(reg, cost))
            {
                Notifications.Show(NotificationType.Warning, "reallife_interior_refresh_cash", data, 8f);
                return false;
            }
            int was = f.Wear;
            f.Wear = 0;
            f.RefreshDay = TimeHelper.CurrentDay;
            Rework(TimeHelper.CurrentDay, false);
            Notifications.Show(NotificationType.Success, "reallife_interior_refresh_done", data, 8f);
            Logger?.Info("Interiors: finishes refreshed at " + name + " for " + cost + " (wear " + was / 10 + "% -> 0).");
            return true;
        }
    }
}
