using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using Localizor.LanguageChangeEvent;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The mod's clothes in the game's own clothes screen (ChangeCharacterClothesUI): the screen a wardrobe's "Change
    /// clothes" opens, with the player's preview in the middle, the game's categories down the side, a grid of pieces,
    /// a row of colours and Save.
    ///
    ///  - At home, what the player owns of the mod's is in the Torso, Legs and Feet grids after the game's own pieces,
    ///    in the colours bought. A click shows it on the preview; Save keeps it, and closing without saving puts back
    ///    what was worn (the game asks first, as it does for its own pieces).
    ///  - At the Real Life Overhaul rack in a clothing store the same screen opens as a shop: the mod's garments in
    ///    every colour with their prices (the game's own pieces of those kinds, which the wardrobe gives anyway, are put
    ///    away), and Save says "Buy" with the price of what is tried on and not yet owned, greyed while it cannot be
    ///    paid. Buying pays and keeps it on; closing without buying takes off what was tried.
    ///
    /// How a Save is told from a closing: the game's SaveClothes puts a new list of the player's pieces in the save (a
    /// copy of the preview's), so the list the save held when the screen opened is no longer the one it holds. Nothing
    /// of the game's is replaced or rewired: a component on the screen's object hears it open and close, the grid's
    /// tiles are GridLooks's, and the Save button has only its words and its greying changed, while the shop is open.
    ///
    /// A piece of the game's being tried in this screen covers the mod's look of that kind on the preview (the game's
    /// rule, CharacterCatalogue.Shows); picking one of the mod's again puts the game's piece of that kind on the
    /// preview back to the one the pick is made over, so the mod's look shows and a Save leaves the game's piece as it
    /// was. The screen also dresses employees in uniforms: then nothing of the mod's is offered.
    /// </summary>
    internal static class ClothesScreen
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string BuyKey = "reallife_clothes_screen_buy";

        // The kinds of the game's clothes the shop's screen opens with: everyday wear (its own pieces of the mod's three kinds are put away).
        private static readonly AppearanceTag[] Everyday = { AppearanceTag.Casual, AppearanceTag.Formal, AppearanceTag.Sport, AppearanceTag.Party };

        public static IModLogger Logger;

        private static ChangeCharacterClothesUI _ui;
        private static GridLooks _grid;
        private static Button _save;
        private static TextLocalizationComponent _saveLabel;
        private static string _saveKey;
        private static object _saveArguments;
        private static bool _labelChanged;
        private static FieldInfo _tagsField;
        private static bool _failed;
        private static bool _shopNext;
        private static GridMode _mode = GridMode.Off;
        private static int[] _before;
        private static List<AppearanceElementData> _piecesBefore;   // the save's list of the player's pieces as the screen opened
        private static readonly List<string> ToPay = new List<string>(4);

        public static bool Ready => !_failed && _ui != null && _grid != null;

        /// <summary>Once a second from the director while clothes shops are on; does something once per city.</summary>
        public static void Install()
        {
            if (_failed || (_ui != null && _grid != null))
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            ChangeCharacterClothesUI ui = uis != null ? uis.changeCharacterClothesUI : null;
            EmployeeUniformPreview preview = CharacterTools.FindPreview();
            if (ui == null || preview == null || preview.appearanceSetter == null)
            {
                return;   // the interface is not there yet: asked again in a second
            }
            try
            {
                _tagsField = typeof(CharacterCustomizer).GetField("tags", Hidden);
                _save = typeof(ChangeCharacterClothesUI).GetField("saveClothesChangesButton", Hidden)?.GetValue(ui) as Button;
                if (_tagsField == null || _save == null)
                {
                    Fail("the clothes screen is not built as this version of the mod expects");
                    return;
                }
                _saveLabel = _save.GetComponentInChildren<TextLocalizationComponent>(true);
                _grid?.Dispose();
                _grid = GridLooks.Attach(ui, preview.appearanceSetter, Logger, GridMode.Off, Choose);
                if (_grid == null)
                {
                    _failed = true;   // (GridLooks has said why)
                    return;
                }
                if (ui.GetComponent<ClothesScreenHook>() == null)
                {
                    ui.gameObject.AddComponent<ClothesScreenHook>();
                }
                _ui = ui;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Fail(e.Message);
            }
        }

        /// <summary>The city is going: what was tried is put back, and the screen is let go of.</summary>
        public static void Shutdown()
        {
            if (_mode != GridMode.Off && !GameManager.isCitySceneBeingUnloaded)
            {
                Closed();
            }
            _mode = GridMode.Off;
            ClothesShop.Trying = false;
            _grid?.Dispose();
            _grid = null;
            _ui = null;
            _save = null;
            _saveLabel = null;
            _labelChanged = false;
            _shopNext = false;
            _failed = false;
        }

        /// <summary>The rack: the clothes screen as a shop. False with a reason (a locale key) if it cannot open.</summary>
        public static bool OpenShop(out string refusalKey)
        {
            return Open(true, out refusalKey);
        }

        /// <summary>The test button: the clothes screen as the wardrobe at home opens it.</summary>
        public static bool OpenWardrobe(out string refusalKey)
        {
            return Open(false, out refusalKey);
        }

        private static bool Open(bool shop, out string refusalKey)
        {
            refusalKey = null;
            Install();
            if (!Settings.CharacterNewLooks || !ModAssets.Ready)
            {
                refusalKey = "reallife_leisure_clothes_looks_off";
                return false;
            }
            if (!Ready || CharacterTools.FindPreview() == null || _ui.gameObject.activeSelf)
            {
                refusalKey = "reallife_clothes_screen_missing";
                return false;
            }
            _shopNext = shop;
            try
            {
                _ui.Show(Everyday);   // the game's own opening: it pauses the game and holds the player, and switches the screen on a frame later
            }
            catch (Exception e)
            {
                _shopNext = false;
                Logger?.Error(e);
                refusalKey = "reallife_clothes_screen_missing";
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the screen opens and closes

        internal static void Opened()
        {
            bool shop = _shopNext;
            _shopNext = false;
            if (!Ready)
            {
                return;
            }
            try
            {
                var tags = _tagsField.GetValue(_ui) as AppearanceTag[];
                CharacterData player = Player();
                AppearanceSetter preview = _ui.appearanceSetter;
                bool uniform = tags != null && Array.IndexOf(tags, AppearanceTag.Uniform) >= 0;
                if (uniform || player == null || preview == null || preview.data == null || preview.data.name != player.name
                    || !Settings.ClothesShops || !Settings.CharacterNewLooks || !ModAssets.Ready || !LeisureState.Loaded)
                {
                    _mode = GridMode.Off;
                    _grid.Mode = GridMode.Off;
                    return;
                }
                _before = CharacterCatalogue.Snapshot();
                _piecesBefore = player.elements;
                _mode = shop ? GridMode.Shop : GridMode.Wardrobe;
                ClothesShop.Trying = shop;   // what is tried on and not owned stays on until the screen closes
                _grid.Mode = _mode;
                Logger?.Info("Clothes: the game's clothes screen is open " + (shop ? "as the shop at the rack" : "as the wardrobe") + ", with the mod's clothes in its Torso, Legs and Feet grids.");
                Price();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Fail(e.Message);
            }
        }

        internal static void Closed()
        {
            GridMode mode = _mode;
            _mode = GridMode.Off;
            if (_grid != null)
            {
                _grid.Mode = GridMode.Off;
            }
            if (mode == GridMode.Off)
            {
                return;
            }
            try
            {
                RestoreLabel();
                if (mode == GridMode.Shop)
                {
                    ClothesShop.Trying = false;
                }
                CharacterData player = Player();
                bool saved = player != null && !ReferenceEquals(player.elements, _piecesBefore);
                if (!saved)
                {
                    Undo(mode == GridMode.Shop ? "the shop closed without buying" : "the wardrobe closed without saving");
                    return;
                }
                int total = mode == GridMode.Shop ? Owed() : 0;
                if (total <= 0)
                {
                    if (CharacterCatalogue.Differs(_before))
                    {
                        Logger?.Info("Clothes: kept, from the " + (mode == GridMode.Shop ? "shop" : "wardrobe") + ".");
                    }
                    return;
                }
                if (!LeisureMoney.Charge(total, "reallife_transaction_clothes"))
                {
                    Undo("it could not be paid for");
                    JobText.Toast(NotificationType.Warning, "reallife_leisure_no_money", 6f);
                    return;
                }
                string names = string.Empty;
                for (int i = 0; i < ToPay.Count; i++)
                {
                    LeisureState.Own(ToPay[i]);
                }
                LookSex sex = ClothesShop.PlayerSex();
                foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
                {
                    if (slot.Sex == sex && slot.Index > 0 && ToPay.Contains(slot.Ids[slot.Index]))
                    {
                        names += (names.Length > 0 ? ", " : string.Empty) + ClothesShop.NameOf(slot, slot.Index);
                    }
                }
                JobText.Toast(NotificationType.Info, "reallife_clothes_screen_bought", "items", names, "price", JobText.Money(total), 8f);
                Logger?.Info("Clothes: bought " + string.Join(", ", ToPay) + " for " + total + ".");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // What was tried in the screen comes off: every pick as it was when the screen opened.
        private static void Undo(string why)
        {
            if (!CharacterCatalogue.Differs(_before))
            {
                return;
            }
            CharacterCatalogue.Restore(_before, LookSource.Wardrobe);
            Logger?.Info("Clothes: what was tried is taken off again (" + why + ").");
        }

        // ---------------------------------------------------------------- a click on one of the mod's tiles

        private static void Choose(CharacterCatalogue.Slot slot, int entry)
        {
            if (_mode == GridMode.Off || _ui == null || _ui.appearanceSetter == null)
            {
                return;
            }
            CharacterCatalogue.Pick(slot, entry, LookSource.Wardrobe);
            bool back = PutBack(slot.Dress ? LookKind.Top : slot.Kind);
            if (slot.Dress)
            {
                back |= PutBack(LookKind.Legs);
            }
            if (back)
            {
                _ui.appearanceSetter.UpdateVisuals();
            }
            if (_mode == GridMode.Wardrobe)
            {
                _save.interactable = true;   // something to save, and the game asks before it is thrown away
            }
            Price();
        }

        // The preview's piece of the game's of this kind, back to the one the save has (the one the pick is made over).
        private static bool PutBack(LookKind kind)
        {
            AppearanceElementType type = CharacterCatalogue.ElementOf(kind);
            AppearanceElementData saved = Element(Player(), type);
            AppearanceElementData tried = Element(_ui.appearanceSetter.data, type);
            if (saved == null || tried == null || (tried.variantId == saved.variantId && tried.colorId == saved.colorId))
            {
                return false;
            }
            tried.variantId = saved.variantId;
            tried.colorId = saved.colorId;
            return true;
        }

        private static AppearanceElementData Element(CharacterData data, AppearanceElementType type)
        {
            List<AppearanceElementData> elements = data != null ? data.elements : null;
            for (int i = 0; elements != null && i < elements.Count; i++)
            {
                if (elements[i] != null && elements[i].type == type)
                {
                    return elements[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the shop's Buy

        // In the shop, the Save button names what buying costs, and is greyed while the money is not there.
        private static void Price()
        {
            if (_mode != GridMode.Shop || _save == null)
            {
                return;
            }
            int total = Owed();
            if (total > 0)
            {
                SetLabel(JobText.Money(total));
                _save.interactable = LeisureMoney.CanPay(total);
                return;
            }
            RestoreLabel();
            if (CharacterCatalogue.Differs(_before))
            {
                _save.interactable = true;   // only what is owned, worn another way: a Save keeps it
            }
        }

        // What is on and not yet owned, and what it all costs (a dress is one purchase, its skirt with it).
        private static int Owed()
        {
            ToPay.Clear();
            int total = 0;
            LookSex sex = ClothesShop.PlayerSex();
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                if (slot.Sex != sex || slot.Index <= 0)
                {
                    continue;
                }
                string id = slot.Ids[slot.Index];
                if (ClothesShop.Has(id))
                {
                    continue;
                }
                ToPay.Add(id);
                if (slot.Dress)
                {
                    ToPay.Add(slot.LegsIds[slot.Index]);
                }
                total += ClothesShop.Price(id);
            }
            return total;
        }

        private static void SetLabel(string price)
        {
            if (_saveLabel == null)
            {
                return;
            }
            if (!_labelChanged)
            {
                _saveKey = _saveLabel.Key;
                _saveArguments = _saveLabel.Arguments;
                _labelChanged = true;
            }
            _saveLabel.Arguments = new Dictionary<string, string>(1) { { "price", price } };   // (read by the game as it is)
            _saveLabel.Key = BuyKey;
        }

        private static void RestoreLabel()
        {
            if (!_labelChanged || _saveLabel == null)
            {
                return;
            }
            _labelChanged = false;
            _saveLabel.Arguments = _saveArguments;
            _saveLabel.Key = _saveKey;
        }

        // ---------------------------------------------------------------- helpers

        private static CharacterData Player()
        {
            var save = SaveGameManager.Current;
            return save != null && save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null;
        }

        private static void Fail(string why)
        {
            if (_failed)
            {
                return;
            }
            _failed = true;
            Logger?.Warn("Clothes: the mod's clothes are left out of the game's clothes screen for this session (" + why + "); the options page still has them.");
        }
    }

    /// <summary>On the game's clothes screen: tells <see cref="ClothesScreen"/> when it opens and closes. It has no Update.</summary>
    public sealed class ClothesScreenHook : MonoBehaviour
    {
        private void OnEnable()
        {
            ClothesScreen.Opened();
        }

        private void OnDisable()
        {
            ClothesScreen.Closed();
        }
    }
}
