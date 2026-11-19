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

        /// <summary>The screen opened by the way that fits it, then settled; fills what was done and whether the kit shows it.</summary>
        public static IEnumerator Open(string id, float settle, Opening into)
        {
            if (SelfTestGame.Showing(id) != null)
            {
                into.Shown = true;
                into.Opener = true;
                into.How = "it was showing already";
                yield break;
            }
            string refused = null;
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
            else if (test != null && PressButton(test, out refused))
            {
                into.Opener = true;
                into.How = "the options button " + test;
            }
            else
            {
                into.Why = test != null ? "its options button '" + test + "' could not be pressed: " + refused
                    : "no app and no options test button opens it: it stands in the game only in a situation (a desk, a door, a funeral, another screen's page) or is a template's sample";
                yield break;
            }
            yield return new WaitForSecondsRealtime(settle);
            into.Shown = SelfTestGame.Showing(id) != null;
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
