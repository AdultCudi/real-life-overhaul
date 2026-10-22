using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>One row the Planner shows about the brands, made on the day event and when the player changes something.</summary>
    public struct BrandAlert
    {
        public int A, B;        // what it is and which one: the Planner's id for the row
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Brands and franchises: keeps the feature going. Per frame: nothing, it has no Update; the credit side's second
    /// costs a dozen comparisons while idle, and its hour and day events drive the rest. The start after a load and the
    /// app's install run in seconds of their own: together they would cost a frame of about 36 ms. The footfall figures are put
    /// back before every save and whenever this stops; a fault switches the feature off for the session.
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
        private readonly ListIndex<BrandShop, string> _places = new ListIndex<BrandShop, string>(PlaceOf, StringComparer.Ordinal);
        private readonly ListIndex<BrandShop, BuildingRegistration> _regs = new ListIndex<BrandShop, BuildingRegistration>(RegOf, ReferenceComparer<BuildingRegistration>.Instance);
        private int _read;   // the reads of _shops so far: an index made before the last one is made again
        private readonly List<BrandAlert> _alerts = new List<BrandAlert>(16);
        private readonly List<string> _districts = new List<string>(16);
        private readonly List<BrandSite> _sites = new List<BrandSite>(12);

        private BrandsState _state;
        private string _beforeTests;   // the state's line before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;          // a fault: off for the session
        private bool _holding;         // the footfall figures are the game's own for a save until the next game hour
        private bool _gaveUp;          // the game's phone is not built as expected: the app is left off, the rest runs
        private int _sitesFor;         // the applicant the list of buildings was made for
        private BrandsApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;
        private Func<Franchise, string> _markName;

        public bool Running => _running;

        /// <summary>Null while the feature is not running.</summary>
        public BrandsState State => _running ? _state : null;

        /// <summary>The player's businesses as read this morning, or when the app was last opened.</summary>
        public IReadOnlyList<BrandShop> Shops => _shops;

        public IReadOnlyList<BrandAlert> Alerts => _alerts;

        /// <summary>The price index as brands count it: tenths of a point, 1000 at the start and while the city's economy is off.</summary>
        public static int Index => Prices.Level;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            BrandsGame.Logger = Logger;
            BrandsGame.Forget();   // a new city: the map's rows of the last one went with it
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _markName = MarkName;
            EconomyDirector.Follow(StartSeats.Brands, _second);   // its first second after a load has a frame of its own (Core/StartLine)
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
            // The city is unloading: nothing is written to the save here, only when the game saves.
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

        private static readonly int HotSecond = HotSpots.Slot("Economy: BrandsDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
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
                using (HotSpots.Time(HotStart))
                {
                    StartUp();
                }
                SaveHook.Keep(_onSave);
                return;   // the app goes on the phone at the next second, in a frame of its own
            }
            SaveHook.Keep(_onSave);
            if (_app == null && !_gaveUp)
            {
                using (HotSpots.Time(HotInstall))
                {
                    Install();
                }
            }
            BrandsGame.Marks(_state, true, _markName);
        }

        private static readonly int HotStart = HotSpots.Slot("Economy: BrandsDirector start (the save's brands and the shops read)");
        private static readonly int HotInstall = HotSpots.Slot("Economy: BrandsDirector app put on the phone");

        private string MarkName(Franchise f)
        {
            return BrandsText.Shop(_state.Brand(f.BrandId), f);
        }

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
            Logger?.Info("Brands: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + _state.Brands.Count + " brand(s), "
                         + _state.Members.Count + " member shop(s), " + _state.Franchises.Count + " franchised shop(s), " + _state.Applicants.Count + " applicant(s), "
                         + _state.Taken.Count + " shop(s) under a city chain" + (left > 0 ? "; " + left + " shop(s) no longer the player's were dropped" : string.Empty) + ".");
        }

        // The player's businesses and, for those under a brand or a chain, their figures; then what each asks for.
        private void Read(int day, out int left)
        {
            BrandsGame.ReadShops(_state, _shops, _pool, day, out left);
            _read++;
            for (int i = 0; i < _shops.Count; i++)
            {
                // A place the player has made another kind of business of is no shop of its brand any more.
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
            _sitesFor = 0;   // the buildings to let are read again when an applicant's page next asks
            Recount();
        }

        // Every figure the feature changed is the game's own again; with 'keep' the state goes into the save's data first.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null)
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
                        WriteFootfall(_shops[i], true, false);   // nothing is running, so nothing is added
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
            _books.Clear();   // kept prices stay as they stand; what was written is looked at afresh on the next start
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
            Logger?.Warn("Brands: switched off for this session after an error in " + where
                         + ". Every shop's footfall is the game's own again; the game's daily run and the rest of the economy are not affected.");
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new BrandsApp(this);
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
                    Logger?.Warn("Brands: the game's phone is not built as this version expects, so the Brands app is left off. "
                                 + "The rules still run, and the Planner and the notifications still say what needs an answer.");
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

        private static readonly int HotOnHour = HotSpots.Slot("Economy: BrandsDirector.OnHour");
        // Every game hour, and first thing in the game's NewDay before its daily run.
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
                    return;   // the game's run writes every footfall figure afresh in a moment, and the day event follows it
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
        // After the game's daily run, the credit side's day and the city's economy's and the company's.
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
                _state.LastDay = day;   // a jump of many days is one pass, for today
                int left;
                Read(day, out left);
                KeepRound(true);   // before the chains look at the prices
                float read = Time.realtimeSinceStartup;
                RulesDay(day);
                Recount();
                float ruled = Time.realtimeSinceStartup;
                int written = WriteAll(true, true);
                Changed(false);
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Brands check (day " + day + "): " + _state.Brands.Count + " brand(s), " + _state.Members.Count + " member shop(s)"
                                 + (left > 0 ? ", " + left + " no longer the player's" : string.Empty) + ", " + _state.Franchises.Count + " franchised shop(s), "
                                 + _state.Applicants.Count + " applicant(s), " + written + " footfall figure(s) written, " + _alerts.Count + " row(s) for the Planner; reading the game "
                                 + ((read - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, the rules "
                                 + ((ruled - read) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms, footfall and rows "
                                 + ((done - ruled) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms, "
                                 + ((done - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms in all: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // The footfall figures are the game's own until the next game hour, as the registration that holds them is saved.
        private void OnSave()
        {
            try
            {
                if (!_running || Save == null)
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

        /// <summary>A cost of running the brand, marked deductible. Unless 'force', false and nothing taken if the cash does not cover it.</summary>
        internal bool Cost(int amount, string key, string name, string how, bool force = false)
        {
            bool taken = BrandsGame.Take(amount, key, name, true, force);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Brands check (transaction, " + key + ", " + name + "): " + amount + (taken ? " taken, deductible" : " could not be taken: the cash does not cover it")
                             + "; " + how + ": PASS");
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

        internal void Pay(int amount, string key, string name, string how)
        {
            BrandsGame.Give(amount, key, name);
            if (Verbose && amount > 0)
            {
                Logger?.Info("Brands check (transaction, " + key + ", " + name + "): " + amount + " paid to the player; " + how + ": PASS");
            }
        }

        /// <summary>A notification of the game's that opens the app on a tab, with something picked.</summary>
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

        /// <summary>The app has opened: the businesses are read again, so that a shop opened or sold since the morning shows.</summary>
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

        /// <summary>Something has changed: with 'refoot' the footfall is worked out and written again; then the Planner's rows, the badge and the app.</summary>
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

        /// <summary>How many things wait for the player's answer: applicants, and what franchisees ask (a bid among them).</summary>
        public int Waiting()
        {
            if (!_running)
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

        /// <summary>The shop at a place among today's shops: a look-up in an index made again with each read.</summary>
        internal BrandShop ShopAt(string place)
        {
            return _places.First(_shops, place, _read);
        }

        internal string ShopName(string place)
        {
            BrandShop shop = ShopAt(place);
            return shop != null ? shop.Name : "?";
        }

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
