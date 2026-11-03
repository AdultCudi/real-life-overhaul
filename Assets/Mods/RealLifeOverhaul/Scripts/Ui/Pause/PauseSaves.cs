using System;
using System.Collections.Generic;
using System.Diagnostics;
using UI.Load;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// This city's saves for the pause menu's recent saves card, read and loaded through the game's own calls: the list as
    /// the main menu's load screen reads it (SaveGamePathHelper.GetAllSaveGamesFromVersion), kept to this character's, the
    /// latest first; a save loaded from the city the way the game's funeral loads its back-up (its save threads joined, the
    /// save handed to TransitionToSave, the loading screen started), after the game's own question box. Saving over one is
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

        /// <summary>Whether a save may be loaded from the city now: not in a shared session (its host decides the city), not while the game is saving or loading.</summary>
        internal static bool CanLoad => !GoingPublic.InSession && !LoadScene.isLoading;

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

        // As the game's funeral loads its back-up: the save's threads joined, the save handed over, the loading screen started.
        private static void Load(SaveGameManager.SaveGameStruct s)
        {
            if (!CanLoad)
            {
                return;
            }
            UiDirector.Logger?.Info("UI: loading the save \"" + s.name + "\" from the pause menu.");
            SaveGameManager.JoinSaveGameThreads();
            TransitionToSave.saveToLoadData = s;
            LoadScene.LoadTransitionToSave();
        }
    }
}
