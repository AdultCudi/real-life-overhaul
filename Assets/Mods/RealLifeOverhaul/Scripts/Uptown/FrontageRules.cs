using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>A filler to stand: a design of the catalogue for its look alone, on a piece of ground as a lot's building stands (its front on the ground's front line).</summary>
    public struct FrontFill
    {
        public int Design;
        public MadeLot Lot;
        public bool Back;     // a second row behind a lot's building, not a gap along the street
    }

    /// <summary>
    /// How Uptown's buildings stand on their spaces (the owner, 25 Oct 2026: "There is so much free space between buildings
    /// in uptown ... Please fill the lots", and "I can see empty spaces in lots that have buildings"), game-free
    /// (EconomyCheck's UptownFrontChecks). Every building of the plan stands on the whole spaces its size takes
    /// (SpacesRules), so the buildings of a street stand side by side; its look is a design that fills them
    /// (<see cref="Look"/>); where none does, its size's that fits best (<see cref="Choose"/>) with a filler, the game's own
    /// buildings copied for their looks alone, in what it leaves. Since 27 Oct 2026 the pick also looks at what a lot shows
    /// (a player walking the district: "buildings do not face the road properly", "gaps between buildings"): a lot with an
    /// end open to a street or to land takes a design whose side there can be finished (LookRules), and a design that leaves
    /// no yard behind it before one that does. Every answer comes from the catalogue (the same on every machine) and the
    /// arguments alone.
    /// COST: nothing per frame; a few passes over the catalogue for each lot once a load, behind the loading screen.
    /// </summary>
    public static class FrontageRules
    {
        public const float FrontShare = 0.9f;    // of the widest fitting design's front: a lot's building is among those this wide
        public const float DepthShare = 0.75f;   // of the deepest of those: and among those this deep
        public const float HeightShare = 0.6f;   // of the tallest of those: and among those this tall
        public const float FillerSpare = 0.4f;   // m a filler's slot is wider than its front, so it fits as a lot's design does
        public const float BackLeast = 10f;      // m of a lot left behind its building that takes a second row: less is a yard

        /// <summary>What a lot asks of the design it wears besides its size and its frontage: the ends of it that are open, its facing, and whether the pick looks at them.</summary>
        public struct Want
        {
            public LotEnds Ends;    // the ends of the lot that nothing stands against
            public float Facing;    // the way its street lies from it (MadeLot's)
            public bool Sides;      // the pick looks at its sides and its depth; off, the pick is the one made before 27 Oct 2026
            public float Depth;     // m: the lot's depth, a design leaves a yard when it is this much shallower than BackLeast

            /// <summary>0 to 3: two for a design with every open end finished, one for a design that leaves no yard of a second row's size; the best is picked first.</summary>
            public int Score(DesignEntry d)
            {
                if (!Sides)
                {
                    return 3;
                }
                int score = !Ends.Any || LookRules.Finished(d, Ends, Facing) ? 2 : 0;
                return score + (Deep(d) >= Depth - BackLeast + 0.01f ? 1 : 0);
            }

            /// <summary>The best score a design can have.</summary>
            public int Best => 3;
        }

        /// <summary>
        /// The design a lot's building wears: of its size's designs that can be copied and fit (those of its type if any),
        /// the widest within <see cref="FrontShare"/>, of those the deepest within <see cref="DepthShare"/>, of those the
        /// tallest within <see cref="HeightShare"/>, one picked by the lot's id. -1 when none fits.
        /// </summary>
        public static int Choose(IList<DesignEntry> designs, string size, string type, float width, float depth, string id)
        {
            return Choose(designs, size, type, width, depth, id, default);
        }

        /// <summary>As above, a design with a finished side where the lot's ends are open picked first when <paramref name="want"/> asks for it.</summary>
        public static int Choose(IList<DesignEntry> designs, string size, string type, float width, float depth, string id, Want want)
        {
            int pick = ChooseOf(designs, size, type, width, depth, id, want, want.Sides && want.Ends.Any);
            return pick >= 0 || !want.Sides || !want.Ends.Any ? pick : ChooseOf(designs, size, type, width, depth, id, want, false);
        }

        // One pass of Choose: with <finished> only the designs whose open ends are finished are in it.
        private static int ChooseOf(IList<DesignEntry> designs, string size, string type, float width, float depth, string id, Want want, bool finished)
        {
            if (designs == null)
            {
                return -1;
            }
            int typed = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                typed += Fits(designs[i], size, width, depth) && designs[i].Type == type && Allowed(designs[i], want, finished) ? 1 : 0;
            }
            bool byType = typed > 0;
            float wide = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                wide = In(designs[i], size, type, byType, width, depth) && Allowed(designs[i], want, finished) ? Math.Max(wide, Across(designs[i])) : wide;
            }
            if (wide <= 0f)
            {
                return -1;
            }
            float deep = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                deep = In(designs[i], size, type, byType, width, depth) && Allowed(designs[i], want, finished) && Across(designs[i]) >= wide * FrontShare ? Math.Max(deep, Deep(designs[i])) : deep;
            }
            float tall = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                tall = Pooled(designs[i], size, type, byType, width, depth, wide, deep, 0f) && Allowed(designs[i], want, finished) ? Math.Max(tall, designs[i].Height) : tall;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += Pooled(designs[i], size, type, byType, width, depth, wide, deep, tall) && Allowed(designs[i], want, finished) ? 1 : 0;
            }
            if (pool == 0)
            {
                return -1;
            }
            int wanted = (int)(NewBuildingsRules.Hash(id + "|front") % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (Pooled(designs[i], size, type, byType, width, depth, wide, deep, tall) && Allowed(designs[i], want, finished) && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Allowed(DesignEntry d, Want want, bool finished)
        {
            return !finished || LookRules.Finished(d, want.Ends, want.Facing);
        }

        private static bool Fits(DesignEntry d, string size, float width, float depth)
        {
            if (d == null || d.Size != size)
            {
                return false;
            }
            Measure m = Of(d);
            return m.Free && m.Across <= width && m.Deep <= depth;
        }

        private static bool In(DesignEntry d, string size, string type, bool byType, float width, float depth)
        {
            return Fits(d, size, width, depth) && (!byType || d.Type == type);
        }

        private static bool Pooled(DesignEntry d, string size, string type, bool byType, float width, float depth, float wide, float deep, float tall)
        {
            return In(d, size, type, byType, width, depth) && Across(d) >= wide * FrontShare && Deep(d) >= deep * DepthShare && d.Height >= tall * HeightShare;
        }

        /// <summary>
        /// A filler for a gap of a run: of every design that can be copied (any size, never a whole special one) no wider than
        /// <paramref name="most"/> less <see cref="FillerSpare"/> and no deeper than <paramref name="depth"/>, the widest
        /// within <see cref="FrontShare"/>, one picked by <paramref name="key"/>. -1 when none is that narrow.
        /// </summary>
        public static int Filler(IList<DesignEntry> designs, float most, float depth, string key)
        {
            return Filler(designs, most, depth, key, default);
        }

        /// <summary>As above, a design with a finished side where the filler's ends are open picked first when <paramref name="want"/> asks for it.</summary>
        public static int Filler(IList<DesignEntry> designs, float most, float depth, string key, Want want)
        {
            int pick = FillerOf(designs, most, depth, key, want, want.Sides && want.Ends.Any);
            return pick >= 0 || !want.Sides || !want.Ends.Any ? pick : FillerOf(designs, most, depth, key, want, false);
        }

        private static int FillerOf(IList<DesignEntry> designs, float most, float depth, string key, Want want, bool finished)
        {
            float wide = 0f;
            for (int i = 0; designs != null && i < designs.Count; i++)
            {
                wide = FillerFits(designs[i], most, depth) && Allowed(designs[i], want, finished) ? Math.Max(wide, Across(designs[i])) : wide;
            }
            if (wide <= 0f)
            {
                return -1;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += FillerFits(designs[i], most, depth) && Allowed(designs[i], want, finished) && Across(designs[i]) >= wide * FrontShare ? 1 : 0;
            }
            int wanted = (int)(NewBuildingsRules.Hash(key + "|filler") % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (FillerFits(designs[i], most, depth) && Allowed(designs[i], want, finished) && Across(designs[i]) >= wide * FrontShare && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool FillerFits(DesignEntry d, float most, float depth)
        {
            if (d == null)
            {
                return false;
            }
            Measure m = Of(d);
            return m.Free && m.Across + FillerSpare <= most && m.Deep <= depth;
        }

        /// <summary>
        /// The look a building on its spaces wears, filling them (SpacesRules.Fills: its frontage within
        /// <see cref="SpacesRules.FillSliver"/> of the spaces' width, never wider, its depth within the lot's): of the designs
        /// that can be copied, those of its own size and version, else of its own size, else of its type and as many ways in
        /// (the game's sizes M, R and S have two), else of as many ways in; one picked by the lot's id among the first of
        /// those that has any, and only one the made buildings' engine takes by its name for this size. -1 when none fills.
        /// </summary>
        public static int Look(IList<DesignEntry> designs, string size, int version, string type, float width, float depth, string id)
        {
            return Look(designs, size, version, type, width, depth, id, default);
        }

        /// <summary>
        /// As above; when <paramref name="want"/> asks for it, the designs that please it best (a finished side at every open end, no
        /// yard behind) are looked through first, tier by tier, then those that please it less.
        /// </summary>
        public static int Look(IList<DesignEntry> designs, string size, int version, string type, float width, float depth, string id, Want want)
        {
            if (designs == null || string.IsNullOrEmpty(size))
            {
                return -1;
            }
            int doors = SpacesRules.Doors(size);
            int lowest = want.Sides ? 0 : want.Best;
            for (int score = want.Best; score >= lowest; score--)
            {
                for (int tier = 0; tier < 4; tier++)
                {
                    int pool = 0;
                    for (int i = 0; i < designs.Count; i++)
                    {
                        pool += InTier(designs[i], tier, size, version, type, doors, width, depth) && want.Score(designs[i]) == score ? 1 : 0;
                    }
                    if (pool == 0)
                    {
                        continue;
                    }
                    // From the hash's pick on round the pool: the first the engine takes by name (one name may stand for several sizes).
                    int start = (int)(NewBuildingsRules.Hash(id + "|look") % (uint)pool);
                    for (int k = 0; k < pool; k++)
                    {
                        int wanted = (start + k) % pool;
                        for (int i = 0; i < designs.Count; i++)
                        {
                            if (InTier(designs[i], tier, size, version, type, doors, width, depth) && want.Score(designs[i]) == score && wanted-- == 0)
                            {
                                if (NewBuildingsRules.Pick(designs, size, type, designs[i].Name, width + 0.01f, depth, id) == i)
                                {
                                    return i;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        private static bool InTier(DesignEntry d, int tier, string size, int version, string type, int doors, float width, float depth)
        {
            if (d == null)
            {
                return false;
            }
            Measure m = Of(d);
            if (!m.Free || m.Deep > depth || !SpacesRules.Fills(m.Across, width))
            {
                return false;
            }
            bool ownSize = d.Size == size;
            switch (tier)
            {
                case 0:
                    return ownSize && d.Version == version;
                case 1:
                    return ownSize && d.Version != version;
                case 2:
                    return !ownSize && d.Type == type && SpacesRules.Doors(d.Size) == doors;
                default:
                    return !ownSize && d.Type != type && SpacesRules.Doors(d.Size) == doors;
            }
        }

        /// <summary>
        /// The district's streets built up, every building on its own spaces: each placeable declaration (<paramref name="specOf"/>
        /// by id, null for an open lot) given a look that fills its lot (<see cref="Look"/>), in its middle; where none fills,
        /// its own design or <see cref="Choose"/>'s against its first side, a filler in what it leaves where one fits. The
        /// fillers into <paramref name="fills"/> (cleared), with second rows behind buildings that leave <see cref="BackLeast"/>
        /// or more of their lot. Returns how many were placed, the same on every machine; the overload also how many a look fills.
        /// </summary>
        public static int Plan(IList<BuildingsLot> lots, Func<string, MadeSpec> specOf, IList<DesignEntry> designs, List<FrontFill> fills)
        {
            return Plan(lots, specOf, designs, fills, out int _, true);
        }

        /// <summary>As above, with how many buildings wear a look that fills their spaces.</summary>
        public static int Plan(IList<BuildingsLot> lots, Func<string, MadeSpec> specOf, IList<DesignEntry> designs, List<FrontFill> fills, out int filled)
        {
            return Plan(lots, specOf, designs, fills, out filled, true);
        }

        /// <summary>
        /// As above; with <paramref name="sides"/> off, the pick of before 27 Oct 2026 (the checks hold it against the new one): a design
        /// never looks at the ends of its lot that are open or at the yard it leaves.
        /// </summary>
        public static int Plan(IList<BuildingsLot> lots, Func<string, MadeSpec> specOf, IList<DesignEntry> designs, List<FrontFill> fills, out int filled, bool sides)
        {
            fills.Clear();
            filled = 0;
            Measures.Clear();   // a load's catalogue is new: nothing of the last one's measures is kept
            if (lots == null || specOf == null || designs == null)
            {
                return 0;
            }
            var ends = new Dictionary<string, LotEnds>(lots.Count * 2);
            if (sides)
            {
                LookRules.Open(lots, id => specOf(id) != null, ends);
            }
            int placed = 0;
            for (int i = 0; i < lots.Count; i++)
            {
                BuildingsLot lot = lots[i];
                MadeSpec spec = specOf(lot.Id);
                if (spec == null || !Placeable(spec, lot))
                {
                    continue;
                }
                var want = new Want { Ends = ends.TryGetValue(lot.Id, out LotEnds open) ? open : default, Facing = lot.Facing, Sides = sides, Depth = lot.Depth };
                float along = Middle(lot);
                int look = string.IsNullOrEmpty(spec.Design) ? Look(designs, spec.Size, spec.Version, spec.Type, lot.Width, lot.Depth, spec.Id, want) : -1;
                if (look < 0 && !string.IsNullOrEmpty(spec.Design))
                {
                    // A look declared by name (a cinema's theatre front) fills its spaces as it can: in their middle.
                    look = NewBuildingsRules.Pick(designs, spec.Size, spec.Type, spec.Design, lot.Width, lot.Depth, spec.Id);
                }
                if (look >= 0)
                {
                    filled += SpacesRules.Fills(Across(designs[look]), lot.Width) ? 1 : 0;
                    Wear(spec, designs[look], Ground(lot, along, lot.Width + 0.01f, lot.Depth, 0f));
                    Behind(lot, designs, designs[look], along, lot.Width, fills, want);
                    placed++;
                    continue;
                }
                int pick = DesignOf(spec, lot, designs, want);
                pick = pick >= 0 ? pick : Widest(designs, spec, lot, want);
                if (pick < 0)
                {
                    continue;
                }
                float across = Across(designs[pick]), start = along - lot.Width * 0.5f, left = lot.Width - across;
                // The filler beside the building stands at the lot's higher end: that end is the filler's to show.
                var beside = new Want { Ends = new LotEnds { High = want.Ends.High, HighRoad = want.Ends.HighRoad }, Facing = lot.Facing, Sides = sides, Depth = lot.Depth };
                int filler = left >= SliverMost ? Filler(designs, left, lot.Depth, lot.Id + "|beside", beside) : -1;
                float own = filler >= 0 ? lot.Width - (Across(designs[filler]) + FillerSpare) : lot.Width;   // the filler's slot, the rest the building's
                float ownMiddle = start + own * 0.5f;
                Wear(spec, designs[pick], Ground(lot, ownMiddle, own + 0.01f, lot.Depth, 0f));
                if (filler >= 0)
                {
                    fills.Add(new FrontFill { Design = filler, Lot = Ground(lot, start + own + (lot.Width - own) * 0.5f, lot.Width - own, lot.Depth, 0f) });
                }
                Behind(lot, designs, designs[pick], ownMiddle, own, fills, want);
                placed++;
            }
            return placed;
        }

        // Where no design of its own size fits its spaces: the widest of any size with as many ways in that does, one picked by
        // its id among those within FrontShare, and only one the engine takes by its name for this size.
        private static int Widest(IList<DesignEntry> designs, MadeSpec spec, BuildingsLot lot, Want want)
        {
            int pick = WidestOf(designs, spec, lot, want, want.Sides && want.Ends.Any);
            return pick >= 0 || !want.Sides || !want.Ends.Any ? pick : WidestOf(designs, spec, lot, want, false);
        }

        private static int WidestOf(IList<DesignEntry> designs, MadeSpec spec, BuildingsLot lot, Want want, bool finished)
        {
            int doors = SpacesRules.Doors(spec.Size);
            float wide = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                wide = AnyFits(designs[i], doors, lot) && Allowed(designs[i], want, finished) ? Math.Max(wide, Across(designs[i])) : wide;
            }
            if (wide <= 0f)
            {
                return -1;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += AnyFits(designs[i], doors, lot) && Allowed(designs[i], want, finished) && Across(designs[i]) >= wide * FrontShare ? 1 : 0;
            }
            int start = (int)(NewBuildingsRules.Hash(spec.Id + "|widest") % (uint)pool);
            for (int k = 0; k < pool; k++)
            {
                int wanted = (start + k) % pool;
                for (int i = 0; i < designs.Count; i++)
                {
                    if (AnyFits(designs[i], doors, lot) && Allowed(designs[i], want, finished) && Across(designs[i]) >= wide * FrontShare && wanted-- == 0)
                    {
                        if (NewBuildingsRules.Pick(designs, spec.Size, spec.Type, designs[i].Name, lot.Width, lot.Depth, spec.Id) == i)
                        {
                            return i;
                        }
                        break;
                    }
                }
            }
            return -1;
        }

        private static bool AnyFits(DesignEntry d, int doors, BuildingsLot lot)
        {
            if (d == null || SpacesRules.Doors(d.Size) != doors)
            {
                return false;
            }
            Measure m = Of(d);
            return m.Free && m.Across <= lot.Width && m.Deep <= lot.Depth;
        }

        // A declaration given its look by name and its ground; a look of another size keeps its record's floor area its own
        // size's, as the interior the game loads is that size's.
        private static void Wear(MadeSpec spec, DesignEntry d, MadeLot ground)
        {
            spec.DesignArea &= d.Size == spec.Size;
            spec.Design = d.Name;
            spec.Lots = new[] { ground };
            spec.Preferred = 0;
            spec.Others = null;   // its structure is settled: the made buildings' engine takes this design by name
        }

        // A second row behind a building that leaves BackLeast or more of its lot: a building facing the same street if one with a finished side at
        // every open end fits, else one turned to face the open end (a street at a corner, an open lot), so the yard is no hole in the street and
        // no blank wall of a second row stands against it; where neither is to be had, the building facing the street as it is.
        private static void Behind(BuildingsLot lot, IList<DesignEntry> designs, DesignEntry d, float along, float width, List<FrontFill> fills, Want want)
        {
            float behind = lot.Depth - Deep(d);
            int back = behind >= BackLeast ? Filler(designs, width, behind, lot.Id + "|back", want) : -1;
            bool open = want.Sides && want.Ends.Any;
            if (back >= 0 && (!open || LookRules.Finished(designs[back], want.Ends, want.Facing)))
            {
                fills.Add(new FrontFill { Design = back, Lot = Ground(lot, along, width, behind, lot.Depth - behind), Back = true });
                return;
            }
            if (behind >= BackLeast && open && TurnedYard(lot, designs, along, width, behind, want, fills))
            {
                return;
            }
            if (back >= 0)
            {
                fills.Add(new FrontFill { Design = back, Lot = Ground(lot, along, width, behind, lot.Depth - behind), Back = true });
            }
        }

        // The yard behind a building given a building turned a quarter to face its open end: its width along the strip becomes the building's
        // depth, its depth the building's frontage. False when no design fits or the yard has no open end.
        private static bool TurnedYard(BuildingsLot lot, IList<DesignEntry> designs, float along, float width, float behind, Want want, List<FrontFill> fills)
        {
            // The yard's ends along the strip: the building's slot begins at the lot's lower end, and reaches its higher end where nothing stands beside it.
            float lotStart = Middle(lot) - lot.Width * 0.5f, slotStart = along - width * 0.5f, slotEnd = along + width * 0.5f, lotEnd = lotStart + lot.Width;
            bool low = want.Ends.Low && slotStart - lotStart < 0.5f, high = want.Ends.High && lotEnd - slotEnd < 0.5f;
            if (!low && !high)
            {
                return false;
            }
            bool alongX = Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5;
            bool toHigh = high && (!low || (NewBuildingsRules.Hash(lot.Id + "|yard") & 1u) == 0u || want.Ends.HighRoad && !want.Ends.LowRoad);
            float turned = toHigh ? (alongX ? 90f : 0f) : (alongX ? 270f : 180f);
            var asked = new Want { Ends = default, Facing = turned, Sides = false, Depth = width };
            int pick = Filler(designs, behind, width, lot.Id + "|yard", asked);
            if (pick < 0)
            {
                return false;
            }
            MadeLot yard = Ground(lot, along, width, behind, lot.Depth - behind);
            fills.Add(new FrontFill { Design = pick, Lot = new MadeLot(yard.X, yard.Z, turned, behind, width), Back = true });
            return true;
        }

        // A declaration this may place: its own lot alone as it is planned (never a landmark's or a finished building's span
        // of several), on its front line and in its middle, no special place's look copied whole.
        private static bool Placeable(MadeSpec spec, BuildingsLot lot)
        {
            if (spec.Lots == null || spec.Lots.Length != 1 || spec.Whole)
            {
                return false;
            }
            MadeLot own = spec.Lots[0];
            return own.Inset == 0f && own.Side == 0 && Math.Abs(own.Width - lot.Width) < 0.5f && Math.Abs(own.Depth - lot.Depth) < 0.5f
                   && Math.Abs(own.X - lot.X) < 0.5f && Math.Abs(own.Z - lot.Z) < 0.5f;
        }

        public const float SliverMost = 1f;      // m: the widest gap between two pieces of a run that is no gap to the eye

        // A declaration's design: its own by name, else Choose's of its size, else of its first other structure with one (taken).
        private static int DesignOf(MadeSpec spec, BuildingsLot lot, IList<DesignEntry> designs, Want want)
        {
            if (!string.IsNullOrEmpty(spec.Design))
            {
                return NewBuildingsRules.Pick(designs, spec.Size, spec.Type, spec.Design, lot.Width, lot.Depth, spec.Id);
            }
            int pick = Choose(designs, spec.Size, spec.Type, lot.Width, lot.Depth, spec.Id, want);
            for (int s = 0; pick < 0 && spec.Others != null && s < spec.Others.Length; s++)
            {
                pick = Choose(designs, spec.Others[s].Size, spec.Others[s].Type, lot.Width, lot.Depth, spec.Id, want);
                if (pick >= 0)
                {
                    spec.Size = spec.Others[s].Size;
                    spec.Version = spec.Others[s].Version;
                    spec.Type = spec.Others[s].Type;
                }
            }
            if (pick >= 0)
            {
                spec.Others = null;   // its structure is settled: the made buildings' engine takes this design by name
            }
            return pick;
        }

        // The ground of a place along a lot's strip: its middle at <along> on the strip's axis, as wide and deep as given,
        // its front <back> m behind the lot's front line, facing the lot's street.
        private static MadeLot Ground(BuildingsLot lot, float along, float width, float depth, float back)
        {
            double f = lot.Facing * Math.PI / 180.0;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float frontX = lot.X + fx * lot.Depth * 0.5f, frontZ = lot.Z + fz * lot.Depth * 0.5f;   // the lot's front line, at its middle
            float inX = frontX - fx * (back + depth * 0.5f), inZ = frontZ - fz * (back + depth * 0.5f);
            bool alongX = Math.Abs(fz) > 0.5f;
            return new MadeLot(alongX ? along : inX, alongX ? inZ : along, lot.Facing, width, depth);
        }

        // m along a lot's strip (x for an avenue's lots, z for a street's): its middle.
        private static float Middle(BuildingsLot lot)
        {
            return Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5 ? lot.X : lot.Z;
        }

        /// <summary>m: a design's front, across its door's side.</summary>
        public static float Across(DesignEntry d) => Of(d).Across;

        /// <summary>m: a design's depth back from its door's side.</summary>
        public static float Deep(DesignEntry d) => Of(d).Deep;

        // A design's measures worked out once a layout: its front, its depth, and whether it may stand for a lot or a gap.
        private struct Measure
        {
            public float Across, Deep;
            public bool Free;
        }

        private static readonly Dictionary<DesignEntry, Measure> Measures = new Dictionary<DesignEntry, Measure>(1024);

        private static Measure Of(DesignEntry d)
        {
            if (Measures.TryGetValue(d, out Measure m))
            {
                return m;
            }
            NewBuildingsRules.DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            m = new Measure { Across = uMax - uMin, Deep = vMax - vMin, Free = !d.Whole && NewBuildingsRules.Copyable(d) };
            Measures.Add(d, m);
            return m;
        }
    }
}
