using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class TechLive
    {
        private static readonly long[] FundSteps = { 1000000, 5000000, 25000000, 100000000 };   // cents at the start's prices
        private static int _fundStep = 1;
        private const int PageCap = 0, PageRaise = 1;

        private sealed partial class Page
        {
            private void VentureOff()
            {
                _m.Shape = TechShape.Off;
                _m.OffTitle = TechText.T("reallife_roles_off_title");
                _m.OffBody = TechText.T("reallife_vc_off_body");
                _m.OffAction = TechText.T("reallife_ui_tech_close");
            }

            // ------------------------------------------------------------------------------------------------- the startups tab

            private void StartupsTab()
            {
                VentureFile v = _d.Ventures;
                if (v == null)
                {
                    VentureOff();
                    return;
                }
                string pick = _v.Pick;
                if (pick != null)
                {
                    Startup picked = int.TryParse(Pick(pick, TechApp.StartupRow), out int sid) ? v.StartupOf(sid) : null;
                    if (picked != null)
                    {
                        StartupPage(v, picked);
                        return;
                    }
                    _v.Pick = null;
                }
                var rows = new List<UiDataRow>(16);
                var keys = new List<string>(16);
                long invested = 0, value = 0;
                int holdings = 0;
                for (int i = 0; i < v.Startups.Count; i++)
                {
                    Startup s = v.Startups[i];
                    long mine = VentureRules.PlayerValue(s, out int share, out long put);
                    bool known = s.Diligence || (s.Revealed & 1) != 0;
                    long revenue = known ? s.Mrr : VentureRules.Claimed(s);
                    UiDataRow row = Row(8);
                    row.Cells[0] = s.Name;
                    row.Sub = s.Founder;
                    row.Values[0] = (int)TechGlyphs.Startup;
                    row.Cells[1] = TechText.VentureName((Venture)s.Sector);
                    row.Cells[2] = TechText.RoundName((RoundStage)s.Stage);
                    row.Cells[3] = Cents(_d.Level(revenue)) + (known ? string.Empty : " *");
                    row.Cells[4] = Cents(_d.Level(s.Post));
                    row.Cells[5] = put > 0 ? TechText.Mille(share) : "-";
                    row.Values[5] = share / 10;
                    row.Cells[6] = Cents(_d.Level(mine));
                    row.Cells[7] = s.Ask != null ? TechText.T("reallife_vc_state_asks") : TechText.StartupStateName((StartupState)s.State);
                    row.Tones[7] = s.Ask != null ? UiRole.Ok : s.Alive ? UiRole.Notice : UiRole.Warn;
                    rows.Add(row);
                    keys.Add("start:" + s.Id);
                    if (put > 0)
                    {
                        holdings++;
                        invested += put;
                        value += mine;
                    }
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_vc_col_startup", UiCellKind.Tile), Col("reallife_vc_col_sector", UiCellKind.Text, 120f), Col("reallife_vc_col_stage", UiCellKind.Text, 96f),
                        Col("reallife_vc_col_revenue", UiCellKind.Money, 100f), Col("reallife_vc_col_value", UiCellKind.Money, 100f), Col("reallife_vc_col_share", UiCellKind.Text, 72f),
                        Col("reallife_vc_col_yours", UiCellKind.Money, 100f), Col("reallife_ui_tech_col_status", UiCellKind.Chip, 120f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_vc_portfolio_title"),
                        EmptyBody = TechText.T("reallife_vc_fund_none"), EmptyIcon = TechGlyphs.Startup
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = TechText.Fill("reallife_vc_portfolio", "count", holdings.ToString(Inv), "invested", Cents(_d.Level(invested)), "value", Cents(_d.Level(value)), "hype",
                    TechText.Mille(_d.HypeNow));
                FundCard(v);
            }

            // The player's fund beside the startups: what it holds, how much to put in, and the buttons.
            private void FundCard(VentureFile v)
            {
                Fund fund = v.Fund;
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_fund_title"));
                card.Say(fund.Open ? TechText.Fill("reallife_vc_fund_line", "committed", Cents(_d.Level(fund.Committed)), "cash", Cents(_d.Level(fund.Cash)), "invested",
                    Cents(_d.Level(fund.Invested)),
                    "returned", Cents(_d.Level(fund.Returned))) : TechText.T("reallife_vc_fund_none"));
                int at = Math.Max(0, Math.Min(FundSteps.Length - 1, _fundStep));
                card.Step(TechText.T("reallife_ui_tech_fund_step"), TechText.T("reallife_vc_fund_step", "amount", Cents(_d.Level(FundSteps[at]))), null, at > 0, at < FundSteps.Length - 1,
                    Go(view => _fundStep = at - 1), Go(view => _fundStep = at + 1));
                long step = _d.Level(FundSteps[at]);
                card.Button(TechText.T(fund.Open ? "reallife_vc_button_fund_in" : "reallife_vc_button_fund_open"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.FundIn(step)));
                if (fund.Open && fund.Cash > 0)
                {
                    card.Button(TechText.T("reallife_vc_button_fund_out"), UiButtonKind.Secondary, UiAction.Option, Do(() => _d.FundOut()));
                }
                _m.Side = card.Build();
            }

            private void StartupPage(VentureFile v, Startup s)
            {
                int id = s.Id;
                long mine = VentureRules.PlayerValue(s, out int share, out long put);
                bool known = s.Diligence || (s.Revealed & 1) != 0;
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_startups");
                _m.Head = new UiHeadModel
                {
                    Name = s.Name, Icon = TechGlyphs.Startup, Role = TechText.VentureName((Venture)s.Sector) + ", " + s.Founder, Status = s.Ask != null ? TechText.T("reallife_vc_state_asks")
                        : TechText.StartupStateName((StartupState)s.State), StatusTone = s.Alive ? UiRole.Ok : UiRole.Warn
                };
                _m.Figures = new[]
                {
                    new UiFigureModel { Label = TechText.T("reallife_vc_col_revenue"), Value = Cents(_d.Level(known ? s.Mrr : VentureRules.Claimed(s))) },
                    new UiFigureModel { Label = TechText.T("reallife_vc_col_value"), Value = Cents(_d.Level(s.Post)) },
                    new UiFigureModel { Label = TechText.T("reallife_vc_col_stage"), Value = TechText.RoundName((RoundStage)s.Stage) },
                    new UiFigureModel { Label = TechText.T("reallife_vc_col_yours"), Value = put > 0 ? Cents(_d.Level(mine)) : "-" }
                };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_startup_facts"));
                if (s.Ask != null)
                {
                    card.Say(TechText.Fill("reallife_vc_startup_ask", "amount", Cents(_d.Level(s.Ask.Amount)), "pre", Cents(_d.Level(s.Ask.Pre)), "share",
                        TechText.Mille(s.Ask.EquityMille), "day", s.Ask.Lapse.ToString(Inv)),
                        UiRole.OkText);
                }
                if (put > 0)
                {
                    card.Say(TechText.Fill("reallife_vc_startup_yours", "share", TechText.Mille(share), "value", Cents(_d.Level(mine)), "invested", Cents(_d.Level(put))));
                }
                card.Say(TechText.Fill("reallife_vc_startup_line", "founder", s.Founder, "stage", TechText.RoundName((RoundStage)s.Stage), "founded", s.Founded.ToString(Inv),
                    "revenue", Cents(_d.Level(known ? s.Mrr : VentureRules.Claimed(s))), "said", TechText.T(known ? "reallife_vc_startup_true" : "reallife_vc_startup_claimed"),
                        "value", Cents(_d.Level(s.Post))));
                int runway = VentureRules.Runway(s.Cash, s.Burn, s.Mrr);
                card.Say(s.Diligence
                    ? TechText.Fill("reallife_vc_startup_diligence", "quality",
                        TechText.T(s.Quality >= 700 ? "reallife_vc_quality_strong" : s.Quality >= 400 ? "reallife_vc_quality_fair" : "reallife_vc_quality_weak"),
                        "claim", TechText.Bp(s.ClaimBp), "runway", runway == int.MaxValue ? "-" : runway.ToString(Inv))
                    : TechText.T("reallife_vc_startup_no_diligence", "fee", Cents(_d.Level(VentureCatalogue.DiligenceFee))));
                StartupTerms(card, s);
                var actions = new List<UiActionModel>(3);
                if (!s.Diligence && s.Alive)
                {
                    actions.Add(Button(TechText.T("reallife_vc_button_diligence"), UiButtonKind.Secondary, UiAction.Option, Do(() => _d.Diligence(id))));
                }
                bool followOn = put > 0 && VentureRules.FollowOnOpen(s, _day);
                if (followOn)
                {
                    card.Say(TechText.T("reallife_vc_startup_follow_on", "amount", Cents(_d.Level(VentureRules.FollowOnAmount(s)))));
                    card.Button(TechText.T("reallife_vc_button_follow_angel"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.FollowOn(id, false)));
                    if (_d.FundOpen)
                    {
                        card.Button(TechText.T("reallife_vc_button_follow_fund"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.FollowOn(id, true)));
                    }
                }
                if (s.Ask != null)
                {
                    actions.Add(Button(TechText.T("reallife_vc_button_meet"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.MeetFounders(id))));
                }
                _m.HeadActions = actions.ToArray();
                _m.Left = new[] { card.Build() };
            }

            // ------------------------------------------------------------------------------------------------- the funding tab

            private void FundingTab()
            {
                VentureFile v = _d.Ventures;
                if (v == null)
                {
                    VentureOff();
                    return;
                }
                IReadOnlyList<FirmPlace> firms = _d.FirmPlaces;
                string pick = _v.Pick;
                string cap = pick != null ? Pick(pick, TechApp.CapRow) : null;
                FirmPlace picked = cap != null ? _d.FirmOf(cap) : null;
                if (picked != null)
                {
                    if (_v.Page == PageRaise)
                    {
                        RaisePage(picked);
                    }
                    else if (_v.Page == PageBooks)
                    {
                        BooksPage(picked);
                    }
                    else
                    {
                        CapPage(picked);
                    }
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(8);
                var keys = new List<string>(8);
                for (int i = 0; i < firms.Count; i++)
                {
                    FirmPlace firm = firms[i];
                    Cap c = v.CapOf(firm.File.Place);
                    long profit = _d.FirmProfit(firm);
                    long runway = profit < 0 ? VentureGame.Cash / -profit : -1;
                    UiDataRow row = Row(8);
                    row.Cells[0] = firm.File.Name;
                    row.Sub = TechText.FirmName(firm.File.K);
                    row.Values[0] = (int)TechGlyphs.Of(TechGlyphs.Firm, firm.File.Kind);
                    row.Cells[1] = c != null && c.Stage >= 0 ? TechText.RoundName((RoundStage)c.Stage) : TechText.T("reallife_vc_bootstrapped");
                    int share = c != null ? VentureRules.PlayerShare(c) : 1000;
                    row.Cells[2] = TechText.Mille(share);
                    row.Cells[3] = Cents(c != null ? _d.Level(c.Post) : 0);
                    long raised = 0;
                    for (int h = 0; c != null && h < c.Holders.Count; h++)
                    {
                        raised += c.Holders[h].Kind == (int)HolderKind.Vc ? c.Holders[h].Invested : 0;
                    }
                    row.Cells[4] = Cents(_d.Level(raised));
                    row.Cells[5] = c != null ? c.VcSeats + " / " + c.BoardSeats : "-";
                    row.Cells[6] = runway < 0 ? "-" : runway.ToString(Inv);
                    row.Tones[6] = runway >= 0 && runway < 60 ? UiRole.DangerText : UiRole.None;
                    row.Cells[7] = c == null ? "-" : c.RaiseUntil > 0 ? TechText.T("reallife_vc_raising")
                        : c.Pressure == 1 ? TechText.T("reallife_vc_warned") : c.Pressure == 2 ? TechText.T("reallife_vc_replaced")
                        : TechText.T("reallife_vc_steady");
                    row.Tones[7] = c != null && c.Pressure > 0 ? UiRole.Warn : c != null && c.RaiseUntil > 0 ? UiRole.Notice : UiRole.Ok;
                    rows.Add(row);
                    keys.Add("cap:" + firm.File.Place);
                }
                _m.Shape = TechShape.Table;
                _m.Table = new UiDataTableModel
                {
                    Columns = new[]
                    {
                        Col("reallife_tech_col_firm", UiCellKind.Tile), Col("reallife_vc_col_stage", UiCellKind.Text, 104f), Col("reallife_vc_col_share", UiCellKind.Text, 72f),
                            Col("reallife_vc_col_value", UiCellKind.Money, 100f),
                        Col("reallife_vc_col_raised", UiCellKind.Money, 100f), Col("reallife_vc_col_board", UiCellKind.Text, 72f), Col("reallife_vc_col_runway", UiCellKind.Figure, 76f),
                        Col("reallife_ui_tech_col_status", UiCellKind.Chip, 120f)
                    },
                    Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = TechText.T("reallife_vc_funding_title"),
                    EmptyBody = TechText.Fill("reallife_vc_funding_about", "days", VentureCatalogue.RaiseDays.ToString(), "growth", TechText.Mille(VentureCatalogue.TargetGrowth),
                        "target", VentureCatalogue.TargetDays.ToString()),
                    EmptyIcon = UiIcon.Coin
                };
                _m.RowKeys = keys.ToArray();
                _m.Next = TechText.Fill("reallife_vc_funding_about", "days", VentureCatalogue.RaiseDays.ToString(), "growth", TechText.Mille(VentureCatalogue.TargetGrowth),
                    "target", VentureCatalogue.TargetDays.ToString());
            }

            private void CapPage(FirmPlace firm)
            {
                string place = firm.File.Place;
                Cap c = _d.CapFor(place, false);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_funding");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_vc_cap_title", "firm", firm.File.Name), Icon = TechGlyphs.Of(TechGlyphs.Firm, firm.File.Kind),
                    Role = TechText.FirmName(firm.File.K) };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_cap_table"));
                long profit = _d.FirmProfit(firm);
                card.Say(profit < 0 ? TechText.Fill("reallife_vc_cap_burn", "burn", Cents(-profit), "days",
                    (VentureGame.Cash / -profit).ToString()) : TechText.T("reallife_vc_cap_profit", "profit", Cents(profit)),
                    profit < 0 ? UiRole.WarnText : UiRole.None);
                if (c != null)
                {
                    long total = Math.Max(1, c.Total);
                    for (int i = 0; i < c.Holders.Count; i++)
                    {
                        Holder h = c.Holders[i];
                        string who = h.Kind == (int)HolderKind.Player ? TechText.T("reallife_vc_holder_you") : h.Kind == (int)HolderKind.Pool ? TechText.T("reallife_vc_holder_pool")
                            : VentureCatalogue.Firms[TalentCatalogue.Clamp(h.Who, 0, VentureCatalogue.FirmCount - 1)].Name;
                        card.Row(who, TechText.Mille(VentureRules.ShareOf(h, total)));
                    }
                    card.Say(TechText.Fill("reallife_vc_cap_board", "seats", c.BoardSeats.ToString(), "theirs", c.VcSeats.ToString(), "target", Cents(_d.Level(c.TargetMrr)), "day",
                        c.TargetDay.ToString(),
                        "now", Cents(_d.FirmMrr(place)), "misses", c.Misses.ToString()));
                }
                else
                {
                    card.Say(TechText.T("reallife_vc_cap_all_yours"));
                }
                if (c != null)
                {
                    AccountLines(card, firm, c);
                }
                var chiefs = new List<Person>(8);
                IReadOnlyList<Person> people = _d.People;
                for (int i = 0; i < people.Count; i++)
                {
                    if (people[i].File.Place == place && people[i].File.Level >= TalentCatalogue.Senior)
                    {
                        chiefs.Add(people[i]);
                    }
                }
                if (chiefs.Count > 0)
                {
                    string ceo = c != null ? c.Ceo : string.Empty;
                    int at = 0;
                    for (int i = 0; i < chiefs.Count; i++)
                    {
                        at = chiefs[i].File.Id == ceo ? i + 1 : at;
                    }
                    int n = chiefs.Count + 1;
                    string Chief(int i) => TechText.T("reallife_vc_cap_chief", "name", i == 0 ? TechText.T("reallife_vc_holder_you") : chiefs[i - 1].Name);
                    TechRun Set(int i) => Do(() => _d.SetChief(place, i == 0 ? string.Empty : chiefs[i - 1].File.Id));
                    card.Step(TechText.T("reallife_ui_tech_chief"), Chief(at), null, true, true, Set((at + n - 1) % n), Set((at + 1) % n));
                }
                card.Button(TechText.T("reallife_vc_button_options"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.GrantOptions(place)));
                if (c != null && c.VcSeats > 0)
                {
                    card.Button(TechText.T("reallife_vc_button_sell"), UiButtonKind.Secondary, UiAction.Count, Ask("reallife_vc_confirm_sell_title",
                        "reallife_vc_confirm_sell_body", () => _d.SellShares(place)));
                }
                card.Button(TechText.T("reallife_vc_button_listing"), UiButtonKind.Secondary, UiAction.Count, Do(() => _d.PrepareListing(place)));
                var head = new List<UiActionModel>(2);
                if (c != null && StartupMoney.Has(c))
                {
                    head.Add(Button(TechText.T("reallife_vc_button_books"), UiButtonKind.Secondary, UiAction.Option, Go(v => v.Page = PageBooks)));
                }
                head.Add(Button(TechText.T("reallife_vc_button_raise_page"), UiButtonKind.Primary, UiAction.Alt, Go(v => v.Page = PageRaise)));
                _m.HeadActions = head.ToArray();
                _m.Left = new[] { card.Build() };
                UiFactCardModel[] held = HeldCards(c);
                if (held != null)
                {
                    _m.Right = held;
                }
            }

            private void RaisePage(FirmPlace firm)
            {
                string place = firm.File.Place;
                Cap c = _d.CapFor(place, false);
                _m.Shape = TechShape.Sheet;
                _m.Back = TechText.T("reallife_ui_tech_back_cap");
                _m.Head = new UiHeadModel { Name = TechText.T("reallife_vc_raise_title", "firm", firm.File.Name), Icon = UiIcon.Coin, Role = TechText.FirmName(firm.File.K) };
                var card = new TechCard(_runs, TechText.T("reallife_ui_tech_raise_round"));
                bool raising = c != null && c.RaiseUntil != 0;
                card.Say(raising ? TechText.Fill("reallife_vc_raise_on", "day", c.RaiseUntil.ToString(), "met", c.Meetings.ToString(), "most", VentureCatalogue.VcMeetingsMost.ToString())
                    : TechText.Fill("reallife_vc_raise_about", "days", VentureCatalogue.RaiseDays.ToString(), "meetings", VentureCatalogue.VcMeetingsMost.ToString()));
                if (c != null && c.Bridge > 0)
                {
                    card.Say(TechText.T("reallife_vc_bridge_owed", "amount", Cents(_d.Level(c.Bridge))), UiRole.WarnText);
                }
                var actions = new List<UiActionModel>(3);
                if (c != null && c.VcSeats > 0 && c.Bridge == 0)
                {
                    actions.Add(Button(TechText.T("reallife_vc_button_bridge"), UiButtonKind.Secondary, UiAction.Option, Do(() => _d.TakeBridge(place))));
                }
                if (!raising)
                {
                    actions.Add(Button(TechText.T("reallife_vc_button_raise"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.StartRaise(place))));
                }
                else if (c.Meetings < VentureCatalogue.VcMeetingsMost)
                {
                    actions.Add(Button(TechText.T("reallife_vc_button_meet_vc"), UiButtonKind.Primary, UiAction.Alt, Do(() => _d.MeetVc(place))));
                }
                _m.HeadActions = actions.ToArray();
                for (int i = 0; c != null && i < c.Sheets.Count; i++)
                {
                    TermSheet t = c.Sheets[i];
                    int index = i;
                    card.Say(TechText.Fill("reallife_vc_sheet_line", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(t.Vc, 0, VentureCatalogue.FirmCount - 1)].Name, "amount",
                        Cents(_d.Level(t.Amount)),
                        "pre", Cents(_d.Level(t.Pre)), "share", TechText.Mille(t.EquityMille), "board",
                            TechText.T(t.Board ? "reallife_vc_sheet_board" : "reallife_vc_sheet_no_board"), "day", t.Lapse.ToString()));
                    card.Button(TechText.T("reallife_vc_button_take_sheet"), UiButtonKind.Primary, UiAction.Count, Ask("reallife_vc_confirm_sheet_title",
                        "reallife_vc_confirm_sheet_body", () => _d.TakeSheet(place, index)));
                }
                _m.Left = new[] { card.Build() };
                if (c != null && c.Sheets.Count > 0)
                {
                    _m.Right = OfferCards(firm, c);
                }
            }
        }
    }
}
