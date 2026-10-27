using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.NewGround;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The districts' edges (<see cref="EdgeRules"/>, GroundGame.Edges): the boxes that close every street end and the fog behind every
    /// stretch of edge laid at the load, behind the loading screen, once the walls stand; the fencing, signs and cones once the props
    /// bundle has loaded (the game's late call, or when it has); the load's check line with Developer diagnostics on; and a test under
    /// Options > Tests, which the city expansion's run presses too. The test reads and counts; it changes nothing, so it runs in a
    /// Going Public session too. COST: nothing per frame; the check reads the lanes and the ground's boxes once at the load with Developer
    /// diagnostics on, and when the test is pressed.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string EdgesTest = "Uptown: the districts' edges";

        private readonly List<EdgeSeg> _edges = new List<EdgeSeg>(8);
        private readonly List<Barricade> _ends = new List<Barricade>(32);
        private string _edgesWhy;      // why the fog does not stand at this load, or null
        private string _piecesWhy;     // why the pieces do not stand, or null

        // The boxes and the fog laid behind the loading screen, once the walls stand.
        private void LayEdges()
        {
            EdgeRules.Edges(_edges);
            EdgeRules.Ends(_ends);
            _edgesWhy = GroundGame.LayEdges(_ends, EdgeRules.Fog(_edges));
            if (_edgesWhy != null)
            {
                Logger?.Warn("Uptown: the fog behind the districts' edges does not stand at this load: " + _edgesWhy + ". The street ends are closed all the same.");
            }
            ScenePrewarm.AddLate("uptown: the barricades' fencing, signs and cones, once the props are loaded", LatePieces);
        }

        // The pieces, in the game's late call or as soon after as the bundle has loaded; the load's check line with them.
        private bool LatePieces()
        {
            if (_build == null)
            {
                return true;
            }
            if (!PropLibrary.Settled)
            {
                return false;
            }
            _piecesWhy = GroundGame.LayPieces(_ends);
            if (_piecesWhy != null)
            {
                Logger?.Warn("Uptown: the street ends' fencing, signs and cones do not stand at this load: " + _piecesWhy + ". The boxes close the ends all the same.");
            }
            Logger?.Info("Uptown: the street ends are closed with " + GroundGame.EdgeWalls + " boxes and " + GroundGame.EdgePieces + " pieces of fencing, signs and cones, the edges' fog is "
                         + GroundGame.EdgeFogCurtains + " layers and a lid in " + GroundGame.EdgeFogVertices + " vertices and one draw call.");
            if (Meta.Dev.Verbose)
            {
                string line = EdgesWords(out bool pass, out string detail);
                Logger?.Info("Uptown check (edges): " + line + ": " + (pass ? "PASS" : "FAIL"));
                Logger?.Info("Uptown check (edges), measured: " + detail);
            }
            return true;
        }

        /// <summary>"Uptown: the districts' edges": every street end closed and every stretch of edge behind fog, read back from what stands.</summary>
        public static void TestEdges()
        {
            try
            {
                if (Instance == null)
                {
                    TestRefusals.Tell(Logger, EdgesTest, TestRefusal.NotLoaded);
                    return;
                }
                Instance.Edges();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Edges()
        {
            if (_build == null)
            {
                TestRefusals.TellKey(Logger, EdgesTest, GroundText.NotLaid, _laidWhy);
                return;
            }
            string line = EdgesWords(out bool pass, out string detail);
            if (pass)
            {
                Logger?.Info("Uptown check (edges): " + line + ": PASS");
                Logger?.Info("Uptown check (edges), measured: " + detail);
                Notice(GroundText.EdgesWhole, new Dictionary<string, string>(2) { { "ends", GroundGame.EdgeWalls.ToString(Inv) }, { "segments", GroundGame.EdgeFogSegments.ToString(Inv) } });
            }
            else
            {
                Logger?.Warn("Uptown check (edges): " + line + ": FAIL");
                Logger?.Warn("Uptown check (edges), measured: " + detail);
                Notice(GroundText.EdgesOpen, null);
            }
        }

        // What stands, in the load's line: the street ends closed and the stretches of edge behind fog. detail says what each was measured
        // against: the boxes and pieces of every street end, the fog's layers and mesh, and no lane or walking ground across a barricade.
        private string EdgesWords(out bool pass, out string detail)
        {
            var arms = new List<OpenArm>(32);
            EdgeRules.OpenArms(LayoutPlans.Streets(), _edges, arms);
            EdgeFogData fog = EdgeRules.Fog(_edges);
            int wantedPieces = EdgeRules.PieceCount(_ends);
            string crossed = Crossed();
            bool ends = GroundGame.EdgeWalls == _ends.Count && arms.Count == _ends.Count && _ends.Count > 0;
            bool pieces = GroundGame.EdgePieces == wantedPieces && _piecesWhy == null;
            bool fogged = GroundGame.EdgeFogSegments == _edges.Count && GroundGame.EdgeFogCurtains == fog.Curtains && GroundGame.EdgeFogVertices == fog.Vertices
                          && GroundGame.EdgeFogTriangles * 3 == fog.Triangles.Count && _edgesWhy == null;
            pass = ends && pieces && fogged && crossed == null;
            var words = new StringBuilder(320);
            words.Append(_ends.Count.ToString(Inv)).Append(" street ends wanted, ").Append(arms.Count.ToString(Inv)).Append(" left open into an edge by the lanes, ");
            words.Append(GroundGame.EdgeWalls.ToString(Inv)).Append(" boxes stand; ").Append(GroundGame.EdgePieces.ToString(Inv)).Append(" of ").Append(wantedPieces.ToString(Inv));
            words.Append(" pieces").Append(_piecesWhy != null ? " (" + _piecesWhy + ")" : string.Empty).Append("; ").Append(_edges.Count.ToString(Inv));
            words.Append(" fog segments wanted in ").Append(fog.Curtains.ToString(Inv)).Append(" layers and ").Append(fog.Vertices.ToString(Inv)).Append(" vertices, ");
            words.Append(GroundGame.EdgeFogCurtains.ToString(Inv)).Append(" layers and ").Append(GroundGame.EdgeFogVertices.ToString(Inv)).Append(" vertices stand");
            words.Append(_edgesWhy != null ? " (no fog: " + _edgesWhy + ")" : string.Empty).Append("; ").Append(crossed ?? "no lane and no walking ground crosses a barricade");
            detail = words.ToString();
            return GroundGame.EdgeWalls.ToString(Inv) + " street ends closed, " + GroundGame.EdgeFogSegments.ToString(Inv) + " fog segments";
        }

        // The first lane, walking ground, zone or terminus across a barricade's box, in words, or null when none is.
        private string Crossed()
        {
            RoadPlan lanes = LayoutPlans.Streets();
            for (int e = 0; e < _ends.Count; e++)
            {
                for (int r = 0; r < lanes.Roads.Count; r++)
                {
                    if (EdgeRules.LaneTouches(lanes.Roads[r], _ends[e]))
                    {
                        return "a lane (road " + r + ") runs into the barricade at " + _ends[e].Name;
                    }
                }
            }
            GroundPlan[] walks = { LayoutPlans.PlayerGround(), LayoutPlans.NpcGround() };
            for (int w = 0; w < walks.Length; w++)
            {
                for (int b = 0; b < walks[w].Boxes.Count; b++)
                {
                    GroundBox box = walks[w].Boxes[b];
                    for (int e = 0; e < _ends.Count; e++)
                    {
                        if (EdgeRules.Overlap(_ends[e].Wall, box.X, box.Z, box.HalfX, box.HalfZ))
                        {
                            return "walking ground (" + walks[w].Name + ") lies across the barricade at " + _ends[e].Name;
                        }
                    }
                }
            }
            return null;
        }
    }
}
