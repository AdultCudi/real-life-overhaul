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
        /// The player's own bank with Standing (its savers' trust, integrity lost to a fine), the council's levers, the
        /// courts, the Stocks app's quotes for its fund and the City Ledger. Read once a day or while its page is open.
        /// </summary>
        public static class OwnBankLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;

            internal static void Connect()
            {
                OwnBankBridges.Integrity = () => Standing.Integrity;
                OwnBankBridges.StandingOn = () => FameDirector.Instance != null && FameDirector.Instance.File != null;
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

            // The listed companies, and those gone with what each share was paid out at; the strings are the stock's own.
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
                        if (s.Own != null)
                        {
                            continue;   // the player's own listed company: the bank's money never props its owner's price
                        }
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
