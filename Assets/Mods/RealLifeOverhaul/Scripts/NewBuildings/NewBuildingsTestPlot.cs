namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The test entries' building and its plot, and nothing else of the mod's: six lots of open ground on the south side
    /// of the straight stretch of the road round the north of the airport between Industry City and the Hamptons
    /// (the traffic's Road_237, its southern lane at z -903 from x -2313 to -2087), each 30 m along the road and 34 m deep,
    /// their fronts 6 m from that lane's middle. The ground there is the player's walking mesh and nobody's building
    /// (the feasibility study's land and road maps). That ground is very likely where a free update of the game puts its
    /// airport, so the lots are checked at every load like any made building's, and the test building stands down or
    /// moves along when the game's own content is there. The mod's own places are never put here.
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

        private const float FrontZ = -909f;    // m: the lots' front line, 6 m south of the southern lane's middle
        private const float Width = 30f;       // m along the road
        private const float Depth = 34f;       // m back from it
        private const float Facing = 0f;       // degrees: the road lies to the north (+Z) of every lot

        /// <summary>The six lots, west to east, 35 m apart (5 m between two).</summary>
        public static readonly MadeLot[] Lots =
        {
            new MadeLot(-2290f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
            new MadeLot(-2255f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
            new MadeLot(-2220f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
            new MadeLot(-2185f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
            new MadeLot(-2150f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
            new MadeLot(-2115f, FrontZ - Depth * 0.5f, Facing, Width, Depth),
        };

        public const float HereGap = 4f;   // m from where the developer stood to the front of the lot ahead of them

        /// <summary>The test building on the plot's six lots alone, as when it was asked for from the main menu.</summary>
        public static MadeSpec Spec()
        {
            return Spec(false, 0f, 0f, 0f);
        }

        /// <summary>
        /// The test building as a feature would declare it: a new one each time, so nothing it is given is shared. With
        /// <paramref name="here"/>, the lot ahead of where the developer stood when they asked comes first and the plot's six
        /// after it: in a player's game the plot's ground was taken by the game's barricades (23 Oct 2026), and the ground
        /// check still refuses any lot something stands on.
        /// </summary>
        public static MadeSpec Spec(bool here, float x, float z, float yaw)
        {
            MadeLot[] lots;
            if (here)
            {
                lots = new MadeLot[Lots.Length + 1];
                lots[0] = NewBuildingsRules.LotAhead(x, z, yaw, Width, Depth, HereGap);
                System.Array.Copy(Lots, 0, lots, 1, Lots.Length);
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
