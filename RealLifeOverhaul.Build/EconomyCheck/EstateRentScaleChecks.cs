using System;
using RealLifeOverhaul.Economy;

// The rental income option (Scripts/Economy/Estate, EstateClimate.RentScale): the market's rent scales by it within its
// bounds, a climate that never had it set is the market's own, a unit's share, what applicants offer and the rent the
// game is told all follow it, and as many applicants come at any setting: a higher one is a richer city of tenants, not
// emptier buildings. A lease keeps the rent it was agreed at.
internal static partial class Program
{
    private static EstateClimate Scaled(int scale)
    {
        EstateClimate c = Climate();
        c.RentScale = scale;
        return c;
    }

    private static void EstateRentScale()
    {
        int plain = EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Climate());
        Check(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Scaled(0)) == plain && EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Scaled(1000)) == plain && EstateClimate.Steady.RentScale == 0,
            "rental income: unset or at 100%, the market's rent is what it always was");
        Check(Math.Abs(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Scaled(2000)) - 2 * plain) <= 1 && Math.Abs(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Scaled(500)) - plain / 2) <= 1
              && Math.Abs(EstateRules.MarketRent(1000000, 1, 1000, 1000, 0, Scaled(3000)) - 3 * plain) <= 1,
            "rental income: at 50%, 200% and 300% the market's rent is half, twice and three times (" + plain + " a day at 100%)");
        Check(EstateRules.RentShare(9000) == EstateCatalogue.RentScaleMost && EstateRules.RentShare(100) == EstateCatalogue.RentScaleLeast && EstateRules.RentShare(-5) == 1000
              && EstateCatalogue.RentScaleLeast == 500 && EstateCatalogue.RentScaleMost == 3000,
            "rental income: kept between 50% and 300%, the option's own bounds");
        bool rising = true;
        int last = 0;
        for (int scale = EstateCatalogue.RentScaleLeast; scale <= EstateCatalogue.RentScaleMost; scale += 50)
        {
            int rent = EstateRules.MarketRent(750000, 0, 900, 800, 1, Scaled(scale));
            rising &= rent >= last;
            last = rent;
        }
        Check(rising, "rental income: a higher setting is never a lower rent");
        EstateClimate controlled = Scaled(2000);
        controlled.RentLever = 1;
        EstateClimate plainControlled = Climate();
        plainControlled.RentLever = 1;
        Check(Math.Abs(EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, controlled) - 2 * EstateRules.MarketRent(1000000, 0, 1000, 1000, 0, plainControlled)) <= 1
              && EstateRules.MarketRent(1000000, 1, 1000, EstateCatalogue.ConditionUnfit - 1, 0, Scaled(3000)) == 0,
            "rental income: the council's rent policy still applies on top, and an unfit building still lets for nothing");

        // A unit's share, and the applicants: the same draws at 100% and at 200% bring the same applicants, at twice the offers.
        EstateHolding a = Units(3), b = Units(3);
        EstateClimate one = Scaled(1000), two = Scaled(2000);
        Check(Math.Abs(EstateRules.UnitRent(b, two) - 2 * EstateRules.UnitRent(a, one)) <= 2, "rental income: a unit's share of the rent follows it");
        for (int day = 1; day < 40; day++)
        {
            for (int u = 0; u < a.Units.Count; u++)
            {
                EstateRules.Apply(a, u, 21, day, one);
                EstateRules.Apply(b, u, 21, day, two);
            }
        }
        bool same = a.ApplicantCount == b.ApplicantCount && a.ApplicantCount > 0, doubled = true;
        for (int u = 0; same && u < a.Units.Count; u++)
        {
            same &= a.Units[u].Applicants.Count == b.Units[u].Applicants.Count;
            for (int i = 0; same && i < a.Units[u].Applicants.Count; i++)
            {
                doubled &= Math.Abs(b.Units[u].Applicants[i].Offer - 2 * a.Units[u].Applicants[i].Offer) <= 2 && b.Units[u].Applicants[i].Record == a.Units[u].Applicants[i].Record;
            }
        }
        Check(same, "rental income: as many applicants come at 200% as at 100%, so a higher setting does not empty the buildings (" + a.ApplicantCount + " and " + b.ApplicantCount + ")");
        Check(doubled, "rental income: at 200% every applicant offers twice what the same applicant offers at 100%");

        // A lease agreed keeps its rent; the game is told what the leases pay.
        if (b.Units[0].Applicants.Count > 0)
        {
            RentApplicant first = b.Units[0].Applicants[0];
            EstateRules.Accept(b, first.Id, 40);
            int agreed = b.Units[0].Rent;

            Check(agreed == first.Offer && EstateRules.PayingRent(b) == agreed, "rental income: a lease is agreed at the offer made under the setting, and the game is told to pay exactly that");
        }
    }
}
