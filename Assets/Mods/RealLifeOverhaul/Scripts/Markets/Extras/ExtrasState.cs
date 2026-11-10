using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>A split that took effect: so many new shares for so many old, on a day. Kept for the charts' notes.</summary>
    public struct SplitNote
    {
        public int Stock, Day;
        public int New, Old;         // 2 and 1 for a two for one split; 1 and 10 for one for ten

        public bool Reverse => Old > New;
    }

    /// <summary>The fund's distributions owed to the player: what its companies declared at a record day, paid on their day.</summary>
    public struct FundDue
    {
        public int PayDay;
        public long Cents;
    }

    /// <summary>The lasting move of the player's own trades still in a company's price (ln), fading as the market's mood does.</summary>
    public struct PushNote
    {
        public int Stock;
        public double Ln;
    }

    /// <summary>The share of a company the market was last told a holder has, in the steps it is told at.</summary>
    public struct StakeTold
    {
        public int Stock;
        public int Holder;           // 0 the player; a tycoon's holding (its stock id)
        public int Bp;
    }

    /// <summary>A block of a company a rival tycoon holds.</summary>
    public struct RivalStake
    {
        public int Stock;
        public long Shares;
        public long Cost;            // money
        public int Day;
    }

    /// <summary>What one rival tycoon has on the market beside their holding: units of the fund, blocks, an arm they listed.</summary>
    public sealed class RivalBook
    {
        public long Units;           // of the fund
        public long UnitsCost;       // money
        public int LookDay = -1;     // the last day they looked at the market
        public int ArmStock;         // the bank or lab they put on the market (its stock id); 0 none
        public int ArmKind;          // 0 a bank, 1 a lab
        public int ArmDay;
        public readonly List<RivalStake> Stakes = new List<RivalStake>(ExtrasCatalogue.RivalStakesMost);

        public bool Empty => Units == 0 && Stakes.Count == 0 && ArmStock == 0 && LookDay < 0;
    }

    /// <summary>Where the player's bank stands on its way to the market. Saved as a number.</summary>
    public enum BankStage { None = 0, Advised = 1, Listed = 2, CalledOff = 3, Ended = 4 }

    /// <summary>One day of the bank's own books, in cents.</summary>
    public struct BankDayNote
    {
        public int Day;
        public long Income, Profit;
    }

    /// <summary>The player's bank on its way to the market and on it: its own books and the offer. In their save only.</summary>
    public sealed class BankFloat
    {
        public BankStage Stage;
        public int AdvisedDay, PriceDay;         // the advisers engaged; the book priced at this day's open
        public int FloatBp;
        public int FairCents, LowCents, HighCents;
        public long OldShares, NewShares;
        public int PriceCents;                   // what the book came to
        public long Raised;                      // money the offer put into the bank's capital, after the underwriters and the exchange
        public long Fees;                        // money paid on the way
        public int StockId;
        public int ListDay;
        public long PaidOthers;                  // cents of the bank's payouts that were the other holders'
        public readonly List<BankDayNote> Days = new List<BankDayNote>(ExtrasCatalogue.BankDaysKept + 1);
        public readonly List<BookQuarter> Books = new List<BookQuarter>(ExtrasCatalogue.BankQuartersKept + 1);   // in whole money, as a company's

        public bool OnTheWay => Stage == BankStage.Advised;
        public bool Empty => Stage == BankStage.None && Days.Count == 0 && Books.Count == 0 && Fees == 0;
    }

    /// <summary>
    /// Everything the stock market's extras keep of a save, in a line of their own (<see cref="ExtrasStore"/>): the player's
    /// units of the fund, the fund's standing against the index, splits for the charts, the player's own push on prices, what
    /// was disclosed, the rivals' books and the bank's way to the market. Never in the line a host sends. Game-free.
    /// </summary>
    public sealed class ExtrasState
    {
        // The fund.
        public long Units;
        public long Cost;                        // money, commission included
        public long Realised;                    // gains and losses taken
        public long Paid;                        // distributions received, before tax
        public long Fees;                        // what the yearly fee has taken of the player's units, cents
        public double Track = 1.0;               // the fund's worth against the index's: the fee and companies that left paying less than their price
        public int TrackDay;                     // the last market day whose fee came out
        public long OddCents;                    // cents under a whole unit of money still the player's: a consolidation's and the fund's
        public readonly List<FundDue> Dues = new List<FundDue>(8);

        public readonly List<SplitNote> Splits = new List<SplitNote>(8);
        public readonly List<PushNote> Pushes = new List<PushNote>(8);
        public readonly List<StakeTold> Told = new List<StakeTold>(8);
        public readonly RivalBook[] Rivals = { new RivalBook(), new RivalBook(), new RivalBook() };
        public readonly BankFloat Bank = new BankFloat();

        public bool Empty
        {
            get
            {
                for (int i = 0; i < Rivals.Length; i++)
                {
                    if (!Rivals[i].Empty)
                    {
                        return false;
                    }
                }
                return Units == 0 && Cost == 0 && Realised == 0 && Paid == 0 && Track == 1.0 && OddCents == 0 && Dues.Count == 0 && Splits.Count == 0 && Pushes.Count == 0 && Told.Count == 0
                       && Bank.Empty;
            }
        }

        /// <summary>The player's own push still in a company's price (ln); nothing for one they have not moved.</summary>
        public double PushOf(int stock)
        {
            for (int i = 0; i < Pushes.Count; i++)
            {
                if (Pushes[i].Stock == stock)
                {
                    return Pushes[i].Ln;
                }
            }
            return 0.0;
        }

        /// <summary>A fill's lasting move added to the player's push on its company.</summary>
        public void Push(int stock, double ln)
        {
            if (ln == 0.0)
            {
                return;
            }
            for (int i = 0; i < Pushes.Count; i++)
            {
                if (Pushes[i].Stock == stock)
                {
                    PushNote p = Pushes[i];
                    p.Ln += ln;
                    Pushes[i] = p;
                    return;
                }
            }
            Pushes.Add(new PushNote { Stock = stock, Ln = ln });
        }

        /// <summary>Every push faded by a share of itself, as the market's mood fades; one so small it is nothing goes.</summary>
        public void Fade(double share)
        {
            double keep = Math.Max(0.0, 1.0 - share);
            for (int i = Pushes.Count - 1; i >= 0; i--)
            {
                PushNote p = Pushes[i];
                p.Ln *= keep;
                if (Math.Abs(p.Ln) < 1e-7)
                {
                    Pushes.RemoveAt(i);
                }
                else
                {
                    Pushes[i] = p;
                }
            }
        }

        public void NoteSplit(int stock, int day, int fresh, int old)
        {
            Splits.Add(new SplitNote { Stock = stock, Day = day, New = fresh, Old = old });
            while (Splits.Count > ExtrasCatalogue.SplitsKept)
            {
                Splits.RemoveAt(0);
            }
        }

        /// <summary>The last split of a company, if it is remembered.</summary>
        public bool LastSplit(int stock, out SplitNote note)
        {
            for (int i = Splits.Count - 1; i >= 0; i--)
            {
                if (Splits[i].Stock == stock)
                {
                    note = Splits[i];
                    return true;
                }
            }
            note = default(SplitNote);
            return false;
        }

        public int ToldBp(int stock, int holder)
        {
            for (int i = 0; i < Told.Count; i++)
            {
                if (Told[i].Stock == stock && Told[i].Holder == holder)
                {
                    return Told[i].Bp;
                }
            }
            return 0;
        }

        public void SetTold(int stock, int holder, int bp)
        {
            for (int i = 0; i < Told.Count; i++)
            {
                if (Told[i].Stock == stock && Told[i].Holder == holder)
                {
                    if (bp <= 0)
                    {
                        Told.RemoveAt(i);
                    }
                    else
                    {
                        Told[i] = new StakeTold { Stock = stock, Holder = holder, Bp = bp };
                    }
                    return;
                }
            }
            if (bp > 0)
            {
                Told.Add(new StakeTold { Stock = stock, Holder = holder, Bp = bp });
                while (Told.Count > ExtrasCatalogue.DisclosuresKept)
                {
                    Told.RemoveAt(0);
                }
            }
        }

        public void CopyFrom(ExtrasState o)
        {
            Units = o.Units;
            Cost = o.Cost;
            Realised = o.Realised;
            Paid = o.Paid;
            Fees = o.Fees;
            Track = o.Track;
            TrackDay = o.TrackDay;
            OddCents = o.OddCents;
            Dues.Clear();
            Dues.AddRange(o.Dues);
            Splits.Clear();
            Splits.AddRange(o.Splits);
            Pushes.Clear();
            Pushes.AddRange(o.Pushes);
            Told.Clear();
            Told.AddRange(o.Told);
            for (int i = 0; i < Rivals.Length; i++)
            {
                RivalBook a = Rivals[i], b = o.Rivals[i];
                a.Units = b.Units;
                a.UnitsCost = b.UnitsCost;
                a.LookDay = b.LookDay;
                a.ArmStock = b.ArmStock;
                a.ArmKind = b.ArmKind;
                a.ArmDay = b.ArmDay;
                a.Stakes.Clear();
                a.Stakes.AddRange(b.Stakes);
            }
            BankFloat k = Bank, f = o.Bank;
            k.Stage = f.Stage;
            k.AdvisedDay = f.AdvisedDay;
            k.PriceDay = f.PriceDay;
            k.FloatBp = f.FloatBp;
            k.FairCents = f.FairCents;
            k.LowCents = f.LowCents;
            k.HighCents = f.HighCents;
            k.OldShares = f.OldShares;
            k.NewShares = f.NewShares;
            k.PriceCents = f.PriceCents;
            k.Raised = f.Raised;
            k.Fees = f.Fees;
            k.StockId = f.StockId;
            k.ListDay = f.ListDay;
            k.PaidOthers = f.PaidOthers;
            k.Days.Clear();
            k.Days.AddRange(f.Days);
            k.Books.Clear();
            k.Books.AddRange(f.Books);
        }
    }
}
