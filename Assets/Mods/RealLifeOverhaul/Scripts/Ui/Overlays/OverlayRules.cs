using System;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a press on a card does.</summary>
    public enum OverlayAct : byte { None, Action, Open, Choose, Page, Step, ActionPage }

    /// <summary>How a person's mood reads, from the game's own satisfaction (0 to 100).</summary>
    public enum OverlayMood : byte { Low, Middle, High }

    /// <summary>
    /// The numbers behind the cards over the world's items, kept apart from the game so they are checked offline: the mood
    /// bands at the game's own thresholds, a stock's tone, a margin, a shift as a person is on it now, what a week has earned,
    /// the ingredients the game colours red when the pallets run short, the pages of a long choice. Cost: arithmetic.
    /// </summary>
    public static class OverlayRules
    {
        public const int LowMood = 30, HighMood = 60;     // the game's own satisfaction thresholds (ColoredTextHelper.GetSatisfactionText)
        public const int LowStock = 25, MiddleStock = 50;  // its stock thresholds (ColoredTextHelper.GetAmountText), in per cent
        public const int MostSkill = 100;                  // the game's skills run 0 to 100 and print as per cent (EmployeeOverlay.HandleSkills)

        /// <summary>The mood band of a satisfaction: under 30 low, under 60 middle, else high.</summary>
        public static OverlayMood Mood(float satisfaction) => satisfaction < LowMood ? OverlayMood.Low : satisfaction < HighMood ? OverlayMood.Middle : OverlayMood.High;

        /// <summary>The colour of a mood: a portrait's ring and its bar.</summary>
        public static UiRole MoodTone(OverlayMood mood) => mood == OverlayMood.Low ? UiRole.Danger : mood == OverlayMood.Middle ? UiRole.Warn : UiRole.Ok;

        /// <summary>A satisfaction as a bar's value, 0 to 100.</summary>
        public static int Percent(float value) => (int)Math.Max(0f, Math.Min(100f, (float)Math.Floor(value)));

        /// <summary>A stock as a share of its room, 0 to 100; -1 when there is no room to count against.</summary>
        public static int StockShare(int amount, int room) => room <= 0 ? -1 : (int)Math.Max(0L, Math.Min(100L, (long)amount * 100L / room));

        /// <summary>A stock's tone: under a quarter bad, under a half warning, else good, as the game colours its amount.</summary>
        public static UiRole StockTone(int share) => share < LowStock ? UiRole.Danger : share < MiddleStock ? UiRole.Warn : UiRole.Ok;

        /// <summary>The margin of a price over its cost in whole per cent; -1 when either is not known (a cost of nothing is not known).</summary>
        public static int Margin(float price, float cost)
        {
            if (price <= 0f || cost <= 0f)
            {
                return -1;
            }
            return (int)Math.Round((price - cost) / price * 100f, MidpointRounding.AwayFromZero);
        }

        /// <summary>The tone of a margin: under nothing bad, under a fifth warning, else good.</summary>
        public static UiRole MarginTone(int margin) => margin < 0 ? UiRole.DangerText : margin < 20 ? UiRole.WarnText : UiRole.OkText;

        /// <summary>
        /// The shift a person is on at an hour: the shift that holds it, joined to the shifts that run on from its end and back from
        /// its start (two shifts in a row are one stretch at work). False when none holds the hour.
        /// </summary>
        public static bool OnShift(int[] starts, int[] ends, int count, int hour, out int from, out int until)
        {
            from = until = -1;
            for (int i = 0; i < count; i++)
            {
                if (starts[i] <= hour && hour < ends[i])
                {
                    from = starts[i];
                    until = ends[i];
                    break;
                }
            }
            if (from < 0)
            {
                return false;
            }
            for (int pass = 0; pass < count; pass++)
            {
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    if (starts[i] == until && ends[i] > until)
                    {
                        until = ends[i];
                        moved = true;
                    }
                    else if (ends[i] == from && starts[i] < from)
                    {
                        from = starts[i];
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
            }
            return true;
        }

        /// <summary>The minutes a person has been at their shift: from its first hour to the clock's hour and minute, never under nothing.</summary>
        public static int WorkedMinutes(int from, int hour, int minute) => Math.Max(0, (hour - from) * 60 + Math.Max(0, Math.Min(59, minute)));

        /// <summary>A share of a shift gone, 0 to 100, for its bar.</summary>
        public static int ShiftShare(int from, int until, int hour, int minute)
        {
            int total = (until - from) * 60;
            return total <= 0 ? 0 : (int)Math.Min(100L, WorkedMinutes(from, hour, minute) * 100L / total);
        }

        /// <summary>What a week's hours have earned at the hourly wage, whole currency units kept as the game keeps them.</summary>
        public static float Earned(int hoursThisWeek, float hourlyWage) => Math.Max(0, hoursThisWeek) * Math.Max(0f, hourlyWage);

        /// <summary>The hours and minutes of a count of minutes.</summary>
        public static void Split(int minutes, out int hours, out int rest)
        {
            minutes = Math.Max(0, minutes);
            hours = minutes / 60;
            rest = minutes % 60;
        }

        /// <summary>A skill as a bar's value: the game's own 0 to 100.</summary>
        public static int SkillShare(float value) => Percent(value * 100f / MostSkill);

        /// <summary>The share of seats taken, 0 to 100; -1 when there are no seats.</summary>
        public static int SeatShare(int taken, int seats) => seats <= 0 ? -1 : (int)Math.Min(100L, Math.Max(0, taken) * 100L / seats);

        /// <summary>What making one unit costs: the ingredients' amounts at their unit costs over the units a batch makes; -1 when any cost is unknown.</summary>
        public static float MakingCost(float[] amounts, float[] unitCosts, int count, int made)
        {
            if (count <= 0 || made <= 0)
            {
                return -1f;
            }
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (unitCosts[i] <= 0f)
                {
                    return -1f;
                }
                sum += amounts[i] * unitCosts[i];
            }
            return sum / made;
        }

        /// <summary>The page an option stands on, for a list of pages of <paramref name="perPage"/>.</summary>
        public static int PageOf(int option, int perPage) => option < 0 ? 0 : option / Math.Max(1, perPage);

        /// <summary>A page kept inside the pages a list has.</summary>
        public static int ClampPage(int page, int count, int perPage) => Math.Max(0, Math.Min(page, Math.Max(0, (count - 1) / Math.Max(1, perPage))));

        /// <summary>The volume after a step of a tenth, kept inside 0 to 100.</summary>
        public static int Step(int level, int direction) => Math.Max(0, Math.Min(100, (level + 5) / 10 * 10 + direction * 10));

        /// <summary>A volume 0 to 1 as per cent.</summary>
        public static int Volume(float slider) => (int)Math.Max(0f, Math.Min(100f, (float)Math.Round(slider * 100f)));

        /// <summary>
        /// The words of the game's rich text without its tags, the lines kept, and a colon's lead taken off when the words start
        /// with it ("Contents: Flour" is Flour after "Contents:").
        /// </summary>
        public static string After(string words, string lead)
        {
            if (string.IsNullOrEmpty(words))
            {
                return null;
            }
            string plain = Plain(words);
            if (!string.IsNullOrEmpty(lead) && plain.StartsWith(lead, StringComparison.Ordinal))
            {
                plain = plain.Substring(lead.Length).TrimStart(':', ' ', ' ', '：');
            }
            return plain.Length == 0 ? null : plain;
        }

        /// <summary>The words with every tag taken off.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return text == null ? null : text.Trim();
            }
            var sb = new StringBuilder(text.Length);
            bool tag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    tag = true;
                }
                else if (c == '>' && tag)
                {
                    tag = false;
                }
                else if (!tag)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// The ingredients of a recipe as the game writes them (MachineOverlay.UpdateIngredientsList): names joined by commas,
        /// each wrapped in a colour tag, the red one for what the pallets lack. Each name and whether it is the red one.
        /// </summary>
        public static int Ingredients(string text, string redHex, string[] names, bool[] short_)
        {
            int n = 0;
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int at = 0;
            while (n < names.Length && at < text.Length)
            {
                int open = text.IndexOf("<color=", at, StringComparison.OrdinalIgnoreCase);
                if (open < 0)
                {
                    string rest = Plain(text.Substring(at)).Trim(',', ' ');
                    if (rest.Length > 0)
                    {
                        names[n] = rest;
                        short_[n++] = false;
                    }
                    break;
                }
                int close = text.IndexOf('>', open);
                int end = close < 0 ? -1 : text.IndexOf("</color>", close, StringComparison.OrdinalIgnoreCase);
                if (close < 0 || end < 0)
                {
                    break;
                }
                string colour = text.Substring(open + 7, close - open - 7).Trim().TrimStart('#');
                names[n] = text.Substring(close + 1, end - close - 1);
                short_[n++] = !string.IsNullOrEmpty(redHex) && colour.StartsWith(redHex.TrimStart('#'), StringComparison.OrdinalIgnoreCase);
                at = end + 8;
            }
            return n;
        }

        /// <summary>A person's name's first name and last initial ("Maria Rossi" is "Maria R."), for a short line.</summary>
        public static string Short(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }
            string[] parts = name.Trim().Split(' ');
            return parts.Length < 2 || parts[parts.Length - 1].Length == 0 ? parts[0] : parts[0] + " " + parts[parts.Length - 1][0] + ".";
        }

        /// <summary>Whether a small card shows its leader: it stands where its point puts it, over the point, not moved by the screen's edges.</summary>
        public static bool LeaderShows(float pointX, float pointY, float cardX, float cardY, float cardW, float cardH, float lift)
        {
            float natural = pointY - lift - cardH;
            return Math.Abs(cardY - natural) < 0.5f && pointX >= cardX + UiTokens.S3 && pointX <= cardX + cardW - UiTokens.S3;
        }

        /// <summary>
        /// What a press on a card means, by the command it was drawn with: an action's game button or handler, a choice opened, an
        /// option chosen in the open choice, a page turned, the volume stepped; none when nothing of the card answers it (a dead
        /// button, or a command that is another part's).
        /// </summary>
        public static OverlayAct Route(UiOverlayModel m, int command, int arg, int openChoice)
        {
            switch (command)
            {
                case UiOverlayCard.ActionCommand:
                    return arg >= 0 && arg < m.ActionCount && !m.Actions[arg].Hint ? OverlayAct.Action : OverlayAct.None;
                case UiOverlayCard.ChoiceCommand:
                    return arg >= 0 && arg < m.ChoiceCount ? OverlayAct.Open : OverlayAct.None;
                case UiOverlayCard.OptionCommand:
                    return openChoice >= 0 && openChoice < m.ChoiceCount && arg >= 0 && arg < m.Choices[openChoice].OptionCount ? OverlayAct.Choose : OverlayAct.None;
                case UiOverlayCard.PageCommand:
                    return openChoice >= 0 && openChoice < m.ChoiceCount && (arg == -1 || arg == 1) ? OverlayAct.Page : OverlayAct.None;
                case UiOverlayCard.ActionPageCommand:
                    return ActionPages(m.ActionCount, UiOverlayModel.MostActions) > 1 && (arg == -1 || arg == 1) ? OverlayAct.ActionPage : OverlayAct.None;
                case UiOverlayCard.StepCommand:
                    return m.Level >= 0 && (arg == -1 || arg == 1) ? OverlayAct.Step : OverlayAct.None;
            }
            return OverlayAct.None;
        }

        /// <summary>
        /// True when the game's box under a card must be looked through again for pieces to hide: its transforms changed in number (the
        /// game made or destroyed a button as it refreshed, or another mod put one in), or the card is read again anyway. A look is a walk
        /// over the box's own few transforms, never the scene.
        /// </summary>
        public static bool SweepDue(int seen, int count, bool read) => seen != count || read;

        /// <summary>
        /// The same, and also when a button looked at last time is gone: the game destroys the buttons of a computer's panel and makes new
        /// ones in one refresh, which can leave the count of transforms as it was while every button is a new, unhidden one.
        /// </summary>
        public static bool SweepDue(int seen, int count, bool read, bool lost) => lost || SweepDue(seen, count, read);

        /// <summary>The pages a card's buttons fall over: one while they all fit, else pages of one fewer than fit, the pager taking the last row.</summary>
        public static int ActionPages(int count, int fit) => count <= fit ? 1 : (count + fit - 2) / (fit - 1);

        /// <summary>The buttons of a page: first and one past the last (all of them while they fit).</summary>
        public static void ActionWindow(int count, int page, int fit, out int first, out int end)
        {
            if (count <= fit)
            {
                first = 0;
                end = count;
                return;
            }
            Window(count, Math.Max(0, Math.Min(page, ActionPages(count, fit) - 1)), fit - 1, out first, out end);
        }

        /// <summary>The list of indexes of a page of a list: first and one past the last.</summary>
        public static void Window(int count, int page, int perPage, out int first, out int end)
        {
            first = Math.Max(0, Math.Min(page * perPage, count));
            end = Math.Min(count, first + perPage);
        }
    }
}
