using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A screen of the game studio or another tech firm (StudioViews, SectorViews: StudioScreen) as the kit's form: its
    /// title and what it is, what to do next, each section's lines and choices (a selector, or a slider of steps, with
    /// what the pick gives under it), and the primary action with at most two others at the foot. The fields' choices
    /// and the foot's actions are handed back so a press reaches the feature's own act. Game-free.
    /// Cost: a model a screen, when it opens or a pick changes.
    /// </summary>
    public static class StudioPages
    {
        public const int MostOthers = 2;   // actions at the foot beside the primary: a card keeps to a few

        public static UiFormModel Model(StudioScreen v, UiWords t, List<ViewChoice> choices, List<ViewAction> actions)
        {
            choices.Clear();
            actions.Clear();
            var m = new UiFormModel { Title = Fill(t, v.TitleKey, v.TitlePairs) };
            m.About = string.IsNullOrEmpty(v.ChipKey) ? v.Subtitle : (string.IsNullOrEmpty(v.Subtitle) ? t(v.ChipKey, v.ChipKey) : v.Subtitle + " (" + t(v.ChipKey, v.ChipKey) + ")");
            var fields = new List<UiFieldModel>(16);
            if (v.Next != null)
            {
                fields.Add(Note(Line(t, v.Next), true, null));
                choices.Add(null);
            }
            foreach (ViewSection section in v.Sections)
            {
                string head = t(section.TitleKey, section.TitleKey);
                foreach (ViewLine line in section.Lines)
                {
                    fields.Add(Note(Line(t, line), false, head));
                    choices.Add(null);
                    head = null;
                }
                foreach (ViewChoice c in section.Choices)
                {
                    int count = c.Options.Count;
                    int picked = count == 0 ? 0 : Math.Max(0, Math.Min(count - 1, c.Picked));
                    fields.Add(new UiFieldModel
                    {
                        Kind = c.Slider ? UiFieldKind.Slider : UiFieldKind.Choice, Section = head, Label = t(c.LabelKey, c.LabelKey),
                        Value = count > 0 ? c.Options[picked] : string.Empty, Share = count > 1 ? picked / (float)(count - 1) : 0f
                    });
                    choices.Add(c);
                    head = null;
                    if (c.Trade != null)
                    {
                        fields.Add(Note(Line(t, c.Trade), false, null));
                        choices.Add(null);
                    }
                    if (c.Go != null && c.Go.Enabled && actions.Count < MostOthers + 1)
                    {
                        actions.Add(c.Go);
                    }
                }
            }
            if (fields.Count == 0 && !string.IsNullOrEmpty(v.EmptyKey))
            {
                fields.Add(Note(t(v.EmptyKey, v.EmptyKey), false, null));
                choices.Add(null);
            }
            for (int i = 0; i < v.Others.Count && actions.Count < MostOthers; i++)
            {
                actions.Add(v.Others[i]);
            }
            if (v.Primary != null)
            {
                actions.Add(v.Primary);
            }
            m.Fields = fields.ToArray();
            m.FieldCount = m.Fields.Length;
            m.Actions = new UiActionModel[actions.Count];
            int keyed = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                ViewAction a = actions[i];
                bool primary = a == v.Primary;
                m.Actions[i] = new UiActionModel
                {
                    Label = t(a.Key, a.Key), Kind = primary ? UiButtonKind.Primary : a.Danger ? UiButtonKind.Destructive : UiButtonKind.Secondary,
                    Key = primary ? UiAction.Alt : keyed++ == 0 ? UiAction.Option : UiAction.Count, Command = UiForm.ActionCommand, Disabled = !a.Enabled
                };
            }
            m.ActionCount = m.Actions.Length;
            bool picks = false;
            for (int i = 0; i < choices.Count && !picks; i++)
            {
                picks = choices[i] != null;
            }
            var prompts = new List<UiPrompt>(4)
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                // Enter steps a choice where the page has one. On a page of lines and buttons it presses the button in focus.
                new UiPrompt { Action = UiAction.Confirm, Label = picks ? t("reallife_ui_prompt_change", "Change") : t("reallife_ui_prompt_select", "Select") },
            };
            if (v.Primary != null)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = t(v.Primary.Key, v.Primary.Key) });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            m.Prompts = prompts.ToArray();
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        /// <summary>The value a choice sends when it steps to its next option (the options' own values where they have them).</summary>
        public static int NextValue(ViewChoice c)
        {
            int count = c.Options.Count;
            if (count == 0)
            {
                return 0;
            }
            int next = (Math.Max(0, c.Picked) + 1) % count;
            return c.Values.Count == count ? c.Values[next] : next;
        }

        private static UiFieldModel Note(string words, bool strong, string section) => new UiFieldModel { Kind = UiFieldKind.Note, Label = words, On = strong, Section = section };

        private static string Line(UiWords t, ViewLine l) => Fill(t, l.Key, l.Pairs);

        // A key's words with its names filled: name, value, name, value (as the game's own Fill does).
        private static string Fill(UiWords t, string key, string[] pairs)
        {
            string words = string.IsNullOrEmpty(key) ? string.Empty : t(key, key);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                words = words.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return words;
        }
    }
}
