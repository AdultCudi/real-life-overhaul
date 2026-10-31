namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare in the city by rule, the player's name and their businesses (the brief's fourth step: "your fame lifts your businesses (founder-led), a
    /// scandal sinks them until answered"): each day some of the player's followers in the city hear of each of the player's businesses because they
    /// follow its founder, and while the player is in a scandal nobody answered, some of them hear of it against each business. Game-free.
    /// COST: a walk over the player's followers in the city for each of the player's businesses, once a day.
    /// </summary>
    public static partial class FlareCityRules
    {
        private const int SaltFounder = 81, SaltFounderInterest = 82, SaltScandal = 83;

        /// <summary>The day's founder-led talk (and a scandal's, while unanswered) of the player's businesses. Thousandths of a resident added, good and bad.</summary>
        public static void Founder(FlareCityState c, Population.PopulationState s, CityInputs inputs, int day, out long good, out long bad)
        {
            good = bad = 0;
            int n = c.FollowersOf(FlareCityState.PlayerAccount, out int start);
            for (int k = 0; k < inputs.PlayerBusinessCount && k < inputs.PlayerBusinesses.Length; k++)
            {
                int b = inputs.PlayerBusinesses[k];
                if (b < 0 || b >= c.BusinessCount || b >= s.Businesses.Count)
                {
                    continue;
                }
                int kind = s.Businesses.Kind[b];
                for (int i = 0; i < n; i++)
                {
                    int p = c.FollowerList[start + i];
                    if (p < 0 || p >= s.People.Count || s.People.Id[p] == 0)
                    {
                        continue;
                    }
                    int id = s.People.Id[p];
                    int d = DistrictOf(s, p);
                    if (d < 0 || d >= c.Districts)
                    {
                        continue;
                    }
                    if (Mille(c.Seed, id, SaltFounder, day * 64 + k) < FlareCityCatalogue.FounderDaily)
                    {
                        int h = s.People.Household[p];
                        int interest = InterestOf(s.Households.Tastes[h], kind, FlareCityCatalogue.InterestMatch, FlareCityCatalogue.InterestOther);
                        if (Mille(c.Seed, id, SaltFounderInterest, day * 64 + k) < interest)
                        {
                            good += Impress(c, b, d, 1, 1000);
                        }
                    }
                    if (inputs.Scandal && Mille(c.Seed, id, SaltScandal, day * 64 + k) < FlareCityCatalogue.ScandalSour)
                    {
                        bad += Impress(c, b, d, -1, 1000);
                    }
                }
            }
        }
    }
}
