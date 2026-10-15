namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business as the rival tycoons use it (Scripts/Rivals, through Wiring/RivalLinks): every building's value
    /// written again at once when a rival's buying up of a district comes or ends, so its premium on the buildings for sale
    /// there (EstateBridges.Premium) is in their prices that day and not from the property business's next day. On a
    /// rival's move or an answer to one; never per frame.
    /// </summary>
    public sealed partial class EstateDirector
    {
        /// <summary>The buildings' values written again now, while the property part runs.</summary>
        public void Revalue()
        {
            if (!_running || _broken || _state == null || _partBroken[(int)EstatePart.Property] || !Settings.Estate)
            {
                return;
            }
            EstateGame.WriteValues(_state, _climate.Index);
        }
    }
}
