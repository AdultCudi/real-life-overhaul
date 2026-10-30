using System;
using System.Collections;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Keeps the start days of the player's businesses (<see cref="FoundingRules"/>, <see cref="FoundingGame"/>): the game forgets a
    /// business's start day when the moving company moves the business, so each business is looked at once a game hour, while it
    /// still has it, and the day is kept in the save. Also puts the records in the save as it is written.
    /// COST: no Update and no event on the game's day; a one-second real-time timer compares the game hour with the last one looked
    /// at, and once a game hour makes a pass over the save's building registrations.
    /// </summary>
    public sealed class FoundingWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "FoundingWatch", typeof(FoundingWatch));   // its timer

        public static IModLogger Logger;

        private Action _onSave;

        private void Awake()
        {
            FoundingGame.Logger = Logger;
            _onSave = OnSave;
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (FrameGuard.Off || SaveGameManager.Current == null || LoadScene.isLoading)
                {
                    continue;
                }
                SaveHook.Keep(_onSave);
                FoundingGame.Watch();
            }
        }

        private void OnSave()
        {
            try
            {
                FoundingGame.BeforeSave();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
