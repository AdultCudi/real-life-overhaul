using System;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using RealLifeOverhaul.Economy;
using Streets;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The money a building's card adds to the game's box, as the game writes money; null where the game has no figure.</summary>
    internal struct BizWorth
    {
        public string Market;       // Building.GetMarketValue, the figure BizMan's building side shows
        public string Valuation;    // a rival's business: CompetitionHelper.CalculateAiOwnedValuation; the player's: what a sale of it would ask
        public bool Yours;          // the valuation is the player's own business's
    }

    /// <summary>
    /// The valuation of a business and the market value of a building, for every card of the kit that names them (the map's place card and picked place, the street's
    /// building panel and door card). The figures are the game's: the market value is Building.GetMarketValue (the mod's property economy writes into it, so one building has
    /// one value), a rival's business is valued by the game's CompetitionHelper.CalculateAiOwnedValuation where BizMan sets it, and the player's own business by the sale
    /// page's value, the only valuation of it there is. Read once for a place and again when the day turns or the place changes hands, never a frame.
    /// </summary>
    internal static class BizFacts
    {
        private static BuildingRegistration _for;
        private static int _day = -1, _state;
        private static BizWorth _worth;

        /// <summary>The figures of a building's card; empty for a place the game has none for.</summary>
        public static BizWorth Read(CityBuildingController c)
        {
            BuildingRegistration reg = c != null ? c.buildingRegistration : null;
            Building building = c != null ? c.building : null;
            if (reg == null || building == null)
            {
                return default;
            }
            int day = TimeHelper.CurrentDay;
            int state = (reg.RentedByPlayer ? 1 : 0) | (reg.AvailableForRent ? 2 : 0) | (reg.BuildingOwnedByPlayer ? 4 : 0) | (string.IsNullOrEmpty(reg.businessOwnerRivalId) ? 8 : 0);
            if (ReferenceEquals(reg, _for) && day == _day && state == _state)
            {
                return _worth;
            }
            _for = reg;
            _day = day;
            _state = state;
            _worth = Compute(reg, building);
            return _worth;
        }

        private static BizWorth Compute(BuildingRegistration reg, Building building)
        {
            var w = new BizWorth();
            try
            {
                float market = building.GetMarketValue();
                w.Market = BizFactsRules.IsFigure(market) ? UiText.Money(market) : null;
            }
            catch (Exception)
            {
                w.Market = null;   // a building the game cannot value
            }
            try
            {
                bool warehouse = building.BuildingType == "ba:buildingtype_warehouse";
                if (reg.RentedByPlayer)
                {
                    ServicesDirector sale = ServicesDirector.Instance;
                    if (!warehouse && sale != null && sale.SaleOn && ServicesGame.IsSellable(reg))
                    {
                        int value = sale.Value(reg).Value;
                        w.Valuation = value > 0 ? UiText.Money(value) : null;
                        w.Yours = true;
                    }
                }
                else if (!string.IsNullOrEmpty(reg.BusinessName) && BizFactsRules.ShowsValuation(building.SpecialService != null,
                    BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.canbetakenover), warehouse, reg.AvailableForRent, false))
                {
                    float value = CompetitionHelper.CalculateAiOwnedValuation(reg);
                    w.Valuation = BizFactsRules.IsValuation(value) ? UiText.Money(value) : null;
                }
            }
            catch (Exception)
            {
                w.Valuation = null;   // no takings on the books yet: the game has nothing to value it by
            }
            return w;
        }

        /// <summary>The valuation's label as a row: the game keeps its own only inside a sentence, so the row's is the mod's.</summary>
        public static string ValuationLabel(BizWorth w) => UiText.T(w.Yours ? "reallife_ui_biz_value_sold" : "reallife_ui_biz_valuation");

        /// <summary>The valuation as a sentence: the game's own for a rival's business, the label and the figure for the player's.</summary>
        public static string ValuationSentence(BizWorth w) => w.Valuation == null ? null
            : w.Yours ? BizFactsRules.Pair(ValuationLabel(w), w.Valuation) : UiText.Fill("bizman_estimated_valuation", "valuation", w.Valuation);

        /// <summary>The market value as a sentence: the game's label and the figure.</summary>
        public static string MarketSentence(BizWorth w) => BizFactsRules.Pair(UiText.T("bizman_market_value"), w.Market);
    }
}
