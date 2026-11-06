using System;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Characters;
using Helpers;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A hotel's sign, awning and luggage cart, each stood only where a real wall and clear pavement were measured for it;
    /// and the city's one doorman, 07:00 to midnight at the better hotels, put out and taken in only out of view.
    /// </summary>
    public sealed partial class Hotels
    {
        private const string CharacterPrefab = "Characters/HumanDefinitionLow";

        // Distances in metres.
        private const float DoormanWithin = 70f;
        private const float DoormanAway = 130f;
        private const float AwningHeight = 2.75f;
        private const float AwningDepth = 1.4f;
        private const float AwningWidth = 2.4f;

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };
        private static readonly Collider[] Touching = new Collider[4];

        private CityProps _props;
        private CityPerson _doorman;
        private HotelSite _doormanAt;
        private float _doormanRetry;
        private bool _doormanFailed;
        private int _wallMask;
        private int _solidMask;

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
                if (!Shapes.Available)
                {
                    return;
                }
                var dressing = new GameObject("RealLifeOverhaul Hotel " + site.Slot.Id).transform;
                dressing.SetParent(_root, false);
                site.Dressing = dressing;
                Color colour = TierColour(site.Slot.Tier);
                int placed = 0;

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
                        site.Desk = beside;   // no wall for a board: guests are met on the pavement
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

        // hotel_sign stands on the ground at its middle, facing +Z, 0.70 wide, 1.90 tall, 0.16 deep. Its name plate is
        // 0.60 x 0.34 centred 1.50 m up, its face at z 0.080: the name is lettered 5 mm proud of it, inside a 2 cm margin.
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
            TMP_FontAsset font = GameUi.GameFont.Asset;
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

        // luggage_cart stands on the ground at its middle, its long side along X: 1.10 x 0.60 on the ground, 1.75 tall.
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

        // hotel_awning has its origin at the middle of its wall edge, at its underside, and reaches 1.40 out along +Z, 2.40
        // wide along X, 0.30 deep at the wall. It is hung only on a wall square above the door, in a space that holds nothing.
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
                Transform canopy = Shapes.Piece("Canopy", awning, Shapes.Cube, Shapes.Paint(colour), new Vector3(0f, 0.19f, AwningDepth * 0.5f), new Vector3(AwningWidth, 0.07f, AwningDepth));
                canopy.localRotation = Quaternion.Euler(8f, 0f, 0f);
                Shapes.Piece("Valance", awning, Shapes.Cube, Shapes.Paint(colour), new Vector3(0f, 0.02f, AwningDepth - 0.02f), new Vector3(AwningWidth, 0.22f, 0.03f));
                Shapes.Piece("Trim", awning, Shapes.Cube, Shapes.Paint(new Color(0.90f, 0.86f, 0.74f)), new Vector3(0f, -0.08f, AwningDepth - 0.02f), new Vector3(AwningWidth, 0.03f, 0.035f));
                Shapes.Piece("Rail", awning, Shapes.Cube, Shapes.Paint(new Color(0.12f, 0.12f, 0.13f)), new Vector3(0f, 0.28f, 0.03f), new Vector3(AwningWidth, 0.06f, 0.06f));
            }
            return true;
        }

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
                    _doorman.Idle();   // without the clip bundle he stands as the game's people do
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

        /// <summary>
        /// Behind the loading screen (Core/ScenePrewarm): the doorman made and shelved, so he is not made in play the first
        /// time a hotel's door is near (a prefab and a mesh merge). Made again in play only after he is knocked down.
        /// </summary>
        public bool PrewarmDoorman()
        {
            if (!Settings.Hotels || _doormanFailed || _doorman != null)
            {
                return true;
            }
            PlayerController player = PlayerHelper.PlayerController;
            _doorman = MakeDoorman(player != null ? player.transform.position : Vector3.zero);
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Travel check (the hotel doorman made ahead): made behind the loading screen, not in play: PASS");
            }
            return true;
        }

        private CityPerson MakeDoorman(Vector3 near)
        {
            if (_props == null)
            {
                _props = new CityProps(_root);
            }
            using var piece = SessionWatch.Piece("people: a character being made");
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
            Looks.Count("travel: the hotel doorman's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
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
