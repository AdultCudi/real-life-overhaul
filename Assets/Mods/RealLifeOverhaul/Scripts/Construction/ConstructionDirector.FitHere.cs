using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>One plot or open lot of "You could build here": where it is, what fits it, and where the camera goes to it.</summary>
    public struct FitSite
    {
        public string Name;     // its street and number, as the map names it
        public string Fits;     // "Homes 5, Shops 7": the designs that fit, by use
        public int Designs;     // all of them
        public float X, Z;      // m: where the camera goes
        public bool Mine;       // the player's own plot (else land for sale)
    }

    /// <summary>
    /// "You could build here" in Build mode: the player's plots with nothing on them and the lots for sale, each with the designs that
    /// fit it by use, from the same footprint rule the Place tool's window uses (FootprintDesigns), never a figure of its own. Read when
    /// Build mode asks, on a pick or after an action, never a frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int FitExamined = 24;   // lots for sale looked at at most: each is a read of the designs

        /// <summary>The player's empty plots first, then lots for sale, into <paramref name="into"/>, at most <paramref name="most"/>; only those with a design that fits.</summary>
        public void FitHere(List<FitSite> into, int most)
        {
            into.Clear();
            CellGrid grid = Cells;
            for (int i = 0; i < _state.Lots.Count && into.Count < most; i++)
            {
                LotFile own = _state.Lots[i];
                if (_state.OnLot(own.Id) != null || ParkOf(own.Id) != null || !Space(own.Id, out LotView first))
                {
                    continue;
                }
                int column = grid.ColumnOf(first.Strip, first.Index);
                if (column < 0)
                {
                    continue;
                }
                int rows = CellRules.MinRows(own.Rows, own.Spaces, CellRules.RowsFor(first.Depth));
                string fits = FitsOf(column, own.Spaces, rows, ZonesNow(own), -1, out int total);
                if (total > 0)
                {
                    AddFit(into, grid, column, LotWords(own.Id), fits, total, true);
                }
            }
            var seen = new HashSet<string>();
            int looked = 0;
            for (int c = 0; c < grid.Columns.Count && into.Count < most && looked < FitExamined; c++)
            {
                if (!IsFreeColumn(c) || !LotOfColumn(c, out LotView lot) || !seen.Add(lot.Id))
                {
                    continue;
                }
                looked++;
                int use = CouncilColumnUse(c);
                if (use < 0)
                {
                    continue;
                }
                string fits = FitsOf(c, 1, CellRules.RowsFor(lot.Depth), 1 << use, use, out int total);
                if (total > 0)
                {
                    AddFit(into, grid, c, LotWords(lot.Id), fits, total, false);
                }
            }
        }

        private static void AddFit(List<FitSite> into, CellGrid grid, int column, string name, string fits, int total, bool mine)
        {
            grid.Rect(column, 0, 0f, out float x1, out float z1, out float x2, out float z2);
            into.Add(new FitSite { Name = name, Fits = fits, Designs = total, X = (x1 + x2) * 0.5f, Z = (z1 + z2) * 0.5f, Mine = mine });
        }

        // The designs a footprint has for each use its zoning allows (or the one asked for), "Homes 5, Shops 7".
        private string FitsOf(int column, int columns, int rows, int zones, int only, out int total)
        {
            total = 0;
            var words = new List<string>(4);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if ((only >= 0 && u != only) || !LandRules.Zoned(zones, (BuildUse)u))
                {
                    continue;
                }
                int n = CountDesigns(column, columns, rows, (BuildUse)u);
                if (n > 0)
                {
                    total += n;
                    words.Add(ConstructionText.T(BuildCatalogue.UseKeys[u]) + " " + ConstructionText.N(n));
                }
            }
            return string.Join(", ", words);
        }
    }
}
