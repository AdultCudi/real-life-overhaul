using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Designs
{
    /// <summary>How a show flat's design is dressed: the game's residential design whole, or cut to the furniture a buyer pictures a home by.</summary>
    public enum ShowFinish
    {
        Staged = 0,   // every piece of the game's design: the art, the plants, the lamps, the rugs
        Plain = 1     // the beds, seats, tables, kitchen and storage only: cheaper, and a lower decor score
    }

    /// <summary>One of the show flats' designs as the game's files give it: where it is, what it keeps and what it costs.</summary>
    public sealed class ShowDesign
    {
        public string Id = string.Empty;       // "B1/Modern/0": the structure's folder, the game's design and the finish (ShowFlatDesigns.Id)
        public string Folder = string.Empty;   // the structure as the game writes it ("B1")
        public string Size = string.Empty;     // the game's size id ("ba:buildingsize_b")
        public int Version;
        public int Sqm;                        // one storey's floor area of the structure, square metres
        public string Design = string.Empty;   // the game's design's file name ("Modern")
        public ShowFinish Finish;
        public int Interior;                   // the game's own interior design score of its walls and floors, 0 to 100
        public long Furniture;                 // the pieces kept at the game's market prices, whole dollars
        public long Fee;                       // the installation firm's fee for the structure's floor area
        public readonly List<string> Names = new List<string>(64);              // each piece kept, by the game's item name
        public readonly HashSet<string> Kept = new HashSet<string>(StringComparer.Ordinal);   // the ids of the pieces kept

        /// <summary>True for a piece of the design's layout the finish keeps, by its id.</summary>
        public bool Keeps(string id) => Kept.Contains(id ?? string.Empty);
    }

    /// <summary>
    /// The show flats' designs, made the Designs way (from the game's own residential designs and furniture, at the game's own
    /// prices): for each apartment structure the game has designs for, each of its designs staged as the game ships it and cut
    /// plain. They are laid on a show flat's floor (the floors core) and never written to the firms' folders: a show flat is no
    /// home the firm installs in. Game-free, checked offline against the installed game's files (EconomyCheck ShowFlatsChecks).
    /// </summary>
    public static class ShowFlatDesigns
    {
        public const string Building = "ba:buildingtype_residential";
        public const string Folder = "Residential";               // under InteriorDesigns: the firms' residential designs
        public const string HouseSize = "ba:buildingsize_t";      // the Hamptons' houses: entered another way, never a floor
        public const string ParkingSize = "ba:buildingsize_parking";
        public const int PlainLeast = 6;                          // pieces a plain cut keeps at least to be offered
        public const int Most = 8;                                // designs a show flat is offered at most

        /// <summary>What a plain cut keeps, by item name without "ba:itemname_": what a home is pictured by, none of its dressing.</summary>
        public static readonly string[] PlainKeeps =
        {
            "bed1", "kingsizebed", "wardrobe", "smallcabinet",
            "cornersofa01", "cornersofa02right", "modularsofa1l", "modularsofa1m", "modularsofa1r", "armchair1", "armchair2",
            "coffeetable1", "coffeetable2", "coffeetable3", "table1", "roundtable", "hightable", "largemeetingtable",
            "regularchair", "barstool", "valenciachair", "valenciaottoman", "multipurposechair", "officechair2", "officedesk1", "officedesk2left",
            "counter1", "counter2", "countercorner", "countercorneroutermodern", "counterleftend", "counterrightend", "moderncounter",
            "standardfridge", "stoveoven", "cheapcoffeemachine",
            "modernshelf", "modernshelfhorizontal",
            "ceilinglamppinecone", "ceilinglampround", "ceilinglampsquare", "ceilinglamptube", "wallradiator"
        };

        private const string ItemPrefix = "ba:itemname_";

        /// <summary>True for a size of the game's a show flat may stand on: an apartment's, never a Hamptons house or the car park.</summary>
        public static bool Flat(string sizeId)
        {
            return !string.IsNullOrEmpty(sizeId) && sizeId != HouseSize && sizeId != ParkingSize;
        }

        /// <summary>True when a finish keeps a piece, by the game's item name.</summary>
        public static bool Keeps(ShowFinish finish, string itemName)
        {
            if (finish == ShowFinish.Staged)
            {
                return !string.IsNullOrEmpty(itemName);
            }
            string bare = itemName != null && itemName.StartsWith(ItemPrefix, StringComparison.Ordinal) ? itemName.Substring(ItemPrefix.Length) : itemName;
            return !string.IsNullOrEmpty(bare) && Array.IndexOf(PlainKeeps, bare) >= 0;
        }

        /// <summary>A design's id: the structure's folder, the game's design and the finish.</summary>
        public static string Id(string folder, string design, ShowFinish finish)
        {
            return (folder ?? string.Empty) + "/" + (design ?? string.Empty) + "/" + ((int)finish).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A design's id read back; false for one that is not a show flat's.</summary>
        public static bool Parse(string id, out string folder, out string design, out ShowFinish finish)
        {
            folder = design = null;
            finish = ShowFinish.Staged;
            string[] parts = string.IsNullOrEmpty(id) ? null : id.Split('/');
            if (parts == null || parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || (parts[2] != "0" && parts[2] != "1"))
            {
                return false;
            }
            folder = parts[0];
            design = parts[1];
            finish = parts[2] == "1" ? ShowFinish.Plain : ShowFinish.Staged;
            return true;
        }

        /// <summary>
        /// The structure a show flat stands on: the one whose storey is nearest the flats' own floor area, the smaller on a tie, so
        /// the show flat is the size of what is sold. -1 for none.
        /// </summary>
        public static int Nearest(IList<int> sqm, int flatArea)
        {
            int best = -1;
            for (int i = 0; sqm != null && i < sqm.Count; i++)
            {
                if (sqm[i] <= 0)
                {
                    continue;
                }
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                int d = Math.Abs(sqm[i] - flatArea), b = Math.Abs(sqm[best] - flatArea);
                if (d < b || (d == b && sqm[i] < sqm[best]))
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>True when a cut is worth offering: a staged design always, a plain one with enough pieces left and fewer than the whole.</summary>
        public static bool Offered(ShowFinish finish, int kept, int whole)
        {
            return finish == ShowFinish.Staged ? kept > 0 : kept >= PlainLeast && kept < whole;
        }
    }
}
