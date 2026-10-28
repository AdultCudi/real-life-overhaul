using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>
    /// The building trades' one component: the architects' practice asked for a floor (and let go with its option), its panel
    /// over the practice's desk, the trades' line in the save (<see cref="DesignBook"/>), a bribe that comes out, the employers'
    /// workplaces and what their ranks ask (<see cref="ICareerWorkplace"/>), and the tests. The site trades are jobs of the
    /// jobs' director (Jobs/Kinds/TradeJob, DesignJob, TenderJob), taken at a site's notice (<see cref="SiteWork"/>).
    /// Cost: no Update; a one-second timer (a few comparisons, a distance on the practice's floor).
    /// </summary>
    public sealed partial class TradesDirector : MonoBehaviour, ICareerWorkplace
    {
        private const float DeskReach = 2.6f;   // metres from the practice's desk within which its panel stands over it
        private const float PanelUp = 1.9f;     // metres
        private const float SiteFar = 6000f;    // metres: "show me where" finds the nearest site in the whole city

        public static IModLogger Logger;
        public static TradesDirector Instance { get; private set; }

        private readonly TradeOffice _office = new TradeOffice();
        private readonly TradeSite _site = new TradeSite();
        private readonly WorldPanel _panel = new WorldPanel { Name = "practice" };
        private Action _onDesign, _onFrame, _onTender, _onRota;
        private Action _onSave;
        private GameInstance _save;
        private bool _asked;
        private bool _broken;
        private int _panelState = int.MinValue;
        private int _day = -1;

        /// <summary>The career's doors of the building trades, as a value Wiring hands to Jobs/CareerLinks.</summary>
        public static ICareerWorkplace Workplace => Instance;

        private void Awake()
        {
            Instance = this;
            DesignBook.Reset();
            _onSave = OnSave;
            _onDesign = () => Desk(TradesBridges.Holds(TradeCatalogue.Designing) ? JobCatalogue.Architect : JobCatalogue.Draughting);
            _onFrame = () => Desk(JobCatalogue.Engineer);
            _onTender = () => Desk(JobCatalogue.Estimator);
            _onRota = Rota;
        }

        private void Start()
        {
            StartCoroutine(Guard.Routine(Look()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            _panel.Hide();
            if (_asked)
            {
                FloorsDirector.Release(_office, "the city is going");
                _asked = false;
            }
            DesignBook.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Look()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                if (!_broken)
                {
                    try
                    {
                        Tick();
                    }
                    catch (Exception e)
                    {
                        Fail(e);
                    }
                }
                yield return second;
            }
        }

        private void Fail(Exception e)
        {
            Logger?.Error(e);
            Logger?.Warn("Trades: switched off for this session after the error above; the practice's floor is let go and its panel taken down.");
            _broken = true;
            _panel.Hide();
            if (_asked)
            {
                FloorsDirector.Release(_office, "the trades stood down");
                _asked = false;
            }
        }

        private void Tick()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ModState.Loaded)
            {
                return;
            }
            SaveHook.Keep(_onSave);
            if (!ReferenceEquals(save, _save))
            {
                Load(save);
            }
            bool office = Settings.Trades && Settings.TradesOffice;
            if (office != _asked)
            {
                _asked = office;
                if (office)
                {
                    FloorsDirector.Ask(_office);
                    Logger?.Info("Trades: the architects' practice asks for a floor of a building the mod holds.");
                }
                else
                {
                    FloorsDirector.Release(_office, "its option is off");
                }
            }
            int day = TimeHelper.CurrentDay;
            if (day != _day)
            {
                _day = day;
                Bribe(day);
            }
            Practice();
        }

        private void Load(GameInstance save)
        {
            _save = save;
            string line = null;
            save.modData?.TryGetValue(DesignBook.Key, out line);
            if (!DesignBook.Read(line))
            {
                SaveLines.Unreadable(save.modData, DesignBook.Key, "it could not be read");   // kept as it was read (Core/SaveLines)
            }
            Logger?.Info("Trades: ready; " + DesignBook.Count + " design(s) in the book, " + DesignBook.Waiting + " brief(s) from builders waiting"
                         + (DesignBook.BribeDay >= 0 ? ", a bribe of day " + DesignBook.BribeDay + " still to come out or be forgotten" : string.Empty) + ".");
        }

        private void OnSave()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || !ReferenceEquals(save, _save) || !DesignBook.Loaded)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                if (DesignBook.Count > 0 || DesignBook.Waiting > 0 || DesignBook.OwnCount > 0 || DesignBook.BribeDay >= 0)
                {
                    save.modData[DesignBook.Key] = DesignBook.Write();
                }
                else
                {
                    save.modData.Remove(DesignBook.Key);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The save's trades are forgotten (Save tools: reset the mod's data).</summary>
        public void Forget()
        {
            DesignBook.Read(null);
        }

        // A bribe taken some days ago: it comes out, or is forgotten. Decided by the site and the day, the same on every look.
        private void Bribe(int day)
        {
            if (!DesignBook.BribeDue(day))
            {
                return;
            }
            int amount = DesignBook.BribeAmount;
            bool caught = TradeRules.BribeCaught(DesignBook.BribeSite, DesignBook.BribeDay);
            DesignBook.BribeDone();
            if (!caught)
            {
                Logger?.Info("Trades: the bribe taken was never found out.");
                return;
            }
            int fine = amount * 2;
            RealLifeOverhaul.Economy.BizBooks.Book(-fine, new TransactionInfo("reallife_transaction_trades_bribe_fine"), null, null, force: true);
            bool dismissed = CareerLinks.EmployerId == CareerCatalogue.Inspection;
            if (dismissed)
            {
                CareerState.Leave(day, CareerReference.MarkedDown);   // the city's building control lets its inspector go
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Trades check (bribe): the bribe of day " + (day - DesignBook.BribeComesOutDays) + " came out: fined twice it (" + fine + ")"
                             + (dismissed ? ", the inspector let go by the city with the reference marked down" : string.Empty) + ": PASS.");
            }
            var data = new Dictionary<string, string>(1) { { "amount", JobText.Money(fine) } };
            Notifications.Show(NotificationType.Error, dismissed ? "reallife_trades_bribe_out_dismissed" : "reallife_trades_bribe_out", data, 14f);
            Logger?.Info("Trades: the bribe came out: fined " + fine + (dismissed ? ", and dismissed by the city's building control" : string.Empty) + ".");
        }

        // The practice's panel over its desk, while the player is on its floor with no job on.
        private void Practice()
        {
            if (!Settings.Trades || !Settings.TradesOffice || JobDirector.OnShift || !TradeOffice.Desk(out Vector3 desk, out _)
                || JobSpots.Flat(PlayerHelper.GetPosition(), desk) > DeskReach || ThirdPersonCamera.IsUiOpen())
            {
                HidePanel();
                return;
            }
            bool rota = CareerLinks.EmployerId == CareerCatalogue.Architects && CareerLinks.ShiftDueToday(TimeHelper.CurrentDay);
            bool architect = TradesBridges.Holds(TradeCatalogue.Designing);
            int state = (rota ? 1 : 0) | (architect ? 2 : 0) | (DesignBook.Waiting << 2);
            if (state != _panelState)
            {
                _panelState = state;
                _panel.Lines(JobText.T("reallife_career_architects_name"),
                    DesignBook.Waiting > 0 ? JobText.T("reallife_trades_office_briefs", "count", DesignBook.Waiting.ToString()) : JobText.T("reallife_trades_office_line"),
                    null, WorldPanel.White());
                _panel.Button(0, architect ? "reallife_trades_office_design" : "reallife_trades_office_draught", _onDesign, true, true);
                _panel.Button(1, "reallife_trades_office_frame", _onFrame, true, false);
                _panel.Button(2, "reallife_trades_office_tender", _onTender, true, false);
                _panel.Button(3, rota ? "reallife_trades_office_clock_on" : "reallife_trades_office_speak", _onRota, true, false);
                _panel.For(desk);
                _panel.Position = desk + Vector3.up * PanelUp;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Trades check (practice panel): the game's own panel over the practice's desk offers " + (architect ? "a design" : "draughting")
                                 + ", a frame, a tender and " + (rota ? "today's shift" : "the partner") + ": PASS.");
                }
            }
            _panel.Show();
        }

        private void HidePanel()
        {
            if (_panelState != int.MinValue)
            {
                _panelState = int.MinValue;
                _panel.Hide();
            }
        }

        // A desk trade taken at the practice: the jobs' director starts it where the player stands.
        private void Desk(string id)
        {
            try
            {
                JobDirector director = JobDirector.Instance;
                JobDef def = JobCatalogue.Find(id);
                if (director == null || def == null)
                {
                    JobText.Toast(NotificationType.Warning, "reallife_job_no_off");
                    return;
                }
                TradeDef trade = TradeCatalogue.Find(id);
                if (trade != null && !TradesBridges.Holds(trade.Needs))
                {
                    JobText.Toast(NotificationType.Info, trade.Needs.LacksKey, 8f);
                    return;
                }
                HidePanel();
                if (!director.Take(def, out string reason))
                {
                    JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The practice's own: today's shift clocked on to, or the partner spoken to (signing on, a review).
        private void Rota()
        {
            try
            {
                JobDirector director = JobDirector.Instance;
                if (director == null || director.Career == null)
                {
                    return;
                }
                HidePanel();
                if (CareerLinks.EmployerId == CareerCatalogue.Architects && CareerLinks.ShiftDueToday(TimeHelper.CurrentDay))
                {
                    if (!director.Career.ClockOn(out string reason))
                    {
                        JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
                    }
                    return;
                }
                CareerPhone.OpenInPerson(CareerCatalogue.IndexOf(CareerCatalogue.Architects));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A construction company has put a brief at the practice (TradesBridges.Commission): the player is told once.</summary>
        public void CommissionAdded(string projectId)
        {
            Notifications.Show(NotificationType.Info, "reallife_trades_commission", new Dictionary<string, string>(1) { { "count", DesignBook.Waiting.ToString() } }, 10f);
            Logger?.Info("Trades check (commission): a brief for project '" + projectId + "' is at the practice, " + DesignBook.Waiting + " waiting: PASS.");
        }

        // ---- the career's doors (Jobs/CareerLinks) ----

        bool ICareerWorkplace.Depot(string employerId, Vector3 near, out Vector3 at, out string address)
        {
            at = default;
            address = null;
            if (!Settings.Trades)
            {
                return false;
            }
            if (employerId == CareerCatalogue.Architects)
            {
                if (!Settings.TradesOffice || !FloorsDirector.Where(_office, out FloorWhere where))
                {
                    return false;
                }
                at = TradeOffice.Desk(out Vector3 desk, out _) ? desk : where.Door;
                address = TradesGame.Address(where.Street, where.Number);
                return true;
            }
            if (employerId == CareerCatalogue.Builders || employerId == CareerCatalogue.Inspection)
            {
                if (!TradesBridges.NearestSite(near, SiteFar, _site))
                {
                    return false;
                }
                at = _site.At;
                address = _site.Address;
                return true;
            }
            return false;
        }

        string ICareerWorkplace.Lacks(string needs)
        {
            if (!Settings.Trades)
            {
                return "reallife_career_needs_trades_off";
            }
            TradeRole role = TradeCatalogue.Role(needs);
            if (role == null || TradesBridges.Holds(role.Needs))
            {
                return null;
            }
            return role.Needs.LacksKey;
        }
    }
}
