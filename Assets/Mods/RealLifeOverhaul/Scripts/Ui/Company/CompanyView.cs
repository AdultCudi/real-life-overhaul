using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the player has chosen on the Company app: the tab, what is picked on each, the sub page in view, the question open as a card and the
    /// figure being typed. The facts are read for exactly this, so each tab builds its own page from a reset model and no tab can show another's.
    /// </summary>
    public sealed class CompanyView
    {
        public const int Tabs = 7;
        public const int Overview = 0, Staff = 1, Managers = 2, Rota = 3, Marketing = 4, Cover = 5, Tax = 6;

        // Sub pages: 0 is a tab's own page; the others are named by the tab that has them.
        public const int SubMain = 0;
        public const int SubBusiness = 1;                                   // Overview: a business's page
        public const int SubStrike = 1, SubOvertime = 2;                    // Staff: a business's strike, its overtime offer
        public const int SubAppoint = 1, SubDecision = 2, SubTask = 3, SubInbox = 4, SubPolicy = 5, SubRule = 6, SubTemplates = 7, SubTemplate = 8,
            SubDashboard = 9, SubRegion = 10, SubSeat = 11, SubEscalation = 12, SubVacant = 13;   // Managers
        public const int SubDay = 1, SubAdvice = 2, SubEdit = 3;            // Rota: the day, the planner's ladder, a plan's needs
        public const int SubReport = 1, SubBook = 2;                        // Marketing
        public const int SubClaim = 1, SubPolicyCard = 2;                   // Cover
        public const int SubReturn = 1, SubAccountant = 2, SubHide = 3, SubLicence = 4, SubReserve = 5;   // Tax

        public int Tab;
        public readonly string[] Pick = new string[Tabs];      // what is chosen on each tab ("" for nothing): an id the live source knows
        public readonly int[] Sub = new int[Tabs];             // the sub page of each tab
        public readonly string[] SubPick = new string[Tabs];   // what is chosen in the list of a sub page ("" for nothing); the tab's own pick stays what the sub page is about
        public readonly int[] SubArg = new int[Tabs];          // the figure a sub page was opened with (the task, the seat, the rule)
        public int Question;                                   // the escalation open as a decision card; 0 for none
        public int Choice;                                     // the choice chosen on the Lead or the card
        public string Typing;                                  // the figure or name being typed; null for none
        public int TypingKind;                                 // CompanyTyping
        public string TypingId;                                // what the typed text is for (a plan's id)
        public int Day;                                        // the day of the rota in view, 0 to 6
        public int Amount;                                     // the figure to set aside in the tax reserve; 0: what is still to set aside
        public int Insurer;                                    // the insurer chosen on a policy's page
        public int Kind;                                       // the campaign kind or the accountant's tier chosen on its page
        public int Filter;                                     // the filter chosen over the list of a tab
        public int Armed = -1;                                 // a destructive button's index waiting for its second press
        public string ArmedId;
        public bool Folded;                                    // the managers' tree folded to its top rows
        public readonly System.Collections.Generic.HashSet<int> Later = new System.Collections.Generic.HashSet<int>();   // questions put off ("Decide later"): the Lead asks the next one

        // The tree's folds, by node id, kept across reads.
        public readonly System.Collections.Generic.HashSet<string> FoldedNodes = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        public CompanyView()
        {
            for (int i = 0; i < Tabs; i++)
            {
                Pick[i] = string.Empty;
                SubPick[i] = string.Empty;
            }
        }

        public string Picked => Pick[Tab] ?? string.Empty;

        /// <summary>The sub page of the tab in view.</summary>
        public int Page => Sub[Tab];

        /// <summary>A copy of this view's choices, for the checks.</summary>
        public CompanyView Copy()
        {
            var v = new CompanyView { Tab = Tab, Question = Question, Choice = Choice, Typing = Typing, TypingKind = TypingKind, TypingId = TypingId, Day = Day, Amount = Amount, Insurer = Insurer, Kind = Kind,
                Filter = Filter, Armed = Armed, ArmedId = ArmedId, Folded = Folded };
            Array.Copy(Pick, v.Pick, Tabs);
            Array.Copy(Sub, v.Sub, Tabs);
            Array.Copy(SubPick, v.SubPick, Tabs);
            Array.Copy(SubArg, v.SubArg, Tabs);
            foreach (string f in FoldedNodes)
            {
                v.FoldedNodes.Add(f);
            }
            foreach (int l in Later)
            {
                v.Later.Add(l);
            }
            return v;
        }
    }

    /// <summary>What a typed field is for.</summary>
    public static class CompanyTyping
    {
        public const int None = 0, Amount = 1, PlanName = 2;
        public const int PlanNameField = 0;   // the plan figure that is its name (the director's PlanName)
        public const int NameMost = 24;      // letters a plan's name takes (RotaPlanRules.NameMost)
    }

    /// <summary>Where the layout put each scrolling block and each target's action, so the wheel, a focus and a press reach what they belong to. Written back by every layout.</summary>
    public sealed class CompanyLayout
    {
        public const int Slots = 6;   // the list, the tree, the card, the page's own scroll, the grid, the log
        public readonly float[] Offset = new float[Slots];
        public readonly int[] Reveal = { -1, -1, -1, -1, -1, -1 };
        public readonly short[] List = { -1, -1, -1, -1, -1, -1 };
        public readonly short[] Clip = { -1, -1, -1, -1, -1, -1 };

        public const int SlotList = 0, SlotTree = 1, SlotCard = 2, SlotPage = 3, SlotGrid = 4, SlotLog = 5;

        /// <summary>The actions of the page as laid out, by the index a target carries; the main and second ones that the keys run.</summary>
        public CompanyButton[] Acts = new CompanyButton[64];
        public int ActCount;
        public int Main = -1, Second = -1;

        /// <summary>The list's rows' ids, so a press carries an index and the screen finds its id.</summary>
        public string[] RowIds = new string[0];
        public string[] TreeIds = new string[0];
        public CompanyAct[] RowActs = new CompanyAct[0];

        public void Reset()
        {
            for (int i = 0; i < Slots; i++)
            {
                List[i] = -1;
                Clip[i] = -1;
            }
            ActCount = 0;
            Main = Second = -1;
        }

        /// <summary>Registers an action and returns the index its target carries.</summary>
        public int Add(CompanyButton b)
        {
            if (ActCount == Acts.Length)
            {
                Array.Resize(ref Acts, Acts.Length * 2);
            }
            Acts[ActCount] = b;
            if (b.Key == UiAction.Alt && Main < 0)
            {
                Main = ActCount;
            }
            else if (b.Key == UiAction.Option && Second < 0)
            {
                Second = ActCount;
            }
            return ActCount++;
        }

        public int SlotOfClip(int clip)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (clip >= 0 && Clip[i] == clip)
                {
                    return i;
                }
            }
            return -1;
        }

        public int SlotOfList(int list)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (list >= 0 && List[i] == list)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
