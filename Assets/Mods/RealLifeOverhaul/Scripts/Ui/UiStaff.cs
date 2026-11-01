using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A skill on a person's page: its name, its level graded red to green, and the button that trains it where one can.</summary>
    public struct UiStaffSkill
    {
        public string Label;
        public int Value;          // 0 to 100
        public string Train;       // the train button's words; null where the skill cannot be trained now
    }

    /// <summary>A want or demand on a person's page: its kind's icon, its name, a line under it, how much it matters and whether it is met.</summary>
    public struct UiStaffDemand
    {
        public UiIcon Icon;
        public string Name;
        public string Note;        // a line under the name (the hours scheduled and worked); null for none
        public string Priority;    // "Critical"
        public UiRole PriorityTone;
        public int Met;            // 1 met, 0 not met, -1 not asked (a candidate's: nothing to meet yet)
        public string MetWords;
    }

    /// <summary>
    /// One person on the staff page, as Football Manager opens a player from the squad: the head (face, role, firm, state,
    /// facts, rating) with the actions, the skills graded with their train buttons, the work they do (where they are
    /// assigned as a choice, their satisfaction, a few facts) and their wants and demands with what each is worth to them.
    /// </summary>
    public sealed class UiStaffPerson
    {
        public UiProfileModel Profile = new UiProfileModel();
        public UiActionModel[] Actions = new UiActionModel[0];    // the danger first, the main one last (F)
        public string SkillsTitle;
        public UiStaffSkill[] Skills = new UiStaffSkill[0];
        public string WorkTitle;
        public string AssignLabel;                 // the assignment's label; null where the person has none to choose (a rival's)
        public string AssignValue;
        public bool AssignDisabled;                // held while they train, as the game hides its choice then
        public string SatisfactionLabel;
        public int Satisfaction = -1;              // 0 to 100, or -1 for none (a candidate's)
        public string[] RowLabels = new string[0];
        public string[] RowValues = new string[0];
        public string Note;                        // a line under the work: called in sick, training; null for none
        public UiRole NoteTone = UiRole.Warn;
        public string DemandsTitle;
        public UiStaffDemand[] Demands = new UiStaffDemand[0];
        public string DemandsEmpty;
        public string PeopleTitle;                 // the people beside them: who works at the same business (a candidate: your staff of that skill)
        public string PeopleCount;
        public UiPersonRow[] People = new UiPersonRow[0];
        public string PeopleMore;                  // a link to all of them in the table; null for none
        public float AssignX, AssignY, AssignW;    // written back: where the choice stood, so the page opens its list under it
    }

    /// <summary>
    /// The row under the cursor in the squad, beside the table as a management game previews a player: the face, the role,
    /// a state, the facts the table has no room for, the skill and the mood graded, a line about their demands, and the
    /// way to their page or the action that row offers.
    /// </summary>
    public sealed class UiStaffPreview
    {
        public string Name;
        public float Hue;
        public UiRole Ring = UiRole.None;
        public string Role;
        public UiIcon RoleIcon = UiIcon.Briefcase;
        public string Status;
        public UiRole StatusTone = UiRole.Muted;
        public string[] RowLabels = new string[0];
        public string[] RowValues = new string[0];
        public string[] BarLabels = new string[0];
        public int[] BarValues = new int[0];
        public string Note;                        // a line with its mark (their demands met); null for none
        public bool NoteMet;
        public string DemandsTitle;
        public UiStaffDemand[] Demands = new UiStaffDemand[0];   // their wants and demands, compact: how much each matters and whether met
        public string Link;                        // the way to their page; null for none
        public UiActionModel[] Actions = new UiActionModel[0];   // the row's own action (negotiate), full width at the foot
    }

    /// <summary>
    /// What the staff page shows: the app's tabs across, a switch on the tab band, a lead line, the squad's big table or one
    /// person's page, an open list over either, a card over everything, and the prompts.
    /// </summary>
    public sealed class UiStaffModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.People;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public string SwitchLabel;                 // a switch at the right of the tab band (the game's toggle over a tab's table); null for none
        public bool SwitchOn;
        public string Next;                        // a lead line over the table (a rival's staff: who can be poached and how)
        public UiActionModel[] LeadActions = new UiActionModel[0];
        public UiDataTableModel Table = new UiDataTableModel();
        public UiStaffPerson Person;               // one person's page; null for the squad
        public UiStaffPreview Preview;             // the row under the cursor, beside the table; null for none
        public int OpenList = -1;                  // the choice whose list is open: a filter's index, UiStaff.ListAssign or ListMass; -1 for none
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public float OptionOffset;
        public UiCardModel Card;                   // a card over the page (the training under way); null for none
        public UiPrompt[] Prompts = new UiPrompt[0];
        public float TableX, TableY;               // written back: where the table's tool row stood
    }

    /// <summary>
    /// The staff page, after Football Manager's squad screen in the Cities: Skylines II look: an app window with its tabs
    /// across, the squad as the kit's big table (faces, firms, graded skills, pay, satisfaction meters, a state chip; sorted,
    /// filtered, searched, picked several at a time for a mass action), and a person opened from it as a profile, their
    /// skills graded with a train button each, their work and their wants and demands with how much each matters and
    /// whether it is met. A list opens over a choice, a card over the page. Its blocks (<see cref="Squad"/> and
    /// <see cref="Person"/>) are public, so another app's staff tab lays out the same pages. Commands: the window's
    /// (<see cref="UiAppWindow.NavCommand"/> a tab, <see cref="UiAppWindow.ActionCommand"/> an action), the big table's from
    /// <see cref="UiDataTable.First"/>, <see cref="SwitchCommand"/>, <see cref="AssignCommand"/>, <see cref="TrainCommand"/>
    /// (a skill's index) and <see cref="OptionCommand"/> (an open list's option), and the card's while it stands.
    /// Game-free. Cost: a layout a change, the table's rows in view only.
    /// </summary>
    public static class UiStaff
    {
        public const int SwitchCommand = 200, AssignCommand = 201, TrainCommand = 202, OptionCommand = 203, PreviewCommand = 204;
        public const int PreviewLink = -1;                  // PreviewCommand's argument for the link to the page; an action's index otherwise
        public const float PreviewLeast = 1360f;            // px: the narrowest page that takes the preview beside its table
        private const float PreviewShare = 0.24f;           // of the page's width, the preview's
        private const float PeopleLeast = 2f * 60f + 40f;   // px: room for the people's title and two of them, or they are left out
        public const int ListAssign = 10, ListMass = 11;   // OpenList for the assignment's and a mass action's choice; 0 up a filter's
        private const float WorkShare = 0.5f;               // of the page's width, the skills' and work's column beside the demands
        private const float DemandRow = 48f;                // px: a demand with its line under its name
        private const float MetColumn = 136f;               // px: the met mark and its words
        private const float AssignShare = 0.6f;             // of the work column, the assignment's choice

        public static void Lay(UiScene s, UiStaffModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("staff", frame.Width, frame.Height, dark);
            UiFrame under = frame;
            if (m.Card != null)
            {
                under.Held = true;      // the card over the page has the keys: the page's buttons show no key caps under it
                under.Pointer = true;
            }
            UiAt at = UiAppWindow.Shell(s, under, m.Title, m.Icon, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (!string.IsNullOrEmpty(m.SwitchLabel) && m.Tabs.Length > 0)
            {
                // The game's toggle over a tab's table, in the room the tabs leave at the right of their band, so the table
                // under it keeps its rows; the switch hugs the band's right edge.
                frame.Window(out float wx, out float wy, out float ww, out float _);
                float band = UiFrame.Strip + wy, bandH = UiTokens.Control + UiTokens.S2 * 4f;
                float left = wx + UiTokens.S2 + TabsWidth(m.Tabs) + UiTokens.S8, right = wx + ww - UiTokens.S5;
                if (right - left >= UiTokens.S16 * 2f)
                {
                    UiDraw.Switch(s, at, left, band + (bandH - UiTokens.Control) / 2f, UiDraw.SnapDown(right - left), m.SwitchOn, m.SwitchLabel, SwitchCommand, 0, false);
                }
            }
            if (m.Person != null)
            {
                Person(s, at, m.Person, x, y, w, bottom - y, keys);
            }
            else
            {
                Squad(s, at, m, x, y, w, bottom - y, keys);
            }
            OpenList(s, at, m);
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, under, keys);
            if (m.Card != null)
            {
                UiCard.LayOver(s, m.Card, frame, keys);
            }
        }

        /// <summary>The squad: a lead line with its actions where there is one, then the big table filling the room under it.</summary>
        public static void Squad(UiScene s, UiAt at, UiStaffModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float top = y;
            if (!string.IsNullOrEmpty(m.Next) || m.LeadActions.Length > 0)
            {
                y = UiAppWindow.Lead(s, at, m.Next, m.LeadActions, x, y, w, keys);
            }
            float pw = m.Preview != null && m.Table.RowCount > 0 && w >= PreviewLeast ? UiDraw.SnapDown(w * PreviewShare) : 0f;
            m.TableX = x;
            m.TableY = y;
            UiDataTable.Lay(s, at, m.Table, x, y, pw > 0f ? w - pw - UiTokens.S6 : w, top + h - y, keys);
            if (pw > 0f)
            {
                Preview(s, at, m.Preview, x + w - pw, y, pw, top + h - y, keys);
            }
        }

        /// <summary>
        /// The row under the cursor beside the table, on a band as tall as what it says: the face in its ring with the name,
        /// the role and a state, the facts as rows, the skill and the mood graded on one label column, a line about the
        /// demands, the link to the page, and the row's own action at the foot.
        /// </summary>
        public static void Preview(UiScene s, UiAt at, UiStaffPreview p, float x, float y, float w, float h, IUiKeys keys)
        {
            float inner = w - UiTokens.S4 * 2f, ix = x + UiTokens.S4;
            float face = UiTokens.AvatarHero, ring = UiTokens.S1;
            float content = UiTokens.S4 + face + ring * 2f + UiTokens.S4 + p.RowLabels.Length * UiTokens.PairRow
                            + (p.BarLabels.Length > 0 ? UiTokens.S3 + p.BarLabels.Length * UiTokens.PairRow : 0f)
                            + (string.IsNullOrEmpty(p.Note) ? 0f : UiTokens.S3 + UiTokens.PairRow) + (string.IsNullOrEmpty(p.Link) ? 0f : UiTokens.S2 + UiTokens.Control)
                            + (p.Demands.Length > 0 ? UiTokens.S6 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + p.Demands.Length * UiTokens.ControlSmall : 0f)
                            + (p.Actions.Length > 0 ? UiTokens.S4 + p.Actions.Length * (UiTokens.Control + UiTokens.S2) - UiTokens.S2 : 0f) + UiTokens.S4;
            float ph = UiDraw.Snap(Math.Min(h, content));
            int band = UiDraw.Box(s, at, x, y, w, ph, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float py = y + UiTokens.S4;
            UiDraw.Portrait(s, on, ix + ring, py + ring, face, p.Name, p.Hue, p.Ring);
            float tx = ix + face + ring * 2f + UiTokens.S4, tw = x + w - UiTokens.S4 - tx;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float block = lh + UiTokens.PairRow + (string.IsNullOrEmpty(p.Status) ? 0f : UiTokens.S1 + UiTokens.Badge + UiTokens.S1);
            float ty = py + UiDraw.SnapDown((face + ring * 2f - block) / 2f);
            UiDraw.Text(s, on, tx, ty, tw, UiStyle.Title, UiRole.Text, p.Name, UiFlags.Truncate);
            ty += lh;
            UiDraw.Icon(s, on, tx, ty + (UiTokens.PairRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, p.RoleIcon, UiRole.None, UiFlags.Middle, true);
            UiDraw.TextMiddle(s, on, tx + UiTokens.IconSmall + UiTokens.S2, ty, UiTokens.PairRow, tw - UiTokens.IconSmall - UiTokens.S2, UiStyle.Small, UiRole.Text2, p.Role,
                UiFlags.Truncate);
            ty += UiTokens.PairRow + UiTokens.S1;
            if (!string.IsNullOrEmpty(p.Status))
            {
                UiDraw.Chip(s, on, tx, ty, p.Status, p.StatusTone, tw);
            }
            py += face + ring * 2f + UiTokens.S4;
            for (int i = 0; i < p.RowLabels.Length && i < p.RowValues.Length; i++)
            {
                py += UiDraw.InfoRow(s, on, ix, py, inner, p.RowLabels[i], p.RowValues[i]);
            }
            if (p.BarLabels.Length > 0)
            {
                py += UiTokens.S3;
                float lw = UiDraw.LabelColumn(inner, p.BarLabels);
                for (int i = 0; i < p.BarLabels.Length && i < p.BarValues.Length; i++)
                {
                    py += UiDraw.GradeBar(s, on, ix, py, inner, p.BarLabels[i], p.BarValues[i], lw);
                }
            }
            if (!string.IsNullOrEmpty(p.Note))
            {
                py += UiTokens.S3;
                py += UiDraw.CheckLine(s, on, ix, py, inner, p.Note, p.NoteMet);
            }
            if (p.Demands.Length > 0)
            {
                py += UiTokens.S6;
                UiDraw.Text(s, on, ix, py, inner, UiStyle.SectionHeader, UiRole.Text, p.DemandsTitle, UiFlags.Label | UiFlags.Truncate);
                py += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                float foot = y + ph - UiTokens.S4 - (string.IsNullOrEmpty(p.Link) ? 0f : UiTokens.S2 + UiTokens.Control);
                for (int i = 0; i < p.Demands.Length && py + UiTokens.ControlSmall <= foot; i++)
                {
                    PreviewDemand(s, on, ref p.Demands[i], ix, py, inner);
                    py += UiTokens.ControlSmall;
                }
            }
            if (!string.IsNullOrEmpty(p.Link))
            {
                py += UiTokens.S2;
                py += UiDraw.LinkRow(s, on, ix, py, inner, UiTokens.Control, p.Link, PreviewCommand, PreviewLink);
            }
            if (p.Actions.Length > 0)
            {
                py += UiTokens.S4;
                for (int i = 0; i < p.Actions.Length; i++)
                {
                    ref UiActionModel a = ref p.Actions[i];
                    UiDraw.Button(s, on, ix, py, inner, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, PreviewCommand, i, a.Disabled);
                    py += UiTokens.Control + UiTokens.S2;
                }
            }
        }

        // A demand on one compact row of the preview: its kind's glyph, its name, how much it matters, and whether it is met.
        private static void PreviewDemand(UiScene s, UiAt at, ref UiStaffDemand d, float x, float y, float w)
        {
            float h = UiTokens.ControlSmall;
            UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, d.Icon, UiRole.None, UiFlags.Middle, true);
            float right = x + w;
            if (d.Met >= 0)
            {
                right -= UiTokens.Icon;
                UiDraw.Icon(s, at, right, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, d.Met > 0 ? UiIcon.Check : UiIcon.Warning, UiRole.None, UiFlags.Middle, true);
                right -= UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(d.Priority))
            {
                float chip = Math.Min(UiDraw.SnapDown(w * 0.42f), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(d.Priority, UiStyle.SmallStrong) + UiTokens.S1));
                right -= chip;
                UiDraw.Chip(s, at, right, y + (h - UiTokens.Badge - UiTokens.S1) / 2f, d.Priority, d.PriorityTone, chip);
                right -= UiTokens.S2;
            }
            float tx = x + UiTokens.Icon + UiTokens.S2;
            UiDraw.TextMiddle(s, at, tx, y, h, right - tx, UiStyle.Body, UiRole.Text, d.Name, UiFlags.Truncate);
        }

        /// <summary>
        /// One person: the head across with the actions at its right, then two columns, the skills and the work at the left
        /// and the wants and demands at the right.
        /// </summary>
        public static void Person(UiScene s, UiAt at, UiStaffPerson p, float x, float y, float w, float h, IUiKeys keys)
        {
            float bottom = y + h;
            y = UiAppWindow.Profile(s, at, p.Profile, p.Actions, x, y, w, keys);
            float lw = UiDraw.SnapDown(w * WorkShare), gap = UiTokens.S8, rw = w - lw - gap;
            float ly = Skills(s, at, p, x, y, lw, keys);
            ly = Work(s, at, p, x, ly, lw, keys);
            if (p.People.Length > 0 && bottom - ly >= PeopleLeast)
            {
                // Who works beside them, as Football Manager lists a player's team-mates: as many as fit, then the way to all.
                UiAppWindow.People(s, at, p.PeopleTitle, p.PeopleCount, p.People, p.PeopleMore, null, UiRole.None, null, x, ly, lw, bottom - ly);
            }
            Demands(s, at, p, x + lw + gap, y, rw, bottom - y);
        }

        // The skills as graded bars on one label column, each with its train button at the right where the game offers it.
        private static float Skills(UiScene s, UiAt at, UiStaffPerson p, float x, float y, float w, IUiKeys keys)
        {
            if (p.Skills.Length == 0)
            {
                return y;
            }
            y = Title(s, at, p.SkillsTitle, x, y, w);
            float trainW = 0f;
            var labels = new string[p.Skills.Length];
            for (int i = 0; i < p.Skills.Length; i++)
            {
                labels[i] = p.Skills[i].Label;
                if (!string.IsNullOrEmpty(p.Skills[i].Train))
                {
                    trainW = Math.Max(trainW, UiDraw.ButtonWidth(UiButtonKind.Secondary, p.Skills[i].Train, UiIcon.Cap, UiAction.Count, keys));
                }
            }
            float bw = trainW > 0f ? w - trainW - UiTokens.S4 : w;
            float lw = UiDraw.LabelColumn(bw, labels);
            for (int i = 0; i < p.Skills.Length; i++)
            {
                float ry = y + i * UiTokens.Control;
                UiDraw.GradeBar(s, at, x, ry + (UiTokens.Control - UiTokens.PairRow) / 2f, bw, p.Skills[i].Label, p.Skills[i].Value, lw);
                if (!string.IsNullOrEmpty(p.Skills[i].Train))
                {
                    UiDraw.Button(s, at, x + w - trainW, ry + (UiTokens.Control - UiTokens.ControlSmall) / 2f, trainW, UiTokens.ControlSmall, UiButtonKind.Secondary,
                        p.Skills[i].Train, UiIcon.Cap, UiAction.Count, keys, TrainCommand, i);
                }
            }
            return y + p.Skills.Length * UiTokens.Control + UiTokens.S6;
        }

        // The work: where they are assigned as the game's choice, their satisfaction graded, a few facts as rows, and a note.
        private static float Work(UiScene s, UiAt at, UiStaffPerson p, float x, float y, float w, IUiKeys keys)
        {
            y = Title(s, at, p.WorkTitle, x, y, w);
            if (p.AssignLabel != null)
            {
                float cw = UiDraw.SnapDown(w * AssignShare);
                UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, w - cw - UiTokens.S3, UiStyle.Body, UiRole.Text2, p.AssignLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.Choice(s, at, x + w - cw, y, cw, p.AssignValue, AssignCommand, 0, p.AssignDisabled);
                p.AssignX = x + w - cw;
                p.AssignY = y;
                p.AssignW = cw;
                y += UiTokens.Control + UiTokens.S2;
            }
            if (p.Satisfaction >= 0)
            {
                y += UiDraw.GradeBar(s, at, x, y, w, p.SatisfactionLabel, p.Satisfaction, UiDraw.LabelColumn(w, p.SatisfactionLabel, p.RowLabels.Length > 0 ? p.RowLabels[0] : null));
            }
            for (int i = 0; i < p.RowLabels.Length && i < p.RowValues.Length; i++)
            {
                y += UiDraw.InfoRow(s, at, x, y, w, p.RowLabels[i], p.RowValues[i]);
            }
            if (!string.IsNullOrEmpty(p.Note))
            {
                y += UiTokens.S3;
                bool warns = p.NoteTone == UiRole.Warn || p.NoteTone == UiRole.Danger;   // a warning wears the warning's own glyph, never a recoloured info mark
                UiDraw.Icon(s, at, x, y + UiTokens.Hairline, UiTokens.Icon, warns ? UiIcon.Warning : UiIcon.Info, warns ? p.NoteTone : UiRole.None, UiFlags.Middle, true);
                int words = UiDraw.Text(s, at, x + UiTokens.Icon + UiTokens.S2, y, w - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text2, p.Note, UiFlags.Label | UiFlags.Wrap, 2);
                y = UiDraw.Snap(s.Items[words].Y + s.Items[words].H);
            }
            return y + UiTokens.S6;
        }

        // The wants and demands: each its kind's glyph on a tile, its name and a line under it, how much it matters as a chip
        // and whether it is met, as many as the room holds.
        private static void Demands(UiScene s, UiAt at, UiStaffPerson p, float x, float y, float w, float h)
        {
            float bottom = y + h;
            y = Title(s, at, p.DemandsTitle, x, y, w);
            if (p.Demands.Length == 0)
            {
                UiDraw.Text(s, at, x, y, w, UiStyle.Body, UiRole.Text2, p.DemandsEmpty, UiFlags.Label | UiFlags.Wrap, 3);
                return;
            }
            bool met = false;
            for (int i = 0; i < p.Demands.Length; i++)
            {
                met |= p.Demands[i].Met >= 0;
            }
            float tile = UiTokens.ControlSmall;
            float chipMost = UiDraw.SnapDown(w * 0.28f);
            for (int i = 0; i < p.Demands.Length && y + DemandRow <= bottom; i++)
            {
                ref UiStaffDemand d = ref p.Demands[i];
                int face = UiDraw.Tile(s, at, x, y + (DemandRow - tile) / 2f, tile, UiIconTints.Of(d.Icon), false);
                UiDraw.Icon(s, at.In(face), x + (tile - UiTokens.Icon) / 2f, y + (DemandRow - UiTokens.Icon) / 2f, UiTokens.Icon, d.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                float right = x + w;
                if (met)
                {
                    right -= MetColumn;
                    if (d.Met >= 0)
                    {
                        UiDraw.CheckLine(s, at, right + UiTokens.S3, y + (DemandRow - UiTokens.PairRow) / 2f, MetColumn - UiTokens.S3, d.MetWords, d.Met > 0);
                    }
                }
                if (!string.IsNullOrEmpty(d.Priority))
                {
                    float chip = Math.Min(chipMost, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(d.Priority, UiStyle.SmallStrong) + UiTokens.S1));
                    right -= chip;
                    UiDraw.Chip(s, at, right, y + (DemandRow - UiTokens.Badge - UiTokens.S1) / 2f, d.Priority, d.PriorityTone, chip);
                    right -= UiTokens.S3;
                }
                float tx = x + tile + UiTokens.S3, tw = right - tx;
                if (string.IsNullOrEmpty(d.Note))
                {
                    UiDraw.TextMiddle(s, at, tx, y, DemandRow, tw, UiStyle.Title, UiRole.Text, d.Name, UiFlags.Truncate);
                }
                else
                {
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                    float top = y + (DemandRow - lh - sh) / 2f;
                    UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, d.Name, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, UiRole.Text2, d.Note, UiFlags.Truncate | UiFlags.Middle);
                }
                y += DemandRow;
            }
        }

        // The tabs' run across their band, as the window lays them (each its tile, its name and its count, a step apart).
        private static float TabsWidth(UiNavEntry[] tabs)
        {
            float w = 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                float label = UiDraw.Snap(UiTextRules.Width(tabs[i].Label, UiStyle.Nav) + UiTokens.S1);
                float count = string.IsNullOrEmpty(tabs[i].Count) ? 0f : UiDraw.Snap(UiTextRules.Width(tabs[i].Count, UiStyle.Small) + UiTokens.S1);
                w += UiTokens.S3 + UiTokens.ControlSmall + UiTokens.S3 + label + (count > 0f ? UiTokens.S2 + count : 0f) + UiTokens.S4 + (i > 0 ? UiTokens.S1 : 0f);
            }
            return w;
        }

        // A block's title in capitals, as the window's blocks head theirs. Returns the y under it.
        private static float Title(UiScene s, UiAt at, string title, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(title))
            {
                return y;
            }
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            return y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
        }

        // The open list of a choice, on the overlay under its field: a filter's, the assignment's or a mass action's.
        private static void OpenList(UiScene s, UiAt at, UiStaffModel m)
        {
            if (m.OpenList < 0 || m.Options.Length == 0)
            {
                return;
            }
            float fx, fy, fw;
            if (m.OpenList == ListAssign && m.Person != null)
            {
                fx = m.Person.AssignX;
                fy = m.Person.AssignY;
                fw = m.Person.AssignW;
            }
            else if (m.OpenList == ListMass && m.Person == null)
            {
                fx = m.Table.MassFieldX;
                fy = m.TableY;
                fw = m.Table.MassFieldW;
            }
            else if (m.OpenList >= 0 && m.OpenList < m.Table.Filters.Length && m.Person == null)
            {
                fx = UiDataTable.FilterLeft(m.Table, m.OpenList, m.TableX);
                fy = m.TableY;
                fw = Math.Max(UiDataTable.FilterWidthOf(m.Table.Filters[m.OpenList]), UiTokens.S16 * 4f);
            }
            else
            {
                return;
            }
            if (fw <= 0f)
            {
                return;
            }
            UiDraw.ChoiceList(s, at, fx, fy + UiTokens.Control + UiTokens.S1, fw, m.Options, m.Options.Length, m.OptionChosen, m.OptionOffset, OptionCommand);
        }
    }
}
