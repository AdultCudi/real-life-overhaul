using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The Investors tab. Its first page lists what is open: offers, money promised, deals. "Raise money" leads through
    /// the pages of a pitch: the business the money is for (or one not yet open: its kind and what it will cost), then
    /// the investor, then the meeting, which the director holds in the game's list window with the phone put away.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private const int PageList = 0, PageCounter = 1, PageBusiness = 2, PageType = 3, PageWho = 4;
        private const string OfferRow = "offer:", DealRow = "deal:", PledgeRow = "pledge:", BusinessRow = "biz:", NewRow = "new:", TypeRow = "type:", WhoRow = "inv:";

        private int _page;
        private string _raisePlace;    // the business the money is for; null for one not yet open
        private string _raiseType;     // ...whose kind this is
        private int _budget = 3;       // ...and whose budget is this step of the catalogue's
        private int _who;              // the investor picked to approach

        private TableSpec WriteInvestors(InfoBox box, VenturesFile file, int day)
        {
            UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_head_investors", "deals", file.Deals.Count.ToString(), "name", file.Reputation.ToString()));
            switch (_page)
            {
                case PageBusiness: return WriteRaiseBusiness(box, file, day);
                case PageType: return WriteRaiseType(box, file);
                case PageWho: return WriteRaiseWho(box, file, day);
            }
            for (int i = 0; i < file.Offers.Count; i++)
            {
                Offer o = file.Offers[i];
                TableRow row = MoneyRow(file, OfferRow, o.Id, o.InvestorId, VenturesTables.IsOffer,
                    o.ForNew ? FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(o.TypeName)) : VenturesText.Business(o.Place, o.Business), o.Amount, o.ShareBp);
                row.NoTotal = true;   // (offered, not yet put in)
                row.Set(6, FinanceText.T("reallife_ven_row_answer_by", "day", o.LapseDay.ToString()), TableRow.Yellow);
            }
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                Pledge p = file.Pledges[i];
                TableRow row = MoneyRow(file, PledgeRow, p.Id, p.InvestorId, VenturesTables.IsPledge, FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(p.TypeName)), p.Amount, p.ShareBp);
                row.NoTotal = true;   // (promised for a business not yet open)
                row.Set(6, FinanceText.T("reallife_ven_row_open_by", "day", p.OpenBy.ToString()));
            }
            for (int i = 0; i < file.Deals.Count; i++)
            {
                Deal d = file.Deals[i];
                bool called = d.Stage == DealStage.Called;
                TableRow row = MoneyRow(file, DealRow, d.Id, d.InvestorId, called ? VenturesTables.IsCalled : VenturesTables.IsDeal, VenturesText.Business(d.Place, d.Business), d.Amount,
                    called ? 0 : d.ShareNow(day));
                row.Set(5, Money(d.PaidOut), d.PaidOut);
                row.Set(6, called ? FinanceText.T("reallife_ven_row_owed", "amount", Money(d.CallOwed)) : DealState(d),
                    called || d.Stage == DealStage.Demand ? TableRow.Red : d.Misses > 0 ? TableRow.Yellow : TableRow.Plain);
            }
            if (_rows.Count == 0)
            {
                Say(FinanceText.T("reallife_ven_row_nothing"));
            }
            Offer offer = file.Offer(Picked(_picked, OfferRow));
            Deal deal = file.Deal(Picked(_picked, DealRow));
            Pledge pledge = null;
            for (int i = 0; i < file.Pledges.Count; i++)
            {
                pledge = file.Pledges[i].Id == Picked(_picked, PledgeRow) ? file.Pledges[i] : pledge;
            }
            if (_page == PageCounter && (offer == null || offer.Countered))
            {
                _page = PageList;   // (the counter's page is only ever an offer's that has not been countered)
            }
            if (offer != null && _page == PageCounter)
            {
                WriteCounter(box, file, offer);
            }
            else if (offer != null)
            {
                WriteOffer(box, file, offer);
            }
            else if (deal != null)
            {
                WriteDeal(box, file, deal, day);
            }
            else if (pledge != null)
            {
                WritePledge(box, pledge);
            }
            else
            {
                _picked = null;
                _page = PageList;
                box.Title(FinanceText.T("reallife_ven_tab_investors"));
                box.Section("reallife_look_sec_now");
                box.Row("reallife_ven_inv_name", FinanceText.T("reallife_ven_of_hundred", "value", file.Reputation.ToString()));
                box.Row("reallife_ven_inv_in", Money(file.InvestedTotal));
                box.Row("reallife_ven_inv_out", Money(file.PaidOutTotal));
                if (Settings.VenturesInvestors)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_raise", () => GoTo(PageBusiness));
                    box.Note(FinanceText.T("reallife_ven_inv_raise_note"));
                }
                else
                {
                    box.Line(FinanceText.T("reallife_ven_inv_off"), CellTone.Yellow);
                }
                box.Note(FinanceText.T("reallife_ven_inv_how", "days", VenturesCatalogue.ProfitDaysNeeded.ToString(), "of", VenturesCatalogue.ProfitWindowDays.ToString(),
                    "profit", Money(VenturesCatalogue.OfferProfitFloor)));
                Meta.Guide.Point(box, GuideTitle);
            }
            return VenturesTables.Investors;
        }

        // The cells every row of the list has: who, what kind of investor, for which business, how much and for what share.
        private TableRow MoneyRow(VenturesFile file, string kind, int id, int investorId, int stage, string business, int amount, int shareBp)
        {
            Investor investor = file.Investor(investorId);
            TableRow row = Row(kind, id);
            row.Kind = investor != null ? (int)investor.Kind : -1;
            row.Kind2 = stage;
            row.Set(0, _director.NameOf(investorId));
            row.Set(1, investor != null ? VenturesText.Kind(investor) : string.Empty);
            row.Set(2, business);
            row.Set(3, Money(amount), amount);
            if (shareBp > 0)
            {
                row.Set(4, VenturesText.Share(shareBp), shareBp / 100.0);
            }
            return row;
        }

        private static string DealState(Deal d)
        {
            switch (d.Stage)
            {
                case DealStage.Called: return FinanceText.T("reallife_ven_state_called");
                case DealStage.Demand: return FinanceText.T("reallife_ven_state_demand");
                case DealStage.Cold: return FinanceText.T("reallife_ven_state_cold");
                case DealStage.Quiet: return FinanceText.T("reallife_ven_state_quiet");
                default: return d.Misses > 0 ? FinanceText.T("reallife_ven_state_warned") : VenturesText.Mood(d);
            }
        }

        private void GoTo(int page)
        {
            _page = page;
            if (page == PageList || page == PageBusiness)
            {
                _picked = null;
            }
            _table.ClearSelection();
            Refresh();
        }

        // A click on a row of this tab. True if it was dealt with here (a page of the pitch).
        private bool InvestorRowClicked(TableRow row)
        {
            switch (_page)
            {
                case PageBusiness:
                    if (row.Id.StartsWith(NewRow, System.StringComparison.Ordinal))
                    {
                        _raisePlace = null;
                        _raiseType = null;
                        GoTo(PageType);
                        return true;
                    }
                    _businesses.Clear();
                    Books.Businesses(_businesses);
                    if (row.Index >= 0 && row.Index < _businesses.Count)
                    {
                        _raisePlace = Books.Key(_businesses[row.Index].Address);
                        _raiseType = null;
                        _who = 0;
                        _businesses.Clear();
                        GoTo(PageWho);
                    }
                    _businesses.Clear();
                    return true;
                case PageType:
                    _raiseType = row.Index >= 0 && row.Index < _types.Count ? _types[row.Index] : null;
                    _picked = row.Id;
                    Refresh();
                    return true;
                case PageWho:
                    _who = row.Index;
                    _picked = row.Id;
                    Refresh();
                    return true;
                default:
                    _page = PageList;
                    return false;
            }
        }

        // ---------------------------------------------------------------- an offer, a promise, a deal

        private void WriteOffer(InfoBox box, VenturesFile file, Offer offer)
        {
            Investor investor = file.Investor(offer.InvestorId);
            box.Title(investor.Name);
            box.Section("reallife_look_sec_now");
            box.Row("reallife_ven_offer_by", FinanceText.Day(offer.LapseDay));
            if (offer.Countered)
            {
                box.Line(FinanceText.T("reallife_ven_offer_countered"));
            }
            box.Row("reallife_ven_offer_for", offer.ForNew ? FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(offer.TypeName))
                : VenturesText.Business(offer.Place, offer.Business));
            box.Row("reallife_ven_offer_amount", Money(offer.Amount));
            box.Row("reallife_ven_offer_share", FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(offer.ShareBp)));
            box.Line(FinanceText.Fill(offer.ForNew ? "reallife_ven_offer_expect_new" : "reallife_ven_offer_expect", "growth", offer.TargetPct.ToString(), "days", offer.TargetDays.ToString(),
                "open", VenturesCatalogue.PledgeDays.ToString(), "target", Money(OfferRules.YoungTarget)));
            box.Line(FinanceText.T("reallife_ven_who_line", "kind", VenturesText.Kind(investor), "temper", VenturesText.Temper(investor)));
            int id = offer.Id;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_accept", () => AcceptOffer(id));
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_ven_refuse", () =>
            {
                _director.Refuse(id);
                ClosePick();
            });
            if (!offer.Countered)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_counter", () => GoTo(PageCounter));
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // The game's own question box: what is agreed is said before it is agreed.
        private void AcceptOffer(int id)
        {
            VenturesFile file = _director.File;
            Offer offer = file != null ? file.Offer(id) : null;
            if (offer == null)
            {
                ClosePick();
                return;
            }
            string name = _director.NameOf(offer.InvestorId);
            GameConfirm.Show("reallife_ven_confirm_accept_title".Localize(new { name }),
                (offer.ForNew ? "reallife_ven_confirm_accept_new" : "reallife_ven_confirm_accept").Localize(new
                {
                    name,
                    amount = Money(offer.Amount),
                    share = VenturesText.Share(offer.ShareBp),
                    business = offer.ForNew ? VenturesGame.TypeName(offer.TypeName) : VenturesText.Business(offer.Place, offer.Business),
                    growth = offer.TargetPct.ToString(),
                    days = offer.TargetDays.ToString(),
                    open = VenturesCatalogue.PledgeDays.ToString()
                }),
                () => UiClone.Safe(() =>
                {
                    _director.Accept(id);
                    AfterChange();
                }), null, "reallife_ven_accept", "common_cancel", false, false);
        }

        // Something was agreed or paid: the page is written again if the app is still showing.
        private void AfterChange()
        {
            if (Showing)
            {
                _ready = _director.Running;
                _picked = null;
                _page = PageList;
                Refresh();
            }
        }

        private void WriteCounter(InfoBox box, VenturesFile file, Offer offer)
        {
            Investor investor = file.Investor(offer.InvestorId);
            box.Title(FinanceText.T("reallife_ven_counter_title", "name", investor.Name));
            box.Line(FinanceText.T("reallife_ven_counter_now", "amount", Money(offer.Amount), "share", VenturesText.Share(offer.ShareBp)));
            box.Note(FinanceText.T("reallife_ven_counter_how", "money", VenturesCatalogue.CounterMoneyPct.ToString(), "share", VenturesCatalogue.CounterSharePct.ToString()));
            box.Line(FinanceText.T("reallife_ven_counter_once"));
            int id = offer.Id;
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_counter_money", () => Counter(id, true));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_counter_share", () => Counter(id, false));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(PageList));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Counter(int id, bool moreMoney)
        {
            _director.Counter(id, moreMoney);
            _page = PageList;
            Refresh();
        }

        private void WritePledge(InfoBox box, Pledge pledge)
        {
            box.Title(_director.NameOf(pledge.InvestorId));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_ven_pledge_for", VenturesGame.TypeName(pledge.TypeName));
            box.Row("reallife_ven_offer_amount", Money(pledge.Amount));
            box.Row("reallife_ven_offer_share", FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(pledge.ShareBp)));
            box.Note(FinanceText.T("reallife_ven_pledge_how", "day", pledge.OpenBy.ToString()));
            int id = pledge.Id;
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_ven_pledge_drop", () =>
            {
                _director.GiveUp(id);
                ClosePick();
            });
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteDeal(InfoBox box, VenturesFile file, Deal deal, int day)
        {
            Investor investor = file.Investor(deal.InvestorId);
            BuildingRegistration r = VenturesGame.Business(deal.Place);
            BusinessFigures b = VenturesGame.Figures(r);
            box.Title(investor.Name);
            int id = deal.Id;
            int price = _director.BuyOutPrice(deal);
            if (deal.Stage == DealStage.Called)
            {
                box.Line(FinanceText.T("reallife_ven_deal_called", "amount", Money(deal.CallOwed), "per_day", Money(deal.CallPerDay)));
                if (deal.CallMisses > 0)
                {
                    box.Line(FinanceText.T("reallife_ven_deal_call_missed", "count", deal.CallMisses.ToString(), "most", VenturesCatalogue.CallMissesMost.ToString()));
                }
                box.Row("reallife_ven_deal_settle", Money(price));
            }
            box.Row("reallife_ven_offer_for", VenturesText.Business(deal.Place, deal.Business));
            box.Row("reallife_ven_deal_in", Money(deal.Amount));
            box.Note(FinanceText.T("reallife_tab_ven_deal_since", "day", deal.StartDay.ToString()));
            box.Row("reallife_ven_deal_out", Money(deal.PaidOut));
            if (deal.Stage == DealStage.Called)
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_pay_off", () => BuyOut(id));
                box.Line(FinanceText.T("reallife_ven_who_line", "kind", VenturesText.Kind(investor), "temper", VenturesText.Temper(investor)));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            box.Row("reallife_ven_offer_share", deal.BoardUntil > day
                ? FinanceText.T("reallife_ven_deal_share_seat", "share", VenturesText.Share(deal.ShareNow(day)), "day", deal.BoardUntil.ToString())
                : FinanceText.T("reallife_ven_share_of_profit", "share", VenturesText.Share(deal.ShareBp)));
            if (deal.Arrears > 0)
            {
                box.Row("reallife_ven_deal_arrears", Money(deal.Arrears));
            }
            if (deal.TargetDay > 0)
            {
                box.Line(FinanceText.T("reallife_ven_deal_target", "target", Money(deal.TargetProfit), "day", deal.TargetDay.ToString(), "now", Money(b.AvgProfit7)));
            }
            if (deal.CostDay > 0)
            {
                box.Line(FinanceText.T("reallife_ven_deal_promise", "target", Money(deal.CostTarget), "day", deal.CostDay.ToString(), "now", Money(b.AvgCost14)));
            }
            box.Row("reallife_ven_deal_mood", DealState(deal) == VenturesText.Mood(deal) ? VenturesText.Mood(deal)
                : FinanceText.T("reallife_ven_deal_mood_state", "mood", VenturesText.Mood(deal), "state", DealState(deal)));
            if (deal.Stage == DealStage.Demand)
            {
                box.Line(FinanceText.Fill(investor.Kind == InvestorKind.Firm ? "reallife_ven_deal_demand_firm" : "reallife_ven_deal_demand", "day", deal.DemandDay.ToString(), "extra", VenturesText.Share(VenturesCatalogue.BoardExtraBp),
                    "seat_days", VenturesCatalogue.BoardDays.ToString(), "cut", VenturesCatalogue.CostCutPct.ToString(), "cut_days", VenturesCatalogue.CostCutDays.ToString()));
                Transform answers = box.ButtonRow();
                if (investor.Kind != InvestorKind.Firm)
                {
                    InfoBox.Button(answers, InfoBox.Positive, "reallife_ven_demand_seat", () => Answer(id, true));
                }
                InfoBox.Button(answers, InfoBox.Additional, "reallife_ven_demand_costs", () => Answer(id, false));
            }
            box.Row("reallife_ven_deal_buy_out", Money(price));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ven_buy_out", () => BuyOut(id));
            box.Line(FinanceText.T("reallife_ven_who_line", "kind", VenturesText.Kind(investor), "temper", VenturesText.Temper(investor)));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Answer(int id, bool board)
        {
            _director.AnswerDemand(id, board);
            Refresh();
        }

        private void BuyOut(int id)
        {
            VenturesFile file = _director.File;
            Deal deal = file != null ? file.Deal(id) : null;
            if (deal == null)
            {
                ClosePick();
                return;
            }
            string name = _director.NameOf(deal.InvestorId);
            GameConfirm.Show("reallife_ven_confirm_buy_out_title".Localize(new { name }),
                (deal.Stage == DealStage.Called ? "reallife_ven_confirm_pay_off" : "reallife_ven_confirm_buy_out").Localize(new
                {
                    name,
                    amount = Money(_director.BuyOutPrice(deal)),
                    business = VenturesText.Business(deal.Place, deal.Business),
                    share = VenturesText.Share(deal.ShareBp)
                }),
                () => UiClone.Safe(() =>
                {
                    _director.BuyOut(id);
                    AfterChange();
                }), null, deal.Stage == DealStage.Called ? "reallife_ven_pay_off" : "reallife_ven_buy_out", "common_cancel", false, false);
        }

        // ---------------------------------------------------------------- raising money: the business, its kind, the investor

        private TableSpec WriteRaiseBusiness(InfoBox box, VenturesFile file, int day)
        {
            _businesses.Clear();
            Books.Businesses(_businesses);
            for (int i = 0; i < _businesses.Count; i++)
            {
                BuildingRegistration r = _businesses[i];
                BusinessFigures b = VenturesGame.Figures(r);
                int deals;
                int promised = file.ShareOn(Books.Key(r.Address), day, out deals);
                TableRow row = Row(BusinessRow, i);
                row.Set(0, VenturesGame.Name(r));
                row.Set(1, FinanceText.T("reallife_ven_row_a_day", "amount", Money(b.AvgProfit7)), b.AvgProfit7);
                row.Set(2, FinanceText.T("reallife_ven_row_days", "days", b.AgeDays.ToString()), b.AgeDays);
                row.Set(3, promised > 0 ? VenturesText.Share(promised) : string.Empty, promised / 100.0, b.AvgProfit7 <= 0f ? TableRow.Red : TableRow.Plain);
            }
            _businesses.Clear();
            TableRow fresh = Row(NewRow, 0);
            fresh.Pinned = true;   // (a way on, not a business: it stays at the head of the list)
            fresh.A = FinanceText.T("reallife_ven_row_new_business");
            fresh.B = FinanceText.T("reallife_ven_row_new_business_note");
            box.Title(FinanceText.T("reallife_ven_raise"));
            box.Note(FinanceText.T("reallife_ven_raise_pick"));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(PageList));
            box.Note(FinanceText.T("reallife_ven_raise_how", "least", VenturesCatalogue.Questions[0].ToString(), "most", VenturesCatalogue.Questions[2].ToString()));
            box.Line(FinanceText.T("reallife_ven_raise_truth", "days", VenturesCatalogue.CaughtCloseDays.ToString()));
            box.Line(FinanceText.T("reallife_ven_raise_again", "days", VenturesCatalogue.PitchCooldownDays.ToString()));
            Meta.Guide.Point(box, GuideTitle);
            return VenturesTables.RaiseBusiness;
        }

        private TableSpec WriteRaiseType(InfoBox box, VenturesFile file)
        {
            VenturesGame.StartableTypes(_types);
            for (int i = 0; i < _types.Count; i++)
            {
                Sector sector = VenturesCatalogue.SectorOf(_types[i]);
                int liked = 0;
                for (int v = 0; v < file.Investors.Count; v++)
                {
                    liked += file.Investors[v].LikesSector(sector) ? 1 : 0;
                }
                TableRow row = Row(TypeRow, i);
                row.Kind = (int)sector;
                row.Set(0, VenturesGame.TypeName(_types[i]));
                row.Set(1, VenturesText.SectorName(sector));
                row.Set(2, FinanceText.T("reallife_ven_row_investors", "count", liked.ToString()), liked);
                int run = VenturesGame.CountOf(_types[i]);
                row.Set(3, run > 0 ? run.ToString() : string.Empty, run);
                if (_types[i] == _raiseType)
                {
                    _picked = row.Id;
                }
            }
            if (_types.Count == 0)
            {
                Say(FinanceText.T("reallife_ven_row_no_types"));
            }
            box.Title(FinanceText.T("reallife_ven_row_new_business"));
            if (_raiseType != null)
            {
                box.Row("reallife_ven_new_kind", VenturesGame.TypeName(_raiseType));
            }
            else
            {
                box.Note(FinanceText.T("reallife_ven_new_pick"));
            }
            box.Headline("reallife_ven_new_budget");
            int[] steps = VenturesCatalogue.BudgetSteps;
            _budget = Mathf.Clamp(_budget, 0, steps.Length - 1);
            if (!box.HasSelector || !box.Selector(steps.Length, _budget, i => Money(steps[i]), i =>
                {
                    _budget = i;
                    Refresh();
                }))
            {
                box.Line(Money(steps[_budget]));
            }
            box.Note(FinanceText.T("reallife_ven_new_how", "least", VenturesCatalogue.BudgetShareLeastPct.ToString(), "most", VenturesCatalogue.BudgetShareMostPct.ToString(),
                "days", VenturesCatalogue.PledgeDays.ToString()));
            Transform buttons = box.ButtonRow();
            if (_raiseType != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_new_next", () =>
                {
                    _who = 0;
                    GoTo(PageWho);
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_back", () => GoTo(PageBusiness));
            Meta.Guide.Point(box, GuideTitle);
            return VenturesTables.RaiseType;
        }

        private TableSpec WriteRaiseWho(InfoBox box, VenturesFile file, int day)
        {
            BuildingRegistration r = VenturesGame.Business(_raisePlace);
            if (r == null && _raiseType == null)
            {
                _page = PageBusiness;
                return WriteRaiseBusiness(box, file, day);
            }
            string about = r != null ? VenturesGame.Name(r) : FinanceText.T("reallife_ven_row_new", "type", VenturesGame.TypeName(_raiseType));
            Sector sector = VenturesCatalogue.SectorOf(r != null ? r.businessTypeName : _raiseType);
            Investor picked = null;
            for (int i = 0; i < file.Investors.Count; i++)
            {
                Investor v = file.Investors[i];
                TableRow row = Row(WhoRow, v.Id);
                row.Kind = (int)v.Kind;
                row.Set(0, v.Name);
                row.Set(1, VenturesText.Kind(v));
                row.Set(2, VenturesText.Likes(v));
                row.Set(3, FinanceText.T("reallife_ven_row_range", "least", Money(v.ChequeLeast), "most", Money(v.ChequeMost)), v.ChequeMost);
                bool backs = r != null || v.Kind != InvestorKind.Tycoon;   // (a tycoon backs no business that is not open yet)
                bool open = backs && v.Open(day);
                // (ordered by the day they would see the player: now first, a day after that, "not again" and "open businesses only" last)
                row.Set(4, !backs ? FinanceText.T("reallife_ven_row_open_only") : v.Open(day) ? FinanceText.T("reallife_ven_row_open_now")
                    : VenturesText.Never(v, day) ? FinanceText.T("reallife_ven_row_never") : FinanceText.Day(v.OpenDay),
                    open ? 0 : backs && !VenturesText.Never(v, day) ? v.OpenDay : double.NaN, open ? (v.LikesSector(sector) ? TableRow.Yellow : TableRow.Plain) : TableRow.Red);
                if (v.Id == _who)
                {
                    picked = v;
                    _picked = row.Id;
                }
            }
            if (picked == null)
            {
                _picked = null;
                box.Title(FinanceText.T("reallife_ven_who_title"));
                box.Section("reallife_look_sec_now");
                box.Row("reallife_ven_offer_for", about);
                box.Note(FinanceText.T("reallife_ven_who_pick", "sector", VenturesText.SectorName(sector)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ven_back", () => GoTo(r != null ? PageBusiness : PageType));
                box.Line(FinanceText.T("reallife_ven_who_kinds"));
                Meta.Guide.Point(box, GuideTitle);
                return VenturesTables.RaiseWho;
            }
            int k = (int)picked.Kind;
            box.Title(picked.Name);
            if (r == null && picked.Kind == InvestorKind.Tycoon)
            {
                box.Line(FinanceText.T("reallife_ven_who_not_open"));
            }
            else if (!picked.Open(day))
            {
                box.Line(VenturesText.Never(picked, day) ? FinanceText.T("reallife_ven_who_never") : FinanceText.T("reallife_ven_who_closed", "day", picked.OpenDay.ToString()));
            }
            box.Row("reallife_ven_who_temper", VenturesText.Temper(picked));
            box.Row("reallife_ven_who_likes", VenturesText.Likes(picked));
            box.Row("reallife_ven_who_sums", FinanceText.T("reallife_ven_row_range", "least", Money(picked.ChequeLeast), "most", Money(picked.ChequeMost)));
            box.Row("reallife_ven_who_share", FinanceText.T("reallife_ven_row_range", "least", VenturesText.Share(VenturesCatalogue.ShareLeastBp[k]), "most", VenturesText.Share(VenturesCatalogue.ShareMostBp[k])));
            box.Row("reallife_ven_who_checks", FinanceText.T(picked.Diligence < 50 ? "reallife_ven_checks_lightly" : picked.Diligence < 78 ? "reallife_ven_checks_carefully" : "reallife_ven_checks_closely"));
            box.Row("reallife_ven_who_questions", VenturesCatalogue.Questions[k].ToString());
            box.Row("reallife_ven_offer_for", about);
            bool meets = picked.Open(day) && (r != null || picked.Kind != InvestorKind.Tycoon);
            Transform buttons = box.ButtonRow();
            if (meets)
            {
                int id = picked.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_who_meet", () => Meet(id));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ven_back", () => GoTo(r != null ? PageBusiness : PageType));
            box.Line(FinanceText.T(k == 0 ? "reallife_ven_who_angel" : k == 1 ? "reallife_ven_who_tycoon" : "reallife_ven_who_firm"));
            Meta.Guide.Point(box, GuideTitle);
            return VenturesTables.RaiseWho;
        }

        // The meeting is held in the game's list window, with the phone put away: the director sees to both.
        private void Meet(int investorId)
        {
            string place = _raisePlace, type = _raiseType;
            int budget = VenturesCatalogue.BudgetSteps[Mathf.Clamp(_budget, 0, VenturesCatalogue.BudgetSteps.Length - 1)];
            _page = PageList;
            _picked = null;
            if (!_director.StartPitch(investorId, place, place != null ? null : type, budget) && Showing)
            {
                _ready = _director.Running;
                Refresh();
            }
        }
    }
}
