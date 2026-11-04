using System;

namespace RealLifeOverhaul.Ui
{
    // What the Banks screen reads from the save, as plain numbers and the few words only the game can write (a bank's name, a state, a tip).
    // Game-free: BanksBuild writes the page from it, the live screen (BanksLive) and the preview's samples both fill it.

    /// <summary>One bank of the city as the player stands with it.</summary>
    public sealed class BankFact
    {
        public string Name = string.Empty;
        public int Rank;                // the largest 0
        public float Rate;              // the loan rate it offers at your rating, a year, in per cent
        public int Most;                // the most it lends you by phone now
        public bool Lends;              // that most reaches the least loan
        public int Since;               // the day your first account or loan here began; 0 for none yet
        public float Owed;              // what you owe it
        public int Debts;
        public bool Answer;             // the same application at this bank: yes or no, by the rating and its limit
    }

    /// <summary>A debt held at the bank: a game loan, an account, a card, an overdraft.</summary>
    public sealed class BankDebt
    {
        public string Id = string.Empty;
        public int Number;              // an account's number (its Id in the file); 0 for a game loan
        public bool GameLoan;
        public int Kind;                // 0 a loan, 1 a business loan, 2 an overdraft, 3 a card, 4 car finance, 5 a mortgage, 6 arrears, 7 other
        public string Name = string.Empty;
        public float Owed, Total;       // Total: what it was (a loan's total, an account's agreed)
        public string Rate;             // the rate as the page writes it; null for none
        public bool RateRed;
        public int NextAmount, NextDay; // what the next payment is and its day; 0 for none
        public string NextWord;         // the day as words ("tomorrow", "day 311")
        public int EndDay;              // the day it will be paid at its payment; 0 for none
        public string State = string.Empty;
        public bool Sound;
        public bool Frozen, CallDue, PlanUsed;
        public float PlanLeft, PlanPerDay;
        public float Overdue;
        public int Payment, Left;       // the daily payment and the days left at it
        public float Interest;          // a day
        public string Thing;            // what a secured debt is secured on, or null
        public string About;            // a sentence about the kind
        public int Opened;              // the day it began
        public int PayMost, PaySuggested;   // the most one payment may be, and the one it starts at
        public int Minimum, StatementLeft, DueDay, NextStatement;   // a card
        public int Free;                // a limit's free part
        public int DrawMost;            // the most a card may be drawn on now
        public bool Business;
    }

    /// <summary>A card or an overdraft here, or what the bank would give.</summary>
    public sealed class BankLimit
    {
        public bool Held;               // open here
        public string Elsewhere;        // the bank that holds the one you have, when it is not this one; null otherwise
        public string Refusal;          // why none is on offer; null when one is
        public int OfferLimit, OfferRate;       // what it would open at: a limit and the rate in hundredths of a per cent
        public BankDebt Account;        // when Held
        public string Free;             // the free-part sentence of an overdraft
    }

    /// <summary>A movement of the card or a line of the statement.</summary>
    public struct BankMove
    {
        public string Day;
        public string What;
        public float Amount;            // plus came in, minus went out
        public float Balance;
    }

    /// <summary>What a product offers at this bank, a row of the Apply tab.</summary>
    public sealed class BankProduct
    {
        public int Kind;                // 0 loan, 1 overdraft, 2 card, 3 business, 4 car, 5 home, 6 consolidation
        public string Name = string.Empty;
        public int Most;
        public string Rate;
        public string Daily;
        public string Fee;
        public string State = string.Empty;   // On offer, Open, Not offered, held elsewhere: as words
        public bool On;                 // the application can be made
        public bool Open;               // already open
    }

    /// <summary>The form of the product chosen: its amount, its terms, its costs and the one button.</summary>
    public sealed class BankQuote
    {
        public string Refused;          // the reason, when nothing can be applied for
        public string Rate;
        public string Record;           // a business's record of takings
        public bool HasAmount;
        public int Amount, Least, Most, AmountPrev = -1, AmountNext = -1;
        public string AmountHint;
        public string[] Labels = new string[0];
        public string[] Values = new string[0];
        public string Note;
        public string Note2;
        public string StepLabel1, StepText1, StepLabel2, StepText2, StepLabel3, StepText3;   // deposit, term, shop or bank: as words
        public int Step1Prev = -1, Step1Next = -1, Step2Prev = -1, Step2Next = -1, Step3Prev = -1, Step3Next = -1;   // the view's values a minus and a plus go to
        public int Step1Field = MoneyView.FDeposit, Step2Field = MoneyView.FTerm, Step3Field = MoneyView.FShop;
        public bool CanApply = true;
        public bool HasApproval;        // a finance is approved: its cost so far and the cancel
        public string ApprovalLine;
        public int Meter = -1;          // headroom, 0 to 100
    }

    /// <summary>A line of "what the bank checked": a sentence and whether it holds.</summary>
    public struct BankCheck
    {
        public string Text;
        public bool Met;
    }

    /// <summary>The rating as this bank sees it: a row of a band.</summary>
    public struct BankBand
    {
        public string Score;
        public string Kind;
        public string Loan;
        public string Rate;
        public string Card;
        public string Overdraft;
        public string Car;
        public bool Yours;
        public bool Refused;
    }

    /// <summary>One graded reason of the rating, with its sentence.</summary>
    public struct BankFactor
    {
        public string Name;
        public int Part;                // 0 to 100
        public string Stands;
    }

    /// <summary>Everything the Banks screen shows, for the tab and the bank chosen. Only the chosen tab's part is filled.</summary>
    public sealed class BanksFacts
    {
        public bool Ready;
        public int Day;
        public BankFact[] Banks = new BankFact[0];
        public int Bank;
        public int Score, Threshold, ScoreMax;
        public string Grade = string.Empty;
        public UiRole GradeTone = UiRole.Ok;
        public bool Lends;              // the rating reaches the threshold
        public int Halves;              // stars in halves
        public float Cash;
        public bool IsHouse;            // this bank holds your cash
        public string HouseName = string.Empty;
        public float Owed, Tomorrow;    // here
        public int Debts;
        public bool ProductsOn;
        public string Gate;             // why no application can be made at all (a bankruptcy, an arrangement), or null
        public string Note;             // the bank's own words about itself

        // Accounts
        public BankMove[] Statement = new BankMove[0];
        public float StatementIn, StatementOut;
        public string[] Periods = new string[0];
        public int Period;

        // Loans
        public BankDebt[] Debt = new BankDebt[0];

        // Overdraft, Card
        public BankLimit Overdraft = new BankLimit();
        public BankLimit Card = new BankLimit();
        public BankMove[] CardMoves = new BankMove[0];

        // Apply
        public BankProduct[] Products = new BankProduct[0];
        public int Product;
        public BankQuote Quote = new BankQuote();
        public BankCheck[] Checks = new BankCheck[0];
        public string[] Tips = new string[0];
        public int Headroom = -1;

        // Rating
        public BankFactor[] Factors = new BankFactor[0];
        public BankBand[] Bands = new BankBand[0];
        public string Next;             // the next band, as a sentence
    }
}
