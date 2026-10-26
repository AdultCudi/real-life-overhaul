using System;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// A building of the mod's knocked down in play (a demolition in Uptown, docs/features/construction.md, "Demolition"): its
    /// address will never stand again until a new building is finished there, so every record of the save in memory that
    /// names it leaves for good and none is kept for a later load, whose records would otherwise come back naming a
    /// building that is no more (a registration with no building stops the game's own code: the orphan registrations fix).
    /// As <see cref="KeepOut"/>, with a scoped set of that one address (the mod's prefix alone names nothing, so no other
    /// building is swept in), but nothing written to the mod's section: the stand-ins are written for good (the player at
    /// the spot outside its door, the player's cars at the way out, a contact's or the destination's address cleared),
    /// the game's lookups brought down to match, and whatever the mod's section kept for that address from an earlier
    /// stand-down let go as well. Another player's things are never taken (a demolition runs only outside a Going Public
    /// session). COST: nothing per frame; one strip of the save when called.
    /// </summary>
    public static partial class DistrictSaveDirector
    {
        /// <summary>
        /// Every record naming the address (a street of the mod's own) out of the save in memory for good, none kept; the spot
        /// outside its door (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>, facing <paramref name="heading"/>,
        /// in the game's district <paramref name="gameDistrict"/>) where a stand-in puts what stood inside. Returns how many
        /// records, stand-ins and kept records went; 0 for an address of the game's, which is never cleared.
        /// </summary>
        public static int Clear(string street, int number, float x, float y, float z, float heading, string gameDistrict, string why)
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || !DistrictSaveRules.IsModId(street))
                {
                    return 0;   // the game's own addresses are never cleared
                }
                PutBackNow("before the mod's building at " + street + " " + number + " is cleared for good");
                Flush();
                PlaceSet places = PlaceSet.Scoped().Address(street, number).Door(street, number, x, y, z, heading, gameDistrict);
                DistrictSaveGame.Strip s = DistrictSaveGame.TakeOut(save, places, true);
                s.PutBacks.Clear();   // for good: what it took is never put back, and never kept
                DistrictSaveGame.Down(s);
                int kept = DistrictSaveStore.RetireAddress(KeptRecords, KeptItems, street, number);
                string line = "District save: the mod's building at " + street + " " + number + " is gone (" + why + "): " + s.Left + " record(s) naming it taken out of the save in memory for good, "
                              + s.StoodIn + " stand-in(s) written, " + kept + " record(s) the mod's section kept for it let go"
                              + (s.Faults.Count > 0 ? "; not taken out: " + string.Join("; ", s.Faults) + ": FAIL" : ": PASS");
                if (s.Faults.Count > 0)
                {
                    Logger?.Warn(line);
                }
                else
                {
                    Logger?.Info(line);
                }
                return s.Left + s.StoodIn + kept;
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // every save still takes out what names the mod's places
                return 0;
            }
        }
    }
}
