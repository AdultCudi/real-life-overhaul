using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>One line of the criminal record.</summary>
    public struct RecordEntry
    {
        public int Day;         // the game day it happened
        public int Charges;     // LawBook.Bit of each charge
        public int Hours;       // in the cell (0 for a ticket or an escape)
        public int Fines;       // fines and damages paid at the time
        public Outcome Outcome;
    }

    /// <summary>The player's criminal record in this save. Safe to read at any time from any feature; empty until the save loads.</summary>
    public static class Record
    {
        public const int Most = 40;     // the oldest lines drop off

        private static readonly List<RecordEntry> Entries = new List<RecordEntry>(Most);

        /// <summary>Raised after a line is added, changed or the record is cleared.</summary>
        public static event Action Changed;

        public static int Count => Entries.Count;

        /// <summary>The record's lines, oldest first.</summary>
        public static RecordEntry Entry(int index) => Entries[index];

        public static bool Clean => Entries.Count == 0;

        public static bool Has(Charge charge)
        {
            return Has(charge, int.MaxValue);
        }

        /// <summary>True if this charge is on the record from the last so many game days.</summary>
        public static bool Has(Charge charge, int withinDays)
        {
            int bit = LawBook.Bit(charge);
            int today = Today;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if ((Entries[i].Charges & bit) != 0 && today - Entries[i].Day <= withinDays)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if nothing was added to the record in the last so many game days (a ticket counts).</summary>
        public static bool CleanFor(int days)
        {
            int since = DaysSinceLastOffence;
            return since < 0 || since >= days;
        }

        /// <summary>Game days since the latest line; -1 if there is none.</summary>
        public static int DaysSinceLastOffence
        {
            get
            {
                if (Entries.Count == 0)
                {
                    return -1;
                }
                return Mathf.Max(0, Today - Entries[Entries.Count - 1].Day);
            }
        }

        /// <summary>How many lines of any kind are from the last so many game days.</summary>
        public static int Recent(int withinDays)
        {
            int today = Today;
            int count = 0;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (today - Entries[i].Day <= withinDays)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>How many times the player was held in a cell in the last so many game days.</summary>
        public static int TimesHeld(int withinDays)
        {
            int today = Today;
            int count = 0;
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                RecordEntry entry = Entries[i];
                if (entry.Hours > 0 && entry.Outcome != Outcome.Held && today - entry.Day <= withinDays)
                {
                    count++;
                }
            }
            return count;
        }

        private static int Today => SaveGameManager.Current != null ? TimeHelper.CurrentDay : 0;

        /// <summary>Adds a line, and lets the reputation follow.</summary>
        internal static void Add(int charges, int hours, int fines, Outcome outcome)
        {
            if (Entries.Count >= Most)
            {
                Entries.RemoveAt(0);
            }
            Entries.Add(new RecordEntry { Day = Today, Charges = charges, Hours = Mathf.Max(0, hours), Fines = Mathf.Max(0, fines), Outcome = outcome });
            Reputation.Offence(charges);
            Changed?.Invoke();
        }

        /// <summary>How the latest line still "held" ended, and how long it really was.</summary>
        internal static void Close(Outcome outcome, int hoursServed)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Outcome == Outcome.Held)
                {
                    RecordEntry entry = Entries[i];
                    entry.Outcome = outcome;
                    entry.Hours = Mathf.Max(0, hoursServed);
                    Entries[i] = entry;
                    Changed?.Invoke();
                    return;
                }
            }
        }

        internal static void Clear()
        {
            Entries.Clear();
            Changed?.Invoke();
        }

        /// <summary>Nobody is in a cell: a line still "held" (saved between the arrest and the cell) becomes a roadside arrest.</summary>
        internal static void NobodyHeld()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Outcome == Outcome.Held)
                {
                    RecordEntry entry = Entries[i];
                    entry.Outcome = Outcome.Cited;
                    entry.Hours = 0;
                    Entries[i] = entry;
                }
            }
        }

        internal static void Read(string text)
        {
            Entries.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string line in text.Split(';'))
            {
                string[] parts = line.Split(',');
                if (parts.Length == 5 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int charges)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours)
                    && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fines)
                    && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int outcome)
                    && charges != 0 && outcome >= 0 && outcome <= (int)Outcome.Escaped && Entries.Count < Most)
                {
                    Entries.Add(new RecordEntry
                    {
                        Day = day, Charges = charges, Hours = Mathf.Clamp(hours, 0, LawBook.MostHours), Fines = Mathf.Max(0, fines), Outcome = (Outcome)outcome
                    });
                }
            }
        }

        internal static string Write()
        {
            var text = new StringBuilder(Entries.Count * 16);
            for (int i = 0; i < Entries.Count; i++)
            {
                RecordEntry entry = Entries[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                text.Append(entry.Day.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.Charges.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.Hours.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(entry.Fines.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(((int)entry.Outcome).ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// What the police and the public think of the player, 0 to 100 each: police standing falls with each line on the record
    /// and comes back with clean days, public standing rises with good deeds; a record grown lately means more scrutiny.
    /// </summary>
    public static class Reputation
    {
        public const int Start = 55;
        public const int ScrutinyDays = 5;          // game days of record the police go by
        public const int LikedFrom = 70;            // from here the public shows it
        public const int LovedFrom = 85;            // ...and now and then with a few dollars
        private const int DeedsCountedPerDay = 3;
        private const int CleanDayPolice = 2;       // a day with nothing new on the record
        private const int CleanDayPoliceMost = 75;  // ...gives trust back up to here; beyond it takes deeds

        private static readonly float[] ScrutinyFactor = { 1f, 1.15f, 1.3f, 1.5f };

        /// <summary>Standing with the police, 0 to 100.</summary>
        public static int Police { get; private set; } = Start;

        /// <summary>Standing with the public, 0 to 100.</summary>
        public static int Public { get; private set; } = Start;

        /// <summary>What the odds of a traffic offence being seen are multiplied by: 1, up to 1.5 with a long recent record.</summary>
        public static float Scrutiny { get; private set; } = 1f;

        /// <summary>True while the record is long enough that an officer tickets without a warning first.</summary>
        public static bool NoWarnings { get; private set; }

        /// <summary>0 to 3: how closely the police are watching.</summary>
        public static int ScrutinyLevel { get; private set; }

        /// <summary>True if the public thinks well enough of the player to show it.</summary>
        public static bool Liked => Public >= LikedFrom;

        /// <summary>Raised when either value or the level of scrutiny changes.</summary>
        public static event Action Changed;

        /// <summary>Raised for a good deed that counted (three a day at most): Flare offers it as a post (Wiring/FlareLinks).</summary>
        public static event Action Deed;

        internal static int LastDay = -1;           // the last game day the daily step ran for
        internal static int NoticedUntil = -1;      // a day the police watch more closely than the record says; not saved
        internal static int DeedDay = -1;
        internal static int Deeds;
        internal static int ThanksDay = -1;         // the last day a passer-by said thanks

        internal static void Reset()
        {
            Police = Start;
            Public = Start;
            Scrutiny = 1f;
            NoWarnings = false;
            ScrutinyLevel = 0;
            LastDay = -1;
            NoticedUntil = -1;
            DeedDay = -1;
            Deeds = 0;
            ThanksDay = -1;
        }

        /// <summary>A new line on the record: both values follow the offence table.</summary>
        internal static void Offence(int charges)
        {
            int police = 0, people = 0;
            for (int i = 0; i < LawBook.Count; i++)
            {
                if ((charges & (1 << i)) != 0)
                {
                    ChargeDef def = LawBook.Of((Charge)i);
                    police = Mathf.Min(police, def.Police);
                    people = Mathf.Min(people, def.Public);
                }
            }
            Police = Mathf.Clamp(Police + police, 0, 100);
            Public = Mathf.Clamp(Public + people, 0, 100);
            Refresh();
        }

        /// <summary>Something that earned the "helped" mood (staying with a casualty, reporting an accident, stopping a thief).</summary>
        public static void GoodDeed()
        {
            if (!LawState.Loaded || !CountDeed())
            {
                return;
            }
            Public = Mathf.Clamp(Public + 4, 0, 100);
            Police = Mathf.Clamp(Police + 1, 0, 100);
            Changed?.Invoke();
            try
            {
                Deed?.Invoke();
            }
            catch (Exception e)
            {
                LawDirector.Logger?.Error(e);
            }
        }

        /// <summary>A 911 call that led to an arrest: the police remember who called.</summary>
        public static void Reported()
        {
            if (!LawState.Loaded || !CountDeed())
            {
                return;
            }
            Police = Mathf.Clamp(Police + 5, 0, 100);
            Public = Mathf.Clamp(Public + 2, 0, 100);
            Changed?.Invoke();
        }

        /// <summary>A crime reported or a fine settled at a front desk: a little trust, counted with the day's good deeds.</summary>
        public static void AtTheDesk(int police)
        {
            if (!LawState.Loaded || police <= 0 || !CountDeed())
            {
                return;
            }
            Police = Mathf.Clamp(Police + police, 0, 100);
            Changed?.Invoke();
        }

        private static bool CountDeed()
        {
            int day = TimeHelper.CurrentDay;
            if (day != DeedDay)
            {
                DeedDay = day;
                Deeds = 0;
            }
            return ++Deeds <= DeedsCountedPerDay;
        }

        /// <summary>Once a game day: a clean day gives a little trust back, and the public forgets slowly either way.</summary>
        internal static void NewDay(int day)
        {
            if (LastDay >= 0 && day > LastDay)
            {
                int days = Mathf.Min(day - LastDay, 30);
                int since = Record.DaysSinceLastOffence;
                for (int i = 0; i < days; i++)
                {
                    if ((since < 0 || since > 0) && Police < CleanDayPoliceMost)
                    {
                        Police = Mathf.Min(CleanDayPoliceMost, Police + CleanDayPolice);
                    }
                    if (Public < Start)
                    {
                        Public++;
                    }
                }
            }
            LastDay = day;
            Refresh();
        }

        /// <summary>Works out the scrutiny from the record as it stands today.</summary>
        internal static void Refresh()
        {
            int recent = Record.Recent(ScrutinyDays);
            int level = recent >= 4 ? 3 : recent == 3 ? 2 : recent == 2 ? 1 : 0;
            if (LastDay >= 0 && LastDay < NoticedUntil)
            {
                level = Mathf.Min(3, level + 1);
            }
            ScrutinyLevel = level;
            Scrutiny = ScrutinyFactor[level];
            NoWarnings = level >= 2;
            Changed?.Invoke();
        }

        /// <summary>A picture posted while wanted (Wiring/FlareLinks): scrutiny one level up until the next day's step.</summary>
        public static void Noticed()
        {
            if (!LawState.Loaded || LastDay < 0)
            {
                return;
            }
            NoticedUntil = LastDay + 1;
            Refresh();
        }

        internal static void Set(int police, int people)
        {
            Police = Mathf.Clamp(police, 0, 100);
            Public = Mathf.Clamp(people, 0, 100);
            Refresh();
        }

        /// <summary>A locale key for how the police see the player.</summary>
        public static string PoliceWord =>
            Police < 25 ? "reallife_law_rep_police_1" : Police < 45 ? "reallife_law_rep_police_2" : Police < 65 ? "reallife_law_rep_police_3"
            : Police < 85 ? "reallife_law_rep_police_4" : "reallife_law_rep_police_5";

        /// <summary>A locale key for how the public sees the player.</summary>
        public static string PublicWord =>
            Public < 25 ? "reallife_law_rep_public_1" : Public < 45 ? "reallife_law_rep_public_2" : Public < 65 ? "reallife_law_rep_public_3"
            : Public < 85 ? "reallife_law_rep_public_4" : "reallife_law_rep_public_5";
    }

    /// <summary>What is kept about a stay in the cell, so that a save made in the middle of one resumes it.</summary>
    public sealed class CustodyState
    {
        public bool Active;
        public int Precinct = -1;
        public float ReleaseAt;      // game time, total minutes
        public float StartedAt;
        public int Charges;
        public int Hours;            // as sentenced
        public int Paid;             // fines and damages charged at the arrest
        public int Bail;             // 0: none
        public bool Lawyer;          // already called
        public string VehicleId;     // the car left at the scene, or null
        public bool Court;           // held by a court's order (Custody.Hold): no charge sheet, no lawyer, no line on the record
        public string Reason;        // ...and the locale key of why

        public void Clear()
        {
            Active = false;
            Precinct = -1;
            ReleaseAt = 0f;
            StartedAt = 0f;
            Charges = 0;
            Hours = 0;
            Paid = 0;
            Bail = 0;
            Lawyer = false;
            VehicleId = null;
            Court = false;
            Reason = null;
        }
    }

    /// <summary>The law's part of the save: five lines in modData, which the base game ignores, so a save loads without the mod.</summary>
    internal static class LawState
    {
        private const string KeyRecord = "reallife:law_record";
        private const string KeyReputation = "reallife:law_reputation";
        private const string KeyCustody = "reallife:law_custody";
        private const string KeyPrecincts = "reallife:law_precincts";
        private const string KeyStation = "reallife:law_station";

        public static IModLogger Logger;
        public static bool Loaded { get; private set; }
        public static readonly CustodyState Custody = new CustodyState();
        /// <summary>The precincts' addresses as saved ("street|number", ';' between), or null.</summary>
        public static string PrecinctAddresses;

        // The station's own few facts: one line in the save (KeyStation).
        /// <summary>2 once the precincts are chosen by the walk-in rules; a save from before is looked over once.</summary>
        public static int PrecinctRules;
        /// <summary>True once the "Police stations" map filter has been switched on for this save; then it is the player's.</summary>
        public static bool MapFilterShown;
        /// <summary>The car held at the pound, to be collected at a front desk; null if none.</summary>
        public static string ImpoundVehicleId;
        /// <summary>What the player is wanted for after getting away (LawBook bits) and the game day; 0 if nothing.</summary>
        public static int WantedCharges;
        public static int WantedDay = -1;
        /// <summary>The latest thing a police report can be asked for (Bridges.NoteIncident), and its game day.</summary>
        public static string IncidentKind;
        public static int IncidentDay = -1;
        /// <summary>How many police reports have been handed over in this save: the next one's number.</summary>
        public static int ReportsGiven;

        public static void Reset()
        {
            Loaded = false;
            Record.Clear();
            Reputation.Reset();
            Custody.Clear();
            PrecinctAddresses = null;
            PrecinctRules = 0;
            MapFilterShown = false;
            ImpoundVehicleId = null;
            WantedCharges = 0;
            WantedDay = -1;
            IncidentKind = null;
            IncidentDay = -1;
            ReportsGiven = 0;
        }

        public static void Load()
        {
            Reset();
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            Dictionary<string, string> data = save.modData;
            data.TryGetValue(KeyRecord, out string record);
            Record.Read(record);

            if (data.TryGetValue(KeyReputation, out string reputation) && !string.IsNullOrEmpty(reputation))
            {
                string[] parts = reputation.Split(',');
                if (parts.Length >= 6)
                {
                    Reputation.LastDay = Int(parts[2], -1);
                    Reputation.DeedDay = Int(parts[3], -1);
                    Reputation.Deeds = Int(parts[4], 0);
                    Reputation.ThanksDay = Int(parts[5], -1);
                    Reputation.Set(Int(parts[0], Reputation.Start), Int(parts[1], Reputation.Start));
                }
            }
            else
            {
                Reputation.Refresh();
            }

            if (data.TryGetValue(KeyCustody, out string custody) && !string.IsNullOrEmpty(custody))
            {
                string[] parts = custody.Split('|');
                if (parts.Length >= 9)
                {
                    Custody.Precinct = Int(parts[0], -1);
                    Custody.ReleaseAt = Float(parts[1]);
                    Custody.StartedAt = Float(parts[2]);
                    Custody.Charges = Int(parts[3], 0);
                    Custody.Hours = Int(parts[4], 0);
                    Custody.Paid = Int(parts[5], 0);
                    Custody.Bail = Int(parts[6], 0);
                    Custody.Lawyer = parts[7] == "1";
                    Custody.VehicleId = parts[8].Length > 0 ? parts[8] : null;
                    // A line from before the courts held people here has nine fields: an arrest's.
                    Custody.Court = parts.Length >= 11 && parts[9] == "1";
                    Custody.Reason = Custody.Court && parts[10].Length > 0 ? parts[10] : null;
                    Custody.Active = (Custody.Charges != 0 || Custody.Court) && Custody.ReleaseAt > 0f;
                }
            }
            data.TryGetValue(KeyPrecincts, out PrecinctAddresses);
            if (data.TryGetValue(KeyStation, out string station) && !string.IsNullOrEmpty(station))
            {
                string[] parts = station.Split('|');
                if (parts.Length >= 8)
                {
                    PrecinctRules = Int(parts[0], 0);
                    MapFilterShown = parts[1] == "1";
                    ImpoundVehicleId = parts[2].Length > 0 ? parts[2] : null;
                    WantedCharges = Int(parts[3], 0);
                    WantedDay = Int(parts[4], -1);
                    IncidentKind = parts[5].Length > 0 ? parts[5] : null;
                    IncidentDay = Int(parts[6], -1);
                    ReportsGiven = Int(parts[7], 0);
                }
            }
            if (!Custody.Active)
            {
                Record.NobodyHeld();
            }
            Loaded = true;
            Logger?.Info($"Law: state loaded. record={Record.Count} police={Reputation.Police} public={Reputation.Public} scrutiny={Reputation.ScrutinyLevel} inCustody={Custody.Active}");
        }

        /// <summary>From the game's save event, just before the save is written.</summary>
        public static void Save()
        {
            GameInstance save = SaveGameManager.Current;
            if (!Loaded || save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            Dictionary<string, string> data = save.modData;
            data[KeyRecord] = Record.Write();
            data[KeyReputation] = string.Join(",", Text(Reputation.Police), Text(Reputation.Public), Text(Reputation.LastDay),
                Text(Reputation.DeedDay), Text(Reputation.Deeds), Text(Reputation.ThanksDay));
            data[KeyCustody] = Custody.Active
                ? string.Join("|", Text(Custody.Precinct), Custody.ReleaseAt.ToString(CultureInfo.InvariantCulture), Custody.StartedAt.ToString(CultureInfo.InvariantCulture),
                    Text(Custody.Charges), Text(Custody.Hours), Text(Custody.Paid), Text(Custody.Bail), Custody.Lawyer ? "1" : "0", Clean(Custody.VehicleId),
                    Custody.Court ? "1" : "0", Clean(Custody.Reason))
                : string.Empty;
            if (PrecinctAddresses != null)
            {
                data[KeyPrecincts] = PrecinctAddresses;
            }
            data[KeyStation] = string.Join("|", Text(PrecinctRules), MapFilterShown ? "1" : "0", Clean(ImpoundVehicleId), Text(WantedCharges), Text(WantedDay),
                Clean(IncidentKind), Text(IncidentDay), Text(ReportsGiven));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Clean(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", string.Empty);

        private static int Int(string text, int fallback)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private static float Float(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }
    }
}
