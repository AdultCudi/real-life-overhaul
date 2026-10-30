namespace RealLifeOverhaul.Meals
{
    /// <summary>One seat near the player as the director measured it when the player ate: where it is, how far on foot, and what it is.</summary>
    public struct SeatOption
    {
        public float Flat;      // m: straight across the floor from the player to the seat
        public float Up;        // m: how far the seat stands above (or below) the player's feet
        public float Path;      // m: the walk there over the game's walking mesh; negative when there is no way there
        public bool Table;      // a place at a table: the game's own table seat, or a chair with a table top in front of it
        public bool Free;       // nobody sits there and the game lets the player sit
    }

    /// <summary>Where the food is put in front of a seated player: the order <see cref="MealsRules.Serve"/> tries them in.</summary>
    public enum Plate
    {
        Tray,    // the table's own tray, as the place gives its customers
        Table,   // a plate on the table top in front of the seat
        Lap,     // a plate in the lap: a bench, a sofa or a chair with no table
        Hand     // nothing to put down: eaten from the hand
    }

    /// <summary>What Eat at a seat would eat (<see cref="MealsRules.Choose"/>).</summary>
    public enum TakeoutFood
    {
        None,
        Bag,      // a meal of the game's in the bag in the player's hand
        Packed    // a home portion packed to take
    }

    /// <summary>
    /// The rules of eating seated, the home-cooked plate and dancing to the music, game-free: which seat a meal is taken to,
    /// whether the walk keeps to the same room, how long the meal is watched, and how often dancing lifts the mood. The
    /// food's effect is the game's own (or home cooking's) and is never touched here: these rules decide only what is seen.
    /// </summary>
    public static class MealsRules
    {
        public const float Reach = 9f;              // m: the farthest seat a meal is carried to, straight across the floor
        public const float MostUp = 1.0f;           // m: a seat higher or lower than this is on another floor or a stage
        public const float WalkSlack = 1.5f;        // the walk may be this many times the straight distance...
        public const float WalkExtra = 1.5f;        // ...plus this many metres (round a table), and still be the same room
        public const int MostLooked = 8;            // seats whose walk is measured, the nearest first: one path each
        public const float SeatedAtLeast = 8f;      // the hunger bar's rise (of 100) that is a meal worth sitting down to
        public const float LeastSeconds = 8f;       // s: the shortest meal watched at the table
        public const float MostSeconds = 15f;       // s: the longest
        public const float WindDownMost = 1.5f;     // s: the most the plate waits for the eating animation to end after the meal's time, then it goes anyway
        public const float SecondsPerFill = 0.125f; // s of eating per point the meal fills, on top of the shortest
        public const float WaitMost = 5f;           // s: the game's own bite and its panel are waited for at most this long
        public const float WalkMost = 20f;          // s: a walk to the seat that has not arrived by then is given up
        public const float TableLow = 0.55f;        // m above the seat's floor: the lowest table top a plate is put on
        public const float TableHigh = 1.05f;       // m: the highest
        public const float PlateAhead = 0.40f;      // m in front of the seat's middle: where the plate goes on a table top
        public const float LapAhead = 0.30f;        // m in front of the seat's place: a plate on the thighs of somebody seated
        public const float LapUp = 0.62f;           // m above the seat's floor: the top of the thighs on a seat of ordinary height

        public const int DanceAtLeast = 15;         // game minutes danced to the music before the mood lifts
        public const int DanceGapHours = 8;         // game hours before dancing can lift the mood again (as long as the lift lasts)
        public const int DanceKinds = 9;            // the game's own dances (Dancing.DanceType)

        /// <summary>True when a rise of the hunger bar is a meal worth sitting down to (a sip of a drink is not).</summary>
        public static bool Wanted(float rise)
        {
            return rise >= SeatedAtLeast;
        }

        /// <summary>True when the walk to a seat keeps to the room the player is in: no longer than the straight line allows.</summary>
        public static bool SameSpace(float flat, float path)
        {
            return path >= 0f && path <= flat * WalkSlack + WalkExtra;
        }

        /// <summary>True when a seat this far above (or below) the player's feet is on the floor they stand on: one upstairs, downstairs or on a stage is never taken.</summary>
        public static bool SameFloor(float up)
        {
            return up <= MostUp && up >= -MostUp;
        }

        /// <summary>True when the meal may be taken to this seat at all.</summary>
        public static bool Usable(SeatOption seat)
        {
            return seat.Free && seat.Flat <= Reach && SameFloor(seat.Up) && SameSpace(seat.Flat, seat.Path);
        }

        /// <summary>
        /// The place a seat found while looking round the building takes in a shortlist of at most <paramref name="most"/>, so
        /// that the nearest seats on the player's own floor are kept whatever order the building lists its furniture in. A
        /// house with an upper floor lists the seats above among those below, and those above can never be used. The answer
        /// is the next place while there is room, else the place of the farthest seat kept when this one is nearer. -1 leaves
        /// it out: on another floor, beyond <see cref="Reach"/>, or no nearer than any seat kept. The first
        /// <paramref name="count"/> places of <paramref name="kept"/> hold the seats kept so far.
        /// </summary>
        public static int Slot(SeatOption[] kept, int count, int most, float flat, float up)
        {
            if (kept == null || !SameFloor(up) || flat > Reach)
            {
                return -1;
            }
            int room = most < kept.Length ? most : kept.Length;
            if (count < room)
            {
                return count;
            }
            int far = -1;
            float farthest = flat;
            for (int i = 0; i < room; i++)
            {
                if (kept[i].Flat > farthest)
                {
                    far = i;
                    farthest = kept[i].Flat;
                }
            }
            return far;
        }

        /// <summary>
        /// The seat a meal is taken to: a place at a table before any other seat, then the shortest walk; the earlier of two
        /// equal ones. -1 when none of the first <paramref name="count"/> is usable.
        /// </summary>
        public static int Pick(SeatOption[] seats, int count)
        {
            int best = -1;
            for (int i = 0; i < count && i < seats.Length; i++)
            {
                SeatOption seat = seats[i];
                if (!Usable(seat))
                {
                    continue;
                }
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                SeatOption was = seats[best];
                if ((seat.Table && !was.Table) || (seat.Table == was.Table && seat.Path < was.Path))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Seconds the meal is eaten at the table: longer for a meal that fills more, within the shortest and the longest.</summary>
        public static float EatSeconds(float fill)
        {
            float seconds = LeastSeconds + (fill > 0f ? fill : 0f) * SecondsPerFill;
            return seconds > MostSeconds ? MostSeconds : seconds;
        }

        /// <summary>True when a surface this high above the seat's floor is a table top to put a plate on.</summary>
        public static bool TableTop(float height)
        {
            return height >= TableLow && height <= TableHigh;
        }

        /// <summary>
        /// Where the food goes at a seat: the table's own tray before anything (never for a dish of the player's own, which
        /// comes on a plate), then a plate on a table top, then a plate in the lap, else the hand.
        /// </summary>
        public static Plate Serve(bool tray, bool ownDish, bool tableTop, bool lap)
        {
            if (tray && !ownDish)
            {
                return Plate.Tray;
            }
            return tableTop ? Plate.Table : lap ? Plate.Lap : Plate.Hand;
        }

        /// <summary>
        /// True when something in the player's bag can be eaten at a seat: bought (paid), opened, something the game lets
        /// the player eat or drink, and a meal worth sitting down to; the game's own inventory asks the same of its Eat.
        /// </summary>
        public static bool Eatable(bool paid, bool sealedUp, bool consumable, float saturation)
        {
            return paid && !sealedUp && consumable && Wanted(saturation);
        }

        /// <summary>What Eat at a seat eats: the meal in the bag in hand first (it is what the player holds), then a packed home portion.</summary>
        public static TakeoutFood Choose(bool bagMeal, int packed)
        {
            return bagMeal ? TakeoutFood.Bag : packed > 0 ? TakeoutFood.Packed : TakeoutFood.None;
        }

        /// <summary>
        /// True when the game's rest panel shows Eat: the option on, the player seated by the game's own rest (a chair, a
        /// bench), no meal already on, and something to eat.
        /// </summary>
        public static bool Offered(bool option, bool seatedResting, bool mealOn, TakeoutFood food)
        {
            return option && seatedResting && !mealOn && food != TakeoutFood.None;
        }

        /// <summary>
        /// True when dancing to the music lifts the mood now: danced long enough, and the last lift (game minutes; negative
        /// for none this session) is at least the gap ago.
        /// </summary>
        public static bool Lifts(float dancedMinutes, float nowMinutes, float lastLiftMinutes)
        {
            if (dancedMinutes < DanceAtLeast)
            {
                return false;
            }
            return lastLiftMinutes < 0f || nowMinutes - lastLiftMinutes >= DanceGapHours * 60f || nowMinutes < lastLiftMinutes;
        }

        /// <summary>The game's dance to start, 0 to 8: never the one danced last, from a roll of 0 to 1.</summary>
        public static int NextDance(int last, float roll)
        {
            if (roll < 0f)
            {
                roll = 0f;
            }
            int step = 1 + (int)(roll * (DanceKinds - 1));
            if (step > DanceKinds - 1)
            {
                step = DanceKinds - 1;
            }
            int from = last >= 0 && last < DanceKinds ? last : 0;
            return last >= 0 && last < DanceKinds ? (from + step) % DanceKinds : step - 1;
        }
    }
}
