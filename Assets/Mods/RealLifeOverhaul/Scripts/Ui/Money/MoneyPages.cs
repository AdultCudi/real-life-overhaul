using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The three money apps, each shaped by what the player does there. The window and its sections are the kit's; the page
    /// under them is a statement, an offer, a league, a form or a catalogue, never the same row of cards. Game-free. Cost:
    /// a layout when the model changes.
    /// </summary>
    public static partial class MoneyPages
    {
        public static void Lay(UiScene s, MoneyModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin(m.Title ?? "money", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Nav, m.Tab, m.Across, keys, out float x, out float y, out float w, out float bottom);
            switch (m.Shape)
            {
                case MoneyShape.Overview: Overview(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Offer: Offer(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Flow: Flow(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.League: League(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Factors: Factors(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Apply: Apply(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Home: Home(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Table: Table(s, at, m, x, y, w, bottom - y, keys); break;
                case MoneyShape.Fund: Fund(s, at, m, x, y, w, bottom, keys); break;
                case MoneyShape.Counter: Counter(s, at, m, x, y, w, bottom, keys); break;
                default: Overview(s, at, m, x, y, w, bottom, keys); break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The borrower, what they owe by kind, then the lines behind it filling the rest.
        private static void Overview(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Profile != null)
            {
                y = UiAppWindow.Profile(s, at, m.Profile, m.Actions, x, y, w, keys);
            }
            y = Note(s, at, m.Note, x, y, w);
            float donutH = UiTokens.S16 * 3f;
            if (m.Donut.Length > 0 && bottom - y > donutH + UiTokens.S16)
            {
                y += Donut(s, at, m, x, y, w, donutH) + UiTokens.S6;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        // The offer as the one thing on the page: who lends, the amount, what the term costs, then the feed.
        private static void Offer(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Hero != null)
            {
                y += UiAppWindow.Hero(s, at, m.Hero, x, y, w) + UiTokens.S5;
            }
            y = Stepper(s, at, m, x, y, w, keys);
            y = Note(s, at, m.Note, x, y, w);
            y = Note(s, at, m.Note2, x, y, w);
            if (m.Actions.Length > 0)
            {
                y = Buttons(s, at, m.Actions, x, y, keys) + UiTokens.S4;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        // The month as a line, then the statement down to the foot.
        private static void Flow(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float chartH = UiTokens.S16 * 4f;
            if (m.Chart != null && bottom - y > chartH + UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S4;
            }
            if (m.Actions.Length > 0)
            {
                y = Buttons(s, at, m.Actions, x, y, keys) + UiTokens.S4;
            }
            y = Note(s, at, m.Note, x, y, w);
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        // The rich list on the left, the chosen rival and the answers on the right, the chart under the list.
        private static void League(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float gap = UiTokens.S6;
            float left = UiDraw.SnapDown((w - gap) * 0.56f);
            float rx = x + left + gap;
            float rw = x + w - rx;
            float h = bottom - y;
            if (m.Profile != null)
            {
                float py = UiAppWindow.Profile(s, at, m.Profile, m.Actions, rx, y, rw, keys);
                py = Choices(s, at, m, rx, py, rw, keys);
                py = Note(s, at, m.Note, rx, py, rw);
                if (m.Chart != null && bottom - py > UiTokens.S16 * 2f)
                {
                    UiChart.Lay(s, at, m.Chart, rx, py, rw, bottom - py);
                }
            }
            if (m.Table != null)
            {
                UiDataTable.Lay(s, at, m.Table, x, y, left, h, keys);
            }
            else
            {
                Empty(s, at, m, x, y, left, h, keys);
            }
        }

        // Each factor as a graded bar with its sentence, the table of them filling what remains.
        private static void Factors(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            y = Bars(s, at, m, x, y, w);
            y = Note(s, at, m.Note, x, y, w);
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        // The application on the left, the headroom and the other banks on the right.
        private static void Apply(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Profile != null)
            {
                y = UiAppWindow.Profile(s, at, m.Profile, m.Actions, x, y, w, keys);
            }
            float gap = UiTokens.S6;
            float left = UiDraw.SnapDown((w - gap) * 0.5f);
            float rx = x + left + gap;
            float rw = x + w - rx;
            float top = y;
            y = Stepper(s, at, m, x, y, left, keys);
            y = Bars(s, at, m, x, y, left);
            y = Lines(s, at, m, x, y, left);
            y = Note(s, at, m.Note, x, y, left);
            if (m.Meter >= 0)
            {
                UiDraw.GradeBar(s, at, rx, top, rw, m.MeterLabel ?? string.Empty, m.Meter);
                top += UiTokens.S12 + UiTokens.S2;
            }
            top = Note(s, at, m.Note2, rx, top, rw);
            float foot = Math.Max(y, top);
            if (m.Table != null && bottom - foot > UiTokens.S16)
            {
                UiDataTable.Lay(s, at, m.Table, x, foot, w, bottom - foot, keys);
            }
            else if (bottom - y > UiTokens.S16)
            {
                Rest(s, at, m, x, y, w, bottom, keys);
            }
        }

        // The bank's level as the hero, the month's figures, then the table.
        private static void Home(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Hero != null)
            {
                y += UiAppWindow.Hero(s, at, m.Hero, x, y, w) + UiTokens.S5;
            }
            if (m.Figures.Length > 0)
            {
                y = UiAppWindow.Figures(s, at, m.Figures, x, y, w) + UiTokens.S4;
            }
            y = Lines(s, at, m, x, y, w);
            y = Note(s, at, m.Note, x, y, w);
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        // The fund's month, then its holdings beside the largest accounts.
        private static void Fund(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float chartH = UiTokens.S16 * 4f;
            if (m.Chart != null && bottom - y > chartH + UiTokens.S16 * 2f)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, chartH);
                y += chartH + UiTokens.S4;
            }
            float donutH = UiTokens.S16 * 3f;
            if (m.Donut.Length > 0 && bottom - y > donutH)
            {
                float dw = UiDraw.SnapDown(w * 0.42f);
                Donut(s, at, m, x, y, dw, donutH);
                if (m.Table != null)
                {
                    UiDataTable.Lay(s, at, m.Table, x + dw + UiTokens.S6, y, w - dw - UiTokens.S6, bottom - y, keys);
                }
                return;
            }
            Rest(s, at, m, x, y, w, bottom, keys);
        }

        private static void Rest(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float h = bottom - y;
            if (h < UiTokens.S16)
            {
                return;
            }
            if (m.Statement != null)
            {
                UiStatement.Lay(s, at, m.Statement, x, y, w, h, keys);
            }
            else if (m.Table != null)
            {
                Table(s, at, m, x, y, w, h, keys);
            }
            else if (m.Chart != null)
            {
                UiChart.Lay(s, at, m.Chart, x, y, w, h);
            }
            else
            {
                Empty(s, at, m, x, y, w, h, keys);
            }
        }

        private static void Table(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Table != null)
            {
                UiDataTable.Lay(s, at, m.Table, x, y, w, h, keys);
            }
            else
            {
                Empty(s, at, m, x, y, w, h, keys);
            }
        }

        private static void Empty(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            UiDraw.EmptyState(s, at, x, y, w, h, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, null, -1, keys);
        }

        private static float Donut(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, float h)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float size = UiTokens.S16 * 2f;
            UiDraw.Text(s, on, x + UiTokens.S4, y + UiTokens.S3, w - UiTokens.S8, UiStyle.SectionHeader, UiRole.Text, m.DonutTitle ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            float dy = y + UiTokens.S3 + UiTokens.PairRow;
            UiDraw.Donut(s, on, x + UiTokens.S4, dy, size, m.Donut, m.Donut.Length);
            int n = m.DonutLabels.Length < m.DonutValues.Length ? m.DonutLabels.Length : m.DonutValues.Length;
            n = n < m.Donut.Length ? n : m.Donut.Length;
            UiDraw.Legend(s, on, x + UiTokens.S4 + size + UiTokens.S6, dy, w - size - UiTokens.S4 * 2f - UiTokens.S6, m.DonutLabels, m.DonutValues, n);
            return h;
        }

        private static float Stepper(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.Amount))
            {
                return y;
            }
            float field = UiDraw.SnapDown(Math.Min(w, UiTokens.S16 * 5f));
            UiDraw.Stepper(s, at, x, y, field, m.Amount, m.CanLess, m.CanMore, MoneyCommands.StepLess, MoneyCommands.StepType, MoneyCommands.StepMore, 0, false);
            return y + UiTokens.Control + UiTokens.S4;
        }

        private static float Buttons(UiScene s, UiAt at, UiActionModel[] actions, float x, float y, IUiKeys keys)
        {
            float cx = x;
            float h = UiTokens.Control;
            for (int i = 0; i < actions.Length; i++)
            {
                ref UiActionModel a = ref actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                if (cx > x && cx + bw > x + UiTokens.S16 * 8f)
                {
                    break;
                }
                UiDraw.Button(s, at, cx, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command == 0 ? UiAppWindow.ActionCommand : a.Command, a.Arg == 0 ? i : a.Arg, a.Disabled);
                cx += bw + UiTokens.S3;
            }
            return y + h;
        }

        private static float Note(UiScene s, UiAt at, string text, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(text))
            {
                return y;
            }
            int id = UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, text, UiFlags.Wrap | UiFlags.Truncate, 3);
            return UiDraw.Snap(s.Items[id].Y + s.Items[id].H) + UiTokens.S3;
        }

        private static float Lines(UiScene s, UiAt at, MoneyModel m, float x, float y, float w)
        {
            int n = m.Lines.Length;
            for (int i = 0; i < n; i++)
            {
                bool on = m.LineOn != null && i < m.LineOn.Length && m.LineOn[i];
                UiDraw.Icon(s, at, x, y + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, on ? UiIcon.Check : UiIcon.Info, UiRole.None, UiFlags.Middle, true);
                string body = m.LineBody != null && i < m.LineBody.Length ? m.LineBody[i] : null;
                string words = string.IsNullOrEmpty(body) ? m.Lines[i] : m.Lines[i] + "  " + body;
                UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, UiTokens.PairRow, w - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text, words, UiFlags.Truncate);
                y += UiTokens.PairRow;
            }
            return n > 0 ? y + UiTokens.S3 : y;
        }

        private static float Bars(UiScene s, UiAt at, MoneyModel m, float x, float y, float w)
        {
            if (m.Marks == null || m.Marks.Length == 0)
            {
                return Lines(s, at, m, x, y, w);
            }
            int n = m.Marks.Length < m.Lines.Length ? m.Marks.Length : m.Lines.Length;
            for (int i = 0; i < n; i++)
            {
                int value = m.Marks[i] < 0 ? 0 : m.Marks[i] > 100 ? 100 : m.Marks[i];
                y += UiDraw.GradeBar(s, at, x, y, w, m.Lines[i], value) + UiTokens.S2;
                string body = m.LineBody != null && i < m.LineBody.Length ? m.LineBody[i] : null;
                y = Note(s, at, body, x, y, w);
            }
            return y;
        }

        private static float Choices(UiScene s, UiAt at, MoneyModel m, float x, float y, float w, IUiKeys keys)
        {
            int n = m.Choices.Length > 3 ? 3 : m.Choices.Length;
            for (int i = 0; i < n; i++)
            {
                var key = (UiAction)((int)UiAction.Pick1 + i);
                string label = (i + 1).ToString() + "  " + m.Choices[i];
                float bw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Secondary, label, UiIcon.None, key, keys));
                UiDraw.Button(s, at, x, y, bw, UiTokens.Control, UiButtonKind.Secondary, label, UiIcon.None, key, keys, MoneyCommands.Choice, i, false);
                y += UiTokens.Control;
                string body = m.ChoiceBody != null && i < m.ChoiceBody.Length ? m.ChoiceBody[i] : null;
                y = Note(s, at, body, x, y, w);
            }
            return y;
        }
    }
}
