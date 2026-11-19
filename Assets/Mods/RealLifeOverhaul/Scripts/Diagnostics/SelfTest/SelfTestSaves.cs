using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using Blueprints;
using Intro;
using TMPro;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The game's saves and the calls that load, make and leave one.</summary>
    internal static class SelfTestSaves
    {
        /// <summary>A save of this version's folder: by name (or "character/name"), the newest, or the newest of a character whose name has these words.</summary>
        public static SaveGameManager.SaveGameStruct Pick(string which, string character, out string why)
        {
            why = null;
            List<SaveGameManager.SaveGameStruct> all = SaveGamePathHelper.GetAllSaveGamesFromVersion();
            SaveGameManager.SaveGameStruct best = null;
            int seen = 0;
            for (int i = 0; i < all.Count; i++)
            {
                SaveGameManager.SaveGameStruct s = all[i];
                if (s == null || s.isTemporary)
                {
                    continue;
                }
                seen++;
                if (!string.IsNullOrEmpty(character) && (s.characterData == null || s.characterData.name == null || s.characterData.name.IndexOf(character, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                bool named = !string.Equals(which, "newest", StringComparison.OrdinalIgnoreCase);
                if (named && !string.Equals(s.name, which, StringComparison.OrdinalIgnoreCase) && !string.Equals(SaveGameManager.GetSaveGamePrettyName(s), which, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!named && s.isRecoverSave)
                {
                    continue;   // the newest one the player made: the game's own Recover saves are the autosaves
                }
                if (best == null || s.lastPlayedDate > best.lastPlayedDate)
                {
                    best = s;
                }
            }
            if (best == null)
            {
                string who = string.IsNullOrEmpty(character) ? string.Empty : " of a character named like '" + character + "'";
                why = "no save fits '" + which + "'" + who + " among the " + seen + " in this version's folder";
            }
            return best;
        }

        public static void Start(SaveGameManager.SaveGameStruct save)
        {
            TransitionToSave.saveToLoadData = save;
            LoadScene.LoadTransitionToSave();
        }

        public static bool StartNew(int money, int age, bool tutorial, string seed)
        {
            MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
            if (menu == null)
            {
                return false;
            }
            var variables = new GameVariables { tutorialEnabled = tutorial, startingMoney = money, startingAge = age };
            UnityEngine.Random.InitState(Animator.StringToHash(seed));   // the character screen's random look is drawn from it
            menu.StartNewGame(variables);
            if (SaveGameManager.Current != null)
            {
                SaveGameManager.Current.seed = seed;   // the game's own seed is the weather's: fixed, the weather is the same each time
            }
            return SaveGameManager.Current != null;
        }

        public static bool Save(string name)
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.SaveGame(name, true);
        }

        /// <summary>The file of a save of the running character, or null when it is not there.</summary>
        public static string FileOf(string name)
        {
            string folder = SaveGamePathHelper.GetCharacterFolderPath(SaveGameManager.Current.characterId);
            string safe = FileSystemHelper.MakeValidFilename(name);
            foreach (string ext in new[] { ".json", ".hsg" })
            {
                string path = Path.Combine(folder, safe + ext);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        public static void LeaveCity()
        {
            OverlayHide();
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (gm != null)
            {
                gm.StartCoroutine(LoadScene.LoadMainMenuFromCity());   // on the game's own manager so it outlives the city, as the funeral screen leaves
            }
        }

        private static void OverlayHide()
        {
            Player.HUD.ItemInfoOverlays.OverlayManager overlays = InstanceBehavior<Player.HUD.ItemInfoOverlays.OverlayManager>.Instance;
            if (overlays != null)
            {
                overlays.HideOverlays();
            }
        }
    }
}
