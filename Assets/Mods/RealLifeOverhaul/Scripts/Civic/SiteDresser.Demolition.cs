using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A demolition site (an Uptown building knocked down by a builder of the construction game's): the hoarding along the
    /// door's wall, the game's own scaffolding up every street side and wrapped to the roof for the strip-out, the building
    /// coming down behind the wrap (its look hidden by the construction game, which knocks it down at the handover), then the
    /// wrap gone and the rubble heaped behind the hoarding on the cleared ground, the skip at the kerb throughout. The game has
    /// no excavator or bulldozer, and the props bundle is nearly full: the machines are the game's scaffolding and skip and the
    /// crew's own tools, and nothing new is drawn.
    /// </summary>
    public sealed partial class SiteDresser
    {
        private const float RubbleBack = 4f;    // metres behind the hoarding the first row of heaps stands, on the building's ground
        private const float RubbleStep = 3.2f;  // metres between the rows of heaps once the building is down

        private void BuildDemolition(Dressed dressed)
        {
            int phase = dressed.Phase;
            Vector3 foot = dressed.Kerb, outward = dressed.Out, along = dressed.Along;
            for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay++)
            {
                Put(dressed, bay == 0 ? "site_hoarding_notice" : "site_hoarding", foot + along * (bay * BayWidth) + outward * WallGap, outward);
            }
            if (dressed.HasSkip && phase >= 1)
            {
                PutOnRoad(dressed, "skip_bin", dressed.SkipAt, Vector3.Cross(dressed.SkipAlong, Vector3.up));
            }
            SiteWrap wrap = dressed.Wrap;
            bool wrapped = wrap != null && wrap.Plan.Pieces.Count > 0;
            if (wrapped)
            {
                WrapRules.DemolitionStage(phase, wrap.Plan.Lifts, out int lifts, out bool fabric);
                wrap.Want(lifts, fabric);
                Framed = true;
            }
            if (phase >= 3)
            {
                // The rubble where the building stood: a row of heaps behind the hoarding as it comes down, a second once it is down.
                int rows = phase >= 4 ? 2 : 1;
                for (int row = 0; row < rows; row++)
                {
                    for (int bay = dressed.BayMin; bay <= dressed.BayMax; bay += 2)
                    {
                        Put(dressed, "spoil_heap", foot + along * (bay * BayWidth) - outward * (RubbleBack + row * RubbleStep), outward);
                    }
                }
            }

            // People: on the first deck while the scaffold stands, in front of the hoarding with the plans, and digging the
            // last of the rubble once the building is down.
            Vector3 front = foot + outward * 1.6f;
            Vector3 left = foot - along * 3f, right = foot + along * 3f;
            bool deck = wrapped && phase >= 1 && phase <= 3;
            switch (phase)
            {
                case 0:
                    Plan(dressed, foot + along * (dressed.BayMin * BayWidth) + outward * 0.8f, foot + along * (dressed.BayMin * BayWidth), CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                case 1:
                case 2:
                    OnDeck(dressed, deck, left, "kneel_work", "arms_crossed", CityLook.RoadWorker);
                    Plan(dressed, front, foot, CityLook.RoadWorker, phase == 1 ? "clipboard_write" : "arms_crossed", null);
                    break;
                case 3:
                    OnDeck(dressed, deck, right, "kneel_work", null, CityLook.RoadWorker);
                    Plan(dressed, front, foot, CityLook.RoadWorker, "clipboard_write", null);
                    break;
                default:
                    Plan(dressed, front, foot, CityLook.RoadWorker, "shovel_dig", "shovel_rest");
                    Plan(dressed, front + along * 1.4f, foot + along * 1.4f, CityLook.RoadWorker, "kneel_work", "arms_crossed");
                    break;
            }
        }
    }
}
