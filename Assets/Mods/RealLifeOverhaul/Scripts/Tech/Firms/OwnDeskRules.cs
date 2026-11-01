namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The owner as a professional at a desk of their own firm, as Careers reads them for the firm's type (TechBridges.Owner):
    /// the profession and rank they could come in at now, its role and level, and their skills. Never saved: it is read again
    /// from what they have learnt each time it is asked.
    /// </summary>
    public struct OwnerStanding
    {
        public int Prof;      // the profession of Careers/Work/ProCatalogue they work as here
        public int Rank;      // its rank they could come in at now (ProRules.BestRank)
        public int Role;      // the professions' role of that rank (TalentCatalogue.Roles)
        public int Level;     // its level, junior to principal
        public int Main;      // the points of the rank's two skills, 0 to 100: what a professional's main skill is to them
        public int A, B;      // the role's two specialisms, thousandths of a point, as a professional's
        public int Burnout;   // thousandths: a job's of the player's, 0 without one
    }

    /// <summary>
    /// The owner's own work at a desk of their firm (TechDirector.OwnDesk), game-free: their minutes kept by the day as a
    /// professional's hours are, and what those minutes give the next morning's crew: exactly what a professional of the same
    /// role, level and skills gives for the same hours (TalentRules.Output, FirmsRules.Join), with no pay and no seat. Cost:
    /// a few sums a firm each morning; nothing per frame.
    /// </summary>
    public static class OwnDeskRules
    {
        public const int StintMinutes = TalentCatalogue.DayHours * 60;   // minutes: a stint at the desk is a working day at most, stopped at any time
        public const int Morale = 750;        // thousandths: the owner's heart in their own firm, an output factor of one (TalentRules.Output's 700 + 750 * 400 / 1000)
        public const int DayMost = 24 * 60;   // minutes a day can hold, whatever a fast forward over midnight credits

        /// <summary>Minutes worked at a firm on a day: added to that day's count; a later day starts a new count and keeps the one before.</summary>
        public static void Credit(Firm f, int day, int minutes)
        {
            if (f == null || minutes <= 0)
            {
                return;
            }
            if (day == f.OwnDay)
            {
                f.OwnMinutes = System.Math.Min(DayMost, f.OwnMinutes + minutes);
            }
            else if (day == f.OwnPrevDay)
            {
                f.OwnPrevMinutes = System.Math.Min(DayMost, f.OwnPrevMinutes + minutes);
            }
            else if (day > f.OwnDay)
            {
                f.OwnPrevDay = f.OwnDay;
                f.OwnPrevMinutes = f.OwnMinutes;
                f.OwnDay = day;
                f.OwnMinutes = System.Math.Min(DayMost, minutes);
            }
        }

        /// <summary>The owner's minutes at a firm on a day, from either count; 0 for a day neither holds.</summary>
        public static int MinutesOn(Firm f, int day)
        {
            return f == null || day < 0 ? 0 : day == f.OwnDay ? f.OwnMinutes : day == f.OwnPrevDay ? f.OwnPrevMinutes : 0;
        }

        /// <summary>The owner as one of the firm's professionals would be filed: their role, level, skills and burnout, the morale of <see cref="Morale"/>.</summary>
        public static Profile AsProfile(in OwnerStanding s)
        {
            return new Profile
            {
                Role = s.Role, Level = TalentCatalogue.Clamp(s.Level, 0, TalentCatalogue.Levels - 1), A = TalentCatalogue.Clamp(s.A, 0, 100000),
                B = TalentCatalogue.Clamp(s.B, 0, 100000), Morale = Morale, Burnout = TalentCatalogue.Clamp(s.Burnout, 0, 1000)
            };
        }

        /// <summary>The role of a standing, or null for one that is no role of the professions'.</summary>
        public static RoleDef RoleOf(in OwnerStanding s) => s.Role >= 0 && s.Role < TalentCatalogue.RoleCount ? TalentCatalogue.Role(s.Role) : null;

        /// <summary>The work points the owner's minutes give, before the office's connection: a professional's of the same profile for the same time.</summary>
        public static int Points(in OwnerStanding s, int minutes, int day)
        {
            if (minutes <= 0 || !FirmsRules.Counts(RoleOf(s)))
            {
                return 0;
            }
            long output = TalentRules.Output(AsProfile(s), TalentCatalogue.Clamp(s.Main, 0, 100), day);
            return (int)(output * FirmsCatalogue.PointsPerDay * minutes / (TalentCatalogue.DayHours * 60) / 1000);
        }

        /// <summary>
        /// The owner's minutes of a day in a crew of their own, to join the firm's with <see cref="Add"/>; their engineering
        /// counts in the firm's seniors and faults as a professional's does. Returns the points.
        /// </summary>
        public static int Join(ref Crew own, in OwnerStanding s, int minutes, int day, ref int seniors, ref long faultWeighted)
        {
            RoleDef r = RoleOf(s);
            int points = Points(s, minutes, day);
            if (r == null || minutes <= 0 || !FirmsRules.Counts(r))
            {
                return 0;
            }
            Profile p = AsProfile(s);
            FirmsRules.Join(ref own, r, points, (minutes + 59) / 60, p.Level >= TalentCatalogue.Senior, TalentRules.Faults(p, TalentCatalogue.Clamp(s.Main, 0, 100)), ref seniors,
                ref faultWeighted);
            return points;
        }

        /// <summary>The owner's crew joined to the firm's: their points and a support hour, nothing to wages, seats, security or reliability.</summary>
        public static void Add(ref Crew c, in Crew own)
        {
            c.Engineering += own.Engineering;
            c.Testing += own.Testing;
            c.Design += own.Design;
            c.Web += own.Web;
            c.Product += own.Product;
            c.Selling += own.Selling;
            c.Support += own.Support;
        }

        /// <summary>The owner's hours yesterday (begun hours) where their role does the work asked (-1: any of the firm's), as a professional's are listed; else 0.</summary>
        public static int HoursFor(int minutes, int role, int kind, FirmKind firm)
        {
            if (minutes <= 0 || role < 0 || role >= TalentCatalogue.RoleCount)
            {
                return 0;
            }
            RoleDef r = TalentCatalogue.Role(role);
            return (kind >= 0 ? FirmsRules.CanDo(r, (WorkKind)kind) : FirmsRules.WorksFor(r, firm)) ? (minutes + 59) / 60 : 0;
        }

        /// <summary>The owner's hours yesterday counted as worked (<see cref="HoursFor"/>), so nobody is said to have been idle when they were at a desk.</summary>
        public static void Count(ref FirmFacts f, int minutes, int role, int kind, FirmKind firm)
        {
            f.Worked += HoursFor(minutes, role, kind, firm);
        }
    }
}
