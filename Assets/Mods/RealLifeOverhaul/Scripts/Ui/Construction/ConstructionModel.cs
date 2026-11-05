using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Construction app's page is: nothing yet, the site office's overview, a tab's table, or one thing's page.</summary>
    public enum ConstructionShape : byte
    {
        Off = 0,
        Overview,   // the builder's head, the next decision, the projects as stage tracks, and the cards of money, crews, planning and rivals
        Table,      // a tab's table with its head, and a card of what the tab is beside it
        Detail      // one thing's page: its head, its way back, and the cards of what it is and what to do
    }

    /// <summary>A project in the overview: where it stands on its stage track, what it has cost against its budget, and the word that says whether
    /// it waits on the player.</summary>
    public struct ConstructionProject
    {
        public UiIcon Icon;
        public string Title, Sub;
        public string[] StepLabels;      // the track's steps, the first at its left
        public int StepAt;               // the step in hand, or the steps' count for a finished one
        public int Progress;             // 0 to 100: the works so far, or -1 for no bar
        public string Spent, Of;         // "$312,000" and "of $498,000"
        public string Chip;
        public UiRole ChipTone;
        public int Run;                  // an index into the model's runs: opens the project
    }

    /// <summary>The decision the player is next asked for, as a band across the overview: what and where, what to do about it, and the buttons that do it.</summary>
    public sealed class ConstructionLead
    {
        public UiIcon Icon = UiIcon.Building;
        public string Kicker, Title, Body;
        public UiActionModel[] Actions = new UiActionModel[0];
    }

    /// <summary>One dated line of the week's list: the day, what happens on the sites, and what it costs or pays.</summary>
    public struct ConstructionDay
    {
        public string When, What, Amount;
        public UiRole AmountInk;
        public int Run;
    }

    /// <summary>
    /// What the Construction app shows, after the kit's app window: the sections down its sidebar, and a page of the shape given. Plain words and
    /// figures with the actions as runs (what a press does is the source's), so a state can be drawn and walked offline.
    /// </summary>
    public sealed class ConstructionModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Building;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public ConstructionShape Shape;
        public string OffTitle, OffBody, OffAction;
        public UiPrompt[] Prompts = new UiPrompt[0];

        // The head of a page: a glyph (or the firm's two letters) on a tile, a title, a line under it and up to four figures at the right.
        public UiIcon HeadIcon = UiIcon.Building;
        public string HeadMark;
        public string HeadTitle, HeadSub;
        public string[] FigLabels = new string[0], FigValues = new string[0];
        public UiRole[] FigInks = new UiRole[0];

        public ConstructionLead Lead;

        // The overview.
        public string RowsTitle, RowsLink, RowsEmpty;
        public int RowsLinkRun = -1;
        public ConstructionProject[] Projects = new ConstructionProject[0];
        public UiFactCardModel[] Side = new UiFactCardModel[0];     // the cards at the right: the money on the jobs, the crews, the planning, the rival builders
        public string WeekTitle;
        public ConstructionDay[] Week = new ConstructionDay[0];

        // A tab's table, and the card beside it.
        public UiDataTableModel Table;
        public string[] RowKeys = new string[0];                    // by row of the table: what a press picks
        public UiFactCardModel Card;                                // beside the table: what the tab is and what to do first
        public UiFactCardModel Foot;                                // under the table: what the tab's rows say together (the land's asks by district)
        public string TableLink;
        public int TableLinkRun = -1;
        public UiActionModel[] TableActions = new UiActionModel[0];
        public int FilterKinds;                                     // how many kinds of row the table's filter steps through (0 for no filter)

        // One thing's page.
        public string DetailTitle, DetailSub, DetailChip, DetailLocate, Back;
        public UiIcon DetailIcon = UiIcon.Building;
        public UiRole DetailChipTone = UiRole.Ok;
        public int DetailLocateRun = -1;
        public UiTrackModel Track;
        public UiDataTableModel DetailTable;                        // a table over the cards of a page that compares things (the builders' quotes), its rows picked by RowKeys
        public UiFactCardModel[] Cards = new UiFactCardModel[0];    // in the order read: the face stands them in columns
        public UiActionModel[] Actions = new UiActionModel[0];      // what to do, the main one last, over the cards
        public int Dropped;                                         // written back by the layout: lines it had no room for, which a check holds at nothing

        public Func<bool>[] Runs = new Func<bool>[0];

        // Written back by the layout, so a wheel and a focus reach the table they belong to.
        public short TableClip = -1, TableList = -1;
    }

    /// <summary>Where the screen stands in what it shows: how far its table is scrolled, how it is ordered and filtered.</summary>
    public sealed class ConstructionView
    {
        public float Offset;
        public int Reveal = -1;
        public int Sort = -1;
        public bool SortDown;
        public int Filter = -1;       // the kind of row the table shows, or -1 for all
    }

    /// <summary>
    /// The runs of a page: what each button, link and step does, collected as the page is written and given to the model as an array, so a press is an
    /// index. A step takes two runs in a row (less, then more). Game-free; the source decides what a run does.
    /// </summary>
    public sealed class ConstructionRuns
    {
        private readonly System.Collections.Generic.List<Func<bool>> _list = new System.Collections.Generic.List<Func<bool>>(16);

        public int Count => _list.Count;

        public int Add(Func<bool> run)
        {
            _list.Add(run ?? (() => false));
            return _list.Count - 1;
        }

        /// <summary>A run that does something and always counts as done (an action of the director: it tells its own refusal).</summary>
        public int Do(Action run)
        {
            return Add(() =>
            {
                run();
                return true;
            });
        }

        public UiActionModel Button(string label, UiButtonKind kind, UiIcon icon, UiAction key, Action run, bool disabled = false)
        {
            return new UiActionModel
            {
                Label = label, Kind = kind, Icon = icon, Key = key, Command = ConstructionFace.ActionCommand, Arg = disabled ? -1 : Do(run), Disabled = disabled
            };
        }

        public UiFactLine Link(string words, Action run)
        {
            return UiFactLine.Go(words, ConstructionFace.ActionCommand, Do(run));
        }

        public UiFactLine Switch(string label, bool on, Action<bool> set, bool disabled = false)
        {
            return UiFactLine.Toggle(label, on, ConstructionFace.ActionCommand, Do(() => set(!on)), disabled);
        }

        public UiFactLine Step(string label, string value, string hint, bool canLess, bool canMore, Action less, Action more)
        {
            int at = Do(less);
            Do(more);
            return UiFactLine.Amount(label, value, hint, canLess, canMore, ConstructionFace.StepLess, ConstructionFace.StepMore, at);
        }

        public Func<bool>[] ToArray() => _list.ToArray();
    }
}
