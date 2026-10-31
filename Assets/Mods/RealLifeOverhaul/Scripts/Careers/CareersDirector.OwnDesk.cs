using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The player at a desk of their own tech firm (Tech's TechDirector.OwnDesk, through TechBridges.Owner and OwnerWorked):
    /// the profession they work as there, read from what they have learnt (OwnRules.FirmProfession), and what the hours do to
    /// them: learning at work as a contract's hours teach, a job's burnout for a long week, and a restraint clause in the same
    /// field. No pay: it is their own firm. Cost: a loop over the professions when asked (a click, the firm's page, the
    /// morning's crew); nothing per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private readonly List<int> _firmProfs = new List<int>(8);
        private readonly StringBuilder _firmNames = new StringBuilder(96);

        /// <summary>
        /// The player as a professional at a business of theirs of a type: true with their standing, else false with why (a key)
        /// and, when no profession of theirs fits, the professions the type employs (final words) for its {professions}.
        /// </summary>
        internal bool FirmStanding(string type, out OwnerStanding standing, out string why, out string professions)
        {
            standing = new OwnerStanding { Prof = -1, Rank = -1, Role = -1 };
            professions = string.Empty;
            if (!_workOn || !OwnOn || _study == null || _study.Work == null || Save == null)
            {
                why = "reallife_tech_own_why_off";
                return false;
            }
            int prof = OwnRules.FirmProfession(_study, type, Save.Day, Year, out int rank);
            Profession p = ProCatalogue.Find(prof);
            if (p == null)
            {
                why = "reallife_tech_own_why_none";
                professions = FirmNames(type);
                return false;
            }
            ProRank r = p.Ranks[rank];
            RoleDef role = TalentCatalogue.Role(r.Role);
            standing.Prof = prof;
            standing.Rank = rank;
            standing.Role = r.Role;
            standing.Level = r.Level;
            standing.Main = ProRules.Points(_study, p, rank);
            standing.A = StudyRules.Points(_study, role.SkillA) * 1000;
            standing.B = StudyRules.Points(_study, role.SkillB) * 1000;
            standing.Burnout = _study.Work.Job != null ? _study.Work.Job.Burnout : 0;
            why = null;
            return true;
        }

        // The professions a type employs that the player could work as there, by name; asked only when none of theirs fits.
        private string FirmNames(string type)
        {
            OwnRules.FirmProfessions(type, _firmProfs);
            _firmNames.Length = 0;
            for (int i = 0; i < _firmProfs.Count; i++)
            {
                _firmNames.Append(i > 0 ? ", " : string.Empty).Append(CareersText.ProName(_firmProfs[i]));
            }
            return _firmNames.ToString();
        }

        /// <summary>Minutes worked at a desk of the player's own firm as a profession's rank: what they learnt, a long week's burnout, a restraint clause.</summary>
        internal void FirmWorked(int prof, int rank, int minutes)
        {
            Profession p = ProCatalogue.Find(prof);
            if (!_workOn || _study == null || _study.Work == null || Save == null || p == null || minutes <= 0)
            {
                return;
            }
            int day = Save.Day;
            p.SkillsOf(rank, out int a, out int b);
            int hours = Math.Max(1, minutes / 60);
            LearnAtWork(a, hours, day);
            LearnAtWork(b, hours, day);
            WorkFile w = _study.Work;
            if (w.Job != null && Settings.CareersLife)
            {
                Burn(w.Job, w.Job.WeekMinutes, minutes);   // on top of the job's own week: the firm's hours make it a long one too
            }
            if (OwnOn && w.RestraintUntil >= day && w.RestraintProf == prof)
            {
                TestRestraint(false);
            }
            Changed();
        }
    }
}
