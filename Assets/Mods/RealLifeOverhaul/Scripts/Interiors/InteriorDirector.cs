using System;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BAModAPI;
using Helpers;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// "Interiors and decor matter": how a business of the player's looks counts. Once a game day it reads every one of
    /// them (the game's own interior design score, the placed items, the floor area, the cleanliness, the district) and
    /// works out by <see cref="InteriorRules"/> its decor score, its wear, the district's standard and what each does:
    /// customers (business pressure's footfall, through <see cref="BusinessGame.Ambient"/>), the price the district
    /// bears (the city's economy), complaints (business pressure's own), the brand's standards, Standing, the staff's
    /// comfort, the jobs board and a firm's bids (all through Wiring/InteriorLinks), and the news. Once a game hour,
    /// with the basket switched on, the customers of the coming hour take more products, by the size of their orders.
    ///
    /// Two other authors' mods do parts of this (Core/OtherMods): with Meaningful Interiors switched on the district
    /// standard's footfall and complaints and the basket stay off; with Meaningful Ambience the decor's footfall does.
    /// The rest keeps running. Only the player's own businesses are looked at (GoingPublic.Mine), on this machine.
    ///
    /// Saved: one line, each business's wear and when it was last in the news (<see cref="InteriorStore"/>).
    /// Cost: Update is one comparison a frame; once a second of real time it looks at the clock and the switches.
    /// The daily round is one walk over the player's businesses and their placed items (each kind of item sorted
    /// once a session); the hourly basket one walk over the coming hour's customers of the shops it applies to.
    /// A fault is logged once, every footfall change of the feature's is put back, and it stays off for the session.
    /// </summary>
    public sealed class InteriorDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("interiors", "InteriorDirector", typeof(InteriorDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;
        public static InteriorDirector Instance { get; private set; }

        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(16);
        private readonly List<InteriorShop> _shops = new List<InteriorShop>(16);
        private readonly List<InteriorShop> _pool = new List<InteriorShop>(16);
        private readonly Dictionary<BuildingRegistration, InteriorShop> _byReg = new Dictionary<BuildingRegistration, InteriorShop>(16);
        private InteriorStore _store;
        private float _nextSlow;
        private int _lastDay = -1, _lastHour = -1;
        private long _switches = -1;
        private int _basketToday, _officeToday;
        private readonly Dictionary<string, int> _carry = new Dictionary<string, int>(StringComparer.Ordinal);       // an office's fraction of a client carried to the next hour, thousandths
        private readonly Dictionary<string, int> _booked = new Dictionary<string, int>(StringComparer.Ordinal);      // clients booked today, by office
        private readonly HashSet<CustomerEntry> _ours = new HashSet<CustomerEntry>();   // today's clients the feature booked
        private Action<BuildingRegistration> _onRedrawn;
        private bool _broken;
        private Action _onSave;

        /// <summary>True while the feature runs: switched on, a save read and no fault.</summary>
        public bool Running => !_broken && _store != null && Settings.Interiors;

        /// <summary>Today's businesses as the feature sees them (a shared list: read it, do not keep it).</summary>
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

        // ---------------------------------------------------------------- once a second

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
                _store = InteriorStore.Load(save.modData);   // (a line this version cannot read is kept as it was: Core/SaveLines)
                OtherMods.Look(Logger);
                Logger?.Info("Interiors: ready. " + _store.Files.Count + " business file(s)" + (OtherMods.MeaningfulInteriors.Loaded ? "; Meaningful Interiors is on, so the district standard's part and the basket stay off" : string.Empty)
                             + (OtherMods.MeaningfulAmbience.Loaded ? "; Meaningful Ambience is on, so the decor's footfall stays off" : string.Empty)
                             + (_store.RulesFrom != InteriorFile.Unset ? "; the bounded rules first read this save on day " + _store.RulesFrom : "; the bounded rules meet this save today, and its businesses' effects come in over " + InteriorRules.RampDays + " days")
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
                    Hourly();   // the new day's first customers, now that they have been drawn with today's figures
                }
                return;
            }
            if (switches != _switches)
            {
                _switches = switches;
                Rework(day, false);   // a switch moved: the same day's figures again, with the parts that are on now
            }
            if (hour != _lastHour)
            {
                _lastHour = hour;
                Hourly();
            }
        }

        // The options and the two other mods, as one number: a change of any is a change of what applies (each slider
        // takes 8 bits: 0 to 200).
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

        /// <summary>The district's standard counts (its strength is not off, and Meaningful Interiors is not on).</summary>
        public static bool StandardOn => Parts.StandardScale > 0;
        public static bool DecorOn => Parts.DecorMille > 0;
        public static bool BasketOn => Parts.Basket;

        // ---------------------------------------------------------------- once a game day

        // The morning's round: every business read, its wear aged by the days since it was last read, the figures worked
        // out, footfall written, and a complaint and a piece of news perhaps.
        private void Round(int day, bool events)
        {
            if (_basketToday > 0 || _officeToday > 0)
            {
                Logger?.Info("Interiors: yesterday " + _basketToday + " product(s) more in customers' baskets, " + _officeToday + " client(s) more booked at offices.");
            }
            _basketToday = _officeToday = 0;
            _carry.Clear();
            _booked.Clear();
            _ours.Clear();   // (yesterday's list of customers is the game's no more)
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

        // Reads every business and works its figures out; with 'age' each one's wear takes the days since it was last read.
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
            InteriorGame.Places(_regs);
            InteriorParts parts = Parts;
            if (_store.RulesFrom == InteriorFile.Unset)
            {
                _store.RulesFrom = day;   // these rules meet the save today: what every business already trading does comes in from now
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
                        f.FirstDay = _firstRound ? InteriorFile.LongAgo : day;   // (one already there when the session's first round reads the save has had its days)
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
            }
            _bestOffice = bestOffice;
            int written = 0;
            for (int i = 0; i < _regs.Count; i++)
            {
                InteriorShop s = _byReg[_regs[i]];
                int was;
                before.TryGetValue(s.Place, out was);
                // (the session's first round writes every shop: a figure an older version wrote into the save is put right at once)
                if (s.Kind == InteriorKind.Shop && (_firstRound || s.Footfall != 0 || was != 0) && (age || s.Footfall != was) && Write(_regs[i], age))
                {
                    written++;
                }
            }
            _regs.Clear();
            Logger?.Info("Interiors: day " + day + ", " + _shops.Count + " business(es) read, " + written + " footfall figure(s) written; the best office scores " + Math.Max(0, bestOffice) + ".");
        }

        private int _bestOffice = -1;
        private bool _firstRound;   // the session's first round: a business without a file was trading before the save was read

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

        // A business's footfall written again with its figure of today, and today's customers drawn again ('newDay': on
        // the morning's round, when business pressure redraws a business with a change of its own running itself).
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

        // A business short of its district's standard may hear of it from a customer: at most one complaint a day.
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
                BuildingRegistration reg = BusinessGame.Find(s.Place);
                if (reg != null && Ask(() => InteriorBridges.Complain(reg)))
                {
                    Logger?.Info("Interiors: a complaint about " + s.Name + ": " + s.Overall + " against the district's standard of " + s.Standard + ".");
                    return;
                }
            }
        }

        // A striking interior or a shabby one in the news: one business a day at most, each once in its rest at most.
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
                Logger?.Info("Interiors: " + s.Name + " is in the news for " + (kind > 0 ? "a striking interior (" + s.Overall + ")." : "a shabby one (" + s.Overall + ", wear " + s.Wear / 10 + "%)."));
                return;
            }
        }

        // ---------------------------------------------------------------- once a game hour

        private void Hourly()
        {
            Selling();
            Offices();
            if (!BasketOn)
            {
                return;
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                InteriorShop s = _shops[i];
                if (s.Basket <= 0)
                {
                    continue;
                }
                BuildingRegistration reg = BusinessGame.Find(s.Place);
                _basketToday += reg != null ? InteriorGame.Basket(reg, s.Basket, s.Kind == InteriorKind.Office) : 0;
            }
        }

        // Each office that takes clients is booked its extra ones for the coming hour.
        private void Offices()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].OfficeBoost > 0)
                {
                    BuildingRegistration reg = BusinessGame.Find(_shops[i].Place);
                    if (reg != null)
                    {
                        Book(reg, _shops[i]);
                    }
                }
            }
        }

        private void Book(BuildingRegistration reg, InteriorShop s)
        {
            int carry;
            _carry.TryGetValue(s.Place, out carry);
            int n = InteriorGame.BookOffice(reg, s.OfficeBoost, ref carry, _ours);
            _carry[s.Place] = carry;
            if (n > 0)
            {
                int had;
                _booked.TryGetValue(s.Place, out had);
                _booked[s.Place] = had + n;
                _officeToday += n;
            }
        }

        // A business's customers to come were drawn again: an office's extra clients for the coming hour went with them, and are booked again.
        private void Redrawn(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            if (s != null && s.OfficeBoost > 0 && !_broken)
            {
                Book(reg, s);
            }
        }

        /// <summary>Clients the feature booked at an office today, and how many of them are still in the game's list of the day (some go with a redraw).</summary>
        public int BookedToday(BuildingRegistration reg, out int standing)
        {
            standing = 0;
            InteriorShop s = At(reg);
            int n = 0;
            if (s == null || !_booked.TryGetValue(s.Place, out n))
            {
                return 0;
            }
            List<CustomerEntry> entries = CustomerDraw.Entries(reg);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                standing += entries[i] != null && _ours.Contains(entries[i]) ? 1 : 0;
            }
            return n;
        }

        /// <summary>True for a customer of today's list the feature booked (an office's extra client).</summary>
        public bool Ours(CustomerEntry e)
        {
            return e != null && _ours.Contains(e);
        }

        // Whether each business sells anything this hour: one that stops brings nobody extra from its interior until it
        // sells again, and its footfall is written again when that changes what it brings.
        private void Selling()
        {
            InteriorParts parts = Parts;
            foreach (KeyValuePair<BuildingRegistration, InteriorShop> pair in _byReg)
            {
                InteriorShop s = pair.Value;
                SaleStop stop = InteriorGame.Stop(pair.Key);
                if (stop == s.Stop)
                {
                    continue;
                }
                int was = s.Footfall;
                SaleStop before = s.Stop;
                s.Stop = stop;
                InteriorRules.Work(s, s.Wear, parts);
                Logger?.Info("Interiors: " + s.Name + (stop != SaleStop.None ? " sells nothing just now (" + stop + "): its interior brings nobody extra until it does."
                                                                              : " sells again (it was " + before + "): its interior's customers are back."));
                if (s.Kind == InteriorKind.Shop && s.Footfall != was)
                {
                    Write(pair.Key, false);
                }
            }
        }

        // ---------------------------------------------------------------- off, and the save

        // Everything of the feature's taken off the businesses: their footfall written again without it.
        private void StandDown()
        {
            var had = new List<BuildingRegistration>(_shops.Count);
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].Footfall != 0)
                {
                    BuildingRegistration reg = BusinessGame.Find(_shops[i].Place);
                    if (reg != null)
                    {
                        had.Add(reg);
                    }
                }
            }
            _pool.AddRange(_shops);
            _shops.Clear();
            _byReg.Clear();
            _bestOffice = -1;
            for (int i = 0; i < had.Count; i++)
            {
                Write(had[i], false);   // (FootfallOf answers nothing now)
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

        // ---------------------------------------------------------------- what the rest of the mod asks

        /// <summary>A business's figures today, or null.</summary>
        public static InteriorShop At(BuildingRegistration reg)
        {
            InteriorDirector d = Instance;
            InteriorShop s;
            return d != null && d.Running && reg != null && d._byReg.TryGetValue(reg, out s) ? s : null;
        }

        /// <summary>A business's figures today by its place, or null.</summary>
        public static InteriorShop At(string place)
        {
            InteriorDirector d = Instance;
            if (d == null || !d.Running || string.IsNullOrEmpty(place))
            {
                return null;
            }
            for (int i = 0; i < d._shops.Count; i++)
            {
                if (string.Equals(d._shops[i].Place, place, StringComparison.Ordinal))
                {
                    return d._shops[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The interior's part of a business's footfall, per cent (business pressure's interior part). Asked as business
        /// pressure writes the business's figure, so its shortfall gives way at once to a dip of business pressure's that
        /// has just started or ended there (a complaint): the two together never take more than the bound.
        /// </summary>
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

        /// <summary>
        /// Refreshes a business's finishes: charged at today's prices, its wear back to nothing, its figures worked out
        /// again. A toast either way. True if it was done.
        /// </summary>
        public bool Refresh(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            InteriorFile f = s != null ? _store.File(s.Place, true) : null;
            if (f == null || Parts.WearScale <= 0)
            {
                return false;
            }
            int cost = InteriorGame.RefreshCost(s.Sqm);
            string name = s.Name;   // (the figures are worked out again below: this shop's object goes back to the pool)
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

        // ---------------------------------------------------------------- test buttons

        // The business the player stands in, or the first of theirs the feature looks at.
        private InteriorShop Here(out BuildingRegistration reg)
        {
            reg = BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            InteriorShop s = At(reg);
            if (s == null && _shops.Count > 0)
            {
                s = _shops[0];
                reg = BusinessGame.Held(s.Place);
            }
            return s;
        }

        private bool Refused(string test)
        {
            TestRefusal why = _broken || _store == null ? TestRefusal.NotLoaded : !Settings.Interiors ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return true;
            }
            return false;
        }

        /// <summary>Options page: today's figures again for every business, and those of the one the player stands in shown.</summary>
        public void TestReport()
        {
            if (Refused("Interiors report"))
            {
                return;
            }
            Rework(TimeHelper.CurrentDay, false);
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            if (s == null)
            {
                TestRefusals.TellKey(Logger, "Interiors report", "reallife_interior_test_none");
                return;
            }
            Logger?.Info("Interiors report: " + s.Name + ": interior " + s.Interior + " (" + s.InteriorNow + " after wear and dirt), decor " + s.DecorRaw + " (" + s.Decor + "), overall " + s.Overall
                         + ", standard " + s.Standard + " (income " + s.Income + ", game minimum " + s.GameMinimum + "), " + s.Sqm + " m2, target " + s.Target / 10 + ", wear " + s.Wear / 10
                         + "%, clean " + s.Clean + "; footfall " + s.Footfall + "% (standard " + s.StandardFootfall + " of " + s.StandardFull + " in full, decor " + s.DecorFootfall + " of " + s.DecorFull
                         + ", bound " + s.Bound + ", business pressure's dip " + s.IssueDip + "), phased in " + s.Ramp / 10 + "% since day " + s.PhaseFrom + ", bears " + s.Bears + " per mille, basket "
                         + s.Basket + " per mille, takings " + InteriorRules.Takings(s.Footfall, s.Basket) + " per mille, complaint " + s.ComplaintChance + " per mille, brand " + s.BrandPoints
                         + ", standing " + s.StandingPoints + ", comfort " + s.Comfort + ", office esteem " + s.OfficeEsteem + ", bids " + s.OfficeBid + " per mille.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_report", InteriorText.Report(s), 12f);
        }

        /// <summary>Options page: the finishes of the business the player stands in (or the first) a hundred days older.</summary>
        public void TestWear()
        {
            if (Refused("Interiors wear"))
            {
                return;
            }
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            InteriorFile f = s != null ? _store.File(s.Place, true) : null;
            if (f == null)
            {
                TestRefusals.TellKey(Logger, "Interiors wear", "reallife_interior_test_none");
                return;
            }
            string name = s.Name;
            for (int d = 0; d < 100; d++)
            {
                f.Wear = InteriorRules.Age(f.Wear, s.Clean, false);
            }
            Rework(TimeHelper.CurrentDay, false);
            Logger?.Info("Interiors wear test: " + name + "'s finishes a hundred days older: wear " + f.Wear / 10 + "%.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_worn", new Dictionary<string, string>(2) { { "business", name }, { "wear", (f.Wear / 10).ToString() } }, 9f);
        }

        /// <summary>Options page: the business the player stands in (or the first) in tomorrow's paper for its interior, striking or shabby by its figures.</summary>
        public void TestNews()
        {
            if (Refused("Interiors news"))
            {
                return;
            }
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            if (s == null)
            {
                TestRefusals.TellKey(Logger, "Interiors news", "reallife_interior_test_none");
                return;
            }
            bool striking = s.Overall >= s.Standard;
            InteriorBridges.Story?.Invoke(striking ? "reallife_news_story_interior_striking" : "reallife_news_story_interior_shabby");
            if (striking)
            {
                InteriorBridges.Striking?.Invoke(s.Place, s.Name);
            }
            Logger?.Info("Interiors news test: " + s.Name + " in tomorrow's paper for " + (striking ? "a striking" : "a shabby") + " interior.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_news", new Dictionary<string, string>(1) { { "business", s.Name } }, 9f);
        }
    }
}
