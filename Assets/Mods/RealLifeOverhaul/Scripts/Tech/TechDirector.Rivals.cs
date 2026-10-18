using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The competitors part, for the Tech app's Competitors page and BizMan's start screen. Read when asked; nothing kept.</summary>
    public sealed partial class TechDirector
    {
        private readonly List<FirmSeen> _seen = new List<FirmSeen>(8);
        private readonly List<string> _schools = new List<string>(2);
        private readonly List<Competitor> _competitors = new List<Competitor>(32), _competitorPool = new List<Competitor>(32);

        /// <summary>The competitors of one of the player's firms, with where each is: the director's own list, written again on every call.</summary>
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

        /// <summary>A kind's competitors in the city by the Tech app's rule; <paramref name="read"/> reads the businesses and schools again.</summary>
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
