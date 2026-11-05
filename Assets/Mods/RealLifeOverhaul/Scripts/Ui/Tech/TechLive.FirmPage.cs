using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private sealed partial class Page
        {
            private const int MostPeople = 7;

            private bool Studio => _d.StudioRuns(_firm.File.K);

            // ------------------------------------------------------------------------------------------------- a firm's sections

            // The sidebar while a firm is open: the way out to the firms, then the firm's own sections.
            private void FirmNav()
            {
                Firm m = _firm.File;
                bool studio = Studio;
                var nav = new List<UiNavEntry>(8);
                var keys = new List<string>(8);
                nav.Add(new UiNavEntry { Icon = UiIcon.Back, Tint = UiRole.None, Label = TechText.T("reallife_ui_tech_all_firms_back") });
                keys.Add("tab:" + TabFirms);
                int products = 0;
                for (int i = 0; i < _f.Products.Count; i++)
                {
                    products += _f.Products[i].Place == m.Place && _f.Products[i].Stage < 2 ? 1 : 0;
                }
                int rivals = _d.RivalsOf(_firm).Count;
                int awards = studio ? AwardsOf(m.Place) : 0;
                string group = TechText.T("reallife_ui_tech_group_firm");
                void Add(int section, UiIcon icon, string label, int count)
                {
                    nav.Add(new UiNavEntry { Icon = icon, Tint = UiRole.None, Label = label, Count = Count(count), Group = section == TechSection.Overview ? group : null });
                    keys.Add("sec:" + section);
                }
                Add(TechSection.Overview, UiIcon.Grid, TechText.T("reallife_ui_tech_sec_overview"), 0);
                Add(TechSection.Products, studio ? UiIcon.GameController : UiIcon.Package, TechText.T(studio ? "reallife_ui_tech_sec_games" : "reallife_tech_tab_products"), products);
                Add(TechSection.Team, UiIcon.People, TechText.T("reallife_ui_tech_sec_team"), _firm.People);
                Add(TechSection.Market, UiIcon.Globe, TechText.T("reallife_ui_tech_sec_market"), 0);
                Add(TechSection.Rivals, UiIcon.Flag, TechText.T("reallife_ui_tech_sec_rivals"), rivals);
                Add(TechSection.Money, UiIcon.Coin, TechText.T("reallife_ui_tech_sec_money"), 0);
                if (studio)
                {
                    Add(TechSection.Awards, UiIcon.Trophy, TechText.T("reallife_ui_tech_sec_awards"), awards);
                }
                _m.Nav = nav.ToArray();
                _m.NavKeys = keys.ToArray();
                _m.Title = m.Name;
                _m.Icon = TechGlyphs.Of(TechGlyphs.Firm, m.Kind);
                int selected = 1;
                for (int i = 1; i < keys.Count; i++)
                {
                    selected = keys[i] == "sec:" + _v.Section ? i : selected;
                }
                _m.NavSelected = selected;
                if (selected == 1 && _v.Section != TechSection.Overview)
                {
                    _v.Section = TechSection.Overview;   // a section this firm does not have (Awards on a firm that is no studio)
                }
            }

            private int AwardsOf(string place)
            {
                StudioFile s = _d.Studio;
                int n = 0;
                for (int i = 0; s != null && i < s.Games.Count; i++)
                {
                    n += s.Games[i].Place == place && s.Games[i].Award ? 1 : 0;
                }
                return n;
            }

            private void FirmPage()
            {
                string pick = _v.Pick;
                if (pick != null)
                {
                    if (Pick(pick, TechApp.ProductRow) != null)
                    {
                        ProductPage(pick);
                        return;
                    }
                    if (Pick(pick, TechApp.NewProductRow) != null)
                    {
                        NewProductPage(pick);
                        return;
                    }
                    if (Pick(pick, TechApp.RivalRow) != null)
                    {
                        RivalPage(Pick(pick, TechApp.RivalRow));
                        return;
                    }
                    if (Pick(pick, TechApp.ContractRow) != null)
                    {
                        ContractPage(pick);
                        return;
                    }
                    _v.Pick = null;
                }
                switch (_v.Section)
                {
                    case TechSection.Products: FirmProducts(); break;
                    case TechSection.Team: Team(); break;
                    case TechSection.Market: Market(); break;
                    case TechSection.Rivals: RivalsList(); break;
                    case TechSection.Money: Money(); break;
                    case TechSection.Awards: Awards(); break;
                    default: Overview(); break;
                }
            }

            // ------------------------------------------------------------------------------------------------- the overview

            private void Overview()
            {
                Firm m = _firm.File;
                string place = m.Place;
                bool studio = Studio;
                FirmPage page = studio ? _d.StudioFirmPage(_firm, W) : _d.SectorOn ? _d.SectorFirmPage(_firm, W) : null;
                var app = new UiAppModel();
                _m.Shape = TechShape.Overview;
                _m.App = app;
                StudioScreen screen = page != null ? page.Screen : null;
                if (screen != null && screen.Next != null)
                {
                    _m.Next = Words(screen.Next);
                }
                else
                {
                    _m.Next = TechText.T("reallife_ui_tech_next_firm", "firm", m.Name);
                }
                var actions = new List<UiActionModel>(3);
                for (int i = 0; screen != null && i < screen.Others.Count && actions.Count < 2; i++)
                {
                    ViewAction a = screen.Others[i];
                    if (!ShowsOnOverview(a.Act))
                    {
                        continue;
                    }
                    actions.Add(Button(TechText.T(a.Key), UiButtonKind.Secondary, actions.Count == 0 ? UiAction.Option : UiAction.Count, ActRun(a, place, 0), !a.Enabled));
                }
                if (screen != null && screen.Primary != null && ShowsOnOverview(screen.Primary.Act))
                {
                    actions.Add(Button(TechText.T(screen.Primary.Key), UiButtonKind.Primary, UiAction.Alt, ActRun(screen.Primary, place, 0), !screen.Primary.Enabled));
                }
                else if (FirmsCatalogue.MakesProducts[m.Kind] != 0)
                {
                    actions.Add(Button(TechText.T("reallife_tech_button_new_product"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("newprod:" + place))));
                }
                _m.Actions = actions.ToArray();
                app.Hero = HeroOf(place, studio);
                var trend = Weeks(Ring(m.Days));
                var takings = new UiFigureModel { Label = TechText.T("reallife_ui_tech_takings_30"), Value = Cents(m.Revenue30), Trend = trend };
                if (TechStatusRules.Change(m.Revenue30, m.Prev30, m.PrevDays, out int percent))
                {
                    takings.Change = (percent > 0 ? "+" : string.Empty) + percent.ToString(Inv) + "%";
                    takings.Rising = percent >= 0;
                    takings.Good = percent >= 0;
                }
                app.Figures = new[] { takings };
                ProductsTable(app, place, studio);
                MarketTable(app, page, studio);
                PeopleBlock(app, place, page);
            }

            private static bool ShowsOnOverview(int act) => act != StudioAct.Close && act != StudioAct.BeatPick && act != StudioAct.ProjectPick;

            private float[] Ring(long[] ring)
            {
                var days = new float[ring.Length];
                for (int k = 0; k < days.Length; k++)
                {
                    days[k] = ring[(_day + 1 + k) % ring.Length];
                }
                return days;
            }

            // What a button of a studio's or a firm's page asks: the app's own moves, or the director's. A desk step only shows the desk (the director's ToDesk), never runs.
            private TechRun ActRun(ViewAction a, string place, int id)
            {
                bool studio = Studio;
                int act = a.Act, arg = a.Arg;
                if (act == StudioAct.Concept || (!studio && act == SectorAct.NewProduct))
                {
                    return Go(v => v.Open("newprod:" + place));
                }
                if (!studio && act == SectorAct.Contracts)
                {
                    return Go(v => v.Open("tab:" + TabContracts));
                }
                if (act == StudioAct.Withdraw)
                {
                    return Ask("reallife_tech_confirm_withdraw_title", "reallife_tech_confirm_withdraw_body", () => _d.Withdraw(id));
                }
                return studio ? Do(() => _d.StudioDo(act, place, id, arg)) : Do(() => _d.SectorDo(act, place, arg));
            }

            // The game or product being made: its picture, its progress, and for a studio's game the five points of its making met against waiting.
            private UiHeroModel HeroOf(string place, bool studio)
            {
                Product best = null;
                for (int i = 0; i < _f.Products.Count; i++)
                {
                    Product p = _f.Products[i];
                    if (p.Place == place && p.Stage == 0 && (best == null || p.Progress > best.Progress))
                    {
                        best = p;
                    }
                }
                if (best == null)
                {
                    return null;
                }
                GameDev g = studio && best.K == ProductKind.Game && _d.Studio != null ? _d.Studio.GameOf(best.Id) : null;
                var hero = new UiHeroModel { Kicker = TechText.T("reallife_ui_tech_in_development"), Title = best.Name, Tint = UiRole.None };
                if (g != null)
                {
                    var states = new int[StudioViews.MilestoneCount];
                    int met = StudioViews.Milestones(g, states);
                    int made = StudioRules.Progress(g) / 10;
                    hero.Icon = TechGlyphs.Of(TechGlyphs.Genre, g.Genre);
                    hero.Progress = Math.Max(0, Math.Min(100, made));
                    hero.Line = TechText.Fill("reallife_ui_tech_hero_game", "genre", TechText.T(StudioViews.GenreKey(g.Genre)), "made", made.ToString(Inv),
                        "met", met.ToString(Inv), "of", StudioViews.MilestoneCount.ToString(Inv));
                    for (int i = 0; i < states.Length; i++)
                    {
                        if (states[i] == StudioViews.StateNow)
                        {
                            hero.Note = TechText.T("reallife_ui_tech_at_desk");
                            hero.NoteIcon = UiIcon.Locate;
                        }
                    }
                    return hero;
                }
                hero.Icon = TechGlyphs.Of(TechGlyphs.Product, best.Kind);
                int need = Math.Max(1, FirmsRules.FeatureCost(best.Features));
                int percent = Math.Max(0, Math.Min(100, best.Progress * 100 / need));
                hero.Progress = best.K == ProductKind.Game ? Math.Max(0, Math.Min(100, (int)Math.Min(100L,
                    (long)best.Progress * 100 / FirmsCatalogue.GameScope[TalentCatalogue.Clamp(best.Scope, 0, 2)]))) : percent;
                hero.Line = TechText.Fill("reallife_ui_tech_hero_product", "kind", TechText.ProductName(best.K), "features", best.Features.ToString(Inv), "percent", hero.Progress.ToString(Inv));
                return hero;
            }

            // The games or products out, with their scores graded and their state as a chip.
            private void ProductsTable(UiAppModel app, string place, bool studio)
            {
                app.TableTitle = TechText.T(studio ? "reallife_ui_tech_games_out" : "reallife_ui_tech_products_out");
                app.Columns = new[]
                {
                    new UiColumn { Title = TechText.T(studio ? "reallife_studio_page_col_game" : "reallife_ui_tech_col_product") },
                    new UiColumn { Title = TechText.T(studio ? "reallife_studio_page_col_genre" : "reallife_ui_tech_col_kind"), Width = 112f },
                    new UiColumn { Title = TechText.T(studio ? "reallife_studio_page_col_score" : "reallife_ui_tech_col_quality"), Width = 88f, Right = true },
                    new UiColumn { Title = TechText.T(studio ? "reallife_studio_page_col_copies" : "reallife_ui_tech_col_customers"), Width = 96f, Right = true },
                    new UiColumn { Title = TechText.T(studio ? "reallife_studio_page_col_kept" : "reallife_ui_tech_col_month"), Width = 96f, Right = true },
                    new UiColumn { Title = TechText.T("reallife_ui_tech_col_status"), Width = 136f, Right = true }
                };
                app.GradeColumn = 2;
                var rows = new List<UiAppRow>(8);
                var keys = new List<string>(8);
                for (int i = _f.Products.Count - 1; i >= 0 && rows.Count < 6; i--)
                {
                    Product p = _f.Products[i];
                    if (p.Place != place || p.Stage == 0)
                    {
                        continue;
                    }
                    rows.Add(ProductRow(p, studio));
                    keys.Add("prod:" + p.Id);
                }
                app.Rows = rows.ToArray();
                _m.RowKeys = keys.ToArray();
            }

            private UiAppRow ProductRow(Product p, bool studio)
            {
                GameDev g = studio && p.K == ProductKind.Game && _d.Studio != null ? _d.Studio.GameOf(p.Id) : null;
                int quality = g != null ? Math.Max(0, g.Score) : FirmsRules.Quality(p) / 10;
                var row = new UiAppRow
                {
                    Icon = g != null ? TechGlyphs.Of(TechGlyphs.Genre, g.Genre) : TechGlyphs.Of(TechGlyphs.Product, p.Kind), Tint = UiRole.None, Grade = quality
                };
                string status = p.Stage == 2 ? TechText.T("reallife_tech_state_withdrawn") : TechText.T("reallife_ui_tech_out_days", "days", Math.Max(0, _day - p.Launched).ToString(Inv));
                row.Status = status;
                row.StatusTone = p.Stage == 2 ? UiRole.Warn : UiRole.Ok;
                row.Cells = new[]
                {
                    p.Name, g != null ? TechText.T(StudioViews.GenreKey(g.Genre)) : TechText.ProductName(p.K), g != null && g.Score >= 0 ? g.Score.ToString(Inv) : quality.ToString(Inv),
                    TechText.Whole(p.Users), Cents(g != null ? Level(g.Net) : Level(FirmsRules.MonthlyRevenue(p))), status
                };
                return row;
            }

            // The market under the table: a studio's rival games, a firm's own markets.
            private void MarketTable(UiAppModel app, FirmPage page, bool studio)
            {
                StudioFile s = _d.Studio;
                if (studio && s != null)
                {
                    app.MoreTitle = TechText.T("reallife_ui_tech_games_market");
                    app.MoreColumns = new[]
                    {
                        new UiColumn { Title = TechText.T("reallife_studio_page_col_rival") }, new UiColumn { Title = TechText.T("reallife_studio_page_col_studio"), Grow = true },
                        new UiColumn { Title = TechText.T("reallife_studio_page_col_score"), Width = 64f, Right = true },
                            new UiColumn { Title = TechText.T("reallife_studio_page_col_copies"), Width = 80f, Right = true }
                    };
                    app.MoreGrade = 2;
                    var best = new List<RivalGame>(s.Rivals);
                    best.Sort((a, b) => b.Sold.CompareTo(a.Sold));
                    var rows = new List<UiAppRow>(5);
                    for (int i = 0; i < best.Count && i < 5; i++)
                    {
                        RivalGame r = best[i];
                        rows.Add(new UiAppRow
                        {
                            Icon = TechGlyphs.Of(TechGlyphs.Genre, r.Genre), Tint = UiRole.None, Grade = r.Score,
                            Cells = new[] { r.Name, StudioCatalogue.RivalNames[Math.Max(0, Math.Min(r.Studio, StudioCatalogue.RivalNames.Length - 1))], r.Score.ToString(Inv), TechText.Whole(r.Sold) }
                        });
                    }
                    app.MoreRows = rows.ToArray();
                    return;
                }
                if (page != null && page.More.Rows.Count > 0)
                {
                    ViewTable t = page.More;
                    app.MoreTitle = TechText.T("reallife_ui_tech_markets");
                    var cols = new UiColumn[t.ColumnKeys.Length];
                    for (int i = 0; i < cols.Length; i++)
                    {
                        bool figure = i < t.Figures.Length && t.Figures[i];
                        cols[i] = new UiColumn { Title = TechText.T(t.ColumnKeys[i]), Width = figure ? 88f : 0f, Right = figure, Grow = i == 1 };
                    }
                    app.MoreColumns = cols;
                    app.MoreGrade = -1;
                    var rows = new List<UiAppRow>(5);
                    for (int i = 0; i < t.Rows.Count && i < 5; i++)
                    {
                        rows.Add(new UiAppRow { Icon = UiIcon.Globe, Tint = UiRole.None, Cells = t.Rows[i], Grade = -1 });
                    }
                    app.MoreRows = rows.ToArray();
                }
            }

            // The team: faces with their skill and morale graded, the way to the rest, and what the firm lacks with the link to fill it.
            private void PeopleBlock(UiAppModel app, string place, FirmPage page)
            {
                var people = PeopleOf(place);
                app.PeopleTitle = TechText.T("reallife_ui_tech_sec_team");
                app.PeopleCount = TechText.T("reallife_ui_tech_people_n", "count", people.Count.ToString(Inv));
                var rows = new List<UiPersonRow>(MostPeople);
                var keys = new List<string>(MostPeople);
                for (int i = 0; i < people.Count && rows.Count < MostPeople; i++)
                {
                    rows.Add(PersonRow(people[i]));
                    keys.Add("sec:" + TechSection.Team);
                }
                app.People = rows.ToArray();
                _m.PersonKeys = keys.ToArray();
                if (people.Count > MostPeople)
                {
                    app.PeopleMore = TechText.T("reallife_ui_tech_see_all_team", "count", people.Count.ToString(Inv));
                }
                bool needed = false;
                for (int i = 0; page != null && i < page.Team.Count; i++)
                {
                    needed |= page.Team[i].Needed;
                }
                StudioPlace sp = Studio ? _d.StudioPlaceOf(_firm) : null;
                if (sp != null && sp.Tickets > sp.Answered)
                {
                    app.Note = TechText.T("reallife_ui_tech_note_support", "tickets", TechText.Whole(sp.Tickets - sp.Answered));
                    app.NoteAction = TechText.T("reallife_ui_tech_open_roles");
                    _m.NoteRun = Run(Do(() => _d.OpenRoles(TechDirector.TabTeam, null)));
                }
                else if (needed)
                {
                    app.Note = TechText.T("reallife_studio_page_needed_how");
                    app.NoteAction = TechText.T("reallife_ui_tech_open_roles");
                    _m.NoteRun = Run(Do(() => _d.OpenRoles(TechDirector.TabTeam, null)));
                }
            }

            private List<Person> PeopleOf(string place)
            {
                var list = new List<Person>(16);
                IReadOnlyList<Person> all = _d.People;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].File.Place == place)
                    {
                        list.Add(all[i]);
                    }
                }
                list.Sort((a, b) => b.MainPoints.CompareTo(a.MainPoints));
                return list;
            }

            private static UiPersonRow PersonRow(Person p) => new UiPersonRow
            {
                Name = p.Name, Hue = UiColour.HueOf(p.Name), RoleIcon = FamilyIcon(p.File.Def.Family), RoleTint = UiRole.None, Role = TechText.Role(p.File.Role),
                FirstLabel = TechText.T("reallife_ui_tech_skill"), First = Math.Max(0, Math.Min(100, p.MainPoints)),
                SecondLabel = TechText.T("reallife_ui_tech_morale"), Second = Math.Max(0, Math.Min(100, p.File.Morale / 10))
            };

            private static UiIcon FamilyIcon(RealLifeOverhaul.Tech.Family f)
            {
                switch (f)
                {
                    case RealLifeOverhaul.Tech.Family.Engineering: return UiIcon.Code;
                    case RealLifeOverhaul.Tech.Family.Data: return UiIcon.Database;
                    case RealLifeOverhaul.Tech.Family.Security: return UiIcon.ShieldCheck;
                    case RealLifeOverhaul.Tech.Family.Infrastructure: return UiIcon.HardDrives;
                    case RealLifeOverhaul.Tech.Family.Product: return UiIcon.Lightbulb;
                    case RealLifeOverhaul.Tech.Family.Design: return UiIcon.PaintBrush;
                    case RealLifeOverhaul.Tech.Family.Commercial: return UiIcon.Megaphone;
                    case RealLifeOverhaul.Tech.Family.Support: return UiIcon.Headset;
                    default: return UiIcon.Briefcase;
                }
            }

            // ------------------------------------------------------------------------------------------------- the other sections

            private void FirmProducts()
            {
                Firm m = _firm.File;
                string place = m.Place;
                bool studio = Studio;
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                for (int i = _f.Products.Count - 1; i >= 0; i--)
                {
                    Product p = _f.Products[i];
                    if (p.Place == place)
                    {
                        rows.Add(ProductDataRow(p));
                        keys.Add("prod:" + p.Id);
                    }
                }
                _m.Shape = TechShape.Table;
                _m.Table = ProductTable(rows, TechText.T("reallife_tech_products_none"));
                _m.RowKeys = keys.ToArray();
                ProductsLead(place);
            }

            private void ProductsLead(string place)
            {
                Firm m = _firm != null ? _firm.File : null;
                bool makes = m != null && FirmsCatalogue.MakesProducts[m.Kind] != 0;
                if (makes)
                {
                    int room = FirmsRules.Room(_f, place, _d.SectorOn);
                    _m.Next = room == FirmsRules.RoomYes ? TechText.Fill("reallife_tech_products_room", "firm", FirmsRules.LiveCount(_f, place).ToString(Inv), "firm_most",
                        ProductsPerFirm.ToString(Inv),
                        "all", FirmsRules.LiveCount(_f, null).ToString(Inv), "all_most", ProductsMost.ToString(Inv))
                        : TechText.T(room == FirmsRules.RoomFirmFull ? "reallife_tech_new_full_firm" : "reallife_tech_new_full_all");
                    _m.Actions = new[] { Button(TechText.T("reallife_tech_button_new_product"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Open("newprod:" + place)), room != FirmsRules.RoomYes) };
                }
                else
                {
                    _m.Next = TechText.T("reallife_tech_products_no_maker");
                }
            }

            private int ProductsPerFirm => _d.SectorOn ? SectorCatalogue.ProductsPerFirm : FirmsCatalogue.ProductsPerFirm;

            private int ProductsMost => _d.SectorOn ? SectorCatalogue.ProductsMost : FirmsCatalogue.ProductsMost;

            private void Team()
            {
                string place = _firm.File.Place;
                var people = PeopleOf(place);
                var rows = new List<UiDataRow>(16);
                for (int i = 0; i < people.Count; i++)
                {
                    Person p = people[i];
                    UiDataRow row = Row(5);
                    row.Cells[0] = p.Name;
                    row.Sub = TechText.Role(p.File.Role);
                    row.Hue = UiColour.HueOf(p.Name);
                    row.Ring = p.File.Morale >= 600 ? UiRole.Ok : p.File.Morale >= 350 ? UiRole.Warn : UiRole.Danger;
                    row.Cells[1] = TechText.Level(p.File.Level);
                    row.Cells[2] = TechText.Percent(p.MainPoints);
                    row.Values[2] = Math.Max(0, Math.Min(100, p.MainPoints));
                    row.Cells[3] = TechText.Percent(p.File.Morale / 10);
                    row.Values[3] = Math.Max(0, Math.Min(100, p.File.Morale / 10));
                    row.Cells[4] = p.HoursYesterday.ToString(Inv);
                    row.Values[4] = p.HoursYesterday;
                    rows.Add(row);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_ui_tech_col_person", UiCellKind.Person), Col("reallife_ui_tech_col_level", UiCellKind.Text, 120f), Col("reallife_ui_tech_skill", UiCellKind.Meter, 160f),
                        Col("reallife_ui_tech_morale", UiCellKind.Meter, 160f), Col("reallife_ui_tech_col_hours", UiCellKind.Figure, 96f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, Count = TechText.T("reallife_ui_tech_people_n", "count", people.Count.ToString(Inv)),
                    EmptyTitle = TechText.T("reallife_ui_tech_team_empty"), EmptyBody = TechText.T("reallife_ui_tech_team_empty_body"), EmptyIcon = UiIcon.People
                };
                _m.RowKeys = new string[0];
                _m.Next = TechText.T(people.Count == 0 ? "reallife_ui_tech_team_empty_body" : "reallife_ui_tech_team_next");
                _m.Actions = new[] { Button(TechText.T("reallife_ui_tech_open_roles"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.OpenRoles(TechDirector.TabTeam, null))) };
            }

            private void Market()
            {
                var left = new List<UiFactCardModel>(4);
                var right = new List<UiFactCardModel>(4);
                string place = _firm.File.Place;
                StudioScreen v = Studio ? _d.StudioFirmScreen(_firm, W) : _d.SectorOn ? _d.SectorFirmScreen(_firm, W) : null;
                _m.Shape = TechShape.Sheet;
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_ui_tech_sec_market"), Icon = UiIcon.Globe, Role = _firm.File.Name };
                if (v != null)
                {
                    for (int i = 0; i < v.Sections.Count; i++)
                    {
                        (i % 2 == 0 ? left : right).Add(SectionCard(v.Sections[i], place, 0));
                    }
                }
                else
                {
                    left.Add(new TechCard(_runs).Say(TechText.T("reallife_ui_tech_market_none")).Build());
                }
                _m.Left = left.ToArray();
                _m.Right = right.ToArray();
            }

            private void Money()
            {
                string place = _firm.File.Place;
                Firm m = _firm.File;
                long arr = Level(FirmsRules.Arr(_f, place));
                _m.Shape = TechShape.Sheet;
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_ui_tech_sec_money"), Icon = UiIcon.Coin, Role = m.Name };
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_takings_30"), Value = Cents(m.Revenue30) },
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_value"), Value = Cents(FirmsRules.Value(m, arr, 0)) },
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_yearly"), Value = Cents(arr) }
                };
                var facts = new TechCard(_runs, TechText.T("reallife_ui_tech_standing"));
                facts.Row(TechText.T("reallife_ui_tech_reputation"), TechText.Mille(m.Reputation), m.Reputation < 350 ? UiRole.DangerText : UiRole.None);
                facts.Row(TechText.T("reallife_ui_tech_debt"), TechText.Mille(m.Debt));
                facts.Row(TechText.T("reallife_ui_tech_outages_breaches"), m.Outages.ToString(Inv) + " / " + m.Breaches.ToString(Inv));
                facts.Say(_day < m.RegisteredUntil ? TechText.T("reallife_tech_firm_registered", "day", m.RegisteredUntil.ToString(Inv))
                    : TechText.T("reallife_tech_firm_unregistered", "fee", TechText.Money(Prices.At(FirmsCatalogue.RegisterFee / 100))), _day < m.RegisteredUntil ? UiRole.None : UiRole.WarnText);
                if (_day >= m.RegisteredUntil - 7)
                {
                    facts.Button(TechText.T("reallife_tech_button_register"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.Register(place)));
                }
                string focus = TechText.T("reallife_tech_firm_focus", "focus", TechText.FocusName((Focus)m.Focus));
                facts.Step(TechText.T("reallife_ui_tech_focus"), focus, null, true, true,
                    Do(() => _d.SetFocus(place, (m.Focus + (int)Focus.Count - 1) % (int)Focus.Count)), Do(() => _d.SetFocus(place, (m.Focus + 1) % (int)Focus.Count)));
                _m.Left = new[] { facts.Build() };
                var raise = new TechCard(_runs, TechText.T("reallife_tech_tab_funding"));
                VentureFile venture = _d.Ventures;
                if (venture == null)
                {
                    raise.Say(TechText.T("reallife_vc_off_body"));
                }
                else
                {
                    Cap cap = venture.CapOf(place);
                    raise.Row(TechText.T("reallife_vc_col_stage"), cap != null && cap.Stage >= 0 ? TechText.RoundName((RoundStage)cap.Stage) : TechText.T("reallife_vc_bootstrapped"));
                    raise.Row(TechText.T("reallife_vc_col_share"), TechText.Mille(cap != null ? VentureRules.PlayerShare(cap) : 1000));
                    raise.Go(TechText.T("reallife_ui_tech_open_funding"), Go(v => v.Open("tab:" + TabFunding)));
                }
                _m.Right = new[] { raise.Build() };
                _m.Back = null;
                PathCard();
            }

            private void PathCard()
            {
            }

            private void Awards()
            {
                StudioFile s = _d.Studio;
                string place = _firm.File.Place;
                var rows = new List<UiDataRow>(4);
                var keys = new List<string>(4);
                for (int i = 0; s != null && i < s.Games.Count; i++)
                {
                    GameDev g = s.Games[i];
                    if (g.Place != place || !g.Award)
                    {
                        continue;
                    }
                    Product p = _f.ProductOf(g.Id);
                    UiDataRow row = Row(4);
                    row.Cells[0] = p != null ? p.Name : "-";
                    row.Values[0] = (int)TechGlyphs.Of(TechGlyphs.Genre, g.Genre);
                    row.Cells[1] = TechText.T(StudioViews.GenreKey(g.Genre));
                    row.Cells[2] = g.Score.ToString(Inv);
                    row.Values[2] = Math.Max(0, Math.Min(100, g.Score));
                    row.Cells[3] = TechText.Day(g.Launched);
                    rows.Add(row);
                    keys.Add("prod:" + g.Id);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_studio_page_col_game", UiCellKind.Tile), Col("reallife_studio_page_col_genre", UiCellKind.Text, 140f), Col("reallife_studio_page_col_score",
                            UiCellKind.Figure, 90f, true),
                        Col("reallife_ui_tech_col_out_on", UiCellKind.Date, 120f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_ui_tech_awards_empty"), EmptyBody = TechText.T("reallife_ui_tech_awards_empty_body"),
                    EmptyIcon = UiIcon.Trophy
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = TechText.T("reallife_ui_tech_awards_next", "score", StudioCatalogue.AwardScore.ToString(Inv));
            }

            // One section of a studio's or a sector's screen as a card: its title, its lines, its choices as steps with what they mean, and a button on a choice.
            private UiFactCardModel SectionCard(ViewSection section, string place, int id)
            {
                var card = new TechCard(_runs, TechText.T(section.TitleKey));
                for (int i = 0; i < section.Lines.Count; i++)
                {
                    card.Say(Words(section.Lines[i]), Ink(section.Lines[i].Tone));
                }
                for (int i = 0; i < section.Choices.Count; i++)
                {
                    ChoiceLines(card, section.Choices[i], place, id);
                }
                return card.Build();
            }

            private void ChoiceLines(TechCard card, ViewChoice c, string place, int id)
            {
                if (c.Options.Count > 0)
                {
                    int count = c.Options.Count, at = Math.Max(0, Math.Min(count - 1, c.Picked)), act = c.Act;
                    string label = TechText.T(c.LabelKey);
                    int Value(int index) => c.Values.Count == count ? c.Values[index] : index;
                    TechRun Pick(int index) => ChoiceRun(act, place, id, Value(index));
                    card.Step(label, c.Options[at], c.Trade != null ? Words(c.Trade) : null, count > 1, count > 1, Pick((at + count - 1) % count), Pick((at + 1) % count));
                }
                else if (c.Trade != null)
                {
                    card.Say(Words(c.Trade), Ink(c.Trade.Tone));
                }
                if (c.Go != null && c.Go.Enabled)
                {
                    ViewAction go = c.Go;
                    card.Button(TechText.T(go.Key), UiButtonKind.Secondary, UiAction.Count, ActRun(go, place, id));
                }
            }

            // A choice's own state (the marketing beat and the project picked) is the view's; the rest is the director's.
            private TechRun ChoiceRun(int act, string place, int id, int value)
            {
                if (act == StudioAct.BeatPick)
                {
                    return Go(v => _beatPick = value);
                }
                if (act == StudioAct.ProjectPick)
                {
                    return Go(v => _projectPick = value);
                }
                return Studio ? Do(() => _d.StudioDo(act, place, id, value)) : Do(() => _d.SectorDo(act, place, value));
            }
        }

        // What a studio's game page has chosen and not yet pressed: the beat and the project, kept with the live screen over the page's writing.
        private static int _beatPick, _projectPick;
    }
}
