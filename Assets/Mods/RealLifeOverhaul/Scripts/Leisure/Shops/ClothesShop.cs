using System;
using BigAmbitions.Characters;
using Buildings;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Leisure
{
    public enum ClothesTier { Everyday, Smart, Premium }

    /// <summary>
    /// The mod's wardrobe as something that is bought: what each garment costs, which of them
    /// this save owns, and the rule "clothes must be bought". The garments themselves, the
    /// player's picks and how they are drawn stay where they were (Character/CharacterCatalogue,
    /// ModLooks); this only decides what may be picked.
    /// </summary>
    internal static class ClothesShop
    {
        public const string NotOwnedKey = "reallife_look_not_owned";

        private static int _catalogueVersion = -1;
        private static int _ownedVersion = -1;

        /// <summary>A browser has something on the player that is not theirs yet: the rule waits until it is closed.</summary>
        public static bool Trying;

        // Tier and price are the catalogue's (one field each per garment and shoe style).
        public static ClothesTier Tier(string id)
        {
            switch (CharacterCatalogue.TierOf(id))
            {
                case LookTier.Premium: return ClothesTier.Premium;
                case LookTier.Smart: return ClothesTier.Smart;
                default: return ClothesTier.Everyday;
            }
        }

        public static int Price(string id)
        {
            return CharacterCatalogue.PriceOf(id);
        }

        public static string TierKey(ClothesTier tier)
        {
            switch (tier)
            {
                case ClothesTier.Premium: return "reallife_leisure_tier_premium";
                case ClothesTier.Smart: return "reallife_leisure_tier_smart";
                default: return "reallife_leisure_tier_everyday";
            }
        }

        /// <summary>Whether this save may wear a look: always while clothes need not be bought, otherwise only what was bought. The game's own (null) is always theirs.</summary>
        public static bool Has(string id)
        {
            return id == null || !LeisureState.Loaded || !LeisureState.MustBuy || LeisureState.Owns(id);
        }

        /// <summary>Whether this save may wear at least one colourway of the garment whose first entry this is.</summary>
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

        /// <summary>Whether the bundle really has this look (a row whose mesh is missing is not sold).</summary>
        public static bool InStock(string id)
        {
            return id != null && ModAssets.Ready && ModAssets.Find(id) != null;
        }

        public static LookSex PlayerSex()
        {
            CharacterData data = PlayerHelper.CharacterData;
            return data != null && data.gender == Gender.Female ? LookSex.Female : LookSex.Male;
        }

        /// <summary>An entry by name: "garment, colour" for a garment slot, the shoe's own label otherwise. Builds a string: on a key press only.</summary>
        public static string NameOf(CharacterCatalogue.Slot slot, int index)
        {
            string garment = JobText.T(CharacterCatalogue.GarmentKey(slot, index));
            string colour = CharacterCatalogue.ColourKey(slot, index);
            return colour != null ? garment + ", " + JobText.T(colour) : garment;
        }

        /// <summary>A garment or a shoe style by name, without its colourway ("Court sneakers, white and navy" is "Court sneakers").</summary>
        public static string GarmentName(CharacterCatalogue.Slot slot, int first)
        {
            string name = JobText.T(CharacterCatalogue.GarmentKey(slot, first));
            if (slot.Paired)
            {
                return name;
            }
            int cut = name.IndexOf(',');
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>A colourway by name: the palette's for a garment slot; for a shoe what stands after the comma of its label.</summary>
        public static string ColourName(CharacterCatalogue.Slot slot, int index)
        {
            string colour = CharacterCatalogue.ColourKey(slot, index);
            if (colour != null)
            {
                return JobText.T(colour);
            }
            string name = JobText.T(CharacterCatalogue.GarmentKey(slot, index));
            int cut = name.IndexOf(", ", StringComparison.Ordinal);
            return cut > 0 && cut + 2 < name.Length ? char.ToUpperInvariant(name[cut + 2]) + name.Substring(cut + 3) : name;
        }

        /// <summary>
        /// Puts a look on (or takes it off, with 0). A top or trousers take a dress off, since the dress would hide them.
        /// </summary>
        public static void Wear(CharacterCatalogue.Slot slot, int index)
        {
            CharacterCatalogue.Pick(slot, index, LookSource.Wardrobe);
        }

        /// <summary>
        /// A save read for the first time by this version: a game just begun starts with the rule on and owns what was
        /// picked for it during character creation; a game already going starts with it off, so nobody loses what they wear.
        /// </summary>
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

        /// <summary>The options toggle was moved: it speaks for the save that is loaded.</summary>
        public static void OptionChanged(bool on)
        {
            if (LeisureState.Loaded)
            {
                LeisureState.SetMustBuy(on);
            }
        }

        /// <summary>
        /// Keeps the rule: called by the director once a second, and does something only when a pick or what is owned
        /// has changed. Anything picked that is not owned comes off (the game's own goes back on) with one toast, and
        /// the options page's dropdowns name what is not owned as "not bought yet": a shoe by its colourway, a garment
        /// when no colourway of it is owned (its colours are one dropdown for all garments, which cannot say). The game
        /// builds a dropdown from the array it was given at start, so entries cannot be taken out of it: they are renamed.
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
                    // A garment slot's dropdown lists garments (entry i is garment i, whose colourways start at Ids[1 + (i - 1) * colours]).
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

        /// <summary>The dropdowns named as the catalogue named them: no save is loaded.</summary>
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

    /// <summary>
    /// The Real Life Overhaul rack in a clothing store: one prop of the mod's own, stood on a
    /// piece of the shop floor that was found clear and tested (room all round, nothing of the
    /// shop's inside its footprint, walkable to from the door), for as long as the player is in
    /// the store, and taken back when they leave. A store with no such piece of floor gets no
    /// prop: the mod's garments are then browsed at the nearest of the store's own racks.
    /// </summary>
    public sealed class ClothesRack
    {
        private static readonly Collider[] Touching = new Collider[8];

        private Transform _prop;
        private Building _for;
        private bool _tried;

        /// <summary>Where the rack is browsed: the mod's own, or one of the store's. False if the store has neither.</summary>
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

        public bool Standing => _prop != null;

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
                    || CitySpots.Flat(spot - player) < 2.5f)
                {
                    continue;
                }
                // Facing whoever comes from the door, and with nothing of the shop's inside its footprint.
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
                    solid.carving = true;   // the shop's customers go round it; over a metre of floor is left on every side
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
