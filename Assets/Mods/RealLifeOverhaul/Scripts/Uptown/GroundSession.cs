// STAND-IN, to be taken out at the merge: Uptown's ground (feature/uptown-ground, Scripts/Uptown/GroundSession.cs) gives
// this name; this file holds it so the district's side builds on its own branch.
namespace RealLifeOverhaul.Uptown
{
    /// <summary>The ground's Going Public verdict, readable before the scene exists (stand-in: alone only, the option on).</summary>
    public static class GroundSession
    {
        public static bool Agrees(out string why)
        {
            bool alone = !GoingPublic.InSession;
            why = alone ? null : "in a Going Public session (the ground's own verdict comes with its branch)";
            return alone;
        }
    }
}
