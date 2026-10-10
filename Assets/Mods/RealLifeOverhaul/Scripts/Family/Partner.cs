using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Who the partner is, whoever keeps them. With the Social bridge set, they are one of
    /// Social's people and everything here reads from and reports to it; without it, one minimal
    /// record in the family state (a name, a seed for their look, how fond they are) stands in,
    /// met and talked to through the "Someone special" phone contact.
    /// </summary>
    public static class Partner
    {
        private static readonly string[] Women =
        {
            "Maya", "Elena", "Sofia", "Hannah", "Priya", "Naomi", "Clara", "Jade", "Amara", "Rosa", "Ines", "Tessa", "Leila", "Freya", "Dalia", "Nora"
        };

        private static readonly string[] Men =
        {
            "Daniel", "Marco", "Theo", "Samir", "Lucas", "Andre", "Felix", "Jonah", "Kenji", "Mateo", "Elias", "Omar", "Nico", "Aaron", "Caleb", "Idris"
        };

        // What they do for a living, and what a day of it brings in. Keys are written out in full for the locale check.
        private static readonly string[] Trades =
        {
            "reallife_family_job_nurse", "reallife_family_job_teacher", "reallife_family_job_chef", "reallife_family_job_designer",
            "reallife_family_job_mechanic", "reallife_family_job_accountant", "reallife_family_job_librarian", "reallife_family_job_paramedic",
            "reallife_family_job_architect", "reallife_family_job_barista"
        };

        private static readonly int[] JobIncome = { 210, 180, 170, 230, 190, 260, 140, 200, 280, 110 };

        public static bool UsesSocial => FamilyBridges.Social != null && FamilyBridges.Social.Active;

        /// <summary>
        /// Social's person, if they are the partner Family keeps (or Family keeps none yet). A partner from before Social supplied them
        /// (engaged, married or living in with somebody else) stays on Family's own record until that is settled.
        /// </summary>
        public static bool Kept(out PersonInfo person)
        {
            person = default;
            return UsesSocial && FamilyBridges.Social.Ready && FamilyBridges.Social.TryGetPartner(out person)
                   && (string.IsNullOrEmpty(FamilyState.S.PName) || FamilyState.S.PName == person.Name);
        }

        public static string Name => string.IsNullOrEmpty(FamilyState.S.PName) ? FamilyText.T("reallife_family_someone") : FamilyState.S.PName;
        public static int Seed => FamilyState.S.PSeed;
        public static bool Female => FamilyState.S.PFemale;
        public static string JobKey => Trades[Mathf.Clamp(FamilyState.S.PJob, 0, Trades.Length - 1)];
        public static int Income => FamilyState.S.PIncome > 0 ? FamilyState.S.PIncome : JobIncome[Mathf.Clamp(FamilyState.S.PJob, 0, JobIncome.Length - 1)];

        /// <summary>How fond they are, 0 to 100: Social's figure when it keeps them, else the default record's.</summary>
        public static float Affection
        {
            get
            {
                if (Kept(out PersonInfo p))
                {
                    return Mathf.Clamp(p.Affection, 0f, 100f);
                }
                return FamilyState.S.PAffection;
            }
        }

        /// <summary>Days since the two began seeing each other.</summary>
        public static int DaysTogether
        {
            get
            {
                if (Kept(out PersonInfo p) && p.DaysTogether > 0)
                {
                    return p.DaysTogether;
                }
                return FamilyState.S.PMetDay > 0 ? TimeHelper.CurrentDay - FamilyState.S.PMetDay : 0;
            }
        }

        /// <summary>The default bridge only: somebody new, picked by the player's stated interest. Returns their name.</summary>
        public static string Meet(int interest)
        {
            FamilySave s = FamilyState.S;
            bool female = interest == 1 || (interest != 2 && Random.value < 0.5f);
            string[] names = female ? Women : Men;
            string name = names[Random.Range(0, names.Length)];
            if (name == s.ExName)
            {
                name = names[(System.Array.IndexOf(names, name) + 1) % names.Length];
            }
            s.PName = name;
            s.PFemale = female;
            s.PSeed = Random.Range(1, int.MaxValue);
            s.PAffection = FamilyBalance.AffectionStart;
            s.PMetDay = TimeHelper.CurrentDay;
            s.PJob = Random.Range(0, Trades.Length);
            s.PIncome = JobIncome[s.PJob];
            s.PWantsChildren = Random.value < 0.8f;
            s.Interest = interest;
            s.Harmony = FamilyBalance.HarmonyStart;
            FamilyState.Bond = Bond.Dating;
            s.SinceDay = TimeHelper.CurrentDay;
            s.LastTalkDay = TimeHelper.CurrentDay;
            s.LivesIn = false;
            return name;
        }

        /// <summary>
        /// Brings the family state into step with whoever keeps the partner. With Social: its
        /// partner is ours (name, look, sex mirrored); if it has none and nothing binds the two
        /// yet (not living together, not engaged or married), the player is single again.
        /// </summary>
        public static void Resync()
        {
            if (!UsesSocial || !FamilyBridges.Social.Ready)
            {
                return;   // (Social has not read its people yet: nobody is dropped for being unread)
            }
            FamilySave s = FamilyState.S;
            if (FamilyBridges.Social.TryGetPartner(out PersonInfo p) && !string.IsNullOrEmpty(p.Name))
            {
                bool other = s.PName != p.Name;
                if (other && (FamilyState.Bond == Bond.Engaged || FamilyState.Bond == Bond.Married || s.LivesIn))
                {
                    return;   // bound to somebody already: Social's new flame is not Family's business until that is settled
                }
                s.PName = p.Name;
                s.PSeed = p.LookSeed != 0 ? p.LookSeed : (p.Name.GetHashCode() & 0x7fffffff) | 1;
                s.PFemale = p.Female;
                s.PAffection = Mathf.Clamp(p.Affection, 0f, 100f);   // kept in step, so that the record carries on if Social loses them
                if (other || FamilyState.Bond == Bond.Single || FamilyState.Bond == Bond.Divorced)
                {
                    s.PJob = (s.PSeed & 0x7fffffff) % Trades.Length;
                    s.PIncome = JobIncome[s.PJob];
                    s.PWantsChildren = (s.PSeed & 7) != 0;
                    s.PMetDay = TimeHelper.CurrentDay - Mathf.Max(0, p.DaysTogether);
                    s.SinceDay = s.PMetDay;
                    s.Harmony = FamilyBalance.HarmonyStart;
                    FamilyState.Bond = Bond.Dating;
                }
            }
            else if (FamilyState.Bond == Bond.Dating && !s.LivesIn)
            {
                FamilyState.Bond = Bond.Single;
                s.FreeDay = TimeHelper.CurrentDay;
            }
        }

        /// <summary>
        /// Something happened between the two. Social is told and moves its own figures; the
        /// default record moves its affection by the same amount.
        /// </summary>
        public static void Tell(FamilyEvent what, float affection, float trust = 0f)
        {
            if (Kept(out _))
            {
                try
                {
                    FamilyBridges.Social.Changed(what, affection, trust);
                }
                catch (System.Exception e)
                {
                    FamilyDirector.Logger?.Error(e);
                }
                return;
            }
            FamilyState.S.PAffection = Mathf.Clamp(FamilyState.S.PAffection + affection, 0f, 100f);
        }

        /// <summary>The two part for good (a break-up, a divorce): the record is kept only as a name not to meet again.</summary>
        public static void Part()
        {
            FamilySave s = FamilyState.S;
            s.ExName = s.PName;
            s.LivesIn = false;
            s.FreeDay = TimeHelper.CurrentDay;
            s.Ring = 0;
            s.Wedding = new WeddingPlan();
            s.InLaws = false;
            s.PName = null;
            s.PSeed = 0;
            s.PAffection = 0f;
        }
    }
}
