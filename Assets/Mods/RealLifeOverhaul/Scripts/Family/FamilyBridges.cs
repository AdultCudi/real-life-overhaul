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
    }

    /// <summary>What family life tells the owner of relationships about. Each is sent once, when it happens.</summary>
    public enum FamilyEvent
    {
        MovedIn, MovedOut, Engaged, ProposalDeclined, Married, WeddingPostponed, WeddingCancelled,
        Expecting, ChildArrived, Argued, MadeUp, Counselling, Separated, Reconciled, Divorced,
        SharedTime,      // a routine, a walk, a dinner at home: a little closer
        BrokenPromise    // stood up, a missed appointment: a little further apart
    }

    /// <summary>
    /// What the Social feature implements so that the partner is one of ITS people. Until it does
    /// (<see cref="FamilyBridges.Social"/> is the default), family life keeps one minimal partner
    /// record of its own, reached through the "Someone special" phone contact.
    ///
    /// Social calls <c>FamilyBridges.Social = mine;</c> once its own state is loaded (and sets it
    /// back to null when it unloads). From then on:
    ///  - <see cref="TryGetPartner"/> returns the one person the player is dating or partnered
    ///    with (Stage 4 or 5), or false. Family never creates, renames or removes that person.
    ///  - <see cref="Changed"/> is how Family reports what happened to the two of them; Social
    ///    moves its own affection/trust (the suggested change is in points of a hundred; Social
    ///    may scale it) and may set flags of its own (moved in, engaged, married).
    ///  - <see cref="StartOuting"/> starts an outing with them (kind: "dinner", "walk", "show");
    ///    false if Social cannot start one now, and Family then runs its own short version.
    ///    <c>done(true)</c> when it went well, <c>done(false)</c> when it was abandoned.
    ///  - <see cref="ClosePeople"/> fills the list with the people close enough to ask to a
    ///    wedding (friends and closer, family first), at most <c>most</c>, and returns how many.
    ///  - Social tells Family about the other direction with <see cref="FamilyApi.PartnerChanged"/>
    ///    (they broke up outside Family's own flows, or began dating somebody new).
    /// </summary>
    public interface ISocialBridge
    {
        /// <summary>True while Social supplies the partner (its option is on). When it is false, Family keeps its own stand-in as if there were no bridge.</summary>
        bool Active { get; }
        /// <summary>True once Social has read its people from the save. Until then Family waits to read who the partner is.</summary>
        bool Ready { get; }
        bool TryGetPartner(out PersonInfo partner);
        void Changed(FamilyEvent what, float affection, float trust);
        bool StartOuting(string kind, Action<bool> done);
        int ClosePeople(List<PersonInfo> into, int most);
    }

    /// <summary>
    /// What the Home feature implements. Each call returns true if Home did it its own way (its
    /// own routine, its own sleep and health model); false and Family does its short default
    /// (a few lines and a mood).
    /// </summary>
    public interface IHomeBridge
    {
        /// <summary>Cook a meal together at home. Home runs its cooking routine with two at the stove.</summary>
        bool CookTogether(string partner);
        /// <summary>An evening in front of the television together.</summary>
        bool WatchTogether(string partner);
        /// <summary>The player is ill and the partner looks after them: Home shortens or eases the illness.</summary>
        bool CareWhenSick(string partner);
        /// <summary>A night broken by a baby: Home takes so many wakings out of the night's sleep.</summary>
        bool BrokenNight(int wakings);
        /// <summary>A health visit (a scan, a child's check-up) the player attends: Home may record it. reason is a locale key.</summary>
        bool HealthVisit(string reason);
    }

    /// <summary>What the Travel feature implements: a trip for two. False and Family skips a few days with a mood.</summary>
    public interface ITravelBridge
    {
        bool Honeymoon(string partner, int days, Action<bool> done);
    }

    /// <summary>What the Leisure feature implements: an evening out at one of its venues. False and Family does dinner at a restaurant door.</summary>
    public interface ILeisureBridge
    {
        bool DateNight(string partner, Action<bool> done);
        /// <summary>kind: "school_play" (a theatre), "birthday" (a venue party). child is the child's name.</summary>
        bool FamilyOuting(string kind, string child, Action<bool> done);
    }

    /// <summary>
    /// What Law / Justice implements: whether the player is locked up. An arrest itself is told with
    /// <see cref="FamilyApi.PlayerArrested"/>, once, when it happens, so that its weight comes with it.
    /// </summary>
    public interface ILawBridge
    {
        /// <summary>True while the player is held (custody, prison): family life waits.</summary>
        bool InCustody { get; }
    }

    /// <summary>
    /// The doors between family life and the features it leans on. Each is null until its owner
    /// sets it; Family then uses its own stand-alone default. Setting one is a single assignment
    /// from the owner's load, and setting it back to null on unload is good manners.
    /// </summary>
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

    /// <summary>
    /// What the rest of the mod may ask of family life or tell it. Safe to call at any time, with
    /// or without a save loaded, with the feature on or off.
    /// </summary>
    public static class FamilyApi
    {
        public static bool Active => FamilyDirector.Instance != null && FamilyState.Loaded && Settings.Family;
        public static bool HasPartner => Active && FamilyState.Together;
        public static bool LivesTogether => Active && FamilyState.Together && FamilyState.S.LivesIn;
        public static bool Engaged => Active && FamilyState.Bond == Bond.Engaged;
        public static bool Married => Active && FamilyState.Bond == Bond.Married;
        public static int Children => Active ? FamilyState.AtHome() : 0;
        public static string PartnerName => Active && FamilyState.HasPartner ? Partner.Name : null;

        /// <summary>True on the wedding day itself, for anything that should leave the player alone then.</summary>
        public static bool WeddingToday => Active && FamilyState.S.Wedding.Active && FamilyState.S.Wedding.Day == TimeHelper.CurrentDay;

        /// <summary>
        /// Law / Justice: the player has been arrested (or convicted). severity 1 a night in the
        /// cells, 2 charged, 3 prison. The partner takes it hard; it never ends things by itself.
        /// Call it once for each arrest or sentence, when it happens (Wiring.Arrests does).
        /// </summary>
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

        /// <summary>Social / Leisure: time well spent with the partner that Family did not start itself.</summary>
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
