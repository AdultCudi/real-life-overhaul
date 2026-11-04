namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Blocks that both the live source and the samples build from the same question, so that a page which repeats what the question card holds (the inbox's open question) cannot say
    /// one thing in the game and another in a preview. Game-free; cost: a few small arrays when a page is read, nothing per frame.
    /// </summary>
    internal static class CompanyBlocks
    {
        /// <summary>The question's own figures (pay now, what is asked, the limit) as labelled figures side by side; null where it has none.</summary>
        public static CompanyBlock Facts(CompanyQuestion q)
        {
            if (q == null || q.FactLabels == null || q.FactLabels.Length == 0)
            {
                return null;
            }
            return new CompanyBlock { Kind = CompanyBlockKind.Figures, Options = q.FactLabels, Values = q.FactValues };
        }

        /// <summary>The answers a question can be given, each with what it comes to: rows that are read, not pressed, because the question's card is where an answer is given.</summary>
        public static CompanyBlock Answers(CompanyQuestion q)
        {
            if (q == null || q.Choices == null || q.Choices.Length == 0)
            {
                return null;
            }
            var items = new CompanyItem[q.Choices.Length];
            for (int i = 0; i < items.Length; i++)
            {
                CompanyChoice c = q.Choices[i];
                items[i] = new CompanyItem
                {
                    Grade = -1, Title = c.Name, Sub = c.Lines != null && c.Lines.Length > 0 ? c.Lines[0].Text : null, Figure = c.Note, Icon = IconOf(c.Act), Tint = UiRole.None
                };
            }
            return new CompanyBlock { Kind = CompanyBlockKind.Items, Items = items };
        }

        /// <summary>
        /// What a plan's floor of crew asks of the place, read from its blocks: the busiest block, the crew hours it comes to in a day and the people on the staff there, with a line where the busiest
        /// block needs more people than there are. The blocks are given as three arrays of the same length: first hour, last hour, crew.
        /// </summary>
        public static CompanyBlock[] FloorAsks(CompanyWords w, int[] from, int[] to, int[] crew, int staff)
        {
            int busiest = 0, busyFrom = 0, busyTo = 0, hours = 0;
            for (int i = 0; i < crew.Length; i++)
            {
                hours += System.Math.Max(0, to[i] - from[i]) * crew[i];
                if (crew[i] > busiest)
                {
                    busiest = crew[i];
                    busyFrom = from[i];
                    busyTo = to[i];
                }
            }
            var list = new System.Collections.Generic.List<CompanyBlock>(5)
            {
                new CompanyBlock { Kind = CompanyBlockKind.Section, Text = w.T("reallife_rota_ask_sec", "What the floor asks") },
                new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_rota_ask_busiest", "Busiest block"), Value =
                    w.F("reallife_rota_needs_crew", "{count} on shift", "count", busiest.ToString())
                    + ", " + busyFrom.ToString("00") + "-" + busyTo.ToString("00"), Tone = UiRole.Text },
                new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_rota_ask_day", "Crew hours a day"),
                    Value = w.F("reallife_rota_hours_n", "{hours} h", "hours", hours.ToString()), Tone = UiRole.Text },
                new CompanyBlock { Kind = CompanyBlockKind.Row, Text = w.T("reallife_rota_ask_staff", "People on the staff here"), Value = staff.ToString(), Tone = UiRole.Text }
            };
            if (busiest > staff)
            {
                list.Add(new CompanyBlock { Kind = CompanyBlockKind.Note,
                    Text = w.F("reallife_rota_ask_short", "The busiest block needs {count} more people than the place has.", "count", (busiest - staff).ToString()),
                    Tone = UiRole.WarnText });
            }
            return list.ToArray();
        }

        // One glyph a kind of answer: agreeing, refusing, a bonus, a wider limit, haggling, holding over. The answers' numbers are CompanyDo's own.
        private static UiIcon IconOf(CompanyAct act)
        {
            switch (act.Do)
            {
                case CompanyDo.Settle:
                    return act.B == 0 ? UiIcon.Check : act.B == 1 ? UiIcon.Close : act.B == 3 ? UiIcon.Coin : act.B == 4 ? UiIcon.Gear : UiIcon.Handshake;
                case CompanyDo.LeaseAnswer:
                    return act.B == 0 ? UiIcon.Check : act.B == 4 ? UiIcon.Handshake : UiIcon.Clock;
                case CompanyDo.Dismiss:
                    return UiIcon.Close;
                default:
                    return UiIcon.Next;
            }
        }
    }
}
