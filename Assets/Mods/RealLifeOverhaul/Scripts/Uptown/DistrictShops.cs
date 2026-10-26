using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One of the mod's own kinds of shop (Scripts/Shops) as Uptown's mix pass may open one run by the city.</summary>
    public sealed class DistrictShopKind
    {
        public string Type;        // the game's business type id the kind is registered under
        public string Donor;       // the game's type it trades as: an AI business of it lends its hours, sign and layout
        public string[] Primary;   // the game's item names of its primary goods: a lent layout must show one of them
    }

    /// <summary>
    /// The mod's own kinds of shop for Uptown's mix pass, set by the wiring (Wiring/Wiring.UptownShopsLinks) from the shops'
    /// catalogue whatever the options say, so every machine has the same kinds. Unset: the pass opens the game's kinds alone.
    /// COST: nothing per frame.
    /// </summary>
    public static class DistrictShops
    {
        /// <summary>The kinds, the same list each call.</summary>
        public static Func<IReadOnlyList<DistrictShopKind>> Kinds;
    }
}
