using System;

namespace RealLifeOverhaul.Ui
{
    // What the Company app on the kit shows, as plain data: game-free, so the offline checks and the previews lay it out with nothing behind it. The live
    // source (CompanyLive) reads the director and fills these from a reset state before each read; the samples fill them by hand. A figure that is words is
    // already written in the game's language and money form; a number that is graded or measured is a number.

    /// <summary>What a press on the page does: the director's call it stands for. Every action drawn carries one, and the screen's source does it.</summary>
    public enum CompanyDo : byte
    {
        None = 0,
        // moving about: the screen's own (a tab, a pick, a sub page, back), never the director's
        Go,                 // tab A, pick S, sub A2
        BizMan,             // open the business S in the game's BizMan
        SellBusiness,       // open the sale of S
        OpenCleaning,       // open the cleaning company
        OpenBank,           // open the player's own bank
        OpenStocks,         // open the company's line in Stocks
        // questions waiting on the owner
        Settle,             // escalation A settled with answer B (agree 0, refuse 1, bonus 3, limit 4, haggle 5)
        Dismiss,            // escalation A dismissed
        TryNow,             // Deep Mode: escalation A tried at the level that holds it
        Pass,               // Deep Mode: escalation A passed up
        // staff
        RaiseAnswer,        // the raise asked by employee S: answer A (agree, offer less, refuse, bonus)
        RaiseGive,          // a raise given to employee S unasked
        OfferAnswer,        // the rival's offer to employee S: match (A 1) or let go (A 0)
        StrikeAnswer,       // the strike at place S: raise for all (0), a one-off payment (1), sit it out (2)
        OvertimeAnswer,     // the overtime offered at place S: agree (1) or refuse (0)
        RaisesAll,          // every raise asked at once: agree (A 0) or refuse (A 1)
        SickPolicy,         // sick pay: policy A
        // cover
        CoverBuy,           // cover kind A at place S from insurer B
        CoverCancel,        // cover kind A at place S ended
        ClaimFile,          // claim A put in
        // marketing
        CampaignBook,       // campaign kind A at place S
        // tax and licences
        ReturnFile,         // the waiting return filed
        AccountantHire,     // accountant tier A hired
        AccountantDismiss,  // the accountant let go
        HideSet,            // cash off the books at place S: per cent A
        LicenceBuy,         // licence kind A at place S bought, renewed or applied for
        LicenceAuto,        // licence kind A at place S renews by itself: A2 on or off
        ReserveSet,         // money set aside in the tax reserve: A whole money
        ReserveRelease,     // the reserve back in the cash
        ReserveAuto,        // the finance chief tops the reserve up: A 1 on, 0 off
        // rota
        RotaMode,           // place S planned by mode A
        RotaFill,           // place S filled by the plan of rung A (-1 the best)
        RotaBack,           // place S back to the last rota
        PlanNew,            // a new rota plan at place S
        PlanCopy,           // plan A at place S copied
        PlanUse,            // plan A at place S in use
        PlanDelete,         // plan A at place S taken away
        PlanChange,         // plan A at place S: figure B set to C (RotaPlanChange's fields)
        // managers
        TaskSet,            // place S: task A on (B 1) or off (B 0)
        LimitSet,           // place S: limit A moved to step B
        StandDown,          // the manager at place S stood down (A 1: a regional manager)
        Appoint,            // staff member S made manager of place A... (S the employee id; place in S2)
        HandBack,           // everything the manager at place S holds handed back
        Undo,               // decision A undone
        DeepMode,           // the owner's Deep Mode: A 1 on, 0 off
        // policy, templates, dashboards, seats (Deep Mode)
        RuleSet,            // rule A at scope S set to step B
        RuleClear,          // rule A at scope S follows the scope above
        TemplateSave,       // the scope S's rules saved as a template
        TemplateApply,      // template A applied to scope S
        TemplateDelete,     // template A taken away
        StoreFill,          // place S's gaps filled now
        StoreRaise,         // place S's raises now
        StoreArea,          // place S put in area A
        RegionFill,         // region S's gaps filled
        RegionRaise,        // region S's raises
        RegionMove,         // region S's crew moved
        RegionAppoint,      // a regional manager appointed in region S: employee in S2
        SeatAppoint,        // seat A given to professional S
        SeatStandDown       // seat A emptied
    }

    /// <summary>One thing a press does: what, to which place or person, and the figures. Carried by every action's target.</summary>
    public struct CompanyAct
    {
        public CompanyDo Do;
        public string S, S2;
        public int A, B, C;

        public static CompanyAct Of(CompanyDo d, string s = null, int a = 0, int b = 0, int c = 0, string s2 = null) => new CompanyAct { Do = d, S = s, S2 = s2, A = a, B = b, C = c };
    }

