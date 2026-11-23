namespace RealLifeOverhaul.Flare
{
    /// <summary>The one read of a business's rating that the interface kit's pages share: the city's star rows found by the population's key, the feed's review posts on top.</summary>
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

        /// <summary>
        /// The stars a page shows for a business: the city's row (<see cref="CityStarsOf"/>) and the review posts of the feed that went up for it. Null with none of either;
        /// the city's own row when no post is about the place, so nothing is copied then.
        /// </summary>
        public FlareStarRow RatingOf(string place, long now)
        {
            FlareStarRow city = CityStarsOf(place);
            if (_file == null || string.IsNullOrEmpty(place))
            {
                return city;
            }
            FlareStarRow row = null;
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.Source != FlareSource.Review || p.Author == FlareAuthor.Player || p.Place != place || p.At > now
                    || (p.Author == FlareAuthor.Network && _file.Net.IsHidden(p.AuthorId)))
                {
                    continue;
                }
                row = row ?? CopyOf(city, place);
                switch (FlareRules.Clamp(p.Arg, 1, 5))
                {
                    case 1: row.One++; break;
                    case 2: row.Two++; break;
                    case 3: row.Three++; break;
                    case 4: row.Four++; break;
                    default: row.Five++; break;
                }
            }
            return row ?? city;
        }

        /// <summary>The game's own satisfaction of the player's business at a place, 0 to 100, the figure the old page draws its stars from; false with none.</summary>
        public bool GameSatisfaction(string place, out int overall)
        {
            return FlareGame.Satisfaction(FlareGame.Business(place), out overall, new int[4]);
        }
    }
}
