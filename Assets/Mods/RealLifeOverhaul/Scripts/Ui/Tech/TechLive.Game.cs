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
            private const int GameOverview = 0, GamePlan = 1;

            // A studio's game: its hero, its five points as a track, what the critics said and how it sold on the first page; its build, marketing, after-launch work and team on the
            // second, where every choice the old page had stands. A desk step (a milestone's sign-off, a playtest, a patch, a sale) only opens the desk; the session is the desk's.
            private bool GamePage(Product p)
            {
                StudioScreen v = _d.StudioGameScreen(p, _beatPick, _projectPick, W);
                StudioFile s = _d.Studio;
                GameDev g = s != null ? s.GameOf(p.Id) : null;
                if (v == null || g == null)
                {
                    return false;
                }
                string place = p.Place;
                int id = p.Id;
                if (_v.Page == GamePlan)
                {
                    GamePlanPage(v, p, place);
                    return true;
                }
                _m.Shape = TechShape.Game;
                _m.Back = null;
                _m.Next = v.Next != null ? Words(v.Next) : string.Empty;
                var actions = new List<UiActionModel>(3);
                actions.Add(Button(TechText.T("reallife_ui_tech_plan"), UiButtonKind.Secondary, UiAction.Option, Go(view => view.Page = GamePlan)));
                if (v.Primary != null && v.Primary.Enabled)
                {
                    actions.Add(Button(TechText.T(v.Primary.Key), UiButtonKind.Primary, UiAction.Alt, ActRun(v.Primary, place, id)));
                }
                _m.Actions = actions.ToArray();
                int made = Math.Max(0, Math.Min(100, StudioRules.Progress(g) / 10));
                string phase = g.Early && g.OnSale ? TechText.T("reallife_studio_chip_early") : TechText.T(StudioViews.PhaseKey(g.Phase));
                _m.Hero = new UiHeroModel
                {
                    Icon = TechGlyphs.Of(TechGlyphs.Genre, g.Genre), Tint = UiRole.None,
                    Kicker = g.Launched >= 0 ? TechText.T("reallife_ui_tech_out_platforms", "days", Math.Max(0, _day - g.Launched).ToString(Inv), "platforms",
                        StudioViews.Platforms(g.Platforms, W)).ToUpperInvariant()
                        : (phase + ", " + StudioViews.Platforms(g.Platforms, W)).ToUpperInvariant(),
                    Title = p.Name,
                    Line = TechText.T("reallife_ui_tech_game_line", "genre", TechText.T(StudioViews.GenreKey(g.Genre)), "setting",
                        TechText.T(StudioViews.SettingKey(StudioRules.SettingOf(g.Genre, g.Setting)))),
                    Progress = g.Launched >= 0 ? -1 : made
                };
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_ui_tech_price"), Value = g.Price > 0 ? Cents(Level(g.Price)) : TechText.T("reallife_studio_price_free") },
                    new UiFigureModel { Label = TechText.T("reallife_studio_page_col_copies"), Value = TechText.Whole(g.Sold) },
                    new UiFigureModel { Label = TechText.T("reallife_studio_page_col_kept"), Value = Cents(Level(g.Net)) }
                };
                _m.Track = Track(g);
                GameReviews(g);
                GameSales(g, p);
                return true;
            }

            // The five points of the game's making as a track, each with where it stands.
            private UiTrackModel Track(GameDev g)
            {
                var states = new int[StudioViews.MilestoneCount];
                int met = StudioViews.Milestones(g, states);
                UiIcon[] icons = { UiIcon.Lightbulb, UiIcon.ClipboardText, UiIcon.Bug, UiIcon.ClipboardText, UiIcon.Rocket };
                string[] keys = { "reallife_ui_tech_ms_pitch", "reallife_ui_tech_ms_review1", "reallife_ui_tech_ms_playtest", "reallife_ui_tech_ms_review2", "reallife_ui_tech_ms_launch" };
                var track = new UiTrackModel
                {
                    Title = TechText.T("reallife_ui_tech_milestones"),
                    Line = TechText.T("reallife_ui_tech_met_of", "met", met.ToString(Inv), "of", StudioViews.MilestoneCount.ToString(Inv)),
                    Steps = new UiTrackStep[StudioViews.MilestoneCount],
                    StateWords = new[]
                    {
                        TechText.T("reallife_ui_tech_ms_done"), TechText.T("reallife_ui_tech_ms_now"), TechText.T("reallife_ui_tech_ms_next"), TechText.T("reallife_ui_tech_ms_locked"),
                        TechText.T("reallife_ui_tech_ms_later")
                    }
                };
                for (int i = 0; i < states.Length; i++)
                {
                    string note = i == StudioViews.MsPitch ? TechText.Day(g.Started) : i == StudioViews.MsLaunch && g.Launched >= 0 ? TechText.Day(g.Launched)
                        : states[i] == StudioViews.StateNow ? TechText.T("reallife_ui_tech_at_desk") : string.Empty;
                    UiStepState state = states[i] == StudioViews.StateDone ? UiStepState.Done : states[i] == StudioViews.StateNow ? UiStepState.Now
                        : states[i] == StudioViews.StateNext ? UiStepState.Next : UiStepState.Later;
                    track.Steps[i] = new UiTrackStep { Icon = icons[i], Label = TechText.T(keys[i]), Note = note, State = state };
                }
                return track;
            }

            // The reviews come with the outlets (StudioRules.Reviews); a game not out yet has none to quote.
            private void GameReviews(GameDev g)
            {
                _m.QuotesTitle = TechText.T("reallife_ui_tech_reviews");
                _m.QuotesEmpty = TechText.T("reallife_ui_tech_reviews_none");
                if (g.Reviews.Count == 0)
                {
                    return;
                }
                var quotes = new TechQuote[g.Reviews.Count];
                int sum = 0;
                for (int i = 0; i < quotes.Length; i++)
                {
                    GameReview r = g.Reviews[i];
                    quotes[i] = new TechQuote
                    {
                        Outlet = StudioCatalogue.Outlets[Math.Max(0, Math.Min(r.Outlet, StudioCatalogue.Outlets.Length - 1))], Score = r.Score, Day = TechText.Day(r.Day),
                        Line = TechText.T(StudioRules.QuoteKey(r))
                    };
                    sum += r.Score;
                }
                Array.Sort(quotes, (a, b) => b.Score.CompareTo(a.Score));
                _m.Quotes = quotes;
                _m.QuotesCount = TechText.T("reallife_ui_tech_outlets_n", "count", quotes.Length.ToString(Inv));
                _m.QuotesAverage = (sum / quotes.Length).ToString(Inv);
                _m.QuotesAverageLabel = TechText.T("reallife_ui_tech_average_score");
            }

            // Copies a day over the ring's 90 days with the best selling rival game's line beside it, and the events on it as a line of words under.
            private void GameSales(GameDev g, Product p)
            {
                if (g.Launched < 0)
                {
                    return;
                }
                int days = Math.Min(StudioCatalogue.SalesDays, Math.Max(2, _day - g.Launched + 1));
                var mine = new float[days];
                for (int i = 0; i < days; i++)
                {
                    mine[i] = StudioRules.SalesOn(g, _day - days + 1 + i);
                }
                var series = new List<UiSeries> { new UiSeries { Label = p.Name, Values = mine, Tint = UiRole.None } };
                StudioFile s = _d.Studio;
                RivalGame rival = s != null ? StudioRules.BestRival(s, g.Genre) : null;
                if (rival != null)
                {
                    var theirs = new float[days];
                    StudioRules.RivalSeries(rival, _day - days + 1, theirs);
                    series.Add(new UiSeries
                    {
                        Label = rival.Name + ", " + StudioCatalogue.RivalNames[Math.Max(0, Math.Min(rival.Studio, StudioCatalogue.RivalNames.Length - 1))], Values = theirs, Tint = UiRole.None
                    });
                }
                var points = new string[days];
                for (int i = 0; i < days; i++)
                {
                    points[i] = TechText.T("reallife_ui_tech_day_n", "day", (_day - days + 1 + i).ToString(Inv));
                }
                _m.Chart = new UiChartModel
                {
                    Kind = UiChartKind.Line, Title = TechText.T("reallife_ui_tech_copies_a_day"), Points = points, Series = series.ToArray(), Format = v => TechText.Whole((long)v),
                    AxisFormat = v => TechText.Whole((long)v), EvenAxis = true, Whole = true
                };
                var notes = new System.Text.StringBuilder(96);
                notes.Append(TechText.T("reallife_ui_tech_event_launch", "day", g.Launched.ToString(Inv)));
                for (int i = 0; i < g.Reviews.Count && i < 1; i++)
                {
                    notes.Append(", ").Append(TechText.T("reallife_ui_tech_event_review", "day", g.Reviews[i].Day.ToString(Inv)));
                }
                _m.ChartNotes = notes.ToString();
            }

            // The second page of a game: its sections as cards in two columns, every choice a step and every action a button.
            private void GamePlanPage(StudioScreen v, Product p, string place)
            {
                int id = p.Id;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_game");
                _m.Head = new UiHeadModel { Name = p.Name, Icon = TechGlyphs.Of(TechGlyphs.Product, p.Kind), Role = TechText.T("reallife_ui_tech_plan"),
                    Status = TechText.T(v.ChipKey ?? "reallife_studio_phase_pre"), StatusTone = Tone(v.ChipTone) };
                var left = new List<UiFactCardModel>(4);
                var right = new List<UiFactCardModel>(4);
                for (int i = 0; i < v.Sections.Count; i++)
                {
                    (i % 2 == 0 ? left : right).Add(SectionCard(v.Sections[i], place, id));
                }
                var actions = new List<UiActionModel>(2);
                for (int i = 0; i < v.Others.Count; i++)
                {
                    ViewAction a = v.Others[i];
                    if (a.Act == StudioAct.Withdraw)
                    {
                        actions.Add(Button(TechText.T(a.Key), UiButtonKind.Destructive, UiAction.Count, ActRun(a, place, id)));
                    }
                    else if (a.Act == StudioAct.Name && GameUi.TextBox.CanType)
                    {
                        actions.Add(Button(TechText.T(a.Key), UiButtonKind.Secondary, UiAction.Option, ActRun(a, place, id)));   // the game renamed, in the typed answer's box
                    }
                }
                _m.HeadActions = actions.ToArray();
                _m.Left = left.ToArray();
                _m.Right = right.ToArray();
            }
        }
    }
}