    /// <summary>An action drawn: its words, look and key, what it does, and whether it can be done now (and, if not, why).</summary>
    public struct CompanyButton
    {
        public string Label;
        public UiButtonKind Kind;
        public UiIcon Icon;
        public UiAction Key;        // Alt (F) the page's main one, Option (P) its second, Count for none
        public CompanyAct Act;
        public bool Disabled;
        public string Why;          // when disabled: the reason, shown in place of nothing ("Not now: they are resting")
        public bool Arms;           // a destructive action: the first press arms it, the second does it
    }

    /// <summary>A consequence line of a choice: the words, the way it goes (the arrow) and whether that is good (the colour).</summary>
    public struct CompanyConsequence
    {
        public string Text;
        public int Tone;            // UiConsequence's: +1 good, -1 bad, Loss money paid out, 0 neither
        public int Change;          // +1 rises, -1 falls, 0 none
    }

    /// <summary>A numbered choice of a question: its name, its note at the right, its consequences and what it does.</summary>
    public struct CompanyChoice
    {
        public string Name;
        public string Button;       // the main button's words while this one is chosen ("Raise Dev to $19"); the name where null
        public string Note;         // "now", "once", "standing", "no cost"
        public CompanyConsequence[] Lines;
        public CompanyAct Act;
        public bool Disabled;
        public string Why;
    }

    /// <summary>A question waiting on the owner (an escalation): who asks, about whom, what it is, what can be done, and the page of who asked.</summary>
    public sealed class CompanyQuestion
    {
        public int Id;                          // the escalation's; 0 for none
        public string Kicker;                   // "A manager's question, Speedy Bites, 7 5th Avenue" / "Needs you, Ray Walsh, Speedy Bites"
        public string Title;                    // "A raise above my limit"
        public string Headline;                 // the Lead's one-line statement: "Dev Kapoor asks for $19 an hour. Ray can approve up to $18."
        public string Body;                     // the narrative
        public string Asker, AskerRole, Subject, SubjectRole;     // the manager (or executive) who asks and the person it is about; null where there is none
        public float AskerHue;
        public UiRole AskerRing = UiRole.Warn, SubjectRing = UiRole.Ok;
        public string[] FactLabels = new string[0];
        public string[] FactValues = new string[0];
        public CompanyChoice[] Choices = new CompanyChoice[0];
        public int Selected;                    // the choice the main button does
        public string OpenLabel;                // "Open Ray's page"
        public CompanyAct Open;                 // where it goes
        public bool Deep;                       // held below the owner: Deep Mode's Try now and Pass stand in place of answers
    }

    /// <summary>A row of "also waiting": who, what, and where the question opens.</summary>
    public struct CompanyWaiting
    {
        public string Name, Line;
        public float Hue;
        public UiRole Ring;
        public UiIcon Icon;                     // a glyph on a tile where what waits is no person's (a licence, a gap in the rota); None for the person's face
        public CompanyAct Open;
        public string OpenLabel;
    }

    /// <summary>A row of "done for you today": when, what the manager did, and what it cost or saved.</summary>
    public struct CompanyDone
    {
        public string When, Text, Money;
        public int Tone;                        // +1 earned or saved, -1 paid, 0 none
        public CompanyAct Open;
    }

    /// <summary>A business as the company's head and its day table show it.</summary>
    public struct CompanyBusiness
    {
        public string Place, Name, Mark;        // the place key, the name, its initials on the tile
        public float Hue;
        public string Takings, Costs, Result;   // the day's, as money; null where the game has no statement for it
        public int ResultTone;                  // +1, -1, 0
        public bool HasBooks;
        public bool Waiting;                    // something at it waits for the owner
    }

    /// <summary>A row of the manager list on the overview: who, where, and how their day went.</summary>
    public struct CompanyManagerToday
    {
        public string Name, Place, Figures;     // "14 decided, 1 asked you"
        public float Hue;
        public UiRole Ring;
        public CompanyAct Open;
    }

    /// <summary>The overview's facts: the head, the day, the question that needs the owner, what else waits, what was done and where the money went.</summary>
    public sealed class CompanyOverviewFacts
    {
        public string Line;                     // "Head office 7 5th Avenue, Midtown, 31 staff, 6 managers, run since Day 1"
        public CompanyBusiness[] Businesses = new CompanyBusiness[0];
        public string ResultLabel, Result;      // "Today's result", "+$1,240"
        public int ResultTone;
        public float[] Spark;                   // the days that exist, oldest first; null with fewer than two
        public string ResultNote;               // "Not yet: after day 2" where there is no history
        public CompanyQuestion Lead;
        public CompanyWaiting[] Waiting = new CompanyWaiting[0];
        public string WaitingCount;
        public CompanyDone[] Done = new CompanyDone[0];
        public string DoneNote;                 // what stands in place of an empty list
        public string TotalTakings, TotalCosts, TotalResult;
        public int TotalTone;
        public string[] CostLabels = new string[0];
        public string[] CostValues = new string[0];
        public string CostTotal;
        public CompanyManagerToday[] Managers = new CompanyManagerToday[0];
        public CompanyButton[] Tools = new CompanyButton[0];     // Open cleaning, Open your bank, the listing: only what is there
        public string Note;                     // a line under the right column (the stock market line)
        public bool PartOff;                    // the company's parts are off
    }

