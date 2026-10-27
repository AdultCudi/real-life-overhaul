using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The Game file of Uptown's street furniture, the only one of the streets' files that touches the scene: the game's own
    /// pavement bench, litter bin and three street trees in their holes from Manhattan's pavements (no bike-share dock: the
    /// one Industry City's scene gives drew as a bare black slab on the campus lawn, 26 Oct 2026, and the mod's own docks are the
    /// garage's), each found once a load (the piece of its name nearest a fixed place of
    /// the game's scene, so every machine copies the same, switched off or on: GamePieces), copied at the places of
    /// StreetsRules.Furniture under the art's holder, so they go with the art when the city goes or the district stands down.
    /// They are laid behind the loading screen before the walking ground grows, so both meshes are grown round them as round
    /// the copied modules' lamps; a place where something already stands (a module's lamp or litter, a pole, an old
    /// scaffold) or where a car crosses into a big special place is left empty. The bench is the game's
    /// OutsideBenchController whole, so the game's own rest seats the player on it and its pedestrians sit there as on
    /// Manhattan's. COST: nothing after the load.
    /// </summary>
    public static class StreetsGame
    {
        public const string HolderName = "RealLifeOverhaul Uptown street furniture";   // under the art's holder: the ground's count of the game's colliders leaves it out

        private const float Reach = 40f;       // m from its fixed place the template must be: farther, and the game's scene has moved

        // Build 3682's pieces on Manhattan's pavements (the navlab's scene scan, props_scan of 25 Oct 2026). Room is the room a
        // piece needs, half each way, turned with it: nothing of the game's may stand there.
        private static readonly PieceTemplate[] Templates =
        {
            new PieceTemplate
            {
                Root = GamePieces.BenchRoot, Name = GamePieces.BenchName, X = GamePieces.BenchX, Z = GamePieces.BenchZ, Room = new Vector3(0.95f, 0.6f, 0.4f), Y = 0.03f, Within = Reach,
                AltRoot = GamePieces.BenchAltRoot, AltX = GamePieces.BenchAltX, AltZ = GamePieces.BenchAltZ, Sittable = true
            },
            new PieceTemplate { Root = "Trash Cans", Name = "SM_Trash Can", X = -87.00f, Z = 453.90f, Room = new Vector3(0.35f, 0.6f, 0.35f), Y = 0.06f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "SM_PavementHoleforTrees_Broadleaf M1", X = 148.67f, Z = 475.57f, Room = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "SM_PavementHoleforTrees_02 Birch", X = 346.53f, Z = 475.57f, Room = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f, Within = Reach },
            new PieceTemplate { Root = "Vegetation", Name = "SM_PavementHoleforTrees_02 Broadleaf L1", X = 278.95f, Z = 328.38f, Room = new Vector3(0.7f, 0.9f, 0.7f), Y = 0.05f, Within = Reach }
        };

        private const float ClearFrom = 0.15f;     // m above the pavement the room is looked at from: over its kerb, under nothing

        private static readonly GameObject[] Found = new GameObject[5];
        private static readonly List<StreetPlace> Plan = new List<StreetPlace>(1024);
        private static readonly List<TransitPlan.Route> Routes = new List<TransitPlan.Route>(4);
        private static int[,] _laid = new int[StreetsRules.FirstAvenue + 1, StreetsRules.Kinds];
        private static int _mask;

        /// <summary>The pieces laid at this load, of the plan's; those left as something stood there, off a big place's mouth, off a stop.</summary>
        public static int Laid { get; private set; }
        public static int Planned => Plan.Count;
        public static int Occupied { get; private set; }
        public static int Mouths { get; private set; }
        public static int OffStops { get; private set; }

        /// <summary>The pieces taken away since they were laid, from the mouths of garage doors found once the buildings stood.</summary>
        public static int GateAway { get; private set; }

        private const float PieceReach = 0.6f;   // m a piece of furniture reaches round its middle: a bench is 1.9 m long, a bin or a tree pit half a metre

        /// <summary>The kinds whose template was not found at this load, in a word each, or null.</summary>
        public static string Missing { get; private set; }

        /// <summary>What was found and what was not at this load, for Player.log: "found: a, b; not found: c".</summary>
        public static string FoundWords { get; private set; }

        /// <summary>The pieces of a kind laid on a street's pavements (StreetsRules.Counts' order).</summary>
        public static int Count(int street, StreetPiece kind)
        {
            return street >= 0 && street < _laid.GetLength(0) ? _laid[street, (int)kind] : 0;
        }

        /// <summary>
        /// Lays the band's furniture under <paramref name="art"/>, the copied modules' holder: the templates found, every
        /// place of the plan checked for room and copied, made asleep and woken together. Returns the pieces laid.
        /// </summary>
        public static int Furnish(Transform art)
        {
            Forget();
            if (art == null)
            {
                Missing = "the art's holder";
                return 0;
            }
            Missing = GamePieces.Find(Templates, Found);
            FoundWords = GamePieces.Words(Templates, Found);
            StreetsRules.Routes(Routes);
            OffStops = StreetsRules.Furniture(Routes, Plan);
            _mask = LayerMask.GetMask("Default", "Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined");
            var holder = new GameObject(HolderName);
            holder.SetActive(false);
            holder.transform.SetParent(art, false);
            Physics.SyncTransforms();   // the modules just woken stand where the room is looked for
            for (int i = 0; i < Plan.Count; i++)
            {
                StreetPlace place = Plan[i];
                int t = TemplateOf(place);
                GameObject template = Found[t];
                if (template == null)
                {
                    continue;
                }
                if (GroundGame.InMouth(place.X, place.Z))
                {
                    Mouths++;
                    continue;
                }
                Quaternion turn = Quaternion.Euler(0f, place.Yaw, 0f);
                float y = UptownLots.GroundY + Templates[t].Y;
                Vector3 half = Templates[t].Room;
                if (Physics.CheckBox(new Vector3(place.X, y + ClearFrom + half.y, place.Z), half, turn, _mask, QueryTriggerInteraction.Ignore))
                {
                    Occupied++;
                    continue;
                }
                GamePieces.Stand(template, holder.transform, HolderName + " " + place.Kind + " " + i, new Vector3(place.X, y, place.Z), place.Yaw);
                _laid[place.Street, (int)place.Kind]++;
                Laid++;
            }
            holder.SetActive(true);
            return Laid;
        }

        /// <summary>
        /// The pieces of furniture standing in the mouths of garage doors (MouthRules, found once the district's buildings stand) taken
        /// away, so a car or a truck turns in from the road; how many. The furniture is laid before the buildings stand, so these were
        /// laid where a door was yet to be.
        /// </summary>
        public static int Open(IList<GroundRect> mouths)
        {
            Transform art = GroundGame.ArtRoot;
            Transform holder = art != null ? art.Find(HolderName) : null;
            int away = 0;
            for (int i = holder != null ? holder.childCount - 1 : -1; i >= 0; i--)
            {
                Transform piece = holder.GetChild(i);
                if (piece.gameObject.activeSelf && MouthRules.Holds(mouths, piece.position.x, piece.position.z, PieceReach))
                {
                    piece.gameObject.SetActive(false);
                    Object.Destroy(piece.gameObject);
                    away++;
                }
            }
            Laid -= away;
            GateAway += away;
            return away;
        }

        /// <summary>What was laid is forgotten (its pieces go with the art's holder): a new load begins.</summary>
        public static void Forget()
        {
            Laid = 0;
            Occupied = 0;
            Mouths = 0;
            GateAway = 0;
            OffStops = 0;
            Missing = null;
            FoundWords = null;
            Plan.Clear();
            _laid = new int[StreetsRules.FirstAvenue + 1, StreetsRules.Kinds];
            for (int i = 0; i < Found.Length; i++)
            {
                Found[i] = null;
            }
        }

        private static int TemplateOf(StreetPlace place)
        {
            switch (place.Kind)
            {
                case StreetPiece.Bench:
                    return 0;
                case StreetPiece.Bin:
                    return 1;
                default:
                    return 2 + place.Variant % StreetsRules.TreeKinds;
            }
        }
    }
}
