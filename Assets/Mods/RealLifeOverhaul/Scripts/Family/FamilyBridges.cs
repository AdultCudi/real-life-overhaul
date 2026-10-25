using System;
using System.Collections.Generic;
using Entities;

namespace RealLifeOverhaul.Family
{
    /// <summary>Somebody as family life needs to know them: who they are, what they look like, how things stand.</summary>
    public struct PersonInfo
    {
        public string Id;            // whatever the owner uses to tell people apart; never shown
        public string Name;
        public int LookSeed;         // the seed their look is built from (AppearanceSetter.RandomizeElements)
        public bool Female;
        public float Affection;      // 0..100
        public float Trust;          // 0..100
        public int Stage;            // 0 stranger, 1 acquaintance, 2 friend, 3 close friend, 4 dating, 5 partner
        public int DaysTogether;     // days since they began dating
        public int LastContactDay;   // the owner's last call, text, outing or meeting with them (0: none known)
        public int Spend;            // how they are with money, a per cent of the usual allowance they expect (PartnerExpectRules); 0 unknown
        public string JobKey;        // what they do for a living, the owner's locale key said after "a"; null if the owner keeps none
        public int FreeFrom;         // the hours of a weekday they are free (0 and 0: the owner keeps none)
        public int FreeTo;
        public bool NightOwl;        // they keep late hours: up late, asleep in the morning
        public int Leaning;          // how they take the player's money: PartnerWealthRules.Neither, Supportive or Envious
    }

    /// <summary>What family life tells the owner of relationships about. Each is sent once, when it happens.</summary>
    public enum FamilyEvent
    {
        MovedIn, MovedOut, Engaged, ProposalDeclined, Married, WeddingPostponed, WeddingCancelled,
        Expecting, ChildArrived, Argued, MadeUp, Counselling, Separated, Reconciled, Divorced,
        SharedTime,      // a routine, a walk, a dinner at home: a little closer
        BrokenPromise    // stood up, a missed appointment: a little further apart
    }

    /// <summary>What Social implements so that the partner is one of its people; until it sets <see cref="FamilyBridges.Social"/>, Family keeps its own.</summary>
    public interface ISocialBridge
    {
        /// <summary>True while Social supplies the partner (its option is on); false and Family keeps its own stand-in.</summary>
        bool Active { get; }
        /// <summary>True once Social has read its people from the save: until then Family waits to read who the partner is.</summary>
        bool Ready { get; }
        /// <summary>The one person the player is dating or partnered with (Stage 4 or 5); Family never makes, renames or removes them.</summary>
        bool TryGetPartner(out PersonInfo partner);
        /// <summary>What happened to the two, with a suggested change in points of a hundred, which Social may scale.</summary>
        void Changed(FamilyEvent what, float affection, float trust);
        /// <summary>An outing with them ("dinner", "walk", "show"): false if Social cannot start one now; done(true) when it went well.</summary>
        bool StartOuting(string kind, Action<bool> done);
        /// <summary>Fills the list with the people close enough to ask to a wedding, family first, at most so many; returns how many.</summary>
        int ClosePeople(List<PersonInfo> into, int most);
        /// <summary>The partner's own contact in the phone, which Family's texts from the partner come from; null if there is none.</summary>
        Entities.Contact PartnerContact { get; }
        /// <summary>
        /// The partner took up another job in family life (retrained for the one they always wanted): Social's record says so,
        /// so that People and everything said about them names it. The key is one of Social's jobs (PartnerJobRules); false,
        /// with nothing changed, when there is no partner or the key is not one of them.
        /// </summary>
        bool PartnerJob(string jobKey);
        /// <summary>A chat face to face with the partner, Social's topics and answers; <paramref name="more"/> is the rest of the conversation. Null if Social cannot.</summary>
        DialogEntry ChatInPerson(Func<DialogEntry> more, Action leave);
    }

    /// <summary>What Home implements. Each call is true if Home did it its own way; false and Family does a short default (a few lines and a mood).</summary>
    public interface IHomeBridge
    {
        /// <summary>A meal cooked together from what the home has in; done(true) once it is served, done(false) if nothing was cooked.</summary>
        bool CookTogether(string partner, Action<bool> done);
        bool WatchTogether(string partner);
        /// <summary>The player is ill and the partner looks after them: Home shortens or eases the illness.</summary>
        bool CareWhenSick(string partner);
        /// <summary>A night broken by a baby: Home takes so many wakings out of the night's sleep.</summary>
        bool BrokenNight(int wakings);
        /// <summary>A health visit the player attends (a scan, a child's check-up); reason is a locale key.</summary>
        bool HealthVisit(string reason);
        /// <summary>Units of food in the home's fridges and portions put by, read while the player is in the home; -1 otherwise.</summary>
        int FoodUnits { get; }
        /// <summary>How tidy the player's home is (0..100); -1 if not known.</summary>
        float Tidiness { get; }
        /// <summary>The player's hygiene (0..100); -1 if not known.</summary>
        float Hygiene { get; }
        /// <summary>The game's dirt on the dirtiest of the player's cars (0..1); -1 with no car.</summary>
        float CarDirt { get; }
        /// <summary>The partner cleaned: the player's home is tidier by so many points of a hundred. False if Home keeps no tidiness.</summary>
        bool Tidied(float points);
    }

    /// <summary>What Travel implements: a trip for two. False and Family skips a few days with a mood.</summary>
    public interface ITravelBridge
    {
        bool Honeymoon(string partner, int days, Action<bool> done);
    }

