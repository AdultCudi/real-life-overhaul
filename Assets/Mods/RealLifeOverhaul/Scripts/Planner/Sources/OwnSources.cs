using Helpers;

namespace RealLifeOverhaul.Planner
{
    /// <summary>The player's own notes: a repeating one on each day it falls on, as (note, number, day).</summary>
    public sealed class NoteSource : IPlannerSource
    {
        public const string SourceId = PlannerStore.NoteSourceId;

        public string Id => SourceId;
        public bool Available => PlannerStore.Loaded;

        public void Collect(int fromDay, int toDay, PlannerList into)
        {
            for (int i = 0; i < PlannerStore.Notes.Count; i++)
            {
                PlannerNote note = PlannerStore.Notes[i];
                for (int day = PlannerStore.NextTime(note, fromDay); day >= 0 && day <= toDay; day = PlannerStore.NextTime(note, day + 1))
                {
                    Add(note, day, into);
                }
            }
        }

        private static void Add(PlannerNote note, int day, PlannerList into)
        {
            if (!PlannerStore.Falls(note, day, out bool done))
            {
                return;
            }
            EntryId id = PlannerStore.NoteEntry(note, day);
            PlannerEntry e = into.Add(SourceId, id.A, id.B, PlannerCategory.Note, day);
            if (e == null)
            {
                return;
            }
            e.Own = true;
            e.Done = done;
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

    /// <summary>The test's samples: a marked entry of every kind around now, to see every row, icon and state; not saved.</summary>
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
                // from two hours ago: some over or missed, one on now, two clashing, the rest to come
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

    /// <summary>The game's tax bill (TaxHelper): due day and amount; the day after, TaxHelper.RunDaily takes it by force.</summary>
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
                float owed = TaxHelper.GetCurrentTaxesToPay();
                PlannerText.Costs(e, owed, PlannerText.Money(owed));
                e.IfMissed = PlannerText.T("reallife_planner_tax_missed");
            }
        }
    }
}
