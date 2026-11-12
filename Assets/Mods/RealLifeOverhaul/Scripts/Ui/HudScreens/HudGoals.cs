using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Localizor.LanguageChangeEvent;
using Player.PlayerMissions;
using TMPro;
using UI;
using UI.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The objectives at the left, mirrored from the game's objectives window (TasksUI) as the game and the mods fill it: its
    /// groups in its order (the business alerts, Uncle Fred, the side quests, the delivery missions, the workout plan, the
    /// mod's construction and any group another mod adds) with how many are open or done, each line's words, tick, place and
    /// way out, a high priority alert marked, a mission's quieter lines and the delivery's way so far as a meter; after the
    /// alerts, the game's personal goals now with their progress (HudGoalBook), their lines pressing nothing. A line, its locate
    /// link and its Cancel press the entry's own buttons, so the game does what its window does; the strip folds the game's
    /// own window (TasksUI.SetCollapsedState) and each group folds for the session. The panel never runs into the bar: the
    /// last open groups fold first, then a long group shows its first lines. The game's window stays up, hidden by leaf.
    /// Cost: four reads a second of the window's entries while the HUD shows.
    /// </summary>
    internal static class HudGoals
    {
        private const int MostGroups = 12, MostLines = 24;
        private const string DeliveryKey = "ba:skill_deliverydriver";   // the delivery driver's group, as the game titles it
        private const string AlertsKey = "bizman_alerts";               // the business alerts' group: the personal goals' band follows it

        private static readonly UiConceal Hidden = new UiConceal();
        private static readonly UiObjectivesModel Model = new UiObjectivesModel { Groups = new UiObjectiveGroup[MostGroups], GroupCount = 0 };
        private static readonly Transform[] GroupOf = new Transform[MostGroups];
        private static readonly Transform[][] LineOf = new Transform[MostGroups][];
        private static readonly Dictionary<string, bool> Folded = new Dictionary<string, bool>();   // the player's own fold per group's words, this session
        private static readonly bool[] AutoFolded = new bool[MostGroups];

        private static TasksUI _tasks;
        private static CanvasGroup _shown;
        private static Transform _content;
        private static TMP_Text _title;
        private static bool _on;

        static HudGoals()
        {
            for (int g = 0; g < MostGroups; g++)
            {
                Model.Groups[g].Lines = new UiObjectiveLine[MostLines];
                LineOf[g] = new Transform[MostLines];
            }
        }

        /// <summary>The game's window hidden by leaf; false while it is not built.</summary>
        public static bool Begin(UIs uis)
        {
            _tasks = uis != null ? uis.tasksUI : null;
            Transform panel = _tasks != null ? _tasks.transform.Find("Container/Panel") : null;
            if (panel == null)
            {
                return false;
            }
            _shown = _tasks.GetComponent<CanvasGroup>();
            _content = panel.Find("Viewport/Content");
            Transform head = panel.Find("Headline/Label");
            _title = head != null ? head.GetComponent<TMP_Text>() : null;
            Hidden.Add(panel);
            Hidden.Add(_tasks.transform.Find("ExpandButton"));
            Hidden.Hide();
            _on = true;
            return true;
        }

        /// <summary>The game's window given back as found.</summary>
        public static void End()
        {
            if (!_on)
            {
                return;
            }
            _on = false;
            Hidden.Clear();
        }

        /// <summary>A new city: the window is looked for again and the folds forgotten.</summary>
        public static void Rearm()
        {
            End();
            _tasks = null;
            _shown = null;
            _content = null;
            _title = null;
            Folded.Clear();
            Array.Clear(GroupOf, 0, GroupOf.Length);
            HudGoalBook.Forget();
        }

        /// <summary>The window read again into the HUD's model, fitted above the bar; the signature of what shows.</summary>
        public static int Look(UiHudModel model, float frameHeight)
        {
            if (!_on || _tasks == null || _content == null)
            {
                model.Objectives = null;
                return 0;
            }
            // The game hides its window by its own group's alpha (the full menu, a mini game): the HUD's goes with it.
            if (!_tasks.gameObject.activeInHierarchy || (_shown != null && _shown.alpha < 0.5f))
            {
                model.Objectives = null;
                return 1;
            }
            int sig;
            try
            {
                sig = Read();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's objectives");
                model.Objectives = null;
                return 2;
            }
            Fit(frameHeight);
            model.Objectives = Model;
            unchecked
            {
                sig = sig * 31 + (Model.Collapsed ? 1 : 2);
                for (int g = 0; g < Model.GroupCount; g++)
                {
                    sig = sig * 31 + (Model.Groups[g].Collapsed ? 1 : 2) + Model.Groups[g].LineCount * 4;
                }
            }
            return sig;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Read()
        {
            UiObjectivesModel m = Model;
            m.Title = _title != null ? UiWidgets.Text(_title) : UiText.T("tutorial_objectives");
            m.Collapsed = _tasks.IsCollapsed;
            m.LocateLabel = UiText.T("reallife_ui_hud_locate");
            m.EmptyTitle = UiText.T("reallife_ui_hud_goals_empty");
            m.EmptyBody = UiText.T("reallife_ui_hud_goals_empty_body");
            int groups = 0, open = 0, sig = Hash(m.Title);
            bool goals = GoalsKept() && HudGoalBook.Count > 0;
            int after = goals ? AlertsAt() : -1;   // the goals' band after the alerts' group, first without one
            for (int i = 0; i < _content.childCount && groups < MostGroups; i++)
            {
                if (goals && i > after)
                {
                    groups += GoalsBand(groups, ref sig);
                    goals = false;
                }
                Transform group = _content.GetChild(i);
                if (!group.gameObject.activeSelf || group.name == "GroupTemplate" || groups >= MostGroups)
                {
                    continue;
                }
                ref UiObjectiveGroup g = ref m.Groups[groups];
                string key = KeyOf(group, out TMP_Text head);
                int lines = Lines(group, groups, ref g, key == DeliveryKey, ref open, ref sig);
                if (lines == 0)
                {
                    continue;   // the game counts an empty group as not shown
                }
                g.Title = Words(head);
                g.Icon = IconOf(key, group);
                int done = 0;
                for (int l = 0; l < lines; l++)
                {
                    done += g.Lines[l].Done ? 1 : 0;
                }
                g.Note = HudNoteRules.GroupNote(done, lines, UiText.T("reallife_ui_hud_goals_done"));   // "3" open, or "1 of 3 done"
                g.LineCount = lines;
                g.Collapsed = Folded.TryGetValue(g.Title ?? string.Empty, out bool f) && f;
                GroupOf[groups] = group;
                AutoFolded[groups] = false;
                sig = unchecked(sig * 31 + Hash(g.Title) + lines);
                groups++;
            }
            if (goals && groups < MostGroups)
            {
                groups += GoalsBand(groups, ref sig);   // the window's groups are all before it
            }
            m.GroupCount = groups;
            m.Meta = open > 0 ? UiText.T("reallife_ui_hud_goals_open").Replace("{count}", open.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
            return unchecked(sig * 31 + open);
        }

        // A group's key, as the game titled it, and its headline's label.
        private static string KeyOf(Transform group, out TMP_Text head)
        {
            head = Text(group, "Headline/Label");
            TextLocalizationComponent named = head != null ? head.GetComponent<TextLocalizationComponent>() : null;
            return named != null ? named.Key : null;
        }

        // The window's child the business alerts are in; -1 for none.
        private static int AlertsAt()
        {
            for (int i = 0; i < _content.childCount; i++)
            {
                Transform group = _content.GetChild(i);
                if (group.gameObject.activeSelf && group.name != "GroupTemplate" && KeyOf(group, out _) == AlertsKey)
                {
                    return i;
                }
            }
            return -1;
        }

        // The game's personal goals read when due (HudGoalBook); none on a game without them.
        private static bool GoalsKept()
        {
            try
            {
                HudGoalBook.Keep();
                return true;
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the personal goals on the HUD's objectives");
                return false;
            }
        }

        // The personal goals' band: the first kinds' goals now, each with its progress, and at its right all the goals done.
        private static int GoalsBand(int index, ref int sig)
        {
            ref UiObjectiveGroup g = ref Model.Groups[index];
            int n = Math.Min(HudGoalBook.Count, MostLines);
            for (int k = 0; k < n; k++)
            {
                g.Lines[k] = new UiObjectiveLine { Text = HudGoalBook.Titles[k], Progress = HudGoalBook.Progress[k] };
                LineOf[index][k] = null;   // nothing of the game's behind it: a press does nothing
                sig = unchecked(sig * 31 + Hash(g.Lines[k].Text) + Hash(g.Lines[k].Progress) * 3);
            }
            g.Title = UiText.T("common_personal_goals");
            g.Icon = UiIcon.Trophy;
            g.Note = HudNoteRules.DoneNote(HudGoalBook.Done, HudGoalBook.Total, UiText.T("reallife_ui_hud_goals_done"));
            g.LineCount = n;
            g.Collapsed = Folded.TryGetValue(g.Title ?? string.Empty, out bool f) && f;
            GroupOf[index] = null;
            AutoFolded[index] = false;
            sig = unchecked(sig * 31 + Hash(g.Title) + Hash(g.Note) + n);
            return 1;
        }

        // A group's entries as lines: a plain entry's words, tick, place and way out; a mission's nested entry's address
        // with its items under it; in the delivery driver's group, its timed line with Cancel carries the way so far.
        private static int Lines(Transform group, int index, ref UiObjectiveGroup g, bool delivery, ref int open, ref int sig)
        {
            int lines = 0;
            Transform[] of = LineOf[index];
            for (int i = 0; i < group.childCount && lines < MostLines; i++)
            {
                Transform entry = group.GetChild(i);
                if (!entry.gameObject.activeSelf || entry.name == "Headline" || entry.name == "EntryTemplate" || entry.name == "NestedEntryTemplate")
                {
                    continue;
                }
                CanvasGroup fade = entry.GetComponent<CanvasGroup>();
                if (fade != null && fade.alpha < 0.05f)
                {
                    continue;   // a finished alert fading out
                }
                TMP_Text label = Text(entry, "Label") ?? Text(entry, "Task/Label");
                string words = Words(label);
                if (string.IsNullOrEmpty(words))
                {
                    continue;
                }
                Toggle tick = Child<Toggle>(entry, "Checkmark");
                bool done = (tick != null && tick.gameObject.activeSelf && tick.isOn) || (label.fontStyle & FontStyles.Strikethrough) != 0;
                Button close = Child<Button>(entry, "CloseButton");
                bool closes = close != null && close.gameObject.activeSelf;
                ref UiObjectiveLine line = ref g.Lines[lines];
                line.Text = words;
                line.Done = done;
                line.Progress = null;
                line.Locate = UiWidgets.There(Child<Button>(entry, "DestinationButton"));
                line.Sub = Subtasks(entry);
                line.Close = closes ? UiWidgets.Label(close) : null;
                line.Urgent = !done && HudSides.Tone(label.color, UiRole.None) == UiRole.Danger;
                line.HasShare = delivery && closes && DeliveryShare(out line.Share);
                of[lines] = entry;
                if (!done)
                {
                    open++;
                }
                unchecked
                {
                    sig = sig * 31 + words.GetHashCode() + (done ? 1 : 0) + (line.Locate ? 2 : 0) + (line.Urgent ? 4 : 0) + Hash(line.Sub) * 3 + Hash(line.Close) * 5
                          + (line.HasShare ? (int)(line.Share * 100f) * 7 : 0);
                }
                lines++;
            }
            return lines;
        }

        // A nested entry's items, as the game lists them under its address.
        private static string Subtasks(Transform entry)
        {
            Transform subs = entry.Find("Task/Subtasks");
            if (subs == null)
            {
                return null;
            }
            string all = null;
            for (int i = 0; i < subs.childCount; i++)
            {
                Transform sub = subs.GetChild(i);
                if (!sub.gameObject.activeSelf)
                {
                    continue;
                }
                string words = UiWidgets.Text(sub.GetComponent<TMP_Text>());
                if (!string.IsNullOrEmpty(words))
                {
                    all = all == null ? words : all + ", " + words;
                }
            }
            return all;
        }

        // The delivery driver's way so far; none on a game without the mission's pieces.
        private static bool DeliveryShare(out float share)
        {
            try
            {
                return Delivery(out share);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the delivery driver's share on the HUD's objectives");
                share = 0f;
                return false;
            }
        }

        // The delivery driver's way so far, from the game's own mission: deliveries made of its stops.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Delivery(out float share)
        {
            share = 0f;
            if (!(SaveGameManager.Current?.currentPlayerMission is DeliveryDriverMission mission) || mission.destinations == null || mission.destinations.Count == 0)
            {
                return false;
            }
            share = Mathf.Clamp01(mission.GetCompletedDeliveries() / (float)mission.destinations.Count);
            return true;
        }

        // Each group's glyph by the key the game titled it with; the side quests and other mods' groups share the flag.
        private static UiIcon IconOf(string key, Transform group)
        {
            switch (key)
            {
                case "bizman_alerts": return UiIcon.ListChecks;
                case "uncle_fred": return UiIcon.Person;
                case "food_delivery_job_title": return UiIcon.ForkKnife;
                case DeliveryKey: return UiIcon.Truck;
                case "workout_plan": return UiIcon.Heartbeat;
                case "reallife_bld_track_header": return UiIcon.Hammer;
                case "common_personal_goals": return UiIcon.Trophy;
            }
            return UiIcon.Flag;
        }

        // Too tall for the room above the bar: the last open groups fold, then the last group's lines are cut.
        private static void Fit(float frameHeight)
        {
            UiObjectivesModel m = Model;
            if (m.Collapsed)
            {
                return;
            }
            float room = frameHeight - UiTokens.S4 - UiFrame.BandHeight - UiTokens.S4 - UiDraw.KeyChipHeight - UiTokens.S3;
            // A group the player opened stays open while another can fold instead.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int g = m.GroupCount - 1; g > 0 && UiObjectives.Height(m) > room; g--)
                {
                    bool opened = Folded.TryGetValue(m.Groups[g].Title ?? string.Empty, out bool f) && !f;
                    if (!m.Groups[g].Collapsed && (pass == 1 || !opened))
                    {
                        m.Groups[g].Collapsed = true;
                        AutoFolded[g] = true;
                    }
                }
            }
            while (m.GroupCount > 1 && UiObjectives.Height(m) > room)
            {
                m.GroupCount--;   // more groups than bands fit: the last wait under the strip's count
            }
            for (int g = 0; g < m.GroupCount; g++)
            {
                while (!m.Groups[g].Collapsed && m.Groups[g].LineCount > 1 && UiObjectives.Height(m) > room)
                {
                    m.Groups[g].LineCount--;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- presses

        /// <summary>The objectives' commands: the strip folds the game's window, a band folds its group, a line, its locate link and its way out press the entry's own buttons.</summary>
        public static bool Press(int command, int arg)
        {
            if (_tasks == null)
            {
                return false;
            }
            if (command == UiObjectives.FoldCommand)
            {
                _tasks.SetCollapsedState(!_tasks.IsCollapsed);
                return true;
            }
            if (command == UiObjectives.GroupCommand)
            {
                if (arg < 0 || arg >= Model.GroupCount)
                {
                    return false;
                }
                Folded[Model.Groups[arg].Title ?? string.Empty] = !Model.Groups[arg].Collapsed;   // what shows flips, and stays so
                return true;
            }
            int group = arg / 32, line = arg % 32;
            if (group < 0 || group >= Model.GroupCount || line < 0 || line >= Model.Groups[group].LineCount)
            {
                return false;
            }
            Transform entry = LineOf[group][line];
            if (entry == null)
            {
                return false;
            }
            switch (command)
            {
                case UiObjectives.LineCommand:
                    return UiWidgets.Press(entry.GetComponent<Button>());
                case UiObjectives.LocateCommand:
                    return UiWidgets.Press(Child<Button>(entry, "DestinationButton"));
                case UiObjectives.CloseCommand:
                    return UiWidgets.Press(Child<Button>(entry, "CloseButton"));
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- shared

        private static TMP_Text Text(Transform parent, string path) => Child<TMP_Text>(parent, path);

        private static string Words(TMP_Text t) => t != null ? UiWidgets.Text(t) : null;

        private static int Hash(string s) => s != null ? s.GetHashCode() : 0;

        private static T Child<T>(Transform parent, string path) where T : Component
        {
            Transform t = parent != null ? parent.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }
    }
}
