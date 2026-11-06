using System;
using System.Collections.Generic;
using RealLifeOverhaul.Rivals;
using UI.Load;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The player's net worth and each active rival tycoon's, snapshotted once a game day for the net worth card over the money pill
    /// and the rich list (<see cref="WorthBook"/>, kept in the save as <see cref="WorthStore.Key"/>). The first look at a new day
    /// takes the day's figures; the book is read from the save the first time a city is seen and written into its modData as the
    /// game saves. The player's own figure: a guest of a shared session records nothing and writes nothing, and nothing goes over
    /// the network. A line this version cannot read is kept as it was read and no history shows. Cost: no Update; a comparison of
    /// the day on the interface's one-second tick, and one net worth (the game's wealth walk) and three worths a day.
    /// </summary>
    internal static class WorthWatch
    {
        private static readonly Action OnSave = Saved;
        private static readonly long[] Worths = new long[WorthBook.Tycoons];
        private static readonly bool[] Active = new bool[WorthBook.Tycoons];
        private static GameInstance _of;      // the save the book belongs to
        private static WorthBook _book = new WorthBook();
        private static bool _unread;          // its line could not be read: kept, and no history shown
        private static bool _said;            // a fault in the net worth said once

        /// <summary>The save's days on record, or null while there is no city, a guest is in it or its line could not be read.</summary>
        internal static WorthBook Book => _of != null && ReferenceEquals(_of, SaveGameManager.Current) && !_unread && !GoingPublic.IsGuest ? _book : null;

        /// <summary>The interface's one-second tick while a city is loaded: the book read for a new city, a new day's figures taken.</summary>
        internal static void Tick()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || LoadScene.isLoading || Meta.SaveTools.Dormant || GoingPublic.IsGuest)
            {
                return;
            }
            SaveHook.Keep(OnSave);
            if (!ReferenceEquals(_of, save))
            {
                _of = save;
                _unread = false;
                _book = new WorthBook();
                string text = null;
                if (save.modData != null && save.modData.TryGetValue(WorthStore.Key, out text) && !WorthStore.Read(text, out _book))
                {
                    _unread = true;
                    SaveLines.Unreadable(save.modData, WorthStore.Key, "it could not be read");   // put back as it was read at every save
                }
            }
            if (_unread || _book.Player.LastDay >= save.Day)
            {
                return;
            }
            Take(save.Day);
        }

        /// <summary>A new city, or the mod's data taken out of the save: the book is read again from the next city's line.</summary>
        internal static void Forget()
        {
            _of = null;
            _book = new WorthBook();
            _unread = false;
        }

        /// <summary>The player's net worth now, as the Overview counts it; false when it cannot be read.</summary>
        internal static bool Now(out long worth)
        {
            worth = 0L;
            try
            {
                worth = Books.NetWorth();
                return true;
            }
            catch (Exception e)
            {
                if (!_said)
                {
                    _said = true;
                    RealLifeOverhaul.Ui.UiDirector.Logger?.Warn("UI: the net worth could not be read for the money card: " + e.Message);
                }
                return false;
            }
        }

        // The day's figures: the player's, and each tycoon who has come forward and not been taken over.
        private static void Take(int day)
        {
            if (!Now(out long player))
            {
                return;
            }
            RivalFile rivals = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            int count = rivals != null ? Math.Min(rivals.Tycoons.Length, Worths.Length) : 0;
            for (int i = 0; i < Worths.Length; i++)
            {
                Active[i] = i < count && rivals.Tycoons[i].Active;
                Worths[i] = Active[i] ? rivals.Tycoons[i].Worth : 0L;
            }
            _book.Day(day, player, Worths, Active, Worths.Length);
        }

        // The book into the save's modData as the game writes it.
        private static void Saved()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ReferenceEquals(_of, save) || _unread || Meta.SaveTools.Dormant || GoingPublic.IsGuest || _book.Empty)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (SaveLines.IsKept(save.modData, WorthStore.Key))
            {
                return;
            }
            save.modData[WorthStore.Key] = WorthStore.Write(_book);
        }
    }
}
