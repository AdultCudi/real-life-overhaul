using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The net worth card over the money pill, as a tooltip card's words: a header with the day, the last thirty days as a line with its
    /// highest and lowest at the right, then Now, Highest and Lowest with their days, and the change over the window. Where the save
    /// has fewer than two days on record it says the line starts later and shows Now alone. Game-free. Cost: one model a showing.
    /// </summary>
    public static class UiNetWorthTip
    {
        /// <summary>The card's words for a summary; <paramref name="line"/> holds <see cref="WorthSummary.Points"/> values, oldest first.</summary>
        public static UiTooltipModel Model(UiWords t, WorthSummary s, int today, float[] line, Func<long, string> money, Func<long, string> shortMoney)
        {
            var m = new UiTooltipModel();
            int span0 = Math.Max(1, today - s.FromDay + 1);
            string span = t("reallife_ui_tip_worth_span", "The last {days} days").Replace("{days}", Math.Min(span0, WorthBook.Window).ToString());
            m.Header(t("reallife_ui_tip_worth_title", "Net worth"), Day(t, today), s.Has ? span : null,
                UiIcon.ChartLineUp);
            if (s.Has && s.Points >= 2 && line != null)
            {
                m.Chart(line, shortMoney(s.High), shortMoney(s.Low), Day(t, s.FromDay), Day(t, today));
            }
            UiTipBlock rows = m.Rows();
            rows.Lines.Add(new UiTipLine { Left = t("reallife_ui_tip_worth_now", "Now"), Right = money(s.Now) });
            if (s.Has)
            {
                rows.Lines.Add(new UiTipLine { Left = t("reallife_ui_tip_worth_high", "Highest"), Right = On(t, money(s.High), s.HighDay) });
                rows.Lines.Add(new UiTipLine { Left = t("reallife_ui_tip_worth_low", "Lowest"), Right = On(t, money(s.Low), s.LowDay) });
                rows.Lines.Add(new UiTipLine
                {
                    Left = t("reallife_ui_tip_worth_change", "Change"), Right = (s.Change > 0 ? "+" : string.Empty) + (s.Change < 0 ? "-" + money(-s.Change) : money(s.Change)),
                    Ink = s.Change > 0 ? UiRole.OkText : s.Change < 0 ? UiRole.DangerText : UiRole.None
                });
            }
            else
            {
                m.Text(t("reallife_ui_tip_worth_early", "The line starts once a second day is on record."), UiRole.Text2);
            }
            return m;
        }

        private static string Day(UiWords t, int day) => t("reallife_ui_tip_day", "Day {day}").Replace("{day}", day.ToString());

        private static string On(UiWords t, string amount, int day) => t("reallife_ui_tip_worth_on_day", "{amount} on day {day}").Replace("{amount}", amount).Replace("{day}", day.ToString());
    }
}
