using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>One of the player's staff as RLO Texts reads them (Wiring/Wiring.TextsCity). Game-free; refilled each look.</summary>
    public sealed class TextStaff
    {
        public string Id;            // the game's employee id
        public string Name, First;
        public string Job;           // in the card's English words (TextWords.StaffJob)
        public string Shop;          // the business they work at, as the game names it
        public int Seed;             // of their name: their style of writing
        public int Paid;             // what they are paid an hour, hundredths
        public int Going;            // the going rate for them, hundredths an hour
        public int AskDay;           // the day they asked for a raise; 0 with none waiting
        public int AskTo;            // ...what they asked for an hour, hundredths
        public int Raise;            // the raise agreed through the mod, hundredths an hour
        public bool Sick;            // off sick today, as the game picked
        public int WeekHours;        // hours the game has them down for this week
        public int MostHours;        // the most hours a week they agreed to (their demand); 0 with none
        public bool Complaining;     // the game's own complaint of theirs is open: the mod then says nothing of hours
    }

    /// <summary>What RLO Texts last saw of one of the staff: the raise, and the days or week each message was sent.</summary>
    public sealed class TextStaffMark
    {
        public string Id;
        public int RaiseSeen = -1;   // the raise when last seen, hundredths; -1 never seen, so a first sight thanks nobody
        public int AskTold;          // the day the raise asked for was texted
        public int SickTold;         // the day the sick message was sent
        public int HoursWeek = -1;   // the week the hours were complained of
        public int LastDay;          // the last day any message of theirs was sent: one a day at most
        public int SeenDay;          // the last day they were seen: marks of staff gone are let go
    }

    /// <summary>The player's life as the people in it could hear of it, read once a game hour. Game-free; refilled each look.</summary>
    public sealed class TextLife
    {
        public int Day;
        public int Children;         // children of the family life, adopted or born
        public string ChildName;     // the newest
        public bool Adopted;         // ...adopted
        public string Partner;       // the partner's first name, or null
        public bool Married;
        public int MarriedDay;
        public string Degree;        // the degree graduated with today, or null
        public int CourtClosed;      // the court's cases against the player closed so far
        public bool CourtGuilty;     // ...the newest a conviction
        public string Listed;        // the player's company on the stock market, or null
        public int ListedDay;
        public string Business;      // the player's newest business, or null
        public int BusinessDay;      // ...the game's creation day of it
        public long Worth;           // the player's net worth (the Finance app's)
        public bool HasWorth;        // the worth was read
    }

    /// <summary>Where the player's life stood when RLO Texts last looked; saved, so a load does not tell everything again.</summary>
    public sealed class TextLifeMark
    {
        public int Day = -1;         // the day of the last look; -1 never looked
        public int Children;
        public int MarriedDay;
        public int DegreeDay;
        public int CourtClosed;
        public int ListedDay;
        public int BusinessDay;
        public long Worth;
        public int WorthDay = -1;
    }

    /// <summary>Something of the player's life the people in it hear of, by TextMemoryRules' event codes.</summary>
    public sealed class TextLifeEvent
    {
        public string Kind;          // "crash", "arrest", "court_guilty", "court_cleared", "opened", "graduated", "listed", "married", "baby", "win", "loss", "knocked"
        public int Day;
        public string Value;         // the street, business, company, degree or sum; or null
        public string Child;         // a baby's name
        public bool Adopted;         // a child adopted
        public int Texted = -1;      // the person of the social life who texted about it, or -1
        public bool Done;            // its own text sent, or no one to send it
        public bool Gossiped;        // passed on by gossip (once)
    }

    /// <summary>A person of the social life as gossip and the life's texts read them (Wiring/Wiring.TextsCity).</summary>
    public sealed class TextPerson
    {
        public int Index;
        public string First;
        public int Seed;
        public int Stage;            // SocialStage: 0 a stranger ... 3 a close friend, 4 dating, 5 the partner
        public bool Together;        // the partner
        public bool Family;          // Mom
        public bool Met;
        public bool Drifted;
        public bool Asleep;          // asleep at this hour: no text now
        public int District;
        public int Origin;           // SocialOrigin
        public int LastTextDay;
        public int Closeness;        // 0 to 100
    }

    /// <summary>Who sends a life event's text: their place in the social life, the intent and branch of the card, and the written line's key.</summary>
    public struct TextLifeSender
    {
        public int Index;
        public string Intent, Branch, Key;
    }

    /// <summary>The lists RLO Texts fills from the city on each look, kept and cleared.</summary>
    public sealed class TextCityViews
    {
        public readonly List<TextStaff> Staff = new List<TextStaff>(16);
        public readonly List<TextPerson> People = new List<TextPerson>(16);
        public readonly TextLife Life = new TextLife();
    }
}
