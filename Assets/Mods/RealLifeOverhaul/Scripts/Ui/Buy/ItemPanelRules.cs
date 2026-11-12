namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The item panel's own rules, game-free so the offline checks hold them. The game's panel gives every stack in what
    /// the player carries a slot with four buttons of its own: the slot itself (take the stack out), its action (Eat, Drink,
    /// Equip, Read, Place), its sell and its discard. The kit draws a slot as a tile, so each of the other three needs a
    /// place of its own on the kit's panel, or the way to the game's own view. Cost: a string joined when a stack's words change.
    /// </summary>
    public static class ItemPanelRules
    {
        /// <summary>
        /// A stack's action as a button of the panel: the game's word for it and the stack's name, as the game's fridge
        /// words its own buttons ("Eat Cupcake"); the word alone for a stack with no name; null when the slot offers none.
        /// </summary>
        public static string SlotAction(string does, string what)
        {
            if (string.IsNullOrEmpty(does))
            {
                return null;
            }
            return string.IsNullOrEmpty(what) ? does : does + " " + what;
        }

        /// <summary>
        /// Whether the panel offers the way to the game's own view: another mod's button stands in the game's panel, or a
        /// slot shows a button of its own the kit has no place for (a stack's sell or discard).
        /// </summary>
        public static bool More(bool foreign, bool slotButtons) => foreign || slotButtons;
    }
}
