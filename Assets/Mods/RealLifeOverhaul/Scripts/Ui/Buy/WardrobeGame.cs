using System;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Which of the game's three character screens a wardrobe host draws over.</summary>
    internal enum WardrobeKind : byte
    {
        Clothes = 0,   // the clothes shop and the wardrobe at home (ChangeCharacterClothesUI)
        Hair,          // the hairdresser (ChangeCharacterHairUI)
        Surgery        // the surgeon (PlasticSurgeryUI)
    }

    /// <summary>
    /// One of the game's character screens as the host binds it: the side panels drawn by the kit, the 3D preview and its
    /// camera the game's own. The game's categories, pieces (the mod's own tiles from GridLooks among them, read from the
    /// bands it adds), colours, the surgeon's body sliders and the buttons are pressed as the player would; Leave asks the
    /// game's own question when something is not kept.
    /// </summary>
    internal sealed class WardrobeGame : IUiGameScreen, IUiGameWay
    {
        private readonly WardrobeKind _kind;

        public WardrobeGame(WardrobeKind kind)
        {
            _kind = kind;
        }

        public string Id => BuyKits.WardrobeId;

        public string Name => _kind == WardrobeKind.Clothes ? "the clothes screen" : _kind == WardrobeKind.Hair ? "the hairdresser" : "the surgeon";

        public string Title => WardrobeScreen.TitleOf(_kind);

        public UiIcon Icon => UiIcon.Person;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => true;   // the tabs take Q and E, and Cancel leaves through the kit alone

        public bool Blocks => false;     // the preview turns by the pointer: the kit takes only its own clicks

        public Component Find() => Ui(_kind);

        public string Check(Component found) => BuyKits.Get<MenuVertical>(GameContract.LookMenu, found) == null || BuyKits.Get<ElementPicker>(GameContract.LookPicker, found) == null
            ? "its menu or its pieces are not there" : null;

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform root = found.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                conceal.Add(root.GetChild(i));
            }
            conceal.Add(root.GetComponent<Graphic>());
        }

        public IUiScreen Make(Component found) => new WardrobeScreen(_kind, (CharacterCustomizer)found);

        internal static CharacterCustomizer Ui(WardrobeKind kind)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null)
            {
                return null;
            }
            return kind == WardrobeKind.Clothes ? uis.changeCharacterClothesUI : kind == WardrobeKind.Hair ? (CharacterCustomizer)uis.changeCharacterHairUI : uis.plasticSurgeryUI;
        }
    }
}
