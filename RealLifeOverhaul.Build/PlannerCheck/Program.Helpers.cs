using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Planner;

// The Planner's two small helpers of 14 Oct 2026: each tab's name with how many entries it lists under its filter
// (PlannerRules.TabCount, as GameUi.TableModel lets a filter go), and the morning line, added below as it comes.
internal static partial class Program
{
    private static void TabCounts()
    {
        var list = new PlannerList();
        list.Begin(5, 12, false);
        Add(list, "work", 1, 5, 540, 600);
        Add(list, "work", 2, 5, 660, 720);
        PlannerEntry bill = list.Add("money", 3, 0, PlannerCategory.Money, 5);
        Add(list, "work", 4, 6, 540, 600);
        PlannerEntry later = list.Add("money", 5, 0, PlannerCategory.Money, 9);
        PlannerEntry far = list.Add("money", 6, 0, PlannerCategory.Money, 12);
        Check(bill != null && later != null && far != null, "tab counts: the entries are taken");
        List<PlannerEntry> items = list.Items;
        int work = (int)PlannerGroup.Work, money = (int)PlannerGroup.Money, life = (int)PlannerGroup.Life;
        Check(PlannerRules.TabCount(items, PlannerRules.TabToday, 5, -1) == 3 && PlannerRules.TabCount(items, PlannerRules.TabUpcoming, 5, -1) == 3
              && PlannerRules.TabCount(items, PlannerRules.TabWeek, 5, -1) == 5,
            "tab counts: Today (3), Upcoming (3: the days after), Week (5: seven days from today, day 12 is the eighth)");
        Check(PlannerRules.TabCount(items, PlannerRules.TabToday, 5, work) == 2 && PlannerRules.TabCount(items, PlannerRules.TabToday, 5, money) == 1,
            "tab counts: under a filter, the entries of its group");
        Check(PlannerRules.TabCount(items, PlannerRules.TabToday, 5, life) == 3, "tab counts: a group with nothing on the tab lists everything, as the table does");
        Check(PlannerRules.TabCount(items, PlannerRules.TabUpcoming, 6, money) == 2 && PlannerRules.TabCount(items, PlannerRules.TabToday, 6, money) == 1,
            "tab counts: a tab with one group only lists everything whatever the filter (as the table lets the filter go)");
        Check(PlannerRules.TabCount(new List<PlannerEntry>(), PlannerRules.TabToday, 5, -1) == 0, "tab counts: an empty day is (0)");
        bool same = true;
        for (int tab = 0; tab < 3; tab++)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                n += PlannerRules.OnTab(items[i], tab, 5) ? 1 : 0;
            }
            same &= n == PlannerRules.TabCount(items, tab, 5, -1);
        }
        Check(same, "tab counts: with no filter the count is what the tab lists");

        string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        bool texts = true;
        foreach (string lang in ModLanguages.Present(folder))
        {
            Dictionary<string, string> w = Words(folder, lang);
            texts &= w != null && w.TryGetValue("reallife_planner_tab_count", out string t) && t.Contains("{tab}") && t.Contains("{count}");
        }
        Check(texts, "tab counts: the tab's name with its count is in every language");
    }

    private static void MorningLine()
    {
        // when it is due: awake, from 05:00 until noon, once a day
        Check(!PlannerRules.MorningDue(8, 4, false, 0) && PlannerRules.MorningDue(8, 5, false, 0) && PlannerRules.MorningDue(8, 11, false, 0) && !PlannerRules.MorningDue(8, 12, false, 0),
            "morning: from 05:00 until noon, not in the night and not in the afternoon");
        Check(!PlannerRules.MorningDue(8, 7, true, 0), "morning: not while the player sleeps");
        Check(!PlannerRules.MorningDue(8, 7, false, 8) && PlannerRules.MorningDue(9, 7, false, 8), "morning: once a day, again the next day");

        // a night's sleep to 07:20 and a reload at 09:00: one line, at waking
        PlannerStore.Reset();
        PlannerStore.Load(new Dictionary<string, string>());
        int sent = 0, at = -1;
        for (int minute = 0; minute < 24 * 60; minute += 10)
        {
            int hour = minute / 60;
            bool asleep = minute < 7 * 60 + 20;
            if (minute == 9 * 60)
            {
                var data = new Dictionary<string, string>();
                PlannerStore.Save(data);
                PlannerStore.Load(data);
            }
            if (PlannerRules.MorningDue(8, hour, asleep, PlannerStore.MorningDay))
            {
                PlannerStore.MorningDay = 8;
                sent++;
                at = at < 0 ? minute : at;
            }
        }
        Check(sent == 1 && at == 7 * 60 + 20, "morning: one line on the day, at the first look after waking, a reload included (" + sent + ", at " + at + ")");
        var kept = new Dictionary<string, string>();
        PlannerStore.Save(kept);
        Check(kept[PlannerStore.Key].Contains(";morning=8"), "morning: the day it was sent is in the save's line");
        PlannerStore.Load(new Dictionary<string, string> { { PlannerStore.Key, "v=1;next=9;hide=0;lead=2;hello=1;seen=5" } });
        Check(PlannerStore.MorningDay == 0 && PlannerStore.NextNoteId == 9, "morning: an old save without the field loads with no line sent");

        // what it says: the day's entries still to do, the first at its time, and the key for each case
        var list = new PlannerList();
        list.Begin(8, 8, false);
        PlannerEntry over = Add(list, "work", 1, 8, 300, 330);
        PlannerEntry ticket = Add(list, "study", 2, 8, 540, 660);
        PlannerEntry shift = Add(list, "work", 3, 8, 780, 900);
        PlannerEntry allDay = list.Add("money", 4, 0, PlannerCategory.Money, 8);
        PlannerLogic.Arrange(list, PlannerTime.At(8, 7, 0));
        Check(PlannerRules.MorningCount(list.Items, 8) == 3 && PlannerRules.MorningCount(list.Items, 9) == 0, "morning: the day's entries to do are counted, not one already over");
        Check(PlannerRules.FirstOf(list.Items, 8) == ticket, "morning: the first is the first with a time still to come");
        ticket.Placed = true;
        Check(PlannerRules.LeaveBy(ticket, 25) == PlannerTime.At(8, 8, 35), "morning: a way of 25 minutes to a 09:00 class says leave by 08:35");
        Check(PlannerRules.MorningKey(0, true, true) == null, "morning: nothing for an empty day");
        Check(PlannerRules.MorningKey(3, true, true) == "reallife_planner_morning" && PlannerRules.MorningKey(3, true, false) == "reallife_planner_morning_here"
              && PlannerRules.MorningKey(1, true, true) == "reallife_planner_morning_one" && PlannerRules.MorningKey(1, true, false) == "reallife_planner_morning_one_here"
              && PlannerRules.MorningKey(2, false, false) == "reallife_planner_morning_all_day" && PlannerRules.MorningKey(1, false, false) == "reallife_planner_morning_all_day_one",
            "morning: a key for many or one, with and without a time to leave, and for a day with no set times");

        // the texts in every language, at most 240 characters with a long title
        string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        string[] keys = { "reallife_planner_morning", "reallife_planner_morning_here", "reallife_planner_morning_one", "reallife_planner_morning_one_here",
            "reallife_planner_morning_all_day", "reallife_planner_morning_all_day_one", "reallife_planner_opt_morning", "reallife_opt_tip_planner_morning" };
        var wrong = new List<string>();
        Dictionary<string, string> en = Words(folder, "en");
        foreach (string lang in ModLanguages.Present(folder))
        {
            Dictionary<string, string> w = Words(folder, lang);
            foreach (string key in keys)
            {
                if (w == null || en == null || !w.TryGetValue(key, out string t) || t.Length == 0 || string.Join(",", Slots(t)) != string.Join(",", Slots(en[key])))
                {
                    wrong.Add(lang + " " + key);
                }
            }
        }
        Check(wrong.Count == 0, "morning: every text is in every language with the same slots (" + string.Join(", ", wrong) + ")");
        if (en != null && en.TryGetValue("reallife_planner_morning", out string line))
        {
            string filled = line.Replace("{count}", "12").Replace("{time}", "09:00").Replace("{title}", new string('t', PlannerStore.TitleLength)).Replace("{leave}", "08:35");
            Check(filled.Length <= 240 && line.IndexOf('!') < 0, "morning: the line with the longest title is " + filled.Length + " characters, at most 240, and plain");
            Check(line.Replace("{count}", "3").Replace("{time}", "09:00").Replace("{title}", "Forklift ticket").Replace("{leave}", "08:35")
                  == "Today: 3 entries, the first at 09:00 (Forklift ticket), leave by 08:35.", "morning: the line reads as asked");
        }
    }
}
