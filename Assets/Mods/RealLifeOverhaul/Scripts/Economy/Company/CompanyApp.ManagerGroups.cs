using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Managers tab's directory (ManagerDirectoryRules): its first page, one row a group with its count, what it costs
    /// a month, its open decisions and how it stands, and an overview of the company's managers with a short list of what
    /// needs the player; then a page for each group: the regional managers by district, the managers of each kind of
    /// place (by district, then name, ordered and filtered by any column), the places with no manager, and what needs the
    /// player, each line a click from its page. Written when a page is shown or something changes, never per frame; the
    /// order and the filters a player picks are kept by the tables until the game is closed, never in the save.
    /// </summary>
    public sealed partial class CompanyApp
    {
        // The picks of the directory's groups, by ManagerGroup.
        private static readonly string[] GroupIds = { "mg:need", "mg:region", "mg:shop", "mg:works", "mg:warehouse", "mg:vacant" };
        private static readonly string[] GroupKeys =
        {
            "reallife_mgr_dir_need", "reallife_mgr_dir_regional", "reallife_mgr_dir_shop", "reallife_mgr_dir_works", "reallife_mgr_dir_warehouse", "reallife_mgr_dir_vacant"
        };

        private readonly List<ManagerLine> _lines = new List<ManagerLine>(16), _linePool = new List<ManagerLine>(16);
        private readonly DirectoryCounts _counts = new DirectoryCounts();
        private string[] _byDistrict = new string[16], _byName = new string[16], _byId = new string[16];
        private int[] _byOrder = new int[16];
        private readonly List<ManagerLine> _grouped = new List<ManagerLine>(16);
        private string _mgrGroup;    // the group page a place was opened from, for its Back; null from anywhere else
        private string _mgrRegion;   // the district's page a place was opened from, likewise

        private static string GroupPick(ManagerGroup group)
        {
            return GroupIds[(int)group];
        }

        private bool GroupPicked(out ManagerGroup group)
        {
            for (int g = 0; g < GroupIds.Length; g++)
            {
                if (_pick == GroupIds[g])
                {
                    group = (ManagerGroup)g;
                    return true;
                }
            }
            group = ManagerGroup.Count;
            return false;
        }

        // Where a place's Back goes: the district's page or the group it was opened from, else the group it is listed in.
        private string PlaceBack(ManagedPlace mp)
        {
            if (_mgrRegion != null && string.Equals(_mgrRegion, "rg:" + mp.District, StringComparison.Ordinal))
            {
                return _mgrRegion;
            }
            return _mgrGroup ?? GroupPick(ManagerDirectoryRules.GroupOf(mp.Kind, mp.Own != null));
        }

        // --- The first page ---

        private void WriteDirectory(InfoBox box, ManagerState ms, ref string selected)
        {
            _spec = CompanyTables.Managers;
            _mgrGroup = _mgrRegion = null;
            DirectoryCounts c = _counts;
            int needs = c.Needs[(int)ManagerGroup.Needs];
            if (c.AllPlaces == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_places"));
            }
            else
            {
                HierarchyRows(ref selected);
                GroupRow(ManagerGroup.Needs, needs.ToString(), string.Empty, string.Empty,
                    needs > 0 ? CompanyText.T("reallife_mgr_dir_look") : CompanyText.T("reallife_co_state_in_order"), needs > 0, ref selected);
                int regional = c.Managers[(int)ManagerGroup.Regional];
                GroupRow(ManagerGroup.Regional, Of(regional, c.Districts), CompanyText.Money(c.Cost[(int)ManagerGroup.Regional]), string.Empty,
                    StateOf(0, c.Sick[(int)ManagerGroup.Regional], c.Districts - regional), c.Sick[(int)ManagerGroup.Regional] > 0, ref selected);
                for (int g = (int)ManagerGroup.Shop; g <= (int)ManagerGroup.Warehouse; g++)
                {
                    if (c.Places[g] > 0)
                    {
                        GroupRow((ManagerGroup)g, Of(c.Managers[g], c.Places[g]), CompanyText.Money(c.Cost[g]), c.Open[g].ToString(),
                            StateOf(c.Needs[g], c.Sick[g], 0), c.Needs[g] > 0, ref selected);
                    }
                }
                int vacant = c.Places[(int)ManagerGroup.Vacant];
                if (vacant > 0)
                {
                    GroupRow(ManagerGroup.Vacant, vacant.ToString(), string.Empty, c.Open[(int)ManagerGroup.Vacant].ToString(),
                        CompanyText.T("reallife_mgr_dir_appoint"), false, ref selected);
                }
            }
            box.Title(CompanyText.T("reallife_mgr_tab"));
            box.Chip(CompanyText.T("reallife_mgr_chip_count", "count", c.AllManagers.ToString()), c.AllManagers > 0 ? CellTone.Green : CellTone.Plain);
            if (needs > 0)
            {
                box.Chip(CompanyText.T("reallife_mgr_chip_needs", "count", needs.ToString()), CellTone.Yellow);
            }
            box.Section("reallife_mgr_sec_directory");
            box.RowPair("reallife_mgr_dir_regional", Of(c.Managers[(int)ManagerGroup.Regional], c.Districts), "reallife_mgr_over_places", Of(c.PlaceManagers, c.AllPlaces));
            box.RowPair("reallife_mgr_over_cost", CompanyText.Money(c.AllCost), "reallife_mgr_over_open", c.AllOpen.ToString());
            box.RowPair("reallife_mgr_over_sick", c.AllSick.ToString(), "reallife_mgr_over_uncovered", c.Uncovered.ToString());
            int decided = 0, mistakes = 0, undone = 0, last = 0, lastMistakes = 0, lastUndone = 0, lastPaid = 0;
            for (int i = 0; i < ms.Managers.Count; i++)
            {
                ManagerFile m = ms.Managers[i];
                decided += m.Decided;
                mistakes += m.Mistakes;
                undone += m.Undone;
                last += m.LastDecided;
                lastMistakes += m.LastMistakes;
                lastUndone += m.LastUndone;
                lastPaid += m.LastPaid;
            }
            box.Section("reallife_mgr_sec_month");
            box.RowPair("reallife_mgr_decided", decided.ToString(), "reallife_mgr_mistakes", mistakes.ToString());
            box.RowPair("reallife_mgr_undone", undone.ToString(), "reallife_mgr_month_from", CompanyText.Day(ms.MonthFrom));
            if (last > 0 || lastPaid > 0)
            {
                box.Line(CompanyText.Fill("reallife_mgr_place_last", "decisions", last.ToString(), "mistakes", lastMistakes.ToString(),
                    "undone", lastUndone.ToString(), "amount", CompanyText.Money(lastPaid)));
            }
            box.Section("reallife_mgr_sec_needs");
            int listed = NeedRows(box, false, ManagerDirectoryRules.NeedsShown, ref selected);
            if (listed == 0)
            {
                box.Note(CompanyText.T("reallife_mgr_needs_none"));
            }
            else if (needs > listed)
            {
                box.Note(CompanyText.T("reallife_co_over_more", "count", (needs - listed).ToString()));
            }
            if (RotaGame.LeftOut > 0)
            {
                box.Note(CompanyText.T("reallife_mgr_left_out", "count", RotaGame.LeftOut.ToString(), "most", CompanyCatalogue.ShopsMost.ToString()));
            }
            box.Note(CompanyText.T("reallife_mgr_dir_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        // A group's row on the first page: 0 the group, 1 its count, 2 its cost a month, 3 its open decisions, 4 how it stands.
        private void GroupRow(ManagerGroup group, string count, string cost, string open, string state, bool attention, ref string selected)
        {
            Pick(GroupPick(group), CompanyText.T(GroupKeys[(int)group]), count, cost, open, 0, ref selected);
            Last.Set(4, state, attention ? CellTone.Yellow : CellTone.Plain);
        }

        private static string Of(int count, int all)
        {
            return CompanyText.T("reallife_mgr_dir_of", "count", count.ToString(), "all", all.ToString());
        }

        // How a group stands in a few words: what needs the player first, then those off sick, then empty posts.
        private static string StateOf(int needs, int sick, int empty)
        {
            return needs > 0 ? CompanyText.T("reallife_mgr_dir_needs", "count", needs.ToString())
                : sick > 0 ? CompanyText.T("reallife_mgr_dir_sick", "count", sick.ToString())
                : empty > 0 ? CompanyText.T("reallife_mgr_dir_empty", "count", empty.ToString())
                : CompanyText.T("reallife_co_state_in_order");
        }

        // --- A group's page ---

        private void WriteGroup(InfoBox box, ManagerState ms, ManagerGroup group, ref string selected)
        {
            _mgrGroup = _pick;
            _mgrRegion = null;
            ManagedKind kind;
            if (ManagerDirectoryRules.KindOf(group, out kind))
            {
                WriteKind(box, kind, ref selected);
            }
            else if (group == ManagerGroup.Regional)
            {
                WriteRegions(box, ref selected);
            }
            else if (group == ManagerGroup.Vacant)
            {
                WriteVacancies(box, ref selected);
            }
            else
            {
                WriteNeeds(box);
            }
        }

        private void GroupHead(InfoBox box, ManagerGroup group, string chip, CellTone tone)
        {
            box.Title(CompanyText.T(GroupKeys[(int)group]));
            box.Chip(chip, tone);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
        }

        // The places of the lines chosen into _grouped, by district and then name (ManagerDirectoryRules.Order).
        private void Grouped(ManagedKind kind, bool any, bool has)
        {
            _grouped.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                if ((any || _lines[i].Mp.Kind == kind) && _lines[i].Line.Has == has)
                {
                    _grouped.Add(_lines[i]);
                }
            }
            int n = _grouped.Count;
            if (_byOrder.Length < n)
            {
                _byDistrict = new string[n * 2];
                _byName = new string[n * 2];
                _byId = new string[n * 2];
                _byOrder = new int[n * 2];
            }
            for (int i = 0; i < n; i++)
            {
                ManagedPlace mp = _grouped[i].Mp;
                _byDistrict[i] = CompanyText.District(mp.District);
                _byName[i] = mp.Place.Name;
                _byId[i] = mp.Place.Place;
            }
            ManagerDirectoryRules.Order(_byDistrict, _byName, _byId, n, _byOrder);
        }

        private void WriteKind(InfoBox box, ManagedKind kind, ref string selected)
        {
            TableSpec spec = CompanyTables.ManagerKinds[(int)kind];
            _spec = spec;
            TableFilter districts = spec.Filters[0];
            districts.Names.Clear();
            Grouped(kind, false, true);
            for (int k = 0; k < _grouped.Count; k++)
            {
                ManagerLine line = _grouped[_byOrder[k]];
                ManagedPlace mp = line.Mp;
                ManagerFile m = mp.Own;
                string district = _byDistrict[_byOrder[k]];
                Pick("mp:" + mp.Place.Place, mp.Place.Name, m.Name, district, m.Skill.ToString(), 0, ref selected);
                TableRow row = Last;
                row.Kind = CompanyTables.KindOf(districts, district);
                row.Kind2 = ManagerDirectoryRules.FilterOf(line.State, line.Needs);
                row.Set(3, m.Skill.ToString(), m.Skill);
                row.Set(4, CompanyText.Money(line.Line.Cost), line.Line.Cost);
                row.Set(5, line.Line.Decided.ToString(), line.Line.Decided);
                row.Set(6, line.Line.Open.ToString(), line.Line.Open);
                row.Set(7, line.Needs != 0 ? NeedWords(line) : PostWords(line.State, mp.Region), ManagerDirectoryRules.StateOrder(line.State, line.Needs),
                    line.Needs != 0 || line.State == PostState.Uncovered ? CellTone.Yellow : CellTone.Plain);
            }
            ManagerGroup group = ManagerDirectoryRules.KindGroup(kind);
            DirectoryCounts c = _counts;
            GroupHead(box, group, CompanyText.T("reallife_mgr_chip_count", "count", c.Managers[(int)group].ToString()), c.Managers[(int)group] > 0 ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_mgr_sec_directory");
            box.RowPair("reallife_mgr_over_places", Of(c.Managers[(int)group], c.Places[(int)group]), "reallife_mgr_over_cost", CompanyText.Money(c.Cost[(int)group]));
            box.RowPair("reallife_mgr_over_open", c.Open[(int)group].ToString(), "reallife_mgr_over_sick", c.Sick[(int)group].ToString());
            int vacant = c.Places[(int)group] - c.Managers[(int)group];
            if (vacant > 0)
            {
                box.Line(CompanyText.T("reallife_mgr_group_vacant", "count", vacant.ToString()), CellTone.Yellow);
                int chosen = (int)kind;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_mgr_dir_vacant", () =>
                {
                    CompanyTables.ManagerVacancies.Filters[0].Chosen = chosen;   // the vacancies of this kind; the table puts it back to all if none are left
                    Go(GroupPick(ManagerGroup.Vacant));
                });
            }
            box.Note(CompanyText.T("reallife_mgr_group_places_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        private void WriteRegions(InfoBox box, ref string selected)
        {
            _spec = CompanyTables.ManagerRegions;
            IReadOnlyList<string> districts = _director.ManagerDistricts;
            int covered = 0;
            for (int i = 0; i < districts.Count; i++)
            {
                string district = districts[i];
                ManagerFile r = _director.RegionOf(district);
                bool sick = CompanyDirector.Sick(r);
                int places = 0, standing = r != null ? _director.StandingIn(district) : 0;
                for (int k = 0; k < _lines.Count; k++)
                {
                    places += _lines[k].Line.Has && string.Equals(_lines[k].Mp.District, district, StringComparison.Ordinal) ? 1 : 0;
                }
                covered += standing;
                int cost = _director.MonthCostOf(r);
                Pick("rg:" + district, CompanyText.District(district), r != null ? r.Name : CompanyText.T("reallife_mgr_none"), r != null ? r.Skill.ToString() : string.Empty,
                    places.ToString(), 0, ref selected);
                TableRow row = Last;
                row.Kind = r == null ? CompanyTables.RegionEmpty : sick ? CompanyTables.RegionSick : CompanyTables.RegionInPost;
                row.Set(2, r != null ? r.Skill.ToString() : string.Empty, r != null ? r.Skill : double.NaN);
                row.Set(3, places.ToString(), places);
                row.Set(4, standing.ToString(), standing);
                row.Set(5, r != null ? CompanyText.Money(cost) : string.Empty, r != null ? cost : double.NaN);
                row.Set(6, RegionState(r), r == null ? 2 : sick ? 0 : 1, sick ? CellTone.Yellow : CellTone.Plain);
            }
            if (districts.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_row_no_places"));
            }
            DirectoryCounts c = _counts;
            int regional = c.Managers[(int)ManagerGroup.Regional];
            GroupHead(box, ManagerGroup.Regional, Of(regional, c.Districts), regional > 0 ? CellTone.Green : CellTone.Plain);
            box.Section("reallife_mgr_sec_directory");
            box.RowPair("reallife_mgr_over_cost", CompanyText.Money(c.Cost[(int)ManagerGroup.Regional]), "reallife_mgr_over_sick", c.Sick[(int)ManagerGroup.Regional].ToString());
            box.RowPair("reallife_mgr_standing_in", covered.ToString(), "reallife_mgr_over_uncovered", c.Uncovered.ToString());
            box.Note(CompanyText.T("reallife_mgr_group_regional_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        private void WriteVacancies(InfoBox box, ref string selected)
        {
            TableSpec spec = CompanyTables.ManagerVacancies;
            _spec = spec;
            TableFilter districts = spec.Filters[1];
            districts.Names.Clear();
            Grouped(ManagedKind.Shop, true, false);
            for (int k = 0; k < _grouped.Count; k++)
            {
                ManagerLine line = _grouped[_byOrder[k]];
                ManagedPlace mp = line.Mp;
                string district = _byDistrict[_byOrder[k]];
                int staff = mp.Place.Staff;
                Pick("ma:" + mp.Place.Place, mp.Place.Name, KindName(mp.Kind), district, staff.ToString(), 0, ref selected);
                TableRow row = Last;
                row.Kind = (int)mp.Kind;
                row.Kind2 = CompanyTables.KindOf(districts, district);
                row.Set(3, staff.ToString(), staff);
                row.Set(4, mp.Region != null ? mp.Region.Name : CompanyText.T("reallife_mgr_none"));
                row.Set(5, staff > 0 ? CompanyText.T("reallife_mgr_vacant_staff", "count", staff.ToString()) : CompanyText.T("reallife_mgr_vacant_nobody"),
                    staff > 0 ? CellTone.Plain : CellTone.Yellow);
            }
            if (_grouped.Count == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_vacant_none"));
            }
            DirectoryCounts c = _counts;
            int vacant = c.Places[(int)ManagerGroup.Vacant];
            GroupHead(box, ManagerGroup.Vacant, vacant.ToString(), vacant > 0 ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_mgr_sec_directory");
            box.RowPair("reallife_mgr_dir_vacant", Of(vacant, c.AllPlaces), "reallife_mgr_over_open", c.Open[(int)ManagerGroup.Vacant].ToString());
            box.Note(CompanyText.T("reallife_mgr_group_vacant_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        private void WriteNeeds(InfoBox box)
        {
            _spec = CompanyTables.ManagerNeeds;
            string selected = null;
            int rows = NeedRows(box, true, int.MaxValue, ref selected);
            if (rows == 0)
            {
                Say("none", CompanyText.T("reallife_mgr_needs_none"));
            }
            int mistakes = 0, uncovered = 0, budgets = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                int needs = _lines[i].Needs;
                mistakes += (needs & ManagerDirectoryRules.NeedMistake) != 0 ? 1 : 0;
                uncovered += (needs & ManagerDirectoryRules.NeedUncovered) != 0 ? 1 : 0;
                budgets += (needs & ManagerDirectoryRules.NeedBudget) != 0 ? 1 : 0;
            }
            GroupHead(box, ManagerGroup.Needs, rows.ToString(), rows > 0 ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_mgr_sec_needs");
            box.RowPair("reallife_mgr_need_kind_mistake", mistakes.ToString(), "reallife_mgr_need_kind_uncovered", uncovered.ToString());
            box.Row("reallife_mgr_need_kind_budget", budgets.ToString());
            box.Note(CompanyText.T("reallife_mgr_group_need_what"));
            Meta.Guide.Point(box, "reallife_meta_title_economy_managers");
        }

        // What needs the player at a place, in a few words: the first of its reasons.
        private string NeedWords(ManagerLine line)
        {
            if ((line.Needs & ManagerDirectoryRules.NeedUncovered) != 0)
            {
                return CompanyText.T("reallife_mgr_need_uncovered");
            }
            if ((line.Needs & ManagerDirectoryRules.NeedMistake) != 0)
            {
                return CompanyText.T("reallife_mgr_need_mistakes", "count", line.Line.OpenMistakes.ToString());
            }
            return CompanyText.T("reallife_mgr_need_budget", "spent", CompanyText.Money(line.Line.Spent), "budget", CompanyText.Money(line.Line.Budget));
        }

        /// <summary>
        /// What needs the player, one line a place and reason, by place in the order the places were read: into the table
        /// (each a click from its page: a single mistake's decision, else the place), or at most <paramref name="most"/>
        /// lines into the box. Returns the lines written.
        /// </summary>
        private int NeedRows(InfoBox box, bool table, int most, ref string selected)
        {
            int written = 0;
            int today = _director.Today;
            for (int i = 0; i < _lines.Count && written < most; i++)
            {
                ManagerLine line = _lines[i];
                ManagedPlace mp = line.Mp;
                string who = mp.Own != null ? mp.Own.Name : CompanyText.T("reallife_mgr_none");
                for (int reason = 0; reason < 3 && written < most; reason++)
                {
                    int bit = reason == CompanyTables.NeedMistake ? ManagerDirectoryRules.NeedMistake
                        : reason == CompanyTables.NeedUncovered ? ManagerDirectoryRules.NeedUncovered : ManagerDirectoryRules.NeedBudget;
                    if ((line.Needs & bit) == 0)
                    {
                        continue;
                    }
                    string why;
                    string pick = "mp:" + mp.Place.Place;
                    int day = today;
                    if (reason == CompanyTables.NeedMistake)
                    {
                        Decision d = NewestMistake(mp.Place.Place);
                        why = line.Line.OpenMistakes == 1 && d != null ? CompanyText.T("reallife_mgr_need_mistake", "what", TaskName(d.Task))
                            : CompanyText.T("reallife_mgr_need_mistakes", "count", line.Line.OpenMistakes.ToString());
                        pick = line.Line.OpenMistakes == 1 && d != null ? "md:" + d.Id : pick;
                        day = d != null ? d.Day : today;
                    }
                    else if (reason == CompanyTables.NeedUncovered)
                    {
                        why = CompanyText.T("reallife_mgr_need_uncovered");
                    }
                    else
                    {
                        why = CompanyText.T("reallife_mgr_need_budget", "spent", CompanyText.Money(line.Line.Spent), "budget", CompanyText.Money(line.Line.Budget));
                    }
                    written++;
                    if (!table)
                    {
                        box.Line("<b>" + mp.Place.Name + "</b> " + why, CellTone.Yellow);
                        continue;
                    }
                    Pick(pick, mp.Place.Name, who, why, CompanyText.T("reallife_mgr_row_day", "day", day.ToString()), 0, ref selected);
                    TableRow row = Last;
                    row.Id = pick + "#" + reason;   // one place can be on two lines; the click reads what is before the mark
                    row.Kind = reason;
                    row.Set(3, CompanyText.T("reallife_mgr_row_day", "day", day.ToString()), day);
                }
            }
            return written;
        }

        private Decision NewestMistake(string place)
        {
            ManagerState ms = _director.ManagerData;
            for (int i = ms != null ? ms.Decisions.Count - 1 : -1; i >= 0; i--)
            {
                Decision d = ms.Decisions[i];
                if (d.Open && d.Mistake && d.Place == place)
                {
                    return d;
                }
            }
            return null;
        }
    }
}
