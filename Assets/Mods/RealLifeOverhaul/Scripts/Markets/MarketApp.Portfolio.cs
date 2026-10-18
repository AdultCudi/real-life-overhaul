using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the Stocks app: the Portfolio tab, every holding a row with what it is, its worth, gain, income and next
    /// payment. The box has three pages so it keeps its foot: what is held, what it pays, and the account. A bond's row leads
    /// to it on the Bonds tab, where bonds are managed; a company's to its page. Figures from InvestRules.
    /// </summary>
    public sealed partial class MarketApp
    {
        private const string PortfolioBondRow = "pb:";
        private const int PaymentsInBox = 3;   // the table has each holding's own
        private const int PageHeld = 0, PagePays = 1, PageAccount = 2;
        private static readonly string[] PortfolioPages = { "reallife_mkt_inv_page_held", "reallife_mkt_inv_page_pays", "reallife_mkt_inv_page_account" };

        private readonly List<PortfolioRow> _holdings = new List<PortfolioRow>(32);
        private readonly List<ComingPayment> _payments = new List<ComingPayment>(PaymentsInBox + 1);
        private readonly PortfolioSums _sums = new PortfolioSums();
        private int _portfolioPage;

        private TableSpec WritePortfolio(InfoBox box, MarketState s)
        {
            InvestRules.Holdings(s, s.Phase, _holdings, Settings.MarketsBonds);
            for (int i = 0; i < _holdings.Count; i++)
            {
                WriteHolding(s, _holdings[i]);
            }
            InvestRules.Sums(_holdings, _sums);
            WritePortfolioBox(box, s);
            return MarketTables.Portfolio;
        }

        // A holding's row: the player's, a bot's or the founder's shares, or a bond.
        private void WriteHolding(MarketState s, in PortfolioRow h)
        {
            Stock k = h.Stock > 0 ? s.Stock(h.Stock) : null;
            BondHolding bond = h.Bond > 0 ? s.Bonds.Holding(h.Bond) : null;
            TableRow row = h.Founder ? Row(CompanyRow, h.Stock) : bond != null ? Row(PortfolioBondRow, h.Bond)
                : h.Firm > 0 ? Row(BotHoldingRow, h.Firm * BotHoldingIds + h.Stock) : Row(HoldingRow, h.Stock);
            row.Kind = k != null ? (int)k.Sector : -1;
            row.Kind2 = (int)h.Kind;
            row.Pinned = h.Founder;   // the founder's stake first, in no total: it is their company, not the account's
            row.NoTotal = h.Founder;
            row.Set(0, HoldingName(h, k, bond));
            row.Set(1, InvestText.Kind(h.Kind), (int)h.Kind);
            if (bond != null)
            {
                row.Set(2, "-", double.NaN);
                row.Set(3, h.Kind == HoldingKind.Deposit ? "-" : BondText.Price(h.Price / 100.0), double.NaN);
            }
            else
            {
                row.Set(2, Count(h.Shares), h.Shares);
                row.Set(3, MarketText.Price(h.Price), h.Price / 100.0);
            }
            row.Set(5, MarketText.Money(h.Worth), h.Worth);
            if (h.Founder)
            {
                row.Set(4, "-", double.NaN);
                row.Set(6, "-", double.NaN);
                row.Set(7, "-", double.NaN);
                return;
            }
            row.Set(4, MarketText.Money(h.Cost), h.Cost);
            row.Set(6, MarketText.Money(h.Gain), h.Gain, Tone(Math.Sign(h.Gain)));
            row.Set(7, h.Firm > 0 ? "-" : MarketText.Money(h.Paid), h.Firm > 0 ? double.NaN : h.Paid);   // a bot's dividends are on the Bots tab
            row.Set(8, PaymentWords(h.Next), h.Next.Day > 0 ? h.Next.Day : double.NaN);
        }

        // A holding's name with its marks: the founder's, a bot's firm, a test's, the share put back, a bond that rolls over.
        private static string HoldingName(in PortfolioRow h, Stock k, BondHolding bond)
        {
            string test = h.Test ? InvestText.Mark(MarketText.T("reallife_mkt_test_mark")) : string.Empty;
            if (bond != null)
            {
                return BondText.Name(bond) + (h.Rollover ? InvestText.Mark(MarketText.T("reallife_mkt_inv_mark_rolls")) : string.Empty) + test;
            }
            string name = k != null ? k.Name : string.Empty;
            if (h.Founder)
            {
                return name + InvestText.Mark(MarketText.T("reallife_lst_yours"));
            }
            if (h.Firm > 0)
            {
                BotFirm f = MarketBots.Firm(h.Firm);
                return name + InvestText.Mark(f != null ? f.Name : string.Empty);
            }
            return name + test + (h.Reinvest > 0 ? InvestText.Mark(MarketText.Fill("reallife_mkt_inv_mark_back", "pct", h.Reinvest.ToString())) : string.Empty);
        }

        // A payment to come in words: "120, day 34", owed on a short "-120, day 34", a bond's last with its end or rollover.
        private static string PaymentWords(in ComingPayment p)
        {
            if (p.Day <= 0 || p.Cents == 0)
            {
                return string.Empty;
            }
            return MarketText.Fill(p.Ends ? "reallife_mkt_inv_next_end" : p.Rolls ? "reallife_mkt_inv_next_roll" : "reallife_mkt_next_pay",
                "amount", (p.Cents < 0 ? "-" : string.Empty) + BondText.Money(Math.Abs(p.Cents)), "day", p.Day.ToString());
        }

        // What a payment to come is from: the company, with a bot's firm, or the bond.
        private static string PaymentName(MarketState s, in ComingPayment p)
        {
            if (p.Bond > 0)
            {
                return BondText.Name(s.Bonds.Holding(p.Bond));
            }
            Stock k = s.Stock(p.Stock);
            BotFirm f = p.Firm > 0 ? MarketBots.Firm(p.Firm) : null;
            return (k != null ? k.Name : string.Empty) + (f != null ? InvestText.Mark(f.Name) : string.Empty);
        }

        private void WritePortfolioBox(InfoBox box, MarketState s)
        {
            Account a = s.Account;
            box.Title(MarketText.T("reallife_mkt_inv_title"));
            int pct = _sums.NetCost != 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, _sums.Gain * 10000L / Math.Abs(_sums.NetCost))) : 0;
            if (a.Margin && a.CallDay > 0)
            {
                box.Chip(MarketText.T("reallife_ibox_chip_margin_call"), CellTone.Red);
            }
            else if (_sums.NetCost != 0)
            {
                box.Chip(MarketText.Change(pct), pct > 0 ? CellTone.Green : pct < 0 ? CellTone.Red : CellTone.Plain);
            }
            StatusLine(box);
            _portfolioPage = Mathf.Clamp(_portfolioPage, 0, PortfolioPages.Length - 1);
            box.Selector(PortfolioPages.Length, _portfolioPage, i => MarketText.T(PortfolioPages[i]), i => UiClone.Safe(() =>
            {
                _portfolioPage = i;
                Refresh();
            }));
            switch (_portfolioPage)
            {
                case PagePays:
                    WritePaysPage(box, s);
                    break;
                case PageAccount:
                    WriteAccountPage(box, s);
                    break;
                default:
                    WriteHeldPage(box, s);
                    break;
            }
            OwnButton(box);
            Meta.Guide.Point(box, GuideTitle);
        }

        // What is held: worth, cost and gain, the spread across kinds, the cash and worked orders' money of the day.
        private void WriteHeldPage(InfoBox box, MarketState s)
        {
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ibox_worth", MarketText.Money(_sums.Net));
            box.Row("reallife_ibox_cost", MarketText.Money(_sums.NetCost));
            box.Row("reallife_ibox_gain", MarketText.Money(_sums.Gain), _sums.Gain > 0 ? CellTone.Green : _sums.Gain < 0 ? CellTone.Red : CellTone.Plain);
            long day = MarketTrading.DayChange(s, out long dayBefore);   // today against the previous close, in cents
            if (dayBefore > 0)
            {
                box.Row("reallife_mkt_day_change",
                    MarketText.Fill("reallife_mkt_day_change_value", "money", MarketText.Money(day / 100), "percent", MarketText.Change(MarketTrading.DayChangeBp(day, dayBefore))),
                    day > 0 ? CellTone.Green : day < 0 ? CellTone.Red : CellTone.Plain);
            }
            box.Section("reallife_mkt_inv_sec_kinds");
            bool any = false;
            for (int k = 0; k < (int)HoldingKind.Count; k++)
            {
                if (_sums.Count[k] == 0)
                {
                    continue;
                }
                any = true;
                int bp = _sums.ShareBp((HoldingKind)k);
                box.MeterWords(InvestText.Kind((HoldingKind)k), bp / 10000f,
                    MarketText.Fill("reallife_mkt_inv_share", "worth", MarketText.Money(_sums.Worth[k]), "share", MarketText.Percent(bp)), CellTone.Plain);
            }
            if (!any)
            {
                box.Note(MarketText.T("reallife_mkt_inv_none"));
            }
            if (_sums.Shorts > 0)
            {
                box.Row("reallife_mkt_inv_shorts", MarketText.Money(_sums.Shorts), CellTone.Red);
            }
            if (_sums.Founder > 0)
            {
                box.Row("reallife_mkt_inv_founder", MarketText.Money(_sums.Founder));
            }
            box.Row("reallife_mkt_inv_cash", MarketText.Money(_director.Cash));
            InvestRules.WithBroker(s, out long parked, out long due);   // worked orders' money of the day, out of the cash until the close
            if (parked > 0)
            {
                box.Row("reallife_mkt_inv_held_money", MarketText.Money(parked));
            }
            if (due > 0)
            {
                box.Row("reallife_mkt_inv_due_money", MarketText.Money(due));
            }
            if (s.Account.Margin)
            {
                box.Row("reallife_mkt_inv_margin_cash", MarketText.Money(s.Account.Cash), s.Account.Cash < 0 ? CellTone.Red : CellTone.Plain);
            }
        }

        // What it pays: the next year's income by kind before tax, its yield, and the next payments.
        private void WritePaysPage(InfoBox box, MarketState s)
        {
            box.Section("reallife_mkt_inv_sec_year");
            bool any = false;
            for (int k = 0; k < (int)HoldingKind.Count; k++)
            {
                if (_sums.YearCents[k] > 0)
                {
                    any = true;
                    box.RowWords(InvestText.Income((HoldingKind)k), BondText.Money(_sums.YearCents[k]));
                }
            }
            if (_sums.OwedCents > 0)
            {
                box.Row("reallife_mkt_inv_income_owed", "-" + BondText.Money(_sums.OwedCents), CellTone.Red);
            }
            if (any || _sums.OwedCents > 0)
            {
                box.Row("reallife_mkt_inv_income_all", BondText.Money(_sums.TotalYearCents - _sums.OwedCents));
                box.Row("reallife_mkt_inv_yield", MarketText.Percent(_sums.YieldBp));
                box.Note(MarketText.T("reallife_mkt_inv_income_note"));
            }
            else
            {
                box.Note(MarketText.T("reallife_mkt_inv_income_none"));
            }
            box.Section("reallife_ibox_sec_payments");
            int all = InvestRules.NextPayments(_holdings, _payments, PaymentsInBox);
            for (int i = 0; i < _payments.Count; i++)
            {
                box.RowWords(PaymentName(s, _payments[i]), PaymentWords(_payments[i]));
            }
            if (all == 0)
            {
                box.Note(MarketText.T("reallife_mkt_inv_no_payments"));
            }
            else if (all > _payments.Count)
            {
                box.Note(MarketText.Fill("reallife_mkt_inv_more_payments", "count", (all - _payments.Count).ToString()));
            }
        }

        // The account: its dividends and commission, the margin account and its buttons, the bonds held.
        private void WriteAccountPage(InfoBox box, MarketState s)
        {
            Account a = s.Account;
            box.Section(a.Margin ? "reallife_mkt_account_margin" : "reallife_mkt_account_cash");
            if (a.Margin && a.CallDay > 0)
            {
                box.Line(MarketText.Fill("reallife_mkt_account_call", "amount", MarketText.Money(a.CallAmount), "day", a.CallDay.ToString()), CellTone.Red);
            }
            box.Line(MarketText.Fill("reallife_mkt_account_dividends", "dividends", MarketText.Money(a.Dividends), "commission", MarketText.Money(a.Commission)));
            Transform buttons;
            if (a.Margin)
            {
                long equity = MarketTrading.Equity(s, s.Phase), need = MarketTrading.Maintenance(s, s.Phase), gross = MarketTrading.GrossExposure(s, s.Phase);
                long power = Math.Max(0, equity * 100 / MarketCatalogue.InitialMarginPct - gross);
                box.Line(MarketText.Fill("reallife_mkt_account_cash_line", "cash", MarketText.Money(a.Cash), "equity", MarketText.Money(equity)));
                box.Line(MarketText.Fill("reallife_mkt_account_margin_line", "need", MarketText.Money(need), "power", MarketText.Money(power)));
                box.Line(MarketText.Fill("reallife_mkt_account_withdrawable", "amount", MarketText.Money(_director.MostWithdrawable())));
                if (a.Interest + a.BorrowFees > 0)
                {
                    box.Line(MarketText.Fill("reallife_mkt_account_costs", "interest", MarketText.Money(a.Interest), "fees", MarketText.Money(a.BorrowFees)));
                }
                buttons = box.ButtonRow();
                if (Settings.MarketsMargin)
                {
                    AskMoney(buttons, InfoBox.Positive, "reallife_mkt_deposit", "reallife_mkt_deposit_placeholder", amount =>
                    {
                        if (!_director.Deposit(amount))
                        {
                            Notifications.ShowError("reallife_mkt_note_deposit_refused", null, false);
                        }
                    });
                }
                AskMoney(buttons, InfoBox.Additional, "reallife_mkt_withdraw", "reallife_mkt_withdraw_placeholder", amount =>
                {
                    if (!_director.Withdraw(amount))
                    {
                        Notifications.ShowError("reallife_mkt_note_withdraw_refused", null, false);
                    }
                });
            }
            else if (Settings.MarketsMargin)
            {
                box.Line(MarketText.Fill("reallife_mkt_account_margin_offer", "least", MarketText.Money(Prices.At(MarketCatalogue.MarginOpenLeastAtStart))));
                buttons = box.ButtonRow();
                AskMoney(buttons, InfoBox.Additional, "reallife_mkt_open_margin", "reallife_mkt_deposit_placeholder", amount =>
                {
                    if (!_director.Deposit(amount))
                    {
                        Notifications.ShowError("reallife_mkt_note_deposit_refused", null, false);
                    }
                });
            }
            BondsLine(box, s);
        }
    }
}
