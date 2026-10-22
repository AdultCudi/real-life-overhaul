using System;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>The rules of interiors and decor: every effect with its cap, the customers' split, and one business worked out.</summary>
    public static partial class InteriorRules
    {
        /// <summary>The standard's part of footfall, per cent: short of it 1% for each 4 points (rounded down, 6 at most), over it 1% for each 8 (3 at most).</summary>
        public static int StandardFootfall(int overall, int standard)
        {
            return StandardFootfall(overall, standard, 1000);
        }

        /// <summary>The same at the standard's strength, thousandths (both ways: 12% at most short of it at double).</summary>
        public static int StandardFootfall(int overall, int standard, int scale)
        {
            int gap = overall - standard;
            if (gap < 0)
            {
                return -Scale(Math.Min(ShortFootfallMost, -gap / ShortPointsPerPercent), scale);
            }
            return Scale(Math.Min(OverFootfallMost, gap / OverPointsPerPercent), scale);
        }

        /// <summary>The most falling short of the standard may cost at a strength, per cent: 6 at the default, 12 at double, nothing off.</summary>
        public static int Bound(int scale)
        {
            return Scale(ShortFootfallMost, scale);
        }

        /// <summary>The decor's part of footfall, per cent: so many per mille at a decor score of 100 (100, 10%, by default; 200 at double), rounded down.</summary>
        public static int DecorFootfall(int decor, int mille)
        {
            return Clamp(decor, 0, 100) * Clamp(mille, 0, Strength(StrengthMost) * DecorMille / 1000) / 1000;
        }

        /// <summary>The interior's footfall, per cent: the parts that are on, together, inside -12 to +24.</summary>
        public static int Footfall(int standardPart, int decorPart)
        {
            return Clamp(standardPart + decorPart, FootfallLeast, FootfallMost);
        }

        /// <summary>The shortfall's part beside business pressure's dip, per cent: the two together never take more than the bound.</summary>
        public static int GiveWay(int standardPart, int issuePercent, int bound)
        {
            if (standardPart >= 0)
            {
                return standardPart;
            }
            int room = Math.Min(0, -Math.Max(0, bound) - Math.Min(0, issuePercent));
            return Math.Max(standardPart, room);
        }

        /// <summary>Thousandths of the effects that apply on a day, a seventh more each day from <paramref name="from"/>; in full with none.</summary>
        public static int Ramp(int from, int day)
        {
            if (from == InteriorFile.Unset || from == InteriorFile.LongAgo)
            {
                return 1000;
            }
            return Clamp((int)(((long)day - from + 1) * 1000 / RampDays), 0, 1000);
        }

        /// <summary>The first day an effect applies in full, for a business it began to come in for on <paramref name="from"/>.</summary>
        public static int FullFrom(int from)
        {
            return from + RampDays - 1;
        }

        /// <summary>Thousandths more of the price the district bears at this business: 1 a point over the standard, 30 at most; under it nothing less.</summary>
        public static int Bears(int overall, int standard)
        {
            return Bears(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the prices option, thousandths.</summary>
        public static int Bears(int overall, int standard, int scale)
        {
            int gap = overall - standard;
            return gap > 0 ? Scale(Math.Min(BearsOverMost, gap), scale) : 0;
        }

        /// <summary>The chance a complaint about the place starts today, per mille: nothing until 10 short of the standard, then 4 a point, 120 at most.</summary>
        public static int ComplaintChance(int overall, int standard)
        {
            return ComplaintChance(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the standard, thousandths; none at all under the default strength, whose bound is smaller than a complaint.</summary>
        public static int ComplaintChance(int overall, int standard, int scale)
        {
            int under = standard - overall;
            return under < ComplaintFrom || scale < ComplaintFromStrength ? 0 : Scale(Math.Min(ComplaintMost, under * ComplaintPerPoint), scale);
        }

        /// <summary>The most the basket adds in a district, per mille of an order's products: 100 where the standard is lowest (15), 60 where it is highest (70).</summary>
        public static int BasketMost(int standard)
        {
            int s = Clamp(standard, StandardLeast, StandardMost);
            return BasketMostPoor - (s - StandardLeast) * (BasketMostPoor - BasketMostRich) / (StandardMost - StandardLeast);
        }

        /// <summary>Per mille more products a customer takes: nothing at the standard, rising to the district's most at an overall 85.</summary>
        public static int BasketShare(int overall, int standard)
        {
            return BasketShare(overall, standard, 1000);
        }

        /// <summary>The same at the strength of the prices option, thousandths.</summary>
        public static int BasketShare(int overall, int standard, int scale)
        {
            int gap = Clamp(overall, 0, 100) - standard;
            if (gap <= 0)
            {
                return 0;
            }
            int most = BasketMost(standard), span = BasketFullAt - standard;
            return Scale(span <= 0 ? most : most * Math.Min(gap, span) / span, scale);
        }

        /// <summary>Products one customer takes more: their order's times the share, the rest by chance (draw 0 to 999); an office's one.</summary>
        public static int BasketExtra(int products, int share, int draw, bool office)
        {
            long total = (long)Math.Max(0, products) * Clamp(share, 0, 1000);
            int n = (int)(total / 1000);
            if (draw < total % 1000)
            {
                n++;
            }
            return office ? Math.Min(n, BasketOfficeMost) : n;
        }

        /// <summary>Points a shop of a brand counts more or less in its brand's standards: one for each 15 over or under 50, three at most.</summary>
        public static int BrandPoints(int overall)
        {
            return Clamp((overall - BrandMiddle) / BrandPointsPer, -BrandMost, BrandMost);
        }

        /// <summary>Points a shop's figure in the customers' reputation moves: one for each 20 over or under 50, two at most.</summary>
        public static int StandingPoints(int overall)
        {
            return Clamp((overall - StandingMiddle) / StandingPointsPer, -StandingMost, StandingMost);
        }

        /// <summary>Staff comfort, 0 to 5: plants (one, two with three or more), good light (the lighting at half its share or more), a rest corner (two).</summary>
        public static int Comfort(int plants, int lightingCapped, int target, bool restCorner)
        {
            int comfort = plants >= PlantsForTwo ? 2 : plants > 0 ? 1 : 0;
            if (target > 0 && lightingCapped * 2 >= (int)((long)target * CapShare[(int)DecorCategory.Lighting] / 1000))
            {
                comfort++;
            }
            if (restCorner)
            {
                comfort += 2;
            }
            return Clamp(comfort, 0, ComfortMost);
        }

        /// <summary>Thousandths a professional's morale heads higher for the comfort of their workplace.</summary>
        public static int Morale(int comfort)
        {
            return Clamp(comfort, 0, ComfortMost) * ComfortMoralePerPoint;
        }

        /// <summary>What the best office adds to the esteem candidates feel: 1 at 60, 2 at 80.</summary>
        public static int OfficeEsteem(int bestOffice)
        {
            return bestOffice >= OfficeTwo ? 2 : bestOffice >= OfficeOne ? 1 : 0;
        }

        /// <summary>Thousandths a firm's bids weigh better for its office: nothing to 50, then up to 20 at 100.</summary>
        public static int OfficeBid(int overall)
        {
            return Clamp((overall - BidFrom) * BidMost / (100 - BidFrom), 0, BidMost);
        }

        /// <summary>1 for a striking interior, -1 for a shabby one, 0 for neither.</summary>
        public static int Newsworthy(int overall, int standard, int wear, bool standardOn)
        {
            if (overall >= StrikingFrom && wear < WornFrom)
            {
                return 1;
            }
            if (wear >= ShabbyWear || (standardOn && standard - overall >= ShabbyShort))
            {
                return -1;
            }
            return 0;
        }

        /// <summary>True if a newsworthy business may be in the news today: rested since the last time, and the day's draw under the chance.</summary>
        public static bool Story(int lastStory, int day, int draw)
        {
            return day - lastStory >= StoryRestDays && draw < StoryChance;
        }

        /// <summary>True while a business first read on <paramref name="firstDay"/> is still fitting out: the standard's shortfall does not count yet.</summary>
        public static bool Fitting(int firstDay, int day)
        {
            return firstDay != InteriorFile.Unset && day - firstDay < GraceDays;
        }

        /// <summary>Why a business sells nothing now, the first that holds; None when it can sell.</summary>
        public static SaleStop Stop(bool closed, bool stocked, bool staffed, int recent, int refused)
        {
            if (closed)
            {
                return SaleStop.Closed;
            }
            if (!stocked)
            {
                return SaleStop.Empty;
            }
            if (!staffed)
            {
                return SaleStop.NoStaff;
            }
            return recent >= RecentOrders && refused >= recent ? SaleStop.Prices : SaleStop.None;
        }

        /// <summary>
        /// Today's customers by where they came from. The game draws them by its multiplier, a base plus three quarters of the
        /// promotion figure in hundredths; the figure is the street's traffic and marketing (<paramref name="own"/>), written
        /// again with this mod's parts on top (<paramref name="written"/>). The game's <paramref name="drawn"/> are shared by
        /// those parts (a part may be negative), what the top of 100 cut shared by size; <paramref name="booked"/> and
        /// <paramref name="otherMod"/> are added as they are. The shares add up to the total exactly.
        /// </summary>
        public static CustomerSplit Split(int drawn, int booked, int otherMod, int baseMille, int traffic, int own, int written, int interiorPercent, int otherPercent)
        {
            var split = new CustomerSplit { Total = Math.Max(0, drawn) + Math.Max(0, booked) + Math.Max(0, otherMod), OtherMod = Math.Max(0, otherMod) };
            drawn = Math.Max(0, drawn);
            own = Clamp(own, 0, 100);
            written = Clamp(written, 0, 100);
            long baseTen = 10L * Math.Max(0, baseMille);
            long game = baseTen + 75L * Clamp(traffic, 0, own);
            long ownM = baseTen + 75L * own;
            long all = baseTen + 75L * written;
            long change = all - ownM;
            long interiorRaw = ownM * interiorPercent / 100, otherRaw = ownM * otherPercent / 100;
            long interior = interiorRaw + otherRaw != 0 && Math.Sign(interiorRaw + otherRaw) == Math.Sign(change)
                ? change * interiorRaw / (interiorRaw + otherRaw) : interiorRaw;
            long others = change - interior;
            long marketing = ownM - game;
            if (all <= 0 || drawn == 0)
            {
                split.Game = drawn;
            }
            else
            {
                split.Marketing = (int)Rounded(drawn * marketing, all);
                split.Interiors = (int)Rounded(drawn * interior, all);
                split.Others = (int)Rounded(drawn * others, all);
                split.Game = drawn - split.Marketing - split.Interiors - split.Others;
            }
            split.Interiors += Math.Max(0, booked);
            return split;
        }

        private static long Rounded(long value, long by)
        {
            return value >= 0 ? (value + by / 2) / by : -((-value + by / 2) / by);
        }

        /// <summary>Thousandths more clients an office gets: the decor's per mille (100, 10%, by default; 200 at double) at its overall score.</summary>
        public static int OfficeBoost(int overall, int decorMille)
        {
            return Clamp(Clamp(overall, 0, 100) * Clamp(decorMille, 0, OfficeBoostMost) / 100, 0, OfficeBoostMost);
        }

        /// <summary>Clients more for the coming hour, the fraction carried on, never more than the free staffed stations.</summary>
        public static int OfficeClients(int gameClients, int free, int boostMille, ref int carryMille)
        {
            long total = (long)Math.Max(0, gameClients) * Clamp(boostMille, 0, OfficeBoostMost) + Clamp(carryMille, 0, 999);
            int n = (int)(total / 1000);
            carryMille = (int)(total - (long)n * 1000);
            if (n > free)
            {
                n = Math.Max(0, free);
                carryMille = 0;
            }
            return n;
        }

        /// <summary>Every figure of a business from what was read, its wear and the parts on; an office gets no customers' parts.</summary>
        public static void Work(InteriorShop s, int wear, InteriorParts p)
        {
            s.Wear = Clamp(wear, 0, WearMost);
            s.Target = Target(s.Sqm);
            for (int i = 0; i < Categories; i++)
            {
                s.Capped[i] = Capped((DecorCategory)i, s.Points[i], s.Target);
            }
            s.DecorRaw = DecorScore(s.Capped, s.Target, s.Kinds);
            s.Decor = Effective(s.DecorRaw, s.Wear, s.Clean);
            s.InteriorNow = Effective(s.Interior, s.Wear, s.Clean);
            s.Overall = Overall(s.InteriorNow, s.Decor);
            s.Standard = Standard(s.Income, s.GameMinimum);
            bool shop = s.Kind == InteriorKind.Shop;
            s.Ramp = Clamp(s.Ramp, 0, 1000);
            s.Bound = shop ? Bound(p.StandardScale) : 0;
            s.StandardFull = shop ? StandardFootfall(s.Overall, s.Standard, p.StandardScale) : 0;
            if (s.Fitting)
            {
                s.StandardFull = Math.Max(0, s.StandardFull);   // fitting out: over the standard counts, short of it not yet
            }
            s.DecorFull = shop ? DecorFootfall(s.Decor, p.DecorMille) : 0;
            if (s.Stop != SaleStop.None)
            {
                s.StandardFull = Math.Min(0, s.StandardFull);   // selling nothing: nobody extra, the shortfall's cost stays
                s.DecorFull = 0;
            }
            Combine(s);
            s.Bears = shop ? Scale(Bears(s.Overall, s.Standard, p.PriceScale), s.Ramp) : 0;
            s.ComplaintChance = shop && !s.Fitting && s.Ramp >= 1000 ? ComplaintChance(s.Overall, s.Standard, p.StandardScale) : 0;
            s.Basket = shop && p.Basket && s.Stop == SaleStop.None ? Scale(BasketShare(s.Overall, s.Standard, p.PriceScale), s.Ramp) : 0;
            s.BrandPoints = shop && p.Standing ? BrandPoints(s.Overall) : 0;
            s.StandingPoints = shop && p.Standing ? StandingPoints(s.Overall) : 0;
            s.Comfort = s.Kind != InteriorKind.None && p.Staff
                ? Comfort(s.Items[(int)DecorCategory.Greenery], s.Capped[(int)DecorCategory.Lighting], s.Target, s.RestCorner) : 0;
            s.OfficeEsteem = s.Kind == InteriorKind.Office && p.Staff ? OfficeEsteem(s.Overall) : 0;
            s.OfficeBoost = s.Kind == InteriorKind.Office && s.Stop == SaleStop.None ? Scale(OfficeBoost(s.Overall, p.DecorMille), s.Ramp) : 0;
            s.OfficeBid = s.Kind != InteriorKind.None && p.Staff ? OfficeBid(s.Overall) : 0;
        }

        /// <summary>A worked business's footfall again from its parts, for when business pressure's dip changes, without reading it again.</summary>
        public static void Combine(InteriorShop s)
        {
            s.StandardFootfall = GiveWay(Scale(s.StandardFull, s.Ramp), s.IssueDip, s.Bound);
            s.DecorFootfall = Scale(s.DecorFull, s.Ramp);
            s.Footfall = Footfall(s.StandardFootfall, s.DecorFootfall);
        }

        /// <summary>What the interior does to a shop's takings today, thousandths either way, from its customers and their baskets.</summary>
        public static int Takings(int footfall, int basket)
        {
            long t = (100L + Clamp(footfall, FootfallLeast, FootfallMost)) * (1000L + Clamp(basket, 0, 1000)) / 100 - 1000;
            return (int)t;
        }

        /// <summary>The category to add to next: the one furthest under its cap, decor before furniture and equipment; -1 when every one is full.</summary>
        public static int Next(InteriorShop s)
        {
            int best = -1, room = 0;
            for (int i = 0; i < (int)DecorCategory.Furniture; i++)
            {
                int cap = (int)((long)s.Target * CapShare[i] / 1000);
                int left = cap - s.Capped[i];
                if (left > room)
                {
                    room = left;
                    best = i;
                }
            }
            return best;
        }
    }
}
