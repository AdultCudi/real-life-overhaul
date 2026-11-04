using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The picture the kit makes for another mod's phone app that has none of its own: the first letter of its name on its
    /// tile, in one of the chart's hues chosen from the name, so the same app has the same mark on every screen and in every
    /// session. A mark rides in an app's picture handle as a negative number (a handle is 1 or more, 0 is none), so the phone,
    /// the app row and All apps carry it without a new field. Game-free. Cost: a few comparisons when an app is read.
    /// </summary>
    public static class UiForeignMark
    {
        private const int Hues = 8;   // the chart's hues (UiTokens.Chart)

        /// <summary>The mark for an app: its title's first letter or digit (else its id's), its hue from its id. Always negative.</summary>
        public static int Of(string title, string id)
        {
            char letter = First(title);
            if (letter == '\0')
            {
                letter = First(AfterPrefix(id));
            }
            if (letter == '\0')
            {
                letter = '?';
            }
            return -(1 + letter * Hues + Hue(id ?? title));
        }

        /// <summary>True for a handle that is a mark.</summary>
        public static bool Is(int picture) => picture < 0;

        /// <summary>The mark's letter, as a one-letter text.</summary>
        public static string Letter(int picture)
        {
            return picture < 0 ? ((char)((-picture - 1) / Hues)).ToString() : null;
        }

        /// <summary>The mark's colour: one of the chart's eight, by its hue.</summary>
        public static UiRole Tint(int picture)
        {
            return picture < 0 ? UiTokens.Chart((-picture - 1) % Hues) : UiRole.None;
        }

        /// <summary>A stable hue (0 to 7) for a name: the same on every machine and in every session (not string.GetHashCode).</summary>
        public static int Hue(string name)
        {
            uint h = 2166136261u;
            for (int i = 0; name != null && i < name.Length; i++)
            {
                h = (h ^ name[i]) * 16777619u;
            }
            return (int)(h % Hues);
        }

        // The first letter or digit of a name, in capitals; none for a name without one.
        private static char First(string name)
        {
            for (int i = 0; name != null && i < name.Length; i++)
            {
                if (char.IsLetterOrDigit(name[i]))
                {
                    return char.ToUpperInvariant(name[i]);
                }
            }
            return '\0';
        }

        // "BAMP_Chat" is "Chat" for its letter: the author's prefix is not the app's name.
        private static string AfterPrefix(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }
            int cut = id.LastIndexOf('_');
            return cut >= 0 && cut < id.Length - 1 ? id.Substring(cut + 1) : id;
        }
    }
}
