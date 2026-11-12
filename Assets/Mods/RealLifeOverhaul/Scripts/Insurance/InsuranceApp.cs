using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>The Insurance app's tables as data (GameUi.TableSpec).</summary>
    public static class InsuranceColumns
    {
        public static readonly TableSpec Off = new TableSpec("Insurance", "Off").Column("reallife_ins_col_name", 600f, TableSort.None, flexible: true);

        /// <summary>A cover's tab: 0 what is insured, 1 the cover, 2 its status, 3 the premium.</summary>
        public static readonly TableSpec Covers = new TableSpec("Insurance", "Covers")
            .Column("reallife_ins_col_name", 420f, TableSort.Text, flexible: true)
            .Column("reallife_ins_col_cover", 280f, TableSort.Text)
            .Column("reallife_ins_col_status", 240f, TableSort.Text)
            .Column("reallife_ins_col_premium", 240f, TableSort.Money, sums: true, right: true)
            .Empty("reallife_ins_empty");

        /// <summary>Claims: 0 the claim, 1 its day, 2 the bill, 3 what the insurer paid.</summary>
        public static readonly TableSpec Claims = new TableSpec("Insurance", "Claims")
            .Column("reallife_ins_col_claim", 480f, TableSort.Text, flexible: true)
            .Column("reallife_ins_col_day", 160f, TableSort.Number)
            .Column("reallife_ins_col_bill", 240f, TableSort.Money, sums: true)
            .Column("reallife_ins_col_paid", 240f, TableSort.Money, sums: true, right: true)
            .Empty("reallife_ins_claims_none");
    }

    /// <summary>
    /// The Insurance app in the game's frame (GameUi.AppShell), for a player with the RLO interface off: Cars, Health, Homes, Businesses and Claims tabs over a table,
    /// and the white box for what is picked, with its tier, its terms and the buttons. The same facts and the same calls as the kit's page (Ui/Ventures/InsuranceLive).
    /// Nothing runs while it is closed.
    /// </summary>
    public sealed class InsuranceApp
    {
        private const string GuideTitle = "reallife_meta_title_insurance";
        private static readonly string[] TabKeys = { "reallife_ins_tab_cars", "reallife_ins_tab_health", "reallife_ins_tab_homes", "reallife_ins_tab_business", "reallife_ins_tab_claims" };

        private readonly InsuranceFacts _facts = new InsuranceFacts();
        private readonly List<TableRow> _rows = new List<TableRow>(12);
        private readonly List<TableRow> _pool = new List<TableRow>(12);
        private AppShell _shell;
        private DataTable _table;
        private int _tab, _pick, _tier = -1, _insurer = InsuranceCatalogue.StandardTier;
        private bool _writing;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("insurance: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, InsuranceDirector.AppId);
            _table = DataTable.On(_shell, "Insurance");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(InsuranceColumns.Off))
            {
                return null;
            }
            _table.Money = amount => InsuranceGame.Money((long)amount);
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        public void Opened()
        {
            int want = InsuranceDirector.TakeWantedTab();
            if (want >= 0)
            {
                _tab = want;
                _pick = 0;
                _tier = -1;
            }
            Refresh();
        }

        public void Closed()
        {
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _pick = 0;
            _tier = -1;
            Refresh();
        }

        private void RowClicked(TableRow row)
        {
            if (row != null && row.Pick && row.Index != _pick)
            {
                _pick = row.Index;
                _tier = -1;
            }
            Refresh();
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            row.Reset(id);
            row.Pick = true;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private static string T(string key) => key.GetLocalization();

        private static string Share(int thousandths) => RealLifeOverhaul.Ui.VentWords.Share(thousandths * 10);

        private static string Terms(int share, long excess)
        {
            return share <= 0 ? T("reallife_ins_not_covered") : excess > 0 ? InsuranceText.Fill("reallife_ins_terms", "share", Share(share), "excess", InsuranceGame.Money(excess)) : Share(share);
        }

        private static string Status(bool on, int waitUntil, int graceUntil)
        {
            return !on ? T("reallife_ins_sub_none") : graceUntil > 0 ? T("reallife_ins_chip_grace") : waitUntil > 0 ? T("reallife_ins_chip_wait") : T("reallife_ins_chip_on");
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                InsuranceDirector d = InsuranceDirector.Instance;
                bool ready = d != null && d.Facts(_facts);
                TableSpec spec = InsuranceColumns.Off;
                UiClone.Words(_shell.Header, InsuranceText.Fill("reallife_ins_app_head", "amount", InsuranceGame.Money(ready ? _facts.PremiumsMonth : 0)));
                if (!ready)
                {
                    box.Title(T("reallife_ins_off_title"));
                    box.Note(T("reallife_ins_off_body"));
                }
                else
                {
                    switch (_tab)
                    {
                        case RealLifeOverhaul.Ui.InsurePages.TabHealth: spec = Health(box); break;
                        case RealLifeOverhaul.Ui.InsurePages.TabHomes: spec = Homes(box); break;
                        case RealLifeOverhaul.Ui.InsurePages.TabBusiness: spec = Business(box); break;
                        case RealLifeOverhaul.Ui.InsurePages.TabClaims: spec = Claims(box); break;
                        default: spec = Cars(box); break;
                    }
                    Meta.Guide.Point(box, GuideTitle);
                }
                box.Done();
                _table.Show(spec, _rows, null);
            }
            finally
            {
                _writing = false;
            }
        }

        // The tier's row of the box: the game's selector where the box has one.
        private void TierRow(InfoBox box, string[] keys, int tier)
        {
            if (!box.HasSelector || !box.Selector(InsuranceCatalogue.Tiers, tier, i => T(keys[InsuranceCatalogue.Tier(i)]), i => { _tier = i; Refresh(); }))
            {
                box.Line(T(keys[InsuranceCatalogue.Tier(tier)]));   // a box with no selector shows the tier it would buy
            }
        }

        private TableSpec Cars(InfoBox box)
        {
            InsuranceFacts f = _facts;
            for (int i = 0; i < f.Cars.Count; i++)
            {
                CarCoverFact c = f.Cars[i];
                TableRow row = Row("c" + i);
                row.Set(0, c.Name);
                row.Set(1, c.Insured ? InsuranceText.CarTier(c.Tier) : string.Empty);
                row.Set(2, Status(c.Insured, c.WaitUntil, c.GraceUntil), c.Insured && c.WaitUntil == 0 && c.GraceUntil == 0 ? TableRow.Green : TableRow.Yellow);
                row.Set(3, InsuranceGame.Money(c.Premium[c.Tier]), c.Insured ? c.Premium[c.Tier] : 0);
            }
            if (f.Cars.Count == 0)
            {
                box.Title(T("reallife_ins_sec_cars"));
                box.Note(T(f.CarsOn ? "reallife_ins_cars_none" : "reallife_ins_cars_off"));
                return InsuranceColumns.Covers;
            }
            CarCoverFact p = f.Cars[Mathf.Clamp(_pick, 0, f.Cars.Count - 1)];
            int tier = InsuranceCatalogue.Tier(_tier < 0 ? p.Tier : _tier);
            string id = p.Id;
            box.Title(p.Name);
            box.Line(Status(p.Insured, p.WaitUntil, p.GraceUntil), p.Insured && p.GraceUntil == 0 ? CellTone.Plain : CellTone.Yellow);
            TierRow(box, InsuranceCatalogue.CarTierKeys, tier);
            box.RowWords(T("reallife_ins_row_premium"), InsuranceGame.Money(p.Premium[tier]));
            box.RowWords(T("reallife_ins_row_repair"), Terms(InsuranceCatalogue.CarRepairShare[tier], InsuranceRules.At(InsuranceCatalogue.CarRepairExcess[tier], f.Level)));
            box.RowWords(T("reallife_ins_row_liability"), Terms(InsuranceCatalogue.CarLiabilityShare[tier], InsuranceRules.At(InsuranceCatalogue.CarLiabilityExcess[tier], f.Level)));
            box.RowWords(T("reallife_ins_row_tow"), Terms(InsuranceCatalogue.CarTowShare[tier], 0));
            box.RowWords(T("reallife_ins_row_cap"), InsuranceText.Fill("reallife_ins_cap_value", "amount", InsuranceGame.Money(InsuranceRules.At(InsuranceCatalogue.CarYearCap[tier], f.Level)),
                "n", InsuranceCatalogue.CarYearClaims[tier].ToString()));
            box.RowWords(T("reallife_ins_row_noclaim"), Share(p.NoClaim));
            box.RowWords(T("reallife_ins_row_paid_year"), InsuranceText.Fill("reallife_ins_cap_value", "amount", InsuranceGame.Money(p.PaidYear), "n", p.ClaimsYear.ToString()));
            Transform buttons = box.ButtonRow();
            if (!p.Insured)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_insure", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.BuyCar(id, tier); Refresh(); });
            }
            else
            {
                if (tier != p.Tier)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_change", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.ChangeCar(id, tier); _tier = -1; Refresh(); });
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ins_btn_cancel", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.CancelCar(id); Refresh(); });
            }
            if (p.Insured && tier > p.Tier)
            {
                box.Line(p.Sound ? InsuranceText.Fill("reallife_ins_change_costs", "amount", InsuranceGame.Money(p.Change[tier])) : T("reallife_ins_car_unsound"),
                    p.Sound ? CellTone.Plain : CellTone.Yellow);
            }
            box.Note(InsuranceText.Fill("reallife_ins_car_foot", "days", InsuranceCatalogue.CarWaitDays.ToString(), "grace", InsuranceCatalogue.GraceDays.ToString()));
            return InsuranceColumns.Covers;
        }

        private TableSpec Health(InfoBox box)
        {
            HealthCoverFact h = _facts.Health;
            if (!h.On)
            {
                box.Title(T("reallife_ins_sec_plans"));
                box.Note(T("reallife_ins_health_off"));
                return InsuranceColumns.Covers;
            }
            for (int i = 0; i < InsuranceCatalogue.Tiers; i++)
            {
                TableRow row = Row("h" + i);
                row.Set(0, h.Names[i]);
                row.Set(1, InsuranceText.Fill("reallife_ins_plan_sub", "hospital", Share(h.HospitalShare[i]), "doctor", Share(h.DoctorShare[i])));
                row.Set(2, h.Plan == i + 1 ? T("reallife_ins_plan_yours") : string.Empty, h.Plan == i + 1 ? TableRow.Green : TableRow.Plain);
                row.Set(3, InsuranceGame.Money(h.Premium[i]), h.Plan == i + 1 ? h.Premium[i] : 0);
            }
            int k = Mathf.Clamp(_pick, 0, InsuranceCatalogue.Tiers - 1);
            bool yours = h.Plan == k + 1;
            box.Title(h.Names[k]);
            box.RowWords(T("reallife_ins_row_h_premium"), InsuranceGame.Money(h.Premium[k]));
            box.RowWords(T("reallife_ins_row_h_hospital"), Share(h.HospitalShare[k]));
            box.RowWords(T("reallife_ins_row_h_excess"), InsuranceGame.Money(h.Excess[k]));
            box.RowWords(T("reallife_ins_row_h_doctor"), Share(h.DoctorShare[k]));
            box.RowWords(T("reallife_ins_row_cap"), InsuranceGame.Money(h.YearCap[k]));
            if (yours)
            {
                box.RowWords(T("reallife_ins_row_next"), InsuranceText.Fill("reallife_ins_day_value", "day", h.NextDue.ToString()));
                box.RowWords(T("reallife_ins_row_paid_year"), InsuranceGame.Money(h.PaidYear));
            }
            Transform buttons = box.ButtonRow();
            if (!yours)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_take_plan", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.TakeHealth(k + 1); Refresh(); });
            }
            if (h.Plan > 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ins_btn_cancel_plan", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.CancelHealth(); Refresh(); });
            }
            box.Note(T("reallife_ins_health_foot"));
            return InsuranceColumns.Covers;
        }

        private TableSpec Homes(InfoBox box)
        {
            InsuranceFacts f = _facts;
            for (int i = 0; i < f.Homes.Count; i++)
            {
                HomeCoverFact c = f.Homes[i];
                TableRow row = Row("m" + i);
                row.Set(0, c.Name);
                row.Set(1, c.Active ? InsuranceText.HomeTier(c.Tier) : string.Empty);
                row.Set(2, Status(c.Active, c.WaitUntil, c.GraceUntil), c.Active && c.WaitUntil == 0 && c.GraceUntil == 0 ? TableRow.Green : TableRow.Yellow);
                row.Set(3, InsuranceGame.Money(c.Premium[c.Tier]), c.Active ? c.Premium[c.Tier] : 0);
            }
            if (f.Homes.Count == 0)
            {
                box.Title(T("reallife_ins_sec_homes"));
                box.Note(T("reallife_ins_homes_none"));
                return InsuranceColumns.Covers;
            }
            HomeCoverFact p = f.Homes[Mathf.Clamp(_pick, 0, f.Homes.Count - 1)];
            int tier = InsuranceCatalogue.Tier(_tier < 0 ? p.Tier : _tier);
            string place = p.Place;
            box.Title(p.Name);
            box.Line(Status(p.Active, p.WaitUntil, p.GraceUntil), p.Active && p.GraceUntil == 0 ? CellTone.Plain : CellTone.Yellow);
            TierRow(box, InsuranceCatalogue.HomeTierKeys, tier);
            box.RowWords(T("reallife_ins_row_premium"), InsuranceGame.Money(p.Premium[tier]));
            box.RowWords(T("reallife_ins_row_home_repair"), Terms(InsuranceCatalogue.HomeRepairShare[tier], InsuranceRules.At(InsuranceCatalogue.HomeExcess[tier], f.Level)));
            box.RowWords(T("reallife_ins_row_belongings"), Terms(InsuranceCatalogue.BelongingsShare[tier], InsuranceRules.At(InsuranceCatalogue.HomeExcess[tier], f.Level)));
            box.RowWords(T("reallife_ins_row_cap"), InsuranceText.Fill("reallife_ins_cap_value", "amount", InsuranceGame.Money(InsuranceRules.At(InsuranceCatalogue.HomeYearCap[tier], f.Level)),
                "n", InsuranceCatalogue.HomeYearClaims[tier].ToString()));
            if (p.Active)
            {
                box.RowWords(T("reallife_ins_row_next"), InsuranceText.Fill("reallife_ins_day_value", "day", p.NextDue.ToString()));
            }
            box.RowWords(T("reallife_ins_row_noclaim"), Share(p.NoClaim));
            box.RowWords(T("reallife_ins_row_paid_year"), InsuranceText.Fill("reallife_ins_cap_value", "amount", InsuranceGame.Money(p.PaidYear), "n", p.ClaimsYear.ToString()));
            Transform buttons = box.ButtonRow();
            if (!p.Active)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_insure", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.BuyHome(place, tier); Refresh(); });
            }
            else
            {
                if (tier != p.Tier)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_change", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.ChangeHome(place, tier); _tier = -1; Refresh(); });
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ins_btn_cancel", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.CancelHome(place); Refresh(); });
            }
            if (p.Active && tier > p.Tier)
            {
                box.Line(InsuranceText.Fill("reallife_ins_change_costs", "amount", InsuranceGame.Money(p.Change[tier])));
            }
            box.Note(InsuranceText.Fill("reallife_ins_home_foot", "days", InsuranceCatalogue.HomeWaitDays.ToString(), "grace", InsuranceCatalogue.GraceDays.ToString()));
            return InsuranceColumns.Covers;
        }

        private TableSpec Business(InfoBox box)
        {
            InsuranceFacts f = _facts;
            for (int i = 0; i < f.Business.Count; i++)
            {
                BizCoverFact c = f.Business[i];
                TableRow row = Row("b" + i);
                row.Set(0, c.Name);
                row.Set(1, c.KindName);
                row.Set(2, c.Active ? f.Insurers[InsuranceCatalogue.Tier(c.Insurer)] : T("reallife_ins_sub_none"), c.Active ? TableRow.Green : TableRow.Yellow);
                row.Set(3, c.Active ? InsuranceText.Fill("reallife_ins_day", "amount", InsuranceGame.Money(c.Premium)) : string.Empty, c.Active ? c.Premium : 0);
            }
            if (f.Business.Count == 0)
            {
                box.Title(T("reallife_ins_sec_business"));
                box.Note(T(f.BusinessOn ? "reallife_ins_business_none" : "reallife_ins_business_off"));
                return InsuranceColumns.Covers;
            }
            BizCoverFact p = f.Business[Mathf.Clamp(_pick, 0, f.Business.Count - 1)];
            string place = p.Place;
            int kind = p.Kind, insurer = p.Active ? InsuranceCatalogue.Tier(p.Insurer) : InsuranceCatalogue.Tier(_insurer);
            box.Title(InsuranceText.Fill("reallife_ins_biz_title", "cover", p.KindName, "business", p.Name));
            if (p.Active)
            {
                box.RowWords(T("reallife_ins_row_insurer"), f.Insurers[insurer]);
                box.RowWords(T("reallife_ins_row_b_premium"), InsuranceText.Fill("reallife_ins_day", "amount", InsuranceGame.Money(p.Premium)));
                box.RowWords(T("reallife_ins_row_b_term"), InsuranceText.Fill("reallife_ins_day_value", "day", p.Until.ToString()));
                box.RowWords(T("reallife_ins_row_b_claims"), p.Claims.ToString());
            }
            else
            {
                if (!box.HasSelector || !box.Selector(InsuranceCatalogue.Tiers, insurer, i => f.Insurers[InsuranceCatalogue.Tier(i)], i => { _insurer = i; Refresh(); }))
                {
                    box.Line(f.Insurers[insurer]);
                }
                box.RowWords(T("reallife_ins_row_b_premium"), InsuranceText.Fill("reallife_ins_day", "amount", InsuranceGame.Money(p.Quote[insurer])));
            }
            box.RowWords(T("reallife_ins_row_b_excess"), InsuranceText.Fill("reallife_ins_b_excess_value", "share", Share(p.ExcessShare[insurer]), "least",
                InsuranceGame.Money(p.ExcessLeast[insurer])));
            box.RowWords(T("reallife_ins_row_b_most"), InsuranceGame.Money(p.Most));
            box.RowWords(T("reallife_ins_row_b_pays"), InsuranceText.Fill("reallife_ins_days_value", "n", p.PayDays[insurer].ToString()));
            Transform buttons = box.ButtonRow();
            if (p.Active)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ins_btn_cancel", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.CancelBusiness(place, kind); Refresh(); });
            }
            else if (p.Writes[insurer])
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ins_btn_buy_cover", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.BuyBusiness(place, kind, insurer); Refresh(); });
            }
            else
            {
                box.Line(T("reallife_ins_biz_refuses"), CellTone.Yellow);
            }
            box.Note(T("reallife_ins_biz_foot"));
            return InsuranceColumns.Covers;
        }

        private TableSpec Claims(InfoBox box)
        {
            InsuranceFacts f = _facts;
            for (int i = 0; i < f.Claims.Count; i++)
            {
                ClaimFact c = f.Claims[i];
                TableRow row = Row("k" + i);
                row.Set(0, ClaimTitle(c));
                row.Set(1, c.Day.ToString(), c.Day);
                row.Set(2, InsuranceGame.Money(c.Loss), c.Loss);
                row.Set(3, InsuranceGame.Money(c.Payout), c.Payout, c.Stage == 0 ? TableRow.Green : c.Stage == 2 ? TableRow.Plain : TableRow.Yellow);
            }
            box.Title(T("reallife_ins_sec_how"));
            if (f.Claims.Count > 0)
            {
                ClaimFact p = f.Claims[Mathf.Clamp(_pick, 0, f.Claims.Count - 1)];
                int id = p.Id;
                box.Line(ClaimTitle(p));
                box.RowWords(T("reallife_ins_row_c_loss"), InsuranceGame.Money(p.Loss));
                box.RowWords(T("reallife_ins_row_c_part"), InsuranceGame.Money(p.Loss - p.Payout));
                box.RowWords(T("reallife_ins_row_c_pays"), InsuranceGame.Money(p.Payout));
                if (p.Stage == 1 && id >= 0)
                {
                    box.Line(InsuranceText.Fill("reallife_ins_chip_put_in", "day", p.StageDay.ToString()), CellTone.Yellow);
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ins_btn_file", () => { RealLifeOverhaul.Ui.InsuranceLive.Instance.FileClaim(id); Refresh(); });
                }
                else if (p.Stage == 2)
                {
                    box.Line(InsuranceText.Fill("reallife_ins_chip_filed", "day", p.StageDay.ToString()));
                }
            }
            box.Note(T("reallife_ins_claims_how"));
            return InsuranceColumns.Claims;
        }

        private static string ClaimTitle(ClaimFact c)
        {
            string name = c.KindName ?? T(InsuranceCatalogue.ClaimKeys[InsuranceCatalogue.Clamp(c.Kind, 0, InsuranceCatalogue.ClaimKeys.Length - 1)]);
            return InsuranceText.Fill("reallife_ins_claim_title", "kind", name, "what", c.What ?? string.Empty);
        }
    }
}
