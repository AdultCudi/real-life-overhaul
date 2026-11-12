using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rows of the Controls page, game-free: the game's rebinding rows (OptionsKey) grouped by what they do, each under its group's
    /// heading with the sentence under its name, a second binding of one action marked as another key for it, and a mark where its
    /// key is shared with another action. The screen reads the game's rows into keys and calls this; the samples and the checks call
    /// it with keys of their own. Cost: a pass over the rows when the page is built.
    /// </summary>
    public static class OptionsPages
    {
        /// <summary>
        /// Appends the page's rows: a heading for each group and a key row for each binding of the scheme shown. <paramref name="source"/>
        /// receives the index of the key each row stands for, or -1 for a heading, in step with the rows.
        /// </summary>
        public static void KeyRows(OptionsKey[] keys, int count, int[] clash, bool gamepad, bool both, UiWords t, List<UiSettingRow> rows, List<int> source)
        {
            var order = new int[count];
            int n = OptionsRules.Arrange(keys, count, gamepad, both, order);
            int group = -1;
            for (int i = 0; i < n; i++)
            {
                int k = order[i];
                int g = OptionsRules.GroupOf(keys[k].Action);
                if (g != group)
                {
                    group = g;
                    rows.Add(new UiSettingRow { Control = UiSettingControl.None, Name = t(OptionsRules.GroupKeys[g], OptionsRules.GroupKeys[g]), Icon = OptionsRules.GroupIcons[g] });
                    source.Add(-1);
                }
                int nth = 0;
                for (int e = 0; e < i; e++)
                {
                    nth += string.Equals(keys[order[e]].Action, keys[k].Action, StringComparison.Ordinal) ? 1 : 0;
                }
                string key = OptionsRules.DescriptionKey(keys[k].Action);
                var row = new UiSettingRow
                {
                    Control = UiSettingControl.Key,
                    Name = keys[k].Label,
                    Value = keys[k].Binding,
                    Listening = keys[k].Listening,
                    Tip = nth > 0 ? t("reallife_ui_opt_another", "Another key for the same action") : key != null ? t(key, key) : null
                };
                if (clash != null && clash[k] >= 0)
                {
                    row.Mark = t("reallife_ui_opt_shared", "Shared with {action}").Replace("{action}", keys[clash[k]].Label);
                }
                rows.Add(row);
                source.Add(k);
            }
        }
    }
}
