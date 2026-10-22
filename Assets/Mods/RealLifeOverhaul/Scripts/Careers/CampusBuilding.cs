using System;
using BAModAPI;
using BigAmbitions.Rivals;
using Blueprints;
using Buildings;
using RealLifeOverhaul.Meta;
using Streets;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Hudson City University's building: its permanent building, 26 2nd Avenue, reserved from the start; while a player holds
    /// it, the largest free office building meanwhile, else the door of the game's own school, the same on every machine.
    /// Its name, sign, map row and board at the door. Looked for once a city load, then a few comparisons every five seconds.
    /// </summary>
    public sealed class CampusBuilding
    {
        private const string Office = "ba:buildingtype_office";
        private const float FromWall = 0.30f;                                                                                                // metres the board stands from the wall
        private const float Reach = 2.2f;                                                                                                    // metres from the board the player attends within
        // metres: clear of the driving school's desk at the school's door
        private static readonly float ReachAtSchool = CampusRules.DoorReachAt(CampusPlace.School, LicenceDesks.Aside, LicenceDesks.Reach);
        private const int WaitForOthers = 30;   // seconds to wait for the utilities office and the courthouse to choose theirs
        private const float SignCullRadius = 2f;   // metres round the sign's foot: all of it (Performance/ModCulling)
        private static readonly float[] Aside = { -4.6f, 4.6f, -6.2f, 6.2f };   // metres along the front; the courthouse tries the other side first

        private readonly IModLogger _logger;
        private readonly Func<bool> _sitting;    // a class or exam of the mod's is being sat (CareersDirector.SittingNow)
        private GameObject _sign;
        private CullEntry _cull;                  // the sign, not drawn beyond the game's far band (Performance/ModCulling)
        private bool _failed;
        private int _waited;
        private int _look;
        private CityBuildingController _home;     // the permanent building in this city, once looked for
        private CityBuildingController[] _homeOf; // the city's list it was found in
        private bool _homeSaid;                   // its check and, in an older game, why the university waits for it: said once a city load

        /// <summary>The test's pretence (CareersDirector.TestAway): 1 its home and every office taken, 2 only its home; 0 none. The city is not touched.</summary>
        internal static int Pretend;

        public CampusBuilding(IModLogger logger, Func<bool> sitting = null)
        {
            _logger = logger;
            _sitting = sitting;
        }

        /// <summary>True while the university is in its permanent building.</summary>
        public bool AtHome => Building != null && CampusRules.IsHome(Street, Number);

        public bool Searched { get; private set; }
        public bool Usable { get; private set; }
        public Vector3 Front { get; private set; }

        /// <summary>Where the player stands at the door, outside: the floors are chosen here (the sign's place if the door has no pavement).</summary>
        public Vector3 Door { get; private set; }
        public string Street { get; private set; } = string.Empty;
        public int Number { get; private set; }

        /// <summary>"street|number" of the building the earlier rule would take (the largest office, whatever business was in it), for a save that kept none.</summary>
        public string Before { get; private set; }

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
            float reach = _sign != null ? Reach : Place == CampusPlace.School ? ReachAtSchool : CampusRules.DoorReach;
            return offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < reach * reach;
        }

        /// <summary>Once a second: finds the university and stands its board; then every five seconds moves it off a building it may no longer have, or home once free.</summary>
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
                    string why = Place == CampusPlace.School ? null : WhyNotStill();   // the game's school is always the game's school
                    bool inside = CareersGame.InsideAt(Street, Number);
                    if (why != null && !inside)
                    {
                        _logger?.Info("Careers: the university's building, " + Name + ", may no longer serve: " + why + "; the university moves to another.");
                        Dispose();
                    }
                    else if (why == null && CampusRules.MovesHome(AtHome, Block() == HomeBlock.None, inside, _sitting != null && _sitting()))
                    {
                        Dev.Always(_logger, "Careers: the university's permanent building, " + _home.building.Address.ToFormattedString() + ", is free now; the university moves there from "
                                            + Name + " for good.");
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
                FindHome(buildings);
                HomeBlock block = Block();
                if (block != HomeBlock.None)
                {
                    Func<bool> others = CareersBridges.OthersPicked;
                    if (others != null && !others() && ++_waited < WaitForOthers)
                    {
                        return;   // the utilities office and the courthouse choose first: the university is never one of them
                    }
                }
                CityBuildingController office = Pick(buildings, out CityBuildingController largest, out CityBuildingController before);
                office = Pretend == 1 ? null : office;   // the test's pretence: every office building taken
                CityBuildingController school = School(buildings);
                Before = before != null ? before.building.StreetName + "|" + before.building.StreetNumber : null;
                Searched = true;
                Blocked = block;
                Place = CampusRules.Choose(block == HomeBlock.None, office != null, school != null);
                CityBuildingController picked = Place == CampusPlace.Home ? _home : Place == CampusPlace.Office ? office : Place == CampusPlace.School ? school : null;
                if (Place == CampusPlace.Office && largest != null && !ReferenceEquals(largest, picked))
                {
                    // Said once a city load: why the university is not in the city's largest office building.
                    string held = SpecialBuildings.NameFor(largest);
                    _logger?.Info("Careers: the largest office building, " + largest.building.Address.ToFormattedString() + ", is "
                                  + (held != null ? "another of the mod's buildings (" + held + ")" : SpecialBuildings.Players(largest) ? "the player's"
                                      : "a business's (" + (largest.buildingRegistration != null ? largest.buildingRegistration.businessTypeName : "?") + ")")
                                  + "; meanwhile the university takes the largest office building with no business in it.");
                }
                if (picked == null)
                {
                    Place = CampusPlace.None;
                    Dev.Always(_logger, "Careers: the university has no building: " + Away(block)
                                        + ", no office building is free and the city has no Manhattan Business School; its lectures are followed online from home.");
                    return;
                }
                Building = picked;
                Street = picked.building.Address.streetName;
                Number = picked.building.Address.streetNumber;
                Name = picked.building.Address.ToFormattedString();
                bool atDoor = JobSpots.Front(picked, out Vector3 door);
                bool placed = Place != CampusPlace.School && Stand(picked);   // the game's school has its own sign and the driving school's desk at its front
                if (!placed && atDoor)
                {
                    Front = door;
                    Usable = true;
                }
                Door = atDoor ? door : Front;
                if (Usable && Place != CampusPlace.School)
                {
                    // Its own name wherever the game names the building, and on its lit sign; the game's school keeps its own name.
                    SpecialBuildings.Set(CampusRules.BookId, picked, SpecialKind.Campus, "reallife_study_campus_university", "reallife_campus_card_type", () => CareersDirector.UniversityOpen);
                    SpecialBuildings.Boarded(CampusRules.BookId, placed);   // its board stands at the door; the building's own sign carries the name, lit (FrontSignRules.Look)
                }
                // One plain line whatever the diagnostics switch says, so a player's log shows where the university is and why.
                Dev.Always(_logger, "Careers: the university is " + Name + " (" + (Place == CampusPlace.Home ? "its permanent building" : Place == CampusPlace.Office
                                        ? "meanwhile, the largest office building with no business in it, as " + Away(block)
                                        : "meanwhile, at the door of Manhattan Business School, as " + Away(block) + " and no office building is free")
                                    + "; " + picked.building.totalSqm + " sqm, " + picked.building.BuildingType + ", size " + picked.building.BuildingSize
                                    + " version " + picked.building.BuildingVersion + "); "
                                    + (placed ? "board up." : Usable ? "joined at the door." : "no pavement at its door: online only."));
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

        /// <summary>Where the university is (CampusRules.Choose); None until found or with no building at all.</summary>
        public CampusPlace Place { get; private set; }

        /// <summary>What kept it out of its permanent building when it was found (None at home).</summary>
        public HomeBlock Blocked { get; private set; }

        /// <summary>The permanent building's address as the game writes it (the constant until the city is there).</summary>
        public string HomeName => _home != null && _home.building != null ? _home.building.Address.ToFormattedString() : CampusRules.HomeAddress(null);

        public void Dispose()
        {
            ModCulling.Remove(_cull);
            _cull = null;
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
            Place = CampusPlace.None;
            SpecialBuildings.Remove(CampusRules.BookId);
        }

        // The permanent building, looked for once a city, with its check said once: as chosen, and still no rival can take it.
        private void FindHome(CityBuildingController[] buildings)
        {
            if (ReferenceEquals(buildings, _homeOf))
            {
                return;
            }
            _homeOf = buildings;
            _home = null;
            for (int i = 0; i < buildings.Length; i++)
            {
                Building b = buildings[i] != null ? buildings[i].building : null;
                if (b != null && CampusRules.IsHome(b.StreetName, b.StreetNumber))
                {
                    _home = buildings[i];
                    break;
                }
            }
            if (_homeSaid)
            {
                return;
            }
            _homeSaid = true;
            Building h = _home != null ? _home.building : null;
            bool chosen = h != null && CampusRules.HomeIsAsChosen(h.BuildingType, h.BuildingSize, h.BuildingVersion, h.SpecialService != null);
            bool rivals = h != null && RivalsCanHave(h);
            string line = "Careers: the university's permanent building check (" + CampusRules.HomeAddress(null) + ": "
                          + (h == null ? "not in this city"
                              : h.BuildingType + ", " + h.BuildingSize + " version " + h.BuildingVersion + (h.SpecialService != null ? ", a special service" : string.Empty))
                          + "; wanted an office of size C version 2 that the game's rivals cannot have, reserved for the university): "
                          + (chosen && !rivals ? "PASS" : !chosen ? "FAIL (the game's city has changed: it is not the building chosen)"
                              : "FAIL (the game's rivals can have an office of its size now)") + ".";
            if (chosen && !rivals)
            {
                _logger?.Info(line);
            }
            else
            {
                _logger?.Warn(line);
            }
        }

        // True if the game's rivals may open a business in an office of the building's size; false when the game cannot say.
        private static bool RivalsCanHave(Building b)
        {
            try
            {
                var size = new BuildingSizeInfo(b);
                return RivalsHelper.IsBuildingSuitableForImporterRival(b.BuildingType, size) || RivalsHelper.IsBuildingSuitableForWholesalerRival(b.BuildingType, size)
                       || RivalsHelper.IsBuildingSuitableForSpecialRival(b.BuildingType, size);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // What keeps the university out of its permanent building now (CampusRules.HomeBlockOf); its own book entry is no block.
        private HomeBlock Block()
        {
            if (Pretend > 0)
            {
                return HomeBlock.Business;   // the test's pretence
            }
            CityBuildingController h = _home;
            BuildingRegistration r = h != null ? h.buildingRegistration : null;
            string filed = SpecialBuildings.FiledAt(h);
            bool serves = r != null && May(h) && CampusRules.HomeIsAsChosen(h.building.BuildingType, h.building.BuildingSize, h.building.BuildingVersion, h.building.SpecialService != null);
            return CampusRules.HomeBlockOf(serves, r != null ? r.businessTypeName : null, r != null && r.RentedByPlayer, r != null && r.BuildingOwnedByPlayer,
                r != null ? r.businessOwnerRivalId : null, r != null && !string.IsNullOrEmpty(r.Layout), filed != null && filed != CampusRules.BookId);
        }

        // The block in words, for the log.
        private static string Away(HomeBlock block)
        {
            switch (block)
            {
                case HomeBlock.Players:
                    return "its permanent building " + CampusRules.HomeAddress(null) + " is rented or owned by the player"
                           + (GoingPublic.InSession ? " (or, in this session, another player)" : string.Empty);
                case HomeBlock.Business: return "its permanent building " + CampusRules.HomeAddress(null) + " holds a business (the game's, a rival's or another mod's)";
                case HomeBlock.Left: return "its permanent building " + CampusRules.HomeAddress(null) + " has a layout left inside";
                case HomeBlock.Missing: return "its permanent building " + CampusRules.HomeAddress(null) + " cannot be used in this city";
                case HomeBlock.Held:
                    return "its permanent building " + CampusRules.HomeAddress(null)
                           + " is still another of the mod's buildings, chosen while the university was off, until it moves off at its next choice";
                default: return "its permanent building is free";
            }
        }

        // The game's own Manhattan Business School (its special service is the school), if it can be walked to.
        private static CityBuildingController School(CityBuildingController[] buildings)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                if (b != null && b.building != null && b.building.SpecialService != null && b.building.SpecialService.businessTypeName == CareersGame.SchoolType
                    && b.entranceDoors != null && b.entranceDoors.Length > 0 && b.entranceDoors[0] != null && b.entranceDoors[0].doorTransform != null && !GameMembers.Fenced(b.building))
                {
                    return b;
                }
            }
            return null;
        }

        // The largest office building that may serve and no other feature holds; also the largest of all, to say when they differ.
        private static CityBuildingController Pick(CityBuildingController[] buildings, out CityBuildingController largest, out CityBuildingController before)
        {
            Func<int, bool> fits = i =>
            {
                CityBuildingController b = buildings[i];
                return May(b) && !SpecialBuildings.Players(b);   // one the player rents or owns stays theirs
            };
            Func<int, bool> free = i => fits(i) && Empty(buildings[i]);
            Func<int, int> sqm = i => buildings[i].building.totalSqm;
            Func<int, string> street = i => buildings[i].building.StreetName;
            Func<int, int> number = i => buildings[i].building.StreetNumber;
            int any = SpecialNameBook.PickLargest(buildings.Length, fits, sqm, street, number, null);
            int chosen = SpecialNameBook.PickLargest(buildings.Length, free, sqm, street, number, SpecialBuildings.Holds);
            int old = SpecialNameBook.PickLargest(buildings.Length, fits, sqm, street, number, SpecialBuildings.Holds);   // the earlier rule: whatever was in it
            largest = any >= 0 ? buildings[any] : null;
            before = old >= 0 ? buildings[old] : null;
            return chosen >= 0 ? buildings[chosen] : null;
        }

        // An office building of the city's own that the player can walk to: not a special service, not behind a fence.
        private static bool May(CityBuildingController b)
        {
            return b != null && b.building != null && b.entranceDoors != null && b.entranceDoors.Length > 0 && b.entranceDoors[0] != null && b.entranceDoors[0].doorTransform != null
                   && !GameMembers.Fenced(b.building) && b.building.SpecialService == null && b.building.BuildingType == Office && !Offices.Conversions.Holds(b.building);
        }

        /// <summary>True for a building that holds no business and is nobody's (CampusRules.NoBusinessIn); a layout left inside counts as a business.</summary>
        public static bool Empty(CityBuildingController b)
        {
            BuildingRegistration r = b != null ? b.buildingRegistration : null;
            return r != null && CampusRules.NoBusinessIn(r.businessTypeName, r.RentedByPlayer, r.BuildingOwnedByPlayer, r.businessOwnerRivalId)
                   && string.IsNullOrEmpty(r.Layout);   // a layout left behind would have the game look for it inside, and go back out
        }

        /// <summary>Null if the university may still have its building (no business in it, not a player's), else why not, for the log.</summary>
        public string WhyNotStill()
        {
            if (!Searched || Building == null)
            {
                return null;
            }
            BuildingRegistration r = Building.buildingRegistration;
            if (r == null)
            {
                return null;
            }
            if (SpecialBuildings.Players(r))
            {
                return "it is rented or owned by the player" + (GoingPublic.InSession ? " (or, in this session, another player)" : string.Empty);
            }
            if (!Empty(Building))
            {
                return "a business has opened in it (" + (r.businessTypeName ?? "?") + (string.IsNullOrEmpty(r.businessOwnerRivalId) ? string.Empty : ", " + r.businessOwnerRivalId) + ")";
            }
            return null;
        }

        private bool Stand(CityBuildingController building)
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
                ModCulling.Remove(_cull);
                _cull = ModCulling.Add(CullRules.Kind.BuildingSign, _sign.transform, at, SignCullRadius);
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
