using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Browsing the mod's garments: at the rack in a clothing store (try on, buy) or at a
    /// wardrobe at home (wear what is owned, three saved outfits). A list on the right of the
    /// screen, one section per part of the body, in two steps: the garments of the section
    /// (the interaction key opens one), then that garment's colourways (the interaction key
    /// tries one on, buys it, wears it or takes it off; the third key goes back). Moved through
    /// with the three choice keys. Trying on is done on the player themselves: the look goes on
    /// at once through the catalogue, exactly as picking it on the options page would, and
    /// whatever was tried and not bought comes off again when the list closes (walking away
    /// closes it). The player is never held.
    /// </summary>
    internal sealed class ClothesBrowser : LeisureActivity
    {
        public static readonly string[] OutfitNames =
        {
            "reallife_leisure_outfit_name_1", "reallife_leisure_outfit_name_2", "reallife_leisure_outfit_name_3",
            "reallife_leisure_outfit_name_4", "reallife_leisure_outfit_name_5", "reallife_leisure_outfit_name_6"
        };

        private const int MaxRows = 48;    // the garments of a section, or the colourways of a garment (a palette and room to grow)

        private readonly bool _wardrobe;
        private readonly Vector3 _at;
        private readonly List<CharacterCatalogue.Slot> _sections = new List<CharacterCatalogue.Slot>(5);
        private readonly int[] _rowIndex = new int[MaxRows];       // the slot's entry a row stands for (a garment row: its first colourway)
        private readonly string[] _names = new string[MaxRows];
        private readonly string[] _rights = new string[MaxRows];
        private readonly int[] _before = new int[8];
        private string[] _keysGarments;
        private string[] _keysColours;
        private int _section;
        private int _garment;      // 0: the section's garments are listed; otherwise the first entry of the garment whose colourways are
        private int _garmentRow;   // the row to come back to
        private int _rows;
        private int _lit;
        private int _first;
        private bool _bought;
        private string _foot;
        private string _title;

        public ClothesBrowser(bool wardrobe, Vector3 at)
        {
            _wardrobe = wardrobe;
            _at = at;
        }

        public override LeisureKind Kind => _wardrobe ? LeisureKind.Wardrobe : LeisureKind.Rack;
        public override string TitleKey => _wardrobe ? "reallife_leisure_wardrobe_title" : "reallife_leisure_rack_title";

        private bool OnOutfits => _wardrobe && _section == _sections.Count;
        private int SectionCount => _sections.Count + (_wardrobe ? 1 : 0);

        public override bool Begin(float now, out string refusalKey)
        {
            refusalKey = null;
            if (!Settings.CharacterNewLooks || !ModAssets.Ready)
            {
                refusalKey = "reallife_leisure_clothes_looks_off";
                return false;
            }
            LookSex sex = ClothesShop.PlayerSex();
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                if (slot.Sex == sex)
                {
                    _sections.Add(slot);
                }
            }
            if (_sections.Count == 0)
            {
                refusalKey = "reallife_leisure_clothes_looks_off";
                return false;
            }
            for (int i = 0; i < CharacterCatalogue.Slots.Length && i < _before.Length; i++)
            {
                _before[i] = CharacterCatalogue.Slots[i].Index;
            }
            ClothesShop.Trying = !_wardrobe;
            Anchor = _at;
            Leash = 3.6f;
            OwnsInteractKey = true;
            _keysGarments = new[] { T("reallife_leisure_browse_up"), T("reallife_leisure_browse_down"), T("reallife_leisure_browse_section") };
            _keysColours = new[] { _keysGarments[0], _keysGarments[1], T("reallife_leisure_browse_back") };
            Choices = _keysGarments;
            _foot = T(_wardrobe ? "reallife_leisure_wardrobe_foot" : "reallife_leisure_rack_foot");
            Show(T(_wardrobe ? "reallife_leisure_wardrobe_intro" : "reallife_leisure_rack_intro"));
            _section = 0;
            Build(true);
            return true;
        }

        public override void Tick(float now)
        {
        }

        public override void Choose(int choice, float now)
        {
            if (choice == 0)
            {
                _lit = _rows == 0 ? 0 : (_lit + _rows - 1) % _rows;
            }
            else if (choice == 1)
            {
                _lit = _rows == 0 ? 0 : (_lit + 1) % _rows;
            }
            else if (_garment > 0)
            {
                Back();
                return;
            }
            else
            {
                _section = (_section + 1) % SectionCount;
                Build(true);
                return;
            }
            Build(false);
        }

        // From a garment's colourways back to the section's garments, on the row it was opened from.
        private void Back()
        {
            _garment = 0;
            Choices = _keysGarments;
            Build(true);
            _lit = Mathf.Clamp(_garmentRow, 0, Mathf.Max(0, _rows - 1));
            Build(false);
        }

        public override void Interact(float now)
        {
            if (_rows == 0)
            {
                return;
            }
            if (OnOutfits)
            {
                Outfit(_lit);
                Build(false);
                return;
            }
            CharacterCatalogue.Slot slot = _sections[_section];
            int entry = _rowIndex[_lit];
            if (_garment == 0 && entry > 0)
            {
                // A garment: its colourways, the one worn (or the first) lit.
                _garment = entry;
                _garmentRow = _lit;
                Choices = _keysColours;
                Build(true);
                LeisureSound.Cue(LeisureCue.Tick);
                return;
            }
            string id = slot.Ids[entry];
            bool worn = slot.Index == entry;
            if (ClothesShop.Has(id))
            {
                ClothesShop.Wear(slot, worn && entry > 0 ? 0 : entry);
                LeisureSound.Cue(LeisureCue.Tick);
            }
            else if (_wardrobe)
            {
                Objective = T("reallife_leisure_wardrobe_not_owned");
                LeisureSound.Cue(LeisureCue.Bad);
            }
            else if (!worn)
            {
                ClothesShop.Wear(slot, entry);   // tried on
                LeisureSound.Cue(LeisureCue.Tick);
            }
            else
            {
                int price = ClothesShop.Price(id);
                if (!LeisureMoney.Charge(price, "reallife_transaction_clothes"))
                {
                    Objective = T("reallife_leisure_no_money");
                    LeisureSound.Cue(LeisureCue.Bad);
                }
                else
                {
                    LeisureState.Own(id);
                    if (slot.Dress)
                    {
                        LeisureState.Own(slot.LegsIds[entry]);
                    }
                    _bought = true;
                    Objective = T("reallife_leisure_rack_bought", "item", ClothesShop.NameOf(slot, entry), "price", JobText.Money(price));
                    LeisureSound.Cue(LeisureCue.Good);
                    Ctx.Logger?.Info("Leisure: bought '" + id + "' for " + price + ".");
                }
            }
            Build(false);
        }

        // The first three rows wear an outfit, the last three save what is worn now as one.
        private void Outfit(int row)
        {
            int which = row % LeisureState.Outfits;
            SavedOutfit outfit = LeisureState.Saved[which];
            if (row >= LeisureState.Outfits)
            {
                outfit.Name = outfit.Used ? (outfit.Name + 1) % OutfitNames.Length : which;
                outfit.Used = true;
                for (int i = 0; i < CharacterCatalogue.Slots.Length && i < outfit.Ids.Length; i++)
                {
                    CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[i];
                    outfit.Ids[i] = slot.Ids[slot.Index];
                }
                Objective = T("reallife_leisure_outfit_saved", "name", T(OutfitNames[outfit.Name]));
                LeisureSound.Cue(LeisureCue.Good);
                return;
            }
            if (!outfit.Used)
            {
                Objective = T("reallife_leisure_outfit_empty");
                LeisureSound.Cue(LeisureCue.Bad);
                return;
            }
            // Dresses last: a top put on after a dress would take it off again.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < CharacterCatalogue.Slots.Length && i < outfit.Ids.Length; i++)
                {
                    CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[i];
                    if (slot.Dress != (pass == 1))
                    {
                        continue;
                    }
                    int entry = Mathf.Max(0, System.Array.IndexOf(slot.Ids, outfit.Ids[i]));
                    if (!ClothesShop.Has(slot.Ids[entry]))
                    {
                        entry = 0;
                    }
                    if (pass == 1 && entry == 0 && slot.Index == 0)
                    {
                        continue;
                    }
                    CharacterCatalogue.Wear(slot, entry);
                }
            }
            Objective = T("reallife_leisure_outfit_worn", "name", T(OutfitNames[outfit.Name]));
            LeisureSound.Cue(LeisureCue.Tick);
        }

        // The rows in view: the section's garments, or one garment's colourways. Builds strings: called on a key press, never on a timer.
        private void Build(bool fresh)
        {
            _rows = 0;
            int litEntry = -1;
            if (OnOutfits)
            {
                _title = T("reallife_leisure_outfits_section");
                for (int i = 0; i < LeisureState.Outfits; i++)
                {
                    SavedOutfit outfit = LeisureState.Saved[i];
                    _names[_rows] = outfit.Used
                        ? T("reallife_leisure_outfit_wear", "name", T(OutfitNames[outfit.Name]))
                        : T("reallife_leisure_outfit_slot_empty", "number", (i + 1).ToString());
                    _rights[_rows++] = string.Empty;
                }
                for (int i = 0; i < LeisureState.Outfits; i++)
                {
                    _names[_rows] = T("reallife_leisure_outfit_save", "number", (i + 1).ToString());
                    _rights[_rows++] = string.Empty;
                }
                Prompt = T("reallife_leisure_browse_prompt_outfit");
            }
            else if (_garment == 0)
            {
                CharacterCatalogue.Slot slot = _sections[_section];
                _title = T(slot.LabelKey);
                if (_wardrobe)
                {
                    // The game's own: takes whatever of the mod's is worn in this section off.
                    _rowIndex[_rows] = 0;
                    _names[_rows] = T(CharacterCatalogue.GarmentKey(slot, 0));
                    _rights[_rows++] = slot.Index == 0 ? T("reallife_leisure_browse_wearing") : string.Empty;
                }
                int count;
                for (int first = 1; first < slot.Ids.Length && _rows < MaxRows; first += count)
                {
                    count = CharacterCatalogue.GroupCount(slot, first);
                    bool has = ClothesShop.HasAny(slot, first);
                    if (!ClothesShop.InStock(slot.Ids[first]) || (_wardrobe && !has))
                    {
                        continue;
                    }
                    bool worn = slot.Index >= first && slot.Index < first + count;
                    if (worn)
                    {
                        litEntry = first;
                    }
                    _rowIndex[_rows] = first;
                    _names[_rows] = ClothesShop.GarmentName(slot, first);
                    _rights[_rows] = worn
                        ? T(ClothesShop.Has(slot.Ids[slot.Index]) ? "reallife_leisure_browse_wearing" : "reallife_leisure_browse_trying")
                        : _wardrobe ? string.Empty : JobText.Money(ClothesShop.Price(slot.Ids[first]));
                    _rows++;
                }
            }
            else
            {
                CharacterCatalogue.Slot slot = _sections[_section];
                int count = CharacterCatalogue.GroupCount(slot, _garment);
                _title = ClothesShop.GarmentName(slot, _garment);
                for (int entry = _garment; entry < _garment + count && _rows < MaxRows; entry++)
                {
                    string id = slot.Ids[entry];
                    bool has = ClothesShop.Has(id);
                    if (!ClothesShop.InStock(id) || (_wardrobe && !has))
                    {
                        continue;
                    }
                    bool worn = slot.Index == entry;
                    if (worn)
                    {
                        litEntry = entry;
                    }
                    _rowIndex[_rows] = entry;
                    _names[_rows] = ClothesShop.ColourName(slot, entry);
                    _rights[_rows] = worn
                        ? T(has ? "reallife_leisure_browse_wearing" : "reallife_leisure_browse_trying")
                        : has
                            ? (_wardrobe ? string.Empty : T("reallife_leisure_browse_owned"))
                            : JobText.Money(ClothesShop.Price(id));
                    _rows++;
                }
            }
            if (fresh)
            {
                // A list just opened starts on what is worn.
                _lit = 0;
                _first = 0;
                for (int row = 0; row < _rows && litEntry >= 0; row++)
                {
                    if (_rowIndex[row] == litEntry)
                    {
                        _lit = row;
                    }
                }
            }
            _lit = Mathf.Clamp(_lit, 0, Mathf.Max(0, _rows - 1));
            if (_lit < _first)
            {
                _first = _lit;
            }
            else if (_lit >= _first + LeisureHud.ListRows)
            {
                _first = _lit - LeisureHud.ListRows + 1;
            }
            if (!OnOutfits)
            {
                Prompt = PromptFor();
            }
            Status = _garment > 0
                ? T("reallife_leisure_browse_status_colours", "row", (_lit + 1).ToString(), "rows", _rows.ToString())
                : T("reallife_leisure_browse_status", "section", (_section + 1).ToString(), "sections", SectionCount.ToString());
            ListDirty = true;
        }

        private string PromptFor()
        {
            if (_rows == 0)
            {
                return T(_wardrobe ? "reallife_leisure_wardrobe_nothing" : "reallife_leisure_rack_nothing");
            }
            CharacterCatalogue.Slot slot = _sections[_section];
            int entry = _rowIndex[_lit];
            if (_garment == 0 && entry > 0)
            {
                return T("reallife_leisure_browse_prompt_open");
            }
            string id = slot.Ids[entry];
            bool worn = slot.Index == entry;
            if (ClothesShop.Has(id))
            {
                return T(worn && entry > 0 ? "reallife_leisure_browse_prompt_take_off" : "reallife_leisure_browse_prompt_wear");
            }
            return worn
                ? T("reallife_leisure_browse_prompt_buy", "price", JobText.Money(ClothesShop.Price(id)), "tier", T(ClothesShop.TierKey(ClothesShop.Tier(id))))
                : T("reallife_leisure_browse_prompt_try", "price", JobText.Money(ClothesShop.Price(id)), "tier", T(ClothesShop.TierKey(ClothesShop.Tier(id))));
        }

        public override void DrawList(LeisureHud hud)
        {
            hud.ShowList(_title, _names, _rights, _first, _rows, _lit, _foot);
        }

        public override void End()
        {
            // Whatever is on and is not theirs comes off: back to what they came in wearing, if that is theirs.
            bool reverted = false;
            for (int i = 0; i < CharacterCatalogue.Slots.Length && i < _before.Length; i++)
            {
                CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[i];
                if (slot.Index > 0 && !ClothesShop.Has(slot.Ids[slot.Index]))
                {
                    CharacterCatalogue.Wear(slot, ClothesShop.Has(slot.Ids[_before[i]]) ? _before[i] : 0);
                    reverted = true;
                }
            }
            if (reverted)
            {
                // A dress that came off for a top that was only tried goes back on.
                for (int i = 0; i < CharacterCatalogue.Slots.Length && i < _before.Length; i++)
                {
                    CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[i];
                    if (slot.Dress && slot.Index == 0 && _before[i] > 0 && ClothesShop.Has(slot.Ids[_before[i]]) && !WearsSeparates(slot.Sex))
                    {
                        CharacterCatalogue.Wear(slot, _before[i]);
                    }
                }
            }
            ClothesShop.Trying = false;
            if (_bought)
            {
                ModState.AddModifier(ModState.MoodUp);
            }
        }

        // True if a top or trousers of the mod's own are on (a dress would hide them).
        private bool WearsSeparates(LookSex sex)
        {
            for (int i = 0; i < CharacterCatalogue.Slots.Length && i < _before.Length; i++)
            {
                CharacterCatalogue.Slot slot = CharacterCatalogue.Slots[i];
                if (!slot.Dress && slot.Sex == sex && (slot.Kind == LookKind.Top || slot.Kind == LookKind.Legs) && slot.Index != _before[i])
                {
                    return true;
                }
            }
            return false;
        }
    }
}
