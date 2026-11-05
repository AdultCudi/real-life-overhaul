using System;
using BigAmbitions.DayNightCycle;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The place card's two small stores in the save, the only file of them that touches the game: the player's saved places (<c>reallife:map_places</c>) and what the places
    /// they looked at usually draw (<c>reallife:map_busy</c>), each read once a save and written when it is saved, a line that cannot be read left as it was read
    /// (Core/SaveLines). A place's average moves when it is looked at, once a game day at most, so nothing runs when the day turns. Cost: a read and a write a save,
    /// a few numbers a look; nothing a frame.
    /// </summary>
    internal static class MapStoreGame
    {
        private static MapSaved _saved = new MapSaved();
        private static MapBusy _busy = new MapBusy();
        private static GameInstance _save;
        private static int _savedVersion;
        private static bool _savedKept = true, _busyKept = true;   // false while the line in the save is one this version cannot read: it is left as it was
        private static Action _onSave;

        /// <summary>How many places are saved now.</summary>
        public static int Count
        {
            get
            {
                Load();
                return _saved.Count;
            }
        }

        /// <summary>Changes whenever the list of saved places does (a place saved or taken out, a save loaded or the city gone).</summary>
        public static int SavedVersion
        {
            get
            {
                Load();
                return _savedVersion;
            }
        }

        /// <summary>The saved place at an index of the list, the newest first.</summary>
        public static MapSavedPlace SavedAt(int index)
        {
            Load();
            return _saved[index];
        }

        public static bool Saved(string street, int number)
        {
            Load();
            return _saved.Contains(street, number);
        }

        /// <summary>Saves the place or takes it out of the list; returns whether it is saved now.</summary>
        public static bool Toggle(string street, int number, string name)
        {
            Load();
            _savedVersion++;
            return _saved.Toggle(street, number, name);
        }

        /// <summary>
        /// The player looks at a place: true when it draws more today than it usually does (once there is a usual to go by), after which today is folded into the average,
        /// once a game day, and the place is kept from now (started at what it draws today, so it is never busier on the first look).
        /// </summary>
        public static bool Look(string place, int arrivals)
        {
            Load();
            int day = TimeHelperDay();
            bool busier = _busy.Usual(place, out int usual) && MapPlaceRules.Busier(arrivals, usual);
            _busy.Move(place, arrivals, day);
            _busy.Watch(place, arrivals, day);
            return busier;
        }

        private static int TimeHelperDay() => TimeHelper.CurrentDay;

        // Reads both lines once for a save and puts the writers on the game's events: the save's, and a new game day's.
        private static void Load()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ModState.Loaded)
            {
                return;
            }
            _onSave = _onSave ?? OnSave;
            SaveHook.Keep(_onSave);
            if (ReferenceEquals(save, _save))
            {
                return;
            }
            _save = save;
            string line = null;
            _savedKept = true;
            if (save.modData != null && save.modData.TryGetValue(MapSaved.Key, out line) && !MapSaved.Readable(line))
            {
                SaveLines.Unreadable(save.modData, MapSaved.Key, "its version is not one this version of the mod reads");
                _savedKept = false;
            }
            _saved = _savedKept ? MapSaved.Read(line) : new MapSaved();
            _savedVersion++;
            line = null;
            _busyKept = true;
            if (save.modData != null && save.modData.TryGetValue(MapBusy.Key, out line) && !MapBusy.Readable(line))
            {
                SaveLines.Unreadable(save.modData, MapBusy.Key, "its version is not one this version of the mod reads");
                _busyKept = false;
            }
            _busy = _busyKept ? MapBusy.Read(line) : new MapBusy();
        }

        private static void OnSave()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !ReferenceEquals(save, _save))
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new System.Collections.Generic.Dictionary<string, string>();
            }
            Put(save, MapSaved.Key, _savedKept ? _saved.Write() : null, _savedKept);
            Put(save, MapBusy.Key, _busyKept ? _busy.Write() : null, _busyKept);
        }

        private static void Put(GameInstance save, string key, string line, bool kept)
        {
            if (!kept)
            {
                return;   // a line this version cannot read stays as it was read
            }
            if (line == null)
            {
                save.modData.Remove(key);
            }
            else
            {
                save.modData[key] = line;
            }
        }

        /// <summary>The city unloaded: the next save is read again.</summary>
        public static void Forget()
        {
            _save = null;
            _saved = new MapSaved();
            _busy = new MapBusy();
            _savedVersion++;
        }
    }
}
