using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// A player parked a rented scooter outside Hudson City University, sat a class and came out to no scooter (24 Nov 2026). The game takes a scooter
// away once it has been unseen for its type's minutes (AutoDestroyVehicle, every frame, the player's whereabouts not asked), a building is out of
// its sight, and a degree's class is four hours. Nothing in the mod moved it. Now the mod marks such a scooter seen once a second while the
// player is indoors (ScooterHold). The owner then asked whether sleep counts: a rental lasts the game day it was made on, held indoors until
// midnight and taken back, with one notice, the first second after it that the scooter is out of sight with nobody on it (ScooterHold,
// ScooterRentals). Offline: run with --scooter-hold.
internal static partial class Program
{
    private static void ScooterHold()
    {
        const int Limit = 240;   // the figure the mod's earlier notes give the game's scooter (BikeMount); the rules take it as data

        // The longest class of the catalogue, the player arriving early: the stay that was lost.
        int longest = 0;
        foreach (CourseDef c in StudyCatalogue.All)
        {
            longest = Math.Max(longest, c.Hours * 60 + StudyCatalogue.ArriveEarlyMinutes);
        }
        Check(longest >= Limit, "scooter hold: a class with its early arrival (" + longest + " min) outlasts a scooter's " + Limit + " minutes, which is what the report was");
        Check(ScooterHoldRules.TakenDuring(Limit, longest, 1f, false), "scooter hold: without the hold the game takes a scooter parked outside a class of the longest kind");
        Check(!ScooterHoldRules.TakenDuring(Limit, longest, 1f, true), "scooter hold: with the hold the same stay leaves it");
        Check(!ScooterHoldRules.TakenDuring(Limit, 120f, 1f, false), "scooter hold: a two hour film is no trouble either way, the game's rule is not otherwise changed");
        foreach (float tick in new[] { 1f, 3f, 10f, 30f })
        {
            Check(!ScooterHoldRules.TakenDuring(Limit, 24f * 60f, tick, true), "scooter hold: a whole day indoors at " + tick + " game minutes a second leaves it");
        }
        Check(ScooterHoldRules.TakenDuring(Limit, 24f * 60f, Limit + 1f, true), "scooter hold: a clock put back less often than the limit is no hold (the model can fail)");

        // The game's own test, as AutoDestroyVehicle writes it.
        Check(ScooterHoldRules.GameTakes(240, true, 300f, 60f) && !ScooterHoldRules.GameTakes(240, true, 299f, 60f), "scooter hold: the game takes a vehicle at its limit and not a minute before");
        Check(!ScooterHoldRules.GameTakes(-1, true, 1e6f, 0f) && !ScooterHoldRules.GameTakes(240, false, 1e6f, 0f), "scooter hold: no limit, or nothing counting, and the game takes nothing");

        // Which vehicles the hold touches (the last argument: rented today).
        Check(ScooterHoldRules.Holds(true, true, true, 240, true, true, false, false, true), "scooter hold: a rented scooter on the street, nobody on it, the player indoors, rented today, is held");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, true, false, false, false), "scooter hold: one rented on an earlier day is no longer held");
        Check(!ScooterHoldRules.Holds(false, true, true, 240, true, true, false, false, true), "scooter hold: with the option off nothing is held");
        Check(!ScooterHoldRules.Holds(true, false, true, 240, true, true, false, false, true), "scooter hold: out of doors the game's clock is the game's");
        Check(!ScooterHoldRules.Holds(true, true, false, 240, true, true, false, false, true), "scooter hold: a car, a van or a hand truck is not a scooter and is left to the game");
        Check(!ScooterHoldRules.Holds(true, true, true, -1, true, true, false, false, true), "scooter hold: a vehicle the game never takes away has nothing to hold");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, false, true, false, false, true), "scooter hold: one nothing is counting for (in sight, or never seen) is left as it is");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, false, false, false, true), "scooter hold: a scooter filed at a building's address is the game's to bring back with that building");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, true, true, false, true), "scooter hold: the one the player is on is never touched");
        Check(!ScooterHoldRules.Holds(true, true, true, 240, true, true, false, true, true), "scooter hold: the mod's own bicycles and motorbikes keep their own rules");

        // Returns: the same scooters, after their day, whoever is where.
        Check(ScooterHoldRules.Returns(true, true, 240, true, true, false, false, false), "scooter return: a rented scooter of an earlier day, out of sight, nobody on it, goes");
        Check(!ScooterHoldRules.Returns(false, true, 240, true, true, false, false, false), "scooter return: with the option off nothing is taken back");
        Check(!ScooterHoldRules.Returns(true, true, 240, true, true, false, false, true), "scooter return: a rental of today is not taken back");
        Check(!ScooterHoldRules.Returns(true, true, 240, false, true, false, false, false), "scooter return: one in sight (or never seen) stays until it is out of sight");
        Check(!ScooterHoldRules.Returns(true, true, 240, true, true, true, false, false), "scooter return: the one the player is on is never taken");
        Check(!ScooterHoldRules.Returns(true, true, 240, true, true, false, true, false), "scooter return: the mod's own bicycles and motorbikes keep their own rules");
        Check(!ScooterHoldRules.Returns(true, false, 240, true, true, false, false, false) && !ScooterHoldRules.Returns(true, true, -1, true, true, false, false, false)
              && !ScooterHoldRules.Returns(true, true, 240, true, false, false, false, false), "scooter return: a car, a vehicle the game never takes, or one filed at a building is left alone");
        bool both = false;
        for (int bits = 0; bits < 512; bits++)
        {
            int b = bits;
            bool B(int n) => (b & (1 << n)) != 0;
            both |= ScooterHoldRules.Holds(B(0), B(1), B(2), B(3) ? 240 : -1, B(4), B(5), B(6), B(7), B(8)) && ScooterHoldRules.Returns(B(0), B(2), B(3) ? 240 : -1, B(4), B(5), B(6), B(7), B(8));
        }
        Check(!both, "scooter return: no scooter is both held and taken back");
        Check(ScooterHoldRules.RentedToday(5, 5) && !ScooterHoldRules.RentedToday(5, 6) && ScooterHoldRules.RentedToday(5, 4), "scooter return: a rental is today's on its own day, over from the next, and a clock set back is still inside it");
        Check(ScooterHoldRules.Notices(0) == 0 && ScooterHoldRules.Notices(1) == 1 && ScooterHoldRules.Notices(4) == 1, "scooter return: the notice is told once, however many go in one second");

        // The whole day, minute by minute, against the game's own clock.
        const int Day = 5 * 1440;
        const int H = 60;
        foreach (int tick in new[] { 1, 3, 10 })
        {
            // Rented at 20:00, ridden home to 20:30, then asleep indoors until 08:00: gone at the first second after midnight, not before.
            int slept = ScooterRun("a", Day + 20 * H, Day + 32 * H, tick, true, m => m >= Day + 20 * H + 30, m => m < Day + 20 * H + 30, m => m < Day + 20 * H + 30, false, 0);
            Check(slept >= Day + 24 * H && slept <= Day + 24 * H + tick, "scooter return: rented at 20:00 and asleep indoors until 08:00 it is gone at the first second after midnight (" + slept + " against " + (Day + 24 * H) + ", tick " + tick + ")");

            // Rented at 09:00, a class of the longest kind the same day: kept, and still there when the player comes out.
            int classEnd = Day + 9 * H + 20 + longest;
            int inClass = ScooterRun("b", Day + 9 * H, classEnd, tick, true, m => m >= Day + 9 * H + 20, m => m < Day + 9 * H + 20, m => m < Day + 9 * H + 20, false, 0);
            Check(inClass == -1, "scooter return: rented at 09:00, a class of " + longest + " minutes the same day keeps it (tick " + tick + ")");

            // Ridden across midnight in sight: stays; left out of sight at 01:00 it goes at once, indoors or out.
            int rode = ScooterRun("c", Day + 22 * H, Day + 30 * H, tick, true, m => m >= Day + 25 * H, m => m < Day + 25 * H, m => m < Day + 25 * H, false, 0);
            Check(rode >= Day + 25 * H && rode <= Day + 25 * H + tick, "scooter return: ridden across midnight it stays while ridden and goes the first second it is left out of sight (" + rode + ", tick " + tick + ")");
            // Left in sight outdoors until 01:30: it stays till then, being seen.
            int seen = ScooterRun("d", Day + 22 * H, Day + 30 * H, tick, true, m => false, m => m < Day + 25 * H, m => m < Day + 25 * H + 30, false, 0);
            Check(seen >= Day + 25 * H + 30 && seen <= Day + 25 * H + 30 + tick, "scooter return: left in sight after midnight it stays while it is seen (" + seen + ")");

            // A save from before: no line for it, the load day is its rental day, so it goes at the next midnight and not at once.
            ScooterRentals.Read(null);
            int old = ScooterRun("e", Day + 10 * H, Day + 30 * H, tick, true, m => true, m => false, m => false, false, 0, true);
            Check(ScooterRentals.Days.TryGetValue("e", out int loadDay) && loadDay == 5, "scooter return: a scooter of a save from before takes the day it is first seen as its rental day");
            Check(old >= Day + 24 * H && old <= Day + 24 * H + tick, "scooter return: that scooter is not lost at once on updating and goes at the next midnight (" + old + ")");

            // Saving and loading does not stretch the rental: once at 22:00 asleep, once after midnight with the scooter ridden in sight.
            int reloaded = ScooterRun("f", Day + 20 * H, Day + 32 * H, tick, true, m => m >= Day + 20 * H + 30, m => m < Day + 20 * H + 30, m => m < Day + 20 * H + 30, false, Day + 22 * H);
            Check(reloaded >= Day + 24 * H && reloaded <= Day + 24 * H + tick, "scooter return: a save and load at 22:00 leaves the rental ending at midnight (" + reloaded + ")");
            int late = Day + 24 * H + 30;
            int rideLoad = ScooterRun("g", Day + 20 * H, Day + 60 * H, tick, true, m => m >= late, m => m < late, m => m < late, false, Day + 24 * H + 15);
            Check(rideLoad >= late && rideLoad <= late + tick, "scooter return: a save and load while riding past midnight does not give it a new day (" + rideLoad + ")");
            // The model can fail: a load that lost the line would give the scooter a new day.
            int lost = ScooterRun("h", Day + 20 * H, Day + 60 * H, tick, true, m => m >= late, m => m < late, m => m < late, false, -(Day + 24 * H + 15));
            Check(lost > late + tick, "scooter return: a load that forgot the rental day would extend it (the model can fail; gone at " + lost + ")");
        }
        // Lines that cannot be read are left out, and the scooter begins on the day it is seen.
        ScooterRentals.Read("good|7\nbad|x\n|9\nnobar\n  \nlast|3");
        Check(ScooterRentals.Days.Count == 2 && ScooterRentals.Days["good"] == 7 && ScooterRentals.Days["last"] == 3, "scooter rentals: a saved line is read back and a line that cannot be read is left out");
        ScooterRentals.Days["gone"] = 4;
        string kept = ScooterRentals.Write(new HashSet<string> { "good", "last" });
        ScooterRentals.Read(kept);
        Check(ScooterRentals.Days.Count == 2 && !ScooterRentals.Days.ContainsKey("gone") && ScooterRentals.Days["good"] == 7, "scooter rentals: a scooter that is no longer in the save is forgotten when saving");
        ScooterRentals.Clear();

        // The option off: the game's own rule alone, nothing held, nothing taken back, nothing kept.
        int offEnd = ScooterRun("i", Day + 20 * H, Day + 40 * H, 1, false, m => m >= Day + 20 * H + 30, m => m < Day + 20 * H + 30, m => m < Day + 20 * H + 30, false, 0);
        Check(offEnd == Day + 20 * H + 30 + Limit && ScooterRentals.Days.Count == 0, "scooter return: with the option off the game's own four hours decide, as without the mod, and no day is kept (" + offEnd + ")");
        int offShort = ScooterRun("j", Day + 20 * H, Day + 21 * H, 1, false, m => m >= Day + 20 * H + 30, m => m < Day + 20 * H + 30, m => m < Day + 20 * H + 30, false, 0);
        Check(offShort == -1, "scooter return: with the option off a short stay indoors is no concern of the mod's either");
        ScooterRentals.Clear();

        // Over the sources.
        string hold = ModSource(@"Garage\ScooterHold.cs");
        Check(hold.Contains("save.VehicleInstances") && !hold.Contains("FindObjects") && hold.Contains("GoingPublic.MineVehicle(controller)") && hold.Contains("controller.vehicleInstance == vehicle"),
            "scooter hold source: the save's own vehicles are read and only the player's own object for one of them is taken, so no other player's vehicle is touched");
        Check(hold.Contains("AutoDestroyVehicle.ShouldDestroyVehicle(vehicle, -1f)") && hold.Contains("save.VehicleInstances.Remove(vehicle)") && hold.Contains("Object.Destroy(controller.gameObject)")
              && hold.Contains("ScooterRentals.Forget(vehicle.id)"), "scooter hold source: it is taken back as the game does, the object destroyed and the instance removed from the save, and the game's keep for paid cargo holds");
        Check(hold.Contains("FullMenu.IsOpen") && hold.Contains("GameManager.isCitySceneBeingUnloaded") && hold.Contains("ScooterHoldRules.Returns("), "scooter hold source: it waits where the game's own rule waits");
        Check(ScooterCount(hold, "Notifications.Show(") == 1 && hold.Contains("ScooterHoldRules.Notices(taken)") && hold.Contains("\"reallife_garage_scooter_returned\""), "scooter hold source: one notice for the second's returns, outside the loop");
        Check(!hold.Contains("Update(") && !hold.Contains("LateUpdate") && !hold.Contains("new List") && !hold.Contains("new Dictionary"), "scooter hold source: no frame work and no allocation on the way");
        Check(hold.Contains("lastSeen.SetCurrentTime()") && !hold.Contains("new Timestamp") && !hold.Contains("TimeHelper.Now()"), "scooter hold source: the clock is written in place, nothing allocated");
        Check(hold.Contains("BuildingManager.IsInsideBuilding") && hold.Contains("GarageState.Bike(") && hold.Contains("save.ActiveVehicleId") && hold.Contains("autoDestroyAfterMinutes"),
            "scooter hold source: asked indoors for the hold, leaves the mod's bikes and the ridden scooter, and the limit is the game's own figure");
        string gstate = ModSource(@"Garage\GarageState.cs");
        Check(gstate.Contains("ScooterRentals.Read(") && gstate.Contains("ScooterRentals.Write(") && gstate.Contains("ScooterRentals.Clear()") && ModSource(@"Garage\ScooterRentals.cs").Contains("reallife:garage_scooter_days"),
            "scooter hold source: the rental days are read with the save's modData, written when it saves and cleared with the Garage");
        string director = ModSource(@"Garage\GarageDirector.cs");
        Check(director.Contains("ScooterHold.Tick(save)") && director.IndexOf("ScooterHold.Tick(save)", StringComparison.Ordinal) > director.IndexOf("private void Slow(", StringComparison.Ordinal),
            "scooter hold source: it runs from the director's second, not a frame");
        Check(ModSource(@"Core\Settings.cs").Contains("public static bool GarageScooterHold = true;"), "scooter hold source: on by default");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("Settings.GarageScooterHoldId, \"reallife_garage_scooter_hold\"")
              && ModSource(@"Meta\OptionsTree.cs").Contains("\"garage_scooter_hold\"") && ModSource(@"Meta\FeatureList.Life.cs").Contains("Settings.GarageScooterHoldId"),
            "scooter hold source: a toggle on the options page, in its tree and owned by the Garage's feature");
        Check(ModSource(@"Garage\BikeMount.cs").Contains("_autoDestroy.enabled = false;"), "scooter hold source: the mod's own bikes are still kept out of the game's away-clock");
        var en = EnglishOf();
        Check(en.TryGetValue("reallife_garage_scooter_hold", out string label) && label.Contains("midnight") && en.TryGetValue("reallife_opt_tip_garage_scooter_hold", out string tip) && tip.Length <= 190 && tip.Split(' ').Length <= 32
              && tip.Contains("midnight") && tip.Contains("rented"),
            "scooter hold texts: a label and a tip of at most 190 characters and 32 words that tell of midnight");
        Check(en.TryGetValue("reallife_guide_garage", out string guide) && label != null && guide.Contains("<b>" + label + "</b>") && guide.Contains("until midnight of the day you rented it") && guide.Contains("with a notice"),
            "scooter hold texts: the Guide says what the option does, under the option's own name");
        Check(en.TryGetValue("reallife_garage_scooter_returned", out string returned) && returned.Length <= 240 && returned.Contains("midnight"), "scooter hold texts: the notice says the day's rental ended at midnight, in at most 240 characters");
        Check(WhatsNewHas(en, "a scooter rented from the game's stands is no longer taken away while you are in a building"), "scooter hold texts: What's new tells of the first fix");
        Check(WhatsNewHas(en, "a rented scooter is now kept while you are indoors only for the day you rented it"), "scooter hold texts: What's new tells of the midnight return");
    }

    private static int ScooterCount(string text, string what)
    {
        int n = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }

    // One rented scooter's life against the game's own clock, a minute at a time, the mod's rules run once every tick minutes as the director's
    // second does. The game's away-clock is set when the scooter leaves sight and cleared when it is seen again. Returns the minute it was taken
    // (by the game or by the mod's return), or -1 if it was there at the end. A positive reload is a save and a load at that minute, a negative
    // one a load that lost the rental days.
    private static int ScooterRun(string id, int start, int end, int tick, bool optionOn, Func<int, bool> indoors, Func<int, bool> ridden, Func<int, bool> inSight,
        bool ourBike, int reload, bool keepRecords = false)
    {
        const int Limit = 240;
        if (!keepRecords)
        {
            ScooterRentals.Clear();
        }
        float? lastSeen = null;
        bool wasSight = !inSight(start);   // the game's first look sets its clock either way
        bool reloaded = false;
        for (int now = start; now <= end; now++)
        {
            bool sight = inSight(now);
            if (sight != wasSight)
            {
                wasSight = sight;
                lastSeen = sight ? (float?)null : now;
            }
            bool isRidden = ridden(now);
            if (!isRidden && lastSeen != null && ScooterHoldRules.GameTakes(Limit, true, now, lastSeen.Value))
            {
                return now;
            }
            if (reload != 0 && !reloaded && now >= Math.Abs(reload))
            {
                reloaded = true;
                string saved = ScooterRentals.Write(null);
                ScooterRentals.Clear();
                if (reload > 0)
                {
                    ScooterRentals.Read(saved);
                }
            }
            if (!optionOn || (now - start) % tick != 0)
            {
                continue;
            }
            int today = now / 1440;
            bool rentedToday = ScooterHoldRules.RentedToday(ScooterRentals.DayOf(id, today), today);
            bool counting = lastSeen != null;
            if (ScooterHoldRules.Holds(true, indoors(now), true, Limit, counting, true, isRidden, ourBike, rentedToday))
            {
                lastSeen = now;
            }
            else if (ScooterHoldRules.Returns(true, true, Limit, counting, true, isRidden, ourBike, rentedToday))
            {
                return now;
            }
        }
        return -1;
    }
}
