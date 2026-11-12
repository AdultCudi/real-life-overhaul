using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One thing an investor was promised or asked of the player: its words and where it stands (0 to come, 1 kept, 2 broken).</summary>
    public struct VenPromiseFact
    {
        public string Text;
        public int State;
    }

    /// <summary>A firm an investor also backs, and their stake in it in per cent.</summary>
    public struct VenBackFact
    {
        public string Name;
        public int Pct;
    }

    /// <summary>An investor as the pages show them: a face, a fund or a kind, a track record, what they like and when they see the player.</summary>
    public sealed class VenInvestorFact
    {
        public int Id;
        public string Name, Kind, Temper, Likes;
        public int Portfolio;
        public VenBackFact[] Backs = new VenBackFact[0];
        public bool Open;
        public string OpenText;             // "Open now", "From day 120", "Not again"
        public long Least, Most;
        public int Committed;               // what a rival's round took of their sum
        public bool Never;
    }

    /// <summary>Money on the table for a share of one business's profit.</summary>
    public sealed class VenOfferFact
    {
        public int Id;
        public VenInvestorFact Who;
        public string For;                  // the business, or "A new Coffee shop"
        public bool ForNew;
        public long Amount;
        public int ShareBp;
        public long Implied;
        public int TargetPct, TargetDays, DaysLeft;
        public bool Countered, CanMoreMoney;
        public string BuyBack;              // the term in words, or null for none
        public string SaleLine;             // what happens to the investor if the firm is sold
        public VenPromiseFact[] Promises = new VenPromiseFact[0];
        public string TargetLine;
    }

    /// <summary>Money promised for a business not yet open.</summary>
    public sealed class VenPledgeFact
    {
        public int Id;
        public VenInvestorFact Who;
        public string Type;
        public long Amount;
        public int ShareBp, OpenBy, DaysLeft;
    }

    /// <summary>An investor's stake in one business of the player's.</summary>
    public sealed class VenDealFact
    {
        public int Id;
        public VenInvestorFact Who;
        public string Business;
        public long Amount, PaidOut, Arrears;
        public int ShareBp, ShareNowBp, StartDay, Mood;
        public long Implied;
        public string MoodText, StateText;
        public int Stage;                   // DealStage as a number: 0 running, 1 demand, 2 cold, 3 called, 4 quiet
        public string TargetLine, CostLine, SeatLine, DemandLine, CalledLine;
        public bool CanSeat;                // the investor is not a fund: a board seat may answer a demand
        public long BuyOut;
        public string BuyBack;              // the term's words, or null
        public bool BuyBackNow, BuyBackCalls;   // the term's day has come, and the investor may call it
        public long BuyBackPrice;
        public string SaleLine;
        public VenPromiseFact[] Promises = new VenPromiseFact[0];
        public int Kept, Total;
        public bool ReportDue;              // a report promise is open and no report was sent this quarter
        public string Day;                  // "Signed day 301"
    }

    /// <summary>A building of the player's, read as an investment.</summary>
    public sealed class VenPropertyFact
    {
        public string Key, Address, District;
        public int Let;
        public long Paid, Worth, Week, Month, Mortgage;
        public string Yield, Bought;
        public int YieldGrade;
    }

    /// <summary>A business the player could raise money for, or the kind of one not yet open.</summary>
    public sealed class VenRaiseFact
    {
        public string Key, Name, Line;
        public long AvgProfit;
        public int AgeDays, Promised, Liked, Running;
        public string Sector;
    }

    /// <summary>Everything the Ventures pages read, plain: the live source fills it from the director and the game, the samples from invented numbers.</summary>
    public sealed class VenFacts
    {
        public bool Ready = true;
        public string OffTitle, OffBody;
        public int Day;
        public bool InvestorsOn = true;
        public int Reputation;
        public long Invested, PaidOutTotal;
        public int TargetsMet, PromisesKept, PromisesTotal;
        public VenOfferFact[] Offers = new VenOfferFact[0];
        public VenPledgeFact[] Pledges = new VenPledgeFact[0];
        public VenDealFact[] Deals = new VenDealFact[0];
        public VenInvestorFact[] Investors = new VenInvestorFact[0];
        public string[] MonthNames = new string[0];      // the valuation ring's months, oldest first
        public long[] Mine = new long[0];                // the player's valuation by month, 0 for a month with no deal
        public long[] Rival = new long[0];               // the rival's, in the same months
        public string RivalName;
        public string RivalLine;                         // "X raised $4.2M from Y last month and is valued at $9M", or null
        public string HeardLine;                         // what the other investors heard, or null
        public VenPropertyFact[] Property = new VenPropertyFact[0];
        public long PropWeek, PropMonth, PropPaid;
        public VenRaiseFact[] RaiseBusinesses = new VenRaiseFact[0];
        public VenRaiseFact[] RaiseTypes = new VenRaiseFact[0];
        public long[] Budgets = new long[0];
        public int Offers4;                              // the most offers the file keeps
        public int Least4;                               // unused; keeps the layout of a sample stable
        public string HowText;                           // what it takes to be offered money
        public bool RaiseNow;                            // a meeting may start now
    }

    /// <summary>What a button on the Ventures pages does in the game: the director's methods, or the recording stub the checks press.</summary>
    public interface IVenActions
    {
        void Accept(int offerId);
        void Refuse(int offerId);
        void Counter(int offerId, bool moreMoney);
        void Demand(int dealId, bool board);
        void BuyOut(int dealId);
        void BuyBack(int dealId);
        void Report(int dealId);
        void GiveUp(int pledgeId);
        void Pitch(int investorId, string place, string type, int budgetStep);
        void Locate(string place);
        void OpenBizMan();
    }
}
