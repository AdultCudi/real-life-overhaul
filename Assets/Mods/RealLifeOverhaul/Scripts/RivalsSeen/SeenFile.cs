using System.Collections.Generic;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// One rival as their source has them today: a tycoon of the rich list (Scripts/Rivals) or one of the game's own rival owners (the game's Rivals app). Read by the
    /// director once a game day and never kept: every figure here is the source's own, nothing is made up.
    /// </summary>
    public sealed class SeenSubject
    {
        public string Key = string.Empty;       // "t" and the tycoon's index, or "g" and the game's own id of the rival
        public string Name = string.Empty;      // as their source writes it
        public string Firm = string.Empty;      // a tycoon's holding, or an owner's busiest business; empty where there is none
        public bool Tycoon;
        public long Figure;                     // a tycoon's worth, or an owner's takings of the last seven days, in today's money
        public long Before;                     // the same a week ago, where the source keeps it; 0 where it does not
        public int Shops;                       // businesses they run now
        public int ShopsBefore;                 // ...and a week ago; -1 where the source does not keep it
        public bool Pressed;                    // a counter of the player's holds them back (a tycoon slowed or barred)
        public string District = string.Empty;  // the game's own key of the district they work in; empty where the source has none
        public string Office = string.Empty;    // the place of their own premises, as the city's table writes a place; empty with none
        public bool Hamptons;                   // they own a house in the Hamptons
        public bool Gone;                       // out of the picture by their source's own rules (taken over, defeated, not come forward): seen nowhere
    }

    /// <summary>What the feature keeps of one rival: their week's mark, their car, and how well they know the player.</summary>
    public sealed class SeenRecord
    {
        public string Key = string.Empty;
        public string Name = string.Empty;      // as last read, for a line about somebody since gone
        public long Figure;                     // the figure at the last weekly mark
        public long Before;                     // ...and at the mark before it; 0 while there has been none
        public int Shops, ShopsBefore;
        public int MarkDay = -1;                // the day of the last mark; -1 for a record never marked
        public SeenForm Form = SeenForm.Steady; // how the business is doing, read at the mark
        public int Tier;                        // their means, read at the mark
        public int Rung = -1;                   // the rung of the car they drive; -1 while they have none on record
        public string Car = string.Empty;       // that car, by the game's own id of its type
        public int CarDay = -1;                 // the day they took it
        public int Known;                       // how well they know the player, tenths of a point of the social life's familiarity (0 to 1000)
        public int GreetDay = -1;               // the last day the player greeted them
        public int Greets;                      // greetings in all
        public int MetDay = -1;                 // the last day the player stood near them in the world
    }

    /// <summary>A sighting: what was seen of whom, where and when. What it says is fixed when it is made, so the paper and the pages read the same line every time.</summary>
    public sealed class SeenNote
    {
        public int Day, Hour;
        public SeenNoteKind Kind;
        public SeenSpot Spot;
        public SeenForm Form;                   // how the business was doing, by the real figures, when it was written
        public string Key = string.Empty;       // whose it is; empty for the player's
        public string Name = string.Empty;
        public string Firm = string.Empty;
        public string Place = string.Empty;     // the business it happened at, by its name
        public string District = string.Empty;  // the game's own key of the district
        public string Car = string.Empty;       // the game's own id of a car's type
        public string Old = string.Empty;       // ...and of the car before it
        public bool Posted;                     // it has gone out on Flare, or its hour for that has passed
    }

    /// <summary>The save's part of the rivals seen around the city. Game-free.</summary>
    public sealed class SeenFile
    {
        public int Seed;
        public int Day = -1;                    // the last day whose pass has run; -1 for a new file
        public int Hour = -1;                   // the last hour of that day gone through
        public int PlayerDay = -1000;           // the day of the last line about the player
        public bool Coast = true;               // the city has the Hamptons (read each day, never saved): without them nobody weekends there
        public readonly List<SeenRecord> Records = new List<SeenRecord>(32);
        public readonly List<SeenNote> Notes = new List<SeenNote>(SeenCatalogue.NotesKept + 4);

        /// <summary>The record of a rival, or null.</summary>
        public SeenRecord Of(string key)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                if (Records[i].Key == key)
                {
                    return Records[i];
                }
            }
            return null;
        }
    }

    /// <summary>Where a rival is at an hour: the place, how smart an eating place it is, and whether they sit alone.</summary>
    public struct SeenWhere
    {
        public SeenSpot Spot;
        public int Grade;                       // an eating place's: 0 modest, 1 middling, 2 smart
        public bool Alone;                      // a quiet table for one
    }

    /// <summary>One rival who is somewhere this hour, for the world to look at: who, where, and their turn among the others.</summary>
    public struct SeenDue
    {
        public int Subject;                     // their place in the day's list
        public SeenWhere Where;
        public int Pick;                        // 0 to 999: which of the places that fit, the same all day
        public int Order;                       // the lower goes first
    }

    /// <summary>
    /// What the rules ask of the city (SeenGame reads it from the game; the checks hand in a fake): the game's cars as a ladder, and the eating places of a district.
    /// Asked on the day's pass, never per frame.
    /// </summary>
    public interface ISeenCity
    {
        /// <summary>The game's cars on the ladder, the cheapest first.</summary>
        int Cars { get; }

        /// <summary>A car of the ladder by its place: the game's own id of its type.</summary>
        string Car(int index);

        /// <summary>The districts a rival with none of their own may eat in, in the game's own order.</summary>
        int Districts { get; }

        string District(int index);

        /// <summary>
        /// An eating place of a district that is open at an hour of a day, for a grade (0 modest to 2 smart), chosen from those open by <see cref="SeenRules.VenueIndex"/>
        /// and <paramref name="pick"/> (0 to 999): its name. False where the district has none open then.
        /// </summary>
        bool Eatery(string district, int grade, int pick, int day, int hour, out string name);
    }
}
