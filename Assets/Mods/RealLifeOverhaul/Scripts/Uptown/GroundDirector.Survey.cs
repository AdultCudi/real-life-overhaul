using Helpers;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// "What is this ground?" (Options > Tests), the owner's "I can't find what area that is" (25 Oct 2026): where the player
    /// stands, its district, its nearest street on each side, the district's block and lot, and what the ground is (a lot built
    /// or open, a landmark's site, a big place's, Birchwood School's campus, a square the plan kept as a park, the ground
    /// between buildings, a street, the scenery past the walls or the game's own city), in Player.log and a notice. The plan's
    /// answer is GroundSurvey's; what stands there now is read here. It reads and counts and changes nothing, so it runs in a
    /// Going Public session too.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string SurveyTest = "Uptown: what is this ground";
        private const string CampusId = "schools_birchwood";   // Birchwood School's main school among the made buildings (Scripts/Schools)

        /// <summary>"What is this ground?": the spot where the player stands, read against the district's plan and the game's city.</summary>
        public static void TestWhatGround()
        {
            if (!ModState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, SurveyTest, TestRefusal.NotLoaded);
                return;
            }
            Vector3 at = PlayerHelper.GetCityPosition();
            GroundAnswer a = GroundSurvey.At(at.x, at.z);
            string what = What(a, out string state);
            string district = a.Kind == GroundKind.GameGround ? GameDistrict(at) : (a.Kind == GroundKind.PastWalls ? null : DistrictValues.Id.GetLocalization());
            if (a.Kind == GroundKind.GameGround)
            {
                GameStreets(at, out a.West, out a.East, out a.South, out a.North);
            }
            string line = "Ground check (what is this ground): at (" + at.x.ToString("F1", Inv) + ", " + at.z.ToString("F1", Inv) + ") " + a.Kind
                          + (a.Block > 0 ? ", block " + a.Block + (a.First ? " of the first block" : string.Empty) : string.Empty)
                          + (a.Block > 0 && a.Lot == null && state != null ? " (" + state + ")" : string.Empty)
                          + (a.Lot != null ? ", lot " + a.Lot + " at " + a.LotNumber + " " + a.LotStreet + (state != null ? " (" + state + ")" : string.Empty) : string.Empty)
                          + (a.On != null ? ", on " + a.On : string.Empty) + "; west " + (a.West ?? "none") + ", east " + (a.East ?? "none") + ", south " + (a.South ?? "none")
                          + ", north " + (a.North ?? "none") + "; district " + (district ?? "none") + ": PASS";
            Logger?.Info(line);
            Notifications.Show(NotificationType.Info, GroundText.WhatNotice, new System.Collections.Generic.Dictionary<string, string>(6)
            {
                { "what", what }, { "district", district ?? GroundText.WhatNone.GetLocalization() }, { "west", Name(a.West) }, { "east", Name(a.East) },
                { "south", Name(a.South) }, { "north", Name(a.North) }
            }, 14f);
        }

        // What the ground is, in the player's language; what stands on a lot now, for the log.
        private static string What(GroundAnswer a, out string state)
        {
            state = null;
            string block = a.Block.ToString(Inv);
            switch (a.Kind)
            {
                case GroundKind.PastWalls: return GroundText.WhatPastWalls.GetLocalization();
                case GroundKind.Street:
                case GroundKind.Avenue:
                case GroundKind.Crossing: return Fill(GroundText.WhatStreet, "street", Name(a.On));
                case GroundKind.Ring: return GroundText.WhatRing.GetLocalization();
                case GroundKind.Campus:
                    state = MadeBuildings.Get(CampusId) != null ? "the campus stands" : "the campus does not stand at this load";
                    return Fill(GroundText.WhatCampus, "block", block);
                case GroundKind.ParkSquare:
                    state = ParksGame.Stands(a.Block) ? "the park stands" : "the park does not stand at this load";
                    return Fill(GroundText.WhatPark, "block", block);
                case GroundKind.Between: return Fill(GroundText.WhatBetween, "block", block);
                case GroundKind.BigPlace: return Fill(GroundText.WhatBigPlace, "block", block, "address", Address(a));
                case GroundKind.LandmarkSite:
                    state = a.Lot != null && DistrictBuildings.Occupied(a.Lot) ? "built" : "held";
                    return Fill(GroundText.WhatLandmark, "block", block);
                case GroundKind.OpenRun:
                case GroundKind.Lot:
                    bool built = a.Lot != null && DistrictBuildings.Occupied(a.Lot);
                    state = built ? "built" : a.Lot != null && DistrictBuildings.Taken(a.Lot) ? "taken" : "open";
                    string key = built ? GroundText.WhatLotBuilt : a.Kind == GroundKind.OpenRun ? GroundText.WhatOpenRun : GroundText.WhatLotOpen;
                    return Fill(key, "block", block, "address", Address(a));
                default: return GroundText.WhatGame.GetLocalization();
            }
        }

        private static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        private static string Address(GroundAnswer a) => a.LotNumber.ToString(Inv) + " " + Name(a.LotStreet != null ? StreetKey(a.LotStreet) : null);

        // A street's name: Uptown's own by its key, the game's by its "ba:street_" key; "none" for none.
        private static string Name(string key) => string.IsNullOrEmpty(key) ? GroundText.WhatNone.GetLocalization() : key.GetLocalization();

        private static string StreetKey(string streetId)
        {
            LayoutStreet s = LayoutStreets.Find(streetId);
            return s != null ? s.NameKey : streetId;
        }

        // The game's own city: the district of the nearest of its buildings.
        private static string GameDistrict(Vector3 at)
        {
            CityBuildingController best = Nearest(at, 0f, 0f, out _);
            string district = best != null && best.building != null ? best.building.Neighbourhood : null;
            return string.IsNullOrEmpty(district) ? null : district.GetLocalization();
        }

        // The game's own city: the street the nearest building on each side fronts.
        private static void GameStreets(Vector3 at, out string west, out string east, out string south, out string north)
        {
            west = StreetOf(Nearest(at, -1f, 0f, out _));
            east = StreetOf(Nearest(at, 1f, 0f, out _));
            south = StreetOf(Nearest(at, 0f, -1f, out _));
            north = StreetOf(Nearest(at, 0f, 1f, out _));
        }

        private static string StreetOf(CityBuildingController b) => b != null && b.building != null ? b.building.StreetName : null;

        // The nearest of the city's buildings, only those that way (a unit direction, or none for any way): a pass over the
        // city's buildings, on a test's press.
        private static CityBuildingController Nearest(Vector3 at, float wx, float wz, out float metres)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            CityBuildingController best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (b == null || b.building == null)
                {
                    continue;
                }
                Vector3 d = b.transform.position - at;
                if ((wx != 0f || wz != 0f) && d.x * wx + d.z * wz < Mathf.Abs(d.x * wz - d.z * wx))
                {
                    continue;   // not that way: within the quarter round its direction
                }
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = b;
                }
            }
            metres = best != null ? Mathf.Sqrt(bestSqr) : 0f;
            return best;
        }
    }
}
