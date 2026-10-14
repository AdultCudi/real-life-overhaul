using System;
using System.Collections.Generic;
using System.IO;
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
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> w = Words(folder, lang);
            texts &= w != null && w.TryGetValue("reallife_planner_tab_count", out string t) && t.Contains("{tab}") && t.Contains("{count}");
        }
        Check(texts, "tab counts: the tab's name with its count is in the three languages");
    }
}
