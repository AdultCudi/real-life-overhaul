using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.Mods;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The kit's and phone's opening calls.</summary>
    internal static class SelfTestUi
    {
        public sealed class Opening
        {
            public bool Shown;      // the kit screen shows
            public bool Opener;     // something that opens it was run
            public string How;      // what opened it, in words
            public string Why;      // why nothing could open it
        }

        /// <summary>True when an entry's name passes the plan's only and skip lists (comma separated).</summary>
        public static bool Wanted(string name, string only, string skip)
        {
            return (string.IsNullOrEmpty(only) || InList(only, name)) && (string.IsNullOrEmpty(skip) || !InList(skip, name));
        }

        private static bool InList(string list, string name)
        {
            foreach (string part in list.Split(','))
            {
                if (string.Equals(part.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The kit screen a game app is drawn by, or null.</summary>
        public static string KitOfGameApp(string app)
        {
            for (int i = 0; i < SelfTestScreenTable.Apps.Length; i++)
            {
                if (string.Equals(SelfTestScreenTable.Apps[i][1], app, StringComparison.OrdinalIgnoreCase))
                {
                    return SelfTestScreenTable.Apps[i][0];
                }
            }
            return null;
        }

        /// <summary>Opens an app by the game's name or the mod's app id.</summary>
        public static bool OpenApp(string app)
        {
            return SelfTestGame.OpenGameApp(app) || OpenModApp(app);
        }

        private static bool OpenModApp(string id) => PhoneApp.OpenById(id);

        /// <summary>
        /// The registry's ids in the order open_every_screen walks them: screens an app opens first (the plain windows), then those a test
        /// button opens, then the situations (a door's prompt, pickers, build mode, the world's boxes), each group in the registry's order.
        /// </summary>
        public static List<string> Ordered(UiScreenDef[] all)
        {
            var ranked = new List<KeyValuePair<int, string>>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                bool app = SelfTestScreenTable.AppOf(all[i].Id) != null || ModAppOf(all[i].Id) != null;
                ranked.Add(new KeyValuePair<int, string>(SelfTestScreenTable.Rank(all[i].Id, app), all[i].Id));
            }
            var order = new List<string>(all.Length);
            for (int rank = 0; rank <= 2; rank++)
            {
                for (int i = 0; i < ranked.Count; i++)
                {
                    if (ranked[i].Key == rank)
                    {
                        order.Add(ranked[i].Value);
                    }
                }
            }
            return order;
        }

        /// <summary>The screen opened by the way that fits it, then settled; fills what was done and whether the kit shows it.</summary>
        public static IEnumerator Open(string id, float settle, Opening into)
        {
            if (SelfTestGame.IsShowing(id))
            {
                into.Shown = true;
                into.Opener = true;
                into.How = "it was showing already";
                yield break;
            }
            string refused = null;
            string never = SelfTestScreenTable.Why(id);
            if (never != null)
            {
                into.Why = never;
                yield break;
            }
            string app = SelfTestScreenTable.AppOf(id);
            string modApp = ModAppOf(id);
            string test = SelfTestScreenTable.TestOf(id);
            if (app != null && OpenApp(app))
            {
                into.Opener = true;
                into.How = "the phone's app " + app;
            }
            else if (modApp != null && PhoneApp.OpenById(modApp))
            {
                into.Opener = true;
                into.How = "the mod's app " + modApp;
            }
            else if (SelfTestScreenTable.GameOpenOf(id) != null && SelfTestWorld.GameOpen(SelfTestScreenTable.GameOpenOf(id), out refused))
            {
                into.Opener = true;
                into.How = "the game's own " + SelfTestScreenTable.GameOpenOf(id) + " window";
            }
            else if (id == "monologue" && SelfTestWorld.Monologue("common_close"))
            {
                into.Opener = true;
                into.How = "the game's own monologue queue";
            }
            else if (test != null && PressButton(test, out refused))
            {
                into.Opener = true;
                into.How = "the options button " + test;
            }
            else
            {
                into.Why = test != null ? "its options button '" + test + "' could not be pressed: " + refused
                    : "no app, test button or call of the game's opens it: it is a page inside another screen (open that one and kit_press its tab) "
                    + "or a mod page opened from a button of another window";
                yield break;
            }
            yield return new WaitForSecondsRealtime(settle);
            into.Shown = SelfTestGame.IsShowing(id);
        }

        /// <summary>Takes a toggling test's screen away again (the button pressed a second time); the others close by Back.</summary>
        public static void Undo(string id)
        {
            string test = SelfTestScreenTable.TestOf(id);
            if (test != null && SelfTestScreenTable.Toggles(id) && PressButton(test, out string unused))
            {
                return;
            }
        }

        private static string ModAppOf(string screenId)
        {
            var list = new List<KeyValuePair<string, string>>(32);
            PhoneApp.SelfTestList(list);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Value == screenId)
                {
                    return list[i].Key;
                }
            }
            return null;
        }

        /// <summary>The mod's options page's buttons by the key of their label.</summary>
        public static void Buttons(List<string> into)
        {
            foreach (KeyValuePair<string, ModOptions> entry in OptionsService.RegisteredEntries)
            {
                if (entry.Key != OptionKeysRules.FixedId)
                {
                    continue;
                }
                foreach (ModOption option in entry.Value.Options)
                {
                    if (option is ButtonOption button && button.Label != null)
                    {
                        into.Add(button.Label);
                    }
                }
            }
        }

        public static bool PressButton(string label, out string why)
        {
            why = null;
            foreach (KeyValuePair<string, ModOptions> entry in OptionsService.RegisteredEntries)
            {
                if (entry.Key != OptionKeysRules.FixedId)
                {
                    continue;
                }
                foreach (ModOption option in entry.Value.Options)
                {
                    if (option is ButtonOption button && button.Label == label)
                    {
                        if (button.OnClick == null)
                        {
                            why = "the button '" + label + "' does nothing";
                            return false;
                        }
                        button.OnClick();
                        return true;
                    }
                }
            }
            why = "the mod's options page has no button labelled '" + label + "' (list_buttons names them)";
            return false;
        }
    }
}
