using System;
using System.Collections.Generic;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The time played in each city, for the pause menu's card: read from the save's line (<see cref="PlayTimeStore"/>) the
    /// first time a city is seen, counted on in real seconds while it is loaded, and written into the save's modData as the
    /// game saves. The player's own figure: a guest of a shared session counts nothing and writes nothing, and nothing goes
    /// over the network. A line this version cannot read is kept as it was read and the card says nothing of play time.
    /// COST: no Update. A comparison and an addition on the interface's one-second tick; a line written at each save.
    /// </summary>
    internal static class PlayTime
    {
        private const float StepMost = 5f;   // seconds counted for one tick at most: a stall or a hidden window is not play

        private static readonly Action OnSave = Saved;
        private static GameInstance _of;      // the save the figure belongs to
        private static long _seconds;          // whole seconds played in it
        private static float _part;            // the second's part not yet whole
        private static float _last = -1f;      // unscaled time of the last tick
        private static bool _unread;           // its line could not be read: kept, and the figure not shown

        /// <summary>Seconds played in this city, or -1 when it is not known (no city, a guest, or its line could not be read).</summary>
        internal static long Seconds => _of != null && ReferenceEquals(_of, SaveGameManager.Current) && !_unread && !GoingPublic.IsGuest ? _seconds : -1L;

        /// <summary>The interface's one-second tick while a city is loaded: the figure read for a new city, the time since the last tick added.</summary>
        internal static void Tick()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || Meta.SaveTools.Dormant || GoingPublic.IsGuest)
            {
                _last = -1f;
                return;
            }
            SaveHook.Keep(OnSave);
            float now = Time.unscaledTime;
            if (!ReferenceEquals(_of, save))
            {
                _of = save;
                _part = 0f;
                _unread = false;
                string text = null;
                if (save.modData != null && save.modData.TryGetValue(PlayTimeStore.Key, out text) && !PlayTimeStore.Read(text, out _seconds))
                {
                    _unread = true;
                    SaveLines.Unreadable(save.modData, PlayTimeStore.Key, "it could not be read");   // put back as it was read at every save
                }
                else if (text == null)
                {
                    _seconds = 0L;
                }
                _last = now;
                return;
            }
            if (_last >= 0f)
            {
                _part += Math.Min(StepMost, Math.Max(0f, now - _last));
                long whole = (long)_part;
                _seconds += whole;
                _part -= whole;
            }
            _last = now;
        }

        /// <summary>A new city, or the mod's data taken out of the save: the figure is read again from the next city's line.</summary>
        internal static void Forget()
        {
            _of = null;
            _seconds = 0L;
            _part = 0f;
            _last = -1f;
            _unread = false;
        }

        // The figure into the save's modData as the game writes it.
        private static void Saved()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ReferenceEquals(_of, save) || _unread || Meta.SaveTools.Dormant || GoingPublic.IsGuest)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (SaveLines.IsKept(save.modData, PlayTimeStore.Key))
            {
                return;
            }
            save.modData[PlayTimeStore.Key] = PlayTimeStore.Write(_seconds);
        }
    }
}
