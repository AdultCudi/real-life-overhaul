using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// What the route on the open city map is labelled with: where on the line it stands in the world, and the minutes left over the way still to go, as the minimap's
    /// destination block says them. <see cref="On"/> false says there is nothing to label now (no route, or the map shut).
    /// </summary>
    public struct NavRouteLabel
    {
        public bool On;
        public Vector3 At;
        public string Minutes, Distance;
        public bool OnFoot;   // the route is the walking one
    }
}
