using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UI.Load;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// This city's saves for the pause menu's recent saves card, read and loaded through the game's own calls: the list as
    /// the main menu's load screen reads it (SaveGamePathHelper.GetAllSaveGamesFromVersion), kept to this character's, the
    /// latest first; a save loaded after the game's own question box, by way of the main menu as the game leaves a city
    /// (<see cref="Load"/>). Saving over one is
    /// the menu's own Save with that name typed in, so the game's "it exists, save over it?" question asks first.
    /// COST: one read of the saves' folder as the menu opens (behind the paused game, as a game's load list reads it); nothing per frame.
    /// </summary>
    internal static class PauseSaves
    {
        private static readonly Stopwatch Clock = new Stopwatch();

        /// <summary>
        /// This character's saves, the latest first, at most <paramref name="most"/>; returns how many it has in all, or -1
        /// when the game's list could not be read (the card then says nothing of saves).
        /// </summary>
        internal static int Read(List<SaveGameManager.SaveGameStruct> into, int most)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || string.IsNullOrEmpty(save.characterId))
            {
                return -1;
            }
            Clock.Restart();
            List<SaveGameManager.SaveGameStruct> all;
            try
            {
                all = SaveGamePathHelper.GetAllSaveGamesFromVersion();
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Warn("UI: the pause menu could not read the city's saves (" + e.GetType().Name + "); its card says nothing of them.");
                return -1;
            }
            int total = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                SaveGameManager.SaveGameStruct s = all[i];
                if (s == null || !string.Equals(s.characterId, save.characterId, StringComparison.Ordinal) || s.isTemporary)
                {
                    continue;
                }
                total++;
                // The latest few kept in order as they come: a place found by date, the oldest let go past the last.
                int at = into.Count;
                while (at > 0 && into[at - 1].lastPlayedDate < s.lastPlayedDate)
                {
                    at--;
                }
                if (at < most)
                {
                    into.Insert(at, s);
                    if (into.Count > most)
                    {
                        into.RemoveAt(into.Count - 1);
                    }
                }
            }
            Clock.Stop();
            if (Meta.Dev.Verbose)
            {
                UiDirector.Logger?.Info("UI: pause menu saves check (" + total + " of this city's among " + (all != null ? all.Count : 0) + " read in "
                                        + Clock.ElapsedMilliseconds + " ms against 50 ms as the menu opens): " + (Clock.ElapsedMilliseconds <= 50 ? "PASS" : "slow; it runs once an opening"));
            }
            return total;
        }

        /// <summary>
        /// Whether a save may be loaded from the city now: not in a shared session (its host decides the city), not while
        /// the game is saving or loading, not while one is already on its way.
        /// </summary>
        internal static bool CanLoad => !GoingPublic.InSession && !LoadScene.isLoading && _pending == null;

        private const float MenuWait = 120f;   // real seconds the main menu is given to stand; a slow disk takes most of a minute

        private static SaveGameManager.SaveGameStruct _pending;   // the save chosen, from the question until the main menu's scene is in
        private static UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _loaded;

        /// <summary>Loads a save from the city after the game's own question: play since the last save is lost.</summary>
        internal static void AskLoad(SaveGameManager.SaveGameStruct s)
        {
            if (s == null || !CanLoad)
            {
                return;
            }
            var body = Localizor.LanguageChangeEvent.LanguageChangeEventDataHolder.Create("reallife_ui_pause_load_body", new { name = s.name });
            // Never skipped: play not saved is lost.
            GameConfirm.Show(default(Localizor.LanguageChangeEvent.LanguageChangeEventDataHolder), body, Guard.Wrap(() => Load(s)), null, "reallife_ui_pause_load", null,
                false, true);
        }

        // The game leaves a city only by way of its main menu: LoadMainMenuFromCity marks the city as unloading, so its
        // things skip the mouse's reset as they go, and unloads the City mods, so the save's load starts them afresh. A save
        // handed to TransitionToSave from the city itself, as the funeral hands its back-up, did neither: the city's things
        // threw by the thousand as they went, the City mods still counted as active so the load started none of them, and
        // the new city ran without the mod.
        private static void Load(SaveGameManager.SaveGameStruct s)
        {
            GameManager gm = InstanceBehavior<GameManager>.Instance;
            if (!CanLoad || gm == null)
            {
                return;
            }
            UiDirector.Logger?.Info("UI: loading the save \"" + s.name + "\" from the pause menu, by way of the main menu as the game leaves a city.");
            _pending = s;
            _loaded = _loaded ?? Guard.Listener<Scene, LoadSceneMode>(OnSceneLoaded);
            SceneManager.sceneLoaded -= _loaded;
            SceneManager.sceneLoaded += _loaded;
            gm.StartCoroutine(LoadScene.LoadMainMenuFromCity());   // on the game's own manager, as Permadeath and the self-test leave
        }

        // The main menu's scene is in: the save waits on the menu's own controller, which stands until the save's load begins.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
            if (menu == null)
            {
                return;   // another of the scenes coming and going
            }
            SceneManager.sceneLoaded -= _loaded;
            SaveGameManager.SaveGameStruct s = _pending;
            _pending = null;   // taken now: if the menu goes before it stands, nothing is left to block the next load
            if (s != null)
            {
                menu.StartCoroutine(Guard.Routine(HandOver(menu, s)));
            }
        }

        private static IEnumerator HandOver(MainMenuController menu, SaveGameManager.SaveGameStruct s)
        {
            float until = Time.realtimeSinceStartup + MenuWait;
            while (!MenuStands(menu))
            {
                if (Time.realtimeSinceStartup > until)
                {
                    UiDirector.Logger?.Warn("UI: the main menu did not stand within 120 s, so the save \"" + s.name + "\" was not loaded; the player loads it from the menu.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            UiDirector.Logger?.Info("UI: the main menu stands; the save \"" + s.name + "\" handed over as its load screen hands one.");
            TransitionToSave.saveToLoadData = s;
            LoadScene.LoadTransitionToSave();
        }

        // Its start view up, no scene loading, and the menu's own loading screen gone: one made while that one still fades
        // takes itself away as a second copy, and the load it carries never starts.
        private static bool MenuStands(MainMenuController menu)
        {
            return menu != null && menu.startView != null && menu.startView.activeInHierarchy && !LoadScene.isLoading
                   && !InstanceBehavior<LoadingScreen>.IsInitialized;
        }
    }
}
