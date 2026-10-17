using System;
using BigAmbitions.Characters;
using Buildings;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>The mod's garments as something bought: prices, what this save owns, and the buying rule.</summary>
    internal static class ClothesShop
    {
        public const string NotOwnedKey = "reallife_look_not_owned";

        private static int _catalogueVersion = -1;
        private static int _ownedVersion = -1;

        /// <summary>Something not owned is being tried on: the rule waits until the screen closes.</summary>
        public static bool Trying;

        public static int Price(string id)
        {
            return Prices.At(CharacterCatalogue.PriceOf(id));   // the catalogue's prices are the start's
        }

        /// <summary>Whether this save may wear a look: anything unless clothes must be bought.</summary>
        public static bool Has(string id)
        {
            return id == null || !LeisureState.Loaded || !LeisureState.MustBuy || LeisureState.Owns(id);
        }

        /// <summary>Whether this save may wear any colourway of the garment from <paramref name="first"/>.</summary>
        public static bool HasAny(CharacterCatalogue.Slot slot, int first)
        {
            int count = CharacterCatalogue.GroupCount(slot, first);
            for (int i = 0; i < count; i++)
            {
                if (Has(slot.Ids[first + i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether the bundle has this look; a row whose mesh is missing is not sold.</summary>
        public static bool InStock(string id)
        {
            return id != null && ModAssets.Ready && ModAssets.Find(id) != null;
        }

        public static LookSex PlayerSex()
        {
            CharacterData data = PlayerHelper.CharacterData;
            return data != null && data.gender == Gender.Female ? LookSex.Female : LookSex.Male;
        }

        /// <summary>"Garment, colour" for a garment slot, the shoe's own label otherwise. Builds a string.</summary>
        public static string NameOf(CharacterCatalogue.Slot slot, int index)
        {
            string garment = JobText.T(CharacterCatalogue.GarmentKey(slot, index));
            string colour = CharacterCatalogue.ColourKey(slot, index);
            return colour != null ? garment + ", " + JobText.T(colour) : garment;
        }

        /// <summary>A new game starts with the rule on; an old one off, so nobody loses what they wear.</summary>
        public static void FirstLoad(bool newGame)
        {
            LeisureState.SetMustBuy(newGame);
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                if (slot.Index > 0)
                {
                    LeisureState.Own(slot.Ids[slot.Index]);
                    if (slot.Dress)
                    {
                        LeisureState.Own(slot.LegsIds[slot.Index]);
                    }
                }
            }
            Settings.SetClothesMustBeBought(LeisureState.MustBuy);
        }

        public static void OptionChanged(bool on)
        {
            if (LeisureState.Loaded)
            {
                LeisureState.SetMustBuy(on);
            }
        }

        /// <summary>
        /// Takes off what is not owned whenever picks or purchases change. The options page's dropdowns are built once,
        /// so what is not owned is renamed there rather than removed.
        /// </summary>
        public static void Enforce()
        {
            if (Trying || (CharacterCatalogue.Version == _catalogueVersion && LeisureState.OwnedVersion == _ownedVersion))
            {
                return;
            }
            bool refused = false;
            for (int s = 0; s < CharacterCatalogue.Slots.Length; s++)
            {
                CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[s];
                if (slot.Index > 0 && !Has(slot.Ids[slot.Index]))
                {
                    CharacterCatalogue.Wear(slot, 0, LookSource.Rule);
                    refused = true;
                }
                for (int i = 1; i < slot.ChoiceKeys.Length; i++)
                {
                    // A garment slot's entry i is garment i, whose colourways start at Ids[1 + (i - 1) * colours].
                    bool has = slot.Paired ? HasAny(slot, 1 + (i - 1) * CharacterCatalogue.Palette.Length) : Has(slot.Ids[i]);
                    slot.ChoiceKeys[i] = has ? slot.Labels[i] : NotOwnedKey;
                }
            }
            _catalogueVersion = CharacterCatalogue.Version;
            _ownedVersion = LeisureState.OwnedVersion;
            if (refused)
            {
                JobText.Toast(UI.Notification.NotificationType.Warning, "reallife_leisure_clothes_not_owned", 8f);
            }
        }

        public static void RestoreLabels()
        {
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                Array.Copy(slot.Labels, slot.ChoiceKeys, slot.Labels.Length);
            }
            _catalogueVersion = -1;
            _ownedVersion = -1;
        }
    }

    /// <summary>The mod's rack on a clear piece of a clothing store's floor, else the store's own racks.</summary>
    public sealed class ClothesRack
    {
        private static readonly Collider[] Touching = new Collider[8];

        private Transform _prop;
        private Building _for;
        private bool _tried;

        /// <summary>Where the rack is browsed: the mod's own, or one of the store's; false if it has neither.</summary>
        public bool Find(Vector3 player, out Vector3 point)
        {
            if (_prop != null)
            {
                point = _prop.position;
                return true;
            }
            ItemController rack = LeisureVenues.Nearest(LeisureVenues.Racks, player, 60f);
            point = rack != null ? rack.transform.position : default;
            return rack != null;
        }

        /// <summary>Once a second while the player is in a clothing store.</summary>
        public void Tick(BuildingManager manager, LeisureProps props, Vector3 player)
        {
            if (!ReferenceEquals(manager.building, _for))
            {
                TakeIn(props);
                _for = manager.building;
            }
            if (_tried || manager.enteringBuilding || manager.exitingBuilding || !Shapes.Available)
            {
                return;
            }
            _tried = true;
            for (int attempt = 0; attempt < 8 && _prop == null; attempt++)
            {
                if (!JobSites.FloorSpot(player, null, 0, 0f, out Vector3 spot)
                    || !NavMesh.FindClosestEdge(spot, out NavMeshHit edge, NavMesh.AllAreas) || edge.distance < 1.1f
                    || LeisureReach.Within(CitySpots.Flat(spot - player), LeisureReach.RackAway))
                {
                    continue;
                }
                // Facing whoever comes from the door, with nothing of the shop's inside its footprint.
                Vector3 to = player - spot;
                to.y = 0f;
                Quaternion facing = Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward);
                int count = Physics.OverlapBoxNonAlloc(spot + Vector3.up * 0.95f, new Vector3(0.80f, 0.75f, 0.36f), Touching, facing, ~0, QueryTriggerInteraction.Ignore);
                bool clear = true;
                for (int i = 0; i < count; i++)
                {
                    Collider touched = Touching[i];
                    Touching[i] = null;
                    if (touched != null && touched.GetComponentInParent<PlayerController>() == null)
                    {
                        clear = false;
                    }
                }
                if (!clear)
                {
                    continue;
                }
                Transform prop = props.Take("clothes_rack");
                if (prop == null)
                {
                    return;
                }
                prop.SetPositionAndRotation(spot, facing);
                if (!prop.TryGetComponent(out NavMeshObstacle solid))
                {
                    solid = prop.gameObject.AddComponent<NavMeshObstacle>();
                    solid.shape = NavMeshObstacleShape.Box;
                    solid.center = new Vector3(0f, 0.8f, 0f);
                    solid.size = new Vector3(1.5f, 1.6f, 0.6f);
                    solid.carving = true;   // customers go round it; over a metre of floor is left on every side
                }
                MeshCuts.Note("a clothes rack on a shop floor");
                _prop = prop;
                LeisureDirector.Logger?.Info("Leisure: the rack stands on the shop floor, " + edge.distance.ToString("F1") + " m from the nearest fitting.");
            }
            if (_prop == null)
            {
                LeisureDirector.Logger?.Info("Leisure: this store has no clear piece of floor for the rack; its own racks are used (" + LeisureVenues.Racks.Count + ").");
            }
        }

        public void TakeIn(LeisureProps props)
        {
            if (_prop != null)
            {
                props.Return(_prop);
                _prop = null;
            }
            _for = null;
            _tried = false;
        }
    }
}
