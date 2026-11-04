using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a press on the work apps does, by app: each app's page names the action behind a button or a row and the live source carries it
    /// to the director that already owns it. A command number of the apps' own: the kit's numbers are not unique across parts, so every
    /// handler tests the page it belongs to first (WorkScreen).
    /// </summary>
    public struct WorkDo
    {
        public int Kind;          // the app's own action number; 0 for none
        public int Arg;           // a row, a step or a post
        public string Why;        // not null: the action is drawn disabled, and this is the reason
        public bool Opens;        // the action only opens a page or a place (the checks accept it as acting through a changed view)

        public static WorkDo None => default(WorkDo);
        public bool Any => Kind != 0;
    }

    /// <summary>What the player has chosen on the apps' pages, owned by the runner and read by the source: the tab, a row, a filter and a sub page.</summary>
    public sealed class WorkView
    {
        public int Tab;
        public int Row = -1;      // the row the detail shows, by its place in the page's table
        public int Filter;        // a filter chip chosen
        public int Sub;           // a sub page (a person's page in Open Roles, a course in the diary); 0 for none
        public int Pick;          // an id the page carries from one tab to another (a post, a course)
        public int Amount;        // an amount stepped (an offer, a haggle)

        public void Reset()
        {
            Row = -1;
            Filter = 0;
            Sub = 0;
            Amount = 0;
        }
    }

    /// <summary>
    /// One page of a work app as plain data: the app window's model with, beside each thing that can be pressed, what pressing it does. Game-free.
    /// A source builds a fresh one for a tab every time (never reusing the last tab's), so a tab cannot show another's parts.
    /// </summary>
    public sealed class WorkPage
    {
        public readonly UiAppModel Model = new UiAppModel();
        public WorkDo[] Acts = new WorkDo[0];       // by the model's Actions
        public WorkDo[] Rows = new WorkDo[0];       // by the first table's rows
        public WorkDo[] MoreRows = new WorkDo[0];   // by the second table's rows
        public WorkDo[] People = new WorkDo[0];     // by the people
        public WorkDo[] GridActs = new WorkDo[0];   // by the catalogue detail's buttons
        public WorkDo All;                          // the link under the people
        public WorkDo Note;                         // the link under the note
        public int Chosen = -1;                     // the table's row the detail shows
        public bool Back;                           // a sub page: Back steps out of it
        public Action<UiScene, UiAt, float, float, float, float, IUiKeys> Body;   // a page laid out by hand from the kit's blocks, in the room the window gives (x, y, width, bottom)
        public string Empty;                        // a sentence for a page that holds nothing yet (shown as the lead when the page has no head)

        public bool Has(WorkDo[] list, int i) => list != null && i >= 0 && i < list.Length && list[i].Any;
    }

    /// <summary>What a work app's screen needs from its source: a generation,
    /// the sections and a page for a tab. The live source reads the game; the preview's reads a sample.</summary>
    public interface IWorkSource
    {
        string Id { get; }
        string Title { get; }
        UiIcon Icon { get; }
        int Generation { get; }
        bool Across { get; }

        /// <summary>A text by its key, the English standing in where the key is new (the offline checks and the previews read the English).</summary>
        string T(string key, string english);

        /// <summary>The sections, in order, with their counts.</summary>
        UiNavEntry[] Nav(WorkView view);

        /// <summary>A fresh page for the view's tab.</summary>
        WorkPage Page(WorkView view);

        /// <summary>Carries an action to its director; true when it did something or opened something.</summary>
        bool Do(WorkDo act, WorkView view);

        /// <summary>A tab a notice or a link asked for (-1 for none), taken once.</summary>
        int TakeWanted();
    }
}
