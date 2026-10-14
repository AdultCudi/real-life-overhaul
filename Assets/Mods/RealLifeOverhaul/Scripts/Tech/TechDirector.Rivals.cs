using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The competitors part: who competes with a kind of firm (Tech/Firms/CompetitorRules, the one rule), for the Tech app's
    /// Competitors page of each of the player's firms, with where each is (a company's tower and floor: Tech/Sites; a
    /// business's address and district), and how many a kind of business has in the city, for BizMan's start screen
    /// (Tech/Firms/StartCards). Read when the page is written or the screen is opened, from what the parts and the game hold
    /// then: the city's startups, the businesses the player and the other players run, the schools (Careers). Nothing is
    /// kept between two readings and nothing is saved; in a Going Public session another player's business is only read.
    /// </summary>
    public sealed partial class TechDirector
    {
        private readonly List<FirmSeen> _seen = new List<FirmSeen>(8);
        private readonly List<string> _schools = new List<string>(2);
        private readonly List<Competitor> _competitors = new List<Competitor>(32), _competitorPool = new List<Competitor>(32);

        /// <summary>The competitors of one of the player's firms, each with where it is. The list is the director's own, written again on every call.</summary>
        internal IReadOnlyList<Competitor> RivalsOf(FirmPlace firm)
        {
            for (int i = 0; i < _competitors.Count; i++)
            {
                _competitorPool.Add(_competitors[i]);
            }
            _competitors.Clear();
            if (firm == null || Save == null)
            {
                return _competitors;
            }
            FirmsGame.Seen(_seen, TechBridges.AcademyNow());
            VentureFile v = Ventures;
            CompetitorRules.Gather(firm.File.Kind, v != null ? v.Startups : null, null, _seen, firm.File.Place, _competitors, _competitorPool);
            TechSites sites = TechSites.Instance;
            for (int i = 0; i < _competitors.Count; i++)
            {
                Competitor c = _competitors[i];
                if (c.Kind == (int)RivalKind.Company && sites != null && sites.FloorOf(c.Name, out string tower, out int floor, out string street, out int number))
                {
                    c.Building = tower;
                    c.Floor = floor;
                    c.Street = street;
                    c.Number = number;
                }
                else if (c.Kind == (int)RivalKind.Firm && FirmsGame.Where(c.Place, out string at, out int no, out string district))
                {
                    c.Street = at;
                    c.Number = no;
                    c.District = district;
                }
            }
            return _competitors;
        }

        /// <summary>
        /// How many competitors a kind of business (CompetitorRules: a FirmKind, or the academy) has in the city, by the same
        /// rule as the Tech app's page: the companies, the startups that run, the schools for an academy, every business of a
        /// kind that competes the player or another player runs. With <paramref name="read"/> the businesses and the schools
        /// are read again (once for each opening of BizMan's start screen).
        /// </summary>
        internal int InCity(int kind, bool read)
        {
            if (read)
            {
                FirmsGame.Seen(_seen, TechBridges.AcademyNow());
                TechBridges.SchoolsNow(_schools);
            }
            VentureFile v = Ventures;
            return CompetitorRules.Gather(kind, v != null ? v.Startups : null, _schools, _seen, null, null, null);
        }

        /// <summary>The game's destination at a competitor's tower or business. False where it has none.</summary>
        internal bool RivalDestination(Competitor c) => c != null && !string.IsNullOrEmpty(c.Street) && FirmsGame.Destination(c.Street, c.Number);
    }
}
