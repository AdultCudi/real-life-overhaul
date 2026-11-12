namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The checkout's own rules, game-free so the offline checks hold them. Where the game's rows are is one of them, in
    /// words, because it was got wrong once: the game copies its row templates beside themselves (the template's parent,
    /// inside the list's scroll view), and the object it switches on and off for the whole list (PurchaseUI.itemsContainer)
    /// is the scroll view, two levels up. Cost: none.
    /// </summary>
    public static class CheckoutRules
    {
        /// <summary>The field of the game's panel whose parent holds the goods rows, and the one for the tax rows.</summary>
        public const string RowsBeside = "itemTemplate", TaxesBeside = "taxItemTemplate";

        /// <summary>
        /// True when Place order must be refused: the register lets the player choose (a counter) and nothing is taken. The
        /// game leaves its button ready then, takes an order of nothing and shows a queue nobody serves.
        /// </summary>
        public static bool NothingChosen(bool chooses, int taken) => chooses && taken <= 0;
    }
}
