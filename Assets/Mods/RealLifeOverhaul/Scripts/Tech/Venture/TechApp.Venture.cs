using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app's Startups and Funding tabs: the city's startups and the player's money in them, and the player's own firms with investors.</summary>
    public sealed partial class TechApp
    {
        private const int PageCap = 0, PageRaise = 1;
        private static readonly long[] FundSteps = { 1000000, 5000000, 25000000, 100000000 };   // cents at the start's prices
        private int _fundStep = 1, _chief;
        private readonly List<Person> _chiefs = new List<Person>(16);

        // ---------------------------------------------------------------- startups

        private TableSpec WriteStartups(InfoBox box, int day)
        {
            VentureFile v = _director.Ventures;
            if (v == null)
            {
                box.Title(TechText.T("reallife_roles_off_title"));
                box.Line(TechText.T("reallife_vc_off_body"));
                _picked = null;
                return TechColumns.Off;
            }
            Startup picked = null;
            string id = Picked(_picked, StartupRow);
            long invested = 0, value = 0;
            int holdings = 0;
            for (int i = 0; i < v.Startups.Count; i++)
            {
                Startup s = v.Startups[i];
                int share;
                long put;
                long mine = VentureRules.PlayerValue(s, out share, out put);
                bool known = s.Diligence || (s.Revealed & 1) != 0;
                long revenue = known ? s.Mrr : VentureRules.Claimed(s);
                TableRow row = Row(StartupRow + s.Id);
                row.Set(0, s.Name);
                row.Set(1, TechText.VentureName((Venture)s.Sector));
                row.Set(2, TechText.RoundName((RoundStage)s.Stage), s.Stage);
                row.Set(3, TechText.Cents(_director.Level(revenue)) + (known ? string.Empty : " *"), _director.Level(revenue) / 100.0);
                Cents(row, 4, _director.Level(s.Post));
                row.Set(5, put > 0 ? TechText.Mille(share) : "-", share);
                Cents(row, 6, _director.Level(mine));
                row.Set(7, s.Ask != null ? TechText.T("reallife_vc_state_asks") : TechText.StartupStateName((StartupState)s.State), s.Ask != null ? TableRow.Green : s.Alive ? TableRow.Plain : TableRow.Yellow);
                row.Kind = !s.Alive ? TechColumns.Closed : put > 0 ? TechColumns.Yours : s.Ask != null ? TechColumns.AsksYou : TechColumns.Others;
                if (put > 0)
                {
                    holdings++;
                    invested += put;
                    value += mine;
                }
                picked = id == s.Id.ToString() ? s : picked;
            }
            if (picked != null)
            {
                WriteStartup(box, v, picked, day);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_vc_portfolio_title"));
                box.Line(TechText.Fill("reallife_vc_portfolio", "count", holdings.ToString(), "invested", TechText.Cents(_director.Level(invested)), "value", TechText.Cents(_director.Level(value)),
                    "hype", TechText.Mille(_director.HypeNow)));
                Fund fund = v.Fund;
                box.Line(fund.Open
                    ? TechText.Fill("reallife_vc_fund_line", "committed", TechText.Cents(_director.Level(fund.Committed)), "cash", TechText.Cents(_director.Level(fund.Cash)), "invested", TechText.Cents(_director.Level(fund.Invested)),
                        "returned", TechText.Cents(_director.Level(fund.Returned)))
                    : TechText.T("reallife_vc_fund_none"));
                if (box.HasSelector)
                {
                    box.Selector(FundSteps.Length, _fundStep, i => TechText.T("reallife_vc_fund_step", "amount", TechText.Cents(_director.Level(FundSteps[i]))), i => Set(() => _fundStep = i));
                }
                Transform buttons = box.ButtonRow();
                long step = _director.Level(FundSteps[Mathf.Clamp(_fundStep, 0, FundSteps.Length - 1)]);
                InfoBox.Button(buttons, InfoBox.Positive, fund.Open ? "reallife_vc_button_fund_in" : "reallife_vc_button_fund_open", () => Do(() => _director.FundIn(step)));
                if (fund.Open && fund.Cash > 0)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_fund_out", () => Do(() => _director.FundOut()));
                }
                Meta.Guide.Point(box, StartupsGuide);
            }
            return TechColumns.Startups;
        }

        private void WriteStartup(InfoBox box, VentureFile v, Startup s, int day)
        {
            int id = s.Id;
            int share;
            long put;
            long mine = VentureRules.PlayerValue(s, out share, out put);
            bool known = s.Diligence || (s.Revealed & 1) != 0;
            box.Title(TechText.T("reallife_vc_startup_title", "name", s.Name, "sector", TechText.VentureName((Venture)s.Sector)));
            if (s.Ask != null)
            {
                box.Line(TechText.Fill("reallife_vc_startup_ask", "amount", TechText.Cents(_director.Level(s.Ask.Amount)), "pre", TechText.Cents(_director.Level(s.Ask.Pre)), "share", TechText.Mille(s.Ask.EquityMille),
                    "day", s.Ask.Lapse.ToString()));
            }
            if (put > 0)
            {
                box.Line(TechText.Fill("reallife_vc_startup_yours", "share", TechText.Mille(share), "value", TechText.Cents(_director.Level(mine)), "invested", TechText.Cents(_director.Level(put))));
            }
            box.Line(TechText.Fill("reallife_vc_startup_line", "founder", s.Founder, "stage", TechText.RoundName((RoundStage)s.Stage), "founded", s.Founded.ToString(),
                "revenue", TechText.Cents(_director.Level(known ? s.Mrr : VentureRules.Claimed(s))), "said", TechText.T(known ? "reallife_vc_startup_true" : "reallife_vc_startup_claimed"),
                "value", TechText.Cents(_director.Level(s.Post))));
            box.Line(s.Diligence
                ? TechText.Fill("reallife_vc_startup_diligence", "quality", TechText.T(s.Quality >= 700 ? "reallife_vc_quality_strong" : s.Quality >= 400 ? "reallife_vc_quality_fair" : "reallife_vc_quality_weak"),
                    "claim", TechText.Bp(s.ClaimBp), "runway", VentureRules.Runway(s.Cash, s.Burn, s.Mrr) == int.MaxValue ? "-" : VentureRules.Runway(s.Cash, s.Burn, s.Mrr).ToString())
                : TechText.T("reallife_vc_startup_no_diligence", "fee", TechText.Cents(_director.Level(VentureCatalogue.DiligenceFee))));
            bool followOn = put > 0 && s.Alive && day - s.LastRound <= VentureCatalogue.ProRataDays && VentureRules.FollowOnAmount(s) > 0;
            if (followOn)
            {
                box.Line(TechText.T("reallife_vc_startup_follow_on", "amount", TechText.Cents(_director.Level(VentureRules.FollowOnAmount(s)))));
            }
            Transform buttons = box.ButtonRow();
            if (s.Ask != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_vc_button_meet", () => Do(() => _director.MeetFounders(id)));
            }
            if (!s.Diligence && s.Alive)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_diligence", () => Do(() => _director.Diligence(id)));
            }
            if (followOn)
            {
                Transform follow = box.ButtonRow();
                InfoBox.Button(follow, InfoBox.Positive, "reallife_vc_button_follow_angel", () => Do(() => _director.FollowOn(id, false)));
                if (_director.FundOpen)
                {
                    InfoBox.Button(follow, InfoBox.Positive, "reallife_vc_button_follow_fund", () => Do(() => _director.FollowOn(id, true)));
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, StartupsGuide);
        }

        // ---------------------------------------------------------------- funding

        private TableSpec WriteFunding(InfoBox box, int day)
        {
            VentureFile v = _director.Ventures;
            if (v == null)
            {
                box.Title(TechText.T("reallife_roles_off_title"));
                box.Line(TechText.T("reallife_vc_off_body"));
                _picked = null;
                return TechColumns.Off;
            }
            IReadOnlyList<FirmPlace> firms = _director.FirmPlaces;
            FirmPlace picked = null;
            string id = Picked(_picked, CapRow);
            for (int i = 0; i < firms.Count; i++)
            {
                FirmPlace firm = firms[i];
                Cap c = v.CapOf(firm.File.Place);
                long profit = _director.FirmProfit(firm);
                long runway = profit < 0 ? VentureGame.Cash / -profit : -1;
                TableRow row = Row(CapRow + firm.File.Place);
                row.Set(0, firm.File.Name);
                row.Set(1, c != null && c.Stage >= 0 ? TechText.RoundName((RoundStage)c.Stage) : TechText.T("reallife_vc_bootstrapped"), c != null ? c.Stage : -1);
                int share = c != null ? VentureRules.PlayerShare(c) : 1000;
                row.Set(2, TechText.Mille(share), share);
                Cents(row, 3, c != null ? _director.Level(c.Post) : 0);
                long raised = 0;
                for (int h = 0; c != null && h < c.Holders.Count; h++)
                {
                    raised += c.Holders[h].Kind == (int)HolderKind.Vc ? c.Holders[h].Invested : 0;
                }
                Cents(row, 4, _director.Level(raised));
                row.Set(5, c != null ? c.VcSeats + " / " + c.BoardSeats : "-");
                Cents(row, 6, c != null && c.TargetMrr > 0 ? _director.Level(c.TargetMrr) : 0);
                row.Set(7, runway < 0 ? "-" : runway.ToString(), runway, runway >= 0 && runway < 60 ? TableRow.Red : TableRow.Plain);
                row.Set(8, c == null ? "-" : c.RaiseUntil > 0 ? TechText.T("reallife_vc_raising") : c.Pressure == 1 ? TechText.T("reallife_vc_warned") : c.Pressure == 2 ? TechText.T("reallife_vc_replaced") : TechText.T("reallife_vc_steady"),
                    c != null && c.Pressure > 0 ? TableRow.Yellow : TableRow.Plain);
                picked = firm.File.Place == id ? firm : picked;
            }
            if (picked != null)
            {
                if (_page == PageRaise)
                {
                    WriteRaise(box, picked, day);
                }
                else
                {
                    WriteCap(box, picked, day);
                }
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_vc_funding_title"));
                box.Line(TechText.Fill("reallife_vc_funding_about", "days", VentureCatalogue.RaiseDays.ToString(), "growth", TechText.Mille(VentureCatalogue.TargetGrowth), "target", VentureCatalogue.TargetDays.ToString()));
                Meta.Guide.Point(box, StartupsGuide);
            }
            return TechColumns.Funding;
        }

        private void WriteCap(InfoBox box, FirmPlace firm, int day)
        {
            string place = firm.File.Place;
            Cap c = _director.CapFor(place, false);
            box.Title(TechText.T("reallife_vc_cap_title", "firm", firm.File.Name));
            long profit = _director.FirmProfit(firm);
            box.Line(profit < 0 ? TechText.Fill("reallife_vc_cap_burn", "burn", TechText.Cents(-profit), "days", (VentureGame.Cash / -profit).ToString())
                : TechText.T("reallife_vc_cap_profit", "profit", TechText.Cents(profit)));
            if (c != null)
            {
                long total = System.Math.Max(1, c.Total);
                var words = new System.Text.StringBuilder(128);
                for (int i = 0; i < c.Holders.Count; i++)
                {
                    Holder h = c.Holders[i];
                    if (words.Length > 0)
                    {
                        words.Append(", ");
                    }
                    words.Append(h.Kind == (int)HolderKind.Player ? TechText.T("reallife_vc_holder_you") : h.Kind == (int)HolderKind.Pool ? TechText.T("reallife_vc_holder_pool")
                        : VentureCatalogue.Firms[TalentCatalogue.Clamp(h.Who, 0, VentureCatalogue.FirmCount - 1)].Name).Append(' ').Append(TechText.Mille(VentureRules.ShareOf(h, total)));
                }
                box.Line(TechText.Pair("reallife_vc_cap_holders", words.ToString()));
                box.Line(TechText.Fill("reallife_vc_cap_board", "seats", c.BoardSeats.ToString(), "theirs", c.VcSeats.ToString(), "target", TechText.Cents(_director.Level(c.TargetMrr)), "day", c.TargetDay.ToString(),
                    "now", TechText.Cents(_director.FirmMrr(place)), "misses", c.Misses.ToString()));
            }
            else
            {
                box.Line(TechText.T("reallife_vc_cap_all_yours"));
            }
            // the chief executive: the player, or one of the firm's professionals
            _chiefs.Clear();
            IReadOnlyList<Person> people = _director.People;
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i].File.Place == place && people[i].File.Level >= TalentCatalogue.Senior)
                {
                    _chiefs.Add(people[i]);
                }
            }
            string ceo = c != null ? c.Ceo : string.Empty;
            int at = 0;
            for (int i = 0; i < _chiefs.Count; i++)
            {
                at = _chiefs[i].File.Id == ceo ? i + 1 : at;
            }
            if (box.HasSelector && _chiefs.Count > 0)
            {
                box.Selector(_chiefs.Count + 1, at, i => TechText.T("reallife_vc_cap_chief", "name", i == 0 ? TechText.T("reallife_vc_holder_you") : _chiefs[i - 1].Name),
                    i => Do(() => _director.SetChief(place, i == 0 ? string.Empty : _chiefs[i - 1].File.Id)));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_vc_button_raise_page", () =>
            {
                _page = PageRaise;
                Refresh();
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_options", () => Do(() => _director.GrantOptions(place)));
            Transform more = box.ButtonRow();
            if (c != null && c.VcSeats > 0)
            {
                InfoBox.Button(more, InfoBox.Additional, "reallife_vc_button_sell", () => HudConfirm.Show("reallife_vc_confirm_sell_title", "reallife_vc_confirm_sell_body",
                    () => UiClone.Safe(() => Do(() => _director.SellShares(place))), null, null, null, true, true));
            }
            InfoBox.Button(more, InfoBox.Additional, "reallife_vc_button_listing", () => Do(() => _director.PrepareListing(place)));
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, StartupsGuide);
        }

        private void WriteRaise(InfoBox box, FirmPlace firm, int day)
        {
            string place = firm.File.Place;
            Cap c = _director.CapFor(place, false);
            box.Title(TechText.T("reallife_vc_raise_title", "firm", firm.File.Name));
            bool raising = c != null && c.RaiseUntil != 0;
            box.Line(raising ? TechText.Fill("reallife_vc_raise_on", "day", c.RaiseUntil.ToString(), "met", c.Meetings.ToString(), "most", VentureCatalogue.VcMeetingsMost.ToString())
                : TechText.Fill("reallife_vc_raise_about", "days", VentureCatalogue.RaiseDays.ToString(), "meetings", VentureCatalogue.VcMeetingsMost.ToString()));
            if (c != null && c.Bridge > 0)
            {
                box.Line(TechText.T("reallife_vc_bridge_owed", "amount", TechText.Cents(_director.Level(c.Bridge))));
            }
            Transform buttons = box.ButtonRow();
            if (!raising)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_vc_button_raise", () => Do(() => _director.StartRaise(place)));
            }
            else if (c.Meetings < VentureCatalogue.VcMeetingsMost)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_vc_button_meet_vc", () => Do(() => _director.MeetVc(place)));
            }
            if (c != null && c.VcSeats > 0 && c.Bridge == 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_vc_button_bridge", () => Do(() => _director.TakeBridge(place)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_roles_button_back", () =>
            {
                _page = PageCap;
                Refresh();
            });
            for (int i = 0; c != null && i < c.Sheets.Count; i++)
            {
                TermSheet t = c.Sheets[i];
                int index = i;
                box.Line(TechText.Fill("reallife_vc_sheet_line", "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(t.Vc, 0, VentureCatalogue.FirmCount - 1)].Name, "amount", TechText.Cents(_director.Level(t.Amount)),
                    "pre", TechText.Cents(_director.Level(t.Pre)), "share", TechText.Mille(t.EquityMille), "board", TechText.T(t.Board ? "reallife_vc_sheet_board" : "reallife_vc_sheet_no_board"), "day", t.Lapse.ToString()));
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_vc_button_take_sheet", () => HudConfirm.Show("reallife_vc_confirm_sheet_title", "reallife_vc_confirm_sheet_body",
                    () => UiClone.Safe(() => Do(() => _director.TakeSheet(place, index))), null, null, null, true, false));
            }
            Meta.Guide.Point(box, StartupsGuide);
        }
    }
}
