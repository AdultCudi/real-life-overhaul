using System;
using BigAmbitions.Items;
using TMPro;
using Tooltip;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The designer's tooltips, raised through the game's own TooltipSystem as the game raises them (a TooltipTarget's pointer enter, a short delay, then its
    /// labels): a furniture tile's is the game's ItemInfoTooltip for that item, word for word, and a tool or a step on the bar says its name the same way. The
    /// kit's tooltip card draws whatever the TooltipSystem builds, so this draws nothing of its own. The two carriers stand on one object under the kit's own
    /// root, which is on while the designer shows and off after it, and a carrier hides its tooltip as it goes off. Cost: nothing while the mouse rests where
    /// there is no tooltip; a handful of calls as it comes to one.
    /// </summary>
    internal static class DesignerTips
    {
        private static readonly FeatureGuard TipGuard = Guard.For("ui", "DesignerTips", typeof(DesignerTips));   // the game's tooltip asks it
        private static GameObject _carrier;
        private static ItemInfoTooltip _item;
        private static NameTip _name;
        private static bool _on;

        /// <summary>The game's tooltip for an item, as its own tile in the furniture panel shows it.</summary>
        public static void Item(Transform under, Item item)
        {
            if (item == null || !Make(under))
            {
                return;
            }
            Off();
            _item.targetItem = item;
            _item.OnPointerEnter(null);
            _on = true;
        }

        /// <summary>A tool's or a step's name as a tooltip.</summary>
        public static void Words(Transform under, string words)
        {
            if (string.IsNullOrEmpty(words) || !Make(under))
            {
                return;
            }
            Off();
            _name.Words = words;
            _name.OnPointerEnter(null);
            _on = true;
        }

        /// <summary>The tooltip taken away: the mouse left what raised it, or the panel went.</summary>
        public static void Off()
        {
            if (!_on)
            {
                return;
            }
            _on = false;
            if (_item != null)
            {
                _item.OnPointerExit(null);
            }
            if (_name != null)
            {
                _name.OnPointerExit(null);
            }
        }

        /// <summary>The carriers given up with the kit's root: a new city, or the interface off.</summary>
        public static void Forget()
        {
            Off();
            if (_carrier != null)
            {
                UnityEngine.Object.Destroy(_carrier);
            }
            _carrier = null;
            _item = null;
            _name = null;
        }

        private static bool Make(Transform under)
        {
            if (_carrier != null)
            {
                return _item != null && _name != null;
            }
            if (under == null)
            {
                return false;
            }
            _carrier = new GameObject("RealLife designer tooltips", typeof(RectTransform));
            _carrier.transform.SetParent(under, false);
            _item = _carrier.AddComponent<ItemInfoTooltip>();
            _name = _carrier.AddComponent<NameTip>();
            return true;
        }

        /// <summary>A tooltip of one label, bold and centred, as the game's own for an item opens.</summary>
        private sealed class NameTip : TooltipTarget
        {
            public string Words;

            protected override void ShowTooltip()
            {
                try
                {
                    if (!TipGuard.Off)
                    {
                        TooltipSystem.AddLabel(Words, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                    }
                }
                catch (Exception e)
                {
                    Guard.Fail(TipGuard, e);
                }
            }
        }
    }
}
