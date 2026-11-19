using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Changing who an AI model is sold to (SegmentRules): what the page says before the player asks, and the re-launch itself. Both interfaces read the same lines and press the same
    /// method. COST: a few sums when a page is written or a button pressed, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private static readonly CultureInfo SegmentInv = CultureInfo.InvariantCulture;

        /// <summary>True if the product's segment can be changed now (an AI model on sale, not already moving).</summary>
        internal bool CanMove(Product p) => _firmsOn && Save != null && SegmentRules.Can(p, Save.Day);

        /// <summary>
        /// True if the firm the product is made at was founded in a niche that does not buy in a segment, so a model made for it never sells into the niche (the same
        /// rule as <see cref="NicheSegment"/> for a new product and the page's warning for a product on sale).
        /// </summary>
        internal bool NicheOff(Product p, int to, out int niche)
        {
            niche = -1;
            FoundedFirm f = _founding != null && p != null ? _founding.Of(p.Place) : null;
            if (f == null || !f.Counts || f.Niche < 0)
            {
                return false;
            }
            niche = f.Niche;
            return !NicheServes(f, SectorRules.MarketOf(p.K, to));
        }

        /// <summary>The segments a model can be moved to, its own left out, in the order the pages list them.</summary>
        internal static int[] MoveTargets(Product p)
        {
            var list = new List<int>(2);
            for (int s = 0; s < (int)Segment.Count; s++)
            {
                if (SegmentRules.Differs(p, s))
                {
                    list.Add(s);
                }
            }
            return list.ToArray();
        }

        /// <summary>The line a re-launch under way gives a model: when it is back on sale and the work still to pay. Null when none is under way.</summary>
        internal ViewLine MoveUnderWay(Product p)
        {
            if (p == null || Save == null || (p.Moving <= Save.Day && p.Owe <= 0))
            {
                return null;
            }
            return new ViewLine
            {
                Key = "reallife_tech_move_under_way", Tone = ViewTone.Warn,
                Pairs = new[] { "to", TechText.SegmentName(p.S), "day", p.Moving.ToString(SegmentInv), "left", TechText.Whole(p.Owe) }
            };
        }

        /// <summary>
        /// What a move to a segment costs and brings, line by line, for the page and before its question box: the work, the days off sale, the customers who leave and what is
        /// kept, the awareness it starts from, and the niche's warning when the firm's niche does not buy in the new segment.
        /// </summary>
        internal List<ViewLine> MoveLines(Product p, int to)
        {
            var lines = new List<ViewLine>(6);
            if (p == null || Save == null)
            {
                return lines;
            }
            SegmentMove m = SegmentRules.Preview(p, to, Save.Day);
            string from = TechText.SegmentName((Segment)m.From), into = TechText.SegmentName((Segment)m.To);
            long price = FirmsRules.MonthPrice(p) * FirmsCatalogue.Price[m.To] / FirmsCatalogue.Price[m.From];   // the new segment's list price at the tier the model is sold at
            lines.Add(new ViewLine
            {
                Key = "reallife_tech_move_work", Tone = ViewTone.Plain,
                Pairs = new[] { "points", TechText.Whole(m.Work), "share", TechText.Mille(SegmentRules.Work), "cost", TechText.Whole(m.GenerationCost) }
            });
            lines.Add(new ViewLine
            {
                Key = "reallife_tech_move_days", Tone = ViewTone.Plain, Pairs = new[] { "days", m.Days.ToString(SegmentInv), "day", m.ResumeDay.ToString(SegmentInv) }
            });
            lines.Add(new ViewLine
            {
                Key = "reallife_tech_move_customers", Tone = m.Lost > 0 ? ViewTone.Warn : ViewTone.Plain,
                Pairs = new[]
                {
                    "lost", TechText.Whole(m.Lost), "all", TechText.Whole(m.Customers), "share", TechText.Mille(SegmentRules.Lost), "from", from, "to", into,
                    "kept", TechText.Whole(m.Kept), "price", TechText.Cents(Level(price)), "after", TechText.Cents(Level(m.MonthAfter)), "now", TechText.Cents(Level(m.MonthNow))
                }
            });
            if (_sectorOn)
            {
                lines.Add(new ViewLine
                {
                    Key = "reallife_tech_move_aware", Tone = ViewTone.Plain,
                    Pairs = new[] { "to", into, "aware", TechText.Mille(m.Aware), "days", m.AwareDays.ToString(SegmentInv) }
                });
            }
            if (NicheOff(p, to, out int niche))
            {
                lines.Add(new ViewLine
                {
                    Key = "reallife_tech_mkt_product_niche_off", Tone = ViewTone.Warn,
                    Pairs = new[] { "niche", TechText.T(NicheCatalogue.NameKey(niche)), "who", into }
                });
            }
            return lines;
        }

        /// <summary>The question box's title and body for a move, with its figures: the same ones the page showed.</summary>
        internal void MoveQuestion(Product p, int to, out string title, out string body)
        {
            SegmentMove m = SegmentRules.Preview(p, to, Save != null ? Save.Day : 0);
            title = TechText.Fill("reallife_tech_confirm_move_title", "product", p.Name);
            body = TechText.Fill("reallife_tech_confirm_move_body", "product", p.Name, "from", TechText.SegmentName((Segment)m.From), "to", TechText.SegmentName((Segment)m.To),
                "points", TechText.Whole(m.Work), "days", m.Days.ToString(SegmentInv), "lost", TechText.Whole(m.Lost), "all", TechText.Whole(m.Customers));
        }

        /// <summary>
        /// An AI model re-launched for another segment: its customers cut, its work owed, off sale for the days, its awareness in the new segment started. False if it cannot
        /// be done now (not a model on sale, already moving, the same segment). Nothing is paid in money: the price is work, days and customers.
        /// </summary>
        internal bool ChangeSegment(int id, int to)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            if (p == null || Save == null || !SegmentRules.Can(p, Save.Day) || !SegmentRules.Differs(p, to) || FirmOf(p.Place) == null)
            {
                return false;
            }
            SegmentMove m = SegmentRules.Apply(p, _sectorOn ? _sector.ProductFor(p.Id) : null, to, Save.Day);
            TellTech(NotificationType.Info, "reallife_tech_note_moved", Data("product", p.Name, "to", TechText.SegmentName((Segment)m.To), "day", m.ResumeDay.ToString(SegmentInv)),
                TabProducts, TechApp.ProductRow + p.Id);
            if (Verbose)
            {
                Logger?.Info("Tech check (segment): " + p.Name + " re-launched for segment " + m.To + ": " + m.Lost + " of " + m.Customers + " customers left, " + m.Kept + " kept, "
                             + m.Work + " work points owed, off sale until day " + m.ResumeDay + ": PASS");
            }
            ChangedTech();
            return true;
        }
    }
}
