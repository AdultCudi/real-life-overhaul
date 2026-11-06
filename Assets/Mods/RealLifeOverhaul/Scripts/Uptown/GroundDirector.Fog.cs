using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown under the city map's fog (GroundGame.Fog): the cut put on at the load once the map's bounds reach over the
    /// band, one line when the game's fog is not the one the cut was made from, the load's check line with Developer
    /// diagnostics on, and its test under Options > Tests, which the city expansion's test run presses too. The test reads
    /// and counts; it changes nothing, so it runs in a Going Public session too. COST: nothing per frame; the cut is read
    /// back once at the load with Developer diagnostics on, and when the test is pressed.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string FogTest = "Uptown: the city map's fog";

        private string _fogWhy;   // why the map's fog is not cut at this load, or null

        // The game's fog over the city's border cut away over the band, once the map's bounds reach over it.
        private void LayFog()
        {
            _fogWhy = GroundGame.CutFog();
            if (_fogWhy != null)
            {
                Logger?.Warn("Uptown: the city map's fog stays over the district as the game has it at this load: " + _fogWhy + ".");
            }
            if (Meta.Dev.Verbose)
            {
                int over = GroundGame.FogOver(LayoutRules.BandGround, out int vertices, out int triangles);
                Logger?.Info("Uptown check (map fog, the load): the game's fog measured " + (GroundGame.FogMeasured ?? "not at all") + "; " + FogWords(over, vertices, triangles)
                             + ": " + (FogPasses(over, vertices, triangles) ? "PASS" : "FAIL"));
            }
        }

        /// <summary>"Uptown: the city map's fog": the cut on the game's fog, and the middles of the band's ground under none of it.</summary>
        public static void TestFog()
        {
            try
            {
                if (Instance == null)
                {
                    TestRefusals.Tell(Logger, FogTest, TestRefusal.NotLoaded);
                    return;
                }
                Instance.Fog();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Fog()
        {
            if (_build == null)
            {
                TestRefusals.TellKey(Logger, FogTest, GroundText.NotLaid, _laidWhy);
                return;
            }
            List<GroundRect> band = LayoutRules.BandGround;
            int over = GroundGame.FogOver(band, out int vertices, out int triangles);
            bool pass = FogPasses(over, vertices, triangles);
            string line = "Uptown check (map fog): " + FogWords(over, vertices, triangles) + ": " + (pass ? "PASS" : "FAIL");
            if (pass)
            {
                Logger?.Info(line);
                Notice(GroundText.FogCut, new Dictionary<string, string>(1) { { "places", band.Count.ToString(Inv) } });
            }
            else
            {
                Logger?.Warn(line);
                Notice(GroundText.FogOver, null);
            }
        }

        private static bool FogPasses(int over, int vertices, int triangles)
        {
            return over == 0 && vertices == LayoutFog.VertexCount && triangles * 3 == LayoutFog.Triangles.Length;
        }

        // What the fog draws now against what the cut wants: the cut on it, and none of the band's ground's middles under it.
        private string FogWords(int over, int vertices, int triangles)
        {
            int places = LayoutRules.BandGround.Count;
            if (over < 0)
            {
                return "the game's own fog is on its object, not the cut (" + (_fogWhy ?? "the cut was given back") + "), so the band's " + places
                       + " pieces of ground lie under it (wanted the cut, " + LayoutFog.VertexCount + " vertices and " + LayoutFog.Triangles.Length / 3 + " triangles)";
            }
            return "the cut is on the game's fog (" + vertices + " vertices and " + triangles + " triangles, wanted " + LayoutFog.VertexCount + " and " + LayoutFog.Triangles.Length / 3
                   + "), the band's " + places + " pieces of ground with their middles under it: " + over + " (wanted none); the sheet reaches north to "
                   + Metres(LayoutFog.SheetNorth) + " and east to " + Metres(LayoutFog.SheetEast) + ", the cut x " + Metres(LayoutFog.CutWest) + " to " + Metres(LayoutFog.CutEast)
                   + ", z " + Metres(LayoutFog.CutSouth) + " to " + Metres(LayoutFog.CutNorth);
        }
    }
}
