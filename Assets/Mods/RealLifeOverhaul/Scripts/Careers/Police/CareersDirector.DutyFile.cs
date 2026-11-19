using System.Collections.Generic;

namespace RealLifeOverhaul.Careers
{
    // The officer's file and what a review and a recorded shift read of it: the game-free part of the police shift (Careers/Police), so that EconomyCheck runs it as
    // the director's own code beside CareersDirector.People.cs. The shift itself, the uniform and the list are in CareersDirector.Duty.cs.
    public sealed partial class CareersDirector
    {
        private DutyFile _duty;
        private bool _dutyRead;

        private static bool IsPolice(Job j) => j != null && j.Def != null && j.Def.Id == ProCatalogue.Police;

        private void DutyLoad()
        {
            if (_dutyRead || Save == null)
            {
                return;
            }
            _dutyRead = true;
            _duty = DutyStore.Load(Save.modData, out int _) ?? new DutyFile();
        }

        /// <summary>The officer's file: arrests, tickets, commendations and the rest; empty for someone who never served.</summary>
        public DutyFile Duty
        {
            get
            {
                DutyLoad();
                return _duty;
            }
        }

        private void DutyWrite(Dictionary<string, string> data)
        {
            if (_dutyRead && _duty != null)
            {
                data[DutyStore.Key] = DutyStore.Write(_duty);
            }
        }

        private void DutyForget()
        {
            _duty = null;
            _dutyRead = false;
        }

        /// <summary>What the last shift ratings add to a review's score, within the rules' bounds; 0 for anyone but an officer.</summary>
        private int DutyReviewBonus(Job j)
        {
            if (!IsPolice(j))
            {
                return 0;
            }
            DutyFile f = Duty;
            return f != null ? PoliceRules.ReviewBonus(f.Ring, f.Count) : 0;
        }

        /// <summary>The rating a shift of a day is recorded with: today's while the file is for the day, else the quiet shift's.</summary>
        private int DutyRatingOn(int day)
        {
            DutyFile f = Duty;
            return f != null && f.Day == day ? f.Rating : PoliceRules.RatingStart;
        }
    }
}
