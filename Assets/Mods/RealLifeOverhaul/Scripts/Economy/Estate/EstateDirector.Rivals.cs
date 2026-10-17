namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business as the rival tycoons use it (Wiring/RivalLinks): on a rival's move, never per frame.
    /// </summary>
    public sealed partial class EstateDirector
    {
        /// <summary>The buildings' values written again now, so a rival's premium on a district (EstateBridges.Premium) is in its prices that day.</summary>
        public void Revalue()
        {
            if (On(EstatePart.Property))
            {
                EstateGame.WriteValues(_state, _climate.Index);
            }
        }
    }
}
