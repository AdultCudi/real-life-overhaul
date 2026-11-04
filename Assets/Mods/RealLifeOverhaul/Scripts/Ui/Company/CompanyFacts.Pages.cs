using System;

namespace RealLifeOverhaul.Ui
{
    // The tabs with a shape of their own (the mockups): the managers' tree and profile, the rota's week and day, tax and licences.

    /// <summary>A switch of a manager's decisions: what they decide and who decides it now.</summary>
    public struct CompanyTask
    {
        public string Id;
        public UiIcon Icon;
        public string Title, Line;
        public bool On, Disabled;
        public string Who;                      // the word at the switch's left: the manager's first name, or "You"
        public string Why;
        public CompanyAct Act;
        public CompanyAct Open;                 // the task's own page (its limits)
    }

    /// <summary>A limit of a manager: its words, its value and the two steps.</summary>
    public struct CompanyLimit
    {
        public string Title, Sub, Value;
        public bool CanLess, CanMore, Disabled;
        public string Why;
        public CompanyAct Less, More;
        public CompanyAct Typed;                // the act a figure typed into the field does: the director reads the typed figure from C
    }

    /// <summary>The page of the one chosen in the managers' tree: a manager, an executive, the owner, a crew member or an open post.</summary>
    public sealed class CompanyProfile
    {
        public string Id;
        public string Name, Role, Status;
        public UiRole StatusTone = UiRole.Ok;
        public float Hue;
        public UiRole Ring = UiRole.Ok;
        public string[] FactLabels = new string[0];
        public string[] FactValues = new string[0];
        public string WaitingText;              // "1 question waiting for you" over its line; null for none
        public string WaitingLine;
        public CompanyButton Waiting;
        public string TasksTitle, TasksHint;
        public CompanyTask[] Tasks = new CompanyTask[0];
        public string LimitsTitle;
        public CompanyLimit[] Limits = new CompanyLimit[0];
        public string RouteTitle, RouteSub;     // "Questions Ray cannot decide go to", "Meera Iyer picks up what you leave for a day"
        public string[] RouteOptions = new string[0];
        public int RouteChosen;
        public CompanyAct[] RouteActs = new CompanyAct[0];
        public string GradesTitle;
        public string[] GradeLabels = new string[0];
        public int[] GradeValues = new int[0];
        public string LogTitle, LogAll;
        public CompanyDone[] Log = new CompanyDone[0];
        public CompanyAct LogOpen;
        public string CrewTitle, CrewCount;
        public CompanyItem[] Crew = new CompanyItem[0];
        public CompanyButton[] Buttons = new CompanyButton[0];
        public string Note;
        public CompanyBlock[] More = new CompanyBlock[0];       // what a kind of node has besides (an executive's area figures, an open post's candidates)
    }

    /// <summary>The managers tab's facts: the tree of who decides what, and the page of the one chosen.</summary>
    public sealed class CompanyManagersFacts
    {
        public bool PartOff;
        public string OffTitle, OffBody;
        public UiOrgNode[] Tree = new UiOrgNode[0];     // every row, in order, with the levels (the folds are the view's)
        public string[] TreeIds = new string[0];        // each row's id
        public string TreeTitle, TreeAction, TreeFoot;
        public CompanyAct TreeFootAct;
        public string[] Filters = new string[0];        // "All", "Needs you", "Open posts"
        public string[] FilterIds = new string[0];      // on the policy centre: the node each scope chip picks
        public string Chosen;                           // the id of the row the page shows
        public CompanyProfile Profile;                  // null while nothing is chosen
        public CompanyPage Sub = new CompanyPage();     // the page of a sub page (the inbox, the policy centre, the dashboards, the seats...): a list and a card
        public string Empty;
        public bool Deep;
        public CompanyButton DeepSwitch;                // the owner's Deep Mode
        public CompanyBlock[] Summary = new CompanyBlock[0];    // what the page says with nothing chosen: the month's figures, what needs the owner
    }

    /// <summary>A person of the rota: their name and role and their shift on each day.</summary>
    public struct CompanyRotaPerson
    {
        public string Name, Role, Hours;
        public float Hue;
        public UiRole Ring, HoursTone;
        public string[] Shifts;                 // seven: "07-15", or null for a day off
        public UiRole[] Tones;                  // seven: None, Warn for a shift that breaks a demand
    }

    /// <summary>A rung of the planner's ladder: what the plan comes to, and the button that uses it.</summary>
    public struct CompanyRung
    {
        public string Name, Line, Hours, Net, Gain;
        public bool Advised;
        public CompanyButton Use;
        public string[] Needs;
    }

