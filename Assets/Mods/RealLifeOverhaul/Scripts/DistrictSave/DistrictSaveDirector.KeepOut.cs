using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// One feature's records out of the save in memory for the rest of this city, kept in the mod's section: for a feature
    /// whose places stand down in play after their records went back at the load (Uptown when its feature cleans up, or
    /// when a Going Public session stops being one where every machine runs this mod in this version). As the save
    /// tools' <see cref="KeepOutForGood"/>, but with that one feature's places alone (<see cref="DistrictPlaces.Of"/>, a
    /// scoped set: the mod's prefix alone names nothing, so another feature's buildings are never swept in). The
    /// stand-ins of those places are written for good (the player inside one of its doors outside it, the player out of
    /// doors on its ground at its nearest way out, its cars, contacts, destination and the rest), another player's things
    /// stay where they stand, and the game's lookups are brought down to match (the staff by id, the rivals' lists). The
    /// records are written at every save from then on and go back at a load where their places are made again. A write
    /// that fails puts everything back as it was. COST: nothing per frame; one strip and one write when called.
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        private static readonly HashSet<string> AlsoKept = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// A feature's own line that says which of its places stand (Uptown's buildings finished by the construction game):
        /// the save tools leave it with the district's records, which without it would never go back. Once, at game start.
        /// </summary>
        public static void KeepsAlso(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                AlsoKept.Add(key);
            }
        }

        /// <summary>
        /// Takes every record that names the places <paramref name="owner"/>'s fill names out of the save in memory, kept in
        /// the mod's section; returns how many records and stand-ins. Nothing is asked of the game for those places after
        /// this: the caller keeps its buildings' records in the game's address lookup while anything may still ask for them.
        /// </summary>
        public static int KeepOut(string owner, string why)
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null)
                {
                    return 0;
                }
                PutBackNow("before " + (owner ?? "a feature") + "'s places are taken out for this city");
                Flush();
                PlaceSet places = DistrictPlaces.Of(owner);
                if (places == null || places.Empty)
                {
                    Logger?.Info("District save: " + (owner ?? "(none)") + " stands down (" + why + "), and names no places of its own"
                                 + (places == null ? " (its places could not be read)" : string.Empty) + ": nothing taken out.");
                    return 0;
                }
                DistrictSaveGame.Strip s = DistrictSaveGame.TakeOut(save, places, true);
                Write(save, s, out int records, out string fault);
                if (fault != null)
                {
                    DistrictSaveGame.PutBack(s, null);   // not kept, so not out: every save goes on taking them out and putting them back
                    Logger?.Warn("District save: " + owner + " stands down (" + why + "), and its records stay in the save in memory, as " + fault + ": FAIL");
                    return 0;
                }
                s.PutBacks.Clear();   // for this city: they come back at a load where their places are made
                for (int i = 0; i < s.Records.Count; i++)
                {
                    StoredRecord r = s.Records[i];
                    KeptRecords.Add(r);
                    KeptItems.Add(r.Slot >= 0 && r.Slot < s.Payload.Count ? s.Payload[r.Slot] : null);
                }
                DistrictSaveGame.Down(s);
                string line = "District save: " + owner + " stands down (" + why + "): " + s.Left + " record(s) of its places taken out of the save in memory for this city and "
                              + s.StoodIn + " stand-in(s) written; the mod's section keeps " + records + " record(s) for a load where they are made"
                              + (s.Faults.Count > 0 ? "; not taken out: " + string.Join("; ", s.Faults) + ": FAIL" : ": PASS");
                if (s.Faults.Count > 0)
                {
                    Logger?.Warn(line);
                }
                else
                {
                    Logger?.Info(line);
                }
                return s.Left + s.StoodIn;
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // every save still takes out what names the mod's places
                return 0;
            }
        }
    }
}
