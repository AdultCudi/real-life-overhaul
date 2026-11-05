namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// When the dock must read the game's map again. The look runs four times a second only while the dock is drawn, so a close and an open in between leave no trace
    /// of the close: a gap longer than a look is read as the map having been shut. Game-free.
    /// </summary>
    public sealed class MapLook
    {
        /// <summary>Seconds between two looks past which the map must have been shut and opened again (a look is every quarter second).</summary>
        public const float Gap = 1f;

        private float _last = float.NegativeInfinity;
        private bool _wasOpen;

        /// <summary>The places on each line are counted again at the next layout.</summary>
        public bool CountsStale = true;

        /// <summary>The game's groups are found again at the next layout.</summary>
        public bool GroupsStale = true;

        /// <summary>A look at the map at time <paramref name="now"/>: a fresh open (the edge, or a gap since the last look) asks for counts and groups afresh.</summary>
        public void Look(bool open, float now)
        {
            if (open && (!_wasOpen || now - _last > Gap))
            {
                CountsStale = true;
                GroupsStale = true;
            }
            _wasOpen = open;
            _last = now;
        }

        /// <summary>What the dock's page says changes with the district pointed at or kept only while it is the district page.</summary>
        public static int DistrictStamp(bool districtPage, int stamp) => districtPage ? stamp : 0;
    }
}
