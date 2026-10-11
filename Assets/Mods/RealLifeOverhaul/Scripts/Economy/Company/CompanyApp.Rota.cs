using System.Collections.Generic;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Rota tab, three steps deep, each a table and what is picked in the white box:
    ///
    ///   the places     every place the player runs: its hours a week, its staff, and whether open hours have nobody
    ///                  on a shift. Pick one for
    ///   its week       the seven days with the hours it keeps and the hours advised, then the people the advised
    ///                  hours take, a station that is missing, and what its rota leaves unmet or uncovered. The box:
    ///                  what a week comes to as it stands, with the present staff at their best and with everybody
    ///                  it takes, and the buttons that fill the rota and put the old one back. Pick a day for
    ///   that day       hour by hour: the customers the game would send, what the hour makes with its crew, and
    ///                  how many it takes against how many are rostered
    ///
    /// A business is worked out when it is picked and when the director says something changed, never while the tab
    /// only shows the list (CompanyDirector.RotaSheetFor).
    /// </summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] RotaWeekColumns = { "reallife_rota_col_day", "reallife_rota_col_now", "reallife_rota_col_advised", "reallife_rota_col_customers" };
        private static readonly string[] RotaDayColumns = { "reallife_rota_col_hour", "reallife_rota_col_customers", "reallife_rota_col_worth", "reallife_rota_col_staff" };
        private static readonly string[] RotaDayKeys = { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };   // the game's own
        private const int RotaRowsMost = 8;     // unmet demands and gaps listed under a week

        partial void WriteRota(InfoBox box, ref string selected)
        {
            if (!_director.RotaOn)
            {
                box.Title(CompanyText.T(TabKeys[_tab]));
                box.Line(CompanyText.T("reallife_rota_off"));
                Say("off", CompanyText.T("reallife_co_part_off_row"));
                return;
            }
            string place;
            int weekday;
            bool onDay = Picked("rday:", out place, out weekday);
            place = onDay ? place : Picked("rota:");
            RotaPlace at = _director.RotaPlaceAt(place);
            if (at == null)
            {
                selected = RotaList(box);
                return;
            }
            RotaSheet sheet = _director.RotaSheetFor(at);
            if (sheet == null)
            {
                box.Title(at.Name);
                box.Line(CompanyText.T(_director.RotaUnread(at.Place) ? "reallife_rota_cannot" : _director.RotaWorking(at.Place) ? "reallife_rota_working" : "reallife_rota_off"));
                BackButton(box);
                return;
            }
            if (onDay && weekday >= 0 && weekday < RotaCatalogue.Days && sheet.Shop.HasCustomers)
            {
                RotaDay(box, sheet, weekday);
            }
            else
            {
                RotaWeek(box, sheet);
            }
        }

        // ---------------------------------------------------------------- the places

        private string RotaList(InfoBox box)
        {
            string selected = null;
            IReadOnlyList<RotaPlace> places = _director.RotaPlaces;
            for (int i = 0; i < places.Count; i++)
            {
                RotaPlace p = places[i];
                string hours = p.OpenHours > 0 ? CompanyText.T("reallife_rota_row_open", "hours", p.OpenHours.ToString()) : CompanyText.T("reallife_rota_closed");
                bool gaps = p.Type != RotaType.Warehouse && !p.Shut && p.Staff > 0 && p.GapHours > 0;
                string state = p.Type == RotaType.Warehouse ? CompanyText.T("reallife_rota_row_none")
                    : p.Staff == 0 ? CompanyText.T("reallife_rota_row_no_staff")
                    : gaps ? CompanyText.T("reallife_rota_row_gaps", "hours", p.GapHours.ToString())
                    : CompanyText.T("reallife_rota_row_covered");
                Pick("rota:" + p.Place, p.Name, RotaGame.TypeName(p.Reg) + ", " + hours, CompanyText.T("reallife_rota_row_staff", "count", p.Staff.ToString()), state, gaps ? 2 : 0, ref selected);
            }
            if (places.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }
            box.Title(CompanyText.T("reallife_rota_tab"));
            box.Line(CompanyText.T("reallife_rota_intro"));
            box.Line(CompanyText.T("reallife_rota_intro_hours"));
            box.Line(CompanyText.T("reallife_rota_intro_fill"));
            box.Line(CompanyText.T("reallife_rota_intro_pick"));
            return selected;
        }

        // ---------------------------------------------------------------- one business's week

        private void RotaWeek(InfoBox box, RotaSheet sheet)
        {
            RotaPlace at = sheet.Place;
            RotaShop shop = sheet.Shop;
            RotaOutlook o = sheet.Outlook;
            string place = at.Place, none = null;
            _shell.SetColumns(ColumnNames, RotaWeekColumns, ColumnWidths, 1);
            for (int d = 0; d < RotaCatalogue.Days; d++)
            {
                int customers = 0, busiest = -1;
                bool differs = false;
                for (int h = 0; h < RotaCatalogue.Hours; h++)
                {
                    int cell = d * RotaCatalogue.Hours + h;
                    customers += shop.Customers[cell];
                    busiest = shop.Customers[cell] > 0 && (busiest < 0 || shop.Customers[cell] > shop.Customers[d * RotaCatalogue.Hours + busiest]) ? h : busiest;
                    differs |= o.Advised && shop.Open[cell] != o.Staffed[cell];
                }
                string coming = !shop.HasCustomers ? string.Empty : busiest < 0 ? CompanyText.T("reallife_rota_none_come")
                    : CompanyText.T("reallife_rota_row_customers", "count", customers.ToString(), "hour", RotaHour(busiest));
                string advised = o.Advised ? RotaHours(o.Staffed, d) : CompanyText.T("reallife_rota_as_now");
                if (shop.HasCustomers)
                {
                    Pick("rday:" + place + ":" + d, RotaDayName(d), RotaHours(shop.Open, d), advised, coming, differs ? 1 : 0, ref none);
                }
                else
                {
                    FinanceRow row = Row("day" + d);
                    row.A = RotaDayName(d);
                    row.B = RotaHours(shop.Open, d);
                    row.C = advised;
                }
            }
            RotaPeople(sheet);
            RotaUnmet(sheet);

            box.Title(at.Name);
            string refusal = CompanyDirector.RotaRefusal(sheet);
            if (at.Type == RotaType.Warehouse || sheet.Stations.Count == 0)
            {
                box.Line(RotaRefused(refusal));
                BackButton(box);
                return;
            }
            if (shop.HasCustomers)
            {
                box.Line(CompanyText.T(sheet.FromBooks ? "reallife_rota_box_worth_books" : "reallife_rota_box_worth_prices", "takings", CompanyText.Wage(shop.Takings), "margin", CompanyText.Wage(shop.Margin)));
                box.Line(FinanceText.Pair("reallife_rota_box_now", CompanyText.T("reallife_rota_box_week", "profit", RotaMoney(o.ProfitNow), "hours", o.HoursNow.ToString())));
                if (o.Advised)
                {
                    box.Line(FinanceText.Pair("reallife_rota_box_staffed", CompanyText.T("reallife_rota_box_week", "profit", RotaMoney(o.ProfitStaffed), "hours", o.HoursStaffed.ToString())));
                    box.Line(FinanceText.Pair("reallife_rota_box_best", CompanyText.T("reallife_rota_box_week", "profit", RotaMoney(o.ProfitBest), "hours", o.HoursBest.ToString())));
                }
                string note = !sheet.RequirementsMet ? CompanyText.T("reallife_rota_note_requirements")
                    : at.Shut ? CompanyText.T("reallife_rota_note_shut")
                    : !o.Advised ? CompanyText.T("reallife_rota_note_no_worth")
                    : null;
                if (note != null)
                {
                    box.Line(note);
                }
                // The late opening permit, whenever it matters: the business opens late without one, or hours that would pay are kept out of the advice for want of one.
                string fee = CompanyText.Money(LicenceRules.Fee((int)LicenceKind.Late, _director.Index)), term = CompanyCatalogue.LicenceTerm[(int)LicenceKind.Late].ToString();
                if (sheet.Late)
                {
                    box.Line(CompanyText.T("reallife_rota_note_late", "fee", fee, "days", term));
                }
                if (o.Barred)
                {
                    box.Line(CompanyText.Fill("reallife_rota_note_permit", "fee", fee, "days", term, "hours", (o.HoursFree - o.HoursBest).ToString(), "amount", RotaMoney(o.ProfitFree - o.ProfitBest)));
                }
            }
            else
            {
                box.Line(CompanyText.T(at.Type == RotaType.Headquarters ? "reallife_rota_hq" : at.Type == RotaType.Work ? "reallife_rota_work" : "reallife_rota_no_customers"));
            }
            box.Line(refusal != null ? RotaRefused(refusal)
                : CompanyText.T("reallife_rota_box_rota", "covered", sheet.Now.CoveredHours.ToString(), "wanted", sheet.Now.WantedHours.ToString(), "unmet", sheet.Now.UnmetCount.ToString()));
            var buttons = box.ButtonRow();
            if (refusal == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_rota_fill", () => _director.FillRota(place, false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            if (refusal == null && o.Advised && o.HoursStaffed > 0 && RotaDiffers(shop.Open, o.Staffed))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_rota_fill_hours", () => _director.FillRota(place, true));
            }
            if (_director.RotaHasUndo(place))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_rota_back", () => _director.RotaBack(place));
            }
        }

        // The people the advised hours take, group by group, and a station that would pay for itself.
        private void RotaPeople(RotaSheet sheet)
        {
            RotaOutlook o = sheet.Outlook;
            if (o.Roles.Count > 0)
            {
                Say("people", CompanyText.T("reallife_rota_head_people"));
            }
            for (int i = 0; i < o.Roles.Count; i++)
            {
                RotaRole role = o.Roles[i];
                FinanceRow row = Row("role" + i);
                row.A = RotaGroup(sheet, role.Group);
                row.B = CompanyText.T("reallife_rota_role", "need", role.Need.ToString(), "have", role.Have.ToString(), "skill", RotaGame.GroupSkill(sheet, role.Group));
                row.C = role.Hire > 0 ? CompanyText.T("reallife_rota_role_hire", "count", role.Hire.ToString()) : role.Spare > 0 ? CompanyText.T("reallife_rota_role_spare", "count", role.Spare.ToString()) : CompanyText.T("reallife_rota_role_ok");
                row.D = role.Hire > 0 ? CompanyText.T("reallife_rota_a_week", "amount", RotaMoney(role.Cost)) : string.Empty;
                row.Tone = role.Hire > 0 ? 1 : 0;
            }
            for (int i = 0; i < o.Missing.Count; i++)
            {
                RotaMissing missing = o.Missing[i];
                FinanceRow row = Row("more" + i);
                row.A = CompanyText.T("reallife_rota_missing", "station", RotaGroup(sheet, missing.Group));
                row.B = CompanyText.T("reallife_rota_missing_what", "customers", missing.Customers.ToString());
                row.C = RotaDayName(missing.Day);
            }
            for (int i = 0; i < sheet.Broken.Count; i++)
            {
                Say("broken" + i, CompanyText.T("reallife_rota_broken", "station", sheet.Broken[i]));
            }
        }

        // What the rota as it stands leaves unmet and uncovered, in words: who asks for what and why it gave way; which station, when, and why nobody is at it.
        private void RotaUnmet(RotaSheet sheet)
        {
            RotaPlan plan = sheet.Now;
            if (sheet.Workers.Count == 0 || (plan.Unmet.Count == 0 && plan.Gaps.Count == 0))
            {
                return;
            }
            Say("unmet", CompanyText.T(sheet.Shown != null ? "reallife_rota_head_unmet_made" : "reallife_rota_head_unmet"));
            int rows = 0;
            for (int i = 0; i < plan.Unmet.Count && rows < RotaRowsMost; i++, rows++)
            {
                RotaUnmet unmet = plan.Unmet[i];
                EmployeeInstance who = unmet.Worker < sheet.Workers.Count ? sheet.Workers[unmet.Worker] : null;
                RotaDemand[] demands = unmet.Worker < sheet.Problem.Workers ? sheet.Problem.Worker[unmet.Worker].Demands : RotaWorker.Empty;
                string asks = unmet.Demand < demands.Length ? RotaGame.DemandName(who, demands[unmet.Demand]) : string.Empty;
                string why = unmet.Reason == RotaReason.Short ? "reallife_rota_why_short" : unmet.Reason == RotaReason.NoStation ? "reallife_rota_why_no_station"
                    : sheet.Shown != null ? "reallife_rota_why_needed" : "reallife_rota_why_broken";
                FinanceRow row = Row("unmet" + i);
                row.A = RotaGame.WorkerName(who);
                row.B = (asks.Length > 0 ? asks + ": " : string.Empty) + CompanyText.T(why);
                row.C = unmet.Station >= 0 && unmet.Station < sheet.Stations.Count ? RotaGame.Name(sheet.Stations[unmet.Station]) : string.Empty;
                row.D = unmet.Day >= 0 ? RotaDayName(unmet.Day) : string.Empty;
                row.Tone = 1;
            }
            for (int i = 0; i < plan.Gaps.Count && rows < RotaRowsMost; i++, rows++)
            {
                RotaGap gap = plan.Gaps[i];
                FinanceRow row = Row("gap" + i);
                row.A = gap.Station < sheet.Stations.Count ? RotaGame.Name(sheet.Stations[gap.Station]) : string.Empty;
                row.B = CompanyText.T(gap.Reason == RotaReason.NoSkill ? "reallife_rota_gap_no_skill" : "reallife_rota_gap_stretched");
                row.C = CompanyText.T("reallife_rota_from_to", "from", RotaHour(gap.From), "to", RotaHour(gap.To));
                row.D = RotaDayName(gap.Day);
                row.Tone = 2;
            }
            int left = plan.Unmet.Count + plan.Gaps.Count - rows;
            if (left > 0)
            {
                Say("left", CompanyText.T("reallife_rota_more", "count", left.ToString()));
            }
        }

        // ---------------------------------------------------------------- one day

        private void RotaDay(InfoBox box, RotaSheet sheet, int d)
        {
            RotaShop shop = sheet.Shop;
            RotaOutlook o = sheet.Outlook;
            string place = sheet.Place.Place;
            int stations = shop.Stations, customers = 0, busiest = -1;
            long makes = 0;
            _shell.SetColumns(ColumnNames, RotaDayColumns, ColumnWidths, 1);
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                int cell = d * RotaCatalogue.Hours + h, coming = shop.Customers[cell];
                bool now = shop.Open[cell], advised = o.Staffed[cell];
                if (coming <= 0 && !now && !advised)
                {
                    continue;
                }
                int have = 0;
                for (int s = 0; s < stations; s++)
                {
                    have += shop.GroupKind[shop.Group[s]] != RotaGroupKind.Cleaning && (sheet.Now.At[cell * stations + s] >= 0 || shop.Taken[cell * stations + s]) ? 1 : 0;
                }
                if (advised)
                {
                    customers += coming;
                    makes += o.Net[cell];
                    busiest = busiest < 0 || coming > shop.Customers[d * RotaCatalogue.Hours + busiest] ? h : busiest;
                }
                FinanceRow row = Row("hour" + h);
                row.A = RotaHour(h);
                row.B = CompanyText.T("reallife_rota_hour_customers", "count", coming.ToString(), "takings", RotaMoney((long)coming * shop.Takings));
                row.C = RotaMoney(o.Net[cell]);
                string crew = CompanyText.T("reallife_rota_hour_staff", "need", o.Crew[cell].ToString(), "have", have.ToString());
                row.D = advised && now ? crew : advised ? CompanyText.T("reallife_rota_hour_open_it", "need", o.Crew[cell].ToString())
                    : !now ? CompanyText.T("reallife_rota_closed") : o.Advised ? CompanyText.T("reallife_rota_hour_close_it") : crew;
                row.Tone = advised != now && o.Advised ? (advised ? 1 : 2) : advised && have < o.Crew[cell] ? 1 : 0;
            }
            box.Title(CompanyText.T("reallife_rota_day_title", "day", RotaDayName(d), "business", sheet.Place.Name));
            box.Line(FinanceText.Pair("reallife_rota_day_now", RotaHours(shop.Open, d)));
            if (o.Advised)
            {
                box.Line(FinanceText.Pair("reallife_rota_day_staffed", RotaHours(o.Staffed, d)));
                box.Line(FinanceText.Pair("reallife_rota_day_best", RotaHours(o.Best, d)));
                box.Line(busiest < 0 ? CompanyText.T("reallife_rota_day_nothing")
                    : CompanyText.T("reallife_rota_day_makes", "customers", customers.ToString(), "hour", RotaHour(busiest), "amount", RotaMoney(makes)));
                if (shop.DayFee[d] > 0)
                {
                    box.Line(FinanceText.Pair("reallife_rota_day_fee", RotaMoney(shop.DayFee[d])));
                }
            }
            else
            {
                box.Line(CompanyText.T("reallife_rota_note_no_worth"));
            }
            box.Line(CompanyText.T("reallife_rota_day_columns"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_rota_day_back", () =>
            {
                _pick = "rota:" + place;
                _table.ClearSelection();
                Refresh();
            });
        }

        // ---------------------------------------------------------------- words

        private static string RotaDayName(int d)
        {
            return RotaDayKeys[d < 0 ? 0 : d >= RotaDayKeys.Length ? RotaDayKeys.Length - 1 : d].GetLocalization();
        }

        // An hour as the game writes the time of day, by its own twelve or twenty-four hour setting.
        private static string RotaHour(int hour)
        {
            return (hour % RotaCatalogue.Hours).GetFormattedTime(0f, false);
        }

        // The opening hours of one day in words: "09:00 to 18:00", two blocks joined by a comma, or "closed".
        private static string RotaHours(bool[] open, int d)
        {
            string words = null;
            for (int h = 0; h < RotaCatalogue.Hours;)
            {
                if (!open[d * RotaCatalogue.Hours + h])
                {
                    h++;
                    continue;
                }
                int from = h;
                while (h < RotaCatalogue.Hours && open[d * RotaCatalogue.Hours + h])
                {
                    h++;
                }
                words = (words != null ? words + ", " : string.Empty) + CompanyText.T("reallife_rota_from_to", "from", RotaHour(from), "to", RotaHour(h));
            }
            return words ?? CompanyText.T("reallife_rota_closed");
        }

        private static bool RotaDiffers(bool[] a, bool[] b)
        {
            for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
            {
                if (a[cell] != b[cell])
                {
                    return true;
                }
            }
            return false;
        }

        // Why a rota cannot be filled, with the most the planner keeps where the sentence names it.
        private static string RotaRefused(string key)
        {
            return CompanyText.T(key, "stations", RotaCatalogue.StationsMost.ToString(), "staff", RotaCatalogue.WorkersMost.ToString());
        }

        // Hundredths as the game writes money.
        private static string RotaMoney(long hundredths)
        {
            return CompanyText.Money(hundredths / 100f);
        }

        private static string RotaGroup(RotaSheet sheet, int group)
        {
            return group >= 0 && group < sheet.GroupNames.Count ? sheet.GroupNames[group] : string.Empty;
        }
    }
}
