using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Startups and Funding tabs' founding pages: the short flow that founds a startup (problem, niche, co-founders, split, articles, pre-seed source), the niches with their size,
    /// and a founded firm's go to market (its stage, fit, niches and co-founders). Every button is the director's call and nothing is read in a frame: the page is written when it
    /// changes.
    /// </summary>
    public sealed partial class TechLive
    {
        public const string FoundRow = "found:", NichesRow = "niches:", NicheRow = "niche:";
        private const int PageGtm = 3;

        // What the founding flow has picked so far; a page that was left keeps it until the player founds or picks otherwise.
        private static string _foundPlace;
        private static int _foundSector, _foundNiche = -1, _foundCo = -1, _foundSplit = 4, _foundArticles, _foundSource, _foundStep, _foundAngel;

        // The problems a startup can solve: every sector of the venture catalogue but games, which the studio makes.
        private static readonly int[] FoundSectors =
        {
            (int)Venture.Software, (int)Venture.Fintech, (int)Venture.Health, (int)Venture.Ai, (int)Venture.Marketplace, (int)Venture.Climate, (int)Venture.Security, (int)Venture.DevTools
        };

        private static readonly int[] FoundAngels = { 0, 7 };

        private sealed partial class Page
        {
            private TechRun Cycle(int n, int at, int by, Action<int> set) => Go(v => set(((at + by) % n + n) % n));

            private void CycleStep(TechCard card, string label, string value, string hint, int n, int at, Action<int> set)
            {
                card.Step(label, value, hint, n > 1, n > 1, Cycle(n, at, -1, set), Cycle(n, at, 1, set));
            }

            // ------------------------------------------------------------------------------------------------- founding a startup

            private void FoundPage()
            {
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_startups");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_found_title"), Icon = TechGlyphs.Startup, Role = TechText.T("reallife_found_role") };
                var firms = new List<FirmPlace>(8);
                _d.Foundable(firms);
                if (firms.Count == 0)
                {
                    var none = new TechCard(_runs, TechText.T("reallife_found_card_idea"));
                    none.Say(TechText.T("reallife_found_none"));
                    none.Go(TechText.T("reallife_found_firms_link"), Go(v => v.Open("tab:" + TabFirms)));
                    _m.Left = new[] { none.Build() };
                    return;
                }
                int at = 0;
                for (int i = 0; i < firms.Count; i++)
                {
                    at = firms[i].File.Place == _foundPlace ? i : at;
                }
                FirmPlace firm = firms[at];
                _foundPlace = firm.File.Place;
                var niches = new List<int> { -1 };
                for (int n = 0; n < NicheCatalogue.Count; n++)
                {
                    if (_d.NicheNow(n).Exists)
                    {
                        niches.Add(n);
                    }
                }
                int nicheAt = Math.Max(0, niches.IndexOf(_foundNiche));
                _foundNiche = niches[nicheAt];
                int sectorAt = Math.Max(0, Array.IndexOf(FoundSectors, _foundSector));
                _foundSector = FoundSectors[sectorAt];
                List<CofounderOffer> offers = _d.Cofounders();
                _foundCo = Math.Min(_foundCo, offers.Count - 1);
                _foundSource = Math.Max(0, Math.Min(FoundCatalogue.Sources - 1, _foundSource));

                var idea = new TechCard(_runs, TechText.T("reallife_found_card_idea"));
                CycleStep(idea, TechText.T("reallife_found_firm"), firm.File.Name, null, firms.Count, at, i => _foundPlace = firms[i].File.Place);
                CycleStep(idea, TechText.T("reallife_found_problem"), TechText.VentureName((Venture)_foundSector), null, FoundSectors.Length, sectorAt, i => _foundSector = FoundSectors[i]);
                CycleStep(idea, TechText.T("reallife_found_niche"), niches[nicheAt] < 0 ? TechText.T("reallife_found_niche_none") : TechText.T(NicheCatalogue.NameKey(niches[nicheAt])), null,
                    niches.Count, nicheAt, i => _foundNiche = niches[i]);
                if (_foundNiche >= 0)
                {
                    NicheLines(idea, _foundNiche);
                }
                else
                {
                    idea.Say(TechText.T("reallife_found_niche_about"));
                }

                var team = new TechCard(_runs, TechText.T("reallife_found_card_team"));
                var names = new List<string> { TechText.T("reallife_found_cofounder_none") };
                for (int i = 0; i < offers.Count; i++)
                {
                    names.Add(offers[i].Name);
                }
                CycleStep(team, TechText.T("reallife_found_cofounder"), names[_foundCo + 1], null, names.Count, _foundCo + 1, i => _foundCo = i - 1);
                if (_foundCo >= 0)
                {
                    CofounderOffer o = offers[_foundCo];
                    team.Say(TechText.Fill("reallife_found_offer_line", "name", o.Name, "age", o.Age.ToString(Inv), "role", TechText.Role(o.Role), "level", TechText.Level(o.Level),
                        "skill", (o.Main / 1000).ToString(Inv), "from", TechText.T(o.Origin == CofounderOrigin.Population ? "reallife_found_origin_population" : "reallife_found_origin_pool")));
                    int steps = FoundCatalogue.SplitMost / FoundCatalogue.SplitStep + 1;
                    _foundSplit = Math.Max(1, Math.Min(steps - 1, _foundSplit));
                    CycleStep(team, TechText.T("reallife_found_split"), TechText.Mille(_foundSplit * FoundCatalogue.SplitStep), null, steps - 1, _foundSplit - 1, i => _foundSplit = i + 1);
                    team.Say(TechText.Fill("reallife_found_vesting", "years", VentureCatalogue.VestYears.ToString(Inv), "cliff", VentureCatalogue.CliffYears.ToString(Inv)));
                }
                else
                {
                    team.Say(offers.Count == 0 ? TechText.T("reallife_found_cofounder_nobody") : TechText.T("reallife_found_cofounder_about"));
                }

                var papers = new TechCard(_runs, TechText.T("reallife_found_card_papers"));
                CycleStep(papers, TechText.T("reallife_found_articles"), TechText.T(FoundCatalogue.ArticlesKey(_foundArticles)), null, FoundCatalogue.Articles, _foundArticles,
                    i => _foundArticles = i);
                papers.Say(TechText.T(FoundCatalogue.ArticlesAboutKey(_foundArticles)));
                papers.Say(TechText.Fill("reallife_found_fee", "fee", Cents(_d.FeeNow(_foundArticles))));
                CycleStep(papers, TechText.T("reallife_found_source"), TechText.T(FoundCatalogue.SourceKey(_foundSource)), null, FoundCatalogue.Sources, _foundSource, i => _foundSource = i);
                SourceLines(papers);
                _m.Left = new[] { idea.Build(), team.Build() };
                _m.Right = new[] { papers.Build() };

                var draft = new FoundDraft
                {
                    Place = _foundPlace, Sector = _foundSector, Niche = _foundNiche, Articles = _foundArticles, Source = _foundSource, Cofounder = _foundCo,
                    Split = _foundSplit * FoundCatalogue.SplitStep, Step = _foundStep, Angel = FoundAngels[Math.Max(0, Math.Min(FoundAngels.Length - 1, _foundAngel))]
                };
                _m.HeadActions = new[]
                {
                    Button(TechText.T("reallife_found_button"), UiButtonKind.Primary, UiAction.Alt, Ask("reallife_found_confirm_title", "reallife_found_confirm_body", () =>
                    {
                        if (_d.FoundStartup(draft))
                        {
                            _foundCo = -1;
                            _v.Open("cap:" + draft.Place);
                            _v.Page = PageGtm;
                        }
                    }))
                };
            }

            // The first money's terms for the source picked, in the words of what the player gets and gives.
            private void SourceLines(TechCard card)
            {
                switch (_foundSource)
                {
                    case FoundCatalogue.SourceFamily:
                    {
                        int n = FoundCatalogue.FamilyTickets.Length;
                        _foundStep = Math.Max(0, Math.Min(n - 1, _foundStep));
                        long amount = FoundRules.FamilyAmount(_foundStep), pre = FoundRules.FamilyPre();
                        CycleStep(card, TechText.T("reallife_found_family_step"), Cents(_d.Level(amount)), null, n, _foundStep, i => _foundStep = i);
                        card.Say(TechText.Fill("reallife_found_src_family", "amount", Cents(_d.Level(amount)), "share", TechText.Mille((int)(amount * 1000 / (pre + amount)))));
                        break;
                    }
                    case FoundCatalogue.SourceAngels:
                    {
                        _foundAngel = Math.Max(0, Math.Min(FoundAngels.Length - 1, _foundAngel));
                        int vc = FoundAngels[_foundAngel];
                        CycleStep(card, TechText.T("reallife_found_angel"), VentureCatalogue.Firms[vc].Name, null, FoundAngels.Length, _foundAngel, i => _foundAngel = i);
                        TermSheet t = _d.AngelOffer(vc, _foundArticles);
                        card.Say(TechText.Fill("reallife_found_src_angels", "amount", Cents(_d.Level(t.Amount)), "share", TechText.Mille(t.EquityMille), "pre", Cents(_d.Level(t.Pre))));
                        break;
                    }
                    case FoundCatalogue.SourceAccelerator:
                        card.Say(TechText.Fill("reallife_found_src_accel", "amount", Cents(_d.Level(FoundRules.AccelAmount())), "share", TechText.Mille(FoundCatalogue.AccelShare),
                            "days", FoundCatalogue.AccelDays.ToString(Inv)));
                        break;
                    default:
                        card.Say(TechText.T("reallife_found_src_savings"));
                        break;
                }
                if (_foundSource != FoundCatalogue.SourceSavings)
                {
                    card.Say(TechText.T("reallife_found_held"));
                }
            }

            // A niche's size in a few lines, as the founding page and the niche's own page both say it.
            private void NicheLines(TechCard card, int niche)
            {
                NicheFacts f = _d.NicheNow(niche);
                card.Say(TechText.Fill("reallife_niche_line", "businesses", TechText.Whole(f.Businesses), "buyers", TechText.Whole(f.Buyers), "ceiling", Cents(_d.Level(f.Ceiling)),
                    "rivals", f.Rivals.ToString(Inv)));
                card.Say(TechText.Fill("reallife_niche_mood", "cycle", TechText.Mille(f.Mood.Cycle), "season", TechText.Mille(f.Mood.Season), "rent", TechText.Mille(f.Mood.Rent)));
            }

            // ------------------------------------------------------------------------------------------------- the niches

            private void NichesPage()
            {
                var rows = new List<UiDataRow>(NicheCatalogue.Count);
                var keys = new List<string>(NicheCatalogue.Count);
                for (int n = 0; n < NicheCatalogue.Count; n++)
                {
                    NicheFacts f = _d.NicheNow(n);
                    if (!f.Exists)
                    {
                        continue;
                    }
                    UiDataRow row = Row(7);
                    row.Cells[0] = TechText.T(NicheCatalogue.NameKey(n));
                    row.Values[0] = (int)TechGlyphs.Startup;
                    row.Cells[1] = TechText.Whole(f.Businesses);
                    row.Cells[2] = TechText.Whole(f.Buyers);
                    row.Cells[3] = Cents(_d.Level(f.Ceiling));
                    row.Cells[4] = f.Rivals.ToString(Inv);
                    row.Cells[5] = TechText.Mille(f.Going);
                    int mood = f.Mood.All;
                    row.Cells[6] = TechText.Mille(mood);
                    row.Tones[6] = mood >= 1050 ? UiRole.Ok : mood <= 950 ? UiRole.Warn : UiRole.Notice;
                    rows.Add(row);
                    keys.Add(NicheRow + n);
                }
                _m.Shape = TechShape.Table;
                _m.Back = TechText.T("reallife_ui_tech_back_startups");
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_niche_col_niche", UiCellKind.Tile), Col("reallife_niche_col_businesses", UiCellKind.Figure, 96f), Col("reallife_niche_col_buyers", UiCellKind.Figure, 100f),
                        Col("reallife_niche_col_ceiling", UiCellKind.Money, 112f), Col("reallife_niche_col_rivals", UiCellKind.Figure, 76f), Col("reallife_niche_col_price", UiCellKind.Text, 76f),
                        Col("reallife_niche_col_mood", UiCellKind.Chip, 120f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_niche_empty"), EmptyBody = TechText.T("reallife_niche_empty_body"),
                    EmptyIcon = TechGlyphs.Startup
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = TechText.T("reallife_niche_about");
            }

            private void NichePage(int niche)
            {
                NicheFacts f = _d.NicheNow(niche);
                NicheDef def = NicheCatalogue.Def(niche);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_niche_back");
                _m.Head = new UiHeadModel { Name = TechText.T(NicheCatalogue.NameKey(niche)), Icon = TechGlyphs.Startup, Role = TechText.T("reallife_niche_role") };
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_niche_col_businesses"), Value = TechText.Whole(f.Businesses) },
                    new UiFigureModel { Label = TechText.T("reallife_niche_col_buyers"), Value = TechText.Whole(f.Buyers) },
                    new UiFigureModel { Label = TechText.T("reallife_niche_col_ceiling"), Value = Cents(_d.Level(f.Ceiling)) }
                };
                var facts = new TechCard(_runs, TechText.T("reallife_niche_card_size"));
                facts.Row(TechText.T("reallife_niche_col_businesses"), TechText.Whole(f.Businesses));
                if (f.Staff > 0)
                {
                    facts.Row(TechText.T("reallife_niche_staff"), TechText.Whole(f.Staff));
                }
                facts.Row(TechText.T("reallife_niche_col_buyers"), TechText.Whole(f.Buyers));
                facts.Row(TechText.T("reallife_niche_col_rivals"), f.Rivals.ToString(Inv));
                facts.Row(TechText.T("reallife_niche_col_price"), TechText.Mille(f.Going));
                facts.Say(TechText.T("reallife_niche_size_about"));
                var mood = new TechCard(_runs, TechText.T("reallife_niche_card_mood"));
                mood.Row(TechText.T("reallife_niche_cycle"), TechText.Mille(f.Mood.Cycle));
                mood.Row(TechText.T("reallife_niche_season"), def.Good != null ? TechText.Mille(f.Mood.Season) : "-");
                mood.Row(TechText.T("reallife_niche_rent"), def.Rented ? TechText.Mille(f.Mood.Rent) : "-");
                mood.Say(TechText.T("reallife_niche_mood_about"));
                _m.Left = new[] { facts.Build() };
                var right = new List<UiFactCardModel>(2) { mood.Build() };
                FoundedFile file = _d.Foundings;
                var open = new TechCard(_runs, TechText.T("reallife_niche_card_open"));
                for (int i = 0; file != null && i < file.Items.Count; i++)
                {
                    FoundedFirm rec = file.Items[i];
                    FirmPlace firm = _d.FirmOf(rec.Place);
                    if (firm == null || !FoundRules.CanExpand(rec, niche) || !rec.Counts)
                    {
                        continue;
                    }
                    string place = rec.Place;
                    open.Button(TechText.Fill("reallife_niche_button_open", "firm", firm.File.Name, "cost", Cents(_d.ExpandCostNow(place))), UiButtonKind.Secondary, UiAction.Count,
                        Ask("reallife_niche_confirm_title", "reallife_niche_confirm_body", () => _d.Expand(place, niche)));
                }
                if (open.LineCount == 0)
                {
                    open.Say(TechText.T("reallife_niche_open_none"));
                }
                right.Add(open.Build());
                _m.Right = right.ToArray();
            }

            // ------------------------------------------------------------------------------------------------- a founded firm's go to market

            private void GtmPage(FirmPlace firm)
            {
                string place = firm.File.Place;
                FoundedFirm f = _d.FoundingOf(place);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_cap");
                _m.Head = new UiHeadModel { Name = TechText.Fill("reallife_gtm_title", "firm", firm.File.Name), Icon = TechGlyphs.Startup, Role = TechText.FirmName(firm.File.K) };
                if (f == null)
                {
                    var none = new TechCard(_runs, TechText.T("reallife_gtm_card_stage"));
                    none.Say(TechText.T("reallife_gtm_none"));
                    none.Go(TechText.T("reallife_gtm_found_link"), Go(v => v.Open(FoundRow + place)));
                    _m.Left = new[] { none.Build() };
                    return;
                }
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_gtm_figure_stage"), Value = TechText.T(FoundCatalogue.StageKey(f.Stage)) },
                    new UiFigureModel { Label = TechText.T("reallife_gtm_figure_fit"), Value = f.Counts ? TechText.Mille(f.Fit) : "-" },
                    new UiFigureModel { Label = TechText.T("reallife_gtm_figure_niche"), Value = f.Niche >= 0 ? TechText.T(NicheCatalogue.NameKey(f.Niche)) : "-" }
                };
                var stage = new TechCard(_runs, TechText.T("reallife_gtm_card_stage"));
                StageLines(stage, f);
                if (f.Placed && !f.Engaged)
                {
                    stage.Say(TechText.T("reallife_gtm_placed"));
                    stage.Button(TechText.T("reallife_gtm_button_engage"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.Engage(place)));
                }
                var left = new List<UiFactCardModel>(2) { stage.Build() };
                if (f.Niche >= 0)
                {
                    var niche = new TechCard(_runs, TechText.T("reallife_gtm_card_niche"));
                    niche.Row(TechText.T("reallife_gtm_main_niche"), TechText.T(NicheCatalogue.NameKey(f.Niche)));
                    NicheLines(niche, f.Niche);
                    for (int i = 0; i < f.Expanded.Count; i++)
                    {
                        niche.Row(TechText.T("reallife_gtm_opened"), TechText.T(NicheCatalogue.NameKey(f.Expanded[i].Niche)));
                    }
                    niche.Go(TechText.T("reallife_gtm_niches_link"), Go(v => v.Open(NichesRow)));
                    left.Add(niche.Build());
                }
                _m.Left = left.ToArray();
                var right = new List<UiFactCardModel>(2);
                var papers = new TechCard(_runs, TechText.T("reallife_gtm_card_papers"));
                papers.Row(TechText.T("reallife_found_articles"), TechText.T(FoundCatalogue.ArticlesKey(f.Articles)));
                papers.Row(TechText.T("reallife_found_source"), TechText.T(FoundCatalogue.SourceKey(f.Source)));
                papers.Say(TechText.T(FoundCatalogue.ArticlesAboutKey(f.Articles)));
                right.Add(papers.Build());
                if (f.Cofounders.Count > 0)
                {
                    var team = new TechCard(_runs, TechText.T("reallife_gtm_card_cofounders"));
                    Cap cap = _d.CapFor(place, false);
                    for (int i = 0; i < f.Cofounders.Count; i++)
                    {
                        CoFounder co = f.Cofounders[i];
                        int no = co.No;
                        if (co.Left > 0)
                        {
                            team.Row(co.Name, TechText.Fill("reallife_gtm_cofounder_left", "kept", TechText.Whole(co.Kept), "day", co.Left.ToString(Inv)), UiRole.WarnText);
                            continue;
                        }
                        int vested = FoundRules.Vested(co, _day, GameCalendar.DaysPerYear);
                        team.Who(co.Name, TechText.Role(co.Role) + ", " + TechText.Level(co.Level), TechText.Fill("reallife_gtm_cofounder_vested", "vested", TechText.Mille(vested)));
                        team.Button(TechText.Fill("reallife_gtm_button_part", "name", co.Name), UiButtonKind.Secondary, UiAction.Count,
                            Ask("reallife_gtm_part_title", "reallife_gtm_part_body", () => _d.PartWays(place, no)), cap == null);
                    }
                    right.Add(team.Build());
                }
                _m.Right = right.ToArray();
            }

            // Where the firm stands in going to market: the stage, fit as a meter, the channel and price chosen, and what to do next at the desk.
            private void StageLines(TechCard card, FoundedFirm f)
            {
                card.Row(TechText.T("reallife_gtm_figure_stage"), TechText.T(FoundCatalogue.StageKey(f.Stage)));
                card.Bar(TechText.T("reallife_gtm_figure_fit"), f.Counts ? f.Fit / 10 : 0, f.Counts ? TechText.Mille(f.Fit) : "-");
                card.Say(f.PmfDay > 0 ? TechText.Fill("reallife_gtm_fit_found", "day", f.PmfDay.ToString(Inv)) : TechText.Fill("reallife_gtm_fit_not", "bar", TechText.Mille(FoundCatalogue.PmfBar),
                    "users", FoundCatalogue.PmfUsers.ToString(Inv)), f.PmfDay > 0 ? UiRole.OkText : UiRole.None);
                card.Row(TechText.T("reallife_gtm_learned"), TechText.Mille(f.Learned));
                card.Row(TechText.T("reallife_gtm_channel"), f.Channel == FoundCatalogue.ChannelPartner && f.Partner >= 0 && f.Partner < HiringCatalogue.RivalFirms.Length
                    ? TechText.T(FoundCatalogue.ChannelKey(f.Channel)) + ": " + HiringCatalogue.RivalFirms[f.Partner] : TechText.T(FoundCatalogue.ChannelKey(f.Channel)));
                card.Row(TechText.T("reallife_gtm_price"), f.Price >= 0 ? TechText.Tier(f.Price) : "-");
                card.Say(TechText.T(FoundCatalogue.NextKey(f.Stage)));
            }

            // The firm's go to market in a card on its money page: the stage and fit, and the way to the page.
            private void GtmCard(List<UiFactCardModel> into, string place)
            {
                FoundedFirm f = _d.FoundingOf(place);
                if (f == null)
                {
                    return;
                }
                var card = new TechCard(_runs, TechText.T("reallife_gtm_card_stage"));
                card.Row(TechText.T("reallife_gtm_figure_stage"), TechText.T(FoundCatalogue.StageKey(f.Stage)));
                card.Row(TechText.T("reallife_gtm_figure_fit"), f.Counts ? TechText.Mille(f.Fit) : "-");
                card.Row(TechText.T("reallife_gtm_figure_niche"), f.Niche >= 0 ? TechText.T(NicheCatalogue.NameKey(f.Niche)) : "-");
                card.Go(TechText.T("reallife_gtm_open"), Go(v =>
                {
                    v.Open("tab:" + TabFunding);
                    v.Open("cap:" + place);
                    v.Page = PageGtm;
                }));
                into.Add(card.Build());
            }
        }
    }
}
