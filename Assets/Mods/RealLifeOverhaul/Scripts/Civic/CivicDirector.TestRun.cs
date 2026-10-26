using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the city expansion's test run (Scripts/TestRun) asks of the construction tests besides pressing them: the id
    /// the programme gives its next site, kept before the tests, and the sites they set out since then taken out of the
    /// programme again at the run's end, as a builder's site given up is (CivicWorks.Drop: the street lets go of it when
    /// nobody is looking). The city's own sites and the builders' are left be. COST: on the run's call only.
    /// </summary>
    public sealed partial class CivicDirector
    {
        private static readonly List<SiteState> Leaving = new List<SiteState>(4);

        /// <summary>The id of the next site the programme sets out; 0 before the save's programme is read.</summary>
        public int TestNextSite => _loaded && Works != null && Works.State != null ? Works.State.NextId : 0;

        /// <summary>The rebuilds and plots set out with ids from <paramref name="from"/> up to <paramref name="to"/> taken out of the programme; how many.</summary>
        public int TestTakeAway(int from, int to)
        {
            if (!_loaded || Works == null || Works.State == null || from <= 0 || to <= from || HostOwns("Civic test run", 'W'))
            {
                return 0;
            }
            Leaving.Clear();
            for (int i = 0; i < Works.State.Sites.Count; i++)
            {
                SiteState site = Works.State.Sites[i];
                if (site.Id >= from && site.Id < to && (site.Kind == SiteKind.Rebuild || site.Kind == SiteKind.Plot) && !SiteBuilders.IsOwned(site))
                {
                    Leaving.Add(site);
                }
            }
            int taken = 0;
            for (int i = 0; i < Leaving.Count; i++)
            {
                taken += Works.Drop(Leaving[i]) ? 1 : 0;
            }
            Leaving.Clear();
            return taken;
        }
    }
}