    /// <summary>What Leisure implements: an evening out at one of its venues. False and Family does dinner at a restaurant door.</summary>
    public interface ILeisureBridge
    {
        bool DateNight(string partner, Action<bool> done);
        /// <summary>kind: "school_play" (a theatre), "birthday" (a venue party). child is the child's name.</summary>
        bool FamilyOuting(string kind, string child, Action<bool> done);
    }

    /// <summary>What Law implements: whether the player is locked up. An arrest itself is told with <see cref="FamilyApi.PlayerArrested"/>.</summary>
    public interface ILawBridge
    {
        /// <summary>True while the player is held (custody, prison): family life waits.</summary>
        bool InCustody { get; }
    }

    /// <summary>What the schools implement (Scripts/Schools): a child at Birchwood School, its fee, its reports and its gate.</summary>
    public interface ISchoolBridge
    {
        /// <summary>A child's day of fees at the campus, today's money (none at the player's own school); -1 when the child is not there.</summary>
        int FeeFor(string child);
        /// <summary>A child's reports at the campus: points over a public school's, from its results; NaN when the child is not there.</summary>
        float StandingOf(string child);
        /// <summary>The campus's address for a child there, for their school run and parents' evenings; false when not there.</summary>
        bool AddressFor(string child, out string street, out int number);
    }

    /// <summary>The doors to the features family life leans on: each is null until its owner sets it, and Family uses its own default.</summary>
    public static class FamilyBridges
    {
        public static ISocialBridge Social;
        public static IHomeBridge Home;
        public static ITravelBridge Travel;
        public static ILeisureBridge Leisure;
        public static ILawBridge Law;
        public static ISchoolBridge School;

        public static void Reset()
        {
            Social = null;
            Home = null;
            Travel = null;
            Leisure = null;
            Law = null;
            School = null;
        }
    }

    /// <summary>What the rest of the mod may ask of family life or tell it: safe at any time, with or without a save, the feature on or off.</summary>
    public static class FamilyApi
    {
        public static bool Active => FamilyDirector.Instance != null && FamilyState.Loaded && Settings.Family;
        public static bool HasPartner => Active && FamilyState.Together;
        public static bool LivesTogether => Active && FamilyState.Together && FamilyState.S.LivesIn;

        /// <summary>Whether the partner who lives with the player is free at that hour (awake, not at work) by the family's day; null when People's own hours stand.</summary>
        public static bool? PartnerFree(int hour, bool weekend) => Active ? HomeLife.PeopleFree(hour, weekend) : null;
        public static bool Engaged => Active && FamilyState.Bond == Bond.Engaged;
        public static bool Married => Active && FamilyState.Bond == Bond.Married;
        public static int Children => Active ? FamilyState.AtHome() : 0;
        public static string PartnerName => Active && FamilyState.HasPartner ? Partner.Name : null;

        /// <summary>True on the wedding day itself, for anything that should leave the player alone then.</summary>
        public static bool WeddingToday => Active && FamilyState.S.Wedding.Active && FamilyState.S.Wedding.Day == TimeHelper.CurrentDay;

        /// <summary>An arrest (1 a night in the cells, 2 charged, 3 prison), told once as it happens: taken hard, but it never ends things.</summary>
        public static void PlayerArrested(int severity)
        {
            if (Active)
            {
                FamilyDirector.Instance.Guarded(() => HardTimes.Arrest(severity));
            }
        }

        /// <summary>Social: who the partner is has changed outside Family's own flows (a break-up, somebody new).</summary>
        public static void PartnerChanged()
        {
            if (Active)
            {
                FamilyDirector.Instance.Guarded(Partner.Resync);
            }
        }

        /// <summary>The player's children into the lists (cleared): name, age, schooling (0 not yet, 1 public, 2 private) and whether grown; how many.</summary>
        public static int ChildrenOf(List<string> names, List<float> ages, List<int> schools, List<bool> grown)
        {
            names.Clear();
            ages.Clear();
            schools.Clear();
            grown.Clear();
            if (!Active)
            {
                return 0;
            }
            List<ChildRecord> kids = FamilyState.S.Children;
            for (int i = 0; i < kids.Count; i++)
            {
                names.Add(kids[i].Name);
                ages.Add(kids[i].Age);
                schools.Add(kids[i].School);
                grown.Add(kids[i].Grown);
            }
            return names.Count;
        }

        /// <summary>
        /// A child of school age moved to a private school (the schools' campus) or back to the public one: their fee, reports and
        /// school run follow from it. False when there is no such child, or they are not of school age.
        /// </summary>
        public static bool SetSchool(string name, bool privateSchool)
        {
            if (!Active || string.IsNullOrEmpty(name))
            {
                return false;
            }
            List<ChildRecord> kids = FamilyState.S.Children;
            for (int i = 0; i < kids.Count; i++)
            {
                ChildRecord c = kids[i];
                if (c.Name == name && !c.Grown && c.Age >= FamilyBalance.SchoolFrom)
                {
                    c.School = privateSchool ? 2 : 1;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Social or Leisure: time well spent with the partner that Family did not start itself.</summary>
        public static void SharedTime(float harmony)
        {
            if (Active && FamilyState.Together)
            {
                FamilyState.Harmony(UnityEngine.Mathf.Clamp(harmony, 0f, 10f));
                FamilyState.S.LastSharedDay = TimeHelper.CurrentDay;
            }
        }
    }
}
