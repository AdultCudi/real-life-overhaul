namespace RealLifeOverhaul.Ui
{
    /// <summary>Why a press on a stack's tile took nothing, as the game's own click decides it (CargoItemUi.ClickItem and PickCargoInstance).</summary>
    public enum CargoTakeRefusal
    {
        None,                 // the press took the stack, or a part of it
        NotThere,             // the game's tile or its button is gone (its panel was rebuilt under the press)
        Held,                 // the game holds its tile's button
        NotYours,             // another player's vehicle in a Going Public session: never theirs to unload
        SameHolder,           // the stack is in what the player holds or pushes already: the game moves nothing onto itself
        CartNeedsFreeHands,   // a hand truck or a flatbed cart comes out of the cargo only with empty hands and no vehicle in use
        NoRoom,               // what the player holds or pushes, or the storage that is open, has no room for it
        DriverGarage,         // the private driver's garage holds the vehicle: the game refuses and says so itself
        Unknown               // the game's click changed nothing for a reason the mod does not know
    }

    /// <summary>
    /// The rules of a press on a stack's tile, with no game types, checked offline (EconomyCheck, the vehicle panel's checks):
    /// whether the game's click took anything, by what the holder held before and after, and the reason when it did not,
    /// for the one line the log gets then.
    /// </summary>
    public static class CargoTakeRules
    {
        /// <summary>True when the game's click moved something: a stack or pieces left the holder, the hands changed, or the player got on or off a vehicle (a cart taken out).</summary>
        public static bool Took(int stacksBefore, int piecesBefore, int stacksAfter, int piecesAfter, bool handsChanged, bool vehicleChanged) =>
            stacksAfter != stacksBefore || piecesAfter != piecesBefore || handsChanged || vehicleChanged;

        /// <summary>
        /// Why a press may take nothing, in the order the game itself decides. <paramref name="fromUsedVehicle"/>: the tile is
        /// on the panel of the vehicle the player uses (the game's PickCargoInstance), else in manage cargo or on a thing in
        /// the hands (its ClickItem). <paramref name="ownHolds"/>: the player holds a thing or uses a vehicle.
        /// </summary>
        public static CargoTakeRefusal Why(bool there, bool pressable, bool mine, bool fromUsedVehicle, bool sameHolder, bool cart, bool ownHolds, bool storageOpen,
            bool driverGarage)
        {
            if (!there)
            {
                return CargoTakeRefusal.NotThere;
            }
            if (!pressable)
            {
                return CargoTakeRefusal.Held;
            }
            if (!mine)
            {
                return CargoTakeRefusal.NotYours;
            }
            if (fromUsedVehicle)
            {
                return storageOpen ? CargoTakeRefusal.NoRoom : driverGarage ? CargoTakeRefusal.DriverGarage : CargoTakeRefusal.Unknown;
            }
            if (sameHolder)
            {
                return CargoTakeRefusal.SameHolder;
            }
            if (ownHolds)
            {
                return cart ? CargoTakeRefusal.CartNeedsFreeHands : CargoTakeRefusal.NoRoom;
            }
            return CargoTakeRefusal.Unknown;
        }

        /// <summary>True for a reason known before the game's button is pressed: the press is then refused without pressing it.</summary>
        public static bool Before(CargoTakeRefusal why) => why == CargoTakeRefusal.NotThere || why == CargoTakeRefusal.Held || why == CargoTakeRefusal.NotYours;

        /// <summary>The reason in the log's plain English.</summary>
        public static string Words(CargoTakeRefusal why)
        {
            switch (why)
            {
                case CargoTakeRefusal.NotThere:
                    return "the game's tile was gone when it was pressed (its panel was being rebuilt)";
                case CargoTakeRefusal.Held:
                    return "the game holds its tile's button now";
                case CargoTakeRefusal.NotYours:
                    return "the vehicle is another player's, and its cargo is theirs";
                case CargoTakeRefusal.SameHolder:
                    return "the stack is in what the player holds or pushes already, and the game moves nothing onto itself";
                case CargoTakeRefusal.CartNeedsFreeHands:
                    return "a hand truck or a cart comes out of the cargo only with empty hands and no vehicle in use";
                case CargoTakeRefusal.NoRoom:
                    return "there is no room for it where it would go (what the player holds or pushes, or the storage that is open)";
                case CargoTakeRefusal.DriverGarage:
                    return "the private driver's garage holds the vehicle, as the game's own notice says";
                default:
                    return "the game's own click changed nothing, for a reason the mod does not know";
            }
        }
    }
}
