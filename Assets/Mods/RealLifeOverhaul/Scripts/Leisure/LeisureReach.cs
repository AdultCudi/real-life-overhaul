namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// How far leisure reaches, in metres on the flat, and the one test every reach is made with: CitySpots.Flat is the
    /// square of a distance, so a reach is squared before the two are compared. Game-free, so NavCheck runs it
    /// (LeisureReachCheck.cs).
    /// </summary>
    public static class LeisureReach
    {
        public const float ChessFrom = 30f, ChessTo = 70f;   // metres from the player: the ring the chess table is put out in
        public const float ChessKept = 140f;                 // metres: farther off, the table is taken in again
        public const float ChessOffered = 3f;                // metres from the player's stool: a game is offered
        public const float ChessStarts = 4f;                 // metres from the stool: farther, a game is refused
        public const float ChessSeated = 0.5f;               // metres from the stool: near enough to be sat down
        public const float ChessLeash = 7f;                  // metres from the table
        public const float StoolOut = 0.62f;                 // metres from the table's centre to a stool's (chess_sit)
        public const float ChessTestAhead = 3.5f;            // metres ahead of the player: the test button's table

        public const float NightNear = 75f;                  // metres: the open venue door the night's two stand outside
        public const float NightKept = 120f;                 // metres: farther off, the two are cleared
        public const float NightApart = 0.75f;               // metres at least between the two

        public const float Leash = 6f;                       // metres from an activity's anchor, unless it sets its own
        public const float DanceLeash = 3f;                  // metres from where the dance began
        public const float BarLeash = 4.5f;                  // metres from the bar
        public const float StandingLeash = 3f;               // metres from where the player stood: fishing, sightseeing
        public const float BarOffered = 2.8f;                // metres from the game's cocktail bar
        public const float DanceOffered = 3.5f;              // metres from the game's dance floor
        public const float BarStepUp = 0.5f;                 // metres from the place to stand at the bar
        public const float Settles = 0.6f;                   // metres: the most the body is brought on to its spot
        public const float ApiPlace = 6f;                    // metres: a place another feature names must be this near
        public const float RackOffered = 2.6f;               // metres from the clothes rack
        public const float RackAway = 2.5f;                  // metres at least from the player when the rack is stood
        public const float FishingLandmark = 320f;           // metres from a waterside landmark

        /// <summary>True if a square of a flat distance (CitySpots.Flat) is nearer than so many metres.</summary>
        public static bool Within(float flatSquare, float metres) => flatSquare < metres * metres;

        /// <summary>True if a square of a flat distance is farther than so many metres.</summary>
        public static bool Beyond(float flatSquare, float metres) => flatSquare > metres * metres;
    }
}
