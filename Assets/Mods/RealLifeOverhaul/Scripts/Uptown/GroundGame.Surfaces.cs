using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's road and ground surfaces as they stand now, for the surfaces check (SurfaceRules): each copied module
    /// where its object stands (a street, avenue or crossing by its middle; a block's ground by its drawn box), the game's
    /// own pieces at 1st Avenue as the game has them, and every block ground of the backdrop that is still shown over the band.
    /// A backdrop's block ground lay off the grid at the height of the pavements, so it fought the pavement it reached over
    /// (a player's #general post, 26 Oct 2026); each is now put away under the ground laid over its block (CopyArt), and the
    /// check says so when one is not. COST: a pass over the copies at the load with Developer diagnostics on and when the
    /// test is pressed; nothing per frame.
    /// </summary>
    public static partial class GroundGame
    {
        /// <summary>The backdrop's block grounds still shown over the band at this moment (each a flat piece in the list).</summary>
        public static int BackdropShown { get; private set; }

        /// <summary>
        /// Fills <paramref name="into"/> with the surfaces standing now: the copies' pieces, the game's own at 1st Avenue, and the
        /// backdrop's block grounds still shown whose middle is on the band. Returns the copies counted.
        /// </summary>
        public static int Surfaces(List<SurfacePiece> into)
        {
            into.Clear();
            BackdropShown = 0;
            int copies = 0;
            for (int i = 0; i < Copies.Count; i++)
            {
                GameObject copy = Copies[i];
                if (copy == null || !copy.activeInHierarchy)
                {
                    continue;
                }
                ModulePlace place = Places[CopyPlace[i]];
                Vector3 at = copy.transform.position;
                if (place.Kind == ModuleKind.Ground)
                {
                    Renderer drawn = copy.GetComponent<Renderer>();
                    if (drawn == null)
                    {
                        continue;
                    }
                    Bounds b = drawn.bounds;   // a flat piece: its top is its box's top
                    into.Add(SurfaceRules.Flat(SurfaceRules.Name(place), b.min.x, b.min.z, b.max.x, b.max.z, b.max.y));
                }
                else
                {
                    into.Add(SurfaceRules.Place(place.Kind, SurfaceRules.Name(place), at.x, at.y, at.z));
                }
                copies++;
            }
            SurfaceRules.GamePieces(into);
            for (int i = 0; i < KeptPlanes.Count; i++)
            {
                Renderer plane = KeptPlanes[i];
                if (plane == null || !plane.gameObject.activeInHierarchy || !plane.name.StartsWith(GroundPlane, System.StringComparison.Ordinal))
                {
                    continue;
                }
                Bounds b = plane.bounds;
                if (!LayoutRules.OnGround(b.center.x, b.center.z))
                {
                    continue;
                }
                into.Add(SurfaceRules.Flat("the backdrop's " + plane.name, b.min.x, b.min.z, b.max.x, b.max.z, b.max.y));
                BackdropShown++;
            }
            return copies;
        }
    }
}
