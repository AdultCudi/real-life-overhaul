using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.Mods;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    internal enum LookKind { Hair = 0, Beard = 1, Top = 2, Legs = 3, Feet = 4 }

    internal enum LookSex { Any, Male, Female }

    internal enum LookTier { Everyday = 0, Smart = 1, Premium = 2 }

    /// <summary>Who changed a look: named in the log line of every change.</summary>
    internal enum LookSource { GameUi = 0, Creator = 1, Options = 2, Wardrobe = 3, Rule = 4, Load = 5 }

    /// <summary>Whose picks the catalogue holds: nobody's (the main menu), the character being created, or the loaded save's.</summary>
    internal enum LookContext { None, Creator, Save }

    /// <summary>
    /// The new looks the mod offers and which of them the player has picked.
    ///
    /// Clothes are a table of garments (<see cref="Garments"/>: one line each) and one shared table of colours
    /// (<see cref="Palette"/>: one line each); the bundle has an item "mesh_colour" for every pair. Each of the
    /// Top, Trousers and Dress choices is two dropdowns on the mod's options page: the garment (first entry "the
    /// game's own", then by tier) and its colour. Shoes are rows of their own with one dropdown.
    ///
    /// A pick is the item's id and belongs to one character: the one being created (the picks start empty and are
    /// written straight into the new game's modData) or the loaded save's (read from its modData through
    /// <see cref="ModState"/>, written back with every save). With no character (the main menu) there are no picks.
    /// PlayerPrefs holds only a mirror of the picks for the game's options page, which draws its dropdowns from
    /// there: it is written on every change and never read back as a pick.
    ///
    /// The last deliberate choice wins, per kind. With the picks the catalogue remembers which piece of the game's
    /// own lies under each kind (<see cref="Observe"/>): when the player picks another piece or colour of that kind
    /// in one of the game's own screens (character creation, a clothing store, the hairdresser), the mod's look of
    /// that kind is taken off, so the click shows. A piece the game puts on only for a while (bare skin in the
    /// shower) or one being tried in a game screen takes nothing off: the mod's look is just not drawn over it
    /// (<see cref="Shows"/>). Nothing is ever written into the game's own appearance data. An id the bundle lacks
    /// is simply not worn (the game's own piece shows).
    /// </summary>
    internal static class CharacterCatalogue
    {
        public sealed class Row
        {
            public readonly string Id;
            public readonly LookKind Kind;
            public readonly LookSex Sex;
            public readonly string LabelKey;
            public readonly string LegsId;     // a dress: its skirt, a look of its own worn in the legs' place
            public readonly string Mesh;       // the style: its colourways are the rows that share it
            public readonly LookTier Tier;
            public readonly int Price;         // in the clothing stores (Leisure's rack)

            public Row(string id, LookKind kind, LookSex sex, string labelKey, string legsId, string mesh, LookTier tier, int price)
            {
                Id = id;
                Kind = kind;
                Sex = sex;
                LabelKey = labelKey;
                LegsId = legsId;
                Mesh = mesh;
                Tier = tier;
                Price = price;
            }
        }

        /// <summary>A garment: its mesh in the bundle (worn as "mesh_colour"), what it is and its label.</summary>
        public sealed class Garment
        {
            public readonly string Mesh;
            public readonly LookKind Kind;
            public readonly LookSex Sex;
            public readonly LookTier Tier;
            public readonly int Price;         // in the clothing stores (Leisure's rack)
            public readonly string LabelKey;   // its text starts with the tier's name
            public readonly string Skirt;      // a dress: the mesh of its skirt, a look of its own worn in the legs' place

            public Garment(string mesh, LookKind kind, LookSex sex, LookTier tier, int price, string labelKey, string skirt = null)
            {
                Mesh = mesh;
                Kind = kind;
                Sex = sex;
                Tier = tier;
                Price = price;
                LabelKey = labelKey;
                Skirt = skirt;
            }
        }

        public sealed class Colour
        {
            public readonly string Id;
            public readonly string LabelKey;

            public Colour(string id, string labelKey)
            {
                Id = id;
                LabelKey = labelKey;
            }
        }

        public sealed class Slot
        {
            public readonly string OptionId;   // the options page's dropdown (a garment slot: its garment dropdown)
            public readonly string SaveKey;    // savegame modData
            public readonly string LabelKey;
            public readonly LookKind Kind;
            public readonly LookSex Sex;
            public string[] Ids;               // [0] is null: the game's own. A garment slot: every garment in every colour
            public string[] ChoiceKeys;        // the (first) dropdown's entries
            public string[] LegsIds;           // a dress slot: the skirt that goes with each entry
            public string[] Meshes;            // each entry's garment or shoe style: its colourways are neighbours
            public LookTier[] Tiers;           // each entry's tier
            public int[] Prices;               // each entry's price in the clothing stores
            public string[] Labels;            // ChoiceKeys as they were made (Leisure renames what is not owned on the page)
            public readonly bool Dress;        // its entries are bodice and skirt, and take the place of top and legs
            public int Index;                  // into Ids: what is worn

            // A garment slot is two dropdowns: the garment (0 = the game's own), then its colour.
            public readonly string ColourOptionId;
            public readonly string ColourLabelKey;
            public Garment[] Garments;         // by tier; ChoiceKeys[i + 1] is Garments[i]
            public int GarmentIndex;
            public int ColourIndex;

            public bool Paired => ColourOptionId != null;

            public Slot(string optionId, string saveKey, string labelKey, LookKind kind, LookSex sex, bool dress = false,
                string colourOptionId = null, string colourLabelKey = null)
            {
                OptionId = optionId;
                SaveKey = saveKey;
                LabelKey = labelKey;
                Kind = kind;
                Sex = sex;
                Dress = dress;
                ColourOptionId = colourOptionId;
                ColourLabelKey = colourLabelKey;
            }
        }

        private const string GameOwnKey = "reallife_look_game";

        // Shoes (CharacterAssets/scripts/build_shoes.py): three styles in four colourways each, the same for men and women.
        // Clothes (CharacterAssets/scripts/build_garments.py): Garments and Palette below.
        // Hair is not a pick: the game's own styles are finished (HairLook) and get a scalp underlay (ModAssets.FindUnderlay).
        // A look worth showing gets a line here, a slot below if its kind has none yet, and its labels in Locales/en.json.
        // Shoe colourways: the id's ending, then its label key (whole keys, so the locale check can find them).
        private static readonly string[] Court =
        {
            "white", "reallife_look_feet_court_white", "green", "reallife_look_feet_court_green",
            "black", "reallife_look_feet_court_black", "grey", "reallife_look_feet_court_grey"
        };
        private static readonly string[] Runner =
        {
            "grey", "reallife_look_feet_runner_grey", "black", "reallife_look_feet_runner_black",
            "navy", "reallife_look_feet_runner_navy", "white", "reallife_look_feet_runner_white"
        };
        private static readonly string[] Boot =
        {
            "brown", "reallife_look_feet_boot_brown", "black", "reallife_look_feet_boot_black",
            "tan", "reallife_look_feet_boot_tan", "oxblood", "reallife_look_feet_boot_oxblood"
        };
        private static readonly string[] Loafer =
        {
            "brown", "reallife_look_feet_loafer_brown", "black", "reallife_look_feet_loafer_black",
            "tan", "reallife_look_feet_loafer_tan", "oxblood", "reallife_look_feet_loafer_oxblood"
        };
        private static readonly string[] Derby =
        {
            "brown", "reallife_look_feet_derby_brown", "black", "reallife_look_feet_derby_black",
            "tan", "reallife_look_feet_derby_tan", "oxblood", "reallife_look_feet_derby_oxblood"
        };
        // ---------------------------------------------------------------- clothes: garments x colours
        //
        // A new garment is one line here (and its label, which starts with the tier's name, at the end of Locales/en.json);
        // a new colour is one line in Palette (and its label). Both follow build_garments.py: GARMENTS and PALETTE there
        // make an item "<mesh>_<colour id>" for every pair, a print a garment cannot carry falling back to a plain colour.
        // Label keys are whole literals so that the locale check finds them.
        public static readonly Garment[] Garments =
        {
            new Garment("top_m_tee", LookKind.Top, LookSex.Male, LookTier.Everyday, 35, "reallife_garment_tee_m"),
            new Garment("top_m_bomber", LookKind.Top, LookSex.Male, LookTier.Everyday, 140, "reallife_garment_bomber"),
            new Garment("legs_m_jeans", LookKind.Legs, LookSex.Male, LookTier.Everyday, 60, "reallife_garment_jeans"),
            new Garment("legs_m_chinos", LookKind.Legs, LookSex.Male, LookTier.Everyday, 55, "reallife_garment_chinos"),
            new Garment("legs_m_suit", LookKind.Legs, LookSex.Male, LookTier.Smart, 110, "reallife_garment_suit_trousers"),
            new Garment("top_f_tee", LookKind.Top, LookSex.Female, LookTier.Everyday, 35, "reallife_garment_tee_f"),
            new Garment("legs_f_jeans", LookKind.Legs, LookSex.Female, LookTier.Everyday, 60, "reallife_garment_jeans"),
            new Garment("legs_f_chinos", LookKind.Legs, LookSex.Female, LookTier.Everyday, 55, "reallife_garment_chinos"),
            new Garment("legs_f_suit", LookKind.Legs, LookSex.Female, LookTier.Smart, 110, "reallife_garment_suit_trousers"),
            new Garment("legs_f_pencil", LookKind.Legs, LookSex.Female, LookTier.Smart, 110, "reallife_garment_pencil_skirt"),
            new Garment("dress_f_summer", LookKind.Top, LookSex.Female, LookTier.Everyday, 120, "reallife_garment_dress_summer", "skirt_f_summer"),
            new Garment("dress_f_evening", LookKind.Top, LookSex.Female, LookTier.Premium, 260, "reallife_garment_dress_evening", "skirt_f_evening")
        };

        // The colours every garment comes in, in the order of the colour dropdowns. The id is the ending of the item's id.
        public static readonly Colour[] Palette =
        {
            new Colour("white", "reallife_colour_white"),
            new Colour("cream", "reallife_colour_cream"),
            new Colour("stone", "reallife_colour_stone"),
            new Colour("grey", "reallife_colour_grey"),
            new Colour("charcoal", "reallife_colour_charcoal"),
            new Colour("black", "reallife_colour_black"),
            new Colour("navy", "reallife_colour_navy"),
            new Colour("indigo", "reallife_colour_indigo"),
            new Colour("cobalt", "reallife_colour_cobalt"),
            new Colour("sky", "reallife_colour_sky"),
            new Colour("teal", "reallife_colour_teal"),
            new Colour("emerald", "reallife_colour_emerald"),
            new Colour("forest", "reallife_colour_forest"),
            new Colour("olive", "reallife_colour_olive"),
            new Colour("sage", "reallife_colour_sage"),
            new Colour("mustard", "reallife_colour_mustard"),
            new Colour("tan", "reallife_colour_tan"),
            new Colour("chocolate", "reallife_colour_chocolate"),
            new Colour("rust", "reallife_colour_rust"),
            new Colour("red", "reallife_colour_red"),
            new Colour("burgundy", "reallife_colour_burgundy"),
            new Colour("blush", "reallife_colour_blush"),
            new Colour("heather", "reallife_colour_heather"),
            new Colour("dots", "reallife_colour_dots"),
            new Colour("dotsred", "reallife_colour_dotsred"),
            new Colour("finedots", "reallife_colour_finedots"),
            new Colour("breton", "reallife_colour_breton"),
            new Colour("pinstripe", "reallife_colour_pinstripe"),
            new Colour("gingham", "reallife_colour_gingham"),
            new Colour("block", "reallife_colour_block"),
            new Colour("trim", "reallife_colour_trim"),
            new Colour("ringer", "reallife_colour_ringer")
        };

        // Colour ids of earlier versions (and the washes of the baked jeans) that saves may still name: old, new.
        private static readonly string[] ColourAliases =
        {
            "khaki", "tan", "midnight", "navy", "champagne", "cream", "yellow", "mustard", "rinse", "navy", "light", "sky"
        };

        // Whole ids of earlier versions whose colour meant something else then (the dotted summer dresses): old id, new colour.
        private static readonly string[] OldIds =
        {
            "dress_f_summer_navy", "dots", "dress_f_summer_red", "dotsred", "dress_f_summer_yellow", "finedots"
        };

        public static readonly Row[] Rows = Build(
            Colours("feet_m_court", LookKind.Feet, LookSex.Male, LookTier.Everyday, 70, Court),
            Colours("feet_m_runner", LookKind.Feet, LookSex.Male, LookTier.Smart, 95, Runner),
            Colours("feet_m_derby", LookKind.Feet, LookSex.Male, LookTier.Premium, 220, Derby),
            Colours("feet_f_court", LookKind.Feet, LookSex.Female, LookTier.Everyday, 70, Court),
            Colours("feet_f_runner", LookKind.Feet, LookSex.Female, LookTier.Smart, 95, Runner),
            Colours("feet_f_derby", LookKind.Feet, LookSex.Female, LookTier.Premium, 220, Derby),
            Colours("feet_m_boot", LookKind.Feet, LookSex.Male, LookTier.Smart, 150, Boot),
            Colours("feet_m_loafer", LookKind.Feet, LookSex.Male, LookTier.Smart, 130, Loafer),
            Colours("feet_f_boot", LookKind.Feet, LookSex.Female, LookTier.Smart, 150, Boot),
            Colours("feet_f_loafer", LookKind.Feet, LookSex.Female, LookTier.Smart, 130, Loafer));

        /// <summary>The cheapest and the dearest piece the racks sell, at today's prices (the tables are the start's).</summary>
        public static int CheapestPiece => Prices.At(PieceBound(true));
        public static int DearestPiece => Prices.At(PieceBound(false));

        private static int PieceBound(bool least)
        {
            int bound = least ? int.MaxValue : 0;
            foreach (Slot slot in Slots)
            {
                if (slot.Prices == null)
                {
                    continue;
                }
                foreach (int price in slot.Prices)
                {
                    if (price > 0)
                    {
                        bound = least ? System.Math.Min(bound, price) : System.Math.Max(bound, price);
                    }
                }
            }
            return bound == int.MaxValue ? 0 : bound;
        }

        public static readonly Slot[] Slots =
        {
            // Option id, savegame key, label key, kind, sex; a garment slot also has its colour dropdown's id and label.
            // (Savegames keep their keys and their ids. The option ids only name the page's dropdowns: what PlayerPrefs
            // holds under them is a mirror of the picks, never a pick.)
            new Slot("look2_top_m", "reallife:look_top_m", "reallife_looks_top_m", LookKind.Top, LookSex.Male, false, "look2_top_m_colour", "reallife_looks_top_m_colour"),
            new Slot("look2_legs_m", "reallife:look_legs_m", "reallife_looks_legs_m", LookKind.Legs, LookSex.Male, false, "look2_legs_m_colour", "reallife_looks_legs_m_colour"),
            new Slot("look_feet_m", "reallife:look_feet_m", "reallife_looks_feet_m", LookKind.Feet, LookSex.Male),
            new Slot("look2_dress_f", "reallife:look_dress_f", "reallife_looks_dress_f", LookKind.Top, LookSex.Female, true, "look2_dress_f_colour", "reallife_looks_dress_f_colour"),
            new Slot("look2_top_f", "reallife:look_top_f", "reallife_looks_top_f", LookKind.Top, LookSex.Female, false, "look2_top_f_colour", "reallife_looks_top_f_colour"),
            new Slot("look2_legs_f", "reallife:look_legs_f", "reallife_looks_legs_f", LookKind.Legs, LookSex.Female, false, "look2_legs_f_colour", "reallife_looks_legs_f_colour"),
            new Slot("look_feet_f", "reallife:look_feet_f", "reallife_looks_feet_f", LookKind.Feet, LookSex.Female)
        };

        // A shoe's colourways: ids "<mesh>_<colour>".
        private static Row[] Colours(string mesh, LookKind kind, LookSex sex, LookTier tier, int price, string[] colours, string skirt = null)
        {
            var rows = new Row[colours.Length / 2];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new Row(mesh + "_" + colours[i * 2], kind, sex, colours[i * 2 + 1], skirt != null ? skirt + "_" + colours[i * 2] : null, mesh, tier, price);
            }
            return rows;
        }

        private static Row[] Build(params Row[][] groups)
        {
            var rows = new List<Row>(64);
            foreach (Row[] group in groups)
            {
                rows.AddRange(group);
            }
            return rows.ToArray();
        }

        private const int KindCount = 5;

        // The game's element of each kind, in the order of LookKind.
        private static readonly AppearanceElementType[] Elements =
        {
            AppearanceElementType.Hair, AppearanceElementType.Beard, AppearanceElementType.Torso, AppearanceElementType.Legs, AppearanceElementType.Feet
        };

        // Savegame: the game's own piece each kind's pick was made over, as "variant id|colour id".
        private static readonly string[] GameKeys =
        {
            "reallife:look_game_hair", "reallife:look_game_beard", "reallife:look_game_top", "reallife:look_game_legs", "reallife:look_game_feet"
        };

        private static readonly string[] SourceNames =
        {
            "the game's own screen", "character creation", "the options page", "the rack or wardrobe", "the clothes-must-be-bought rule", "the save"
        };

        public static IModLogger Logger;

        private static string _modId;
        private static readonly string[] PaletteKeys = new string[Palette.Length];
        private static readonly bool[] HasSlots = new bool[KindCount];
        private static readonly string[] GameVariant = new string[KindCount];
        private static readonly string[] GameColour = new string[KindCount];
        private static readonly bool[] GameKnown = new bool[KindCount];
        private static AppearanceSetter _creator;
        private static bool _refusalLogged;

        /// <summary>Goes up whenever a pick changes; whoever applies the looks compares it with what it last applied.</summary>
        public static int Version { get; private set; }

        /// <summary>Whose picks these are. Picks are only taken while there is a character to wear them.</summary>
        public static LookContext Context { get; private set; }

        static CharacterCatalogue()
        {
            for (int i = 0; i < Palette.Length; i++)
            {
                PaletteKeys[i] = Palette[i].LabelKey;
            }
            var ids = new List<string>(8);
            var keys = new List<string>(8);
            var legs = new List<string>(8);
            var meshes = new List<string>(8);
            var tiers = new List<LookTier>(8);
            var prices = new List<int>(8);
            var garments = new List<Garment>(8);
            foreach (Slot slot in Slots)
            {
                ids.Clear();
                keys.Clear();
                legs.Clear();
                meshes.Clear();
                tiers.Clear();
                prices.Clear();
                garments.Clear();
                ids.Add(null);
                keys.Add(GameOwnKey);
                legs.Add(null);
                meshes.Add(null);
                tiers.Add(LookTier.Everyday);
                prices.Add(0);
                HasSlots[(int)slot.Kind] = true;
                if (slot.Dress)
                {
                    HasSlots[(int)LookKind.Legs] = true;
                }
                if (slot.Paired)
                {
                    // By tier, and within a tier as listed. Ids: every garment in every colour, garment by garment.
                    for (int tier = 0; tier <= (int)LookTier.Premium; tier++)
                    {
                        foreach (Garment garment in Garments)
                        {
                            if ((int)garment.Tier == tier && garment.Kind == slot.Kind && garment.Sex == slot.Sex && (garment.Skirt != null) == slot.Dress)
                            {
                                garments.Add(garment);
                                keys.Add(garment.LabelKey);
                                foreach (Colour colour in Palette)
                                {
                                    ids.Add(garment.Mesh + "_" + colour.Id);
                                    legs.Add(garment.Skirt != null ? garment.Skirt + "_" + colour.Id : null);
                                    meshes.Add(garment.Mesh);
                                    tiers.Add(garment.Tier);
                                    prices.Add(garment.Price);
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (Row row in Rows)
                    {
                        if (row.Kind == slot.Kind && row.Sex == slot.Sex && (row.LegsId != null) == slot.Dress)
                        {
                            ids.Add(row.Id);
                            keys.Add(row.LabelKey);
                            legs.Add(row.LegsId);
                            meshes.Add(row.Mesh);
                            tiers.Add(row.Tier);
                            prices.Add(row.Price);
                        }
                    }
                }
                slot.Ids = ids.ToArray();
                slot.ChoiceKeys = keys.ToArray();
                slot.LegsIds = legs.ToArray();
                slot.Meshes = meshes.ToArray();
                slot.Tiers = tiers.ToArray();
                slot.Prices = prices.ToArray();
                slot.Labels = (string[])slot.ChoiceKeys.Clone();
                slot.Garments = garments.ToArray();
            }
        }

        // ---------------------------------------------------------------- whose picks: nobody's, the creator's, a save's

        /// <summary>Called once at game start: there is no character yet, so there are no picks. What the options page last showed is not a pick.</summary>
        public static void Load(string modId)
        {
            _modId = modId;
            foreach (Slot slot in Slots)
            {
                if (slot.Paired)
                {
                    slot.ColourIndex = Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt(PrefsKey(slot.ColourOptionId), 0), 0, Palette.Length - 1);
                }
            }
            Leave();
        }

        /// <summary>No character any more (the main menu, a scene being left): no picks, and the options page shows "the game's own".</summary>
        public static void Leave()
        {
            Context = LookContext.None;
            _creator = null;
            Clear();
        }

        /// <summary>Only if character creation is what is being left: a save that has loaded in the meantime keeps its picks.</summary>
        public static void LeaveCreator()
        {
            if (Context == LookContext.Creator)
            {
                Leave();
            }
        }

        /// <summary>Character creation begins: a new character starts in the game's own look, whatever any other game wore.</summary>
        public static void BeginCreator()
        {
            Leave();
            Context = LookContext.Creator;
            _refusalLogged = false;
            Logger?.Info("Looks: character creation starts in the game's own look; the mod's starter garments are in the Torso, Legs and Feet grids.");
        }

        /// <summary>The character being created: the game's pieces on it are what the picks are made over.</summary>
        public static void SetCreator(AppearanceSetter character)
        {
            if (Context == LookContext.Creator)
            {
                _creator = character;
            }
        }

        private static void Clear()
        {
            foreach (Slot slot in Slots)
            {
                slot.Index = 0;
                slot.GarmentIndex = 0;
            }
            for (int kind = 0; kind < KindCount; kind++)
            {
                GameKnown[kind] = false;
                GameVariant[kind] = null;
                GameColour[kind] = null;
            }
            Version++;
            MirrorAll();
        }

        // ---------------------------------------------------------------- the options page

        /// <summary>The dropdowns for the options page. They show the picks of the character there is, and "the game's own" when there is none.</summary>
        public static ModOptions AddLooks(this ModOptions options)
        {
            foreach (Slot slot in Slots)
            {
                Slot captured = slot;
                if (slot.Paired)
                {
                    options.AddDropdown(slot.OptionId, slot.LabelKey, slot.ChoiceKeys, 0, index => GarmentPicked(captured, index));
                    options.AddDropdown(slot.ColourOptionId, slot.ColourLabelKey, PaletteKeys, slot.ColourIndex, index => ColourPicked(captured, index));
                }
                else
                {
                    options.AddDropdown(slot.OptionId, slot.LabelKey, slot.ChoiceKeys, 0, index => FromOptions(captured, index));
                }
            }
            return options;
        }

        // The page calls its dropdowns with the stored value whenever it is drawn, too: only a change counts.
        private static void GarmentPicked(Slot slot, int garment)
        {
            garment = Mathf.Clamp(garment, 0, slot.Garments.Length);
            if (garment != slot.GarmentIndex)
            {
                FromOptions(slot, Combined(garment, slot.ColourIndex));
            }
        }

        private static void ColourPicked(Slot slot, int colour)
        {
            colour = Mathf.Clamp(colour, 0, Palette.Length - 1);
            if (colour == slot.ColourIndex)
            {
                return;
            }
            if (slot.GarmentIndex > 0)
            {
                FromOptions(slot, Combined(slot.GarmentIndex, colour));
            }
            else
            {
                slot.ColourIndex = colour;   // the colour the next garment picked here comes in
                Mirror(slot);
            }
        }

        private static void FromOptions(Slot slot, int index)
        {
            index = Mathf.Clamp(index, 0, slot.Ids.Length - 1);
            if (index == slot.Index)
            {
                return;
            }
            string refusal = Context == LookContext.None ? "there is no character to wear it (pick it in character creation or in a loaded game)"
                : Context == LookContext.Creator && index > 0 && !Starter(slot, index) ? "a new character starts with everyday garments only; this one is bought in a clothing store"
                : null;
            if (refusal != null)
            {
                Mirror(slot);   // the page shows the pick there is the next time it is drawn
                if (!_refusalLogged)
                {
                    _refusalLogged = true;
                    Logger?.Info("Looks: '" + slot.Ids[index] + "' picked on the options page is not put on: " + refusal + ". (Logged once.)");
                }
                return;
            }
            Pick(slot, index, LookSource.Options);
        }

        // PlayerPrefs is where the game's options page reads what a dropdown shows (the key format is the game's).
        private static void Mirror(Slot slot)
        {
            if (_modId == null)
            {
                return;
            }
            UnityEngine.PlayerPrefs.SetInt(PrefsKey(slot.OptionId), slot.Paired ? slot.GarmentIndex : slot.Index);
            if (slot.Paired)
            {
                UnityEngine.PlayerPrefs.SetInt(PrefsKey(slot.ColourOptionId), slot.ColourIndex);
            }
        }

        private static void MirrorAll()
        {
            foreach (Slot slot in Slots)
            {
                Mirror(slot);
            }
        }

        private static string PrefsKey(string optionId) => "m:" + _modId + ":" + optionId;

        // ---------------------------------------------------------------- what is picked

        /// <summary>The id of the look picked for this kind and gender, or null for the game's own.</summary>
        public static string Selected(LookKind kind, Gender gender)
        {
            LookSex sex = gender == Gender.Female ? LookSex.Female : LookSex.Male;
            // A dress, when one is picked, is the top and the legs.
            for (int i = 0; i < Slots.Length; i++)
            {
                Slot slot = Slots[i];
                if (slot.Dress && slot.Sex == sex && slot.Index > 0 && (kind == LookKind.Top || kind == LookKind.Legs))
                {
                    return kind == LookKind.Top ? slot.Ids[slot.Index] : slot.LegsIds[slot.Index];
                }
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Kind == kind && Slots[i].Sex == sex && !Slots[i].Dress)
                {
                    return Slots[i].Ids[Slots[i].Index];
                }
            }
            return null;
        }

        /// <summary>What a new character may start in: the everyday tier. Everything else is bought (Leisure's rack).</summary>
        public static bool Starter(Slot slot, int index)
        {
            return index > 0 && index < slot.Tiers.Length && slot.Tiers[index] == LookTier.Everyday;
        }

        /// <summary>The tier of a look by its id (a skirt has its dress's); everyday for an id nobody offers.</summary>
        public static LookTier TierOf(string id)
        {
            return Locate(id, out Slot slot, out int index) ? slot.Tiers[index] : LookTier.Everyday;
        }

        /// <summary>What a look costs in a clothing store, by its id (a skirt has its dress's price: the dress is one purchase); 0 for an id nobody offers.</summary>
        public static int PriceOf(string id)
        {
            return Locate(id, out Slot slot, out int index) ? slot.Prices[index] : 0;
        }

        private static bool Locate(string id, out Slot found, out int index)
        {
            found = null;
            index = 0;
            if (id == null)
            {
                return false;
            }
            foreach (Slot slot in Slots)
            {
                index = System.Array.IndexOf(slot.Ids, id);
                if (index < 0 && slot.Dress)
                {
                    index = System.Array.IndexOf(slot.LegsIds, id);
                }
                if (index > 0)
                {
                    found = slot;
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- a slot's entries, garment by garment
        //
        // The entries of a slot are every garment (or shoe style) in every colourway, a garment's colourways side by side.

        /// <summary>The first entry of the garment this entry belongs to.</summary>
        public static int GroupFirst(Slot slot, int entry)
        {
            while (entry > 1 && slot.Meshes[entry - 1] == slot.Meshes[entry])
            {
                entry--;
            }
            return entry;
        }

        /// <summary>How many colourways the garment has whose first entry this is.</summary>
        public static int GroupCount(Slot slot, int first)
        {
            int next = first + 1;
            while (next < slot.Ids.Length && slot.Meshes[next] == slot.Meshes[first])
            {
                next++;
            }
            return next - first;
        }

        /// <summary>The label of an entry's garment (a garment slot), or of the entry itself (shoes: "style, colourway").</summary>
        public static string GarmentKey(Slot slot, int entry)
        {
            if (entry <= 0 || entry >= slot.Ids.Length)
            {
                return GameOwnKey;
            }
            return slot.Paired ? slot.Garments[(entry - 1) / Palette.Length].LabelKey : slot.Labels[entry];
        }

        /// <summary>The label of an entry's colour, for a garment slot; null for shoes, whose label names the colourway already.</summary>
        public static string ColourKey(Slot slot, int entry)
        {
            return slot.Paired && entry > 0 && entry < slot.Ids.Length ? Palette[(entry - 1) % Palette.Length].LabelKey : null;
        }

        /// <summary>
        /// The player picks a look of the mod's (the options page, the creator's grid, the rack, the wardrobe): it goes
        /// on over whatever piece of the game's own is there now. A top or trousers take a dress off, which would hide them.
        /// </summary>
        public static void Pick(Slot slot, int index, LookSource source)
        {
            index = Mathf.Clamp(index, 0, slot.Ids.Length - 1);
            Observe(null);   // first, anything the game changed that nobody has looked at yet
            if (index > 0 && !slot.Dress && (slot.Kind == LookKind.Top || slot.Kind == LookKind.Legs))
            {
                foreach (Slot other in Slots)
                {
                    if (other.Dress && other.Sex == slot.Sex && other.Index > 0)
                    {
                        Set(other, 0, source);
                    }
                }
            }
            Set(slot, index, source);
        }

        /// <summary>Puts exactly this entry on this slot and touches no other (an outfit being put on piece by piece, a tried piece coming off).</summary>
        public static void Wear(Slot slot, int index, LookSource source = LookSource.Wardrobe)
        {
            Observe(null);
            Set(slot, Mathf.Clamp(index, 0, slot.Ids.Length - 1), source);
        }

        private static int Combined(int garment, int colour) => garment == 0 ? 0 : 1 + (garment - 1) * Palette.Length + colour;

        // The one place a pick changes. Keeps a garment slot's two dropdown values in step with it.
        private static bool Set(Slot slot, int index, LookSource source, bool quiet = false)
        {
            if (index == slot.Index)
            {
                return false;
            }
            slot.Index = index;
            if (slot.Paired)
            {
                slot.GarmentIndex = index == 0 ? 0 : (index - 1) / Palette.Length + 1;
                if (index > 0)
                {
                    slot.ColourIndex = (index - 1) % Palette.Length;
                }
            }
            Version++;
            if (!quiet)
            {
                Mirror(slot);
                Persist();
                Logger?.Info("Looks: " + Name(slot) + " is now " + (index > 0 ? "the mod's '" + slot.Ids[index] + "'" : "the game's own") + " (from " + SourceNames[(int)source] + ").");
            }
            return true;
        }

        private static string Name(Slot slot)
        {
            return (slot.Dress ? "Dress" : slot.Kind.ToString()) + (slot.Sex == LookSex.Female ? " (women)" : " (men)");
        }

        // ---------------------------------------------------------------- the game's own piece under a pick

        // Whose appearance data the picks are made over: the character being created, or the save's player.
        private static CharacterData Reference()
        {
            if (Context == LookContext.Creator)
            {
                return _creator != null ? _creator.data : null;
            }
            if (Context != LookContext.Save)
            {
                return null;
            }
            var save = SaveGameManager.Current;
            return save != null && save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null;
        }

        private static void Read(CharacterData data, AppearanceElementType type, out string variant, out string colour)
        {
            variant = null;
            colour = null;
            List<AppearanceElementData> elements = data.elements;
            for (int i = 0; elements != null && i < elements.Count; i++)
            {
                if (elements[i] != null && elements[i].type == type)
                {
                    variant = string.IsNullOrEmpty(elements[i].variantId) ? null : elements[i].variantId;
                    colour = string.IsNullOrEmpty(elements[i].colorId) ? null : elements[i].colorId;
                    return;
                }
            }
        }

        /// <summary>
        /// Looks at the game's own piece of every kind on the character the picks belong to. A kind whose piece or
        /// colour is another than the one the mod's look was picked over was picked by the player in one of the game's
        /// screens: the mod's look of that kind comes off (and a dress with its top or its skirt), and the options
        /// page follows. Bare skin (the shower and the toilet take clothes off in the same data and put them back) is
        /// not a pick. Called where the game announces a change of look and before every pick; never on a timer.
        /// </summary>
        public static void Observe(AppearanceSetter seenOn, LookSource source = LookSource.GameUi)
        {
            CharacterData data = Reference();
            if (data == null)
            {
                return;
            }
            for (int kind = 0; kind < KindCount; kind++)
            {
                if (!HasSlots[kind])
                {
                    continue;
                }
                Read(data, Elements[kind], out string variant, out string colour);
                if (!GameKnown[kind])
                {
                    GameKnown[kind] = true;
                    GameVariant[kind] = variant;
                    GameColour[kind] = colour;
                    continue;
                }
                if (variant == GameVariant[kind] && colour == GameColour[kind])
                {
                    continue;
                }
                AppearanceSetter look = seenOn != null ? seenOn : Wearer();
                AppearanceElementVariant piece = Find(look, Elements[kind], variant);
                if (Context == LookContext.Save && piece != null && (piece.colors == null || piece.colors.Length == 0))
                {
                    continue;   // bare: for a while only
                }
                GameVariant[kind] = variant;
                GameColour[kind] = colour;
                string off = TakeOff((LookKind)kind, source);
                Persist();
                Logger?.Info("Looks: " + (LookKind)kind + " is now the game's own '" + (piece != null ? piece.name : variant ?? "none")
                             + "' (" + (source == LookSource.Load ? "it changed while this look was not in charge: read from " : "picked in ") + SourceNames[(int)source] + ")"
                             + (off != null ? "; the mod's '" + off + "' is taken off." : "; no look of the mod's was on it."));
            }
        }

        /// <summary>
        /// Whether the mod's look of this kind may be drawn on a character with this appearance: only over the game's
        /// piece it was picked over. Over anything else (a piece being tried in a clothing store's screen, bare skin in
        /// the shower) the game's own piece shows, and the pick waits.
        /// </summary>
        public static bool Shows(LookKind kind, CharacterData shown)
        {
            if (shown == null || !HasSlots[(int)kind] || !GameKnown[(int)kind])
            {
                return true;
            }
            Read(shown, Elements[(int)kind], out string variant, out string colour);
            return variant == GameVariant[(int)kind] && colour == GameColour[(int)kind];
        }

        // The mod's looks of this kind off, whichever sex they are for; a dress goes with its top or its skirt. Returns what came off.
        private static string TakeOff(LookKind kind, LookSource source)
        {
            string off = null;
            foreach (Slot slot in Slots)
            {
                bool covers = slot.Kind == kind || (slot.Dress && (kind == LookKind.Top || kind == LookKind.Legs));
                if (covers && slot.Index > 0)
                {
                    off = off == null ? slot.Ids[slot.Index] : off + "', '" + slot.Ids[slot.Index];
                    Set(slot, 0, source, true);
                    Mirror(slot);
                }
            }
            return off;
        }

        // A character whose pieces can be looked up by id when nobody handed one in.
        private static AppearanceSetter Wearer()
        {
            if (Context == LookContext.Creator)
            {
                return _creator;
            }
            PlayerController player = PlayerHelper.PlayerController;
            return player != null && player.Character != null ? player.Character.appearanceSetter : null;
        }

        private static AppearanceElementVariant Find(AppearanceSetter look, AppearanceElementType type, string variant)
        {
            if (look == null || variant == null)
            {
                return null;
            }
            List<AppearanceElementVariant> variants = look.GetElementVariants(type);
            for (int i = 0; variants != null && i < variants.Count; i++)
            {
                if (variants[i] != null && variants[i].id == variant)
                {
                    return variants[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- garment and colour

        private static int ColourIndex(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }
            for (int i = 0; i < Palette.Length; i++)
            {
                if (Palette[i].Id == id)
                {
                    return i;
                }
            }
            for (int i = 0; i + 1 < ColourAliases.Length; i += 2)
            {
                if (ColourAliases[i] == id)
                {
                    return ColourIndex(ColourAliases[i + 1]);
                }
            }
            return -1;
        }

        /// <summary>
        /// An item id as a garment slot's two indices: the garment with the longest mesh name the id starts with, and
        /// the rest as its colour (an alias, or the first colour if this version does not know it). False: no garment
        /// of this slot.
        /// </summary>
        private static bool Parse(Slot slot, string id, out int garment, out int colour)
        {
            garment = 0;
            colour = 0;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            int length = 0;
            for (int i = 0; i < slot.Garments.Length; i++)
            {
                string mesh = slot.Garments[i].Mesh;
                if (mesh.Length > length && id.Length > mesh.Length && id[mesh.Length] == '_' && id.StartsWith(mesh, System.StringComparison.Ordinal))
                {
                    garment = i + 1;
                    length = mesh.Length;
                }
            }
            if (garment == 0)
            {
                return false;
            }
            string name = id.Substring(length + 1);
            for (int i = 0; i + 1 < OldIds.Length; i += 2)
            {
                if (OldIds[i] == id)
                {
                    name = OldIds[i + 1];
                }
            }
            colour = Mathf.Max(0, ColourIndex(name));
            return true;
        }

        // ---------------------------------------------------------------- the savegame

        /// <summary>
        /// Takes the picks of the save that has loaded, and nothing from anywhere else: a save without them (one from
        /// before this feature, or a character created in the game's own look) wears the game's own. A new game's
        /// picks were written into it during character creation. An id this version does not know means the game's
        /// own. A kind whose piece of the game's own is no longer the one its look was picked over (the save was
        /// played without the mod, say) loses that look.
        /// </summary>
        public static void ReadSave(Dictionary<string, string> data)
        {
            Context = LookContext.Save;
            _creator = null;
            foreach (Slot slot in Slots)
            {
                int index = 0;
                if (data.TryGetValue(slot.SaveKey, out string id) && !string.IsNullOrEmpty(id))
                {
                    if (slot.Paired)
                    {
                        index = Parse(slot, id, out int garment, out int colour) ? Combined(garment, colour) : 0;
                    }
                    else
                    {
                        index = Mathf.Max(0, System.Array.IndexOf(slot.Ids, id));
                    }
                }
                slot.Index = -1;   // so that the garment and colour are worked out afresh, whatever was there
                Set(slot, index, LookSource.Load, true);
            }
            for (int kind = 0; kind < KindCount; kind++)
            {
                GameKnown[kind] = false;
                GameVariant[kind] = null;
                GameColour[kind] = null;
                if (data.TryGetValue(GameKeys[kind], out string under) && under != null)
                {
                    int cut = under.IndexOf('|');
                    if (cut >= 0)
                    {
                        GameKnown[kind] = true;
                        GameVariant[kind] = cut > 0 ? under.Substring(0, cut) : null;
                        GameColour[kind] = cut + 1 < under.Length ? under.Substring(cut + 1) : null;
                    }
                }
            }
            Version++;
            Observe(null, LookSource.Load);
            MirrorAll();
            var text = new System.Text.StringBuilder("Looks: this save wears");
            foreach (Slot slot in Slots)
            {
                if (slot.Index > 0)
                {
                    text.Append(' ').Append(Name(slot)).Append(" '").Append(slot.Ids[slot.Index]).Append("',");
                }
            }
            Logger?.Info(text.Append(" the game's own for everything else (from the save).").ToString());
        }

        public static void WriteSave(Dictionary<string, string> data)
        {
            if (Context == LookContext.Save)
            {
                Observe(null);   // so that the picks written agree with the game's pieces written beside them
                Write(data);
            }
        }

        private static void Write(Dictionary<string, string> data)
        {
            foreach (Slot slot in Slots)
            {
                data[slot.SaveKey] = slot.Ids[slot.Index] ?? "";
            }
            for (int kind = 0; kind < KindCount; kind++)
            {
                if (GameKnown[kind])
                {
                    data[GameKeys[kind]] = (GameVariant[kind] ?? "") + "|" + (GameColour[kind] ?? "");
                }
                else
                {
                    data.Remove(GameKeys[kind]);
                }
            }
        }

        // Character creation: the picks go straight into the new game's own data, so the game it starts reads them
        // like any save does and no other game ever sees them.
        private static void Persist()
        {
            if (Context != LookContext.Creator)
            {
                return;
            }
            var save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            Write(save.modData);
        }
    }
}
