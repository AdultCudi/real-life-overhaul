using UI.Overlays;

namespace RealLifeOverhaul
{
    /// <summary>The interaction key, and key names in texts: they carry {key} or {key1} to {key3}, filled in by <see cref="Fill"/>.</summary>
    public static class JobKeys
    {
        public const int Choices = 3;

        private static readonly string[] ChoiceLabels = { "1", "2", "3" };

        /// <summary>What the interaction key is called in a prompt.</summary>
        public static string Label => Keys.Label(KeyAction.Interact);

        /// <summary>The interaction key went down this frame: F always, E only while the game offers no interaction of its own on it.</summary>
        public static bool InteractDown()
        {
            return Keys.Down(KeyAction.Interact) || (Keys.Down(KeyAction.InteractAlt) && !OverlayUI.IsVisible);
        }

        /// <summary>Puts the key names into a text; builds a string only when there is a placeholder, so never per frame.</summary>
        public static string Fill(string text)
        {
            if (text == null || text.IndexOf("{key", System.StringComparison.Ordinal) < 0)
            {
                return text;
            }
            return text.Replace("{key}", Label).Replace("{key1}", ChoiceLabels[0]).Replace("{key2}", ChoiceLabels[1]).Replace("{key3}", ChoiceLabels[2]);
        }
    }
}
