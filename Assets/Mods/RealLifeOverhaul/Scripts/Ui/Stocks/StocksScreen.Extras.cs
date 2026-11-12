using System;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Stocks screen's part for the market's extras: what each of their sheets starts with, and what its button asks of
    /// the market (units of the fund, a block, the founder's split, the bank's advisers). A refusal is said in the market's
    /// own words; the market tells what was done. On a press only.
    /// </summary>
    public sealed partial class StocksScreen
    {
        // What an extras' sheet starts with.
        private void ExtrasDefaults(MarketState s, byte kind, int key)
        {
            long[] v = _sheet.V;
            switch (kind)
            {
                case StocksSheets.Fund:
                    v[StocksSheets.FundSide] = 0;
                    v[StocksSheets.FundUnits] = 10;
                    break;
                case StocksSheets.Block:
                {
                    StocksBuild.BlockCompanies(s, _parts.Companies);
                    int at = Math.Max(0, _parts.Companies.IndexOf(key));   // the company last opened, when a block may be dealt in it
                    v[StocksSheets.BlockPick] = at;
                    v[StocksSheets.BlockSide] = 0;
                    Stock k = _parts.Companies.Count > 0 ? s.Stock(_parts.Companies[at]) : null;
                    v[StocksSheets.BlockQty] = k != null ? BlockRules.Least(k) : 1;
                    break;
                }
                case StocksSheets.Split:
                    v[StocksSheets.SplitRatio] = 0;
                    break;
                case StocksSheets.BankList:
                    v[StocksSheets.BankFloatAt] = Math.Max(0, Array.IndexOf(ExtrasCatalogue.BankFloatSteps, ExtrasCatalogue.BankFloatDefault));
                    break;
            }
        }

        // A block's company or side changed: its shares start again at the fewest a block is.
        private void BlockChanged(MarketState s, int id)
        {
            if (id != StocksSheets.BlockPick && id != StocksSheets.BlockSide)
            {
                return;
            }
            StocksBuild.BlockCompanies(s, _parts.Companies);
            int at = (int)Math.Max(0, Math.Min(_parts.Companies.Count - 1, _sheet.V[StocksSheets.BlockPick]));
            Stock k = _parts.Companies.Count > 0 ? s.Stock(_parts.Companies[at]) : null;
            _sheet.V[StocksSheets.BlockQty] = k != null ? BlockRules.Least(k) : 1;
        }

        private bool DealFund()
        {
            bool buying = _sheet.V[StocksSheets.FundSide] == 0;
            FundDeal d = _market.FundTrade(Math.Max(1L, _sheet.V[StocksSheets.FundUnits]), buying);
            if (d.Why != FundWhy.None)
            {
                Refused(ExtrasText.Why(d.Why));
                return false;
            }
            _sheet.Close();
            return true;
        }

        private bool DealBlock()
        {
            MarketState s = _market.State;
            StocksBuild.BlockCompanies(s, _parts.Companies);
            if (_parts.Companies.Count == 0)
            {
                return false;
            }
            int at = (int)Math.Max(0, Math.Min(_parts.Companies.Count - 1, _sheet.V[StocksSheets.BlockPick]));
            Stock k = s.Stock(_parts.Companies[at]);
            bool buying = _sheet.V[StocksSheets.BlockSide] == 0;
            long least = BlockRules.Least(k), most = Math.Max(least, BlockRules.Most(s, k, buying));
            BlockQuote q = _market.Block(k.Id, buying, Math.Max(least, Math.Min(most, _sheet.V[StocksSheets.BlockQty])));
            if (q.Why != BlockWhy.None)
            {
                Refused(ExtrasText.Why(q.Why));
                return false;
            }
            _sheet.Close();
            return true;
        }

        private bool AskSplit()
        {
            int ratio = StocksBuild.SplitChoice(_sheet.V[StocksSheets.SplitRatio]);
            SplitWhy why = _market.AskSplit(ratio);
            if (why != SplitWhy.None)
            {
                Refused(ExtrasText.Why(why));
                return false;
            }
            Told(UI.Notification.NotificationType.Info, "reallife_stx_note_split_asked", "ratio", ExtrasText.Ratio(ratio));
            _sheet.Close();
            return true;
        }

        private bool BankStep()
        {
            BankFloat b = _market.BankFloat;
            if (b == null)
            {
                return false;
            }
            if (b.Stage == BankStage.Advised)
            {
                bool gone = _market.BankWithdraw();
                _sheet.Close();
                return gone;
            }
            int floatBp = ExtrasCatalogue.BankFloatSteps[(int)Math.Max(0, Math.Min(ExtrasCatalogue.BankFloatSteps.Length - 1, _sheet.V[StocksSheets.BankFloatAt]))];
            BankWhy why = _market.BankEngage(floatBp);
            if (why != BankWhy.None)
            {
                Refused(ExtrasText.Why(why));
                return false;
            }
            _sheet.Close();
            return true;
        }
    }
}
