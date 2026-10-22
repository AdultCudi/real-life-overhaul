using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The director's part for what people remember of the player (data v6): the store of the facts the game records
    /// (TextMemoryStore, the save's "reallife:texts_memory"), and the store of the city's new talk (TextsCityStore,
    /// "reallife:texts_city"), each read with the save and written at every save only once a data 6 pack ran or it
    /// changed, so that a save played without one keeps its lines as they were. Facts are remembered only while a pack
    /// of data 6 runs (<see cref="Six"/>): nothing is recorded, and no card carries memory, below it. The player's life
    /// events are noted with RLO Texts on, a pack or not (<see cref="Heard"/>, TextsDirector.City): the people of the
    /// player's life text about them in either case, in the pack's words or their written lines.
    /// COST: nothing per frame; a dictionary's few facts when one is remembered or a card is built.
    /// </summary>
    public sealed partial class TextsDirector
    {
        private static readonly TextMemoryStore MemoryStore = new TextMemoryStore();
        private static readonly TextsCityStore CityStore = new TextsCityStore();
        private static bool _memoryOk, _cityOk, _cityChanged;   // each store read from this save (or it had none): it may be written

        /// <summary>True while RLO Texts is on and a pack of data 6 has been chosen: only then is anything remembered, or anything new said.</summary>
        public static bool Six => Settings.Texts && _pack != null && _pack.Data >= TextIntents.DataV6;

        /// <summary>What people remember, as kept (the checks and the readout read it).</summary>
        public static TextMemoryStore Memory => MemoryStore;

        /// <summary>The facts kept of a person, added to a card's request (Wiring/Wiring.TextsMemory adds what the social life keeps of its own).</summary>
        public static void Recall(string person, List<TextFact> into)
        {
            if (Six && person != null && into != null)
            {
                MemoryStore.AddTo(person, into);
            }
        }

        /// <summary>
        /// The game records a fact of a person's (Wiring: a line of street talk, a text, a call, a Flare message, one of the
        /// staff): kept only while a pack of data 6 runs. False when it is not kept.
        /// </summary>
        public static bool Remember(string person, TextMemoryKinds kind, string code, string value = null, string from = null)
        {
            if (!Six || person == null)
            {
                return false;
            }
            return MemoryStore.Remember(person, new TextFact(kind, code, TextsGame.Day, value, from));
        }

        /// <summary>A stranger who became somebody the player knows: what they remembered goes with them.</summary>
        public static void Move(string from, string to)
        {
            if (Six)
            {
                MemoryStore.Move(from, to);
            }
        }

        /// <summary>
        /// A life event of the player's the city hears of (a crash, an arrest: Wiring/TextsLinks from the paper's log), kept for
        /// the life's texts (a pack or not) and gossip (a pack of data 6).
        /// </summary>
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
                    return;   // (once a day each: a second crash the same day is the same news)
                }
            }
            CityStore.Events.Add(new TextLifeEvent { Kind = kind, Day = day, Value = value, Done = kind == "arrest" });   // (an arrest is texted by the social life already)
            _cityChanged = true;
        }

        /// <summary>The player's life events of the last weeks the people of the player's life have heard of (newest last).</summary>
        public static IReadOnlyList<TextLifeEvent> Events => CityStore.Events;

        private static void ReadMemory(GameInstance save)
        {
            _memoryOk = ReadLine(save, TextMemoryStore.SaveKey, MemoryStore.Read, "what people remember");
            _cityOk = ReadLine(save, TextsCityStore.SaveKey, CityStore.Read, "the city's new talk");
            _cityChanged = false;
        }

        // One store's line read: true if it can be written again (read, or none there); else it is kept as it was read.
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

        // Another save: both stores let go (read again from the new one). (Not Forget: that is Your own AI's forgotten key.)
        private static void ForgetStores()
        {
            MemoryStore.Read(null);
            CityStore.Clear();
            _memoryOk = _cityOk = _cityChanged = false;
        }

        // Once a game day: facts past their lifetime and strangers past their day (with a pack of data 6: below it nothing is
        // remembered, and its line is kept as it was), events past theirs and the marks of staff gone (always: the life's
        // texts note events without a pack too).
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
