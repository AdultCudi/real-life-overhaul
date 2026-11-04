namespace RealLifeOverhaul.Ui
{
    /// <summary>What the cargo panel's Move to a warehouse is for a stack, as rules with no game types, checked offline (EconomyCheck FixW2Checks).</summary>
    public enum CargoMoveState
    {
        Ready,         // the button moves the stack
        NoWarehouse,   // the vehicle does not stand at a warehouse of the player's: no button
        Unpaid,        // goods still owed for are not the player's to store: no button
        Sealed,        // a sealed container is moved by the game's own hands only: no button
        Full           // the warehouse has no shelf for it: the button is drawn disabled, and says so
    }

    /// <summary>The rules of Move to a warehouse: when it is offered, when it is not, and what the player is told after.</summary>
    public static class CargoMoveRules
    {
        /// <summary>The state of the button for the chosen stack.</summary>
        public static CargoMoveState Can(bool atOwnWarehouse, bool paid, bool sealedStack, bool room)
        {
            return !atOwnWarehouse ? CargoMoveState.NoWarehouse : !paid ? CargoMoveState.Unpaid : sealedStack ? CargoMoveState.Sealed : !room ? CargoMoveState.Full : CargoMoveState.Ready;
        }

        /// <summary>True when the button is drawn at all (a disabled one is drawn, to say why).</summary>
        public static bool Shown(CargoMoveState state) => state == CargoMoveState.Ready || state == CargoMoveState.Full;

        /// <summary>The text key of the button: its words when it moves, the reason when it cannot.</summary>
        public static string LabelKey(CargoMoveState state) => state == CargoMoveState.Full ? "reallife_ui_buy_move_full" : "reallife_ui_buy_move_to";

        /// <summary>The text key of the notice after a move: all of it, part of it (the shelves filled), or none.</summary>
        public static string NoticeKey(int moved, int packs) => moved <= 0 ? "reallife_ui_buy_move_none" : moved < packs ? "reallife_ui_buy_moved_part" : "reallife_ui_buy_moved";

        /// <summary>True when the free room of a warehouse can take a pack: a free pallet slot, or a part box of the same goods to fill.</summary>
        public static bool Room(int freeSlots, int partBoxes) => freeSlots > 0 || partBoxes > 0;
    }
}
