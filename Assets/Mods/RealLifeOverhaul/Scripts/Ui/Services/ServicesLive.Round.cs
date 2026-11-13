using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class ServicesLive
    {
        private sealed partial class Page
        {
            private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(16);
            private readonly List<EmployeeInstance> _cleaners = new List<EmployeeInstance>(16);
            private readonly CrewMember _member = new CrewMember();

            // The chips over a table: each a view, with how many rows it holds.
            private void Chips(string[] labels, int[] counts)
            {
                _v.Filter = Math.Max(0, Math.Min(labels.Length - 1, _v.Filter));
                _m.Chips = new UiActionModel[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    _m.Chips[i] = ServicesRuns.Chip(ServicesText.T("reallife_ui_svc_chip", "name", labels[i], "count", counts[i].ToString(Inv)), ServicesFace.ChipCommand, i);
                }
                _m.ChipAt = _v.Filter;
            }

            private UiActionModel BackButton() => new UiActionModel
            {
                Label = ServicesText.T("reallife_co_back"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = ServicesFace.BackCommand, Arg = 0
            };

            private const long N = TableRules.None;   // a sort key for a cell with nothing to order by

            // The table of rows written, in the order its head is set to (as written where none is) and with the sums of the columns given at its foot. The keys come back in step.
            private UiDataTableModel Arranged(UiDataColumn[] columns, List<UiDataRow> rows, List<string> keys, int[] sums, out string[] shownKeys)
            {
                for (int c = 0; c < columns.Length; c++)
                {
                    columns[c].Sortable = columns[c].Kind != UiCellKind.Action && !string.IsNullOrEmpty(columns[c].Title);
                }
                int sort = _v.SortColumn >= 0 && _v.SortColumn < columns.Length && columns[_v.SortColumn].Sortable ? _v.SortColumn : -1;
                bool down = sort >= 0 && _v.SortDown;
                UiDataRow[] written = rows.ToArray();
                int[] order = TableRules.Order(written, sort, down, null);
                var t = new UiDataTableModel { Columns = columns, Rows = sort >= 0 ? TableRules.Take(written, order) : written, RowCount = written.Length, SortColumn = sort, SortDown = down };
                shownKeys = sort >= 0 ? TableRules.Take(keys, order) : keys.ToArray();
                if (sums != null && written.Length > 0)
                {
                    t.TotalsLabel = ServicesText.T("reallife_table_total");
                    t.Totals = new string[columns.Length];
                    for (int i = 0; i < sums.Length; i++)
                    {
                        t.Totals[sums[i]] = Money((float)Math.Min(TableRules.Sum(written, order, sums[i]), int.MaxValue / 2));
                    }
                }
                return t;
            }

            private void Rows(UiDataColumn[] columns, List<UiDataRow> rows, List<string> keys, string emptyTitle, string emptyBody, UiIcon icon, int[] sums = null)
            {
                _m.Table = Arranged(columns, rows, keys, sums, out string[] shown);
                _m.RowKeys = shown;
                int picked = Array.IndexOf(shown, _v.Pick);
                if (_v.Pick != null && picked < 0)
                {
                    _v.Pick = null;   // a row that is gone (a contract ended, a business sold): the page goes back to the table
                }
                _m.Table.Selected = picked;
                _m.Table.EmptyTitle = emptyTitle;
                _m.Table.EmptyBody = emptyBody;
                _m.Table.EmptyIcon = icon;
            }

            // ------------------------------------------------------------------------------------------------- Cleaning

            public void Cleaning()
            {
                _m.Shape = ServicesShape.Table;
                int now = ServicesGame.Now, own = 0, contract = 0, notOn = 0;
                var rows = new List<UiDataRow>(24);
                var keys = new List<string>(24);
                CleaningClient pickedClient = null;
                BuildingRegistration adding = null;
                for (int i = 0; i < _s.Clients.Count; i++)
                {
                    CleaningClient c = _s.Clients[i];
                    own += c.Outside ? 0 : 1;
                    contract += c.Outside ? 1 : 0;
                    if (_v.Filter == 2 && !c.Outside || _v.Filter == 1 && c.Outside || _v.Filter == 3)
                    {
                        continue;
                    }
                    int need = _d.NeedMinutes(c);
                    string next = c.Unserved > 0 ? ServicesText.Why(c.Unserved)
                        : c.NextStart >= 0 ? ServicesText.T(c.NextStart <= now ? "reallife_svc_row_in_now" : "reallife_svc_row_next", "time", ServicesText.Clock(c.NextStart), "name", c.NextBy)
                        : !c.Outside && !c.Open ? ServicesText.T("reallife_svc_row_closed")
                        : ServicesText.T(c.Mode == ServicesCatalogue.ModeAsNeeded ? "reallife_svc_row_waiting" : "reallife_svc_row_done");
                    string state = c.Unserved > 0 ? ServicesText.T("reallife_ui_svc_state_nobody") : ServicesText.T(c.Outside ? "reallife_svc_row_contract" : "reallife_svc_row_on");
                    UiRole tone = c.Unserved > 0 ? UiRole.Warn : c.Outside ? UiRole.Notice : UiRole.Ok;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { c.Name, need > 0 ? ServicesText.T("reallife_ui_svc_min", "count", need.ToString(Inv)) : string.Empty,
                            ServicesText.T("reallife_ui_svc_min", "count", c.MinutesToday.ToString(Inv)), c.Now.ToString(Inv), next, state },
                        Values = new[] { (int)(c.Outside ? UiIcon.Building : UiIcon.Store), 0, 0, c.Now, 0, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone },
                        Subs = new[] { ServicesText.District(c.District), null, null, null, null, null },
                        SortKeys = new[] { N, need > 0 ? need : N, c.MinutesToday, c.Now, N, N }
                    });
                    keys.Add("cli:" + c.Place);
                    pickedClient = "cli:" + c.Place == _v.Pick ? c : pickedClient;
                }
                ServicesGame.Shops(_shops);
                for (int i = 0; i < _shops.Count; i++)
                {
                    string place = ServicesGame.Place(_shops[i]);
                    if (_s.Client(place) != null)
                    {
                        continue;
                    }
                    notOn++;
                    if (_v.Filter == 1 || _v.Filter == 2)
                    {
                        continue;
                    }
                    int clean = ServicesGame.Cleanliness(_shops[i]);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { ServicesGame.Name(_shops[i]), string.Empty, string.Empty, clean.ToString(Inv), string.Empty, ServicesText.T("reallife_ui_svc_state_not_on") },
                        Values = new[] { (int)UiIcon.Store, 0, 0, clean, 0, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Muted },
                        Subs = new[] { ServicesText.District(GameRegistrations.District(_shops[i])), null, null, null, null, null },
                        SortKeys = new[] { N, N, N, clean, N, N }
                    });
                    keys.Add("add:" + place);
                    adding = "add:" + place == _v.Pick ? _shops[i] : adding;
                }
                var columns = new[]
                {
                    new UiDataColumn { Title = ServicesText.T("reallife_co_col_business"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_needs"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_got"), Kind = UiCellKind.Figure, Width = 112f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_cleanliness"), Kind = UiCellKind.Meter, Width = 152f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_next"), Kind = UiCellKind.Text, Width = 232f },
                    new UiDataColumn { Title = ServicesText.T("reallife_co_col_state"), Kind = UiCellKind.Chip, Width = 152f }
                };
                Chips(new[] { ServicesText.T("reallife_ui_svc_chip_all"), ServicesText.T("reallife_ui_svc_chip_own"), ServicesText.T("reallife_ui_svc_chip_contracts"),
                    ServicesText.T("reallife_ui_svc_chip_not_on") }, new[] { own + contract + notOn, own, contract, notOn });
                Rows(columns, rows, keys, ServicesText.T("reallife_ui_svc_no_round"), ServicesText.T("reallife_svc_empty_cleaning"), UiIcon.Spark);
                _m.Side = pickedClient != null ? ClientCard(pickedClient, now) : adding != null ? AddCard(adding) : ServiceCard();
                _shops.Clear();
            }

            // The service as a whole: who is on it, its cost against one cleaner per shop, the van, today's round.
            private UiFactCardModel ServiceCard()
            {
                int planned = 0, left = 0, now = ServicesGame.Now;
                for (int i = 0; i < _s.Clients.Count; i++)
                {
                    left += _s.Clients[i].Unserved > 0 ? 1 : 0;
                }
                IReadOnlyList<DayVisit> visits = _d.Visits;
                for (int i = 0; i < visits.Count; i++)
                {
                    planned += visits[i].End > now ? 1 : 0;
                }
                int wages, van, own = _d.OwnWeek(), fees = _d.ContractWeekFees();
                int service = _d.ServiceWeek(out wages, out van);
                var lines = new List<UiFactLine>(10);
                if (_s.Crew.Count == 0)
                {
                    lines.Add(UiFactLine.Tick(ServicesText.T("reallife_svc_service_no_crew"), false));
                }
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_service_who", "crew", _s.Crew.Count.ToString(Inv), "businesses", _s.Clients.Count.ToString(Inv),
                    "contracts", ContractRules.Running(_s).ToString(Inv))));
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_service_week"), Money(service)));
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_ui_svc_week_split", "wages", Money(wages), "van", Money(van)), UiRole.Muted));
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_own_week"), Money(own)));
                if (fees > 0)
                {
                    lines.Add(UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_fees_week"), Money(fees)));
                }
                lines.Add(UiFactLine.Say(ServicesText.T(left > 0 ? "reallife_svc_service_round_left" : "reallife_svc_service_round", "count", planned.ToString(Inv), "left", left.ToString(Inv))));
                lines.Add(_runs.Switch(ServicesText.T("reallife_ui_svc_van"), _s.Van, on => _d.SetVan(on)));
                lines.Add(UiFactLine.Say(ServicesText.T(_s.Van ? "reallife_svc_van_says_on" : "reallife_svc_van_says_off", "cost", Money(TravelRules.PerKm(ServicesGame.Index))), UiRole.Muted));
                if (_s.Crew.Count > 0)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_service_pick")));
                }
                return new UiFactCardModel { Title = ServicesText.T("reallife_ui_svc_sec_service"), Heading = true, Lines = lines.ToArray() };
            }

            private UiFactCardModel ClientCard(CleaningClient c, int now)
            {
                string place = c.Place;
                int need = _d.NeedMinutes(c);
                var lines = new List<UiFactLine>(14);
                if (c.Unserved > 0)
                {
                    lines.Add(UiFactLine.Tick(ServicesText.Why(c.Unserved), false));
                }
                else if (!c.Outside && !c.Open)
                {
                    lines.Add(UiFactLine.Tick(ServicesText.T("reallife_svc_client_closed"), false));
                }
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_svc_client_clean"), ServicesText.Trend(c.Now, c.CleanBefore)));
                lines.Add(UiFactLine.Say(need > 0
                    ? ServicesText.T("reallife_svc_client_needs", "need", ServicesText.Minutes(need), "today", ServicesText.Minutes(c.MinutesToday), "before", ServicesText.Minutes(c.MinutesBefore))
                    : ServicesText.T("reallife_svc_client_needs_unknown", "today", ServicesText.Minutes(c.MinutesToday))));
                if (c.LastDay > 0)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T(c.LastDay == _day ? "reallife_svc_client_last_today" : "reallife_svc_client_last", "name", c.LastCleaner,
                        "time", ServicesText.Clock(c.LastMinute),
                        "day", c.LastDay.ToString(Inv))));
                }
                if (c.Unserved == 0 && c.NextStart >= 0)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_client_next", "name", c.NextBy, "from", ServicesText.Clock(c.NextStart), "to",
                        ServicesText.Clock(c.NextEnd)), UiRole.OkText));
                }
                if (c.Outside)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_client_contract", "amount", Money(c.Fee), "standard", c.Standard.ToString(Inv))));
                }
                lines.Add(UiFactLine.Section(ServicesText.T("reallife_ibox_sec_how_often")));
                int mode = c.Mode;
                lines.Add(_runs.Step(ServicesText.T("reallife_ui_svc_row_how_often"),
                    ServicesText.T(mode == 0 ? "reallife_ui_svc_visits_needed" : "reallife_ui_svc_visits_n", "count", mode.ToString(Inv)), null,
                    mode > 0, mode < ServicesCatalogue.VisitsMost, () => _d.SetMode(place, mode - 1), () => _d.SetMode(place, mode + 1)));
                if (mode == ServicesCatalogue.ModeAsNeeded)
                {
                    int at = 0;
                    int[] steps = ServicesCatalogue.Thresholds;
                    for (int i = 0; i < steps.Length; i++)
                    {
                        at = steps[i] <= c.Threshold ? i : at;
                    }
                    int index = at;
                    lines.Add(_runs.Step(ServicesText.T("reallife_ui_svc_row_when_under"), steps[at].ToString(Inv), ServicesText.T("reallife_ui_svc_threshold_hint", "value", steps[at].ToString(Inv)),
                        at > 0, at < steps.Length - 1, () => _d.SetThreshold(place, steps[index - 1]), () => _d.SetThreshold(place, steps[index + 1])));
                }
                else
                {
                    for (int k = 0; k < mode; k++)
                    {
                        int visit = k, hour = c.Hours[k];
                        lines.Add(_runs.Step(ServicesText.T("reallife_ui_svc_row_visit_at", "number", (k + 1).ToString(Inv)), ServicesText.Hour(hour), null,
                            hour > 0, hour < 23, () => _d.SetHour(place, visit, hour - 1), () => _d.SetHour(place, visit, hour + 1)));
                    }
                }
                var actions = new List<UiActionModel>(2) { BackButton() };
                if (!c.Outside)
                {
                    actions.Add(_runs.Button(ServicesText.T("reallife_svc_client_remove"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () =>
                    {
                        _v.Pick = null;
                        _d.RemoveClient(place);
                    }));
                }
                return new UiFactCardModel
                {
                    Title = c.Name, Sub = ServicesText.District(c.District), Chip = ServicesText.T(c.Outside ? "reallife_svc_row_contract" : "reallife_svc_row_on"),
                    ChipTone = c.Unserved > 0 ? UiRole.Warn : UiRole.Ok, Lines = lines.ToArray(), Actions = actions.ToArray()
                };
            }

            // A shop of the player's that is not on the round.
            private UiFactCardModel AddCard(BuildingRegistration reg)
            {
                string place = ServicesGame.Place(reg);
                int hours = ServicesGame.OwnCleaningHours(reg);
                var lines = new List<UiFactLine>(4)
                {
                    UiFactLine.Row(ServicesText.T("reallife_svc_client_clean"), ServicesGame.Cleanliness(reg).ToString(Inv)),
                    UiFactLine.Say(ServicesText.T(hours > 0 ? "reallife_svc_add_own_hours" : "reallife_svc_add_no_hours", "hours", hours.ToString(Inv))),
                    UiFactLine.Say(ServicesText.T("reallife_svc_add_says"), UiRole.Muted)
                };
                return new UiFactCardModel
                {
                    Title = ServicesGame.Name(reg), Sub = ServicesText.District(GameRegistrations.District(reg)), Lines = lines.ToArray(),
                    Actions = new[]
                    {
                        BackButton(),
                        _runs.Button(ServicesText.T("reallife_svc_add_do"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () =>
                        {
                            _v.Pick = "cli:" + place;
                            _d.AddClient(place);
                        })
                    }
                };
            }

            // ------------------------------------------------------------------------------------------------- Crew

            public void Crew()
            {
                _m.Shape = ServicesShape.Table;
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                ServicesGame.Cleaners(_cleaners);
                int on = 0, off = 0;
                EmployeeInstance picked = null;
                for (int i = 0; i < _cleaners.Count; i++)
                {
                    ServicesGame.Fill(_member, _cleaners[i]);
                    bool crew = _s.InCrew(_member.Id);
                    on += crew ? 1 : 0;
                    off += crew ? 0 : 1;
                    if (_v.Filter == 1 && !crew || _v.Filter == 2 && crew)
                    {
                        continue;
                    }
                    string today = !_member.Present ? ServicesText.T("reallife_svc_crew_sick")
                        : _member.TodayFrom >= 0 ? ServicesText.T("reallife_svc_crew_shift", "from", ServicesText.Clock(_member.TodayFrom), "to",
                            ServicesText.Clock(Math.Min(_member.TodayTo, 24 * 60 - 1)))
                        : ServicesText.T("reallife_svc_crew_no_shift");
                    int week = (int)((long)_member.Wage * _member.WeekHours / 100);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { _member.Name, _member.Skill.ToString(Inv), _member.Mood.ToString(Inv), CompanyText.Wage(_member.Wage), _member.WeekHours.ToString(Inv), Money(week), today,
                        crew && _s.GroupOf(_member.Id) >= 0 ? GroupName(_s.GroupOf(_member.Id)) : ServicesText.T(crew ? "reallife_svc_crew_on" : "reallife_svc_crew_off") },
                        Values = new[] { 0, _member.Skill, _member.Mood, 0, 0, 0, 0, 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, crew ? UiRole.Ok : UiRole.Muted },
                        Subs = new[] { _member.Base != null ? _member.BaseName : ServicesText.T("reallife_svc_crew_no_base"), null, null, null, null, null, null, null },
                        Hue = UiColour.HueOf(_member.Name), Ring = _member.Mood >= 70 ? UiRole.Ok : _member.Mood >= 40 ? UiRole.Warn : UiRole.Danger,
                        SortKeys = new[] { N, _member.Skill, _member.Mood, _member.Wage, _member.WeekHours, week, N, N }
                    });
                    keys.Add("emp:" + _member.Id);
                    picked = "emp:" + _member.Id == _v.Pick ? _cleaners[i] : picked;
                }
                var columns = new[]
                {
                    new UiDataColumn { Title = ServicesText.T("reallife_co_col_name"), Kind = UiCellKind.Person, Width = 0f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_skill"), Kind = UiCellKind.Meter, Width = 128f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_mood"), Kind = UiCellKind.Meter, Width = 128f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_wage"), Kind = UiCellKind.Figure, Width = 104f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_hours"), Kind = UiCellKind.Figure, Width = 104f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_week"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_today"), Kind = UiCellKind.Text, Width = 168f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_crew"), Kind = UiCellKind.Chip, Width = 152f }
                };
                Chips(new[] { ServicesText.T("reallife_ui_svc_chip_all"), ServicesText.T("reallife_svc_kind_crew_on"), ServicesText.T("reallife_svc_kind_crew_off") }, new[] { on + off, on, off });
                Rows(columns, rows, keys, ServicesText.T("reallife_ui_svc_no_cleaners"), ServicesText.T("reallife_svc_row_no_cleaner"), UiIcon.People, new[] { 5 });
                if (picked == null)
                {
                    _m.Side = new UiFactCardModel
                    {
                        Title = ServicesText.T("reallife_ui_svc_sec_crew"), Heading = true,
                        Lines = new[]
                        {
                            UiFactLine.Say(ServicesText.T("reallife_svc_crew_says_hire")), UiFactLine.Say(ServicesText.T("reallife_svc_crew_says_hours")),
                            UiFactLine.Say(ServicesText.T("reallife_svc_crew_says_pay")), UiFactLine.Say(ServicesText.T("reallife_svc_crew_says_pick"), UiRole.Muted)
                        }
                    };
                }
                else
                {
                    _m.Side = CleanerCard(picked);
                }
                _cleaners.Clear();
                _member.Employee = null;
                _member.Base = null;
            }

            private UiFactCardModel CleanerCard(EmployeeInstance e)
            {
                ServicesGame.Fill(_member, e);
                string id = _member.Id;
                bool on = _s.InCrew(id);
                var lines = new List<UiFactLine>(10);
                if (_member.Base == null || _member.WeekHours == 0)
                {
                    lines.Add(UiFactLine.Tick(ServicesText.T("reallife_svc_crew_needs_shift"), false));
                }
                lines.Add(UiFactLine.Bar(ServicesText.T("reallife_ibox_skill"), _member.Skill));
                lines.Add(UiFactLine.Bar(ServicesText.T("reallife_ui_svc_row_mood"), _member.Mood));
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_crew_skill", "skill", _member.Skill.ToString(Inv), "mood", _member.Mood.ToString(Inv),
                    "rate", (CleaningRules.Rate(_member.Skill, _member.Mood, 1) / 100).ToString(Inv))));
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_crew_wage", "wage", CompanyText.Wage(_member.Wage), "hours", _member.WeekHours.ToString(Inv))));
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_svc_crew_base"), _member.Base != null ? _member.BaseName : ServicesText.T("reallife_svc_crew_no_base")));
                int group = _s.GroupOf(id);
                if (on)
                {
                    lines.Add(_runs.Step(ServicesText.T("reallife_ui_svc_row_named_crew"), group >= 0 ? GroupName(group) : ServicesText.T("reallife_ui_svc_no_group"), null, group >= 0,
                        group < ServicesCatalogue.CrewGroups - 1, () => _d.SetGroup(id, group - 1), () => _d.SetGroup(id, group + 1)));
                }
                IReadOnlyList<DayVisit> visits = _d.Visits;
                int shown = 0, all = 0;
                for (int i = 0; on && i < visits.Count; i++)
                {
                    DayVisit v = visits[i];
                    CleaningClient c = v.CleanerId == id ? _s.Client(v.Place) : null;
                    if (c != null)
                    {
                        all++;
                    }
                    if (c != null && shown < 4)
                    {
                        lines.Add(UiFactLine.Say(ServicesText.Fill(v.Skipped ? "reallife_svc_crew_visit_missed" : "reallife_svc_crew_visit", "from",
                            ServicesText.Clock(v.Start), "to", ServicesText.Clock(v.End),
                            "business", c.Name, "way", ServicesText.Distance(v.Metres))));
                        shown++;
                    }
                }
                if (on && shown == 0)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_crew_no_visits"), UiRole.Muted));
                }
                else if (all > shown)
                {
                    lines.Add(UiFactLine.Say(ServicesText.T("reallife_ui_svc_more_visits", "count", (all - shown).ToString(Inv)), UiRole.Muted));
                }
                return new UiFactCardModel
                {
                    Title = _member.Name, Sub = _member.Base != null ? _member.BaseName : null, Chip = ServicesText.T(on ? "reallife_svc_crew_on" : "reallife_svc_crew_off"),
                    ChipTone = on ? UiRole.Ok : UiRole.Muted, Lines = lines.ToArray(),
                    Actions = new[]
                    {
                        BackButton(),
                        _runs.Button(ServicesText.T(on ? "reallife_svc_crew_take_off" : "reallife_svc_crew_put_on"), on ? UiButtonKind.Secondary : UiButtonKind.Primary, UiIcon.None, UiAction.Alt,
                            () => _d.SetCrew(id, !on))
                    }
                };
            }

            // ------------------------------------------------------------------------------------------------- Contracts

            public void Contracts()
            {
                _m.Shape = ServicesShape.Table;
                var rows = new List<UiDataRow>(12);
                var keys = new List<string>(12);
                CleaningOffer offer = null;
                CleaningClient contract = null;
                int running = 0;
                for (int i = 0; i < _s.Clients.Count; i++)
                {
                    running += _s.Clients[i].Outside ? 1 : 0;
                }
                for (int i = 0; _v.Filter != 2 && i < _s.Offers.Count; i++)
                {
                    CleaningOffer o = _s.Offers[i];
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { o.Name, Money(o.Fee), o.Standard.ToString(Inv), string.Empty, ServicesText.T("reallife_econ_day", "day", o.Until.ToString(Inv)),
                            Bidding(o) },
                        Values = new[] { (int)UiIcon.Building, 0, 0, -1, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn },
                        Subs = new[] { ServicesText.District(o.District), null, null, null, null, null },
                        SortKeys = new[] { N, o.Fee, o.Standard, N, o.Until, N }
                    });
                    keys.Add("offer:" + o.Place);
                    offer = "offer:" + o.Place == _v.Pick ? o : offer;
                }
                for (int i = 0; _v.Filter != 1 && i < _s.Clients.Count; i++)
                {
                    CleaningClient c = _s.Clients[i];
                    if (!c.Outside)
                    {
                        continue;
                    }
                    int weekEnds = c.WeekFrom + ServicesCatalogue.ContractWeekDays;
                    bool late = c.DaysUnder > ServicesCatalogue.ContractDaysUnderMost || c.Missed > 0;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { c.Name, Money(c.Fee), c.Standard.ToString(Inv), c.Now.ToString(Inv), ServicesText.T("reallife_econ_day", "day", weekEnds.ToString(Inv)),
                            ServicesText.T(late ? "reallife_ui_svc_state_late" : "reallife_ui_svc_state_active") },
                        Values = new[] { (int)UiIcon.Building, 0, 0, c.Now, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, late ? UiRole.Danger : UiRole.Ok },
                        Subs = new[] { c.Crew >= 0 ? ServicesText.T("reallife_ui_svc_sub_crew", "district", ServicesText.District(c.District), "crew", GroupName(c.Crew))
                            : ServicesText.District(c.District), null, null, null, null, null },
                        SortKeys = new[] { N, c.Fee, c.Standard, c.Now, weekEnds, N }
                    });
                    keys.Add("con:" + c.Place);
                    contract = "con:" + c.Place == _v.Pick ? c : contract;
                }
                var columns = new[]
                {
                    new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_client"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_fee_week"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_standard"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = ServicesText.T("reallife_svc_col_floor"), Kind = UiCellKind.Meter, Width = 128f },
                    new UiDataColumn { Title = ServicesText.T("reallife_ui_svc_col_week_ends"), Kind = UiCellKind.Date, Width = 112f },
                    new UiDataColumn { Title = ServicesText.T("reallife_co_col_state"), Kind = UiCellKind.Chip, Width = 168f }
                };
                Chips(new[] { ServicesText.T("reallife_ui_svc_chip_all"), ServicesText.T("reallife_svc_kind_offer"), ServicesText.T("reallife_ui_svc_chip_running") },
                    new[] { _s.Offers.Count + running, _s.Offers.Count, running });
                Rows(columns, rows, keys, ServicesText.T("reallife_ui_svc_no_contracts"),
                    ServicesText.T(_s.Crew.Count == 0 ? "reallife_svc_row_no_offer_no_crew" : "reallife_svc_row_no_offer"), UiIcon.ClipboardText, new[] { 1 });
                _m.Chart = MonthChart();
                _m.Side = offer != null ? OfferCard(offer) : contract != null ? ContractCard(contract) : ContractsCard(running);
            }

            private static string GroupName(int group) => ServicesText.T("reallife_ui_svc_crew_name", "letter", ((char)('A' + group)).ToString());

            // "Bidding, 2 rivals" while rival firms bid for the same contract; a plain offer otherwise.
            private static string Bidding(CleaningOffer o)
            {
                return o.Rivals.Count == 0 ? ServicesText.T("reallife_ui_svc_state_offer")
                    : ServicesText.T(o.Rivals.Count == 1 ? "reallife_ui_svc_state_bidding_one" : "reallife_ui_svc_state_bidding", "count", o.Rivals.Count.ToString(Inv));
            }

            // The fees contracted a month over the months kept, with this month's figure and its change on the month before; a series with no history says so.
            private UiChartModel MonthChart()
            {
                int[] series = ContractRules.MonthSeries(_s);
                var chart = new UiChartModel
                {
                    Kind = UiChartKind.Line, Still = true, Whole = false, Format = v => Money((int)Math.Round(v)),
                    EmptyTitle = ServicesText.T("reallife_ui_svc_month_none"), EmptyBody = ServicesText.T("reallife_ui_svc_month_none_body")
                };
                string now = Money(ContractRules.MonthFees(_s));
                if (series.Length >= 2)
                {
                    int before = series[series.Length - 2];
                    int tenths = before > 0 ? (int)Math.Round((series[series.Length - 1] - before) * 1000.0 / before) : 0;
                    chart.Title = ServicesText.T("reallife_ui_svc_month_title", "amount", now, "change", (tenths >= 0 ? "+" : "-") + (Math.Abs(tenths) / 10f).ToString("0.#", Inv) + "%");
                    chart.Points = new string[series.Length];
                    chart.Reads = new string[series.Length];
                    var values = new float[series.Length];
                    for (int i = 0; i < series.Length; i++)
                    {
                        chart.Points[i] = ServicesText.T("reallife_ui_svc_month_n", "n", (_s.FeeMonth - series.Length + 2 + i).ToString(Inv));
                        chart.Reads[i] = chart.Points[i];
                        values[i] = series[i];
                    }
                    chart.Series = new[] { new UiSeries { Label = ServicesText.T("reallife_ui_svc_month_series"), Values = values } };
                }
                else
                {
                    chart.Title = ServicesText.T("reallife_ui_svc_month_title_now", "amount", now);
                }
                return chart;
            }

            // The named crews as people with their skill, the lead first: who works which contracts.
            private void CrewLines(List<UiFactLine> lines)
            {
                ServicesGame.Cleaners(_cleaners);
                for (int g = 0; g < ServicesCatalogue.CrewGroups; g++)
                {
                    string lead = ContractRules.Lead(_s, g, id => SkillOf(id));
                    bool any = false;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int i = 0; i < _s.Places.Count; i++)
                        {
                            string id = _s.Places[i].Id;
                            if (_s.Places[i].Group != g || (id == lead) != (pass == 0) || !MemberOf(id))
                            {
                                continue;
                            }
                            if (!any)
                            {
                                lines.Add(UiFactLine.Section(GroupName(g)));
                                any = true;
                            }
                            lines.Add(UiFactLine.Who(_member.Name, ServicesText.T(pass == 0 ? "reallife_ui_svc_role_lead" : "reallife_ui_svc_role_cleaner"), _member.Skill.ToString(Inv),
                                UiTokens.Grade(_member.Skill) == UiRole.Ok ? UiRole.OkText : UiRole.None));
                        }
                    }
                }
                _cleaners.Clear();
            }

            // Fills the member scratch with the cleaner of this id from the cleaners just read.
            private bool MemberOf(string id)
            {
                for (int i = 0; i < _cleaners.Count; i++)
                {
                    ServicesGame.Fill(_member, _cleaners[i]);
                    if (string.Equals(_member.Id, id, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }

            private int SkillOf(string id) => MemberOf(id) ? _member.Skill : 0;

            private UiFactCardModel ContractsCard(int running)
            {
                var crews = new List<UiFactLine>(12);
                CrewLines(crews);
                if (crews.Count == 0)
                {
                    crews.Add(UiFactLine.Say(ServicesText.T("reallife_ui_svc_crews_none"), UiRole.Muted));
                }
                if (_s.RivalWon > 0)
                {
                    crews.Insert(0, UiFactLine.Say(ServicesText.T("reallife_ui_svc_rivals_won", "count", _s.RivalWon.ToString(Inv)), UiRole.WarnText));
                }
                var all = new List<UiFactLine>(20)
                {
                        UiFactLine.Row(ServicesText.T("reallife_svc_contracts_running"), running.ToString(Inv)),
                        UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_fees_week"), Money(_d.ContractWeekFees())),
                        UiFactLine.Row(ServicesText.T("reallife_ui_svc_row_booked_week"), ServicesText.Signed(ServicesState.Week(_s.FeeIncome))),
                        UiFactLine.Say(ServicesText.T("reallife_svc_contracts_says", "days", ServicesCatalogue.OfferEveryDays.ToString(Inv), "most", ServicesCatalogue.OffersMost.ToString(Inv))),
                        UiFactLine.Say(ServicesText.T("reallife_svc_contracts_says_week", "days", ServicesCatalogue.ContractDaysUnderMost.ToString(Inv), "share",
                            ServicesCatalogue.ContractPenaltyPct.ToString(Inv),
                            "weeks", ServicesCatalogue.ContractMissesMost.ToString(Inv)))
                };
                all.AddRange(crews);
                return new UiFactCardModel { Title = ServicesText.T("reallife_ui_svc_sec_contracts"), Heading = true, Lines = all.ToArray() };
            }

            private UiFactCardModel OfferCard(CleaningOffer offer)
            {
                string place = offer.Place;
                int minutes = CleaningRules.Minutes(ContractRules.DirtDay(offer.Capacity), _d.CrewRate(CleaningRules.Force(offer.Sqm)));
                var lines = new List<UiFactLine>(8)
                {
                    UiFactLine.Row(ServicesText.T("reallife_svc_contract_fee"), ServicesText.T("reallife_svc_row_a_week", "amount", Money(offer.Fee))),
                    UiFactLine.Say(ServicesText.T("reallife_svc_contract_standard", "standard", offer.Standard.ToString(Inv), "days", ServicesCatalogue.ContractDaysUnderMost.ToString(Inv))),
                    UiFactLine.Say(ServicesText.T("reallife_svc_contract_work", "size", offer.Sqm.ToString(Inv), "minutes", ServicesText.Minutes(minutes))),
                    UiFactLine.Say(ServicesText.T("reallife_svc_contract_penalty", "share", ServicesCatalogue.ContractPenaltyPct.ToString(Inv), "weeks",
                        ServicesCatalogue.ContractMissesMost.ToString(Inv)), UiRole.Muted),
                    UiFactLine.Say(ServicesText.T("reallife_svc_contract_until", "day", offer.Until.ToString(Inv)), UiRole.WarnText)
                };
                if (offer.Rivals.Count > 0)
                {
                    lines.Add(UiFactLine.Section(ServicesText.T("reallife_ui_svc_sec_bidding")));
                    for (int r = 0; r < offer.Rivals.Count; r++)
                    {
                        lines.Add(UiFactLine.Row(ServicesCatalogue.RivalFirms[offer.Rivals[r].Firm], ServicesText.T("reallife_ui_svc_rival_bid", "pct", ServicesText.Percent(offer.Rivals[r].Pct),
                            "skill", offer.Rivals[r].Skill.ToString(Inv))));
                    }
                    int win = ContractRules.Winner(offer, _d.CrewSkill());
                    lines.Add(UiFactLine.Say(win >= 0 ? ServicesText.T("reallife_ui_svc_forecast_lose", "firm", ServicesCatalogue.RivalFirms[offer.Rivals[win].Firm])
                        : ServicesText.T("reallife_ui_svc_forecast_win", "skill", _d.CrewSkill().ToString(Inv)), win >= 0 ? UiRole.WarnText : UiRole.OkText));
                }
                return new UiFactCardModel
                {
                    Title = offer.Name, Sub = ServicesText.District(offer.District), Chip = ServicesText.T("reallife_ui_svc_state_offer"), ChipTone = UiRole.Warn, Lines = lines.ToArray(),
                    Actions = new[]
                    {
                        BackButton(),
                        _runs.Button(ServicesText.T("reallife_svc_contract_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () =>
                        {
                            _v.Pick = null;
                            _d.RefuseOffer(place);
                        }),
                        _runs.Button(ServicesText.T("reallife_svc_contract_take"), UiButtonKind.Primary, UiIcon.None, UiAction.Alt, () =>
                        {
                            _v.Pick = "con:" + place;
                            _d.AcceptOffer(place);
                        })
                    }
                };
            }

            private UiFactCardModel ContractCard(CleaningClient c)
            {
                string place = c.Place;
                var lines = new List<UiFactLine>(8);
                if (c.Missed > 0)
                {
                    lines.Add(UiFactLine.Tick(ServicesText.T("reallife_svc_contract_missed", "count", c.Missed.ToString(Inv), "most", ServicesCatalogue.ContractMissesMost.ToString(Inv)), false));
                }
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_svc_contract_fee"), ServicesText.T("reallife_svc_row_a_week", "amount", Money(c.Fee))));
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_svc_client_clean"), ServicesText.T("reallife_svc_row_standard", "now", ServicesText.Trend(c.Now,
                    c.CleanBefore), "standard", c.Standard.ToString(Inv))));
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_contract_week", "under", c.DaysUnder.ToString(Inv), "most", ServicesCatalogue.ContractDaysUnderMost.ToString(Inv),
                    "day", (c.WeekFrom + ServicesCatalogue.ContractWeekDays).ToString(Inv))));
                lines.Add(UiFactLine.Row(ServicesText.T("reallife_svc_contract_earned"), Money(c.Earned)));
                lines.Add(UiFactLine.Say(ServicesText.T("reallife_svc_contract_pattern", "mode", ServicesText.Mode(c.Mode)), UiRole.Muted));
                int crew = c.Crew;
                lines.Add(_runs.Step(ServicesText.T("reallife_ui_svc_row_served_by"), crew >= 0 ? GroupName(crew) : ServicesText.T("reallife_ui_svc_whoever"), null, crew >= 0,
                    crew < ServicesCatalogue.CrewGroups - 1 && _d.CrewHas(crew + 1), () => _d.SetContractCrew(place, crew - 1), () => _d.SetContractCrew(place, crew + 1)));
                lines.Add(_runs.Link(ServicesText.T("reallife_svc_contract_visits"), () => Show(() =>
                {
                    _v.Tab = ServicesDirector.TabCleaning;
                    _v.Pick = "cli:" + place;
                    _v.Filter = 0;
                })));
                bool late = c.DaysUnder > ServicesCatalogue.ContractDaysUnderMost || c.Missed > 0;
                return new UiFactCardModel
                {
                    Title = c.Name, Sub = ServicesText.District(c.District), Chip = ServicesText.T(late ? "reallife_ui_svc_state_late" : "reallife_ui_svc_state_active"),
                        ChipTone = late ? UiRole.Danger : UiRole.Ok,
                    Lines = lines.ToArray(),
                    Actions = new[] { BackButton(), _runs.Button(ServicesText.T("reallife_svc_contract_end"), UiButtonKind.Secondary, UiIcon.None, UiAction.Count, () => _d.EndContract(place)) }
                };
            }
        }
    }
}
