using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Rota tab, four pages deep: the places, one business's plans (.RotaPlans), one plan's week and one day of it
    /// (.RotaWeek). A business is worked out only when picked. Cost: nothing unless the tab is written.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] RotaDayKeys =   // the game's own
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };
        private static readonly string[] RotaModeKeys = { "reallife_rota_mode_auto", "reallife_rota_mode_staff", "reallife_rota_mode_lean" };
        private const int RotaRowsMost = 8;     // unmet demands and gaps listed under a week

        partial void WriteRota(InfoBox box, ref string selected)
        {
            if (!_director.RotaOn)
            {
                box.Title(CompanyText.T(TabKeys[_tab]));
                box.Line(CompanyText.T("reallife_rota_off"), CellTone.Yellow);
                Say("off", CompanyText.T("reallife_co_part_off_row"));
                return;
            }
            string place;
            int code;
            bool onDay = Picked("rday:", out place, out code), onRung = !onDay && Picked("rung:", out place, out code);
            place = onDay || onRung ? place : Picked("rota:");
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
                string why = _director.RotaUnread(at.Place) ? "reallife_rota_cannot" : _director.RotaWorking(at.Place) ? "reallife_rota_working" : "reallife_rota_off";
                box.Line(CompanyText.T(why), CellTone.Yellow);
                BackButton(box);
                return;
            }
            int rung = onDay ? code / 10 : code, weekday = onDay ? code % 10 : -1;   // a day's code is rung * 10 + weekday
            bool has = (onDay || onRung) && rung >= 0 && rung < sheet.Ladder.Count;
            if (has && onDay && weekday < RotaCatalogue.Days && sheet.Shop.HasCustomers)
            {
                RotaDay(box, sheet, rung, weekday);
            }
            else if (has)
            {
                RotaPlan(box, sheet, rung);
            }
            else
            {
                RotaPlans(box, sheet);
            }
        }

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
                Pick("rota:" + p.Place, p.Name, RotaGame.TypeName(p.Reg) + ", " + hours, CompanyText.T("reallife_rota_row_staff", "count", p.Staff.ToString()), state,
                    gaps ? 2 : 0, ref selected);
                Last.Key[2] = p.Staff;
            }
            if (places.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }
            box.Title(CompanyText.T("reallife_rota_tab"));
            box.Note(CompanyText.T("reallife_rota_intro"));
            box.Section("reallife_ibox_sec_how");
            box.Line(CompanyText.T("reallife_rota_intro_plans"));
            box.Line(CompanyText.T("reallife_rota_intro_fill"));
            box.Note(CompanyText.T("reallife_rota_intro_pick"));
            return selected;
        }

        private static string RotaPlanName(RotaSheet sheet, RotaRung r)
        {
            if (r.Kind != RotaRungKind.Hire)
            {
                return CompanyText.T(r.Kind == RotaRungKind.AsItIs ? "reallife_rota_plan_now" : "reallife_rota_plan_best");
            }
            return CompanyText.T("reallife_rota_plan_hire", "count", r.Hires.ToString(), "who", RotaHireWho(sheet, r));
        }

        private static int RotaHiresFor(RotaRung r, int group)
        {
            int count = 0;
            for (int i = 0; i < r.Hires; i++)
            {
                count += r.Hire[i] == group ? 1 : 0;
            }
            return count;
        }

        // The people a plan takes on, by the skill to look for: "2 Customer Service, 1 Cleaning".
        private static string RotaHireWho(RotaSheet sheet, RotaRung r)
        {
            string who = string.Empty;
            for (int g = 0; g < sheet.Shop.Groups; g++)
            {
                int count = RotaHiresFor(r, g);
                if (count > 0)
                {
                    who += (who.Length > 0 ? ", " : string.Empty) + count + " " + RotaGame.GroupSkill(sheet, g);
                }
            }
            return who;
        }

        private static string RotaNeeds(RotaSheet sheet, RotaRung r)
        {
            if (r.Kind == RotaRungKind.AsItIs)
            {
                return CompanyText.T("reallife_rota_needs_nothing");
            }
            if (r.Kind == RotaRungKind.Best)
            {
                return CompanyText.T(r.Source == 0 ? "reallife_rota_needs_same" : r.Source == 1 ? "reallife_rota_needs_rota" : "reallife_rota_needs_hours");
            }
            int group = r.Hire[r.Hires - 1];
            return CompanyText.T("reallife_rota_needs_hire", "who", RotaGame.GroupSkill(sheet, group), "wage", CompanyText.Wage(RotaLadder.Going(sheet.Shop, group)));
        }

        // The customers an hour the stations serve when all are manned: the smallest service group's sum (the game's own limit).
        private static int RotaServes(RotaShop shop)
        {
            int least = -1;
            for (int g = 0; g < shop.Groups; g++)
            {
                int sum = 0, n = 0;
                for (int s = 0; s < shop.Stations; s++)
                {
                    if (shop.Group[s] == g)
                    {
                        sum += shop.Cap[s];
                        n++;
                    }
                }
                if (shop.GroupKind[g] == RotaGroupKind.Service && n > 0 && (least < 0 || sum < least))
                {
                    least = sum;
                }
            }
            return least < 0 ? 0 : least;
        }

        private static int RotaHoursOf(RotaRung r, int worker)
        {
            return worker < r.Workers ? r.HoursOf[worker] : 0;
        }

        // What somebody asks for about their time, in words: hours a week, days, days off, hours not worked.
        private static string RotaAsks(RotaWorker worker)
        {
            string words = string.Empty;
            for (int i = 0; i < worker.Demands.Length; i++)
            {
                RotaDemand d = worker.Demands[i];
                string one = null;
                if (d.Kind == RotaDemandKind.Hours)
                {
                    one = d.A > 0 && d.B > 0 ? CompanyText.T("reallife_rota_asks_between", "least", d.A.ToString(), "most", d.B.ToString())
                        : d.A > 0 ? CompanyText.T("reallife_rota_asks_least", "least", d.A.ToString())
                        : d.B > 0 ? CompanyText.T("reallife_rota_asks_most", "most", d.B.ToString()) : null;
                }
                else if (d.Kind == RotaDemandKind.Days)
                {
                    one = CompanyText.T("reallife_rota_asks_days", "count", d.A.ToString());
                }
                else if (d.Kind == RotaDemandKind.FreeDays)
                {
                    string days = string.Empty;
                    for (int k = 0; k < RotaCatalogue.Days; k++)
                    {
                        days += ((d.Mask >> k) & 1UL) != 0 ? (days.Length > 0 ? ", " : string.Empty) + RotaDayName(k) : string.Empty;
                    }
                    one = days.Length > 0 ? CompanyText.T("reallife_rota_asks_free", "days", days) : null;
                }
                else if (d.Kind == RotaDemandKind.NoHours)
                {
                    string hours = string.Empty;
                    for (int h = 0; h < RotaCatalogue.Hours;)
                    {
                        if (((d.Mask >> h) & 1UL) == 0)
                        {
                            h++;
                            continue;
                        }
                        int from = h;
                        while (h < RotaCatalogue.Hours && ((d.Mask >> h) & 1UL) != 0)
                        {
                            h++;
                        }
                        hours += (hours.Length > 0 ? ", " : string.Empty) + CompanyText.T("reallife_rota_from_to", "from", RotaHour(from), "to", RotaHour(h));
                    }
                    one = hours.Length > 0 ? CompanyText.T("reallife_rota_asks_not", "hours", hours) : null;
                }
                words += one != null ? (words.Length > 0 ? "; " : string.Empty) + one : string.Empty;
            }
            return words.Length > 0 ? words : CompanyText.T("reallife_rota_asks_any");
        }

        // The hours a week somebody asks for against what a plan gives: green inside the range; red outside it or left out,
        // with the clash and what would remove it. Null for somebody who asks for no hours, or is left as they are.
        private static string RotaRange(RotaSheet sheet, RotaRung r, int worker, out CellTone tone)
        {
            int least, most, hours = RotaHoursOf(r, worker);
            tone = TableRow.Plain;
            RotaFit.Range(sheet.Problem.Worker[worker], out least, out most);
            if ((least <= 0 && most == int.MaxValue) || (worker < sheet.Fixed.Length && sheet.Fixed[worker]))
            {
                return null;
            }
            string asks = most == int.MaxValue ? CompanyText.T("reallife_rota_asks_least", "least", least.ToString())
                : CompanyText.T("reallife_rota_asks_between", "least", least.ToString(), "most", most.ToString());
            for (int i = 0; i < r.Left.Count; i++)
            {
                RotaLeft left = r.Left[i];
                if (left.Worker != worker)
                {
                    continue;
                }
                tone = TableRow.Red;
                return left.Why == RotaClash.Service ? CompanyText.T("reallife_rota_range_service", "asks", asks)
                    : left.Why == RotaClash.Shared
                        ? CompanyText.Fill("reallife_rota_range_shared", "asks", asks, "asked", left.Asked.ToString(), "held", left.Held.ToString())
                    : CompanyText.Fill("reallife_rota_range_hours", "asks", asks, "room", left.Room.ToString(), "short", (left.Least - left.Room).ToString());
            }
            if (hours > 0 && hours >= least && hours <= most)
            {
                tone = TableRow.Green;
                return CompanyText.T("reallife_rota_range_in", "asks", asks, "hours", hours.ToString());
            }
            if (hours == 0 && least <= 0)
            {
                return CompanyText.T("reallife_rota_range_in", "asks", asks, "hours", hours.ToString());
            }
            tone = TableRow.Red;
            return CompanyText.Fill("reallife_rota_range_out", "asks", asks, "hours", hours.ToString(), "off", (hours < least ? least - hours : hours - most).ToString());
        }

        // The first demand of somebody a rota leaves unmet, in the game's own words; null if it meets them all.
        private static string RotaUnmetOf(RotaSheet sheet, RotaPlan plan, int worker)
        {
            for (int i = 0; i < plan.Unmet.Count; i++)
            {
                RotaUnmet unmet = plan.Unmet[i];
                if (unmet.Worker != worker || worker >= sheet.Workers.Count)
                {
                    continue;
                }
                RotaDemand[] demands = sheet.Problem.Worker[worker].Demands;
                string asks = unmet.Demand < demands.Length ? RotaGame.DemandName(sheet.Workers[worker], demands[unmet.Demand]) : string.Empty;
                string why = CompanyText.T(unmet.Reason == RotaReason.Short ? "reallife_rota_why_short"
                    : unmet.Reason == RotaReason.NoStation ? "reallife_rota_why_no_station" : "reallife_rota_why_needed");
                return (asks.Length > 0 ? asks + ": " : string.Empty) + why;
            }
            return null;
        }

        private static string RotaDemandState(RotaSheet sheet, RotaRung r, int worker)
        {
            if (worker < sheet.Fixed.Length && sheet.Fixed[worker])
            {
                return string.Empty;
            }
            string unmet = RotaUnmetOf(sheet, r.Plan, worker);
            if (unmet == null)
            {
                return CompanyText.T(sheet.Problem.Worker[worker].Demands.Length > 0 ? "reallife_rota_demands_met" : "reallife_rota_demands_none");
            }
            long cost = worker < r.DemandCost.Length ? r.DemandCost[worker] : 0;
            int quit = worker < r.QuitHours.Length ? r.QuitHours[worker] : 0;
            return unmet + ". " + (quit > 0
                ? CompanyText.T("reallife_rota_person_quit", "days", System.Math.Max(1, quit / RotaCatalogue.Hours).ToString(), "amount", RotaMoney(cost))
                : CompanyText.T("reallife_rota_person_settles", "amount", RotaMoney(cost)));
        }

        private static string RotaDayName(int d)
        {
            return RotaDayKeys[d < 0 ? 0 : d >= RotaDayKeys.Length ? RotaDayKeys.Length - 1 : d].GetLocalization();
        }

        // An hour as the game writes the time of day, by its own twelve or twenty-four hour setting.
        private static string RotaHour(int hour)
        {
            return (hour % RotaCatalogue.Hours).GetFormattedTime(0f, false);
        }

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

        private static string RotaRefused(string key)
        {
            return CompanyText.T(key, "stations", RotaCatalogue.StationsMost.ToString(), "staff", RotaCatalogue.WorkersMost.ToString());
        }

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
