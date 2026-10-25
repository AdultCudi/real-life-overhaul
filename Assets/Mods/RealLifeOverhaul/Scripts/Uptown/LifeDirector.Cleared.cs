using System;
using System.Collections.Generic;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One of the district's buildings standing at this load, as the construction game reads one to redevelop it.</summary>
    public struct DistrictBuilding
    {
        public string Lot;          // its own lot: its address's
        public string Street;
        public int Number;
        public string Size;         // the game's structure it stands as
        public int Version;
        public string Type;
        public string Design;       // the look it wears (a DesignEntry's name)
        public int Storeys;         // storeys of floor area its record has now
        public int SizeSqm;         // one storey of its size: what a storey more adds
        public int Sqm;             // its floor area at this load
        public int Height;          // m: its look's height
        public string[] Over;       // the other lots of its block it stands over, or null
        public bool Finished;       // a building the construction game finished (a record of the district's line)
        public bool Kept;           // a place of the mod's own, a lease, a big place, a special service or a landmark: never redeveloped
        public float DoorX, DoorY, DoorZ, DoorFacing;
    }

    /// <summary>
    /// The district's buildings for redevelopment (docs/features/construction.md, "Demolition", "Extensions and change of use"):
    /// a standing building read for the construction game, one knocked down in play (out of the game's lists, hidden, its
    /// carving taken off so its ground is walked on again, its fillers hidden, every record naming its address let go for
    /// good, its lots marked cleared in the district's line so nothing stands there from the next load until a building is
    /// finished there again), and its look hidden while it comes down behind the wrap. Nothing here runs a frame; each call a
    /// pass over the standing buildings at most.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<BuildingsLot> ClearSpan = new List<BuildingsLot>(8);

        /// <summary>True for a lot whose building was knocked down and on which nothing has been finished since.</summary>
        public static bool Cleared(string lotId)
        {
            return _state.IsCleared(lotId) && _state.On(lotId) == null;
        }

        // A lot with nothing of its own to stand at this load: knocked down and not built on again, or under a building finished over it.
        private static bool Unbuilt(string lotId)
        {
            if (Cleared(lotId))
            {
                return true;
            }
            FinishedBuilding over = _state.Covering(lotId);
            return over != null && !string.Equals(over.Lot, lotId, StringComparison.Ordinal);
        }

        /// <summary>The district's building standing on a lot at this load (its own lot's), as the construction game reads it; false for none.</summary>
        public static bool StandingOn(string lotId, out DistrictBuilding b)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Standing.Count; i++)
            {
                if (string.Equals(Standing[i].Spec.Id, lotId, StringComparison.Ordinal))
                {
                    return Read(Standing[i], out b);
                }
            }
            b = default(DistrictBuilding);
            return false;
        }

        /// <summary>Every one of the district's buildings standing at this load, as the construction game reads them, into the caller's list (cleared).</summary>
        public static void StandingAll(List<DistrictBuilding> into)
        {
            into.Clear();
            for (int i = 0; i < Standing.Count; i++)
            {
                if (Read(Standing[i], out DistrictBuilding b))
                {
                    into.Add(b);
                }
            }
        }

        /// <summary>The district's building standing at an address at this load; false for none.</summary>
        public static bool StandingAt(string street, int number, out DistrictBuilding b)
        {
            for (int i = 0; !string.IsNullOrEmpty(street) && i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (made.Spec.Number == number && string.Equals(made.Spec.Street, street, StringComparison.Ordinal))
                {
                    return Read(made, out b);
                }
            }
            b = default(DistrictBuilding);
            return false;
        }

        private static bool Read(MadeBuilding made, out DistrictBuilding b)
        {
            b = default(DistrictBuilding);
            if (made == null || made.Building == null || made.Controller == null)
            {
                return false;
            }
            string lot = made.Spec.Id;
            FinishedBuilding f = _state.On(lot);
            int sizeSqm = Math.Max(1, MadeGame.SquareMetres(made.Spec.Size));
            int sqm = Math.Max(1, made.Building.totalSqm);
            IList<DesignEntry> designs = CopyGame.Catalogue;
            DesignEntry d = made.DesignIndex >= 0 && made.DesignIndex < designs.Count ? designs[made.DesignIndex] : null;
            // Its type and storeys as they stand now: an extension or a change of use in play holds at once, its record's (f) from the next load.
            int storeys = f != null && f.Storeys > 0 ? f.Storeys : made.Spec.DesignArea ? Math.Max(1, (int)Math.Round(sqm / (double)sizeSqm)) : Math.Max(1, made.Spec.Floors);
            b = new DistrictBuilding
            {
                Lot = lot, Street = made.Spec.Street, Number = made.Spec.Number, Size = made.Spec.Size, Version = made.Spec.Version, Type = made.Building.BuildingType,
                Design = made.Design ?? string.Empty, Storeys = storeys,
                SizeSqm = sizeSqm, Sqm = sqm, Height = d != null ? Mathf.RoundToInt(d.Height) : 0, Over = f != null ? f.Over : null, Finished = f != null,
                Kept = KeptFromRedevelopment(made), DoorX = made.DoorX, DoorY = made.DoorY, DoorZ = made.DoorZ, DoorFacing = made.DoorFacing
            };
            return true;
        }

        // A building redevelopment leaves alone: a lease's office (the city's rival builders), a big place, one of the district's
        // special places, one the mod holds for a place of its own, a landmark's (its block kept open for it), a whole copy.
        private static bool KeptFromRedevelopment(MadeBuilding made)
        {
            string lot = made.Spec.Id;
            if (LeaseOf(lot) >= 0 || BigPlacesRules.Holds(lot) || SpecialsRules.At(lot) != null || made.Spec.Whole || made.Building.SpecialService != null
                || SpecialBuildings.TakenBy(made.Registration) != null)
            {
                return true;
            }
            Lot plain = UptownLots.Get(lot);
            Func<int, bool> landmarks = DistrictBuildings.KeptOpen;
            try
            {
                return plain != null && landmarks != null && landmarks(plain.Block);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return true;   // when it cannot be told, the building is left standing
            }
        }

        /// <summary>
        /// A standing building knocked down now (DistrictBuildings.Demolish): every record naming its address let go for good,
        /// out of the game's lists, hidden, its carving off, its fillers hidden, its lots cleared in the district's line. False,
        /// with why, for no building standing there, one redevelopment leaves alone, the player inside it, or the district's
        /// line unreadable. Out of a Going Public session only (the construction game asks so).
        /// </summary>
        public static bool Demolish(string lotId, out string why)
        {
            why = null;
            MadeBuilding made = null;
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Standing.Count && made == null; i++)
            {
                made = string.Equals(Standing[i].Spec.Id, lotId, StringComparison.Ordinal) ? Standing[i] : null;
            }
            why = made == null || made.Building == null ? "no building of the district stands on lot " + (lotId ?? "(none)") + " at this load"
                  : KeptFromRedevelopment(made) ? "it is one of the district's own places, never knocked down"
                  : LifeGame.Inside(made) ? "the player is inside it"
                  : _stateUnread ? "the district's own line in this save cannot be read, and is kept as it is" : null;
            if (why != null)
            {
                Logger?.Info("Uptown: the building on lot " + (lotId ?? "(none)") + " is not knocked down: " + why + ".");
                return false;
            }
            FinishedBuilding f = _state.On(lotId);
            ClearSpan.Clear();
            if (FindLot(lotId, out BuildingsLot own))
            {
                ClearSpan.Add(own);
            }
            for (int i = 0; f != null && f.Over != null && i < f.Over.Length; i++)
            {
                if (FindLot(f.Over[i], out BuildingsLot over))
                {
                    ClearSpan.Add(over);
                }
            }
            float turn = made.DoorFacing * Mathf.Deg2Rad;
            int records = DistrictSaveDirector.Clear(made.Spec.Street, made.Spec.Number, made.DoorX + Mathf.Sin(turn) * OutsideDoor, made.DoorY,
                made.DoorZ + Mathf.Cos(turn) * OutsideDoor, made.DoorFacing, UptownLots.Exit.District, "it was knocked down");
            int fillers = LifeGame.Leave(made, ClearSpan);
            CopyGame.Show(made.Controller, false);
            made.Shown = false;
            MadeBuildings.Changed();
            Standing.Remove(made);
            if (f != null)
            {
                _state.Finished.Remove(f);
            }
            for (int i = 0; i < ClearSpan.Count; i++)
            {
                string id = ClearSpan[i].Id;
                StandingLots.Remove(id);
                BuiltLots.Remove(id);
                KeptLots.Remove(id);
                _state.MarkCleared(id);
            }
            WriteState();
            _stamp++;
            DistrictPlaces.Changed();
            Logger?.Info("Uptown: the building at " + made.Address + " on lot " + lotId + (ClearSpan.Count > 1 ? " and over " + (ClearSpan.Count - 1) + " more lot(s)" : string.Empty)
                         + " was knocked down: " + records + " record(s) naming it let go for good, " + fillers + " filler(s) on its ground hidden, its ground walked on again; its "
                         + ClearSpan.Count + " lot(s) stay cleared until a building is finished there.");
            ClearSpan.Clear();
            return true;
        }

        /// <summary>A standing building's look hidden while it comes down behind the wrap, or shown again (its works given up); false when nothing changed.</summary>
        public static bool HideLook(string lotId, bool hide)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Standing.Count; i++)
            {
                if (string.Equals(Standing[i].Spec.Id, lotId, StringComparison.Ordinal))
                {
                    return MadeBuildings.HideLook(Standing[i], hide);
                }
            }
            return false;
        }
    }
}
