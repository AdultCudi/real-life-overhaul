using System;
using UnityEngine;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>
    /// What the landmarks ask of the rest of the mod, each with a default so they work alone; Wiring/Wiring.LandmarkLinks
    /// fills them in: the construction game's tenders and its claims on the district's lots (Scripts/Construction), the
    /// city's construction programme for the rivals' and the city's own works (Scripts/Civic), and Standing (Fame).
    /// Each is asked on a day's pass, a page or a press, never per frame.
    /// </summary>
    public static class LandmarkBridges
    {
        // ------------------------------------------------------------------ the construction game

        /// <summary>
        /// A landmark put out to tender: its site's id, its use (the construction game's BuildUse), floor area and storeys.
        /// The tender's id, or 0 with why. Default: none (the city builds it).
        /// </summary>
        public static TenderCall OpenTender;

        public delegate int TenderCall(string site, int use, int area, int storeys, out string why);

        /// <summary>A tender's state by its id: 0 open, 1 won by the player, 2 lost to a rival, 3 gone, -1 not known. Default: not known.</summary>
        public static Func<int, int> TenderState;

        /// <summary>A landmark's open tender taken back (the tests' way back). Default: no.</summary>
        public static Func<int, bool> DropTender;

        /// <summary>A landmark's open tender awarded now (a test brings its closing forward). Default: no.</summary>
        public static Func<int, bool> AwardTender;

        /// <summary>True for a lot somebody has in the construction game: the player's land, a project, a development. Default: no.</summary>
        public static Func<string, bool> Claimed;

        /// <summary>A rival builder's name by their index, in the player's language. Default: none.</summary>
        public static Func<int, string> RivalName;

        // ------------------------------------------------------------------ the city's programme

        /// <summary>A rival's or the city's works on a landmark's plot: its middle, yaw, width and depth, storeys, contractor and hours. The site's id, or 0 with why. Default: none.</summary>
        public static PlotCall StartPlot;

        public delegate int PlotCall(Vector3 middle, int yaw, int width, int depth, int storeys, int contractor, int hours, out string why);

        /// <summary>Where a site of the programme stands (its middle); false for one not in it. Default: not known.</summary>
        public static SiteCall Site;

        public delegate bool SiteCall(int id, out float x, out float z);

        /// <summary>A site of the programme handed over now, its works done: the frame comes down and its look stands out of view. Default: no.</summary>
        public static Func<int, bool> FinishSite;

        /// <summary>A site of the programme taken away with nothing handed over (the tests' way back). Default: no.</summary>
        public static Func<int, bool> DropSite;

        // ------------------------------------------------------------------ Standing

        /// <summary>Standing moved by the press: points of recognition in the district. Default: nothing.</summary>
        public static Action<int> Standing;

        public static void Reset()
        {
            OpenTender = null;
            TenderState = null;
            DropTender = null;
            AwardTender = null;
            Claimed = null;
            RivalName = null;
            StartPlot = null;
            Site = null;
            FinishSite = null;
            DropSite = null;
            Standing = null;
        }
    }
}
