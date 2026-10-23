using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The council's planning business at the player's meetings: a builder's zoning application put on the agenda for the
    /// next meeting, and a councillor's or mayor's own motion to change the zoning of a whole class of the new district's
    /// lots. A vote on either is a conflict of interest for a player with a construction company or land in the district,
    /// declared or not, as any other (the register, the misconduct path). Nothing per frame.
    /// </summary>
    public sealed partial class CivicOffice
    {
        /// <summary>The zoning application for a lot and use on the agenda, or null.</summary>
        public OfficeMotion ZoningMotion(string lot, int use)
        {
            for (int i = 0; lot != null && i < State.Agenda.Count; i++)
            {
                OfficeMotion m = State.Agenda[i];
                if (m.Zoning && m.Lot == lot && m.Use == use)
                {
                    return m;
                }
            }
            return null;
        }

        // A builder's application for the next meeting: false when the zoning matters have no room, and the city's council hears it.
        private bool PutZoning(string lot, int lotClass, int use, int suit, int reputation)
        {
            if (!Sits() || string.IsNullOrEmpty(lot) || State.ZoningCount() >= CouncilZoning.AgendaMost)
            {
                return false;
            }
            if (ZoningMotion(lot, use) == null)
            {
                State.Agenda.Add(new OfficeMotion
                {
                    Kind = OfficeMotion.KindZoning, By = OfficeMotion.ByApplicant, Day = TimeHelper.CurrentDay, Lot = lot, Class = lotClass, Use = use, Add = true, Suit = suit,
                    Reputation = reputation
                });
                Logger("a zoning application for " + use + " on lot " + lot + " is on the agenda of the meeting on day " + State.NextMeeting + ".");
            }
            return true;
        }

        // The application is decided elsewhere now (a test, or the council's own sitting): off the agenda.
        private void DecideZoningNow(string lot, int use, int day)
        {
            OfficeMotion m = ZoningMotion(lot, use);
            if (m != null)
            {
                State.Agenda.Remove(m);
            }
        }

        /// <summary>True when the player has an interest in a zoning matter: a construction company, or land in the new district.</summary>
        public bool Conflicted(OfficeMotion m)
        {
            return m != null && (m.Zoning ? ZoningBridges.HasInterest() : m.Kind == OfficeMotion.KindPolicy && Conflicted(m.Policy));
        }

        // The vote on a zoning matter: the city's members by the council's planning odds, the player as they voted.
        private MotionVote DecideZoning(OfficeMotion m, int vote, bool declaredAside, int day, int speak)
        {
            bool councillor = State.Holds == OfficeRace.Council;
            float tabled = m.By == OfficeMotion.ByPlayer ? Influence / 400f : 0f;
            MotionVote result = _gov.ZoningVote(m.Lot, m.Class, m.Use, m.Suit, m.Reputation, m.Add, day, councillor ? State.Seat : -1, tabled, State.Holds == OfficeRace.Mayor ? speak : 0);
            bool voted = councillor && (vote == 0 || vote == 1);
            if (voted)
            {
                if (vote == 1)
                {
                    result.Ayes++;
                }
                else
                {
                    result.Noes++;
                }
            }
            result.Passed = OfficeRules.Passes(result.Ayes, result.Noes);
            if (voted)
            {
                result.Decisive = OfficeRules.Passes(result.Ayes - (vote == 1 ? 1 : 0), result.Noes - (vote == 0 ? 1 : 0)) != result.Passed;
            }
            ApplyZoning(m, result, voted, declaredAside, day);
            return result;
        }

        private void ApplyZoning(OfficeMotion m, MotionVote result, bool voted, bool declaredAside, int day)
        {
            // An undeclared interest in it is kept as the building code's own policy (the permits lever the way the vote went).
            int policy = CivicCatalogue.IndexOf(CivicCatalogue.Find(Lever.Permits, m.Add ? 1 : -1));
            bool interest = ZoningBridges.HasInterest();
            if (voted && interest && !declaredAside && State.Secrets.Count < OfficeState.SecretsMost)
            {
                State.Secrets.Add(new OfficeSecret { Day = day, Policy = policy, Decisive = result.Decisive });
            }
            if (declaredAside)
            {
                CivicBridges.Moved(1, 0);
            }
            if (m.Undeclared && result.Passed && State.Secrets.Count < OfficeState.SecretsMost)
            {
                State.Secrets.Add(new OfficeSecret { Day = day, Policy = policy, Decisive = true });
            }
            if (m.Lot != null)
            {
                ZoningBridges.TellDecided(m.Lot, m.Use, result.Passed, result.Ayes, result.Noes);
                Logger("the meeting decided the zoning application for " + m.Use + " on lot " + m.Lot + ": " + result.Ayes + " to " + result.Noes + ".");
                return;
            }
            if (result.Passed)
            {
                ZoningBridges.TellRezoned(m.Class, m.Use, m.Add);
                State.Story(day + 1, m.Add ? "reallife_civic_news_office_rezoned" : "reallife_civic_news_office_unzoned", CivicText.PlayerName(), MotionWords(m));
            }
            Logger("the meeting " + (result.Passed ? "carried" : "lost") + " the motion on the zoning of class " + m.Class + " lots (" + (m.Add ? "allow " : "stop ") + m.Use + "): "
                   + result.Ayes + " to " + result.Noes + ".");
        }

        /// <summary>A zoning matter in words: the lot's application, or the change for a class of lots.</summary>
        public static string MotionWords(OfficeMotion m)
        {
            if (m == null)
            {
                return string.Empty;
            }
            if (m.Lot != null)
            {
                return CivicText.T("reallife_civic_zoning_application", "use", ZoningBridges.Use(m.Use), "lot", ZoningBridges.Lot(m.Lot));
            }
            return CivicText.T(m.Add ? "reallife_civic_zoning_allow" : "reallife_civic_zoning_stop", "use", ZoningBridges.Use(m.Use), "lots", ZoningBridges.Class(m.Class));
        }

        /// <summary>True when a class of lots could be rezoned so: the use not allowed there yet (to allow), or allowed by the council before (to stop).</summary>
        public bool MayRezone(int lotClass, int use, bool add, int baseZones)
        {
            int zones = ZoningBridges.Zones(lotClass), bit = 1 << use;
            bool allowed = (zones & bit) != 0;
            return add ? !allowed : allowed && (baseZones & bit) == 0;
        }

        /// <summary>
        /// The player puts a change of a class's zoning before the next meeting, as a policy motion is tabled. A mayor's
        /// undeclared interest becomes a secret if it passes. Returns the locale key of the answer.
        /// </summary>
        public string TableZoning(int lotClass, int use, bool add, bool declared, int day)
        {
            string why = MayTable(out _);
            if (why != null)
            {
                return why;
            }
            if (!ZoningBridges.DistrictLives())
            {
                return "reallife_civic_zoning_no_district";
            }
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                OfficeMotion o = State.Agenda[i];
                if (o.Zoning && o.Lot == null && o.Class == lotClass && o.Use == use)
                {
                    return "reallife_civic_office_table_there";
                }
            }
            if (State.ZoningCount() >= CouncilZoning.AgendaMost)
            {
                return "reallife_civic_office_table_full";
            }
            bool interest = ZoningBridges.HasInterest();
            State.Agenda.Add(new OfficeMotion
            {
                Kind = OfficeMotion.KindZoning, By = OfficeMotion.ByPlayer, Day = day, Class = Mathf.Clamp(lotClass, 0, 3), Use = Mathf.Clamp(use, 0, 3), Add = add,
                Suit = ZoningBridges.SuitOf(lotClass, use), Reputation = 50, Undeclared = State.Holds == OfficeRace.Mayor && !declared && interest
            });
            State.Tabled = day;
            if (declared && interest)
            {
                CivicBridges.Moved(1, 0);
            }
            Logger("the player tabled a change of the zoning of class " + lotClass + " lots: " + (add ? "allow " : "stop ") + use + ".");
            return "reallife_civic_office_tabled";
        }

        /// <summary>
        /// Test: a meeting held now, the player voting aye on everything without declaring an interest; the agenda cleared and
        /// nothing posted of it (the test puts the office back after). Returns the motions carried.
        /// </summary>
        public int TestMeeting(int day, out int decided)
        {
            Prepare(day);
            decided = State.Agenda.Count;
            int passed = 0;
            for (int i = 0; i < State.Agenda.Count; i++)
            {
                passed += Decide(State.Agenda[i], 1, false, day).Passed ? 1 : 0;
            }
            State.Agenda.Clear();
            State.NextMeeting = day + OfficeRules.MeetingEvery;
            return passed;
        }
    }
}
