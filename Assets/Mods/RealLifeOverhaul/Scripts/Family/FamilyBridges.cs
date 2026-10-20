using System;
using System.Collections.Generic;

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
        public string JobKey;        // what they do for a living, the owner's locale key said after "a"; null if the owner keeps none
        public int FreeFrom;         // the hours of a weekday they are free (0 and 0: the owner keeps none)
        public int FreeTo;
        public bool NightOwl;        // they keep late hours: up late, asleep in the morning
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
    }

    /// <summary>What Home implements. Each call is true if Home did it its own way; false and Family does a short default (a few lines and a mood).</summary>
    public interface IHomeBridge
    {
        bool CookTogether(string partner);
        bool WatchTogether(string partner);
        /// <summary>The player is ill and the partner looks after them: Home shortens or eases the illness.</summary>
        bool CareWhenSick(string partner);
        /// <summary>A night broken by a baby: Home takes so many wakings out of the night's sleep.</summary>
        bool BrokenNight(int wakings);
        /// <summary>A health visit the player attends (a scan, a child's check-up); reason is a locale key.</summary>
        bool HealthVisit(string reason);
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

    /// <summary>The doors to the features family life leans on: each is null until its owner sets it, and Family uses its own default.</summary>
    public static class FamilyBridges
    {
        public static ISocialBridge Social;
        public static IHomeBridge Home;
        public static ITravelBridge Travel;
        public static ILeisureBridge Leisure;
        public static ILawBridge Law;

        public static void Reset()
        {
            Social = null;
            Home = null;
            Travel = null;
            Leisure = null;
            Law = null;
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
