using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Finance screen asks the game to do. Each is the old app's own action, reached through the same director method.</summary>
    public enum FinanceAct : byte
    {
        None = 0,
        Economy,         // open the Economy app
        EconoView,       // the game's loans page, where a loan is repaid early
        Take,            // take the phone loan chosen, for the amount stepped to
        Open,            // open an overdraft or a card, take a business loan, approve a finance, consolidate: the product chosen
        Apply,           // the Credit tab's offer: go and borrow it
        Pay,             // pay the stepped amount on the account chosen
        PayOff,          // pay the whole account
        Draw,            // draw cash on a card
        Close,           // close an account that owes nothing
        CancelApproval,  // cancel a finance approval
        Arrange,         // a debt arrangement
        Bankrupt,        // bankruptcy
        PayOverdue,      // a call for what is overdue: pay it
        AgreePlan,       // a call for what is overdue: agree a plan
        CannotPay,       // a call for what is overdue: say you cannot
        Export,          // write the statement to a file
        Follow,          // follow a rival on Flare
        Counter,         // a counter against a rival (outbid, poach, press, lobby, takeover)
        CutPrices,       // cut the prices of the player's own shops for 14 days
        HoldPrices,      // end a price cut
        OpenHolding,     // the rival's holding in the Stocks app
        ShowList,        // the rich list as a City Ledger page
        Accept,          // accept a request or an invitation
        Refuse,          // refuse a request or decline an invitation
        Join,            // join an accepted invitation
        Lobby,           // push the council for a change
        Campaign         // answer a rival's move with a campaign on Flare
    }

    /// <summary>What the player has chosen on the Finance screen. The screen keeps it; the source reads it to write the facts and to know what a button means.</summary>
    public sealed class FinanceView
    {
        public int Tab;
        public int Offer;                 // the offer chosen on Borrow: the banks first, then the other kinds of credit
        public int Amount;                // the amount stepped to on Borrow, 0 for the most on offer
        public int Deposit, Term, Pick;   // a finance's deposit and term, and the shop or bank a business loan or a consolidation is with
        public int Period = 1;            // Cash flow: yesterday, 7 days, 30 days
        public string Account;            // Accounts: the row chosen, by its id
        public int Pay;                   // Accounts: the amount to pay or draw, 0 for what is due
        public int Rival = -1;            // Rivals: the row of the rich list chosen
        public int Answer;                // Rivals: the answer chosen
        public string Standing;           // Standing: the row chosen, by its id
        public int CreditRow;             // Credit: the offer chosen
        public string Typing;             // the amount while it is typed, null otherwise
    }

    /// <summary>A step on a card: the figure it shows, what it means, and the value a press of minus or plus moves the view to (-1 at an end).</summary>
    public struct FinanceStep
    {
        public string Label;
        public string Text;
        public string Hint;
        public int Prev, Next;
        public int Which;       // FinanceFace.StepDeposit, StepTerm or StepPick
    }

    /// <summary>One offer on Borrow and Credit: a bank's loan or another kind of credit.</summary>
    public sealed class FinanceOffer
    {
        public string Name;
        public string Sub;              // "Up to $400,000, fee 1%"
        public string Rate;             // "5.2%"
        public string Kind;             // "Loan by phone", "Overdraft"
        public bool Offered;
        public string Why;              // why not, while it is not offered
        public bool IsLoan;
    }

    /// <summary>The offer chosen on Borrow, with what it costs for the amount stepped to: the hero, the cost lines, the steps and the buttons.</summary>
    public sealed class FinanceQuoteFacts
    {
        public string Name, Kind, Rate;     // the hero: who lends, what kind, the yearly rate
        public string Big, BigNote;         // "You pay $13,050", "a month, $435 a day"
        public bool HasAmount;              // the amount stepper is shown
        public int Amount, Least, Most;     // the amount, and its range
        public int AmountPrev, AmountNext;  // what a press of minus or plus steps it to (-1 at an end)
        public string AmountHint;           // "$5,000 to $400,000"
        public string[] Labels = new string[0];   // the cost lines
        public string[] Values = new string[0];
        public string Note;                 // a sentence under the lines
        public string Note2;
        public UtilitiesStep[] Steps = new UtilitiesStep[0];
        public FinanceStep[] StepMoves = new FinanceStep[0];
        public string TakeLabel;            // the main button, or null while the offer is refused
        public string Refused;              // why the offer cannot be taken now
        public UiChartModel Owing;          // what you still owe, day by day
        public string[] TakeMore = new string[0];   // extra buttons' words
        public bool[] TakeMoreOn = new bool[0];
        public FinanceAct[] TakeMoreActs = new FinanceAct[0];
        public bool CanTake;
    }

    /// <summary>The credit score as the page writes it: the figure, its grade, how it moved and what the lending floor means.</summary>
    public sealed class FinanceScore
    {
        public int Value, Min, Max, Threshold;
        public string Grade;
        public UiRole Tone = UiRole.Ok;
        public bool Lends;
        public bool HasChange;
        public int Change;               // against ChangeDays ago
        public int ChangeDays;
        public int Halves;               // stars in halves, 0 to 10
        public string Sentence;          // what the lender floor means, built from the code's thresholds
    }

    /// <summary>The Overview tab: the player, cash and debts, what leaves next, the city lines and the cash chart.</summary>
    public sealed class FinanceOverviewFacts
    {
        public string Player;
        public string Cash, Owed, Worth, Leaves, LeavesNote;
        public string RiseNote;               // "Your variable debts cost $X more a month after the last rise", or null
        public UiDataTableModel Debts;
        public string[] NextLabels = new string[0];
        public string[] NextValues = new string[0];
        public float[] Donut = new float[0];
        public string[] DonutLabels = new string[0];
        public string[] DonutValues = new string[0];
        public string[] CityLabels = new string[0];
        public string[] CityValues = new string[0];
        public bool EconomyApp;
        public UiChartModel Cash30;
        public string BestName, BestLine;     // the best offer today, or null
        public bool BestOn;
        public string Day;
    }

    /// <summary>One factor of the credit score: its name, weight, bar, how it stands and what moves it.</summary>
    public sealed class FinanceFactor
    {
        public string Name, Weight, Word, Stands, Tip;
        public int Part;                      // 0 to 100
        public UiRole Tone;
    }

    /// <summary>The Credit tab: the factors, what would raise the score, the offers at this score and the last events.</summary>
    public sealed class FinanceCreditFacts
    {
        public FinanceFactor[] Factors = new FinanceFactor[0];
        public string[] Tips = new string[0];
        public string Worst;
        public UiDataTableModel Offers;
        public UiDataTableModel Events;
        public string OffersTitle;
        public string Refused;
    }

    /// <summary>The Borrow tab: the offers, the one chosen with its quote, who else is borrowing, or why nobody lends.</summary>
    public sealed class FinanceBorrowFacts
    {
        public UiDataTableModel List;         // the banks, then the other kinds of credit
        public FinanceQuoteFacts Quote;
        public UiDataTableModel Feed;
        public string FeedTitle;
        public string Refused;                // the rating is under the floor: why, and what would raise it
        public string[] RefusedTips = new string[0];
    }

    /// <summary>The Cash flow tab: the statement for the period, the cash chart, where it went and what is coming up.</summary>
    public sealed class FinanceCashFacts
    {
        public string[] Periods = new string[0];
        public UiStatementModel Statement;
        public UiChartModel Chart;
        public string ChartTitle, ChartTotal, ChartChange, ChartRange;
        public bool ChangeGood;
        public string WhereTitle, CameInLabel, CameIn, WentOutLabel, WentOut;
        public string WhereNote;              // the saved ledger's reach: "Not yet before day 12", or null once it covers the 30 days
        public string[] InNames = new string[0], InValues = new string[0];
        public int[] InShare = new int[0];
        public string[] OutNames = new string[0], OutValues = new string[0];
        public int[] OutShare = new int[0];
        public string NetLabel, Net;
        public bool NetGood;
        public string ComingTitle;
        public string[] ComingLabels = new string[0], ComingValues = new string[0];
        public string Reach;                  // "The game keeps movements from day 3."
        public string ExportLabel;
    }

    /// <summary>The Accounts tab: every loan, account and card, and the card for the one chosen.</summary>
    public sealed class FinanceAccountsFacts
    {
        public string Headline;               // cash and what you owe
        public UiDataTableModel Table;
        public string[] Ids = new string[0];  // by row
        public UtilitiesCard Card;
        public bool CardPay;                  // the card has a pay amount stepper
        public string PayHint;
        public int PayAmount, PayLeast, PayMost;
        public int PayPrev, PayNext;
    }

    /// <summary>One dated move of a rival, the live one marked.</summary>
    public sealed class FinanceRivalMove
    {
        public string Day, What;
        public bool Live;
    }

    /// <summary>One answer to a rival that does something now: its words, what it does and the act behind it.</summary>
    public sealed class FinanceAnswer
    {
        public string Title, Hint, Tag;
        public FinanceAct Act;
        public int Arg;
        public bool On;
    }

    /// <summary>The Rivals tab: the rich list, the profile of the row chosen, its moves, the answers and the chart.</summary>
    public sealed class FinanceRivalsFacts
    {
        public string Headline;               // the rank and the next move
        public UiDataTableModel Table;
        public int[] Who = new int[0];        // by row: a tycoon's index, -1 the player, 100 and up an old fortune
        public UiProfileModel Profile;
        public string[] Labels = new string[0];
        public string[] Values = new string[0];
        public string Reading;                // the live move in words: what it does to you and when, or null with none
        public string MovesTitle;
        public FinanceRivalMove[] Moves = new FinanceRivalMove[0];
        public string MovesNone;
        public string AnswersTitle;
        public FinanceAnswer[] Answers = new FinanceAnswer[0];
        public UiChartModel Chart;
        public bool CanFollow;
        public string FollowLabel;
        public string HoldingLabel;
        public bool CanHolding;
        public string ListLabel;
    }

    /// <summary>The Standing tab: the reputation and its parts, every row of it and the card for the one chosen.</summary>
    public sealed class FinanceStandingFacts
    {
        public string Profile, Overall;
        public int OverallValue;
        public string[] PartNames = new string[0], PartNotes = new string[0];
        public int[] PartMarks = new int[0];
        public string[] PartWords = new string[0];
        public string Scandal;
        public UiDataTableModel Table;
        public string[] Ids = new string[0];
        public UtilitiesCard Card;
    }

    /// <summary>Everything the Finance screen shows, one section for each tab, so no tab can show another's parts. The source fills the section of the tab asked for.</summary>
    public sealed class FinanceFacts
    {
        public bool Ready;
        public string Day;
        public FinanceScore Score = new FinanceScore();
        public FinanceOverviewFacts Overview = new FinanceOverviewFacts();
        public FinanceCreditFacts Credit = new FinanceCreditFacts();
        public FinanceBorrowFacts Borrow = new FinanceBorrowFacts();
        public FinanceCashFacts Cash = new FinanceCashFacts();
        public FinanceAccountsFacts Accounts = new FinanceAccountsFacts();
        public FinanceRivalsFacts Rivals = new FinanceRivalsFacts();
        public FinanceStandingFacts Standing = new FinanceStandingFacts();
        public bool RivalsOn = true, StandingOn = true;
        public int RivalsCount;               // shown beside the Rivals entry
    }

    /// <summary>The game behind the Finance screen: facts for the tab and the choices, a generation that moves when the save does, and what a button does.</summary>
    public interface IFinanceSource
    {
        int Generation { get; }

        void Read(FinanceFacts into, FinanceView view);

        /// <summary>The tab a notice asked for, once; -1 for none.</summary>
        int TakeWanted();

        bool Do(FinanceAct act, FinanceView view, int arg);
    }
}
