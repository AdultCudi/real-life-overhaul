using Tooltip;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's tooltips on the RLO interface (Options, the pause section): every tooltip the game's TooltipSystem is given in the city is
    /// drawn as the kit's card (TooltipHost), and the money pill's own net worth card. Its own switch; the game's tooltip is kept alive
    /// and hidden under it, so Build mode's hover tip, the game's item and employee tips, Going Public's and other mods' that go through the
    /// same builder are all drawn, and the game's own tooltip is back the moment the switch is off, a fault comes or the city goes. The main
    /// menu's tooltips stay the game's. Cost: nothing while the switch is off; see <see cref="TooltipHost"/>.
    /// </summary>
    public static class TooltipKit
    {
        public const string Id = "tooltips";

        private static TooltipHost _host;
        private static string _state = "not looked at yet";
        private static bool _stood;   // the system is not built as this version expects: stood down for the city, said once

        /// <summary>Once a second while the interface is on: the host made while the switch is on and the game's system is there.</summary>
        public static void Install()
        {
            if (!UiScreenSwitches.On(Id))
            {
                Remove("its switch is off");
                return;
            }
            if (_host != null || _stood)
            {
                return;
            }
            TooltipSystem system = InstanceBehavior<TooltipSystem>.Instance;
            if (system == null)
            {
                _state = "waiting for the game's tooltip system";
                return;
            }
            string why = TooltipHost.Make(system, out TooltipHost host);
            if (why != null)
            {
                _stood = true;
                _state = "the game's own (" + why + ")";
                GameUi.UiClone.Fallback("the game's tooltips are not built as expected (" + why + "), so they stay the game's own");
                return;
            }
            _host = host;
            _state = "drawn over the game's";
        }

        /// <summary>The game's tooltips as they were, the card gone.</summary>
        public static void Remove(string why)
        {
            if (_host != null)
            {
                _host.Clear();
                _host = null;
                _state = "the game's own (" + why + ")";
            }
        }

        /// <summary>A fault in the card: the game's tooltips for the session, with one warning.</summary>
        internal static void Fault(string why)
        {
            Remove(why);
            _stood = true;
            GameUi.UiClone.Fallback("the tooltip card faulted, so the game's tooltips stay the game's own");
        }

        /// <summary>A new city: the system looked for again.</summary>
        public static void Rearm()
        {
            _stood = false;
        }

        /// <summary>The screen's state for the interface's line at load.</summary>
        public static string States() => "tooltips (" + _state + ")";

        /// <summary>True while the card is drawn over the game's tooltips, for the tests.</summary>
        internal static bool Drawn => _host != null;

        /// <summary>True while a card is on screen, for the tests.</summary>
        internal static bool Showing => _host != null && _host.Showing;

        internal static TooltipHost Host => _host;

        /// <summary>
        /// The pointer rests on the money pill: its box as fractions of the screen from the top left (so the card stands over it whatever the
        /// canvas), and the net worth card shows after a moment. False while the card is not drawn (its switch is off).
        /// </summary>
        public static bool Money(float x, float y, float w)
        {
            if (_host == null)
            {
                return false;
            }
            _host.MoneyIn(x, y, w);
            return true;
        }

        /// <summary>The pointer rests on the time pill's clock: its box as fractions of the screen, and the years and days card shows after a moment.</summary>
        public static bool Years(float x, float y, float w)
        {
            if (_host == null)
            {
                return false;
            }
            _host.YearsIn(x, y, w);
            return true;
        }

        /// <summary>The pointer rests on a held button of the bar: its box as fractions of the screen, and its reason shows in a card after a moment.</summary>
        public static bool Note(float x, float y, float w, string words)
        {
            if (_host == null)
            {
                return false;
            }
            _host.NoteIn(x, y, w, words);
            return true;
        }

        /// <summary>The pointer left the money pill or pressed it: the net worth card goes.</summary>
        public static void MoneyOut()
        {
            if (_host != null)
            {
                _host.MoneyOut();
            }
        }
    }
}
