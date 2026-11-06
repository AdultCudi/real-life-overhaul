using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's standing buildings as the look census reads them (LookCensus): each building's design and the ground it stands on,
    /// with the root its copy stands on, for the window overlays on its plain sides (SideWalls) and the census test. The district's
    /// special places copied whole, the buildings that stand with another look worn over them and any building made with a placement of
    /// its own are left out: their look is their own. COST: nothing per frame; a pass over the buildings at a load and in a test.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>The standing buildings as census pieces, with the transform each stands on, into the caller's lists (not cleared).</summary>
        public static void BuildingsInto(IList<MadeBuilding> standing, List<LookPiece> pieces, List<Transform> roots)
        {
            IList<DesignEntry> designs = CopyGame.Catalogue;
            for (int i = 0; standing != null && i < standing.Count; i++)
            {
                MadeBuilding made = standing[i];
                if (made == null || made.Controller == null || made.Spec == null || made.Spec.Whole || made.Spec.Lots == null || made.Lot < 0 || made.Lot >= made.Spec.Lots.Length
                    || made.DesignIndex < 0 || made.DesignIndex >= designs.Count)
                {
                    continue;
                }
                MadeLot ground = made.Spec.Lots[made.Lot];
                if (ground.Inset != 0f || ground.Side != 0 || CopyGame.LookHidden(made.Controller) || CopyGame.Wears(made.Controller))
                {
                    continue;
                }
                pieces.Add(new LookPiece { Id = made.Spec.Id, Design = designs[made.DesignIndex], Ground = ground });
                roots.Add(made.Controller.transform);
            }
        }
    }
}
