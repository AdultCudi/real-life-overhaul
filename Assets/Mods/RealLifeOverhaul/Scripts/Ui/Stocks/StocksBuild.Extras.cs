using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks desk's sheets for the market's extras: units of the index fund, a block trade, the founder's split and the
    /// player's bank on its way to the market. Each is built from the market's own quote, so a figure on a sheet is the one
    /// its button deals at. Game-free but for the director's answers; built when a sheet is shown or a figure moves.
    /// </summary>
    public static partial class StocksBuild
    {
        /// <summary>The page buttons' codes of the extras: on the Portfolio the fund and a block, on the Listing page the split and the bank.</summary>
        public const int CodeFund = 10, CodeBlock = 11, CodeSplit = 200, CodeBank = 201;

        /// <summary>The ratios the founder may choose, as the sheet cycles them: splits first, then shares put together (under nothing).</summary>
        public static int SplitChoice(long at)
        {
            int n = ExtrasCatalogue.FounderForward.Length, all = n + ExtrasCatalogue.FounderReverse.Length;
            int i = (int)Math.Max(0, Math.Min(all - 1, at));
            return i < n ? ExtrasCatalogue.FounderForward[i] : -ExtrasCatalogue.FounderReverse[i - n];
        }

        public static int SplitChoices => ExtrasCatalogue.FounderForward.Length + ExtrasCatalogue.FounderReverse.Length;

        /// <summary>The companies a block may be dealt in, in the order the sheet cycles them: the rival tycoons' holdings, then what the player holds, then the rest.</summary>
        public static void BlockCompanies(MarketState s, List<int> into)
        {
            into.Clear();
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    Stock k = s.Stocks[i];
                    if (!k.Listed || k.Own != null)
                    {
                        continue;
                    }
                    bool holding = k.Def != null && k.Def.Holding, held = s.Held(k.Id) != 0;
                    if (pass == 0 ? holding : pass == 1 ? !holding && held : !holding && !held)
                    {
                        into.Add(k.Id);
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the index fund

        public static StocksSheetModel FundSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            MarketState s = d.State;
            ExtrasState x = s.Extras;
            bool buying = st.V[StocksSheets.FundSide] == 0;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_portfolio", "Portfolio"), w("reallife_stx_fund", "Index fund"), UiIcon.ChartLineUp);
            long units = Math.Max(1L, st.V[StocksSheets.FundUnits]);
            FundDeal q = d.FundQuote(units, buying);
            m.Name = ExtrasCatalogue.FundName;
            m.Big = MarketText.Price(q.NavCents);
            m.Sub = ExtrasCatalogue.FundTicker + "  " + w("reallife_stx_fund_sub", "Every listed company, by the index's weights");
            m.SideLabels = new[]
            {
                w("reallife_stx_fund_units", "Units held"), w("reallife_stk_worth", "Worth"), w("reallife_stx_cost", "Cost"), w("reallife_stx_fund_paid", "Paid to you"),
                w("reallife_stx_fund_fee", "Fee a year"), w("reallife_stk_cash", "Cash")
            };
            m.SideValues = new[]
            {
                x.Units.ToString("N0"), Money(FundRules.Worth(s, x.Units, s.Phase)), Money(x.Cost), Money(x.Paid), MarketText.Percent(ExtrasCatalogue.FundFeeBp), Money(d.Cash)
            };
            long most = Math.Max(1L, q.Most);
            m.Fields = new[]
            {
                PickField(StocksSheets.FundSide, w("reallife_stx_deal", "Deal"), buying ? w("reallife_mkt_buy", "Buy") : w("reallife_mkt_sell", "Sell"), 2),
                StepField(StocksSheets.FundUnits, w("reallife_stx_units", "Units"), units.ToString("N0"), units, 1L, most, Nice(most / 20, 1), true),
                Fact(w("reallife_stx_fund_price", "Price a unit"), MarketText.Price(q.PriceCents)),
                Fact(w("reallife_stk_sh_fee", "Commission"), Money(q.Fee)),
                Fact(w("reallife_stx_total", "In all"), Money(q.Total)),
                Note(w("reallife_stx_fund_about", "A unit is dealt at what its slice of every company costs at that company's own offer or bid, so it is never cheaper than its parts."))
            };
            m.Confirm = buying ? w("reallife_stx_fund_buy", "Buy units") : w("reallife_stx_fund_sell", "Sell units");
            m.ConfirmOff = q.Why != FundWhy.None;
            m.Reason = q.Why != FundWhy.None ? ExtrasText.Why(q.Why) : null;
            return m;
        }

        // ------------------------------------------------------------------------------------------------ a block trade

        public static StocksSheetModel BlockSheet(MarketDirector d, StocksSheetState st, UiWords w, List<int> companies)
        {
            MarketState s = d.State;
            StocksSheetModel m = Frame(w, w("reallife_mkt_tab_portfolio", "Portfolio"), w("reallife_stx_block", "Block trade"), UiIcon.Receipt);
            BlockCompanies(s, companies);
            if (companies.Count == 0)
            {
                m.Name = w("reallife_stk_none", "None");
                m.ConfirmOff = true;
                return m;
            }
            int at = (int)Math.Max(0, Math.Min(companies.Count - 1, st.V[StocksSheets.BlockPick]));
            Stock k = s.Stock(companies[at]);
            bool buying = st.V[StocksSheets.BlockSide] == 0;
            long least = BlockRules.Least(k), most = Math.Max(least, BlockRules.Most(s, k, buying));
            long qty = Math.Max(least, Math.Min(most, st.V[StocksSheets.BlockQty]));
            BlockQuote q = d.BlockQuote(k.Id, buying, qty);
            long held = s.Held(k.Id);
            m.Name = k.Name;
            m.Big = MarketText.Price(k.Last);
            m.Sub = k.Ticker + "  " + (k.Def != null && k.Def.Holding ? w("reallife_stx_block_holding", "A rival's holding") : MarketText.Sector(k.Sector));
            m.SideLabels = new[]
            {
                w("reallife_stx_block_held", "You hold"), w("reallife_stx_block_most", "The most one holder may"), w("reallife_stx_block_order", "One order's most"),
                w("reallife_stk_cash", "Cash")
            };
            m.SideValues = new[]
            {
                held.ToString("N0") + "  (" + MarketText.Percent(BlockRules.StakeBp(held, k.Shares)) + ")", MarketText.Percent(MarketTrading.MostHeldBp(k)),
                MarketTrading.MostPerOrder(k).ToString("N0"), Money(d.Cash)
            };
            m.Fields = new[]
            {
                PickField(StocksSheets.BlockPick, w("reallife_mkt_col_company", "Company"), k.Name, companies.Count),
                PickField(StocksSheets.BlockSide, w("reallife_stx_deal", "Deal"), buying ? w("reallife_mkt_buy", "Buy") : w("reallife_mkt_sell", "Sell"), 2),
                StepField(StocksSheets.BlockQty, w("reallife_mkt_col_shares", "Shares"), qty.ToString("N0"), qty, least, most, Nice(Math.Max(1L, (most - least) / 20), 1), true),
                Fact(w("reallife_stx_block_price", "Price a share"), q.Cents > 0 ? MarketText.Price(q.Cents) : MarketText.Price(k.Last)),
                Fact(w("reallife_stx_block_moves", "Its size moves the price"), MarketText.Percent(q.ImpactBp), q.ImpactBp > 0 ? -1 : 0),
                Fact(w("reallife_stk_sh_fee", "Commission"), Money(q.Fee)),
                Fact(w("reallife_stx_total", "In all"), Money(q.Total)),
                Fact(w("reallife_stx_block_after", "You would hold"), MarketText.Percent(q.Cents > 0 ? q.StakeBp : BlockRules.StakeBp(held, k.Shares))),
                Note(w("reallife_stx_block_about", "A holding is told to the market at every 5% it passes. A block is no way round the most one holder may have."))
            };
            m.Confirm = buying ? w("reallife_stx_block_buy", "Buy the block") : w("reallife_stx_block_sell", "Sell the block");
            m.ConfirmOff = q.Why != BlockWhy.None;
            m.Reason = q.Why != BlockWhy.None ? ExtrasText.Why(q.Why) : null;
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the founder's split

        public static StocksSheetModel SplitSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            StocksSheetModel m = Frame(w, w("reallife_stk_nav_listing", "Your listing"), w("reallife_stx_split", "Split the shares"), UiIcon.ChartLineUp);
            Stock own = d.MyCompany;
            if (own == null)
            {
                m.Name = w("reallife_stk_none", "None");
                m.ConfirmOff = true;
                return m;
            }
            int ratio = SplitChoice(st.V[StocksSheets.SplitRatio]);
            SplitWhy why = d.SplitWhyNow(ratio);
            long after = ratio > 0 ? (long)own.Last / ratio : (long)own.Last * -ratio;
            long mine = ratio > 0 ? own.Own.Founder * ratio : own.Own.Founder / -ratio;
            m.Name = own.Name;
            m.Big = MarketText.Price(own.Last);
            m.Sub = own.Ticker;
            m.SideLabels = new[] { w("reallife_stx_split_yours", "Your shares"), w("reallife_stx_split_all", "Shares in issue"), w("reallife_stx_split_stake", "Your part") };
            m.SideValues = new[] { own.Own.Founder.ToString("N0"), own.Shares.ToString("N0"), MarketText.Percent(own.Own.StakeBp(own)) };
            m.Fields = new[]
            {
                PickField(StocksSheets.SplitRatio, w("reallife_stx_split_ratio", "New shares for old"), ExtrasText.Ratio(ratio), SplitChoices),
                Fact(w("reallife_stx_split_price", "A share after"), MarketText.Price((int)Math.Max(1L, Math.Min(int.MaxValue, after)))),
                Fact(w("reallife_stx_split_mine", "Your shares after"), mine.ToString("N0")),
                Fact(w("reallife_stx_split_notice", "Market days of notice"), MarketCatalogue.SplitNoticeDays.ToString()),
                Note(w("reallife_stx_split_about", "Every holding, order and price changes in the same moment. Nobody's part of the company or its worth changes."))
            };
            m.Confirm = w("reallife_stx_split_ask", "Ask the board");
            m.ConfirmOff = why != SplitWhy.None;
            m.Reason = why != SplitWhy.None ? ExtrasText.Why(why) : null;
            return m;
        }

        // ------------------------------------------------------------------------------------------------ the player's bank

        public static StocksSheetModel BankSheet(MarketDirector d, StocksSheetState st, UiWords w)
        {
            StocksSheetModel m = Frame(w, w("reallife_stk_nav_listing", "Your listing"), w("reallife_stx_bank", "List your bank"), UiIcon.Bank);
            BankFloat b = d.BankFloat;
            if (b == null)
            {
                m.ConfirmOff = true;
                return m;
            }
            Stock stock = d.BankStock;
            if (b.Stage == BankStage.Listed && stock != null)
            {
                m.Name = stock.Name;
                m.Big = MarketText.Price(stock.Last);
                m.Sub = stock.Ticker;
                m.Fields = new[]
                {
                    Fact(w("reallife_stx_split_stake", "Your part"), MarketText.Percent(stock.Own.StakeBp(stock))),
                    Fact(w("reallife_stx_bank_raised", "The offer put into its capital"), Money(b.Raised)),
                    Fact(w("reallife_stx_bank_others", "Paid to its other shareholders"), Money(b.PaidOthers / 100L)),
                    Note(w("reallife_stx_bank_listed_about", "What the bank pays you is now every holder's by their shares. You deal in its shares on its own page."))
                };
                return m;
            }
            if (b.Stage == BankStage.Advised)
            {
                m.Title = w("reallife_lst_withdraw", "Withdraw");
                m.Big = MarketText.Fill("reallife_stx_range", "low", MarketText.Price(b.LowCents), "high", MarketText.Price(b.HighCents));
                m.Fields = new[]
                {
                    Fact(w("reallife_stx_bank_priced", "The book is priced on day"), b.PriceDay.ToString()),
                    Fact(w("reallife_stx_bank_new", "New shares offered"), b.NewShares.ToString("N0")),
                    Fact(w("reallife_stx_bank_fees", "Paid so far"), Money(b.Fees)),
                    Note(w("reallife_stx_bank_way_about", "Under the bottom of the range the offer is called off. Withdrawing now gives the retainer up."))
                };
                m.Confirm = w("reallife_lst_withdraw", "Withdraw");
                return m;
            }
            int floatBp = ExtrasCatalogue.BankFloatSteps[(int)Math.Max(0, Math.Min(ExtrasCatalogue.BankFloatSteps.Length - 1, st.V[StocksSheets.BankFloatAt]))];
            BankWhy why = d.BankWhyNow(out double worth);
            m.Big = Money((long)worth);
            m.Sub = w("reallife_stx_bank_worth", "Worth on its own books");
            m.Fields = new[]
            {
                PickField(StocksSheets.BankFloatAt, w("reallife_stx_bank_float", "Part of the bank offered"), MarketText.Percent(floatBp), ExtrasCatalogue.BankFloatSteps.Length),
                Fact(w("reallife_stx_bank_retainer", "The advisers' retainer"), Money(d.BankRetainer(worth))),
                Fact(w("reallife_stx_bank_days", "Market days to the pricing"), ListingCatalogue.BookDays.ToString()),
                Note(w("reallife_stx_bank_about", "The offer's money goes into the bank's capital, not your cash. Savers' deposits are no part of its worth."))
            };
            m.Confirm = w("reallife_lst_engage", "Engage advisers");
            m.ConfirmOff = why != BankWhy.None;
            m.Reason = why != BankWhy.None ? ExtrasText.Why(why) : null;
            return m;
        }

        /// <summary>The words of an extras' page button.</summary>
        public static string ExtrasCodeWords(int code, UiWords w)
        {
            return code == CodeFund ? w("reallife_stx_fund", "Index fund") : code == CodeBlock ? w("reallife_stx_block", "Block trade")
                : code == CodeSplit ? w("reallife_stx_split", "Split the shares") : w("reallife_stx_bank", "List your bank");
        }

        /// <summary>The fund's row of the Portfolio's account card, when units are held: what they are worth now.</summary>
        public static bool FundRow(MarketState s, UiWords w, out StocksRow row)
        {
            row = default(StocksRow);
            if (s.Extras.Units <= 0)
            {
                return false;
            }
            long worth = FundRules.Worth(s, s.Extras.Units, s.Phase);
            row = new StocksRow
            {
                Label = ExtrasCatalogue.FundName + "  (" + s.Extras.Units.ToString("N0") + ")", Value = Money(worth),
                Tone = worth > s.Extras.Cost ? UiRole.OkText : worth < s.Extras.Cost ? UiRole.DangerText : UiRole.None
            };
            return true;
        }

        /// <summary>The line the chart's reading adds for a company's last split, or null: earlier prices are shown as they would be at today's share count.</summary>
        public static string SplitLine(MarketState s, int stockId)
        {
            return s.Extras.LastSplit(stockId, out SplitNote note) ? ExtrasText.Note(note) : null;
        }
    }
}
