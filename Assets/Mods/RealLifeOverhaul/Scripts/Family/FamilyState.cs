using System;
using System.Collections.Generic;
using BAModAPI;
using Newtonsoft.Json;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>Where the player stands with their partner.</summary>
    public enum Bond { Single = 0, Dating = 1, Engaged = 2, Married = 3, Separated = 4, Divorced = 5 }

    /// <summary>Who looks after the small children in the day.</summary>
    public enum Care { You = 0, Partner = 1, Nursery = 2, Nanny = 3 }

    /// <summary>A child on the way: by a pregnancy or an adoption.</summary>
    public enum ProcessKind { None = 0, Pregnancy = 1, Adoption = 2 }

    /// <summary>One child: state, props and events, never a walking character.</summary>
    public sealed class ChildRecord
    {
        public string Name;
        public bool Girl;
        public float Age;            // years, on the options page's scale
        public bool Adopted;
        public int ArrivedDay;
        public float Closeness = 60f;   // 0..100: how close the player is to them
        public int School;           // 0 not yet, 1 public, 2 private
        public int Fund;             // the college fund, in money
        public int Pocket;           // index into FamilyBalance.PocketMoney
        public int Seen;             // bit per milestone already remembered
        public int Birthdays;        // the last birthday kept
        public float LastCheckup = -1f;
        public float LastReport;     // age at the last school report
        public float LastPlay;       // age at the last school play
        public float LastEvening;    // age at the last parents' evening
        public float LastDrawing;    // age at the last drawing on the fridge
        public bool Grown;           // left home
    }

    /// <summary>One line of the memories timeline.</summary>
    public sealed class Memory
    {
        public int Day;
        public string Key;           // a complete en.json key
        public string Who;           // fills {name} or {child}
    }

    /// <summary>Somewhere to be: a scan, a school play, a dinner. Kept in the save so that a load in the middle of the day loses nothing.</summary>
    public sealed class Errand
    {
        public string Kind;          // see Errands
        public int Day;
        public int FromHour;
        public int ToHour;
        public int Child = -1;
        public bool Told;
        public string Street;        // a booked place (a restaurant for dinner), or null for the nearest of its kind
        public int Number;
    }

    /// <summary>A wedding booked: where, how grand, what was paid for, and what has happened since.</summary>
    public sealed class WeddingPlan
    {
        public bool Active;
        public int Day;
        public int Venue;            // 0 city hall, 1 a place outdoors, 2 restaurant, 3 theatre
        public string Street;
        public int Number;
        public float X, Y, Z;        // the place outdoors the player chose
        public int Tier;             // 0 simple, 1 classic, 2 grand
        public bool Planner;
        public bool Flowers;
        public bool Music;
        public bool Catering;
        public bool Invited;
        public int Guests;
        public int Paid;
        public int Postponed;
        public int Missed;
        public bool ColdFeet;        // dealt with (or never came up)
        public bool TheirColdFeet;
    }

    /// <summary>Everything family life remembers about one save, written as one JSON string in the save's modData.</summary>
    public sealed class FamilySave
    {
        public int Version = 1;

        // The partner the default bridge keeps; with Social, it owns who they are and the name is mirrored here.
        public string PName;
        public int PSeed;
        public bool PFemale;
        public float PAffection;
        public int PMetDay;
        public int PJob;
        public int PIncome;          // a day
        public bool PWantsChildren = true;

        public int Bond;
        public int SinceDay;
        public bool LivesIn;
        public int MovedInDay;
        public string HomeStreet;
        public int HomeNumber;

        public float Harmony = FamilyBalance.HarmonyStart;
        public int LastTalkDay;
        public int LastSharedDay;
        public int LastChatDay;
        public int LastDateDay;
        public int BrokenPromises;
        public int LastArgueDay;
        public List<int> SeenArguments = new List<int>();
        public int LastArgument = -1;
        public string Hard;             // the difficult moments' memory (HardMemory's line); null from an older save
        public int LowDays;
        public int LastChoreDay;
        public int LastSupportDay;
        public int LastSickCareDay;
        public int LastContributionDay;
        public int LastGreetStamp;      // day * 24 + hour of the last greeting at the door
        public int LastCookDay;
        public int LastWatchDay;
        public int Dates;               // evenings out together
        public int LastNeglectDay;
        public int LastEncourageKey = -1;

        public int Ring;
        public int LastProposalDay;
        public int EngagedDay;
        public WeddingPlan Wedding = new WeddingPlan();
        public int MarriedDay;
        public bool HoneymoonAsked;
        public bool HoneymoonTaken;      // the trip was made: it is never offered again

        public List<ChildRecord> Children = new List<ChildRecord>();
        public int PKind;
        public int PStartDay;
        public int PDueDay;
        public int PStep;
        public int PCarrier;         // 0 the partner, 1 the player
        public int PAdoptAge;        // 0 a baby, 1 a toddler, 2 a child of school age
        public bool Cot, Pushchair, Supplies;
        public int ChildTalkDay;
        public int CareChoice;
        public int LastWeekDay;
        public int LastDay;
        public int NewParentUntil;
        public int LastWalkDay;
        public int LastNightDay;
        public int LastRunDay;
        public int LastFlavourDay;
        public int BirthHour;
        public int Walks;

        // The month's money, and all of it since the beginning.
        public int MonthStart;
        public int MIn, MChildcare, MSchool, MPocket, MFund, MOther;
        public int TIn, TOut;

        public List<Memory> Memories = new List<Memory>();
        public List<Errand> Errands = new List<Errand>();

        public bool InLaws;
        public int LastHoliday = -1;     // year * 4 + which
        public int LastMomDay;
        public int LastInLawDay;
        public int InLawAskDay;
        public int InLawAsk;         // what they asked for: 0 nothing, 1 a lift, 2 a loan, 3 a hand with a move
        public int InLawLoanDay;     // the day a loan is paid back; 0 none
        public int LastInLawChatDay;
        public int LastHolidayTold = -1;
        public int MomDinnerDay;     // a holiday the player may spend at Mom's
        public int MomDinnerWhich;
        public int LastMomVisitDay;
        public int BondBefore;       // what the two were before they separated
        public string NextOfKin;

        public int Counselling;
        public int LastCounselDay;
        public int SeparatedDay;
        public int Custody;          // 0 none, 1 every other weekend, 2 every weekend
        public int LastCustodyDay;
        public string ExName;
        public int FreeDay;          // the day the last relationship ended
        public int Interest;         // who the player asked to meet: 0 anyone, 1 women, 2 men (the options page sets the default)

        // The partner's work as the player's fortune grows (PartnerWork).
        public int PWork;            // PartnerWorkRules.FullTime or PartTime
        public bool PWorkAsk;        // they have asked to cut back, and it waits to be talked over
        public int PWorkAskDay;      // the day they last asked, or last went back to full days

        public string PJobKey;       // Social's partner's job in People (its locale key); null: the trade Family gave them (PJob)
        public string PGlad;         // what the player said yes to, whose warm texts are still to come (PartnerGlad); null none
        public int PGladDay;         // the day of that yes
        public int PGladNext;        // which of its texts comes next
        public int PVoiceDay;        // the day of the partner's last word about the player's fortune (PartnerVoice)
        public string PAsk;          // the kind of ask open now (PartnerVoice.Work and others); null none
        public int PAskDay;          // the day it was asked
        public int PAskReminded;     // reminders of it sent
        public int PFreeFrom;        // People's partner: the hours they are free on a weekday (PFreeTo 0: Family's own trades' hours)
        public int PFreeTo;
        public bool PNightOwl;       // ...and whether they keep late hours
    }

    /// <summary>Family life's per-save state, as JSON under one key of modData, so that a save made with the mod loads without it.</summary>
    public static class FamilyState
    {
        private const string Key = "reallife:family";
        private const int MostMemories = 80;

        public static IModLogger Logger;
        public static FamilySave S = new FamilySave();
        public static bool Loaded { get; private set; }

        public static Bond Bond
        {
            get => (Bond)Mathf.Clamp(S.Bond, 0, 5);
            set => S.Bond = (int)value;
        }

        public static bool Together => Bond == Bond.Dating || Bond == Bond.Engaged || Bond == Bond.Married;
        public static bool HasPartner => Together || Bond == Bond.Separated;
        public static Care Care => (Care)Mathf.Clamp(S.CareChoice, 0, 3);
        public static ProcessKind Process => (ProcessKind)Mathf.Clamp(S.PKind, 0, 2);

        public static void Reset()
        {
            S = new FamilySave();
            Loaded = false;
        }

        public static void Load()
        {
            S = new FamilySave();
            try
            {
                var save = SaveGameManager.Current;
                if (save != null && save.modData != null && save.modData.TryGetValue(Key, out string json) && !string.IsNullOrEmpty(json))
                {
                    FamilySave read = JsonConvert.DeserializeObject<FamilySave>(json);
                    if (read != null)
                    {
                        S = read;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Family: the saved family state could not be read and starts afresh.");
                S = new FamilySave();
                SaveLines.Unreadable(SaveGameManager.Current != null ? SaveGameManager.Current.modData : null, Key, "it could not be read");   // the line is kept as it is
            }
            // Whatever an older or a hand-edited save left out.
            if (S.Wedding == null)
            {
                S.Wedding = new WeddingPlan();
            }
            if (S.Children == null)
            {
                S.Children = new List<ChildRecord>();
            }
            if (S.Memories == null)
            {
                S.Memories = new List<Memory>();
            }
            if (S.Errands == null)
            {
                S.Errands = new List<Errand>();
            }
            if (S.SeenArguments == null)
            {
                S.SeenArguments = new List<int>();
            }
            S.Children.RemoveAll(c => c == null);
            S.Memories.RemoveAll(m => m == null || string.IsNullOrEmpty(m.Key));
            S.Errands.RemoveAll(e => e == null || string.IsNullOrEmpty(e.Kind));
            // In an older save "asked" meant taken or put off: the trip is in the memories if it was made.
            if (S.HoneymoonAsked && !S.HoneymoonTaken && S.Memories.Exists(m => m.Key == "reallife_family_memory_honeymoon"))
            {
                S.HoneymoonTaken = true;
            }
            while (S.Children.Count > FamilyBalance.MaxChildren)
            {
                S.Children.RemoveAt(S.Children.Count - 1);
            }
            S.Harmony = Mathf.Clamp(S.Harmony, 0f, 100f);
            S.PAffection = Mathf.Clamp(S.PAffection, 0f, 100f);
            Loaded = true;
            Logger?.Info($"Family: state loaded (bond {Bond}, lives in {S.LivesIn}, children {S.Children.Count}, memories {S.Memories.Count}).");
        }

        /// <summary>Called on the game's save event, before the save is written.</summary>
        public static void Write()
        {
            if (!Loaded)
            {
                return;
            }
            var save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            save.modData[Key] = JsonConvert.SerializeObject(S, Formatting.None);
        }

        /// <summary>The test button: nothing of the family is left, in memory or (from the next save on) in the save.</summary>
        public static void Clear()
        {
            S = new FamilySave();
            var save = SaveGameManager.Current;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(Key);
            }
        }

        public static void Harmony(float change)
        {
            S.Harmony = Mathf.Clamp(S.Harmony + change, 0f, 100f);
        }

        public static void Remember(string key, string who = null)
        {
            S.Memories.Add(new Memory { Day = TimeHelper.CurrentDay, Key = key, Who = who });
            while (S.Memories.Count > MostMemories)
            {
                S.Memories.RemoveAt(0);
            }
        }

        /// <summary>Children still at home (not grown up).</summary>
        public static int AtHome()
        {
            int count = 0;
            for (int i = 0; i < S.Children.Count; i++)
            {
                if (!S.Children[i].Grown)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>The youngest child of pushchair age, or null.</summary>
        public static ChildRecord Smallest()
        {
            ChildRecord best = null;
            for (int i = 0; i < S.Children.Count; i++)
            {
                ChildRecord c = S.Children[i];
                if (!c.Grown && c.Age < FamilyBalance.PushchairUntil && (best == null || c.Age < best.Age))
                {
                    best = c;
                }
            }
            return best;
        }

        public static bool AnyUnder(float years)
        {
            for (int i = 0; i < S.Children.Count; i++)
            {
                if (!S.Children[i].Grown && S.Children[i].Age < years)
                {
                    return true;
                }
            }
            return false;
        }

        public static Errand FindErrand(string kind)
        {
            for (int i = 0; i < S.Errands.Count; i++)
            {
                if (S.Errands[i].Kind == kind)
                {
                    return S.Errands[i];
                }
            }
            return null;
        }

        public static void DropErrand(string kind)
        {
            for (int i = S.Errands.Count - 1; i >= 0; i--)
            {
                if (S.Errands[i].Kind == kind)
                {
                    S.Errands.RemoveAt(i);
                }
            }
        }

        public static void MoneyIn(int amount)
        {
            S.MIn += amount;
            S.TIn += amount;
        }

        /// <param name="line">0 childcare, 1 school, 2 pocket money, 3 college fund, 4 everything else.</param>
        public static void MoneyOut(int amount, int line)
        {
            switch (line)
            {
                case 0: S.MChildcare += amount; break;
                case 1: S.MSchool += amount; break;
                case 2: S.MPocket += amount; break;
                case 3: S.MFund += amount; break;
                default: S.MOther += amount; break;
            }
            S.TOut += amount;
        }

        public static void RollMonth(int day)
        {
            if (S.MonthStart <= 0)
            {
                S.MonthStart = day;
            }
            if (day - S.MonthStart >= GameCalendar.MonthDays)
            {
                S.MonthStart = day;
                S.MIn = S.MChildcare = S.MSchool = S.MPocket = S.MFund = S.MOther = 0;
            }
        }
    }
}
