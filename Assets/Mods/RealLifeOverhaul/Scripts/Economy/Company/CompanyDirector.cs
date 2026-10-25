using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One row the Planner shows for the company.</summary>
    public struct CompanyAlert
    {
        public int A, B;        // the Planner's id for the row: what it is and which one
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Running a business (docs/features/economy-company.md): the parts' days, the footfall and money they cause, the
    /// save and the Company app. No Update, so it costs nothing per frame; it follows the credit side's second, hour
    /// and day. A fault in a part switches that part off for the session; any other fault switches the feature off.
    /// </summary>
    public sealed partial class CompanyDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeCompany";
        public const int TabOverview = 0, TabStaff = 1, TabCover = 2, TabMarketing = 3, TabTax = 4;   // TabRota is in .Rota

        private static readonly string[] PartNames = { "staff", "insurance", "marketing", "tax", "licences" };
        private static readonly string[] AppGlyphs = { "Icon-Shop", "Icon-Building", "Icon-Neighborhood" };

        public static IModLogger Logger;
        public static CompanyDirector Instance { get; private set; }

        private readonly List<CompanyShop> _shops = new List<CompanyShop>(16);
        private readonly List<CompanyShop> _pool = new List<CompanyShop>(16);
        private readonly ListIndex<CompanyShop, string> _places = new ListIndex<CompanyShop, string>(PlaceOf, StringComparer.Ordinal);
        private readonly ListIndex<CompanyShop, BuildingRegistration> _regs =
            new ListIndex<CompanyShop, BuildingRegistration>(RegOf, ReferenceComparer<BuildingRegistration>.Instance);
        private int _read;   // reads of _shops so far: an index made before the last read is made again
        private readonly List<CompanyAlert> _alerts = new List<CompanyAlert>(16);
        private readonly bool[] _partBroken = new bool[(int)CompanyPart.Count];

        private CompanyState _state;
        private string _beforeTests;   // the state's line before the first test tool changed it; null without one
        private bool _running;
        private bool _broken;          // off for the session
        private bool _holding;         // the footfall figures are the game's own for a save until the next game hour
        private bool _gaveUp;          // the game's phone is not as expected: no app, the rest runs
        private int _level;
        private int _parts;            // the parts on at the last look, a bit each
        private int _index = CompanyCatalogue.IndexStart;
        private CycleState _cycle = CycleState.Steady;
        private CompanyApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public CompanyState State => _running ? _state : null;

        /// <summary>The player's businesses as read this morning, or when the feature started.</summary>
        public IReadOnlyList<CompanyShop> Shops => _shops;

        public IReadOnlyList<CompanyAlert> Alerts => _alerts;
        public int Level => _level;

        /// <summary>The price index: tenths of a point, 1000 at the start.</summary>
        public int Index => _index;

        public CycleState Cycle => _cycle;

        /// <summary>The game's day; 0 with no save.</summary>
        public int Today => Save != null ? Save.Day : 0;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            EconomyDirector.Follow(StartSeats.Company, _second);
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
                return;   // another city's director has started since: the state and the figures are its own
            }
            PickedRaiseGame.Keep(false);   // the game's lists outlive the city: the action goes with the director that answers it
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
            Instance = null;
        }

        /// <summary>Whether a part runs: the feature runs, the part's option is on and no fault has stopped it.</summary>
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
            mask |= Settings.EconomyCompanyHide ? 1 << (int)CompanyPart.Count : 0;   // cash off the books: no part of its own
            mask |= RotaOn ? 1 << ((int)CompanyPart.Count + 1) : 0;                  // the rota planner: no part either
            mask |= ManagersOn ? 1 << ((int)CompanyPart.Count + 2) : 0;              // nor the managers
            mask |= HierarchyOn ? 1 << ((int)CompanyPart.Count + 3) : 0;             // nor their hierarchy: its regions are read again when it is switched
            return mask;
        }

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
        partial void ManagerLoad(GameInstance save);
        partial void ManagerSave(GameInstance save);
        partial void ManagerStop();
        partial void ManagerHour(int day, int hour);
        partial void ManagerDay(int day);
        partial void HierarchyLoad(GameInstance save);
        partial void HierarchySave(GameInstance save);
        partial void HierarchyDay(int day);
        partial void HierarchyHour(int day, int hour);

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

        private static readonly int HotSecond = HotSpots.Slot("Economy: CompanyDirector.Second");
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
            if (!Settings.Economy || !Settings.EconomyCompany || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyCompany ? "the option is off" : "the credit side is not running", true, true, false);
                }
                PickedRaiseGame.Keep(false);
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            PickedRaiseGame.Keep(On(CompanyPart.Staff));   // "Give a raise" among My Employees' actions while raises can be given
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
            // The strictness or a part's switch changed: what no longer applies is taken back at once.
            _level = level;
            _parts = parts;
            StaffLevel();
            TaxLevel();
            WriteAll(Save.Day, true, false);
            RotaRead();
            ManagerRead();
            Changed();
            if (Verbose)
            {
                Logger?.Info("Company check (options): strictness " + _level + ", parts on " + PartList(parts)
                             + "; the footfall figures were written again: PASS");
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
            ManagerLoad(save);
            HierarchyLoad(save);   // after the managers: a save from before the policy keeps their own limits as their stores' rules
            BuildAlerts(today);
            Logger?.Info("Company: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". "
                         + _shops.Count + " business(es)"
                         + (fresh && added > 0 ? ", each with the licences it needs for a first term" : string.Empty) + ", "
                         + _state.Staff.Count + " employee(s), strictness " + _level
                         + ", parts on: " + PartList(_parts) + ", quarter " + _state.Tax.Quarter + " from day " + _state.Tax.QuarterFrom + ".");
        }

        // Every figure the feature changed is the game's own again. With 'keep' the state goes into the save's data first.
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
                        WriteFootfall(_shops[i], 0, true, false);   // nothing runs now, so this is the game's own figure
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
            ManagerStop();
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Company check (restored, " + why + "): " + put
                             + " footfall figure(s) put back; no wage and no figure of a business was ever written: PASS");
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

        /// <summary>A fault outside the parts: one line, every figure put back, and the feature off for the session.</summary>
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
            Logger?.Warn("Company: switched off for this session after an error in " + where
                         + ". Every business's footfall is the game's own again; the game's daily run and the rest of the economy are not affected.");
        }

        /// <summary>A fault in one part: that part is off for the session and its footfall taken back; the others go on.</summary>
        private void PartFault(CompanyPart part, string where, Exception e)
        {
            _partBroken[(int)part] = true;
            Logger?.Error(e);
            Logger?.Warn("Company: " + PartNames[(int)part] + " switched off for this session after an error in " + where
                         + ". What it added to any business's footfall is taken back; the other parts, the game's daily run and the rest of the economy are not affected.");
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
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new CompanyApp(this);
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
                    Logger?.Warn("Company: the game's phone is not built as this version expects, so the Company app is left off. "
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

        /// <summary>A notification of the game's that opens the Company app on a tab, with something picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        /// <summary>A notice that opens nothing when pressed, <paramref name="seconds"/> on screen.</summary>
        internal static void Notify(NotificationType type, string key, Dictionary<string, string> data, float seconds)
        {
            Notifications.Show(type, key, data, seconds, null, null, true, false);
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>
        /// The app has opened: the staff are read again, so a wage changed in the game's own app since the morning shows, and
        /// an offer of the company's whose employee the game's own rival offer has reached since gives way before it is drawn.
        /// </summary>
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
                if (On(CompanyPart.Staff))
                {
                    GiveWayAll(Save.Day);
                }
                RotaRead();
                ManagerRead();
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: the Planner's rows, the badge and the open app are made again.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            BuildAlerts(Save.Day);
            Badge();
            _view?.Changed();
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

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
            // A loss three days running by the game's own statements, whatever parts are on.
            for (int i = 0; i < _shops.Count; i++)
            {
                if (_shops[i].File.LossRun >= CompanyCatalogue.LossRunDays)
                {
                    Alert(1, PlaceNumber(_shops[i].File.Place), day, CompanyText.T("reallife_co_plan_loss_title", "business", _shops[i].Name),
                        CompanyText.T("reallife_co_plan_loss_what", "days", _shops[i].File.LossRun.ToString(), "amount", FinanceText.Money(-_shops[i].Profit)),
                        null, null);
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

        /// <summary>A number that is always the same for a place or an id: the second half of a Planner row's id.</summary>
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

        /// <summary>Today's business at a place: a look-up, made again with each read of them.</summary>
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
            ManagerForget();
            HierarchyForget();
        }
    }
}
