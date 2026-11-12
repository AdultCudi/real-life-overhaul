using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Parties
{
    /// <summary>The player's home as a party finds it: tidiness 0 to 100 (-1: not known), and whether it has power and water.</summary>
    public struct HomeFacts
    {
        public int Tidy;
        public bool Dark, Dry;
    }

    /// <summary>A place a party can be held at, as the game has it now.</summary>
    public struct PlaceFacts
    {
        public bool Found;       // the home is the player's, or the venue stands and is one
        public bool Open;        // open for the whole of the party (a home always is)
        public bool Own;         // a venue of the player's own
        public bool Made;        // a venue of the player's own that is shut by their own doing (their switch or opening hours), not by a closure of the mod's or the city's
        public string Street, Name;
        public int Number;
        public int Sqm;          // its floor, square metres; 0: not known
        public int District;     // the game's district it is in, as the social life numbers them; -1: not known
    }

    /// <summary>One of a business's staff as an event finds them.</summary>
    public struct StaffHand
    {
        public string Id, Name;
        public int Mood;         // the game's satisfaction, 0 to 100; -1 where nobody keeps one (a rival's crew)
        public bool Underpaid;   // under the going rate by the company's rule, or the game's wage formula where the company keeps no file
        public bool Sick;        // off sick or away today
        public StaffFile File;   // the company's file of them (their standing); null where it keeps none
    }

    /// <summary>How a notice reads.</summary>
    public enum PartyTone { Info, Good, Warn, Bad }

    /// <summary>
    /// Everything parties and company events read from or do to the game and the other features, so every flow
    /// (<see cref="PartyDesk"/>, <see cref="StaffDesk"/>) is game-free and the offline checks drive the very code the game
    /// runs, with a world of their own. PartyGame answers it in the game. Nothing here is asked per frame.
    /// </summary>
    public interface IPartyWorld
    {
        /// <summary>The option is on, under the living economy.</summary>
        bool On { get; }

        /// <summary>A guest of a Going Public session: the city's people are the host's, so nothing is booked, begun or answered.</summary>
        bool Guest { get; }

        /// <summary>The game minute now (day x 1440 + hour x 60 + minute), the day, and the city's price level (1000 at the start).</summary>
        int Now { get; }
        int Day { get; }
        int Level { get; }

        /// <summary>The player's money, whole.</summary>
        long Money { get; }

        /// <summary>The investors' reserve the wallet must keep (startup money's floor), whole money; 0 with none.</summary>
        long Floor { get; }

        /// <summary>Money out through the mod's booking path, at a business when <paramref name="place"/> is given. False, and nothing taken, if it cannot be.</summary>
        bool Pay(int amount, string key, string place);

        /// <summary>Money given back through the same path, under the same key.</summary>
        void Refund(int amount, string key, string place);

        /// <summary>Somebody of the social life's cast by their place in it; null with nobody there.</summary>
        SocialPerson Person(int index);

        /// <summary>How many places the cast has.</summary>
        int People { get; }

        /// <summary>A relationship moved, by the social life's own call (its option that nothing is ever lost, its stages).</summary>
        void Change(SocialPerson person, float familiarity, float affection, float trust);

        void Remember(SocialPerson person, MemoryCode code);

        /// <summary>The place as it is now: the player's home, or the venue at an address.</summary>
        PlaceFacts Place(PartyPlace place, string street, int number, int start, int minutes);

        /// <summary>The home a party was held at, by its address (the home now with none given); not known gives <see cref="HomeFacts.Tidy"/> -1.</summary>
        HomeFacts Home(string street, int number);

        /// <summary>The player is at the place now: inside the home, or at or inside the venue.</summary>
        bool There(PartyPlace place, string street, int number);

        /// <summary>The home at an address is this much messier.</summary>
        void Mess(int points, string street, int number);

        /// <summary>The staff of a business of the player's now, into <paramref name="into"/> (cleared first); -1 if the business is not the player's.</summary>
        int Staff(string place, List<StaffHand> into);

        /// <summary>The game's satisfaction of one employee moved by so many points.</summary>
        void Mood(string id, float points);

        /// <summary>The company's file of one employee of the player's (their standing); null where it keeps none.</summary>
        StaffFile File(string id);

        /// <summary>What a business of the player's is called; empty when it is not theirs.</summary>
        string Business(string place);

        /// <summary>A refusal as a sentence, for a notice that says why a manager could not book.</summary>
        string Why(PartyRefusal why);

        /// <summary>
        /// The business is closed for the opening hours an event takes, by the mod's own closures: the hours it is shut for,
        /// 0 when the event is outside its opening hours, -1 when it is open then and cannot be shut.
        /// </summary>
        int Shut(string place, int day, int hour, int minutes);

        /// <summary>The business has a manager at work who can be told to hold an event.</summary>
        bool Manager(string place);

        /// <summary>A notice: its key and the names its words are filled with, in pairs.</summary>
        void Tell(PartyTone tone, string key, params string[] pairs);

        /// <summary>A text from somebody of the cast, by the social life's own texts and their limits. False if it was not sent.</summary>
        bool Text(SocialPerson person, string key, params string[] pairs);

        /// <summary>A venue of a kind open for the whole of a party from a game minute, found as the social life finds one for an outing; not Found with none.</summary>
        PlaceFacts FindVenue(PartyPlace place, int start, int minutes);

        /// <summary>Money as the game writes it.</summary>
        string Cash(int amount);

        /// <summary>A day and an hour as the game writes them.</summary>
        string When(int day, int hour);
    }
}
