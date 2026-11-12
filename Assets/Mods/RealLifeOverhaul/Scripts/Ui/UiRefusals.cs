using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Why a press on a kit screen came to nothing. A press that answers false gets the error sound and nothing else, so
    /// a refusal nobody wrote down cannot be told from a fault afterwards (Start business, the order's add button). Whoever
    /// refuses says why while the press is under way (<see cref="Because"/>: the game's widgets do, a screen may), and the
    /// host that sent the press writes one line of Player.log the first time a screen's control is refused for a reason
    /// (<see cref="Line"/>), whatever the diagnostics' switch says. Game-free, so the offline checks read it. Cost: nothing
    /// until a press is refused; then a set looked up and, the first time, one line made.
    /// </summary>
    public static class UiRefusals
    {
        public const int Most = 48;                         // lines a session: a screen that refuses everything cannot fill the log
        public const string Unsaid = "its screen answered false and gave no reason";
        public const string Disabled = "it is drawn disabled";
        private static readonly HashSet<string> Said = new HashSet<string>();
        private static string _why;

        /// <summary>How many lines were written this session, for the checks.</summary>
        public static int Count => Said.Count;

        /// <summary>A press starts: no reason is held over from the one before.</summary>
        public static void Begin()
        {
            _why = null;
        }

        /// <summary>Why the press under way is refused. The first reason given stands: it is the one nearest the cause.</summary>
        public static void Because(string why)
        {
            if (_why == null && !string.IsNullOrEmpty(why))
            {
                _why = why;
            }
        }

        /// <summary>The reason given since <see cref="Begin"/>, or null.</summary>
        public static string Why => _why;

        /// <summary>
        /// The line for a refused press, or null when this screen's control was already written down for this reason (or the
        /// session's lines are spent). A control is its command: the rows of one list are one control.
        /// </summary>
        public static string Line(string screen, int command, int arg, string label, string why)
        {
            why = string.IsNullOrEmpty(why) ? Unsaid : why;
            if (Said.Count >= Most || !Said.Add(screen + "|" + command + "|" + why))
            {
                return null;
            }
            return "UI: a press was refused on " + (string.IsNullOrEmpty(screen) ? "a screen" : screen) + ": control " + command + "/" + arg
                + (string.IsNullOrEmpty(label) ? string.Empty : " '" + label + "'") + ": " + why + ". (Said once for this control and reason.)";
        }

        /// <summary>The words a target shows (its first text), or its icon's name, for the line; null for a bare target.</summary>
        public static string Label(UiScene scene, int node)
        {
            if (scene == null || node < 0 || node >= scene.NodeCount)
            {
                return null;
            }
            string icon = null;
            for (int i = 0; i < scene.ItemCount; i++)
            {
                if (scene.Items[i].Node != node)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(scene.Items[i].Text))
                {
                    return scene.Items[i].Text;
                }
                if (icon == null && scene.Items[i].Icon != UiIcon.None)
                {
                    icon = scene.Items[i].Icon.ToString();
                }
            }
            return icon;
        }

        /// <summary>A new session's lines (the checks, a new game).</summary>
        public static void Reset()
        {
            Said.Clear();
            _why = null;
        }
    }
}
