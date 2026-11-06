using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The director's part for what people remember and the city's new talk: both stores written only once a data 6 pack
    /// ran or they changed, so a save played without one keeps its lines. COST: a dictionary's few facts when asked.
    /// </summary>
    public sealed partial class TextsDirector
    {
        private static readonly TextMemoryStore MemoryStore = new TextMemoryStore();
        private static readonly TextsCityStore CityStore = new TextsCityStore();
        private static bool _memoryOk, _cityOk, _cityChanged;   // each store read from this save, or it had none: it may be written

        /// <summary>True while RLO Texts is on with a pack of data 6: only then is anything remembered, or anything new said.</summary>
        public static bool Six => Settings.Texts && _pack != null && _pack.Data >= TextIntents.DataV6;

        /// <summary>What people remember, for the checks and the readout.</summary>
        public static TextMemoryStore Memory => MemoryStore;

        /// <summary>Adds the facts kept of a person to a card's request; Wiring/Wiring.TextsMemory adds what the social life keeps.</summary>
        public static void Recall(string person, List<TextFact> into)
        {
            if (Six && person != null && into != null)
            {
                MemoryStore.AddTo(person, into);
            }
        }

        /// <summary>Records a fact of a person's, kept only while a pack of data 6 runs; false when it is not kept.</summary>
        public static bool Remember(string person, TextMemoryKinds kind, string code, string value = null, string from = null)
        {
            if (!Six || person == null)
            {
                return false;
            }
            return MemoryStore.Remember(person, new TextFact(kind, code, TextsGame.Day, value, from));
        }

        /// <summary>Moves what a stranger remembered to their new key once they are somebody the player knows.</summary>
        public static void Move(string from, string to)
        {
            if (Six)
            {
                MemoryStore.Move(from, to);
            }
        }

        /// <summary>
        /// Lets go of everything remembered of a person, whatever pack runs: the player lost touch with them, and somebody new
        /// takes their key. The save's line is written again at the next save.
        /// </summary>
        public static void ForgetPerson(string person)
        {
            MemoryStore.Forget(person);
        }

        /// <summary>Notes a life event of the player's the city hears of, for the life's texts and gossip.</summary>
        public static void Heard(string kind, string value)
        {
            if (!Settings.Texts || kind == null)
            {
                return;
            }
            int day = TextsGame.Day;
            for (int i = 0; i < CityStore.Events.Count; i++)
            {
                if (CityStore.Events[i].Kind == kind && CityStore.Events[i].Day == day)
                {
                    return;   // once a day each: a second crash the same day is the same news
                }
            }
            CityStore.Events.Add(new TextLifeEvent { Kind = kind, Day = day, Value = value, Done = kind == "arrest" });   // the social life texts about an arrest already
            _cityChanged = true;
        }

        /// <summary>The player's life events of the last weeks that the people of the player's life heard of, newest last.</summary>
        public static IReadOnlyList<TextLifeEvent> Events => CityStore.Events;

        private static void ReadMemory(GameInstance save)
        {
            _memoryOk = ReadLine(save, TextMemoryStore.SaveKey, MemoryStore.Read, "what people remember");
            _cityOk = ReadLine(save, TextsCityStore.SaveKey, CityStore.Read, "the city's new talk");
            _cityChanged = false;
        }

        // One store's line read: true if it can be written again, read or none there; else it is kept as it was read.
        private static bool ReadLine(GameInstance save, string key, Func<string, bool> read, string what)
        {
            string line = null;
            if (save.modData == null || !save.modData.TryGetValue(key, out line) || string.IsNullOrEmpty(line))
            {
                read(null);
                return true;
            }
            if (read(line))
            {
                return true;
            }
            SaveLines.Unreadable(save.modData, key, "it could not be read");
            Logger?.Warn("RLO Texts: the save's line of " + what + " could not be read (" + key + "); it is kept as it is, and nothing new is added to it.");
            return false;
        }

        private static void WriteMemory(GameInstance save)
        {
            if (_memoryOk && (MemoryStore.Changed || (Six && MemoryStore.People > 0)))
            {
                save.modData[TextMemoryStore.SaveKey] = MemoryStore.Write();
            }
            if (_cityOk && (_cityChanged || (Six && CityStore.Mark.Day >= 0)))
            {
                save.modData[TextsCityStore.SaveKey] = CityStore.Write();
                _cityChanged = false;
            }
        }

        // Another save: both stores let go, read again from the new one.
        private static void ForgetStores()
        {
            MemoryStore.Read(null);
            CityStore.Clear();
            _memoryOk = _cityOk = _cityChanged = false;
        }

        // Once a game day: facts past their lifetime with a pack of data 6 (without one the line is kept as it was), and always
        // the events past theirs and the marks of staff gone.
        private static void PruneMemory(int day)
        {
            int gone = Six ? MemoryStore.Prune(day) : 0;
            int city = CityStore.Prune(day);
            _cityChanged |= city > 0;
            if (Meta.Dev.Verbose && (gone > 0 || city > 0))
            {
                Logger?.Info("RLO Texts: on day " + day + " " + gone + " remembered fact(s) and " + city + " event(s) or staff mark(s) let go; " + MemoryStore.People + " people remember "
                             + MemoryStore.Facts + " fact(s).");
            }
        }
    }
}
