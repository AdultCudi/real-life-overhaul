using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The people screen's reads from the running game: the live population's views, the game's words, its money, its name maker and its places.</summary>
    internal static class PopulationLive
    {
        internal static PopulationSource Source() => new PopulationSource
        {
            Open = Build, Words = UiText.Words, Money = PopulationText.Money, NameOf = PopulationGame.NameOf, Place = UiText.Place
        };

        // The demand the population's director shows (null while it is settling or not running).
        private static PopDemandState Demand()
        {
            PopDemandState m = PopulationDirector.Instance != null ? PopulationDirector.Instance.DemandShown : null;
            return m != null && m.AnchorDay >= 0 ? m : null;
        }

        // A household or a person opens through PopulationViews.Open, which checks the slot still holds the one the row
        // was made for, so a slot used again after a death or a move opens as gone, never as somebody else.
        private static PopulationView Build(ViewTarget to)
        {
            PopulationState s = CityPopulation.Live;
            if (s == null)
            {
                return null;
            }
            switch (to.Kind)
            {
                case ViewKind.Demands: return Demand() != null ? PopDemandViews.Demands(Demand()) : null;
                case ViewKind.Demand: return Demand() != null ? PopDemandViews.Demand(Demand(), to.Index) : null;
                default: return PopulationViews.Open(s, CityPopulation.Pools, to);
            }
        }
    }
}
