using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The keys jobs listen to, and what they are called on screen. Every key test and every key
    /// name the jobs show goes through here and nowhere else, so that when the mod's rebindable
    /// keys arrive (Core/Keys: Keys.Down(action), Keys.Label(action)) the change is the two
    /// marked lines below. Texts in en.json never name a key: they carry {key} (the interaction
    /// key) or {key1}..{key3} (the choice keys), filled in by <see cref="Fill"/>.
    /// </summary>
    public static class JobKeys
    {
        public const int Choices = 3;

        private static readonly string[] ChoiceLabels = { "1", "2", "3" };

        /// <summary>What the interaction key is called in a prompt.</summary>
        public static string Label => Keys.Label(KeyAction.Interact);

        /// <summary>What the key for a choice (0, 1 or 2) is called.</summary>
        public static string ChoiceLabel(int choice)
        {
            return choice >= 0 && choice < Choices ? ChoiceLabels[choice] : string.Empty;
        }

        /// <summary>
        /// The interaction key went down this frame: F always, E only while the game is not
        /// offering an interaction of its own with it.
        /// </summary>
        public static bool InteractDown()
        {
            return Keys.Down(KeyAction.Interact) || (Keys.Down(KeyAction.InteractAlt) && !OverlayUI.IsVisible);
        }

        /// <summary>Which of the choice keys went down this frame (0, 1 or 2), or -1. Top row or number pad.</summary>
        public static int ChoiceDown()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                return 0;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                return 1;
            }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                return 2;
            }
            return -1;
        }

        /// <summary>
        /// Puts the key names into a text. Builds a string only when the text has a placeholder:
        /// for when something changes, never per frame.
        /// </summary>
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
