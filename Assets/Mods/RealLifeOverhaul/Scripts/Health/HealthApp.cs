using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Health
{
    /// <summary>The Health app's tables as data (GameUi.TableSpec).</summary>
    public static class HealthColumns
    {
        /// <summary>A list of facts: 0 what, 1 how it stands.</summary>
        public static readonly TableSpec Lines = new TableSpec("Health", "Lines")
            .Column("reallife_hc_col_what", 620f, TableSort.None, flexible: true)
            .Column("reallife_hc_col_value", 420f, TableSort.None, right: true);

        /// <summary>Plans: 0 plan, 1 a month, 2 of a hospital stay, 3 of the doctor.</summary>
        public static readonly TableSpec Plans = new TableSpec("Health", "Plans")
            .Column("reallife_hc_col_plan", 380f, TableSort.Text, flexible: true)
            .Column("reallife_hc_col_month", 240f, TableSort.Money)
            .Column("reallife_hc_col_hospital", 220f, TableSort.Number)
            .Column("reallife_hc_col_doctor", 220f, TableSort.Number, right: true);
    }

    /// <summary>
    /// The Health app in the game's frame (GameUi.AppShell), for a player with the RLO interface off: You, Insurance, Bills and leave and The city tabs over a table, and the
    /// white box with what to do. The same facts and the same director calls as the kit's page. Nothing runs while it is closed.
    /// </summary>
    public sealed class HealthApp
    {
        private static readonly string[] TabKeys = { "reallife_hc_tab_you", "reallife_hc_tab_cover", "reallife_hc_tab_bills", "reallife_hc_tab_city" };

        private readonly HealthFacts _facts = new HealthFacts();
        private readonly List<TableRow> _rows = new List<TableRow>(12);
        private readonly List<TableRow> _pool = new List<TableRow>(12);
        private AppShell _shell;
        private DataTable _table;
        private int _tab, _pick = 1;
        private bool _writing;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("health: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, HealthDirector.AppId);
            _table = DataTable.On(_shell, "Health");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(HealthColumns.Lines))
            {
                return null;
            }
            _table.Money = amount => HealthGame.Money((long)amount);
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
            Refresh();
        }

        public void Closed()
        {
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            Refresh();
        }

        private void RowClicked(TableRow row)
        {
            if (row != null && row.Pick && _tab == HealthPages.TabCover)
            {
                _pick = row.Index;
            }
            Refresh();
        }

        private TableRow Row(string id, bool pick = false)
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
            row.Pick = pick;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private void Line(string key, string value)
        {
            TableRow row = Row(key);
            row.Set(0, HealthText.Fill(key));
            row.Set(1, value);
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
                HealthDirector d = HealthDirector.Instance;
                bool ready = d != null && d.Facts(_facts);
                TableSpec spec = HealthColumns.Lines;
                UiClone.Words(_shell.Header, HealthText.Fill("reallife_hc_app_head", "owed", HealthGame.Money(_facts.Owed)));
                if (!ready)
                {
                    box.Title(HealthText.Fill("reallife_hc_off_title"));
                    box.Note(HealthText.Fill("reallife_hc_off_body"));
                }
                else
                {
                    switch (_tab)
                    {
                        case HealthPages.TabCover: spec = Cover(box); break;
                        case HealthPages.TabBills: Bills(box); break;
                        case HealthPages.TabCity: City(box); break;
                        default: You(box); break;
                    }
                }
                box.Done();
                _table.Show(spec, _rows, null);
            }
            finally
            {
                _writing = false;
            }
        }

        private void You(InfoBox box)
        {
            HealthFacts f = _facts;
            string illness = HealthText.Name(f.Kind);
            Line("reallife_hc_fig_you", f.InHospital ? HealthText.Fill("reallife_hc_now_hospital") : f.Kind == Condition.None ? HealthText.Fill("reallife_hc_now_well") : illness);
            Line("reallife_hc_row_wave", HealthText.Fill(HealthPages.WaveKey(f.Wave)));
            Line("reallife_hc_row_home", f.IllAtHome.ToString());
            Line("reallife_hc_row_living", HealthText.Fill(HealthPages.LivingKey(f.Lifestyle)));
            if (f.Booked)
            {
                Line("reallife_hc_row_booked", HealthText.Fill("reallife_hc_row_booked_value", "day", f.BookedDay.ToString(), "hour", f.BookedHour.ToString()));
                Line("reallife_hc_row_where", f.Clinic);
            }
            box.Title(HealthText.Fill("reallife_hc_sec_you"));
            if (f.InHospital)
            {
                box.Note(HealthText.Fill("reallife_hc_you_hospital", "day", f.StayUntilDay.ToString()));
            }
            else if (f.Kind == Condition.None)
            {
                box.Note(HealthText.Fill("reallife_hc_you_well"));
            }
            else if (f.Serious)
            {
                box.Line(HealthText.Fill("reallife_hc_you_serious", "illness", illness), CellTone.Yellow);
            }
            else if (f.DaysLeft > 0)
            {
                box.Note(HealthText.Fill(f.Treated ? "reallife_hc_you_injury_treated" : "reallife_hc_you_injury", "illness", illness, "days", f.DaysLeft.ToString()));
            }
            else
            {
                box.Note(HealthText.Fill(f.Treated ? "reallife_hc_you_ill_treated" : "reallife_hc_you_ill", "illness", illness));
            }
            box.Note(HealthText.Fill("reallife_hc_doctor_how", "wait", HealthCatalogue.AppointmentWait.ToString(), "fee",
                HealthGame.Money(HealthRules.At(HealthCatalogue.MissedFee, f.Level))));
            Transform buttons = box.ButtonRow();
            if (f.Booked)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_hc_cancel_booking", () => { HealthLive.Instance.CancelBooking(); Refresh(); });
            }
            else if (f.CanBook && !f.InHospital)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hc_book", () => { HealthLive.Instance.Book(); Refresh(); });
            }
        }

        private TableSpec Cover(InfoBox box)
        {
            HealthFacts f = _facts;
            for (int i = 0; i < f.Plans.Length; i++)
            {
                int p = (int)f.Plans[i].Plan;
                TableRow row = Row("p" + i, true);
                row.Set(0, HealthText.PlanName(f.Plans[i].Plan), f.Plan == f.Plans[i].Plan ? TableRow.Green : TableRow.Plain);
                row.Set(1, HealthGame.Money(f.Plans[i].Premium), f.Plans[i].Premium);
                row.Set(2, VentWords.Share(HealthCatalogue.HospitalShare[p] * 10), HealthCatalogue.HospitalShare[p]);
                row.Set(3, VentWords.Share(HealthCatalogue.DoctorShare[p] * 10), HealthCatalogue.DoctorShare[p]);
            }
            PlanFact pick = f.Plans[Mathf.Clamp(_pick, 0, f.Plans.Length - 1)];
            int k = (int)pick.Plan;
            box.Title(HealthText.PlanName(pick.Plan));
            box.RowWords(HealthText.Fill("reallife_hc_row_premium"), HealthGame.Money(pick.Premium));
            box.RowWords(HealthText.Fill("reallife_hc_row_hospital"), VentWords.Share(HealthCatalogue.HospitalShare[k] * 10));
            box.RowWords(HealthText.Fill("reallife_hc_row_excess"), HealthGame.Money(HealthRules.At(HealthCatalogue.Excess[k], f.Level)));
            box.RowWords(HealthText.Fill("reallife_hc_row_doctor"), VentWords.Share(HealthCatalogue.DoctorShare[k] * 10));
            box.RowWords(HealthText.Fill("reallife_hc_row_cap"), HealthGame.Money(HealthRules.At(HealthCatalogue.YearCap[k], f.Level)));
            if (f.Plan == pick.Plan)
            {
                box.RowWords(HealthText.Fill("reallife_hc_row_next"), HealthText.Fill("reallife_hc_row_next_value", "day", f.NextDue.ToString()));
            }
            if (f.Employer != Plan.None)
            {
                box.Line(HealthText.Fill("reallife_hc_employer_note", "plan", HealthText.PlanName(f.Employer)), CellTone.Green);
            }
            box.Note(HealthText.Fill("reallife_hc_not_covered", "days", HealthCatalogue.WaitDays.ToString()));
            Transform buttons = box.ButtonRow();
            int plan = k;
            if (f.Plan != pick.Plan && pick.Premium <= f.Spare)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hc_take_plan", () => { HealthLive.Instance.TakePlan(plan); Refresh(); });
            }
            if (f.Plan != Plan.None)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_hc_cancel_plan", () => { HealthLive.Instance.CancelPlan(); Refresh(); });
            }
            return HealthColumns.Plans;
        }

        private void Bills(InfoBox box)
        {
            HealthFacts f = _facts;
            Line("reallife_hc_row_bills", HealthGame.Money(f.BillsYear));
            Line("reallife_hc_row_claims", HealthGame.Money(f.ClaimsYear));
            if (f.LastBillDay > 0)
            {
                Line("reallife_hc_row_last", HealthText.Fill("reallife_hc_row_last_value", "bill", HealthGame.Money(f.LastBill), "claim", HealthGame.Money(f.LastClaim)));
            }
            Line("reallife_hc_row_owed", HealthGame.Money(f.Owed));
            if (f.Owed > 0)
            {
                Line("reallife_hc_row_per_day", HealthGame.Money(f.DebtPerDay));
            }
            Line("reallife_hc_row_paid_days", HealthText.Fill("reallife_hc_row_paid_days_value", "used", f.PaidUsed.ToString(), "days", f.PaidDays.ToString()));
            Line("reallife_hc_row_sick_pay", HealthGame.Money(f.SickPay));
            Line("reallife_hc_row_spell", HealthText.Fill("reallife_hc_row_spell_value", "n", f.SpellDays.ToString(), "most", HealthCatalogue.SelfCertDays.ToString()));
            if (f.NoteUntil > 0)
            {
                Line("reallife_hc_row_note", HealthText.Fill("reallife_hc_row_next_value", "day", f.NoteUntil.ToString()));
            }
            if (f.CalledWell > 0)
            {
                Line("reallife_hc_row_called", HealthText.Fill("reallife_hc_row_called_value", "n", f.CalledWell.ToString(), "seen", f.Noticed.ToString()));
            }
            box.Title(HealthText.Fill("reallife_hc_sec_bills"));
            box.Note(HealthText.Fill("reallife_hc_debt_how", "months", HealthCatalogue.DebtMonths.ToString(), "days", HealthCatalogue.DebtClaimAfter.ToString()));
            box.Note(HealthText.Fill("reallife_hc_leave_how", "days", HealthCatalogue.SelfCertDays.ToString()));
            Transform buttons = box.ButtonRow();
            if (f.Owed > 0 && f.Owed <= f.Spare)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_hc_pay_off", () => { HealthLive.Instance.PayDebt(); Refresh(); });
            }
            if (f.ShiftOpen && !f.CalledIn)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_hc_call_in", () => { HealthLive.Instance.CallInSick(); Refresh(); });
            }
        }

        private void City(InfoBox box)
        {
            HealthFacts f = _facts;
            if (f.People)
            {
                Line("reallife_hc_row_ill_now", HealthText.Fill("reallife_hc_row_ill_now_value", "n", f.IllNow.ToString(), "people", f.CityPeople.ToString()));
                Line("reallife_hc_row_cases", f.Cases.ToString());
                Line("reallife_hc_row_serious", f.SeriousCases.ToString());
                Line("reallife_hc_row_lost", f.LostDays.ToString());
                Line("reallife_hc_row_city_bills", HealthGame.Money(f.CityBills));
                Line("reallife_hc_row_city_debt", HealthGame.Money(f.CityDebt));
            }
            Line("reallife_hc_row_wave", HealthText.Fill(HealthPages.WaveKey(f.Wave)));
            Line("reallife_hc_row_staff_off", f.StaffOff.ToString());
            Line("reallife_hc_row_policy", HealthText.Fill(HealthPages.PolicyKey(f.SickPolicy)));
            box.Title(HealthText.Fill("reallife_hc_sec_city"));
            if (!f.People)
            {
                box.Line(HealthText.Fill(f.Session ? "reallife_hc_why_session" : "reallife_hc_why_people"), CellTone.Yellow);
            }
            box.Note(HealthText.Fill("reallife_hc_staff_how"));
            box.Note(string.IsNullOrEmpty(f.RivalLine) ? HealthText.Fill("reallife_hc_rivals_none") : f.RivalLine);
        }
    }
}
