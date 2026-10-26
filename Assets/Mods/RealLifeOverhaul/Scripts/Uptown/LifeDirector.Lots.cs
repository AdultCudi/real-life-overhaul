using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's lots for the construction game (DistrictBuildings): which hold a building, a finished building kept in
    /// the district's own line of the save and stood as a district building from the next load, one standing over several
    /// lots of its block (a landmark's), the plan's lots built from the first day, and a lot's traffic index with what stands
    /// near it lifting it. The same calls give the same line on every machine with the same mod. Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly HashSet<string> FirstDayLots = new HashSet<string>(StringComparer.Ordinal);   // the plan's, worked out once a run
        private static readonly HashSet<string> StandingLots = new HashSet<string>(StringComparer.Ordinal);   // under a standing building at this load, its own lot or one it stands over
        private static readonly List<BuildingsLot> Span = new List<BuildingsLot>(BuildingsRules.SpanMost);
        private static readonly List<int> FirstDayPick = new List<int>(512);
        private static Func<int, bool> _firstDayFor;   // the kept-open blocks FirstDayLots were worked out with
        private static bool _liftFaulted;

        /// <summary>
        /// True when the lot holds a district building at this load, or one finished there (or over it) waits for the next
        /// load; and always for a lot something of the game's stands into (BuildingsRules.Clear), which stays empty.
        /// </summary>
        public static bool Taken(string lotId)
        {
            if (string.IsNullOrEmpty(lotId))
            {
                return false;
            }
            return !BuildingsRules.Clear(lotId) || _state.Covering(lotId) != null || BuiltLots.Contains(lotId) || StandsOn(lotId);
        }

        /// <summary>True when a district building stands on the lot at this load, its own lot or one it stands over.</summary>
        public static bool StandsOn(string lotId)
        {
            return !string.IsNullOrEmpty(lotId) && StandingLots.Contains(lotId);
        }

        // The lots under the standing buildings, made once their buildings stand: Taken is asked of every lot on a page.
        private static void MarkStanding()
        {
            StandingLots.Clear();
            for (int i = 0; i < Standing.Count; i++)
            {
                string own = Standing[i].Spec.Id;
                StandingLots.Add(own);
                FinishedBuilding f = _state.On(own);
                for (int k = 0; f != null && f.Over != null && k < f.Over.Length; k++)
                {
                    StandingLots.Add(f.Over[k]);
                }
            }
        }

        /// <summary>
        /// True for a lot the plan builds from the first day (BuildingsRules.Built, with the blocks kept open for the landmarks):
        /// fixed data, the same whether or not the district lives at this load, so a feature can leave those lots out of a site
        /// of its own before the city loads.
        /// </summary>
        public static bool FirstDay(string lotId)
        {
            Func<int, bool> kept = DistrictBuildings.KeptOpen;
            if (FirstDayLots.Count == 0 || !ReferenceEquals(kept, _firstDayFor))
            {
                if (Plan.Count == 0)
                {
                    LifeGame.Lots(Plan);
                }
                FirstDayLots.Clear();
                _firstDayFor = kept;
                BuildingsRules.Built(Plan, kept, LifeGame.AwayX, LifeGame.AwayZ, FirstDayPick);
                for (int i = 0; i < FirstDayPick.Count; i++)
                {
                    FirstDayLots.Add(Plan[FirstDayPick[i]].Id);
                }
            }
            return !string.IsNullOrEmpty(lotId) && FirstDayLots.Contains(lotId);
        }

        /// <summary>The lot's traffic index by the rules, lifted by what stands near it (DistrictBuildings.TrafficLift); -1 for a lot not in the plan.</summary>
        public static int TrafficOf(string lotId)
        {
            return FindLot(lotId, out BuildingsLot lot) ? BuildingsRules.Lifted(BuildingsRules.Traffic(lot, _nearest, LifeGame.AwayX, LifeGame.AwayZ), Lift(lotId) + Density(lot)) : -1;   // and a packed block's people (LifeDirector.Market)
        }

        // What stands near a lot lifts its traffic, as the feature that set the lift says; nothing when unset, or after a fault (said once).
        private static int Lift(string lotId)
        {
            Func<string, int> lift = DistrictBuildings.TrafficLift;
            if (lift == null || _liftFaulted)
            {
                return 0;
            }
            try
            {
                return Math.Max(0, lift(lotId));
            }
            catch (Exception e)
            {
                _liftFaulted = true;
                Logger?.Error(e);
                Logger?.Warn("Uptown: the lift of its lots' traffic failed; its lots keep their own traffic for the rest of the session.");
                return 0;
            }
        }

        /// <summary>A building finished on an empty lot, kept in the district's line, a district building from the next load (DistrictBuildings.Finish).</summary>
        public static bool Finish(string lotId, string design, string size, int version, string type, int storeys, FinishOwner owner, double price, out string why)
        {
            return FinishOver(lotId, null, design, size, version, type, storeys, owner, price, out why);
        }

        /// <summary>
        /// A building finished on an empty lot and standing over <paramref name="over"/>, other empty lots of its block, too
        /// (DistrictBuildings.FinishOver): its address its own lot's, its ground the rectangle round them all. None: its own lot alone.
        /// </summary>
        public static bool FinishOver(string lotId, IList<string> over, string design, string size, int version, string type, int storeys, FinishOwner owner, double price,
                                      out string why)
        {
            bool known = FindLot(lotId, out BuildingsLot lot);
            bool structure = known && MadeGame.StructureTakes(size, version, type);
            bool spanned = over != null && over.Count > 0;
            why = BuildingsRules.FinishRefusal(lot, known, Taken(lotId), size, version, type, storeys, owner, price, structure, Lives, GoingPublic.InSession, spanned, Cleared(lotId));
            if (why == null && spanned)
            {
                why = SpanWhy(lotId, over);
            }
            if (why == null && _stateUnread)
            {
                why = "the district's own line in this save cannot be read (a later version's, or damaged), and is kept as it is";
            }
            if (why == null && _state.Finished.Count >= LifeStore.FinishedMost)
            {
                why = "the district keeps no more than " + LifeStore.FinishedMost + " finished buildings";
            }
            if (why != null)
            {
                Logger?.Info("Uptown: a building finished on lot " + (lotId ?? "(none)") + " is refused: " + why + ".");
                return false;
            }
            string[] also = null;
            if (spanned)
            {
                also = new string[over.Count];
                over.CopyTo(also, 0);
            }
            GameInstance save = SaveGameManager.Current;
            _state.Finished.Add(new FinishedBuilding
            {
                Lot = lotId, Size = size, Version = version, Type = type, Design = design ?? string.Empty, Storeys = storeys, Owner = owner,
                Price = owner == FinishOwner.Player ? price : 0, Day = save != null ? save.Day : 0, Settled = false, Over = also
            });
            WriteState();
            _stamp++;
            Logger?.Info("Uptown: a building finished on lot " + lotId + (spanned ? " and over " + also.Length + " lot(s) of its block" : string.Empty) + " ("
                         + NewBuildingsRules.StructureName(size, version) + ", " + type + ", " + (owner == FinishOwner.Player ? "the player's own" : "the city's")
                         + ") stands there as a district building from the next load.");
            return true;
        }

        // Why a building may not stand over the other lots named as well as its own, or null.
        private static string SpanWhy(string lotId, IList<string> over)
        {
            Lot own = UptownLots.Get(lotId);
            int known = 0, free = 0, sameBlock = 0;
            bool distinct = true;
            for (int i = 0; i < over.Count; i++)
            {
                string id = over[i];
                distinct &= !string.Equals(id, lotId, StringComparison.Ordinal);
                for (int k = 0; k < i; k++)
                {
                    distinct &= !string.Equals(over[k], id, StringComparison.Ordinal);
                }
                Lot l = UptownLots.Get(id);
                known += l != null && FindLot(id, out _) ? 1 : 0;
                free += Taken(id) ? 0 : 1;
                sameBlock += l != null && own != null && l.Block == own.Block ? 1 : 0;
            }
            return BuildingsRules.SpanRefusal(over.Count, known, free, sameBlock, distinct);
        }

        // The ground a finished building stands on: its own lot's, or the rectangle round its lot and every lot it stands over.
        private static BuildingsLot GroundOf(BuildingsLot lot, FinishedBuilding f)
        {
            if (f == null || f.Over == null || f.Over.Length == 0)
            {
                return lot;
            }
            Span.Clear();
            Span.Add(lot);
            for (int i = 0; i < f.Over.Length; i++)
            {
                if (FindLot(f.Over[i], out BuildingsLot other))
                {
                    Span.Add(other);
                }
            }
            if (!BuildingsRules.SpanGround(Span, out float x, out float z, out float width, out float depth))
            {
                return lot;
            }
            lot.X = x;
            lot.Z = z;
            lot.Width = (int)Math.Floor(width);   // whole metres, never past the lots' own edges
            lot.Depth = (int)Math.Floor(depth);
            return lot;
        }

        /// <summary>
        /// A building finished on a lot taken back before any load stood it (the tests' way back): false, with why, once its
        /// first load has set it up or it stands, as the game's records then name it.
        /// </summary>
        public static bool Withdraw(string lotId, out string why)
        {
            FinishedBuilding f = _state.On(lotId);
            why = f == null ? "no building was finished on that lot"
                  : f.Settled || StandsOn(lotId) ? "it has stood at a load, and the game's records name it"
                  : _stateUnread ? "the district's own line in this save cannot be read" : null;
            if (why != null)
            {
                return false;
            }
            _state.Finished.Remove(f);
            WriteState();
            _stamp++;
            Logger?.Info("Uptown: the building finished on lot " + lotId + " was taken back before any load stood it.");
            return true;
        }

        /// <summary>What lifts the lots' traffic changed: readers of the lots read them again.</summary>
        public static void LiftsChanged()
        {
            _stamp++;
        }

        private static bool FindLot(string lotId, out BuildingsLot lot)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Plan.Count; i++)
            {
                if (string.Equals(Plan[i].Id, lotId, System.StringComparison.Ordinal))
                {
                    lot = Plan[i];
                    return true;
                }
            }
            lot = default;
            return false;
        }
    }
}
