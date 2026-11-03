using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// One switch for each screen on the RLO interface, so a player who meets a broken screen turns just that one back to
    /// the game's own while the rest stay on the kit. A screen asks for one in its entry (UiScreenDef.Switch, the one line a
    /// job adds); its option id is ui_screen_ and the screen's id, its name the label prefix and the id, and its tip
    /// the options page's tip prefix and the option id. Each stands in Mod settings under one of the RLO interface's
    /// sections, on by default, needing the interface's own switch. The settings read them through <see cref="Load"/>,
    /// the page sets them through <see cref="Set"/>, and a screen asks <see cref="On"/>. Game-free; checked offline
    /// (EconomyCheck: every runner has its switch, every switch its texts). Cost: an array, read once a second at most.
    /// </summary>
    public static class UiScreenSwitches
    {
        private const string Mod = "reallife";               // apart, so that the locale check does not take a prefix for a key
        public const string OptionPrefix = "ui_screen_";
        public const string LabelPrefix = Mod + "_ui_screen_";

        /// <summary>The sections of Mod settings the switches stand in, each under the RLO interface's section, each at most ten rows.</summary>
        public const string Apps = "uiscreens_apps";      // phone apps and the overhaul's pages
        public const string Business = "uiscreens_business";   // the apps a business is run from (BizMan)
        public const string Boxes = "uiscreens_boxes";    // the boxes and the notices every screen uses
        public const string Help = "uiscreens_help";      // Mod settings, the Guide, Staff and roles, the bug report and the save tools
        public const string Market = "uiscreens_market";  // the player's own profile, the rivals and the districts' market (the game's Persona, Rivals, Market Insider)
        public const string Hud = "uiscreens_hud";        // the HUD over the running game and its pieces
        public const string Activity = "uiscreens_activity";   // what the player does over time: an activity's card and strip, the time skip, the funeral, the time of day
        public const string Buy = "uiscreens_buy";        // buying and what the player holds: the checkout, a vehicle, the item panel, cargo, clothes, the garage
        public const string Tools = "uiscreens_tools";    // the world's smaller windows: the private driver, the items list, a sign's look and the like

        /// <summary>Every section, in the page's order; a section with no switch yet is left off the page.</summary>
        public static readonly string[] Sections = { Apps, Business, Market, Boxes, Hud, Tools, Activity, Buy, Help };

        private static readonly UiScreenDef[] Switched = Gather();
        private static readonly bool[] Values = Fill(Switched.Length);

        /// <summary>Told when a switch changes, so the screen goes back to the game's own (or comes back) at once.</summary>
        public static Action Changed;

        /// <summary>The screens that have a switch, in the registry's order.</summary>
        public static UiScreenDef[] All => Switched;

        /// <summary>A screen's option id.</summary>
        public static string OptionOf(string screenId) => OptionPrefix + screenId;

        /// <summary>A screen's switch label key.</summary>
        public static string LabelOf(string screenId) => LabelPrefix + screenId;

        /// <summary>True unless the screen has a switch and it is off.</summary>
        public static bool On(string screenId)
        {
            int at = IndexOf(screenId);
            return at < 0 || Values[at];
        }

        /// <summary>Sets a screen's switch by its option id (the options page's own); a change is told.</summary>
        public static void Set(string optionId, bool on)
        {
            for (int i = 0; i < Switched.Length; i++)
            {
                if (OptionOf(Switched[i].Id) == optionId && Values[i] != on)
                {
                    Values[i] = on;
                    Changed?.Invoke();
                    return;
                }
            }
        }

        /// <summary>Reads every switch from the saved options (the settings' reader, with the default on).</summary>
        public static void Load(Func<string, bool, bool> read)
        {
            for (int i = 0; i < Switched.Length; i++)
            {
                Values[i] = read(OptionOf(Switched[i].Id), Values[i]);
            }
        }

        /// <summary>A switch's value by its option id, for the page's own row.</summary>
        public static bool Value(string optionId)
        {
            for (int i = 0; i < Switched.Length; i++)
            {
                if (OptionOf(Switched[i].Id) == optionId)
                {
                    return Values[i];
                }
            }
            return true;
        }

        /// <summary>Every switch's option id, for the feature that owns them.</summary>
        public static string[] OptionIds()
        {
            var ids = new string[Switched.Length];
            for (int i = 0; i < Switched.Length; i++)
            {
                ids[i] = OptionOf(Switched[i].Id);
            }
            return ids;
        }

        /// <summary>The option ids of one section's switches, in order.</summary>
        public static string[] OptionIds(string section)
        {
            var ids = new List<string>(Switched.Length);
            for (int i = 0; i < Switched.Length; i++)
            {
                if (Switched[i].Section == section)
                {
                    ids.Add(OptionOf(Switched[i].Id));
                }
            }
            return ids.ToArray();
        }

        private static int IndexOf(string screenId)
        {
            for (int i = 0; i < Switched.Length; i++)
            {
                if (Switched[i].Id == screenId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static UiScreenDef[] Gather()
        {
            var list = new List<UiScreenDef>(16);
            foreach (UiScreenDef def in UiScreens.All)
            {
                if (def.Section != null)
                {
                    list.Add(def);
                }
            }
            return list.ToArray();
        }

        private static bool[] Fill(int count)
        {
            var on = new bool[count];
            for (int i = 0; i < count; i++)
            {
                on[i] = true;
            }
            return on;
        }
    }
}
