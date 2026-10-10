using Helpers;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The player's own notes (PlannerStore.Notes).</summary>
    public sealed class NoteSource : IPlannerSource
    {
        public const string SourceId = "note";

        public string Id => SourceId;
        public bool Available => PlannerStore.Loaded;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int i = 0; i < PlannerStore.Notes.Count; i++)
            {
                PlannerNote note = PlannerStore.Notes[i];
                PlannerEntry e = into.Add(SourceId, note.Id, 0, PlannerCategory.Note, note.Day);
                if (e == null)
                {
                    continue;
                }
                e.Own = true;
                e.Done = note.Done;
                e.Start = note.Start;
                e.Street = note.Street;
                e.Number = note.Number;
                if (into.WantText)
                {
                    e.Title = note.Title;
                    e.Where = note.Place;
                }
            }
        }
    }

    /// <summary>
    /// The test button's samples: one marked entry of every category, today and tomorrow, so that every row, icon and
    /// state can be looked at without living through a week. They are not in the save and go with the session or the
    /// other test button.
    /// </summary>
    public sealed class SampleSource : IPlannerSource
    {
        public string Id => "sample";
        public bool Available => PlannerDirector.Samples;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            int today = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            for (int c = 0; c < (int)PlannerCategory.Note; c++)
            {
                // Spread round the clock from two hours ago: some are over or missed, one is on now, two clash, the rest are to come.
                int at = hour - 2 + c;
                PlannerEntry e = into.Add(Id, c, 0, (PlannerCategory)c, at >= 24 ? today + 1 : today);
                if (e == null)
                {
                    continue;
                }
                at = ((at % 24) + 24) % 24;
                e.Start = at * 60;
                e.End = e.Start + (c == 2 ? 120 : 60);
                e.Attend = c % 2 == 0;
                e.Deadline = false;
                if (into.WantText)
                {
                    string mark = PlannerText.T("reallife_planner_sample_mark");
                    e.Title = mark + " " + PlannerText.T(PlannerText.CategoryKey((PlannerCategory)c));
                    e.Where = PlannerText.T("reallife_planner_sample_where");
                    e.What = PlannerText.T("reallife_planner_sample_what");
                    e.Money = c == (int)PlannerCategory.Work ? PlannerText.T("reallife_planner_pays", "amount", PlannerText.Money(64f)) : null;
                    e.IfMissed = e.Attend ? PlannerText.T("reallife_planner_sample_missed") : null;
                }
            }
        }
    }

    /// <summary>
    /// The base game's tax bill (Helpers.TaxHelper): the day it is due, what is owed, and what the game does the day
    /// after (TaxHelper.RunDaily: the amount is taken by force, belongings included).
    /// </summary>
    public sealed class TaxSource : IPlannerSource
    {
        public string Id => "tax";
        public bool Available => SaveGameManager.Current != null && SaveGameManager.Current.currentUnpaidTaxes != null;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            if (!TaxHelper.HasCurrentTaxesToPay())
            {
                return;
            }
            int due = TaxHelper.GetCurrentTaxesDueDay();
            PlannerEntry e = into.Add(Id, due, 0, PlannerCategory.Money, due);
            if (e == null)
            {
                return;
            }
            if (into.WantText)
            {
                e.Title = PlannerText.T("reallife_planner_tax_title");
                e.What = PlannerText.T("reallife_planner_tax_what");
                e.Money = PlannerText.T("reallife_planner_costs", "amount", PlannerText.Money(TaxHelper.GetCurrentTaxesToPay()));
                e.IfMissed = PlannerText.T("reallife_planner_tax_missed");
            }
        }
    }
}
