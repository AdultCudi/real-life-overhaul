using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What the love life (Social/Love) asks of family life, reaching only its existing flows: a separation is Family's (never a second
    /// one), the partner at home waits to talk through the same ask as the partner's other questions, the in-laws speak through their
    /// own contact, a child of a night joins the children, and what comes next after living together is read from the bond. Safe at any
    /// time; nothing when family life is off.
    /// </summary>
    public static class FamilyAffairs
    {
        public const string ConfrontKind = "confront";

        /// <summary>The person is the partner family life keeps and lives with the player, is engaged to them or is married to them.</summary>
        public static bool Bound(string name)
        {
            return FamilyApi.Active && FamilyState.Together && Partner.Name == name && (FamilyState.S.LivesIn || FamilyState.Bond == Bond.Engaged || FamilyState.Bond == Bond.Married);
        }

        /// <summary>Family's own separation: <paramref name="theirs"/> when it is their decision.</summary>
        public static void Separate(bool theirs)
        {
            if (FamilyApi.Active)
            {
                FamilyDirector.Instance.Guarded(() => HardTimes.Separate(theirs));
            }
        }

        /// <summary>The partner at home waits to talk: their prompt says so, and speaking to them opens the talk (as for their other asks).</summary>
        public static void Raise()
        {
            if (FamilyApi.Active && FamilyState.Together && FamilyState.S.LivesIn)
            {
                FamilySave s = FamilyState.S;
                s.PAsk = ConfrontKind;
                s.PAskDay = TimeHelper.CurrentDay;
                s.PAskReminded = 0;
            }
        }

        /// <summary>Nobody waits at home to talk about it.</summary>
        public static void Lower()
        {
            if (FamilyApi.Active && FamilyState.S.PAsk == ConfrontKind)
            {
                FamilyState.S.PAsk = null;
            }
        }

        public static void InLawsHear(string key)
        {
            if (FamilyApi.Active)
            {
                FamilyDirector.Instance.Guarded(() => Relatives.InLawsSay(key));
            }
        }

        /// <summary>The child joins the family's children; null when it cannot.</summary>
        public static string AddChild(bool girl, string otherName, int otherIndex, bool away)
        {
            return FamilyApi.Active ? Children.ArriveFromNight(girl, otherName, otherIndex, away) : null;
        }

        public static int ChildRoom() => FamilyApi.Active ? Children.Room() : 0;

        /// <summary>
        /// What comes next for a partner who lives with the player, as a key of the Guide's path from dating to marriage and the days still to go
        /// (the ring needs <see cref="FamilyBalance.ProposeDaysTogether"/> together); null when family life says nothing.
        /// </summary>
        public static string NextStep(out int days)
        {
            days = 0;
            if (!FamilyApi.Active)
            {
                return null;
            }
            FamilySave s = FamilyState.S;
            switch (FamilyState.Bond)
            {
                case Bond.Dating:
                    if (!s.LivesIn)
                    {
                        return null;   // moving in is the figures' step (SocialRules.Next)
                    }
                    if (s.Ring == 0)
                    {
                        days = Mathf.Max(0, FamilyBalance.ProposeDaysTogether - Partner.DaysTogether);
                        return "reallife_love_next_ring";
                    }
                    return "reallife_love_next_propose";
                case Bond.Engaged:
                    return s.Wedding.Active ? "reallife_love_next_wedding" : "reallife_love_next_plan";
                case Bond.Married:
                    return s.Harmony < FamilyBalance.HarmonyCounsel ? "reallife_love_next_hard" : "reallife_love_next_married";
                case Bond.Separated:
                    return "reallife_love_next_separated";
                case Bond.Divorced:
                    return "reallife_love_next_divorced";
                default:
                    return null;
            }
        }

        /// <summary>The day the wedding is on, 0 for none.</summary>
        public static int WeddingDay => FamilyApi.Active && FamilyState.S.Wedding.Active ? FamilyState.S.Wedding.Day : 0;
    }
}
