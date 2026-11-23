namespace RealLifeOverhaul.Flare
{
    /// <summary>The city's star row of a business by the population's key, and the game's satisfaction, for the one rating read (StarsWithPosts, which adds the lasting review book).</summary>
    public sealed partial class FlareDirector
    {
        /// <summary>
        /// The city's star row of a place. The rows are kept by the population's key ("street|number", the street unescaped) and a place is written
        /// "street~number" with the street escaped, so the place is turned into its key first; a key by the place's own text never matched a street whose id holds a colon.
        /// </summary>
        public FlareStarRow CityStarsOf(string place)
        {
            if (!CityOn || string.IsNullOrEmpty(place))
            {
                return null;
            }
            string key = FlareCityPass.PlaceToKey(place);
            return key != null ? FlareUseRules.StarsOf(_city.StarRows, key) : null;
        }

        /// <summary>The game's own satisfaction of the player's business at a place, 0 to 100, the figure the old page draws its stars from; false with none.</summary>
        public bool GameSatisfaction(string place, out int overall)
        {
            return FlareGame.Satisfaction(FlareGame.Business(place), out overall, new int[4]);
        }
    }
}
