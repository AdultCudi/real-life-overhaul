using System;
using BAModAPI;
using Streets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's courthouse: one of the game's own buildings, the same one in every game on the
    /// same map. The game has no court, so the most civic building it does have is taken: the tax
    /// office first (the one government building), then a bank, then a school, the largest of its
    /// kind, ties broken by address; failing all of those, the largest office building.
    ///
    /// Its frontage is dressed with one sign (the modelled <c>courthouse_sign</c> when the prop
    /// bundle has it, otherwise a plain board on two posts lettered with the game's font), stood
    /// against the wall a few steps from the door where CitySpots found wall, pavement and room,
    /// the way the help-wanted boards are. Hearings are attended standing in front of it; if no
    /// place for the sign could be found the pavement in front of the door is used instead.
    /// Looked for once per city load; nothing here runs by itself.
    /// </summary>
    public sealed class Courthouse
    {
        public const string SignProp = "courthouse_sign";

        private const float FromWall = 0.30f;       // metres from the wall to the middle of the sign
        private const float Reach = 1.7f;           // how near the attend spot the player must be
        private const float ReachAtDoor = 2.4f;     // ...when that spot is the door front
        private const int WaitForProps = 25;        // ticks (seconds) to wait for the prop bundle before the plain sign is built

        private static readonly float[] Aside = { 4.6f, -4.6f, 6.2f, -6.2f };   // metres along the wall from the door (help-wanted boards stand at 2.4)

        private readonly IModLogger _logger;
        private GameObject _sign;
        private int _waited;
        private bool _failed;

        public Courthouse(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True once the search is over, whatever it found.</summary>
        public bool Searched { get; private set; }

        /// <summary>True if there is a place to attend a hearing in person.</summary>
        public bool Usable { get; private set; }

        public bool HasSign => _sign != null;

        /// <summary>Where the player stands to go in: in front of the sign, or of the door.</summary>
        public Vector3 Front { get; private set; }

        public Address Address { get; private set; }

        /// <summary>The building that is the courthouse; null until found (it lights up on the city map: MapPlaces).</summary>
        public CityBuildingController Building { get; private set; }

        /// <summary>The address as the game writes it; empty until found.</summary>
        public string Name { get; private set; } = string.Empty;

        public bool InReach(Vector3 player)
        {
            if (!Usable)
            {
                return false;
            }
            Vector3 offset = Front - player;
            float reach = _sign != null ? Reach : ReachAtDoor;
            return offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < reach * reach;
        }

        /// <summary>Once a second until <see cref="Searched"/>: finds the building, then stands the sign.</summary>
        public void Tick()
        {
            if (Searched || _failed)
            {
                return;
            }
            try
            {
                // The modelled sign is worth a short wait: the bundle loads in the background.
                if (!PropLibrary.Ready && _waited++ < WaitForProps)
                {
                    return;
                }
                CityManager city = InstanceBehavior<CityManager>.Instance;
                CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
                if (buildings == null || buildings.Length == 0)
                {
                    return;
                }
                CityBuildingController picked = Pick(buildings);
                Searched = true;
                if (picked == null)
                {
                    _logger?.Warn("Justice: no building could serve as the courthouse; hearings are held by video link.");
                    return;
                }
                Address = picked.building.Address;
                Building = picked;
                Name = Address.ToFormattedString();
                bool placed = Place(picked);
                if (!placed && JobSpots.Front(picked, out Vector3 door))
                {
                    Front = door;
                    Usable = true;
                }
                if (Usable)
                {
                    // Its own name wherever the game names the building (Core/SpecialBuildings).
                    SpecialBuildings.Set("courthouse", picked, SpecialKind.Court, "reallife_justice_door_courthouse", "reallife_justice_card_type",
                        () => JusticeSettings.Enabled && JusticeSettings.Courthouse);
                }
                _logger?.Info("Justice: the courthouse is " + Name + " (" + Kind(picked) + ", " + picked.building.totalSqm + " sqm); "
                              + (placed ? "sign up (" + (PropLibrary.TryGet(SignProp, out _, out _) ? "modelled" : "plain stand-in") + ")."
                                  : Usable ? "no room for the sign: hearings are attended at the door."
                                  : "no pavement at its door: hearings are held by video link."));
            }
            catch (Exception e)
            {
                _failed = true;
                Searched = true;
                Usable = false;
                _logger?.Error(e);
                _logger?.Warn("Justice: the courthouse could not be set up after the error above; hearings are held by video link.");
            }
        }

        public void Dispose()
        {
            if (_sign != null)
            {
                UnityEngine.Object.Destroy(_sign);
                _sign = null;
            }
            Usable = false;
        }

        // ---------------------------------------------------------------- which building

        private static CityBuildingController Pick(CityBuildingController[] buildings)
        {
            CityBuildingController best = null;
            long bestScore = long.MinValue;
            string bestName = null;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                for (int i = 0; i < buildings.Length; i++)
                {
                    CityBuildingController building = buildings[i];
                    if (building == null || building.building == null || building.entranceDoors == null || building.entranceDoors.Length == 0
                        || building.building.locatedInsidePrivateFenceIndex >= 0)
                    {
                        continue;
                    }
                    int civic = Civic(building);
                    // First pass: civic buildings only. Second (a city with none): the largest office.
                    if (pass == 0 ? civic <= 0 : building.building.BuildingType != "ba:buildingtype_office")
                    {
                        continue;
                    }
                    long score = civic * 1000000L + Mathf.Clamp(building.building.totalSqm, 0, 999999);
                    string name = building.building.StreetName + "#" + building.building.StreetNumber;
                    if (score > bestScore || (score == bestScore && string.CompareOrdinal(name, bestName) < 0))
                    {
                        best = building;
                        bestScore = score;
                        bestName = name;
                    }
                }
            }
            return best;
        }

        private static string Kind(CityBuildingController building)
        {
            return building.building.SpecialService != null ? building.building.SpecialService.businessTypeName : building.building.BuildingType;
        }

        private static int Civic(CityBuildingController building)
        {
            if (building.building.SpecialService == null)
            {
                return 0;
            }
            switch (building.building.SpecialService.businessTypeName)
            {
                case "ba:businesstype_irs": return 9;
                case "ba:businesstype_bank": return 7;
                case "ba:businesstype_school": return 5;
                default: return 0;
            }
        }

        // ---------------------------------------------------------------- the sign

        // Against the wall some steps to one side of the door, facing the pavement, where nothing else stands.
        private bool Place(CityBuildingController building)
        {
            if (!Shapes.Available || !JobSpots.Front(building, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int i = 0; i < Aside.Length; i++)
            {
                // Measured 0.75 m out from the wall with 0.75 m of room: the strip the sign stands in is inside what was found clear.
                if (!CitySpots.Shopfront(front + along * Aside[i], FromWall + 0.45f, 0.75f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                Vector3 reader = stand + outward * 0.5f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, front) < 3f
                    || !CitySpots.Stand(reader, 0.4f, out reader))
                {
                    continue;
                }
                at.y = ground;
                _sign = Build(at, outward);
                if (_sign == null)
                {
                    return false;
                }
                Front = reader;
                Usable = true;
                return true;
            }
            return false;
        }

        // One object, shared meshes and materials, no collider, no script. Read from its +Z, like the help-wanted board.
        private static GameObject Build(Vector3 at, Vector3 outward)
        {
            var sign = new GameObject("RealLifeOverhaul Courthouse Sign");
            Transform root = sign.transform;
            root.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
            if (Shapes.Prop(SignProp, root))
            {
                return sign;
            }
            Material post = Shapes.Paint(new Color(0.16f, 0.16f, 0.18f));
            Material board = Shapes.Paint(new Color(0.09f, 0.13f, 0.25f));
            Material brass = Shapes.Paint(new Color(0.78f, 0.62f, 0.26f));
            if (post == null || board == null || brass == null)
            {
                UnityEngine.Object.Destroy(sign);
                return null;
            }
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(-0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(-0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Board", root, Shapes.Cube, board, new Vector3(0f, 1.52f, 0f), new Vector3(1.30f, 0.62f, 0.05f));
            Shapes.Piece("Rule", root, Shapes.Cube, brass, new Vector3(0f, 1.27f, 0.004f), new Vector3(1.18f, 0.03f, 0.05f));
            Shapes.Piece("Cap", root, Shapes.Cube, brass, new Vector3(0f, 1.85f, 0f), new Vector3(1.36f, 0.04f, 0.08f));

            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font != null)
            {
                var label = new GameObject("Lettering");
                Transform place = label.transform;
                place.SetParent(root, false);
                // Text is read from behind its own forward: turned to face out from the board.
                place.localPosition = new Vector3(0f, 1.56f, 0.027f);
                place.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var text = label.AddComponent<TextMeshPro>();
                text.font = font;
                text.text = JusticeText.T("reallife_justice_sign");
                text.color = new Color(0.93f, 0.90f, 0.80f);
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
#pragma warning disable CS0618
                text.enableWordWrapping = false;
#pragma warning restore CS0618
                text.rectTransform.sizeDelta = new Vector2(1.16f, 0.40f);
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.05f;
                text.fontSizeMax = 3f;
                var renderer = label.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            return sign;
        }
    }
}
