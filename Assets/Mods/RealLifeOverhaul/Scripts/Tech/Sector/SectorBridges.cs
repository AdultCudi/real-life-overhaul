using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the other tech firms say on Flare's bridge: the kinds of news a firm's residents can talk about.</summary>
    public enum SectorNews
    {
        Launch = 0, Outage = 1, Breach = 2, Benchmark = 3, RivalShipped = 4, RivalCut = 5, TenderWon = 6, Award = 7, Incident = 8, Count = 9
    }

    /// <summary>
    /// What the other tech firms ask of the living economy's other workstreams, filled in by Wiring/Wiring.SectorLinks. Each is
    /// optional with a default, and a fault in the other feature reads as the default. Game-free: EconomyCheck compiles it.
    ///
    /// The population (<see cref="City"/>): the city's people, its firms by size (from the population's employers: small
    /// under 50 people, mid-sized to 999, large from 1,000; SectorRules.FirmsOf counts them) and its households' digital
    /// spending today, cents. Until it is wired, or while the population is not running, the documented city
    /// (SectorCatalogue.Reference*) stands in, so the markets keep the scale of the money bands.
    ///
    /// Flare (<see cref="Talk"/>): news a firm's residents talk about: the kind (SectorNews), the firm's place, a name (the
    /// product, the rival, the client) and a figure (customers, a score, a share). With none wired, nothing is posted.
    /// </summary>
    public static class SectorBridges
    {
        /// <summary>The city now: its people, firms by size and the day's digital spending (cents); false while the population has no figures.</summary>
        public delegate bool CityAsk(out int people, out int small, out int mid, out int large, out long digitalCents);

        /// <summary>The population's city. Default: none, so the documented city.</summary>
        public static CityAsk City;

        /// <summary>A firm's news for Flare: the kind, the firm's place, a name and a figure. Default: nothing is posted.</summary>
        public static Action<int, string, string, long> Talk;

        /// <summary>
        /// The city read into the sector's figures: the population's when it has them (the first day's digital spending a
        /// person kept as the anchor its mood is measured against), the documented city otherwise or if it faults. True when
        /// the population's.
        /// </summary>
        public static bool Read(SectorFile f)
        {
            try
            {
                if (City != null && City(out int people, out int small, out int mid, out int large, out long digital) && people > 0)
                {
                    if (f.DigitalAnchor <= 0 && digital > 0)
                    {
                        f.DigitalAnchor = SectorRules.DigitalPerPerson(digital, people);
                    }
                    f.City.Set(people, small, mid, large, SectorRules.Digital(digital, people, f.DigitalAnchor), true);
                    return true;
                }
            }
            catch (Exception)
            {
                // the population's fault: the documented city stands in, as with none
            }
            f.City.Set(SectorCatalogue.ReferencePeople, SectorCatalogue.ReferenceSmall, SectorCatalogue.ReferenceMid, SectorCatalogue.ReferenceLarge, 1000, false);
            return false;
        }

        /// <summary><see cref="Talk"/> told, a fault in Flare ignored.</summary>
        public static void Say(SectorNews news, string place, string name, long figure)
        {
            try
            {
                Talk?.Invoke((int)news, place, name, figure);
            }
            catch (Exception)
            {
                // Flare's fault: the firm goes on unheard
            }
        }

        public static void Reset()
        {
            City = null;
            Talk = null;
        }
    }
}
