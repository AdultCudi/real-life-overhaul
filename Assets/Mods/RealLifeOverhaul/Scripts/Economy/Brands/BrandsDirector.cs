using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>One thing the Planner should say about the brands: built on the day event and when the player changes something, never per frame.</summary>
    public struct BrandAlert
    {
        public int A, B;        // what it is and which one: the Planner's id for the row
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Brands and franchises: a brand made from a shop, its recognition and standards, what it adds to its member
    /// shops' footfall, its campaigns, shared prices and hours; franchising it to applicants who open shops under it
    /// and pay a royalty; and one of the player's own shops under a city chain. See <see cref="BrandRules"/> for the
    /// rules and the balance, <see cref="BrandsGame"/> for what is read from and done to the game.
    ///
    /// This class keeps it going. PER FRAME: NOTHING. It has no Update and no timer of its own. Once a second the
    /// credit side's timer calls <see cref="EachSecond"/>: while nothing has changed that is a dozen comparisons (the
    /// save is there, the options are as they were) and one number worked out from the franchised shops to see that
    /// the map's markers are up to date (three multiplications for each shop), with no allocation. The credit
    /// side's own handlers of the game's hour and day events call <see cref="OnHour"/> and <see cref="OnDay"/> when
    /// their own work is done (EconomyDirector.AfterHour, AfterDay), after the city's economy and the company.
    ///
    ///   the day    one pass: the player's businesses are read once (for a shop under a brand or a chain: four figures,
    ///              its order history's newest entry, its price list and its staff); every franchised shop takes its
    ///              day (about forty integer operations and a walk over the brand's shops); each brand's recognition
    ///              takes its step; then the footfall figures are written and the Planner's rows are made again. The
    ///              rules for a city with sixty franchised shops take under 0.2 ms (checked offline); the pass says how
    ///              many milliseconds it took with "Developer diagnostics" on.
    ///   the hour   the footfall figure of a shop where the brand asks for something is kept: one write for each;
    ///              and one round over the shops whose prices are kept (BrandsDirector.Prices): a dictionary look-up
    ///              a product, and a write only where a price is not as it should stand
    ///   the save   the footfall figures are the game's own in the save; the state is written
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   footfall     what <see cref="FootfallOf"/> answers when business pressure writes a business's promotion figure
    ///                (Wiring/BrandLinks adds it to the city's economy's and the company's: the mod's one place for
    ///                that figure). Put back before every save, when this stops for any reason, and by the game itself
    ///                every night
    ///   money        transactions of the mod's own, each with its own key and label
    ///   a shop       a franchised shop that is a business of the game's own stays one for as long as its agreement
    ///                runs, with the option off too (it is an ordinary business of the city's then: nothing is paid and
    ///                nothing is asked); when the agreement ends the game's own closure puts the building back to let
    ///   a start day  the day the game has for a franchised shop's start is kept at today while its agreement runs,
    ///                so that the game's own market does not close it (BrandsGame.Keep); for a shop that traded
    ///                before the agreement the day it had is put back when the agreement ends
    ///   prices, hours   written when the player presses the button, as the game's own screens write them: the
    ///                player's own settings from then on, not taken back. Kept prices (the player's switch) are
    ///                written each day and put back each hour while the switch is on; switched off, they stay as
    ///                they stand
    ///
    /// A fault switches the feature off for the session with one line: the footfall figures are put back, and the
    /// game's daily run and the rest of the economy are left alone.
    /// </summary>
    public sealed partial class BrandsDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeBrands";
        public const int TabBrands = 0, TabShops = 1, TabApplicants = 2, TabAgreements = 3;

        private static readonly string[] AppGlyphs = { "Icon-Neighborhood", "Icon-Shop", "Icon-Building" };

        public static IModLogger Logger;
        public static BrandsDirector Instance { get; private set; }

        private readonly List<BrandShop> _shops = new List<BrandShop>(16);
        private readonly List<BrandShop> _pool = new List<BrandShop>(16);
        private readonly ListIndex<BrandShop, string> _places = new ListIndex<BrandShop, string>(PlaceOf, StringComparer.Ordinal);   // a shop of _shops by its place
        private readonly ListIndex<BrandShop, BuildingRegistration> _regs = new ListIndex<BrandShop, BuildingRegistration>(RegOf, ReferenceComparer<BuildingRegistration>.Instance);
        private int _read;   // the reads of _shops so far: an index made before the last one is made again
        private readonly List<BrandAlert> _alerts = new List<BrandAlert>(16);
        private readonly List<string> _districts = new List<string>(16);
        private readonly List<BrandSite> _sites = new List<BrandSite>(12);

        private BrandsState _state;
        private string _beforeTests;   // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault: off for the session
        private bool _holding;         // the footfall figures are the game's own for a save: nothing is added until the next game hour
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _sitesFor;         // the applicant the list of buildings was made for
        private BrandsApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;
        private Func<Franchise, string> _markName;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public BrandsState State => _running ? _state : null;

        /// <summary>The player's businesses as they were read this morning (or when the app was last opened).</summary>
        public IReadOnlyList<BrandShop> Shops => _shops;

        public IReadOnlyList<BrandAlert> Alerts => _alerts;

        /// <summary>The price index as brands count it: tenths of a point, 1000 at the start and while the city's economy is off.</summary>
        public static int Index => Prices.Level;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            BrandsGame.Logger = Logger;
            BrandsGame.Forget();   // (a new city: the map's rows of the last one went with it)
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _markName = MarkName;
            EconomyDirector.Follow(StartSeats.Brands, _second);   // (its first second after a load has a frame of its own: Core/StartLine)
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
            BrandsGame.Forget();
            Instance = null;
        }

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private static readonly int HotSecond = HotSpots.Slot("Economy: BrandsDirector.Second");
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
            if (!Settings.Economy || !Settings.EconomyBrands || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyBrands ? "the option is off" : "the credit side is not running", true, true, false);
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
            BrandsGame.Marks(_state, true, _markName);
        }

        private string MarkName(Franchise f)
        {
            return BrandsText.Shop(_state.Brand(f.BrandId), f);
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
            BrandsState state = BrandStore.Load(save.modData, BrandStore.Key, out version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new BrandsState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = today };
            }
            string before;
            _beforeTests = save.modData.TryGetValue(BrandStore.TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            state.LastDay = today;   // days that passed while this was off are not counted
            _state = state;
            _holding = false;
            _running = true;
            _sitesFor = 0;
            int left;
            Read(today, out left);
            WriteAll(true, false);
            BuildAlerts(today);
            Logger?.Info("Brands: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + _state.Brands.Count + " brand(s), " + _state.Members.Count
                         + " member shop(s), " + _state.Franchises.Count + " franchised shop(s), " + _state.Applicants.Count + " applicant(s), " + _state.Taken.Count + " shop(s) under a city chain"
                         + (left > 0 ? "; " + left + " shop(s) no longer the player's were dropped" : string.Empty) + ".");
        }

        // The game is read once: the player's businesses and, for those under a brand or a chain, their figures. What each asks for is worked out with it.
        private void Read(int day, out int left)
        {
            BrandsGame.ReadShops(_state, _shops, _pool, day, out left);
            _read++;
            for (int i = 0; i < _shops.Count; i++)
            {
                // (a place the player has made another kind of business of is no shop of its brand any more)
                BrandShop shop = _shops[i];
                Brand of = shop.File != null ? _state.Brand(shop.File.BrandId) : null;
                if (shop.File != null && (of == null || !string.Equals(of.Type, shop.Type, StringComparison.Ordinal)))
                {
                    BrandRules.Leave(_state, shop.Place);
                    shop.File = null;
                    left++;
                }
            }
            for (int i = 0; i < _state.Brands.Count; i++)
            {
                Brand b = _state.Brands[i];
                b.Standards = BrandRules.Standards(_state, b);
                b.Ticket = BrandRules.Ticket(_state, b);
            }
            _sitesFor = 0;   // (the buildings to let are read again the next time an applicant's page asks)
            Recount();
        }

        // What the brand or the chain asks for at each shop, per cent of customers: a pure function of the state.
        private void Recount()
        {
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                Brand b = shop.File != null ? _state.Brand(shop.File.BrandId) : null;
                shop.Asked = b != null ? BrandRules.Apply(_state, b, shop.File)
                    : shop.Chain != null && FranchiseRules.ChainGives(shop.Chain) ? FranchiseRules.ChainFootfall(shop.Chain.Chain) : 0;
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
            CustomerDraw.Hold("the brands stood down");
            try
            {
                for (int i = 0; !unloading && i < _shops.Count; i++)
                {
                    if (_shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], true, false);   // (nothing is running, so nothing is added: the figure is the game's own, with whatever else the mod has there)
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
                _shops[i].File = null;
                _shops[i].Chain = null;
                _pool.Add(_shops[i]);
            }
            _shops.Clear();
            _read++;
            _alerts.Clear();
            _sites.Clear();
            _sitesFor = 0;
            _books.Clear();   // (kept prices stay as they stand: what was written is looked at afresh when this starts again)
            _keepFrom = 0;
            if (!unloading)
            {
                BrandsGame.Marks(null, false, _markName);
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Brands check (restored, " + why + "): " + put + " footfall figure(s) put back; franchised shops that are businesses of the game's own trade on as the city's: PASS");
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

        /// <summary>A fault: one line, every figure put back, and nothing more of the feature this session.</summary>
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
            Logger?.Warn("Brands: switched off for this session after an error in " + where + ". Every shop's footfall is the game's own again; the game's daily run and the rest of the economy are not affected.");
        }

        private void Install()
        {
            var view = new BrandsApp(this);
            GameSprites.Want(AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_brand_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(236, 150, 84, 255), new Color32(176, 84, 40, 255), 0.42f, "AppIcon-Brands"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Brands: the game's phone is not built as this version expects, so the Brands app is left off. The rules still run, and the Planner and the notifications still say what needs an answer.");
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

        private static readonly int HotOnHour = HotSpots.Slot("Economy: BrandsDirector.OnHour");
        // The game's hour event (every game hour, and first thing in its NewDay, before its daily run), after the credit side's, the city's and the company's work on it.
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
                if (Save.Day > _state.LastDay)
                {
                    return;   // (the game's run writes every footfall figure afresh in a moment, and the day event follows it)
                }
                WriteAll(false, false);
                KeepRound(false);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private static readonly int HotOnDay = HotSpots.Slot("Economy: BrandsDirector.OnDay");
        // The game's day event (after its daily run), after the credit side has settled its day and the city's economy and the company have applied theirs.
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
                _state.LastDay = day;   // (a jump of many days is one pass, for today)
                int left;
                Read(day, out left);
                KeepRound(true);   // (before the chains look at the prices)
                float read = Time.realtimeSinceStartup;
                RulesDay(day);
                Recount();
                float ruled = Time.realtimeSinceStartup;
                int written = WriteAll(true, true);
                Changed(false);
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Brands check (day " + day + "): " + _state.Brands.Count + " brand(s), " + _state.Members.Count + " member shop(s)" + (left > 0 ? ", " + left + " no longer the player's" : string.Empty) + ", "
                                 + _state.Franchises.Count + " franchised shop(s), " + _state.Applicants.Count + " applicant(s), " + written + " footfall figure(s) written, " + _alerts.Count
                                 + " row(s) for the Planner; reading the game " + ((read - started) * 1000f).ToString("0.0") + " ms, the rules " + ((ruled - read) * 1000f).ToString("0.00") + " ms, footfall and rows "
                                 + ((done - ruled) * 1000f).ToString("0.0") + " ms, " + ((done - started) * 1000f).ToString("0.0") + " ms in all: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // The game is about to write a save. The footfall figures are put at what they are without the brands (the
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
                        WriteFootfall(_shops[i], false, false);
                        put++;
                    }
                }
                Write(Save);
                if (Verbose)
                {
                    Logger?.Info("Brands check (before a save): " + put + " footfall figure(s) put at the game's own until the next game hour; the state written under its own key: PASS");
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
            save.modData[BrandStore.Key] = BrandStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[BrandStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(BrandStore.TestKey);
            }
        }

        // ---------------------------------------------------------------- the day's rules

        // One day for every brand, every franchised shop, the applicants and the shops under a city chain. The shops were read just before.
        private void RulesDay(int day)
        {
            int index = Index, yearDays = BrandsGame.DaysPerYear;
            CycleState cycle = CityEconomy.State;
            _districts.Clear();
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                if (shop.File != null && BrandsBridges.StrikeStarts != null && BrandsBridges.StrikeStarts(shop.Place, day))
                {
                    Heard(shop.Place, Scandal.Strike);
                }
            }
            for (int k = 0; k < _state.Brands.Count; k++)
            {
                Brand b = _state.Brands[k];
                int royalty = 0, levy = 0, support = 0, shops = 0;
                for (int i = _state.Franchises.Count - 1; i >= 0; i--)
                {
                    Franchise f = _state.Franchises[i];
                    if (f.BrandId != b.Id)
                    {
                        continue;
                    }
                    if (!BrandsGame.Stands(f, b.Type))
                    {
                        Gone(b, f, day);
                        continue;
                    }
                    BrandsGame.Keep(f, b.Type, day);
                    if (f.Ask == Ask.Dispute && f.Suit != 0 && (BrandsBridges.ClaimOpen == null || !BrandsBridges.ClaimOpen(f.Suit)))
                    {
                        Logger?.Info("Brands: the court has dealt with claim " + f.Suit + " of " + f.Name + "; the dispute is over.");
                        FranchiseRules.Stand(b, f, day);
                    }
                    FranchiseDay r = FranchiseRules.Day(_state, b, f, day, Mathf.RoundToInt(CityEconomy.DistrictIncome(f.District)), cycle, index);
                    royalty += r.Royalty;
                    levy += r.Levy;
                    support += r.Support;
                    shops++;
                    After(b, f, r.Event, day);
                }
                if (shops > 0)
                {
                    Pay(royalty, BrandsGame.RoyaltyKey, b.Name, "the royalty of " + shops + " franchised shop(s) for day " + (day - 1));
                    Pay(levy, BrandsGame.LevyKey, b.Name, "their marketing levy; the brand's fund is now " + (b.Fund + levy));
                    Cost(support, BrandsGame.SupportKey, b.Name, "supporting them: " + BrandCatalogue.SupportBase + " each at prices of " + index + " and " + BrandsText.Bp(BrandCatalogue.SupportBp) + " of their takings", true);
                    b.Fund += levy;
                    b.Earned += royalty;
                }
                FranchiseRules.NoteNet(b, royalty - support);
                BrandRules.Day(_state, b);
                int good;
                if (b.Offered && !BrandRules.CanOffer(_state, b, out good))
                {
                    b.Offered = false;
                    Tell(NotificationType.Info, "reallife_brand_note_offer_off", new Dictionary<string, string> { { "brand", b.Name } }, TabBrands, "brand:" + b.Id);
                }
                if (FranchiseRules.Arrives(_state.Seed, b, day) && _state.Applicants.Count < BrandCatalogue.ApplicantsMost)
                {
                    if (_districts.Count == 0)
                    {
                        BrandsGame.Districts(_districts);
                    }
                    Applicant a = FranchiseRules.Generate(_state.Seed, _state.NextId++, b.Id, day, _districts);
                    _state.Applicants.Add(a);
                    Tell(NotificationType.Info, "reallife_brand_note_applicant", new Dictionary<string, string>
                    {
                        { "name", a.Name }, { "brand", b.Name }, { "district", BrandsText.District(a.District) }, { "days", BrandCatalogue.ApplicantDays.ToString() }
                    }, TabApplicants, "app:" + a.Id);
                    if (Verbose)
                    {
                        Logger?.Info("Brands check (applicant " + a.Id + " for " + b.Name + "): " + a.Name + ", " + a.Temper + ", capital " + a.Capital + ", " + a.Experience + " year(s), ability " + a.Ability
                                     + ", wants " + a.District + ", waits until day " + a.Until + "; made from the save's seed and the number alone: PASS");
                    }
                }
            }
            FranchiseRules.Lapse(_state, day);
            for (int i = 0; i < _shops.Count; i++)
            {
                BrandShop shop = _shops[i];
                if (shop.Chain != null)
                {
                    ChainDay(shop, day, index, yearDays);
                }
            }
        }

        // What a franchised shop's day ended in: it closed, its agreement ran out, or the franchisee asks for something.
        private void After(Brand b, Franchise f, DayEvent what, int day)
        {
            string pick = "fr:" + f.Id;
            switch (what)
            {
                case DayEvent.Failed:
                {
                    BrandsGame.Close(f, b.Type);
                    int lost = FranchiseRules.Fail(_state, b, f, day);
                    Logger?.Info("Brands: " + f.Name + " (" + b.Name + ", " + f.District + ") has closed with " + f.Health + " in hand; recognition there fell by " + lost + " hundredths.");
                    Tell(NotificationType.Error, "reallife_brand_note_failed", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "brand", b.Name }, { "district", BrandsText.District(f.District) }, { "points", BrandsText.Lost(lost) }
                    }, TabBrands, "brand:" + b.Id);
                    break;
                }
                case DayEvent.Lapsed:
                    BrandsGame.Close(f, b.Type);
                    FranchiseRules.Lapse(_state, b, f, day);
                    Logger?.Info("Brands: the agreement with " + f.Name + " (" + b.Name + ") ran out on day " + day + " and was not renewed.");
                    Tell(NotificationType.Info, "reallife_brand_note_lapsed", new Dictionary<string, string> { { "name", f.Name }, { "brand", b.Name } }, TabAgreements, null);
                    break;
                case DayEvent.AsksHelp:
                    Tell(NotificationType.Warning, "reallife_brand_note_help", new Dictionary<string, string> { { "name", f.Name }, { "amount", BrandsText.Money(f.AskAmount) }, { "day", f.AskUntil.ToString() } }, TabAgreements, pick);
                    break;
                case DayEvent.AsksSecond:
                    Tell(NotificationType.Info, "reallife_brand_note_second", new Dictionary<string, string> { { "name", f.Name }, { "district", BrandsText.District(f.District) }, { "day", f.AskUntil.ToString() } }, TabAgreements, pick);
                    break;
                case DayEvent.AsksRenewal:
                    Tell(NotificationType.Info, "reallife_brand_note_renewal", new Dictionary<string, string> { { "name", f.Name }, { "day", f.Until.ToString() }, { "amount", BrandsText.Money(f.AskAmount) } }, TabAgreements, pick);
                    break;
                case DayEvent.Disputes:
                    f.Suit = BrandsBridges.FileClaim != null ? Math.Max(0, BrandsBridges.FileClaim(f.AskAmount)) : 0;
                    Logger?.Info("Brands: " + f.Name + " disputes the terms and claims " + f.AskAmount + (f.Suit != 0 ? "; filed with the court as claim " + f.Suit : "; no court to file with, so it waits for an answer until day " + f.AskUntil) + ".");
                    Tell(NotificationType.Warning, f.Suit != 0 ? "reallife_brand_note_dispute_court" : "reallife_brand_note_dispute", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "amount", BrandsText.Money(f.AskAmount) }, { "day", f.AskUntil.ToString() }
                    }, TabAgreements, pick);
                    break;
                case DayEvent.Bids:
                    Tell(NotificationType.Info, "reallife_brand_note_bid", new Dictionary<string, string> { { "name", f.Name }, { "brand", b.Name }, { "amount", BrandsText.Money(b.Bid) }, { "day", b.BidUntil.ToString() } }, TabBrands, "brand:" + b.Id);
                    break;
            }
        }

        // A franchised shop that was a business of the game's is no longer there (the city's own market closed it, or the building changed hands): the agreement ends with nobody at fault.
        private void Gone(Brand b, Franchise f, int day)
        {
            FranchiseRules.Lapse(_state, b, f, day);
            Logger?.Info("Brands: '" + f.GameName + "' at " + f.Place + ", the shop of " + f.Name + " (" + b.Name + "), is no longer the business it was; the agreement has ended with nobody at fault.");
            Tell(NotificationType.Warning, "reallife_brand_note_gone", new Dictionary<string, string> { { "name", f.Name }, { "brand", b.Name } }, TabAgreements, null);
        }

        // One day of a shop of the player's under a city chain: the royalty on yesterday's takings, and what the chain does about a breach.
        private void ChainDay(BrandShop shop, int day, int index, int yearDays)
        {
            Taken t = shop.Chain;
            string chain = BrandsText.Chain(t.Chain);
            int royalty = FranchiseRules.ChainRoyalty(t, shop.Takings);
            Cost(royalty, BrandsGame.ChainRoyaltyKey, shop.Name, BrandsText.Bp(BrandCatalogue.ChainRoyaltyBp[t.Chain]) + " of the " + shop.Takings + " the game's order history shows for day " + (day - 1), true);
            int step = FranchiseRules.ChainStep(t, FranchiseRules.ChainBreach(t.Chain, t.Standards, t.Prices), day, yearDays);
            var data = new Dictionary<string, string> { { "chain", chain }, { "business", shop.Name }, { "days", t.Breach.ToString() } };
            if (step == 1)
            {
                Tell(NotificationType.Warning, "reallife_brand_note_chain_warned", data, TabShops, "shop:" + shop.Place);
            }
            else if (step == 2)
            {
                int fine = FranchiseRules.ChainFine(t, Index);
                Charge(fine, BrandsGame.ChainFineKey, shop.Name, "seven days outside what " + chain + " asks");
                data["amount"] = BrandsText.Money(fine);
                Tell(NotificationType.Error, "reallife_brand_note_chain_fined", data, TabShops, "shop:" + shop.Place);
            }
            else if (step == 3)
            {
                int exit = FranchiseRules.ChainExit(t, index);
                Charge(exit, BrandsGame.ChainExitKey, shop.Name, "the chain ended the agreement after fourteen days in breach");
                _state.Taken.Remove(t);
                shop.Chain = null;
                data["amount"] = BrandsText.Money(exit);
                Tell(NotificationType.Error, "reallife_brand_note_chain_ended", data, TabShops, "shop:" + shop.Place);
            }
        }

        /// <summary>
        /// Something the city hears about has happened at a business (a failed inspection, a fire, a strike: told by
        /// Wiring/BrandLinks from those features' own events). If it trades under a brand of the player's, the brand's
        /// recognition falls in the district and a little city-wide.
        /// </summary>
        public void Heard(string place, Scandal kind)
        {
            if (!_running || _broken || _state == null)
            {
                return;
            }
            try
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                if (b == null)
                {
                    return;
                }
                int before = BrandRules.Effective(b, m.District);
                int scale = BrandsBridges.ScandalScale != null ? BrandsBridges.ScandalScale(place) : 1000;
                int lost = BrandRules.Hit(b, m.District, kind, scale);
                BrandShop shop = ShopAt(place);
                if (scale != 1000)
                {
                    Logger?.Info("Brands: the scandal at " + (shop != null ? shop.Name : place) + " spread at " + scale + " thousandths of its loss (Flare).");
                }
                Logger?.Info("Brands check (scandal, " + kind + " at " + (shop != null ? shop.Name : place) + "): " + b.Name + "'s recognition in " + m.District + " " + before + " to " + BrandRules.Effective(b, m.District)
                             + " hundredths (" + lost + " lost of the " + BrandCatalogue.ScandalLoss[(int)kind] + " such a thing costs), city-wide now " + b.City + ": PASS");
                Tell(NotificationType.Warning, "reallife_brand_note_scandal", new Dictionary<string, string>
                {
                    { "what", BrandsText.Scandal(kind) }, { "business", shop != null ? shop.Name : "?" }, { "brand", b.Name }, { "points", BrandsText.Lost(lost) }, { "district", BrandsText.District(m.District) }
                }, TabBrands, "brand:" + b.Id);
                Changed(true);
                BrandsBridges.ScandalHeard?.Invoke(place, shop != null ? shop.Name : b.Name, lost, scale);
            }
            catch (Exception e)
            {
                Fault("a scandal", e);
            }
        }

        /// <summary>Recognition a scandal took at a shop is given back (an honest answer on Flare, through Wiring/FlareLinks). Returns what came back, hundredths.</summary>
        public int Recover(string place, int points)
        {
            if (!_running || _broken || _state == null)
            {
                return 0;
            }
            try
            {
                Member m = _state.Member(place);
                Brand b = m != null ? _state.Brand(m.BrandId) : null;
                int back = b != null ? BrandRules.Regain(b, m.District, points) : 0;
                if (back > 0)
                {
                    Logger?.Info("Brands: " + b.Name + " won back " + back + " hundredths of recognition in " + m.District + " after an honest answer to the scandal.");
                    Changed(true);
                }
                return back;
            }
            catch (Exception e)
            {
                Fault("a scandal answered", e);
                return 0;
            }
        }

        // ---------------------------------------------------------------- footfall

        /// <summary>
        /// What a brand or a city chain adds to a business's footfall now, in per cent of customers: asked each time
        /// business pressure writes a business's figure (Wiring/BrandLinks). Nothing while this is not running, and
        /// nothing while the figures are held at the game's own for a save. A look-up among the player's businesses as read
        /// (Core/LookupRules ListIndex: asked on every figure of every business written).
        /// </summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            BrandsDirector d = Instance;
            if (d == null || !d._running || d._holding || d._broken || reg == null)
            {
                return 0;
            }
            BrandShop shop = d._regs.First(d._shops, reg, d._read);
            return shop != null ? shop.Asked : 0;
        }

        private static string PlaceOf(BrandShop shop)
        {
            return shop.Place;
        }

        private static BuildingRegistration RegOf(BrandShop shop)
        {
            return shop.Reg;
        }

        private void WriteFootfall(BrandShop shop, bool redraw, bool newDay)
        {
            int own = 0, written = 0;
            bool has = BrandsBridges.Footfall != null && shop.Reg != null && BrandsBridges.Footfall(shop.Reg, redraw, newDay, out own, out written);
            bool applied = has && _running && !_holding && shop.Asked != 0;
            if (Verbose && (applied != shop.Applied || shop.Asked != shop.Last))
            {
                Member m = shop.File;
                Logger?.Info("Brands check (footfall, " + shop.Name + "): " + (shop.Asked > 0 ? "+" : string.Empty) + shop.Asked + "% of customers asked for"
                             + (m != null ? " (the name +" + m.Rec + ", prices given back +" + m.Forgiven + ", near franchises -" + m.Cannibal + "; bounds " + BrandCatalogue.FootfallLeast + " to +" + BrandCatalogue.FootfallMost + ")" : string.Empty)
                             + (has ? ", promotion " + own + " to " + written + " (with whatever else the mod has there, the sum kept to +15%)" : ", the game keeps no promotion figure there: nothing written")
                             + (applied ? ": applied" : shop.Applied ? ": restored" : string.Empty) + ": PASS");
            }
            shop.Last = _running && !_holding ? shop.Asked : 0;
            shop.Applied = applied;
        }

        // Every shop where a brand adds something, or did: its figure is written. Returns how many. One batch of footfall
        // writes: a shop asked to be drawn again is drawn once, spread over the frames that follow (Core/CustomerDraw).
        private int WriteAll(bool redraw, bool newDay)
        {
            int written = 0;
            CustomerDraw.Hold("the brands' figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Asked != 0 || _shops[i].Applied)
                    {
                        WriteFootfall(_shops[i], redraw, newDay);
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

        // The shops where what is asked for is not what was last written: their figures, now.
        private void Sync()
        {
            CustomerDraw.Hold("the brands' figures");
            try
            {
                for (int i = 0; i < _shops.Count; i++)
                {
                    if (_shops[i].Asked != _shops[i].Last)
                    {
                        WriteFootfall(_shops[i], true, false);
                    }
                }
            }
            finally
            {
                CustomerDraw.Release();
            }
        }

        /// <summary>True where the game keeps a customer figure for the business, so that a brand can change its footfall.</summary>
        public bool Promoted(BrandShop shop)
        {
            return shop != null && shop.Reg != null && BrandsBridges.Promoted != null && BrandsBridges.Promoted(shop.Reg);
        }

        // ---------------------------------------------------------------- money

        /// <summary>A cost of running the brand: a transaction of its own, marked deductible. Unless 'force', false and nothing taken if the cash does not cover it.</summary>
        internal bool Cost(int amount, string key, string name, string how, bool force = false)
        {
            bool taken = BrandsGame.Take(amount, key, name, true, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Brands check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken, deductible" : " could not be taken: the cash does not cover it") + "; " + how + ": PASS");
            }
            return taken;
        }

        /// <summary>A charge that is no cost of trading (a fine, compensation, a fee for leaving): taken whatever the cash is, never deductible.</summary>
        internal void Charge(int amount, string key, string name, string how)
        {
            BrandsGame.Take(amount, key, name, false, true);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Brands check (transaction, " + key + ", " + name + "): " + amount + " taken; " + how + ": PASS");
            }
        }

        /// <summary>Money paid to the player.</summary>
        internal void Pay(int amount, string key, string name, string how)
        {
            BrandsGame.Give(amount, key, name);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Brands check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }

        // ---------------------------------------------------------------- what the player is told

        /// <summary>A notification of the game's that opens the Brands app on a tab, with something picked.</summary>
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

        /// <summary>The app has been opened: the player's businesses are read again, so that a shop opened or sold since the morning shows.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                int left;
                Read(Save.Day, out left);
                Sync();
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: what each shop gets is worked out again and written where it differs, the Planner's rows and the app's badge are made again, and the app if it is open.</summary>
        internal void Changed(bool refoot = true)
        {
            if (!_running || Save == null)
            {
                return;
            }
            if (refoot)
            {
                for (int i = 0; i < _state.Brands.Count; i++)
                {
                    _state.Brands[i].Standards = BrandRules.Standards(_state, _state.Brands[i]);
                }
                Recount();
                Sync();
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

        /// <summary>How many things wait for an answer from the player: applicants, what franchisees ask, and a bid.</summary>
        public int Waiting()
        {
            if (!_running || _state == null)
            {
                return 0;
            }
            int waiting = _state.Applicants.Count;
            for (int i = 0; i < _state.Franchises.Count; i++)
            {
                waiting += _state.Franchises[i].Ask != Ask.None ? 1 : 0;
            }
            return waiting;
        }

        // ---------------------------------------------------------------- the Planner's rows

        private void BuildAlerts(int day)
        {
            _alerts.Clear();
            for (int i = 0; i < _state.Applicants.Count; i++)
            {
                Applicant a = _state.Applicants[i];
                Brand b = _state.Brand(a.BrandId);
                Alert(1, a.Id, Math.Max(day, a.Until), BrandsText.T("reallife_brand_plan_applicant_title", "name", a.Name),
                    BrandsText.T("reallife_brand_plan_applicant_what", "brand", b != null ? b.Name : "?", "district", BrandsText.District(a.District)), null, BrandsText.T("reallife_brand_plan_applicant_missed"));
            }
            for (int i = 0; i < _state.Franchises.Count; i++)
            {
                Franchise f = _state.Franchises[i];
                Brand b = _state.Brand(f.BrandId);
                string shop = BrandsText.Shop(b, f);
                if (f.Ask == Ask.Renewal)
                {
                    Alert(2, f.Id, Math.Max(day, f.Until), BrandsText.T("reallife_brand_plan_renewal_title", "name", shop), BrandsText.T("reallife_brand_plan_renewal_what"),
                        PlannerMoney("reallife_planner_pays", f.AskAmount), BrandsText.T("reallife_brand_plan_renewal_missed"));
                }
                else if (f.Ask != Ask.None)
                {
                    Alert(3, f.Id, Math.Max(day, f.AskUntil), BrandsText.T("reallife_brand_plan_ask_title", "name", shop), BrandsText.Ask(f.Ask), null,
                        f.Suit != 0 ? BrandsText.T("reallife_brand_plan_ask_court") : BrandsText.T("reallife_brand_plan_ask_missed"));
                }
                if (FranchiseRules.InspectionDue(f, day))
                {
                    Alert(4, f.Id, day, BrandsText.T("reallife_brand_plan_inspect_title", "name", shop), BrandsText.T("reallife_brand_plan_inspect_what", "days", (day - f.SeenDay).ToString()), null, null);
                }
            }
            for (int i = 0; i < _state.Taken.Count; i++)
            {
                Taken t = _state.Taken[i];
                if (t.Breach > 0)
                {
                    BrandShop shop = ShopAt(t.Place);
                    Alert(5, CompanyDirector.PlaceNumber(t.Place), day, BrandsText.T("reallife_brand_plan_chain_title", "chain", BrandsText.Chain(t.Chain), "business", shop != null ? shop.Name : "?"),
                        BrandsText.T("reallife_brand_plan_chain_what", "days", t.Breach.ToString(), "most", BrandCatalogue.ChainEndDays.ToString()), null, null);
                }
            }
        }

        private static string PlannerMoney(string key, int amount)
        {
            return BrandsText.T(key, "amount", BrandsText.Money(amount));
        }

        private void Alert(int a, int b, int day, string title, string what, string money, string ifMissed)
        {
            if (_alerts.Count < BrandCatalogue.AlertsMost)
            {
                _alerts.Add(new BrandAlert { A = a, B = b, Day = day, Title = title, What = what, Money = money, IfMissed = ifMissed });
            }
        }

        // ---------------------------------------------------------------- for the app

        /// <summary>The shop at a place, as a walk of today's shops finds it: a look-up (Core/LookupRules ListIndex), made again with each read of them (the hourly kept prices ask it of every kept shop's flagship).</summary>
        internal BrandShop ShopAt(string place)
        {
            return _places.First(_shops, place, _read);
        }

        internal string ShopName(string place)
        {
            BrandShop shop = ShopAt(place);
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

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it. Franchised shops that are businesses of the game's own stay in the city as its own.</summary>
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
