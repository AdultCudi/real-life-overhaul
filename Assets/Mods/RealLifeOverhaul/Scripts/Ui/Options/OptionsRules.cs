using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One rebinding row of the game's Controls view, as the kit reads it: the action, the words it shows and the key now bound.</summary>
    public struct OptionsKey
    {
        public string Action;      // the input action's own name ("Move"), never shown
        public string Label;       // the game's words for it
        public string Binding;     // the key now bound, as the game writes it
        public string Path;        // the bound control's path ("<Keyboard>/escape"), null for a composite's head
        public bool Gamepad;       // its binding belongs to the gamepad's scheme
        public bool Composite;     // one row rebinds several keys in turn (Move)
        public bool Listening;     // waiting for the new key
    }

    /// <summary>
    /// What the game's Options in the city asks of the kit, game-free: the five categories and the game's names for them,
    /// the groups the Controls rows go into and the one sentence under each action, which rows share a key, the search's
    /// matching, a slider's steps and the one line at the head. The groups are a fixed table by the input action's name;
    /// an action of another mod's, or one the table does not know, goes in Other. Checked offline (EconomyCheck
    /// --options-ui). Cost: a few string comparisons when a page is built; nothing a frame.
    /// </summary>
    public static class OptionsRules
    {
        /// <summary>The sidebar's categories, in the game's order.</summary>
        public const int Graphics = 0, Audio = 1, Controls = 2, Game = 3, Mods = 4, Categories = 5;

        /// <summary>The words of each category (the game's own where it has them).</summary>
        public static readonly string[] CategoryKeys =
        {
            "menu_options_graphics", "menu_options_audio", "menu_options_controls", "reallife_ui_opt_cat_game", "menu_options_mods"
        };

        /// <summary>The name of each category's button under the game's Menu (what the game calls the Game category: Others).</summary>
        public static readonly string[] GameTabs = { "Graphics", "Audio", "Controls", "Others", "Mods" };

        public static readonly UiIcon[] CategoryIcons = { UiIcon.Monitor, UiIcon.SpeakerHigh, UiIcon.Keyboard, UiIcon.Sliders, UiIcon.Cube };

        /// <summary>The groups of the Controls rows.</summary>
        public const int Movement = 0, Time = 1, Interaction = 2, Apps = 3, Vehicle = 4, Designer = 5, Other = 6, GroupCount = 7;

        public static readonly string[] GroupKeys =
        {
            "reallife_ui_opt_group_movement", "reallife_ui_opt_group_time", "reallife_ui_opt_group_interaction", "reallife_ui_opt_group_apps",
            "reallife_ui_opt_group_vehicle", "reallife_ui_opt_group_designer", "reallife_ui_opt_group_other"
        };

        public static readonly UiIcon[] GroupIcons =
        {
            UiIcon.Walk, UiIcon.Clock, UiIcon.Pin, UiIcon.Layout, UiIcon.Car, UiIcon.PaintBrush, UiIcon.Gear
        };

        private static readonly string[][] Table =
        {
            new[] { "move", "rotateleft", "rotateright", "rotate", "zoom", "togglerunning", "autorun", "reversecarcam", "fastmappan", "cursormovement", "cursorscroll" },
            new[] { "pause", "sleep" },
            new[]
            {
                "interact", "secondaryinteract", "specialinteract", "cancel", "confirm", "nextoption", "previousoption", "click", "rightclick", "sell",
                "performactionwithoutconfirm", "sliderleft", "sliderright", "selectmultipleelements", "snapfreeplacement", "invertitemrotation", "rotateitem"
            },
            new[] { "menu", "opennotifications", "openhelp", "openbugreport", "quicksave", "openmap", "openbizman", "skipsong", "showallitemoverlays" },
            new[] { "steering", "throttle", "brakes", "handbrake", "vehicleleftblinker", "vehiclerightblinker", "vehiclehorn" },
            new[]
            {
                "undo", "redo", "togglewalls", "showupperfloor", "showlowerfloor", "specialbehavior", "apply", "exit", "openblueprintpanel",
                "interiordesignerundo", "interiordesignerredo", "interiordesignerpicker"
            }
        };

        /// <summary>
        /// The actions that have a sentence of their own under their name, each a key reallife_ui_opt_act_ and the action's
        /// name in lower case. The designer's tools (an action named Open and the tool's name) share one.
        /// </summary>
        public static readonly string[] Described =
        {
            "move", "rotateleft", "rotateright", "rotate", "zoom", "togglerunning", "autorun", "reversecarcam", "fastmappan", "pause", "sleep",
            "interact", "secondaryinteract", "specialinteract", "cancel", "confirm", "nextoption", "previousoption", "click", "rightclick", "sell",
            "performactionwithoutconfirm", "sliderleft", "sliderright", "selectmultipleelements", "snapfreeplacement", "invertitemrotation", "rotateitem",
            "menu", "opennotifications", "openhelp", "openbugreport", "quicksave", "openmap", "openbizman", "skipsong", "showallitemoverlays",
            "steering", "throttle", "brakes", "handbrake", "vehicleleftblinker", "vehiclerightblinker", "vehiclehorn",
            "undo", "redo", "togglewalls", "showupperfloor", "showlowerfloor", "specialbehavior", "apply", "exit", "openblueprintpanel",
            "interiordesignerundo", "interiordesignerredo", "interiordesignerpicker", "tool"
        };

        private static readonly Dictionary<string, int> GroupByAction = Index();

        private static Dictionary<string, int> Index()
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int g = 0; g < Table.Length; g++)
            {
                for (int i = 0; i < Table[g].Length; i++)
                {
                    map[Table[g][i]] = g;
                }
            }
            return map;
        }

        /// <summary>True for one of the designer's tool actions (OpenHandTool, OpenWallTool): they share one sentence.</summary>
        public static bool IsTool(string lower) => lower.Length > 8 && lower.StartsWith("open", StringComparison.Ordinal) && lower.EndsWith("tool", StringComparison.Ordinal);

        /// <summary>The group an input action's controls go in; Other for one the table does not know.</summary>
        public static int GroupOf(string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                return Other;
            }
            string lower = action.ToLowerInvariant();
            if (GroupByAction.TryGetValue(lower, out int g))
            {
                return g;
            }
            return IsTool(lower) ? Designer : Other;
        }

        /// <summary>The key of the sentence under an action's name, or null when it has none (another mod's action).</summary>
        public static string DescriptionKey(string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                return null;
            }
            string lower = action.ToLowerInvariant();
            if (IsTool(lower))
            {
                return "reallife_ui_opt_act_tool";
            }
            return Array.IndexOf(Described, lower) >= 0 ? "reallife_ui_opt_act_" + lower : null;
        }

        /// <summary>True when a binding's control schemes (the game's groups, separated by semicolons) name the gamepad's.</summary>
        public static bool GamepadScheme(string groups)
        {
            return !string.IsNullOrEmpty(groups) && (groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0 || groups.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// The schemes the rebinding rows are split by: the keyboard's and the gamepad's tabs show only when the game's own
        /// rows carry bindings of both (the game's action maps decide); with one scheme there is nothing to choose.
        /// </summary>
        public static bool TwoSchemes(OptionsKey[] keys, int count)
        {
            bool keyboard = false, pad = false;
            for (int i = 0; i < count; i++)
            {
                keyboard |= !keys[i].Gamepad;
                pad |= keys[i].Gamepad;
            }
            return keyboard && pad;
        }

        /// <summary>
        /// For each row the row of another action that is bound to the same control in the same scheme, or -1. A composite's
        /// head has no path of its own and is left out; rows of one action never clash with themselves.
        /// </summary>
        public static void Shared(OptionsKey[] keys, int count, int[] into)
        {
            for (int i = 0; i < count; i++)
            {
                into[i] = -1;
                if (string.IsNullOrEmpty(keys[i].Path))
                {
                    continue;
                }
                for (int k = 0; k < count && into[i] < 0; k++)
                {
                    if (k != i && keys[k].Gamepad == keys[i].Gamepad && !string.Equals(keys[k].Action, keys[i].Action, StringComparison.Ordinal)
                        && string.Equals(keys[k].Path, keys[i].Path, StringComparison.OrdinalIgnoreCase))
                    {
                        into[i] = k;
                    }
                }
            }
        }

        /// <summary>
        /// The Controls rows of one scheme in the order the page shows them: by group, and inside a group in the game's own
        /// order. The result holds indexes into the keys; the count is returned.
        /// </summary>
        public static int Arrange(OptionsKey[] keys, int count, bool gamepad, bool both, int[] into)
        {
            int n = 0;
            for (int g = 0; g < GroupCount; g++)
            {
                for (int i = 0; i < count; i++)
                {
                    if ((!both || keys[i].Gamepad == gamepad) && GroupOf(keys[i].Action) == g)
                    {
                        into[n++] = i;
                    }
                }
            }
            return n;
        }

        /// <summary>True when every word of the query is in the name or the sentence, whatever the case. An empty query matches nothing.</summary>
        public static bool Matches(string query, string name, string tip)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }
            int from = 0;
            while (from < query.Length)
            {
                while (from < query.Length && query[from] == ' ')
                {
                    from++;
                }
                int to = from;
                while (to < query.Length && query[to] != ' ')
                {
                    to++;
                }
                if (to > from)
                {
                    string word = query.Substring(from, to - from);
                    bool found = (name != null && name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                                 || (tip != null && tip.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!found)
                    {
                        return false;
                    }
                }
                from = to;
            }
            return true;
        }

        /// <summary>Where the query's first word stands in a name, for the marked letters; false when it is not there.</summary>
        public static bool MarkAt(string query, string name, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(name))
            {
                return false;
            }
            string word = query.Trim();
            int space = word.IndexOf(' ');
            if (space > 0)
            {
                word = word.Substring(0, space);
            }
            int at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }
            start = at;
            length = word.Length;
            return true;
        }

        /// <summary>A slider's step with the keys: a whole-number slider moves by one, any other by a twentieth of its length.</summary>
        public static float Step(float min, float max, bool whole)
        {
            return whole ? 1f : (max - min) / 20f;
        }

        /// <summary>A slider's place on its track, 0 to 1.</summary>
        public static float ShareOf(float value, float min, float max)
        {
            if (max <= min)
            {
                return 0f;
            }
            float share = (value - min) / (max - min);
            return share < 0f ? 0f : share > 1f ? 1f : share;
        }

        /// <summary>The value a share of the track stands for, rounded to a whole number for a slider that holds whole numbers.</summary>
        public static float ValueAt(float share, float min, float max, bool whole)
        {
            share = share < 0f ? 0f : share > 1f ? 1f : share;
            float value = min + (max - min) * share;
            return whole ? (float)Math.Round(value, MidpointRounding.AwayFromZero) : value;
        }

        /// <summary>The value after a step, kept inside the slider's ends.</summary>
        public static float Nudged(float value, int direction, float min, float max, bool whole)
        {
            float next = value + direction * Step(min, max, whole);
            if (whole)
            {
                next = (float)Math.Round(next, MidpointRounding.AwayFromZero);
            }
            return next < min ? min : next > max ? max : next;
        }

        /// <summary>The line at the head of the window: "Game build 3682, 4 mods"; the mods alone while the build is not known.</summary>
        public static string BuildLine(int build, string buildFormat, string mods)
        {
            if (build <= 0 || string.IsNullOrEmpty(buildFormat))
            {
                return mods;
            }
            return buildFormat.Replace("{build}", build.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{mods}", mods ?? string.Empty);
        }

        /// <summary>How many values of the custom difficulty differ from the ones the game applies now: what the panel's count says.</summary>
        public static int Changed(float[] now, float[] applied, bool[] locked, int count)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!locked[i] && Math.Abs(now[i] - applied[i]) > 0.0001f)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
