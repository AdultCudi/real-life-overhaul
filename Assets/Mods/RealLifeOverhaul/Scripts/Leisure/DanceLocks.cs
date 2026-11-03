using System;
using System.Runtime.CompilerServices;
using Localizor.LanguageChangeEvent;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The dances the player has not learned (DanceRules, LeisureState.Dances) held back while the city's courses run
    /// (Settings.CareersStudy, so the school's dance class can teach them): the game's own dance list keeps every button,
    /// the unlearned ones not pressable. With the courses off, or the careers standing down, every button is given back.
    /// The HUD's dance list shows the same buttons, so it greys the same rows; the mod's dance floor dances known dances
    /// alone (Activities/NightOut). NPC dancers keep every dance. Cost: a flag test a second, nine buttons written on a change.
    /// </summary>
    public static class DanceLocks
    {
        private const string KeyHead = "dancetype_dance";   // the game's name keys of its dances: dancetype_dance1 to dancetype_dance9

        private static int _written = -1;      // the set the buttons were last written for; -1 for none
        private static int _version = -1;      // LeisureState.DancesVersion when they were
        private static Transform _listSeen;    // the game's list they were written in...
        private static int _childrenSeen;      // ...and its children then (the game fills it on its first start)

        /// <summary>True while unlearned dances are held back.</summary>
        public static bool Active { get; private set; }

        /// <summary>The dances the player may dance now: the learned ones while held back, every dance otherwise.</summary>
        public static int Mask => Active ? LeisureState.Dances : DanceRules.All;

        /// <summary>Once a second from the careers' timer: the buttons written again for the set, when it or the list changed.</summary>
        public static void Keep(bool active)
        {
            Active = active;
            int set = Mask;
            try
            {
                Write(set);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the dances not learned yet");
                Active = false;
            }
        }

        /// <summary>The careers stand down: every dance button given back.</summary>
        public static void Release() => Keep(false);

        /// <summary>The dance a button of the game's list starts, by its name's key (its place in the game's list); -1 for none.</summary>
        public static int DanceOf(Transform button)
        {
            Transform label = button != null ? button.Find("DanceNameLabel") : null;
            TextLocalizationComponent named = label != null ? label.GetComponent<TextLocalizationComponent>() : null;
            string key = named != null ? named.Key : null;
            if (key == null || !key.StartsWith(KeyHead, StringComparison.OrdinalIgnoreCase))   // the game writes "dancetype_Dance1"
            {
                return -1;
            }
            return int.TryParse(key.Substring(KeyHead.Length), out int n) && n >= 1 && n <= DanceRules.Count ? n - 1 : -1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Write(int set)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            UI.Topbar.PlayerDancesUI dances = uis != null && uis.topBar != null ? uis.topBar.playerDancesUI : null;
            Transform list = dances != null ? dances.transform.Find("DanceTypesList") : null;
            if (list == null)
            {
                _listSeen = null;
                return;
            }
            if (ReferenceEquals(list, _listSeen) && set == _written && LeisureState.DancesVersion == _version && list.childCount == _childrenSeen)
            {
                return;
            }
            for (int i = 0; i < list.childCount; i++)
            {
                Transform child = list.GetChild(i);
                int dance = DanceOf(child);
                Button button = dance >= 0 ? child.GetComponent<Button>() : null;
                if (button != null)
                {
                    button.interactable = DanceRules.Known(set, dance);
                }
            }
            _listSeen = list;
            _childrenSeen = list.childCount;
            _written = set;
            _version = LeisureState.DancesVersion;
        }
    }
}
