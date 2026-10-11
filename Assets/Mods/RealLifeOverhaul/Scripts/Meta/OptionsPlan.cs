using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Meta
{
    /// <summary>What kind of row the page built.</summary>
    public enum PlanKind
    {
        Toggle,
        Slider,
        Choice,
        Button,
        Key       // a key of the mod's, changed by a press
    }

    /// <summary>One row of the page as it was built: all the plan needs to know of it.</summary>
    public struct PlanRow
    {
        public PlanKind Kind;
        public string Id;         // the saved option's id; null for a button or a key
        public string LabelKey;

        public PlanRow(PlanKind kind, string id, string labelKey)
        {
            Kind = kind;
            Id = id;
            LabelKey = labelKey;
        }

        /// <summary>What the tree names it by: its option id, else its label key.</summary>
        public string Key => string.IsNullOrEmpty(Id) ? LabelKey : Id;
    }

    /// <summary>The rows of one section, as indices into the list that was placed, in the order of the page.</summary>
    public sealed class PlanSection
    {
        public readonly TreeSection Tree;     // null for "Other"
        public readonly List<int> Rows = new List<int>();

        internal PlanSection(TreeSection tree)
        {
            Tree = tree;
        }
    }

    /// <summary>Where every built row goes.</summary>
    public sealed class Plan
    {
        public readonly List<PlanSection> Sections = new List<PlanSection>();   // in the order of the tree; only those with a row
        public readonly PlanSection Other = new PlanSection(null);              // rows nothing claims: must be empty
        public readonly List<string> Missing = new List<string>();              // rows the tree names that were not built
        public int Placed;
        public int Groups;

        public PlanSection Find(string sectionId)
        {
            foreach (PlanSection section in Sections)
            {
                if (section.Tree.Id == sectionId)
                {
                    return section;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The arithmetic of the options page, with no Unity or game types so that CatalogueCheck runs it as it
    /// ships: where each built row goes by <see cref="OptionsTree"/>, what a row needs before it does anything
    /// and which need is not met, and how many switches of a list are on. Cost: <see cref="Place"/> is one
    /// dictionary and one pass over the rows and the tree (about 250 rows), once when the page is built;
    /// <see cref="Blocker"/> walks a row's few needs, when the page is drawn and on a click. Nothing per frame.
    /// </summary>
    public static class OptionsPlan
    {
        private const int DeepestNeed = 8;   // links followed from a row to what it needs: the tree's longest chain is three

        /// <summary>Sorts the built rows into the tree's sections, each in the tree's order; what nothing claims goes to <see cref="Plan.Other"/>.</summary>
        public static Plan Place(IList<PlanRow> rows)
        {
            var plan = new Plan();
            var first = new Dictionary<string, int>(rows.Count, StringComparer.Ordinal);
            var taken = new bool[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                string key = rows[i].Key;
                if (key != null && !first.ContainsKey(key))
                {
                    first[key] = i;
                }
            }
            foreach (TreeGroup group in OptionsTree.Groups)
            {
                bool any = false;
                foreach (TreeSection section in group.Sections)
                {
                    PlanSection placed = null;
                    foreach (TreeRow row in section.Rows)
                    {
                        if (!first.TryGetValue(row.Id, out int index) || taken[index] || OptionsTree.SectionOf(row.Id) != section
                            || row.IsButton != (rows[index].Id == null || rows[index].Id.Length == 0))
                        {
                            plan.Missing.Add(row.Id);
                            continue;
                        }
                        if (placed == null)
                        {
                            placed = new PlanSection(section);
                            plan.Sections.Add(placed);
                        }
                        taken[index] = true;
                        placed.Rows.Add(index);
                        plan.Placed++;
                    }
                    any |= placed != null;
                }
                if (any)
                {
                    plan.Groups++;
                }
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (!taken[i])
                {
                    plan.Other.Rows.Add(i);
                }
            }
            return plan;
        }

        // ---------------------------------------------------------------- what a row needs

        private static Dictionary<string, string[]> _needs;

        /// <summary>
        /// The options that must be on before this one does anything: what its section needs, what the row
        /// itself needs, and its section's main switch (unless it is that switch, or free of it).
        /// </summary>
        public static string[] NeedsOf(string optionId)
        {
            if (_needs == null)
            {
                var all = new Dictionary<string, string[]>(StringComparer.Ordinal);
                var list = new List<string>();
                foreach (TreeGroup group in OptionsTree.Groups)
                {
                    foreach (TreeSection section in group.Sections)
                    {
                        foreach (TreeRow row in section.Rows)
                        {
                            if (row.IsButton || all.ContainsKey(row.Id))
                            {
                                continue;
                            }
                            list.Clear();
                            if (section.Master != null && !row.Free && row.Id != section.Master)
                            {
                                list.Add(section.Master);
                            }
                            foreach (string need in row.Needs)
                            {
                                if (need != row.Id && !list.Contains(need))
                                {
                                    list.Add(need);
                                }
                            }
                            foreach (string need in section.Needs)
                            {
                                if (need != row.Id && !list.Contains(need))
                                {
                                    list.Add(need);
                                }
                            }
                            all[row.Id] = list.Count == 0 ? OptionsTree.None : list.ToArray();
                        }
                    }
                }
                _needs = all;
            }
            return optionId != null && _needs.TryGetValue(optionId, out string[] needs) ? needs : OptionsTree.None;
        }

        /// <summary>
        /// The option that keeps this one from doing anything: the first of its needs that is off, or that
        /// is itself kept from working (then the one that keeps that). Null when all it needs is on.
        /// </summary>
        public static string Blocker(string optionId, Func<string, bool> isOn)
        {
            return Blocker(optionId, isOn, 0);
        }

        private static string Blocker(string optionId, Func<string, bool> isOn, int depth)
        {
            if (depth >= DeepestNeed)
            {
                return null;   // a circle in the table: the check fails on it, the page goes on
            }
            string[] needs = NeedsOf(optionId);
            for (int i = 0; i < needs.Length; i++)
            {
                if (!isOn(needs[i]))
                {
                    return needs[i];
                }
            }
            for (int i = 0; i < needs.Length; i++)
            {
                string behind = Blocker(needs[i], isOn, depth + 1);
                if (behind != null)
                {
                    return behind;
                }
            }
            return null;
        }

        /// <summary>The options that need this one, directly: whose rows change when it is switched.</summary>
        public static List<string> NeededBy(string optionId)
        {
            NeedsOf(null);
            var list = new List<string>();
            foreach (KeyValuePair<string, string[]> pair in _needs)
            {
                if (Array.IndexOf(pair.Value, optionId) >= 0)
                {
                    list.Add(pair.Key);
                }
            }
            return list;
        }

        /// <summary>A need that leads back to itself, as "a > b > a", or null when there is none. For the check.</summary>
        public static string FindCycle()
        {
            NeedsOf(null);
            var state = new Dictionary<string, int>(StringComparer.Ordinal);   // 1: on the path, 2: done
            var path = new List<string>();
            foreach (string id in new List<string>(_needs.Keys))
            {
                string found = Walk(id, state, path);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static string Walk(string id, Dictionary<string, int> state, List<string> path)
        {
            state.TryGetValue(id, out int seen);
            if (seen == 2)
            {
                return null;
            }
            path.Add(id);
            if (seen == 1)
            {
                return string.Join(" > ", path.GetRange(path.IndexOf(id), path.Count - path.IndexOf(id)).ToArray());
            }
            state[id] = 1;
            foreach (string need in NeedsOf(id))
            {
                string found = Walk(need, state, path);
                if (found != null)
                {
                    return found;
                }
            }
            state[id] = 2;
            path.RemoveAt(path.Count - 1);
            return null;
        }

        /// <summary>How many of these switches are on. <paramref name="total"/> is how many there are.</summary>
        public static int CountOn(IList<bool> isSwitch, IList<bool> on, IList<int> rows, out int total)
        {
            int count = 0;
            total = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int row = rows[i];
                if (row >= 0 && row < isSwitch.Count && isSwitch[row])
                {
                    total++;
                    if (on[row])
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }

    /// <summary>
    /// What the search box finds. A word typed finds a text when a word of the text begins with it ("rent"
    /// finds "rent" and "rental", not "current"); every word typed must be found in the row's own text, its
    /// section's or its group's. Texts are lower case already. No allocation: IndexOf over a few short strings.
    /// </summary>
    public static class OptionsFind
    {
        public const int ShortestQuery = 2;   // one letter begins a word in nearly every row: switching all of them on would be a hitch for nothing

        /// <summary>Takes what was typed apart into lower-case words. Returns the query as it is compared, or empty when it is too short to search for.</summary>
        public static string Split(string typed, List<string> into)
        {
            into.Clear();
            string query = typed == null ? string.Empty : typed.Trim();
            if (query.Length < ShortestQuery)
            {
                return string.Empty;
            }
            query = query.ToLowerInvariant();
            int start = -1;
            for (int i = 0; i <= query.Length; i++)
            {
                bool gap = i == query.Length || char.IsWhiteSpace(query[i]);
                if (gap && start >= 0)
                {
                    into.Add(start == 0 && i == query.Length ? query : query.Substring(start, i - start));
                    start = -1;
                }
                else if (!gap && start < 0)
                {
                    start = i;
                }
            }
            return query;
        }

        /// <summary>True when a word of <paramref name="text"/> begins with <paramref name="word"/>.</summary>
        public static bool Has(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            {
                return false;
            }
            int at = 0;
            while (at < text.Length && (at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
            {
                if (at == 0 || !char.IsLetterOrDigit(text[at - 1]))
                {
                    return true;
                }
                at++;
            }
            return false;
        }

        /// <summary>Every word typed is in the row's text, its section's or its group's.</summary>
        public static bool Matches(List<string> words, string row, string section, string group)
        {
            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                if (!Has(row, word) && !Has(section, word) && !Has(group, word))
                {
                    return false;
                }
            }
            return words.Count > 0;
        }
    }

    /// <summary>
    /// The rules every label of the options page keeps, so that the check can hold each text of en.json to
    /// them: how long a label may be, and the mod's own words that never stand on the page.
    /// </summary>
    public static class OptionsWording
    {
        public const int LabelWords = 9;      // a switch, a slider, a choice, a button of the player's: "at most about 8 words"
        public const int LabelChars = 60;     // and it stays on one line beside its control
        public const int TitleWords = 6;      // a group or a section
        public const int TitleChars = 40;
        public const int TestWords = 13;      // a test button says what it stages and where
        public const int TestChars = 80;
        public const int TipWords = 32;       // the one sentence under the pointer
        public const int TipChars = 190;

        /// <summary>Words of the mod's own making, and the tails that explained them. Whole words, lower case.</summary>
        public static readonly string[] Banned =
        {
            "director", "finish", "bridge", "ambient", "footfall", "stamp", "tier", "pool", "prewarm", "stand-in", "underlay", "rung",
            "(visible", "(off", "(needs", "(the presets", "test:", "gta", "ragdoll", "ragdolls", "prefab", "hud", "toggle", "feature"
        };

        public static int WordCount(string text)
        {
            int count = 0;
            bool inWord = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                bool letter = !char.IsWhiteSpace(text[i]);
                if (letter && !inWord)
                {
                    count++;
                }
                inWord = letter;
            }
            return count;
        }

        /// <summary>The first banned word in the text, or null.</summary>
        public static string FirstBanned(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string lower = text.ToLowerInvariant();
            foreach (string word in Banned)
            {
                int at = 0;
                while (at < lower.Length && (at = lower.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
                {
                    int end = at + word.Length;
                    bool startsWord = at == 0 || !char.IsLetterOrDigit(lower[at - 1]) || !char.IsLetterOrDigit(word[0]);
                    bool endsWord = end >= lower.Length || !char.IsLetterOrDigit(lower[end]) || !char.IsLetterOrDigit(word[word.Length - 1]);
                    if (startsWord && endsWord)
                    {
                        return word;
                    }
                    at++;
                }
            }
            return null;
        }
    }
}
