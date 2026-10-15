using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Rivals
{
    /// <summary>A rival's holding as the stock market has it now: listed here, its shares, the player's, the most one holder may have, its price in cents.</summary>
    public delegate bool HoldingLook(int stockId, out long shares, out long held, out long most, out int midCents);

    /// <summary>A rival's offer to the player's most skilled employee: who it went to and where they work. False when there is nobody to offer it to.</summary>
    public delegate bool RivalOffer(string rival, int premiumPermille, out string name, out string business);

    /// <summary>The City Ledger's page of the rich list, as words: the paper prints it as a front page of its own.</summary>
    public sealed class RichListPage
    {
        public int Day;
        public string Header = string.Empty, EarLeftLabel = string.Empty, EarLeft = string.Empty, EarRightLabel = string.Empty, EarRight = string.Empty;
        public string DateMiddle = string.Empty, DateRight = string.Empty, Kicker = string.Empty, Headline = string.Empty, Stand = string.Empty, Byline = string.Empty, Body = string.Empty;
        public string NoticeHead = string.Empty, Notice = string.Empty;
        public readonly List<string> Labels = new List<string>(3);
        public readonly List<List<string>> Texts = new List<List<string>>(3);
        public readonly List<string> BoxLabels = new List<string>(2);
        public readonly List<List<string>> BoxLines = new List<List<string>>(2);
    }

    /// <summary>
    /// What the rival tycoons ask of the rest of the mod, set by the wiring (Wiring/RivalLinks). The rivals call no other
    /// feature: every move goes through the feature that owns what it touches, by one of these, and each one left unset
    /// simply makes that kind of move come to nothing (it is never tried). Reset when the wiring is taken down.
    /// </summary>
    public static class RivalBridges
    {
        // ---- what the city is
        /// <summary>The player's net worth as the Finance app shows it (Economy/Finance, Books.NetWorth). Default: the game's own figure (RivalGame).</summary>
        public static Func<long> Worth;
        /// <summary>The city's cycle: 0 boom, 1 steady, 2 slowdown, 3 recession (Economy/City). Default: steady.</summary>
        public static Func<int> Cycle;
        /// <summary>The playthrough's id the stock market keeps, for a new file's seed. Default: none (the day and the money are used).</summary>
        public static Func<ulong> Playthrough;

        // ---- the stock market (Markets)
        /// <summary>The market runs on this machine on its own (not following another player's). Default: no.</summary>
        public static Func<bool> MarketHere;
        /// <summary>The player's company is listed and trading. Default: no.</summary>
        public static Func<bool> Listed;
        /// <summary>Days the player's company has traded on the market (the market's own listing day); -1 while it is not listed.</summary>
        public static Func<int> CompanyDays;
        /// <summary>The market's year in days (60 to 365, as the save was started).</summary>
        public static Func<int> YearDays;
        /// <summary>A holding comes to the market now (its stock id), sized by its tycoon's fortune (dollars); true if it is listed (or was already).</summary>
        public static Func<int, long, bool> ListHolding;
        /// <summary>A holding with no tycoon in play leaves the market at its price, the player's shares paid out at it. True if it did.</summary>
        public static Func<int, bool> DelistHolding;
        public static HoldingLook Holding;
        /// <summary>A rival's holding (its stock id) buys a block of the player's company and presses its board. True if it did.</summary>
        public static Func<int, bool> Stake;
        /// <summary>A rival's holding (its stock id) bids for the player's company. True if the bid stands.</summary>
        public static Func<int, bool> Bid;
        /// <summary>The player takes a holding over (its stock id, the cost): the cost taken from their cash, the holding off the market. True if done.</summary>
        public static Func<int, long, bool> TakeOver;
        /// <summary>Opens the Stocks app on a company (its stock id).</summary>
        public static Action<int> OpenStock;

        // ---- business pressure (Business)
        public static Func<bool> IssuesOn;
        /// <summary>Business pressure's own smear at the player's business at a place. True if it started.</summary>
        public static Func<string, bool> Smear;
        /// <summary>An inspection (or a licence check) called in at the player's business at a place. True if one started.</summary>
        public static Func<string, bool> Inspect;
        /// <summary>The business at a place has its customers written again now (business pressure's one writer).</summary>
        public static Action<string> Footfall;

        // ---- the company (Economy/Company)
        public static Func<bool> StaffOn;
        public static RivalOffer Offer;

        // ---- the courts (Justice)
        public static Func<bool> CourtOn;
        /// <summary>A rival's civil claim (the amount, the claimant's name). Its number in the court's books, or 0.</summary>
        public static Func<long, string, int> Sue;
        /// <summary>A claim not yet judged withdrawn (a test put back). True if there was one.</summary>
        public static Func<int, bool> Withdraw;

        // ---- property (Economy/Estate)
        /// <summary>The buildings' values written again now, with a buying up's premium (EstateDirector.Revalue).</summary>
        public static Action Revalue;

        // ---- Standing (Fame)
        /// <summary>Points on the customers' reputation, for a piece in the paper.</summary>
        public static Action<int> Customers;

        // ---- the City Ledger (Ledger)
        public static Func<bool> PaperOn;
        /// <summary>The rich list as a page of the City Ledger, opened now. False if the paper cannot show it yet.</summary>
        public static Func<RichListPage, bool> ShowList;

        // ---- the Finance app (Economy/Finance)
        /// <summary>Opens the Finance app on the rivals' page.</summary>
        public static Action Open;

        public static void Reset()
        {
            Worth = null;
            Cycle = null;
            Playthrough = null;
            MarketHere = null;
            Listed = null;
            CompanyDays = null;
            YearDays = null;
            ListHolding = null;
            DelistHolding = null;
            Withdraw = null;
            Revalue = null;
            Holding = null;
            Stake = null;
            Bid = null;
            TakeOver = null;
            OpenStock = null;
            IssuesOn = null;
            Smear = null;
            Inspect = null;
            Footfall = null;
            StaffOn = null;
            Offer = null;
            CourtOn = null;
            Sue = null;
            Customers = null;
            PaperOn = null;
            ShowList = null;
            Open = null;
        }

        /// <summary>A bridge's answer, or the default when it is not set.</summary>
        public static bool Ask(Func<bool> bridge) => bridge != null && bridge();
    }
}
