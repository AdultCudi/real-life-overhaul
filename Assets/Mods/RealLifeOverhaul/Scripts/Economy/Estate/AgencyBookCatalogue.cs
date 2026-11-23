namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every figure of the agency's book of other owners' blocks (management contracts). Game-free. Money is whole dollars, a share thousandths unless named, hours are
    /// tenths of an hour, days are the game's own. The measures a tenant, a repair call and the city's inspection are judged by are the property business's own
    /// (<see cref="EstateCatalogue"/>): only what is new to managing for somebody else is here.
    /// </summary>
    public static class AgencyBookCatalogue
    {
        public const int BlocksMost = 6;            // blocks held at once
        public const int OffersMost = 3;            // offers open at once
        public const int LostKept = 4;              // blocks lost that the page still shows, the newest
        public const int CallsMost = 8;             // open repair calls a block keeps count of
        public const int CityOwners = 6;            // landlords of the city who are not rivals, named by EstateText.Owner

        public const int OfferEveryDays = 5;        // days between one landlord's call for pitches and the next
        public const int OfferOpenDays = 6;         // days an offer stands before the owner chooses
        public const int OwnerRivalShare = 500;     // thousandths of the offers that come from a rival of the save who is a landlord, when there is one

        public const int UnitsLeast = 4, UnitsMost = 24;       // flats in a block
        public const int FlatSqmLeast = 40, FlatSqmMost = 110; // a flat's floor area, square metres

        public const int FeeAskLeast = 70, FeeAskMost = 110;   // the owner's asking fee, thousandths of the rent collected: round the property manager's 100
        public const int UndercutPct = 80;                      // a pitch that undercuts is made at this per cent of the asking fee
        public const int BidPctLeast = 85, BidPctMost = 105;   // a rival agency's pitch, per cent of the asking fee

        public const int RivalAgencyShare = 500;    // thousandths of the rival landlords who run an agency
        public const int RivalBidShare = 600;       // thousandths of the rival agencies that pitch for a given block
        public const int RivalBidsMost = 3;         // rival pitches for one block
        public const int RivalSkillLeast = 40, RivalSkillMost = 90;   // an agency's skill: the least for a small landlord
        public const int RivalUnitsPerPoint = 8;    // units a rival landlord holds for each point of skill over the least
        public const int SkillWeight = 2;           // a point of the agents' skill counts for this many points of the fee
        public const int RepDivisor = 20;           // the agency's reputation in the district, 0 to 1000, counts as a twentieth of its points

        public const int HoursPerUnit = 1;          // tenths of an agent's hour a day for each flat the agency manages
        public const int OwnUnitHours = 1;          // ...and for each unit of the player's own buildings put under the agency
        public const int OwnCoverLeast = 500;       // thousandths of the hours the agents must cover before the agency runs the player's own buildings that day

        public static readonly int[] Standards = { 450, 520, 580 };   // what an owner expects of the tenants' satisfaction: fair, good, strict
        public const int StartCondLeast = 450, StartCondMost = 900;   // a block's condition when the agency first sees it
        public const int StartLetLeast = 800, StartLetMost = 950;     // thousandths of its flats let when the agency takes it on

        public const int LeaseEndPermille = 8;      // chance a day that a let flat's lease ends: about every 125 days, the property business's own 60 to 180
        public const int ReletBase = 100;           // chance a day an empty flat is let again, with the agents covering every hour and a steady market
        public const int ReletMost = 600;
        public const int AnswerPermille = 700;      // chance a day an open repair call is answered, with the agents covering every hour
        public const int ServiceSlope = 200;        // points of satisfaction lost at the worst when the agents cover none of the hours
        public const int ServedGood = 800, ServedPoor = 500;   // thousandths of the hours covered that keep a building up, and below which it runs down
        public const int CondUp = 1, CondDown = 1;  // condition a day

        public const int LettingFeeDays = EstateCatalogue.AgentLetDays;   // the agency's fee for a flat it lets again, days of the agreed rent
        public const int MissesMost = 3;            // months in a row under the owner's standard before the block is taken away
        public const int PenaltyPct = 50;           // per cent of a month's fee the owner withholds when the standard was missed
        public const int StrikesMost = 3;           // failed inspections in a row before the block is taken away

        public const int RepWon = 15, RepKept = 10, RepMissed = -20, RepLost = -40, RepResigned = -10, RepRival = -5;   // the agency's reputation in the district
    }
}
