using System;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's buildings extended and changed in use (docs/features/construction.md, "Extensions and change of use"): a
    /// standing building given more storeys of floor area, its look its own, or made another use that the game's structure of its
    /// size and version takes. Each holds now (the game's building record; for its floor area the player's deed too, so the rent
    /// from the city's tenants and the property tax follow the game's own arithmetic at once) and from every load (the district's
    /// line: the record of the building as it stands, with its storeys or its type changed; a building standing from the first
    /// day is given one, its address, registration and deed staying its own, so nothing of the game's names anything new).
    /// Nothing here runs a frame; each call a pass over the standing buildings at most.
    /// </summary>
    public sealed partial class LifeDirector
    {
        /// <summary>
        /// A standing building given so many storeys of floor area in all (DistrictBuildings.Extend). False, with why, for no
        /// building standing there, one redevelopment leaves alone, storeys not more than it has or past the most a record takes,
        /// or the district's line unreadable or full. Out of a Going Public session only (the construction game asks so).
        /// </summary>
        public static bool Extend(string lotId, int storeys, out string why)
        {
            MadeBuilding made = StandingMade(lotId);
            int now = made != null && Read(made, out DistrictBuilding b) ? b.Storeys : 0;
            why = Refused(made, lotId) ?? (storeys <= now || storeys > BuildingsRules.StoreysMost
                ? "its storeys " + storeys + " are not between the " + now + " it has and " + BuildingsRules.StoreysMost : null);
            FinishedBuilding f = why == null ? Record(made, out why) : null;
            if (f == null)
            {
                Logger?.Info("Uptown: the building on lot " + (lotId ?? "(none)") + " is not extended: " + why + ".");
                return false;
            }
            int was = made.Building.totalSqm, sqm = Math.Max(1, MadeGame.SquareMetres(made.Spec.Size)) * storeys;
            f.Storeys = storeys;
            made.Building.totalSqm = sqm;   // the floor area now; the copy engine makes the record so from every load (its storeys)
            bool deed = LifeGame.Resize(made, sqm);
            WriteState();
            _stamp++;
            Logger?.Info("Uptown: the building at " + made.Address + " on lot " + lotId + " was extended from " + now + " to " + storeys + " storey(s): " + was + " m2 of floor now "
                         + sqm + (deed ? ", its deed's floor area with it" : ", no deed of the player's to bring up to date") + "; its look stays its own.");
            return true;
        }

        /// <summary>
        /// A standing building made another use (DistrictBuildings.Convert): the game's building type the structure of its size and
        /// version takes. False, with why, for no building standing there, one redevelopment leaves alone, a type its structure does
        /// not take or the one it is already, the player inside it, or the district's line unreadable or full. Out of a Going Public
        /// session only (the construction game asks so); its unit is emptied first, by the construction game's notice.
        /// </summary>
        public static bool Convert(string lotId, string type, out string why)
        {
            MadeBuilding made = StandingMade(lotId);
            why = Refused(made, lotId);
            if (why == null)
            {
                why = string.IsNullOrEmpty(type) || !LifeGame.Takes(made.Spec.Size, made.Spec.Version, type)
                    ? "the game's structure of its size and version takes no " + (type ?? "(none)")
                    : string.Equals(type, made.Building.BuildingType, StringComparison.Ordinal) ? "it is of " + type + " already"
                    : LifeGame.Inside(made) ? "the player is inside it" : null;
            }
            FinishedBuilding f = why == null ? Record(made, out why) : null;
            if (f == null)
            {
                Logger?.Info("Uptown: the building on lot " + (lotId ?? "(none)") + " is not changed in use: " + why + ".");
                return false;
            }
            string was = made.Building.BuildingType;
            f.Type = type;
            // Everything of the game asks the record's type (the businesses that may start there, the map, the rent, the
            // customers); the interior goes by its size and version alone, which stay (Offices/ConversionGame's way).
            made.Building.BuildingType = type;
            Mapped(made, "changed in use");   // the map's filters read the record's type at each look: its marker and the search list follow at once
            WriteState();
            _stamp++;
            DistrictPlaces.Changed();
            Logger?.Info("Uptown: the building at " + made.Address + " on lot " + lotId + " changed in use from " + was + " to " + type + ", now and from every load.");
            return true;
        }

        // The district's building standing on a lot (its own) at this load, or null.
        private static MadeBuilding StandingMade(string lotId)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < Standing.Count; i++)
            {
                if (string.Equals(Standing[i].Spec.Id, lotId, StringComparison.Ordinal))
                {
                    return Standing[i];
                }
            }
            return null;
        }

        // Why a building may not be redeveloped at all; null when it may.
        private static string Refused(MadeBuilding made, string lotId)
        {
            return made == null || made.Building == null ? "no building of the district stands on lot " + (lotId ?? "(none)") + " at this load"
                : KeptFromRedevelopment(made) ? "it is one of the district's own places, never redeveloped"
                : _stateUnread ? "the district's own line in this save cannot be read, and is kept as it is" : null;
        }

        // The district line's record of a standing building, made for one standing from the first day: its structure, its type,
        // the look it wears and its floor area as they are (no storeys of its own for a building of its design's floor area), set
        // up already (its deed or its letting is the game's from its first day), so its next load makes it as it stands.
        private static FinishedBuilding Record(MadeBuilding made, out string why)
        {
            why = null;
            FinishedBuilding f = _state.On(made.Spec.Id);
            if (f != null)
            {
                return f;
            }
            if (_state.Finished.Count >= LifeStore.FinishedMost)
            {
                why = "the district's line holds no more buildings";
                return null;
            }
            GameInstance save = SaveGameManager.Current;
            f = new FinishedBuilding
            {
                Lot = made.Spec.Id, Size = made.Spec.Size, Version = made.Spec.Version, Type = made.Building.BuildingType, Design = made.Design ?? string.Empty,
                Storeys = made.Spec.DesignArea ? 0 : Math.Max(1, made.Spec.Floors), Owner = LifeGame.Deed(made) != null ? FinishOwner.Player : FinishOwner.City,
                Price = 0.0, Day = save != null ? save.Day : 0, Settled = true, Over = null
            };
            _state.Finished.Add(f);
            return f;
        }
    }
}
