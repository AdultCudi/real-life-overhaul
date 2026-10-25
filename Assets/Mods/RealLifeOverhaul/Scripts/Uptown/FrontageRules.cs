using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One piece of a run of frontage: a lot's building (<see cref="Item"/> its place in the run) or a filler (-1), from and to along the run (m).</summary>
    public struct FrontSlot
    {
        public int Item;      // the run's building it is, or -1 for a filler
        public int Design;    // the catalogue's design a filler wears; a building's is its lot's own choice
        public float From;    // m along the run from its start
        public float To;
    }

    /// <summary>A filler to stand: a design of the catalogue for its look alone, on a piece of ground as a lot's building stands (its front on the ground's front line).</summary>
    public struct FrontFill
    {
        public int Design;
        public MadeLot Lot;
        public bool Back;     // a second row behind a lot's building, not a gap along the street
    }

    /// <summary>
    /// How Uptown's streets are built up as Manhattan's busiest are (the owner, 25 Oct 2026: "There is so much free space
    /// between buildings in uptown ... Please fill the lots"), game-free (EconomyCheck's UptownFrontChecks). A lot's
    /// building is the design of its size that fills its frontage best, then its depth, then is the tallest
    /// (<see cref="Choose"/>); the buildings of a run of built lots along one street stand side by side from its start
    /// (<see cref="Arrange"/>), and what their fronts leave of the run is closed with fillers, the game's own buildings
    /// copied for their looks alone, the narrowest 6 m wide, and what is still left shared out between them so no gap is
    /// more than a sliver. Every answer comes from the catalogue (the same on every machine) and the arguments alone.
    /// COST: nothing per frame; a few passes over the catalogue for each lot and run once a load, behind the loading screen.
    /// </summary>
    public static class FrontageRules
    {
        public const float FrontShare = 0.9f;    // of the widest fitting design's front: a lot's building is among those this wide
        public const float DepthShare = 0.75f;   // of the deepest of those: and among those this deep
        public const float HeightShare = 0.6f;   // of the tallest of those: and among those this tall
        public const int FillersMost = 4;        // fillers a run takes at most: the rest of its frontage is shared out
        public const float FillerSpare = 0.4f;   // m a filler's slot is wider than its front, so it fits as a lot's design does
        public const float BackLeast = 10f;      // m of a lot left behind its building that takes a second row: less is a yard
        public const float Adjacent = 0.1f;      // m two lots of a strip may be apart and still be one run

        /// <summary>
        /// The design a lot's building wears: of its size's designs that can be copied and fit (those of its type if any),
        /// the widest within <see cref="FrontShare"/>, of those the deepest within <see cref="DepthShare"/>, of those the
        /// tallest within <see cref="HeightShare"/>, one picked by the lot's id. -1 when none fits.
        /// </summary>
        public static int Choose(IList<DesignEntry> designs, string size, string type, float width, float depth, string id)
        {
            if (designs == null)
            {
                return -1;
            }
            int typed = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                typed += Fits(designs[i], size, width, depth) && designs[i].Type == type ? 1 : 0;
            }
            bool byType = typed > 0;
            float wide = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                wide = In(designs[i], size, type, byType, width, depth) ? Math.Max(wide, Across(designs[i])) : wide;
            }
            if (wide <= 0f)
            {
                return -1;
            }
            float deep = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                deep = In(designs[i], size, type, byType, width, depth) && Across(designs[i]) >= wide * FrontShare ? Math.Max(deep, Deep(designs[i])) : deep;
            }
            float tall = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                tall = Pooled(designs[i], size, type, byType, width, depth, wide, deep, 0f) ? Math.Max(tall, designs[i].Height) : tall;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += Pooled(designs[i], size, type, byType, width, depth, wide, deep, tall) ? 1 : 0;
            }
            int wanted = (int)(NewBuildingsRules.Hash(id + "|front") % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (Pooled(designs[i], size, type, byType, width, depth, wide, deep, tall) && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
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
            float wide = 0f;
            for (int i = 0; designs != null && i < designs.Count; i++)
            {
                wide = FillerFits(designs[i], most, depth) ? Math.Max(wide, Across(designs[i])) : wide;
            }
            if (wide <= 0f)
            {
                return -1;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += FillerFits(designs[i], most, depth) && Across(designs[i]) >= wide * FrontShare ? 1 : 0;
            }
            int wanted = (int)(NewBuildingsRules.Hash(key + "|filler") % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (FillerFits(designs[i], most, depth) && Across(designs[i]) >= wide * FrontShare && wanted-- == 0)
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
        /// A run of built lots along one street, laid out: its buildings (<paramref name="widths"/>, their fronts, in order)
        /// side by side; what their fronts leave of <paramref name="length"/> closed with fillers no deeper than
        /// <paramref name="depth"/>, at most <see cref="FillersMost"/>, each the widest that fits what is still left, spread
        /// between the buildings; and what is left after them at the run's soft ends (<paramref name="startSoft"/>,
        /// <paramref name="endSoft"/>: beside open land, not a block's corner), else shared out evenly before, between and after
        /// all of them. Into <paramref name="into"/> (cleared), in order along the run. Picks by <paramref name="key"/>.
        /// </summary>
        public static void Arrange(float length, IList<float> widths, IList<DesignEntry> designs, float depth, string key, List<FrontSlot> into)
        {
            Arrange(length, widths, designs, depth, key, false, false, into);
        }

        public static void Arrange(float length, IList<float> widths, IList<DesignEntry> designs, float depth, string key, bool startSoft, bool endSoft, List<FrontSlot> into)
        {
            into.Clear();
            int n = widths != null ? widths.Count : 0;
            float left = length;
            for (int i = 0; i < n; i++)
            {
                left -= widths[i];
            }
            var fillers = new List<int>(FillersMost);
            var fills = new List<float>(FillersMost);
            while (n > 0 && left > 0f && fillers.Count < FillersMost)
            {
                int f = Filler(designs, left, depth, key + "#" + fillers.Count);
                if (f < 0)
                {
                    break;
                }
                float w = Across(designs[f]) + FillerSpare;
                fillers.Add(f);
                fills.Add(w);
                left -= w;
            }
            int pieces = n + fillers.Count;
            left = Math.Max(0f, left);
            bool soft = startSoft || endSoft;
            float gap = pieces > 0 && !soft ? left / (pieces + 1) : 0f;
            float at = soft ? (startSoft ? (endSoft ? left * 0.5f : left) : 0f) : gap;
            int nextFiller = 0;
            for (int i = 0; i < n; i++)
            {
                into.Add(new FrontSlot { Item = i, Design = -1, From = at, To = at + widths[i] });
                at += widths[i] + gap;
                // The fillers after the buildings at even steps along the run: filler k after building ((k + 1) n / (f + 1)) - 1.
                while (nextFiller < fillers.Count && i == Math.Max(0, (nextFiller + 1) * n / (fillers.Count + 1) - 1))
                {
                    into.Add(new FrontSlot { Item = -1, Design = fillers[nextFiller], From = at, To = at + fills[nextFiller] });
                    at += fills[nextFiller] + gap;
                    nextFiller++;
                }
            }
            for (; nextFiller < fillers.Count; nextFiller++)
            {
                into.Add(new FrontSlot { Item = -1, Design = fillers[nextFiller], From = at, To = at + fills[nextFiller] });
                at += fills[nextFiller] + gap;
            }
        }

        /// <summary>
        /// The district's streets built up: every strip's runs of placeable lots (<paramref name="specOf"/> by id, null for an
        /// open lot) laid out by <see cref="Arrange"/>, each declaration given its design by name (its own, else
        /// <see cref="Choose"/>'s of its size or of its first other structure with one) and its place in the run as its lot;
        /// the fillers into <paramref name="fills"/> (cleared): along the street, and second rows behind buildings that leave
        /// <see cref="BackLeast"/> or more of their lot. Returns how many were placed; the same on every machine.
        /// </summary>
        public static int Plan(IList<BuildingsLot> lots, Func<string, MadeSpec> specOf, IList<DesignEntry> designs, List<FrontFill> fills)
        {
            fills.Clear();
            Measures.Clear();   // a load's catalogue is new: nothing of the last one's measures is kept
            if (lots == null || specOf == null || designs == null)
            {
                return 0;
            }
            var strips = new SortedDictionary<long, List<int>>();
            for (int i = 0; i < lots.Count; i++)
            {
                long key = (long)lots[i].Block * 1000 + (int)Math.Round(lots[i].Facing);
                if (!strips.TryGetValue(key, out List<int> strip))
                {
                    strip = new List<int>();
                    strips.Add(key, strip);
                }
                strip.Add(i);
            }
            int placed = 0;
            var run = new List<int>(16);
            var picks = new List<int>(16);
            var widths = new List<float>(16);
            var slots = new List<FrontSlot>(24);
            foreach (List<int> strip in strips.Values)
            {
                strip.Sort((a, b) => Start(lots[a]).CompareTo(Start(lots[b])));
                run.Clear();
                picks.Clear();
                for (int k = 0; k <= strip.Count; k++)
                {
                    int i = k < strip.Count ? strip[k] : -1;
                    MadeSpec spec = i >= 0 ? specOf(lots[i].Id) : null;
                    int pick = spec != null && Placeable(spec, lots[i]) ? DesignOf(spec, lots[i], designs) : -1;
                    bool joins = pick >= 0 && (run.Count == 0 || Math.Abs(Start(lots[i]) - End(lots[run[run.Count - 1]])) <= Adjacent);
                    if (!joins && run.Count > 0)
                    {
                        // A run's end is soft where the strip goes on past it (open land, reserved ground), hard at the strip's own end.
                        placed += Lay(lots, run, picks, specOf, designs, widths, slots, fills, run[0] != strip[0], run[run.Count - 1] != strip[strip.Count - 1]);
                        run.Clear();
                        picks.Clear();
                    }
                    if (pick >= 0)
                    {
                        run.Add(i);
                        picks.Add(pick);
                    }
                }
            }
            return placed;
        }

        // One run laid out: its declarations given their designs and their places, its fillers listed.
        private static int Lay(IList<BuildingsLot> lots, List<int> run, List<int> picks, Func<string, MadeSpec> specOf, IList<DesignEntry> designs, List<float> widths,
            List<FrontSlot> slots, List<FrontFill> fills, bool startSoft, bool endSoft)
        {
            BuildingsLot first = lots[run[0]];
            float from = Start(first), length = End(lots[run[run.Count - 1]]) - from;
            widths.Clear();
            float left = length;
            for (int k = 0; k < run.Count; k++)
            {
                widths.Add(Across(designs[picks[k]]));
                left -= widths[k];
            }
            // What the run's fronts leave, offered to its buildings first: each not declared with a design of its own may take a
            // wider one of its size with its share of what is left, so real buildings close the run before fillers do.
            for (int k = 0; k < run.Count && left > SliverMost; k++)
            {
                MadeSpec spec = specOf(lots[run[k]].Id);
                if (!string.IsNullOrEmpty(spec.Design))
                {
                    continue;
                }
                float allowed = widths[k] + left / (run.Count - k);
                int wider = Choose(designs, spec.Size, spec.Type, allowed, lots[run[k]].Depth, spec.Id);
                if (wider >= 0 && Across(designs[wider]) > widths[k] && Across(designs[wider]) <= allowed)
                {
                    left -= Across(designs[wider]) - widths[k];
                    widths[k] = Across(designs[wider]);
                    picks[k] = wider;
                }
            }
            Arrange(length, widths, designs, first.Depth, first.Id, startSoft, endSoft, slots);
            for (int s = 0; s < slots.Count; s++)
            {
                FrontSlot slot = slots[s];
                BuildingsLot lot = slot.Item >= 0 ? lots[run[slot.Item]] : first;
                MadeLot ground = Ground(lot, from + (slot.From + slot.To) * 0.5f, slot.To - slot.From + (slot.Item >= 0 ? 0.01f : 0f), lot.Depth, 0f);
                if (slot.Item < 0)
                {
                    fills.Add(new FrontFill { Design = slot.Design, Lot = ground });
                    continue;
                }
                DesignEntry d = designs[picks[slot.Item]];
                MadeSpec spec = specOf(lot.Id);
                spec.Design = d.Name;
                spec.Lots = new[] { ground };
                spec.Preferred = 0;
                float behind = lot.Depth - Deep(d);
                int back = behind >= BackLeast ? Filler(designs, ground.Width, behind, lot.Id + "|back") : -1;
                if (back >= 0)
                {
                    fills.Add(new FrontFill { Design = back, Lot = Ground(lot, from + (slot.From + slot.To) * 0.5f, ground.Width, behind, lot.Depth - behind), Back = true });
                }
            }
            return run.Count;
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
        private static int DesignOf(MadeSpec spec, BuildingsLot lot, IList<DesignEntry> designs)
        {
            if (!string.IsNullOrEmpty(spec.Design))
            {
                return NewBuildingsRules.Pick(designs, spec.Size, spec.Type, spec.Design, lot.Width, lot.Depth, spec.Id);
            }
            int pick = Choose(designs, spec.Size, spec.Type, lot.Width, lot.Depth, spec.Id);
            for (int s = 0; pick < 0 && spec.Others != null && s < spec.Others.Length; s++)
            {
                pick = Choose(designs, spec.Others[s].Size, spec.Others[s].Type, lot.Width, lot.Depth, spec.Id);
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

        // m along a strip's axis (x for an avenue's lots, z for a street's): where a lot starts and ends.
        private static float Start(BuildingsLot lot)
        {
            return (Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5 ? lot.X : lot.Z) - lot.Width * 0.5f;
        }

        private static float End(BuildingsLot lot)
        {
            return Start(lot) + lot.Width;
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
