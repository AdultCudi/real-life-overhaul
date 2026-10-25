using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The levels above the managers on the Managers tab: the owner's overview (L4), which shows only what waits for the
    /// owner and the executives' summaries until Deep Mode opens every level's pages; the executives' seats (L3) with who
    /// sits in each, their log and who could be appointed from the player's professionals; a region's dashboard (L2) with
    /// its figures, what is pending there and its quick actions; and a store's region picked on its manager's panel. A page
    /// is written when it is shown or something changes, never per frame. The picks:
    ///   "xs:" the seats; "xp:seat" one; "xa:seat" who could sit; "xc:seat*id" one of them; "dr:region" a region's dashboard.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private const int OwnerRowsShown = 20;   // escalations on the owner's overview, the newest first

        // The levels' own pages; false for a pick that is not one of them.
        private bool WriteLevels(InfoBox box, ref string selected)
        {
            HierarchyState hs = _director.HierarchyData;
            if (_pick == null || hs == null || !_director.HierarchyOn)
            {
                return false;
            }
            if (_pick == "xs:")
            {
                WriteSeats(hs, ref selected);
                WriteSeatsBox(box, hs);
                return true;
            }
            string rest = Picked("xp:");
            int seat;
            if (rest != null && int.TryParse(rest, out seat) && ExecutiveHiring.Real(seat))
            {
                WriteSeatLog(hs, (ExecSeat)seat, ref selected);
                WriteSeat(box, hs, (ExecSeat)seat);
                return true;
            }
            rest = Picked("xa:");
            if (rest != null && int.TryParse(rest, out seat) && ExecutiveHiring.Real(seat))
            {
                WriteCandidates((ExecSeat)seat, ref selected);
                WriteCandidatesBox(box, (ExecSeat)seat);
                return true;
            }
            string id;
            if (Split("xc:", out rest, out id) && int.TryParse(rest, out seat) && ExecutiveHiring.Real(seat))
            {
                WriteCandidates((ExecSeat)seat, ref selected);
                WriteCandidate(box, (ExecSeat)seat, id);
                return true;
            }
            rest = Picked("dr:");
            if (rest != null && _director.HasArea(rest))
            {
                _escBack = _pick;
                WriteRegionRows(hs, rest, ref selected);
                WriteRegionDashboard(box, rest);
                return true;
            }
            return false;
        }

        // --- The owner's overview ---

        private void WriteOwnerOverview(InfoBox box, HierarchyState hs, ref string selected)
        {
            _spec = CompanyTables.OwnerView;
            _escBack = OverviewBack;
            int yours = 0, shown = 0;
            for (int i = hs.Escalations.Count - 1; i >= 0; i--)
            {
                Escalation e = hs.Escalations[i];
                if (!e.Open || e.Level < EscalationRules.LevelOwner)
                {
                    continue;
                }
                yours++;
                if (shown++ >= OwnerRowsShown)
                {
                    continue;
                }
                Pick("he:" + e.Id, KindWords(e.Kind), PlaceWords(e) + ": " + EscalationWords(e), CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()),
                    CompanyText.T("reallife_hier_held_you"), 1, ref selected);
            }
            if (yours == 0)
            {
                Say("none", CompanyText.T("reallife_hier_owner_none"));
            }
            foreach (ExecSeat seat in ExecutiveHiring.Seats)
            {
                SeatRow(hs, seat, ref selected);
            }
            DirectoryCounts c = _counts;
            Pick("mg:region", CompanyText.T("reallife_mgr_dir_regional"), Of(c.Managers[(int)ManagerGroup.Regional], c.Districts), string.Empty,
                CompanyText.T("reallife_hier_owner_open"), 0, ref selected);
            Pick(c.Places[(int)ManagerGroup.Vacant] > 0 ? "mg:vacant" : "mg:shop", CompanyText.T("reallife_hier_owner_places"), Of(c.PlaceManagers, c.AllPlaces), string.Empty,
                CompanyText.T("reallife_hier_owner_open"), 0, ref selected);
            Pick("pc:", CompanyText.T("reallife_hier_dir_policy"), CompanyText.T("reallife_pol_dir_own", "count", (hs.Regions.Count + hs.Stores.Count).ToString()), string.Empty,
                CompanyText.T("reallife_hier_owner_open"), 0, ref selected);

            box.Title(CompanyText.T("reallife_hier_owner_title"));
            box.Chip(ExecutiveHiring.Code(Rung.Owner) + " " + CompanyText.T(LadderRanks.TitleKey((int)Rung.Owner, ExecSeat.None)));
            box.Chip(CompanyText.T("reallife_hier_chip_yours", "count", yours.ToString()), yours > 0 ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_hier_sec_summaries");
            foreach (ExecSeat seat in ExecutiveHiring.Seats)
            {
                box.Line(SeatSummary(hs, seat));
            }
            box.Section("reallife_mgr_sec_directory");
            box.RowPair("reallife_mgr_dir_regional", Of(c.Managers[(int)ManagerGroup.Regional], c.Districts), "reallife_mgr_over_places", Of(c.PlaceManagers, c.AllPlaces));
            box.RowPair("reallife_mgr_over_cost", CompanyText.Money(c.AllCost), "reallife_hier_exec_cost", CompanyText.Money(_director.ExecutivesMonthCost()));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_hier_dir_inbox", () => Go("hx:"));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_hier_dir_policy", () => OpenPolicy(CompanyDirector.ScopeCompany, null));
            DeepSwitch(box, hs);
            box.Note(CompanyText.T("reallife_hier_owner_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // Deep Mode's switch: the Mod settings app's own, else the box's tick switch, else a button.
        private void DeepSwitch(InfoBox box, HierarchyState hs)
        {
            bool on = hs.Deep;
            Action<bool> set = v => _director.HierarchyDeep(v);
            if (box.Toggle("reallife_hier_deep", CompanyText.T("reallife_hier_deep_what"), on, set) || box.Switch("reallife_hier_deep", on, set))
            {
                return;
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, on ? "reallife_hier_deep_off" : "reallife_hier_deep_on", () => set(!on));
        }

        private void SeatRow(HierarchyState hs, ExecSeat seat, ref string selected)
        {
            ExecutiveFile x = hs.Executive(seat);
            Pick("xp:" + (int)seat, CompanyDirector.SeatName(seat), x != null ? x.Name + ", " + SeatFigures(hs, seat) : CompanyText.T("reallife_hier_seat_empty"), string.Empty,
                x == null ? CompanyText.T("reallife_mgr_empty_post") : x.Seen ? CompanyText.T("reallife_mgr_in_post") : CompanyText.T("reallife_hier_seat_waits"),
                x != null && !x.Seen ? 1 : 0, ref selected);
        }

        // An executive's week in a few words: open in their area, settled by them this week.
        private string SeatFigures(HierarchyState hs, ExecSeat seat)
        {
            ExecutiveFile x = hs.Executive(seat);
            return CompanyText.T("reallife_hier_seat_figures", "open", _director.OpenOfSeat(seat).ToString(),
                "settled", x != null ? _director.SettledSince(null, x.Name, _director.Today - 7).ToString() : "0");
        }

        // An executive's summary line for the owner, or the empty seat's.
        private string SeatSummary(HierarchyState hs, ExecSeat seat)
        {
            ExecutiveFile x = hs.Executive(seat);
            string name = "<b>" + CompanyDirector.SeatName(seat) + "</b> ";
            if (x == null)
            {
                return name + CompanyText.T("reallife_hier_seat_empty_line", "open", _director.OpenOfSeat(seat).ToString());
            }
            return name + CompanyText.T("reallife_hier_seat_summary", "name", x.Name, "open", _director.OpenOfSeat(seat).ToString(),
                "settled", _director.SettledSince(null, x.Name, _director.Today - 7).ToString());
        }

        // --- The seats ---

        private void WriteSeats(HierarchyState hs, ref string selected)
        {
            _spec = CompanyTables.OwnerView;
            foreach (ExecSeat seat in ExecutiveHiring.Seats)
            {
                SeatRow(hs, seat, ref selected);
            }
        }

        private void WriteSeatsBox(InfoBox box, HierarchyState hs)
        {
            box.Title(CompanyText.T("reallife_hier_seats"));
            box.Chip(ExecutiveHiring.Code(Rung.Executive));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
            foreach (ExecSeat seat in ExecutiveHiring.Seats)
            {
                box.Line(SeatSummary(hs, seat));
            }
            box.Note(CompanyText.T("reallife_hier_seats_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // What an executive did, newest first: what they settled, agreed, added and proposed, and their reports.
        private void WriteSeatLog(HierarchyState hs, ExecSeat seat, ref string selected)
        {
            _spec = CompanyTables.EscalationLog;
            ExecutiveFile x = hs.Executive(seat);
            int rows = 0;
            for (int i = hs.Entries.Count - 1; x != null && i >= 0 && rows < EscalationsShown; i--)
            {
                HierarchyEntry entry = hs.Entries[i];
                if (entry.Level != (int)Rung.Executive || !string.Equals(entry.Who, x.Name, StringComparison.Ordinal))
                {
                    continue;
                }
                rows++;
                Pick("le:" + entry.Id, EntryWhat(entry), EntryDetail(entry), CompanyText.T("reallife_mgr_row_day", "day", entry.Day.ToString()), PlaceName(entry.Place), 0, ref selected);
                Last.Pick = false;
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T(x == null ? "reallife_hier_seat_empty" : "reallife_hier_seat_no_log"));
            }
        }

        private void WriteSeat(InfoBox box, HierarchyState hs, ExecSeat seat)
        {
            ExecutiveFile x = hs.Executive(seat);
            int s = (int)seat;
            box.Title(CompanyDirector.SeatName(seat));
            box.Chip(ExecutiveHiring.Code(Rung.Executive));
            box.Chip(x == null ? CompanyText.T("reallife_mgr_empty_post") : x.Seen ? CompanyText.T("reallife_mgr_in_post") : CompanyText.T("reallife_hier_seat_waits"),
                x == null ? CellTone.Plain : x.Seen ? CellTone.Green : CellTone.Yellow);
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, x == null ? InfoBox.Positive : InfoBox.Additional, x == null ? "reallife_mgr_appoint" : "reallife_mgr_replace", () => Go("xa:" + s));
            if (x != null)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_mgr_stand_down", () => _director.HierarchyStandDownExecutive(s));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            box.Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant()));
            if (x == null)
            {
                box.Note(CompanyText.T("reallife_hier_seat_hire", "roles", SeatRoles(seat)));
                Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
                return;
            }
            box.Section("reallife_mgr_sec_manager");
            box.RowPair("reallife_mgr_name", x.Name, "reallife_mgr_since", CompanyText.Day(x.From));
            box.RowPair("reallife_hier_col_role", TechText.Role(x.Role), "reallife_hier_col_level", TechText.Level(x.Level));
            box.Row("reallife_hier_going", CompanyText.Wage(x.Going));
            WriteSeatPay(box, x);
            box.Section("reallife_hier_sec_area");
            box.RowPair("reallife_hier_area_open", _director.OpenOfSeat(seat).ToString(), "reallife_hier_area_settled", _director.SettledSince(null, x.Name, _director.Today - 7).ToString());
            box.RowPair("reallife_mgr_decided", x.Decided.ToString(), "reallife_hier_last_month", x.LastDecided.ToString());
            if (!x.Seen)
            {
                box.Line(CompanyText.T("reallife_hier_seat_waits_line"), CellTone.Yellow);
            }
        }

        // An executive's pay as the seat adds it: the allowance a day on top of their own, this month's and last.
        private void WriteSeatPay(InfoBox box, ExecutiveFile x)
        {
            box.RowPair("reallife_mgr_allowance", CompanyText.T("reallife_co_row_a_day", "amount", CompanyText.Money(CompanyDirector.AllowanceOf(x))),
                "reallife_hier_paid_month", CompanyText.Money(x.Paid));
            if (x.LastPaid > 0)
            {
                box.Line(CompanyText.T("reallife_hier_paid_last", "amount", CompanyText.Money(x.LastPaid), "decisions", x.LastDecided.ToString()));
            }
        }

        // The professions a seat is hired from, in words.
        private static string SeatRoles(ExecSeat seat)
        {
            int[] roles = ExecutiveHiring.SeatRoles[(int)seat];
            string list = string.Empty;
            for (int i = 0; i < roles.Length; i++)
            {
                list += (i > 0 ? ", " : string.Empty) + TechText.Role(roles[i]);
            }
            return list;
        }

        private void WriteCandidates(ExecSeat seat, ref string selected)
        {
            _spec = CompanyTables.Professionals;
            IReadOnlyList<ProfessionalHand> pros = _director.Professionals;
            int rows = 0;
            for (int i = 0; i < pros.Count; i++)
            {
                ProfessionalHand h = pros[i];
                if (h.Leaving || !ExecutiveHiring.Fits(seat, h.Role, h.Level))
                {
                    continue;
                }
                rows++;
                Pick("xc:" + (int)seat + "*" + h.Id, h.Name, TechText.Role(h.Role), TechText.Level(h.Level), CompanyText.Wage(h.Going), 0, ref selected);
                Last.Key[3] = h.Going / 100.0;
                Last.Set(4, h.PlaceName);
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_hier_exec_nobody"));
            }
        }

        private void WriteCandidatesBox(InfoBox box, ExecSeat seat)
        {
            int s = (int)seat;
            box.Title(CompanyText.T("reallife_mgr_appoint_title"));
            box.Subtitle(CompanyDirector.SeatName(seat));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("xp:" + s));
            box.Note(CompanyText.T("reallife_hier_seat_hire", "roles", SeatRoles(seat)));
            box.Note(CompanyText.T("reallife_hier_exec_pick"));
        }

        private void WriteCandidate(InfoBox box, ExecSeat seat, string id)
        {
            IReadOnlyList<ProfessionalHand> pros = _director.Professionals;
            int s = (int)seat;
            for (int i = 0; i < pros.Count; i++)
            {
                ProfessionalHand h = pros[i];
                if (h.Id != id)
                {
                    continue;
                }
                box.Title(h.Name);
                box.Subtitle(TechText.Role(h.Role) + ", " + TechText.Level(h.Level) + (h.PlaceName.Length > 0 ? ", " + h.PlaceName : string.Empty));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_mgr_appoint", () =>
                {
                    _director.HierarchyAppointExecutive(s, id);
                    Go("xp:" + s);
                });
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", () => Go("xa:" + s));
                box.Row("reallife_hier_going", CompanyText.Wage(h.Going));
                ExecutiveFile now = _director.HierarchyData.Executive(seat);
                if (now != null && now.Id != id)
                {
                    box.Note(CompanyText.T("reallife_mgr_appoint_replaces", "name", now.Name));
                }
                box.Note(CompanyText.T(PolicyCatalogue.Mod + "_hier_seat_what_" + seat.ToString().ToLowerInvariant()));
                return;
            }
            WriteCandidatesBox(box, seat);
        }

        // --- A region's dashboard ---

        private void WriteRegionRows(HierarchyState hs, string area, ref string selected)
        {
            _spec = CompanyTables.Escalations;
            int rows = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = hs.Escalations.Count - 1; i >= 0 && rows < EscalationsShown; i--)
                {
                    Escalation e = hs.Escalations[i];
                    ManagedPlace mp = e.Open == (pass == 0) && e.Place.Length > 0 ? _director.ManagedAt(e.Place) : null;
                    if (mp == null || !string.Equals(mp.Area, area, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    rows++;
                    Pick("he:" + e.Id, KindWords(e.Kind), PlaceWords(e), HolderWords(e), CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), 0, ref selected);
                    TableRow row = Last;
                    row.Kind = !e.Open ? CompanyTables.HeldClosed : e.Level >= EscalationRules.LevelOwner ? CompanyTables.HeldOwner
                        : e.Level == EscalationRules.LevelRegion ? CompanyTables.HeldRegion : CompanyTables.HeldExecutive;
                    row.Set(3, CompanyText.T("reallife_mgr_row_day", "day", e.Day.ToString()), e.Day);
                    row.Set(4, CompanyText.T(EscalationStateKeys[CompanyCatalogue.Clamp(e.State, 0, EscalationStateKeys.Length - 1)]), e.Open ? CellTone.Yellow : CellTone.Plain);
                }
            }
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_hier_dash_none"));
            }
        }

        private void WriteRegionDashboard(InfoBox box, string area)
        {
            ManagerFile r = _director.RegionOf(area);
            box.Title(CompanyText.T("reallife_hier_dash_title"));
            box.Subtitle(CompanyText.T("reallife_mgr_row_region", "district", CompanyDirector.AreaName(area)) + (r != null ? ", " + r.Name : string.Empty));
            box.Chip(ExecutiveHiring.Code(Rung.Regional));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", () => Go("rg:" + area));
            int places, managed, gaps, standing, profit, score, open;
            _director.RegionFigures(area, out places, out managed, out gaps, out standing, out profit, out score, out open);
            box.Section("reallife_hier_sec_figures");
            box.RowPair("reallife_mgr_over_places", Of(managed, places), "reallife_hier_fig_gaps", gaps.ToString());
            box.RowPair("reallife_hier_fig_standing", standing.ToString(), "reallife_hier_fig_score", score.ToString());
            box.RowPair("reallife_hier_fig_profit", CompanyText.Money(profit), "reallife_hier_pending", open.ToString());
            int target = PolicyCatalogue.Value(PolicyField.ProfitTarget, _director.RegionRule(area, PolicyField.ProfitTarget));
            box.Row("reallife_pol_field_profit_target", StepWords(PolicyField.ProfitTarget, _director.RegionRule(area, PolicyField.ProfitTarget)),
                target != PolicyCatalogue.ProfitNone && managed > 0 && profit < CompanyCatalogue.AtPrices(target, _director.Index) * managed ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_hier_sec_quick");
            Transform quick = box.ButtonRow();
            InfoBox.Button(quick, InfoBox.Positive, "reallife_hier_quick_fill", () => _director.HierarchyFillRegion(area));
            InfoBox.Button(quick, InfoBox.Additional, "reallife_hier_quick_raise", () => _director.HierarchyRaiseRegion(area));
            Transform more = box.ButtonRow();
            InfoBox.Button(more, InfoBox.Additional, "reallife_hier_quick_move", () => _director.HierarchyReallocate(area));
            InfoBox.Button(more, InfoBox.Additional, "reallife_hier_quick_appoint", () => _director.HierarchyAppointRegion(area));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_hier_open_policy", () => OpenPolicy(CompanyDirector.ScopeRegion, area));
            box.Note(CompanyText.T("reallife_hier_region_dash_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_hierarchy");
        }

        // A store's region, picked on its manager's panel: its district's, or one the player makes (A to H).
        private void RegionPicker(InfoBox box, ManagedPlace mp)
        {
            if (!_director.HierarchyOn || !box.HasSelector)
            {
                return;
            }
            string place = mp.Place.Place, district = mp.District;
            int now = HierarchyRules.CustomNumber(mp.Area);
            box.Headline("reallife_hier_region_pick");
            box.Selector(HierarchyRules.CustomMost + 1, now, i => i == 0 ? CompanyText.T("reallife_hier_region_own", "district", CompanyText.District(district))
                : CompanyText.T("reallife_hier_region_custom", "letter", HierarchyRules.Letter(i)), i => _director.HierarchyPickArea(place, i));
        }
    }
}
