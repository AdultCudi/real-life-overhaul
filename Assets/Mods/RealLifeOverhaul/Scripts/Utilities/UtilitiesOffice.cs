using System;
using BAModAPI;
using Streets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The city's utilities office (City Light and Power and Riverside Water): one of the game's own buildings, the same
    /// one in every game on the same map, chosen as the owner asks for a city office, big and majestic: the largest
    /// office building in the city that no other feature has made its own (the courthouse: UtilitiesBridges.Taken),
    /// ties broken by address. Its frontage has one sign, a board on two posts lettered with the game's font, stood
    /// against the wall a few steps from the door where CitySpots found wall, pavement and room, as the courthouse's is;
    /// the desk is spoken to standing in front of it, or at the door if no place for the sign was found. It lights up
    /// on the city map (World/MapLinks). Looked for once per city load; nothing here runs by itself.
    /// </summary>
    public sealed class UtilitiesOffice
    {
        private const float FromWall = 0.30f;       // metres from the wall to the middle of the sign
        private const float Reach = 1.7f;           // how near the desk spot the player must be
        private const float ReachAtDoor = 2.4f;     // ...when that spot is the door front

        private static readonly float[] Aside = { 4.6f, -4.6f, 6.2f, -6.2f };   // metres along the wall from the door

        private readonly IModLogger _logger;
        private GameObject _sign;
        private bool _failed;

        public UtilitiesOffice(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True once the search is over, whatever it found.</summary>
        public bool Searched { get; private set; }

        /// <summary>True if there is a desk to speak to in person.</summary>
        public bool Usable { get; private set; }

        /// <summary>Where the player stands to speak to the desk: in front of the sign, or of the door.</summary>
        public Vector3 Front { get; private set; }

        /// <summary>The office's building; null until found (it lights up on the city map: MapPlaces).</summary>
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
                    _logger?.Warn("Utilities: no building could serve as the office; accounts are applied for by phone (the Utilities app) instead.");
                    return;
                }
                Building = picked;
                Name = picked.building.Address.ToFormattedString();
                bool placed = Place(picked);
                if (!placed && JobSpots.Front(picked, out Vector3 door))
                {
                    Front = door;
                    Usable = true;
                }
                _logger?.Info("Utilities: the office is " + Name + " (" + picked.building.totalSqm + " sqm); "
                              + (placed ? "sign up." : Usable ? "no room for the sign: the desk is at the door." : "no pavement at its door: accounts are applied for in the app."));
            }
            catch (Exception e)
            {
                _failed = true;
                Searched = true;
                Usable = false;
                _logger?.Error(e);
                _logger?.Warn("Utilities: the office could not be set up after the error above; accounts are applied for in the app.");
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
            int bestSize = int.MinValue;
            string bestName = null;
            Func<CityBuildingController, bool> taken = UtilitiesBridges.Taken;
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building == null || building.building == null || building.entranceDoors == null || building.entranceDoors.Length == 0
                    || building.building.locatedInsidePrivateFenceIndex >= 0 || building.building.SpecialService != null
                    || building.building.BuildingType != "ba:buildingtype_office" || (taken != null && taken(building)))
                {
                    continue;
                }
                int size = Mathf.Clamp(building.building.totalSqm, 0, 999999);
                string name = building.building.StreetName + "#" + building.building.StreetNumber;
                if (size > bestSize || (size == bestSize && string.CompareOrdinal(name, bestName) < 0))
                {
                    best = building;
                    bestSize = size;
                    bestName = name;
                }
            }
            return best;
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

        // One object, shared meshes and materials, no collider, no script. Read from its +Z, like the courthouse's.
        private static GameObject Build(Vector3 at, Vector3 outward)
        {
            var sign = new GameObject("RealLifeOverhaul Utilities Sign");
            Transform root = sign.transform;
            root.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
            Material post = Shapes.Paint(new Color(0.16f, 0.17f, 0.18f));
            Material board = Shapes.Paint(new Color(0.06f, 0.24f, 0.22f));
            Material rule = Shapes.Paint(new Color(0.93f, 0.80f, 0.30f));
            if (post == null || board == null || rule == null)
            {
                UnityEngine.Object.Destroy(sign);
                return null;
            }
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(-0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(-0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Board", root, Shapes.Cube, board, new Vector3(0f, 1.52f, 0f), new Vector3(1.30f, 0.62f, 0.05f));
            Shapes.Piece("Rule", root, Shapes.Cube, rule, new Vector3(0f, 1.27f, 0.004f), new Vector3(1.18f, 0.03f, 0.05f));
            Shapes.Piece("Cap", root, Shapes.Cube, rule, new Vector3(0f, 1.85f, 0f), new Vector3(1.36f, 0.04f, 0.08f));

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
                text.text = UtilitiesText.T("reallife_util_sign");
                text.color = new Color(0.95f, 0.95f, 0.90f);
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
#pragma warning disable CS0618
                text.enableWordWrapping = true;
#pragma warning restore CS0618
                text.rectTransform.sizeDelta = new Vector2(1.16f, 0.44f);
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
