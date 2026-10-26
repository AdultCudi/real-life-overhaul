using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Helpers;

namespace RealLifeOverhaul.Interiors
{
    public sealed partial class InteriorDirector
    {
        /// <summary>
        /// The districts' standards, each business's interior effects and the catalogue's classification. Read only
        /// during a capture, nothing per frame.
        /// </summary>
        public CompanionModule CompanionInteriors()
        {
            if (_broken)
            {
                return new CompanionModule("failed");
            }
            if (!Running)
            {
                return new CompanionModule("unavailable");
            }
            InteriorParts parts = Parts;
            var districts = new List<object>();
            foreach (NeighborhoodData district in NeighborhoodHelper.NeighborhoodsData)
            {
                if (district == null || string.IsNullOrEmpty(district.neighbourhood))
                {
                    continue;
                }
                int income = InteriorGame.IncomeOf(district.neighbourhood);
                districts.Add(CompanionSnapshot.Row("districtId", district.neighbourhood,
                    "income", income, "nativeMinimum", district.minimumInteriorScore,
                    "requiredScore", InteriorRules.Standard(income, district.minimumInteriorScore)));
            }
            var sites = new List<object>();
            var ordered = new List<KeyValuePair<BuildingRegistration, InteriorShop>>(_byReg);
            ordered.Sort((left, right) => CompanionValues.CompareAddress(left.Key.StreetName, left.Key.StreetNumber,
                right.Key.StreetName, right.Key.StreetNumber));
            foreach (KeyValuePair<BuildingRegistration, InteriorShop> pair in ordered)
            {
                InteriorShop shop = pair.Value;
                sites.Add(CompanionSnapshot.Row("address", CompanionValues.Address(pair.Key.StreetName, pair.Key.StreetNumber),
                    "overall", shop.Overall, "decor", shop.Decor, "interior", shop.InteriorNow,
                    "requiredScore", shop.Standard, "footfallPercent", shop.Footfall,
                    "priceToleranceThousandths", shop.Bears, "basketThousandths", shop.Basket,
                    "rampThousandths", shop.Ramp, "standardFromDay", shop.StandardFrom,
                    "officeBoostThousandths", shop.OfficeBoost, "fitting", shop.Fitting, "saleStop", shop.Stop.ToString().ToLowerInvariant()));
            }
            return new CompanionModule("ready", CompanionSnapshot.Row("model", "rlo-interiors-v1",
                "districts", districts, "sites", sites, "items", InteriorGame.CompanionItems(),
                "terms", InteriorCompanion.Terms(parts),
                "hypotheticalDecorCoverage", "catalogue-items"));
        }
    }
}
