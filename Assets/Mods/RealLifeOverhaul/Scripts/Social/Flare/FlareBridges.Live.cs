using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// What the living network asks of the rest of the mod (Wiring/FlareLinks fills each in). Each is optional: without
    /// one the network still runs (the city's posts from what Flare itself knows, the City Office answering without
    /// acting), so Social/Flare works alone.
    /// </summary>
    public static partial class FlareBridges
    {
        /// <summary>The city as the day's posts read it: the weather, the news, prices, the calendar, the election, the works, the market, the districts, a sample of the city's businesses. Default: what Flare knows itself.</summary>
        public static Action<FlareCityFacts> CityFacts;

        /// <summary>
        /// The City Office sends a crew: a site of the council's works (Civic/CivicWorks) at the building with a street door
        /// nearest the point reported (or, with none, one in the district picked by the seed): roadworks for a road, utility
        /// works for a lamp. Its id, the days it should take and its address in words. False if no crew can go there (no
        /// building near enough, a site there already, civic life off, a guest of a session).
        /// </summary>
        public static WorksAsk Works;

        public delegate bool WorksAsk(FlareReport kind, string district, float x, float z, int seed, out int site, out int days, out string address);

        /// <summary>True once a site of the council's works has been handed over, or is gone. Default: never.</summary>
        public static Func<int, bool> WorksDone;

        /// <summary>A building site of the council's within so many metres of a point. Default: none.</summary>
        public static Func<float, float, float, bool> SiteNear;

        /// <summary>The inspector looks at the site nearest a point reported as unsafe (Civic/CivicWorks.ReportUnsafe). True if it was unsafe. Default: nothing.</summary>
        public static Func<float, float, bool> SiteReported;

        /// <summary>Where the player stands now: the police patrolling there, a cleaning crew's round there (once an hour). Default: nothing.</summary>
        public static Action<bool, bool> Watched;

        /// <summary>The district the player stands in (the game's own id), or null. Default: none.</summary>
        public static Func<string> PlayerDistrict;

        /// <summary>The names of the staff of the player's businesses, a few of them, into the list given. Default: none.</summary>
        public static Action<List<string>> Staff;

        /// <summary>The save's own photos' folder (Photos: persistentDataPath/RealLifeOverhaul/Photos/(the save)), where the city's photos are kept beside them. Default: none (no city photos).</summary>
        public static Func<string> PictureFolder;

        /// <summary>The save's id written beside its photos (so a deleted save's go with them: Photos/PhotoStore.MarkSave); called on a worker thread. Default: nothing.</summary>
        public static Action MarkPictureFolder;

        /// <summary>The rival tycoons of the end game who are in play (Rivals), into the list given, each with their latest move announced. Default: none.</summary>
        public static Action<List<FlareTycoon>> Tycoons;

        /// <summary>The player's own name, as the game has it. Default: none (the app says "you").</summary>
        public static Func<string> PlayerName;

        /// <summary>The way to a place ("street~number") is set on the map, as the game's own "set destination" does. Default: nothing.</summary>
        public static Action<string> Destination;

        /// <summary>The way to the street event on now is set on the map. False if none is on. Default: none.</summary>
        public static Func<bool> EventDestination;

        /// <summary>The phone is put away and photo mode starts as a selfie (Leisure/Photo/PhotoMode): the picture saved comes back as a draft. Default: nothing.</summary>
        public static Action TakeSelfie;

        /// <summary>
        /// The phone's own camera opens (Photos/CameraMode, the Camera app's): its first photo comes back to the compose sheet
        /// through SharePhoto. False when the Camera is off: Flare then takes its own shot of the screen. Default: none.
        /// </summary>
        public static Func<bool> OpenCamera;

        /// <summary>One of the player's businesses is hiring (an announcement this week): somebody may write asking for work. Default: no.</summary>
        public static Func<bool> Hiring;

        /// <summary>
        /// The city's real faults (Scripts/Faults) judge a report before the City Office acts: kind, district, where it was
        /// made (zero for a district picked), the reporter ("me", "p:" a player, "c" one of the city's people), the case and,
        /// for a business, its place. FaultVerdict's number (0 true, 1 nothing, 2 nothing and a warning, 3 no crew for
        /// this player for now, 4 a clean business), or -1 to answer as without them; for a real fault, where it is.
        /// Default: not judged.
        /// </summary>
        public static FaultAsk FaultCheck;

        public delegate int FaultAsk(FlareReport kind, string district, float x, float z, string reporter, int caseId, string place, out float fx, out float fz, out int fault);

        /// <summary>A crew is sent on a case the faults judged true: the case, the fault it found (-1: one another case has in hand), works or not. Default: nothing.</summary>
        public static Action<int, int, bool> FaultActed;

        /// <summary>A case's works are handed over: its fault is mended. Default: nothing.</summary>
        public static Action<int> FaultDone;

        /// <summary>True while the city's faults are real: the city's people report only those (they post them through the faults). Default: no.</summary>
        public static Func<bool> FaultsReal;

        private static void ResetLive()
        {
            FaultCheck = null;
            FaultActed = null;
            FaultDone = null;
            FaultsReal = null;
            CityFacts = null;
            Works = null;
            WorksDone = null;
            SiteNear = null;
            SiteReported = null;
            Watched = null;
            PlayerDistrict = null;
            Staff = null;
            PictureFolder = null;
            MarkPictureFolder = null;
            Tycoons = null;
            PlayerName = null;
            Destination = null;
            EventDestination = null;
            TakeSelfie = null;
            OpenCamera = null;
            Hiring = null;
        }
    }
}
