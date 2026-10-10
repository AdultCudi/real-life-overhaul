using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Buildings;
using Character;
using Characters;
using Helpers;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>One hotel as it stands in the city this session.</summary>
    public sealed class HotelSite
    {
        public HotelSlot Slot;
        public CityBuildingController Building;
        public Vector3 Desk;            // where the player stands to be served: the door, or the cottages' board
        public Vector3 Front;           // pavement in front of it
        public Vector3 Outward;         // from the wall towards the street
        public bool Dressed;            // the frontage was looked at (whatever could be stood there, stands)
        public Transform Dressing;      // sign, awning, luggage cart
        public bool HasDoormanSpot;
        public Vector3 DoormanSpot;
        public Vector3 DoormanFace;
        public string Name;
    }

    /// <summary>
    /// The city's hotels. The game has none, so eight existing places are designated: seven
    /// residential buildings (which nobody can walk into unless they rent them) and a board for
    /// rented cottages in the Hamptons. Which building is a matter of static data only
    /// (TravelCatalogue.Hotels), so a booking made in one session finds the same hotel in the
    /// next; a building the player has since rented or bought gives way to the next in line.
    ///
    /// The frontage is dressed the first time the player comes within 150 m: a sign against the
    /// wall beside the door, an awning over it, a luggage cart, each only where a real wall and
    /// clear pavement were measured for it, so nothing stands in anything. With the modelled
    /// props hotel_sign, hotel_awning and luggage_cart in the prop bundle those are used;
    /// until then plain shapes of the same size. A doorman stands at the better hotels from
    /// 07:00 to midnight: one character for the whole city (never from the accident or the
    /// living-city pool), made the first time he is needed, put out and taken in only out of
    /// view. Nothing here runs unless the director calls it, once a second.
    /// </summary>
    public sealed class Hotels
    {
        private const string Residential = "ba:buildingtype_residential";
        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float DressWithin = 150f;
        private const float DeskReach = 3.6f;
        private const float BoardReach = 1.9f;
        private const float DoormanWithin = 70f;
        private const float DoormanAway = 130f;
        private const float AwningHeight = 2.75f;
        private const float AwningDepth = 1.4f;
        private const float AwningWidth = 2.4f;

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };
        private static readonly Collider[] Touching = new Collider[4];

        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly List<HotelSite> _sites = new List<HotelSite>(8);
        private readonly HashSet<Building> _taken = new HashSet<Building>();
        private CityProps _props;
        private CityPerson _doorman;
        private HotelSite _doormanAt;
        private float _doormanRetry;
        private bool _doormanFailed;
        private int _wallMask;
        private int _solidMask;
        private bool _resolved;

        public Hotels(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
        }

        public IReadOnlyList<HotelSite> Sites => _sites;

        /// <summary>The hotel whose desk the player stands at, or null.</summary>
        public HotelSite InReach { get; private set; }

        public HotelSite ById(string id)
        {
            for (int i = 0; i < _sites.Count; i++)
            {
                if (_sites[i].Slot.Id == id)
                {
                    return _sites[i];
                }
            }
            return null;
        }

        /// <summary>The nearest hotel of a tier (or of any, with null) to a point, as the crow flies.</summary>
        public HotelSite Nearest(Vector3 from, HotelTier? tier, out float distance)
        {
            HotelSite best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _sites.Count; i++)
            {
                if (tier.HasValue && _sites[i].Slot.Tier != tier.Value)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(_sites[i].Desk - from);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = _sites[i];
                }
            }
            distance = best != null ? Mathf.Sqrt(bestSqr) : 0f;
            return best;
        }

        /// <summary>Picks the buildings. Once per city load, when the save is ready; false if the city is not there yet.</summary>
        public bool Resolve()
        {
            if (_resolved)
            {
                return true;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null || BuildingHelper.allBuildings == null || BuildingHelper.allBuildings.Count == 0)
            {
                return false;
            }
            _resolved = true;
            _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
            var said = new System.Text.StringBuilder("Travel: hotels:");
            foreach (HotelSlot slot in TravelCatalogue.Hotels)
            {
                CityBuildingController building = slot.Anchors != null ? Anchor(city, slot) : Pick(city, slot, slot.Sizes) ?? Pick(city, slot, null);
                if (building == null || !Door(building, out Vector3 door))
                {
                    said.Append(' ').Append(slot.Id).Append("=none");
                    continue;
                }
                _taken.Add(building.building);
                var site = new HotelSite { Slot = slot, Building = building, Desk = door, Front = door, Name = TravelText.T(slot.NameKey) };
                _sites.Add(site);
                said.Append(' ').Append(slot.Id).Append('=').Append(building.building.StreetName).Append(' ').Append(building.building.StreetNumber);
            }
            _logger?.Info(said.ToString());
            return true;
        }

        /// <summary>Once a second while the player is on foot or driving out of doors.</summary>
        public void Tick(Vector3 player, bool onFoot, int hour)
        {
            HotelSite reach = null;
            HotelSite near = null;
            float nearSqr = DoormanWithin * DoormanWithin;
            for (int i = 0; i < _sites.Count; i++)
            {
                HotelSite site = _sites[i];
                Vector3 offset = site.Desk - player;
                float sqr = CitySpots.Flat(offset);
                if (!site.Dressed && Settings.Hotels && sqr < DressWithin * DressWithin)
                {
                    Dress(site);   // one frontage a second at most: the loop goes on, but there is never more than one this near
                }
                float reachMetres = site.Slot.Anchors != null ? BoardReach : DeskReach;
                if (onFoot && Mathf.Abs(offset.y) < 2.5f && sqr < reachMetres * reachMetres)
                {
                    reach = site;
                }
                if (site.Slot.Doorman && site.HasDoormanSpot && sqr < nearSqr)
                {
                    nearSqr = sqr;
                    near = site;
                }
            }
            InReach = reach;
            TickDoorman(player, near, hour);
        }

        public void Away()
        {
            InReach = null;
        }

        public void Dispose()
        {
            ReleaseDoorman(true);
            for (int i = 0; i < _sites.Count; i++)
            {
                HotelSite site = _sites[i];
                if (site.Dressing != null)
                {
                    UnityEngine.Object.Destroy(site.Dressing.gameObject);
                }
            }
            _sites.Clear();
            _taken.Clear();
            InReach = null;
        }

        // ---------------------------------------------------------------- which buildings

        // The residential building of the district with the most passing trade; ties by street name, then number.
        private CityBuildingController Pick(CityManager city, HotelSlot slot, string sizes)
        {
            Building best = null;
            CityBuildingController bestController = null;
            List<Building> all = BuildingHelper.allBuildings;
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                if (b == null || b.BuildingType != Residential || b.Neighbourhood != slot.Neighbourhood || b.locatedInsidePrivateFenceIndex >= 0
                    || (int)b.requiredDLC != 0 || b.IsHamptonsHouse() || _taken.Contains(b) || string.IsNullOrEmpty(b.BuildingSize))
                {
                    continue;
                }
                if (sizes != null && sizes.IndexOf(b.BuildingSize[b.BuildingSize.Length - 1]) < 0)
                {
                    continue;
                }
                if (best != null && !Better(b, best))
                {
                    continue;
                }
                if (Mine(b))
                {
                    continue;
                }
                CityBuildingController controller = city.FindCityBuildingController(b.Address);
                if (controller == null || !Door(controller, out _))
                {
                    continue;
                }
                best = b;
                bestController = controller;
            }
            return bestController;
        }

        private static bool Better(Building a, Building b)
        {
            if (a.trafficIndex != b.trafficIndex)
            {
                return a.trafficIndex > b.trafficIndex;
            }
            int street = string.CompareOrdinal(a.StreetName, b.StreetName);
            return street != 0 ? street < 0 : a.StreetNumber < b.StreetNumber;
        }

        private static bool Mine(Building building)
        {
            try
            {
                BuildingRegistration registration = BuildingHelper.GetBuildingRegistration(building.Address);
                return registration == null || registration.RentedByPlayer || registration.BuildingOwnedByPlayer;
            }
            catch (Exception)
            {
                return true;   // a building the game cannot answer for is no hotel
            }
        }

        // The cottages have no building of their own: their board stands by the first of these that is there.
        private static CityBuildingController Anchor(CityManager city, HotelSlot slot)
        {
            foreach (string anchor in slot.Anchors)
            {
                int bar = anchor.IndexOf('|');
                if (bar > 0 && int.TryParse(anchor.Substring(bar + 1), out int number))
                {
                    CityBuildingController controller = city.FindCityBuildingController(new Address(anchor.Substring(0, bar), number));
                    if (controller != null && controller.building != null && Door(controller, out _))
                    {
                        return controller;
                    }
                }
            }
            return null;
        }

        private static bool Door(CityBuildingController building, out Vector3 door)
        {
            door = default;
            if (building == null || building.entranceDoors == null)
            {
                return false;
            }
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                if (building.entranceDoors[i] != null && building.entranceDoors[i].doorTransform != null)
                {
                    door = building.entranceDoors[i].doorTransform.position;
                    return true;
                }
            }
            return false;
        }

        // (On the city map the hotels are the places of the "Hotels" category: World/MapLinks, through Core/MapPlaces, which
        // reads Sites. They no longer add markers of their own here.)

        // ---------------------------------------------------------------- the frontage

        private void Dress(HotelSite site)
        {
            site.Dressed = true;
            try
            {
                if (!Door(site.Building, out Vector3 door) || !StreetSpots.OnPavement(door, 6f, out Vector3 front))
                {
                    _logger?.Info("Travel: " + site.Slot.Id + " has no pavement by its door; it is served at the door and has no frontage.");
                    return;
                }
                site.Front = front;
                Vector3 outward = front - door;
                outward.y = 0f;
                bool along = CitySpots.Along(front, out Vector3 run);
                if (outward.sqrMagnitude < 0.04f)
                {
                    outward = along ? Vector3.Cross(run, Vector3.up) : Vector3.forward;
                }
                outward.Normalize();
                if (!along)
                {
                    run = Vector3.Cross(Vector3.up, outward);
                }
                site.Outward = outward;
                if (!Shapes.Available)
                {
                    return;
                }
                var dressing = new GameObject("RealLifeOverhaul Hotel " + site.Slot.Id).transform;
                dressing.SetParent(_root, false);
                site.Dressing = dressing;
                Color colour = TierColour(site.Slot.Tier);
                int placed = 0;

                // The sign against the wall, a step or two to one side of the door.
                int signSide = 0;
                for (int side = -1; side <= 1 && signSide == 0; side += 2)
                {
                    if (WallSpot(front + run * (side * 2.4f), 0.75f, 0.6f, front, out Vector3 stand, out Vector3 wallOut))
                    {
                        Vector3 at = stand - wallOut * 0.45f;
                        at.y = stand.y;
                        BuildSign(dressing, at, wallOut, colour, site.Name);
                        signSide = side;
                        placed++;
                        if (site.Slot.Anchors != null)
                        {
                            site.Desk = stand + wallOut * 0.35f;   // the cottages are booked at their board
                        }
                    }
                }
                if (site.Slot.Anchors != null)
                {
                    if (signSide == 0 && StreetSpots.OnPavement(front + run * 3f, 1.5f, out Vector3 beside))
                    {
                        site.Desk = beside;   // no wall for a board: the agent's rep meets guests on the pavement here
                    }
                    _logger?.Info("Travel: " + site.Slot.Id + (signSide != 0 ? " has its board up." : " found no wall for its board; guests are met on the pavement."));
                    return;
                }

                int otherSide = signSide != 0 ? -signSide : 1;
                if (site.Slot.Tier != HotelTier.Budget)
                {
                    if (WallSpot(front + run * (otherSide * 3.3f), 0.85f, 0.75f, front, out Vector3 cartStand, out Vector3 cartOut))
                    {
                        Vector3 at = cartStand - cartOut * 0.35f;
                        at.y = cartStand.y;
                        BuildCart(dressing, at, cartOut);
                        placed++;
                    }
                    if (WallSpot(front + run * (otherSide * 1.7f), 0.55f, 0.4f, front, out Vector3 manStand, out Vector3 manOut))
                    {
                        site.HasDoormanSpot = true;
                        site.DoormanSpot = manStand;
                        site.DoormanFace = manStand + manOut;
                    }
                }
                if (BuildAwning(dressing, door, front, outward, run, colour))
                {
                    placed++;
                }
                _logger?.Info("Travel: " + site.Slot.Id + " dressed with " + placed + " piece(s)" + (site.HasDoormanSpot ? " and a place for the doorman." : "."));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Travel: the frontage of " + site.Slot.Id + " could not be dressed (see above); the hotel still takes guests.");
            }
        }

        // A measured stretch of real wall with clear pavement in front of it, on this building's side of the street.
        private static bool WallSpot(Vector3 near, float distance, float room, Vector3 front, out Vector3 stand, out Vector3 outward)
        {
            stand = default;
            outward = default;
            if (!CitySpots.Pavement(near, 1.5f, out Vector3 spot) || !CitySpots.Shopfront(spot, distance, room, out stand, out outward))
            {
                return false;
            }
            return CitySpots.Flat(stand - front) < 7f * 7f && CitySpots.Ground(stand, out float ground) && Mathf.Abs(ground - stand.y) < 0.25f;
        }

        private static Color TierColour(HotelTier tier)
        {
            switch (tier)
            {
                case HotelTier.Budget: return new Color(0.16f, 0.50f, 0.36f);
                case HotelTier.Luxury: return new Color(0.42f, 0.10f, 0.14f);
                case HotelTier.Cottage: return new Color(0.20f, 0.46f, 0.62f);
                default: return new Color(0.12f, 0.24f, 0.48f);
            }
        }

        // hotel_sign: stands on the ground at its middle, face to +Z, 0.70 wide, 1.90 tall, 0.16 deep; the name plate is 0.60 x 0.34 centred 1.50 m up,
        // its face at z 0.080 (nothing of the model is in front of that): the name is lettered 5 mm proud of it, inside a 2 cm margin.
        private void BuildSign(Transform parent, Vector3 at, Vector3 facing, Color colour, string name)
        {
            var sign = new GameObject("Sign").transform;
            sign.SetParent(parent, false);
            sign.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
            if (!Shapes.Prop("hotel_sign", sign))
            {
                Shapes.Piece("Post", sign, Shapes.Cube, Shapes.Paint(new Color(0.10f, 0.10f, 0.11f)), new Vector3(0f, 0.95f, 0f), new Vector3(0.70f, 1.90f, 0.10f));
                Shapes.Piece("Band", sign, Shapes.Cube, Shapes.Paint(colour, 0.6f), new Vector3(0f, 1.50f, 0.045f), new Vector3(0.64f, 0.40f, 0.03f));
                Shapes.Piece("Foot", sign, Shapes.Cube, Shapes.Paint(new Color(0.10f, 0.10f, 0.11f)), new Vector3(0f, 0.03f, 0f), new Vector3(0.78f, 0.06f, 0.30f));
                Shapes.Piece("Bed", sign, Shapes.Cube, Shapes.Paint(new Color(0.92f, 0.90f, 0.84f), 0.3f), new Vector3(0f, 0.95f, 0.055f), new Vector3(0.40f, 0.10f, 0.012f));
                Shapes.Piece("Head", sign, Shapes.Cube, Shapes.Paint(new Color(0.92f, 0.90f, 0.84f), 0.3f), new Vector3(-0.17f, 1.03f, 0.055f), new Vector3(0.06f, 0.26f, 0.012f));
            }
            TMP_FontAsset font = Hud.Font;
            if (font == null)
            {
                return;
            }
            var label = new GameObject("Name").transform;
            label.SetParent(sign, false);
            label.localPosition = new Vector3(0f, 1.50f, 0.085f);
            label.localRotation = Quaternion.Euler(0f, 180f, 0f);   // world-space text reads from the side its back is turned to
            var text = label.gameObject.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontSize = 0.9f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = 1.1f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.97f, 0.96f, 0.92f);
            text.rectTransform.sizeDelta = new Vector2(0.56f, 0.30f);
            text.text = name;
            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // luggage_cart: stands on the ground at its middle, long side along X, 1.10 x 0.60 on the ground, 1.75 tall.
        private void BuildCart(Transform parent, Vector3 at, Vector3 wallOut)
        {
            var cart = new GameObject("Luggage Cart").transform;
            cart.SetParent(parent, false);
            cart.SetPositionAndRotation(at, Quaternion.LookRotation(wallOut, Vector3.up));
            if (Shapes.Prop("luggage_cart", cart))
            {
                return;
            }
            Material brass = Shapes.Paint(new Color(0.72f, 0.56f, 0.22f));
            Shapes.Piece("Deck", cart, Shapes.Cube, Shapes.Paint(new Color(0.40f, 0.08f, 0.10f)), new Vector3(0f, 0.20f, 0f), new Vector3(1.10f, 0.06f, 0.60f));
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner < 2 ? -0.52f : 0.52f;
                float z = corner % 2 == 0 ? -0.27f : 0.27f;
                Shapes.Piece("Upright", cart, Shapes.Cube, brass, new Vector3(x, 0.97f, z), new Vector3(0.035f, 1.50f, 0.035f));
                Shapes.Piece("Wheel", cart, Shapes.Cube, Shapes.Paint(new Color(0.08f, 0.08f, 0.08f)), new Vector3(x, 0.085f, z), new Vector3(0.06f, 0.17f, 0.14f));
            }
            Shapes.Piece("Top Near", cart, Shapes.Cube, brass, new Vector3(0f, 1.72f, 0.27f), new Vector3(1.08f, 0.035f, 0.035f));
            Shapes.Piece("Top Far", cart, Shapes.Cube, brass, new Vector3(0f, 1.72f, -0.27f), new Vector3(1.08f, 0.035f, 0.035f));
            Shapes.Piece("Case", cart, Shapes.Cube, Shapes.Paint(new Color(0.20f, 0.24f, 0.34f)), new Vector3(-0.22f, 0.45f, 0f), new Vector3(0.50f, 0.44f, 0.38f));
            Shapes.Piece("Case", cart, Shapes.Cube, Shapes.Paint(new Color(0.46f, 0.30f, 0.16f)), new Vector3(0.30f, 0.38f, 0.02f), new Vector3(0.40f, 0.30f, 0.34f));
            Shapes.Piece("Bag", cart, Shapes.Cube, Shapes.Paint(new Color(0.16f, 0.16f, 0.17f)), new Vector3(-0.20f, 0.78f, 0f), new Vector3(0.44f, 0.22f, 0.30f));
        }

        // hotel_awning: origin at the middle of its wall edge, at its underside; reaches 1.40 out along +Z, 2.40 wide along X, 0.30 deep at the wall.
        // It is hung only on a wall found square above the door at that height, and only if the space it takes holds nothing.
        private bool BuildAwning(Transform parent, Vector3 door, Vector3 front, Vector3 outward, Vector3 run, Color colour)
        {
            Vector3 from = new Vector3(front.x, front.y + AwningHeight + 0.2f, front.z) + outward * 1.5f;
            if (!Physics.Raycast(from, -outward, out RaycastHit wall, 9f, _wallMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            Vector3 normal = wall.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.5f || Vector3.Dot(normal.normalized, outward) < 0.9f)
            {
                return false;   // not a flat wall facing the street (a canopy, a recess, a corner)
            }
            normal.Normalize();
            // Over the door itself, not over where the pavement happened to be nearest.
            Vector3 hang = wall.point + run * Vector3.Dot(door - wall.point, run);
            hang.y = front.y + AwningHeight;
            // The same wall must be there at both ends of it.
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 probe = hang + run * (end * AwningWidth * 0.45f) + normal * 1.5f + Vector3.up * 0.2f;
                if (!Physics.Raycast(probe, -normal, out RaycastHit edge, 2.2f, _wallMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(edge.distance - 1.5f) > 0.12f)
                {
                    return false;
                }
            }
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            Vector3 centre = hang + normal * (0.08f + AwningDepth * 0.5f) + Vector3.up * 0.2f;
            var half = new Vector3(AwningWidth * 0.5f, 0.2f, AwningDepth * 0.5f - 0.06f);
            int count = Physics.OverlapBoxNonAlloc(centre, half, Touching, rotation, _solidMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Touching[i] = null;
            }
            if (count > 0)
            {
                return false;   // a lamp, a sign, a tree, a ledge: no awning here
            }
            var awning = new GameObject("Awning").transform;
            awning.SetParent(parent, false);
            awning.SetPositionAndRotation(hang, rotation);
            if (!Shapes.Prop("hotel_awning", awning))
            {
                Shapes.Piece("Canopy", awning, Shapes.Cube, Shapes.Paint(colour), new Vector3(0f, 0.19f, AwningDepth * 0.5f), new Vector3(AwningWidth, 0.07f, AwningDepth)).localRotation = Quaternion.Euler(8f, 0f, 0f);
                Shapes.Piece("Valance", awning, Shapes.Cube, Shapes.Paint(colour), new Vector3(0f, 0.02f, AwningDepth - 0.02f), new Vector3(AwningWidth, 0.22f, 0.03f));
                Shapes.Piece("Trim", awning, Shapes.Cube, Shapes.Paint(new Color(0.90f, 0.86f, 0.74f)), new Vector3(0f, -0.08f, AwningDepth - 0.02f), new Vector3(AwningWidth, 0.03f, 0.035f));
                Shapes.Piece("Rail", awning, Shapes.Cube, Shapes.Paint(new Color(0.12f, 0.12f, 0.13f)), new Vector3(0f, 0.28f, 0.03f), new Vector3(AwningWidth, 0.06f, 0.06f));
            }
            return true;
        }

        // ---------------------------------------------------------------- the doorman

        private void TickDoorman(Vector3 player, HotelSite near, int hour)
        {
            if (_doormanFailed)
            {
                return;
            }
            try
            {
                bool hours = hour >= 7;   // 07:00 to midnight
                if (_doorman != null && _doormanAt != null)
                {
                    float sqr = CitySpots.Flat(_doormanAt.DoormanSpot - player);
                    bool wanted = hours && Settings.Hotels && sqr < DoormanAway * DoormanAway && !_doorman.Lost && (near == null || near == _doormanAt);
                    if (!wanted && (_doorman.Lost || sqr > 45f * 45f || !CitySpots.InView(_doormanAt.DoormanSpot)))
                    {
                        ReleaseDoorman(false);
                        _doormanRetry = Time.time + 20f;
                    }
                    return;
                }
                if (near == null || !hours || !Settings.Hotels || Time.time < _doormanRetry)
                {
                    return;
                }
                // Put out only where he is not seen appearing: out of the camera's view, or too far off to tell.
                float away = CitySpots.Flat(near.DoormanSpot - player);
                if (CitySpots.InView(near.DoormanSpot) && away < 45f * 45f)
                {
                    return;
                }
                if (!CitySpots.ClearQuietly(near.DoormanSpot, 0.4f))
                {
                    _doormanRetry = Time.time + 15f;
                    return;
                }
                if (_doorman == null)
                {
                    _doorman = MakeDoorman(near.DoormanSpot);
                }
                _doorman.Character.gameObject.SetActive(true);
                _doorman.Reset(_props);
                _doorman.InUse = true;
                _doorman.Actor.InUse = true;
                if (!_doorman.Place(near.DoormanSpot, near.DoormanFace))
                {
                    ShelveDoorman();
                    _doormanRetry = Time.time + 30f;
                    return;
                }
                if (!_doorman.Work("guard_stand", _props))
                {
                    _doorman.Idle();   // the clip bundle is not in yet: he stands as the game's people stand
                }
                _doormanAt = near;
            }
            catch (Exception e)
            {
                _doormanFailed = true;
                _logger?.Error(e);
                _logger?.Warn("Travel: the hotel doorman is off for this session after the error above.");
                try
                {
                    ReleaseDoorman(true);
                }
                catch (Exception)
                {
                    // nothing more to do for him
                }
            }
        }

        private CityPerson MakeDoorman(Vector3 near)
        {
            if (_props == null)
            {
                _props = new CityProps(_root);
            }
            SessionWatch.Mark("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near + Vector3.down * 40f, Quaternion.identity, _root);
            character.name = "RealLifeOverhaul Hotel Doorman";
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            var person = new CityPerson(character);
            person.Look = CityLook.Doorman;
            AppearanceSetter appearance = character.appearanceSetter;
            appearance.SetRandomAge();
            appearance.RandomizeElements(Random.value < 0.7f ? Gender.Male : Gender.Female, SkinColorHelper.GetRandom(), CasualWear);
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, 0.55f);
            CityOutfits.Wear(appearance, CityLook.Doorman, person.Dyes);
            appearance.UpdateVisuals();
            character.gameObject.SetActive(false);
            return person;
        }

        private void ShelveDoorman()
        {
            if (_doorman == null)
            {
                return;
            }
            _doorman.InUse = false;
            if (!_doorman.Actor.InUse || !_doorman.Taken)
            {
                _doorman.Actor.InUse = false;
                if (_doorman.Valid)
                {
                    _doorman.Reset(_props);
                    _doorman.Character.gameObject.SetActive(false);
                }
            }
            _doormanAt = null;
        }

        private void ReleaseDoorman(bool destroy)
        {
            if (_doorman == null)
            {
                return;
            }
            bool taken = _doorman.Valid && _doorman.Taken;
            ShelveDoorman();
            if (destroy || taken || !_doorman.Valid)
            {
                // Knocked down, or the city is going: he is not reused. A casualty is the casualty system's to put away.
                if (_doorman.Valid && !taken)
                {
                    CityOutfits.Restore(_doorman.Dyes);
                    UnityEngine.Object.Destroy(_doorman.Character.gameObject);
                }
                _doorman = null;
            }
        }
    }
}
