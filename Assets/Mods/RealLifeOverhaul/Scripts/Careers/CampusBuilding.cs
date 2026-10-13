using System;
using BAModAPI;
using Buildings;
using Streets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Hudson City University: one of the game's own buildings, the same one in every game on the same map. The game has no
    /// university, so the largest office building of the city that is no special service and not the player's is taken,
    /// ties broken by address, leaving out every building another feature has made its own (Core/SpecialBuildings: the
    /// City Utilities office, the courthouse), which are chosen first. Nothing of it is kept in a save: an enrolment is on
    /// "the university", and its lectures, the Planner and the Campus app read where it is now. Once chosen it carries its
    /// own name wherever the game names the building, and is on the city map with the other places to study (World/MapLinks). Its frontage gets one sign, a board on two posts lettered with the game's font, stood against
    /// the wall a few steps from the door where there is wall, pavement and room, the way the courthouse's is; lectures are
    /// attended standing at it (or inside the building, or at the door if no place for the sign was found). Looked for once
    /// per city load; nothing here runs by itself.
    /// </summary>
    public sealed class CampusBuilding
    {
        private const string Office = "ba:buildingtype_office";
        private const float FromWall = 0.30f;
        private const float Reach = 2.2f;
        private const float ReachAtDoor = 3f;
        private const int WaitForOthers = 30;   // seconds to wait for the utilities office and the courthouse to choose theirs
        private static readonly float[] Aside = { -4.6f, 4.6f, -6.2f, 6.2f };   // (the courthouse tries the other side first)

        private readonly IModLogger _logger;
        private GameObject _sign;
        private bool _failed;
        private int _waited;
        private int _look;

        public CampusBuilding(IModLogger logger)
        {
            _logger = logger;
        }

        public bool Searched { get; private set; }
        public bool Usable { get; private set; }
        public Vector3 Front { get; private set; }
        public string Street { get; private set; } = string.Empty;
        public int Number { get; private set; }

        /// <summary>The university's building; null until found.</summary>
        public CityBuildingController Building { get; private set; }

        /// <summary>Where the player stands to attend: at the sign, or at the door.</summary>
        public bool HasSign => _sign != null;

        /// <summary>The address as the game writes it; empty until found.</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>True at the sign (or the door), or inside the building.</summary>
        public bool At(Vector3 player)
        {
            if (!Usable)
            {
                return false;
            }
            if (CareersGame.InsideAt(Street, Number))
            {
                return true;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            Vector3 offset = Front - player;
            float reach = _sign != null ? Reach : ReachAtDoor;
            return offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < reach * reach;
        }

        /// <summary>
        /// Once a second: until <see cref="Searched"/>, finds the building, then stands the sign; after that, every five
        /// seconds, a university whose building has become the player's (a save from before the mod's buildings were off
        /// the market, another player's in a session) moves off it, to the next that may serve.
        /// </summary>
        public void Tick()
        {
            if (_failed)
            {
                return;
            }
            if (Searched)
            {
                if (Building != null && ++_look >= 5)
                {
                    _look = 0;
                    if (SpecialBuildings.Players(Building))
                    {
                        _logger?.Info("Careers: the university's building, " + Name + ", is the player's; the university moves to another.");
                        Dispose();
                    }
                }
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
                Func<bool> others = CareersBridges.OthersPicked;
                if (others != null && !others() && ++_waited < WaitForOthers)
                {
                    return;   // (the utilities office and the courthouse choose first: the university is never one of them)
                }
                CityBuildingController picked = Pick(buildings, out CityBuildingController largest);
                Searched = true;
                if (largest != null && !ReferenceEquals(largest, picked))
                {
                    // Said once a city load: a save from 0.7.0 may have had its lectures in that building.
                    _logger?.Info("Careers: the largest office building, " + largest.building.Address.ToFormattedString() + ", is another of the mod's buildings ("
                                  + (SpecialBuildings.NameFor(largest) ?? "taken") + "); the university is in the next largest. Enrolments follow it.");
                }
                if (picked == null)
                {
                    _logger?.Warn("Careers: no building could serve as the university; its lectures are followed online from home instead.");
                    return;
                }
                Building = picked;
                Street = picked.building.Address.streetName;
                Number = picked.building.Address.streetNumber;
                Name = picked.building.Address.ToFormattedString();
                bool placed = Place(picked);
                if (!placed && JobSpots.Front(picked, out Vector3 door))
                {
                    Front = door;
                    Usable = true;
                }
                if (Usable)
                {
                    // Its own name wherever the game names the building (Core/SpecialBuildings).
                    SpecialBuildings.Set("campus", picked, SpecialKind.Campus, "reallife_study_campus_university", "reallife_campus_card_type", () => CareersDirector.UniversityOpen);
                }
                _logger?.Info("Careers: the university is " + Name + " (" + picked.building.totalSqm + " sqm); " + (placed ? "sign up." : Usable ? "lectures at the door." : "no pavement at its door: online only."));
            }
            catch (Exception e)
            {
                _failed = true;
                Searched = true;
                Usable = false;
                _logger?.Error(e);
                _logger?.Warn("Careers: the university could not be set up after the error above; its lectures are followed online from home instead.");
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
            Searched = false;
            _failed = false;
            _waited = 0;
            Building = null;
            SpecialBuildings.Remove("campus");
        }

        // The largest office building that may serve and that no other feature holds; also the largest with none left out, to
        // say so when the two differ.
        private static CityBuildingController Pick(CityBuildingController[] buildings, out CityBuildingController largest)
        {
            Func<int, bool> fits = i =>
            {
                CityBuildingController b = buildings[i];
                return b != null && b.building != null && b.entranceDoors != null && b.entranceDoors.Length > 0 && b.building.locatedInsidePrivateFenceIndex < 0
                       && b.building.SpecialService == null && b.building.BuildingType == Office
                       && !SpecialBuildings.Players(b);   // (one the player rents or owns stays theirs)
            };
            Func<int, int> sqm = i => buildings[i].building.totalSqm;
            Func<int, string> street = i => buildings[i].building.StreetName;
            Func<int, int> number = i => buildings[i].building.StreetNumber;
            int any = SpecialNameBook.PickLargest(buildings.Length, fits, sqm, street, number, null);
            int free = SpecialNameBook.PickLargest(buildings.Length, fits, sqm, street, number, SpecialBuildings.Holds);
            largest = any >= 0 ? buildings[any] : null;
            return free >= 0 ? buildings[free] : null;
        }

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
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, front) < 3f || !CitySpots.Stand(reader, 0.4f, out reader))
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
            var sign = new GameObject("RealLifeOverhaul University Sign");
            Transform root = sign.transform;
            root.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
            Material post = Shapes.Paint(new Color(0.18f, 0.16f, 0.14f));
            Material board = Shapes.Paint(new Color(0.36f, 0.08f, 0.12f));
            Material gold = Shapes.Paint(new Color(0.82f, 0.68f, 0.30f));
            if (post == null || board == null || gold == null)
            {
                UnityEngine.Object.Destroy(sign);
                return null;
            }
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(-0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Post", root, Shapes.Post, post, new Vector3(0.60f, 0f, 0f), new Vector3(0.09f, 1.95f, 0.09f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(-0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Foot", root, Shapes.Cube, post, new Vector3(0.60f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.42f));
            Shapes.Piece("Board", root, Shapes.Cube, board, new Vector3(0f, 1.52f, 0f), new Vector3(1.30f, 0.62f, 0.05f));
            Shapes.Piece("Rule", root, Shapes.Cube, gold, new Vector3(0f, 1.27f, 0.004f), new Vector3(1.18f, 0.03f, 0.05f));
            Shapes.Piece("Cap", root, Shapes.Cube, gold, new Vector3(0f, 1.85f, 0f), new Vector3(1.36f, 0.04f, 0.08f));
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font != null)
            {
                var label = new GameObject("Lettering");
                Transform place = label.transform;
                place.SetParent(root, false);
                place.localPosition = new Vector3(0f, 1.56f, 0.027f);
                place.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var text = label.AddComponent<TextMeshPro>();
                text.font = font;
                text.text = CareersText.T("reallife_study_sign");
                text.color = new Color(0.95f, 0.91f, 0.80f);
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
