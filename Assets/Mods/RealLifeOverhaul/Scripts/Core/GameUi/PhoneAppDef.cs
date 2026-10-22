using System;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What an app on the phone is made of.</summary>
    public sealed class PhoneAppDef
    {
        /// <summary>The name of its objects in the game's interface. The game opens an app by its panel's name, so it must be no AppName.</summary>
        public string Id;
        public string TitleKey;
        /// <summary>What the title's names ("{bank}") are filled with, as TextLocalizationComponent.Arguments takes them; null for none.</summary>
        public object TitleArguments;
        /// <summary>Given one of the game's app icons to model its picture on; null keeps the template's.</summary>
        public Func<Sprite, Sprite> Icon;
        /// <summary>Builds the app's panel asleep under the holder it is given, from the game's own (AppShell); null if it cannot.</summary>
        public Func<FullMenu, Transform, GameObject> Build;
        public Action Opened;
        public Action Closed;
        /// <summary>An app the mod opens itself (a newspaper picked up, a counter's page): a panel among the game's, no buttons.</summary>
        public bool NoButtons;
    }
}
