namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>How the weekly look found one business.</summary>
    public enum LookKind
    {
        FollowOn,    // a deal there asked for a second round: an offer from the same investor, or none if they have closed
        NotAsked,    // the business does not qualify, or has an offer out already
        NoTycoon,    // it qualifies, and no tycoon is free to come
        Rolled       // a tycoon sees it: the chance and the roll, an offer if the roll was under it
    }

    /// <summary>What the weekly look made of one business, for the director to tell and log.</summary>
    public struct LookResult
    {
        public LookKind Kind;
        public Offer Offer;          // the offer made, or null
        public Investor Investor;    // who it is from, or who was thought of (null for NotAsked)
        public int Chance, Roll;     // of a Rolled look
    }

    /// <summary>
    /// The weekly look at the player's businesses for an offer (VenturesDirector.Look), one business at a time, game-free.
    /// The look is nothing but its businesses taken one after the other, each looked at against the file as the ones before it
    /// left it and nothing else, so a look made in one go and a look made a business a frame (Core/DaySpread) leave the
    /// file, and say, the same. Every chance is a roll seeded by the save's seed, the day and the place, never a machine's.
    /// </summary>
    public static class LookRound
    {
        /// <summary>One business: its place's key, its name and its figures. Changes <paramref name="file"/> only by the offer made and a follow-on asked for.</summary>
        public static LookResult One(VenturesFile file, int day, int level, int phase, string place, string name, in BusinessFigures b)
        {
            var result = new LookResult();
            Deal due = null;
            for (int d = 0; d < file.Deals.Count; d++)
            {
                due = file.Deals[d].Place == place && file.Deals[d].FollowOn ? file.Deals[d] : due;
            }
            if (due != null)
            {
                due.FollowOn = false;
                Investor again = file.Investor(due.InvestorId);
                result.Kind = LookKind.FollowOn;
                result.Investor = again;
                result.Offer = again != null && day >= again.ClosedUntil ? OfferRules.Make(file, again, place, name, b, day, level, OfferSource.FollowOn) : null;
                result.Chance = 100;
                return result;
            }
            if (!OfferRules.Qualifies(b) || file.HasOffer(place))
            {
                result.Kind = LookKind.NotAsked;
                return result;
            }
            // A tycoon who sees people, has no offer out and no deal here; one who likes the sector first.
            Investor who = null;
            for (int v = 0; v < file.Investors.Count; v++)
            {
                Investor investor = file.Investors[v];
                if (investor.Kind == InvestorKind.Tycoon && day >= investor.ClosedUntil && !file.HasOfferFrom(investor.Id) && !file.HasDeal(place, investor.Id)
                    && (who == null || (investor.LikesSector(b.Sector) && !who.LikesSector(b.Sector))))
                {
                    who = investor;
                }
            }
            if (who == null)
            {
                result.Kind = LookKind.NoTycoon;
                return result;
            }
            result.Kind = LookKind.Rolled;
            result.Investor = who;
            result.Chance = OfferRules.Chance(who.LikesSector(b.Sector), phase);
            result.Roll = VenturesRandom.Percent(file.Seed, day, VenturesRandom.Of(place));
            result.Offer = result.Roll < result.Chance ? OfferRules.Make(file, who, place, name, b, day, level, OfferSource.Inbound) : null;
            return result;
        }
    }
}
