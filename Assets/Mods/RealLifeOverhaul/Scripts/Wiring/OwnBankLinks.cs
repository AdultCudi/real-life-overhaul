using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The player's own bank (Economy/OwnBank) with the rest of the mod. The bank calls no other feature and none calls
        /// it but here:
        ///
        ///   Integrity   the player's integrity as an owner (Standing): the city's trust in the bank, and so its savers
        ///   Policy      the council's levers (Civic): business tax, grants, the wage floor and rents move defaults
        ///   Courts      the justice system takes a lender's claims (Justice: DebtClaims.Available): more of a loan written
        ///               off comes back through the courts, later and for their costs
        ///   Quotes      the Stocks app's listed companies (Markets) as the bank's fund sees them, once a day
        ///   Moved       a fine or a bank closed costs the player integrity (Standing), as a breach in the papers does a little
        ///   the paper   the bank's pieces in the City Ledger the morning after (News.Extra, after the company's)
        ///
        /// The bank's takings and its rows among its headquarters' goods are in the game's statement through
        /// Wiring/EconomyLinks (EconomyBridges.Takings, CostRow). Each is read once a day or when the bank's page is open,
        /// never per frame.
        /// </summary>
        public static class OwnBankLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                OwnBankBridges.Integrity = () => Standing.Integrity;
                OwnBankBridges.Policy = Policy;
                OwnBankBridges.Courts = Courts;
                OwnBankBridges.Quotes = Quotes;
                OwnBankBridges.Moved = Moved;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                OwnBankBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            private static int Policy(Lever lever)
            {
                try
                {
                    return CivicPolicy.Position(lever);
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Policy", e);
                    return 0;
                }
            }

            private static bool Courts()
            {
                try
                {
                    return DebtClaims.Available;
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Courts", e);
                    return false;
                }
            }

            // The market's listed companies, and those gone from it with what each share was paid out at. Strings are the stock's own (nothing is built).
            private static bool Quotes(List<FundQuote> into)
            {
                into.Clear();
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    MarketState state = market != null ? market.State : null;
                    if (state == null)
                    {
                        return false;
                    }
                    for (int i = 0; i < state.Stocks.Count; i++)
                    {
                        Stock s = state.Stocks[i];
                        into.Add(new FundQuote
                        {
                            StockId = s.Id, Ticker = s.Ticker, Name = s.Name, LastCents = s.Last, DpsCents = s.DpsCents, Shares = s.Shares, Size = (int)s.Size,
                            Distress = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, s.Distress)) * 1000.0), Listed = s.Status == StockStatus.Listed, UnderBid = s.BidCents > 0,
                            GoneCents = s.GoneCents
                        });
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Quotes", e);
                    into.Clear();
                    return false;
                }
            }

            private static void Moved(int delta, bool scandal)
            {
                try
                {
                    FameDirector.Instance?.Moved(FamePart.Integrity, scandal ? FameCause.Scandal : FameCause.Press, delta);
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Moved", e);
                }
            }

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Paper (the pieces before)", e);
                }
                try
                {
                    OwnBankDirector.Instance?.PaperPieces(day, into);
                }
                catch (Exception e)
                {
                    Fault("OwnBankLinks.Paper", e);
                }
            }
        }
    }
}
