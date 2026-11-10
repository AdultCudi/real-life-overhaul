using System;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>A kind of scheme, each with a trail of its own. Saved as a number: new ones go at the end.</summary>
    public enum SchemeKind
    {
        Claim = 0,     // an insurance claim for a loss that never happened
        Expense = 1,   // an invented expense on the tax return
        Books = 2,     // sales that never happened, shown to the buyer of a business
        Club = 3,      // a members' club that does not exist, sold to a district's households
        Wash = 4,      // the club's cash run through a till as sales
        Graft = 5      // money to somebody with power over the player's affairs
    }

    /// <summary>Where an inquiry stands. Saved as a number.</summary>
    public enum InquiryStage
    {
        Quiet = 0,     // opened, and nobody has been told
        Letter = 1,    // a letter has come: the Legal file lists it
        Visit = 2,     // investigators have been to the place: the cash in hand is watched
        Charged = 3,   // what was found was taken back and the court has the rest
        Closed = 4
    }

    /// <summary>How an inquiry ended. Saved as a number.</summary>
    public enum InquiryEnd
    {
        None = 0,
        Dropped = 1,   // no case to answer
        Caution = 2,   // everything paid back in time, and little enough: a warning, no charge
        Charged = 3,   // before the court
        Convicted = 4,
        Cleared = 5,   // acquitted, dismissed or quashed
        Settled = 6    // settled with the city, or found with no court to charge in
    }

    /// <summary>What money buys from somebody with a position. Saved as a number: new ones go at the end.</summary>
    public enum Favour
    {
        Word = 0,          // what the city knows: where each trail stands, and who else is looked at
        Gift = 1,          // a gift to somebody with a position: their next favour, without the friendship
        Access = 2,        // a place on the list of the next city reception
        Vote = 3,          // a councillor carries the push the player has before the council
        Investigator = 4,  // the investigator loses part of the file
        Judge = 5          // the judge weighs the evidence lightly
    }

    /// <summary>What somebody does when money is offered: fixed for each person by the save's seed, whoever asks.</summary>
    public enum Temper
    {
        Takes = 0,
        Remembers = 1,     // says no, tells nobody, and wants no more to do with it
        Reports = 2        // says no and reports it
    }

    /// <summary>Whose office somebody holds, for how likely they are to take money.</summary>
    public enum Office
    {
        Contact = 0,       // one of the people the player knows who holds a position
        Councillor = 1,
        Investigator = 2,
        Judge = 3
    }

    /// <summary>Where a rival's fund offered to the player stands. Saved as a number.</summary>
    public enum FundState
    {
        None = 0,
        Open = 1,          // offered, to be answered
        Placed = 2,        // the player's money is in it
        Paid = 3,          // it paid back with its return
        Lost = 4,          // it was a sham: the money is the rival's
        Recovered = 5,     // a sham whose rival was found out: the money came back
        Declined = 6
    }

    /// <summary>
    /// Every figure of the schemes, with its unit and why. Money written "at the start's prices" is asked at today's by the
    /// caller. Heat, evidence and shares are thousandths unless said. Game-free.
    /// </summary>
    public static class SchemeCatalogue
    {
        public const int Kinds = 6;                    // the kinds of scheme, each with its own trail
        public const int Small = 250, Large = 1000;    // an act's size, thousandths of the most that is plausible

        // ---- suspicion (heat): what an act adds to its kind's trail, and how it fades
        public static readonly int[] HeatBase = { 120, 100, 80, 150, 0, 60 };   // an act of each kind, before its size; the till's is daily (WashHeat)
        public const int HeatSize = 600;               // a full-sized act adds this: on its own it always passes the line an inquiry opens at
        public const int HeatRepeat = 70;              // more for each earlier act of the kind in RepeatDays: splitting is judged as the pattern it is
        public const int RepeatDays = 90;              // days an earlier act counts as part of a pattern
        public const int RepeatMost = 5;               // earlier acts counted at most
        public const int HeatKnows = 40;               // for each person who knows of it
        public const int HeatPaper = 60;               // paid through the bank account: a paper trail
        public const int HeatSpill = 100;              // thousandths of an act's heat that also falls on every other trail: one known for one thing is watched for others
        public const int HeatDecay = 4;                // a day: a small act is forgotten in about two months
        public const int HeatMost = 2000;
        public const int SpotPermille = 60;            // an act in so many of a thousand is spotted whatever its size (a routine check): no act is free of risk
        public const int HeatSpotted = 400;            // what being spotted adds
        public const int OpenLeast = 250;              // the line an inquiry opens at is drawn from the seed for each kind and period...
        public const int OpenSpread = 400;             // ...between OpenLeast and OpenLeast + OpenSpread - 1
        public const int OpenPeriodDays = 90;          // days a drawn line stands: none is safe for ever

        // ---- an inquiry: evidence gathered over days, then what is found
        public const int EvidenceBase = 350;           // what investigators start from: the papers that made them look
        public const int EvidenceAct = 90;             // for each act on file
        public const int EvidenceSize = 200;           // for a full-sized act, in proportion
        public const int EvidenceKnows = 40;           // for each person who knows of an act
        public const int EvidencePaper = 60;           // for an act with a paper trail
        public const int EvidenceRepaid = 40;          // less for an act already paid back
        public const int EvidenceMost = 950;
        public const int EvidencePace = 45;            // a day towards what the file holds
        public const int LetterAfter = 3;              // days from opening to the letter
        public const int VisitAfter = 8;               // ...to the visit
        public const int ChargeAfter = 14;             // ...to the findings, if the evidence stands
        public const int GiveUpAfter = 30;             // ...to dropping it, if it never did
        public const int ChargeFrom = 350;             // evidence under this is no case to answer: just over the prosecutor's own line (JusticeLaw.DismissBelow)
        public const int ChargeAtOnce = 800;           // evidence this strong is acted on without waiting for ChargeAfter
        public const int LostFile = 400;               // what a bought investigator takes off what the file holds: one act's worth, never a pattern's
        public const int CoopEvidence = 950;           // telling them everything: the file is complete
        public const int TrailDays = 720;              // days an act stays on file: an inquiry reaches back this far
        public const int ActsMost = 24;                // acts kept for a kind
        public const int InquiriesKept = 12;
        public const int SurchargePermille = 250;      // what is found is taken back with a quarter on top
        public const int SurchargeRepaidPermille = 125;   // ...and what the player paid back before the findings carries half of that
        public const int FelonyFrom = 5000;            // at the start's prices: a file of this much or more is a felony, under it a misdemeanour and a caution if repaid
        public const int ClearedPermille = 500;        // the trail's heat kept after an inquiry found no case
        public const int GaveUpPermille = 700;         // ...after one was dropped

        // ---- the claim: a share of the most the cover counts (CompanyCatalogue.CoverMost), paid by the insurer's own rules
        public const int ClaimGapDays = 20;            // days between two invented claims on one cover

        // ---- the invented expense: so many days of the businesses' average takings at most, worth the game's tax on it
        public const int ExpenseDays = 20;             // a full-sized invented expense, in days of average takings
        public const int ExpenseGapDays = 15;          // days between two

        // ---- the books: sales that never happened, shown to a buyer
        public static readonly int[] LiftSteps = { 0, 100, 300 };   // thousandths the books show over the truth: plain, a little, a lot
        public const int LiftMost = 300;
        public const int LiftHeatDivisor = 50;         // a day of dressed books adds its lift over this: 2 or 6 against a decay of 4
        public static readonly int[] Diligence = { 450, 800, 150, 650 };   // how hard a buyer looks, by kind as selling keeps them: a rival, an investor, the staff, a chain
        public const int HeatWalked = 300;             // a buyer who saw through the books walks away and says why

        // ---- the club: a district's households pay a stake each into a club that does not exist
        public const int ClubSmall = 50, ClubLarge = 200;   // households approached
        public const int ClubStakePermille = 50;       // of a household's savings
        public const int ClubStakeMost = 400;          // at the start's prices, a household
        public const int ClubSavingsLeast = 200;       // at the start's prices: a household with less is left alone
        public const int ClubGapDays = 30;             // days between two clubs from one business
        public const int ClubKnowsEvery = 25;          // households to one who complains: people who know

        // ---- the till: cash in hand run through a business as sales
        public static readonly int[] WashSteps = { 0, 50, 200 };    // thousandths of an average day's takings a day: what the business could plausibly sell more
        public const int WashCut = 150;                // thousandths of the cash lost on the way; the game's tax rate is then taken off the rest
        public const int WashHeatDivisor = 10;         // a day's washing adds its rate over this: 5 or 20 against a decay of 4
        public const int ReportFrom = 2500;            // at the start's prices: a day's cash sales above this are reported by the bank
        public const int HeatReport = 30;
        public const int PatternDays = 30;             // the window the bank adds small deposits up over
        public const int PatternFrom = 25000;          // at the start's prices: the window's sum that is reported as a pattern, once a window
        public const int HeatPattern = 120;

        // ---- money to people: what each favour weighs, costs and whom it asks
        public static readonly int[] FavourSize = { 100, 150, 250, 500, 700, 1000 };          // an act's size, by favour
        public static readonly int[] FavourPrice = { 1500, 800, 4000, 6000, 5000, 25000 };    // at the start's prices; the investigator's is its least
        public const int InvestigatorShare = 600;      // thousandths of the proceeds on file the investigator asks, if more than the least
        public static readonly int[] TakesPermille = { 550, 400, 250, 200 };      // by office: a contact, a councillor, an investigator, a judge
        public static readonly int[] ReportsPermille = { 200, 300, 400, 500 };    // ...who say no and report it; the rest say no and remember
        public const int HoldDays = 180;               // days somebody paid holds what they know
        public const int TalkPermille = 2;             // a day that somebody paid talks...
        public const int TalkPressed = 20;             // ...and while an inquiry into graft is open
        public const int HeatTalk = 250;
        public const int EvidenceTalk = 200;           // what somebody who talks adds to an open inquiry
        public const int HeatReported = 300;           // an offer that was reported, on top of the act it is filed as
        public const int EvidenceReported = 150;       // ...by the investigator of an open inquiry, on that inquiry
        public const int JudgeLean = 200;              // thousandths a bought judge takes off the evidence of each charge
        public const int PaidMost = 16, ColdMost = 64, BoughtMost = 32;

        // ---- the rivals: the same schemes by seeded rules
        public const int RivalEveryDays = 30;          // each rival is looked at once in so many days
        public const int RivalCleanPermille = 300;     // rivals in a thousand who never scheme
        public const int RivalBentLeast = 300, RivalBentSpread = 600;   // a bent rival's chance each look, thousandths
        public const int RivalLargePermille = 300;     // of a rival's schemes that are full-sized
        public const int RivalSmallGain = 5, RivalLargeGain = 40;       // ten-thousandths of their worth a scheme brings them
        public const int RivalGainMost = 250000;       // at the start's prices
        public const int RivalCounsel = 600;           // thousandths: the counsel a rival's money buys
        public const int RivalBarDays = 60;            // days a convicted rival may not lobby or buy up buildings, and grows at half the pace
        public const int RivalQuietDays = 30;          // ...and makes no move

        // ---- a rival's fund offered to the player
        public const int FundEveryDays = 60;           // days between two offers
        public const int FundFirstDays = 10;           // days after the file opens before the first
        public const int FundSharePermille = 20;       // of the player's worth
        public const int FundLeast = 2000, FundMost = 50000;   // at the start's prices
        public const int FundReturn = 80;              // thousandths an honest fund pays on top
        public const int FundDays = 30;                // days the money is placed
        public const int FundAnswerDays = 7;
        public const int FundFeePermille = 30;         // of the sum: what having it looked at costs
        public const int FundFeeLeast = 250;           // at the start's prices
        public const int FundShamShare = 800;          // thousandths of a rival's bent that an offer of theirs is a sham

        /// <summary>Thousandths of a sum, rounded down, never negative.</summary>
        public static long Share(long amount, int permille)
        {
            return Math.Max(0L, amount) * Math.Max(0, permille) / 1000L;
        }

        public static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }

        /// <summary>A sum of the start's prices at a price level in tenths of a point (1000 is the start's).</summary>
        public static long AtPrices(long amount, int level)
        {
            return amount * Math.Max(1, level) / 1000L;
        }
    }
}
