using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's streets built up as Manhattan's busiest (the owner, 25 Oct 2026: "Please fill the lots. The uptown looks very
    /// empty"): once the city's designs are measured and before any building is made (MadeBuildings.Arranging), every run of
    /// the district's declared buildings along a street is laid out by FrontageRules.Plan, each declaration given its design
    /// and its place side by side with its neighbours, and the fillers listed; they stand once the district's buildings do.
    /// COST: nothing per frame; one pass over the plan and the catalogue at a load, behind the loading screen.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<FrontFill> Fills = new List<FrontFill>(512);
        private static readonly Dictionary<string, MadeSpec> SpecById = new Dictionary<string, MadeSpec>(512, System.StringComparer.Ordinal);

        // MadeBuildings.Arranging: this load's declarations placed along the district's streets.
        private static void Arrange(IList<DesignEntry> designs)
        {
            Fills.Clear();
            if (!_known || Declared.Count == 0)
            {
                return;
            }
            long began = Stopwatch.GetTimestamp();
            SpecById.Clear();
            for (int i = 0; i < Declared.Count; i++)
            {
                SpecById[Declared[i].Id] = Declared[i];
            }
            int placed = FrontageRules.Plan(Plan, id => SpecById.TryGetValue(id, out MadeSpec spec) ? spec : null, designs, Fills);
            int back = 0;
            for (int i = 0; i < Fills.Count; i++)
            {
                back += Fills[i].Back ? 1 : 0;
            }
            SpecById.Clear();
            Logger?.Info("Uptown: " + placed + " of its " + Declared.Count + " building(s) stand side by side along its streets, with " + (Fills.Count - back) + " filler(s) between them and "
                         + back + " behind them, laid out in " + ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency).ToString("F1", CultureInfo.InvariantCulture) + " ms.");
        }
    }
}
