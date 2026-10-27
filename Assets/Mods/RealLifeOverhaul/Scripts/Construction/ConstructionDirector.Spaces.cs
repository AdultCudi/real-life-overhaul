using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A run of free spaces side by side along one side of a block (the player's "lot"), as the Land tab sells them.</summary>
    public struct LandRun
    {
        public LotView First;   // its first free space
        public int Spaces;      // free spaces in a row from it
        public int Width;       // m: their frontage together
    }

    /// <summary>
    /// The construction game's land on Uptown's spaces (the owner, 25 Oct 2026: "Multiple building in a lot. By lot i mean
    /// the big rectangle that has buildings in it"): a block's free spaces are sold in runs side by side, a run bought is
    /// one site, and its building is one that fills it. A site is kept as its first space's land (LotFile.Spaces counts
    /// the rest along its strip), so a save's land, projects and tenders keep naming one lot each.
    /// COST: nothing per frame; a pass over the district's lots when the Land tab is drawn or land is bought.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<LotView> _strip = new List<LotView>(16);
        private readonly List<LotView> _joining = new List<LotView>(16);
        private readonly List<string> _over = new List<string>(8);

        /// <summary>True for a lot of the district's grid of spaces (a test's lot or a landmark's site has none).</summary>
        public static bool OnSpaces(LotView lot) => lot.Strip > 0 && lot.Spaces > 0;

        /// <summary>The land that holds a space: the site it is the first of, or one bought from a space before it along its strip; null for none.</summary>
        public LotFile Holding(string spaceId)
        {
            LotFile own = _state.Lot(spaceId);
            if (own != null || !Space(spaceId, out LotView space) || !OnSpaces(space))
            {
                return own;
            }
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                LotFile l = _state.Lots[i];
                if (l.Spaces > 1 && Space(l.Id, out LotView first) && first.Strip == space.Strip && space.Index > first.Index && space.Index < first.Index + l.Spaces)
                {
                    return l;
                }
            }
            return null;
        }

        /// <summary>
        /// A site's spaces in order along its strip, into <paramref name="into"/> (cleared): its first and the spaces after
        /// it it takes. False when one of them is not in the district at this load.
        /// </summary>
        public bool SiteSpaces(string firstId, int spaces, List<LotView> into)
        {
            into.Clear();
            if (!Space(firstId, out LotView at))
            {
                return false;
            }
            into.Add(at);
            while (into.Count < spaces)
            {
                if (!After(at, out at))
                {
                    return false;
                }
                into.Add(at);
            }
            return true;
        }

        /// <summary>
        /// One site's ground as one lot: the rectangle round its spaces, their frontage together, its first space's address
        /// and street; its class by its spaces (one a small building's, two an office's, three or more a warehouse's).
        /// </summary>
        public static LotView Joined(IList<LotView> spaces)
        {
            LotView first = spaces[0];
            if (spaces.Count < 2)
            {
                return first;
            }
            bool alongX = first.Yaw % 180 == 0;   // the grid lies along the world's axes: an avenue's lots (yaw 0 or 180) run along x
            float lo = float.MaxValue, hi = float.MinValue;
            int width = 0, count = 0;
            for (int i = 0; i < spaces.Count; i++)
            {
                float mid = alongX ? spaces[i].X : spaces[i].Z;
                lo = Math.Min(lo, mid - spaces[i].Width * 0.5f);
                hi = Math.Max(hi, mid + spaces[i].Width * 0.5f);
                width += spaces[i].Width;
                count += Math.Max(1, spaces[i].Spaces);
            }
            LotView site = first;
            site.Width = width;
            site.Spaces = count;
            site.Class = count >= 3 ? 3 : count == 2 ? 1 : 0;
            if (alongX)
            {
                site.X = (lo + hi) * 0.5f;
            }
            else
            {
                site.Z = (lo + hi) * 0.5f;
            }
            return site;
        }

        // A site the player holds, as one lot: its spaces joined; false when it is a single lot or one of them is gone.
        private bool JoinedSite(string firstId, out LotView site)
        {
            site = default(LotView);
            LotFile own = _state != null ? _state.Lot(firstId) : null;
            if (own == null || own.Spaces <= 1 || !SiteSpaces(firstId, own.Spaces, _joining))
            {
                return false;
            }
            site = Joined(_joining);
            return true;
        }

        // A lot of the district by id as the plan has it, never joined.
        private bool Space(string id, out LotView lot)
        {
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; id != null && i < lots.Count; i++)
            {
                if (lots[i].Id == id)
                {
                    lot = lots[i];
                    return true;
                }
            }
            lot = default(LotView);
            return false;
        }

        /// <summary>True when a space is for sale: free ground of the grid, nobody's land, no building, no developers.</summary>
        public bool FreeSpace(LotView lot)
        {
            return OnSpaces(lot) && lot.Size == null && lot.Open && !LotTaken(lot.Id) && Holding(lot.Id) == null && !DeveloperOn(lot.Id);
        }

        /// <summary>
        /// Every run of free spaces side by side along a block's side, in the district's order, into <paramref name="into"/>
        /// (cleared): where a block has free spaces and how many in a row, so a building of that many can be bought for.
        /// </summary>
        public void Runs(List<LandRun> into)
        {
            into.Clear();
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                LotView l = lots[i];
                if (!FreeSpace(l) || (Before(l, out LotView before) && FreeSpace(before)))
                {
                    continue;   // not free, or not a run's first
                }
                var run = new LandRun { First = l };
                LotView at = l;
                while (true)
                {
                    run.Spaces++;
                    run.Width += at.Width;
                    if (run.Spaces >= BuildCatalogue.SiteSpacesMost || !After(at, out LotView next) || !FreeSpace(next))
                    {
                        break;
                    }
                    at = next;
                }
                into.Add(run);
            }
        }

        private bool Before(LotView l, out LotView before)
        {
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i].Strip == l.Strip && lots[i].Index + Math.Max(1, lots[i].Spaces) == l.Index)
                {
                    before = lots[i];
                    return true;
                }
            }
            before = default(LotView);
            return false;
        }

        private bool After(LotView l, out LotView after)
        {
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; OnSpaces(l) && i < lots.Count; i++)
            {
                if (lots[i].Strip == l.Strip && lots[i].Index == l.Index + Math.Max(1, l.Spaces))
                {
                    after = lots[i];
                    return true;
                }
            }
            after = default(LotView);
            return false;
        }

        /// <summary>Why <paramref name="spaces"/> free spaces in a row from a first cannot be bought now (a text key), or null.</summary>
        public string BuySiteBlock(string firstId, int spaces)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (spaces < 1 || spaces > BuildCatalogue.SiteSpacesMost || !SiteSpaces(firstId, spaces, _strip))
            {
                return "reallife_bld_answer_no_lot";
            }
            for (int i = 0; i < _strip.Count; i++)
            {
                if (!FreeSpace(_strip[i]))
                {
                    return "reallife_bld_answer_lot_taken";
                }
            }
            if (_state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            return SitePrice(firstId, spaces) <= 0 ? "reallife_bld_answer_no_price" : null;
        }

        /// <summary>A run's price: each of its spaces' land at its own traffic; 0 when one is not known.</summary>
        public long SitePrice(string firstId, int spaces)
        {
            if (!SiteSpaces(firstId, spaces, _strip))
            {
                return 0L;
            }
            long price = 0L;
            for (int i = 0; i < _strip.Count; i++)
            {
                long each = LotPrice(_strip[i]);
                if (each <= 0L)
                {
                    return 0L;
                }
                price += each;
            }
            return price;
        }

        /// <summary>Free spaces in a row from a first bought outright as one site, at their prices. Returns a text key.</summary>
        public string BuySite(string firstId, int spaces)
        {
            string block = BuySiteBlock(firstId, spaces);
            if (block != null)
            {
                return block;
            }
            long price = SitePrice(firstId, spaces);
            if (!ConstructionGame.Take(price, "reallife_transaction_bld_land", LotWords(firstId), false))
            {
                return "reallife_bld_answer_no_money";
            }
            SiteSpaces(firstId, spaces, _strip);
            LotView site = Joined(_strip);
            int day = ConstructionGame.Day;
            _state.Lots.Add(new LotFile { Id = firstId, Bought = day, Paid = price, Zones = LandRules.ZonesOf(site.Class), TaxedTo = day, EmptySince = day, Spaces = spaces });
            Logger?.Info("Construction: " + spaces + " space(s) from " + firstId + " bought as one site (" + site.Width + " m) for " + price + ".");
            Changed();
            return "reallife_bld_answer_lot_bought";
        }

        /// <summary>
        /// The designs a lot may wear, kept from <paramref name="into"/>: on the grid of spaces, of a size taking exactly the
        /// site's spaces, or fewer when its frontage alone fills the site (BuildCatalogue.Offered: a building is never left
        /// with ground beside it inside its site); a lot off the grid keeps the sizes its class takes. Land that no plot's site can be
        /// set out on (LandRules.SiteFit) keeps none, for the player's choice and every pick of the city's own alike: nobody plans on it.
        /// </summary>
        private static void KeepFitting(LotView site, List<DesignChoice> into)
        {
            if (LandRules.SiteFit(site.Width, site.Depth, out _) != PlotFit.Fits)
            {
                into.Clear();
                return;
            }
            if (!OnSpaces(site))
            {
                KeepSizes(site.Id, into);
                return;
            }
            for (int i = into.Count - 1; i >= 0; i--)
            {
                bool fills = into[i].Width <= site.Width + 0.01f && into[i].Width >= site.Width - BuildCatalogue.SiteSliver;
                if (!BuildCatalogue.Offered(site.Spaces, SpacesOf(into[i].Size, into[i].Version), fills))
                {
                    into.RemoveAt(i);
                }
            }
            if (into.Count > DesignsShown)
            {
                into.RemoveRange(DesignsShown, into.Count - DesignsShown);
            }
        }

        // The ground a building on a lot must fit: a site of spaces its whole ground (its building fills it), a lot off the
        // grid its plot's frame.
        private static void SiteGround(LotView lot, out float width, out float depth)
        {
            if (OnSpaces(lot))
            {
                width = lot.Width + 0.01f;
                depth = lot.Depth;
                return;
            }
            PlotFrame(lot.Width, lot.Depth, out width, out depth);
        }

        /// <summary>The other spaces a site's building stands over beside its first, into <paramref name="into"/> (cleared).</summary>
        public void OverOf(string firstId, List<string> into)
        {
            into.Clear();
            LotFile own = _state.Lot(firstId);
            if (own == null || own.Spaces <= 1 || !SiteSpaces(firstId, own.Spaces, _strip))
            {
                return;
            }
            for (int i = 1; i < _strip.Count; i++)
            {
                into.Add(_strip[i].Id);
            }
        }
    }
}
