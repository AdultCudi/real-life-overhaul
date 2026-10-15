using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One thing the Planner should say about the company: built on the day event and when the player changes something, never per frame.</summary>
    public struct CompanyAlert
    {
        public int A, B;        // what it is and which one: the Planner's id for the row
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Running a business: staff as an economy, insurance, marketing that is measured, a quarterly return with an
    /// accountant and cash off the books, and licences. See the README ("Economy: running a business").
    ///
    /// This class keeps it going. It has no Update, no timer and no place of its own in the game's events: once a
    /// second the credit side's timer calls <see cref="EachSecond"/> (the save is waited for, the options are looked
    /// at), and the credit side's own handlers of the game's hour and day events call <see cref="OnHour"/> and
    /// <see cref="OnDay"/> when their own work is done (EconomyDirector.AfterHour, AfterDay), after the city's economy.
    ///
    ///   the day    one pass: the player's businesses, the game's statements for yesterday and every employee are
    ///              read once; then each part does its day (the files CompanyDirector.Staff.cs and its neighbours);
    ///              then the footfall figures are written and the Planner's rows are made again. The pass says how
    ///              many milliseconds it took with "Developer diagnostics" on.
    ///   the hour   the footfall figure of a business with a campaign, overtime or a strike is kept; a strike shuts
    ///              the doors in the morning; on the hour that opens a day the hours worked are noted
    ///   the save   the footfall figures are the game's own in the save; the state is written
    ///
    /// The staff rota planner (CompanyDirector.Rota.cs) is the one thing here that is no part with a day of its own:
    /// it works a business out when its page is opened and writes a rota when the player presses the button.
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   footfall     what <see cref="FootfallOf"/> answers when business pressure writes a business's promotion figure
    ///                (Wiring/CompanyLinks adds it to the city's economy's): put back before every save, when this
    ///                stops for any reason, and by the game itself every night
    ///   a closure    business pressure's own, which it reopens itself
    ///   money        transactions of the mod's own, dated, each with its own key and label
    ///   wages        never written: an agreed raise is paid as a transaction on top of the game's wage
    ///
    /// A fault in one part switches that part off for the session with one line and leaves the others running; a
    /// fault outside the parts switches the whole feature off. Either way the footfall figures are put back and the
    /// game's daily run and the rest of the economy are left alone.
    /// </summary>
    public sealed partial class CompanyDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeCompany";
        public const int TabOverview = 0, TabStaff = 1, TabCover = 2, TabMarketing = 3, TabTax = 4;   // (and TabRota, CompanyDirector.Rota.cs)

        private static readonly string[] PartNames = { "staff", "insurance", "marketing", "tax", "licences" };
        private static readonly string[] AppGlyphs = { "Icon-Shop", "Icon-Building", "Icon-Neighborhood" };

        public static IModLogger Logger;
        public static CompanyDirector Instance { get; private set; }

        private readonly List<CompanyShop> _shops = new List<CompanyShop>(16);
        private readonly List<CompanyShop> _pool = new List<CompanyShop>(16);
        private readonly ListIndex<CompanyShop, string> _places = new ListIndex<CompanyShop, string>(PlaceOf, StringComparer.Ordinal);   // a business of _shops by its place
        private readonly ListIndex<CompanyShop, BuildingRegistration> _regs = new ListIndex<CompanyShop, BuildingRegistration>(RegOf, ReferenceComparer<BuildingRegistration>.Instance);
        private int _read;   // the reads of _shops so far: an index made before the last one is made again
        private readonly List<CompanyAlert> _alerts = new List<CompanyAlert>(16);
        private readonly bool[] _partBroken = new bool[(int)CompanyPart.Count];

        private CompanyState _state;
        private string _beforeTests;   // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault outside the parts: off for the session
        private bool _holding;         // the footfall figures are the game's own for a save: nothing is added until the next game hour
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _level;
        private int _parts;            // which parts were on at the last look, a bit each
        private int _index = CompanyCatalogue.IndexStart;
        private CycleState _cycle = CycleState.Steady;
        private CompanyApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public CompanyState State => _running ? _state : null;

        /// <summary>The player's businesses as they were read this morning (or when the feature started today).</summary>
        public IReadOnlyList<CompanyShop> Shops => _shops;

        public IReadOnlyList<CompanyAlert> Alerts => _alerts;
        public int Level => _level;

        /// <summary>The price index as the company counts it: tenths of a point, 1000 at the start.</summary>
        public int Index => _index;

        public CycleState Cycle => _cycle;

        /// <summary>The game's day now (0 with no save): what the pages measure a rest against.</summary>
        public int Today => Save != null ? Save.Day : 0;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            CompanyGame.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            EconomyDirector.Follow(StartSeats.Company, _second);   // (its first second after a load has a frame of its own: Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state and the figures are its
            }
            // The city is unloading (or the mod is being switched off). Nothing is written to the save here: what a save
            // keeps is written when the game saves.
            try
            {
                StandDown("the city is unloading", false, false, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _running = false;
            }
            Instance = null;
        }

        // ---------------------------------------------------------------- the parts

        /// <summary>Whether a part runs: the feature is running, the part's option is on and no fault has stopped it.</summary>
        public bool On(CompanyPart part)
        {
            return _running && !_broken && !_partBroken[(int)part] && Wanted(part);
        }

        private static bool Wanted(CompanyPart part)
        {
            switch (part)
            {
                case CompanyPart.Staff: return Settings.EconomyCompanyStaff;
                case CompanyPart.Cover: return Settings.EconomyCompanyCover;
                case CompanyPart.Marketing: return Settings.EconomyCompanyMarketing;
                case CompanyPart.Tax: return Settings.EconomyCompanyTax;
                case CompanyPart.Licences: return Settings.EconomyCompanyLicences;
                default: return false;
            }
        }

        private int PartsOn()
        {
            int mask = 0;
            for (int p = 0; p < (int)CompanyPart.Count; p++)
            {
                mask |= On((CompanyPart)p) ? 1 << p : 0;
            }
            mask |= Settings.EconomyCompanyHide ? 1 << (int)CompanyPart.Count : 0;   // (no part of its own: the tax part's cash off the books)
            mask |= RotaOn ? 1 << ((int)CompanyPart.Count + 1) : 0;                  // (no part either: the staff rota planner)
            return mask;
        }

        // Each part's day, hour and rows for the Planner are in its own file; a part that is not built yet is simply not called.
        partial void StaffDay(int day);
        partial void StaffHour(int day, int hour);
        partial void StaffLevel();
        partial void StaffAlerts(int day);
        partial void CoverDay(int day);
        partial void CoverAlerts(int day);
        partial void MarketingDay(int day);
        partial void MarketingAlerts(int day);
        partial void TaxDay(int day);
        partial void TaxLevel();
        partial void TaxAlerts(int day);
        partial void LicenceDay(int day);
        partial void LicenceAlerts(int day);
        partial void RotaRead();
        partial void RotaStop();
        partial void RotaLoad(GameInstance save);
        partial void RotaSave(GameInstance save);
        partial void RotaAlerts(int day);

        private void PartDay(CompanyPart part, int day)
        {
            switch (part)
            {
                case CompanyPart.Staff: StaffDay(day); break;
                case CompanyPart.Cover: CoverDay(day); break;
                case CompanyPart.Marketing: MarketingDay(day); break;
                case CompanyPart.Tax: TaxDay(day); break;
                case CompanyPart.Licences: LicenceDay(day); break;
            }
        }

        private void PartAlerts(CompanyPart part, int day)
        {
            switch (part)
            {
                case CompanyPart.Staff: StaffAlerts(day); break;
                case CompanyPart.Cover: CoverAlerts(day); break;
                case CompanyPart.Marketing: MarketingAlerts(day); break;
                case CompanyPart.Tax: TaxAlerts(day); break;
                case CompanyPart.Licences: LicenceAlerts(day); break;
            }
        }

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private static readonly int HotSecond = HotSpots.Slot("Economy: CompanyDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);   // the credit side's second, for this feature (Core/HotSpots)
            if (_broken)
            {
                return;
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

        private void Slow()
        {
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            if (!Settings.Economy || !Settings.EconomyCompany || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyCompany ? "the option is off" : "the credit side is not running", true, true, false);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            int level = CompanyCatalogue.Level(Settings.EconomyCompanyLevel);
            int parts = PartsOn();
            if (level == _level && parts == _parts)
            {
                return;
            }
            // The strictness or a part's switch was changed: what no longer applies is taken back at once.
            _level = level;
            _parts = parts;
            StaffLevel();
            TaxLevel();
            WriteAll(Save.Day, true, false);
            RotaRead();
            Changed();
            if (Verbose)
            {
                Logger?.Info("Company check (options): strictness " + _level + ", parts on " + PartList(parts) + "; the footfall figures were written again: PASS");
            }
        }

        private static string PartList(int mask)
        {
            string list = string.Empty;
            for (int p = 0; p < PartNames.Length; p++)
            {
                if ((mask & (1 << p)) != 0)
                {
                    list += (list.Length > 0 ? ", " : string.Empty) + PartNames[p];
                }
            }
            return list.Length > 0 ? list : "none";
        }

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
            GameInstance save = Save;
            int today = save.Day;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            CompanyState state = CompanyStore.Load(save.modData, CompanyStore.Key, out version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new CompanyState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = today };
                state.Tax.QuarterFrom = today;
            }
            string before;
            _beforeTests = save.modData.TryGetValue(CompanyStore.TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            state.LastDay = today;   // days that passed while this was off are not counted
            _state = state;
            _level = CompanyCatalogue.Level(Settings.EconomyCompanyLevel);
            _holding = false;
            _running = true;
            _parts = PartsOn();
            int added, dropped, gone;
            Read(today, fresh, false, out added, out dropped, out gone);
            StaffLevel();
            TaxLevel();
            WriteAll(today, true, false);
            RotaLoad(save);
            RotaRead();
            BuildAlerts(today);
            Logger?.Info("Company: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + _shops.Count + " business(es)"
                         + (fresh && added > 0 ? ", each with the licences it needs for a first term" : string.Empty) + ", " + _state.Staff.Count + " employee(s), strictness " + _level
                         + ", parts on: " + PartList(_parts) + ", quarter " + _state.Tax.Quarter + " from day " + _state.Tax.QuarterFrom + ".");
        }

        // The game is read once: the businesses, yesterday's statements, the staff. The economy's figures are read with it.
        private void Read(int day, bool ranBefore, bool countLoss, out int added, out int dropped, out int gone)
        {
            _index = Prices.Level;
            _cycle = CityEconomy.State;
            CompanyGame.ReadShops(_state, _shops, _pool, day, ranBefore, out added, out dropped);
            _read++;
            CompanyGame.ReadBooks(_shops, day);
            for (int i = 0; countLoss && i < _shops.Count; i++)
            {
                ShopFile file = _shops[i].File;
                file.LossRun = _shops[i].HasBooks && _shops[i].Profit < 0 ? file.LossRun + 1 : 0;
            }
            ReadPeople(out gone);
        }

        // The staff as the game has them now, and each business's count of them.
        private void ReadPeople(out int gone)
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                _shops[i].Staff = _shops[i].Sick = 0;
            }
            CompanyGame.ReadStaff(_state, _shops, _index, _cycle, out gone);
            Recount();
        }

        /// <summary>Each business's staff added up from their files: how many stand low, and their standing in all.</summary>
        internal void Recount()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                _shops[i].Staff = _shops[i].Low = _shops[i].Standing = 0;
            }
            List<StaffFile> staff = _state.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                CompanyShop shop = ShopAt(staff[i].Place);
                if (shop != null)
                {
                    shop.Staff++;
                    shop.Standing += staff[i].Standing;
                    shop.Low += StaffRules.StandsLow(staff[i]) ? 1 : 0;
                }
            }
        }

        // The feature stops: every figure it changed is the game's own again. With 'keep' the state goes into the save's
        // data first (a stop in the middle of play).
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null && _state != null)
            {
                Write(Save);
            }
            _running = false;
            _holding = false;
            int put = 0;
            CustomerDraw.Hold("the company stood down");
            try
            {
                for (int i = 0; !unloading && i < _shops.Count; i++)
                {
                    if (_shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], 0, true, false);   // (nothing is running, so nothing is added: the figure is the game's own, with whatever else the mod has there)
                        put++;
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            for (int i = 0; i < _shops.Count; i++)
            {
                _shops[i].Reg = null;
                _pool.Add(_shops[i]);
            }
            _shops.Clear();
            _read++;
            _alerts.Clear();
            RotaStop();
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Company check (restored, " + why + "): " + put + " footfall figure(s) put back; no wage and no figure of a business was ever written: PASS");
            }
        }

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
        }

        /// <summary>A fault outside the parts: one line, every figure put back, and nothing more of the feature this session.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Company: switched off for this session after an error in " + where + ". Every business's footfall is the game's own again; the game's daily run and the rest of the economy are not affected.");
        }

        /// <summary>A fault in one part: that part is off for the session, what it added to footfall is taken back, the others go on.</summary>
        private void PartFault(CompanyPart part, string where, Exception e)
        {
            _partBroken[(int)part] = true;
            Logger?.Error(e);
            Logger?.Warn("Company: " + PartNames[(int)part] + " switched off for this session after an error in " + where + ". What it added to any business's footfall is taken back; the other parts, the game's daily run and the rest of the economy are not affected.");
            try
            {
                _parts = PartsOn();
                if (Save != null)
                {
                    WriteAll(Save.Day, true, false);
                }
            }
            catch (Exception second)
            {
                Fault("putting the footfall back", second);
            }
        }

        private void Install()
        {
            var view = new CompanyApp(this);
            GameSprites.Want(AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_co_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(120, 150, 232, 255), new Color32(52, 78, 176, 255), 0.42f, "AppIcon-Company"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Company: the game's phone is not built as this version expects, so the Company app is left off. The rules still run, and the Planner and the notifications still say what needs an answer.");
                }
                return;
            }
            _view = view;
            Badge();
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the hour, the day, the save

        private static readonly int HotOnHour = HotSpots.Slot("Economy: CompanyDirector.OnHour");
        // The game's hour event (every game hour, and first thing in its NewDay, before its daily run), after the credit side's and the city's work on it.
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                _holding = false;
                int day = Save.Day;
                if (day > _state.LastDay)
                {
                    CompanyGame.NoteHours(_state);   // the hours of the day that is ending, before the game's run clears them
                    return;                          // (the run writes every footfall figure afresh in a moment, and the day event follows it)
                }
                for (int i = 0; i < _shops.Count; i++)
                {
                    int asked = Footfall(_shops[i], day);
                    if (asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], asked, false, false);
                    }
                }
                if (On(CompanyPart.Staff))
                {
                    try
                    {
                        StaffHour(day, Save.Hour);
                    }
                    catch (Exception e)
                    {
                        PartFault(CompanyPart.Staff, "the hour", e);
                    }
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: CompanyDirector.OnDay");
        // The game's day event (after its daily run), after the credit side has settled its day and the city's economy has applied its own.
        private void OnDay()
        {
            using var hotSpot = HotSpots.Time(HotOnDay);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int day = Save.Day;
                if (day <= _state.LastDay)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                _holding = false;
                bool next = day == _state.LastDay + 1;   // (a jump of many days is one pass, for today)
                _state.LastDay = day;
                _state.Age++;
                int added, dropped, gone;
                Read(day, false, next, out added, out dropped, out gone);
                float read = Time.realtimeSinceStartup;
                for (int p = 0; p < (int)CompanyPart.Count; p++)
                {
                    var part = (CompanyPart)p;
                    if (!On(part))
                    {
                        continue;
                    }
                    try
                    {
                        PartDay(part, day);
                    }
                    catch (Exception e)
                    {
                        PartFault(part, "the day", e);
                    }
                }
                float ruled = Time.realtimeSinceStartup;
                int written = WriteAll(day, true, true);
                RotaRead();
                Changed();
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Company check (day " + day + "): " + _shops.Count + " business(es)" + (added > 0 ? ", " + added + " new" : string.Empty) + (dropped > 0 ? ", " + dropped + " no longer the player's" : string.Empty)
                                 + ", " + _state.Staff.Count + " employee(s)" + (gone > 0 ? ", " + gone + " gone and their files dropped" : string.Empty) + ", prices " + (_index / 10f).ToString("0.0") + ", the economy " + _cycle
                                 + ", " + written + " footfall figure(s) written, " + _alerts.Count + " row(s) for the Planner; reading the game " + ((read - started) * 1000f).ToString("0.0") + " ms, the rules "
                                 + ((ruled - read) * 1000f).ToString("0.0") + " ms, footfall and rows " + ((done - ruled) * 1000f).ToString("0.0") + " ms, " + ((done - started) * 1000f).ToString("0.0") + " ms in all: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // The game is about to write a save. The footfall figures are put at what they are without the company (the
        // registration that holds them is part of the save) and stay so until the next game hour; the state is written.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                int put = 0;
                _holding = true;
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], 0, false, false);
                        put++;
                    }
                }
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Company check (before a save): " + put + " footfall figure(s) put at the game's own until the next game hour; the state written under its own key: PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[CompanyStore.Key] = CompanyStore.Write(_state);
            RotaSave(save);
            if (_beforeTests != null)
            {
                save.modData[CompanyStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(CompanyStore.TestKey);
            }
        }

        // ---------------------------------------------------------------- footfall

        // What the company asks for at a business on a day, per cent of customers: a pure function of the files and
        // the day, so it is right whoever asks and whenever.
        private int Footfall(CompanyShop shop, int day)
        {
            return (On(CompanyPart.Staff) ? StaffRules.Footfall(shop.File, day) : 0) + (On(CompanyPart.Marketing) ? CampaignRules.Footfall(shop.File, day) : 0);
        }

        /// <summary>
        /// What the company adds to a business's footfall now, in per cent of customers: asked each time business
        /// pressure writes a business's figure (Wiring/CompanyLinks). Nothing while this is not running, and nothing
        /// while the figures are held at the game's own for a save.
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            CompanyDirector d = Instance;
            if (d == null || !d._running || d._holding || d._broken || Save == null || reg == null)
            {
                return 0;
            }
            CompanyShop shop = d._regs.First(d._shops, reg, d._read);   // (a look-up: asked on every figure of every business written)
            return shop != null ? d.Footfall(shop, Save.Day) : 0;
        }

        private static string PlaceOf(CompanyShop shop)
        {
            return shop.File != null ? shop.File.Place : null;
        }

        private static BuildingRegistration RegOf(CompanyShop shop)
        {
            return shop.Reg;
        }

        private void WriteFootfall(CompanyShop shop, int asked, bool redraw, bool newDay)
        {
            int own = 0, written = 0;
            bool has = CompanyBridges.Footfall != null && shop.Reg != null && CompanyBridges.Footfall(shop.Reg, redraw, newDay, out own, out written);
            bool applied = has && _running && !_holding && asked != 0;
            if (Verbose && (redraw || applied != shop.Applied || asked != shop.Asked))
            {
                Logger?.Info("Company check (footfall, " + shop.Name + "): " + (asked > 0 ? "+" : string.Empty) + asked + "% of customers asked for"
                             + (has ? ", promotion " + own + " to " + written + " (with whatever else the mod has there)" : ", the game keeps no promotion figure there: nothing written")
                             + (applied ? ": applied" : shop.Applied ? ": restored" : string.Empty) + ": PASS");
            }
            shop.Asked = asked;
            shop.Applied = applied;
        }

        // Every business where the company adds something, or did: its figure is written. Returns how many. One batch of
        // footfall writes: a business asked to be drawn again is drawn once, spread over the frames that follow (Core/CustomerDraw).
        private int WriteAll(int day, bool redraw, bool newDay)
        {
            int written = 0;
            CustomerDraw.Hold("the company's figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    int asked = Footfall(_shops[i], day);
                    if (asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], asked, redraw, newDay);
                        written++;
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
            return written;
        }

        /// <summary>One business's figure, now: the player has just changed something there.</summary>
        internal void Refoot(CompanyShop shop)
        {
            if (shop != null && Save != null)
            {
                WriteFootfall(shop, Footfall(shop, Save.Day), true, false);
            }
        }


        /// <summary>True where the game keeps a customer figure for the business, so that a campaign, overtime or a strike can change its footfall.</summary>
        public bool Promoted(CompanyShop shop)
        {
            return shop != null && shop.Reg != null && CompanyBridges.Promoted != null && CompanyBridges.Promoted(shop.Reg);
        }
        // ---------------------------------------------------------------- money

        /// <summary>
        /// A business cost: taken as a transaction of its own (unless 'force', never forced: false, and nothing is taken, if the cash
        /// does not cover it), marked deductible, and counted in the quarter that is running.
        /// </summary>
        internal bool Cost(int amount, string key, CompanyShop shop, string name, CostKind kind, int? day, string how, bool force = false)
        {
            bool taken = CompanyGame.Take(amount, key, name, shop, day, (int)kind, force);
            if (taken && amount > 0)
            {
                TaxRules.Note(_state.Tax, kind, amount);
            }
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken, deductible, counted in quarter " + _state.Tax.Quarter : " could not be taken: the cash does not cover it")
                             + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>A charge that is no business cost (a penalty, a fine, back-tax): never deductible. With 'force' it is taken whatever the cash is.</summary>
        internal bool Charge(int amount, string key, CompanyShop shop, string name, int? day, bool force, string how)
        {
            bool taken = CompanyGame.Take(amount, key, name, shop, day, -1, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken" : " could not be taken: the cash does not cover it") + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>Money paid to the player (a claim, a rebate).</summary>
        internal void Pay(int amount, string key, CompanyShop shop, string name, int? day, string how)
        {
            CompanyGame.Give(amount, key, name, shop, day);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Company check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }

        // ---------------------------------------------------------------- what the player is told

        /// <summary>A notification of the game's that opens the Company app on a tab, with something picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>The app has been opened: the staff are read again, so that a wage changed in the game's own app since the morning shows.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                int gone;
                ReadPeople(out gone);
                RotaRead();
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: the Planner's rows and the app's badge are made again, and the app if it is open.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            BuildAlerts(Save.Day);
            Badge();
            if (_view != null)
            {
                _view.Changed();
            }
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

        /// <summary>How many things wait for an answer from the player.</summary>
        public int Waiting()
        {
            if (!_running || _state == null || Save == null)
            {
                return 0;
            }
            int day = Save.Day, waiting = 0;
            if (On(CompanyPart.Staff))
            {
                for (int i = 0; i < _state.Staff.Count; i++)
                {
                    waiting += (_state.Staff[i].AskDay > 0 ? 1 : 0) + (_state.Staff[i].PoachDay > 0 ? 1 : 0);
                }
                for (int i = 0; i < _state.Shops.Count; i++)
                {
                    waiting += (_state.Shops[i].StrikeFrom > 0 ? 1 : 0) + (_state.Shops[i].OvertimeAsk > 0 ? 1 : 0);
                }
            }
            for (int i = 0; On(CompanyPart.Cover) && i < _state.Claims.Count; i++)
            {
                waiting += _state.Claims[i].Stage == ClaimStage.Open ? 1 : 0;
            }
            waiting += On(CompanyPart.Tax) && _state.Tax.ReturnWaiting ? 1 : 0;
            for (int i = 0; On(CompanyPart.Licences) && i < _shops.Count; i++)
            {
                waiting += LicenceRules.Missing(_shops[i].File, _shops[i].Needs, day) != 0 ? 1 : 0;
            }
            return waiting;
        }

        // ---------------------------------------------------------------- the Planner's rows

        private void BuildAlerts(int day)
        {
            _alerts.Clear();
            for (int p = 0; p < (int)CompanyPart.Count; p++)
            {
                if (On((CompanyPart)p))
                {
                    try
                    {
                        PartAlerts((CompanyPart)p, day);
                    }
                    catch (Exception e)
                    {
                        PartFault((CompanyPart)p, "the Planner's rows", e);
                    }
                }
            }
            // A business that lost money three days running, by the game's own statements: whatever parts are on.
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].File.LossRun >= CompanyCatalogue.LossRunDays)
                {
                    Alert(1, PlaceNumber(_shops[i].File.Place), day, CompanyText.T("reallife_co_plan_loss_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_loss_what", "days", _shops[i].File.LossRun.ToString(), "amount", FinanceText.Money(-_shops[i].Profit)), null, null);
                }
            }
            RotaAlerts(day);
        }

        internal void Alert(int a, int b, int day, string title, string what, string money, string ifMissed)
        {
            if (_alerts.Count < CompanyCatalogue.AlertsMost)
            {
                _alerts.Add(new CompanyAlert { A = a, B = b, Day = day, Title = title, What = what, Money = money, IfMissed = ifMissed });
            }
        }

        /// <summary>A number for a place or an id that is always the same for it: the second half of a Planner row's id.</summary>
        internal static int PlaceNumber(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)(h & 0x3fffffff);
            }
        }

        // ---------------------------------------------------------------- for the app and the parts

        /// <summary>The business at a place, as a walk of today's businesses finds it: a look-up (Core/LookupRules ListIndex), made again with each read of them.</summary>
        internal CompanyShop ShopAt(string place)
        {
            return _places.First(_shops, place, _read);
        }

        /// <summary>Where in today's businesses the one at a place is; -1 if none.</summary>
        internal int ShopIndex(string place)
        {
            return _places.At(_shops, place, _read);
        }

        internal string ShopName(string place)
        {
            CompanyShop shop = ShopAt(place);
            return shop != null ? shop.Name : "?";
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>For the save tools: every figure is the game's own again; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                StandDown("the save tools asked", true, true, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            try
            {
                StandDown("the save's line was removed", false, false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _state = null;
            _beforeTests = null;
        }
    }
}
