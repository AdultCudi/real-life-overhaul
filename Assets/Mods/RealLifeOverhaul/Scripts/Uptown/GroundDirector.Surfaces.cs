using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewGround;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's road and ground surfaces checked against each other: the load's check line with Developer diagnostics on, and
    /// the test under Options > Tests that puts the player at the two places a player reported the road flickering (7th Street
    /// at 1016 and at 1001, #general 26 Oct 2026) and writes the same check there. Two surfaces at one height over one place
    /// fight for the pixels (SurfaceRules); the check counts them from the pieces as they stand now (GroundGame.Surfaces) and
    /// names the worst. The test reads and moves the player on foot; it changes nothing else, so it runs in a Going Public
    /// session too. COST: nothing per frame; a pass over the pieces' pairs at the load with Developer diagnostics on and
    /// when the test is pressed.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string SurfacesTest = "Uptown: road surfaces at two spots";
        private const int WorstShown = 5;          // the worst overlaps named in a line

        // Where the road flickered: the player's own coordinates from the post's screenshots (x, z), and the address near it.
        private static readonly float[] SpotX = { -133.0f, -134.1f };
        private static readonly float[] SpotZ = { 722.5f, 515.2f };
        private static readonly string[] SpotName = { "1016 7th Street", "1001 7th Street" };

        private readonly List<SurfacePiece> _pieces = new List<SurfacePiece>(512);
        private readonly List<SurfaceSpot> _worst = new List<SurfaceSpot>(WorstShown + 1);
        private int _spot;                          // the next place the test goes to
        private int _pairs;                         // the pairs the last check found

        /// <summary>"Uptown: road surfaces at two spots": the player to the next place the road flickered, and the surfaces checked there and everywhere.</summary>
        public static void TestSurfaces()
        {
            try
            {
                if (Instance == null)
                {
                    TestRefusals.Tell(Logger, SurfacesTest, TestRefusal.NotLoaded);
                    return;
                }
                Instance.Surfaces();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The load's check line, once, after the art is laid (Developer diagnostics on).
        private void SurfacesAtLoad()
        {
            string line = SurfacesLine(null, -1, out bool pass);
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        private void Surfaces()
        {
            if (_build == null)
            {
                TestRefusals.TellKey(Logger, SurfacesTest, GroundText.NotLaid, _laidWhy);
                return;
            }
            if (!WalkGame.OnFoot())
            {
                TestRefusals.Tell(Logger, SurfacesTest, TestRefusal.OnFootOnly);
                return;
            }
            int spot = _spot % SpotName.Length;
            _spot++;
            WalkGame.Warp(new Vector3(SpotX[spot], UptownLots.GroundY, SpotZ[spot]));
            string line = SurfacesLine(SpotName[spot], spot, out bool pass);
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
            var data = new Dictionary<string, string>(2)
            {
                { "where", SpotName[spot] + " (" + Metres(SpotX[spot]) + ", " + Metres(SpotZ[spot]) + ")" },
                { "count", _pairs.ToString(Inv) }
            };
            Notice(pass ? GroundText.SurfacesClear : GroundText.SurfacesOver, data);
        }

        // One line: the pieces standing now, the pairs at one height over one place (all of the district, and those within reach of the place),
        // the worst of them named, and PASS when there is none anywhere.
        private string SurfacesLine(string place, int spot, out bool pass)
        {
            int copies = GroundGame.Surfaces(_pieces);
            int pairs = SurfaceRules.Overlaps(_pieces, SurfaceRules.Coplanar, _worst, WorstShown, out float area);
            int near = 0;
            if (spot >= 0)
            {
                var all = new List<SurfaceSpot>(pairs);
                SurfaceRules.Overlaps(_pieces, SurfaceRules.Coplanar, all, int.MaxValue, out _);
                for (int i = 0; i < all.Count; i++)
                {
                    near += SurfaceRules.Near(all[i].X, all[i].Z, SpotX[spot], SpotZ[spot]) ? 1 : 0;
                }
            }
            pass = pairs == 0;
            _pairs = pairs;
            var words = new System.Text.StringBuilder(512);
            words.Append("Uptown check (road surfaces").Append(place != null ? ", near " + place + " at " + Metres(SpotX[spot]) + ", " + Metres(SpotZ[spot]) : string.Empty).Append("): ")
                 .Append(_pieces.Count).Append(" pieces (").Append(copies).Append(" copied, ").Append(GroundGame.BackdropShown).Append(" of the backdrop's block grounds still shown, ")
                 .Append(GroundGame.GroundsHidden).Append(" put away at the load); pairs within ").Append((SurfaceRules.Coplanar * 1000f).ToString("0.0", Inv))
                 .Append(" mm in height over one place: ").Append(pairs).Append(" (").Append(area.ToString("0.0", Inv)).Append(" m2")
                 .Append(spot >= 0 ? ", " + near + " within " + Metres(SurfaceRules.ReachNear) + " m of the place" : string.Empty).Append(")");
            for (int i = 0; i < _worst.Count; i++)
            {
                SurfaceSpot s = _worst[i];
                words.Append(i == 0 ? "; worst: " : ", ").Append(s.A).Append(" with ").Append(s.B).Append(" at (").Append(Metres(s.X)).Append(", ").Append(Metres(s.Z)).Append("), ")
                     .Append(s.Area.ToString("0.00", Inv)).Append(" m2, ").Append((s.Gap * 1000f).ToString("0.00", Inv)).Append(" mm apart");
            }
            return words.Append(": ").Append(pass ? "PASS" : "FAIL").ToString();
        }
    }
}
