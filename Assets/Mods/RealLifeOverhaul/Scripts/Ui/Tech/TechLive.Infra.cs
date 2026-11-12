using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private static readonly int[] CloudPrices = { 700, 850, 1000, 1150, 1300 };
        private static int _fitTier = 1;

        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- the infra tab

            private void InfraTab()
            {
                InfraFile f = _d.Infra;
                if (f == null)
                {
                    _m.Shape = TechShape.Off;
                    _m.OffTitle = TechText.T("reallife_roles_off_title");
                    _m.OffBody = TechText.T("reallife_tech_off_body");
                    _m.OffAction = TechText.T("reallife_ui_tech_close");
                    return;
                }
                string pick = _v.Pick;
                if (pick != null)
                {
                    string dc = Pick(pick, TechApp.CentreRow), fit = Pick(pick, TechApp.FitRow);
                    if (pick == TechApp.CloudRow)
                    {
                        CloudPage(f);
                        return;
                    }
                    if (dc != null && f.CentreAt(dc) != null)
                    {
                        CentrePage(f.CentreAt(dc), _firm == null);
                        return;
                    }
                    if (fit != null)
                    {
                        BuildingRegistration reg = WarehouseAt(fit);
                        if (reg != null)
                        {
                            FitPage(f, reg, fit);
                            return;
                        }
                    }
                    _v.Pick = null;
                }
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                UiDataRow cloud = Row(7);
                cloud.Cells[0] = f.Cloud.Open ? f.Cloud.Name : TechText.T("reallife_infra_row_cloud_none");
                cloud.Sub = TechText.T("reallife_ui_tech_cloud_sub");
                cloud.Values[0] = (int)TechGlyphs.Cloud;
                cloud.Cells[6] = f.Cloud.Open ? TechText.T(f.Cloud.Hyper ? "reallife_infra_state_hyper" : "reallife_infra_state_cloud") : TechText.T("reallife_infra_state_none");
                cloud.Tones[6] = f.Cloud.Open ? UiRole.Ok : UiRole.Notice;
                rows.Add(cloud);
                keys.Add(TechApp.CloudRow);
                for (int i = 0; i < f.Centres.Count; i++)
                {
                    DataCentre c = f.Centres[i];
                    int need = InfraRules.TechniciansNeeded(c);
                    UiDataRow row = Row(7);
                    row.Cells[0] = c.Name;
                    row.Sub = TechText.T("reallife_ui_tech_centre_sub", "tier", TechText.InfraTier(c.Tier), "district", HiringGame.District(c.District));
                    row.Values[0] = (int)TechGlyphs.Centre;
                    row.Cells[1] = (c.Racks + c.GpuRacks).ToString(Inv) + " / " + c.RacksRoom.ToString(Inv);
                    row.Cells[2] = c.GpuRacks.ToString(Inv);
                    row.Values[2] = c.GpuRacks;
                    row.Cells[3] = c.Colo.ToString(Inv);
                    row.Values[3] = c.Colo;
                    row.Cells[4] = c.Technicians + " / " + need;
                    row.Tones[4] = c.Technicians < need ? UiRole.DangerText : UiRole.None;
                    row.Cells[5] = Cents(c.Takings30);
                    int status = TechStatusRules.Centre(c.Racks + c.GpuRacks, c.RacksRoom, c.Down(_day), c.Power && c.Net);
                    row.Cells[6] = StatusWords(status);
                    row.Tones[6] = StatusTone(status);
                    rows.Add(row);
                    keys.Add("dc:" + c.Place);
                }
                IReadOnlyList<BuildingRegistration> warehouses = _d.Warehouses;
                for (int i = 0; i < warehouses.Count; i++)
                {
                    string place = TalentGame.Place(warehouses[i]);
                    if (f.CentreAt(place) != null)
                    {
                        continue;
                    }
                    UiDataRow row = Row(7);
                    row.Cells[0] = TalentGame.NameOf(warehouses[i]);
                    row.Sub = HiringGame.District(InfraGame.District(warehouses[i]));
                    row.Values[0] = (int)TechGlyphs.Warehouse;
                    row.Cells[6] = TechText.T("reallife_infra_state_warehouse");
                    row.Tones[6] = UiRole.Notice;
                    rows.Add(row);
                    keys.Add("fit:" + place);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_ui_tech_col_site", UiCellKind.Tile), Col("reallife_infra_col_racks", UiCellKind.Text, 100f), Col("reallife_infra_col_gpu", UiCellKind.Figure, 72f),
                        Col("reallife_infra_col_let", UiCellKind.Figure, 80f), Col("reallife_infra_col_technicians", UiCellKind.Text, 84f), Col("reallife_ui_tech_col_takings", UiCellKind.Money, 104f),
                        Col("reallife_ui_tech_col_status", UiCellKind.Chip, 136f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = f.Cloud.Open
                    ? TechText.Fill("reallife_infra_cloud_line", "sold", f.Cloud.Sold.ToString(Inv), "own", f.Cloud.Own.ToString(Inv), "gpu", f.Cloud.GpuSold.ToString(Inv),
                        "rival", TechText.Mille(f.Cloud.RivalPrice),
                        "regions", InfraRules.Regions(f.Centres).ToString(Inv))
                    : TechText.Fill("reallife_infra_about", "mbps", InfraCatalogue.NetNeeded.ToString(Inv), "per", InfraCatalogue.RacksPerTechnician.ToString(Inv));
                if (f.Owed > 0)
                {
                    _m.Next = TechText.Fill("reallife_endscale_owed_line", "owed", Cents(f.Owed));
                }
                ViewSection market = _d.SectorInfraSection(W);
                if (market != null && market.Lines.Count > 0)
                {
                    var card = new TechCard(_runs, TechText.T(market.TitleKey));
                    for (int i = 0; i < market.Lines.Count; i++)
                    {
                        card.Say(Words(market.Lines[i]), Ink(market.Lines[i].Tone));
                    }
                    _m.Side = card.Build();
                }
            }

            private BuildingRegistration WarehouseAt(string place)
            {
                IReadOnlyList<BuildingRegistration> warehouses = _d.Warehouses;
                for (int i = 0; i < warehouses.Count; i++)
                {
                    if (TalentGame.Place(warehouses[i]) == place)
                    {
                        return warehouses[i];
                    }
                }
                return null;
            }

            // ------------------------------------------------------------------------------------------------- a data centre

            // The data centre as the approved window: what to do next, the hall as a hero with its racks in use, its supplies as meters, its takings with the month's change and its
            // uptime, the crew as people, and the way to the rest of what it does.
            private void CentrePage(DataCentre d, bool fromTab)
            {
                string place = d.Place;
                int share = _d.ScaleNow(), buy = TechDirector.BuyScale;
                BuildingRegistration reg = TalentGame.Find(place);
                int plan = TechBridges.PlanMbpsAt(reg);
                CentreLimits lim = InfraLimits.Of(d, plan);
                int fitStandard = InfraLimits.Fit(d, false, plan, out int binding);
                int step = InfraRules.RacksStep(buy);
                if (_v.Page == 1)
                {
                    CentreMore(d, lim, plan, share, buy, step);
                    return;
                }
                var app = new UiAppModel();
                _m.Shape = TechShape.Overview;
                _m.App = app;
                _m.Back = fromTab ? TechText.T("reallife_ui_tech_back_infra") : TechText.T("reallife_ui_tech_back_firms");
                int used = d.Racks + d.GpuRacks, room = d.RacksRoom;
                int percent = room > 0 ? Math.Min(100, used * 100 / room) : 0;
                _m.Next = fitStandard > 0 ? TechText.Fill("reallife_ui_tech_dc_next", "name", d.Name, "percent", percent.ToString(Inv), "count", Math.Min(step, fitStandard).ToString(Inv))
                    : TechText.T("reallife_ui_tech_dc_next_full", "name", d.Name, "limit", TechText.T(LimitKey(binding)));
                var actions = new List<UiActionModel>(3);
                actions.Add(Button(TechText.T("reallife_ui_tech_dc_details"), UiButtonKind.Secondary, UiAction.Count, Go(v => v.Page = 1)));
                actions.Add(Button(TechText.T("reallife_ui_tech_see_contracts"), UiButtonKind.Secondary, UiAction.Option, Go(v => { v.Open("tab:" + TabContracts); })));
                actions.Add(Button(TechText.T(buy > 0 ? "reallife_endscale_button_racks" : "reallife_infra_button_racks", "count", Math.Min(step, Math.Max(1, fitStandard)).ToString(Inv)),
                    UiButtonKind.Primary, UiAction.Alt, Do(() => _d.BuyRacks(place, step, false)), fitStandard < Math.Min(step, 1)));
                _m.Actions = actions.ToArray();
                int uptime = InfraRules.Uptime(d, _day, d.Fitted);
                app.Hero = new UiHeroModel
                {
                    Icon = TechGlyphs.Centre, Tint = UiRole.None, Kicker = TechText.T("reallife_ui_tech_dc_kicker", "tier", TechText.InfraTier(d.Tier)).ToUpperInvariant(), Title = d.Name,
                    Line = TechText.Fill("reallife_ui_tech_dc_line", "used", used.ToString(Inv), "room", room.ToString(Inv), "uptime", (uptime / 10f).ToString("0.0", Inv)),
                    Progress = percent
                };
                var takings = new UiFigureModel { Label = TechText.T("reallife_ui_tech_revenue_30"), Value = Cents(d.Takings30), Trend = Weeks(Ring(d.Days)) };
                if (TechStatusRules.Change(d.Takings30, d.Prev30, d.PrevDays, out int change))
                {
                    takings.Change = (change > 0 ? "+" : string.Empty) + change.ToString(Inv) + "%";
                    takings.Rising = change >= 0;
                    takings.Good = change >= 0;
                }
                app.Figures = new[] { takings };
                // The hall's contracts: the racks let, by client, as a table (ColoContracts, the colocation part), then the crew.
                ContractsBlock(app, d);
                var people = CrewOf(place);
                app.PeopleTitle = TechText.T("reallife_ui_tech_crew");
                app.PeopleCount = TechText.T("reallife_ui_tech_people_n", "count", people.Count.ToString(Inv));
                var rows = new List<UiPersonRow>(MostPeople);
                var pkeys = new List<string>(MostPeople);
                for (int i = 0; i < people.Count && rows.Count < MostPeople; i++)
                {
                    rows.Add(PersonRow(people[i]));
                    pkeys.Add(null);
                }
                app.People = rows.ToArray();
                _m.PersonKeys = pkeys.ToArray();
                int need = InfraRules.TechniciansNeeded(d);
                if (d.Technicians < need)
                {
                    app.Note = TechText.T("reallife_ui_tech_dc_crew_short", "have", d.Technicians.ToString(Inv), "need", need.ToString(Inv));
                    app.NoteAction = TechText.T("reallife_infra_button_hire");
                    _m.NoteRun = Run(Do(() => _d.OpenRoles(TechDirector.TabJobs, null)));
                }
                else
                {
                    CapacityNote(app, d, fitStandard);
                }
                _m.Meters = Meters(d, lim, plan);
                _m.MetersTitle = TechText.T("reallife_ui_tech_capacity");
            }

            // The hall's second page: its supplies as bars with what they mean, and the cards of what it draws, its crews, its racks, its upgrade and its buttons.
            private void CentreMore(DataCentre d, CentreLimits lim, int plan, int share, int buy, int step)
            {
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_hall");
                _m.Head = new UiHeadModel { Name = d.Name, Icon = TechGlyphs.Centre, Role = TechText.T("reallife_ui_tech_dc_more") };
                var supply = new TechCard(_runs, TechText.T("reallife_ui_tech_capacity"));
                TechMeter[] meters = Meters(d, lim, plan);
                for (int i = 0; i < meters.Length; i++)
                {
                    supply.Bar(meters[i].Label, meters[i].Fill, meters[i].Figure);
                }
                int fit = InfraLimits.Fit(d, false, plan, out int binding);
                supply.Say(fit > 0 ? TechText.T("reallife_ui_tech_dc_room_for", "count", fit.ToString(Inv)) : TechText.T("reallife_ui_tech_dc_next_full", "name", d.Name, "limit",
                    TechText.T(LimitKey(binding))),
                    fit > 0 ? UiRole.None : UiRole.WarnText);
                _m.Left = CentreCards(d, share, buy, step);
                _m.Right = new[] { supply.Build() };
            }

            private static string LimitKey(int binding)
            {
                switch (binding)
                {
                    case InfraLimits.Power: return "reallife_ui_tech_limit_power";
                    case InfraLimits.Cooling: return "reallife_ui_tech_limit_cooling";
                    case InfraLimits.Bandwidth: return "reallife_ui_tech_limit_bandwidth";
                    default: return "reallife_ui_tech_limit_floor";
                }
            }

            private void CapacityNote(UiAppModel app, DataCentre d, int fit)
            {
                app.Note = fit > 0 ? TechText.T("reallife_ui_tech_dc_prices", "rack", Cents(_d.Level(InfraRules.RackPrice(false, TechDirector.BuyScale))), "gpu",
                    Cents(_d.Level(InfraRules.RackPrice(true, TechDirector.BuyScale)))) : TechText.T("reallife_ui_tech_dc_full_note");
                app.NoteTone = UiRole.Ok;
                app.NoteAction = TechText.T("reallife_ui_tech_open_funding");
                _m.NoteRun = Run(Go(v => v.Open("tab:" + TabFunding)));
            }

            // The meters: racks on the floor, power in megawatts, cooling as a share of the plant, bandwidth in gigabits, each filled by what it uses and toned by how near it is to full.
            private TechMeter[] Meters(DataCentre d, CentreLimits l, int plan)
            {
                int used = d.Racks + d.GpuRacks * 2;
                return new[]
                {
                    Meter(TechText.T("reallife_ui_tech_m_racks"), InfraLimits.Percent(used, d.RacksRoom), TechText.T("reallife_ui_tech_m_of", "used",
                        (d.Racks + d.GpuRacks).ToString(Inv), "cap", d.RacksRoom.ToString(Inv))),
                    Meter(TechText.T("reallife_ui_tech_m_power"), InfraLimits.Percent(l.PowerUse, l.PowerCap), TechText.T("reallife_ui_tech_m_mw", "used", (l.PowerUse / 1000f).ToString("0.0", Inv),
                        "cap", (l.PowerCap / 1000f).ToString("0.0", Inv))),
                    Meter(TechText.T("reallife_ui_tech_m_cooling"), InfraLimits.Percent(l.CoolUse, l.CoolCap), TechText.T("reallife_ui_tech_m_plant", "percent",
                        InfraLimits.Percent(l.CoolUse, l.CoolCap).ToString(Inv))),
                    Meter(TechText.T("reallife_ui_tech_m_bandwidth"), InfraLimits.Percent(l.NetUse, l.NetCap), TechText.T("reallife_ui_tech_m_gbps", "used", (l.NetUse / 1000f).ToString("0.0", Inv),
                        "cap", (l.NetCap / 1000f).ToString("0.0", Inv)))
                };
            }

            private static TechMeter Meter(string label, int percent, string figure) => new TechMeter
            {
                Label = label, Fill = Math.Min(100, percent), Figure = figure, Tone = percent >= 95 ? UiRole.Danger : percent >= TechStatusRules.FullPercent ? UiRole.Warn : UiRole.Ok
            };

            private List<Person> CrewOf(string place) => PeopleOf(place);

            // The hall's contracts table: the colocation contracts when the part is on (ColoContracts), else one line of what is let.
            private void ContractsBlock(UiAppModel app, DataCentre d)
            {
                app.TableTitle = TechText.T("reallife_ui_tech_colo_title");
                app.Columns = new[]
                {
                    new UiColumn { Title = TechText.T("reallife_tech_col_client") }, new UiColumn { Title = TechText.T("reallife_ui_tech_col_racks"), Width = 72f, Right = true },
                    new UiColumn { Title = TechText.T("reallife_ui_tech_col_per_month"), Width = 96f, Right = true },
                        new UiColumn { Title = TechText.T("reallife_ui_tech_col_ends"), Width = 88f, Right = true },
                    new UiColumn { Title = TechText.T("reallife_ui_tech_col_status"), Width = 144f, Right = true }
                };
                var rows = new List<UiAppRow>(8);
                ColoRows(d, rows);
                app.Rows = rows.ToArray();
                _m.RowKeys = new string[rows.Count];
            }

            private void ColoRows(DataCentre d, List<UiAppRow> rows)
            {
                InfraFile f = _d.Infra;
                var mine = new List<ColoContract>(8);
                for (int i = 0; f != null && i < f.Colo.Count; i++)
                {
                    if (f.Colo[i].Place == d.Place)
                    {
                        mine.Add(f.Colo[i]);
                    }
                }
                // what needs the player first: a rival's offer, then the terms ending soonest
                mine.Sort((a, b) => ColoRules.State(b, _day) == 2 ? (ColoRules.State(a, _day) == 2 ? a.End.CompareTo(b.End) : 1) : ColoRules.State(a, _day) == 2 ? -1 : a.End.CompareTo(b.End));
                for (int i = 0; i < mine.Count && rows.Count < 7; i++)
                {
                    ColoContract c = mine[i];
                    int state = ColoRules.State(c, _day);
                    string chip = TechText.T(Mod + "_ui_tech_colo_state_" + state.ToString(Inv));
                    rows.Add(new UiAppRow
                    {
                        Icon = UiIcon.Building, Tint = UiRole.None, Grade = -1, Status = chip, StatusTone = state == 0 ? UiRole.Ok : state == 1 ? UiRole.Accent : UiRole.Warn,
                        Cells = new[] { ColoRules.NameOf(c.Client), c.Racks.ToString(Inv), Cents(_d.Level(c.Fee)), TechText.Day(c.End), chip }
                    });
                }
                int small = d.Colo - ColoRules.Held(f ?? new InfraFile(), d.Place);
                if (small > 0)
                {
                    rows.Add(new UiAppRow
                    {
                        Icon = UiIcon.Building, Tint = UiRole.None, Grade = -1, Status = TechText.T("reallife_ui_tech_colo_state_0"), StatusTone = UiRole.Ok,
                        Cells = new[] { TechText.T("reallife_ui_tech_colo_small"), small.ToString(Inv), Cents(_d.Level((long)small * InfraCatalogue.ColoMonth)),
                            "-", TechText.T("reallife_ui_tech_colo_state_0") }
                    });
                }
            }

            // The hall's other pages of cards: what it draws, its crews at scale, its upgrade and its buttons, as a card under the contracts.
            private UiFactCardModel[] CentreCards(DataCentre d, int share, int buy, int step)
            {
                string place = d.Place;
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_dc_more"));
                long power = InfraRules.Power(d, share), water = InfraRules.Water(d);
                card.Say(TechText.Fill("reallife_infra_centre_power", "kwh", (power / 1000).ToString("N0", Inv), "water", (water / 1000f).ToString("0.0", Inv),
                    "supplied", TechText.T(d.Power ? "reallife_infra_yes" : "reallife_infra_no"), "net", TechText.T(d.Net ? "reallife_infra_yes" : "reallife_infra_no")));
                card.Say(TechText.Fill("reallife_infra_centre_racks", "racks", d.Racks.ToString(Inv), "gpu", d.GpuRacks.ToString(Inv), "room", InfraRules.Room(d).ToString(Inv),
                    "let", d.Colo.ToString(Inv),
                    "book", Cents(_d.Level(d.Book))));
                if (share > 0)
                {
                    card.Say(TechText.Fill("reallife_endscale_infra_crews", "operators", d.Crew[InfraCatalogue.Operators].ToString(Inv), "operators_need", InfraRules.CrewNeed(d,
                        InfraCatalogue.Operators, share).ToString(Inv),
                        "engineers", d.Crew[InfraCatalogue.Engineers].ToString(Inv), "engineers_need", InfraRules.CrewNeed(d, InfraCatalogue.Engineers, share).ToString(Inv),
                        "security", d.Crew[InfraCatalogue.Security].ToString(Inv), "security_need", InfraRules.CrewNeed(d, InfraCatalogue.Security, share).ToString(Inv),
                        "wages", Cents(_d.Level(InfraRules.Crews(d)))));
                    card.Say(TechText.Fill("reallife_endscale_infra_upkeep", "upkeep", Cents(_d.Level(InfraRules.Upkeep(d, share))), "dense", Math.Min(d.Dense, d.Racks).ToString(Inv),
                        "dense_gpu", Math.Min(d.DenseGpu, d.GpuRacks).ToString(Inv), "old", (d.Legacy + d.LegacyGpu).ToString(Inv)));
                }
                int plan = TechBridges.PlanMbpsAt(TalentGame.Find(place));
                int fitGpu = InfraLimits.Fit(d, true, plan, out _);
                card.Button(TechText.T("reallife_infra_button_gpu"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.BuyRacks(place, 1, true)), fitGpu < 1);
                if (buy > 0 && d.Legacy + d.LegacyGpu > 0)
                {
                    card.Button(TechText.T("reallife_endscale_button_replace"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.ReplaceRacks(place)));
                }
                if (d.Tier < InfraCatalogue.Tiers - 1)
                {
                    int next = d.Tier + 1;
                    card.Say(TechText.T("reallife_infra_centre_upgrade", "tier", TechText.InfraTier(next), "cost", Cents(_d.Level(InfraRules.UpgradeCost(d.Sqm, d.Tier, next, buy)))));
                    card.Button(TechText.T("reallife_infra_button_upgrade"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.Upgrade(place, next)));
                }
                card.Button(TechText.T("reallife_infra_button_hire"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.OpenRoles(TechDirector.TabJobs, null)));
                card.Button(TechText.T("reallife_crew_button_brief"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.BriefCrew(place)));
                return new[] { card.Build() };
            }

            // ------------------------------------------------------------------------------------------------- a warehouse to fit out

            private void FitPage(InfraFile f, BuildingRegistration reg, string place)
            {
                int sqm = InfraGame.Sqm(reg);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_infra");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_infra_fit_title", "name", TalentGame.NameOf(reg)), Icon = TechGlyphs.Warehouse,
                    Role = HiringGame.District(InfraGame.District(reg)) };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_fit_title"));
                int tier = _fitTier;
                card.Step(TechText.T("reallife_ui_tech_tier"), TechText.Fill("reallife_infra_fit_tier", "tier", TechText.InfraTier(tier), "outages",
                    TechText.Mille(InfraCatalogue.OutageChance[tier] * 10)), null,
                    tier > 0, tier < InfraCatalogue.Tiers - 1, Go(v => _fitTier = tier - 1), Go(v => _fitTier = tier + 1));
                card.Say(TechText.Fill("reallife_infra_fit_cost", "sqm", sqm.ToString(Inv), "racks", (sqm / InfraCatalogue.SqmPerRack).ToString(Inv),
                    "cost", Cents(_d.Level(InfraRules.FitCost(sqm, tier, TechDirector.BuyScale)))));
                card.Say(TechText.Fill("reallife_infra_fit_supply", "supplied", TechText.T(TechBridges.SuppliedAt(reg, 0) ? "reallife_infra_yes" : "reallife_infra_no"),
                    "water", TechText.T(TechBridges.SuppliedAt(reg, 1) ? "reallife_infra_yes" : "reallife_infra_no"),
                    "net", TechText.T(TechBridges.FastEnough(reg, InfraCatalogue.NetNeeded) ? "reallife_infra_yes" : "reallife_infra_no"), "mbps", InfraCatalogue.NetNeeded.ToString(Inv)));
                int block = InfraRules.FitBlock(f.Centres.Count, f.CentreAt(place) != null);
                if (block != InfraRules.FitFree)
                {
                    card.Say(TechText.T(block == InfraRules.FitAlready ? "reallife_infra_fit_already" : "reallife_infra_fit_most"), UiRole.DangerText);
                }
                else
                {
                    _m.HeadActions = new[]
                    {
                        Button(TechText.T("reallife_infra_button_fit"), UiButtonKind.Primary, UiAction.Alt, () =>
                        {
                            UiClone.Safe(() =>
                            {
                                if (_d.FitOut(place, tier))
                                {
                                    _v.Pick = "dc:" + place;
                                }
                            });
                            _live.Touch();
                            return true;
                        })
                    };
                }
                _m.Left = new[] { card.Build() };
            }

            // ------------------------------------------------------------------------------------------------- the cloud

            private void CloudPage(InfraFile f)
            {
                Cloud c = f.Cloud;
                int share = _d.ScaleNow(), buy = TechDirector.BuyScale;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_infra");
                _m.Head = new UiHeadModel
                {
                    Name = c.Open ? c.Name : TechText.T("reallife_infra_cloud_title_new"), Icon = TechGlyphs.Cloud, Role = TechText.T("reallife_ui_tech_cloud_sub"),
                    Status = c.Open ? TechText.T(c.Hyper ? "reallife_infra_state_hyper" : "reallife_infra_state_cloud") : null
                };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_cloud_sub"));
                if (!c.Open)
                {
                    int needs = _d.CloudNeeds();
                    card.Say(needs == 0 ? TechText.T("reallife_infra_cloud_ready") : _d.Needs(needs, true));
                    card.Say(TechText.Fill("reallife_infra_cloud_about", "racks", InfraRules.CloudRacksLeast(buy).ToString(Inv), "engineers", InfraCatalogue.CloudEngineersLeast.ToString(Inv),
                        "cash", Cents(_d.Level(InfraRules.CloudCashLeast(buy))), "licence", Cents(_d.Level(InfraRules.Licence(buy)))));
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    if (needs == 0 && uis != null && uis.textInputUi != null)
                    {
                        _m.HeadActions = new[]
                        {
                            Button(TechText.T("reallife_infra_button_launch"), UiButtonKind.Primary, UiAction.Alt, () =>
                            {
                                GameUi.TextBox.Ask("reallife_infra_cloud_name_placeholder", "common_confirm", text => UiClone.Safe(() =>
                                {
                                    _d.LaunchCloud(text);
                                    _live.Touch();
                                }), null, string.Empty, false, 30);
                                return true;
                            })
                        };
                    }
                    _m.Left = new[] { card.Build() };
                    return;
                }
                card.Say(TechText.Fill("reallife_infra_cloud_state", "reputation", TechText.Mille(c.Reputation), "day", c.LicenceUntil.ToString(Inv),
                    "hyper", c.Hyper ? TechText.T("reallife_infra_state_hyper") : _d.Needs(_d.HyperNeeds(), false)));
                card.Say(TechText.Fill("reallife_infra_cloud_line", "sold", c.Sold.ToString(Inv), "own", c.Own.ToString(Inv), "gpu", c.GpuSold.ToString(Inv), "rival", TechText.Mille(c.RivalPrice),
                    "regions", InfraRules.Regions(f.Centres).ToString(Inv)));
                ViewLine racks = _d.RacksLine();   // the AI racks train the player's own models first: the reason the cloud sells less of them
                if (racks != null)
                {
                    card.Say(Words(racks));
                }
                if (share > 0)
                {
                    card.Say(TechText.Fill("reallife_endscale_cloud_ops", "ops", c.Ops.ToString(Inv), "wages", Cents(_d.Level((long)c.Ops * InfraCatalogue.OpsWage)), "licence",
                        Cents(_d.Level(InfraRules.Licence(buy)))));
                }
                int at = 2;
                for (int i = 0; i < CloudPrices.Length; i++)
                {
                    at = CloudPrices[i] <= c.Price ? i : at;
                }
                string PriceWords(int i) => TechText.Fill("reallife_infra_cloud_price", "share", TechText.Mille(CloudPrices[i]), "unit",
                    TechText.Rate((int)_d.Level(InfraCatalogue.UnitDay * (long)CloudPrices[i] / 1000)));
                int last = CloudPrices.Length - 1;
                card.Step(TechText.T("reallife_ui_tech_price"), PriceWords(at), null, at > 0, at < last, Do(() => _d.SetCloudPrice(CloudPrices[at - 1])),
                    Do(() => _d.SetCloudPrice(CloudPrices[at + 1])));
                if (_day >= c.LicenceUntil - 14)
                {
                    _m.HeadActions = new[] { Button(TechText.T("reallife_infra_button_licence"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.RenewLicence())) };
                }
                _m.Left = new[] { card.Build() };
            }
        }
    }
}
