using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the city could have heard of the player, as the other features already measure it: the public's standing (Law's Reputation, 0 to 100, 55
    /// for a newcomer), whether there is a record, a case open or a scandal, how well the city knows them (Standing's recognition), their Flare
    /// followers, and the city's view of their integrity (Standing's, which charity and good deeds raise and bribes lower; 50 is neutral). The
    /// defaults are a newcomer nobody has heard of. Filled by Wiring, through <see cref="SocialLinks.Repute"/>.
    /// </summary>
    public struct SocialRepute
    {
        public int Public;        // 0 notorious to 100 spotless
        public bool HasRecord;    // a conviction, a cell, or wanted
        public bool Charged;      // a case is open against them
        public bool Scandal;      // Standing's scandal is on
        public int Recognition;   // 0 nobody knows them to 100 everybody does
        public int Followers;     // on Flare
        public int Integrity;     // 0 to 100, 50 neutral

        /// <summary>A newcomer: a public of 55, no record, nobody knows them and integrity neutral.</summary>
        public static SocialRepute Newcomer => new SocialRepute { Public = 55, Integrity = 50 };
    }

    /// <summary>
    /// First impressions: the trust a newly met person starts with moves with what they could have heard of the player, one rule for every way of
    /// meeting (<see cref="SocialLife.Meet"/>). Good for known goodness (a good public standing, renown, followers, a good name for integrity and
    /// charity) and bad for known badness (a record, an open case, a scandal, a low standing, being notorious). Bounded to <see cref="Most"/> either
    /// way. Game-free. COST: arithmetic, once when somebody is met.
    /// </summary>
    public static class SocialImpression
    {
        public const int Most = 10;              // trust points either way, at most
        public const int Neutral = 55;           // the newcomer's public standing: nothing is added or taken for it
        public const int GoodStep = 9;           // points of public standing above the neutral for each trust point (up to 5 at 100)
        public const int BadStep = 11;           // ...below it, for each trust point taken (up to 5 at 0)
        public const int KnownFrom = 35;         // recognition from which the city knows them (Standing's Known tier); each tier after adds a point
        public const int ProminentFrom = 55;
        public const int FamousFrom = 75;
        public const int FollowersSome = 200;    // Flare followers: a point...
        public const int FollowersMany = 2000;   // ...and two
        public const int IntegrityGood = 60, IntegrityHigh = 75;   // integrity: a point, then two
        public const int IntegrityLow = 40, IntegrityBad = 25;     // ...and one, then two taken
        public const int NotoriousUnder = 40;    // a public standing from which the city thinks badly of them: renown works against them
        public const int RecordTrust = 3;        // trust taken for a record, a case open (one more) and a scandal
        public const int ChargedTrust = 1;
        public const int ScandalTrust = 3;

        /// <summary>
        /// The trust points added to a new person's first trust, from <c>-<see cref="Most"/></c> to <see cref="Most"/>. A newcomer nobody has heard of is
        /// 0. Renown is a good thing for somebody the city thinks well of and a bad one for somebody it does not; followers count for somebody who is
        /// not notorious.
        /// </summary>
        public static int Of(in SocialRepute r)
        {
            int standing = r.Public >= Neutral ? (r.Public - Neutral) / GoodStep : -((Neutral - r.Public) / BadStep);
            int tier = r.Recognition >= FamousFrom ? 3 : r.Recognition >= ProminentFrom ? 2 : r.Recognition >= KnownFrom ? 1 : 0;
            int renown = r.Public >= Neutral ? tier : r.Public < NotoriousUnder ? -tier : 0;
            int followers = r.Public < NotoriousUnder ? 0 : r.Followers >= FollowersMany ? 2 : r.Followers >= FollowersSome ? 1 : 0;
            int name = r.Integrity >= IntegrityHigh ? 2 : r.Integrity >= IntegrityGood ? 1 : r.Integrity <= IntegrityBad ? -2 : r.Integrity <= IntegrityLow ? -1 : 0;
            int bad = (r.HasRecord ? RecordTrust : 0) + (r.Charged ? ChargedTrust : 0) + (r.Scandal ? ScandalTrust : 0);
            int total = standing + renown + followers + name - bad;
            return Math.Max(-Most, Math.Min(Most, total));
        }

        /// <summary>
        /// The figures somebody starts with at the meeting: the warm or cool start, a little more trust and affection for somebody who was helped
        /// (they have seen what the player is like), the impression added to trust, and the first standing the friends gave (<see cref="SocialFriendsRules.Standing"/>,
        /// <paramref name="standing"/> the answers right, -1 for no quiz). With no impression and no quiz they are exactly the figures every version gave.
        /// </summary>
        public static void Start(bool warm, SocialOrigin origin, int impression, int standing, out float familiarity, out float affection, out float trust)
        {
            familiarity = warm ? 12f : 8f;
            affection = warm ? 14f : 8f;
            trust = warm ? 12f : 9f;
            if (origin == SocialOrigin.Helped)
            {
                trust += 14f;   // they have seen what the player is like
                affection += 8f;
            }
            if (standing >= 0)
            {
                SocialWorth first = SocialFriendsRules.Standing(standing);
                familiarity += first.Familiarity;
                affection += first.Affection;
                trust += first.Trust;
            }
            trust += impression;
            familiarity = SocialRules.Clamp(familiarity);
            affection = SocialRules.Clamp(affection);
            trust = SocialRules.Clamp(trust);
        }
    }
}
