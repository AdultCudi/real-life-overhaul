using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The council's planning business: the one council of the city hears the builders' zoning applications and the
    /// district's rezoning, on a Thursday's sitting while the player is not on it and at the player's meetings while they
    /// are. Asked by the construction game through the wiring when an application is made and on the day it is heard;
    /// arithmetic on <see cref="GovState"/> and nothing per frame.
    /// </summary>
    public sealed partial class CivicGovernment
    {
        // The player's office, set by CivicOffice: their next meeting, and a zoning matter put to it (false with no room).
        public Func<int> OfficeMeeting;
        public Func<string, int, int, int, int, bool> ToPlanning;
        public Func<string, int, bool> OnPlanning;
        public Action<string, int, int> DecideNow;

        /// <summary>The day the council hears planning business tabled on a day: the player's next meeting while they sit, else a Thursday's sitting.</summary>
        public int PlanningDay(int day)
        {
            if (Sits())
            {
                int meeting = Meeting();
                if (meeting > day)
                {
                    return meeting;
                }
                if (meeting > 0)
                {
                    return meeting + OfficeRules.MeetingEvery;
                }
            }
            return CouncilZoning.SittingDay(day);
        }

        private int Meeting()
        {
            try
            {
                return OfficeMeeting != null ? OfficeMeeting() : 0;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>
        /// A builder's application to add a use to a lot heard on its day: 1 carried, 0 refused, -1 before the council still
        /// (on the agenda of the player's next meeting, where they have a vote). <paramref name="now"/> decides it at once,
        /// without the player's vote, wherever it stands (a test, or a day long gone by).
        /// </summary>
        public int Hear(string lot, int lotClass, int use, int suit, int reputation, bool now, int day, out int ayes, out int noes)
        {
            ayes = noes = 0;
            if (Sits() && !now)
            {
                bool there = false, put = false;
                try
                {
                    there = OnPlanning != null && OnPlanning(lot, use);
                    put = there || (ToPlanning != null && ToPlanning(lot, lotClass, use, suit, reputation));
                }
                catch (Exception e)
                {
                    CivicDirector.Logger?.Error(e);
                }
                if (put)
                {
                    return -1;   // the player's meeting decides it, and tells the builders then
                }
            }
            else if (now)
            {
                try
                {
                    DecideNow?.Invoke(lot, use, day);   // off the player's agenda if it was there: decided here instead
                }
                catch (Exception e)
                {
                    CivicDirector.Logger?.Error(e);
                }
            }
            MotionVote vote = ZoningVote(lot, lotClass, use, suit, reputation, true, day, -1, 0f, 0);
            ayes = vote.Ayes;
            noes = vote.Noes;
            CivicDirector.Logger?.Info("Civic: the council heard the application for " + use + " on lot " + lot + ": " + ayes + " to " + noes + ", " + (vote.Passed ? "carried" : "refused") + ".");
            return vote.Passed ? 1 : 0;
        }

        /// <summary>
        /// The five vote on a zoning matter: each member by <see cref="CouncilZoning.MemberOdds"/>, a seat left out (the player's
        /// own, who votes themselves) and a little more for every member when the player tabled it with influence; a mayor's
        /// word moves their own party's members.
        /// </summary>
        public MotionVote ZoningVote(string lot, int lotClass, int use, int suit, int reputation, bool add, int day, int skipSeat, float tabled, int mayorWord)
        {
            Salience();
            float top = MaxSalience();
            float growth = top > 0f ? _salience[(int)Issue.Growth] / top : 0f, living = top > 0f ? _salience[(int)Issue.Living] / top : 0f;
            int line = CouncilZoning.Line(State.Mayor, State.Levers[(int)Lever.Permits]);
            int matter = CouncilZoning.Matter(lot, lotClass, use, add);
            var result = new MotionVote();
            for (int seat = 0; seat < State.Council.Length; seat++)
            {
                if (seat == skipSeat)
                {
                    continue;
                }
                bool mayorsParty = State.Council[seat] == State.Mayor.Party;
                float odds = CouncilZoning.MemberOdds(suit, add, State.Levers[(int)Lever.Permits], growth, living, line, mayorsParty, reputation) + tabled;
                if (mayorsParty)
                {
                    odds += mayorWord * OfficeRules.MayorWord;
                }
                odds = Mathf.Clamp(odds, CouncilZoning.LeastOdds, CouncilZoning.MostOdds);
                if (CouncilZoning.Roll(State.Seed, day, seat, matter) < odds)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            result.Passed = OfficeRules.Passes(result.Ayes, result.Noes);
            return result;
        }

        /// <summary>The district's lean on an issue, the new district's people leaning to growth (<see cref="CouncilZoning.BuildingLean"/>).</summary>
        private float BuildingLean(int district, int issue, float lean)
        {
            bool mine = district >= 0 && district < _city.Districts.Count && CityDistricts.Mine(_city.Districts[district].Id);
            return CouncilZoning.BuildingLean(mine, issue, lean);
        }
    }
}
