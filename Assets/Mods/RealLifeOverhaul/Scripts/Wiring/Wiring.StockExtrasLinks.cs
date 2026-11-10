using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The stock market's extras (Markets/Extras/ExtrasBridges) with the rest of the mod:
        ///
        ///   Bank, BankCapital,
        ///   BankPaidIn          the player's own bank as a listing reads it, and the offer's money into its capital (Economy/OwnBank)
        ///   OwnBankBridges      a day of the bank's own books kept for the market; what the bank pays its owner shared with its other holders
        ///   MarketBridges.Books the bank's days taken out of the whole company's books while the bank trades on its own
        ///   Standing            the founder's name, for the bank's book
        ///   Dealt               the founder's deals in the bank's shares on the tax return (Tech/Venture/ExitGains)
        ///   Rival, RivalGain    the rival tycoons' worth, and what they make or lose on the market
        ///   RivalBridges        a tycoon's announced purchase of shares made real, and their line on the rich list
        ///
        /// Asked on a day, a market morning or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class StockExtrasLinks
        {
            private static Func<int, int, OwnBooks> _booksBefore;

            internal static void Connect()
            {
                ExtrasBridges.Bank = Bank;
                ExtrasBridges.BankCapital = BankCapital;
                ExtrasBridges.BankPaidIn = BankPaidIn;
                ExtrasBridges.Standing = () => FameDirector.Instance != null && FameDirector.Instance.File != null ? FameDirector.Instance.File.Overall : -1;
                ExtrasBridges.Dealt = Dealt;
                ExtrasBridges.Rival = Rival;
                ExtrasBridges.RivalGain = RivalGain;
                OwnBankBridges.OwnerPart = OwnerPart;
                OwnBankBridges.DayBooked = DayBooked;
                RivalBridges.MarketLine = tycoon => MarketDirector.Instance != null ? MarketDirector.Instance.RivalMarketLine(tycoon) : string.Empty;
                RivalBridges.Shares = (tycoon, amount) => MarketDirector.Instance != null && MarketDirector.Instance.RivalShares(tycoon, amount);
                _booksBefore = MarketBridges.Books;
                MarketBridges.Books = Books;
            }

            internal static void Disconnect()
            {
                ExtrasBridges.Reset();
                OwnBankBridges.OwnerPart = null;
                OwnBankBridges.DayBooked = null;
                RivalBridges.MarketLine = null;
                RivalBridges.Shares = null;
                MarketBridges.Books = _booksBefore;
                _booksBefore = null;
            }

            private static BankFacts Bank()
            {
                try
                {
                    OwnBankDirector d = OwnBankDirector.Instance;
                    OwnBank b = d != null ? d.Bank : null;
                    if (b == null || !b.Exists)
                    {
                        return default(BankFacts);
                    }
                    bool gone = b.State == OwnBankState.Sold || b.State == OwnBankState.Closed;
                    return new BankFacts
                    {
                        Running = b.Running, Test = b.Test, Clear = b.Stage == Regulator.Clear && !b.Run, Gone = gone, FoundedDay = b.FoundedDay,
                        CapitalCents = gone ? b.EndCents : b.Capital, Name = b.Name
                    };
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.Bank", e);
                    return default(BankFacts);
                }
            }

            private static bool BankCapital(long cents)
            {
                try
                {
                    return OwnBankDirector.Instance != null && OwnBankDirector.Instance.PublicCapital(cents);
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.BankCapital", e);
                    return false;
                }
            }

            private static long BankPaidIn()
            {
                try
                {
                    OwnBank b = OwnBankDirector.Instance != null ? OwnBankDirector.Instance.Bank : null;
                    return b != null ? Math.Max(0L, b.FoundedLeast) : 0L;
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.BankPaidIn", e);
                    return 0L;
                }
            }

            // Of what the bank pays its owner, the owner's own part once its shares trade.
            private static long OwnerPart(long cents)
            {
                try
                {
                    return MarketDirector.BankOwnerPart(cents);
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.OwnerPart", e);
                    return cents;
                }
            }

            private static void DayBooked(int day, long income, long profit)
            {
                try
                {
                    MarketDirector.Instance?.BankBooked(day, income, profit);
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.DayBooked", e);
                }
            }

            // The whole company's books without the bank's days while the bank is a stock of its own: the same takings never feed two stocks.
            private static OwnBooks Books(int from, int to)
            {
                OwnBooks books = _booksBefore != null ? _booksBefore(from, to) : default(OwnBooks);
                try
                {
                    MarketDirector m = MarketDirector.Instance;
                    if (m != null && m.BankListed)
                    {
                        m.BankDays(from, to, out long revenue, out long profit);
                        books.Revenue -= revenue;
                        books.Profit -= profit;
                    }
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.Books", e);
                }
                return books;
            }

            private static void Dealt(string place, string name, long shares, long money, long fee, long held, long invested)
            {
                try
                {
                    TechDirector.Instance?.StakeDealt(place, invested, name, shares, money, fee, held);
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.Dealt", e);
                }
            }

            // A tycoon who is forward, while the rivals are not resting (a Going Public session).
            private static bool Rival(int index, out long worth, out int stockId, out string holding, out string name, out bool listed)
            {
                worth = 0;
                stockId = 0;
                holding = name = string.Empty;
                listed = false;
                try
                {
                    RivalDirector d = RivalDirector.Instance;
                    RivalFile f = d != null && !d.Resting && !GoingPublic.InSession ? d.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t == null || !t.Active)
                    {
                        return false;
                    }
                    worth = t.Worth;
                    stockId = t.Def.StockId;
                    holding = t.Def.Holding;
                    name = t.Def.Name;
                    listed = t.Listed;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.Rival", e);
                    return false;
                }
            }

            // What a tycoon made or lost on a block: into or out of their worth, never under nothing.
            private static void RivalGain(int index, long money)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t != null && t.Active)
                    {
                        t.Worth = Math.Max(0L, t.Worth + money);
                    }
                }
                catch (Exception e)
                {
                    Fault("StockExtrasLinks.RivalGain", e);
                }
            }
        }
    }
}
