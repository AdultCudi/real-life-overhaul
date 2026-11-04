using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A label and its value on an entry's page ("When", "Today, 09:00 to 10:30").</summary>
    public struct UiPlanFact
    {
        public string Label;
        public string Value;
        public UiRole Ink;       // the value's colour when it says a state; None for the words' own
    }

    /// <summary>What an entry's page shows: its kind and name, its state, the facts, what it is, the way there and the reminder.</summary>
    public sealed class UiPlanEntryModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Calendar;
        public UiRole Tint;                                      // the kind's colour; None for the glyph's own
        public string Status;                                    // "Now", "Missed", "Done"; null for none
        public UiRole StatusTone = UiRole.None;
        public string Clash;                                     // "Clashes with Lunch with Alex, 12:30"; null for none
        public string FactsHead, WhatHead, WayHead;              // "When and where", "What it is", "Getting there"
        public UiPlanFact[] Facts = new UiPlanFact[0];
        public string[] Lines = new string[0];                   // what it is, what it costs, what a miss costs
        public int WarnLine = -1;                                // the line that is in the warning's tone
        public UiPlanFact[] Way = new UiPlanFact[0];             // leave by, and how long the way is
        public string WayNote;                                   // "About 8 min by car, 20 min on foot"; null for none
        public string WayLate;                                   // "Leave now: you will be about 5 min late"; null for none
        public string ReminderLabel, ReminderValue;              // "Reminder", "1 hour before"; null label for none
        public bool ReminderLess, ReminderMore;
        public UiActionModel[] Actions = new UiActionModel[0];   // set the destination, done, edit, delete: the part's own commands carry which
    }

    /// <summary>The Planner's sub pages share their head and their commands.</summary>
    public static class UiPlanPage
    {
        public const int BackCommand = 1203, SettingsCommand = 1213;   // 1200 to 1219 are the Planner's pages' (UiPlanDay, UiPlanEntry, UiPlanForm)
        public const float Column = 600f;        // px: the page's column of rows
        public const float ActionsWidth = 260f;  // px: the actions' column beside it

        /// <summary>The head of a sub page: the arrow back, the page's title and its state chip. Returns the y under it.</summary>
        public static float Head(UiScene s, UiAt at, string title, UiIcon icon, UiRole tint, string chip, UiRole chipTone, float x, float y, float w)
        {
            float b = UiTokens.ControlSmall;
            UiDraw.IconButton(s, at, x, y + (UiTokens.Control - b) / 2f, b, UiIcon.Back, UiRole.Text, BackCommand, 0);
            float tx = x + b + UiTokens.S3;
            if (icon != UiIcon.None)
            {
                int t = UiDraw.Tile(s, at, tx, y + (UiTokens.Control - UiTokens.ControlSmall) / 2f, UiTokens.ControlSmall, tint, false);
                UiDraw.Icon(s, at.In(t), tx + (UiTokens.ControlSmall - UiTokens.Icon) / 2f, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, icon, tint,
                    UiFlags.Middle | UiFlags.Centre, true);
                tx += UiTokens.ControlSmall + UiTokens.S3;
            }
            float cw = string.IsNullOrEmpty(chip) ? 0f : UiDraw.Snap(UiTextRules.Width(chip, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2 * 3f + UiTokens.StatusDot);
            float room = x + w - tx - (cw > 0f ? cw + UiTokens.S3 : 0f);
            float tw = Math.Min(room, UiDraw.Snap(UiTextRules.Width(title, UiStyle.SectionTitle) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, tx, y, UiTokens.Control, Math.Max(UiTokens.S16, tw), UiStyle.SectionTitle, UiRole.Text, title, UiFlags.Truncate);
            if (cw > 0f)
            {
                UiDraw.Chip(s, at, tx + Math.Max(UiTokens.S16, tw) + UiTokens.S3, y + (UiTokens.Control - UiTokens.Badge - UiTokens.S1) / 2f, chip, chipTone == UiRole.None ? UiRole.Muted : chipTone);
            }
            return y + UiTokens.Control + UiTokens.S4;
        }

        /// <summary>A section's label in capitals. Returns the y under it.</summary>
        public static float Section(UiScene s, UiAt at, string label, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(label))
            {
                return y;
            }
            float h = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, label, UiFlags.Label | UiFlags.Truncate);
            return y + h + UiTokens.S2;
        }

        /// <summary>Whether the page's column and its actions stand side by side in a width.</summary>
        public static bool Beside(float w) => w >= Column + UiTokens.S8 + ActionsWidth;

        /// <summary>The actions, a button each, in a column of a width. Returns the y under them.</summary>
        public static float Actions(UiScene s, UiAt at, UiActionModel[] actions, int command, float x, float y, float w, IUiKeys keys)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ref UiActionModel a = ref actions[i];
                UiDraw.Button(s, at, x, y, w, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, command, i, a.Disabled);
                y += UiTokens.Control + UiTokens.S2;
            }
            return y;
        }
    }

    /// <summary>
    /// An entry's page, a block of an app window's page: its kind's glyph, its name and its state in the head, the clash it is
    /// in, the facts (when, where, what kind), what it is and what it costs or loses, the way there with the hour to leave by,
    /// the reminder to step through, and beside it the actions the entry allows (set the destination, mark it done, edit it, delete
    /// it). Commands: <see cref="UiPlanPage.BackCommand"/>, <see cref="ActionCommand"/> (an action's index),
    /// <see cref="LessCommand"/> and <see cref="MoreCommand"/> (the reminder). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPlanEntry
    {
        public const int ActionCommand = 1204, LessCommand = 1205, MoreCommand = 1206;

        /// <summary>Lays the page into the room an app window's Shell gave it.</summary>
        public static void Lay(UiScene s, UiAt at, UiPlanEntryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            at = at.InRegion(3);
            float top = UiPlanPage.Head(s, at, m.Title, m.Icon, m.Tint, m.Status, m.StatusTone, x, y, w);
            bool beside = UiPlanPage.Beside(w);
            float cw = beside ? UiPlanPage.Column : w;
            float cy = top, foot = y + h;
            if (!string.IsNullOrEmpty(m.Clash))
            {
                UiDraw.Icon(s, at, x, cy + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, x + UiTokens.IconSmall + UiTokens.S2, cy, UiTokens.PairRow, cw - UiTokens.IconSmall - UiTokens.S2, UiStyle.Body, UiRole.WarnText, m.Clash,
                    UiFlags.Label | UiFlags.Truncate);
                cy += UiTokens.PairRow + UiTokens.S3;
            }
            cy = UiPlanPage.Section(s, at, m.FactsHead, x, cy, cw);
            for (int i = 0; i < m.Facts.Length && cy + UiTokens.PairRow <= foot; i++)
            {
                cy += UiDraw.InfoRow(s, at, x, cy, cw, m.Facts[i].Label, m.Facts[i].Value, false, -1, 0, m.Facts[i].Ink == UiRole.None ? UiRole.Text : m.Facts[i].Ink);
            }
            if (m.Lines.Length > 0)
            {
                cy += UiTokens.S4;
                cy = UiPlanPage.Section(s, at, m.WhatHead, x, cy, cw);
                for (int i = 0; i < m.Lines.Length; i++)
                {
                    float lh = UiDraw.Snap(UiTextRules.WrappedHeight(m.Lines[i], UiStyle.Body, cw, 3));
                    if (cy + lh > foot)
                    {
                        break;
                    }
                    UiDraw.Text(s, at, x, cy, cw, UiStyle.Body, i == m.WarnLine ? UiRole.WarnText : UiRole.Text, m.Lines[i], UiFlags.Label | UiFlags.Wrap, 3);
                    cy += lh + UiTokens.S2;
                }
            }
            if (m.Way.Length > 0 || !string.IsNullOrEmpty(m.WayNote))
            {
                cy += UiTokens.S4;
                cy = UiPlanPage.Section(s, at, m.WayHead, x, cy, cw);
                for (int i = 0; i < m.Way.Length && cy + UiTokens.PairRow <= foot; i++)
                {
                    cy += UiDraw.InfoRow(s, at, x, cy, cw, m.Way[i].Label, m.Way[i].Value, false, -1, 0, m.Way[i].Ink == UiRole.None ? UiRole.Text : m.Way[i].Ink);
                }
                if (!string.IsNullOrEmpty(m.WayLate) && cy + UiTokens.PairRow <= foot)
                {
                    UiDraw.TextMiddle(s, at, x, cy, UiTokens.PairRow, cw, UiStyle.Body, UiRole.WarnText, m.WayLate, UiFlags.Label | UiFlags.Truncate);
                    cy += UiTokens.PairRow;
                }
                if (!string.IsNullOrEmpty(m.WayNote))
                {
                    float nh = UiDraw.Snap(UiTextRules.WrappedHeight(m.WayNote, UiStyle.Small, cw, 2));
                    if (cy + nh <= foot)
                    {
                        UiDraw.Text(s, at, x, cy, cw, UiStyle.Small, UiRole.Muted, m.WayNote, UiFlags.Label | UiFlags.Wrap, 2);
                        cy += nh + UiTokens.S2;
                    }
                }
            }
            if (!string.IsNullOrEmpty(m.ReminderLabel) && cy + UiTokens.Control + UiTokens.S4 <= foot)
            {
                cy += UiTokens.S4;
                UiDraw.TextMiddle(s, at, x, cy, UiTokens.Control, cw * 0.4f, UiStyle.Body, UiRole.Text2, m.ReminderLabel, UiFlags.Label | UiFlags.Truncate);
                float sw = Math.Min(UiPlanForm.SelectorWidth, cw * 0.6f);
                UiPlanForm.Selector(s, at, x + cw - sw, cy, sw, m.ReminderValue, m.ReminderLess, m.ReminderMore, LessCommand, MoreCommand, 0, -1);
            }
            float ax = beside ? x + cw + UiTokens.S8 : x;
            float ay = beside ? top : Math.Max(cy + UiTokens.S4, top);
            UiPlanPage.Actions(s, at, m.Actions, ActionCommand, ax, ay, beside ? Math.Min(UiPlanPage.ActionsWidth, x + w - ax) : Math.Min(UiPlanPage.ActionsWidth, w), keys);
        }
    }
}
