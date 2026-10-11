using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The Market and Portfolio tabs: the listed companies, the shares held, and one company's page with what moves
    /// it and a count of shares to buy or sell. No chart is drawn: the trend is the thirty-day move as a figure, and
    /// the page gives the thirty days' low and high.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private const string CompanyRow = "co:", HoldRow = "hold:";
        private static readonly int[] ShareSteps = { 1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };

        private readonly List<int> _counts = new List<int>(ShareSteps.Length + 2);
        private int _shareStep;        // which of the counts on offer is picked

        private TableSpec WriteMarket(InfoBox box, VenturesFile file)
        {
            UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_head_market", "days", file.MarketSteps.ToString()));
            for (int i = 0; i < file.Companies.Count; i++)
            {
                Company c = file.Companies[i];
                Holding h = file.Holding(c.Id);
                bool held = h != null && h.Shares > 0;
                TableRow row = Row(CompanyRow, c.Id);
                row.Kind = (int)c.Sector;
                row.Kind2 = held ? VenturesTables.Held : VenturesTables.NotHeld;
                row.Set(0, c.Name);
                row.Set(1, FinanceText.T(VenturesCatalogue.MarketSectorKeys[(int)c.Sector]));
                row.Set(2, VenturesText.Price(c.PriceCents), c.PriceCents / 100.0);
                row.Set(3, VenturesText.Change(c.LastBp), c.LastBp / 100.0, Sign(c.LastBp));
                if (held)
                {
                    int value = Market.Value(c, h.Shares);
                    row.Set(4, h.Shares.ToString("N0"), h.Shares);
                    row.Set(5, Money(value), value);
                    row.Set(6, (value >= h.Cost ? "+" : "-") + Money(Mathf.Abs(value - h.Cost)), value - h.Cost, Sign(value - h.Cost));
                }
                row.Set(7, (c.YieldBp / 100f).ToString("0.0#") + "%", c.YieldBp / 100.0);
            }
            Company picked = file.Company(Picked(_picked, CompanyRow));
            if (picked != null)
            {
                WriteCompany(box, file, picked);
                return VenturesTables.Market;
            }
            _picked = null;
            int cost;
            box.Title(FinanceText.T("reallife_ven_tab_market"));
            box.Line(FinanceText.T("reallife_ven_market_what", "count", file.Companies.Count.ToString()));
            box.Line(FinanceText.T("reallife_ven_market_commission", "share", VenturesText.Share(VenturesCatalogue.CommissionBp), "least", Money(VenturesCatalogue.CommissionLeast)));
            box.Line(FinanceText.T("reallife_ven_market_dividends", "every", VenturesCatalogue.DividendEveryDays.ToString(), "days", Market.DaysToDividend(file).ToString()));
            box.Line(FinanceText.T("reallife_ven_market_limit", "most", VenturesCatalogue.HoldingMost.ToString("N0")));
            box.Line(Pair("reallife_ven_market_held", Money(Market.Worth(file, out cost))));
            if (!Settings.VenturesMarket)
            {
                box.Line(FinanceText.T("reallife_ven_market_off"));
            }
            return VenturesTables.Market;
        }

        private TableSpec WritePortfolio(InfoBox box, VenturesFile file)
        {
            int cost, dividends = 0;
            int worth = Market.Worth(file, out cost);
            UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_head_portfolio", "worth", Money(worth)));
            for (int i = 0; i < file.Holdings.Count; i++)
            {
                Holding h = file.Holdings[i];
                Company c = file.Company(h.CompanyId);
                if (c == null)
                {
                    continue;
                }
                int value = Market.Value(c, h.Shares);
                dividends += h.Dividends;
                TableRow row = Row(HoldRow, c.Id);
                row.Kind = (int)c.Sector;
                row.Set(0, c.Name);
                row.Set(1, FinanceText.T(VenturesCatalogue.MarketSectorKeys[(int)c.Sector]));
                row.Set(2, h.Shares.ToString("N0"), h.Shares);
                row.Set(3, Money(value), value);
                row.Set(4, Money(h.Cost), h.Cost);
                row.Set(5, (value >= h.Cost ? "+" : "-") + Money(Mathf.Abs(value - h.Cost)), value - h.Cost, Sign(value - h.Cost));
                row.Set(6, Money(h.Dividends), h.Dividends);
            }
            if (_rows.Count == 0)
            {
                Say(FinanceText.T("reallife_ven_row_no_shares"));
            }
            Company picked = file.Company(Picked(_picked, CompanyRow));
            if (picked != null)
            {
                WriteCompany(box, file, picked);
                return VenturesTables.Portfolio;
            }
            _picked = null;
            box.Title(FinanceText.T("reallife_ven_tab_portfolio"));
            box.Line(Pair("reallife_ven_port_worth", Money(worth)));
            box.Line(Pair("reallife_ven_port_cost", Money(cost)));
            box.Line(Pair("reallife_ven_port_gain", (worth >= cost ? "+" : "-") + Money(Mathf.Abs(worth - cost))));
            box.Line(Pair("reallife_ven_port_dividends", Money(dividends)));
            box.Line(FinanceText.T("reallife_ven_market_dividends", "every", VenturesCatalogue.DividendEveryDays.ToString(), "days", Market.DaysToDividend(file).ToString()));
            box.Line(FinanceText.T("reallife_ven_port_note"));
            return VenturesTables.Portfolio;
        }

        // A gain in the game's green and a loss in its red, as EconoView colours an amount.
        private static int Sign(int amount)
        {
            return amount > 0 ? TableRow.Green : amount < 0 ? TableRow.Red : TableRow.Plain;
        }

        // One company: what it is, what moves it, what is held, and a count of shares to buy or sell.
        private void WriteCompany(InfoBox box, VenturesFile file, Company c)
        {
            Holding held = file.Holding(c.Id);
            int have = held != null ? held.Shares : 0;
            int low = c.PriceCents, high = c.PriceCents;
            for (int i = 0; i < c.HistoryCount; i++)
            {
                low = Mathf.Min(low, c.History[i]);
                high = Mathf.Max(high, c.History[i]);
            }
            box.Title(c.Name);
            box.Line(Pair("reallife_ven_co_sector", FinanceText.T(VenturesCatalogue.MarketSectorKeys[(int)c.Sector])));
            box.Line(Pair("reallife_ven_co_price", FinanceText.T("reallife_ven_co_price_value", "price", VenturesText.Price(c.PriceCents), "change", VenturesText.Change(c.LastBp),
                "cause", VenturesText.Cause(c.LastCause))));
            box.Line(Pair("reallife_ven_co_range", FinanceText.T("reallife_ven_row_range", "least", VenturesText.Price(low), "most", VenturesText.Price(high)))
                     + " " + FinanceText.T("reallife_tab_ven_trend", "change", VenturesText.Change(c.TrendBp)));
            box.Line(Pair("reallife_ven_co_dividend", FinanceText.T("reallife_ven_co_dividend_value", "share", (c.YieldBp / 100f).ToString("0.0#") + "%",
                "every", VenturesCatalogue.DividendEveryDays.ToString())));
            box.Line(FinanceText.T(VenturesCatalogue.MovesKeys[(int)c.Sector]));
            box.Line(Pair("reallife_ven_co_held", have > 0 ? FinanceText.T("reallife_ven_co_held_value", "shares", have.ToString("N0"), "worth", Money(Market.Value(c, have)), "cost", Money(held.Cost))
                : FinanceText.T("reallife_ven_meet_none")));
            if (have > 0)
            {
                // What a share cost on average, commission included, and what the holding has paid in dividends.
                box.Line(FinanceText.T("reallife_tab_ven_held_more", "price", VenturesText.Price((int)((long)held.Cost * 100L / have)), "dividends", Money(held.Dividends)));
            }
            if (!Settings.VenturesMarket)
            {
                box.Line(FinanceText.T("reallife_ven_market_off"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            // The counts on offer: round numbers up to the most that can be bought or is held, and those two themselves.
            int cash = _director.Wallet.Cash;
            int buyable = Market.MostBuyable(file, c, cash);
            int most = Mathf.Max(buyable, have);
            _counts.Clear();
            for (int i = 0; i < ShareSteps.Length && ShareSteps[i] < most; i++)
            {
                _counts.Add(ShareSteps[i]);
            }
            Insert(buyable);
            Insert(have);
            if (_counts.Count == 0)
            {
                box.Line(FinanceText.T("reallife_ven_co_no_cash"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _shareStep = Mathf.Clamp(_shareStep, 0, _counts.Count - 1);
            int count = _counts[_shareStep];
            box.Headline("reallife_ven_co_count");
            if (!box.HasSelector || !box.Selector(_counts.Count, _shareStep, i => _counts[i].ToString("N0"), i =>
                {
                    _shareStep = i;
                    Refresh();
                }))
            {
                box.Line(count.ToString("N0"));
            }
            int buyCost = Market.Cost(c, count), sellValue = Market.Value(c, count);
            box.Line(FinanceText.T("reallife_ven_co_buy_line", "cost", Money(buyCost), "fee", Money(Market.Commission(buyCost))));
            if (count <= have)
            {
                box.Line(FinanceText.T("reallife_ven_co_sell_line", "value", Money(sellValue), "fee", Money(Mathf.Min(sellValue, Market.Commission(sellValue)))));
            }
            int id = c.Id;
            Transform buttons = box.ButtonRow();
            if (count <= buyable)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ven_buy", () => Trade(id, count, true));
            }
            if (count <= have)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ven_sell", () => Trade(id, count, false));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }

        private void Insert(int count)
        {
            if (count <= 0 || _counts.Contains(count))
            {
                return;
            }
            int at = 0;
            while (at < _counts.Count && _counts[at] < count)
            {
                at++;
            }
            _counts.Insert(at, count);
        }

        // The game's own question box: the price and the commission are said before a trade is made.
        private void Trade(int companyId, int count, bool buy)
        {
            VenturesFile file = _director.File;
            Company c = file != null ? file.Company(companyId) : null;
            if (c == null)
            {
                ClosePick();
                return;
            }
            int amount = buy ? Market.Cost(c, count) : Market.Value(c, count);
            int fee = buy ? Market.Commission(amount) : Mathf.Min(amount, Market.Commission(amount));
            HudConfirm.Show((buy ? "reallife_ven_confirm_buy_title" : "reallife_ven_confirm_sell_title").Localize(new { company = c.Name }),
                (buy ? "reallife_ven_confirm_buy" : "reallife_ven_confirm_sell").Localize(new
                {
                    company = c.Name,
                    shares = count.ToString("N0"),
                    price = VenturesText.Price(c.PriceCents),
                    amount = Money(amount),
                    fee = Money(fee),
                    total = Money(buy ? amount + fee : amount - fee)
                }),
                () => UiClone.Safe(() => Traded(companyId, count, buy)), null, buy ? "reallife_ven_buy" : "reallife_ven_sell", "common_cancel", false, false);
        }

        private void Traded(int companyId, int count, bool buy)
        {
            TradeAnswer answer = buy ? _director.Buy(companyId, count) : _director.Sell(companyId, count);
            switch (answer)
            {
                case TradeAnswer.Done:
                    VenturesFile file = _director.File;
                    Company c = file != null ? file.Company(companyId) : null;
                    _director.Tell(NotificationType.Success, buy ? "reallife_ven_note_bought" : "reallife_ven_note_sold", null, "shares", count.ToString("N0"), "company", c != null ? c.Name : string.Empty);
                    break;
                case TradeAnswer.NoCash:
                    Notifications.ShowInsufficientMoney();
                    break;
                case TradeAnswer.OverLimit:
                    _director.Tell(NotificationType.Error, "reallife_ven_note_limit", null, "most", VenturesCatalogue.HoldingMost.ToString("N0"));
                    break;
                case TradeAnswer.NoShares:
                    _director.Tell(NotificationType.Error, "reallife_ven_note_no_shares", null);
                    break;
            }
            if (Showing)
            {
                _ready = _director.Running;
                _shareStep = 0;
                Refresh();
            }
        }
    }
}
