using System;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What Flare in the city knows of one business, for the city map's place card (Ui/Map): its rating and how many have reviewed it, its prices against the city's, what the
    /// simulation counts arriving, what a reviewer would weigh most now and its newest reviews each by the resident who wrote it. Read when a card opens and once a day for the
    /// places kept, never a frame; nothing is saved here, and the city's businesses are all in the table, rivals' and the player's alike.
    /// </summary>
    public sealed partial class FlareDirector
    {
        private const int NewestMost = 6;   // reviews a card holds

        /// <summary>The facts for a place as Flare writes one ("street~number"); not Found when the residents' side does not run or the business is not in the table.</summary>
        public MapFlareFacts PlaceFacts(string place)
        {
            PopulationState s = CityOn ? Residents : null;
            if (s == null || place == null)
            {
                return default;
            }
            int b = _cityPass.BusinessOfPlace(place);
            BusinessTable bt = s.Businesses;
            if (b < 0 || b >= _city.BusinessCount || b >= bt.Count)
            {
                return default;
            }
            // what a reviewer would weigh most now: the weakest of price (against a middle class household), quality, wait and cleanliness, as the day's reviews are written
            int price = PoolRules.PriceTerm(bt.Price[b], 1, 1000);
            int priceScore = price >= 1000 ? 100 : price / 10;
            var facts = new MapFlareFacts
            {
                Found = true, Tenths = _city.Rating(b), ReviewCount = _city.ReviewCount[b], Price = bt.Price[b], Arrivals = bt.Visits[b],
                Why = (int)FlareUseRules.Reason(priceScore, bt.Quality[b], FlareUseRules.WaitScore(bt.Visits[b], bt.Room[b]), bt.Content[b]),
            };
            var newest = new FlareCityReview[NewestMost];
            int n = FlareCityRules.NewestReviews(_city, b, newest);
            facts.Newest = new MapFlareReview[n];
            for (int i = 0; i < n; i++)
            {
                bool female = FlareCityViews.FemaleOf(s, _cityPass.PersonSlot, newest[i].Person);
                facts.Newest[i] = new MapFlareReview { Person = newest[i].Person, Female = female, Stars = newest[i].Stars, Day = newest[i].Day, Name = ResidentName(newest[i].Person, female) };
            }
            return facts;
        }
    }
}
