using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private sealed partial class Page
        {
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
            private static readonly List<LeagueRow> Table = new List<LeagueRow>(16);
            private const int MostLeague = 8;

            private static UiDataRow Row(int cells) => new UiDataRow { Cells = new string[cells], Values = new int[cells], Tones = new UiRole[cells] };

            private static UiDataColumn Col(string key, UiCellKind kind, float width = 0f, bool graded = false) =>
                new UiDataColumn { Title = TechText.T(key), Kind = kind, Width = width, Graded = graded };

            private static string StatusWords(int status) => TechText.T("reallife_ui_tech_status_" + status.ToString(Inv));

            private static UiRole StatusTone(int status)
            {
                switch (status)
                {
                    case TechStatusRules.NearlyFull:
                    case TechStatusRules.NeedsProduct:
                    case TechStatusRules.NoTeam:
                        return UiRole.Warn;
                    case TechStatusRules.Down:
                    case TechStatusRules.Closed:
                        return UiRole.Danger;
                    case TechStatusRules.SeedFunded:
                        return UiRole.Notice;
                    default:
                        return UiRole.Ok;
                }
            }

            // A month's takings against the month before as a trend cell: "+12%" with its arrow, or "new" while the month before is short of days.
            private void ChangeCell(UiDataRow row, int cell, long now, long before, int days)
            {
                if (TechStatusRules.Change(now, before, days, out int percent))
                {
                    row.Cells[cell] = (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
                    row.Values[cell] = percent > 0 ? 1 : percent < 0 ? -1 : 0;
                    return;
                }
                row.Cells[cell] = TechText.T("reallife_ui_tech_change_new");
                row.Values[cell] = 0;
            }

            // The takings of every firm and centre, day by day over the ring's month, oldest first: the ring's newest slot holds the day just ended.
            private float[] DaysOfAll(out long sum)
            {
                var days = new float[FirmsCatalogue.MonthDays];
                sum = 0;
                for (int i = 0; i < _d.FirmPlaces.Count; i++)
                {
                    Firm m = _d.FirmPlaces[i].File;
                    for (int k = 0; k < days.Length; k++)
                    {
                        days[k] += m.Days[(_day + 1 + k) % FirmsCatalogue.MonthDays];
                    }
                }
                InfraFile infra = _d.Infra;
                for (int i = 0; infra != null && i < infra.Centres.Count; i++)
                {
                    DataCentre c = infra.Centres[i];
                    for (int k = 0; k < days.Length; k++)
                    {
                        days[k] += c.Days[(_day + 1 + k) % FirmsCatalogue.MonthDays];
                    }
                }
                for (int k = 0; k < days.Length; k++)
                {
                    sum += (long)days[k];
                }
                return days;
            }

            // ------------------------------------------------------------------------------------------------- the firms tab

            private void FirmsTab()
            {
                _v.Pick = null;
                IReadOnlyList<FirmPlace> firms = _d.FirmPlaces;
                InfraFile infra = _d.Infra;
                VentureFile venture = _d.Ventures;
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                long total = 0;
                int staff = 0, tableRows = 0;
                for (int i = 0; i < firms.Count; i++)
                {
                    FirmPlace firm = firms[i];
                    Firm m = firm.File;
                    int building = 0, onSale = 0;
                    for (int p = 0; p < _f.Products.Count; p++)
                    {
                        Product pr = _f.Products[p];
                        if (pr.Place != m.Place)
                        {
                            continue;
                        }
                        building += pr.Stage == 0 ? 1 : 0;
                        onSale += pr.Stage == 1 ? 1 : 0;
                    }
                    int status = TechStatusRules.Firm(FirmsCatalogue.MakesProducts[m.Kind] != 0, firm.People, building, onSale);
                    UiDataRow row = Row(6);
                    row.Cells[0] = m.Name;
                    row.Sub = TechText.FirmName(m.K);
                    row.Values[0] = (int)TechGlyphs.Of(TechGlyphs.Firm, m.Kind);
                    row.Cells[1] = ProductsLine(onSale, building, FirmsCatalogue.MakesProducts[m.Kind] != 0);
                    row.Cells[2] = Cents(m.Revenue30);
                    row.Tones[2] = UiRole.OkText;
                    ChangeCell(row, 3, m.Revenue30, m.Prev30, m.PrevDays);
                    row.Tones[3] = UiRole.OkText;
                    row.Cells[4] = firm.People.ToString(Inv);
                    row.Values[4] = firm.People;
                    row.Cells[5] = StatusWords(status);
                    row.Tones[5] = StatusTone(status);
                    rows.Add(row);
                    keys.Add("firm:" + m.Place);
                    total += m.Revenue30;
                    staff += firm.People;
                    tableRows++;
                }
                int fullest = -1;
                int fullPercent = 0;
                for (int i = 0; infra != null && i < infra.Centres.Count; i++)
                {
                    DataCentre c = infra.Centres[i];
                    int status = TechStatusRules.Centre(c.Racks + c.GpuRacks, c.RacksRoom, c.Down(_day), c.Power && c.Net);
                    UiDataRow row = Row(6);
                    row.Cells[0] = c.Name;
                    row.Sub = TechText.T("reallife_ui_tech_centre_sub", "tier", TechText.InfraTier(c.Tier), "district", HiringGame.District(c.District));
                    row.Values[0] = (int)TechGlyphs.Centre;
                    row.Cells[1] = TechText.T("reallife_ui_tech_centre_line", "racks", (c.Racks + c.GpuRacks).ToString(Inv), "room", c.RacksRoom.ToString(Inv));
                    row.Cells[2] = Cents(c.Takings30);
                    row.Tones[2] = UiRole.OkText;
                    ChangeCell(row, 3, c.Takings30, c.Prev30, c.PrevDays);
                    row.Tones[3] = UiRole.OkText;
                    row.Cells[4] = c.Technicians.ToString(Inv);
                    row.Values[4] = c.Technicians;
                    row.Cells[5] = StatusWords(status);
                    row.Tones[5] = StatusTone(status);
                    rows.Add(row);
                    keys.Add("dc:" + c.Place);
                    total += c.Takings30;
                    staff += c.Technicians;
                    int percent = c.RacksRoom > 0 ? (c.Racks + c.GpuRacks) * 100 / c.RacksRoom : 0;
                    if (percent > fullPercent)
                    {
                        fullPercent = percent;
                        fullest = i;
                    }
                }
                for (int i = 0; venture != null && i < venture.Startups.Count; i++)
                {
                    Startup s = venture.Startups[i];
                    long mine = VentureRules.PlayerValue(s, out int share, out long put);
                    if (put <= 0)
                    {
                        continue;
                    }
                    int status = TechStatusRules.Startup(s.Alive, s.Stage);
                    UiDataRow row = Row(6);
                    row.Cells[0] = s.Name;
                    row.Sub = TechText.T("reallife_ui_tech_stake_sub", "share", TechText.Mille(share), "sector", TechText.VentureName((Venture)s.Sector));
                    row.Values[0] = (int)TechGlyphs.Startup;
                    row.Cells[1] = TechText.RoundName((RoundStage)s.Stage);
                    row.Cells[2] = Cents(_d.Level(s.Mrr));
                    row.Tones[2] = UiRole.OkText;
                    row.Cells[3] = string.Empty;
                    row.Cells[4] = "-";
                    row.Cells[5] = StatusWords(status);
                    row.Tones[5] = StatusTone(status);
                    rows.Add(row);
                    keys.Add("start:" + s.Id);
                }
                var table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_ui_tech_col_firm", UiCellKind.Tile), Col("reallife_tech_col_products", UiCellKind.Text, 232f), Col("reallife_ui_tech_col_takings", UiCellKind.Money, 112f),
                        Col("reallife_ui_tech_col_change", UiCellKind.Trend, 80f), Col("reallife_ui_tech_col_staff", UiCellKind.Figure, 72f), Col("reallife_ui_tech_col_status", UiCellKind.Chip, 176f)
                    },
                    Rows = rows.ToArray(),
                    RowCount = rows.Count,
                    Selected = -1,
                    TotalsLabel = TechText.T("reallife_ui_tech_all_firms"),
                    Totals = new[] { string.Empty, string.Empty, Cents(total), string.Empty, staff.ToString(Inv), string.Empty },
                    EmptyTitle = TechText.T("reallife_ui_tech_no_firm"),
                    EmptyBody = TechText.T("reallife_ui_tech_no_firm_body"),
                    EmptyIcon = UiIcon.Building
                };
                _m.Shape = TechShape.Table;
                _m.Table = table;
                _m.RowKeys = keys.ToArray();
                _m.TableTitle = TechText.T("reallife_ui_tech_your_firms");
                if (total > 0 || rows.Count > 0)
                {
                    _m.Figures = new[] { new UiFigureModel { Label = TechText.T("reallife_ui_tech_group_takings"), Value = Cents(total), Span = 2, Trend = Weeks(DaysOfAll(out _)) } };
                }
                FirmsLead(fullest);
                LeagueCard(total);
                float[] days = DaysOfAll(out _);
                var points = new string[days.Length];
                for (int k = 0; k < days.Length; k++)
                {
                    points[k] = TechText.T("reallife_ui_tech_day_n", "day", (_day - days.Length + k).ToString(Inv));
                }
                if (rows.Count > 0 && Drawn(days))
                {
                    _m.Chart = new UiChartModel
                    {
                        Kind = UiChartKind.Line, Title = TechText.T("reallife_ui_tech_takings_by_day"), Points = points,
                            Series = new[] { new UiSeries { Label = TechText.T("reallife_ui_tech_all_firms"), Values = days } },
                        Format = v => Cents((long)v), AxisFormat = v => Cents((long)v), EvenAxis = true, Still = true
                    };
                }
            }

            private static bool Drawn(float[] values)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < values.Length; i++)
                {
                    lo = Math.Min(lo, values[i]);
                    hi = Math.Max(hi, values[i]);
                }
                return hi > 0f;
            }

            // A week's takings a day, so one busy day reads as a week's lift (the studio page's own).
            private static float[] Weeks(float[] days)
            {
                int n = days.Length, w = StudioViews.WeekDays;
                var trend = new float[Math.Max(2, n - w + 1)];
                for (int i = 0; i + w <= n; i++)
                {
                    float sum = 0f;
                    for (int d = i; d < i + w; d++)
                    {
                        sum += days[d];
                    }
                    trend[i] = sum;
                }
                return trend;
            }

            private static string ProductsLine(int onSale, int building, bool makes)
            {
                if (!makes)
                {
                    return TechText.T("reallife_ui_tech_services_firm");
                }
                if (onSale == 0 && building == 0)
                {
                    return TechText.T("reallife_ui_tech_no_products");
                }
                return TechText.T("reallife_ui_tech_products_line", "out", onSale.ToString(Inv), "building", building.ToString(Inv));
            }

            // What to do next across the firms, in the order of what waits longest for the player.
            private void FirmsLead(int fullest)
            {
                IReadOnlyList<FirmPlace> firms = _d.FirmPlaces;
                InfraFile infra = _d.Infra;
                int waiting = _d.TechWaiting();
                if (firms.Count == 0 && (infra == null || infra.Centres.Count == 0))
                {
                    _m.Next = TechText.T("reallife_ui_tech_next_start");
                    return;
                }
                if (waiting > 0)
                {
                    _m.Next = TechText.T("reallife_ui_tech_next_waiting", "count", waiting.ToString(Inv));
                    _m.Actions = new[] { Button(TechText.T("reallife_ui_tech_open_contracts"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("tab:" + TabContracts))) };
                    return;
                }
                if (fullest >= 0)
                {
                    DataCentre c = infra.Centres[fullest];
                    int percent = c.RacksRoom > 0 ? (c.Racks + c.GpuRacks) * 100 / c.RacksRoom : 0;
                    if (percent >= TechStatusRules.FullPercent)
                    {
                        string place = c.Place;
                        _m.Next = TechText.T("reallife_ui_tech_next_full", "name", c.Name, "percent", percent.ToString(Inv));
                        _m.Actions = new[] { Button(TechText.T("reallife_ui_tech_order_racks"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("dc:" + place))) };
                        return;
                    }
                }
                for (int i = 0; i < firms.Count; i++)
                {
                    Firm m = firms[i].File;
                    if (FirmsCatalogue.MakesProducts[m.Kind] == 0 || firms[i].People == 0)
                    {
                        continue;
                    }
                    bool any = false;
                    for (int p = 0; p < _f.Products.Count && !any; p++)
                    {
                        any = _f.Products[p].Place == m.Place && _f.Products[p].Stage < 2;
                    }
                    if (!any)
                    {
                        string place = m.Place;
                        _m.Next = TechText.T("reallife_ui_tech_next_product", "firm", m.Name);
                        _m.Actions = new[] { Button(TechText.T("reallife_tech_button_new_product"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("newprod:" + place))) };
                        return;
                    }
                }
                _m.Next = TechText.T("reallife_ui_tech_next_running");
            }

            // The sector league beside the table: the listed companies' takings a month with the player's firms among them, so the player sees where they stand.
            private void LeagueCard(long mine)
            {
                SectorFile sector = _d.Sector;
                if (sector == null || mine <= 0)
                {
                    return;
                }
                int rank = SectorRules.League(sector, mine, Table);
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_league_title"));
                if (rank > 0)
                {
                    card.Sub = TechText.T("reallife_ui_tech_league_rank", "rank", rank.ToString(Inv), "of", Table.Count.ToString(Inv));
                }
                for (int i = 0; i < Table.Count && i < MostLeague; i++)
                {
                    // the league shows its head and always the player: a rank below the last line takes its place
                    LeagueRow r = Table[i == MostLeague - 1 && rank > MostLeague ? rank - 1 : i];
                    string name = r.Company == SectorRules.LeagueYou ? TechText.T("reallife_ui_tech_league_you") : HiringCatalogue.RivalFirms[r.Company];
                    string line = r.Company == SectorRules.LeagueYou ? TechText.T("reallife_ui_tech_league_yours", "count", _d.FirmPlaces.Count.ToString(Inv))
                        : TechText.T("reallife_ui_tech_league_customers", "customers", TechText.Whole(r.Customers));
                    card.Who(name, line, Cents(r.Company == SectorRules.LeagueYou ? mine : _d.Level(r.Monthly)), r.Company == SectorRules.LeagueYou ? UiRole.AccentText : UiRole.None);
                }
                string move = RivalMove();
                if (!string.IsNullOrEmpty(move))
                {
                    card.Space();
                    card.Say(move, UiRole.WarnText);
                    card.Go(TechText.T("reallife_ui_tech_open_roles"), Do(() => _d.OpenRoles(TechDirector.TabJobs, null)));
                }
                _m.Side = card.Build();
            }

            // What a rival did lately to the player's people: a bid for one of them, else the ad it has up.
            private string RivalMove()
            {
                IReadOnlyList<Person> people = _d.People;
                for (int i = 0; i < people.Count; i++)
                {
                    Profile p = people[i].File;
                    if (!string.IsNullOrEmpty(p.PoachBy) && p.PoachDay >= _day)
                    {
                        return TechText.T("reallife_ui_tech_rival_bid", "rival", p.PoachBy, "name", people[i].Name);
                    }
                }
                HiringFile hiring = _d.Hiring;
                for (int i = 0; hiring != null && i < hiring.RivalAds.Count; i++)
                {
                    RivalAd ad = hiring.RivalAds[i];
                    if (ad.To >= _day)
                    {
                        return TechText.T("reallife_ui_tech_rival_hiring", "rival", ad.Rival, "family", TechText.FamilyName((RealLifeOverhaul.Tech.Family)Math.Max(0, ad.Family)));
                    }
                }
                return null;
            }
        }
    }
}
