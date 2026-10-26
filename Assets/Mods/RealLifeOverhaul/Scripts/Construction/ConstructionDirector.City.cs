using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The rules of the city around the company: the building code the elected government sets with its permits policy
    /// (the height limits, the parking asked, the planning fee, the surcharge on land kept empty: CivicCatalogue's figures,
    /// read through the Permits bridge), and zoning heard by the city's one council (Civic's, through the Council bridge):
    /// an application for a lot at its next sitting or at the player's own meeting, and a class of lots rezoned by a motion
    /// the council carried. On the day's pass and a press; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Where the building code stands: the permits policy in force, -1 (strict), 0 (as the city came) or 1 (fast-track).</summary>
        public static int Code => Permits();

        /// <summary>The storeys a project's district allows under the code in force.</summary>
        public static int HeightLimitOf(ProjectFile p)
        {
            return PlansRules.HeightLimit(p.District, !string.IsNullOrEmpty(p.Lot), Permits());
        }

        /// <summary>True while the player has an interest in the building code: a construction company, or land in the new district.</summary>
        public bool Interest => _state != null && (_state.Firm.Founded > 0 || _state.Lots.Count > 0);

        /// <summary>The uses a lot of the player's may be built for: its own zoning, changes granted for it, and its class's as the council has it.</summary>
        public int ZonesNow(LotFile l)
        {
            if (l == null)
            {
                return 0;
            }
            return _state.ZonesOf(l, Lot(l.Id, out LotView lot) ? lot.Class : 0);
        }

        /// <summary>The uses a class of the district's lots is zoned for now: as the district came and as the council changed it.</summary>
        public int ClassZones(int lotClass)
        {
            int c = BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1);
            return LandRules.ZonesWith(LandRules.ZonesOf(c), _state != null ? _state.ClassAdded[c] : 0);
        }

        // The application heard on its day by the city's council: decided now, or waiting for the player's meeting.
        private void HearZoning(LotFile l, bool now)
        {
            BuildUse use = AskedUse(l.ZoneAsk);
            Lot(l.Id, out LotView lot);
            int suit = BuildCatalogue.Clamp(BuildCatalogue.ZoneOdds[(int)use][BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1)] + DemandLift(use), 0, 1000);   // the council weighs the district's demand for the use
            int vote = CouncilVote(l.Id, lot.Class, use, suit, _state.Firm.Reputation, now, out int ayes, out int noes);
            if (vote == -1)
            {
                if (l.ZoneDay <= ConstructionGame.Day)
                {
                    l.ZoneDay = CouncilDay(ConstructionGame.Day);   // before the player's meeting: heard there
                    Logger?.Info("Construction: lot " + l.Id + "'s application for " + use + " is before the council's meeting of day " + l.ZoneDay + ".");
                }
                return;
            }
            if (vote == -2)
            {
                // No council of the city's answers (its elections switched off): the planners decide as the city came.
                int odds = BuildCatalogue.Clamp(LandRules.ZoneOdds(use, lot.Class, Permits(), _state.Firm.Reputation) + DemandLift(use), 0, 1000);
                ZoneDecided(l.Id, (int)use, PlanningRules.ZoneVote(_state.Seed, l.Id, use, odds), -1, -1);
                return;
            }
            ZoneDecided(l.Id, (int)use, vote == 1, ayes, noes);
        }

        private static BuildUse AskedUse(int ask)
        {
            BuildUse use = BuildUse.Homes;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if ((ask & BuildCatalogue.Zone((BuildUse)u)) != 0)
                {
                    use = (BuildUse)u;
                }
            }
            return use;
        }

        /// <summary>The council decided a lot's application (at its sitting, or a meeting of the player's): the zoning changed or not, and the player told.</summary>
        public void ZoneDecided(string lotId, int use, bool carried, int ayes, int noes)
        {
            if (!Ready())
            {
                return;
            }
            LotFile l = _state.Lot(lotId);
            int bit = BuildCatalogue.Zone((BuildUse)BuildCatalogue.Clamp(use, 0, (int)BuildUse.Count - 1));
            if (l == null || (l.ZoneAsk & bit) == 0)
            {
                return;   // asked of a lot no longer the player's, or decided already
            }
            if (carried)
            {
                l.Zones |= l.ZoneAsk;
            }
            else if (l.Paint == use + 1)
            {
                l.Paint = 0;   // build mode's paint of the use refused falls back to none
            }
            l.ZoneAsk = 0;
            NoteVote(ayes, noes, carried);
            Logger?.Info("Construction: the council voted on " + (BuildUse)use + " for lot " + l.Id + (ayes >= 0 ? " (" + ayes + " to " + noes + ")" : " (the planners' odds)") + ": "
                         + (carried ? "agreed" : "refused") + ".");
            if (ayes >= 0)
            {
                var data = new Dictionary<string, string>(4)
                {
                    { "name", LotWords(l.Id) }, { "what", ConstructionText.T(BuildCatalogue.UseKeys[use]) }, { "ayes", ConstructionText.N(ayes) }, { "noes", ConstructionText.N(noes) }
                };
                ConstructionGame.Tell(carried ? NotificationType.Success : NotificationType.Warning, carried ? "reallife_bld_notice_council_yes" : "reallife_bld_notice_council_no", data,
                    () => Open(TabLand, 0));
            }
            else
            {
                Tell(carried ? NotificationType.Success : NotificationType.Warning, carried ? "reallife_bld_notice_zone_yes" : "reallife_bld_notice_zone_no", LotWords(l.Id),
                    BuildCatalogue.UseKeys[use], TabLand, 0);
            }
            Changed();
        }

        /// <summary>The council carried a motion on a class of the district's lots: a use allowed on every one of them, or allowed no longer.</summary>
        public void ClassRezoned(int lotClass, int use, bool add)
        {
            if (!Ready())
            {
                return;
            }
            int c = BuildCatalogue.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1);
            int bit = BuildCatalogue.Zone((BuildUse)BuildCatalogue.Clamp(use, 0, (int)BuildUse.Count - 1));
            if (add)
            {
                _state.ClassAdded[c] |= bit;
            }
            else
            {
                _state.ClassAdded[c] &= ~bit;
            }
            Logger?.Info("Construction: the council " + (add ? "allowed " : "stopped allowing ") + (BuildUse)use + " on every class " + c + " lot of the district.");
            Changed();
        }

        // The day's pass for the city around the company: rings found out, bills paid or taken to court, the building of the year.
        private void CityDay(int day)
        {
            RingsDay(day);
            BillsDay(day);
            if (Settings.ConstructionCity)
            {
                AwardDay(day);
            }
            _state.Tidy();
        }

        /// <summary>A change of the code on its way (the permits policy the winner or the council brought in), and the next election; false for none.</summary>
        internal static bool CodeAhead(out int position, out int day, out int election)
        {
            position = day = election = 0;
            try
            {
                return !Down("CodeAhead") && ConstructionBridges.CodeAhead != null && ConstructionBridges.CodeAhead(out position, out day, out election);
            }
            catch (Exception e)
            {
                Faulty("CodeAhead", e);
                return false;
            }
        }

        /// <summary>The code under a position of the permits policy, in words: the new district's storeys, the parking, the fee and the surcharge.</summary>
        public static string CodeWords(int permits)
        {
            return ConstructionText.Fill(permits > 0 ? "reallife_bld_code_up" : permits < 0 ? "reallife_bld_code_down" : "reallife_bld_code_usual", "uptown",
                ConstructionText.N(PlansRules.HeightLimit(null, true, permits)), "parking", ConstructionText.Share(CivicCatalogue.ParkingPermille(permits)),
                "fee", ConstructionText.Share(CivicCatalogue.PlanningFeePermille(permits)), "vacant", ConstructionText.Share(LandRules.VacantTax(permits)));
        }
    }
}
