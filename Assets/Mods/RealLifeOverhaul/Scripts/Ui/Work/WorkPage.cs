using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a press on the work apps does, by app: each app's page names the action behind a button or a row and the live source carries it
    /// to the director that already owns it. A command number of the apps' own: the kit's numbers are not unique across parts, so every
    /// handler tests the page it belongs to first (WorkScreen), and each app's actions start at 100, apart from the few the screen itself takes
    /// (<see cref="WorkKinds"/>, 1 to 4), so no part's action is ever taken for another's.
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

    /// <summary>What the player has chosen on the apps' pages, owned by the runner and read by the source: the tab, a row, a filter, a search, a sub page and the amounts stepped.</summary>
    public sealed class WorkView
    {
        public const int AmountsMost = 16;
        public int Tab;
        public int Row = -1;      // the row the detail shows, by its place in the page's table
        public int Filter;        // a filter chip chosen
        public int Sub;           // a sub page (a person's page in Open Roles, a haggle); 0 for none
        public int Pick;          // an id the page carries from one tab to another (a post, a course) or into a sub page
        public int Aside;         // the role chosen in a squad page's list
        public int PageTab;       // a person page's tab: File, Career, Contract
        public int Day;           // the diary's chosen day
        public int Month;         // the diary's month, turned from today's
        public string Query = string.Empty;   // what the search field holds
        public string Text = string.Empty;    // what a typed field of a form holds (a new name)
        public float Offset;      // how far the page's list or grid has scrolled
        public int Reveal = -1;   // a row or card scrolled into view on the next layout
        public readonly int[] Amount = new int[AmountsMost];
        private int _set;         // bit i: Amount[i] has been stepped, and no longer follows the source's start

        public void Reset()
        {
            Row = -1;
            Filter = 0;
            Sub = 0;
            Aside = 0;
            PageTab = 0;
            Day = 0;
            Month = 0;
            Query = string.Empty;
            Text = string.Empty;
            Offset = 0f;
            Reveal = -1;
            _set = 0;
        }

        /// <summary>An amount as the player left it, or the source's start where it has not been stepped.</summary>
        public int Get(int id, int start) => id >= 0 && id < AmountsMost && (_set >> id & 1) != 0 ? Amount[id] : start;

        public void Set(int id, int value)
        {
            if (id >= 0 && id < AmountsMost)
            {
                Amount[id] = value;
                _set |= 1 << id;
            }
        }

        public void Forget(int id)
        {
            if (id >= 0 && id < AmountsMost)
            {
                _set &= ~(1 << id);
            }
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
        public WorkDo[] Rows = new WorkDo[0];       // by the table's or the catalogue's rows: a press that opens a page
        public WorkDo[] Locates = new WorkDo[0];    // by the table's rows: the link to the map at the row's right
        public WorkDo[] Asides = new WorkDo[0];     // by the aside's rows
        public WorkDo[] Entries = new WorkDo[0];    // by the diary's entries
        public WorkDo Empty;                        // what an empty page's button does
        public WorkAmount[] Amounts = new WorkAmount[0];
        public UiDataTableModel Table;              // the squad page's table, for its offset and its reveal
        public WorkCardsModel Cards;                // the catalogue page's
        public UiDiaryModel Diary;                  // the diary page's
        public UiReaderModel Reader;                // the reader page's
        public int Chosen = -1;                     // the row the detail shows
        public bool Back;                           // a sub page: Back steps out of it
        public bool Typing;                         // the page has a search field
        public string Why;                          // the reasons its disabled buttons gave, said under them
        public Action<UiScene, UiAt, float, float, float, float, IUiKeys> Body;   // a page laid out by hand from the kit's blocks, in the room the window gives (x, y, width, bottom)

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

        /// <summary>Carries an action to its director; true when it did something or opened something. The view holds what the player stepped.</summary>
        bool Do(WorkDo act, WorkView view);

        /// <summary>A tab a notice or a link asked for (-1 for none), taken once.</summary>
        int TakeWanted();
    }
}
