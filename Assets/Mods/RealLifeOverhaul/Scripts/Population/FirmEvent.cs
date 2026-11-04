namespace RealLifeOverhaul.Population
{
    /// <summary>One thing a firm did: the kind, whose firm it is, the day, the firm's name as the game names it, and a figure that goes with the kind (none for an opening).</summary>
    public struct FirmEvent
    {
        public FirmEventKind Kind;
        public Owner Owner;
        public int Day;
        public int Value;
        public string Name;
    }
}
