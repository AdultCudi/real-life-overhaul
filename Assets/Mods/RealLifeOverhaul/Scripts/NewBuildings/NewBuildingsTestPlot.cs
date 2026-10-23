namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The test entries' building and its ground, and nothing else of the mod's. The developer never has to look for a
    /// free spot: the building is tried round where they stood when they asked, nearest first, then on six lots of the open
    /// grass round the airport between Industry City and the Hamptons. Ground a person picks by eye is nearly always taken
    /// (a tree's trunk, a barricade: a player's game, 23 Oct 2026), so the six were found by running a copy of the ground
    /// check over the build's own scene data, its traffic lanes and the player's walking mesh: each lot has free ground
    /// 16 m round it, fronts a street within reach and has its door on the city's own walking mesh. That ground is very
    /// likely where a free update of the game puts its airport, so the lots are checked at every load like any made
    /// building's, and the building moves along or stands down when the game's own content is there. The mod's own places
    /// are never put here.
    /// </summary>
    public static class NewBuildingsTestPlot
    {
        public const string Id = "newbuildings_test";                       // its hold in the special buildings' book
        public const string Street = MadeStreets.Prefix + "provinglane";    // 1 Proving Lane
        public const int Number = 1;
        public const string Size = "ba:buildingsize_c";                     // 225 m2: the game's C1 structure takes an office
        public const int Version = 1;
        public const string Type = "ba:buildingtype_office";
        public const string District = "ba:neighborhood_industrycity";      // the district the ground is counted to now (the nearest buildings')
        public const int Traffic = 30;                                      // of 100: a quiet street's

        private const float Width = 30f;   // m along the street: room for every design of its size
        private const float Depth = 34f;   // m back from it

        /// <summary>
        /// The open ground's six lots in the order they are tried, each turned with its door to its nearest street (Road_237
        /// to the north for the first four, Road_239 for the fifth, Road_237's bend to the east for the sixth).
        /// </summary>
        public static readonly MadeLot[] Lots =
        {
            new MadeLot(-2087f, -957f, 0f, Width, Depth),
            new MadeLot(-2383f, -957f, 0f, Width, Depth),
            new MadeLot(-2159f, -957f, 0f, Width, Depth),
            new MadeLot(-2295f, -957f, 0f, Width, Depth),
            new MadeLot(-1943f, -1277f, 180f, Width, Depth),
            new MadeLot(-2015f, -981f, 90f, Width, Depth),
        };

        public const float HereGap = 4f;   // m from where the developer stood to the front of the nearest lot

        /// <summary>m from where the developer stood to the fronts of the lots round them, nearest first.</summary>
        public static readonly float[] RingGaps = { HereGap, 20f, 40f };

        /// <summary>
        /// Degrees from the way the camera looked: straight ahead first, then the ways nearest to it, so the building stands
        /// where the developer was looking whenever that ground is free.
        /// </summary>
        public static readonly float[] RingTurns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        /// <summary>How many lots are tried round the developer before the open ground's.</summary>
        public static int RingLots => RingGaps.Length * RingTurns.Length;

        /// <summary>The test building on the open ground's lots alone, as when it was asked for from the main menu or indoors.</summary>
        public static MadeSpec Spec()
        {
            return Spec(false, 0f, 0f, 0f);
        }

        /// <summary>
        /// The test building as a feature would declare it: a new one each time, so nothing it is given is shared. With
        /// <paramref name="here"/>, the lots round where the developer stood (and the way the camera looked) come first,
        /// each turned with its front to them, and the open ground's after them.
        /// </summary>
        public static MadeSpec Spec(bool here, float x, float z, float yaw)
        {
            MadeLot[] lots;
            if (here)
            {
                lots = new MadeLot[RingLots + Lots.Length];
                int k = 0;
                foreach (float gap in RingGaps)
                {
                    foreach (float turn in RingTurns)
                    {
                        lots[k++] = NewBuildingsRules.LotAhead(x, z, yaw + turn, Width, Depth, gap);
                    }
                }
                System.Array.Copy(Lots, 0, lots, k, Lots.Length);
            }
            else
            {
                lots = (MadeLot[])Lots.Clone();
            }
            return new MadeSpec
            {
                Id = Id, Street = Street, Number = Number, Size = Size, Version = Version, Type = Type, District = District,
                Design = null, Lots = lots, Preferred = 0, Traffic = Traffic, Wanted = null
            };
        }
    }
}
