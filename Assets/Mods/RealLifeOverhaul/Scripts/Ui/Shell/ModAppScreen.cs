using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// An app of the mod's own drawn by the kit (PhoneAppDef.Kit), as the host sees it: its panel among the full menu's
    /// apps is the object watched, its own children (the page as the mod built it) are the leaves hidden while the kit
    /// draws, and the panel's group is left to the game's SelectApp, which fades it. Game side; nothing a frame.
    /// </summary>
    public sealed class ModAppScreen : IUiGameScreen
    {
        private readonly string _panel, _screen, _titleKey;
        private readonly UiIcon _icon;
        private readonly Func<GameObject> _root;
        private readonly Func<IUiScreen> _make;

        public ModAppScreen(string panel, string screen, string titleKey, UiIcon icon, Func<GameObject> root, Func<IUiScreen> make)
        {
            _panel = panel;
            _screen = screen;
            _titleKey = titleKey;
            _icon = icon;
            _root = root;
            _make = make;
        }

        public string Id => _screen;

        public string Name => "the app " + _panel;

        public string Title => UiText.T(_titleKey);

        public UiIcon Icon => _icon;

        public UiGameKind Kind => UiGameKind.App;

        public Component Find()
        {
            GameObject root = _root();
            return root != null ? root.transform : null;
        }

        public string Check(Component found) => null;

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform root = found.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                conceal.Add(root.GetChild(i));
            }
        }

        public IUiScreen Make(Component found) => _make();
    }
}
