using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// Career, Campus, Open Roles and the Marketplace on the kit: every tab of each, as the apps read their records. A new player (no job, nothing
/// studied, no ads, nothing bought) and an established one, a long name, a full list and an empty one, a filter chosen, a long language. The
/// names are the sample's own; the figures agree with each other (a count is the rows it counts). Plain English.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] CareerStates = { "professions", "jobs", "jobs_qualify", "applications", "my_job", "management", "own", "cv", "my_job_new", "jobs_empty",
        "jobs_long", "my_job_desk" };
    public static readonly string[] CampusStates = { "courses", "courses_full", "courses_cooking", "studying", "results", "skills", "family", "schools", "results_new", "studying_none",
        "family_none" };
    public static readonly string[] RolesStates = { "jobs", "applicants", "applicants_none", "market", "search", "team", "person", "new_ad", "person_contract" };
    public static readonly string[] MarketplaceStates = { "listings", "listings_machines", "listings_stock", "listings_none", "haggle", "yours", "offers", "freelance",
        "listings_pt" };

    /// <summary>
    /// A work app's source for a sample: the page of a tab, built from the sample's facts. A press goes to <see cref="Done"/>, which records it, and
    /// is answered true only when it arrives as the kind that was asked for (<see cref="Expect"/>): an action that reaches the source as another
    /// kind fails, as the live source's would do.
    /// </summary>
    internal sealed class SampleWork : IWorkSource
    {
        private readonly string _id, _title;
        private readonly UiIcon _icon;
        private readonly bool _across;
        private readonly UiNavEntry[] _nav;
        private readonly System.Func<WorkView, WorkPage> _page;
        private readonly UiWords _t;
        public readonly List<WorkDo> Done = new List<WorkDo>();
        public WorkDo? Expect;
        public int Mismatch;

        public SampleWork(string id, string title, UiIcon icon, bool across, UiNavEntry[] nav, UiWords t, System.Func<WorkView, WorkPage> page)
        {
            _id = id; _title = title; _icon = icon; _across = across; _nav = nav; _t = t; _page = page;
        }
        public string Id => _id;
        public string Title => _title;
        public UiIcon Icon => _icon;
        public int Generation => 0;
        public bool Across => _across;
        public string T(string key, string english) => _t(key, english);
        public UiNavEntry[] Nav(WorkView view) => _nav;
        public WorkPage Page(WorkView view) => _page(view);
        public bool Do(WorkDo act, WorkView view)
        {
            Done.Add(act);
            if (Expect.HasValue && (Expect.Value.Kind != act.Kind || Expect.Value.Arg != act.Arg))
            {
                Mismatch++;
                return false;
            }
            return true;
        }
        public int TakeWanted() => -1;
    }

    private static string[] WkOrder(string app) => app == "career" ? new[] { "professions", "jobs", "applications", "my_job", "management", "own", "cv" }
        : app == "campus" ? new[] { "courses", "studying", "results", "skills", "family", "schools" }
        : app == "roles" ? new[] { "jobs", "applicants", "market", "search", "team" } : new[] { "listings", "yours", "offers", "freelance" };

    private static int WkTabOf(string app, string state)
    {
        string s = state;
        foreach (string suffix in new[] { "_desk", "_new", "_empty", "_long", "_full", "_none", "_machines", "_stock", "_cooking", "_qualify", "_contract" })
        {
            if (s.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                s = s.Substring(0, s.Length - suffix.Length);
            }
        }
        if (s == "person" || s == "new_ad")
        {
            return app == "roles" && s == "person" ? 4 : 0;
        }
        if (s == "haggle")
        {
            return 0;
        }
        int at = System.Array.IndexOf(WkOrder(app), s);
        return at < 0 ? 0 : at;
    }

    private static UiNavEntry WkNav(UiIcon icon, string label, string count = null) => new UiNavEntry { Icon = icon, Label = label, Count = count };

    private static WorkAct WkAct(int kind, string label, UiButtonKind style, UiIcon icon, string why = null, bool opens = false, int arg = 0)
    {
        return new WorkAct { Kind = kind, Label = label, Style = style, Icon = icon, Why = why, Opens = opens, Arg = arg };
    }

    private static UiDataColumn WkCol(string title, UiCellKind kind, float width = 0f, bool graded = false) => WorkBuilder.Col(title, kind, width, graded);
}
