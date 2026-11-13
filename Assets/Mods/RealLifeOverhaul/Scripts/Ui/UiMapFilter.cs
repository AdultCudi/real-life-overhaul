namespace RealLifeOverhaul.Ui
{
    /// <summary>A filter of the city map: its switch, its glyph in its own colour, its name, and whether it can take the camera to its places.</summary>
    public struct UiMapFilter
    {
        public UiIcon Icon;
        public string Name;        // "Subway stations"
        public bool On;
        public bool Locate;        // a locate button that takes the camera to the nearest of them
    }
}
