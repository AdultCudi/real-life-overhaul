using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// This city's saves for the pause menu's recent saves card, read and loaded through the game's own calls: the list as
    /// the main menu's load screen reads it (SaveGamePathHelper.GetAllSaveGamesFromVersion), kept to this character's, the
    /// latest first; a save loaded after the game's own question box, by way of the main menu as the game leaves a city
    /// (<see cref="Leaving"/>). Saving over one is
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
        internal static bool CanLoad => !GoingPublic.InSession && !LoadScene.isLoading && _leaving == null;

        private static Leaving _leaving;

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
            if (!CanLoad)
            {
                return;
            }
            UiDirector.Logger?.Info("UI: loading the save \"" + s.name + "\" from the pause menu, by way of the main menu as the game leaves a city.");
            var host = new GameObject("RealLifeOverhaul Leaving the city");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _leaving = host.AddComponent<Leaving>();
            _leaving.StartCoroutine(Guard.Routine(_leaving.Through(s)));
        }

        /// <summary>
        /// Leaves the city as the pause menu's Main menu row does and, once the main menu stands, loads the save as the main
        /// menu's load screen loads one. On an object of its own that outlives the city, as the self-test's runner is; gone
        /// once the load is handed over. COST: nothing per frame; four looks a second at the main menu while it loads.
        /// </summary>
        private sealed class Leaving : MonoBehaviour
        {
            private const float MenuWait = 120f;   // real seconds the main menu is given to stand; a slow disk takes most of a minute

            internal IEnumerator Through(SaveGameManager.SaveGameStruct s)
            {
                yield return LoadScene.LoadMainMenuFromCity();
                float until = Time.realtimeSinceStartup + MenuWait;
                while (!MenuStands())
                {
                    if (Time.realtimeSinceStartup > until)
                    {
                        UiDirector.Logger?.Warn("UI: the main menu did not stand within " + MenuWait + " s, so no save was loaded; the player loads one from it.");
                        Destroy(gameObject);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                TransitionToSave.saveToLoadData = s;
                LoadScene.LoadTransitionToSave();
                Destroy(gameObject);
            }

            // As the self-test waits for it: its start view up and no scene loading.
            private static bool MenuStands()
            {
                MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
                return menu != null && menu.startView != null && menu.startView.activeInHierarchy && !LoadScene.isLoading;
            }
        }
    }
}