    /// <summary>A place's rota: the plan form, the week by person and day, the day hour by hour, the needs, and the planner's advice.</summary>
    public sealed class CompanyRotaPlace
    {
        public string Id, Name, Sub;
        public string Refused;                              // why there is no rota to show (a warehouse, no stations, unread)
        public string Weekly;                               // "Week 31, open 07:00 to 23:00"
        // The plan form.
        public string[] PlanNames = new string[0];
        public int PlanChosen;
        public CompanyAct[] PlanActs = new CompanyAct[0];   // choosing the plan in use by name
        public string[] RepeatOptions = new string[0];
        public int Repeat;
        public CompanyAct[] RepeatActs = new CompanyAct[0];
        public string HoursValue;
        public bool HoursLess, HoursMore;
        public CompanyAct HoursLessAct, HoursMoreAct, HoursTypeAct;
        public bool Overtime;
        public CompanyAct OvertimeAct;
        public CompanyButton Copy, Fill, Back, Edit;
        public string[] ModeOptions = new string[0];
        public int Mode;
        public CompanyAct[] ModeActs = new CompanyAct[0];
        // The week.
        public string[] DayNames = new string[7];
        public int Day;                                     // the day in view
        public CompanyRotaPerson[] People = new CompanyRotaPerson[0];
        public string[] DayTotals = new string[7];
        public string TotalHours;
        public string[] SummaryLabels = new string[0];
        public string[] SummaryValues = new string[0];
        public UiRole[] SummaryTones = new UiRole[0];
        // The day, hour by hour.
        public string DayTitle, DayNote;
        public string[] HourLabels = new string[0];
        public int[] OnShift = new int[0];
        public int[] Needed = new int[0];
        public string[] Legend = new string[0];
        public UiBoardModel Board;                          // the day as lanes of people on an hour ruler
        // The needs by time block.
        public string NeedsTitle;
        public string[] NeedWhen = new string[0];
        public string[] NeedWho = new string[0];
        public string[] NeedsNote;
        // The planner's ladder.
        public string AdviceTitle, AdviceNote;
        public CompanyRung[] Rungs = new CompanyRung[0];
        public CompanyBlock[] Beside = new CompanyBlock[0];     // your people, stations that would pay, stations out of order, goods that run out
    }

    /// <summary>The rota tab's facts: places with their plans, and the chosen place's page.</summary>
    public sealed class CompanyRotaFacts
    {
        public bool PartOff;
        public string OffTitle, OffBody;
        public CompanyList Places = new CompanyList();     // a place's row, then its plans as dim rows: ids "p:<place>" and "pl:<place>:<plan>"
        public CompanyRotaPlace Place;
        public CompanyCard Edit;                            // the plan being edited (SubEdit), a generic card
    }

    /// <summary>A licence or permit as a row of the tax tab's table: what, where, who handles it, how it stands and what to do.</summary>
    public struct CompanyLicence
    {
        public string Id;
        public UiIcon Icon;
        public string Name, Place, Manager;
        public float ManagerHue;
        public string State;
        public UiRole StateTone;
        public string Day;
        public CompanyButton Action;
        public bool HasAction;
        public CompanyAct Open;
    }

    /// <summary>A line of the tax statement: a label and its figure, bold for a subtotal.</summary>
    public struct CompanyStatementLine
    {
        public string Label, Value;
        public bool Strong;
        public UiRole Tone;
    }

    /// <summary>The tax and licences tab's facts: what needs the owner, the licences, what is coming up, the quarter so far, the returns paid and the property tax.</summary>
    public sealed class CompanyTaxFacts
    {
        public bool PartOff;
        public string OffTitle, OffBody;
        // The lead: a manager or the finance chief's question about the reserve.
        public bool HasLead;
        public string LeadKicker, LeadTitle, LeadBody;
        public string LeadAsker;
        public float LeadHue;
        public CompanyButton[] LeadButtons = new CompanyButton[0];
        // The licences.
        public string LicencesTitle, LicencesCount;
        public CompanyLicence[] Licences = new CompanyLicence[0];
        public string LicencesEmpty;
        public string[] LicenceColumns = new string[0];
        // Coming up.
        public CompanyDone[] ComingUp = new CompanyDone[0];
        // The quarter so far.
        public string QuarterTitle, QuarterRange;
        public CompanyStatementLine[] Quarter = new CompanyStatementLine[0];
        public CompanyBlock[] ReserveControls = new CompanyBlock[0];    // what the owner does with the reserve: the figure to set aside, the finance chief's top-up switch
        // Paid this year.
        public string PaidTitle;
        public CompanyItem[] Paid = new CompanyItem[0];
        public string PaidEmpty;
        // The tools: the return, the accountant, the cash off the books.
        public string ToolsTitle;
        public CompanyItem[] Tools = new CompanyItem[0];
        // Property tax.
        public string PropertyTitle, PropertyNote;
        public CompanyStatementLine[] Property = new CompanyStatementLine[0];
        public CompanyCard Card;                                        // a sub page's card (the return, the accountant, a business's cash, a licence)
    }

    /// <summary>Everything the Company app can show, as plain data. Only the tab in view is filled; the rest stay as reset.</summary>
    public sealed class CompanyFacts
    {
        public bool Ready;                      // the feature runs and a save is loaded
        public string OffTitle, OffBody;
        public string Name, Mark;               // the company's name, its initials on the mark
        public string Today;                    // "Mon, Day 214" at the window's right
        public string[] Counts = new string[CompanyView.Tabs];   // the sidebar's counts, null for none
        public CompanyOverviewFacts Overview = new CompanyOverviewFacts();
        public CompanyPage Staff = new CompanyPage();
        public CompanyManagersFacts Managers = new CompanyManagersFacts();
        public CompanyRotaFacts Rota = new CompanyRotaFacts();
        public CompanyPage Marketing = new CompanyPage();
        public CompanyPage Cover = new CompanyPage();
        public CompanyTaxFacts Tax = new CompanyTaxFacts();
        public CompanyCard Business;            // a business's page (Overview, SubBusiness)
        public CompanyQuestion Question;        // the escalation open as a card, when one is
        public bool Deep;                       // Deep Mode: the policy centre and the levels are in play
        public string GuideNote;                // the guide's link under a page: its words
    }
}