    /// <summary>One block of a card (a generic page): a section label, a row, a note, buttons, a switch, a stepper, a choice, grades or dated lines.</summary>
    public enum CompanyBlockKind : byte
    {
        Section = 0,    // CAPS label (Text: the label, Value: the figure or link at its right)
        Row,            // Text label, Value
        Link,           // Text label, Value as a link that does Act
        Note,           // Text, Tone
        Buttons,        // Buttons
        Switch,         // Text, Sub, On, Act
        Stepper,        // Text, Sub, Value, CanLess, CanMore, Act (less), Act2 (more), Act3 (typed)
        Choice,         // Text, Sub, Options, Chosen, Acts (one per option)
        Lines,          // Rows of when, what, money (a log)
        Field,          // Text label, Value the words now, Sub the hint, Act the commit (a typed name rides in S2)
        Chips,          // Options as a row of chips
        Meter           // Text, Number 0 to 100, Tone
    }

    /// <summary>A row of a card's list: a face or a tile, two lines, a figure, a chip, and what a press does.</summary>
    public struct CompanyItem
    {
        public string Title, Sub, Figure, Note, Chip;
        public UiRole ChipTone, FigureTone;
        public UiIcon Icon;
        public UiRole Tint;
        public float Hue;
        public bool Face;                       // a portrait in place of a tile
        public UiRole Ring;
        public int Grade;                       // 0 to 100 as a meter under the lines, or -1
        public CompanyAct Act;
        public bool Press;                      // the row is a target
        public string Id;                       // the row's own id, for the checks and the pick
    }

    /// <summary>One block of a card.</summary>
    public sealed class CompanyBlock
    {
        public CompanyBlockKind Kind;
        public string Text, Sub, Value;
        public UiRole Tone = UiRole.None;
        public bool On, CanLess = true, CanMore = true, Disabled;
        public string Why;
        public int Number = -1, Chosen;
        public string[] Options;
        public CompanyAct Act, Act2, Act3;
        public CompanyAct[] Acts;
        public CompanyButton[] Buttons;
        public CompanyDone[] Rows;
        public UiIcon Icon = UiIcon.None;
    }

    /// <summary>A generic detail page: a head (a portrait or a tile, a title, a line under it, chips) and an ordered list of blocks. The long tail of the app's pages is this.</summary>
    public sealed class CompanyCard
    {
        public string Kicker, Title, Sub;
        public UiIcon Icon = UiIcon.Store;
        public UiRole Tint = UiRole.None;
        public bool Face;                       // a portrait in place of the tile
        public float Hue;
        public UiRole Ring = UiRole.Ok;
        public string[] Chips = new string[0];
        public UiRole[] ChipTones = new UiRole[0];
        public string[] FactLabels = new string[0];     // up to four labelled figures at the head's right
        public string[] FactValues = new string[0];
        public CompanyBlock[] Blocks = new CompanyBlock[0];
        public CompanyButton[] Back;            // the way out of a sub page: "Back" and its target; null for none
        public string Empty;                    // what stands in the room when there is no card (nothing chosen)
    }

    /// <summary>A row of a left-hand list: a face or a tile, two lines, a figure, a chip, and the pick it makes.</summary>
    public struct CompanyRow
    {
        public string Id;
        public string Title, Sub, Figure, Note, Chip;
        public UiRole ChipTone, FigureTone;
        public UiIcon Icon;
        public UiRole Tint;
        public float Hue;
        public bool Face;
        public UiRole Ring;
        public bool Caption;                    // a label row (a group's), no target
        public bool Dim;
    }

    /// <summary>A left-hand list: its title, count, rows and the one chosen.</summary>
    public sealed class CompanyList
    {
        public string Title, Count;
        public CompanyRow[] Rows = new CompanyRow[0];
        public int Chosen = -1;
        public string Empty;
        public CompanyButton[] Tools = new CompanyButton[0];     // buttons under the title ("New plan")
        public string[] Filters = new string[0];                 // chips above the rows
        public int Filter;
    }

    /// <summary>A card and the list that picks it, with the words for an empty room.</summary>
    public sealed class CompanyPage
    {
        public bool PartOff;
        public string OffTitle, OffBody;
        public CompanyList List = new CompanyList();
        public CompanyCard Card;
        public CompanyCard Top;                 // a summary card over the list's page when nothing is picked (counts, the sick pay choice, the market note)
        public string Lead, LeadTitle;          // the page's own lead line (what needs the owner here)
    }
}
