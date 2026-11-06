using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.Tech;
using UI.Load;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's side of the start days (<see cref="FoundingRules"/>): the player's running businesses read from the save, the records
    /// loaded from and written to the save's modData, and <see cref="Of"/>, the one answer to "what day did this business start" that
    /// every feature asks instead of reading BuildingRegistration.creationDay (which the moving company's move leaves at -1 or at
    /// another business's day). Reads only; nothing of the game's is changed. Nothing is sent over the network: the records are the
    /// player's own, one per business Going Public tells is theirs (<see cref="GoingPublic.Mine"/>), and a guest's are left as they are.
    /// COST: no Update. A pass over the save's building registrations at most once a game hour, when a feature asks or the watch's
    /// one-second timer looks (Core/FoundingWatch); a call to <see cref="Of"/> otherwise is a few comparisons and a short list search.
    /// </summary>
    internal static class FoundingGame
    {
        private const string Empty = "ba:businesstype_empty";

        public static IModLogger Logger;

        /// <summary>
        /// The start day of a business that has none of the game's and no record: its place and name in, a day or 0 out. Wired by
        /// Scripts/Wiring/FoundingLinks (the moving companies' messages are the tech sector's to read); it may be left unset.
        /// </summary>
        public static Func<string, string, int> Estimate;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        private static readonly int HotSample = HotSpots.Slot("Core: FoundingGame.Sample");
        private static readonly List<FoundingSeen> Now = new List<FoundingSeen>(16);
        private static readonly HashSet<string> Asked = new HashSet<string>(StringComparer.Ordinal);   // estimates asked for this session, found or not
        private static readonly Func<string, string, int> Ask = AskOnce;
        private static GameInstance _of;       // the save the records belong to
        private static FoundingFile _file;     // its records; null until read
        private static int _sampledAt = -1;    // the game hour (day * 24 + hour) the records were last brought up to date at

        /// <summary>A business of the player's the records are kept for: theirs, started, in a building the city has.</summary>
        public static bool Running(BuildingRegistration reg)
        {
            return reg != null && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && reg.HasValidAddress && !string.IsNullOrEmpty(reg.businessTypeName)
                   && reg.businessTypeName != Empty && GameRegistrations.Found(reg);
        }

        /// <summary>
        /// The day the business started, kept across moves and type changes: the game's own while it holds one, the day kept for it when the
        /// moving company's move left it none, an estimate for one moved before this was kept; 0 when none is known (0 is never a day).
        /// Any registration may be asked about; one that is not a running business of the player's gives the game's own day.
        /// </summary>
        public static int Of(BuildingRegistration reg)
        {
            if (reg == null)
            {
                return 0;
            }
            int created = reg.creationDay > 0 ? reg.creationDay : 0;
            try
            {
                GameInstance save = Save;
                if (save == null || !Running(reg) || !Open(save))
                {
                    return created;
                }
                Refresh(save);
                return FoundingRules.DayOf(_file, Tech.PlaceRules.Key(reg.StreetName, reg.StreetNumber), TechWriter.Clean(reg.BusinessName), reg.creationDay);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return created;
            }
        }

        /// <summary>The watch's look: reads this save's records when it has changed and brings them up to date once a game hour.</summary>
        public static void Watch()
        {
            GameInstance save = Save;
            if (save != null && Open(save))
            {
                Refresh(save);
            }
        }

        /// <summary>Puts the records in the save's modData as it is written (a line this version could not read is left as it was read).</summary>
        public static void BeforeSave()
        {
            GameInstance save = Save;
            if (save == null || !ReferenceEquals(_of, save) || _file == null || Meta.SaveTools.Dormant)
            {
                return;
            }
            Refresh(save);
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (SaveLines.IsKept(save.modData, FoundingStore.Key))
            {
                return;
            }
            if (_file.Records.Count == 0)
            {
                save.modData.Remove(FoundingStore.Key);
                return;
            }
            save.modData[FoundingStore.Key] = FoundingStore.Write(_file);
        }

        /// <summary>How many start days are kept for this save; -1 before they are read.</summary>
        public static int Kept => _file != null ? _file.Records.Count : -1;

        // The save's records are read once for each save loaded; false while the save's modData is not ready, or the mod's data is being taken out of it.
        private static bool Open(GameInstance save)
        {
            if (LoadScene.isLoading || !ModState.Loaded || Meta.SaveTools.Dormant)
            {
                return false;
            }
            if (!ReferenceEquals(_of, save))
            {
                _of = save;
                _sampledAt = -1;
                Asked.Clear();
                _file = FoundingStore.Load(save.modData, out _) ?? new FoundingFile();
                if (Verbose)
                {
                    Logger?.Info("Founding: " + _file.Records.Count + " start day(s) read from the save.");
                }
            }
            return true;
        }

        private static void Refresh(GameInstance save)
        {
            if (GoingPublic.IsGuest || save.BuildingRegistrations == null)
            {
                return;   // a guest's businesses are not read while they are away from their own city
            }
            int at = save.Day * 24 + save.Hour;
            if (at == _sampledAt)
            {
                return;
            }
            _sampledAt = at;
            using var hotSpot = HotSpots.Time(HotSample);
            Now.Clear();
            List<BuildingRegistration> all = save.BuildingRegistrations;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (Running(reg))
                {
                    Now.Add(new FoundingSeen { Place = Tech.PlaceRules.Key(reg.StreetName, reg.StreetNumber), Name = TechWriter.Clean(reg.BusinessName), Created = reg.creationDay });
                }
            }
            int changes = FoundingRules.Sample(_file, Now, save.Day, Ask);
            if (changes > 0 && Verbose)
            {
                Logger?.Info("Founding check (day " + save.Day + "): " + Now.Count + " business(es) of the player's standing, " + _file.Records.Count + " start day(s) kept, " + changes
                             + " added, followed to another building or dropped: PASS");
            }
            Now.Clear();
        }

        // Asked once for each business a session: an estimate that finds nothing is not looked for again every hour.
        private static int AskOnce(string place, string name)
        {
            if (Estimate == null || !Asked.Add(place + "|" + name))
            {
                return 0;
            }
            try
            {
                return Math.Max(0, Estimate(place, name));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return 0;
            }
        }
    }
}
