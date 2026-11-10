namespace RealLifeOverhaul.Parties
{
    /// <summary>Where a party with friends is held: the player's own home, or a place the social life already meets people at.</summary>
    public enum PartyPlace { Home = 0, Restaurant = 1, Bar = 2 }

    /// <summary>What a party's food and drink are, a head.</summary>
    public enum PartyTier { Simple = 0, Good = 1, Lavish = 2 }

    /// <summary>Where a party stands. Saved as a number: new ones go at the end.</summary>
    public enum PartyState { Planned = 0, Running = 1, Held = 2, Missed = 3, Cancelled = 4, Empty = 5 }

    /// <summary>What somebody answers to an invitation, by the first reason that holds. Saved as a number: new ones go at the end.</summary>
    public enum PartyAnswer { Yes = 0, NotClose = 1, Quarrel = 2, Asleep = 3, Working = 4, Far = 5, RoughDay = 6, Busy = 7 }

    /// <summary>A company event for a business's staff. Saved as a number: new ones go at the end.</summary>
    public enum StaffEventKind { Lunch = 0, EveningOut = 1, TeamDay = 2 }

    /// <summary>Where a company event stands. Saved as a number: new ones go at the end.</summary>
    public enum StaffEventState { Planned = 0, Held = 1, Cancelled = 2, Empty = 3, Running = 4 }

    /// <summary>Where an invitation to somebody else's party stands. Saved as a number: new ones go at the end.</summary>
    public enum InviteState { Asked = 0, Accepted = 1, Declined = 2, Went = 3, Missed = 4, Lapsed = 5, Off = 6 }

    /// <summary>Why something cannot be booked, begun or answered; None when it can. Never saved, so a reason may be added anywhere.</summary>
    public enum PartyRefusal
    {
        None, Off, Guest, NoSave, NoHome, NoVenue, VenueShut, TooSoon, TooFar, BadHour, DayTaken, TooMany, NoGuests, NobodyComes, TooManyGuests, NoMoney,
        NoBusiness, NoStaff, TooOften, Reserve, NotPlanned, NotThere, TooEarly, TooLate, NoManager, Begun
    }

    /// <summary>
    /// Every number of parties and company events, with its unit and where it comes from. The money is written at the start's
    /// prices and asked of <see cref="PriceRules.At(int, int)"/> when charged. Game-free.
    /// </summary>
    public static class PartyCatalogue
    {
        // ---- when
        public const int LeadHours = 3;               // hours at least between booking and the start: time for the invitations to be answered
        public const int AheadDays = 14;              // days ahead at most that a party or an event is booked for
        public const int FirstHour = 10;              // the earliest hour a party with friends starts at
        public const int LastHour = 22;               // ...and the latest
        public const int PlannedMost = 3;             // parties of the player's planned at once
        public const int EarlyMinutes = 30;           // game minutes before the start from which the host can begin
        public const int LateMinutes = SocialRules.LateMinutes;       // after the start: the host is late, as for any plan (20)
        public const int MissedMinutes = SocialRules.StoodUpMinutes;  // after the start with no host: missed, as a plan is stood up (75)
        public static readonly int[] Minutes = { 120, 180, 240 };     // game minutes a party lasts, by tier

        // ---- what it costs a head, at the start's prices, from the social life's own outings (SocialCatalogue.Outing)
        public static readonly int[] PlaceShare = { 100, 150, 150 };  // per cent of the food and drink by place: bought in at home, served at a venue
        public const int OwnShare = 100;              // per cent at a venue of the player's own: the goods at cost, no service, and no sale booked there

        // ---- who can come
        public const int SqmPerGuest = 8;             // square metres of the place a guest
        public const int GuestsLeast = 4;             // guests any place holds
        public static readonly int[] GuestsMost = { 16, 24, 40 };     // guests at most, by place
        public const int HomeCloseness = 20;          // closeness (0 to 100) an acquaintance needs before they come to the player's home
        public const int VenueCloseness = 10;         // ...and to a venue
        public const int QuarrelDays = 14;            // days a quarrel (stood up, rude, money kept, a break-up) keeps somebody away
        public const int FarChance = 300;             // per mille an acquaintance from another district stays away; a homebody always does
        public const int BusyChance = 80;             // per mille anybody has something else on
        public const int BusyFlaky = 250;             // ...somebody flaky
        public const int RoughChance = 500;           // per mille somebody on a rough day says no (SocialRules.PlanMoodNo)

        // ---- how it went
        public static readonly int[] TierQuality = { 55, 70, 85 };    // a party's quality (0 to 100) by tier, before the rest
        public const int TurnoutGood = 75;            // per cent of those who said yes that came: at or over it the party is fuller
        public const int TurnoutPoor = 50;            // ...under it, thinner
        public const int TurnoutBonus = 5, TurnoutPenalty = -10;
        public const int TidyGood = 70, TidyPoor = 40;                // the home's tidiness (0 to 100)
        public const int TidyBonus = 5, TidyPenalty = -15;
        public const int DarkPenalty = -25;           // a home with no power
        public const int DryPenalty = -10;            // a home with no water
        public const int LatePenalty = -15;           // the host came late
        public const int LeftPenalty = -20;           // the host was gone when it ended
        public const int GoodFrom = 60;               // quality from which a party is a good one
        public const int BadBelow = 40;               // ...under which it is a bad one: nothing gained, a little lost
        public const int TidyPerGuest = 2;            // points of tidiness a home party costs a guest who came
        public const int LargeFrom = 10;              // guests who came from which the City Ledger has a line

        // ---- what a party is worth to somebody who came: a share of a week's cap, whatever the money or the number of parties
        public static readonly int[] TierWorth = { 450, 700, 1000 };  // per mille of the cap one party of a tier can give at quality 100
        public const int WindowDays = 7;              // days over which a person's cap refills
        public const float CapFamiliarity = 8f;       // points of 100 a person can gain from parties in a window: an outing's worth
        public const float CapAffection = 6f;
        public const float CapTrust = 3f;
        public const float Ceiling = 70f;             // parties never lift a figure above this: the rest is earned one to one
        public const int SharedAt = 500;              // per mille of the cap a party gave from which it counts as a thing done together
        public const float BadAffection = -1f;        // a bad party, to each who came
        public const float MissedTrust = SocialRules.StoodUpTrust / 2f;          // the host never came: half of standing one person up, to each guest
        public const float MissedAffection = SocialRules.StoodUpAffection / 2f;

        // ---- calling it off
        public const int RefundEarly = 80;            // per cent back a day or more before the start
        public const int RefundLate = 50;             // ...under a day
        public const int RefundLast = 0;              // ...under LeadHours
        public const int EarlyHours = 24;

        // ---- other people's parties
        public const int InviteGapDays = 10;          // days at least between two invitations
        public const int InviteChance = 60;           // per mille a day that a friend who can invites the player, once the gap is over
        public const int InviteAhead = 2;             // days ahead their party is
        public const int InviteHour = 19;             // the hour it starts
        public const int InviteMinutes = 150;         // game minutes it lasts
        public const int InviteWorth = 600;           // per mille of the cap going is worth with the host
        public const float DeclineAffection = -1f;    // said no
        public const float LapseAffection = -2f;      // never answered
        public const float NoShowTrust = SocialRules.StoodUpTrust / 2f;          // said yes and did not come
        public const float NoShowAffection = SocialRules.StoodUpAffection / 2f;

        // ---- company events
        public static readonly int[] EventMinutes = { 60, 180, 480 }; // game minutes by kind: a lunch, an evening out, a team day
        public static readonly int[] EventHourFrom = { 11, 17, 9 };   // the hours a kind can start between
        public static readonly int[] EventHourTo = { 14, 21, 10 };
        public static readonly int[] EventWorth = { 330, 580, 1000 }; // per mille of the cap one event of a kind gives somebody paid fairly
        public const int StaffWindowDays = 30;        // days over which an employee's cap refills, and the lift fades
        public const float CapMood = 12f;             // points of the game's satisfaction (0 to 100) events can add to one employee in a window
        public const int CapStanding = 8;             // points of the company's standing (0 to 100) likewise, taken back as the lift fades
        public const int UnderpaidShare = 50;         // per cent of an event's worth to somebody underpaid: no substitute for pay
        public const int EventGapDays = 7;            // days at least between two events at one business
        public const int AfterHoursNo = 200;          // per mille an employee has a reason not to come to an event outside their working hours
        public const int LowMood = 20;                // an employee at or under this satisfaction does not come after hours
        public static readonly int[] ScheduleDays = { 0, 30, 60, 90 };           // a manager's schedule: off, or every so many days
        public const int RoundHour = 10;              // the hour of the day's round: the lifts fade, the managers book, an invitation may come (texts are quiet until 08:00)

        // ---- rivals
        public const int RivalEveryDays = 45;         // days at least between a rival outlet's staff events
        public const int RivalSpreadDays = 15;        // ...and up to so many more, by a draw of the outlet's own
        public const int RivalLunch = 500;            // per mille of a rival's events that are a lunch
        public const int RivalEvening = 350;          // ...an evening out; the rest a team day
        public const int RivalLevel = 1;              // the company level a rival's crew is underpaid by: Standard, as RivalFirms runs their day
        public const int RivalLogMost = 24;           // rival events kept for the paper and the page, the newest of every outlet always among them
        public const int ManagerRetryDays = 7;        // days between two notices that a manager could not book
        public const int HostedMost = 8;              // parties of the player's kept once over, for the page and for other features
        public const int EventsKept = 12;             // company events kept once over

        /// <summary>The food and drink of a tier a head, at the start's prices: two coffees, a meal, or a meal and drinks, as the social life prices them.</summary>
        public static int HeadBase(PartyTier tier)
        {
            switch (tier)
            {
                case PartyTier.Simple: return SocialCatalogue.Outing(PlanKind.Coffee).BaseCost * 2;
                case PartyTier.Good: return SocialCatalogue.Outing(PlanKind.Meal).BaseCost;
                default: return SocialCatalogue.Outing(PlanKind.Meal).BaseCost + SocialCatalogue.Outing(PlanKind.Drinks).BaseCost;
            }
        }

        /// <summary>A company event a head, at the start's prices: a meal; a meal and drinks; a meal, drinks, the cinema and the gym.</summary>
        public static int EventHeadBase(StaffEventKind kind)
        {
            int meal = SocialCatalogue.Outing(PlanKind.Meal).BaseCost, drinks = SocialCatalogue.Outing(PlanKind.Drinks).BaseCost;
            switch (kind)
            {
                case StaffEventKind.Lunch: return meal;
                case StaffEventKind.EveningOut: return meal + drinks;
                default: return meal + drinks + SocialCatalogue.Outing(PlanKind.Cinema).BaseCost + SocialCatalogue.Outing(PlanKind.Workout).BaseCost;
            }
        }

        /// <summary>The social life's kind of outing a place stands for: its venues, its opening hours and how much somebody likes it.</summary>
        public static PlanKind KindOf(PartyPlace place)
        {
            return place == PartyPlace.Bar ? PlanKind.Drinks : place == PartyPlace.Restaurant ? PlanKind.Meal : PlanKind.Coffee;
        }

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;
    }
}
